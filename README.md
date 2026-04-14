# Transcription Tools

A Windows desktop application for generating subtitles from audio and video sources. Provides a graphical interface for [whisper.cpp](https://github.com/ggerganov/whisper.cpp) speech-to-text with support for batch processing and real-time live transcription.

## Download

Download the latest version from the [Releases](https://github.com/Leapfreak/TranscriptionTools/releases) page:

- **TranscriptionTools_Setup_x.x.x.exe** (recommended) -- Installer with Start Menu shortcuts and uninstaller
- **TranscriptionTools_vx.x.x.zip** -- Portable version, extract and run

On first launch, the app will prompt you to download the required tools (whisper.cpp, yt-dlp, FFmpeg, Whisper model, and SubtitleEdit). This is a one-time setup that downloads everything automatically.

> **Note:** Windows SmartScreen may show a "Windows protected your PC" warning because the installer is not code-signed. Click "More info" then "Run anyway" to proceed. The source code is fully open for inspection.

## Features

**Batch Processing Modes**
- **YouTube -> Subtitles** -- Download a YouTube video (or use a local file), optionally trim to a time range, and generate subtitles
- **Audio File -> Subtitles** -- Transcribe audio files (OGG, MP3, WAV, FLAC, M4A, etc.) directly into subtitles
- **YouTube -> Full Video** -- Download and trim a video without transcription
- **YouTube -> Audio Only** -- Download, trim, and extract audio to MP3

**Live Mode**
- Real-time speech-to-text from a microphone or audio input device using whisper-stream (experimental)

**Other**
- Parallel chunk-based processing with resume support for interrupted jobs
- Multiple output formats: SRT, VTT, TXT, JSON, CSV, LRC
- Built-in subtitle server for live caption overlay
- Configurable whisper parameters (beam size, temperature, VAD, threading, etc.)
- Multi-language UI: English, Spanish, French, German, Catalan, Portuguese, Chinese (Simplified), Japanese
- Light/Dark/System theme support
- Automatic app update checking via GitHub Releases
- Automatic tool update checking and downloading

## Requirements

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (the installer checks for this)

All other dependencies are downloaded automatically on first launch:
- [whisper.cpp](https://github.com/ggerganov/whisper.cpp) (speech-to-text engine)
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) (YouTube downloads)
- [FFmpeg](https://ffmpeg.org/) (audio/video processing)
- [SubtitleEdit](https://github.com/SubtitleEdit/subtitleedit) (subtitle editing)
- Whisper GGML model (ggml-large-v3, ~3GB)

## Build from Source

```bash
git clone https://github.com/Leapfreak/TranscriptionTools.git
cd TranscriptionTools
dotnet build
```

To publish a release build:

```bash
dotnet publish TranscriptionTools/TranscriptionTools.vbproj -c Release -o TranscriptionTools/bin/Publish
```

To build the installer (requires [Inno Setup 6](https://jrsoftware.org/isinfo.php)):

```bash
iscc setup.iss
```

## Dependencies

This application calls the following tools as external processes:

| Tool | License | Purpose |
|------|---------|---------|
| [whisper.cpp](https://github.com/ggerganov/whisper.cpp) | MIT | Speech-to-text engine |
| [yt-dlp](https://github.com/yt-dlp/yt-dlp) | Unlicense | YouTube video downloading |
| [FFmpeg](https://ffmpeg.org/) | LGPL/GPL | Audio/video processing |
| [SubtitleEdit](https://github.com/SubtitleEdit/subtitleedit) | GPL-3.0 | Subtitle editing (optional) |

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
