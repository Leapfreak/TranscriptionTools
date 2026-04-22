Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Resources
Imports System.Threading
Imports Microsoft.Win32
Imports TranscriptionTools.Models
Imports TranscriptionTools.Pipeline

Public Class FormMain

    Private _config As AppConfig
    Private _cts As CancellationTokenSource
    Private _isRunning As Boolean = False
    Private _currentOutputDir As String = ""
    Private _resMgr As ResourceManager
    Private _liveRunner As LiveStreamRunner
    Private _subtitleServer As SubtitleServer
    Private _translationService As TranslationService
    Private _translationUnloadTimer As System.Threading.Timer
    Private _simCts As CancellationTokenSource
    Private _isInitializing As Boolean = True
    Private _exitForReal As Boolean = False

    ' Supported whisper languages
    Private ReadOnly _whisperLanguages As String() = {
        "auto", "en", "es", "fr", "de", "it", "pt", "nl", "pl", "ru",
        "zh", "ja", "ko", "ar", "hi", "tr", "vi", "th", "cs", "el",
        "hu", "ro", "da", "fi", "no", "sv", "sk", "uk", "bg", "hr",
        "ca", "cy", "et", "ga", "lv", "lt", "mt", "sl", "sq", "mk",
        "sr", "bs", "is", "ms", "sw", "tl", "ta", "te", "ml", "si",
        "bn", "gu", "kn", "mr", "ne", "pa", "ur", "my", "lo", "km",
        "he", "fa", "id", "jw", "la", "mn", "ps", "sd", "sn", "so",
        "su", "tg", "tt", "uz", "yo", "af", "am", "as", "az", "ba",
        "be", "br", "fo", "gl", "ha", "ht", "hy", "ka", "kk", "lb",
        "ln", "mg", "mi", "nn", "oc", "sa", "tk", "wo", "yi", "yue"
    }

    ' Available UI locales with native names
    Private ReadOnly _uiLocales As (Code As String, Name As String)() = {
        ("en", "English"),
        ("es", "Espanol"),
        ("fr", "Francais"),
        ("de", "Deutsch"),
        ("ca", "Catala"),
        ("pt", "Portugues"),
        ("zh-Hans", "中文(简体)"),
        ("ja", "日本語")
    }

    Private Sub FormMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        _resMgr = New ResourceManager("TranscriptionTools.Strings", GetType(FormMain).Assembly)

        ' Load config
        _config = ConfigManager.Load()

        ' Populate dropdowns
        PopulateLanguageDropdowns()
        PopulateUiLanguageDropdown()

        ' Bind config to UI
        LoadConfigToUi()
        _isInitializing = False

        ' Set default output directory
        If String.IsNullOrWhiteSpace(txtOutputDir.Text) Then
            Dim stamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")
            txtOutputDir.Text = Path.Combine(AppConfig.ResolvePath(_config.PathOutputRoot), stamp)
        End If

        ' Apply locale
        Dim culture = New CultureInfo(_config.UiLanguage)
        Thread.CurrentThread.CurrentUICulture = culture
        ApplyLocale()

        ' Populate model dropdown
        PopulateModelDropdown()

        ' Set browse button text for YouTube mode on startup
        btnBrowseFile.Text = GetString("Btn_BrowseFile")

        ' Restrict time boxes to digits only
        For Each tb In {txtStartHH, txtStartMM, txtStartSS, txtEndHH, txtEndMM, txtEndSS}
            AddHandler tb.KeyPress, AddressOf TimeBox_KeyPress
        Next

        ' Apply tooltips
        ApplyToolTips()

        ' Load help content
        LoadHelpContent(_config.UiLanguage)

        ' Initialize live translation tab
        PopulateLiveLanguageDropdowns()
        cboLiveDevice.Items.Add("Detecting devices...")
        cboLiveDevice.SelectedIndex = 0
        btnLiveStart.Enabled = False

        ' Enumerate audio devices in the background
        Dim pythonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python-embed", "python.exe")
        Task.Run(Sub()
                     Dim runner As New LiveStreamRunner()
                     Dim devices = runner.EnumerateDevicesAsync(pythonPath)
                     cboLiveDevice.BeginInvoke(Sub()
                                                   UpdateDeviceCombo(devices)
                                                   btnLiveStart.Enabled = True
                                               End Sub)
                 End Sub)

        ' Apply theme
        ApplyTheme(_config.Theme)

        ' Fix tab switching rendering (progress bars don't repaint correctly)
        AddHandler tabMain.SelectedIndexChanged, Sub(s, ev)
                                                     If tabMain.SelectedTab Is tabPageJob Then
                                                         pbOverall.Refresh()
                                                         pbChunk.Refresh()
                                                         grpProgress.Refresh()
                                                     End If
                                                 End Sub

        ' Check for updates in the background
        CheckForUpdatesAsync()

        ' Check for missing dependencies
        CheckDependenciesAsync()

        ' Update translation setup button
        UpdateTranslationButtonAsync()

        ' Wire up system tray
        AddHandler trayIcon.DoubleClick, Sub(s, ev) ShowFromTray()
        AddHandler trayMenuShow.Click, Sub(s, ev) ShowFromTray()
        AddHandler trayMenuExit.Click, Sub(s, ev) ExitApplication()

        ' Apply saved startup preference (first-run setup happens after dependency download)
        If _config.FirstRunComplete Then
            If _config.StartWithWindows Then RegisterStartup() Else UnregisterStartup()
        End If

        ' Auto-start subtitle server after form is fully shown
        AddHandler Me.Shown, Sub(s, ev) StartSubtitleServer()
    End Sub

    Private Sub RunFirstTimeSetup()
        ' Ask about starting with Windows
        Dim bootResult = MessageBox.Show(
            "Would you like Transcription Tools to start automatically when Windows starts?",
            "Startup Preference",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question)
        _config.StartWithWindows = (bootResult = DialogResult.Yes)
        If _config.StartWithWindows Then RegisterStartup() Else UnregisterStartup()

        ' Ask about firewall access (needed for phones to connect to subtitle server)
        Dim fwResult = MessageBox.Show(
            "Allow Transcription Tools to accept connections from other devices on your network?" & vbCrLf & vbCrLf &
            "This is needed for phones to display live subtitles. " &
            "Windows may ask for administrator permission.",
            "Network Access",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question)
        _config.AllowFirewall = (fwResult = DialogResult.Yes)

        _config.FirstRunComplete = True
        ConfigManager.Save(_config)
    End Sub

    Private Sub RegisterStartup()
        Try
            Using key = Registry.CurrentUser.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Run", True)
                key?.SetValue("TranscriptionTools", $"""{Application.ExecutablePath}""")
            End Using
        Catch
        End Try
    End Sub

    Private Sub UnregisterStartup()
        Try
            Using key = Registry.CurrentUser.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Run", True)
                key?.DeleteValue("TranscriptionTools", False)
            End Using
        Catch
        End Try
    End Sub

    Private Sub ShowFromTray()
        Me.Show()
        Me.WindowState = FormWindowState.Maximized
        Me.Activate()
    End Sub

    Private Sub ExitApplication()
        _exitForReal = True
        Me.Close()
    End Sub

    Private Async Sub CheckForUpdatesAsync()
        Dim update = Await Models.UpdateChecker.CheckForUpdateAsync()
        If update Is Nothing Then Return

        ' Try modular (app-only) update first, fall back to full installer
        Dim canModularUpdate = Not String.IsNullOrWhiteSpace(update.AppZipUrl)
        Dim needsWhisper = update.NeedsWhisperUpdate AndAlso Not String.IsNullOrWhiteSpace(update.WhisperZipUrl)

        Dim updateDesc = If(canModularUpdate AndAlso Not needsWhisper,
            "A small app update is available.",
            "An update is available.")

        Dim confirm = MessageBox.Show(
            $"{GetString("Msg_NewVersionAvailable")} {update.TagName}" & vbCrLf & vbCrLf &
            updateDesc & vbCrLf & GetString("Msg_DownloadUpdate"),
            GetString("Msg_UpdateAvailable"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information)
        If confirm <> DialogResult.Yes Then Return

        ' Modular update: download zips and extract in-place
        If canModularUpdate Then
            Try
                Await ApplyModularUpdateAsync(update)
                Return
            Catch ex As Exception
                ' Modular update failed — fall back to installer
                If String.IsNullOrWhiteSpace(update.InstallerUrl) Then
                    MessageBox.Show($"Update failed: {ex.Message}" & vbCrLf & vbCrLf &
                                   "Opening release page instead.",
                                   GetString("Msg_Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Process.Start(New ProcessStartInfo(update.HtmlUrl) With {.UseShellExecute = True})
                    Return
                End If
            End Try
        End If

        ' Full installer fallback
        If String.IsNullOrWhiteSpace(update.InstallerUrl) Then
            Process.Start(New ProcessStartInfo(update.HtmlUrl) With {.UseShellExecute = True})
            Return
        End If

        Try
            Dim tempPath = Path.Combine(Path.GetTempPath(), $"TranscriptionTools_Setup_{update.TagName}.exe")
            Using client As New Net.Http.HttpClient()
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TranscriptionTools-Updater")
                Using response = Await client.GetAsync(update.InstallerUrl)
                    response.EnsureSuccessStatusCode()
                    Using fs As New FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None)
                        Await response.Content.CopyToAsync(fs)
                    End Using
                End Using
            End Using

            Process.Start(New ProcessStartInfo(tempPath) With {.UseShellExecute = True})
            _exitForReal = True
            Application.Exit()
        Catch ex As Exception
            MessageBox.Show($"Update download failed: {ex.Message}" & vbCrLf & vbCrLf &
                           "Opening release page instead.",
                           GetString("Msg_Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Process.Start(New ProcessStartInfo(update.HtmlUrl) With {.UseShellExecute = True})
        End Try
    End Sub

    Private Async Function ApplyModularUpdateAsync(update As Models.UpdateInfo) As Task
        Dim appDir = AppDomain.CurrentDomain.BaseDirectory
        Dim tempDir = Path.Combine(Path.GetTempPath(), $"TranscriptionTools_Update_{update.TagName}")

        If Directory.Exists(tempDir) Then Directory.Delete(tempDir, True)
        Directory.CreateDirectory(tempDir)

        Using client As New Net.Http.HttpClient()
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TranscriptionTools-Updater")
            client.Timeout = TimeSpan.FromMinutes(10)

            ' Download app zip
            Dim appZipPath = Path.Combine(tempDir, "app.zip")
            Using response = Await client.GetAsync(update.AppZipUrl)
                response.EnsureSuccessStatusCode()
                Using fs As New FileStream(appZipPath, FileMode.Create, FileAccess.Write, FileShare.None)
                    Await response.Content.CopyToAsync(fs)
                End Using
            End Using

            ' Download whisper zip if needed
            Dim whisperZipPath = ""
            If update.NeedsWhisperUpdate AndAlso Not String.IsNullOrWhiteSpace(update.WhisperZipUrl) Then
                whisperZipPath = Path.Combine(tempDir, "whisper.zip")
                Using response = Await client.GetAsync(update.WhisperZipUrl)
                    response.EnsureSuccessStatusCode()
                    Using fs As New FileStream(whisperZipPath, FileMode.Create, FileAccess.Write, FileShare.None)
                        Await response.Content.CopyToAsync(fs)
                    End Using
                End Using
            End If

            ' Write a batch script that waits for us to exit, extracts, and relaunches
            Dim batchPath = Path.Combine(tempDir, "apply-update.cmd")
            Dim exeName = Path.GetFileName(Application.ExecutablePath)
            Dim whisperExtract = ""
            If Not String.IsNullOrEmpty(whisperZipPath) Then
                whisperExtract = $"powershell -NoProfile -Command ""Expand-Archive -Path '{whisperZipPath}' -DestinationPath '{appDir}whisper' -Force""" & vbCrLf
            End If

            Dim batchContent = $"@echo off
echo Waiting for Transcription Tools to close...
:wait
tasklist /FI ""PID eq %1"" 2>NUL | find ""%1"" >NUL
if not errorlevel 1 (
    timeout /t 1 /nobreak >NUL
    goto wait
)
echo Applying update...
powershell -NoProfile -Command ""Expand-Archive -Path '{appZipPath}' -DestinationPath '{appDir}' -Force""
{whisperExtract}echo Update complete. Launching...
start """" ""{Path.Combine(appDir, exeName)}""
del ""%~f0""
"
            IO.File.WriteAllText(batchPath, batchContent)

            ' Launch the batch script with our PID, then exit
            Dim myPid = Process.GetCurrentProcess().Id
            Process.Start(New ProcessStartInfo("cmd.exe", $"/c ""{batchPath}"" {myPid}") With {
                .CreateNoWindow = True,
                .UseShellExecute = False
            })

            _exitForReal = True
            Application.Exit()
        End Using
    End Function

    Private Async Sub CheckDependenciesAsync(Optional manualCheck As Boolean = False)
        Try
        If manualCheck Then
            btnCheckToolUpdates.Enabled = False
            btnCheckToolUpdates.Text = GetString("Msg_Checking")
            Cursor = Cursors.WaitCursor
        End If

        Dim toolsDir = AppDomain.CurrentDomain.BaseDirectory
        Dim mgr As New Models.DependencyManager(_config, toolsDir)

        Dim states = Await mgr.CheckAllToolsAsync()
        Dim missing = mgr.GetMissingTools(states)
        Dim updatable = mgr.GetUpdatableTools(states)

        If missing.Count > 0 Then
            Dim names = String.Join(vbCrLf, missing.Select(Function(s) "  - " & s.Name))
            Dim result = MessageBox.Show(
                GetString("Msg_MissingTools") & vbCrLf & vbCrLf &
                names & vbCrLf & vbCrLf &
                GetString("Msg_DownloadNow"),
                GetString("Msg_MissingDeps"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning)
            If result = DialogResult.Yes Then
                Await DownloadToolsAsync(mgr, missing)
            End If
        ElseIf updatable.Count > 0 Then
            Dim names = String.Join(vbCrLf, updatable.Select(Function(s) $"  - {s.Name}  ({s.InstalledVersion} -> {s.LatestVersion})"))
            Dim result = MessageBox.Show(
                GetString("Msg_UpdatesAvailable") & vbCrLf & vbCrLf &
                names & vbCrLf & vbCrLf &
                GetString("Msg_UpdateNow"),
                GetString("Msg_UpdatesTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information)
            If result = DialogResult.Yes Then
                Await DownloadToolsAsync(mgr, updatable)
            End If
        ElseIf manualCheck Then
            MessageBox.Show(GetString("Msg_AllUpToDate"), GetString("Msg_ToolCheck"), MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If

        Catch ex As Exception
            If manualCheck Then
                MessageBox.Show($"{GetString("Msg_ErrorCheckingDeps")} {ex.Message}", GetString("Msg_Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Finally
            If manualCheck Then
                btnCheckToolUpdates.Enabled = True
                btnCheckToolUpdates.Text = GetString("Btn_CheckToolUpdates")
                Cursor = Cursors.Default
            End If
        End Try

        ' Run first-time setup after dependencies are ready
        If Not manualCheck AndAlso Not _config.FirstRunComplete Then
            RunFirstTimeSetup()
        End If
    End Sub

    Private Async Function DownloadToolsAsync(mgr As Models.DependencyManager, tools As List(Of Models.ToolState)) As Task
        Dim progressForm As New Form() With {
            .Text = "Downloading Tools",
            .Size = New Drawing.Size(450, 160),
            .StartPosition = FormStartPosition.CenterParent,
            .FormBorderStyle = FormBorderStyle.FixedDialog,
            .MaximizeBox = False,
            .MinimizeBox = False
        }
        Dim lblStatus As New Label() With {
            .Text = "Preparing...",
            .Location = New Drawing.Point(15, 15),
            .Size = New Drawing.Size(400, 20)
        }
        Dim pbDownload As New ProgressBar() With {
            .Location = New Drawing.Point(15, 45),
            .Size = New Drawing.Size(400, 25),
            .Style = ProgressBarStyle.Continuous
        }
        Dim lblProgress As New Label() With {
            .Text = "",
            .Location = New Drawing.Point(15, 78),
            .Size = New Drawing.Size(400, 20)
        }
        progressForm.Controls.AddRange({lblStatus, pbDownload, lblProgress})
        progressForm.Show(Me)

        Try
            For i = 0 To tools.Count - 1
                Dim tool = tools(i)
                lblStatus.Text = $"Downloading {tool.Name} ({i + 1}/{tools.Count})..."
                pbDownload.Value = 0

                Dim progress As New Progress(Of (downloaded As Long, total As Long))(
                    Sub(p)
                        If p.total > 0 Then
                            Dim pct = CInt(p.downloaded * 100 \ p.total)
                            pbDownload.Value = Math.Min(pct, 100)
                            Dim dlMB = (p.downloaded / 1048576.0).ToString("F1")
                            Dim totalMB = (p.total / 1048576.0).ToString("F1")
                            lblProgress.Text = $"{dlMB} MB / {totalMB} MB"
                        Else
                            Dim dlMB = (p.downloaded / 1048576.0).ToString("F1")
                            lblProgress.Text = $"{dlMB} MB downloaded"
                        End If
                    End Sub)

                Await mgr.DownloadToolAsync(tool, progress)
            Next

            MessageBox.Show(GetString("Msg_DownloadComplete"), GetString("Msg_DownloadCompleteTitle"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information)

            ' Update config paths to point to the tools directory
            UpdateConfigPaths(mgr.ToolsDirectory)

        Catch ex As Exception
            MessageBox.Show($"{GetString("Msg_DownloadFailed")} {ex.Message}", GetString("Msg_DownloadError"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            progressForm.Close()
            progressForm.Dispose()
        End Try
    End Function

    Private Sub UpdateConfigPaths(toolsDir As String)
        Dim whisperDir = IO.Path.Combine(toolsDir, "whisper")
        _config.PathWhisper = IO.Path.Combine(whisperDir, "whisper-cli.exe")
        _config.PathYtdlp = IO.Path.Combine(toolsDir, "yt-dlp.exe")
        _config.PathFfmpeg = IO.Path.Combine(toolsDir, "ffmpeg.exe")
        _config.PathFfprobe = IO.Path.Combine(toolsDir, "ffprobe.exe")
        _config.PathModel = IO.Path.Combine(toolsDir, "ggml-large-v3.bin")
        _config.PathModelAudio = IO.Path.Combine(toolsDir, "ggml-large-v3.bin")
        _config.PathSubtitleEdit = IO.Path.Combine(toolsDir, "SubtitleEdit", "SubtitleEdit.exe")
        _config.PathOutputRoot = toolsDir
        Models.ConfigManager.Save(_config)
        LoadConfigToUi()
        PopulateModelDropdown()

        ' Re-enumerate audio devices
        Dim pythonPath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python-embed", "python.exe")
        btnLiveStart.Enabled = False
        Task.Run(Sub()
                     Dim runner As New LiveStreamRunner()
                     Dim devices = runner.EnumerateDevicesAsync(pythonPath2)
                     cboLiveDevice.BeginInvoke(Sub()
                                                   UpdateDeviceCombo(devices)
                                                   btnLiveStart.Enabled = True
                                               End Sub)
                 End Sub)
    End Sub

    Private Sub btnCheckToolUpdates_Click(sender As Object, e As EventArgs) Handles btnCheckToolUpdates.Click
        CheckDependenciesAsync(manualCheck:=True)
    End Sub

    Private Sub PopulateLanguageDropdowns()
        cboInputLanguage.Items.Clear()
        cboOutputLanguage.Items.Clear()
        For Each lang In _whisperLanguages
            cboInputLanguage.Items.Add(lang)
            cboOutputLanguage.Items.Add(lang)
        Next
    End Sub

    Private Sub PopulateModelDropdown()
        cboModel.Items.Clear()
        Try
            Dim modelDir = Path.GetDirectoryName(AppConfig.ResolvePath(_config.PathModel))
            If Not String.IsNullOrWhiteSpace(modelDir) AndAlso Directory.Exists(modelDir) Then
                For Each binFile In Directory.GetFiles(modelDir, "ggml-*.bin")
                    cboModel.Items.Add(Path.GetFileName(binFile))
                Next
            End If
        Catch
        End Try

        ' Select the model for the current mode
        Dim currentModel = If(cboMode.SelectedIndex = 0, _config.PathModelAudio, _config.PathModel) ' Audio File mode uses separate model
        Dim currentName = Path.GetFileName(currentModel)
        SelectComboItem(cboModel, currentName)

        ' If model not in list, add it
        If cboModel.SelectedIndex < 0 AndAlso Not String.IsNullOrWhiteSpace(currentName) Then
            cboModel.Items.Add(currentName)
            SelectComboItem(cboModel, currentName)
        End If
    End Sub

    Private Sub PopulateUiLanguageDropdown()
        cboUiLanguage.Items.Clear()
        For Each uiLoc In _uiLocales
            cboUiLanguage.Items.Add(uiLoc.Name)
        Next
    End Sub

#Region "Config <-> UI Binding"

    Private Sub LoadConfigToUi()
        ' Paths tab
        txtPathWhisper.Text = _config.PathWhisper
        txtPathYtdlp.Text = _config.PathYtdlp
        txtPathFfmpeg.Text = _config.PathFfmpeg
        txtPathFfprobe.Text = _config.PathFfprobe
        txtPathModel.Text = _config.PathModel
        txtPathModelAudio.Text = _config.PathModelAudio
        txtPathFasterWhisper.Text = _config.PathFasterWhisperModel
        txtPathNllbModel.Text = _config.TranslationModelPath
        txtPathOutputRoot.Text = _config.PathOutputRoot
        txtYtdlpFormat.Text = _config.YtdlpFormat

        ' Settings tab
        SelectComboByValue(cboUiLanguage, _config.UiLanguage, _uiLocales)
        SelectComboItem(cboTheme, _config.Theme)

        ' Output formats
        chkSrt.Checked = _config.OutputSrt
        chkVtt.Checked = _config.OutputVtt
        chkTxt.Checked = _config.OutputTxt
        chkJson.Checked = _config.OutputJson
        chkCsv.Checked = _config.OutputCsv
        chkLrc.Checked = _config.OutputLrc

        ' Language on main tab
        SelectComboItem(cboInputLanguage, _config.Language)
        SelectComboItem(cboOutputLanguage, _config.OutputLanguage)

        ' Subtitle Server
        nudServerPort.Value = Math.Max(nudServerPort.Minimum, Math.Min(nudServerPort.Maximum, _config.SubtitleServerPort))
        If String.IsNullOrEmpty(_config.SubtitleBgColor) OrElse Not _config.SubtitleBgColor.StartsWith("#") Then _config.SubtitleBgColor = "#000000"
        If String.IsNullOrEmpty(_config.SubtitleFgColor) OrElse Not _config.SubtitleFgColor.StartsWith("#") Then _config.SubtitleFgColor = "#FFFFFF"
        Try : btnSubtitleBg.BackColor = ColorTranslator.FromHtml(_config.SubtitleBgColor) : Catch : btnSubtitleBg.BackColor = Drawing.Color.Black : End Try
        Try : btnSubtitleFg.BackColor = ColorTranslator.FromHtml(_config.SubtitleFgColor) : Catch : btnSubtitleFg.BackColor = Drawing.Color.White : End Try
        ' Apply subtitle colors to live output textbox
        Try : rtbLiveOutput.BackColor = ColorTranslator.FromHtml(_config.SubtitleBgColor) : Catch : rtbLiveOutput.BackColor = Drawing.Color.Black : End Try
        Try : rtbLiveOutput.ForeColor = ColorTranslator.FromHtml(_config.SubtitleFgColor) : Catch : rtbLiveOutput.ForeColor = Drawing.Color.White : End Try

        ' Apply subtitle font settings
        If Not String.IsNullOrWhiteSpace(_config.SubtitleFontFamily) Then
            Dim idx = cboSubtitleFont.Items.IndexOf(_config.SubtitleFontFamily)
            If idx >= 0 Then cboSubtitleFont.SelectedIndex = idx
        End If
        nudSubtitleSize.Value = CDec(Math.Max(nudSubtitleSize.Minimum, Math.Min(nudSubtitleSize.Maximum, _config.SubtitleFontSize)))
        chkSubtitleBold.Checked = _config.SubtitleFontBold
        ApplyLiveOutputFont()
    End Sub

    Private Sub SaveUiToConfig()
        If _config Is Nothing OrElse _isInitializing Then Return
        ' Paths
        _config.PathWhisper = txtPathWhisper.Text
        _config.PathYtdlp = txtPathYtdlp.Text
        _config.PathFfmpeg = txtPathFfmpeg.Text
        _config.PathFfprobe = txtPathFfprobe.Text
        _config.PathModel = txtPathModel.Text
        _config.PathModelAudio = txtPathModelAudio.Text
        _config.PathFasterWhisperModel = txtPathFasterWhisper.Text
        _config.TranslationModelPath = txtPathNllbModel.Text
        _config.PathOutputRoot = txtPathOutputRoot.Text
        _config.YtdlpFormat = txtYtdlpFormat.Text

        ' Settings
        If cboUiLanguage.SelectedIndex >= 0 AndAlso cboUiLanguage.SelectedIndex < _uiLocales.Length Then
            _config.UiLanguage = _uiLocales(cboUiLanguage.SelectedIndex).Code
        End If
        If cboTheme.SelectedItem IsNot Nothing Then _config.Theme = cboTheme.SelectedItem.ToString()

        ' Output formats
        _config.OutputSrt = chkSrt.Checked
        _config.OutputVtt = chkVtt.Checked
        _config.OutputTxt = chkTxt.Checked
        _config.OutputJson = chkJson.Checked
        _config.OutputCsv = chkCsv.Checked
        _config.OutputLrc = chkLrc.Checked

        ' Language (sync both dropdowns)
        If cboInputLanguage.SelectedItem IsNot Nothing Then
            _config.Language = cboInputLanguage.SelectedItem.ToString()
        End If
        If cboOutputLanguage.SelectedItem IsNot Nothing Then
            _config.OutputLanguage = cboOutputLanguage.SelectedItem.ToString()
        End If

        ' Subtitle Server
        _config.SubtitleServerPort = CInt(nudServerPort.Value)
        ' Subtitle colors are saved directly from color dialog handlers, not here

        ' Subtitle font
        _config.SubtitleFontFamily = If(cboSubtitleFont.SelectedItem?.ToString(), "Segoe UI")
        _config.SubtitleFontSize = CSng(nudSubtitleSize.Value)
        _config.SubtitleFontBold = chkSubtitleBold.Checked

        ConfigManager.Save(_config)
    End Sub

    Private Sub SelectComboItem(cbo As ComboBox, value As String)
        For i = 0 To cbo.Items.Count - 1
            If cbo.Items(i).ToString().Equals(value, StringComparison.OrdinalIgnoreCase) Then
                cbo.SelectedIndex = i
                Return
            End If
        Next
        If cbo.Items.Count > 0 Then cbo.SelectedIndex = 0
    End Sub

    Private Sub SelectComboByValue(cbo As ComboBox, code As String, locales As (Code As String, Name As String)())
        For i = 0 To locales.Length - 1
            If locales(i).Code.Equals(code, StringComparison.OrdinalIgnoreCase) Then
                If i < cbo.Items.Count Then cbo.SelectedIndex = i
                Return
            End If
        Next
        If cbo.Items.Count > 0 Then cbo.SelectedIndex = 0
    End Sub

#End Region

#Region "Localization"

    Private Sub ApplyLocale()
        Try
            tabPageJob.Text = GetString("Tab_Main")
            tabPagePaths.Text = GetString("Tab_Paths")
            tabPageSettings.Text = GetString("Tab_Settings")
            tabPageLive.Text = GetString("Tab_Live")
            tabPageHelp.Text = GetString("Tab_Help")

            lblMode.Text = GetString("Lbl_Mode")
            grpInput.Text = GetString("Grp_Input")
            lblUrl.Text = If(cboMode.SelectedIndex = 0, GetString("Lbl_AudioFile"), GetString("Lbl_Url"))
            btnBrowseFile.Text = If(cboMode.SelectedIndex = 0, GetString("Btn_BrowseAudio"), GetString("Btn_BrowseFile"))
            lblStartTime.Text = GetString("Lbl_StartTime")
            lblEndTime.Text = GetString("Lbl_EndTime")
            lblOutputDir.Text = GetString("Lbl_OutputDir")
            btnBrowseOutput.Text = GetString("Btn_BrowseOutput")
            lblInputLanguage.Text = GetString("Lbl_InputLanguage")
            lblOutputLanguage.Text = GetString("Lbl_OutputLanguage")
            lblModel.Text = GetString("Lbl_Model")

            grpOutputFormats.Text = GetString("Grp_OutputFormats")
            grpProgress.Text = GetString("Grp_Progress")
            btnStart.Text = GetString("Btn_Start")
            btnCancel.Text = GetString("Btn_Cancel")
            btnOpenOutput.Text = GetString("Btn_OpenOutputFolder")
            btnOpenSubtitleEdit.Text = GetString("Btn_SubtitleEdit")
            lnkPreviewSrt.Text = GetString("Lnk_PreviewSrt")

            lblPathWhisper.Text = GetString("Lbl_PathWhisper")
            lblPathYtdlp.Text = GetString("Lbl_PathYtdlp")
            lblPathFfmpeg.Text = GetString("Lbl_PathFfmpeg")
            lblPathFfprobe.Text = GetString("Lbl_PathFfprobe")
            lblPathModel.Text = GetString("Lbl_PathModel")
            lblPathModelAudio.Text = GetString("Lbl_PathModelAudio")
            lblPathOutputRoot.Text = GetString("Lbl_PathOutputRoot")
            lblYtdlpFormat.Text = GetString("Lbl_YtdlpFormat")
            btnVerifyPaths.Text = GetString("Btn_VerifyPaths")
            lnkDownloadModels.Text = GetString("Lnk_DownloadModels")
            grpPaths.Text = GetString("Grp_Paths")

            btnClearLog.Text = GetString("Btn_ClearLog")
            btnCopyLog.Text = GetString("Btn_CopyLog")

            grpSettings.Text = GetString("Grp_AppSettings")
            lblUiLanguage.Text = GetString("Lbl_UiLanguage")
            lblTheme.Text = GetString("Lbl_Theme")
            btnResetSettings.Text = GetString("Btn_ResetSettings")
            btnCheckToolUpdates.Text = GetString("Btn_CheckToolUpdates")

            ' Live tab
            grpLiveInput.Text = GetString("Grp_LiveInput")
            lblLiveDevice.Text = GetString("Lbl_LiveDevice")
            btnRefreshDevices.Text = GetString("Btn_RefreshDevices")
            lblLiveInputLang.Text = GetString("Lbl_LiveInputLang")
            btnLiveStart.Text = GetString("Btn_LiveStart")
            btnLiveStop.Text = GetString("Btn_LiveStop")
            btnLiveSave.Text = GetString("Btn_LiveSave")
            btnLiveClear.Text = GetString("Btn_LiveClearLog")
            btnLiveSaveLog.Text = GetString("Btn_LiveSaveLog")
            tabPageLiveClients.Text = GetString("Tab_LiveOutput")
            tabPageLiveLog.Text = GetString("Tab_LiveLog")

            ' Subtitle Server tab
            tabPageServer.Text = GetString("Tab_Server")
            grpServerSettings.Text = GetString("Grp_ServerSettings")
            lblServerPort.Text = GetString("Lbl_ServerPort")
            btnServerStart.Text = GetString("Btn_ServerStart")
            btnServerStop.Text = GetString("Btn_ServerStop")
            btnServerRestart.Text = GetString("Btn_ServerRestart")
            btnServerSimulate.Text = GetString("Btn_Simulate")
            btnServerSimStop.Text = GetString("Btn_SimStop")
            lblSubtitleBg.Text = GetString("Lbl_SubtitleBg")
            lblSubtitleFg.Text = GetString("Lbl_SubtitleFg")
            grpServerInfo.Text = GetString("Grp_ServerInfo")
            btnCopyUrl.Text = GetString("Btn_CopyUrl")

            If Not _isRunning Then
                lblStepStatus.Text = GetString("Msg_Ready")
            End If
        Catch
            ' Fallback silently if resource not found
        End Try
    End Sub

    Private Sub ApplyToolTips()
        tipMain.SetToolTip(txtUrl, GetString("Tip_Url"))
        tipMain.SetToolTip(cboInputLanguage, GetString("Tip_InputLanguage"))
        tipMain.SetToolTip(cboOutputLanguage, GetString("Tip_OutputLanguage"))
    End Sub

    Private Function GetString(key As String) As String
        Try
            Dim val = _resMgr.GetString(key)
            Return If(val, key)
        Catch
            Return key
        End Try
    End Function

#End Region

#Region "Help"

    Private Sub LoadHelpContent(langCode As String)
        Try
            Dim appDir = AppDomain.CurrentDomain.BaseDirectory
            Dim helpDir = Path.Combine(appDir, "Help")
            Dim helpFile = Path.Combine(helpDir, $"help.{langCode}.rtf")

            ' Fall back to English if locale-specific help not found
            If Not File.Exists(helpFile) Then
                helpFile = Path.Combine(helpDir, "help.en.rtf")
            End If

            If File.Exists(helpFile) Then
                rtbHelp.LoadFile(helpFile, RichTextBoxStreamType.RichText)
            Else
                rtbHelp.Text = "Help file not found."
            End If
        Catch ex As Exception
            rtbHelp.Text = "Error loading help: " & ex.Message
        End Try
    End Sub

#End Region

#Region "Mode Switching"

    Private Sub cboMode_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboMode.SelectedIndexChanged
        If _config Is Nothing Then Return
        Dim isYouTubeLike = (cboMode.SelectedIndex <> 0) ' 1, 2, 3 are YouTube-like
        Dim isAudioFile = (cboMode.SelectedIndex = 0)

        ' Show/hide YouTube-specific controls (time controls for YouTube-like modes)
        lblStartTime.Visible = isYouTubeLike
        txtStartHH.Visible = isYouTubeLike
        lblStartColon1.Visible = isYouTubeLike
        txtStartMM.Visible = isYouTubeLike
        lblStartColon2.Visible = isYouTubeLike
        txtStartSS.Visible = isYouTubeLike
        lblEndTime.Visible = isYouTubeLike
        txtEndHH.Visible = isYouTubeLike
        lblEndColon1.Visible = isYouTubeLike
        txtEndMM.Visible = isYouTubeLike
        lblEndColon2.Visible = isYouTubeLike
        txtEndSS.Visible = isYouTubeLike
        btnResume.Visible = isYouTubeLike

        ' Show/hide output formats (only for modes that produce subtitles)
        Dim hasSubtitles = (cboMode.SelectedIndex = 0 OrElse cboMode.SelectedIndex = 3)
        grpOutputFormats.Visible = hasSubtitles

        ' Update label and button text
        lblUrl.Text = If(isAudioFile, GetString("Lbl_AudioFile"), GetString("Lbl_Url"))
        btnBrowseFile.Text = If(isAudioFile, GetString("Btn_BrowseAudio"), GetString("Btn_BrowseFile"))

        ' Switch model to the mode's default
        Dim modelName = Path.GetFileName(If(isAudioFile, _config.PathModelAudio, _config.PathModel))
        SelectComboItem(cboModel, modelName)
    End Sub

#End Region

#Region "Pipeline Execution"

    Private Async Sub btnStart_Click(sender As Object, e As EventArgs) Handles btnStart.Click
        If _isRunning Then Return

        ' Save current UI to config and set active model for current mode
        SaveUiToConfig()
        If cboMode.SelectedIndex = 0 Then ' Audio File mode
            _config.PathModel = _config.PathModelAudio
        End If

        _isRunning = True
        _cts = New CancellationTokenSource()
        _currentOutputDir = txtOutputDir.Text

        ' UI state
        btnStart.Enabled = False
        btnResume.Enabled = False
        btnCancel.Enabled = True
        btnOpenOutput.Enabled = False
        btnOpenSubtitleEdit.Enabled = False
        lnkPreviewSrt.Visible = False
        pbOverall.Value = 0
        pbChunk.Value = 0
        pbChunk.Visible = False

        ' Switch to Job tab (log is visible there)
        tabMain.SelectedTab = tabPageJob
        Application.DoEvents()

        Dim progress As New Progress(Of PipelineProgress)(
            Sub(p)
                lblStepStatus.Text = p.StatusMessage
                pbOverall.Value = Math.Min(100, p.OverallPercent)
                If p.ChunkTotal > 0 Then
                    pbChunk.Visible = True
                    pbChunk.Maximum = p.ChunkTotal
                    pbChunk.Value = Math.Min(p.ChunkTotal, p.ChunkDone)
                End If
            End Sub)

        Dim runner As New PipelineRunner(_config, progress, _cts.Token)
        AddHandler runner.LogMessage, Sub(s, entry) LogToRtb(entry.Message, entry.Level)

        Try
            Dim url = txtUrl.Text.Trim()
            Dim startTime = BuildTimeString(txtStartHH.Text, txtStartMM.Text, txtStartSS.Text)
            Dim endTime = BuildTimeString(txtEndHH.Text, txtEndMM.Text, txtEndSS.Text)

            Select Case cboMode.SelectedIndex
                Case 0 ' Audio File mode
                    Await runner.RunAudioFileAsync(url, _currentOutputDir)
                Case 1 ' YouTube / Audio Only mode
                    Await runner.RunExtractAudioAsync(url, startTime, endTime, _currentOutputDir)
                Case 2 ' YouTube / Download Only mode
                    Await runner.RunDownloadOnlyAsync(url, startTime, endTime, _currentOutputDir)
                Case Else ' YouTube / Subtitles mode
                    Await runner.RunAsync(url, startTime, endTime, _currentOutputDir)
            End Select

            lblStepStatus.Text = GetString("Msg_Done")
            pbOverall.Value = 100
            btnOpenOutput.Enabled = True
            btnOpenSubtitleEdit.Enabled = (cboMode.SelectedIndex = 0 OrElse cboMode.SelectedIndex = 3)
            lnkPreviewSrt.Visible = (cboMode.SelectedIndex = 0 OrElse cboMode.SelectedIndex = 3)
        Catch ex As OperationCanceledException
            lblStepStatus.Text = GetString("Msg_Cancelled")
            LogToRtb("Pipeline cancelled by user.", PipelineRunner.LogLevel.Err)
        Catch ex As PipelineException
            lblStepStatus.Text = GetString(ex.MessageKey)
            LogToRtb($"ERROR: {ex.Message}", PipelineRunner.LogLevel.Err)
            MessageBox.Show(ex.Message, GetString("Msg_PipelineError"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            lblStepStatus.Text = "Error"
            LogToRtb($"UNEXPECTED ERROR: {ex.Message}", PipelineRunner.LogLevel.Err)
            MessageBox.Show(ex.Message, GetString("Msg_UnexpectedError"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _isRunning = False
            btnStart.Enabled = True
            btnResume.Enabled = True
            btnCancel.Enabled = False
            pbChunk.Visible = False
        End Try
    End Sub

    Private Async Sub btnResume_Click(sender As Object, e As EventArgs) Handles btnResume.Click
        If _isRunning Then Return

        ' Let user pick an existing output folder
        Using dlg As New FolderBrowserDialog()
            dlg.Description = "Select an existing output folder to resume"
            If Not String.IsNullOrWhiteSpace(_config.PathOutputRoot) Then
                dlg.SelectedPath = AppConfig.ResolvePath(_config.PathOutputRoot)
            End If
            If dlg.ShowDialog() <> DialogResult.OK Then Return
            txtOutputDir.Text = dlg.SelectedPath
        End Using

        SaveUiToConfig()

        _isRunning = True
        _cts = New CancellationTokenSource()
        _currentOutputDir = txtOutputDir.Text

        ' UI state
        btnStart.Enabled = False
        btnResume.Enabled = False
        btnCancel.Enabled = True
        btnOpenOutput.Enabled = False
        lnkPreviewSrt.Visible = False
        pbOverall.Value = 0
        pbChunk.Value = 0
        pbChunk.Visible = False

        ' Switch to Job tab (log is visible there)
        tabMain.SelectedTab = tabPageJob
        Application.DoEvents()

        Dim progress As New Progress(Of PipelineProgress)(
            Sub(p)
                lblStepStatus.Text = p.StatusMessage
                pbOverall.Value = Math.Min(100, p.OverallPercent)
                If p.ChunkTotal > 0 Then
                    pbChunk.Visible = True
                    pbChunk.Maximum = p.ChunkTotal
                    pbChunk.Value = Math.Min(p.ChunkTotal, p.ChunkDone)
                End If
            End Sub)

        Dim runner As New PipelineRunner(_config, progress, _cts.Token)
        AddHandler runner.LogMessage, Sub(s, entry) LogToRtb(entry.Message, entry.Level)

        Try
            Dim startTime = BuildTimeString(txtStartHH.Text, txtStartMM.Text, txtStartSS.Text)
            Dim endTime = BuildTimeString(txtEndHH.Text, txtEndMM.Text, txtEndSS.Text)

            Select Case cboMode.SelectedIndex
                Case 1 ' YouTube / Audio Only mode
                    Await runner.RunExtractAudioAsync("", startTime, endTime, _currentOutputDir, resumeMode:=True)
                Case 2 ' YouTube / Download Only mode
                    Await runner.RunDownloadOnlyAsync("", startTime, endTime, _currentOutputDir, resumeMode:=True)
                Case Else ' YouTube / Subtitles mode
                    Await runner.RunAsync("", startTime, endTime, _currentOutputDir, resumeMode:=True)
            End Select

            lblStepStatus.Text = GetString("Msg_Done")
            pbOverall.Value = 100
            btnOpenOutput.Enabled = True
            lnkPreviewSrt.Visible = (cboMode.SelectedIndex = 3)
        Catch ex As OperationCanceledException
            lblStepStatus.Text = GetString("Msg_Cancelled")
            LogToRtb("Pipeline cancelled by user.", PipelineRunner.LogLevel.Err)
        Catch ex As PipelineException
            lblStepStatus.Text = GetString(ex.MessageKey)
            LogToRtb($"ERROR: {ex.Message}", PipelineRunner.LogLevel.Err)
            MessageBox.Show(ex.Message, GetString("Msg_PipelineError"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            lblStepStatus.Text = "Error"
            LogToRtb($"UNEXPECTED ERROR: {ex.Message}", PipelineRunner.LogLevel.Err)
            MessageBox.Show(ex.Message, GetString("Msg_UnexpectedError"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _isRunning = False
            btnStart.Enabled = True
            btnResume.Enabled = True
            btnCancel.Enabled = False
            pbChunk.Visible = False
        End Try
    End Sub

    Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        _cts?.Cancel()
    End Sub

#End Region

#Region "Logging"

    Private _logAutoScroll As Boolean = True

    Private Sub LogToRtb(message As String, level As PipelineRunner.LogLevel)
        If rtbLog.InvokeRequired Then
            rtbLog.BeginInvoke(Sub() LogToRtb(message, level))
            Return
        End If

        ' Skip verbose messages from the UI log to avoid flooding the RTB
        If level = PipelineRunner.LogLevel.Verbose Then Return

        Dim color As Drawing.Color
        Select Case level
            Case PipelineRunner.LogLevel.Success : color = Drawing.Color.DarkGreen
            Case PipelineRunner.LogLevel.Err : color = Drawing.Color.Red
            Case Else : color = Drawing.Color.Black
        End Select

        rtbLog.SelectionStart = rtbLog.TextLength
        rtbLog.SelectionLength = 0
        rtbLog.SelectionColor = color
        rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}")

        If _logAutoScroll Then
            rtbLog.ScrollToCaret()
        End If
    End Sub

    Private Sub rtbLog_VScroll(sender As Object, e As EventArgs) Handles rtbLog.VScroll
        ' Detect if user scrolled away from bottom
        Dim pos = rtbLog.GetPositionFromCharIndex(rtbLog.TextLength - 1)
        _logAutoScroll = pos.Y <= rtbLog.Height + 50
    End Sub

    Private Sub btnClearLog_Click(sender As Object, e As EventArgs) Handles btnClearLog.Click
        rtbLog.Clear()
    End Sub

    Private Sub btnCopyLog_Click(sender As Object, e As EventArgs) Handles btnCopyLog.Click
        If rtbLog.TextLength > 0 Then
            Clipboard.SetText(rtbLog.Text)
        End If
    End Sub

#End Region

#Region "Browse Buttons"

    Private Sub btnBrowseFile_Click(sender As Object, e As EventArgs) Handles btnBrowseFile.Click
        Using dlg As New OpenFileDialog()
            If cboMode.SelectedIndex = 0 Then
                ' Audio File mode
                dlg.Filter = "Audio files|*.wav;*.mp3;*.ogg;*.flac;*.m4a;*.wma;*.aac;*.opus|All files|*.*"
                Dim resolvedRoot = AppConfig.ResolvePath(_config.PathOutputRoot)
                If Not String.IsNullOrWhiteSpace(resolvedRoot) AndAlso Directory.Exists(resolvedRoot) Then
                    dlg.InitialDirectory = resolvedRoot
                End If
            Else
                ' YouTube / Download Only / Extract Audio modes
                dlg.Filter = "Video/Audio files|*.mp4;*.mkv;*.avi;*.webm;*.wav;*.mp3;*.m4a;*.flac|All files|*.*"
            End If
            If dlg.ShowDialog() = DialogResult.OK Then
                txtUrl.Text = dlg.FileName
            End If
        End Using
    End Sub

    Private Sub btnBrowseOutput_Click(sender As Object, e As EventArgs) Handles btnBrowseOutput.Click
        Using dlg As New FolderBrowserDialog()
            dlg.SelectedPath = txtOutputDir.Text
            If dlg.ShowDialog() = DialogResult.OK Then
                txtOutputDir.Text = dlg.SelectedPath
            End If
        End Using
    End Sub

    Private Sub btnOpenOutput_Click(sender As Object, e As EventArgs) Handles btnOpenOutput.Click
        If Directory.Exists(_currentOutputDir) Then
            Process.Start("explorer.exe", _currentOutputDir)
        End If
    End Sub

    Private Sub lnkPreviewSrt_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles lnkPreviewSrt.LinkClicked
        Dim srtPath = FindOutputSrt()
        If srtPath IsNot Nothing Then
            Process.Start(New ProcessStartInfo(srtPath) With {.UseShellExecute = True})
        End If
    End Sub

    Private Sub btnOpenSubtitleEdit_Click(sender As Object, e As EventArgs) Handles btnOpenSubtitleEdit.Click
        Dim srtPath = FindOutputSrt()
        If srtPath Is Nothing Then
            MessageBox.Show(GetString("Msg_NoSrtFound"), "Subtitle Edit", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        Dim subtitleEditPath = AppConfig.ResolvePath(_config.PathSubtitleEdit)
        If Not File.Exists(subtitleEditPath) Then
            MessageBox.Show($"{GetString("Msg_SubtitleEditNotFound")} {subtitleEditPath}", "Subtitle Edit", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        Process.Start(subtitleEditPath, $"""{srtPath}""")
    End Sub

    Private Function FindOutputSrt() As String
        If String.IsNullOrWhiteSpace(_currentOutputDir) OrElse Not Directory.Exists(_currentOutputDir) Then Return Nothing

        ' YouTube mode: preview.srt
        Dim previewSrt = Path.Combine(_currentOutputDir, "preview.srt")
        If File.Exists(previewSrt) Then Return previewSrt

        ' Audio File mode: first .srt in output dir
        Dim srtFiles = Directory.GetFiles(_currentOutputDir, "*.srt")
        If srtFiles.Length > 0 Then Return srtFiles(0)

        Return Nothing
    End Function

    Private Sub BrowseForExe(textBox As TextBox)
        Using dlg As New OpenFileDialog()
            dlg.Filter = "Executable files|*.exe|All files|*.*"
            If Not String.IsNullOrWhiteSpace(textBox.Text) Then
                Try
                    dlg.InitialDirectory = Path.GetDirectoryName(textBox.Text)
                Catch
                End Try
            End If
            If dlg.ShowDialog() = DialogResult.OK Then
                textBox.Text = dlg.FileName
            End If
        End Using
    End Sub

    Private Sub BrowseForFile(textBox As TextBox, filter As String)
        Using dlg As New OpenFileDialog()
            dlg.Filter = filter
            If Not String.IsNullOrWhiteSpace(textBox.Text) Then
                Try
                    dlg.InitialDirectory = Path.GetDirectoryName(textBox.Text)
                Catch
                End Try
            End If
            If dlg.ShowDialog() = DialogResult.OK Then
                textBox.Text = dlg.FileName
            End If
        End Using
    End Sub

    Private Sub BrowseForFolder(textBox As TextBox)
        Using dlg As New FolderBrowserDialog()
            If Not String.IsNullOrWhiteSpace(textBox.Text) Then
                dlg.SelectedPath = textBox.Text
            End If
            If dlg.ShowDialog() = DialogResult.OK Then
                textBox.Text = dlg.SelectedPath
            End If
        End Using
    End Sub

    Private Sub btnBrowseWhisper_Click(sender As Object, e As EventArgs) Handles btnBrowseWhisper.Click
        BrowseForExe(txtPathWhisper)
    End Sub


    Private Sub btnBrowseYtdlp_Click(sender As Object, e As EventArgs) Handles btnBrowseYtdlp.Click
        BrowseForExe(txtPathYtdlp)
    End Sub

    Private Sub btnBrowseFfmpeg_Click(sender As Object, e As EventArgs) Handles btnBrowseFfmpeg.Click
        BrowseForExe(txtPathFfmpeg)
    End Sub

    Private Sub btnBrowseFfprobe_Click(sender As Object, e As EventArgs) Handles btnBrowseFfprobe.Click
        BrowseForExe(txtPathFfprobe)
    End Sub

    Private Sub btnBrowseModel_Click(sender As Object, e As EventArgs) Handles btnBrowseModel.Click
        BrowseForFile(txtPathModel, "Model files|*.bin|All files|*.*")
    End Sub

    Private Sub btnBrowseModelAudio_Click(sender As Object, e As EventArgs) Handles btnBrowseModelAudio.Click
        BrowseForFile(txtPathModelAudio, "Model files|*.bin|All files|*.*")
    End Sub

    Private Sub btnBrowseOutputRoot_Click(sender As Object, e As EventArgs) Handles btnBrowseOutputRoot.Click
        BrowseForFolder(txtPathOutputRoot)
    End Sub

#End Region

#Region "Paths Verification"

    Private Sub btnVerifyPaths_Click(sender As Object, e As EventArgs) Handles btnVerifyPaths.Click
        Dim results As New List(Of String)
        Dim allOk = True

        Dim checks = {
            ("whisper-cli", txtPathWhisper.Text),
            ("yt-dlp", txtPathYtdlp.Text),
            ("ffmpeg", txtPathFfmpeg.Text),
            ("ffprobe", txtPathFfprobe.Text),
            ("YouTube Model", txtPathModel.Text),
            ("Audio File Model", txtPathModelAudio.Text),
            ("faster-whisper Model", txtPathFasterWhisper.Text),
            ("NLLB Translation Model", txtPathNllbModel.Text),
            ("Output root", txtPathOutputRoot.Text)
        }

        Dim folderChecks = {"Output root", "faster-whisper Model", "NLLB Translation Model"}
        For Each chk In checks
            Dim exists As Boolean
            If folderChecks.Contains(chk.Item1) Then
                exists = Directory.Exists(AppConfig.ResolvePath(chk.Item2))
            Else
                exists = File.Exists(AppConfig.ResolvePath(chk.Item2))
            End If

            Dim status = If(exists, "OK", "NOT FOUND")
            If Not exists Then allOk = False
            results.Add($"{chk.Item1}: {status} - {chk.Item2}")
        Next

        Dim icon = If(allOk, MessageBoxIcon.Information, MessageBoxIcon.Warning)
        MessageBox.Show(String.Join(Environment.NewLine, results), GetString("Msg_PathVerification"), MessageBoxButtons.OK, icon)
    End Sub

    Private Sub lnkDownloadModels_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles lnkDownloadModels.LinkClicked
        Process.Start(New ProcessStartInfo("https://huggingface.co/ggerganov/whisper.cpp/tree/main") With {.UseShellExecute = True})
    End Sub

#End Region

#Region "Settings Events"

    Private Sub cboUiLanguage_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboUiLanguage.SelectedIndexChanged
        If _config Is Nothing Then Return
        If cboUiLanguage.SelectedIndex >= 0 AndAlso cboUiLanguage.SelectedIndex < _uiLocales.Length Then
            Dim code = _uiLocales(cboUiLanguage.SelectedIndex).Code
            _config.UiLanguage = code
            Thread.CurrentThread.CurrentUICulture = New CultureInfo(code)
            ApplyLocale()
            ApplyToolTips()
            LoadHelpContent(code)
            SaveUiToConfig()
        End If
    End Sub

    Private Sub cboTheme_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboTheme.SelectedIndexChanged
        If cboTheme.SelectedItem IsNot Nothing Then
            ApplyTheme(cboTheme.SelectedItem.ToString())
            SaveUiToConfig()
        End If
    End Sub

    Private Sub cboModel_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboModel.SelectedIndexChanged
        If _config Is Nothing OrElse cboModel.SelectedItem Is Nothing Then Return
        Dim modelDir = Path.GetDirectoryName(AppConfig.ResolvePath(_config.PathModel))
        If String.IsNullOrWhiteSpace(modelDir) Then modelDir = Path.GetDirectoryName(AppConfig.ResolvePath(_config.PathModelAudio))
        Dim fullPath = Path.Combine(If(modelDir, ""), cboModel.SelectedItem.ToString())

        Select Case cboMode.SelectedIndex
            Case 0 ' Audio File mode
                _config.PathModelAudio = fullPath
                txtPathModelAudio.Text = fullPath
            Case 3 ' YouTube / Subtitles mode
                _config.PathModel = fullPath
                txtPathModel.Text = fullPath
            Case Else ' YouTube / Download Only, YouTube / Audio Only - no model needed
                Return
        End Select

        ConfigManager.Save(_config)
    End Sub

    Private Sub btnResetSettings_Click(sender As Object, e As EventArgs) Handles btnResetSettings.Click
        Dim result = MessageBox.Show(GetString("Msg_ResetAll"),
                                      GetString("Msg_ResetAllTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If result = DialogResult.Yes Then
            ConfigManager.Reset()
            _config = New AppConfig()
            LoadConfigToUi()
        End If
    End Sub

#End Region

#Region "Theme"

    Private Sub ApplyTheme(theme As String)
        Dim backColor, foreColor, controlBack As Drawing.Color

        Select Case theme.ToLower()
            Case "dark"
                backColor = Drawing.Color.FromArgb(30, 30, 30)
                foreColor = Drawing.Color.FromArgb(220, 220, 220)
                controlBack = Drawing.Color.FromArgb(45, 45, 48)
            Case "light"
                backColor = Drawing.Color.White
                foreColor = Drawing.Color.Black
                controlBack = Drawing.Color.White
            Case Else ' System
                backColor = Drawing.SystemColors.Control
                foreColor = Drawing.SystemColors.ControlText
                controlBack = Drawing.SystemColors.Window
        End Select

        Me.BackColor = backColor
        Me.ForeColor = foreColor
        ApplyThemeToControls(Me, backColor, foreColor, controlBack)
    End Sub

    Private Sub ApplyThemeToControls(parent As Control, backColor As Drawing.Color, foreColor As Drawing.Color, controlBack As Drawing.Color)
        For Each ctrl As Control In parent.Controls
            ' Skip live output — uses subtitle colors
            If ctrl Is rtbLiveOutput Then Continue For

            ctrl.ForeColor = foreColor

            If TypeOf ctrl Is TextBox OrElse TypeOf ctrl Is MaskedTextBox OrElse
               TypeOf ctrl Is RichTextBox OrElse TypeOf ctrl Is ComboBox OrElse
               TypeOf ctrl Is NumericUpDown Then
                ctrl.BackColor = controlBack
            ElseIf TypeOf ctrl Is TabControl Then
                ' Don't change tab control background
            ElseIf TypeOf ctrl Is Button Then
                ' Skip color picker buttons
                If ctrl Is btnSubtitleBg OrElse ctrl Is btnSubtitleFg Then
                    Continue For
                End If
                ' Keep buttons readable
                If backColor = Drawing.SystemColors.Control Then
                    ctrl.BackColor = Drawing.SystemColors.Control
                Else
                    ctrl.BackColor = Drawing.Color.FromArgb(
                        Math.Min(255, backColor.R + 30),
                        Math.Min(255, backColor.G + 30),
                        Math.Min(255, backColor.B + 30))
                End If
            Else
                ctrl.BackColor = backColor
            End If

            If ctrl.HasChildren Then
                ApplyThemeToControls(ctrl, backColor, foreColor, controlBack)
            End If
        Next
    End Sub

#End Region

#Region "Live Translation"

    Private Sub PopulateLiveLanguageDropdowns()
        cboLiveInputLang.Items.Clear()
        For Each lang In _whisperLanguages
            cboLiveInputLang.Items.Add(lang)
        Next

        SelectComboItem(cboLiveInputLang, _config.Language)
    End Sub

    Private Sub cboLiveDevice_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboLiveDevice.SelectedIndexChanged
        If cboLiveDevice.SelectedItem IsNot Nothing Then
            Dim txt = cboLiveDevice.SelectedItem.ToString()
            Dim colonIdx = txt.IndexOf(":"c)
            If colonIdx > 0 Then
                _config.LastLiveDeviceId = txt.Substring(0, colonIdx).Trim()
                ConfigManager.Save(_config)
            End If
        End If
    End Sub

    Private Sub cboLiveInputLang_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboLiveInputLang.SelectedIndexChanged
        If _isInitializing OrElse cboLiveInputLang.SelectedItem Is Nothing Then Return
        _config.Language = cboLiveInputLang.SelectedItem.ToString()
        SelectComboItem(cboInputLanguage, _config.Language)
        ConfigManager.Save(_config)
    End Sub

    Private Sub btnRefreshDevices_Click(sender As Object, e As EventArgs) Handles btnRefreshDevices.Click
        SaveUiToConfig()
        cboLiveDevice.Items.Clear()
        cboLiveDevice.Items.Add("Detecting devices...")
        cboLiveDevice.SelectedIndex = 0
        cboLiveDevice.Enabled = False
        btnRefreshDevices.Enabled = False

        Dim pythonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python-embed", "python.exe")

        Task.Run(Sub()
                     Dim runner As New LiveStreamRunner()
                     Dim devices = runner.EnumerateDevicesAsync(pythonPath)
                     cboLiveDevice.BeginInvoke(Sub()
                                                   UpdateDeviceCombo(devices)
                                                   cboLiveDevice.Enabled = True
                                                   btnRefreshDevices.Enabled = True
                                               End Sub)
                 End Sub)
    End Sub

    Private Async Sub btnLiveStart_Click(sender As Object, e As EventArgs) Handles btnLiveStart.Click
        SaveUiToConfig()

        ' Check live dependencies (Python, packages, model)
        Dim toolsDir = AppDomain.CurrentDomain.BaseDirectory
        Dim mgr As New Models.DependencyManager(_config, toolsDir)
        Dim liveDeps = Await mgr.CheckLiveDepsAsync()

        If Not liveDeps.pythonOk OrElse Not liveDeps.depsOk OrElse Not liveDeps.modelOk Then
            If _isRemoteCommand Then
                AppendServerLog("ERROR: Live transcription dependencies not installed.")
                Return
            End If

            MessageBox.Show(
                GetString("Msg_LiveDepsMissing"),
                GetString("Msg_DepsMissing"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning)
            Return
        End If

        ' Get device ID from combo selection
        Dim deviceId = 0
        If cboLiveDevice.SelectedItem IsNot Nothing Then
            Dim deviceText = cboLiveDevice.SelectedItem.ToString()
            Dim colonIdx = deviceText.IndexOf(":"c)
            If colonIdx > 0 Then
                Integer.TryParse(deviceText.Substring(0, colonIdx).Trim(), deviceId)
            End If
        End If

        ' Get input language
        Dim inputLang = "auto"
        If cboLiveInputLang.SelectedItem IsNot Nothing Then inputLang = cboLiveInputLang.SelectedItem.ToString()

        Dim translateToEn = False

        _liveRunner = New LiveStreamRunner()

        ' In-progress line refinement (show in app only, not broadcast to subtitle clients)
        AddHandler _liveRunner.OutputLineUpdated, Sub(s, line)
                                                      If rtbLiveOutput.InvokeRequired Then
                                                          rtbLiveOutput.BeginInvoke(Sub() ReplaceLiveLastLine(line))
                                                      Else
                                                          ReplaceLiveLastLine(line)
                                                      End If
                                                  End Sub

        ' Committed final line - finalize the in-progress line with a newline
        AddHandler _liveRunner.OutputLineCommitted, Sub(s, line)
                                                        TranslateAndBroadcastAsync(line)
                                                        If rtbLiveOutput.InvokeRequired Then
                                                            rtbLiveOutput.BeginInvoke(Sub() CommitLiveLine())
                                                        Else
                                                            CommitLiveLine()
                                                        End If
                                                    End Sub

        AddHandler _liveRunner.ErrorReceived, Sub(s, line)
                                                  If rtbLiveOutput.InvokeRequired Then
                                                      rtbLiveOutput.BeginInvoke(Sub() AppendLiveText(line, Drawing.Color.Gray))
                                                  Else
                                                      AppendLiveText(line, Drawing.Color.Gray)
                                                  End If
                                              End Sub

        AppendLiveText($"Starting live transcription (device {deviceId}, lang={inputLang})...", Drawing.Color.Yellow)
        AppendLiveText($"faster-whisper + Silero VAD (port {_config.LiveServerPort})", Drawing.Color.Gray)
        Dim fgColor As Drawing.Color = Drawing.Color.White
        Try : fgColor = ColorTranslator.FromHtml(_config.SubtitleFgColor) : Catch : End Try
        AppendLiveText("", fgColor)

        _liveRunner.Start(_config, deviceId, inputLang, translateToEn)

        If _liveRunner.IsRunning Then
            btnLiveStart.Enabled = False
            btnLiveStop.Enabled = True
            grpLiveInput.Enabled = False
            UpdateLiveRunningStatus()
        End If
    End Sub

    Private Sub btnLiveStop_Click(sender As Object, e As EventArgs) Handles btnLiveStop.Click
        If _liveRunner IsNot Nothing AndAlso _liveRunner.IsRunning Then
            _liveRunner.Stop()
            AppendLiveText("", Drawing.Color.Gray)
            AppendLiveText("Live transcription stopped.", Drawing.Color.Yellow)
        End If

        btnLiveStart.Enabled = True
        btnLiveStop.Enabled = False
        grpLiveInput.Enabled = True
        UpdateLiveRunningStatus()
    End Sub

    Private _isRemoteCommand As Boolean = False

    Private Sub HandleRemoteCommand(command As String)
        Dim isLiveActive = _liveRunner IsNot Nothing AndAlso _liveRunner.IsRunning
        Dim isSimActive = _simCts IsNot Nothing AndAlso Not _simCts.IsCancellationRequested

        _isRemoteCommand = True
        Try
            Select Case command
                Case "start"
                    If Not isLiveActive Then
                        AppendServerLog("Remote command: START")
                        btnLiveStart_Click(Nothing, EventArgs.Empty)
                    End If
                Case "stop"
                    If isLiveActive Then
                        AppendServerLog("Remote command: STOP")
                        btnLiveStop_Click(Nothing, EventArgs.Empty)
                    End If
                    If isSimActive Then
                        AppendServerLog("Remote command: STOP (simulation)")
                        btnServerSimStop_Click(Nothing, EventArgs.Empty)
                    End If
                Case "restart"
                    AppendServerLog("Remote command: RESTART")
                    If isLiveActive Then
                        btnLiveStop_Click(Nothing, EventArgs.Empty)
                    End If
                    btnLiveStart_Click(Nothing, EventArgs.Empty)
                Case "simulate"
                    If Not isSimActive Then
                        AppendServerLog("Remote command: SIMULATE")
                        btnServerSimulate_Click(Nothing, EventArgs.Empty)
                    Else
                        AppendServerLog("Remote command: STOP SIMULATE")
                        btnServerSimStop_Click(Nothing, EventArgs.Empty)
                    End If
                Case "clear"
                    AppendServerLog("Remote command: CLEAR")
                    rtbLiveOutput.Clear()
                    _subtitleServer?.BroadcastClear()
            End Select
        Finally
            _isRemoteCommand = False
        End Try
        UpdateLiveRunningStatus()
    End Sub

    Private Sub UpdateLiveRunningStatus()
        If _subtitleServer IsNot Nothing Then
            _subtitleServer.IsLiveRunning = _liveRunner IsNot Nothing AndAlso _liveRunner.IsRunning
            _subtitleServer.IsSimulating = _simCts IsNot Nothing AndAlso Not _simCts.IsCancellationRequested
        End If
    End Sub

    Private Sub btnLiveSave_Click(sender As Object, e As EventArgs) Handles btnLiveSave.Click
        Using dlg As New SaveFileDialog()
            dlg.Filter = "Text files|*.txt|All files|*.*"
            dlg.DefaultExt = "txt"
            dlg.FileName = $"live_transcript_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
            Dim resolvedOutput = AppConfig.ResolvePath(_config.PathOutputRoot)
            If Not String.IsNullOrWhiteSpace(resolvedOutput) Then
                dlg.InitialDirectory = resolvedOutput
            End If
            If dlg.ShowDialog() = DialogResult.OK Then
                ' Save the content of the live output box
                File.WriteAllText(dlg.FileName, rtbLiveOutput.Text, System.Text.Encoding.UTF8)
                MessageBox.Show($"{GetString("Msg_TranscriptSaved")}{Environment.NewLine}{dlg.FileName}", GetString("Msg_Saved"), MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        End Using
    End Sub

    Private Sub tabLiveOutput_SelectedIndexChanged(sender As Object, e As EventArgs) Handles tabLiveOutput.SelectedIndexChanged
        If tabLiveOutput.SelectedTab Is tabPageLiveLog Then
            ' Defer refresh until after tab switch completes and control is laid out
            BeginInvoke(Sub()
                            rtbLiveOutput.SelectionStart = rtbLiveOutput.TextLength
                            rtbLiveOutput.SelectionLength = 0
                            rtbLiveOutput.ScrollToCaret()
                            rtbLiveOutput.Refresh()
                        End Sub)
        End If
    End Sub

    Private Sub btnLiveClear_Click(sender As Object, e As EventArgs) Handles btnLiveClear.Click
        rtbLiveOutput.Clear()
    End Sub

    Private Sub btnLiveSaveLog_Click(sender As Object, e As EventArgs) Handles btnLiveSaveLog.Click
        Using dlg As New SaveFileDialog()
            dlg.Filter = "Text files|*.txt|All files|*.*"
            dlg.DefaultExt = "txt"
            dlg.FileName = $"live_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
            Dim resolvedOutput = AppConfig.ResolvePath(_config.PathOutputRoot)
            If Directory.Exists(resolvedOutput) Then dlg.InitialDirectory = resolvedOutput
            If dlg.ShowDialog() = DialogResult.OK Then
                File.WriteAllText(dlg.FileName, rtbLiveOutput.Text, System.Text.Encoding.UTF8)
                MessageBox.Show($"Log saved.{Environment.NewLine}{dlg.FileName}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        End Using
    End Sub

    Private Sub AppendLiveText(text As String, color As Drawing.Color)
        rtbLiveOutput.SelectionStart = rtbLiveOutput.TextLength
        rtbLiveOutput.SelectionLength = 0
        rtbLiveOutput.SelectionColor = color
        rtbLiveOutput.AppendText(text & Environment.NewLine)
        rtbLiveOutput.ScrollToCaret()
    End Sub

    Private Sub CommitLiveLine()
        ' Just add a newline after the current in-progress line to make it permanent
        rtbLiveOutput.SelectionStart = rtbLiveOutput.TextLength
        rtbLiveOutput.SelectionLength = 0
        rtbLiveOutput.AppendText(Environment.NewLine)
        rtbLiveOutput.ScrollToCaret()
    End Sub


    Private Async Sub TranslateAndBroadcastAsync(commitData As String)
        ' Parse detected language from tab-separated format: "lang\ttext"
        Dim detectedLang = ""
        Dim line = commitData
        Dim tabIdx = commitData.IndexOf(vbTab)
        If tabIdx > 0 Then
            detectedLang = commitData.Substring(0, tabIdx)
            line = commitData.Substring(tabIdx + 1)
        End If

        WriteDebugLog($"[WHISPER COMMIT] [{detectedLang}] {line}")

        ' Use detected language for NLLB source (convert whisper ISO code to NLLB code)
        Dim sourceLang = If(Not String.IsNullOrEmpty(detectedLang), WhisperToNllbCode(detectedLang), GetCurrentSourceNllbLang())
        Dim sourceShort = NllbToShortCode(sourceLang)

        ' Check if translation is available
        Dim targets = _subtitleServer?.GetActiveTranslationLanguages()
        Dim activeTargets = If(targets IsNot Nothing, String.Join(",", targets), "none")
        targets?.Remove(sourceLang)

        Dim translationReady = targets IsNot Nothing AndAlso targets.Count > 0 AndAlso
            _translationService IsNot Nothing AndAlso _translationService.IsRunning AndAlso _translationService.IsModelLoaded

        WriteDebugLog($"[BROADCAST] targets=[{activeTargets}] source={sourceLang} skip={translationReady}")

        ' No translation clients or translation not ready — send to everyone immediately
        If Not translationReady Then
            _subtitleServer?.BroadcastCommit(line, skipTranslationClients:=False, lang:=sourceShort)
            Return
        End If

        ' Filter garbage commits — send to non-translation clients only
        If IsGarbageCommit(line) Then
            WriteDebugLog($"[FILTERED] garbage commit skipped for translation")
            _subtitleServer?.BroadcastCommit(line, skipTranslationClients:=True, lang:=sourceShort)
            Return
        End If

        ' Translate for all target languages
        Dim sw = Diagnostics.Stopwatch.StartNew()
        Dim translations As New Dictionary(Of String, String)()
        Try
            Dim result = Await _translationService.TranslateAsync(line, sourceLang, targets)
            If result IsNot Nothing Then
                For Each kvp In result
                    translations(kvp.Key) = kvp.Value
                    WriteDebugLog($"[TRANSLATION {kvp.Key}] {kvp.Value}")
                Next
            End If
        Catch ex As Exception
            WriteDebugLog($"[TRANSLATE ERROR] {ex.Message}")
        End Try

        ' Include original text for source-language clients
        translations(sourceLang) = line

        ' Re-check: translate any new languages that appeared during the await
        Dim currentTargets = _subtitleServer?.GetActiveTranslationLanguages()
        If currentTargets IsNot Nothing Then
            Dim missing As New List(Of String)
            For Each t In currentTargets
                If Not translations.ContainsKey(t) Then missing.Add(t)
            Next
            If missing.Count > 0 Then
                WriteDebugLog($"[CATCH-UP] new targets: {String.Join(",", missing)}")
                Try
                    Dim extra = Await _translationService.TranslateAsync(line, sourceLang, missing)
                    If extra IsNot Nothing Then
                        For Each kvp In extra
                            translations(kvp.Key) = kvp.Value
                        Next
                    End If
                Catch ex As Exception
                    WriteDebugLog($"[CATCH-UP ERROR] {ex.Message}")
                End Try
            End If
        End If

        sw.Stop()
        WriteDebugLog($"[ROUND TRIP] {sw.ElapsedMilliseconds}ms")

        ' Build lang tags for display
        Dim langTags As New Dictionary(Of String, String)
        For Each kvp In translations
            langTags(kvp.Key) = NllbToShortCode(kvp.Key)
        Next

        ' Single atomic broadcast — sends the right text to each client based on their
        ' current language. No two-phase race condition.
        _subtitleServer?.BroadcastCommitTranslated(line, sourceShort, translations, langTags)
    End Sub

    Private Shared Function NllbToShortCode(nllbCode As String) As String
        If String.IsNullOrEmpty(nllbCode) Then Return "??"
        ' Extract first part before underscore and uppercase it (e.g. "eng_Latn" → "EN", "spa_Latn" → "ES")
        Dim prefix = nllbCode.Split("_"c)(0).ToUpperInvariant()
        Select Case prefix
            Case "ENG" : Return "EN"
            Case "SPA" : Return "ES"
            Case "FRA" : Return "FR"
            Case "DEU" : Return "DE"
            Case "POR" : Return "PT"
            Case "ITA" : Return "IT"
            Case "CAT" : Return "CA"
            Case "RON" : Return "RO"
            Case "NLD" : Return "NL"
            Case "POL" : Return "PL"
            Case "RUS" : Return "RU"
            Case "UKR" : Return "UK"
            Case "ZHO" : Return "ZH"
            Case "JPN" : Return "JA"
            Case "KOR" : Return "KO"
            Case "ARB" : Return "AR"
            Case "SWE" : Return "SV"
            Case "NOB" : Return "NO"
            Case "DAN" : Return "DA"
            Case "FIN" : Return "FI"
            Case "HUN" : Return "HU"
            Case "CES" : Return "CS"
            Case "SLK" : Return "SK"
            Case "SLV" : Return "SL"
            Case "HRV" : Return "HR"
            Case "SRP" : Return "SR"
            Case "BUL" : Return "BG"
            Case "ELL" : Return "EL"
            Case "TUR" : Return "TR"
            Case "LIT" : Return "LT"
            Case "LVS" : Return "LV"
            Case "EST" : Return "ET"
            Case "AFR" : Return "AF"
            Case "AMH" : Return "AM"
            Case "HYE" : Return "HY"
            Case "AZJ" : Return "AZ"
            Case "EUS" : Return "EU"
            Case "BEL" : Return "BE"
            Case "BEN" : Return "BN"
            Case "BOS" : Return "BS"
            Case "CYM" : Return "CY"
            Case "PES" : Return "FA"
            Case "GLG" : Return "GL"
            Case "KAT" : Return "KA"
            Case "GUJ" : Return "GU"
            Case "HAT" : Return "HT"
            Case "HAU" : Return "HA"
            Case "HEB" : Return "HE"
            Case "HIN" : Return "HI"
            Case "ISL" : Return "IS"
            Case "IND" : Return "ID"
            Case "JAV" : Return "JW"
            Case "KAN" : Return "KN"
            Case "KAZ" : Return "KK"
            Case "KHM" : Return "KM"
            Case "LAO" : Return "LO"
            Case "LTZ" : Return "LB"
            Case "MKD" : Return "MK"
            Case "ZSM" : Return "MS"
            Case "MAL" : Return "ML"
            Case "MLT" : Return "MT"
            Case "MRI" : Return "MI"
            Case "MAR" : Return "MR"
            Case "KHK" : Return "MN"
            Case "MYA" : Return "MY"
            Case "NPI" : Return "NE"
            Case "PBT" : Return "PS"
            Case "PAN" : Return "PA"
            Case "SND" : Return "SD"
            Case "SIN" : Return "SI"
            Case "SNA" : Return "SN"
            Case "SOM" : Return "SO"
            Case "SUN" : Return "SU"
            Case "SWH" : Return "SW"
            Case "TGL" : Return "TL"
            Case "TGK" : Return "TG"
            Case "TAM" : Return "TA"
            Case "TAT" : Return "TT"
            Case "TEL" : Return "TE"
            Case "THA" : Return "TH"
            Case "TUK" : Return "TK"
            Case "URD" : Return "UR"
            Case "UZN" : Return "UZ"
            Case "VIE" : Return "VI"
            Case "YOR" : Return "YO"
            Case "ZUL" : Return "ZU"
            Case Else : Return prefix.Substring(0, Math.Min(2, prefix.Length))
        End Select
    End Function

    Private Shared Function WhisperToNllbCode(whisperLang As String) As String
        ' Map whisper ISO 639-1 codes to NLLB codes
        Select Case whisperLang.ToLowerInvariant()
            Case "en" : Return "eng_Latn"
            Case "es" : Return "spa_Latn"
            Case "fr" : Return "fra_Latn"
            Case "de" : Return "deu_Latn"
            Case "pt" : Return "por_Latn"
            Case "it" : Return "ita_Latn"
            Case "ca" : Return "cat_Latn"
            Case "ro" : Return "ron_Latn"
            Case "nl" : Return "nld_Latn"
            Case "pl" : Return "pol_Latn"
            Case "ru" : Return "rus_Cyrl"
            Case "uk" : Return "ukr_Cyrl"
            Case "zh" : Return "zho_Hans"
            Case "ja" : Return "jpn_Jpan"
            Case "ko" : Return "kor_Hang"
            Case "ar" : Return "arb_Arab"
            Case "sv" : Return "swe_Latn"
            Case "no" : Return "nob_Latn"
            Case "da" : Return "dan_Latn"
            Case "fi" : Return "fin_Latn"
            Case "hu" : Return "hun_Latn"
            Case "cs" : Return "ces_Latn"
            Case "sk" : Return "slk_Latn"
            Case "sl" : Return "slv_Latn"
            Case "hr" : Return "hrv_Latn"
            Case "sr" : Return "srp_Cyrl"
            Case "bg" : Return "bul_Cyrl"
            Case "el" : Return "ell_Grek"
            Case "tr" : Return "tur_Latn"
            Case "lt" : Return "lit_Latn"
            Case "lv" : Return "lvs_Latn"
            Case "et" : Return "est_Latn"
            Case "af" : Return "afr_Latn"
            Case "am" : Return "amh_Ethi"
            Case "hy" : Return "hye_Armn"
            Case "az" : Return "azj_Latn"
            Case "eu" : Return "eus_Latn"
            Case "be" : Return "bel_Cyrl"
            Case "bn" : Return "ben_Beng"
            Case "bs" : Return "bos_Latn"
            Case "cy" : Return "cym_Latn"
            Case "fa" : Return "pes_Arab"
            Case "gl" : Return "glg_Latn"
            Case "ka" : Return "kat_Geor"
            Case "gu" : Return "guj_Gujr"
            Case "ht" : Return "hat_Latn"
            Case "ha" : Return "hau_Latn"
            Case "he" : Return "heb_Hebr"
            Case "hi" : Return "hin_Deva"
            Case "is" : Return "isl_Latn"
            Case "id" : Return "ind_Latn"
            Case "jw" : Return "jav_Latn"
            Case "kn" : Return "kan_Knda"
            Case "kk" : Return "kaz_Cyrl"
            Case "km" : Return "khm_Khmr"
            Case "lo" : Return "lao_Laoo"
            Case "lb" : Return "ltz_Latn"
            Case "mk" : Return "mkd_Cyrl"
            Case "ms" : Return "zsm_Latn"
            Case "ml" : Return "mal_Mlym"
            Case "mt" : Return "mlt_Latn"
            Case "mi" : Return "mri_Latn"
            Case "mr" : Return "mar_Deva"
            Case "mn" : Return "khk_Cyrl"
            Case "my" : Return "mya_Mymr"
            Case "ne" : Return "npi_Deva"
            Case "ps" : Return "pbt_Arab"
            Case "pa" : Return "pan_Guru"
            Case "sd" : Return "snd_Arab"
            Case "si" : Return "sin_Sinh"
            Case "sn" : Return "sna_Latn"
            Case "so" : Return "som_Latn"
            Case "su" : Return "sun_Latn"
            Case "sw" : Return "swh_Latn"
            Case "tl" : Return "tgl_Latn"
            Case "tg" : Return "tgk_Cyrl"
            Case "ta" : Return "tam_Taml"
            Case "tt" : Return "tat_Cyrl"
            Case "te" : Return "tel_Telu"
            Case "th" : Return "tha_Thai"
            Case "tk" : Return "tuk_Latn"
            Case "ur" : Return "urd_Arab"
            Case "uz" : Return "uzn_Latn"
            Case "vi" : Return "vie_Latn"
            Case "yo" : Return "yor_Latn"
            Case "zu" : Return "zul_Latn"
            Case Else : Return "eng_Latn"
        End Select
    End Function

    Private Shared Function IsGarbageCommit(line As String) As Boolean
        Dim trimmed = line.Trim().TrimEnd("."c).Trim()
        ' Empty or just punctuation
        If String.IsNullOrWhiteSpace(trimmed) Then Return True
        ' Single short word (whisper hallucination during silence)
        If trimmed.Length <= 3 AndAlso Not trimmed.Any(Function(c) Char.IsDigit(c)) Then Return True
        ' Known whisper artifacts
        If trimmed.StartsWith("[") AndAlso trimmed.EndsWith("]") Then Return True
        Return False
    End Function

    Private Shared Sub WriteDebugLog(msg As String)
        Try
            Dim logPath = IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pipeline-debug.log")
            IO.File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {msg}{Environment.NewLine}")
        Catch
        End Try
    End Sub

    Private Function GetCurrentSourceNllbLang() As String
        ' Fallback when faster-whisper doesn't provide a detected language
        Dim inputLang = ""
        If cboLiveInputLang.InvokeRequired Then
            inputLang = CStr(cboLiveInputLang.Invoke(Function() If(cboLiveInputLang.SelectedItem, "auto").ToString()))
        Else
            If cboLiveInputLang.SelectedItem IsNot Nothing Then inputLang = cboLiveInputLang.SelectedItem.ToString()
        End If

        If inputLang = "auto" Then inputLang = "es"
        Return TranslationService.WhisperToNllbLang(inputLang)
    End Function

    Private _translationSetupPrompted As Boolean = False

    Private Sub HandleActiveLanguagesChanged(sender As Object, e As EventArgs)
        Dim targets = _subtitleServer?.GetActiveTranslationLanguages()
        If targets Is Nothing OrElse targets.Count = 0 Then
            ' No translation clients — reset unload timer
            ResetTranslationUnloadTimer()
            Return
        End If

        ' Cancel unload timer if active
        _translationUnloadTimer?.Change(Timeout.Infinite, Timeout.Infinite)

        ' If translation service is already running, nothing to do — language routing is handled per-client
        If _translationService IsNot Nothing AndAlso _translationService.IsRunning Then Return

        ' Check if deps are installed first
        Dim deps = TranslationService.CheckDependenciesInstalled()
        If Not deps.pythonOk OrElse Not deps.depsOk OrElse Not deps.modelOk Then
            AppendServerLog("Translation dependencies not installed. Run dependency check from Settings tab.")
            Return
        End If
        StartTranslationService()
    End Sub

    Private Sub StartTranslationService()
        If Not _config.TranslationEnabled Then Return

        ' Clear debug logs on fresh start
        Try
            Dim logPath = IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pipeline-debug.log")
            IO.File.WriteAllText(logPath, $"=== Translation service started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}")
        Catch
        End Try

        _translationService = New TranslationService()
        AddHandler _translationService.StatusChanged, Sub(s, msg)
                                                          AppendServerLog(msg)
                                                      End Sub

        Dim modelPath = _config.TranslationModelPath
        Dim port = _config.TranslationPort
        Dim device = _config.TranslationDevice
        Dim glossaryPath = _config.TranslationGlossaryPath

        _translationService.Start(port, modelPath, device, glossaryPath)
        AppendServerLog("Translation service starting...")
    End Sub

    Private Sub StopTranslationService()
        _translationUnloadTimer?.Dispose()
        _translationUnloadTimer = Nothing
        _translationService?.Stop()
        _translationService = Nothing
    End Sub

    Private Sub ResetTranslationUnloadTimer()
        If _translationService Is Nothing OrElse Not _translationService.IsRunning Then Return

        Dim minutes = _config.TranslationUnloadMinutes
        If minutes <= 0 Then Return

        _translationUnloadTimer?.Dispose()
        _translationUnloadTimer = New System.Threading.Timer(
            Sub(state)
                Dim targets = _subtitleServer?.GetActiveTranslationLanguages()
                If targets Is Nothing OrElse targets.Count = 0 Then
                    AppendServerLog($"No translation clients for {minutes} min, unloading model...")
                    _translationService?.UnloadModelAsync().Wait()
                End If
            End Sub, Nothing, TimeSpan.FromMinutes(minutes), Timeout.InfiniteTimeSpan)
    End Sub

    Private Sub btnSetupTranslation_Click(sender As Object, e As EventArgs) Handles btnSetupTranslation.Click
        CheckDependenciesAsync(manualCheck:=True)
    End Sub

    Private Async Sub UpdateTranslationButtonAsync()
        Dim deps = Await Task.Run(Function() TranslationService.CheckDependenciesInstalled())
        UpdateTranslationButton(deps)
    End Sub

    Private Sub UpdateTranslationButton(Optional deps As (pythonOk As Boolean, depsOk As Boolean, modelOk As Boolean)? = Nothing)
        If deps Is Nothing Then
            deps = TranslationService.CheckDependenciesInstalled()
        End If
        Dim d = deps.Value
        If d.pythonOk AndAlso d.depsOk AndAlso d.modelOk Then
            btnSetupTranslation.Text = "Translation Ready"
            btnSetupTranslation.Enabled = True
        Else
            btnSetupTranslation.Text = GetString("Btn_CheckToolUpdates")
            btnSetupTranslation.Enabled = True
        End If
    End Sub

    Private Sub ReplaceLiveLastLine(text As String)
        ' Find the start of the last line and replace it
        Dim rtb = rtbLiveOutput
        Dim txt = rtb.Text
        Dim lastNewline = txt.LastIndexOf(vbLf)
        If lastNewline >= 0 Then
            rtb.SelectionStart = lastNewline + 1
            rtb.SelectionLength = txt.Length - lastNewline - 1
        Else
            rtb.SelectionStart = 0
            rtb.SelectionLength = txt.Length
        End If
        Try
            rtb.SelectionColor = ColorTranslator.FromHtml(_config.SubtitleFgColor)
        Catch
            rtb.SelectionColor = Drawing.Color.White
        End Try
        rtb.SelectedText = text
        rtb.ScrollToCaret()
    End Sub

    Private Sub UpdateDeviceCombo(devices As List(Of String))
        ' Prefer saved device from config, fall back to current selection
        Dim previousId = _config.LastLiveDeviceId
        If String.IsNullOrEmpty(previousId) AndAlso cboLiveDevice.SelectedItem IsNot Nothing Then
            Dim txt = cboLiveDevice.SelectedItem.ToString()
            Dim colonIdx = txt.IndexOf(":"c)
            If colonIdx > 0 Then previousId = txt.Substring(0, colonIdx).Trim()
        End If

        cboLiveDevice.Items.Clear()
        For Each d In devices
            cboLiveDevice.Items.Add(d)
        Next

        ' Try to re-select the previously selected device
        Dim found = False
        If previousId IsNot Nothing AndAlso previousId.Length > 0 Then
            For i = 0 To cboLiveDevice.Items.Count - 1
                If cboLiveDevice.Items(i).ToString().StartsWith(previousId & ":") Then
                    cboLiveDevice.SelectedIndex = i
                    found = True
                    Exit For
                End If
            Next
        End If
        If Not found AndAlso cboLiveDevice.Items.Count > 0 Then cboLiveDevice.SelectedIndex = 0
    End Sub

#End Region

#Region "Subtitle Server"

    Private Function GetLocalIpAddress() As String
        Try
            For Each addr In System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList
                If addr.AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork Then
                    Dim ip = addr.ToString()
                    If Not ip.StartsWith("127.") Then Return ip
                End If
            Next
        Catch
        End Try
        Return "127.0.0.1"
    End Function

    Private Sub UpdateServerUi(running As Boolean)
        btnServerStart.Enabled = Not running
        btnServerStop.Enabled = running
        btnServerRestart.Enabled = running
        btnServerSimulate.Enabled = running
        nudServerPort.Enabled = Not running
        btnCopyUrl.Enabled = running

        If running Then
            Dim ip = GetLocalIpAddress()
            Dim url = $"http://{ip}:{_subtitleServer.Port}"
            lblServerStatus.Text = "Status: Running"
            lblServerStatus.ForeColor = Drawing.Color.Green
            lblServerUrl.Text = $"URL: {url}"
        Else
            lblServerStatus.Text = "Status: Stopped"
            lblServerStatus.ForeColor = Drawing.SystemColors.ControlText
            lblServerUrl.Text = "URL: (not running)"
            lblServerClients.Text = "Connected clients: 0"
            btnServerSimStop.Enabled = False
        End If
    End Sub

    Private Sub AppendServerLog(text As String)
        If rtbServerLog.InvokeRequired Then
            rtbServerLog.BeginInvoke(Sub() AppendServerLog(text))
            Return
        End If
        rtbServerLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}")
        rtbServerLog.ScrollToCaret()
    End Sub

    Private Sub btnServerStart_Click(sender As Object, e As EventArgs) Handles btnServerStart.Click
        SaveUiToConfig()
        StartSubtitleServer()
    End Sub

    Private Sub StartSubtitleServer()
        Dim port = CInt(nudServerPort.Value)
        If _config.AllowFirewall Then EnsureFirewallRule(port)

        _subtitleServer = New SubtitleServer()
        _subtitleServer.BgColor = _config.SubtitleBgColor
        _subtitleServer.FgColor = _config.SubtitleFgColor
        AddHandler _subtitleServer.StatusChanged, Sub(s, msg)
                                                      AppendServerLog(msg)
                                                      If Me.InvokeRequired Then
                                                          Me.BeginInvoke(Sub()
                                                                             If _subtitleServer IsNot Nothing Then
                                                                                 lblServerClients.Text = $"Connected clients: {_subtitleServer.ConnectedClients}"
                                                                             End If
                                                                         End Sub)
                                                      Else
                                                          If _subtitleServer IsNot Nothing Then
                                                              lblServerClients.Text = $"Connected clients: {_subtitleServer.ConnectedClients}"
                                                          End If
                                                      End If
                                                  End Sub

        AddHandler _subtitleServer.RemoteCommand, Sub(s, cmd)
                                                      Me.BeginInvoke(Sub() HandleRemoteCommand(cmd))
                                                  End Sub

        AddHandler _subtitleServer.ActiveLanguagesChanged, AddressOf HandleActiveLanguagesChanged
        AddHandler _subtitleServer.LogMessage, Sub(s, msg)
                                                   WriteDebugLog(msg)
                                               End Sub

        Try
            _subtitleServer.Start(port, _config.AllowFirewall)
            UpdateServerUi(True)
            AppendServerLog($"Subtitle server started on port {port}")
            NavigateLivePreview(port)
            Dim localIp = GetLocalIpAddress()
            AppendServerLog($"Phones should open: https://{localIp}:{_subtitleServer.HttpsPort}")
            AppendServerLog($"(Accept the certificate warning on first visit)")
        Catch ex As Exception
            AppendServerLog($"ERROR: {ex.Message}")
            AppendServerLog("Tip: Try running as Administrator, or use a different port.")
            _subtitleServer = Nothing
        End Try
    End Sub

    Private Sub NavigateLivePreview(port As Integer)
        Try
            wvLiveClients.Source = New Uri($"http://127.0.0.1:{port}/")
        Catch ex As Exception
            AppendServerLog($"Live preview: {ex.Message}")
        End Try
    End Sub

    Private Sub btnServerStop_Click(sender As Object, e As EventArgs) Handles btnServerStop.Click
        StopSimulation()
        _subtitleServer?.Stop()
        _subtitleServer = Nothing
        UpdateServerUi(False)
        AppendServerLog("Subtitle server stopped.")
    End Sub

    Private Sub btnServerRestart_Click(sender As Object, e As EventArgs) Handles btnServerRestart.Click
        StopSimulation()
        _subtitleServer?.Stop()
        _subtitleServer = Nothing
        AppendServerLog("Restarting server...")
        btnServerStart_Click(sender, e)
    End Sub

    Private Sub btnSubtitleBg_Click(sender As Object, e As EventArgs) Handles btnSubtitleBg.Click
        Using dlg As New ColorDialog()
            dlg.Color = btnSubtitleBg.BackColor
            dlg.FullOpen = True
            If dlg.ShowDialog() = DialogResult.OK Then
                btnSubtitleBg.BackColor = dlg.Color
                rtbLiveOutput.BackColor = dlg.Color
                _config.SubtitleBgColor = ColorToHex(dlg.Color)
                ConfigManager.Save(_config)
                If _subtitleServer IsNot Nothing Then _subtitleServer.BgColor = _config.SubtitleBgColor
            End If
        End Using
    End Sub

    Private Sub btnSubtitleFg_Click(sender As Object, e As EventArgs) Handles btnSubtitleFg.Click
        Using dlg As New ColorDialog()
            dlg.Color = btnSubtitleFg.BackColor
            dlg.FullOpen = True
            If dlg.ShowDialog() = DialogResult.OK Then
                btnSubtitleFg.BackColor = dlg.Color
                rtbLiveOutput.ForeColor = dlg.Color
                _config.SubtitleFgColor = ColorToHex(dlg.Color)
                ConfigManager.Save(_config)
                If _subtitleServer IsNot Nothing Then _subtitleServer.FgColor = _config.SubtitleFgColor
            End If
        End Using
    End Sub

    Private Sub cboSubtitleFont_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboSubtitleFont.SelectedIndexChanged
        ApplyLiveOutputFont()
    End Sub

    Private Sub nudSubtitleSize_ValueChanged(sender As Object, e As EventArgs) Handles nudSubtitleSize.ValueChanged
        ApplyLiveOutputFont()
    End Sub

    Private Sub chkSubtitleBold_CheckedChanged(sender As Object, e As EventArgs) Handles chkSubtitleBold.CheckedChanged
        ApplyLiveOutputFont()
    End Sub

    Private Sub ApplyLiveOutputFont()
        If rtbLiveOutput Is Nothing OrElse nudSubtitleSize Is Nothing OrElse cboSubtitleFont Is Nothing OrElse chkSubtitleBold Is Nothing Then Return
        Dim fontName = If(cboSubtitleFont.SelectedItem?.ToString(), "Segoe UI")
        Dim fontSize = CSng(nudSubtitleSize.Value)
        Dim style = If(chkSubtitleBold.Checked, Drawing.FontStyle.Bold, Drawing.FontStyle.Regular)
        rtbLiveOutput.Font = New Drawing.Font(fontName, fontSize, style)

        If Not _isInitializing Then
            _config.SubtitleFontFamily = fontName
            _config.SubtitleFontSize = fontSize
            _config.SubtitleFontBold = chkSubtitleBold.Checked
            ConfigManager.Save(_config)
        End If
    End Sub

    Private Sub btnCopyUrl_Click(sender As Object, e As EventArgs) Handles btnCopyUrl.Click
        If _subtitleServer IsNot Nothing AndAlso _subtitleServer.IsRunning Then
            Dim url = $"http://{GetLocalIpAddress()}:{_subtitleServer.Port}"
            Clipboard.SetText(url)
            AppendServerLog("URL copied to clipboard.")
        End If
    End Sub

    Private Sub btnServerSimulate_Click(sender As Object, e As EventArgs) Handles btnServerSimulate.Click
        If _subtitleServer Is Nothing OrElse Not _subtitleServer.IsRunning Then Return

        _simCts = New CancellationTokenSource()
        btnServerSimulate.Enabled = False
        btnServerSimStop.Enabled = True
        AppendServerLog("Simulation started - sending random text...")
        UpdateLiveRunningStatus()

        Dim ct = _simCts.Token
        Task.Run(
            Async Function()
                Dim rng As New Random()
                Dim verses = {
                    "For God so loved the world",
                    "that he gave his one and only Son,",
                    "that whoever believes in him",
                    "shall not perish",
                    "but have eternal life.",
                    "For God did not send his Son into the world",
                    "to condemn the world,",
                    "but to save the world through him.",
                    "Whoever believes in him is not condemned,",
                    "but whoever does not believe",
                    "stands condemned already",
                    "because they have not believed",
                    "in the name of God's one and only Son."
                }

                Dim verseIdx = 0
                While Not ct.IsCancellationRequested
                    Dim line = verses(verseIdx Mod verses.Length)
                    Dim words = line.Split(" "c)
                    Dim sentence As New System.Text.StringBuilder()
                    For w = 0 To words.Length - 1
                        If ct.IsCancellationRequested Then Exit For
                        If w > 0 Then sentence.Append(" ")
                        sentence.Append(words(w))
                        _subtitleServer?.BroadcastUpdate(sentence.ToString())
                        Await Task.Delay(rng.Next(150, 350), ct)
                    Next
                    If Not ct.IsCancellationRequested Then
                        TranslateAndBroadcastAsync(sentence.ToString())
                        Await Task.Delay(rng.Next(800, 1500), ct)
                    End If
                    verseIdx += 1
                End While
            End Function, ct).ContinueWith(
            Sub(t)
                If Me.InvokeRequired Then
                    Me.BeginInvoke(Sub()
                                       btnServerSimulate.Enabled = _subtitleServer IsNot Nothing AndAlso _subtitleServer.IsRunning
                                       btnServerSimStop.Enabled = False
                                   End Sub)
                Else
                    btnServerSimulate.Enabled = _subtitleServer IsNot Nothing AndAlso _subtitleServer.IsRunning
                    btnServerSimStop.Enabled = False
                End If
            End Sub)
    End Sub

    Private Sub btnServerSimStop_Click(sender As Object, e As EventArgs) Handles btnServerSimStop.Click
        StopSimulation()
        AppendServerLog("Simulation stopped.")
    End Sub

    Private Sub StopSimulation()
        _simCts?.Cancel()
        _simCts = Nothing
        UpdateLiveRunningStatus()
    End Sub

#End Region

    Private Shared Function BuildTimeString(hh As String, mm As String, ss As String) As String
        hh = hh.Trim()
        mm = mm.Trim()
        ss = ss.Trim()
        If hh.Length = 0 AndAlso mm.Length = 0 AndAlso ss.Length = 0 Then Return ""
        Return $"{hh.PadLeft(2, "0"c)}:{mm.PadLeft(2, "0"c)}:{ss.PadLeft(2, "0"c)}"
    End Function

    Private Sub TimeBox_KeyPress(sender As Object, e As KeyPressEventArgs)
        If Not Char.IsDigit(e.KeyChar) AndAlso Not Char.IsControl(e.KeyChar) Then
            e.Handled = True
        End If
    End Sub

    Private Sub KillOrphanedPythonProcesses()
        Try
            Dim pythonDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python-embed").ToLowerInvariant()
            For Each proc In Process.GetProcessesByName("python")
                Try
                    Dim exePath = proc.MainModule?.FileName
                    If exePath IsNot Nothing AndAlso exePath.ToLowerInvariant().StartsWith(pythonDir) Then
                        proc.Kill(True)
                        proc.WaitForExit(3000)
                    End If
                Catch
                    ' Access denied or already exited — ignore
                End Try
            Next
        Catch
        End Try
    End Sub

    Private Sub FormMain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        ' Always save settings
        SaveUiToConfig()

        If Not _exitForReal Then
            ' Minimize to system tray instead of closing
            e.Cancel = True
            Me.Hide()
            Return
        End If

        ' Real exit — clean up everything
        _cts?.Cancel()
        _liveRunner?.ShutdownServer()
        _simCts?.Cancel()
        StopTranslationService()
        _subtitleServer?.Stop()
        trayIcon.Visible = False
        trayIcon.Dispose()

        ' Kill any orphaned python-embed processes that survived graceful shutdown
        KillOrphanedPythonProcesses()

        ' Offer to clean up today's working folders
        Try
            Dim outputRoot = AppConfig.ResolvePath(_config.PathOutputRoot)
            If String.IsNullOrWhiteSpace(outputRoot) OrElse Not Directory.Exists(outputRoot) Then Return

            Dim todayPrefix = DateTime.Now.ToString("yyyy-MM-dd")
            Dim todayFolders = Directory.GetDirectories(outputRoot).
                Where(Function(d) Path.GetFileName(d).StartsWith(todayPrefix)).
                ToArray()

            If todayFolders.Length = 0 Then Return

            Dim folderNames = String.Join(Environment.NewLine, todayFolders.Select(Function(d) "  " & Path.GetFileName(d)))
            Dim msg = $"Delete {todayFolders.Length} working folder(s) from today?" & Environment.NewLine & Environment.NewLine & folderNames
            Dim result = MessageBox.Show(msg, GetString("Msg_CleanUp"), MessageBoxButtons.YesNo, MessageBoxIcon.Question)

            If result = DialogResult.Yes Then
                For Each folder In todayFolders
                    Try
                        Directory.Delete(folder, True)
                    Catch
                    End Try
                Next
            End If
        Catch
        End Try
    End Sub

    Private Shared Function ColorToHex(c As Drawing.Color) As String
        Return $"#{c.R:X2}{c.G:X2}{c.B:X2}"
    End Function

    Private Shared Sub EnsureFirewallRule(port As Integer)
        Const ruleName As String = "TranscriptionTools Subtitle Server"
        Dim httpsPort = port + 1

        ' Build a single command that deletes old rules then adds new ones (HTTP + HTTPS)
        Dim cmd = $"advfirewall firewall delete rule name=""{ruleName}"" & " &
                  $"netsh advfirewall firewall add rule name=""{ruleName}"" dir=in action=allow protocol=TCP localport={port},{httpsPort}"

        ' First try without elevation
        Try
            Dim psi As New ProcessStartInfo() With {
                .FileName = "netsh",
                .Arguments = cmd,
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True
            }
            Dim p = Process.Start(psi)
            p.WaitForExit(5000)
            If p.ExitCode = 0 Then Return
        Catch
        End Try

        ' Non-elevated failed — try with UAC elevation via cmd /c
        Try
            Dim fullCmd = $"/c netsh advfirewall firewall delete rule name=""{ruleName}"" & " &
                          $"netsh advfirewall firewall add rule name=""{ruleName}"" dir=in action=allow protocol=TCP localport={port},{httpsPort}"
            Dim psi As New ProcessStartInfo() With {
                .FileName = "cmd.exe",
                .Arguments = fullCmd,
                .Verb = "runas",
                .UseShellExecute = True,
                .CreateNoWindow = True,
                .WindowStyle = ProcessWindowStyle.Hidden
            }
            Dim p = Process.Start(psi)
            p?.WaitForExit(10000)
        Catch
            ' User declined UAC or elevation not available — server still works on localhost
        End Try
    End Sub
End Class
