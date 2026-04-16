Imports System.Collections.Concurrent
Imports System.IO
Imports System.Net
Imports System.Net.Security
Imports System.Net.Sockets
Imports System.Net.WebSockets
Imports System.Security.Cryptography
Imports System.Security.Cryptography.X509Certificates
Imports System.Text
Imports System.Threading

Namespace Pipeline
    Public Class SubtitleServer

        Public Event StatusChanged As EventHandler(Of String)
        Public Event RemoteCommand As EventHandler(Of String)

        Private _listener As HttpListener
        Private _httpsListener As TcpListener
        Private _httpsCert As X509Certificate2
        Private _cts As CancellationTokenSource
        Private ReadOnly _clients As New ConcurrentDictionary(Of String, WebSocket)()
        Private _port As Integer = 5080
        Private _httpsPort As Integer = 5081
        Private _isRunning As Boolean = False
        Private Const CertPassword As String = "transcription-tools-cert"
        Private _currentLine As String = ""
        Private ReadOnly _committedLines As New ConcurrentQueue(Of String)()
        Private Const MaxCommittedLines As Integer = 200

        Public ReadOnly Property IsRunning As Boolean
            Get
                Return _isRunning
            End Get
        End Property

        Public ReadOnly Property Port As Integer
            Get
                Return _port
            End Get
        End Property

        Public ReadOnly Property HttpsPort As Integer
            Get
                Return _httpsPort
            End Get
        End Property

        Public ReadOnly Property ConnectedClients As Integer
            Get
                Return _clients.Count
            End Get
        End Property

        Public Sub Start(port As Integer, Optional allowRemote As Boolean = True)
            If _isRunning Then Return

            _port = port
            _httpsPort = port + 1
            _cts = New CancellationTokenSource()

            If allowRemote Then
                If Not TryStartRemote() Then
                    ' All remote attempts failed — fall back to localhost
                    RaiseEvent StatusChanged(Me, "Falling back to localhost only...")
                    TryStartLocalhost()
                End If
            Else
                TryStartLocalhost()
            End If

            ' Try to also start HTTPS so Wake Lock API works on phones
            If _isRunning AndAlso allowRemote Then
                TryStartHttps()
            End If
        End Sub

        Private Function TryStartRemote() As Boolean
            ' Try binding to all interfaces (requires admin or URL ACL)
            _listener = New HttpListener()
            _listener.Prefixes.Add($"http://+:{_port}/")

            Try
                _listener.Start()
                _isRunning = True
                RaiseEvent StatusChanged(Me, "Server started")
                Task.Run(Sub() AcceptLoop(_listener, _cts.Token), _cts.Token)
                Return True
            Catch ex As HttpListenerException
                ' Access denied — try to add a URL reservation via elevated netsh
                RaiseEvent StatusChanged(Me, "Access denied - requesting permission via UAC prompt...")
            End Try

            If TryAddUrlReservation(_port) Then
                ' Create a fresh listener — the old one is disposed after failure
                _listener = New HttpListener()
                _listener.Prefixes.Add($"http://+:{_port}/")
                Try
                    _listener.Start()
                    _isRunning = True
                    RaiseEvent StatusChanged(Me, "Server started")
                    Task.Run(Sub() AcceptLoop(_listener, _cts.Token), _cts.Token)
                    Return True
                Catch ex2 As HttpListenerException
                    RaiseEvent StatusChanged(Me, $"Failed to start on all interfaces: {ex2.Message}")
                End Try
            End If

            Return False
        End Function

        Private Sub TryStartLocalhost()
            _listener = New HttpListener()
            _listener.Prefixes.Add($"http://localhost:{_port}/")
            Try
                _listener.Start()
                _isRunning = True
                RaiseEvent StatusChanged(Me, "Server started (localhost only)")
                Task.Run(Sub() AcceptLoop(_listener, _cts.Token), _cts.Token)
            Catch ex As Exception
                _isRunning = False
                RaiseEvent StatusChanged(Me, $"Failed to start: {ex.Message}")
            End Try
        End Sub

        Private Function TryAddUrlReservation(port As Integer) As Boolean
            Try
                ' Delete any existing reservation first, then re-add
                Dim cmd = $"/c netsh http delete urlacl url=http://+:{port}/ & netsh http add urlacl url=http://+:{port}/ user=Everyone"
                Dim psi As New ProcessStartInfo() With {
                    .FileName = "cmd.exe",
                    .Arguments = cmd,
                    .Verb = "runas",
                    .UseShellExecute = True,
                    .CreateNoWindow = True,
                    .WindowStyle = ProcessWindowStyle.Hidden
                }
                Dim proc = Process.Start(psi)
                proc.WaitForExit(10000)
                Dim success = proc.ExitCode = 0
                proc.Dispose()
                If success Then
                    RaiseEvent StatusChanged(Me, "URL reservation added successfully.")
                Else
                    RaiseEvent StatusChanged(Me, "URL reservation failed or was denied.")
                End If
                Return success
            Catch
                ' User declined UAC or other error
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Start an HTTPS server using raw TcpListener + SslStream.
        ''' No admin privileges, no netsh, no cert store — just works.
        ''' </summary>
        Private Sub TryStartHttps()
            Try
                _httpsCert = GetOrCreateCertificate()
                If _httpsCert Is Nothing Then
                    RaiseEvent StatusChanged(Me, "HTTPS: could not create certificate")
                    Return
                End If

                _httpsListener = New TcpListener(IPAddress.Any, _httpsPort)
                _httpsListener.Start()
                Task.Run(Sub() HttpsAcceptLoop(_cts.Token), _cts.Token)
                RaiseEvent StatusChanged(Me, $"HTTPS enabled on port {_httpsPort}")
            Catch ex As Exception
                RaiseEvent StatusChanged(Me, $"HTTPS failed: {ex.Message}")
                Try : _httpsListener?.Stop() : Catch : End Try
                _httpsListener = Nothing
            End Try
        End Sub

        Private Async Sub HttpsAcceptLoop(ct As CancellationToken)
            While Not ct.IsCancellationRequested
                Try
                    Dim client = Await _httpsListener.AcceptTcpClientAsync(ct).ConfigureAwait(False)
                    Dim unused = Task.Run(Sub() HandleHttpsClient(client, ct), ct)
                Catch ex As OperationCanceledException
                    Exit While
                Catch ex As ObjectDisposedException
                    Exit While
                Catch
                    If ct.IsCancellationRequested Then Exit While
                End Try
            End While
        End Sub

        Private Async Sub HandleHttpsClient(client As TcpClient, ct As CancellationToken)
            Try
                client.NoDelay = True
                Dim sslStream As New SslStream(client.GetStream(), False)
                Try
                    Await sslStream.AuthenticateAsServerAsync(_httpsCert).ConfigureAwait(False)
                Catch
                    sslStream.Dispose()
                    client.Dispose()
                    Return
                End Try

                ' Read the HTTP request line + headers
                Dim headerText = Await ReadHttpHeaders(sslStream, ct).ConfigureAwait(False)
                If headerText Is Nothing Then
                    sslStream.Dispose()
                    client.Dispose()
                    Return
                End If

                ' Parse request line and headers
                Dim lines = headerText.Split({vbCrLf}, StringSplitOptions.None)
                Dim requestLine = If(lines.Length > 0, lines(0), "")
                Dim path = "/"
                Dim parts = requestLine.Split(" "c)
                If parts.Length >= 2 Then path = parts(1)

                Dim headers As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                For i = 1 To lines.Length - 1
                    Dim colonIdx = lines(i).IndexOf(":"c)
                    If colonIdx > 0 Then
                        headers(lines(i).Substring(0, colonIdx).Trim()) = lines(i).Substring(colonIdx + 1).Trim()
                    End If
                Next

                ' Route the request
                If headers.ContainsKey("Upgrade") AndAlso
                   headers("Upgrade").Equals("websocket", StringComparison.OrdinalIgnoreCase) Then
                    ' WebSocket upgrade
                    Dim wsKey = If(headers.ContainsKey("Sec-WebSocket-Key"), headers("Sec-WebSocket-Key"), "")
                    Dim acceptKey = ComputeWebSocketAccept(wsKey)
                    Dim response = "HTTP/1.1 101 Switching Protocols" & vbCrLf &
                                   "Upgrade: websocket" & vbCrLf &
                                   "Connection: Upgrade" & vbCrLf &
                                   "Sec-WebSocket-Accept: " & acceptKey & vbCrLf & vbCrLf
                    Dim respBytes = Encoding.ASCII.GetBytes(response)
                    Await sslStream.WriteAsync(respBytes, 0, respBytes.Length, ct).ConfigureAwait(False)
                    Await sslStream.FlushAsync(ct).ConfigureAwait(False)

                    ' Create managed WebSocket from the SSL stream
                    Dim ws = WebSocket.CreateFromStream(sslStream, New WebSocketCreationOptions With {
                        .IsServer = True,
                        .KeepAliveInterval = TimeSpan.FromSeconds(30)
                    })
                    Await HandleWebSocketStream(ws, ct).ConfigureAwait(False)
                    ' sslStream/client are owned by the WebSocket now — disposed when ws is disposed
                ElseIf path = "/nosleep.wav" Then
                    Dim wav = BuildSilentWav()
                    Dim header = "HTTP/1.1 200 OK" & vbCrLf &
                                 "Content-Type: audio/wav" & vbCrLf &
                                 $"Content-Length: {wav.Length}" & vbCrLf &
                                 "Cache-Control: public, max-age=86400" & vbCrLf &
                                 "Connection: close" & vbCrLf & vbCrLf
                    Dim hdrBytes = Encoding.ASCII.GetBytes(header)
                    Await sslStream.WriteAsync(hdrBytes, 0, hdrBytes.Length, ct).ConfigureAwait(False)
                    Await sslStream.WriteAsync(wav, 0, wav.Length, ct).ConfigureAwait(False)
                    sslStream.Dispose()
                    client.Dispose()
                ElseIf path = "/cert" Then
                    Dim certBytes = _httpsCert.Export(X509ContentType.Cert)
                    Dim header = "HTTP/1.1 200 OK" & vbCrLf &
                                 "Content-Type: application/x-x509-ca-cert" & vbCrLf &
                                 "Content-Disposition: attachment; filename=""TranscriptionTools.crt""" & vbCrLf &
                                 $"Content-Length: {certBytes.Length}" & vbCrLf &
                                 "Connection: close" & vbCrLf & vbCrLf
                    Dim hdrBytes = Encoding.ASCII.GetBytes(header)
                    Await sslStream.WriteAsync(hdrBytes, 0, hdrBytes.Length, ct).ConfigureAwait(False)
                    Await sslStream.WriteAsync(certBytes, 0, certBytes.Length, ct).ConfigureAwait(False)
                    sslStream.Dispose()
                    client.Dispose()
                Else
                    ' Serve the HTML page
                    Dim html = GetHtmlPage()
                    Dim htmlBytes = Encoding.UTF8.GetBytes(html)
                    Dim header = "HTTP/1.1 200 OK" & vbCrLf &
                                 "Content-Type: text/html; charset=utf-8" & vbCrLf &
                                 $"Content-Length: {htmlBytes.Length}" & vbCrLf &
                                 "Cache-Control: no-store, no-cache, must-revalidate" & vbCrLf &
                                 "Pragma: no-cache" & vbCrLf &
                                 "Connection: close" & vbCrLf & vbCrLf
                    Dim hdrBytes = Encoding.ASCII.GetBytes(header)
                    Await sslStream.WriteAsync(hdrBytes, 0, hdrBytes.Length, ct).ConfigureAwait(False)
                    Await sslStream.WriteAsync(htmlBytes, 0, htmlBytes.Length, ct).ConfigureAwait(False)
                    sslStream.Dispose()
                    client.Dispose()
                End If
            Catch
                Try : client?.Dispose() : Catch : End Try
            End Try
        End Sub

        Private Shared Async Function ReadHttpHeaders(stream As Stream, ct As CancellationToken) As Task(Of String)
            Dim sb As New StringBuilder()
            Dim buf = New Byte(0) {}
            Dim endMarker = vbCrLf & vbCrLf
            While sb.Length < 8192
                Dim bytesRead = Await stream.ReadAsync(buf, 0, 1, ct).ConfigureAwait(False)
                If bytesRead = 0 Then Return Nothing
                sb.Append(ChrW(buf(0)))
                If sb.Length >= 4 AndAlso sb.ToString().EndsWith(endMarker) Then
                    Return sb.ToString(0, sb.Length - endMarker.Length)
                End If
            End While
            Return Nothing
        End Function

        Private Shared Function ComputeWebSocketAccept(key As String) As String
            Dim magic = key & "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
            Using sha1 As System.Security.Cryptography.SHA1 = System.Security.Cryptography.SHA1.Create()
                Return Convert.ToBase64String(sha1.ComputeHash(Encoding.ASCII.GetBytes(magic)))
            End Using
        End Function

        ''' <summary>
        ''' Handle a WebSocket connection from the HTTPS TcpListener path.
        ''' Same logic as HandleWebSocket but uses a WebSocket directly instead of HttpListenerContext.
        ''' </summary>
        Private Async Function HandleWebSocketStream(ws As WebSocket, ct As CancellationToken) As Task
            Dim clientId = Guid.NewGuid().ToString()
            _clients.TryAdd(clientId, ws)
            RaiseEvent StatusChanged(Me, $"Client connected ({_clients.Count} total)")

            ' Send history
            Try
                For Each line In _committedLines
                    Dim json = $"{{""type"":""commit"",""text"":{EscapeJson(line)}}}"
                    Dim buf = Encoding.UTF8.GetBytes(json)
                    Await ws.SendAsync(New ArraySegment(Of Byte)(buf), WebSocketMessageType.Text, True, ct).ConfigureAwait(False)
                Next
                If _currentLine.Length > 0 Then
                    Dim json = $"{{""type"":""update"",""text"":{EscapeJson(_currentLine)}}}"
                    Dim buf = Encoding.UTF8.GetBytes(json)
                    Await ws.SendAsync(New ArraySegment(Of Byte)(buf), WebSocketMessageType.Text, True, ct).ConfigureAwait(False)
                End If
            Catch
            End Try

            ' Keep alive by reading
            Dim recvBuf = New Byte(1023) {}
            Try
                While ws.State = WebSocketState.Open AndAlso Not ct.IsCancellationRequested
                    Await ws.ReceiveAsync(New ArraySegment(Of Byte)(recvBuf), ct).ConfigureAwait(False)
                End While
            Catch
            End Try

            Dim removed As WebSocket = Nothing
            _clients.TryRemove(clientId, removed)
            removed?.Dispose()
            RaiseEvent StatusChanged(Me, $"Client disconnected ({_clients.Count} total)")
        End Function

        Private Function GetOrCreateCertificate() As X509Certificate2
            Dim certDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TranscriptionTools")
            Dim certPath = Path.Combine(certDir, "subtitle-server.pfx")

            ' Try to load existing cert
            If File.Exists(certPath) Then
                Try
                    Dim cert As New X509Certificate2(certPath, CertPassword, X509KeyStorageFlags.Exportable)
                    If cert.NotAfter > DateTime.Now.AddDays(30) Then Return cert
                    cert.Dispose()
                Catch
                End Try
            End If

            ' Generate a new self-signed certificate
            Directory.CreateDirectory(certDir)
            Using rsa As RSA = RSA.Create(2048)
                Dim req As New CertificateRequest("CN=Transcription Tools Subtitle Server", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                req.CertificateExtensions.Add(New X509BasicConstraintsExtension(True, False, 0, True))
                req.CertificateExtensions.Add(New X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature Or X509KeyUsageFlags.KeyEncipherment, False))
                req.CertificateExtensions.Add(New X509EnhancedKeyUsageExtension(New OidCollection From {New Oid("1.3.6.1.5.5.7.3.1")}, False))

                ' Add SAN with localhost + all local IPs
                Dim san As New SubjectAlternativeNameBuilder()
                san.AddDnsName("localhost")
                Try
                    For Each addr In Dns.GetHostAddresses(Dns.GetHostName())
                        If addr.AddressFamily = Net.Sockets.AddressFamily.InterNetwork Then
                            san.AddIpAddress(addr)
                        End If
                    Next
                Catch
                End Try
                req.CertificateExtensions.Add(san.Build())

                Dim cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(10))
                Dim pfxBytes = cert.Export(X509ContentType.Pfx, CertPassword)
                File.WriteAllBytes(certPath, pfxBytes)
                Return New X509Certificate2(pfxBytes, CertPassword, X509KeyStorageFlags.Exportable)
            End Using
        End Function

        Public Sub [Stop]()
            If Not _isRunning Then Return

            _cts?.Cancel()

            ' Close all WebSocket connections
            For Each kvp In _clients
                Try
                    Dim ws = kvp.Value
                    If ws.State = WebSocketState.Open Then
                        ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server stopping", CancellationToken.None).Wait(1000)
                    End If
                    ws.Dispose()
                Catch
                End Try
            Next
            _clients.Clear()

            Try
                _listener?.Stop()
                _listener?.Close()
            Catch
            End Try

            Try
                _httpsListener?.Stop()
            Catch
            End Try

            _httpsCert?.Dispose()
            _httpsCert = Nothing

            _isRunning = False
            RaiseEvent StatusChanged(Me, "Server stopped")
        End Sub

        Public Sub BroadcastUpdate(text As String)
            If Not _isRunning Then Return
            _currentLine = text
            Dim json = $"{{""type"":""update"",""text"":{EscapeJson(text)}}}"
            BroadcastMessage(json)
        End Sub

        Public Sub BroadcastCommit(text As String)
            If Not _isRunning Then Return
            _currentLine = ""
            _committedLines.Enqueue(text)
            While _committedLines.Count > MaxCommittedLines
                Dim discard As String = Nothing
                _committedLines.TryDequeue(discard)
            End While
            Dim json = $"{{""type"":""commit"",""text"":{EscapeJson(text)}}}"
            BroadcastMessage(json)
        End Sub

        Private Sub BroadcastMessage(json As String)
            Dim buffer = Encoding.UTF8.GetBytes(json)
            Dim segment = New ArraySegment(Of Byte)(buffer)
            Dim deadKeys As New List(Of String)

            For Each kvp In _clients
                Try
                    Dim ws = kvp.Value
                    If ws.State = WebSocketState.Open Then
                        ws.SendAsync(segment, WebSocketMessageType.Text, True, CancellationToken.None).Wait(500)
                    Else
                        deadKeys.Add(kvp.Key)
                    End If
                Catch
                    deadKeys.Add(kvp.Key)
                End Try
            Next

            For Each key In deadKeys
                Dim removed As WebSocket = Nothing
                _clients.TryRemove(key, removed)
                removed?.Dispose()
            Next

            If deadKeys.Count > 0 Then
                RaiseEvent StatusChanged(Me, $"Clients: {_clients.Count}")
            End If
        End Sub

        Private Async Sub AcceptLoop(listener As HttpListener, ct As CancellationToken)
            While Not ct.IsCancellationRequested
                Try
                    Dim ctx = Await listener.GetContextAsync().ConfigureAwait(False)

                    If ct.IsCancellationRequested Then Exit While

                    If ctx.Request.IsWebSocketRequest Then
                        ' WebSocket upgrade (fire-and-forget per client)
                        Dim unused = Task.Run(Sub() HandleWebSocket(ctx, ct), ct)
                    ElseIf ctx.Request.Url.AbsolutePath = "/nosleep.wav" Then
                        ServeNoSleepAudio(ctx)
                    ElseIf ctx.Request.Url.AbsolutePath = "/cert" Then
                        ServeCertificate(ctx)
                    ElseIf ctx.Request.Url.AbsolutePath.StartsWith("/api/control") Then
                        HandleApiControl(ctx)
                    Else
                        ' Serve the HTML page
                        ServeHtml(ctx)
                    End If
                Catch ex As ObjectDisposedException
                    Exit While
                Catch ex As HttpListenerException
                    If ct.IsCancellationRequested Then Exit While
                Catch
                    If ct.IsCancellationRequested Then Exit While
                End Try
            End While
        End Sub

        Private Async Sub HandleWebSocket(ctx As HttpListenerContext, ct As CancellationToken)
            Dim wsCtx As HttpListenerWebSocketContext = Nothing
            Try
                wsCtx = Await ctx.AcceptWebSocketAsync(Nothing).ConfigureAwait(False)
            Catch
                ctx.Response.StatusCode = 500
                ctx.Response.Close()
                Return
            End Try

            Dim ws = wsCtx.WebSocket
            Dim clientId = Guid.NewGuid().ToString()
            _clients.TryAdd(clientId, ws)
            RaiseEvent StatusChanged(Me, $"Client connected ({_clients.Count} total)")

            ' Send history to new client
            Try
                For Each line In _committedLines
                    Dim json = $"{{""type"":""commit"",""text"":{EscapeJson(line)}}}"
                    Dim buf = Encoding.UTF8.GetBytes(json)
                    Await ws.SendAsync(New ArraySegment(Of Byte)(buf), WebSocketMessageType.Text, True, ct).ConfigureAwait(False)
                Next
                If _currentLine.Length > 0 Then
                    Dim json = $"{{""type"":""update"",""text"":{EscapeJson(_currentLine)}}}"
                    Dim buf = Encoding.UTF8.GetBytes(json)
                    Await ws.SendAsync(New ArraySegment(Of Byte)(buf), WebSocketMessageType.Text, True, ct).ConfigureAwait(False)
                End If
            Catch
            End Try

            ' Keep connection alive by reading (WebSocket protocol requires it)
            Dim recvBuf = New Byte(1023) {}
            Try
                While ws.State = WebSocketState.Open AndAlso Not ct.IsCancellationRequested
                    Await ws.ReceiveAsync(New ArraySegment(Of Byte)(recvBuf), ct).ConfigureAwait(False)
                End While
            Catch
            End Try

            Dim removed As WebSocket = Nothing
            _clients.TryRemove(clientId, removed)
            removed?.Dispose()
            RaiseEvent StatusChanged(Me, $"Client disconnected ({_clients.Count} total)")
        End Sub

        Public Property IsLiveRunning As Boolean = False
        Public Property IsSimulating As Boolean = False
        Public Property BgColor As String = "#000000"
        Public Property FgColor As String = "#FFFFFF"

        Private Sub HandleApiControl(ctx As HttpListenerContext)
            Try
                Dim action = ctx.Request.QueryString("action")
                If String.IsNullOrEmpty(action) Then
                    Dim json = $"{{""live"":{If(IsLiveRunning, "true", "false")},""sim"":{If(IsSimulating, "true", "false")}}}"
                    SendJsonResponse(ctx, json)
                    Return
                End If

                Select Case action.ToLower()
                    Case "start", "stop", "restart", "simulate"
                        RaiseEvent RemoteCommand(Me, action.ToLower())
                        Dim json = $"{{""ok"":true,""action"":""{action.ToLower()}""}}"
                        SendJsonResponse(ctx, json)
                    Case "status"
                        Dim json = $"{{""live"":{If(IsLiveRunning, "true", "false")},""sim"":{If(IsSimulating, "true", "false")}}}"
                        SendJsonResponse(ctx, json)
                    Case Else
                        ctx.Response.StatusCode = 400
                        SendJsonResponse(ctx, "{""error"":""unknown action""}")
                End Select
            Catch
                ctx.Response.StatusCode = 500
                Try : ctx.Response.Close() : Catch : End Try
            End Try
        End Sub

        Private Sub SendJsonResponse(ctx As HttpListenerContext, json As String)
            Dim buffer = Encoding.UTF8.GetBytes(json)
            ctx.Response.ContentType = "application/json; charset=utf-8"
            ctx.Response.ContentLength64 = buffer.Length
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*")
            Try
                ctx.Response.OutputStream.Write(buffer, 0, buffer.Length)
                ctx.Response.OutputStream.Close()
            Catch
            End Try
        End Sub

        Private Sub ServeCertificate(ctx As HttpListenerContext)
            If _httpsCert Is Nothing Then
                ctx.Response.StatusCode = 404
                ctx.Response.Close()
                Return
            End If
            Dim certBytes = _httpsCert.Export(X509ContentType.Cert)
            ctx.Response.ContentType = "application/x-x509-ca-cert"
            ctx.Response.Headers.Add("Content-Disposition", "attachment; filename=""TranscriptionTools.crt""")
            ctx.Response.ContentLength64 = certBytes.Length
            Try
                ctx.Response.OutputStream.Write(certBytes, 0, certBytes.Length)
                ctx.Response.OutputStream.Close()
            Catch
            End Try
        End Sub

        Private Sub ServeNoSleepAudio(ctx As HttpListenerContext)
            Dim wav = BuildSilentWav()
            ctx.Response.ContentType = "audio/wav"
            ctx.Response.ContentLength64 = wav.Length
            Try
                ctx.Response.OutputStream.Write(wav, 0, wav.Length)
                ctx.Response.OutputStream.Close()
            Catch
            End Try
        End Sub

        ''' <summary>
        ''' Build a valid WAV file with 2 seconds of silence (8kHz mono 8-bit PCM).
        ''' Unlike the old empty MP4, this has real audio samples so browsers will play it.
        ''' </summary>
        Private Shared Function BuildSilentWav() As Byte()
            Const sampleRate As Integer = 8000
            Const durationSec As Integer = 2
            Const bitsPerSample As Integer = 8
            Const channels As Integer = 1
            Dim dataSize = sampleRate * durationSec * channels * (bitsPerSample \ 8)
            Dim fileSize = 36 + dataSize  ' 44 byte header - 8 for RIFF header itself

            Using ms As New MemoryStream()
                Using w As New BinaryWriter(ms)
                    ' RIFF header
                    w.Write(Encoding.ASCII.GetBytes("RIFF"))
                    w.Write(fileSize)                         ' file size - 8
                    w.Write(Encoding.ASCII.GetBytes("WAVE"))
                    ' fmt chunk
                    w.Write(Encoding.ASCII.GetBytes("fmt "))
                    w.Write(16)                               ' chunk size
                    w.Write(CShort(1))                        ' PCM format
                    w.Write(CShort(channels))
                    w.Write(sampleRate)
                    w.Write(sampleRate * channels * (bitsPerSample \ 8)) ' byte rate
                    w.Write(CShort(channels * (bitsPerSample \ 8)))     ' block align
                    w.Write(CShort(bitsPerSample))
                    ' data chunk
                    w.Write(Encoding.ASCII.GetBytes("data"))
                    w.Write(dataSize)
                    ' Silence: 128 is zero-point for unsigned 8-bit PCM
                    w.Write(Enumerable.Repeat(CByte(128), dataSize).ToArray())
                End Using
                Return ms.ToArray()
            End Using
        End Function

        Private Sub ServeHtml(ctx As HttpListenerContext)
            Dim html = GetHtmlPage()
            Dim buffer = Encoding.UTF8.GetBytes(html)
            ctx.Response.ContentType = "text/html; charset=utf-8"
            ctx.Response.Headers.Add("Cache-Control", "no-store, no-cache, must-revalidate")
            ctx.Response.Headers.Add("Pragma", "no-cache")
            ctx.Response.ContentLength64 = buffer.Length
            Try
                ctx.Response.OutputStream.Write(buffer, 0, buffer.Length)
                ctx.Response.OutputStream.Close()
            Catch
            End Try
        End Sub

        Private Function GetHtmlPage() As String
            Dim bg = If(String.IsNullOrEmpty(BgColor), "#000000", BgColor)
            Dim fg = If(String.IsNullOrEmpty(FgColor), "#FFFFFF", FgColor)
            Return ("<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, user-scalable=yes"">
<title>Live Subtitles</title>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{background:{{BG_COLOR}};color:{{FG_COLOR}};font-family:'Segoe UI',Arial,sans-serif;
     height:100vh;display:flex;flex-direction:column;overflow:hidden}
#status{padding:6px 12px;font-size:13px;color:#888;background:#111;border-bottom:1px solid #222;flex-shrink:0}
#status.connected{color:#4a4}
#status.disconnected{color:#a44}
#container{flex:1;overflow-y:auto;padding:16px;padding-bottom:32px}
#lines{display:flex;flex-direction:column;min-height:100%;padding-bottom:24px}
#spacer{flex:1}
.line{font-size:28px;line-height:1.4;padding:4px 0;color:{{FG_COLOR}};word-wrap:break-word;border-bottom:1px solid #333;margin-bottom:4px}
.line.in-progress{color:#ff6b6b;opacity:0.85}
#toolbar{position:fixed;top:0;right:0;padding:8px;z-index:10;display:flex;gap:4px}
#toolbar button{background:#222;color:#aaa;border:1px solid #444;border-radius:4px;
                padding:6px 10px;font-size:18px;cursor:pointer;min-width:40px}
#toolbar button.active{color:#4f4;border-color:#4f4}
#toolbar button.recording{color:#f44;border-color:#f44}
#panel{display:none;position:fixed;top:44px;right:8px;background:#222;
       border:1px solid #444;border-radius:6px;padding:12px;z-index:10;min-width:200px}
#panel button{background:#333;color:#fff;border:1px solid #555;border-radius:4px;
              padding:8px 14px;margin:2px;font-size:18px;cursor:pointer}
#panel label{color:#ccc;font-size:14px;display:block;margin:8px 0 4px}
#panel select{background:#333;color:#fff;border:1px solid #555;border-radius:4px;
              padding:6px;font-size:14px;width:100%}
