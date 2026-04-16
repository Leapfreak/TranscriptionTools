Imports System.IO
Imports System.Text.Json

Namespace Models
    Public Class ConfigManager
        Private Shared ReadOnly ConfigDir As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TranscriptionTools")
        Private Shared ReadOnly ConfigPath As String = Path.Combine(ConfigDir, "config.json")

        Private Shared ReadOnly JsonOptions As New JsonSerializerOptions With {
            .WriteIndented = True,
            .PropertyNameCaseInsensitive = True
        }

        Public Shared Function Load() As AppConfig
            Try
                If Not File.Exists(ConfigPath) Then Return New AppConfig()
                Dim json = File.ReadAllText(ConfigPath)
                Dim cfg = JsonSerializer.Deserialize(Of AppConfig)(json, JsonOptions)
                If cfg Is Nothing Then Return New AppConfig()
                ApplyDefaults(cfg)
                Return cfg
            Catch
                Return New AppConfig()
            End Try
        End Function

        Private Shared Sub ApplyDefaults(cfg As AppConfig)
            If String.IsNullOrEmpty(cfg.SubtitleBgColor) OrElse Not cfg.SubtitleBgColor.StartsWith("#") Then cfg.SubtitleBgColor = "#000000"
            If String.IsNullOrEmpty(cfg.SubtitleFgColor) OrElse Not cfg.SubtitleFgColor.StartsWith("#") Then cfg.SubtitleFgColor = "#FFFFFF"

            ' Migrate old flat paths to whisper\ subdirectory
            If cfg.PathWhisper IsNot Nothing AndAlso cfg.PathWhisper.EndsWith("\whisper-cli.exe") AndAlso
               Not cfg.PathWhisper.EndsWith("\whisper\whisper-cli.exe") Then
                cfg.PathWhisper = cfg.PathWhisper.Replace("\whisper-cli.exe", "\whisper\whisper-cli.exe")
            End If
            If cfg.PathStream IsNot Nothing AndAlso cfg.PathStream.EndsWith("\whisper-stream.exe") AndAlso
               Not cfg.PathStream.EndsWith("\whisper\whisper-stream.exe") Then
                cfg.PathStream = cfg.PathStream.Replace("\whisper-stream.exe", "\whisper\whisper-stream.exe")
            End If
        End Sub

        Public Shared Sub Save(config As AppConfig)
            Try
                If Not Directory.Exists(ConfigDir) Then
                    Directory.CreateDirectory(ConfigDir)
                End If
                Dim json = JsonSerializer.Serialize(config, JsonOptions)
                File.WriteAllText(ConfigPath, json)
            Catch
                ' Silently fail - logged elsewhere
            End Try
        End Sub

        Public Shared Sub Reset()
            Try
                If File.Exists(ConfigPath) Then File.Delete(ConfigPath)
            Catch
            End Try
        End Sub
    End Class
End Namespace
