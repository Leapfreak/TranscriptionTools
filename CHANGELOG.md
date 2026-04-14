# Changelog

## v1.0.2 - 2026-04-14

### Added
- Open-source release on GitHub
- Automatic update checker on startup (checks GitHub Releases)
- Built-in dependency manager — downloads and updates tools on first launch
  - yt-dlp, FFmpeg, whisper.cpp, Whisper model (ggml-large-v3), SubtitleEdit
- "Check for Tool Updates" button on Settings tab
- Lightweight Inno Setup installer (~2MB)
- Version tracking for downloaded tools (tool-versions.json)
- Subtitle server remote control admin panel (Start/Stop/Restart/Simulate via web)
- Configurable subtitle foreground and background colors (saved to config)
- Whisper initial prompt / context field with default sermon transcription text
- Live output textbox colors synced with subtitle server colors
- Remember last used audio device across sessions
- Full localization of all UI controls and MessageBox strings across all 8 languages
- MIT license

### Fixed
- Tool version detection no longer falsely prompts for updates (FFmpeg rolling releases, missing saved versions)
- Initialization race condition where dropdown population overwrote config on startup
- Theme system no longer overrides color picker buttons or live output colors
- Color persistence now uses hex format instead of named system colors

### Features (existing)
- YouTube video download and transcription via whisper.cpp
- Audio file transcription (OGG, MP3, WAV, FLAC, M4A, etc.)
- Video download and audio extraction modes
- Live real-time transcription (experimental)
- Parallel chunk processing with resume support
- Multiple output formats: SRT, VTT, TXT, JSON, CSV, LRC
- Multi-language UI (English, Spanish, French, German, Catalan, Portuguese, Chinese, Japanese)
- Light/Dark/System theme support
- Configurable whisper parameters
- Built-in subtitle server for live caption overlay
