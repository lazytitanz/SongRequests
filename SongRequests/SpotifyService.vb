Imports System.Net.Http
Imports System.Text
Imports System.Text.RegularExpressions
Imports Newtonsoft.Json.Linq

''' <summary>
''' Handles Spotify API integration for fetching track information
''' </summary>
Public Class SpotifyService
    Private ReadOnly _httpClient As HttpClient
    Private ReadOnly _clientId As String
    Private ReadOnly _clientSecret As String
    Private _accessToken As String
    Private _tokenExpiry As DateTime

    ' Regex to match Spotify track URLs
    Private Shared ReadOnly SpotifyTrackRegex As New Regex(
        "^https?://open\.spotify\.com/track/([a-zA-Z0-9]+)",
        RegexOptions.Compiled Or RegexOptions.IgnoreCase)

    Public Sub New(clientId As String, clientSecret As String)
        _httpClient = New HttpClient()
        _clientId = clientId
        _clientSecret = clientSecret
        _tokenExpiry = DateTime.MinValue
    End Sub

    ''' <summary>
    ''' Check if a URL is a valid Spotify track URL
    ''' </summary>
    Public Shared Function IsSpotifyUrl(url As String) As Boolean
        Return SpotifyTrackRegex.IsMatch(url)
    End Function

    ''' <summary>
    ''' Extract track ID from Spotify URL
    ''' </summary>
    Public Shared Function ExtractTrackId(url As String) As String
        Dim match = SpotifyTrackRegex.Match(url)
        If match.Success Then
            Return match.Groups(1).Value
        End If
        Return Nothing
    End Function

    ''' <summary>
    ''' Get access token from Spotify API
    ''' </summary>
    Private Async Function GetAccessTokenAsync() As Task(Of String)
        ' Return cached token if still valid
        If Not String.IsNullOrEmpty(_accessToken) AndAlso DateTime.Now < _tokenExpiry Then
            Return _accessToken
        End If

        ' Request new token
        Dim authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"))

        Dim request As New HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        request.Headers.Add("Authorization", $"Basic {authString}")
        request.Content = New FormUrlEncodedContent(New Dictionary(Of String, String) From {
            {"grant_type", "client_credentials"}
        })

        Dim response = Await _httpClient.SendAsync(request)
        response.EnsureSuccessStatusCode()

        Dim json = Await response.Content.ReadAsStringAsync()
        Dim obj = JObject.Parse(json)

        _accessToken = obj("access_token").ToString()
        Dim expiresIn = CInt(obj("expires_in"))
        _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60) ' Refresh 1 min early

        Return _accessToken
    End Function

    ''' <summary>
    ''' Get track information from Spotify
    ''' </summary>
    Public Async Function GetTrackInfoAsync(trackId As String) As Task(Of SpotifyTrackInfo)
        Dim token = Await GetAccessTokenAsync()

        Dim request As New HttpRequestMessage(HttpMethod.Get, $"https://api.spotify.com/v1/tracks/{trackId}")
        request.Headers.Add("Authorization", $"Bearer {token}")

        Dim response = Await _httpClient.SendAsync(request)
        response.EnsureSuccessStatusCode()

        Dim json = Await response.Content.ReadAsStringAsync()
        Dim track = JObject.Parse(json)

        Return New SpotifyTrackInfo With {
            .TrackId = trackId,
            .Name = track("name").ToString(),
            .Artist = track("artists")(0)("name").ToString(),
            .DurationMs = CInt(track("duration_ms")),
            .AlbumArt = If(track("album")("images").HasValues,
                          track("album")("images")(0)("url").ToString(),
                          Nothing)
        }
    End Function

    Public Sub Dispose()
        _httpClient?.Dispose()
    End Sub
End Class

''' <summary>
''' Represents Spotify track information
''' </summary>
Public Class SpotifyTrackInfo
    Public Property TrackId As String
    Public Property Name As String
    Public Property Artist As String
    Public Property DurationMs As Integer
    Public Property AlbumArt As String

    Public ReadOnly Property DurationSeconds As Integer
        Get
            Return CInt(Math.Ceiling(DurationMs / 1000.0))
        End Get
    End Property

    Public ReadOnly Property SearchQuery As String
        Get
            Return $"{Artist} - {Name}"
        End Get
    End Property
End Class
