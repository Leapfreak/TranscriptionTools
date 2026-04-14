Imports System.IO
Imports System.Threading
Imports TranscriptionTools.Models

Namespace Pipeline
    Public Class PipelineRunner

        Private ReadOnly _config As AppConfig
        Private ReadOnly _progress As IProgress(Of PipelineProgress)
        Private ReadOnly _ct As CancellationToken

        Public Event LogMessage As EventHandler(Of LogEntry)

        Public Sub New(config As AppConfig, progress As IProgress(Of PipelineProgress), ct As CancellationToken)
            _config = config
            _progress = progress
            _ct = ct
        End Sub

        Private Sub Log(message As String, Optional level As LogLevel = LogLevel.Info)
            RaiseEvent LogMessage(Me, New LogEntry With {.Message = message, .Level = level})
        End Sub

        Private _stepCount As Integer = 8

        Private Sub Report(stepIndex As Integer, status As String, Optional chunkDone As Integer = 0, Optional chunkTotal As Integer = 0)
            _progress.Report(New PipelineProgress With {
                .StepIndex = stepIndex,
                .StepCount = _stepCount,
                .StatusMessage = status,
                .ChunkDone = chunkDone,
                .ChunkTotal = chunkTotal
            })
        End Sub

        Public Async Function RunAsync(url As String, startTime As String, endTime As String,
                                        outputDir As String, Optional resumeMode As Boolean = False) As Task
            _stepCount = 8 ' Steps 0-7
            ' Step 0: Validate
            Report(0, "Validating inputs...")
            Log("=== Step 0: Validating inputs ===")
            If Not resumeMode Then
                ValidateInputs(url, outputDir)
            Else
                Log("RESUME MODE - skipping completed steps.", LogLevel.Success)
                ValidateInputsForResume(outputDir)
            End If

            If Not Directory.Exists(outputDir) Then
                Directory.CreateDirectory(outputDir)
            End If

            Dim isLocalFile = File.Exists(url)
            Dim fullVideoPath = Path.Combine(outputDir, "yt_video_full.mp4")
            Dim previewPath = Path.Combine(outputDir, "preview.mp4")
            Dim audioPath = Path.Combine(outputDir, "yt_audio.wav")

            ' Step 1: Download
            _ct.ThrowIfCancellationRequested()
            Report(1, "Downloading video...")
            Log("=== Step 1: Downloading video ===")

            If File.Exists(fullVideoPath) Then
                Log("SKIP - yt_video_full.mp4 already exists.", LogLevel.Success)
            ElseIf resumeMode Then
                Throw New PipelineException("Err_DownloadFailed", "Cannot resume: yt_video_full.mp4 not found in output folder")
            ElseIf isLocalFile Then
                Log("Local file provided, copying...")
                File.Copy(url, fullVideoPath, True)
            Else
                Await RunProcessAsync(_config.PathYtdlp,
                    $"-f ""{_config.YtdlpFormat}"" ""{url}"" -o ""{fullVideoPath}""",
                    outputDir, "Err_DownloadFailed", "yt-dlp failed")
            End If

            If Not File.Exists(fullVideoPath) Then
                Throw New PipelineException("Err_DownloadFailed", "yt_video_full.mp4 was not created")
            End If
            Log("Download OK.", LogLevel.Success)

            ' Step 2: Trim video
            _ct.ThrowIfCancellationRequested()
            Report(2, "Trimming video...")
            Log("=== Step 2: Trimming video ===")

            If File.Exists(previewPath) Then
                Log("SKIP - preview.mp4 already exists.", LogLevel.Success)
            Else
                Dim trimArgs = $"-y -i ""{fullVideoPath}"""
                If Not String.IsNullOrWhiteSpace(startTime) AndAlso TimeToSec(startTime) > 0 Then trimArgs &= $" -ss {startTime}"
                If Not String.IsNullOrWhiteSpace(endTime) AndAlso TimeToSec(endTime) > 0 Then trimArgs &= $" -to {endTime}"
                trimArgs &= $" -c:v copy -c:a copy ""{previewPath}"""

                Await RunProcessAsync(_config.PathFfmpeg, trimArgs, outputDir, "Err_TrimFailed", "ffmpeg trim failed")
            End If

            If Not File.Exists(previewPath) Then
                Throw New PipelineException("Err_TrimFailed", "preview.mp4 was not created")
            End If
            Log("Trim OK.", LogLevel.Success)

            ' Step 3: Extract audio
            _ct.ThrowIfCancellationRequested()
            Report(3, "Extracting audio...")
            Log("=== Step 3: Extracting audio ===")

            If File.Exists(audioPath) Then
                Log("SKIP - yt_audio.wav already exists.", LogLevel.Success)
            Else
                Await RunProcessAsync(_config.PathFfmpeg,
                    $"-y -i ""{previewPath}"" -ac 1 -ar 16000 -c:a pcm_s16le ""{audioPath}""",
                    outputDir, "Err_AudioExtractFailed", "ffmpeg audio extraction failed")
            End If

            If Not File.Exists(audioPath) Then
                Throw New PipelineException("Err_AudioExtractFailed", "yt_audio.wav was not created")
            End If
            Log("Audio extraction OK.", LogLevel.Success)

            ' Step 4: Get duration
            _ct.ThrowIfCancellationRequested()
            Report(4, "Getting duration...")
            Log("=== Step 4: Getting duration ===")

            Dim durSec = Await GetDurationAsync(audioPath, outputDir)
            If durSec <= 0 Then
                Throw New PipelineException("Err_DurationFailed", "Could not determine audio duration")
            End If
            Log($"Duration: {Math.Round(durSec)} seconds", LogLevel.Info)

            ' Step 5: Split into chunks
            _ct.ThrowIfCancellationRequested()
            Report(5, "Splitting audio into chunks...")
            Log("=== Step 5: Splitting into chunks ===")

            Dim chunkSec = _config.ChunkSizeSec
            Dim numChunks = CInt(Math.Ceiling(durSec / chunkSec))
            Dim chunkPaths As New List(Of String)
            Dim chunkStarts As New List(Of Double)

            For i = 0 To numChunks - 1
                _ct.ThrowIfCancellationRequested()
                Dim chunkStart = i * chunkSec
                Dim idx = i.ToString("D3")
                Dim outChunk = Path.Combine(outputDir, $"chunk_{idx}.wav")

                chunkPaths.Add(outChunk)
                chunkStarts.Add(chunkStart)

                If File.Exists(outChunk) Then
                    Log($"SKIP - chunk_{idx}.wav already exists.", LogLevel.Success)
                Else
                    Log($"Creating chunk_{idx}.wav (start={chunkStart}s)")
                    Await RunProcessAsync(_config.PathFfmpeg,
                        $"-y -ss {chunkStart} -i ""{audioPath}"" -t {chunkSec} -ac 1 -ar 16000 -c:a pcm_s16le ""{outChunk}""",
                        outputDir, "Err_ChunkFailed", $"ffmpeg chunking failed on chunk {i}")
                End If

                If Not File.Exists(outChunk) Then
                    Throw New PipelineException("Err_ChunkFailed", $"chunk_{idx}.wav was not created")
                End If
            Next
            Log($"Chunking OK. {numChunks} chunks created.", LogLevel.Success)

            ' Step 6: Transcribe chunks in parallel batches
            _ct.ThrowIfCancellationRequested()
            Report(6, "Transcribing...")
            Log($"=== Step 6: Transcribing ({_config.ParallelJobs} parallel) ===")

            Dim startOffsetSec = TimeToSec(startTime)
            Dim srtPaths As New List(Of String)

            For i = 0 To numChunks - 1
                srtPaths.Add(chunkPaths(i) & ".srt")
            Next

            Await TranscribeChunksAsync(chunkPaths, srtPaths, outputDir, numChunks)

            ' Step 7: Merge SRTs
            _ct.ThrowIfCancellationRequested()
            Report(7, "Merging subtitles...")
            Log("=== Step 7: Merging subtitles ===")

            Dim mergedPath = Path.Combine(outputDir, "preview.srt")
            Dim entryCount = SrtMerger.Merge(srtPaths, chunkStarts, TimeToSec(startTime), mergedPath)
            Log($"Merged {entryCount} subtitle entries.", LogLevel.Success)

            ' Clean up chunks if configured
            If Not _config.KeepChunkFiles Then
                For i = 0 To chunkPaths.Count - 1
                    Try
                        If File.Exists(chunkPaths(i)) Then File.Delete(chunkPaths(i))
                        If File.Exists(srtPaths(i)) Then File.Delete(srtPaths(i))
                    Catch
                    End Try
                Next
                Log("Chunk files cleaned up.")
            End If

            ' Clean up preview.mp4 if not keeping
            If Not _config.KeepPreview Then
                Try
                    If File.Exists(previewPath) Then File.Delete(previewPath)
                Catch
                End Try
            End If

            Report(7, "Done!")
            Log("=================================================", LogLevel.Success)
            Log($"Done! {entryCount} subtitles saved to: {mergedPath}", LogLevel.Success)
            Log("Open preview.mp4 in VLC - subtitles load automatically", LogLevel.Success)
            Log("=================================================", LogLevel.Success)
        End Function

        Public Async Function RunDownloadOnlyAsync(url As String, startTime As String, endTime As String,
                                                    outputDir As String, Optional resumeMode As Boolean = False) As Task
            _stepCount = 3 ' Steps 0-2
            ' Step 0: Validate
            Report(0, "Validating inputs...")
            Log("=== Step 0: Validating inputs (Download Only mode) ===")
            If Not resumeMode Then
                ValidateDownloadInputs(url, outputDir)
            Else
                Log("RESUME MODE - skipping completed steps.", LogLevel.Success)
                ValidateDownloadInputsForResume(outputDir)
            End If

            If Not Directory.Exists(outputDir) Then
                Directory.CreateDirectory(outputDir)
            End If

            Dim isLocalFile = File.Exists(url)
            Dim fullVideoPath = Path.Combine(outputDir, "yt_video_full.mp4")
            Dim previewPath = Path.Combine(outputDir, "preview.mp4")

            ' Step 1: Download
            _ct.ThrowIfCancellationRequested()
            Report(1, "Downloading video...")
            Log("=== Step 1: Downloading video ===")

            If File.Exists(fullVideoPath) Then
                Log("SKIP - yt_video_full.mp4 already exists.", LogLevel.Success)
            ElseIf resumeMode Then
                Throw New PipelineException("Err_DownloadFailed", "Cannot resume: yt_video_full.mp4 not found in output folder")
            ElseIf isLocalFile Then
                Log("Local file provided, copying...")
                File.Copy(url, fullVideoPath, True)
            Else
                Await RunProcessAsync(_config.PathYtdlp,
                    $"-f ""{_config.YtdlpFormat}"" ""{url}"" -o ""{fullVideoPath}""",
                    outputDir, "Err_DownloadFailed", "yt-dlp failed")
            End If

            If Not File.Exists(fullVideoPath) Then
                Throw New PipelineException("Err_DownloadFailed", "yt_video_full.mp4 was not created")
            End If
            Log("Download OK.", LogLevel.Success)

            ' Step 2: Trim video
            _ct.ThrowIfCancellationRequested()
            Report(2, "Trimming video...")
            Log("=== Step 2: Trimming video ===")

            If File.Exists(previewPath) Then
                Log("SKIP - preview.mp4 already exists.", LogLevel.Success)
            Else
                Dim trimArgs = $"-y -i ""{fullVideoPath}"""
                If Not String.IsNullOrWhiteSpace(startTime) AndAlso TimeToSec(startTime) > 0 Then trimArgs &= $" -ss {startTime}"
                If Not String.IsNullOrWhiteSpace(endTime) AndAlso TimeToSec(endTime) > 0 Then trimArgs &= $" -to {endTime}"
                trimArgs &= $" -c:v copy -c:a copy ""{previewPath}"""

                Await RunProcessAsync(_config.PathFfmpeg, trimArgs, outputDir, "Err_TrimFailed", "ffmpeg trim failed")
            End If

            If Not File.Exists(previewPath) Then
                Throw New PipelineException("Err_TrimFailed", "preview.mp4 was not created")
            End If
            Log("Trim OK.", LogLevel.Success)

            Report(2, "Done!")
            Log("=================================================", LogLevel.Success)
            Log($"Done! Video saved to: {previewPath}", LogLevel.Success)
            Log("=================================================", LogLevel.Success)
        End Function

        Public Async Function RunExtractAudioAsync(url As String, startTime As String, endTime As String,
                                                    outputDir As String, Optional resumeMode As Boolean = False) As Task
            _stepCount = 4 ' Steps 0-3
            ' Step 0: Validate
            Report(0, "Validating inputs...")
            Log("=== Step 0: Validating inputs (Extract Audio mode) ===")
            If Not resumeMode Then
                ValidateDownloadInputs(url, outputDir)
            Else
                Log("RESUME MODE - skipping completed steps.", LogLevel.Success)
                ValidateDownloadInputsForResume(outputDir)
            End If

            If Not Directory.Exists(outputDir) Then
                Directory.CreateDirectory(outputDir)
            End If

            Dim isLocalFile = File.Exists(url)
            Dim fullVideoPath = Path.Combine(outputDir, "yt_video_full.mp4")
            Dim previewPath = Path.Combine(outputDir, "preview.mp4")
            Dim audioPath = Path.Combine(outputDir, "yt_audio.mp3")

            ' Step 1: Download
            _ct.ThrowIfCancellationRequested()
            Report(1, "Downloading video...")
            Log("=== Step 1: Downloading video ===")

            If File.Exists(fullVideoPath) Then
                Log("SKIP - yt_video_full.mp4 already exists.", LogLevel.Success)
            ElseIf resumeMode Then
                Throw New PipelineException("Err_DownloadFailed", "Cannot resume: yt_video_full.mp4 not found in output folder")
            ElseIf isLocalFile Then
                Log("Local file provided, copying...")
                File.Copy(url, fullVideoPath, True)
            Else
                Await RunProcessAsync(_config.PathYtdlp,
                    $"-f ""{_config.YtdlpFormat}"" ""{url}"" -o ""{fullVideoPath}""",
                    outputDir, "Err_DownloadFailed", "yt-dlp failed")
            End If

            If Not File.Exists(fullVideoPath) Then
                Throw New PipelineException("Err_DownloadFailed", "yt_video_full.mp4 was not created")
            End If
            Log("Download OK.", LogLevel.Success)

            ' Step 2: Trim video
            _ct.ThrowIfCancellationRequested()
            Report(2, "Trimming video...")
            Log("=== Step 2: Trimming video ===")

            If File.Exists(previewPath) Then
                Log("SKIP - preview.mp4 already exists.", LogLevel.Success)
            Else
                Dim trimArgs = $"-y -i ""{fullVideoPath}"""
                If Not String.IsNullOrWhiteSpace(startTime) AndAlso TimeToSec(startTime) > 0 Then trimArgs &= $" -ss {startTime}"
                If Not String.IsNullOrWhiteSpace(endTime) AndAlso TimeToSec(endTime) > 0 Then trimArgs &= $" -to {endTime}"
                trimArgs &= $" -c:v copy -c:a copy ""{previewPath}"""

                Await RunProcessAsync(_config.PathFfmpeg, trimArgs, outputDir, "Err_TrimFailed", "ffmpeg trim failed")
            End If

            If Not File.Exists(previewPath) Then
                Throw New PipelineException("Err_TrimFailed", "preview.mp4 was not created")
            End If
            Log("Trim OK.", LogLevel.Success)

            ' Step 3: Extract audio (full quality, no re-encoding)
            _ct.ThrowIfCancellationRequested()
            Report(3, "Extracting audio...")
            Log("=== Step 3: Extracting audio ===")

            If File.Exists(audioPath) Then
                Log("SKIP - yt_audio.mp3 already exists.", LogLevel.Success)
            Else
                Await RunProcessAsync(_config.PathFfmpeg,
                    $"-y -i ""{previewPath}"" -vn -q:a 0 ""{audioPath}""",
                    outputDir, "Err_AudioExtractFailed", "ffmpeg audio extraction failed")
            End If

            If Not File.Exists(audioPath) Then
                Throw New PipelineException("Err_AudioExtractFailed", "yt_audio.mp3 was not created")
            End If
            Log("Audio extraction OK.", LogLevel.Success)

            ' Clean up preview.mp4 if not keeping
            If Not _config.KeepPreview Then
                Try
                    If File.Exists(previewPath) Then File.Delete(previewPath)
                Catch
                End Try
            End If

            Report(3, "Done!")
            Log("=================================================", LogLevel.Success)
            Log($"Done! Audio saved to: {audioPath}", LogLevel.Success)
            Log("=================================================", LogLevel.Success)
        End Function

        Public Async Function RunAudioFileAsync(inputFile As String, outputDir As String) As Task
            _stepCount = 6 ' Steps 0-5
            ' Step 0: Validate
            Report(0, "Validating inputs...")
            Log("=== Step 0: Validating inputs (Audio File mode) ===")

            If String.IsNullOrWhiteSpace(inputFile) OrElse Not File.Exists(inputFile) Then
                Throw New PipelineException("Err_NoInput", $"Audio file not found: {inputFile}")
            End If
            If Not File.Exists(_config.PathFfmpeg) Then
                Throw New PipelineException("Err_ToolNotFound", $"ffmpeg not found: {_config.PathFfmpeg}")
            End If
            If Not File.Exists(_config.PathFfprobe) Then
                Throw New PipelineException("Err_ToolNotFound", $"ffprobe not found: {_config.PathFfprobe}")
            End If
            If Not File.Exists(_config.PathWhisper) Then
                Throw New PipelineException("Err_ToolNotFound", $"whisper-cli not found: {_config.PathWhisper}")
            End If
            If Not File.Exists(_config.PathModel) Then
                Throw New PipelineException("Err_ToolNotFound", $"Model file not found: {_config.PathModel}")
            End If

            If Not Directory.Exists(outputDir) Then
                Directory.CreateDirectory(outputDir)
            End If

            Dim audioPath = Path.Combine(outputDir, "audio.wav")

            ' Step 1: Convert to WAV
            _ct.ThrowIfCancellationRequested()
            Report(1, "Converting to WAV...")
            Log("=== Step 1: Converting to WAV ===")

            If File.Exists(audioPath) Then
                Log("SKIP - audio.wav already exists.", LogLevel.Success)
            Else
                Await RunProcessAsync(_config.PathFfmpeg,
                    $"-y -i ""{inputFile}"" -ac 1 -ar 16000 -c:a pcm_s16le ""{audioPath}""",
                    outputDir, "Err_AudioExtractFailed", "ffmpeg audio conversion failed")
            End If

            If Not File.Exists(audioPath) Then
                Throw New PipelineException("Err_AudioExtractFailed", "audio.wav was not created")
            End If
            Log("Conversion OK.", LogLevel.Success)

            ' Step 2: Get duration
            _ct.ThrowIfCancellationRequested()
            Report(2, "Getting duration...")
            Log("=== Step 2: Getting duration ===")

            Dim durSec = Await GetDurationAsync(audioPath, outputDir)
            If durSec <= 0 Then
                Throw New PipelineException("Err_DurationFailed", "Could not determine audio duration")
            End If
            Log($"Duration: {Math.Round(durSec)} seconds", LogLevel.Info)

            ' Step 3: Split into chunks
            _ct.ThrowIfCancellationRequested()
            Report(3, "Splitting audio into chunks...")
            Log("=== Step 3: Splitting into chunks ===")

            Dim chunkSec = _config.ChunkSizeSec
            Dim numChunks = CInt(Math.Ceiling(durSec / chunkSec))
            Dim chunkPaths As New List(Of String)
            Dim chunkStarts As New List(Of Double)

            For i = 0 To numChunks - 1
                _ct.ThrowIfCancellationRequested()
                Dim chunkStart = i * chunkSec
                Dim idx = i.ToString("D3")
                Dim outChunk = Path.Combine(outputDir, $"chunk_{idx}.wav")

                chunkPaths.Add(outChunk)
                chunkStarts.Add(chunkStart)

                If File.Exists(outChunk) Then
                    Log($"SKIP - chunk_{idx}.wav already exists.", LogLevel.Success)
                Else
                    Log($"Creating chunk_{idx}.wav (start={chunkStart}s)")
                    Await RunProcessAsync(_config.PathFfmpeg,
                        $"-y -ss {chunkStart} -i ""{audioPath}"" -t {chunkSec} -ac 1 -ar 16000 -c:a pcm_s16le ""{outChunk}""",
                        outputDir, "Err_ChunkFailed", $"ffmpeg chunking failed on chunk {i}")
                End If

                If Not File.Exists(outChunk) Then
                    Throw New PipelineException("Err_ChunkFailed", $"chunk_{idx}.wav was not created")
                End If
            Next
            Log($"Chunking OK. {numChunks} chunks created.", LogLevel.Success)

            ' Step 4: Transcribe chunks
            _ct.ThrowIfCancellationRequested()
            Report(4, "Transcribing...")
            Log($"=== Step 4: Transcribing ({_config.ParallelJobs} parallel) ===")

            Dim srtPaths As New List(Of String)
            For i = 0 To numChunks - 1
                srtPaths.Add(chunkPaths(i) & ".srt")
            Next

            Await TranscribeChunksAsync(chunkPaths, srtPaths, outputDir, numChunks)

            ' Step 5: Merge SRTs
            _ct.ThrowIfCancellationRequested()
            Report(5, "Merging subtitles...")
            Log("=== Step 5: Merging subtitles ===")

            Dim mergedPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputFile) & ".srt")
            Dim entryCount = SrtMerger.Merge(srtPaths, chunkStarts, 0, mergedPath)
            Log($"Merged {entryCount} subtitle entries.", LogLevel.Success)

            ' Clean up chunks if configured
            If Not _config.KeepChunkFiles Then
                For i = 0 To chunkPaths.Count - 1
                    Try
                        If File.Exists(chunkPaths(i)) Then File.Delete(chunkPaths(i))
                        If File.Exists(srtPaths(i)) Then File.Delete(srtPaths(i))
                    Catch
                    End Try
                Next
                Log("Chunk files cleaned up.")
            End If

            Report(5, "Done!")
            Log("=================================================", LogLevel.Success)
            Log($"Done! {entryCount} subtitles saved to: {mergedPath}", LogLevel.Success)
            Log("=================================================", LogLevel.Success)
        End Function

        Private Async Function TranscribeChunksAsync(chunkPaths As List(Of String),
                                                       srtPaths As List(Of String),
                                                       outputDir As String,
                                                       numChunks As Integer) As Task
            Dim batchSize = _config.ParallelJobs
            Dim totalDone = 0

            For i = 0 To numChunks - 1 Step batchSize
                _ct.ThrowIfCancellationRequested()

                Dim batchEnd = Math.Min(i + batchSize, numChunks)
                Dim batchProcesses As New List(Of System.Diagnostics.Process)

                ' Launch batch (skip chunks that already have .srt files)
                Dim batchAllSkipped = True
                For j = i To batchEnd - 1
                    Dim idx = j.ToString("D3")

                    If File.Exists(srtPaths(j)) Then
                        Log($"SKIP - chunk_{idx}.wav.srt already exists.", LogLevel.Success)
                        totalDone += 1
                        Continue For
                    End If

                    batchAllSkipped = False
                    Log($"Starting chunk_{idx}.wav ({j + 1}/{numChunks})...")

                    Dim whisperArgs = TranscriptionCommandBuilder.Build(_config, chunkPaths(j))
                    Dim runner As New ProcessRunner()

                    Dim capturedJ = j
                    AddHandler runner.OutputReceived, Sub(s, data) Log($"  [chunk_{capturedJ.ToString("D3")}] {data}", LogLevel.Info)
                    AddHandler runner.ErrorReceived, Sub(s, data) Log($"  [chunk_{capturedJ.ToString("D3")}] {data}", LogLevel.Err)

                    Dim proc = runner.StartNoWait(_config.PathWhisper, whisperArgs, outputDir, False)
                    batchProcesses.Add(proc)
                Next

                If batchAllSkipped Then
                    Report(6, $"Transcribing... ({totalDone}/{numChunks})", totalDone, numChunks)
                    Continue For
                End If

                ' Poll for completion
                Log("Waiting for batch to complete...")
                Dim timeoutMs = _config.ChunkTimeoutMin * 60 * 1000
                Dim elapsed = 0

                While elapsed < timeoutMs
                    _ct.ThrowIfCancellationRequested()
                    Await Task.Delay(_config.PollIntervalMs, _ct)
                    elapsed += _config.PollIntervalMs

                    Dim allDone = True
                    For j = i To batchEnd - 1
                        If Not File.Exists(srtPaths(j)) Then
                            allDone = False
                            Exit For
                        End If
                    Next

                    If allDone Then Exit While
                End While

                ' Report batch results
                For j = i To batchEnd - 1
                    Dim idx = j.ToString("D3")
                    If File.Exists(srtPaths(j)) Then
                        Log($"  chunk_{idx}: OK", LogLevel.Success)
                        totalDone += 1
                    Else
                        Log($"  chunk_{idx}: WARNING - srt not created (timeout)", LogLevel.Err)
                    End If
                Next

                Report(6, $"Transcribing... ({totalDone}/{numChunks})", totalDone, numChunks)

                ' Kill any remaining processes
                For Each proc In batchProcesses
                    Try
                        If Not proc.HasExited Then proc.Kill(True)
                    Catch
                    End Try
                    proc.Dispose()
                Next
            Next
        End Function

        Private Async Function GetDurationAsync(audioPath As String, workingDir As String) As Task(Of Double)
            Dim runner As New ProcessRunner()
            Dim output As New Text.StringBuilder()

            AddHandler runner.OutputReceived, Sub(s, data) output.AppendLine(data)
            AddHandler runner.ErrorReceived, Sub(s, data) Log($"  ffprobe: {data}", LogLevel.Verbose)

            Dim code = Await runner.RunAsync(_config.PathFfprobe,
                $"-v error -show_entries format=duration -of csv=p=0 ""{audioPath}""",
                workingDir, _ct)

            Dim durSec As Double
            Double.TryParse(output.ToString().Trim(),
                           Globalization.NumberStyles.Any,
                           Globalization.CultureInfo.InvariantCulture,
                           durSec)
            Return durSec
        End Function

        Private Async Function RunProcessAsync(exePath As String, arguments As String,
                                                workingDir As String, errKey As String,
                                                errMsg As String) As Task
            Log($"  CMD: ""{exePath}"" {arguments}", LogLevel.Verbose)
            Log($"  CWD: {workingDir}", LogLevel.Verbose)

            Dim runner As New ProcessRunner()
            AddHandler runner.OutputReceived, Sub(s, data) Log($"  {data}")
            AddHandler runner.ErrorReceived, Sub(s, data) Log($"  {data}", LogLevel.Err)

            Dim code = Await runner.RunAsync(exePath, arguments, workingDir, _ct)
            Log($"  Exit code: {code}", LogLevel.Verbose)
            If code <> 0 Then
                Throw New PipelineException(errKey, $"{errMsg} (exit code {code})")
            End If
        End Function

        Private Sub ValidateDownloadInputs(url As String, outputDir As String)
            If String.IsNullOrWhiteSpace(url) Then
                Throw New PipelineException("Err_NoInput", "No URL or file specified")
            End If

            If Not File.Exists(url) Then
                ' URL mode - need yt-dlp
                If Not File.Exists(_config.PathYtdlp) Then
                    Throw New PipelineException("Err_ToolNotFound", $"yt-dlp not found: {_config.PathYtdlp}")
                End If
            End If

            If Not File.Exists(_config.PathFfmpeg) Then
                Throw New PipelineException("Err_ToolNotFound", $"ffmpeg not found: {_config.PathFfmpeg}")
            End If
        End Sub

        Private Sub ValidateDownloadInputsForResume(outputDir As String)
            If Not Directory.Exists(outputDir) Then
                Throw New PipelineException("Err_NoInput", $"Output folder not found: {outputDir}")
            End If
            If Not File.Exists(_config.PathFfmpeg) Then
                Throw New PipelineException("Err_ToolNotFound", $"ffmpeg not found: {_config.PathFfmpeg}")
            End If
        End Sub

        Private Sub ValidateInputsForResume(outputDir As String)
            If Not Directory.Exists(outputDir) Then
                Throw New PipelineException("Err_NoInput", $"Output folder not found: {outputDir}")
            End If
            If Not File.Exists(_config.PathFfmpeg) Then
                Throw New PipelineException("Err_ToolNotFound", $"ffmpeg not found: {_config.PathFfmpeg}")
            End If
            If Not File.Exists(_config.PathFfprobe) Then
                Throw New PipelineException("Err_ToolNotFound", $"ffprobe not found: {_config.PathFfprobe}")
            End If
            If Not File.Exists(_config.PathWhisper) Then
                Throw New PipelineException("Err_ToolNotFound", $"whisper-cli not found: {_config.PathWhisper}")
            End If
            If Not File.Exists(_config.PathModel) Then
                Throw New PipelineException("Err_ToolNotFound", $"Model file not found: {_config.PathModel}")
            End If
        End Sub

        Private Sub ValidateInputs(url As String, outputDir As String)
            If String.IsNullOrWhiteSpace(url) Then
                Throw New PipelineException("Err_NoInput", "No URL or file specified")
            End If

            ' Only check whisper tools that will be needed
            If Not File.Exists(url) Then
                ' URL mode - need yt-dlp
                If Not File.Exists(_config.PathYtdlp) Then
                    Throw New PipelineException("Err_ToolNotFound", $"yt-dlp not found: {_config.PathYtdlp}")
                End If
            End If

            If Not File.Exists(_config.PathFfmpeg) Then
                Throw New PipelineException("Err_ToolNotFound", $"ffmpeg not found: {_config.PathFfmpeg}")
            End If
            If Not File.Exists(_config.PathFfprobe) Then
                Throw New PipelineException("Err_ToolNotFound", $"ffprobe not found: {_config.PathFfprobe}")
            End If
            If Not File.Exists(_config.PathWhisper) Then
                Throw New PipelineException("Err_ToolNotFound", $"whisper-cli not found: {_config.PathWhisper}")
            End If
            If Not File.Exists(_config.PathModel) Then
                Throw New PipelineException("Err_ToolNotFound", $"Model file not found: {_config.PathModel}")
            End If
        End Sub

        Private Shared Function TimeToSec(t As String) As Double
            If String.IsNullOrWhiteSpace(t) Then Return 0
            Dim parts = t.Split(":"c)
            If parts.Length < 3 Then Return 0
            Dim h, m, s As Integer
            Integer.TryParse(parts(0), h)
            Integer.TryParse(parts(1), m)
            Integer.TryParse(parts(2), s)
            Return h * 3600.0 + m * 60.0 + s
        End Function

        Public Enum LogLevel
            Info
            Success
            Err
            Verbose
        End Enum

        Public Class LogEntry
            Public Property Message As String = ""
            Public Property Level As LogLevel = LogLevel.Info
        End Class
    End Class
End Namespace
