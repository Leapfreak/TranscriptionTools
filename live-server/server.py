"""
Live Transcription Server for Transcription Tools.
FastAPI server: sounddevice audio capture -> Silero VAD -> faster-whisper -> SSE events.
"""

import argparse
import asyncio
import json
import logging
import os
import signal
import sys
import threading
import time

import re
import numpy as np
import sounddevice as sd
from difflib import SequenceMatcher
from faster_whisper import WhisperModel
from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
from sse_starlette.sse import EventSourceResponse

# ---------------------------------------------------------------------------
# Logging — single file: pipeline-debug.log in app root (passed via --log-dir)
# ---------------------------------------------------------------------------
logger = logging.getLogger("live-server")
logger.setLevel(logging.DEBUG)

# Suppress all other loggers
logging.basicConfig(level=logging.WARNING)
logging.getLogger("faster_whisper").setLevel(logging.WARNING)
logging.getLogger("ctranslate2").setLevel(logging.WARNING)
logging.getLogger("uvicorn").setLevel(logging.WARNING)
logging.getLogger("uvicorn.access").setLevel(logging.WARNING)


class _SharedFileHandler(logging.Handler):
    """File handler that opens/closes per write so other processes can share the file."""
    def __init__(self, path):
        super().__init__()
        self._path = path

    def emit(self, record):
        try:
            with open(self._path, "a", encoding="utf-8") as f:
                f.write(self.format(record) + "\n")
        except Exception:
            pass


def setup_logging(log_dir: str):
    """Set up file logging to pipeline-debug.log in the given directory."""
    log_path = os.path.join(log_dir, "pipeline-debug.log")
    handler = _SharedFileHandler(log_path)
    handler.setFormatter(logging.Formatter("%(asctime)s [LIVE] %(message)s", datefmt="%Y-%m-%d %H:%M:%S"))
    handler.setLevel(logging.DEBUG)
    logger.addHandler(handler)
    logger.debug("Live server starting")


app = FastAPI()

# ---------------------------------------------------------------------------
# Global state
# ---------------------------------------------------------------------------
model: WhisperModel | None = None
model_path_global: str = ""
compute_type_global: str = "int8_float16"
device_global: str = "cuda"

capturing: bool = False
capture_thread: threading.Thread | None = None
stop_event: threading.Event = threading.Event()

# SSE subscribers (asyncio queues)
subscribers: list[asyncio.Queue] = []
subscribers_lock: threading.Lock = threading.Lock()

# Audio config
SAMPLE_RATE = 16000

# Capture config (set via /start)
current_config: dict = {}

# Uvicorn server reference for graceful shutdown
_server = None


# ---------------------------------------------------------------------------
# SSE helpers
# ---------------------------------------------------------------------------
def broadcast_event(event_type: str, text: str, lang: str = ""):
    """Send an SSE event to all connected subscribers."""
    payload = {"text": text}
    if lang:
        payload["lang"] = lang
    data = json.dumps(payload)
    with subscribers_lock:
        for q in subscribers:
            try:
                q.put_nowait((event_type, data))
            except asyncio.QueueFull:
                pass


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def _strip_boundary_overlap(new_text: str, prev_text: str, max_overlap_words: int = 4) -> str:
    """Remove overlapping words from the start of new_text that match the end of prev_text."""
    if not prev_text or not new_text:
        return new_text
    prev_words = prev_text.lower().split()
    new_words = new_text.split()
    if not prev_words or not new_words:
        return new_text
    # Check if 1-4 words at the start of new_text match the end of prev_text
    for n in range(min(max_overlap_words, len(prev_words), len(new_words)), 0, -1):
        prev_tail = [re.sub(r"[^\w]", "", w) for w in prev_words[-n:]]
        new_head = [re.sub(r"[^\w]", "", w.lower()) for w in new_words[:n]]
        if prev_tail == new_head:
            stripped = " ".join(new_words[n:])
            if stripped:
                logger.debug(f"  BOUNDARY-DEDUP: stripped {n} overlapping words: {' '.join(new_words[:n])}")
                return stripped
    return new_text