#adminPanel{display:none;position:fixed;top:44px;left:8px;background:#222;
            border:1px solid #444;border-radius:6px;padding:12px;z-index:10;min-width:180px}
#adminPanel button{background:#333;color:#fff;border:1px solid #555;border-radius:4px;
                   padding:10px 16px;margin:4px 0;font-size:16px;cursor:pointer;width:100%;display:block}
#adminPanel button:hover{background:#444}
#adminPanel button.start{border-color:#4a4;color:#4f4}
#adminPanel button.stop{border-color:#a44;color:#f44}
#adminPanel button.restart{border-color:#aa4;color:#ff4}
#adminStatus{font-size:13px;color:#888;text-align:center;margin-bottom:8px}
</style>
</head>
<body>
<div id=""status"" class=""disconnected"">Connecting...</div>
<div id=""toolbar"">
  <button id=""btnAdmin"" onclick=""toggleAdmin()"" title=""Remote Control"">&#9881;</button>
  <button onclick=""togglePanel()"" title=""Settings"">Aa</button>
  <button id=""btnSpeak"" onclick=""toggleSpeak()"" title=""Read aloud"">&#128264;</button>
  <button id=""btnWake"" onclick=""toggleWakeLock()"" title=""Keep screen on"">&#128261;</button>
</div>
<div id=""adminPanel"">
  <div id=""adminStatus"">Checking...</div>
  <button class=""start"" onclick=""sendCommand('start')"">&#9654; Start</button>
  <button class=""stop"" onclick=""sendCommand('stop')"">&#9632; Stop</button>
  <button class=""restart"" onclick=""sendCommand('restart')"">&#8635; Restart</button>
  <button onclick=""sendCommand('simulate')"">&#9881; Simulate</button>
