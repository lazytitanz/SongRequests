Imports System.Net
Imports System.Net.Http
Imports System.Text.RegularExpressions
Imports YoutubeExplode
Imports YoutubeExplode.Search

''' <summary>
''' Handles YouTube video search and URL validation using YouTubeExplode
''' </summary>
Public Class YouTubeService
    Private ReadOnly _youtube As YoutubeClient

    ' Regex to match YouTube URLs
    Private Shared ReadOnly YouTubeRegex As New Regex(
        "^https?://(?:www\.)?(?:youtube\.com/watch\?v=|youtu\.be/)([a-zA-Z0-9_-]{11})",
        RegexOptions.Compiled Or RegexOptions.IgnoreCase)

    Public Sub New()
        ' Create HttpClient with Firefox on Windows 11 user agent
        Dim httpClient As New HttpClient()
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:134.0) Gecko/20100101 Firefox/134.0")

        _youtube = New YoutubeClient(httpClient)
    End Sub

    ''' <summary>
    ''' Check if a URL is a valid YouTube URL
    ''' </summary>
    Public Shared Function IsYouTubeUrl(url As String) As Boolean
        Return YouTubeRegex.IsMatch(url)
    End Function

    ''' <summary>
    ''' Extract video ID from YouTube URL
    ''' </summary>
    Public Shared Function ExtractVideoId(url As String) As String
        Dim match = YouTubeRegex.Match(url)
        If match.Success Then
            Return match.Groups(1).Value
        End If
        Return Nothing
    End Function

    ''' <summary>
    ''' Get YouTube video information from a direct URL
    ''' </summary>
    Public Async Function GetVideoInfoAsync(url As String) As Task(Of YouTubeVideoInfo)
        Try
            Dim videoId = ExtractVideoId(url)
            If String.IsNullOrEmpty(videoId) Then
                Console.WriteLine("[YouTubeService] Invalid YouTube URL - could not extract video ID")
                Return Nothing
            End If

            Dim video = Await _youtube.Videos.GetAsync(videoId)

            Dim thumbnailUrl As String = Nothing
            If video.Thumbnails.Count > 0 Then
                thumbnailUrl = video.Thumbnails(0).Url
            End If

            Return New YouTubeVideoInfo With {
                .VideoId = videoId,
                .Title = video.Title,
                .Author = video.Author.ChannelTitle,
                .Duration = video.Duration.GetValueOrDefault(),
                .ThumbnailUrl = thumbnailUrl,
                .Url = $"https://www.youtube.com/watch?v={videoId}"
            }
        Catch ex As Exception
            Console.WriteLine($"[YouTubeService] Error getting video info for URL '{url}': {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Search YouTube for a video and return the best match
    ''' </summary>
    Public Async Function SearchVideoAsync(query As String) As Task(Of YouTubeVideoInfo)
        Try
            Console.WriteLine($"[YouTubeService] Searching YouTube for: '{query}'")
            Dim searchResults = _youtube.Search.GetVideosAsync(query)

            ' Get first result
            Dim enumerator = searchResults.GetAsyncEnumerator()
            If Await enumerator.MoveNextAsync() Then
                Dim video = enumerator.Current

                Dim thumbnailUrl As String = Nothing
                If video.Thumbnails.Count > 0 Then
                    thumbnailUrl = video.Thumbnails(0).Url
                End If

                Dim result = New YouTubeVideoInfo With {
                    .VideoId = video.Id.Value,
                    .Title = video.Title,
                    .Author = video.Author.ChannelTitle,
                    .Duration = video.Duration.GetValueOrDefault(),
                    .ThumbnailUrl = thumbnailUrl,
                    .Url = $"https://www.youtube.com/watch?v={video.Id.Value}"
                }

                Console.WriteLine($"[YouTubeService] Found: {result.Author} - {result.Title}")
                Await enumerator.DisposeAsync()
                Return result
            End If

            Await enumerator.DisposeAsync()
            Console.WriteLine($"[YouTubeService] No search results found for: '{query}'")
            Return Nothing
        Catch ex As Exception
            Console.WriteLine($"[YouTubeService] Error searching YouTube for '{query}': {ex.Message}")
            Console.WriteLine($"[YouTubeService] Exception type: {ex.GetType().Name}")
            If ex.InnerException IsNot Nothing Then
                Console.WriteLine($"[YouTubeService] Inner exception: {ex.InnerException.Message}")
            End If
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Get direct audio stream URL for a YouTube video
    ''' </summary>
    Public Async Function GetAudioStreamUrlAsync(videoId As String) As Task(Of String)
        Try
            Dim streamManifest = Await _youtube.Videos.Streams.GetManifestAsync(videoId)

            ' Get the best audio-only stream
            Dim audioStreams = streamManifest.GetAudioOnlyStreams()
            Dim audioStreamInfo = audioStreams.OrderByDescending(Function(s) s.Bitrate.BitsPerSecond).FirstOrDefault()

            If audioStreamInfo IsNot Nothing Then
                Return audioStreamInfo.Url
            End If

            Return Nothing
        Catch ex As Exception
            Console.WriteLine($"[YouTubeService] Error getting audio URL: {ex.Message}")
            Return Nothing
        End Try
    End Function
End Class

''' <summary>
''' Represents YouTube video information
''' </summary>
Public Class YouTubeVideoInfo
    Public Property VideoId As String
    Public Property Title As String
    Public Property Author As String
    Public Property Duration As TimeSpan
    Public Property ThumbnailUrl As String
    Public Property Url As String

    Public ReadOnly Property DurationSeconds As Integer
        Get
            Return CInt(Duration.TotalSeconds)
        End Get
    End Property
End Class
