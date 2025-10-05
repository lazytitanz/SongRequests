Imports System
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Threading
Imports Newtonsoft.Json

''' <summary>
''' HTTP server for hosting the Song Requests web UI
''' </summary>
Public Class WebServer
    Private ReadOnly _listener As HttpListener
    Private ReadOnly _port As Integer
    Private ReadOnly _queueManager As QueueManager
    Private ReadOnly _youtube As YouTubeService
    Private ReadOnly _cacheFolder As String
    Private _listenerThread As Thread
    Private _isRunning As Boolean
    Private ReadOnly _sseClients As New List(Of HttpListenerResponse)
    Private ReadOnly _downloadLocks As New Dictionary(Of String, Object)
    Private ReadOnly _downloadingVideos As New HashSet(Of String)

    Public Sub New(queueManager As QueueManager, youtube As YouTubeService, cacheFolder As String, Optional port As Integer = 5847)
        _queueManager = queueManager
        _youtube = youtube
        _cacheFolder = cacheFolder
        _port = port
        _listener = New HttpListener()
        _listener.Prefixes.Add($"http://localhost:{_port}/")
    End Sub

    ''' <summary>
    ''' Start the web server
    ''' </summary>
    Public Sub Start()
        If _isRunning Then Return

        Try
            _listener.Start()
            _isRunning = True

            _listenerThread = New Thread(AddressOf ListenLoop)
            _listenerThread.IsBackground = True
            _listenerThread.Start()

            Console.WriteLine($"[WebServer] Started successfully on http://localhost:{_port}")
            Console.WriteLine($"[WebServer] Open this URL in your browser: http://localhost:{_port}")
        Catch ex As HttpListenerException When ex.ErrorCode = 5
            Console.WriteLine($"[WebServer] ERROR: Access Denied (Error {ex.ErrorCode})")
            Console.WriteLine("[WebServer] HttpListener requires administrator privileges on Windows.")
            Console.WriteLine("[WebServer] Please run the bot as administrator OR run this command in an admin PowerShell:")
            Console.WriteLine($"[WebServer] netsh http add urlacl url=http://localhost:{_port}/ user=Everyone")
            Throw
        Catch ex As Exception
            Console.WriteLine($"[WebServer] Failed to start: {ex.Message}")
            Console.WriteLine($"[WebServer] Exception Type: {ex.GetType().Name}")
            If TypeOf ex Is HttpListenerException Then
                Console.WriteLine($"[WebServer] Error Code: {CType(ex, HttpListenerException).ErrorCode}")
            End If
            Throw
        End Try
    End Sub

    ''' <summary>
    ''' Stop the web server
    ''' </summary>
    Public Sub [Stop]()
        If Not _isRunning Then Return

        _isRunning = False
        _listener.Stop()
        _listener.Close()

        Console.WriteLine("[WebServer] Stopped")
    End Sub

    ''' <summary>
    ''' Main listener loop
    ''' </summary>
    Private Sub ListenLoop()
        While _isRunning
            Try
                Dim context = _listener.GetContext()
                ThreadPool.QueueUserWorkItem(AddressOf HandleRequest, context)
            Catch ex As HttpListenerException
                ' Listener was stopped
                If Not _isRunning Then Exit While
            Catch ex As Exception
                Console.WriteLine($"[WebServer] Error: {ex.Message}")
            End Try
        End While
    End Sub

    ''' <summary>
    ''' Handle incoming HTTP request
    ''' </summary>
    Private Sub HandleRequest(state As Object)
        Dim context = CType(state, HttpListenerContext)
        Dim request = context.Request
        Dim response = context.Response
        Dim closeResponse = True

        Try
            Dim path = request.Url.AbsolutePath

            Select Case path
                Case "/"
                    ServePage(response)
                Case "/api/queue"
                    ServeQueueAPI(response).Wait()
                Case "/api/events"
                    closeResponse = False ' Don't close SSE streams
                    ServeSSE(context)
                    Return
                Case "/api/notify"
                    NotifyClients()
                    response.StatusCode = 200
                    Dim okBytes = Encoding.UTF8.GetBytes("OK")
                    response.OutputStream.Write(okBytes, 0, okBytes.Length)
                Case "/api/skip"
                    If request.HttpMethod = "POST" Then
                        _queueManager.SkipCurrentSong()
                        response.StatusCode = 200
                        Dim okBytes = Encoding.UTF8.GetBytes("OK")
                        response.OutputStream.Write(okBytes, 0, okBytes.Length)
                    Else
                        response.StatusCode = 405
                        Dim errorBytes = Encoding.UTF8.GetBytes("Method Not Allowed")
                        response.OutputStream.Write(errorBytes, 0, errorBytes.Length)
                    End If
                Case Else
                    ' Handle audio file requests
                    If path.StartsWith("/audio/") Then
                        ServeAudioFile(response, path)
                    Else
                        response.StatusCode = 404
                        Dim errorBytes = Encoding.UTF8.GetBytes("Not Found")
                        response.OutputStream.Write(errorBytes, 0, errorBytes.Length)
                    End If
            End Select

        Catch ex As Exception
            Console.WriteLine($"[WebServer] Request error: {ex.Message}")
            response.StatusCode = 500
        Finally
            If closeResponse Then
                Try
                    response.OutputStream.Close()
                Catch
                    ' Stream may already be closed, ignore
                End Try
            End If
        End Try
    End Sub

    ''' <summary>
    ''' Serve the main HTML page
    ''' </summary>
    Private Sub ServePage(response As HttpListenerResponse)
        Dim html = WebUI.GetHTML()
        Dim buffer = Encoding.UTF8.GetBytes(html)

        response.ContentType = "text/html"
        response.ContentLength64 = buffer.Length
        response.OutputStream.Write(buffer, 0, buffer.Length)
    End Sub

    ''' <summary>
    ''' Serve the queue API endpoint
    ''' </summary>
    Private Async Function ServeQueueAPI(response As HttpListenerResponse) As Task
        Dim queue = _queueManager.GetQueue()
        Dim nowPlaying = _queueManager.GetNowPlaying()

        ' Format queue data for JSON
        Dim queueData = queue.Select(Function(song) New With {
            .id = song.Id,
            .title = song.Title,
            .artist = song.Artist,
            .duration = FormatDuration(song.DurationSeconds),
            .url = song.OriginalUrl,
            .videoId = song.YoutubeVideoId,
            .artwork = song.ArtworkUrl,
            .requestedBy = song.RequestedBy,
            .requestedAt = FormatTimeAgo(song.RequestedAt)
        }).ToList()

        ' Download and cache audio for now playing song
        Dim nowPlayingData As Object = Nothing
        If nowPlaying IsNot Nothing Then
            Dim audioUrl As String = Nothing
            If Not String.IsNullOrEmpty(nowPlaying.YoutubeVideoId) Then
                audioUrl = Await EnsureAudioDownloadedAsync(nowPlaying.YoutubeVideoId)
            End If

            nowPlayingData = New With {
                .title = nowPlaying.Title,
                .artist = nowPlaying.Artist,
                .url = nowPlaying.OriginalUrl,
                .videoId = nowPlaying.YoutubeVideoId,
                .audioUrl = audioUrl,
                .artwork = nowPlaying.ArtworkUrl
            }
        End If

        ' Build response object
        Dim responseData = New With {
            .queue = queueData,
            .nowPlaying = nowPlayingData,
            .totalPlayed = _queueManager.GetTotalPlayed(),
            .totalRequests = _queueManager.GetTotalRequests()
        }

        Dim json = JsonConvert.SerializeObject(responseData)
        Dim buffer = Encoding.UTF8.GetBytes(json)

        response.ContentType = "application/json"
        response.Headers.Add("Access-Control-Allow-Origin", "*")
        response.StatusCode = 200
        response.ContentLength64 = buffer.Length
        response.OutputStream.Write(buffer, 0, buffer.Length)
    End Function

    ''' <summary>
    ''' Format duration in seconds to MM:SS
    ''' </summary>
    Private Function FormatDuration(seconds As Integer) As String
        Dim ts = TimeSpan.FromSeconds(seconds)
        Return $"{CInt(ts.TotalMinutes):D2}:{ts.Seconds:D2}"
    End Function

    ''' <summary>
    ''' Format datetime to relative time (e.g., "2 minutes ago")
    ''' </summary>
    Private Function FormatTimeAgo(dt As DateTime) As String
        Dim diff = DateTime.Now - dt
        If diff.TotalMinutes < 1 Then
            Return "Just now"
        ElseIf diff.TotalMinutes < 60 Then
            Return $"{CInt(diff.TotalMinutes)} minute{If(CInt(diff.TotalMinutes) = 1, "", "s")} ago"
        ElseIf diff.TotalHours < 24 Then
            Return $"{CInt(diff.TotalHours)} hour{If(CInt(diff.TotalHours) = 1, "", "s")} ago"
        Else
            Return dt.ToString("MMM d, h:mm tt")
        End If
    End Function

    Public ReadOnly Property Port As Integer
        Get
            Return _port
        End Get
    End Property

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return _isRunning
        End Get
    End Property

    ''' <summary>
    ''' Ensure audio is downloaded for a video, handling concurrent requests
    ''' </summary>
    Private Async Function EnsureAudioDownloadedAsync(videoId As String) As Task(Of String)
        Try
            Dim cachedFilePath = Path.Combine(_cacheFolder, $"{videoId}.webm")

            ' Get or create a lock object for this video
            Dim lockObj As Object
            SyncLock _downloadLocks
                If Not _downloadLocks.ContainsKey(videoId) Then
                    _downloadLocks(videoId) = New Object()
                End If
                lockObj = _downloadLocks(videoId)
            End SyncLock

            ' Use lock to prevent concurrent downloads of same video
            Dim shouldDownload = False
            SyncLock lockObj
                If File.Exists(cachedFilePath) Then
                    ' File already exists
                    Return $"http://localhost:{_port}/audio/{videoId}"
                ElseIf _downloadingVideos.Contains(videoId) Then
                    ' Another thread is downloading, wait for it
                    Console.WriteLine($"[WebServer] Waiting for ongoing download: {videoId}")
                Else
                    ' Mark as downloading and proceed
                    _downloadingVideos.Add(videoId)
                    shouldDownload = True
                End If
            End SyncLock

            ' If another thread is downloading, wait and check
            If Not shouldDownload Then
                ' Wait up to 30 seconds for download to complete
                For i As Integer = 0 To 60
                    Await Task.Delay(500)
                    If File.Exists(cachedFilePath) Then
                        Return $"http://localhost:{_port}/audio/{videoId}"
                    End If
                Next
                Console.WriteLine($"[WebServer] Timeout waiting for download: {videoId}")
                Return Nothing
            End If

            ' Download the file to a temp location first
            Try
                Dim tempFilePath = cachedFilePath & ".tmp"

                ' Delete temp file if it exists from previous failed attempt
                If File.Exists(tempFilePath) Then
                    File.Delete(tempFilePath)
                End If

                Dim success = Await _youtube.DownloadAudioAsync(videoId, tempFilePath)
                If success AndAlso File.Exists(tempFilePath) Then
                    ' Atomically move temp file to final location
                    File.Move(tempFilePath, cachedFilePath, overwrite:=True)
                    Console.WriteLine($"[WebServer] Audio ready: {videoId}")
                    Return $"http://localhost:{_port}/audio/{videoId}"
                Else
                    ' Download failed - remove song from queue
                    Console.WriteLine($"[WebServer] Failed to download audio for video: {videoId} - removing from queue")
                    _queueManager.RemoveSongByVideoId(videoId)
                    Return Nothing
                End If
            Finally
                ' Remove from downloading set
                SyncLock lockObj
                    _downloadingVideos.Remove(videoId)
                End SyncLock
            End Try

        Catch ex As Exception
            Console.WriteLine($"[WebServer] Failed to prepare audio: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Clean up cached audio files
    ''' </summary>
    Public Sub CleanupCache()
        Try
            If Not Directory.Exists(_cacheFolder) Then Return

            Dim files = Directory.GetFiles(_cacheFolder, "*.webm")
            For Each filePath In files
                Try
                    File.Delete(filePath)
                    Console.WriteLine($"[WebServer] Deleted cached file: {Path.GetFileName(filePath)}")
                Catch ex As Exception
                    Console.WriteLine($"[WebServer] Could not delete {Path.GetFileName(filePath)}: {ex.Message}")
                End Try
            Next
        Catch ex As Exception
            Console.WriteLine($"[WebServer] Error cleaning cache: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Delete a specific cached audio file
    ''' </summary>
    Public Sub DeleteCachedFile(videoId As String)
        Try
            Dim filePath = Path.Combine(_cacheFolder, $"{videoId}.webm")
            If File.Exists(filePath) Then
                File.Delete(filePath)
                Console.WriteLine($"[WebServer] Deleted cached file for video: {videoId}")
            End If
        Catch ex As Exception
            Console.WriteLine($"[WebServer] Could not delete cached file for {videoId}: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Serve cached audio file
    ''' </summary>
    Private Sub ServeAudioFile(response As HttpListenerResponse, urlPath As String)
        Try
            ' Extract video ID from path (/audio/VIDEO_ID)
            Dim videoId = urlPath.Replace("/audio/", "")
            Dim filePath = Path.Combine(_cacheFolder, $"{videoId}.webm")

            If Not File.Exists(filePath) Then
                response.StatusCode = 404
                Dim errorBytes = Encoding.UTF8.GetBytes("Audio file not found")
                response.OutputStream.Write(errorBytes, 0, errorBytes.Length)
                Return
            End If

            ' Serve the audio file
            response.ContentType = "audio/webm"
            response.StatusCode = 200
            response.Headers.Add("Access-Control-Allow-Origin", "*")

            Using fileStream = New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
                fileStream.CopyTo(response.OutputStream)
            End Using

            Console.WriteLine($"[WebServer] Served audio file: {videoId}")
        Catch ex As Exception
            Console.WriteLine($"[WebServer] Error serving audio file: {ex.Message}")
            response.StatusCode = 500
        End Try
    End Sub

    ''' <summary>
    ''' Serve Server-Sent Events stream
    ''' </summary>
    Private Sub ServeSSE(context As HttpListenerContext)
        Dim response = context.Response
        response.ContentType = "text/event-stream"
        response.Headers.Add("Cache-Control", "no-cache")
        response.Headers.Add("Connection", "keep-alive")
        response.Headers.Add("Access-Control-Allow-Origin", "*")

        SyncLock _sseClients
            _sseClients.Add(response)
        End SyncLock

        Try
            ' Send initial connection confirmation
            Dim initialMessage = ": connected" & vbLf & vbLf
            Dim initialBytes = Encoding.UTF8.GetBytes(initialMessage)
            response.OutputStream.Write(initialBytes, 0, initialBytes.Length)
            response.OutputStream.Flush()

            ' Keep the connection alive with periodic heartbeat
            Dim heartbeatCount = 0
            While _isRunning
                Thread.Sleep(15000) ' Send heartbeat every 15 seconds
                heartbeatCount += 1

                ' Send heartbeat comment to keep connection alive
                Dim heartbeat = $": heartbeat {heartbeatCount}" & vbLf & vbLf
                Dim heartbeatBytes = Encoding.UTF8.GetBytes(heartbeat)
                response.OutputStream.Write(heartbeatBytes, 0, heartbeatBytes.Length)
                response.OutputStream.Flush()
            End While
        Catch
            ' Client disconnected
        Finally
            SyncLock _sseClients
                _sseClients.Remove(response)
            End SyncLock
        End Try
    End Sub

    ''' <summary>
    ''' Notify all connected SSE clients to refresh
    ''' </summary>
    Public Sub NotifyClients()
        SyncLock _sseClients
            Dim disconnected As New List(Of HttpListenerResponse)

            For Each client In _sseClients
                Try
                    Dim message = "data: refresh" & vbLf & vbLf
                    Dim bytes = Encoding.UTF8.GetBytes(message)
                    client.OutputStream.Write(bytes, 0, bytes.Length)
                    client.OutputStream.Flush()
                Catch
                    disconnected.Add(client)
                End Try
            Next

            ' Remove disconnected clients
            For Each client In disconnected
                _sseClients.Remove(client)
            Next
        End SyncLock
    End Sub
End Class