def _find_sentence_boundary_word(words, min_time: float, audio_end: float) -> tuple:
    """Find the last word that ends a sentence (after min_time seconds).
    Only returns a boundary if it's within 2s of the audio end — meaning
    the speaker has actually paused/stopped after the sentence, not just
    a period mid-speech.
    Returns (text_up_to_boundary, audio_end_time) or (None, None)."""
    if not words:
        return None, None

    last_boundary_idx = -1
    for i, w in enumerate(words):
        word_stripped = w.word.rstrip()
        # Must end with sentence punctuation but NOT ellipsis (...)
        if w.end >= min_time and word_stripped[-1:] in ".!?;" and not word_stripped.endswith("..."):
            last_boundary_idx = i

    if last_boundary_idx < 0:
        return None, None

    boundary_time = words[last_boundary_idx].end
    # Only commit if the boundary is near the end of current audio
    # (within 2s means no significant speech follows the period)
    if audio_end - boundary_time > 2.0:
        return None, None

    text = "".join(w.word for w in words[:last_boundary_idx + 1]).strip()
    # Minimum length to avoid committing garbage fragments like "20 for every one."
    if len(text) < 30:
        return None, None
    return text, boundary_time


def _is_hallucination(segments, last_commit_text: str = "") -> bool:
    """Detect likely hallucinations using segment metadata.
    High no_speech_prob or very low avg_logprob on short segments = hallucination.
    Also detects repetition of previously committed text and self-repetition."""
    if not segments:
        return True
    total_speech_dur = sum(seg.end - seg.start for seg in segments)
    avg_no_speech = sum(seg.no_speech_prob for seg in segments) / len(segments)
    avg_logprob = sum(seg.avg_logprob for seg in segments) / len(segments)

    # Very short audio (< 1.5s) with very high no-speech probability
    if total_speech_dur < 1.5 and avg_no_speech > 0.8:
        return True
    # Low confidence on very short audio (likely hallucinated filler)
    if avg_logprob < -0.8 and total_speech_dur < 1.0:
        return True

    full_text = " ".join(seg.text.strip() for seg in segments if seg.text.strip())

    # Self-repetition: only catch near-exact looping (>90% similar halves).
    # Parallel structures ("not X, but Y") typically score 60-80% — must not filter those.
    words = [re.sub(r"[^\w]", "", w) for w in full_text.lower().split()]
    words = [w for w in words if w]
    if len(words) >= 8:
        mid = len(words) // 2
        first_half = " ".join(words[:mid])
        second_half = " ".join(words[mid:])
        half_ratio = SequenceMatcher(None, first_half, second_half).ratio()
        if half_ratio > 0.9:
            logger.debug(f"  SELF-REPETITION detected: halves {half_ratio:.0%} similar")
            return True

    # Repetition of previous commit
    if last_commit_text and len(last_commit_text) > 20:
        # Normalize: lowercase, strip ALL punctuation and leading numbers, compare words only
        norm_new = " ".join(re.sub(r"[^\w\s]", "", full_text.lower()).split())
        norm_prev = " ".join(re.sub(r"[^\w\s]", "", last_commit_text.lower()).split())
        # Strip leading numbers from both
        norm_new = re.sub(r"^\d+\s*", "", norm_new)
        norm_prev = re.sub(r"^\d+\s*", "", norm_prev)
        # Use sequence matching for contiguous similarity
        ratio = SequenceMatcher(None, norm_new, norm_prev).ratio()
        # Only flag very high similarity (>85%) — obvious exact repeats
        if ratio > 0.85:
            logger.debug(f"  REPETITION detected (similarity={ratio:.0%}, {total_speech_dur:.1f}s)")
            return True

    return False