</div>
<div id=""panel"">
  <button onclick=""changeFontSize(4)"">A+</button>
  <button onclick=""changeFontSize(-4)"">A-</button>
  <label>Font</label>
  <select id=""fontSelect"" onchange=""changeFont(this.value)"">
    <option value=""'Segoe UI',Arial,sans-serif"">Segoe UI</option>
    <option value=""Arial,sans-serif"">Arial</option>
    <option value=""'Courier New',monospace"">Courier New</option>
    <option value=""Georgia,serif"">Georgia</option>
    <option value=""'Times New Roman',serif"">Times New Roman</option>
    <option value=""Verdana,sans-serif"">Verdana</option>
    <option value=""'Trebuchet MS',sans-serif"">Trebuchet MS</option>
    <option value=""Tahoma,sans-serif"">Tahoma</option>
  </select>
  <label>Style</label>
  <button id=""btnBold"" onclick=""toggleBold()"" style=""min-width:60px"">Bold</button>
  <label>Text Color</label>
  <input type=""color"" id=""colorPicker"" value=""{{FG_COLOR}}"" onchange=""changeColor(this.value)"" style=""width:100%;height:32px;border:1px solid #555;border-radius:4px;background:#333;cursor:pointer"">
  <label>Voice</label>
  <select id=""voiceSelect"" onchange=""selectedVoice=this.value;localStorage.setItem('voice',this.value)""></select>
  <label>Speed</label>
  <select id=""rateSelect"" onchange=""speechRate=parseFloat(this.value);localStorage.setItem('rate',this.value)"">
    <option value=""0.7"">Slow</option>
    <option value=""1"" selected>Normal</option>
    <option value=""1.3"">Fast</option>
    <option value=""1.6"">Very Fast</option>
  </select>
