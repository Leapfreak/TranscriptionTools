Imports System.Globalization
Imports System.IO
Imports System.Text

Namespace Pipeline
    Public Class SrtMerger

        Public Shared Function Merge(chunkSrtPaths As IList(Of String),
                                      chunkStartsSec As IList(Of Double),
                                      globalOffsetSec As Double,
                                      outputPath As String) As Integer
            Dim globalIndex = 1
            Dim lastText = ""

            ' UTF-8 with BOM for SRT compatibility
            Using writer As New StreamWriter(outputPath, False, New UTF8Encoding(True))
                For i = 0 To chunkSrtPaths.Count - 1
                    Dim srtPath = chunkSrtPaths(i)
                    If Not File.Exists(srtPath) Then Continue For

                    Dim offsetSec = chunkStartsSec(i) + globalOffsetSec
                    Dim entries = ParseSrt(File.ReadAllLines(srtPath, Encoding.UTF8))

                    For Each entry In entries
                        Dim text = entry.Text.Trim()
                        If text = lastText Then Continue For
                        lastText = text

                        writer.WriteLine(globalIndex)
                        writer.WriteLine($"{SecToSrt(entry.StartSec + offsetSec)} --> {SecToSrt(entry.EndSec + offsetSec)}")
                        writer.WriteLine(text)
                        writer.WriteLine()
                        globalIndex += 1
                    Next
                Next
            End Using

            Return globalIndex - 1
        End Function

        Public Shared Function ParseSrt(lines As String()) As List(Of SrtEntry)
            Dim entries As New List(Of SrtEntry)
            Dim i = 0

            While i < lines.Length
                ' Skip blank lines
                If String.IsNullOrWhiteSpace(lines(i)) Then
                    i += 1
                    Continue While
                End If

                ' Index line
                Dim indexVal As Integer
                If Not Integer.TryParse(lines(i).Trim(), indexVal) Then
                    i += 1
                    Continue While
                End If
                i += 1

                ' Timestamp line
                If i >= lines.Length OrElse Not lines(i).Contains("-->") Then Continue While
                Dim tsParts = lines(i).Split({"-->"}, StringSplitOptions.None)
                If tsParts.Length < 2 Then
                    i += 1
                    Continue While
                End If

                Dim startSec = SrtToSec(tsParts(0).Trim())
                Dim endSec = SrtToSec(tsParts(1).Trim())
                i += 1

                ' Text lines
                Dim textLines As New List(Of String)
                While i < lines.Length AndAlso Not String.IsNullOrWhiteSpace(lines(i))
                    textLines.Add(lines(i))
                    i += 1
                End While

                entries.Add(New SrtEntry With {
                    .Index = indexVal,
                    .StartSec = startSec,
                    .EndSec = endSec,
                    .Text = String.Join(Environment.NewLine, textLines)
                })
            End While

            Return entries
        End Function

        Public Shared Function SrtToSec(ts As String) As Double
            ts = ts.Replace(",", ".").Trim()
            Dim parts = ts.Split(":"c)
            If parts.Length < 3 Then Return 0

            Dim hours As Integer, minutes As Integer
            Dim seconds As Double
            Integer.TryParse(parts(0), hours)
            Integer.TryParse(parts(1), minutes)
            Double.TryParse(parts(2), NumberStyles.Any, CultureInfo.InvariantCulture, seconds)

            Return hours * 3600.0 + minutes * 60.0 + seconds
        End Function

        Public Shared Function SecToSrt(s As Double) As String
            If s < 0 Then s = 0
            Dim totalMs = CLng(Math.Round(s * 1000))
            Dim ms = CInt(totalMs Mod 1000)
            Dim totalSec = CInt(totalMs \ 1000)
            Dim sec = totalSec Mod 60
            Dim min = (totalSec \ 60) Mod 60
            Dim hr = totalSec \ 3600
            Return $"{hr:D2}:{min:D2}:{sec:D2},{ms:D3}"
        End Function

        Public Class SrtEntry
            Public Property Index As Integer
            Public Property StartSec As Double
            Public Property EndSec As Double
            Public Property Text As String = ""
        End Class
    End Class
End Namespace
