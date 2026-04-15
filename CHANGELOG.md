# Changelog

## v1.0.6 - 2026-04-16

### Added
- First-run setup wizard: asks about Windows startup and firewall preferences once, then remembers
- Localhost fallback for subtitle server when remote binding fails (non-admin friendly)

### Fixed
- Subtitle server now respects firewall preference (allowRemote parameter passed correctly)
- Cleaned up subtitle server Start() flow to prevent double-starting AcceptLoop
- Output language defaults to English instead of auto

## v1.0.5 - 2026-04-16

### Fixed
- Bundle CUDA runtime DLLs (cublas64_12, cublasLt64_12, cudart64_12) with installer so users don't need CUDA toolkit installed

## v1.0.4 - 2026-04-16

### Added
- System tray support — closing the window minimizes to tray, right-click to exit
- Auto-start with Windows (registry startup entry)
- Subtitle server auto-starts on program launch
- Font family, size, and bold controls for live transcription output (server side)
- Client-side font, bold, color, and size controls in browser subtitle overlay
- Automatic Windows Firewall rule for subtitle server port (with UAC elevation)
- Auto-update: downloads and runs installer automatically, then exits
- Font settings saved to config (SubtitleFontFamily, SubtitleFontSize, SubtitleFontBold)

### Changed
- Program launches maximized (full screen)
- Tab order: Live, Server, Job (Main tab moved after Server tab)
- Settings now save when minimizing to tray (not just on exit)
- Removed hardcoded default prompt — initial prompt now blank by default

### Fixed
- Relative paths not resolved to absolute before use in process launching and File.Exists checks
- Added AppConfig.ResolvePath() and applied across all path consumers (PipelineRunner, FormMain, LiveStreamRunner, TranscriptionCommandBuilder, DependencyManager)
- Subtitle server failed to auto-start during Form_Load (moved to Shown event)
- Font controls crashed during InitializeComponent (null check guard added)

### Removed
- Unit test project (TranscriptionTools.Tests)

## v1.0.3 - 2026-04-15

### Added
- Custom whisper-stream build with `--prompt` support for live transcription
- `--no-carry-prompt` flag to control prompt persistence across decode windows
- Initial prompt now passed to live stream mode from config
- CI/CD builds whisper.cpp from source with CUDA support (with build caching)

### Changed
- Whisper binaries now built from source instead of downloaded from upstream releases
- Whisper.cpp no longer managed by Dependency Manager (bundled with installer)

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