# ---------------------------------------------------------------------------
# Audio capture + transcription thread
# ---------------------------------------------------------------------------
def capture_and_transcribe():
    """Main capture loop running in a background thread."""
    global capturing, model

    cfg = current_config
    device_index = cfg.get("device_index", None)
    language = cfg.get("language", None)
    translate = cfg.get("translate", False)
    initial_prompt = cfg.get("initial_prompt", "")
    beam_size = cfg.get("beam_size", 5)
    # These are read from current_config each iteration (live-adjustable via /config)
    interim_interval_ms = cfg.get("interim_interval_ms", 1000)

    # Audio buffer
    audio_buffer = []
    buffer_lock = threading.Lock()

    def audio_callback(indata, frames, time_info, status):
        if status:
            logger.warning(f"Audio status: {status}")
        with buffer_lock:
            audio_buffer.append(indata[:, 0].copy())

    # Treat "auto" as None for faster-whisper
    if language == "auto":
        language = None

    task = "translate" if translate else "transcribe"

    try:
        stream = sd.InputStream(
            samplerate=SAMPLE_RATE,
            channels=1,
            dtype="float32",
            device=device_index,
            callback=audio_callback,
            blocksize=int(SAMPLE_RATE * 0.1),  # 100ms blocks
        )
        stream.start()
        logger.debug(f"CAPTURE START device={device_index} lang={language} task={task} beam={beam_size} vad_silence={cfg.get('vad_min_silence_ms', 300)}ms max_seg={cfg.get('vad_max_segment_s', 30)}s interim={interim_interval_ms}ms")
    except Exception as e:
        logger.error(f"Failed to open audio stream: {e}")
        capturing = False
        broadcast_event("error", f"Audio device error: {e}")
        return

    last_interim_time = time.time()
    last_committed_pos = 0  # samples already committed
    last_commit_text = ""  # track previous commit for repetition detection only

    try:
        while not stop_event.is_set():
            # Read live-adjustable config each iteration
            vad_min_silence_ms = current_config.get("vad_min_silence_ms", 300)
            vad_max_segment_s = current_config.get("vad_max_segment_s", 30)
            interim_interval_ms = current_config.get("interim_interval_ms", 1000)

            # Sleep for the interim interval before checking
            time.sleep(interim_interval_ms / 1000.0)

            # Get current audio
            with buffer_lock:
                if not audio_buffer:
                    continue
                current_audio = np.concatenate(audio_buffer)

            total_samples = len(current_audio)
            uncommitted_samples = total_samples - last_committed_pos
            uncommitted_duration = uncommitted_samples / SAMPLE_RATE

            # Only process if we have at least 1s of new audio
            if uncommitted_duration < 1.0:
                continue

            now = time.time()

            audio_to_process = current_audio[last_committed_pos:]

            try:
                segments_iter, info = model.transcribe(
                    audio_to_process,
                    language=language,
                    task=task,
                    beam_size=beam_size,
                    initial_prompt=initial_prompt or None,
                    vad_filter=True,
                    vad_parameters={
                        "threshold": 0.3,
                        "min_silence_duration_ms": vad_min_silence_ms,
                        "max_speech_duration_s": vad_max_segment_s,
                        "speech_pad_ms": 100,
                    },
                    word_timestamps=True,
                    no_repeat_ngram_size=3,
                    repetition_penalty=1.1,
                )

                segments = list(segments_iter)
            except Exception as e:
                logger.error(f"Transcription error: {e}")
                continue

            if not segments:
                logger.debug(f"VAD: no speech in {uncommitted_duration:.1f}s of audio — discarding")
                with buffer_lock:
                    current_audio_full = np.concatenate(audio_buffer)
                    keep_samples = int(0.5 * SAMPLE_RATE)
                    remaining = current_audio_full[-keep_samples:] if len(current_audio_full) > keep_samples else current_audio_full
                    audio_buffer.clear()
                    audio_buffer.append(remaining)
                    last_committed_pos = 0
                continue

            # Gather all words across segments
            all_words = []
            for seg in segments:
                if seg.words:
                    all_words.extend(seg.words)

            # Full text and timing
            audio_duration = len(audio_to_process) / SAMPLE_RATE
            last_seg_end = segments[-1].end if segments else 0
            silence_at_end = audio_duration - last_seg_end
            all_final = silence_at_end >= (vad_min_silence_ms / 1000.0)

            full_text = " ".join(seg.text.strip() for seg in segments if seg.text.strip())

            logger.debug(f"TRANSCRIBE dur={audio_duration:.1f}s segs={len(segments)} last_end={last_seg_end:.1f}s silence_tail={silence_at_end:.2f}s final={all_final} words={len(all_words)}")
            for seg in segments:
                logger.debug(f"  [{seg.start:.1f}-{seg.end:.1f}] no_speech={seg.no_speech_prob:.2f} logprob={seg.avg_logprob:.2f} | {seg.text.strip()}")

            detected_lang = info.language if info else ""

            if all_final:
                # VAD detected end of speech — commit everything and cut audio
                if full_text and not _is_hallucination(segments, last_commit_text):
                    full_text = _strip_boundary_overlap(full_text, last_commit_text)
                    broadcast_event("commit", full_text, lang=detected_lang)
                    logger.debug(f">>> COMMIT [{detected_lang}]: {full_text}")
                    last_commit_text = full_text[-200:]
                elif full_text:
                    logger.debug(f">>> HALLUCINATION SKIPPED (no_speech={sum(s.no_speech_prob for s in segments)/len(segments):.2f} logprob={sum(s.avg_logprob for s in segments)/len(segments):.2f}): {full_text}")

                # Cut audio buffer
                committed_end = last_committed_pos + int(last_seg_end * SAMPLE_RATE)
                with buffer_lock:
                    current_audio_full = np.concatenate(audio_buffer)
                    remaining = current_audio_full[committed_end:]
                    audio_buffer.clear()
                    if len(remaining) > 0:
                        audio_buffer.append(remaining)
                    last_committed_pos = 0
                last_interim_time = now

            elif uncommitted_duration >= 6.0:
                # Try to find a sentence boundary to commit at using word timestamps
                # Look for sentence-ending punctuation with audio time > 5s
                boundary_text, boundary_time = _find_sentence_boundary_word(all_words, 5.0, audio_duration)

                if boundary_text and boundary_time:
                    # Check for hallucination before committing
                    if _is_hallucination(segments, last_commit_text):
                        logger.debug(f">>> SENTENCE-COMMIT BLOCKED (hallucination): {boundary_text}")
                        # Discard the bad audio and reset
                        with buffer_lock:
                            current_audio_full = np.concatenate(audio_buffer)
                            cut_pos = last_committed_pos + int(boundary_time * SAMPLE_RATE)
                            remaining = current_audio_full[cut_pos:]
                            audio_buffer.clear()
                            if len(remaining) > 0:
                                audio_buffer.append(remaining)
                            last_committed_pos = 0
                        last_interim_time = now
                    else:
                        boundary_text = _strip_boundary_overlap(boundary_text, last_commit_text)
                        broadcast_event("commit", boundary_text, lang=detected_lang)
                        logger.debug(f">>> SENTENCE-COMMIT [{detected_lang}] @{boundary_time:.1f}s ({uncommitted_duration:.1f}s): {boundary_text}")
                        last_commit_text = boundary_text[-200:]

                        # Cut audio exactly at sentence boundary (word timestamps are precise)
                        cut_pos = last_committed_pos + int(boundary_time * SAMPLE_RATE)
                        with buffer_lock:
                            current_audio_full = np.concatenate(audio_buffer)
                            remaining = current_audio_full[cut_pos:]
                            audio_buffer.clear()
                            if len(remaining) > 0:
                                audio_buffer.append(remaining)
                            last_committed_pos = 0
                    last_interim_time = now
                else:
                    # No sentence boundary — emit update
                    if full_text:
                        broadcast_event("update", full_text)
                        logger.debug(f">>> UPDATE: {full_text}")
                    last_interim_time = now

            else:
                # Short audio — just emit update (skip hallucinations)
                if full_text and not _is_hallucination(segments, last_commit_text):
                    broadcast_event("update", full_text)
                    logger.debug(f">>> UPDATE: {full_text}")
                elif full_text:
                    logger.debug(f">>> HALLUCINATION SKIPPED (no_speech={sum(s.no_speech_prob for s in segments)/len(segments):.2f} logprob={sum(s.avg_logprob for s in segments)/len(segments):.2f}): {full_text}")
                last_interim_time = now

            # Force-commit if exceeded max segment duration
            if uncommitted_duration >= vad_max_segment_s and not all_final:
                if full_text:
                    full_text = _strip_boundary_overlap(full_text, last_commit_text)
                    broadcast_event("commit", full_text, lang=detected_lang)
                    logger.debug(f">>> FORCE-COMMIT [{detected_lang}] ({uncommitted_duration:.1f}s): {full_text}")
                    last_commit_text = full_text[-200:]

                with buffer_lock:
                    current_audio_full = np.concatenate(audio_buffer) if audio_buffer else np.array([], dtype=np.float32)
                    committed_end = int(last_seg_end * SAMPLE_RATE) + last_committed_pos
                    remaining = current_audio_full[committed_end:]
                    audio_buffer.clear()
                    if len(remaining) > 0:
                        audio_buffer.append(remaining)
                    last_committed_pos = 0
                last_interim_time = now

    except Exception as e:
        logger.error(f"Capture loop error: {e}")
    finally:
        stream.stop()
        stream.close()
        capturing = False
        logger.debug("CAPTURE STOP")