</div>
<div id=""container""><div id=""lines""><div id=""spacer""></div></div></div>
<script>
var fontSize=28;
var currentEl=null;
var speakEnabled=false;
var selectedVoice='';
var speechRate=1;
const synth=window.speechSynthesis;
const lines=document.getElementById('lines');
const container=document.getElementById('container');
const status=document.getElementById('status');
const panel=document.getElementById('panel');
const btnSpeak=document.getElementById('btnSpeak');
const voiceSelect=document.getElementById('voiceSelect');
const rateSelect=document.getElementById('rateSelect');

/* Restore saved preferences */
if(localStorage.getItem('voice'))selectedVoice=localStorage.getItem('voice');
if(localStorage.getItem('rate')){speechRate=parseFloat(localStorage.getItem('rate'));rateSelect.value=localStorage.getItem('rate')}
if(localStorage.getItem('speak')==='true'){speakEnabled=true;btnSpeak.classList.add('active');btnSpeak.innerHTML='&#128266;'}

function populateVoices(){
  const voices=synth.getVoices();
  voiceSelect.innerHTML='';
  const defOpt=document.createElement('option');defOpt.value='';defOpt.textContent='Default';voiceSelect.appendChild(defOpt);
  voices.forEach(v=>{
    const opt=document.createElement('option');opt.value=v.name;
    opt.textContent=v.name+(v.lang?' ('+v.lang+')':'');
    if(v.name===selectedVoice)opt.selected=true;
    voiceSelect.appendChild(opt);
  });
}
populateVoices();
if(synth.onvoiceschanged!==undefined)synth.onvoiceschanged=populateVoices;

