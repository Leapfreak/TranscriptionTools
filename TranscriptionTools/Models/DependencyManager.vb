Imports System.IO
Imports System.IO.Compression
Imports System.Net.Http
Imports System.Text.Json
Imports System.Text.RegularExpressions

Namespace Models

    Public Enum ToolStatus
        Missing
        Installed
        UpdateAvailable
        UpToDate
        CheckFailed
    End Enum

    Public Class ToolState
        Public Property Name As String = ""
        Public Property Status As ToolStatus = ToolStatus.Missing
        Public Property InstalledVersion As String = ""
        Public Property LatestVersion As String = ""
        Public Property DownloadUrl As String = ""
    End Class

    Public Class DependencyManager

        Private Shared ReadOnly _client As New HttpClient()
        Private ReadOnly _toolsDir As String
        Private ReadOnly _config As AppConfig
        Private _versions As Dictionary(Of String, String)

        Private Shared ReadOnly _downloadClient As New HttpClient()

        Shared Sub New()
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("TranscriptionTools-DependencyManager")
            _client.Timeout = TimeSpan.FromSeconds(30)

            _downloadClient.DefaultRequestHeaders.UserAgent.ParseAdd("TranscriptionTools-DependencyManager")
            _downloadClient.Timeout = TimeSpan.FromMinutes(30)
        End Sub

        Public Sub New(config As AppConfig, toolsDir As String)
            _config = config
            _toolsDir = toolsDir
            If Not Directory.Exists(_toolsDir) Then
                Directory.CreateDirectory(_toolsDir)
            End If
            _versions = LoadVersions()
        End Sub

        ' ──────────────────────────────────────────
        '  Version tracking (tool-versions.json)
        ' ──────────────────────────────────────────

        Private Function VersionsFilePath() As String
            Return Path.Combine(_toolsDir, "tool-versions.json")
        End Function

        Private Function LoadVersions() As Dictionary(Of String, String)
            Try
                Dim path = VersionsFilePath()
                If File.Exists(path) Then
                    Dim json = File.ReadAllText(path)
                    Return JsonSerializer.Deserialize(Of Dictionary(Of String, String))(json)
                End If
            Catch
            End Try
            Return New Dictionary(Of String, String)()
        End Function

        Private Sub SaveVersion(toolName As String, version As String)
            _versions(toolName) = version
            Try
                Dim json = JsonSerializer.Serialize(_versions, New JsonSerializerOptions With {.WriteIndented = True})
                File.WriteAllText(VersionsFilePath(), json)
            Catch
            End Try
        End Sub

        Private Function GetSavedVersion(toolName As String) As String
            Dim ver As String = Nothing
            If _versions.TryGetValue(toolName, ver) Then Return ver
            Return ""
        End Function

        Public ReadOnly Property ToolsDirectory As String
            Get
                Return _toolsDir
            End Get
        End Property

        ' ──────────────────────────────────────────
        '  Check all tools
        ' ──────────────────────────────────────────

        Public Async Function CheckAllToolsAsync() As Task(Of List(Of ToolState))
            Dim tasks As New List(Of Task(Of ToolState)) From {
                CheckYtDlpAsync(),
                CheckFfmpegAsync(),
                CheckModelAsync(),
                CheckSubtitleEditAsync()
            }
            Await Task.WhenAll(tasks)
            Return tasks.Select(Function(t) t.Result).ToList()
        End Function

        Public Function GetMissingTools(states As List(Of ToolState)) As List(Of ToolState)
            Return states.Where(Function(s) s.Status = ToolStatus.Missing).ToList()
        End Function

        Public Function GetUpdatableTools(states As List(Of ToolState)) As List(Of ToolState)
            Return states.Where(Function(s) s.Status = ToolStatus.UpdateAvailable).ToList()
        End Function

        ' ──────────────────────────────────────────
        '  yt-dlp
        ' ──────────────────────────────────────────

        Private Function YtDlpInstalledPath() As String
            Return AppConfig.ResolvePath(_config.PathYtdlp)
        End Function

        Private Function YtDlpDownloadPath() As String
            Return Path.Combine(_toolsDir, "yt-dlp.exe")
        End Function

        Public Async Function CheckYtDlpAsync() As Task(Of ToolState)
            Dim state As New ToolState With {.Name = "yt-dlp"}
            Try
                Dim installedPath = YtDlpInstalledPath()
                If File.Exists(installedPath) Then
                    state.InstalledVersion = GetSavedVersion("yt-dlp")
                    ' Fall back to running --version if no saved version
                    If String.IsNullOrEmpty(state.InstalledVersion) Then
                        state.InstalledVersion = Await GetProcessOutputAsync(installedPath, "--version")
                        ' Persist the detected version for future checks
                        If Not String.IsNullOrEmpty(state.InstalledVersion) Then
                            SaveVersion("yt-dlp", state.InstalledVersion)
                        End If
                    End If
                    If String.IsNullOrEmpty(state.InstalledVersion) Then state.InstalledVersion = "unknown"
                    state.Status = ToolStatus.Installed
                End If

                Dim release = Await GetLatestReleaseAsync("yt-dlp/yt-dlp")
                If release IsNot Nothing Then
                    state.LatestVersion = release.Value.TagName
                    state.DownloadUrl = FindAsset(release.Value.Assets, "^yt-dlp\.exe$")

                    If state.Status = ToolStatus.Installed Then
                        state.Status = CompareVersionTags(state.InstalledVersion, state.LatestVersion)
                    End If
                End If
            Catch
                If state.Status = ToolStatus.Missing Then state.Status = ToolStatus.CheckFailed
            End Try
            Return state
        End Function

        Public Async Function DownloadYtDlpAsync(url As String, progress As IProgress(Of (downloaded As Long, total As Long))) As Task
            Await DownloadFileAsync(url, YtDlpDownloadPath(), progress)
        End Function

        ' ──────────────────────────────────────────
        '  FFmpeg + FFprobe
        ' ──────────────────────────────────────────

        Private Function FfmpegInstalledPath() As String
            Return AppConfig.ResolvePath(_config.PathFfmpeg)
        End Function

        Private Function FfprobeInstalledPath() As String
            Return AppConfig.ResolvePath(_config.PathFfprobe)
        End Function

        Public Async Function CheckFfmpegAsync() As Task(Of ToolState)
            Dim state As New ToolState With {.Name = "FFmpeg"}
            Try
                Dim ffmpegPath = FfmpegInstalledPath()
                Dim ffprobePath = FfprobeInstalledPath()
                If File.Exists(ffmpegPath) AndAlso File.Exists(ffprobePath) Then
                    state.InstalledVersion = GetSavedVersion("FFmpeg")
                    If String.IsNullOrEmpty(state.InstalledVersion) Then
                        Dim output = Await GetProcessOutputAsync(ffmpegPath, "-version")
                        Dim m = Regex.Match(output, "ffmpeg version (\S+)")
                        state.InstalledVersion = If(m.Success, m.Groups(1).Value, "unknown")
                    End If
                    state.Status = ToolStatus.Installed
                End If

                Dim release = Await GetLatestReleaseAsync("BtbN/FFmpeg-Builds")
                If release IsNot Nothing Then
                    state.LatestVersion = release.Value.TagName
                    state.DownloadUrl = FindAsset(release.Value.Assets, "ffmpeg-master-latest-win64-gpl\.zip$")

                    ' FFmpeg uses rolling "autobuild-*" tags with no meaningful semver —
                    ' if the exe exists, treat as up to date and save the tag
                    If state.Status = ToolStatus.Installed Then
                        If String.IsNullOrEmpty(GetSavedVersion("FFmpeg")) Then
                            SaveVersion("FFmpeg", state.LatestVersion)
                            state.InstalledVersion = state.LatestVersion
                        End If
                        state.Status = ToolStatus.UpToDate
                    End If
                End If
            Catch
                If state.Status = ToolStatus.Missing Then state.Status = ToolStatus.CheckFailed
            End Try
            Return state
        End Function

        Public Async Function DownloadFfmpegAsync(url As String, progress As IProgress(Of (downloaded As Long, total As Long))) As Task
            Dim zipPath = Path.Combine(_toolsDir, "ffmpeg-temp.zip")
            Try
                Await DownloadFileAsync(url, zipPath, progress)
                ExtractFilesFromZip(zipPath, {"ffmpeg.exe", "ffprobe.exe"}, _toolsDir)
            Finally
                If File.Exists(zipPath) Then File.Delete(zipPath)
            End Try
        End Function

        ' ──────────────────────────────────────────
        '  GGML Model
        ' ──────────────────────────────────────────

        Private Function ModelInstalledPath() As String
            Return AppConfig.ResolvePath(_config.PathModel)
        End Function

        Private Function ModelDownloadPath() As String
            Return Path.Combine(_toolsDir, "ggml-large-v3.bin")
        End Function

        Public Function CheckModelAsync() As Task(Of ToolState)
            Dim state As New ToolState With {
                .Name = "Whisper Model (ggml-large-v3)",
                .DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin"
            }
            If File.Exists(ModelInstalledPath()) Then
                state.Status = ToolStatus.UpToDate
                state.InstalledVersion = "installed"
            End If
            Return Task.FromResult(state)
        End Function

        Public Async Function DownloadModelAsync(url As String, progress As IProgress(Of (downloaded As Long, total As Long))) As Task
            Await DownloadFileAsync(url, ModelDownloadPath(), progress)
        End Function

        ' ──────────────────────────────────────────
        '  SubtitleEdit
        ' ──────────────────────────────────────────

        Private Function SubtitleEditInstalledPath() As String
            Return AppConfig.ResolvePath(_config.PathSubtitleEdit)
        End Function

        Private Function SubtitleEditDownloadDir() As String
            Return Path.Combine(_toolsDir, "SubtitleEdit")
        End Function

        Public Async Function CheckSubtitleEditAsync() As Task(Of ToolState)
            Dim state As New ToolState With {.Name = "Subtitle Edit"}
            Try
                Dim sePath = SubtitleEditInstalledPath()
                If File.Exists(sePath) Then
                    state.InstalledVersion = GetSavedVersion("Subtitle Edit")
                    state.Status = ToolStatus.Installed
                End If

                Dim release = Await GetLatestReleaseAsync("SubtitleEdit/subtitleedit")
                If release IsNot Nothing Then
                    state.LatestVersion = release.Value.TagName
                    state.DownloadUrl = FindAsset(release.Value.Assets, "^SE\d+\.zip$")

                    If state.Status = ToolStatus.Installed Then
                        If String.IsNullOrEmpty(state.InstalledVersion) Then
                            ' No saved version but exe exists — assume current, save latest
                            SaveVersion("Subtitle Edit", state.LatestVersion)
                            state.InstalledVersion = state.LatestVersion
                            state.Status = ToolStatus.UpToDate
                        Else
                            state.Status = CompareVersionTags(state.InstalledVersion, state.LatestVersion)
                        End If
                    End If
                End If
            Catch
                If state.Status = ToolStatus.Missing Then state.Status = ToolStatus.CheckFailed
            End Try
            Return state
        End Function

        Public Async Function DownloadSubtitleEditAsync(url As String, progress As IProgress(Of (downloaded As Long, total As Long))) As Task
            Dim zipPath = Path.Combine(_toolsDir, "subtitleedit-temp.zip")
            Try
                Await DownloadFileAsync(url, zipPath, progress)
                Dim seDir = SubtitleEditDownloadDir()
                If Not Directory.Exists(seDir) Then Directory.CreateDirectory(seDir)
                ExtractAllFromZip(zipPath, seDir)
            Finally
                If File.Exists(zipPath) Then File.Delete(zipPath)
            End Try
        End Function

        ' ──────────────────────────────────────────
        '  Python Embedded + NLLB pip deps
        ' ──────────────────────────────────────────

        Private Function PythonEmbedDir() As String
            Return Path.Combine(_toolsDir, "python-embed")
        End Function

        Private Function PythonExePath() As String
            Return Path.Combine(PythonEmbedDir(), "python.exe")
        End Function

        Public Function CheckPythonEmbedAsync() As Task(Of ToolState)
            Dim state As New ToolState With {.Name = "Python Embedded"}
            If File.Exists(PythonExePath()) Then
                state.Status = ToolStatus.UpToDate
                state.InstalledVersion = "installed"
            End If
            Return Task.FromResult(state)
        End Function

        Public Async Function DownloadPythonEmbedAsync(progress As IProgress(Of (downloaded As Long, total As Long))) As Task
            Dim embedDir = PythonEmbedDir()
            Dim zipPath = Path.Combine(_toolsDir, "python-embed-temp.zip")
            Try
                Dim url = "https://www.python.org/ftp/python/3.12.9/python-3.12.9-embed-amd64.zip"
                Await DownloadFileAsync(url, zipPath, progress)

                ' Extract to python-embed/
                If Directory.Exists(embedDir) Then Directory.Delete(embedDir, True)
                ZipFile.ExtractToDirectory(zipPath, embedDir)

                ' Enable import site in python312._pth so pip works
                Dim pthFile = Path.Combine(embedDir, "python312._pth")
                If File.Exists(pthFile) Then
                    Dim lines = File.ReadAllLines(pthFile).ToList()
                    Dim found = False
                    For i = 0 To lines.Count - 1
                        If lines(i).TrimStart().StartsWith("#") AndAlso lines(i).Contains("import site") Then
                            lines(i) = "import site"
                            found = True
                            Exit For
                        End If
                    Next
                    If Not found Then lines.Add("import site")
                    File.WriteAllLines(pthFile, lines)
                End If

                ' Download and run get-pip.py
                Dim getPipPath = Path.Combine(embedDir, "get-pip.py")
                Await DownloadFileAsync("https://bootstrap.pypa.io/get-pip.py", getPipPath, Nothing)
                Await RunProcessAsync(PythonExePath(), $"""{getPipPath}""", embedDir)
                If File.Exists(getPipPath) Then File.Delete(getPipPath)
            Finally
                If File.Exists(zipPath) Then File.Delete(zipPath)
            End Try
        End Function

        Public Async Function InstallPythonDepsAsync(progress As IProgress(Of (downloaded As Long, total As Long))) As Task
            Dim reqFile = Path.Combine(_toolsDir, "nllb-server", "requirements.txt")
            If Not File.Exists(reqFile) Then
                Throw New FileNotFoundException("requirements.txt not found at " & reqFile)
            End If
            Await RunProcessAsync(PythonExePath(),
                $"-m pip install -r ""{reqFile}"" --no-warn-script-location",
                _toolsDir, 600000)
        End Function

        Public Function CheckPythonDepsInstalled() As Boolean
            If Not File.Exists(PythonExePath()) Then Return False
            Try
                Dim psi As New Diagnostics.ProcessStartInfo With {
                    .FileName = PythonExePath(),
                    .Arguments = "-c ""import ctranslate2; import sentencepiece; import fastapi; import uvicorn""",
                    .UseShellExecute = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .CreateNoWindow = True
                }
                Using proc = Diagnostics.Process.Start(psi)
                    proc.WaitForExit(10000)
                    Return proc.ExitCode = 0
                End Using
            Catch
                Return False
            End Try
        End Function

        Public Function CheckTranslationDepsAsync() As Task(Of (pythonOk As Boolean, depsOk As Boolean, modelOk As Boolean))
            Dim pythonOk = File.Exists(PythonExePath())
            Dim depsOk = If(pythonOk, CheckPythonDepsInstalled(), False)
            Dim modelOk = Directory.Exists(NllbModelDir()) AndAlso
                          File.Exists(Path.Combine(NllbModelDir(), "model.bin")) AndAlso
                          File.Exists(Path.Combine(NllbModelDir(), "sentencepiece.bpe.model"))
            Return Task.FromResult((pythonOk, depsOk, modelOk))
        End Function

        Private Shared Async Function RunProcessAsync(exePath As String, args As String,
                                                       workDir As String,
                                                       Optional timeoutMs As Integer = 300000) As Task
            Dim psi As New Diagnostics.ProcessStartInfo With {
                .FileName = exePath,
                .Arguments = args,
                .WorkingDirectory = workDir,
                .UseShellExecute = False,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .CreateNoWindow = True
            }
            Using proc = Diagnostics.Process.Start(psi)
                Dim stdoutTask = proc.StandardOutput.ReadToEndAsync()
                Dim stderrTask = proc.StandardError.ReadToEndAsync()
                Using cts As New Threading.CancellationTokenSource(timeoutMs)
                    Try
                        Await proc.WaitForExitAsync(cts.Token)
                    Catch ex As OperationCanceledException
                        Try : proc.Kill(True) : Catch : End Try
                        Throw New TimeoutException($"Process timed out after {timeoutMs / 1000}s")
                    End Try
                End Using
                Dim stderr = Await stderrTask
                If proc.ExitCode <> 0 Then
                    Dim stdout = Await stdoutTask
                    Throw New Exception($"Process exited with code {proc.ExitCode}: {If(stderr, stdout)}")
                End If
            End Using
        End Function

        ' ──────────────────────────────────────────
        '  NLLB Translation Model
        ' ──────────────────────────────────────────

        Private Function NllbModelDir() As String
            Return Path.Combine(_toolsDir, "nllb-model")
        End Function

        Public Function CheckNllbModelAsync() As Task(Of ToolState)
            Dim state As New ToolState With {
                .Name = "NLLB Translation Model",
                .DownloadUrl = "https://huggingface.co/JustFrederik/nllb-200-1.3B-ct2-float16/resolve/main"
            }
            Dim modelDir = NllbModelDir()
            If Directory.Exists(modelDir) AndAlso
               File.Exists(Path.Combine(modelDir, "model.bin")) AndAlso
               File.Exists(Path.Combine(modelDir, "sentencepiece.bpe.model")) Then
                state.Status = ToolStatus.UpToDate
                state.InstalledVersion = "installed"
            End If
            Return Task.FromResult(state)
        End Function

        Public Async Function DownloadNllbModelAsync(progress As IProgress(Of (downloaded As Long, total As Long))) As Task
            Dim modelDir = NllbModelDir()
            If Not Directory.Exists(modelDir) Then Directory.CreateDirectory(modelDir)

            Dim baseUrl = "https://huggingface.co/JustFrederik/nllb-200-1.3B-ct2-float16/resolve/main"
            Dim files = {"model.bin", "sentencepiece.bpe.model", "shared_vocabulary.txt", "config.json", "tokenizer_config.json"}

            For Each f In files
                Dim destPath = Path.Combine(modelDir, f)
                If Not File.Exists(destPath) Then
                    Dim url = $"{baseUrl}/{f}"
                    Await DownloadFileAsync(url, destPath, progress)
                End If
            Next
        End Function

        ' ──────────────────────────────────────────
        '  Download a tool by name
        ' ──────────────────────────────────────────

        Public Async Function DownloadToolAsync(state As ToolState, progress As IProgress(Of (downloaded As Long, total As Long))) As Task
            If String.IsNullOrEmpty(state.DownloadUrl) Then Return

            Select Case state.Name
                Case "yt-dlp"
                    Await DownloadYtDlpAsync(state.DownloadUrl, progress)
                Case "FFmpeg"
                    Await DownloadFfmpegAsync(state.DownloadUrl, progress)
                Case "Whisper Model (ggml-large-v3)"
                    Await DownloadModelAsync(state.DownloadUrl, progress)
                Case "Subtitle Edit"
                    Await DownloadSubtitleEditAsync(state.DownloadUrl, progress)
                Case "NLLB Translation Model"
                    Await DownloadNllbModelAsync(progress)
            End Select

            ' Save the downloaded version
            If Not String.IsNullOrEmpty(state.LatestVersion) Then
                SaveVersion(state.Name, state.LatestVersion)
            End If
        End Function

        ' ──────────────────────────────────────────
        '  Version comparison
        ' ──────────────────────────────────────────

        Private Shared Function CompareVersionTags(installed As String, latest As String) As ToolStatus
            If String.IsNullOrEmpty(installed) OrElse installed = "unknown" Then
                Return ToolStatus.UpdateAvailable
            End If
            If String.IsNullOrEmpty(latest) Then
                Return ToolStatus.UpToDate
            End If
            ' Normalize: strip leading v, trim whitespace
            Dim a = installed.Trim().TrimStart("v"c, "V"c)
            Dim b = latest.Trim().TrimStart("v"c, "V"c)
            ' Exact match
            If a = b Then Return ToolStatus.UpToDate
            ' Try semver comparison
            Dim vA As Version = Nothing
            Dim vB As Version = Nothing
            If Version.TryParse(a, vA) AndAlso Version.TryParse(b, vB) Then
                Return If(vB > vA, ToolStatus.UpdateAvailable, ToolStatus.UpToDate)
            End If
            ' If only one side parses, or neither parses, and they don't match,
            ' default to UpToDate to avoid false update prompts
            Return ToolStatus.UpToDate
        End Function

        ' ──────────────────────────────────────────
        '  GitHub API helpers
        ' ──────────────────────────────────────────

        Private Structure ReleaseInfo
            Public TagName As String
            Public Assets As List(Of (Name As String, Url As String))
        End Structure

        Private Shared Async Function GetLatestReleaseAsync(repo As String) As Task(Of ReleaseInfo?)
            Dim url = $"https://api.github.com/repos/{repo}/releases/latest"
            Dim response = Await _client.GetAsync(url)

            ' Follow redirect if repo was moved
            If response.StatusCode = Net.HttpStatusCode.MovedPermanently Then
                Dim redirectUrl = response.Headers.Location?.ToString()
                If Not String.IsNullOrEmpty(redirectUrl) Then
                    response = Await _client.GetAsync(redirectUrl)
                End If
            End If

            If Not response.IsSuccessStatusCode Then Return Nothing

            Dim json = Await response.Content.ReadAsStringAsync()
            Using doc = JsonDocument.Parse(json)
                Dim root = doc.RootElement
                Dim tagName = root.GetProperty("tag_name").GetString()
                Dim assets As New List(Of (Name As String, Url As String))

                For Each asset In root.GetProperty("assets").EnumerateArray()
                    Dim assetName = asset.GetProperty("name").GetString()
                    Dim assetUrl = asset.GetProperty("browser_download_url").GetString()
                    assets.Add((assetName, assetUrl))
                Next

                Return New ReleaseInfo With {
                    .TagName = tagName,
                    .Assets = assets
                }
            End Using
        End Function

        Private Shared Function FindAsset(assets As List(Of (Name As String, Url As String)), pattern As String) As String
            Dim match = assets.FirstOrDefault(Function(a) Regex.IsMatch(a.Name, pattern, RegexOptions.IgnoreCase))
            Return match.Url
        End Function

        ' ──────────────────────────────────────────
        '  Download / Extract helpers
        ' ──────────────────────────────────────────

        Private Shared Async Function DownloadFileAsync(url As String, destPath As String,
                                                        progress As IProgress(Of (downloaded As Long, total As Long))) As Task
            Using response = Await _downloadClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                response.EnsureSuccessStatusCode()
                Dim totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault(-1)

                Using contentStream = Await response.Content.ReadAsStreamAsync(),
                      fileStream As New FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, True)

                    Dim buffer(81919) As Byte
                    Dim totalRead As Long = 0
                    Dim bytesRead As Integer

                    Do
                        bytesRead = Await contentStream.ReadAsync(buffer, 0, buffer.Length)
                        If bytesRead > 0 Then
                            Await fileStream.WriteAsync(buffer, 0, bytesRead)
                            totalRead += bytesRead
                            progress?.Report((totalRead, totalBytes))
                        End If
                    Loop While bytesRead > 0
                End Using
            End Using
        End Function

        Private Shared Sub ExtractFilesFromZip(zipPath As String, fileNames As String(), destDir As String)
            Using archive = ZipFile.OpenRead(zipPath)
                For Each entry In archive.Entries
                    Dim name = Path.GetFileName(entry.FullName)
                    If fileNames.Any(Function(f) f.Equals(name, StringComparison.OrdinalIgnoreCase)) Then
                        Dim destPath = Path.Combine(destDir, name)
                        entry.ExtractToFile(destPath, overwrite:=True)
                    End If
                Next
            End Using
        End Sub

        Private Shared Sub ExtractAllFromZip(zipPath As String, destDir As String)
            Using archive = ZipFile.OpenRead(zipPath)
                For Each entry In archive.Entries
                    ' Skip directories
                    If String.IsNullOrEmpty(entry.Name) Then Continue For
                    Dim destPath = Path.Combine(destDir, entry.Name)
                    entry.ExtractToFile(destPath, overwrite:=True)
                Next
            End Using
        End Sub

        Private Shared Async Function GetProcessOutputAsync(exePath As String, args As String,
                                                            Optional timeoutMs As Integer = 10000) As Task(Of String)
            Dim psi As New Diagnostics.ProcessStartInfo With {
                .FileName = exePath,
                .Arguments = args,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .UseShellExecute = False,
                .CreateNoWindow = True
            }
            Using proc = Diagnostics.Process.Start(psi)
                Using cts As New Threading.CancellationTokenSource(timeoutMs)
                    Try
                        Dim stdoutTask = proc.StandardOutput.ReadToEndAsync()
                        Dim stderrTask = proc.StandardError.ReadToEndAsync()
                        Await proc.WaitForExitAsync(cts.Token)
                        Dim stdout = Await stdoutTask
                        Dim stderr = Await stderrTask
                        ' Return stdout if it has content, otherwise stderr
                        Dim result = If(String.IsNullOrWhiteSpace(stdout), stderr, stdout)
                        Return result.Trim()
                    Catch ex As OperationCanceledException
                        Try : proc.Kill() : Catch : End Try
                        Return ""
                    End Try
                End Using
            End Using
        End Function

    End Class
End Namespace
