@echo off
REM Clean all build artifacts, binaries, logs, and temp files from the project
REM Safe to run before zipping/transferring source code

echo Cleaning Transcription Tools source tree...
echo.

REM .NET build output
if exist "TranscriptionTools\bin" (
    echo Removing TranscriptionTools\bin\
    rmdir /s /q "TranscriptionTools\bin"
)
if exist "TranscriptionTools\obj" (
    echo Removing TranscriptionTools\obj\
    rmdir /s /q "TranscriptionTools\obj"
)

REM Visual Studio
if exist ".vs" (
    echo Removing .vs\
    rmdir /s /q ".vs"
)

REM Installer output
if exist "Output" (
    echo Removing Output\
    rmdir /s /q "Output"
)

REM Whisper binaries (downloaded externally)
if exist "whisper-bin" (
    echo Removing whisper-bin\
    rmdir /s /q "whisper-bin"
)

REM Whisper source (cloned separately)
if exist "whisper.cpp" (
    echo Removing whisper.cpp\
    rmdir /s /q "whisper.cpp"
)

REM Old patches directory
if exist "patches" (
    echo Removing patches\
    rmdir /s /q "patches"
)

REM Log files
if exist "pipeline-debug.log" (
    echo Removing pipeline-debug.log
    del /q "pipeline-debug.log"
)
if exist "nllb-debug.log" (
    echo Removing nllb-debug.log
    del /q "nllb-debug.log"
)
for %%f in (*.log) do (
    echo Removing %%f
    del /q "%%f"
)

REM Temp/scratch files
if exist "UPDATE_NOTES.md" (
    echo Removing UPDATE_NOTES.md
    del /q "UPDATE_NOTES.md"
)
if exist "translation_comparison.html" (
    echo Removing translation_comparison.html
    del /q "translation_comparison.html"
)

REM NuGet packages cache
for %%f in (*.nupkg *.snupkg) do (
    echo Removing %%f
    del /q "%%f"
)

REM User-specific files
for /r . %%f in (*.user *.suo) do (
    echo Removing %%f
    del /q "%%f" 2>nul
)

echo.
echo Done. Source tree is clean.
pause