function togglePanel(){panel.style.display=panel.style.display==='block'?'none':'block'}

function toggleSpeak(){
  speakEnabled=!speakEnabled;
  localStorage.setItem('speak',speakEnabled);
  if(speakEnabled){btnSpeak.classList.add('active');btnSpeak.innerHTML='&#128266;'}
  else{btnSpeak.classList.remove('active');btnSpeak.innerHTML='&#128264;';synth.cancel()}
}

function speak(text){
  if(!speakEnabled||!synth||!text)return;
  const utter=new SpeechSynthesisUtterance(text);
  utter.rate=speechRate;
  if(selectedVoice){const v=synth.getVoices().find(x=>x.name===selectedVoice);if(v)utter.voice=v}
  synth.speak(utter);
}

var fontFamily=localStorage.getItem('fontFamily')||""'Segoe UI',Arial,sans-serif"";
var isBold=localStorage.getItem('bold')==='true';
var textColor=localStorage.getItem('textColor')||'{{FG_COLOR}}';
if(localStorage.getItem('fontSize'))fontSize=parseInt(localStorage.getItem('fontSize'));
(function(){var fs=document.getElementById('fontSelect');for(var i=0;i<fs.options.length;i++){if(fs.options[i].value===fontFamily){fs.selectedIndex=i;break}}
  var bp=document.getElementById('btnBold');if(isBold){bp.classList.add('active')}
  document.getElementById('colorPicker').value=textColor;
})();
function applyStylesToAll(){
  document.querySelectorAll('.line').forEach(function(el){el.style.fontSize=fontSize+'px';el.style.fontFamily=fontFamily;el.style.fontWeight=isBold?'bold':'normal';if(!el.classList.contains('in-progress')&&!el.dataset.highlighted)el.style.color=textColor});
  if(currentEl){currentEl.style.fontSize=fontSize+'px';currentEl.style.fontFamily=fontFamily;currentEl.style.fontWeight=isBold?'bold':'normal'}
  scrollBottom()}
