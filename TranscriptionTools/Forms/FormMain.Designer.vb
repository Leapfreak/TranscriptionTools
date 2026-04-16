<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class FormMain
    Inherits System.Windows.Forms.Form

    Private components As System.ComponentModel.IContainer

    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()

        ' ToolTip component
        Me.tipMain = New ToolTip(Me.components)

        ' === Main TabControl ===
        Me.tabMain = New TabControl()
        Me.tabPageJob = New TabPage()
        Me.tabPageWhisper = New TabPage()
        Me.tabPagePaths = New TabPage()
        Me.tabPageLog = New TabPage()
        Me.tabPageSettings = New TabPage()
        Me.tabPageLive = New TabPage()
        Me.tabPageServer = New TabPage()
        Me.tabPageHelp = New TabPage()

        ' ==============================================
        ' TAB 1: Main / Job
        ' ==============================================
        Me.lblMode = New Label()
        Me.cboMode = New ComboBox()
        Me.grpInput = New GroupBox()
        Me.lblUrl = New Label()
        Me.txtUrl = New TextBox()
        Me.btnBrowseFile = New Button()
        Me.lblStartTime = New Label()
        Me.txtStartHH = New TextBox()
        Me.lblStartColon1 = New Label()
        Me.txtStartMM = New TextBox()
        Me.lblStartColon2 = New Label()
        Me.txtStartSS = New TextBox()
        Me.lblEndTime = New Label()
        Me.txtEndHH = New TextBox()
        Me.lblEndColon1 = New Label()
        Me.txtEndMM = New TextBox()
        Me.lblEndColon2 = New Label()
        Me.txtEndSS = New TextBox()
        Me.lblOutputDir = New Label()
        Me.txtOutputDir = New TextBox()
        Me.btnBrowseOutput = New Button()
        Me.lblInputLanguage = New Label()
        Me.cboInputLanguage = New ComboBox()
        Me.lblOutputLanguage = New Label()
        Me.cboOutputLanguage = New ComboBox()
        Me.lblModel = New Label()
        Me.cboModel = New ComboBox()

        Me.grpOutputFormats = New GroupBox()
        Me.chkSrt = New CheckBox()
        Me.chkVtt = New CheckBox()
        Me.chkTxt = New CheckBox()
        Me.chkJson = New CheckBox()
        Me.chkCsv = New CheckBox()
        Me.chkLrc = New CheckBox()

        Me.grpProgress = New GroupBox()
        Me.pbOverall = New ProgressBar()
        Me.lblStepStatus = New Label()
        Me.pbChunk = New ProgressBar()
        Me.btnStart = New Button()
        Me.btnCancel = New Button()
        Me.btnOpenOutput = New Button()
        Me.btnOpenSubtitleEdit = New Button()
        Me.lnkPreviewSrt = New LinkLabel()

        ' ==============================================
        ' TAB 2: Whisper Parameters
        ' ==============================================
        Me.pnlWhisperScroll = New Panel()

        Me.grpLanguageModel = New GroupBox()
        Me.lblWLanguage = New Label()
        Me.cboWLanguage = New ComboBox()

        Me.grpBeamSampling = New GroupBox()
        Me.lblThreads = New Label()
        Me.nudThreads = New NumericUpDown()
        Me.lblProcessors = New Label()
        Me.nudProcessors = New NumericUpDown()
        Me.lblBeamSize = New Label()
        Me.nudBeamSize = New NumericUpDown()
        Me.lblBestOf = New Label()
        Me.nudBestOf = New NumericUpDown()
        Me.lblTemperature = New Label()
        Me.nudTemperature = New NumericUpDown()
        Me.lblTemperatureInc = New Label()
        Me.nudTemperatureInc = New NumericUpDown()

        Me.grpQualityFiltering = New GroupBox()
        Me.lblMaxContext = New Label()
        Me.nudMaxContext = New NumericUpDown()
        Me.lblWordThreshold = New Label()
        Me.nudWordThreshold = New NumericUpDown()
        Me.lblEntropyThreshold = New Label()
        Me.nudEntropyThreshold = New NumericUpDown()
        Me.lblLogProbThreshold = New Label()
        Me.nudLogProbThreshold = New NumericUpDown()
        Me.lblNoSpeechThreshold = New Label()
        Me.nudNoSpeechThreshold = New NumericUpDown()

        Me.grpSegmentControl = New GroupBox()
        Me.lblMaxSegmentLength = New Label()
        Me.nudMaxSegmentLength = New NumericUpDown()
        Me.lblMaxTokens = New Label()
        Me.nudMaxTokens = New NumericUpDown()
        Me.lblAudioContext = New Label()
        Me.nudAudioContext = New NumericUpDown()

        Me.grpPrompting = New GroupBox()
        Me.lblInitialPrompt = New Label()
        Me.txtInitialPrompt = New TextBox()
        Me.lblHotwords = New Label()
        Me.txtHotwords = New TextBox()

        Me.grpFlags = New GroupBox()
        Me.chkSplitOnWord = New CheckBox()
        Me.chkNoGpu = New CheckBox()
        Me.chkFlashAttn = New CheckBox()
        Me.chkPrintProgress = New CheckBox()
        Me.chkPrintColours = New CheckBox()
        Me.chkPrintRealtime = New CheckBox()
        Me.chkDiarize = New CheckBox()
        Me.chkTinydiarize = New CheckBox()
        Me.chkNoTimestamps = New CheckBox()
        Me.chkTranslate = New CheckBox()

        Me.grpVad = New GroupBox()
        Me.lblVadThreshold = New Label()
        Me.nudVadThreshold = New NumericUpDown()
        Me.lblFreqThreshold = New Label()
        Me.nudFreqThreshold = New NumericUpDown()

        Me.btnRestoreDefaults = New Button()

        ' ==============================================
        ' TAB 3: Paths & Tools
        ' ==============================================
        Me.grpPaths = New GroupBox()
        Me.lblPathWhisper = New Label()
        Me.txtPathWhisper = New TextBox()
        Me.btnBrowseWhisper = New Button()
        Me.lblPathYtdlp = New Label()
        Me.txtPathYtdlp = New TextBox()
        Me.btnBrowseYtdlp = New Button()
        Me.lblPathFfmpeg = New Label()
        Me.txtPathFfmpeg = New TextBox()
        Me.btnBrowseFfmpeg = New Button()
        Me.lblPathFfprobe = New Label()
        Me.txtPathFfprobe = New TextBox()
        Me.btnBrowseFfprobe = New Button()
        Me.lblPathStream = New Label()
        Me.txtPathStream = New TextBox()
        Me.btnBrowseStream = New Button()
        Me.lblPathModel = New Label()
        Me.txtPathModel = New TextBox()
        Me.btnBrowseModel = New Button()
        Me.lblPathModelAudio = New Label()
        Me.txtPathModelAudio = New TextBox()
        Me.btnBrowseModelAudio = New Button()
        Me.lblPathOutputRoot = New Label()
        Me.txtPathOutputRoot = New TextBox()
        Me.btnBrowseOutputRoot = New Button()
        Me.lblYtdlpFormat = New Label()
        Me.txtYtdlpFormat = New TextBox()
        Me.btnVerifyPaths = New Button()

        ' ==============================================
        ' TAB 4: Log
        ' ==============================================
        Me.rtbLog = New RichTextBox()
        Me.btnClearLog = New Button()
        Me.btnCopyLog = New Button()

        ' ==============================================
        ' TAB 5: Settings
        ' ==============================================
        Me.grpSettings = New GroupBox()
        Me.lblUiLanguage = New Label()
        Me.cboUiLanguage = New ComboBox()
        Me.lblParallelJobs = New Label()
        Me.nudParallelJobs = New NumericUpDown()
        Me.lblChunkSize = New Label()
        Me.nudChunkSize = New NumericUpDown()
        Me.lblPollInterval = New Label()
        Me.nudPollInterval = New NumericUpDown()
        Me.lblChunkTimeout = New Label()
        Me.nudChunkTimeout = New NumericUpDown()
        Me.chkKeepChunks = New CheckBox()
        Me.chkKeepPreview = New CheckBox()
        Me.chkSkipDownload = New CheckBox()
        Me.lblTheme = New Label()
        Me.cboTheme = New ComboBox()
        Me.btnResetSettings = New Button()

        ' ==============================================
        ' TAB 7: Live Translation
        ' ==============================================
        Me.grpLiveInput = New GroupBox()
        Me.lblLiveDevice = New Label()
        Me.cboLiveDevice = New ComboBox()
        Me.btnRefreshDevices = New Button()
        Me.lblLiveInputLang = New Label()
        Me.cboLiveInputLang = New ComboBox()
        Me.lblLiveOutputLang = New Label()
        Me.cboLiveOutputLang = New ComboBox()
        Me.lblLiveModel = New Label()
        Me.cboLiveModel = New ComboBox()
        Me.btnLiveStart = New Button()
        Me.btnLiveStop = New Button()
        Me.btnLiveSave = New Button()
        Me.btnLiveClear = New Button()
        Me.rtbLiveOutput = New RichTextBox()

        Me.SuspendLayout()

        ' === TabControl ===
        Me.tabMain.Size = New Drawing.Size(880, 650)
        Me.tabMain.Dock = DockStyle.Fill
        Me.tabMain.TabPages.AddRange({Me.tabPageLive, Me.tabPageServer, Me.tabPageJob, Me.tabPageWhisper, Me.tabPagePaths, Me.tabPageLog, Me.tabPageSettings, Me.tabPageHelp})

        ' Set tab page sizes explicitly so anchoring calculates correctly during SuspendLayout
        Dim tpSize = New Drawing.Size(872, 622)
        Me.tabPageJob.Text = "Main / Job"
        Me.tabPageJob.Padding = New Padding(8)
        Me.tabPageJob.AutoScroll = True
        Me.tabPageJob.ClientSize = tpSize

        Me.tabPageWhisper.Text = "Whisper Parameters"
        Me.tabPageWhisper.Padding = New Padding(8)
        Me.tabPageWhisper.AutoScroll = True
        Me.tabPageWhisper.ClientSize = tpSize

        Me.tabPagePaths.Text = "Paths && Tools"
        Me.tabPagePaths.Padding = New Padding(8)
        Me.tabPagePaths.AutoScroll = True
        Me.tabPagePaths.ClientSize = tpSize

        Me.tabPageLog.Text = "Log"
        Me.tabPageLog.Padding = New Padding(8)
        Me.tabPageLog.ClientSize = tpSize

        Me.tabPageSettings.Text = "Settings"
        Me.tabPageSettings.Padding = New Padding(8)
        Me.tabPageSettings.ClientSize = tpSize

        ' ============================================
        ' TAB 1 LAYOUT: Main / Job
        ' ============================================
        Dim y As Integer = 6

        ' --- Mode Selector ---
        Me.lblMode.Text = "Mode:"
        Me.lblMode.Location = New Drawing.Point(12, y)
        Me.lblMode.AutoSize = True
        Me.cboMode.Location = New Drawing.Point(12, y + 16)
        Me.cboMode.Size = New Drawing.Size(200, 23)
        Me.cboMode.DropDownStyle = ComboBoxStyle.DropDownList
        Me.cboMode.Items.AddRange({"Audio File -> Subtitles", "YouTube -> Audio Only", "YouTube -> Full Video", "YouTube -> Subtitles"})
        Me.cboMode.SelectedIndex = 3

        y += 48

        ' --- Input Group ---
        Me.grpInput.Text = "Input"
        Me.grpInput.Location = New Drawing.Point(8, y)
        Me.grpInput.Size = New Drawing.Size(856, 310)
        Me.grpInput.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Dim gy As Integer = 22
        Me.lblUrl.Text = "YouTube URL or local file:"
        Me.lblUrl.Location = New Drawing.Point(10, gy)
        Me.lblUrl.AutoSize = True
        Me.txtUrl.Text = ""
        Me.txtUrl.Location = New Drawing.Point(10, gy + 16)
        Me.txtUrl.Size = New Drawing.Size(726, 23)
        Me.txtUrl.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        Me.btnBrowseFile.Text = "Browse..."
        Me.btnBrowseFile.Location = New Drawing.Point(746, gy + 15)
        Me.btnBrowseFile.Size = New Drawing.Size(100, 25)
        Me.btnBrowseFile.Anchor = AnchorStyles.Top Or AnchorStyles.Right

        gy += 48
        Me.lblInputLanguage.Text = "Input Language:"
        Me.lblInputLanguage.Location = New Drawing.Point(10, gy)
        Me.lblInputLanguage.AutoSize = True
        Me.cboInputLanguage.Location = New Drawing.Point(10, gy + 16)
        Me.cboInputLanguage.Size = New Drawing.Size(150, 23)
        Me.cboInputLanguage.DropDownStyle = ComboBoxStyle.DropDownList

        Me.lblOutputLanguage.Text = "Output Language:"
        Me.lblOutputLanguage.Location = New Drawing.Point(250, gy)
        Me.lblOutputLanguage.AutoSize = True
        Me.cboOutputLanguage.Location = New Drawing.Point(250, gy + 16)
        Me.cboOutputLanguage.Size = New Drawing.Size(150, 23)
        Me.cboOutputLanguage.DropDownStyle = ComboBoxStyle.DropDownList

        gy += 48
        Me.lblModel.Text = "Model:"
        Me.lblModel.Location = New Drawing.Point(10, gy)
        Me.lblModel.AutoSize = True
        Me.cboModel.Location = New Drawing.Point(10, gy + 16)
        Me.cboModel.Size = New Drawing.Size(400, 23)
        Me.cboModel.DropDownStyle = ComboBoxStyle.DropDownList

        gy += 48
        Me.lblStartTime.Text = "Start time:"
        Me.lblStartTime.Location = New Drawing.Point(10, gy)
        Me.lblStartTime.AutoSize = True

        Dim sx = 10
        Me.txtStartHH.Text = "00" : Me.txtStartHH.Location = New Drawing.Point(sx, gy + 16) : Me.txtStartHH.Size = New Drawing.Size(35, 23) : Me.txtStartHH.MaxLength = 2 : Me.txtStartHH.TextAlign = HorizontalAlignment.Center
        Me.lblStartColon1.Text = ":" : Me.lblStartColon1.Location = New Drawing.Point(sx + 36, gy + 19) : Me.lblStartColon1.AutoSize = True : Me.lblStartColon1.Font = New Drawing.Font(Me.lblStartColon1.Font.FontFamily, 10, Drawing.FontStyle.Bold)
        Me.txtStartMM.Text = "00" : Me.txtStartMM.Location = New Drawing.Point(sx + 48, gy + 16) : Me.txtStartMM.Size = New Drawing.Size(35, 23) : Me.txtStartMM.MaxLength = 2 : Me.txtStartMM.TextAlign = HorizontalAlignment.Center
        Me.lblStartColon2.Text = ":" : Me.lblStartColon2.Location = New Drawing.Point(sx + 84, gy + 19) : Me.lblStartColon2.AutoSize = True : Me.lblStartColon2.Font = New Drawing.Font(Me.lblStartColon2.Font.FontFamily, 10, Drawing.FontStyle.Bold)
        Me.txtStartSS.Text = "00" : Me.txtStartSS.Location = New Drawing.Point(sx + 96, gy + 16) : Me.txtStartSS.Size = New Drawing.Size(35, 23) : Me.txtStartSS.MaxLength = 2 : Me.txtStartSS.TextAlign = HorizontalAlignment.Center

        Me.lblEndTime.Text = "End time:"
        Me.lblEndTime.Location = New Drawing.Point(250, gy)
        Me.lblEndTime.AutoSize = True

        Dim ex = 250
        Me.txtEndHH.Text = "00" : Me.txtEndHH.Location = New Drawing.Point(ex, gy + 16) : Me.txtEndHH.Size = New Drawing.Size(35, 23) : Me.txtEndHH.MaxLength = 2 : Me.txtEndHH.TextAlign = HorizontalAlignment.Center
        Me.lblEndColon1.Text = ":" : Me.lblEndColon1.Location = New Drawing.Point(ex + 36, gy + 19) : Me.lblEndColon1.AutoSize = True : Me.lblEndColon1.Font = New Drawing.Font(Me.lblEndColon1.Font.FontFamily, 10, Drawing.FontStyle.Bold)
        Me.txtEndMM.Text = "00" : Me.txtEndMM.Location = New Drawing.Point(ex + 48, gy + 16) : Me.txtEndMM.Size = New Drawing.Size(35, 23) : Me.txtEndMM.MaxLength = 2 : Me.txtEndMM.TextAlign = HorizontalAlignment.Center
        Me.lblEndColon2.Text = ":" : Me.lblEndColon2.Location = New Drawing.Point(ex + 84, gy + 19) : Me.lblEndColon2.AutoSize = True : Me.lblEndColon2.Font = New Drawing.Font(Me.lblEndColon2.Font.FontFamily, 10, Drawing.FontStyle.Bold)
        Me.txtEndSS.Text = "00" : Me.txtEndSS.Location = New Drawing.Point(ex + 96, gy + 16) : Me.txtEndSS.Size = New Drawing.Size(35, 23) : Me.txtEndSS.MaxLength = 2 : Me.txtEndSS.TextAlign = HorizontalAlignment.Center

        gy += 48
        Me.lblOutputDir.Text = "Output folder:"
        Me.lblOutputDir.Location = New Drawing.Point(10, gy)
        Me.lblOutputDir.AutoSize = True
        Me.txtOutputDir.Location = New Drawing.Point(10, gy + 16)
        Me.txtOutputDir.Size = New Drawing.Size(726, 23)
        Me.txtOutputDir.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        Me.btnBrowseOutput.Text = "Browse..."
        Me.btnBrowseOutput.Location = New Drawing.Point(746, gy + 15)
        Me.btnBrowseOutput.Size = New Drawing.Size(100, 25)
        Me.btnBrowseOutput.Anchor = AnchorStyles.Top Or AnchorStyles.Right

        Me.grpInput.Controls.AddRange({Me.lblUrl, Me.txtUrl, Me.btnBrowseFile,
            Me.lblInputLanguage, Me.cboInputLanguage, Me.lblOutputLanguage, Me.cboOutputLanguage,
            Me.lblModel, Me.cboModel,
            Me.lblStartTime, Me.txtStartHH, Me.lblStartColon1, Me.txtStartMM, Me.lblStartColon2, Me.txtStartSS,
            Me.lblEndTime, Me.txtEndHH, Me.lblEndColon1, Me.txtEndMM, Me.lblEndColon2, Me.txtEndSS,
            Me.lblOutputDir, Me.txtOutputDir, Me.btnBrowseOutput})

        y += 320

        ' --- Output Formats Group ---
        Me.grpOutputFormats.Text = "Output Formats"
        Me.grpOutputFormats.Location = New Drawing.Point(8, y)
        Me.grpOutputFormats.Size = New Drawing.Size(856, 55)
        Me.grpOutputFormats.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Me.chkSrt.Text = "SRT"
        Me.chkSrt.Location = New Drawing.Point(15, 22)
        Me.chkSrt.AutoSize = True
        Me.chkSrt.Checked = True
        Me.chkVtt.Text = "VTT"
        Me.chkVtt.Location = New Drawing.Point(85, 22)
        Me.chkVtt.AutoSize = True
        Me.chkTxt.Text = "TXT"
        Me.chkTxt.Location = New Drawing.Point(155, 22)
        Me.chkTxt.AutoSize = True
        Me.chkJson.Text = "JSON"
        Me.chkJson.Location = New Drawing.Point(225, 22)
        Me.chkJson.AutoSize = True
        Me.chkCsv.Text = "CSV"
        Me.chkCsv.Location = New Drawing.Point(305, 22)
        Me.chkCsv.AutoSize = True
        Me.chkLrc.Text = "LRC"
        Me.chkLrc.Location = New Drawing.Point(375, 22)
        Me.chkLrc.AutoSize = True

        Me.grpOutputFormats.Controls.AddRange({Me.chkSrt, Me.chkVtt, Me.chkTxt, Me.chkJson, Me.chkCsv, Me.chkLrc})

        y += 65

        ' --- Progress Group ---
        Me.grpProgress.Text = "Progress"
        Me.grpProgress.Location = New Drawing.Point(8, y)
        Me.grpProgress.Size = New Drawing.Size(856, 160)
        Me.grpProgress.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Bottom

        Me.lblStepStatus.Text = "Ready"
        Me.lblStepStatus.Location = New Drawing.Point(10, 22)
        Me.lblStepStatus.Size = New Drawing.Size(836, 20)
        Me.lblStepStatus.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Me.pbOverall.Location = New Drawing.Point(10, 45)
        Me.pbOverall.Size = New Drawing.Size(836, 23)
        Me.pbOverall.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Me.pbChunk.Location = New Drawing.Point(10, 73)
        Me.pbChunk.Size = New Drawing.Size(836, 23)
        Me.pbChunk.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        Me.pbChunk.Visible = False

        Me.btnStart.Text = "Start"
        Me.btnStart.Location = New Drawing.Point(10, 105)
        Me.btnStart.Size = New Drawing.Size(100, 35)
        Me.btnStart.Font = New Drawing.Font(Me.btnStart.Font, Drawing.FontStyle.Bold)

        Me.btnResume = New Button()
        Me.btnResume.Text = "Resume"
        Me.btnResume.Location = New Drawing.Point(120, 105)
        Me.btnResume.Size = New Drawing.Size(100, 35)

        Me.btnCancel.Text = "Cancel"
        Me.btnCancel.Location = New Drawing.Point(230, 105)
        Me.btnCancel.Size = New Drawing.Size(100, 35)
        Me.btnCancel.Enabled = False

        Me.btnOpenOutput.Text = "Open Output Folder"
        Me.btnOpenOutput.Location = New Drawing.Point(350, 105)
        Me.btnOpenOutput.Size = New Drawing.Size(150, 35)
        Me.btnOpenOutput.Enabled = False

        Me.btnOpenSubtitleEdit.Text = "Subtitle Edit"
        Me.btnOpenSubtitleEdit.Location = New Drawing.Point(510, 105)
        Me.btnOpenSubtitleEdit.Size = New Drawing.Size(130, 35)
        Me.btnOpenSubtitleEdit.Enabled = False

        Me.lnkPreviewSrt.Text = "Open preview.srt"
        Me.lnkPreviewSrt.Location = New Drawing.Point(660, 115)
        Me.lnkPreviewSrt.AutoSize = True
        Me.lnkPreviewSrt.Visible = False

        Me.grpProgress.Controls.AddRange({Me.lblStepStatus, Me.pbOverall, Me.pbChunk,
            Me.btnStart, Me.btnResume, Me.btnCancel, Me.btnOpenOutput, Me.btnOpenSubtitleEdit, Me.lnkPreviewSrt})

        Me.tabPageJob.Controls.AddRange({Me.lblMode, Me.cboMode, Me.grpInput, Me.grpOutputFormats, Me.grpProgress})

        ' ============================================
        ' TAB 2 LAYOUT: Whisper Parameters
        ' ============================================
        Me.pnlWhisperScroll.Dock = DockStyle.Fill
        Me.pnlWhisperScroll.AutoScroll = True

        Dim wy As Integer = 6

        ' --- Language & Model ---
        Me.grpLanguageModel.Text = "Language && Model"
        Me.grpLanguageModel.Location = New Drawing.Point(8, wy)
        Me.grpLanguageModel.Size = New Drawing.Size(810, 68)
        Me.grpLanguageModel.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Me.lblWLanguage.Text = "Language:"
        Me.lblWLanguage.Location = New Drawing.Point(10, 22)
        Me.lblWLanguage.AutoSize = True
        Me.cboWLanguage.Location = New Drawing.Point(10, 38)
        Me.cboWLanguage.Size = New Drawing.Size(200, 23)
        Me.cboWLanguage.DropDownStyle = ComboBoxStyle.DropDownList
        Me.grpLanguageModel.Controls.AddRange({Me.lblWLanguage, Me.cboWLanguage})

        wy += 76

        ' --- Beam Search / Sampling ---
        Me.grpBeamSampling.Text = "Beam Search / Sampling"
        Me.grpBeamSampling.Location = New Drawing.Point(8, wy)
        Me.grpBeamSampling.Size = New Drawing.Size(810, 170)
        Me.grpBeamSampling.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Dim bsy = 22
        Me.lblThreads.Text = "Threads:" : Me.lblThreads.Location = New Drawing.Point(10, bsy) : Me.lblThreads.AutoSize = True
        Me.nudThreads.Location = New Drawing.Point(10, bsy + 16) : Me.nudThreads.Size = New Drawing.Size(100, 23) : Me.nudThreads.Minimum = 1 : Me.nudThreads.Maximum = 64 : Me.nudThreads.Value = 4
        Me.lblProcessors.Text = "Processors:" : Me.lblProcessors.Location = New Drawing.Point(250, bsy) : Me.lblProcessors.AutoSize = True
        Me.nudProcessors.Location = New Drawing.Point(250, bsy + 16) : Me.nudProcessors.Size = New Drawing.Size(100, 23) : Me.nudProcessors.Minimum = 1 : Me.nudProcessors.Maximum = 8 : Me.nudProcessors.Value = 1

        bsy += 48
        Me.lblBeamSize.Text = "Beam size:" : Me.lblBeamSize.Location = New Drawing.Point(10, bsy) : Me.lblBeamSize.AutoSize = True
        Me.nudBeamSize.Location = New Drawing.Point(10, bsy + 16) : Me.nudBeamSize.Size = New Drawing.Size(100, 23) : Me.nudBeamSize.Minimum = -1 : Me.nudBeamSize.Maximum = 15 : Me.nudBeamSize.Value = 5
        Me.lblBestOf.Text = "Best of:" : Me.lblBestOf.Location = New Drawing.Point(250, bsy) : Me.lblBestOf.AutoSize = True
        Me.nudBestOf.Location = New Drawing.Point(250, bsy + 16) : Me.nudBestOf.Size = New Drawing.Size(100, 23) : Me.nudBestOf.Minimum = 1 : Me.nudBestOf.Maximum = 10 : Me.nudBestOf.Value = 5

        bsy += 48
        Me.lblTemperature.Text = "Temperature:" : Me.lblTemperature.Location = New Drawing.Point(10, bsy) : Me.lblTemperature.AutoSize = True
        Me.nudTemperature.Location = New Drawing.Point(10, bsy + 16) : Me.nudTemperature.Size = New Drawing.Size(100, 23) : Me.nudTemperature.Minimum = 0 : Me.nudTemperature.Maximum = 1 : Me.nudTemperature.DecimalPlaces = 1 : Me.nudTemperature.Increment = 0.1D : Me.nudTemperature.Value = 0D
        Me.lblTemperatureInc.Text = "Temp increment:" : Me.lblTemperatureInc.Location = New Drawing.Point(250, bsy) : Me.lblTemperatureInc.AutoSize = True
        Me.nudTemperatureInc.Location = New Drawing.Point(250, bsy + 16) : Me.nudTemperatureInc.Size = New Drawing.Size(100, 23) : Me.nudTemperatureInc.Minimum = 0 : Me.nudTemperatureInc.Maximum = 1 : Me.nudTemperatureInc.DecimalPlaces = 1 : Me.nudTemperatureInc.Increment = 0.1D : Me.nudTemperatureInc.Value = 0.2D

        Me.grpBeamSampling.Controls.AddRange({Me.lblThreads, Me.nudThreads, Me.lblProcessors, Me.nudProcessors,
            Me.lblBeamSize, Me.nudBeamSize, Me.lblBestOf, Me.nudBestOf,
            Me.lblTemperature, Me.nudTemperature, Me.lblTemperatureInc, Me.nudTemperatureInc})

        wy += 178

        ' --- Quality / Filtering ---
        Me.grpQualityFiltering.Text = "Quality / Filtering"
        Me.grpQualityFiltering.Location = New Drawing.Point(8, wy)
        Me.grpQualityFiltering.Size = New Drawing.Size(810, 170)
        Me.grpQualityFiltering.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Dim qy = 22
        Me.lblMaxContext.Text = "Max context:" : Me.lblMaxContext.Location = New Drawing.Point(10, qy) : Me.lblMaxContext.AutoSize = True
        Me.nudMaxContext.Location = New Drawing.Point(10, qy + 16) : Me.nudMaxContext.Size = New Drawing.Size(100, 23) : Me.nudMaxContext.Minimum = -1 : Me.nudMaxContext.Maximum = 512 : Me.nudMaxContext.Value = 0
        Me.lblWordThreshold.Text = "Word threshold:" : Me.lblWordThreshold.Location = New Drawing.Point(250, qy) : Me.lblWordThreshold.AutoSize = True
        Me.nudWordThreshold.Location = New Drawing.Point(250, qy + 16) : Me.nudWordThreshold.Size = New Drawing.Size(100, 23) : Me.nudWordThreshold.Minimum = 0 : Me.nudWordThreshold.Maximum = 1 : Me.nudWordThreshold.DecimalPlaces = 2 : Me.nudWordThreshold.Increment = 0.01D : Me.nudWordThreshold.Value = 0.01D

        qy += 48
        Me.lblEntropyThreshold.Text = "Entropy threshold:" : Me.lblEntropyThreshold.Location = New Drawing.Point(10, qy) : Me.lblEntropyThreshold.AutoSize = True
        Me.nudEntropyThreshold.Location = New Drawing.Point(10, qy + 16) : Me.nudEntropyThreshold.Size = New Drawing.Size(100, 23) : Me.nudEntropyThreshold.Minimum = 0 : Me.nudEntropyThreshold.Maximum = 5 : Me.nudEntropyThreshold.DecimalPlaces = 1 : Me.nudEntropyThreshold.Increment = 0.1D : Me.nudEntropyThreshold.Value = 2.4D
        Me.lblLogProbThreshold.Text = "Log prob threshold:" : Me.lblLogProbThreshold.Location = New Drawing.Point(250, qy) : Me.lblLogProbThreshold.AutoSize = True
        Me.nudLogProbThreshold.Location = New Drawing.Point(250, qy + 16) : Me.nudLogProbThreshold.Size = New Drawing.Size(100, 23) : Me.nudLogProbThreshold.Minimum = -10 : Me.nudLogProbThreshold.Maximum = 0 : Me.nudLogProbThreshold.DecimalPlaces = 1 : Me.nudLogProbThreshold.Increment = 0.1D : Me.nudLogProbThreshold.Value = -1D

        qy += 48
        Me.lblNoSpeechThreshold.Text = "No speech threshold:" : Me.lblNoSpeechThreshold.Location = New Drawing.Point(10, qy) : Me.lblNoSpeechThreshold.AutoSize = True
        Me.nudNoSpeechThreshold.Location = New Drawing.Point(10, qy + 16) : Me.nudNoSpeechThreshold.Size = New Drawing.Size(100, 23) : Me.nudNoSpeechThreshold.Minimum = 0 : Me.nudNoSpeechThreshold.Maximum = 1 : Me.nudNoSpeechThreshold.DecimalPlaces = 1 : Me.nudNoSpeechThreshold.Increment = 0.1D : Me.nudNoSpeechThreshold.Value = 0.6D

        Me.grpQualityFiltering.Controls.AddRange({Me.lblMaxContext, Me.nudMaxContext, Me.lblWordThreshold, Me.nudWordThreshold,
            Me.lblEntropyThreshold, Me.nudEntropyThreshold, Me.lblLogProbThreshold, Me.nudLogProbThreshold,
            Me.lblNoSpeechThreshold, Me.nudNoSpeechThreshold})

        wy += 178

        ' --- Segment Control ---
        Me.grpSegmentControl.Text = "Segment Control"
        Me.grpSegmentControl.Location = New Drawing.Point(8, wy)
        Me.grpSegmentControl.Size = New Drawing.Size(810, 68)
        Me.grpSegmentControl.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Me.lblMaxSegmentLength.Text = "Max segment length:" : Me.lblMaxSegmentLength.Location = New Drawing.Point(10, 22) : Me.lblMaxSegmentLength.AutoSize = True
        Me.nudMaxSegmentLength.Location = New Drawing.Point(10, 38) : Me.nudMaxSegmentLength.Size = New Drawing.Size(100, 23) : Me.nudMaxSegmentLength.Minimum = 0 : Me.nudMaxSegmentLength.Maximum = 200 : Me.nudMaxSegmentLength.Value = 0
        Me.lblMaxTokens.Text = "Max tokens:" : Me.lblMaxTokens.Location = New Drawing.Point(250, 22) : Me.lblMaxTokens.AutoSize = True
        Me.nudMaxTokens.Location = New Drawing.Point(250, 38) : Me.nudMaxTokens.Size = New Drawing.Size(100, 23) : Me.nudMaxTokens.Minimum = 0 : Me.nudMaxTokens.Maximum = 256 : Me.nudMaxTokens.Value = 0
        Me.lblAudioContext.Text = "Audio context:" : Me.lblAudioContext.Location = New Drawing.Point(490, 22) : Me.lblAudioContext.AutoSize = True
        Me.nudAudioContext.Location = New Drawing.Point(490, 38) : Me.nudAudioContext.Size = New Drawing.Size(100, 23) : Me.nudAudioContext.Minimum = 0 : Me.nudAudioContext.Maximum = 1500 : Me.nudAudioContext.Value = 0

        Me.grpSegmentControl.Controls.AddRange({Me.lblMaxSegmentLength, Me.nudMaxSegmentLength,
            Me.lblMaxTokens, Me.nudMaxTokens, Me.lblAudioContext, Me.nudAudioContext})

        wy += 76

        ' --- Prompting ---
        Me.grpPrompting.Text = "Prompting"
        Me.grpPrompting.Location = New Drawing.Point(8, wy)
        Me.grpPrompting.Size = New Drawing.Size(810, 155)
        Me.grpPrompting.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Me.lblInitialPrompt.Text = "Initial prompt:" : Me.lblInitialPrompt.Location = New Drawing.Point(10, 22) : Me.lblInitialPrompt.AutoSize = True
        Me.txtInitialPrompt.Location = New Drawing.Point(10, 38) : Me.txtInitialPrompt.Size = New Drawing.Size(780, 60)
        Me.txtInitialPrompt.Multiline = True : Me.txtInitialPrompt.WordWrap = True : Me.txtInitialPrompt.ScrollBars = ScrollBars.Vertical
        Me.lblHotwords.Text = "Hotwords:" : Me.lblHotwords.Location = New Drawing.Point(10, 106) : Me.lblHotwords.AutoSize = True
        Me.txtHotwords.Location = New Drawing.Point(10, 122) : Me.txtHotwords.Size = New Drawing.Size(780, 23) : Me.txtHotwords.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Me.grpPrompting.Controls.AddRange({Me.lblInitialPrompt, Me.txtInitialPrompt, Me.lblHotwords, Me.txtHotwords})

        wy += 163

        ' --- Flags ---
        Me.grpFlags.Text = "Flags"
        Me.grpFlags.Location = New Drawing.Point(8, wy)
        Me.grpFlags.Size = New Drawing.Size(810, 90)
        Me.grpFlags.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Dim fx = 15
        Dim fy1 = 20
        Dim fy2 = 45
        Dim fSpacing = 155

        Me.chkSplitOnWord.Text = "Split on word" : Me.chkSplitOnWord.Location = New Drawing.Point(fx, fy1) : Me.chkSplitOnWord.AutoSize = True : Me.chkSplitOnWord.Checked = True
        Me.chkNoGpu.Text = "No GPU" : Me.chkNoGpu.Location = New Drawing.Point(fx + fSpacing, fy1) : Me.chkNoGpu.AutoSize = True
        Me.chkFlashAttn.Text = "Flash attention" : Me.chkFlashAttn.Location = New Drawing.Point(fx + fSpacing * 2, fy1) : Me.chkFlashAttn.AutoSize = True
        Me.chkPrintProgress.Text = "Print progress" : Me.chkPrintProgress.Location = New Drawing.Point(fx + fSpacing * 3, fy1) : Me.chkPrintProgress.AutoSize = True
        Me.chkPrintColours.Text = "Print colours" : Me.chkPrintColours.Location = New Drawing.Point(fx + fSpacing * 4, fy1) : Me.chkPrintColours.AutoSize = True
        Me.chkPrintRealtime.Text = "Print realtime" : Me.chkPrintRealtime.Location = New Drawing.Point(fx, fy2) : Me.chkPrintRealtime.AutoSize = True
        Me.chkDiarize.Text = "Diarize" : Me.chkDiarize.Location = New Drawing.Point(fx + fSpacing, fy2) : Me.chkDiarize.AutoSize = True
        Me.chkTinydiarize.Text = "Tinydiarize" : Me.chkTinydiarize.Location = New Drawing.Point(fx + fSpacing * 2, fy2) : Me.chkTinydiarize.AutoSize = True
        Me.chkNoTimestamps.Text = "No timestamps" : Me.chkNoTimestamps.Location = New Drawing.Point(fx + fSpacing * 3, fy2) : Me.chkNoTimestamps.AutoSize = True
        Me.chkTranslate.Text = "Translate to English" : Me.chkTranslate.Location = New Drawing.Point(fx + fSpacing * 4, fy2) : Me.chkTranslate.AutoSize = True

        Me.grpFlags.Controls.AddRange({Me.chkSplitOnWord, Me.chkNoGpu, Me.chkFlashAttn, Me.chkPrintProgress, Me.chkPrintColours,
            Me.chkPrintRealtime, Me.chkDiarize, Me.chkTinydiarize, Me.chkNoTimestamps, Me.chkTranslate})

        wy += 98

        ' --- VAD ---
        Me.grpVad.Text = "VAD (Voice Activity Detection)"
        Me.grpVad.Location = New Drawing.Point(8, wy)
        Me.grpVad.Size = New Drawing.Size(810, 68)
        Me.grpVad.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Me.lblVadThreshold.Text = "VAD threshold:" : Me.lblVadThreshold.Location = New Drawing.Point(10, 22) : Me.lblVadThreshold.AutoSize = True
        Me.nudVadThreshold.Location = New Drawing.Point(10, 38) : Me.nudVadThreshold.Size = New Drawing.Size(100, 23) : Me.nudVadThreshold.Minimum = 0 : Me.nudVadThreshold.Maximum = 1 : Me.nudVadThreshold.DecimalPlaces = 1 : Me.nudVadThreshold.Increment = 0.1D : Me.nudVadThreshold.Value = 0.6D
        Me.lblFreqThreshold.Text = "Frequency threshold:" : Me.lblFreqThreshold.Location = New Drawing.Point(250, 22) : Me.lblFreqThreshold.AutoSize = True
        Me.nudFreqThreshold.Location = New Drawing.Point(250, 38) : Me.nudFreqThreshold.Size = New Drawing.Size(100, 23) : Me.nudFreqThreshold.Minimum = 0 : Me.nudFreqThreshold.Maximum = 3000 : Me.nudFreqThreshold.DecimalPlaces = 1 : Me.nudFreqThreshold.Increment = 10D : Me.nudFreqThreshold.Value = 100D

        Me.grpVad.Controls.AddRange({Me.lblVadThreshold, Me.nudVadThreshold, Me.lblFreqThreshold, Me.nudFreqThreshold})

        wy += 76

        ' Restore defaults button
        Me.btnRestoreDefaults.Text = "Restore Defaults"
        Me.btnRestoreDefaults.Location = New Drawing.Point(8, wy)
        Me.btnRestoreDefaults.Size = New Drawing.Size(150, 30)

        Me.pnlWhisperScroll.Controls.AddRange({Me.grpLanguageModel, Me.grpBeamSampling, Me.grpQualityFiltering,
            Me.grpSegmentControl, Me.grpPrompting, Me.grpFlags, Me.grpVad, Me.btnRestoreDefaults})
        Me.tabPageWhisper.Controls.Add(Me.pnlWhisperScroll)

        ' ============================================
        ' TAB 3 LAYOUT: Paths & Tools
        ' ============================================
        Me.grpPaths.Text = "Tool Paths"
        Me.grpPaths.Location = New Drawing.Point(8, 6)
        Me.grpPaths.Size = New Drawing.Size(830, 420)
        Me.grpPaths.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Dim py = 22
        Dim pathRows = {
            (Me.lblPathWhisper, Me.txtPathWhisper, Me.btnBrowseWhisper, "whisper-cli.exe:"),
            (Me.lblPathStream, Me.txtPathStream, Me.btnBrowseStream, "whisper-stream.exe:"),
            (Me.lblPathYtdlp, Me.txtPathYtdlp, Me.btnBrowseYtdlp, "yt-dlp.exe:"),
            (Me.lblPathFfmpeg, Me.txtPathFfmpeg, Me.btnBrowseFfmpeg, "ffmpeg.exe:"),
            (Me.lblPathFfprobe, Me.txtPathFfprobe, Me.btnBrowseFfprobe, "ffprobe.exe:"),
            (Me.lblPathModel, Me.txtPathModel, Me.btnBrowseModel, "YouTube model (.bin):"),
            (Me.lblPathModelAudio, Me.txtPathModelAudio, Me.btnBrowseModelAudio, "Audio File model (.bin):"),
            (Me.lblPathOutputRoot, Me.txtPathOutputRoot, Me.btnBrowseOutputRoot, "Default output root:")
        }

        For Each row In pathRows
            row.Item1.Text = row.Item4
            row.Item1.Location = New Drawing.Point(10, py)
            row.Item1.AutoSize = True
            row.Item2.Location = New Drawing.Point(10, py + 16)
            row.Item2.Size = New Drawing.Size(700, 23)
            row.Item2.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
            row.Item3.Text = "Browse..."
            row.Item3.Location = New Drawing.Point(720, py + 15)
            row.Item3.Size = New Drawing.Size(85, 25)
            row.Item3.Anchor = AnchorStyles.Top Or AnchorStyles.Right
            Me.grpPaths.Controls.AddRange({row.Item1, row.Item2, row.Item3})
            py += 48
        Next

        ' yt-dlp format (no browse button)
        Me.lblYtdlpFormat.Text = "yt-dlp format string:"
        Me.lblYtdlpFormat.Location = New Drawing.Point(10, py)
        Me.lblYtdlpFormat.AutoSize = True
        Me.txtYtdlpFormat.Location = New Drawing.Point(10, py + 16)
        Me.txtYtdlpFormat.Size = New Drawing.Size(700, 23)
        Me.txtYtdlpFormat.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        Me.grpPaths.Controls.AddRange({Me.lblYtdlpFormat, Me.txtYtdlpFormat})

        Me.btnVerifyPaths.Text = "Verify All Paths"
        Me.btnVerifyPaths.Location = New Drawing.Point(8, 435)
        Me.btnVerifyPaths.Size = New Drawing.Size(150, 30)

        Me.lnkDownloadModels = New LinkLabel()
        Me.lnkDownloadModels.Text = "Download whisper models..."
        Me.lnkDownloadModels.Location = New Drawing.Point(170, 442)
        Me.lnkDownloadModels.AutoSize = True

        Me.tabPagePaths.Controls.AddRange({Me.grpPaths, Me.btnVerifyPaths, Me.lnkDownloadModels})

        ' ============================================
        ' TAB 4 LAYOUT: Log
        ' ============================================
        Me.rtbLog.Dock = DockStyle.Fill
        Me.rtbLog.ReadOnly = True
        Me.rtbLog.BackColor = Drawing.Color.White
        Me.rtbLog.Font = New Drawing.Font("Consolas", 9)
        Me.rtbLog.WordWrap = False
        Me.rtbLog.ScrollBars = RichTextBoxScrollBars.Both

        Dim pnlLogButtons As New Panel()
        pnlLogButtons.Dock = DockStyle.Bottom
        pnlLogButtons.Height = 40

        Me.btnClearLog.Text = "Clear Log"
        Me.btnClearLog.Location = New Drawing.Point(8, 8)
        Me.btnClearLog.Size = New Drawing.Size(100, 28)
        Me.btnCopyLog.Text = "Copy to Clipboard"
        Me.btnCopyLog.Location = New Drawing.Point(118, 8)
        Me.btnCopyLog.Size = New Drawing.Size(140, 28)

        pnlLogButtons.Controls.AddRange({Me.btnClearLog, Me.btnCopyLog})
        Me.tabPageLog.Controls.Add(Me.rtbLog)
        Me.tabPageLog.Controls.Add(pnlLogButtons)

        ' ============================================
        ' TAB 5 LAYOUT: Settings
        ' ============================================
        Me.grpSettings.Text = "Application Settings"
        Me.grpSettings.Location = New Drawing.Point(8, 6)
        Me.grpSettings.Size = New Drawing.Size(830, 420)
        Me.grpSettings.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Dim sy = 25

        Me.lblUiLanguage.Text = "UI Language:" : Me.lblUiLanguage.Location = New Drawing.Point(10, sy) : Me.lblUiLanguage.AutoSize = True
        Me.cboUiLanguage.Location = New Drawing.Point(10, sy + 16) : Me.cboUiLanguage.Size = New Drawing.Size(200, 23) : Me.cboUiLanguage.DropDownStyle = ComboBoxStyle.DropDownList
        sy += 48

        Me.lblParallelJobs.Text = "Parallel transcription jobs:" : Me.lblParallelJobs.Location = New Drawing.Point(10, sy) : Me.lblParallelJobs.AutoSize = True
        Me.nudParallelJobs.Location = New Drawing.Point(10, sy + 16) : Me.nudParallelJobs.Size = New Drawing.Size(100, 23) : Me.nudParallelJobs.Minimum = 1 : Me.nudParallelJobs.Maximum = 16 : Me.nudParallelJobs.Value = 4
        sy += 48

        Me.lblChunkSize.Text = "Chunk size (seconds):" : Me.lblChunkSize.Location = New Drawing.Point(10, sy) : Me.lblChunkSize.AutoSize = True
        Me.nudChunkSize.Location = New Drawing.Point(10, sy + 16) : Me.nudChunkSize.Size = New Drawing.Size(100, 23) : Me.nudChunkSize.Minimum = 60 : Me.nudChunkSize.Maximum = 1800 : Me.nudChunkSize.Value = 300
        sy += 48

        Me.lblPollInterval.Text = "Poll interval (ms):" : Me.lblPollInterval.Location = New Drawing.Point(10, sy) : Me.lblPollInterval.AutoSize = True
        Me.nudPollInterval.Location = New Drawing.Point(10, sy + 16) : Me.nudPollInterval.Size = New Drawing.Size(100, 23) : Me.nudPollInterval.Minimum = 500 : Me.nudPollInterval.Maximum = 10000 : Me.nudPollInterval.Value = 2000
        sy += 48

        Me.lblChunkTimeout.Text = "Chunk timeout (min):" : Me.lblChunkTimeout.Location = New Drawing.Point(10, sy) : Me.lblChunkTimeout.AutoSize = True
        Me.nudChunkTimeout.Location = New Drawing.Point(10, sy + 16) : Me.nudChunkTimeout.Size = New Drawing.Size(100, 23) : Me.nudChunkTimeout.Minimum = 1 : Me.nudChunkTimeout.Maximum = 120 : Me.nudChunkTimeout.Value = 60
        sy += 48

        Me.chkKeepChunks.Text = "Keep chunk files" : Me.chkKeepChunks.Location = New Drawing.Point(10, sy) : Me.chkKeepChunks.AutoSize = True
        sy += 28
        Me.chkKeepPreview.Text = "Keep trimmed preview.mp4" : Me.chkKeepPreview.Location = New Drawing.Point(10, sy) : Me.chkKeepPreview.AutoSize = True : Me.chkKeepPreview.Checked = True
        sy += 28
        Me.chkSkipDownload.Text = "Skip download if file exists" : Me.chkSkipDownload.Location = New Drawing.Point(10, sy) : Me.chkSkipDownload.AutoSize = True
        sy += 35

        Me.lblTheme.Text = "Theme:" : Me.lblTheme.Location = New Drawing.Point(10, sy) : Me.lblTheme.AutoSize = True
        Me.cboTheme.Location = New Drawing.Point(10, sy + 16) : Me.cboTheme.Size = New Drawing.Size(150, 23) : Me.cboTheme.DropDownStyle = ComboBoxStyle.DropDownList
        Me.cboTheme.Items.AddRange({"System", "Light", "Dark"})
        Me.cboTheme.SelectedIndex = 0

        Me.grpSettings.Controls.AddRange({Me.lblUiLanguage, Me.cboUiLanguage,
            Me.lblParallelJobs, Me.nudParallelJobs,
            Me.lblChunkSize, Me.nudChunkSize,
            Me.lblPollInterval, Me.nudPollInterval,
            Me.lblChunkTimeout, Me.nudChunkTimeout,
            Me.chkKeepChunks, Me.chkKeepPreview, Me.chkSkipDownload,
            Me.lblTheme, Me.cboTheme})

        Me.btnResetSettings.Text = "Reset All Settings"
        Me.btnResetSettings.Location = New Drawing.Point(8, 435)
        Me.btnResetSettings.Size = New Drawing.Size(150, 30)

        Me.btnCheckToolUpdates = New Button()
        Me.btnCheckToolUpdates.Text = "Check for Tool Updates"
        Me.btnCheckToolUpdates.Location = New Drawing.Point(170, 435)
        Me.btnCheckToolUpdates.Size = New Drawing.Size(180, 30)

        Me.tabPageSettings.Controls.AddRange({Me.grpSettings, Me.btnResetSettings, Me.btnCheckToolUpdates})

        ' ============================================
        ' TAB 6 LAYOUT: Live Translation
        ' ============================================
        Me.tabPageLive.Text = "Live Translation (Test)"
        Me.tabPageLive.Padding = New Padding(8)
        Me.tabPageLive.ClientSize = tpSize

        ' Input settings group
        Me.grpLiveInput.Text = "Live Translation Settings"
        Me.grpLiveInput.Location = New Drawing.Point(8, 6)
        Me.grpLiveInput.Size = New Drawing.Size(830, 178)
        Me.grpLiveInput.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Dim ly = 22

        Me.lblLiveDevice.Text = "Audio Device:"
        Me.lblLiveDevice.Location = New Drawing.Point(10, ly)
        Me.lblLiveDevice.AutoSize = True
        Me.cboLiveDevice.Location = New Drawing.Point(10, ly + 16)
        Me.cboLiveDevice.Size = New Drawing.Size(600, 23)
        Me.cboLiveDevice.DropDownStyle = ComboBoxStyle.DropDownList
        Me.cboLiveDevice.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        Me.btnRefreshDevices.Text = "Refresh"
        Me.btnRefreshDevices.Location = New Drawing.Point(620, ly + 15)
        Me.btnRefreshDevices.Size = New Drawing.Size(85, 25)
        Me.btnRefreshDevices.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        ly += 48

        Me.lblLiveInputLang.Text = "Input Language:"
        Me.lblLiveInputLang.Location = New Drawing.Point(10, ly)
        Me.lblLiveInputLang.AutoSize = True
        Me.cboLiveInputLang.Location = New Drawing.Point(10, ly + 16)
        Me.cboLiveInputLang.Size = New Drawing.Size(150, 23)
        Me.cboLiveInputLang.DropDownStyle = ComboBoxStyle.DropDownList

        Me.lblLiveOutputLang.Text = "Output Language:"
        Me.lblLiveOutputLang.Location = New Drawing.Point(250, ly)
        Me.lblLiveOutputLang.AutoSize = True
        Me.cboLiveOutputLang.Location = New Drawing.Point(250, ly + 16)
        Me.cboLiveOutputLang.Size = New Drawing.Size(150, 23)
        Me.cboLiveOutputLang.DropDownStyle = ComboBoxStyle.DropDownList
        ly += 48

        Me.lblLiveModel.Text = "Model:"
        Me.lblLiveModel.Location = New Drawing.Point(10, ly)
        Me.lblLiveModel.AutoSize = True
        Me.cboLiveModel.Location = New Drawing.Point(10, ly + 16)
        Me.cboLiveModel.Size = New Drawing.Size(250, 23)
        Me.cboLiveModel.DropDownStyle = ComboBoxStyle.DropDownList

        Me.grpLiveInput.Controls.AddRange({Me.lblLiveDevice, Me.cboLiveDevice, Me.btnRefreshDevices,
            Me.lblLiveInputLang, Me.cboLiveInputLang, Me.lblLiveOutputLang, Me.cboLiveOutputLang,
            Me.lblLiveModel, Me.cboLiveModel})

        ' Buttons panel
        Dim pnlLiveButtons As New Panel()
        pnlLiveButtons.Location = New Drawing.Point(8, 190)
        pnlLiveButtons.Size = New Drawing.Size(830, 35)
        pnlLiveButtons.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Me.btnLiveStart.Text = "Start"
        Me.btnLiveStart.Location = New Drawing.Point(0, 0)
        Me.btnLiveStart.Size = New Drawing.Size(100, 30)

        Me.btnLiveStop.Text = "Stop"
        Me.btnLiveStop.Location = New Drawing.Point(110, 0)
        Me.btnLiveStop.Size = New Drawing.Size(100, 30)
        Me.btnLiveStop.Enabled = False

        Me.btnLiveSave.Text = "Save Transcript..."
        Me.btnLiveSave.Location = New Drawing.Point(220, 0)
        Me.btnLiveSave.Size = New Drawing.Size(130, 30)

        Me.btnLiveClear.Text = "Clear"
        Me.btnLiveClear.Location = New Drawing.Point(360, 0)
        Me.btnLiveClear.Size = New Drawing.Size(80, 30)

        pnlLiveButtons.Controls.AddRange({Me.btnLiveStart, Me.btnLiveStop, Me.btnLiveSave, Me.btnLiveClear})

        ' Real-time output
        Me.rtbLiveOutput.Location = New Drawing.Point(8, 231)
        Me.rtbLiveOutput.Size = New Drawing.Size(830, 382)
        Me.rtbLiveOutput.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        Me.rtbLiveOutput.[ReadOnly] = True
        Me.rtbLiveOutput.BackColor = Drawing.Color.Black
        Me.rtbLiveOutput.ForeColor = Drawing.Color.FromArgb(0, 255, 100)
        Me.rtbLiveOutput.Font = New Drawing.Font("Consolas", 11)
        Me.rtbLiveOutput.ScrollBars = RichTextBoxScrollBars.Vertical

        Me.tabPageLive.Controls.AddRange({Me.grpLiveInput, pnlLiveButtons, Me.rtbLiveOutput})

        ' ============================================
        ' TAB: Subtitle Server
        ' ============================================
        Me.tabPageServer.Text = "Subtitle Server"
        Me.tabPageServer.Padding = New Padding(8)
        Me.tabPageServer.ClientSize = tpSize

        Me.grpServerSettings = New GroupBox()
        Me.grpServerSettings.Text = "Server Settings"
        Me.grpServerSettings.Location = New Drawing.Point(8, 6)
        Me.grpServerSettings.Size = New Drawing.Size(830, 148)
        Me.grpServerSettings.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Dim svy = 22
        Me.lblServerPort = New Label()
        Me.lblServerPort.Text = "Port:"
        Me.lblServerPort.Location = New Drawing.Point(10, svy)
        Me.lblServerPort.AutoSize = True

        Me.nudServerPort = New NumericUpDown()
        Me.nudServerPort.Location = New Drawing.Point(10, svy + 16)
        Me.nudServerPort.Size = New Drawing.Size(80, 23)
        Me.nudServerPort.Minimum = 1024
        Me.nudServerPort.Maximum = 65535
        Me.nudServerPort.Value = 5080

        Me.btnServerStart = New Button()
        Me.btnServerStart.Text = "Start Server"
        Me.btnServerStart.Location = New Drawing.Point(110, svy + 15)
        Me.btnServerStart.Size = New Drawing.Size(110, 25)

        Me.btnServerStop = New Button()
        Me.btnServerStop.Text = "Stop Server"
        Me.btnServerStop.Location = New Drawing.Point(230, svy + 15)
        Me.btnServerStop.Size = New Drawing.Size(110, 25)
        Me.btnServerStop.Enabled = False

        Me.btnServerRestart = New Button()
        Me.btnServerRestart.Text = "Restart Server"
        Me.btnServerRestart.Location = New Drawing.Point(350, svy + 15)
        Me.btnServerRestart.Size = New Drawing.Size(110, 25)
        Me.btnServerRestart.Enabled = False

        Me.btnServerSimulate = New Button()
        Me.btnServerSimulate.Text = "Simulate"
        Me.btnServerSimulate.Location = New Drawing.Point(480, svy + 15)
        Me.btnServerSimulate.Size = New Drawing.Size(100, 25)
        Me.btnServerSimulate.Enabled = False

        Me.btnServerSimStop = New Button()
        Me.btnServerSimStop.Text = "Stop Sim"
        Me.btnServerSimStop.Location = New Drawing.Point(590, svy + 15)
        Me.btnServerSimStop.Size = New Drawing.Size(90, 25)
        Me.btnServerSimStop.Enabled = False

        svy += 48

        Me.lblSubtitleBg = New Label()
        Me.lblSubtitleBg.Text = "Background:"
        Me.lblSubtitleBg.Location = New Drawing.Point(10, svy)
        Me.lblSubtitleBg.AutoSize = True

        Me.btnSubtitleBg = New Button()
        Me.btnSubtitleBg.Location = New Drawing.Point(10, svy + 16)
        Me.btnSubtitleBg.Size = New Drawing.Size(80, 23)
        Me.btnSubtitleBg.BackColor = Drawing.Color.Black
        Me.btnSubtitleBg.FlatStyle = FlatStyle.Flat
        Me.btnSubtitleBg.FlatAppearance.BorderColor = Drawing.Color.Gray

        Me.lblSubtitleFg = New Label()
        Me.lblSubtitleFg.Text = "Text color:"
        Me.lblSubtitleFg.Location = New Drawing.Point(110, svy)
        Me.lblSubtitleFg.AutoSize = True

        Me.btnSubtitleFg = New Button()
        Me.btnSubtitleFg.Location = New Drawing.Point(110, svy + 16)
        Me.btnSubtitleFg.Size = New Drawing.Size(80, 23)
        Me.btnSubtitleFg.BackColor = Drawing.Color.White
        Me.btnSubtitleFg.FlatStyle = FlatStyle.Flat
        Me.btnSubtitleFg.FlatAppearance.BorderColor = Drawing.Color.Gray

        Me.lblSubtitleFont = New Label()
        Me.lblSubtitleFont.Text = "Font:"
        Me.lblSubtitleFont.Location = New Drawing.Point(210, svy)
        Me.lblSubtitleFont.AutoSize = True

        Me.cboSubtitleFont = New ComboBox()
        Me.cboSubtitleFont.Location = New Drawing.Point(210, svy + 16)
        Me.cboSubtitleFont.Size = New Drawing.Size(200, 23)
        Me.cboSubtitleFont.DropDownStyle = ComboBoxStyle.DropDownList
        For Each fam In Drawing.FontFamily.Families
            Me.cboSubtitleFont.Items.Add(fam.Name)
        Next
        Me.cboSubtitleFont.SelectedItem = "Segoe UI"

        Me.lblSubtitleSize = New Label()
        Me.lblSubtitleSize.Text = "Size:"
        Me.lblSubtitleSize.Location = New Drawing.Point(430, svy)
        Me.lblSubtitleSize.AutoSize = True

        Me.nudSubtitleSize = New NumericUpDown()
        Me.nudSubtitleSize.Location = New Drawing.Point(430, svy + 16)
        Me.nudSubtitleSize.Size = New Drawing.Size(55, 23)
        Me.nudSubtitleSize.Minimum = 8
        Me.nudSubtitleSize.Maximum = 72
        Me.nudSubtitleSize.Value = 12

        Me.chkSubtitleBold = New CheckBox()
        Me.chkSubtitleBold.Text = "Bold"
        Me.chkSubtitleBold.Location = New Drawing.Point(500, svy + 18)
        Me.chkSubtitleBold.AutoSize = True

        Me.grpServerSettings.Size = New Drawing.Size(830, 148)
        Me.grpServerSettings.Controls.AddRange({Me.lblServerPort, Me.nudServerPort,
            Me.btnServerStart, Me.btnServerStop, Me.btnServerRestart,
            Me.btnServerSimulate, Me.btnServerSimStop,
            Me.lblSubtitleBg, Me.btnSubtitleBg, Me.lblSubtitleFg, Me.btnSubtitleFg,
            Me.lblSubtitleFont, Me.cboSubtitleFont, Me.lblSubtitleSize, Me.nudSubtitleSize, Me.chkSubtitleBold})

        ' Connection info group
        Me.grpServerInfo = New GroupBox()
        Me.grpServerInfo.Text = "Connection Info"
        Me.grpServerInfo.Location = New Drawing.Point(8, 160)
        Me.grpServerInfo.Size = New Drawing.Size(830, 100)
        Me.grpServerInfo.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        Me.lblServerStatus = New Label()
        Me.lblServerStatus.Text = "Status: Stopped"
        Me.lblServerStatus.Location = New Drawing.Point(10, 25)
        Me.lblServerStatus.AutoSize = True
        Me.lblServerStatus.Font = New Drawing.Font("Segoe UI", 10, Drawing.FontStyle.Bold)

        Me.lblServerUrl = New Label()
        Me.lblServerUrl.Text = "URL: (not running)"
        Me.lblServerUrl.Location = New Drawing.Point(10, 50)
        Me.lblServerUrl.AutoSize = True
        Me.lblServerUrl.Font = New Drawing.Font("Consolas", 11)

        Me.lblServerClients = New Label()
        Me.lblServerClients.Text = "Connected clients: 0"
        Me.lblServerClients.Location = New Drawing.Point(10, 75)
        Me.lblServerClients.AutoSize = True

        Me.btnCopyUrl = New Button()
        Me.btnCopyUrl.Text = "Copy URL"
        Me.btnCopyUrl.Location = New Drawing.Point(700, 47)
        Me.btnCopyUrl.Size = New Drawing.Size(110, 25)
        Me.btnCopyUrl.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        Me.btnCopyUrl.Enabled = False

        Me.grpServerInfo.Controls.AddRange({Me.lblServerStatus, Me.lblServerUrl, Me.lblServerClients, Me.btnCopyUrl})

        ' Server log
        Me.rtbServerLog = New RichTextBox()
        Me.rtbServerLog.Location = New Drawing.Point(8, 266)
        Me.rtbServerLog.Size = New Drawing.Size(830, 347)
        Me.rtbServerLog.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        Me.rtbServerLog.[ReadOnly] = True
        Me.rtbServerLog.BackColor = Drawing.Color.Black
        Me.rtbServerLog.ForeColor = Drawing.Color.FromArgb(0, 200, 255)
        Me.rtbServerLog.Font = New Drawing.Font("Consolas", 10)
        Me.rtbServerLog.ScrollBars = RichTextBoxScrollBars.Vertical

        Me.tabPageServer.Controls.AddRange({Me.grpServerSettings, Me.grpServerInfo, Me.rtbServerLog})

        ' ============================================
        ' TAB 7 LAYOUT: Help
        ' ============================================
        Me.tabPageHelp.Text = "Help"
        Me.tabPageHelp.Padding = New Padding(8)
        Me.tabPageHelp.ClientSize = tpSize

        Me.rtbHelp = New RichTextBox()
        Me.rtbHelp.Dock = DockStyle.Fill
        Me.rtbHelp.ReadOnly = True
        Me.rtbHelp.BackColor = Drawing.Color.White
        Me.rtbHelp.Font = New Drawing.Font("Segoe UI", 10)
        Me.rtbHelp.BorderStyle = BorderStyle.None
        Me.tabPageHelp.Controls.Add(Me.rtbHelp)

        ' ============================================
        ' Form properties
        ' ============================================
        Me.AutoScaleDimensions = New Drawing.SizeF(7.0F, 15.0F)
        Me.AutoScaleMode = AutoScaleMode.Font
        Me.ClientSize = New Drawing.Size(880, 650)
        Me.MinimumSize = New Drawing.Size(880, 650)
        Me.Controls.Add(Me.tabMain)
        Dim ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
        Me.Text = $"Transcription Tools v{ver.Major}.{ver.Minor}.{ver.Build}"
        Me.Icon = Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.WindowState = FormWindowState.Maximized
        ' === System Tray ===
        Me.trayMenu = New ContextMenuStrip()
        Me.trayMenuShow = New ToolStripMenuItem("Show")
        Me.trayMenuExit = New ToolStripMenuItem("Exit")
        Me.trayMenu.Items.AddRange({Me.trayMenuShow, Me.trayMenuExit})

        Me.trayIcon = New NotifyIcon()
        Me.trayIcon.Icon = Me.Icon
        Me.trayIcon.Text = Me.Text
        Me.trayIcon.ContextMenuStrip = Me.trayMenu
        Me.trayIcon.Visible = True

        ' Send labels to back so they don't paint over adjacent controls
        Me.lblMode.SendToBack()
        For Each grp As Control In {Me.grpInput, Me.grpLanguageModel, Me.grpBeamSampling,
            Me.grpQualityFiltering, Me.grpSegmentControl, Me.grpPrompting, Me.grpVad,
            Me.grpPaths, Me.grpSettings, Me.grpLiveInput, Me.grpServerSettings}
            For Each child As Control In grp.Controls
                If TypeOf child Is Label Then child.SendToBack()
            Next
        Next

        Me.ResumeLayout(False)
    End Sub

    ' TabControl
    Friend WithEvents tabMain As TabControl
    Friend WithEvents tabPageJob As TabPage
    Friend WithEvents tabPageWhisper As TabPage
    Friend WithEvents tabPagePaths As TabPage
    Friend WithEvents tabPageLog As TabPage
    Friend WithEvents tabPageSettings As TabPage
    Friend WithEvents tabPageLive As TabPage
    Friend WithEvents tabPageHelp As TabPage

    ' Tooltip
    Friend WithEvents tipMain As ToolTip

    ' Tab 1: Main / Job
    Friend WithEvents lblMode As Label
    Friend WithEvents cboMode As ComboBox
    Friend WithEvents grpInput As GroupBox
    Friend WithEvents lblUrl As Label
    Friend WithEvents txtUrl As TextBox
    Friend WithEvents btnBrowseFile As Button
    Friend WithEvents lblStartTime As Label
    Friend WithEvents txtStartHH As TextBox
    Friend WithEvents lblStartColon1 As Label
    Friend WithEvents txtStartMM As TextBox
    Friend WithEvents lblStartColon2 As Label
    Friend WithEvents txtStartSS As TextBox
    Friend WithEvents lblEndTime As Label
    Friend WithEvents txtEndHH As TextBox
    Friend WithEvents lblEndColon1 As Label
    Friend WithEvents txtEndMM As TextBox
    Friend WithEvents lblEndColon2 As Label
    Friend WithEvents txtEndSS As TextBox
    Friend WithEvents lblOutputDir As Label
    Friend WithEvents txtOutputDir As TextBox
    Friend WithEvents btnBrowseOutput As Button
    Friend WithEvents lblInputLanguage As Label
    Friend WithEvents cboInputLanguage As ComboBox
    Friend WithEvents lblOutputLanguage As Label
    Friend WithEvents cboOutputLanguage As ComboBox
    Friend WithEvents lblModel As Label
    Friend WithEvents cboModel As ComboBox

    Friend WithEvents grpOutputFormats As GroupBox
    Friend WithEvents chkSrt As CheckBox
    Friend WithEvents chkVtt As CheckBox
    Friend WithEvents chkTxt As CheckBox
    Friend WithEvents chkJson As CheckBox
    Friend WithEvents chkCsv As CheckBox
    Friend WithEvents chkLrc As CheckBox

    Friend WithEvents grpProgress As GroupBox
    Friend WithEvents pbOverall As ProgressBar
    Friend WithEvents lblStepStatus As Label
    Friend WithEvents pbChunk As ProgressBar
    Friend WithEvents btnStart As Button
    Friend WithEvents btnResume As Button
    Friend WithEvents btnCancel As Button
    Friend WithEvents btnOpenOutput As Button
    Friend WithEvents btnOpenSubtitleEdit As Button
    Friend WithEvents lnkPreviewSrt As LinkLabel

    ' Tab 2: Whisper Parameters
    Friend WithEvents pnlWhisperScroll As Panel
    Friend WithEvents grpLanguageModel As GroupBox
    Friend WithEvents lblWLanguage As Label
    Friend WithEvents cboWLanguage As ComboBox
    Friend WithEvents grpBeamSampling As GroupBox
    Friend WithEvents lblThreads As Label
    Friend WithEvents nudThreads As NumericUpDown
    Friend WithEvents lblProcessors As Label
    Friend WithEvents nudProcessors As NumericUpDown
    Friend WithEvents lblBeamSize As Label
    Friend WithEvents nudBeamSize As NumericUpDown
    Friend WithEvents lblBestOf As Label
    Friend WithEvents nudBestOf As NumericUpDown
    Friend WithEvents lblTemperature As Label
    Friend WithEvents nudTemperature As NumericUpDown
    Friend WithEvents lblTemperatureInc As Label
    Friend WithEvents nudTemperatureInc As NumericUpDown
    Friend WithEvents grpQualityFiltering As GroupBox
    Friend WithEvents lblMaxContext As Label
    Friend WithEvents nudMaxContext As NumericUpDown
    Friend WithEvents lblWordThreshold As Label
    Friend WithEvents nudWordThreshold As NumericUpDown
    Friend WithEvents lblEntropyThreshold As Label
    Friend WithEvents nudEntropyThreshold As NumericUpDown
    Friend WithEvents lblLogProbThreshold As Label
    Friend WithEvents nudLogProbThreshold As NumericUpDown
    Friend WithEvents lblNoSpeechThreshold As Label
    Friend WithEvents nudNoSpeechThreshold As NumericUpDown
    Friend WithEvents grpSegmentControl As GroupBox
    Friend WithEvents lblMaxSegmentLength As Label
    Friend WithEvents nudMaxSegmentLength As NumericUpDown
    Friend WithEvents lblMaxTokens As Label
    Friend WithEvents nudMaxTokens As NumericUpDown
    Friend WithEvents lblAudioContext As Label
    Friend WithEvents nudAudioContext As NumericUpDown
    Friend WithEvents grpPrompting As GroupBox
    Friend WithEvents lblInitialPrompt As Label
    Friend WithEvents txtInitialPrompt As TextBox
    Friend WithEvents lblHotwords As Label
    Friend WithEvents txtHotwords As TextBox
    Friend WithEvents grpFlags As GroupBox
    Friend WithEvents chkSplitOnWord As CheckBox
    Friend WithEvents chkNoGpu As CheckBox
    Friend WithEvents chkFlashAttn As CheckBox
    Friend WithEvents chkPrintProgress As CheckBox
    Friend WithEvents chkPrintColours As CheckBox
    Friend WithEvents chkPrintRealtime As CheckBox
    Friend WithEvents chkDiarize As CheckBox
    Friend WithEvents chkTinydiarize As CheckBox
    Friend WithEvents chkNoTimestamps As CheckBox
    Friend WithEvents chkTranslate As CheckBox
    Friend WithEvents grpVad As GroupBox
    Friend WithEvents lblVadThreshold As Label
    Friend WithEvents nudVadThreshold As NumericUpDown
    Friend WithEvents lblFreqThreshold As Label
    Friend WithEvents nudFreqThreshold As NumericUpDown
    Friend WithEvents btnRestoreDefaults As Button

    ' Tab 3: Paths & Tools
    Friend WithEvents grpPaths As GroupBox
    Friend WithEvents lblPathWhisper As Label
    Friend WithEvents txtPathWhisper As TextBox
    Friend WithEvents btnBrowseWhisper As Button
    Friend WithEvents lblPathYtdlp As Label
    Friend WithEvents txtPathYtdlp As TextBox
    Friend WithEvents btnBrowseYtdlp As Button
    Friend WithEvents lblPathFfmpeg As Label
    Friend WithEvents txtPathFfmpeg As TextBox
    Friend WithEvents btnBrowseFfmpeg As Button
    Friend WithEvents lblPathFfprobe As Label
    Friend WithEvents txtPathFfprobe As TextBox
    Friend WithEvents btnBrowseFfprobe As Button
    Friend WithEvents lblPathModel As Label
    Friend WithEvents txtPathModel As TextBox
    Friend WithEvents btnBrowseModel As Button
    Friend WithEvents lblPathModelAudio As Label
    Friend WithEvents txtPathModelAudio As TextBox
    Friend WithEvents btnBrowseModelAudio As Button
    Friend WithEvents lblPathOutputRoot As Label
    Friend WithEvents txtPathOutputRoot As TextBox
    Friend WithEvents btnBrowseOutputRoot As Button
    Friend WithEvents lblYtdlpFormat As Label
    Friend WithEvents txtYtdlpFormat As TextBox
    Friend WithEvents lblPathStream As Label
    Friend WithEvents txtPathStream As TextBox
    Friend WithEvents btnBrowseStream As Button
    Friend WithEvents btnVerifyPaths As Button
    Friend WithEvents lnkDownloadModels As LinkLabel

    ' Tab 4: Log
    Friend WithEvents rtbLog As RichTextBox
    Friend WithEvents rtbHelp As RichTextBox
    Friend WithEvents btnClearLog As Button
    Friend WithEvents btnCopyLog As Button

    ' Tab 5: Settings
    Friend WithEvents grpSettings As GroupBox
    Friend WithEvents lblUiLanguage As Label
    Friend WithEvents cboUiLanguage As ComboBox
    Friend WithEvents lblParallelJobs As Label
    Friend WithEvents nudParallelJobs As NumericUpDown
    Friend WithEvents lblChunkSize As Label
    Friend WithEvents nudChunkSize As NumericUpDown
    Friend WithEvents lblPollInterval As Label
    Friend WithEvents nudPollInterval As NumericUpDown
    Friend WithEvents lblChunkTimeout As Label
    Friend WithEvents nudChunkTimeout As NumericUpDown
    Friend WithEvents chkKeepChunks As CheckBox
    Friend WithEvents chkKeepPreview As CheckBox
    Friend WithEvents chkSkipDownload As CheckBox
    Friend WithEvents lblTheme As Label
    Friend WithEvents cboTheme As ComboBox
    Friend WithEvents btnResetSettings As Button
    Friend WithEvents btnCheckToolUpdates As Button

    ' Subtitle Server tab
    Friend WithEvents tabPageServer As TabPage
    Friend WithEvents grpServerSettings As GroupBox
    Friend WithEvents lblServerPort As Label
    Friend WithEvents nudServerPort As NumericUpDown
    Friend WithEvents btnServerStart As Button
    Friend WithEvents btnServerStop As Button
    Friend WithEvents btnServerRestart As Button
    Friend WithEvents btnServerSimulate As Button
    Friend WithEvents btnServerSimStop As Button
    Friend WithEvents grpServerInfo As GroupBox
    Friend WithEvents lblServerStatus As Label
    Friend WithEvents lblServerUrl As Label
    Friend WithEvents lblServerClients As Label
    Friend WithEvents btnCopyUrl As Button
    Friend WithEvents rtbServerLog As RichTextBox
    Friend WithEvents lblSubtitleBg As Label
    Friend WithEvents btnSubtitleBg As Button
    Friend WithEvents lblSubtitleFg As Label
    Friend WithEvents btnSubtitleFg As Button
    Friend WithEvents lblSubtitleFont As Label
    Friend WithEvents cboSubtitleFont As ComboBox
    Friend WithEvents lblSubtitleSize As Label
    Friend WithEvents nudSubtitleSize As NumericUpDown
    Friend WithEvents chkSubtitleBold As CheckBox

    ' Live Translation tab
    Friend WithEvents grpLiveInput As GroupBox
    Friend WithEvents lblLiveDevice As Label
    Friend WithEvents cboLiveDevice As ComboBox
    Friend WithEvents btnRefreshDevices As Button
    Friend WithEvents lblLiveInputLang As Label
    Friend WithEvents cboLiveInputLang As ComboBox
    Friend WithEvents lblLiveOutputLang As Label
    Friend WithEvents cboLiveOutputLang As ComboBox
    Friend WithEvents lblLiveModel As Label
    Friend WithEvents cboLiveModel As ComboBox
    Friend WithEvents btnLiveStart As Button
    Friend WithEvents btnLiveStop As Button
    Friend WithEvents btnLiveSave As Button
    Friend WithEvents btnLiveClear As Button
    Friend WithEvents rtbLiveOutput As RichTextBox

    ' System Tray
    Friend WithEvents trayIcon As NotifyIcon
    Friend WithEvents trayMenu As ContextMenuStrip
    Friend WithEvents trayMenuShow As ToolStripMenuItem
    Friend WithEvents trayMenuExit As ToolStripMenuItem
End Class
