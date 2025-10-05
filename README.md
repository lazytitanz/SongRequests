# SongRequests Plugin

A TwitchChatBot plugin for managing song requests via Spotify and YouTube with a live web UI.

## Features

- 🎵 **Dual Platform Support** - Accept song requests from both Spotify and YouTube URLs
- 🔍 **Smart Search** - Automatically searches YouTube for Spotify tracks
- 📊 **Live Web UI** - Real-time queue display at `http://localhost:5847`
- 💾 **Persistent Storage** - SQLite database tracks all requests and playback history
- 🎨 **Artwork Display** - Shows album/video artwork in the web interface
- ⚡ **Server-Sent Events** - Live updates without page refresh
- 🔒 **Permission System** - Moderator-only skip and clear commands
- 📁 **Audio Caching** - Downloads and caches audio files to avoid 403 errors

## Commands

| Command | Permission | Description |
|---------|-----------|-------------|
| `!sr <url or search>` | Everyone | Request a song from Spotify/YouTube URL or search query |
| `!queue` | Everyone | Display the current song queue |
| `!nowplaying` / `!np` | Everyone | Show currently playing song |
| `!skip` | Moderator | Skip the current song |
| `!clearqueue` | Moderator | Clear the entire queue |

## Configuration

Create a `config.json` file in the plugin folder:

```json
{
  "spotify": {
    "clientId": "your_spotify_client_id",
    "clientSecret": "your_spotify_client_secret"
  },
  "webServer": {
    "port": 5847
  }
}
```

### Getting Spotify Credentials

1. Go to [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
2. Create a new app
3. Copy the **Client ID** and **Client Secret**

## Web UI

Access the live queue at `http://localhost:5847` (or your configured port).

Features:
- Real-time queue updates
- Now playing display with artwork
- Audio playback (auto-plays downloaded songs)
- Skip button for moderators
- Request statistics

## Architecture

### Components

- **SongRequestsPlugin** - Main plugin entry point, registers commands and services
- **QueueManager** - Coordinates song requests between services
- **SpotifyService** - Spotify Web API integration for track metadata
- **YouTubeService** - YouTube search and audio download via YoutubeExplode
- **WebServer** - HTTP server for web UI and audio streaming
- **DatabaseHelper** - SQLite persistence layer

### Audio Caching System

To avoid YouTube 403 Forbidden errors, the plugin:

1. Downloads audio files via YoutubeExplode to `PluginData/SongRequests/cache/`
2. Uses atomic file operations (`.tmp` → `.webm`) to prevent partial file access
3. Serves cached audio through local web server at `/audio/{videoId}`
4. Implements download locking to prevent concurrent downloads
5. Auto-cleans cache when songs are skipped or queue is cleared
6. Removes failed downloads from queue automatically

### Database Schema

**songs** table:
- `id` - Primary key
- `requester` - Username who requested
- `original_url` - Original Spotify/YouTube URL
- `youtube_url` - Resolved YouTube URL
- `youtube_video_id` - YouTube video ID
- `song_name` - Track title
- `artist_name` - Artist name
- `duration_seconds` - Duration
- `artwork_url` - Album/video artwork URL
- `requested_at` - Timestamp

**queue** table:
- `id` - Primary key
- `song_id` - Foreign key to songs table
- `position` - Queue position
- `status` - `pending` | `playing` | `completed`

## Plugin Manifest

Declared capabilities in `plugin.json`:
- `network` - HTTP requests to Spotify/YouTube APIs
- `disk` - SQLite database and audio cache storage

Allowed external APIs:
- `YoutubeExplode` - YouTube video/audio access
- `AngleSharp` - HTML parsing for metadata
- `Newtonsoft` - JSON serialization

## Dependencies

- **YoutubeExplode** 6.5.5 - YouTube integration
- **Newtonsoft.Json** 13.0.3 - JSON handling
- **System.Data.SQLite** 1.0.119 - Database
- **AngleSharp** 1.1.2 - HTML parsing

## Build

```bash
dotnet build SongRequests.vbproj
```

The build process automatically copies the plugin and dependencies to the TwitchChatBot's `Plugins/SongRequests` folder.

## Development Notes

### Handling YouTube Rate Limits

If YouTube returns 403 Forbidden errors frequently:
- Songs are automatically removed from queue
- Consider implementing retry logic with exponential backoff
- Monitor YoutubeExplode for library updates

### Extending Functionality

To add new commands, create a class implementing `ICommand`:

```vb
Public Class MyCommand
    Implements ICommand

    Public ReadOnly Property RequiredRole As Role = Role.Everyone
    Public ReadOnly Property UserCooldownSeconds As Integer = 10
    Public ReadOnly Property GlobalCooldownSeconds As Integer = 5

    Public Sub Execute(client As TwitchClient, e As OnMessageReceivedArgs, args As String())
        ' Your command logic
    End Sub
End Class
```

Then register in `SongRequestsPlugin.Initialize()`:

```vb
sdk.RegisterCommand("mycommand", New MyCommand(queueManager))
```

## License

Part of the TwitchChatBot plugin ecosystem.