function changeFontSize(d){fontSize=Math.max(12,Math.min(80,fontSize+d));localStorage.setItem('fontSize',fontSize);applyStylesToAll()}
function changeFont(f){fontFamily=f;localStorage.setItem('fontFamily',f);applyStylesToAll()}
function toggleBold(){isBold=!isBold;localStorage.setItem('bold',isBold);document.getElementById('btnBold').classList.toggle('active');applyStylesToAll()}
function changeColor(c){textColor=c;localStorage.setItem('textColor',c);applyStylesToAll()}
var userScrolled=false;
container.addEventListener('scroll',function(){
  var atBottom=container.scrollHeight-container.scrollTop-container.clientHeight<60;
  userScrolled=!atBottom;
});
function scrollBottom(){if(!userScrolled){container.scrollTop=container.scrollHeight}}
function addCommitted(text){
  var el;
  if(currentEl){el=currentEl;currentEl=null}
  else{el=document.createElement('div');lines.appendChild(el)}
  el.textContent=text;
  el.className='line';
  el.style.fontSize=fontSize+'px';el.style.fontFamily=fontFamily;el.style.fontWeight=isBold?'bold':'normal';
  el.style.color='#ffdd57';
  el.dataset.highlighted='1';
  setTimeout(function(){el.style.color=textColor;delete el.dataset.highlighted},5000);
  scrollBottom();
  while(lines.children.length>201){lines.removeChild(lines.children[1])}
  speak(text);
}
function updateCurrent(text){
  if(!currentEl){currentEl=document.createElement('div');currentEl.className='line in-progress';currentEl.style.fontSize=fontSize+'px';currentEl.style.fontFamily=fontFamily;currentEl.style.fontWeight=isBold?'bold':'normal';lines.appendChild(currentEl)}
  currentEl.textContent=text;
  scrollBottom()
}
function connect(){
  const proto=location.protocol==='https:'?'wss:':'ws:';
  const ws=new WebSocket(proto+'//'+location.host+'/ws');
  ws.onopen=()=>{status.textContent='Connected';status.className='connected'};
  ws.onclose=()=>{status.textContent='Disconnected - reconnecting...';status.className='disconnected';setTimeout(connect,2000)};
  ws.onerror=()=>{ws.close()};
  ws.onmessage=(e)=>{
    try{const msg=JSON.parse(e.data);
      if(msg.type==='commit')addCommitted(msg.text);
      else if(msg.type==='update')updateCurrent(msg.text);
    }catch(ex){}
  }
}
connect();