# ---------------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------------
@app.get("/health")
async def health():
    return {
        "status": "ok",
        "model_loaded": model is not None,
        "capturing": capturing,
    }


@app.get("/devices")
async def get_devices():
    devices = sd.query_devices()
    # Filter to host API of the default input device (avoids duplicates from MME/DirectSound/WASAPI)
    try:
        default_input = sd.query_devices(sd.default.device[0])
        default_api = default_input["hostapi"]
    except Exception:
        default_api = None
    result = []
    for i, d in enumerate(devices):
        if d["max_input_channels"] > 0:
            if default_api is None or d["hostapi"] == default_api:
                result.append({"id": i, "name": d["name"]})
    return {"devices": result}


@app.post("/start")
async def start_capture(request: Request):
    global capturing, capture_thread, model, current_config
    global model_path_global, compute_type_global, device_global

    if capturing:
        return JSONResponse({"status": "already_capturing"}, status_code=409)

    body = await request.json()
    current_config = body

    # Load model if needed
    requested_model_path = body.get("model_path", model_path_global)
    requested_compute_type = body.get("compute_type", compute_type_global)
    requested_device = body.get("device", device_global)

    if model is None or requested_model_path != model_path_global:
        logger.debug(f"MODEL LOAD path={requested_model_path} device={requested_device} compute={requested_compute_type}")
        try:
            model = WhisperModel(
                requested_model_path,
                device=requested_device,
                compute_type=requested_compute_type,
            )
            model_path_global = requested_model_path
            compute_type_global = requested_compute_type
            device_global = requested_device
            logger.debug("MODEL LOAD OK")
        except Exception as e:
            logger.error(f"Failed to load model: {e}")
            return JSONResponse({"status": "error", "detail": str(e)}, status_code=500)

    # Start capture
    stop_event.clear()
    capturing = True
    capture_thread = threading.Thread(target=capture_and_transcribe, daemon=True)
    capture_thread.start()

    return {"status": "started"}


