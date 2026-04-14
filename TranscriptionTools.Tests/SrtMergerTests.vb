Imports Xunit
Imports TranscriptionTools.Pipeline

Public Class SrtMergerTests

    <Fact>
    Public Sub SrtToSec_ValidTimestamp_ReturnsCorrectSeconds()
        Assert.Equal(3723.456, SrtMerger.SrtToSec("01:02:03,456"), 2)
    End Sub

    <Fact>
    Public Sub SrtToSec_ZeroTimestamp_ReturnsZero()
        Assert.Equal(0.0, SrtMerger.SrtToSec("00:00:00,000"), 2)
    End Sub

    <Fact>
    Public Sub SrtToSec_WithPeriod_HandlesVttFormat()
        Assert.Equal(3723.456, SrtMerger.SrtToSec("01:02:03.456"), 2)
    End Sub

    <Fact>
    Public Sub SecToSrt_ValidSeconds_ReturnsCorrectTimestamp()
        Assert.Equal("01:02:03,456", SrtMerger.SecToSrt(3723.456))
    End Sub

    <Fact>
    Public Sub SecToSrt_Zero_ReturnsZeroTimestamp()
        Assert.Equal("00:00:00,000", SrtMerger.SecToSrt(0.0))
    End Sub

    <Fact>
    Public Sub SecToSrt_Negative_ClampsToZero()
        Assert.Equal("00:00:00,000", SrtMerger.SecToSrt(-5.0))
    End Sub

    <Fact>
    Public Sub ParseSrt_ValidSrt_ReturnsEntries()
        Dim lines = {
            "1",
            "00:00:01,000 --> 00:00:03,000",
            "Hello world",
            "",
            "2",
            "00:00:04,000 --> 00:00:06,000",
            "Second line",
            ""
        }

        Dim entries = SrtMerger.ParseSrt(lines)

        Assert.Equal(2, entries.Count)
        Assert.Equal("Hello world", entries(0).Text)
        Assert.Equal(1.0, entries(0).StartSec, 1)
        Assert.Equal(3.0, entries(0).EndSec, 1)
        Assert.Equal("Second line", entries(1).Text)
    End Sub

    <Fact>
    Public Sub ParseSrt_MultiLineText_PreservesLines()
        Dim lines = {
            "1",
            "00:00:01,000 --> 00:00:03,000",
            "Line one",
            "Line two",
            ""
        }

        Dim entries = SrtMerger.ParseSrt(lines)

        Assert.Single(entries)
        Assert.Contains("Line one", entries(0).Text)
        Assert.Contains("Line two", entries(0).Text)
    End Sub

    <Fact>
    Public Sub ParseSrt_EmptyInput_ReturnsEmpty()
        Dim entries = SrtMerger.ParseSrt(Array.Empty(Of String)())
        Assert.Empty(entries)
    End Sub

    <Fact>
    Public Sub Merge_SingleChunk_WritesCorrectOutput()
        Dim tempDir = IO.Path.Combine(IO.Path.GetTempPath(), "srtmerge_test_" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(tempDir)

        Try
            Dim srtPath = IO.Path.Combine(tempDir, "chunk_000.wav.srt")
            IO.File.WriteAllText(srtPath,
                "1" & vbCrLf &
                "00:00:01,000 --> 00:00:03,000" & vbCrLf &
                "Hello" & vbCrLf &
                vbCrLf &
                "2" & vbCrLf &
                "00:00:04,000 --> 00:00:06,000" & vbCrLf &
                "World" & vbCrLf)

            Dim outputPath = IO.Path.Combine(tempDir, "merged.srt")
            Dim count = SrtMerger.Merge(
                {srtPath},
                {0.0},
                0.0,
                outputPath)

            Assert.Equal(2, count)
            Assert.True(IO.File.Exists(outputPath))

            Dim content = IO.File.ReadAllText(outputPath)
            Assert.Contains("Hello", content)
            Assert.Contains("World", content)
        Finally
            IO.Directory.Delete(tempDir, True)
        End Try
    End Sub

    <Fact>
    Public Sub Merge_WithOffset_AppliesTimeOffset()
        Dim tempDir = IO.Path.Combine(IO.Path.GetTempPath(), "srtmerge_test_" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(tempDir)

        Try
            Dim srtPath = IO.Path.Combine(tempDir, "chunk_000.wav.srt")
            IO.File.WriteAllText(srtPath,
                "1" & vbCrLf &
                "00:00:01,000 --> 00:00:03,000" & vbCrLf &
                "Test" & vbCrLf)

            Dim outputPath = IO.Path.Combine(tempDir, "merged.srt")
            SrtMerger.Merge(
                {srtPath},
                {300.0},  ' 5 minute chunk offset
                60.0,     ' 1 minute global offset
                outputPath)

            Dim content = IO.File.ReadAllText(outputPath)
            ' 1s + 300s + 60s = 361s = 00:06:01,000
            Assert.Contains("00:06:01,000", content)
        Finally
            IO.Directory.Delete(tempDir, True)
        End Try
    End Sub

    <Fact>
    Public Sub Merge_DuplicateText_Deduplicates()
        Dim tempDir = IO.Path.Combine(IO.Path.GetTempPath(), "srtmerge_test_" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(tempDir)

        Try
            Dim srt1Path = IO.Path.Combine(tempDir, "chunk_000.wav.srt")
            IO.File.WriteAllText(srt1Path,
                "1" & vbCrLf &
                "00:00:01,000 --> 00:00:03,000" & vbCrLf &
                "Same text" & vbCrLf)

            Dim srt2Path = IO.Path.Combine(tempDir, "chunk_001.wav.srt")
            IO.File.WriteAllText(srt2Path,
                "1" & vbCrLf &
                "00:00:00,000 --> 00:00:02,000" & vbCrLf &
                "Same text" & vbCrLf)

            Dim outputPath = IO.Path.Combine(tempDir, "merged.srt")
            Dim count = SrtMerger.Merge(
                {srt1Path, srt2Path},
                {0.0, 300.0},
                0.0,
                outputPath)

            ' Should only have 1 entry since text is identical
            Assert.Equal(1, count)
        Finally
            IO.Directory.Delete(tempDir, True)
        End Try
    End Sub

    <Fact>
    Public Sub Merge_MissingFile_SkipsGracefully()
        Dim tempDir = IO.Path.Combine(IO.Path.GetTempPath(), "srtmerge_test_" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(tempDir)

        Try
            Dim srt1Path = IO.Path.Combine(tempDir, "chunk_000.wav.srt")
            IO.File.WriteAllText(srt1Path,
                "1" & vbCrLf &
                "00:00:01,000 --> 00:00:03,000" & vbCrLf &
                "Exists" & vbCrLf)

            Dim missingPath = IO.Path.Combine(tempDir, "chunk_001.wav.srt")

            Dim outputPath = IO.Path.Combine(tempDir, "merged.srt")
            Dim count = SrtMerger.Merge(
                {srt1Path, missingPath},
                {0.0, 300.0},
                0.0,
                outputPath)

            Assert.Equal(1, count)
        Finally
            IO.Directory.Delete(tempDir, True)
        End Try
    End Sub

    <Fact>
    Public Sub SrtToSec_RoundTrip_Preserves()
        Dim original = 7384.567
        Dim srtStr = SrtMerger.SecToSrt(original)
        Dim roundTripped = SrtMerger.SrtToSec(srtStr)

        Assert.Equal(original, roundTripped, 2)
    End Sub
End Class
