Imports TwitchChatBot
Imports System.IO
Imports System.Text.Json

''' <summary>
''' Song Requests Plugin - Handles song requests from Spotify and YouTube
''' Manages a queue, searches for songs, and stores them in SQLite database
''' </summary>
Public Class SongRequestsPlugin
    Implements IBotPlugin

    Public ReadOnly Property Name As String Implements IBotPlugin.Name
        Get
            Return "SongRequests"
        End Get
    End Property

    Public ReadOnly Property Version As String Implements IBotPlugin.Version
        Get
            Return "1.0.0"
        End Get
    End Property

    Public ReadOnly Property Author As String Implements IBotPlugin.Author
        Get
            Return "SilentNades"
        End Get
    End Property

    Private sdk As BotSDK
    Private db As DatabaseHelper
    Private spotify As SpotifyService
    Private youtube As YouTubeService
    Private queueManager As QueueManager
    Private webServer As WebServer

    Public Sub Initialize(sdk As BotSDK) Implements IBotPlugin.Initialize
        Me.sdk = sdk

        Try
            ' Create plugin data folder
            Dim pluginDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PluginData", "SongRequests")

            ' Load configuration
            Dim config = LoadConfig()
            Dim spotifyClientId = config.RootElement.GetProperty("spotify").GetProperty("clientId").GetString()
            Dim spotifyClientSecret = config.RootElement.GetProperty("spotify").GetProperty("clientSecret").GetString()
            Dim webServerPort = config.RootElement.GetProperty("webServer").GetProperty("port").GetInt32()

            ' Initialize services
            db = New DatabaseHelper(pluginDataFolder)
            spotify = New SpotifyService(spotifyClientId, spotifyClientSecret)
            youtube = New YouTubeService()
            queueManager = New QueueManager(db, spotify, youtube)

            ' Register commands
            sdk.RegisterCommand("sr", New SongRequestCommand(queueManager))
            sdk.RegisterCommand("queue", New QueueCommand(queueManager))
            sdk.RegisterCommand("nowplaying", New NowPlayingCommand(queueManager))
            sdk.RegisterCommand("np", New NowPlayingCommand(queueManager))
            sdk.RegisterCommand("skip", New SkipCommand(queueManager))
            sdk.RegisterCommand("clearqueue", New ClearQueueCommand(queueManager))

            ' Start web server
            webServer = New WebServer(queueManager, youtube, webServerPort)
            queueManager.SetWebServer(webServer)
            webServer.Start()

            sdk.LogInfo(Name, "Song Requests plugin initialized successfully!")
            sdk.LogInfo(Name, "Commands: !sr, !queue, !nowplaying (!np), !skip (mod), !clearqueue (mod)")
            sdk.LogInfo(Name, $"Web UI: http://localhost:{webServer.Port}")
        Catch ex As Exception
            sdk.LogError(Name, $"Failed to initialize: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Load configuration from config.json file
    ''' </summary>
    Private Function LoadConfig() As JsonDocument
        Dim configPath = Path.Combine(sdk.PluginFolder, "config.json")

        If Not File.Exists(configPath) Then
            Throw New FileNotFoundException($"Configuration file not found: {configPath}")
        End If

        Dim jsonContent = File.ReadAllText(configPath)
        Return JsonDocument.Parse(jsonContent)
    End Function

    Public Sub Shutdown() Implements IBotPlugin.Shutdown
        sdk.LogInfo(Name, "SongRequests plugin shutting down...")

        ' Clean up resources
        Try
            webServer?.Stop()
            spotify?.Dispose()
        Catch ex As Exception
            sdk.LogError(Name, $"Error during shutdown: {ex.Message}")
        End Try
    End Sub

End Class