@app.post("/stop")
async def stop_capture():
    global capturing
    if not capturing:
        return {"status": "not_capturing"}

    stop_event.set()
    if capture_thread is not None:
        capture_thread.join(timeout=5)
    capturing = False
    logger.debug("Capture stopped via /stop")
    return {"status": "stopped"}


@app.post("/config")
async def update_config(request: Request):
    """Update live-adjustable config values without restarting capture."""
    body = await request.json()
    updated = []
    for key in ("vad_min_silence_ms", "vad_max_segment_s", "interim_interval_ms"):
        if key in body:
            current_config[key] = body[key]
            updated.append(key)
    logger.debug(f"CONFIG UPDATE: {', '.join(f'{k}={current_config[k]}' for k in updated)}")
    return {"status": "ok", "updated": updated}


@app.post("/shutdown")
async def shutdown():
    """Gracefully shut down the server."""
    global capturing
    logger.debug("Shutdown requested")

    # Stop capture if running
    if capturing:
        stop_event.set()
        if capture_thread is not None:
            capture_thread.join(timeout=3)
        capturing = False

    # Schedule server shutdown
    if _server is not None:
        _server.should_exit = True

    return {"status": "shutting_down"}


@app.get("/stream")
async def stream_events(request: Request):
    """SSE endpoint. Sends update and commit events."""
    q: asyncio.Queue = asyncio.Queue(maxsize=100)
    with subscribers_lock:
        subscribers.append(q)

    async def event_generator():
        try:
            while True:
                if await request.is_disconnected():
                    break
                try:
                    event_type, data = await asyncio.wait_for(q.get(), timeout=15.0)
                    yield {"event": event_type, "data": data}
                except asyncio.TimeoutError:
                    # Send keepalive comment
                    yield {"comment": "keepalive"}
        finally:
            with subscribers_lock:
                if q in subscribers:
                    subscribers.remove(q)

    return EventSourceResponse(event_generator())


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Live transcription server")
    parser.add_argument("--port", type=int, default=5091)
    parser.add_argument("--host", type=str, default="127.0.0.1")
    parser.add_argument("--log-dir", type=str, default=".")
    args = parser.parse_args()

    setup_logging(args.log_dir)

    import uvicorn

    config = uvicorn.Config(app, host=args.host, port=args.port, log_level="warning")
    _server = uvicorn.Server(config)
    _server.run()
