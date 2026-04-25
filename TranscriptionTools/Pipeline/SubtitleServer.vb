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
        Public Event ActiveLanguagesChanged As EventHandler
        Public Event LogMessage As EventHandler(Of String)

        Private _listener As HttpListener
        Private _httpsListener As TcpListener
        Private _httpsCert As X509Certificate2
        Private _cts As CancellationTokenSource
        Private ReadOnly _clients As New ConcurrentDictionary(Of String, ClientInfo)()
        Private _port As Integer = 5080
        Private _httpsPort As Integer = 5081
        Private _isRunning As Boolean = False
        Private Const CertPassword As String = "transcription-tools-cert"
        Private _currentLine As String = ""
        Private ReadOnly _committedLines As New ConcurrentQueue(Of CommittedEntry)()
        Private _lastCommittedEntry As CommittedEntry
        Private Const MaxCommittedLines As Integer = 200
        Private _commitCounter As Integer = 0

        Private Class ClientInfo
            Public Property WebSocket As WebSocket
            Public Property Language As String = ""  ' Empty = original (no translation)
            Public Property UserAgent As String = ""
            Public Property RemoteEndpoint As String = ""
            Public ReadOnly SendLock As New Object()
        End Class

        Private Class CommittedEntry
            Public Property Id As Integer
            Public Property OriginalText As String = ""
            Public Property SourceLang As String = ""
            Public Property Translations As Dictionary(Of String, String)
            Public Property Timestamp As DateTime

            Public Sub New(id As Integer, text As String, Optional lang As String = "", Optional translations As Dictionary(Of String, String) = Nothing)
                Me.Id = id
                OriginalText = text
                SourceLang = If(lang, "")
                Me.Translations = If(translations, New Dictionary(Of String, String)())
                Timestamp = DateTime.Now
            End Sub
        End Class

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
                    Dim ua = If(headers.ContainsKey("User-Agent"), headers("User-Agent"), "")
                    Dim ep = CType(client.Client.RemoteEndPoint, IPEndPoint).Address.ToString()
                    Await HandleWebSocketStream(ws, ua, ep, ct).ConfigureAwait(False)
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
                ElseIf path.StartsWith("/api/control") Then
                    Dim json As String
                    Dim qIdx = path.IndexOf("?"c)
                    Dim action As String = Nothing
                    If qIdx >= 0 Then
                        Dim qs = path.Substring(qIdx + 1)
                        For Each pair In qs.Split("&"c)
                            Dim kv = pair.Split("="c)
                            If kv.Length = 2 AndAlso kv(0) = "action" Then action = kv(1).ToLower()
                        Next
                    End If

                    If String.IsNullOrEmpty(action) OrElse action = "status" Then
                        json = $"{{""live"":{If(IsLiveRunning, "true", "false")},""sim"":{If(IsSimulating, "true", "false")}}}"
                    ElseIf action = "start" OrElse action = "stop" OrElse action = "restart" OrElse action = "simulate" OrElse action = "clear" Then
                        RaiseEvent RemoteCommand(Me, action)
                        json = $"{{""ok"":true,""action"":""{action}""}}"
                    Else
                        json = "{""error"":""unknown action""}"
                    End If

                    Dim jsonBytes = Encoding.UTF8.GetBytes(json)
                    Dim header = "HTTP/1.1 200 OK" & vbCrLf &
                                 "Content-Type: application/json; charset=utf-8" & vbCrLf &
                                 $"Content-Length: {jsonBytes.Length}" & vbCrLf &
                                 "Access-Control-Allow-Origin: *" & vbCrLf &
                                 "Connection: close" & vbCrLf & vbCrLf
                    Dim hdrBytes = Encoding.ASCII.GetBytes(header)
                    Await sslStream.WriteAsync(hdrBytes, 0, hdrBytes.Length, ct).ConfigureAwait(False)
                    Await sslStream.WriteAsync(jsonBytes, 0, jsonBytes.Length, ct).ConfigureAwait(False)
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
        Private Async Function HandleWebSocketStream(ws As WebSocket, userAgent As String, remoteEndpoint As String, ct As CancellationToken) As Task
            Dim clientId = Guid.NewGuid().ToString()
            Dim info As New ClientInfo() With {.WebSocket = ws, .UserAgent = userAgent, .RemoteEndpoint = remoteEndpoint}
            _clients.TryAdd(clientId, info)
            Dim shortUa = ParseUserAgent(userAgent)
            RaiseEvent StatusChanged(Me, $"Client connected: {remoteEndpoint} — {shortUa} ({_clients.Count} total)")

            ' Send history — replay uses final text (never pending)
            Try
                For Each entry In _committedLines
                    Dim text = GetTextForClient(info, entry.OriginalText, entry.Translations)
                    Dim clientLang = GetLangForClient(info, entry.SourceLang, entry.Translations)
                    Dim ts = entry.Timestamp.ToString("HH:mm:ss")
                    Dim json = $"{{""type"":""commit"",""text"":{EscapeJson(text)},""lang"":{EscapeJson(clientLang)},""time"":{EscapeJson(ts)},""id"":{entry.Id}}}"
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

            ' Read loop — parse client messages (setLanguage, etc.)
            Dim recvBuf = New Byte(1023) {}
            Try
                While ws.State = WebSocketState.Open AndAlso Not ct.IsCancellationRequested
                    Dim result = Await ws.ReceiveAsync(New ArraySegment(Of Byte)(recvBuf), ct).ConfigureAwait(False)
                    If result.MessageType = WebSocketMessageType.Text AndAlso result.Count > 0 Then
                        Dim msg = Encoding.UTF8.GetString(recvBuf, 0, result.Count)
                        ProcessClientMessage(clientId, msg)
                    ElseIf result.MessageType = WebSocketMessageType.Close Then
                        Exit While
                    End If
                End While
            Catch
            End Try

            Dim removed As ClientInfo = Nothing
            _clients.TryRemove(clientId, removed)
            removed?.WebSocket?.Dispose()
            RaiseEvent StatusChanged(Me, $"Client disconnected: {remoteEndpoint} ({_clients.Count} total)")
            RaiseEvent ActiveLanguagesChanged(Me, EventArgs.Empty)
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
                    Dim ws = kvp.Value.WebSocket
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

        Public Function GetActiveTranslationLanguages() As List(Of String)
            Dim langs As New HashSet(Of String)()
            For Each kvp In _clients
                Dim lang = kvp.Value.Language
                If Not String.IsNullOrEmpty(lang) Then
                    langs.Add(lang)
                End If
            Next
            Return langs.ToList()
        End Function

        ''' <summary>
        ''' Send data to a single client without blocking the UI thread.
        ''' Serializes sends per-client to avoid concurrent SendAsync (which WebSocket forbids).
        ''' Returns False only if the socket is closed.
        ''' </summary>
        Private Function TrySendToClient(client As ClientInfo, data As Byte()) As Boolean
            Dim ws = client.WebSocket
            If ws.State <> WebSocketState.Open Then Return False
            Try
                SyncLock client.SendLock
                    Task.Run(Async Function()
                                 Await ws.SendAsync(New ArraySegment(Of Byte)(data), WebSocketMessageType.Text, True, CancellationToken.None).ConfigureAwait(False)
                             End Function).Wait(3000)
                End SyncLock
                Return True
            Catch
                ' Timeout or send error — don't kill the client, just skip this message
                Return ws.State = WebSocketState.Open
            End Try
        End Function

        Public Sub BroadcastUpdate(text As String)
            If Not _isRunning Then Return
            _currentLine = text
            Dim json = $"{{""type"":""update"",""text"":{EscapeJson(text)}}}"
            ' Only send live updates to clients without a translation language —
            ' translation clients will see committed+translated text only
            Dim buffer = Encoding.UTF8.GetBytes(json)
            Dim deadKeys As New List(Of String)

            For Each kvp In _clients
                Try
                    If Not String.IsNullOrEmpty(kvp.Value.Language) Then Continue For
                    If Not TrySendToClient(kvp.Value, buffer) Then deadKeys.Add(kvp.Key)
                Catch
                    deadKeys.Add(kvp.Key)
                End Try
            Next

            CleanupDeadClients(deadKeys)
        End Sub

        Public Function BroadcastCommit(text As String, Optional skipTranslationClients As Boolean = False, Optional lang As String = "") As Integer
            Dim commitId = Interlocked.Increment(_commitCounter)
            If Not _isRunning Then Return commitId
            _currentLine = ""

            ' Store in history (original text, no translations yet)
            Dim entry As New CommittedEntry(commitId, text, lang, Nothing)
            _lastCommittedEntry = entry
            _committedLines.Enqueue(entry)
            While _committedLines.Count > MaxCommittedLines
                Dim discard As CommittedEntry = Nothing
                _committedLines.TryDequeue(discard)
            End While

            Dim ts = entry.Timestamp.ToString("HH:mm:ss")
            Dim json = $"{{""type"":""commit"",""text"":{EscapeJson(text)},""lang"":{EscapeJson(lang)},""time"":{EscapeJson(ts)},""id"":{commitId}}}"
            Dim buffer = Encoding.UTF8.GetBytes(json)
            Dim deadKeys As New List(Of String)

            For Each kvp In _clients
                Try
                    If skipTranslationClients AndAlso Not String.IsNullOrEmpty(kvp.Value.Language) Then
                        RaiseEvent LogMessage(Me,$"[SUBTITLE] COMMIT SKIP {kvp.Value.RemoteEndpoint} lang={kvp.Value.Language}")
                        Continue For
                    End If
                    If Not TrySendToClient(kvp.Value, buffer) Then deadKeys.Add(kvp.Key)
                Catch
                    deadKeys.Add(kvp.Key)
                End Try
            Next

            CleanupDeadClients(deadKeys)
            Return commitId
        End Function


        Public Sub BroadcastCommitTranslated(originalText As String, sourceLang As String, translations As Dictionary(Of String, String), langTags As Dictionary(Of String, String))
            If Not _isRunning Then Return
            _currentLine = ""
            Dim entry As New CommittedEntry(Interlocked.Increment(_commitCounter), originalText, sourceLang, translations)
            _lastCommittedEntry = entry
            _committedLines.Enqueue(entry)
            While _committedLines.Count > MaxCommittedLines
                Dim discard As CommittedEntry = Nothing
                _committedLines.TryDequeue(discard)
            End While

            Dim ts = entry.Timestamp.ToString("HH:mm:ss")
            Dim deadKeys As New List(Of String)

            For Each kvp In _clients
                Try
                    ' Determine what text to send this client
                    Dim clientLang = kvp.Value.Language
                    Dim text As String
                    Dim tag As String

                    If String.IsNullOrEmpty(clientLang) Then
                        ' No translation requested — send original
                        text = originalText
                        tag = sourceLang
                    Else
                        ' Translation client — look up their language in translations
                        Dim translated As String = Nothing
                        If translations IsNot Nothing AndAlso translations.TryGetValue(clientLang, translated) Then
                            text = translated
                            tag = clientLang
                        Else
                            ' Language not available — skip (don't send original)
                            RaiseEvent LogMessage(Me, $"[SUBTITLE] SKIP {kvp.Value.RemoteEndpoint} lang={clientLang} — not in translations")
                            Continue For
                        End If
                    End If

                    Dim langTag = ""
                    If langTags IsNot Nothing Then langTags.TryGetValue(tag, langTag)
                    Dim json = $"{{""type"":""commit"",""text"":{EscapeJson(text)},""lang"":{EscapeJson(langTag)},""time"":{EscapeJson(ts)}}}"
                    Dim buffer = Encoding.UTF8.GetBytes(json)
                    If TrySendToClient(kvp.Value, buffer) Then
                        RaiseEvent LogMessage(Me, $"[SUBTITLE] SENT to {kvp.Value.RemoteEndpoint} lang={If(clientLang, "original")}")
                    Else
                        deadKeys.Add(kvp.Key)
                    End If
                Catch
                    deadKeys.Add(kvp.Key)
                End Try
            Next

            CleanupDeadClients(deadKeys)
        End Sub

        Public Sub BroadcastTranslationsOnly(translations As Dictionary(Of String, String), langTags As Dictionary(Of String, String))
            ' Send translated text only to clients whose language matches
            If Not _isRunning OrElse translations Is Nothing Then Return

            ' Update the last history entry with translations so reconnecting clients get them
            If _lastCommittedEntry IsNot Nothing Then
                For Each kvp In translations
                    _lastCommittedEntry.Translations(kvp.Key) = kvp.Value
                Next
            End If

            Dim ts = DateTime.Now.ToString("HH:mm:ss")
            Dim deadKeys As New List(Of String)

            For Each kvp In _clients
                Try
                    Dim lang = kvp.Value.Language
                    If String.IsNullOrEmpty(lang) Then Continue For
                    Dim translated As String = Nothing
                    If Not translations.TryGetValue(lang, translated) Then
                        RaiseEvent LogMessage(Me,$"[SUBTITLE] SKIP client {kvp.Value.RemoteEndpoint} lang={lang} — not in translations [{String.Join(",", translations.Keys)}]")
                        Continue For
                    End If

                    Dim tagValue = ""
                    If langTags IsNot Nothing Then langTags.TryGetValue(lang, tagValue)

                    Dim json = $"{{""type"":""commit"",""text"":{EscapeJson(translated)},""lang"":{EscapeJson(tagValue)},""time"":{EscapeJson(ts)}}}"
                    Dim buffer = Encoding.UTF8.GetBytes(json)
                    If TrySendToClient(kvp.Value, buffer) Then
                        RaiseEvent LogMessage(Me,$"[SUBTITLE] SENT translation to {kvp.Value.RemoteEndpoint} lang={lang}")
                    Else
                        deadKeys.Add(kvp.Key)
                    End If
                Catch ex As Exception
                    RaiseEvent LogMessage(Me,$"[SUBTITLE] SEND ERROR to {kvp.Value.RemoteEndpoint}: {ex.Message}")
                    deadKeys.Add(kvp.Key)
                End Try
            Next

            CleanupDeadClients(deadKeys)
        End Sub

        Public Sub BroadcastClear()
            If Not _isRunning Then Return
            _currentLine = ""
            While _committedLines.Count > 0
                Dim discard As CommittedEntry = Nothing
                _committedLines.TryDequeue(discard)
            End While
            BroadcastMessage("{""type"":""clear""}")
        End Sub

        Private Shared Function GetTextForClient(client As ClientInfo, originalText As String, translations As Dictionary(Of String, String)) As String
            If String.IsNullOrEmpty(client.Language) Then Return originalText
            If translations IsNot Nothing Then
                Dim translated As String = Nothing
                If translations.TryGetValue(client.Language, translated) Then Return translated
            End If
            Return originalText
        End Function

        Private Shared Function GetLangForClient(client As ClientInfo, sourceLang As String, translations As Dictionary(Of String, String)) As String
            If String.IsNullOrEmpty(client.Language) Then Return sourceLang
            If translations IsNot Nothing AndAlso translations.ContainsKey(client.Language) Then Return client.Language
            Return sourceLang
        End Function

        Private Sub BroadcastMessage(json As String)
            Dim buffer = Encoding.UTF8.GetBytes(json)
            Dim deadKeys As New List(Of String)

            For Each kvp In _clients
                Try
                    If Not TrySendToClient(kvp.Value, buffer) Then deadKeys.Add(kvp.Key)
                Catch
                    deadKeys.Add(kvp.Key)
                End Try
            Next

            CleanupDeadClients(deadKeys)
        End Sub

        Private Sub CleanupDeadClients(deadKeys As List(Of String))
            For Each key In deadKeys
                Dim removed As ClientInfo = Nothing
                _clients.TryRemove(key, removed)
                removed?.WebSocket?.Dispose()
            Next

            If deadKeys.Count > 0 Then
                RaiseEvent StatusChanged(Me, $"Clients: {_clients.Count}")
                RaiseEvent ActiveLanguagesChanged(Me, EventArgs.Empty)
            End If
        End Sub

        Private Sub ProcessClientMessage(clientId As String, jsonText As String)
            Try
                Using doc = System.Text.Json.JsonDocument.Parse(jsonText)
                    Dim root = doc.RootElement
                    Dim typeProp As System.Text.Json.JsonElement = Nothing
                    If Not root.TryGetProperty("type", typeProp) Then Return
                    Dim typeStr = typeProp.GetString()
                    If typeStr = "setLanguage" Then
                        Dim langProp As System.Text.Json.JsonElement = Nothing
                        If Not root.TryGetProperty("language", langProp) Then Return
                        Dim lang = langProp.GetString()
                        Dim info As ClientInfo = Nothing
                        If _clients.TryGetValue(clientId, info) Then
                            Dim oldLang = info.Language
                            info.Language = If(lang, "")
                            If oldLang <> info.Language Then
                                RaiseEvent LogMessage(Me,$"[SUBTITLE] LANG CHANGE {info.RemoteEndpoint}: '{oldLang}' -> '{info.Language}'")
                                RaiseEvent ActiveLanguagesChanged(Me, EventArgs.Empty)
                            End If
                        End If
                    End If
                End Using
            Catch
            End Try
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
                wsCtx = Await ctx.AcceptWebSocketAsync(Nothing, TimeSpan.FromSeconds(30)).ConfigureAwait(False)
            Catch
                ctx.Response.StatusCode = 500
                ctx.Response.Close()
                Return
            End Try

            Dim ws = wsCtx.WebSocket
            Dim clientId = Guid.NewGuid().ToString()
            Dim userAgent = If(ctx.Request.UserAgent, "")
            Dim remoteEndpoint = ctx.Request.RemoteEndPoint?.Address.ToString()
            Dim info As New ClientInfo() With {.WebSocket = ws, .UserAgent = userAgent, .RemoteEndpoint = remoteEndpoint}
            _clients.TryAdd(clientId, info)
            Dim shortUa = ParseUserAgent(userAgent)
            RaiseEvent StatusChanged(Me, $"Client connected: {remoteEndpoint} — {shortUa} ({_clients.Count} total)")

            ' Send history to new client — replay uses final text (never pending)
            Try
                For Each entry In _committedLines
                    Dim text = GetTextForClient(info, entry.OriginalText, entry.Translations)
                    Dim clientLang = GetLangForClient(info, entry.SourceLang, entry.Translations)
                    Dim ts = entry.Timestamp.ToString("HH:mm:ss")
                    Dim json = $"{{""type"":""commit"",""text"":{EscapeJson(text)},""lang"":{EscapeJson(clientLang)},""time"":{EscapeJson(ts)},""id"":{entry.Id}}}"
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

            ' Read loop — parse client messages (setLanguage, etc.)
            Dim recvBuf = New Byte(1023) {}
            Try
                While ws.State = WebSocketState.Open AndAlso Not ct.IsCancellationRequested
                    Dim result = Await ws.ReceiveAsync(New ArraySegment(Of Byte)(recvBuf), ct).ConfigureAwait(False)
                    If result.MessageType = WebSocketMessageType.Text AndAlso result.Count > 0 Then
                        Dim msg = Encoding.UTF8.GetString(recvBuf, 0, result.Count)
                        ProcessClientMessage(clientId, msg)
                    ElseIf result.MessageType = WebSocketMessageType.Close Then
                        Exit While
                    End If
                End While
            Catch
            End Try

            Dim removed As ClientInfo = Nothing
            _clients.TryRemove(clientId, removed)
            removed?.WebSocket?.Dispose()
            RaiseEvent StatusChanged(Me, $"Client disconnected: {remoteEndpoint} ({_clients.Count} total)")
            RaiseEvent ActiveLanguagesChanged(Me, EventArgs.Empty)
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
                    Case "start", "stop", "restart", "simulate", "clear"
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
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, user-scalable=yes, viewport-fit=cover"">
<title>Live Subtitles</title>
<style>
*{margin:0;padding:0;box-sizing:border-box}
html{height:100%;height:100dvh;overflow:hidden}
body{background:{{BG_COLOR}};color:{{FG_COLOR}};font-family:'Segoe UI',Arial,sans-serif;
     height:100%;display:flex;flex-direction:column;overflow:hidden}
#status{padding:6px 12px;font-size:13px;color:#888;background:#111;border-bottom:1px solid #222;flex-shrink:0}
#status.connected{color:#4a4}
#status.disconnected{color:#a44}
#container{flex:1;overflow-y:auto;padding:16px;padding-bottom:32px;
           -webkit-overflow-scrolling:touch;overscroll-behavior:contain}
#lines{display:flex;flex-direction:column;min-height:100%;padding-bottom:env(safe-area-inset-bottom,24px)}
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
  <button id=""btnSettings"" onclick=""togglePanel()"" title=""Settings"">Aa</button>
  <button id=""btnSpeak"" onclick=""toggleSpeak()"" title=""Read aloud"">&#128264;</button>
  <button id=""btnWake"" onclick=""toggleWakeLock()"" title=""Keep screen on"">&#128261;</button>
</div>
<div id=""adminPanel"">
  <div id=""adminStatus"">Checking...</div>
  <button class=""start"" onclick=""sendCommand('start')"">&#9654; Start</button>
  <button class=""stop"" onclick=""sendCommand('stop')"">&#9632; Stop</button>
  <button class=""restart"" onclick=""sendCommand('restart')"">&#8635; Restart</button>
  <button onclick=""sendCommand('simulate')"">&#9881; Simulate</button>
  <button class=""stop"" onclick=""sendCommand('clear')"">&#10060; Clear</button>
</div>
<div id=""panel"">
  <button onclick=""changeFontSize(4)"">A+</button>
  <button onclick=""changeFontSize(-4)"">A-</button>
  <label id=""lblFont"">Font</label>
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
  <label id=""lblStyle"">Style</label>
  <button id=""btnBold"" onclick=""toggleBold()"" style=""min-width:60px"">Bold</button>
  <label id=""lblColor"">Text Color</label>
  <input type=""color"" id=""colorPicker"" value=""{{FG_COLOR}}"" onchange=""changeColor(this.value)"" style=""width:100%;height:32px;border:1px solid #555;border-radius:4px;background:#333;cursor:pointer"">
  <label id=""lblVoice"">Voice</label>
  <select id=""voiceSelect"" onchange=""selectedVoice=this.value;localStorage.setItem('voice',this.value)""></select>
  <label id=""lblSpeed"">Speed</label>
  <select id=""rateSelect"" onchange=""speechRate=parseFloat(this.value);localStorage.setItem('rate',this.value)"">
    <option value=""0.7"">Slow</option>
    <option value=""1"" selected>Normal</option>
    <option value=""1.3"">Fast</option>
    <option value=""1.6"">Very Fast</option>
  </select>
  <label id=""lblTransLang"">Translation</label>
  <select id=""transLangSelect"" onchange=""setTransLang(this.value)"">
    <option value="""">Original</option>
    <option value=""afr_Latn"">Afrikaans</option>
    <option value=""amh_Ethi"">&#4768;&#4635;&#4653;&#4763; (Amharic)</option>
    <option value=""arb_Arab"">&#1575;&#1604;&#1593;&#1585;&#1576;&#1610;&#1577; (Arabic)</option>
    <option value=""hye_Armn"">&#1344;&#1377;&#1397;&#1381;&#1408;&#1381;&#1398; (Armenian)</option>
    <option value=""azj_Latn"">Az&#601;rbaycan (Azerbaijani)</option>
    <option value=""eus_Latn"">Euskara (Basque)</option>
    <option value=""bel_Cyrl"">&#1041;&#1077;&#1083;&#1072;&#1088;&#1091;&#1089;&#1082;&#1072;&#1103; (Belarusian)</option>
    <option value=""ben_Beng"">&#2476;&#2494;&#2434;&#2482;&#2494; (Bengali)</option>
    <option value=""bos_Latn"">Bosanski (Bosnian)</option>
    <option value=""bul_Cyrl"">&#1041;&#1098;&#1083;&#1075;&#1072;&#1088;&#1089;&#1082;&#1080; (Bulgarian)</option>
    <option value=""cat_Latn"">Catal&#224;</option>
    <option value=""ces_Latn"">&#268;e&#353;tina (Czech)</option>
    <option value=""zho_Hans"">&#20013;&#25991; (Chinese)</option>
    <option value=""hrv_Latn"">Hrvatski (Croatian)</option>
    <option value=""dan_Latn"">Dansk (Danish)</option>
    <option value=""nld_Latn"">Nederlands (Dutch)</option>
    <option value=""eng_Latn"">English</option>
    <option value=""est_Latn"">Eesti (Estonian)</option>
    <option value=""fin_Latn"">Suomi (Finnish)</option>
    <option value=""fra_Latn"">Fran&#231;ais (French)</option>
    <option value=""glg_Latn"">Galego (Galician)</option>
    <option value=""kat_Geor"">&#4325;&#4304;&#4320;&#4311;&#4323;&#4314;&#4312; (Georgian)</option>
    <option value=""deu_Latn"">Deutsch (German)</option>
    <option value=""ell_Grek"">&#917;&#955;&#955;&#951;&#957;&#953;&#954;&#940; (Greek)</option>
    <option value=""guj_Gujr"">&#2711;&#2753;&#2716;&#2736;&#2750;&#2724;&#2752; (Gujarati)</option>
    <option value=""hat_Latn"">Krey&#242;l Ayisyen (Haitian Creole)</option>
    <option value=""hau_Latn"">Hausa</option>
    <option value=""heb_Hebr"">&#1506;&#1489;&#1512;&#1497;&#1514; (Hebrew)</option>
    <option value=""hin_Deva"">&#2361;&#2367;&#2344;&#2381;&#2342;&#2368; (Hindi)</option>
    <option value=""hun_Latn"">Magyar (Hungarian)</option>
    <option value=""isl_Latn"">&#205;slenska (Icelandic)</option>
    <option value=""ind_Latn"">Bahasa Indonesia (Indonesian)</option>
    <option value=""ita_Latn"">Italiano (Italian)</option>
    <option value=""jpn_Jpan"">&#26085;&#26412;&#35486; (Japanese)</option>
    <option value=""jav_Latn"">Basa Jawa (Javanese)</option>
    <option value=""kan_Knda"">&#3221;&#3240;&#3277;&#3240;&#3233; (Kannada)</option>
    <option value=""kaz_Cyrl"">&#1178;&#1072;&#1079;&#1072;&#1179; (Kazakh)</option>
    <option value=""khm_Khmr"">&#6017;&#6098;&#6040;&#6082;&#6042; (Khmer)</option>
    <option value=""kor_Hang"">&#54620;&#44397;&#50612; (Korean)</option>
    <option value=""lao_Laoo"">&#3749;&#3762;&#3751; (Lao)</option>
    <option value=""lvs_Latn"">Latvie&#353;u (Latvian)</option>
    <option value=""lit_Latn"">Lietuvi&#371; (Lithuanian)</option>
    <option value=""ltz_Latn"">L&#235;tzebuergesch (Luxembourgish)</option>
    <option value=""mkd_Cyrl"">&#1052;&#1072;&#1082;&#1077;&#1076;&#1086;&#1085;&#1089;&#1082;&#1080; (Macedonian)</option>
    <option value=""zsm_Latn"">Bahasa Melayu (Malay)</option>
    <option value=""mal_Mlym"">&#3374;&#3378;&#3375;&#3390;&#3379;&#3330; (Malayalam)</option>
    <option value=""mlt_Latn"">Malti (Maltese)</option>
    <option value=""mri_Latn"">Te Reo M&#257;ori (Maori)</option>
    <option value=""mar_Deva"">&#2350;&#2352;&#2366;&#2336;&#2368; (Marathi)</option>
    <option value=""khk_Cyrl"">&#1052;&#1086;&#1085;&#1075;&#1086;&#1083; (Mongolian)</option>
    <option value=""mya_Mymr"">&#4121;&#4156;&#4116;&#4154;&#4121;&#4140; (Myanmar)</option>
    <option value=""npi_Deva"">&#2344;&#2375;&#2346;&#2366;&#2354;&#2368; (Nepali)</option>
    <option value=""nob_Latn"">Norsk (Norwegian)</option>
    <option value=""pes_Arab"">&#1601;&#1575;&#1585;&#1587;&#1740; (Persian)</option>
    <option value=""pol_Latn"">Polski (Polish)</option>
    <option value=""por_Latn"">Portugu&#234;s (Portuguese)</option>
    <option value=""pan_Guru"">&#2602;&#2672;&#2588;&#2622;&#2604;&#2624; (Punjabi)</option>
    <option value=""ron_Latn"">Rom&#226;n&#259; (Romanian)</option>
    <option value=""rus_Cyrl"">&#1056;&#1091;&#1089;&#1089;&#1082;&#1080;&#1081; (Russian)</option>
    <option value=""srp_Cyrl"">&#1057;&#1088;&#1087;&#1089;&#1082;&#1080; (Serbian)</option>
    <option value=""sna_Latn"">Shona</option>
    <option value=""snd_Arab"">&#1587;&#1606;&#1068;&#1610; (Sindhi)</option>
    <option value=""sin_Sinh"">&#3523;&#3538;&#3458;&#3524;&#3517; (Sinhala)</option>
    <option value=""slk_Latn"">Sloven&#269;ina (Slovak)</option>
    <option value=""slv_Latn"">Sloven&#353;&#269;ina (Slovenian)</option>
    <option value=""som_Latn"">Soomaali (Somali)</option>
    <option value=""spa_Latn"">Espa&#241;ol (Spanish)</option>
    <option value=""sun_Latn"">Basa Sunda (Sundanese)</option>
    <option value=""swh_Latn"">Kiswahili (Swahili)</option>
    <option value=""swe_Latn"">Svenska (Swedish)</option>
    <option value=""tgl_Latn"">Tagalog (Filipino)</option>
    <option value=""tgk_Cyrl"">&#1058;&#1086;&#1207;&#1080;&#1082;&#1251; (Tajik)</option>
    <option value=""tam_Taml"">&#2980;&#2990;&#3007;&#2996;&#3021; (Tamil)</option>
    <option value=""tat_Cyrl"">&#1058;&#1072;&#1090;&#1072;&#1088; (Tatar)</option>
    <option value=""tel_Telu"">&#3108;&#3142;&#3122;&#3137;&#3095;&#3137; (Telugu)</option>
    <option value=""tha_Thai"">&#3616;&#3634;&#3625;&#3634;&#3652;&#3607;&#3618; (Thai)</option>
    <option value=""tur_Latn"">T&#252;rk&#231;e (Turkish)</option>
    <option value=""tuk_Latn"">T&#252;rkmen (Turkmen)</option>
    <option value=""ukr_Cyrl"">&#1059;&#1082;&#1088;&#1072;&#1111;&#1085;&#1089;&#1100;&#1082;&#1072; (Ukrainian)</option>
    <option value=""urd_Arab"">&#1575;&#1585;&#1583;&#1608; (Urdu)</option>
    <option value=""uzn_Latn"">O&#8216;zbek (Uzbek)</option>
    <option value=""vie_Latn"">Ti&#7871;ng Vi&#7879;t (Vietnamese)</option>
    <option value=""cym_Latn"">Cymraeg (Welsh)</option>
    <option value=""yor_Latn"">Yor&#249;b&#225; (Yoruba)</option>
    <option value=""zul_Latn"">isiZulu (Zulu)</option>
  </select>
  <label id=""lblScroll"">Scroll Direction</label>
  <select id=""scrollDir"" onchange=""setScrollDir(this.value)"">
    <option value=""up"">Bottom-up (newest at bottom)</option>
    <option value=""down"">Top-down (newest at top)</option>
  </select>
  <label id=""lblTags"">Tags</label>
  <select id=""tagMode"" onchange=""setTagMode(this.value)"">
    <option value=""off"">Off</option>
    <option value=""lang"">Language</option>
    <option value=""time"">Time</option>
    <option value=""both"">Language + Time</option>
  </select>
  <button id=""btnSave"" onclick=""saveTranscript()"" style=""width:100%;margin-top:12px"">&#128190; Save Transcript</button>
</div>
<div id=""container""><div id=""lines""><div id=""spacer""></div></div></div>
<script>
var T={};
(function(){
  var lang=(navigator.language||'en').toLowerCase();
  var lc=lang.split('-')[0];
  var tr={
    en:{connecting:'Connecting...',connected:'Connected',disconnected:'Disconnected - reconnecting...',
        wakeTitle:'Keep Screen On',wakeDesc:'A secure connection is needed (one-time setup):',
        stepTap:'Tap the button below',stepWarn:'You will see a warning page \u2014 this is normal',
        stepAdv:'Tap \u0022Advanced\u0022',stepProceed:'Tap \u0022Proceed to {0}\u0022',
        stepAccept:'Tap \u0022Accept the Risk and Continue\u0022',
        stepDetails:'Tap \u0022Show Details\u0022',stepVisit:'Tap \u0022visit this website\u0022',
        stepRetry:'Tap the screen wake button again',
        openSecure:'Open Secure Page',cancel:'Cancel',
        sending:'Sending...',cmdSent:' command sent',cmdFail:'Failed to send command',
        liveRun:'Live: RUNNING',simRun:'Simulation: RUNNING',stopped:'Status: STOPPED',
        noServer:'Unable to reach server',checking:'Checking...',
        dfltVoice:'Default',title:'Live Subtitles',
        bold:'Bold',font:'Font',style:'Style',voice:'Voice',speed:'Speed',color:'Text Color',
        slow:'Slow',normal:'Normal',fast:'Fast',vfast:'Very Fast',
        start:'Start',stop:'Stop',restart:'Restart',simulate:'Simulate',clear:'Clear',
        saveTranscript:'Save Transcript',transLang:'Translation',remote:'Remote Control',settings:'Settings',readAloud:'Read aloud',keepScreen:'Keep screen on',scrollDir:'Scroll Direction',scrollUp:'Bottom-up (newest at bottom)',scrollDown:'Top-down (newest at top)',tags:'Tags',tagOff:'Off',tagLang:'Language',tagTime:'Time',tagBoth:'Language + Time'},
    es:{connecting:'Conectando...',connected:'Conectado',disconnected:'Desconectado - reconectando...',
        wakeTitle:'Mantener Pantalla',wakeDesc:'Se necesita conexi\u00f3n segura (configuraci\u00f3n \u00fanica):',
        stepTap:'Toca el bot\u00f3n de abajo',stepWarn:'Ver\u00e1s una advertencia \u2014 es normal',
        stepAdv:'Toca \u0022Avanzado\u0022',stepProceed:'Toca \u0022Continuar a {0}\u0022',
        stepAccept:'Toca \u0022Aceptar el riesgo y continuar\u0022',
        stepDetails:'Toca \u0022Mostrar detalles\u0022',stepVisit:'Toca \u0022visitar este sitio web\u0022',
        stepRetry:'Toca el bot\u00f3n de pantalla de nuevo',
        openSecure:'Abrir P\u00e1gina Segura',cancel:'Cancelar',
        sending:'Enviando...',cmdSent:' comando enviado',cmdFail:'Error al enviar comando',
        liveRun:'En vivo: ACTIVO',simRun:'Simulaci\u00f3n: ACTIVA',stopped:'Estado: DETENIDO',
        noServer:'No se puede conectar',checking:'Comprobando...',
        dfltVoice:'Predeterminado',title:'Subt\u00edtulos en Vivo',
        bold:'Negrita',font:'Fuente',style:'Estilo',voice:'Voz',speed:'Velocidad',color:'Color de texto',
        slow:'Lento',normal:'Normal',fast:'R\u00e1pido',vfast:'Muy R\u00e1pido',
        start:'Iniciar',stop:'Detener',restart:'Reiniciar',simulate:'Simular',clear:'Limpiar',
        saveTranscript:'Guardar Transcripci\u00f3n',transLang:'Traducci\u00f3n',remote:'Control Remoto',settings:'Ajustes',readAloud:'Leer en voz alta',keepScreen:'Mantener pantalla',scrollDir:'Direcci\u00f3n de desplazamiento',scrollUp:'Abajo-arriba (reciente abajo)',scrollDown:'Arriba-abajo (reciente arriba)',tags:'Etiquetas',tagOff:'Desactivado',tagLang:'Idioma',tagTime:'Hora',tagBoth:'Idioma + Hora'},
    fr:{connecting:'Connexion...',connected:'Connect\u00e9',disconnected:'D\u00e9connect\u00e9 - reconnexion...',
        wakeTitle:'\u00c9cran Allum\u00e9',wakeDesc:'Connexion s\u00e9curis\u00e9e requise (une seule fois) :',
        stepTap:'Appuyez sur le bouton ci-dessous',stepWarn:'Un avertissement s\u0027affichera \u2014 c\u0027est normal',
        stepAdv:'Appuyez sur \u0022Avanc\u00e9\u0022',stepProceed:'Appuyez sur \u0022Continuer vers {0}\u0022',
        stepAccept:'Appuyez sur \u0022Accepter le risque\u0022',
        stepDetails:'Appuyez sur \u0022Afficher les d\u00e9tails\u0022',stepVisit:'Appuyez sur \u0022acc\u00e9der \u00e0 ce site\u0022',
        stepRetry:'Appuyez \u00e0 nouveau sur le bouton veille',
        openSecure:'Ouvrir Page S\u00e9curis\u00e9e',cancel:'Annuler',
        sending:'Envoi...',cmdSent:' commande envoy\u00e9e',cmdFail:'Erreur d\u0027envoi',
        liveRun:'En direct : ACTIF',simRun:'Simulation : ACTIVE',stopped:'\u00c9tat : ARR\u00caT\u00c9',
        noServer:'Serveur inaccessible',checking:'V\u00e9rification...',
        dfltVoice:'Par d\u00e9faut',title:'Sous-titres en Direct',
        bold:'Gras',font:'Police',style:'Style',voice:'Voix',speed:'Vitesse',color:'Couleur du texte',
        slow:'Lent',normal:'Normal',fast:'Rapide',vfast:'Tr\u00e8s Rapide',
        start:'D\u00e9marrer',stop:'Arr\u00eater',restart:'Red\u00e9marrer',simulate:'Simuler',clear:'Effacer',
        saveTranscript:'Enregistrer',transLang:'Traduction',remote:'T\u00e9l\u00e9commande',settings:'Param\u00e8tres',readAloud:'Lire \u00e0 voix haute',keepScreen:'\u00c9cran allum\u00e9',scrollDir:'Direction du d\u00e9filement',scrollUp:'Bas en haut (r\u00e9cent en bas)',scrollDown:'Haut en bas (r\u00e9cent en haut)',tags:'\u00c9tiquettes',tagOff:'D\u00e9sactiv\u00e9',tagLang:'Langue',tagTime:'Heure',tagBoth:'Langue + Heure'},
    de:{connecting:'Verbinde...',connected:'Verbunden',disconnected:'Getrennt - verbinde erneut...',
        wakeTitle:'Bildschirm An',wakeDesc:'Sichere Verbindung erforderlich (einmalig):',
        stepTap:'Tippen Sie auf den Button unten',stepWarn:'Sie sehen eine Warnung \u2014 das ist normal',
        stepAdv:'Tippen Sie auf \u0022Erweitert\u0022',stepProceed:'Tippen Sie auf \u0022Weiter zu {0}\u0022',
        stepAccept:'Tippen Sie auf \u0022Risiko akzeptieren\u0022',
        stepDetails:'Tippen Sie auf \u0022Details anzeigen\u0022',stepVisit:'Tippen Sie auf \u0022Website besuchen\u0022',
        stepRetry:'Tippen Sie erneut auf den Wach-Button',
        openSecure:'Sichere Seite \u00d6ffnen',cancel:'Abbrechen',
        sending:'Sende...',cmdSent:' Befehl gesendet',cmdFail:'Befehl fehlgeschlagen',
        liveRun:'Live: L\u00c4UFT',simRun:'Simulation: L\u00c4UFT',stopped:'Status: GESTOPPT',
        noServer:'Server nicht erreichbar',checking:'Pr\u00fcfe...',
        dfltVoice:'Standard',title:'Live-Untertitel',
        bold:'Fett',font:'Schrift',style:'Stil',voice:'Stimme',speed:'Geschwindigkeit',color:'Textfarbe',
        slow:'Langsam',normal:'Normal',fast:'Schnell',vfast:'Sehr Schnell',
        start:'Starten',stop:'Stoppen',restart:'Neustarten',simulate:'Simulieren',clear:'L\u00f6schen',
        saveTranscript:'Speichern',transLang:'\u00dcbersetzung',remote:'Fernsteuerung',settings:'Einstellungen',readAloud:'Vorlesen',keepScreen:'Bildschirm an',scrollDir:'Scrollrichtung',scrollUp:'Unten nach oben (neueste unten)',scrollDown:'Oben nach unten (neueste oben)',tags:'Tags',tagOff:'Aus',tagLang:'Sprache',tagTime:'Zeit',tagBoth:'Sprache + Zeit'},
    ca:{connecting:'Connectant...',connected:'Connectat',disconnected:'Desconnectat - reconnectant...',
        wakeTitle:'Mantenir Pantalla',wakeDesc:'Cal connexi\u00f3 segura (configuraci\u00f3 \u00fanica):',
        stepTap:'Toca el bot\u00f3 de sota',stepWarn:'Veur\u00e0s un av\u00eds \u2014 \u00e9s normal',
        stepAdv:'Toca \u0022Avan\u00e7at\u0022',stepProceed:'Toca \u0022Continuar a {0}\u0022',
        stepAccept:'Toca \u0022Acceptar el risc i continuar\u0022',
        stepDetails:'Toca \u0022Mostrar detalls\u0022',stepVisit:'Toca \u0022visitar aquest lloc\u0022',
        stepRetry:'Toca el bot\u00f3 de pantalla de nou',
        openSecure:'Obrir P\u00e0gina Segura',cancel:'Cancel\u00b7lar',
        sending:'Enviant...',cmdSent:' comanda enviada',cmdFail:'Error en enviar',
        liveRun:'En directe: ACTIU',simRun:'Simulaci\u00f3: ACTIVA',stopped:'Estat: ATURAT',
        noServer:'No es pot connectar',checking:'Comprovant...',
        dfltVoice:'Per defecte',title:'Subt\u00edtols en Directe',
        bold:'Negreta',font:'Tipus de lletra',style:'Estil',voice:'Veu',speed:'Velocitat',color:'Color del text',
        slow:'Lent',normal:'Normal',fast:'R\u00e0pid',vfast:'Molt R\u00e0pid',
        start:'Iniciar',stop:'Aturar',restart:'Reiniciar',simulate:'Simular',clear:'Netejar',
        saveTranscript:'Desar Transcripci\u00f3',transLang:'Traducci\u00f3',remote:'Control Remot',settings:'Ajustos',readAloud:'Llegir en veu alta',keepScreen:'Mantenir pantalla',scrollDir:'Direcci\u00f3 de despla\u00e7ament',scrollUp:'Baix a dalt (recent a baix)',scrollDown:'Dalt a baix (recent a dalt)',tags:'Etiquetes',tagOff:'Desactivat',tagLang:'Idioma',tagTime:'Hora',tagBoth:'Idioma + Hora'},
    pt:{connecting:'Conectando...',connected:'Conectado',disconnected:'Desconectado - reconectando...',
        wakeTitle:'Manter Tela Ligada',wakeDesc:'Conex\u00e3o segura necess\u00e1ria (apenas uma vez):',
        stepTap:'Toque no bot\u00e3o abaixo',stepWarn:'Voc\u00ea ver\u00e1 um aviso \u2014 isso \u00e9 normal',
        stepAdv:'Toque em \u0022Avan\u00e7ado\u0022',stepProceed:'Toque em \u0022Prosseguir para {0}\u0022',
        stepAccept:'Toque em \u0022Aceitar o risco e continuar\u0022',
        stepDetails:'Toque em \u0022Mostrar detalhes\u0022',stepVisit:'Toque em \u0022visitar este site\u0022',
        stepRetry:'Toque no bot\u00e3o de tela novamente',
        openSecure:'Abrir P\u00e1gina Segura',cancel:'Cancelar',
        sending:'Enviando...',cmdSent:' comando enviado',cmdFail:'Falha ao enviar',
        liveRun:'Ao vivo: ATIVO',simRun:'Simula\u00e7\u00e3o: ATIVA',stopped:'Status: PARADO',
        noServer:'N\u00e3o foi poss\u00edvel conectar',checking:'Verificando...',
        dfltVoice:'Padr\u00e3o',title:'Legendas ao Vivo',
        bold:'Negrito',font:'Fonte',style:'Estilo',voice:'Voz',speed:'Velocidade',color:'Cor do texto',
        slow:'Lento',normal:'Normal',fast:'R\u00e1pido',vfast:'Muito R\u00e1pido',
        start:'Iniciar',stop:'Parar',restart:'Reiniciar',simulate:'Simular',clear:'Limpar',
        saveTranscript:'Salvar Transcri\u00e7\u00e3o',transLang:'Tradu\u00e7\u00e3o',remote:'Controle Remoto',settings:'Configura\u00e7\u00f5es',readAloud:'Ler em voz alta',keepScreen:'Manter tela ligada',scrollDir:'Dire\u00e7\u00e3o de rolagem',scrollUp:'Baixo para cima (recente embaixo)',scrollDown:'Cima para baixo (recente em cima)',tags:'Etiquetas',tagOff:'Desativado',tagLang:'Idioma',tagTime:'Hora',tagBoth:'Idioma + Hora'},
    ja:{connecting:'\u63a5\u7d9a\u4e2d...',connected:'\u63a5\u7d9a\u6e08\u307f',disconnected:'\u5207\u65ad - \u518d\u63a5\u7d9a\u4e2d...',
        wakeTitle:'\u753b\u9762\u3092\u70b9\u706f',wakeDesc:'\u5b89\u5168\u306a\u63a5\u7d9a\u304c\u5fc5\u8981\u3067\u3059\uff08\u521d\u56de\u306e\u307f\uff09:',
        stepTap:'\u4e0b\u306e\u30dc\u30bf\u30f3\u3092\u30bf\u30c3\u30d7',stepWarn:'\u8b66\u544a\u304c\u8868\u793a\u3055\u308c\u307e\u3059 \u2014 \u6b63\u5e38\u3067\u3059',
        stepAdv:'\u0022\u8a73\u7d30\u8a2d\u5b9a\u0022\u3092\u30bf\u30c3\u30d7',stepProceed:'\u0022{0}\u306b\u30a2\u30af\u30bb\u30b9\u0022\u3092\u30bf\u30c3\u30d7',
        stepAccept:'\u0022\u30ea\u30b9\u30af\u3092\u627f\u8afe\u3057\u3066\u7d9a\u884c\u0022\u3092\u30bf\u30c3\u30d7',
        stepDetails:'\u0022\u8a73\u7d30\u3092\u8868\u793a\u0022\u3092\u30bf\u30c3\u30d7',stepVisit:'\u0022\u3053\u306e\u30b5\u30a4\u30c8\u3092\u8a2a\u554f\u0022\u3092\u30bf\u30c3\u30d7',
        stepRetry:'\u753b\u9762\u70b9\u706f\u30dc\u30bf\u30f3\u3092\u518d\u5ea6\u30bf\u30c3\u30d7',
        openSecure:'\u5b89\u5168\u306a\u30da\u30fc\u30b8\u3092\u958b\u304f',cancel:'\u30ad\u30e3\u30f3\u30bb\u30eb',
        sending:'\u9001\u4fe1\u4e2d...',cmdSent:'\u30b3\u30de\u30f3\u30c9\u9001\u4fe1\u6e08\u307f',cmdFail:'\u30b3\u30de\u30f3\u30c9\u9001\u4fe1\u5931\u6557',
        liveRun:'\u30e9\u30a4\u30d6: \u5b9f\u884c\u4e2d',simRun:'\u30b7\u30df\u30e5\u30ec\u30fc\u30b7\u30e7\u30f3: \u5b9f\u884c\u4e2d',stopped:'\u30b9\u30c6\u30fc\u30bf\u30b9: \u505c\u6b62',
        noServer:'\u30b5\u30fc\u30d0\u30fc\u306b\u63a5\u7d9a\u3067\u304d\u307e\u305b\u3093',checking:'\u78ba\u8a8d\u4e2d...',
        dfltVoice:'\u30c7\u30d5\u30a9\u30eb\u30c8',title:'\u30e9\u30a4\u30d6\u5b57\u5e55',
        bold:'\u592a\u5b57',font:'\u30d5\u30a9\u30f3\u30c8',style:'\u30b9\u30bf\u30a4\u30eb',voice:'\u97f3\u58f0',speed:'\u901f\u5ea6',color:'\u6587\u5b57\u8272',
        slow:'\u9045\u3044',normal:'\u666e\u901a',fast:'\u901f\u3044',vfast:'\u3068\u3066\u3082\u901f\u3044',
        start:'\u958b\u59cb',stop:'\u505c\u6b62',restart:'\u518d\u958b',simulate:'\u30b7\u30df\u30e5\u30ec\u30fc\u30b7\u30e7\u30f3',clear:'\u30af\u30ea\u30a2',
        saveTranscript:'\u4fdd\u5b58',transLang:'\u7ffb\u8a33',remote:'\u30ea\u30e2\u30fc\u30c8',settings:'\u8a2d\u5b9a',readAloud:'\u8aad\u307f\u4e0a\u3052',keepScreen:'\u753b\u9762\u70b9\u706f',scrollDir:'\u30b9\u30af\u30ed\u30fc\u30eb\u65b9\u5411',scrollUp:'\u4e0b\u304b\u3089\u4e0a (\u6700\u65b0\u304c\u4e0b)',scrollDown:'\u4e0a\u304b\u3089\u4e0b (\u6700\u65b0\u304c\u4e0a)',tags:'\u30bf\u30b0',tagOff:'\u30aa\u30d5',tagLang:'\u8a00\u8a9e',tagTime:'\u6642\u523b',tagBoth:'\u8a00\u8a9e + \u6642\u523b'},
    zh:{connecting:'\u8fde\u63a5\u4e2d...',connected:'\u5df2\u8fde\u63a5',disconnected:'\u5df2\u65ad\u5f00 - \u91cd\u65b0\u8fde\u63a5...',
        wakeTitle:'\u4fdd\u6301\u5c4f\u5e55\u5e38\u4eae',wakeDesc:'\u9700\u8981\u5b89\u5168\u8fde\u63a5\uff08\u4ec5\u9700\u4e00\u6b21\uff09:',
        stepTap:'\u70b9\u51fb\u4e0b\u65b9\u6309\u94ae',stepWarn:'\u60a8\u5c06\u770b\u5230\u8b66\u544a\u9875\u9762 \u2014 \u8fd9\u662f\u6b63\u5e38\u7684',
        stepAdv:'\u70b9\u51fb\u0022\u9ad8\u7ea7\u0022',stepProceed:'\u70b9\u51fb\u0022\u7ee7\u7eed\u8bbf\u95ee{0}\u0022',
        stepAccept:'\u70b9\u51fb\u0022\u63a5\u53d7\u98ce\u9669\u5e76\u7ee7\u7eed\u0022',
        stepDetails:'\u70b9\u51fb\u0022\u663e\u793a\u8be6\u60c5\u0022',stepVisit:'\u70b9\u51fb\u0022\u8bbf\u95ee\u6b64\u7f51\u7ad9\u0022',
        stepRetry:'\u518d\u6b21\u70b9\u51fb\u5c4f\u5e55\u5e38\u4eae\u6309\u94ae',
        openSecure:'\u6253\u5f00\u5b89\u5168\u9875\u9762',cancel:'\u53d6\u6d88',
        sending:'\u53d1\u9001\u4e2d...',cmdSent:'\u547d\u4ee4\u5df2\u53d1\u9001',cmdFail:'\u53d1\u9001\u5931\u8d25',
        liveRun:'\u76f4\u64ad: \u8fd0\u884c\u4e2d',simRun:'\u6a21\u62df: \u8fd0\u884c\u4e2d',stopped:'\u72b6\u6001: \u5df2\u505c\u6b62',
        noServer:'\u65e0\u6cd5\u8fde\u63a5\u670d\u52a1\u5668',checking:'\u68c0\u67e5\u4e2d...',
        dfltVoice:'\u9ed8\u8ba4',title:'\u5b9e\u65f6\u5b57\u5e55',
        bold:'\u7c97\u4f53',font:'\u5b57\u4f53',style:'\u6837\u5f0f',voice:'\u8bed\u97f3',speed:'\u901f\u5ea6',color:'\u6587\u5b57\u989c\u8272',
        slow:'\u6162',normal:'\u6b63\u5e38',fast:'\u5feb',vfast:'\u975e\u5e38\u5feb',
        start:'\u5f00\u59cb',stop:'\u505c\u6b62',restart:'\u91cd\u542f',simulate:'\u6a21\u62df',clear:'\u6e05\u9664',
        saveTranscript:'\u4fdd\u5b58',transLang:'\u7ffb\u8bd1',remote:'\u8fdc\u7a0b\u63a7\u5236',settings:'\u8bbe\u7f6e',readAloud:'\u6717\u8bfb',keepScreen:'\u4fdd\u6301\u5c4f\u5e55',scrollDir:'\u6eda\u52a8\u65b9\u5411',scrollUp:'\u4ece\u4e0b\u5f80\u4e0a (\u6700\u65b0\u5728\u4e0b)',scrollDown:'\u4ece\u4e0a\u5f80\u4e0b (\u6700\u65b0\u5728\u4e0a)',tags:'\u6807\u7b7e',tagOff:'\u5173\u95ed',tagLang:'\u8bed\u8a00',tagTime:'\u65f6\u95f4',tagBoth:'\u8bed\u8a00 + \u65f6\u95f4'}
  };
  if(lang.indexOf('zh')===0)T=tr.zh;
  else T=tr[lc]||tr.en;
})();
function t(k){return T[k]||k}
var fontSize=28;
var currentEl=null;
var speakEnabled=false;
var selectedVoice='';
var speechRate=1;
var synth=window.speechSynthesis;
var lines=document.getElementById('lines');
var container=document.getElementById('container');
var statusEl=document.getElementById('status');
var panel=document.getElementById('panel');
var btnSpeak=document.getElementById('btnSpeak');
var voiceSelect=document.getElementById('voiceSelect');
var rateSelect=document.getElementById('rateSelect');

/* Restore saved preferences */
if(localStorage.getItem('voice'))selectedVoice=localStorage.getItem('voice');
if(localStorage.getItem('rate')){speechRate=parseFloat(localStorage.getItem('rate'));rateSelect.value=localStorage.getItem('rate')}
if(localStorage.getItem('speak')==='true'){speakEnabled=true;btnSpeak.classList.add('active');btnSpeak.innerHTML='&#128266;'}

function populateVoices(){
  var voices=synth.getVoices();
  voiceSelect.innerHTML='';
  var defOpt=document.createElement('option');defOpt.value='';defOpt.textContent=t('dfltVoice');voiceSelect.appendChild(defOpt);
  for(var i=0;i<voices.length;i++){
    var v=voices[i];
    var opt=document.createElement('option');opt.value=v.name;
    opt.textContent=v.name+(v.lang?' ('+v.lang+')':'');
    if(v.name===selectedVoice)opt.selected=true;
    voiceSelect.appendChild(opt);
  }
}
populateVoices();
if(synth.onvoiceschanged!==undefined)synth.onvoiceschanged=populateVoices;

function closeAllPanels(){panel.style.display='none';adminPanel.style.display='none';if(adminPollTimer){clearInterval(adminPollTimer);adminPollTimer=null}}
function togglePanel(){if(panel.style.display==='block'){panel.style.display='none'}else{closeAllPanels();panel.style.display='block'}}
document.addEventListener('click',function(e){if(!panel.contains(e.target)&&!adminPanel.contains(e.target)&&!document.getElementById('toolbar').contains(e.target)){closeAllPanels()}})

function toggleSpeak(){
  speakEnabled=!speakEnabled;
  localStorage.setItem('speak',speakEnabled);
  if(speakEnabled){btnSpeak.classList.add('active');btnSpeak.innerHTML='&#128266;'}
  else{btnSpeak.classList.remove('active');btnSpeak.innerHTML='&#128264;';synth.cancel()}
}

function speak(text){
  if(!speakEnabled||!synth||!text)return;
  var utter=new SpeechSynthesisUtterance(text);
  utter.rate=speechRate;
  if(selectedVoice){var voices=synth.getVoices();for(var i=0;i<voices.length;i++){if(voices[i].name===selectedVoice){utter.voice=voices[i];break}}}
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
  autoScroll()}
function changeFontSize(d){fontSize=Math.max(12,Math.min(80,fontSize+d));localStorage.setItem('fontSize',fontSize);applyStylesToAll();closeAllPanels()}
function changeFont(f){fontFamily=f;localStorage.setItem('fontFamily',f);applyStylesToAll()}
function toggleBold(){isBold=!isBold;localStorage.setItem('bold',isBold);document.getElementById('btnBold').classList.toggle('active');applyStylesToAll();closeAllPanels()}
function changeColor(c){textColor=c;localStorage.setItem('textColor',c);applyStylesToAll()}
function saveTranscript(){
  var els=document.querySelectorAll('.line:not(.in-progress)');
  var text='';
  for(var i=0;i<els.length;i++){
    var ln=els[i].textContent;if(ln)text+=ln+'\n';
  }
  if(!text){return}
  var blob=new Blob([text],{type:'text/plain;charset=utf-8'});
  var url=URL.createObjectURL(blob);
  var a=document.createElement('a');
  a.href=url;
  var d=new Date();
  var pad=function(n){return n<10?'0'+n:''+n};
  a.download='transcript_'+d.getFullYear()+'-'+pad(d.getMonth()+1)+'-'+pad(d.getDate())+'_'+pad(d.getHours())+pad(d.getMinutes())+'.txt';
  document.body.appendChild(a);a.click();
  setTimeout(function(){document.body.removeChild(a);URL.revokeObjectURL(url)},100);
  closeAllPanels();
}
var userScrolled=false;
var scrollMode=localStorage.getItem('scrollDir')||'up';
var tagMode=localStorage.getItem('tagMode')||'lang';
function setTagMode(val){tagMode=val;localStorage.setItem('tagMode',val);closeAllPanels()}
function buildTag(lang,time){
  var parts=[];
  if((tagMode==='lang'||tagMode==='both')&&lang){parts.push(lang)}
  if((tagMode==='time'||tagMode==='both')&&time){parts.push(time)}
  if(parts.length===0)return '';
  return '['+parts.join(' ')+'] ';
}
var spacer=document.getElementById('spacer');
function applyScrollMode(){
  if(scrollMode==='down'){spacer.style.display='none';container.scrollTop=0}
  else{spacer.style.display='';container.scrollTop=container.scrollHeight}
  userScrolled=false;
}
function setScrollDir(val){scrollMode=val;localStorage.setItem('scrollDir',val);applyScrollMode();closeAllPanels()}
container.addEventListener('scroll',function(){
  if(scrollMode==='down'){var atTop=container.scrollTop<60;userScrolled=!atTop}
  else{var atBottom=container.scrollHeight-container.scrollTop-container.clientHeight<60;userScrolled=!atBottom}
});
function autoScroll(){if(!userScrolled){if(scrollMode==='down'){container.scrollTop=0}else{container.scrollTop=container.scrollHeight}}}
function insertLine(el){
  if(scrollMode==='down'){
    var first=spacer.nextSibling;
    if(first){lines.insertBefore(el,first)}else{lines.appendChild(el)}
  }else{lines.appendChild(el)}
}
function addCommitted(text,lang,time){
  var el;
  if(currentEl){el=currentEl;currentEl=null;
    if(scrollMode==='down'&&el.parentNode){el.parentNode.removeChild(el);insertLine(el)}
  }else{el=document.createElement('div');insertLine(el)}
  var tag=buildTag(lang,time);
  el.textContent=tag+text;
  el.className='line';
  el.style.fontSize=fontSize+'px';el.style.fontFamily=fontFamily;el.style.fontWeight=isBold?'bold':'normal';
  el.style.color='#ffdd57';
  el.dataset.highlighted='1';
  setTimeout(function(){el.style.color=textColor;delete el.dataset.highlighted},5000);
  autoScroll();
  while(lines.children.length>201){if(scrollMode==='down'){lines.removeChild(lines.lastChild)}else{lines.removeChild(lines.children[1])}}
  speak(text);
}
function updateCurrent(text){
  if(!currentEl){currentEl=document.createElement('div');currentEl.className='line in-progress';currentEl.style.fontSize=fontSize+'px';currentEl.style.fontFamily=fontFamily;currentEl.style.fontWeight=isBold?'bold':'normal';insertLine(currentEl)}
  currentEl.textContent=text;
  autoScroll()
}
applyScrollMode();
var wsRef=null;
function setTransLang(lang){
  localStorage.setItem('transLang',lang);
  closeAllPanels();
  if(wsRef&&wsRef.readyState===1){wsRef.send(JSON.stringify({type:'setLanguage',language:lang}))}
}
(function(){var tls=document.getElementById('transLangSelect');var saved=localStorage.getItem('transLang')||'';
  for(var i=0;i<tls.options.length;i++){if(tls.options[i].value===saved){tls.selectedIndex=i;break}}
})();
function connect(){
  var proto=location.protocol==='https:'?'wss:':'ws:';
  var ws=new WebSocket(proto+'//'+location.host+'/ws');
  wsRef=ws;
  ws.onopen=function(){statusEl.textContent=t('connected');statusEl.className='connected';
    var lang=localStorage.getItem('transLang')||'';
    if(lang){ws.send(JSON.stringify({type:'setLanguage',language:lang}))}
  };
  ws.onclose=function(){statusEl.textContent=t('disconnected');statusEl.className='disconnected';wsRef=null;setTimeout(connect,2000)};
  ws.onerror=function(){ws.close()};
  ws.onmessage=function(e){
    try{var msg=JSON.parse(e.data);
      if(msg.type==='commit')addCommitted(msg.text,msg.lang||'',msg.time||'');
      else if(msg.type==='update')updateCurrent(msg.text);
      else if(msg.type==='clear'){if(currentEl){currentEl.remove();currentEl=null}while(lines.children.length>1)lines.removeChild(lines.children[1]);autoScroll()}
    }catch(ex){}
  }
}
/* Apply i18n to HTML elements */
document.title=t('title');
statusEl.textContent=t('connecting');
document.getElementById('btnAdmin').title=t('remote');
document.getElementById('btnSettings').title=t('settings');
btnSpeak.title=t('readAloud');
document.getElementById('lblFont').textContent=t('font');
document.getElementById('lblStyle').textContent=t('style');
document.getElementById('lblColor').textContent=t('color');
document.getElementById('btnBold').textContent=t('bold');
document.getElementById('lblVoice').textContent=t('voice');
document.getElementById('lblSpeed').textContent=t('speed');
document.getElementById('lblTransLang').textContent=t('transLang');
document.getElementById('lblScroll').textContent=t('scrollDir');
var sdOpts=document.getElementById('scrollDir').options;sdOpts[0].textContent=t('scrollUp');sdOpts[1].textContent=t('scrollDown');
(function(){var sd=document.getElementById('scrollDir');sd.value=scrollMode;})();
document.getElementById('lblTags').textContent=t('tags');
var tmOpts=document.getElementById('tagMode').options;tmOpts[0].textContent=t('tagOff');tmOpts[1].textContent=t('tagLang');tmOpts[2].textContent=t('tagTime');tmOpts[3].textContent=t('tagBoth');
(function(){var tm=document.getElementById('tagMode');tm.value=tagMode;})();
document.getElementById('btnSave').innerHTML='&#128190; '+t('saveTranscript');
var rOpts=rateSelect.options;rOpts[0].textContent=t('slow');rOpts[1].textContent=t('normal');rOpts[2].textContent=t('fast');rOpts[3].textContent=t('vfast');
connect();

/* Keep screen on — Wake Lock toggle button */
var wakeLockObj=null;
var wakeActive=false;
var btnWake=document.getElementById('btnWake');
btnWake.title=t('keepScreen');

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
  setWakeActive(false);
}

function toggleWakeLock(){if(wakeActive)releaseWakeLock();else acquireWakeLock()}

function showCertSetup(){
  var hp=parseInt(location.port||80)+1;
  var url='https://'+location.hostname+':'+hp;
  var d=document.createElement('div');
  d.style.cssText='position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.95);z-index:1000;display:flex;flex-direction:column;align-items:center;justify-content:center;padding:24px;color:#fff;font-size:16px;line-height:1.6';
  var c=document.createElement('div');c.style.cssText='max-width:400px;text-align:left';
  var h=document.createElement('h2');h.style.cssText='color:#ffdd57;margin-bottom:16px;text-align:center';h.textContent=t('wakeTitle');c.appendChild(h);
  var p0=document.createElement('p');p0.style.cssText='margin-bottom:16px;text-align:center';p0.textContent=t('wakeDesc');c.appendChild(p0);
  var isFF=navigator.userAgent.indexOf('Firefox')>-1;
  var isSafari=/Safari/.test(navigator.userAgent)&&!/Chrome/.test(navigator.userAgent);
  var steps;
  if(isFF){steps=[t('stepTap'),t('stepWarn'),t('stepAdv'),t('stepAccept'),t('stepRetry')]}
  else if(isSafari){steps=[t('stepTap'),t('stepWarn'),t('stepDetails'),t('stepVisit'),t('stepRetry')]}
  else{steps=[t('stepTap'),t('stepWarn'),t('stepAdv'),t('stepProceed').replace('{0}',location.hostname),t('stepRetry')]}
  for(var i=0;i<steps.length;i++){var s=document.createElement('p');s.style.cssText='margin-bottom:8px;padding-left:8px';s.textContent=(i+1)+'. '+steps[i];c.appendChild(s)}
  var br=document.createElement('div');br.style.cssText='text-align:center;margin-top:20px';
  var a1=document.createElement('a');a1.href=url;a1.textContent=t('openSecure');a1.style.cssText='display:inline-block;background:#47f;color:#fff;padding:14px 28px;border-radius:8px;text-decoration:none;font-size:18px;margin-bottom:16px';br.appendChild(a1);
  br.appendChild(document.createElement('br'));
  var b2=document.createElement('button');b2.textContent=t('cancel');b2.style.cssText='background:#333;color:#aaa;border:1px solid #555;padding:8px 20px;border-radius:6px;font-size:14px;cursor:pointer;margin-top:8px';b2.onclick=function(){d.remove()};br.appendChild(b2);
  c.appendChild(br);d.appendChild(c);document.body.appendChild(d);
}

document.addEventListener('visibilitychange',function(){
  if(document.visibilityState==='visible'&&wakeActive&&!wakeLockObj)acquireWakeLock();
});

/* Admin remote control */
var adminPanel=document.getElementById('adminPanel');
var adminStatus=document.getElementById('adminStatus');
var adminPollTimer=null;
function toggleAdmin(){
  if(adminPanel.style.display==='block'){adminPanel.style.display='none';if(adminPollTimer){clearInterval(adminPollTimer);adminPollTimer=null}}
  else{closeAllPanels();adminPanel.style.display='block';pollStatus();adminPollTimer=setInterval(pollStatus,3000)}
}
function sendCommand(action){
  adminStatus.textContent=t('sending');
  fetch('/api/control?action='+action).then(function(r){return r.json()}).then(function(d){
    adminStatus.textContent=action+t('cmdSent');
    setTimeout(function(){closeAllPanels()},600);
  }).catch(function(){adminStatus.textContent=t('cmdFail')});
}
function pollStatus(){
  fetch('/api/control?action=status').then(function(r){return r.json()}).then(function(d){
    if(d.live){adminStatus.textContent=t('liveRun');adminStatus.style.color='#4f4'}
    else if(d.sim){adminStatus.textContent=t('simRun');adminStatus.style.color='#fa0'}
    else{adminStatus.textContent=t('stopped');adminStatus.style.color='#f44'}
  }).catch(function(){adminStatus.textContent=t('noServer');adminStatus.style.color='#888'});
}
/* Apply i18n to admin panel */
adminStatus.textContent=t('checking');
var admBtns=document.querySelectorAll('#adminPanel button');
admBtns[0].innerHTML='&#9654; '+t('start');admBtns[1].innerHTML='&#9632; '+t('stop');
admBtns[2].innerHTML='&#8635; '+t('restart');admBtns[3].innerHTML='&#9881; '+t('simulate');
admBtns[4].innerHTML='&#10060; '+t('clear');
</script>
</body>
</html>").Replace("{{BG_COLOR}}", bg).Replace("{{FG_COLOR}}", fg)
        End Function

        ''' <summary>
        ''' Parse a User-Agent string into a short readable description.
        ''' e.g. "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) ... Safari/604.1"
        '''   → "iPhone iOS 17.0 / Safari"
        ''' </summary>
        Private Shared Function ParseUserAgent(ua As String) As String
            If String.IsNullOrWhiteSpace(ua) Then Return "Unknown"

            Dim device = ""
            Dim os = ""
            Dim browser = ""

            ' Detect device/OS
            If ua.Contains("iPhone") Then
                device = "iPhone"
            ElseIf ua.Contains("iPad") Then
                device = "iPad"
            ElseIf ua.Contains("Android") Then
                device = "Android"
            ElseIf ua.Contains("Windows") Then
                device = "Windows"
            ElseIf ua.Contains("Macintosh") Then
                device = "Mac"
            ElseIf ua.Contains("Linux") Then
                device = "Linux"
            ElseIf ua.Contains("CrOS") Then
                device = "ChromeOS"
            End If

            ' Extract OS version
            Dim m = Text.RegularExpressions.Regex.Match(ua, "iPhone OS (\d+[_\.]\d+)")
            If m.Success Then
                os = "iOS " & m.Groups(1).Value.Replace("_", ".")
            Else
                m = Text.RegularExpressions.Regex.Match(ua, "CPU OS (\d+[_\.]\d+)")
                If m.Success Then
                    os = "iPadOS " & m.Groups(1).Value.Replace("_", ".")
                Else
                    m = Text.RegularExpressions.Regex.Match(ua, "Android (\d+[\.\d]*)")
                    If m.Success Then os = "Android " & m.Groups(1).Value
                End If
            End If

            ' Detect browser (order matters — check specific before generic)
            If ua.Contains("EdgA/") OrElse ua.Contains("Edg/") OrElse ua.Contains("Edge/") Then
                browser = "Edge"
            ElseIf ua.Contains("SamsungBrowser/") Then
                browser = "Samsung Browser"
            ElseIf ua.Contains("OPR/") OrElse ua.Contains("Opera") Then
                browser = "Opera"
            ElseIf ua.Contains("CriOS/") Then
                browser = "Chrome (iOS)"
            ElseIf ua.Contains("FxiOS/") Then
                browser = "Firefox (iOS)"
            ElseIf ua.Contains("Firefox/") Then
                browser = "Firefox"
            ElseIf ua.Contains("Chrome/") Then
                browser = "Chrome"
            ElseIf ua.Contains("Safari/") Then
                browser = "Safari"
            ElseIf ua.Contains("MSIE") OrElse ua.Contains("Trident") Then
                browser = "IE"
            End If

            ' Build description
            Dim parts As New List(Of String)()
            If device.Length > 0 Then parts.Add(device)
            If os.Length > 0 AndAlso Not os.StartsWith(device) Then parts.Add(os)
            If os.Length > 0 AndAlso os.StartsWith(device) Then parts.Clear() : parts.Add(os)
            If browser.Length > 0 Then parts.Add(browser)

            If parts.Count = 0 Then Return If(ua.Length > 80, ua.Substring(0, 80) & "...", ua)
            Return String.Join(" / ", parts)
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
