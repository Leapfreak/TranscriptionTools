# Update Notes — v1.1.0

## New Live Transcription Engine

The live transcription engine has been completely rebuilt. The old C++ audio processor (whisper-stream) used fixed time windows to chop audio into blocks, which often cut sentences in half. It has been replaced with a Python-based engine using **faster-whisper** and **Silero VAD** (Voice Activity Detection). Instead of splitting audio at arbitrary time boundaries, the system now listens for natural pauses in speech and only commits text when a complete thought has been spoken. This produces cleaner, more complete sentences — and because translations now receive full phrases rather than fragments, translation quality is significantly better.

The new engine also includes hallucination detection (filtering out phantom text like "Thank you for watching" that whisper sometimes generates over silence), self-repetition detection, and smarter handling of long continuous speech without pauses.

## 85 Translation Languages

Translation support has been expanded from 16 to **85 languages** using the NLLB-200 model. The full list:

Afrikaans, Amharic, Arabic, Armenian, Azerbaijani, Basque, Belarusian, Bengali, Bosnian, Bulgarian, Catalan, Czech, Chinese, Croatian, Danish, Dutch, English, Estonian, Finnish, French, Galician, Georgian, German, Greek, Gujarati, Haitian Creole, Hausa, Hebrew, Hindi, Hungarian, Icelandic, Indonesian, Italian, Japanese, Javanese, Kannada, Kazakh, Khmer, Korean, Lao, Latvian, Lithuanian, Luxembourgish, Macedonian, Malay, Malayalam, Maltese, Maori, Marathi, Mongolian, Myanmar, Nepali, Norwegian, Persian, Polish, Portuguese, Punjabi, Romanian, Russian, Serbian, Shona, Sindhi, Sinhala, Slovak, Slovenian, Somali, Spanish, Sundanese, Swahili, Swedish, Tagalog/Filipino, Tajik, Tamil, Tatar, Telugu, Thai, Turkish, Turkmen, Ukrainian, Urdu, Uzbek, Vietnamese, Welsh, Yoruba, Zulu.

Each viewer can independently choose their own translation language from the subtitle client — no app restart required.

## Unified First-Run Setup

All dependencies (whisper binaries, Python runtime, pip packages, faster-whisper model, NLLB translation model) are now downloaded in a single unified setup flow on first launch. The separate "Setup Translation" button on the Subtitle Server tab has been replaced with a "Check Dependencies" button that re-runs the same unified check.

## Other Improvements

- **Auto language detection**: The system can automatically detect which language is being spoken in real time, so if a speaker switches languages mid-session, each line is correctly identified and translated from the right source language.
- **Main/Job tab log**: The whisper process output log (previously on a separate Log tab) is now displayed at the bottom of the Main/Job tab, resizing to fill available space.
- **Help files rewritten**: All 8 help files (English + 7 translations) have been rewritten from scratch to match the current UI layout and feature set.
- **Clean shutdown**: Python processes (live-server, nllb-server) are now reliably terminated when the app exits, even if the server wasn't stopped first.
- **Subtitle viewer improvements**: Menus close properly when tapping buttons or tapping outside, and switching your translation language no longer requires refreshing the page.

## Technology Stack

| Component | Technology | Company/Organization |
|-----------|-----------|---------------------|
| Live transcription model | Whisper (large-v3) | OpenAI |
| Live transcription runtime | faster-whisper / CTranslate2 | SYSTRAN |
| Voice Activity Detection | Silero VAD | Silero AI |
| Translation model | NLLB-200 (No Language Left Behind) | Meta AI |
| Translation runtime | CTranslate2 | SYSTRAN |
| Tokenizer (translation) | SentencePiece | Google |
| GPU inference | CUDA / cuBLAS | NVIDIA |
| Python web framework | FastAPI / Uvicorn | Sebastián Ramírez (open-source) |
| Audio capture | sounddevice (PortAudio binding) | PortAudio (open-source) |
| Job tab transcription | whisper.cpp / whisper-cli | Georgi Gerganov (open-source) |
| App framework | .NET 8 / WinForms | Microsoft |
| Python runtime | Python 3.12 embedded | Python Software Foundation |
| Installer | Inno Setup | Jordan Russell (open-source) |
| CI/CD | GitHub Actions | Microsoft (GitHub) |