/* Keep screen on — Wake Lock toggle button */
var wakeLockObj=null;
var wakeActive=false;
var btnWake=document.getElementById('btnWake');

function setWakeActive(on){
  wakeActive=on;
  if(on){btnWake.classList.add('active')}else{btnWake.classList.remove('active')}
}

async function acquireWakeLock(){
  /* HTTPS: use Wake Lock API (only try in secure context to preserve user gesture) */
  if(window.isSecureContext&&'wakeLock' in navigator){
    try{
      wakeLockObj=await navigator.wakeLock.request('screen');
      setWakeActive(true);
      wakeLockObj.addEventListener('release',function(){wakeLockObj=null;if(wakeActive)acquireWakeLock()});
      return;
    }catch(e){}
  }
  /* HTTP: no reliable way to prevent sleep — guide user to HTTPS */
  showCertSetup();
}

function releaseWakeLock(){
  wakeActive=false;
  if(wakeLockObj){try{wakeLockObj.release()}catch(e){}wakeLockObj=null}
  if(window._noSleepAudio){try{window._noSleepAudio.pause()}catch(e){}}
  setWakeActive(false);
}

function toggleWakeLock(){if(wakeActive)releaseWakeLock();else acquireWakeLock()}

function showCertSetup(){
  var hp=parseInt(location.port||80)+1;
  var url='https://'+location.hostname+':'+hp;
  var d=document.createElement('div');
  d.style.cssText='position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.95);z-index:1000;display:flex;flex-direction:column;align-items:center;justify-content:center;padding:24px;color:#fff;font-size:16px;line-height:1.6';
  var c=document.createElement('div');c.style.cssText='max-width:400px;text-align:left';
  var h=document.createElement('h2');h.style.cssText='color:#ffdd57;margin-bottom:16px;text-align:center';h.textContent='Keep Screen On';c.appendChild(h);
  var p0=document.createElement('p');p0.style.cssText='margin-bottom:16px;text-align:center';p0.textContent='A secure connection is needed (one-time setup):';c.appendChild(p0);
  var steps=['Tap the button below','You will see a warning page \u2014 this is normal','Tap \u0022Advanced\u0022','Tap \u0022Proceed to '+location.hostname+'\u0022','Tap the screen wake button again'];
  for(var i=0;i<steps.length;i++){var s=document.createElement('p');s.style.cssText='margin-bottom:8px;padding-left:8px';s.textContent=(i+1)+'. '+steps[i];c.appendChild(s)}
  var br=document.createElement('div');br.style.cssText='text-align:center;margin-top:20px';
  var a1=document.createElement('a');a1.href=url;a1.textContent='Open Secure Page';a1.style.cssText='display:inline-block;background:#47f;color:#fff;padding:14px 28px;border-radius:8px;text-decoration:none;font-size:18px;margin-bottom:16px';br.appendChild(a1);
  br.appendChild(document.createElement('br'));
  var b2=document.createElement('button');b2.textContent='Cancel';b2.style.cssText='background:#333;color:#aaa;border:1px solid #555;padding:8px 20px;border-radius:6px;font-size:14px;cursor:pointer;margin-top:8px';b2.onclick=function(){d.remove()};br.appendChild(b2);
  c.appendChild(br);d.appendChild(c);document.body.appendChild(d);
}

