''' <summary>
''' Manages the song request queue and coordinates between services
''' </summary>
Public Class QueueManager
    Private ReadOnly _db As DatabaseHelper
    Private ReadOnly _spotify As SpotifyService
    Private ReadOnly _youtube As YouTubeService
    Private _webServer As WebServer

    Public Sub New(db As DatabaseHelper, spotify As SpotifyService, youtube As YouTubeService)
        _db = db
        _spotify = spotify
        _youtube = youtube
    End Sub

    Public Sub SetWebServer(webServer As WebServer)
        _webServer = webServer
    End Sub

    ''' <summary>
    ''' Process a song request URL (Spotify or YouTube)
    ''' </summary>
    Public Async Function ProcessRequestAsync(requester As String, url As String) As Task(Of RequestResult)
        Try
            ' Check if it's a Spotify URL
            If SpotifyService.IsSpotifyUrl(url) Then
                Return Await ProcessSpotifyRequestAsync(requester, url)
            ElseIf YouTubeService.IsYouTubeUrl(url) Then
                Return Await ProcessYouTubeRequestAsync(requester, url)
            Else
                Return New RequestResult With {
                    .Success = False,
                    .Message = "Invalid URL. Please provide a Spotify track or YouTube video URL."
                }
            End If
        Catch ex As Exception
            Return New RequestResult With {
                .Success = False,
                .Message = $"Error processing request: {ex.Message}"
            }
        End Try
    End Function

    ''' <summary>
    ''' Process a Spotify track request
    ''' </summary>
    Private Async Function ProcessSpotifyRequestAsync(requester As String, spotifyUrl As String) As Task(Of RequestResult)
        ' Extract track ID
        Dim trackId = SpotifyService.ExtractTrackId(spotifyUrl)
        If String.IsNullOrEmpty(trackId) Then
            Return New RequestResult With {
                .Success = False,
                .Message = "Invalid Spotify track URL."
            }
        End If

        ' Get Spotify track info
        Dim spotifyTrack = Await _spotify.GetTrackInfoAsync(trackId)
        If spotifyTrack Is Nothing Then
            Return New RequestResult With {
                .Success = False,
                .Message = "Could not fetch Spotify track information."
            }
        End If

        ' Search for matching YouTube video
        Dim youtubeVideo = Await _youtube.SearchVideoAsync(spotifyTrack.SearchQuery)
        If youtubeVideo Is Nothing Then
            Return New RequestResult With {
                .Success = False,
                .Message = "Could not find matching YouTube video."
            }
        End If

        ' Add to database
        Dim songId = _db.AddSongRequest(
            requester,
            spotifyUrl,
            youtubeVideo.Url,
            youtubeVideo.VideoId,
            spotifyTrack.Name,
            spotifyTrack.Artist,
            spotifyTrack.DurationSeconds,
            spotifyTrack.AlbumArt
        )

        ' Notify web clients
        _webServer?.NotifyClients()

        Return New RequestResult With {
            .Success = True,
            .Message = $"Added to queue: {spotifyTrack.Artist} - {spotifyTrack.Name}",
            .SongId = songId
        }
    End Function

    ''' <summary>
    ''' Process a YouTube video request
    ''' </summary>
    Private Async Function ProcessYouTubeRequestAsync(requester As String, youtubeUrl As String) As Task(Of RequestResult)
        ' Get YouTube video info
        Dim youtubeVideo = Await _youtube.GetVideoInfoAsync(youtubeUrl)
        If youtubeVideo Is Nothing Then
            Return New RequestResult With {
                .Success = False,
                .Message = "Invalid YouTube URL or could not fetch video information."
            }
        End If

        ' Add to database
        Dim songId = _db.AddSongRequest(
            requester,
            youtubeUrl,
            youtubeVideo.Url,
            youtubeVideo.VideoId,
            youtubeVideo.Title,
            youtubeVideo.Author,
            youtubeVideo.DurationSeconds,
            youtubeVideo.ThumbnailUrl
        )

        ' Notify web clients
        _webServer?.NotifyClients()

        Return New RequestResult With {
            .Success = True,
            .Message = $"Added to queue: {youtubeVideo.Title}",
            .SongId = songId
        }
    End Function

    ''' <summary>
    ''' Get the current song in the queue
    ''' </summary>
    Public Function GetCurrentSong() As SongInfo
        Return _db.GetCurrentSong()
    End Function

    ''' <summary>
    ''' Get all songs in the queue
    ''' </summary>
    Public Function GetQueue() As List(Of SongInfo)
        Return _db.GetQueue()
    End Function

    ''' <summary>
    ''' Skip the current song
    ''' </summary>
    Public Sub SkipCurrentSong()
        ' Get current song before completing it
        Dim currentSong = _db.GetCurrentSong()

        _db.CompleteCurrentSong()

        ' Clean up cached audio file for the skipped song
        If currentSong IsNot Nothing AndAlso Not String.IsNullOrEmpty(currentSong.YoutubeVideoId) Then
            _webServer?.DeleteCachedFile(currentSong.YoutubeVideoId)
        End If

        ' Notify web clients
        _webServer?.NotifyClients()
    End Sub

    ''' <summary>
    ''' Clear the entire queue
    ''' </summary>
    Public Sub ClearQueue()
        _db.ClearQueue()

        ' Clean up all cached audio files
        _webServer?.CleanupCache()

        ' Notify web clients
        _webServer?.NotifyClients()
    End Sub

    ''' <summary>
    ''' Remove a song from the queue by video ID (used when download fails)
    ''' </summary>
    Public Sub RemoveSongByVideoId(videoId As String)
        _db.RemoveSongByVideoId(videoId)

        ' Clean up cached file if it exists
        _webServer?.DeleteCachedFile(videoId)

        ' Notify web clients
        _webServer?.NotifyClients()
    End Sub

    ''' <summary>
    ''' Get the currently playing song
    ''' </summary>
    Public Function GetNowPlaying() As SongInfo
        Return GetCurrentSong()
    End Function

    ''' <summary>
    ''' Get total number of played songs
    ''' </summary>
    Public Function GetTotalPlayed() As Integer
        Return _db.GetTotalPlayedCount()
    End Function

    ''' <summary>
    ''' Get total number of song requests ever made
    ''' </summary>
    Public Function GetTotalRequests() As Integer
        Return _db.GetTotalRequestCount()
    End Function
End Class

''' <summary>
''' Result of processing a song request
''' </summary>
Public Class RequestResult
    Public Property Success As Boolean
    Public Property Message As String
    Public Property SongId As Long?
End Class