document.addEventListener('visibilitychange',function(){
  if(document.visibilityState==='visible'&&wakeActive){
    if(!wakeLockObj)acquireWakeLock();
    else if(window._noSleepAudio)window._noSleepAudio.play().catch(function(){});
  }
});

/* Admin remote control */
const adminPanel=document.getElementById('adminPanel');
const adminStatus=document.getElementById('adminStatus');
var adminPollTimer=null;
function toggleAdmin(){
  if(adminPanel.style.display==='block'){adminPanel.style.display='none';if(adminPollTimer){clearInterval(adminPollTimer);adminPollTimer=null}}
  else{adminPanel.style.display='block';pollStatus();adminPollTimer=setInterval(pollStatus,3000)}
}
function sendCommand(action){
  adminStatus.textContent='Sending...';
  fetch('/api/control?action='+action).then(r=>r.json()).then(d=>{
    adminStatus.textContent=action+' command sent';
    setTimeout(pollStatus,1500);
  }).catch(()=>adminStatus.textContent='Failed to send command');
}
function pollStatus(){
  fetch('/api/control?action=status').then(r=>r.json()).then(d=>{
    if(d.live){adminStatus.textContent='Live: RUNNING';adminStatus.style.color='#4f4'}
    else if(d.sim){adminStatus.textContent='Simulation: RUNNING';adminStatus.style.color='#fa0'}
    else{adminStatus.textContent='Status: STOPPED';adminStatus.style.color='#f44'}
  }).catch(()=>{adminStatus.textContent='Unable to reach server';adminStatus.style.color='#888'});
}
</script>
</body>
</html>").Replace("{{BG_COLOR}}", bg).Replace("{{FG_COLOR}}", fg)
        End Function

        Private Shared Function EscapeJson(s As String) As String
            Dim sb As New StringBuilder("""")
            For Each c In s
                Select Case c
                    Case """"c : sb.Append("\""")
                    Case "\"c : sb.Append("\\")
                    Case ChrW(8) : sb.Append("\b")
                    Case ChrW(9) : sb.Append("\t")
                    Case ChrW(10) : sb.Append("\n")
                    Case ChrW(12) : sb.Append("\f")
                    Case ChrW(13) : sb.Append("\r")
                    Case Else
                        If AscW(c) < 32 Then
                            sb.Append($"\u{AscW(c):X4}")
                        Else
                            sb.Append(c)
                        End If
                End Select
            Next
            sb.Append(""""c)
            Return sb.ToString()
        End Function
    End Class
End Namespace
