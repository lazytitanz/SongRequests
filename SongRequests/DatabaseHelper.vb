Imports System.Data.SQLite
Imports System.IO
Imports TwitchChatBot

''' <summary>
''' Manages SQLite database operations for song requests
''' </summary>
Public Class DatabaseHelper
    Private ReadOnly _connectionString As String
    Private ReadOnly _dbPath As String
    Private ReadOnly _sdk As BotSDK

    Public Sub New(pluginDataFolder As String, sdk As BotSDK)
        _sdk = sdk

        ' Create SongRequests folder if it doesn't exist
        If Not Directory.Exists(pluginDataFolder) Then
            Directory.CreateDirectory(pluginDataFolder)
        End If

        _dbPath = Path.Combine(pluginDataFolder, "songrequests.db")
        _connectionString = $"Data Source={_dbPath};Version=3;"

        InitializeDatabase()
    End Sub

    ''' <summary>
    ''' Initialize database tables if they don't exist
    ''' </summary>
    Private Sub InitializeDatabase()
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()

            ' Create songs table
            Dim createSongsTable As String = "
                CREATE TABLE IF NOT EXISTS songs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    requester TEXT NOT NULL,
                    original_url TEXT NOT NULL,
                    youtube_url TEXT,
                    youtube_video_id TEXT,
                    song_name TEXT,
                    artist_name TEXT,
                    duration_seconds INTEGER,
                    artwork_url TEXT,
                    requested_at DATETIME DEFAULT CURRENT_TIMESTAMP
                )"

            ' Create queue table
            Dim createQueueTable As String = "
                CREATE TABLE IF NOT EXISTS queue (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    song_id INTEGER NOT NULL,
                    position INTEGER NOT NULL,
                    status TEXT DEFAULT 'pending',
                    FOREIGN KEY (song_id) REFERENCES songs(id)
                )"

            Using cmd As New SQLiteCommand(createSongsTable, conn)
                cmd.ExecuteNonQuery()
            End Using

            Using cmd As New SQLiteCommand(createQueueTable, conn)
                cmd.ExecuteNonQuery()
            End Using

            ' Run migrations
            MigrateDatabase(conn)
        End Using
    End Sub

    ''' <summary>
    ''' Apply database migrations for schema updates
    ''' </summary>
    Private Sub MigrateDatabase(conn As SQLiteConnection)
        ' Check if youtube_video_id column exists
        Dim checkColumn As String = "PRAGMA table_info(songs)"
        Dim hasVideoIdColumn As Boolean = False

        Using cmd As New SQLiteCommand(checkColumn, conn)
            Using reader = cmd.ExecuteReader()
                While reader.Read()
                    If reader.GetString(1) = "youtube_video_id" Then
                        hasVideoIdColumn = True
                        Exit While
                    End If
                End While
            End Using
        End Using

        ' Add youtube_video_id column if it doesn't exist
        If Not hasVideoIdColumn Then
            _sdk.LogInfo("DatabaseHelper", "Migrating database: Adding youtube_video_id column...")
            Dim addColumn As String = "ALTER TABLE songs ADD COLUMN youtube_video_id TEXT"
            Using cmd As New SQLiteCommand(addColumn, conn)
                cmd.ExecuteNonQuery()
            End Using
            _sdk.LogInfo("DatabaseHelper", "Migration complete")
        End If

        ' Remove audio_url column if it exists (SQLite doesn't support DROP COLUMN easily, so we'll ignore it)
    End Sub

    ''' <summary>
    ''' Check if a song URL is already in the pending queue
    ''' </summary>
    Public Function IsSongInQueue(originalUrl As String) As Boolean
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()

            Dim query As String = "
                SELECT COUNT(*) FROM songs s
                INNER JOIN queue q ON s.id = q.song_id
                WHERE s.original_url = @url AND q.status = 'pending'"

            Using cmd As New SQLiteCommand(query, conn)
                cmd.Parameters.AddWithValue("@url", originalUrl)
                Return CInt(cmd.ExecuteScalar()) > 0
            End Using
        End Using
    End Function

    ''' <summary>
    ''' Add a song to the database and queue
    ''' </summary>
    Public Function AddSongRequest(requester As String, originalUrl As String, youtubeUrl As String,
                                    youtubeVideoId As String, songName As String, artistName As String,
                                    durationSeconds As Integer, Optional artworkUrl As String = Nothing) As Long
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()

            Using trans = conn.BeginTransaction()
                Try
                    ' Check for duplicates in pending queue
                    Dim checkDupe As String = "
                        SELECT COUNT(*) FROM songs s
                        INNER JOIN queue q ON s.id = q.song_id
                        WHERE s.original_url = @url AND q.status = 'pending'"

                    Using cmd As New SQLiteCommand(checkDupe, conn, trans)
                        cmd.Parameters.AddWithValue("@url", originalUrl)
                        If CInt(cmd.ExecuteScalar()) > 0 Then
                            Throw New InvalidOperationException("That song is already in the queue")
                        End If
                    End Using

                    ' Insert into songs table
                    Dim insertSong As String = "
                        INSERT INTO songs (requester, original_url, youtube_url, youtube_video_id, song_name, artist_name, duration_seconds, artwork_url)
                        VALUES (@requester, @originalUrl, @youtubeUrl, @videoId, @songName, @artistName, @duration, @artwork)"

                    Dim songId As Long
                    Using cmd As New SQLiteCommand(insertSong, conn, trans)
                        cmd.Parameters.AddWithValue("@requester", requester)
                        cmd.Parameters.AddWithValue("@originalUrl", originalUrl)
                        cmd.Parameters.AddWithValue("@youtubeUrl", If(youtubeUrl, String.Empty))
                        cmd.Parameters.AddWithValue("@videoId", If(youtubeVideoId, String.Empty))
                        cmd.Parameters.AddWithValue("@songName", If(songName, String.Empty))
                        cmd.Parameters.AddWithValue("@artistName", If(artistName, String.Empty))
                        cmd.Parameters.AddWithValue("@duration", durationSeconds)
                        cmd.Parameters.AddWithValue("@artwork", If(artworkUrl, String.Empty))
                        cmd.ExecuteNonQuery()

                        songId = conn.LastInsertRowId
                    End Using

                    ' Get next position in queue
                    Dim getMaxPos As String = "SELECT COALESCE(MAX(position), 0) FROM queue"
                    Dim nextPosition As Integer
                    Using cmd As New SQLiteCommand(getMaxPos, conn, trans)
                        nextPosition = CInt(cmd.ExecuteScalar()) + 1
                    End Using

                    ' Add to queue
                    Dim insertQueue As String = "INSERT INTO queue (song_id, position) VALUES (@songId, @position)"
                    Using cmd As New SQLiteCommand(insertQueue, conn, trans)
                        cmd.Parameters.AddWithValue("@songId", songId)
                        cmd.Parameters.AddWithValue("@position", nextPosition)
                        cmd.ExecuteNonQuery()
                    End Using

                    trans.Commit()
                    Return songId
                Catch ex As Exception
                    trans.Rollback()
                    Throw
                End Try
            End Using
        End Using
    End Function

    ''' <summary>
    ''' Get the current song (first in queue with pending status)
    ''' </summary>
    Public Function GetCurrentSong() As SongInfo
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()

            Dim query As String = "
                SELECT s.* FROM songs s
                INNER JOIN queue q ON s.id = q.song_id
                WHERE q.status = 'pending'
                ORDER BY q.position ASC
                LIMIT 1"

            Using cmd As New SQLiteCommand(query, conn)
                Using reader = cmd.ExecuteReader()
                    If reader.Read() Then
                        Return New SongInfo With {
                            .Id = reader.GetInt64(reader.GetOrdinal("id")),
                            .Requester = reader.GetString(reader.GetOrdinal("requester")),
                            .OriginalUrl = reader.GetString(reader.GetOrdinal("original_url")),
                            .YoutubeUrl = If(reader.IsDBNull(reader.GetOrdinal("youtube_url")), Nothing, reader.GetString(reader.GetOrdinal("youtube_url"))),
                            .YoutubeVideoId = If(reader.IsDBNull(reader.GetOrdinal("youtube_video_id")), Nothing, reader.GetString(reader.GetOrdinal("youtube_video_id"))),
                            .SongName = If(reader.IsDBNull(reader.GetOrdinal("song_name")), Nothing, reader.GetString(reader.GetOrdinal("song_name"))),
                            .ArtistName = If(reader.IsDBNull(reader.GetOrdinal("artist_name")), Nothing, reader.GetString(reader.GetOrdinal("artist_name"))),
                            .DurationSeconds = If(reader.IsDBNull(reader.GetOrdinal("duration_seconds")), 0, reader.GetInt32(reader.GetOrdinal("duration_seconds"))),
                            .ArtworkUrl = If(reader.IsDBNull(reader.GetOrdinal("artwork_url")), Nothing, reader.GetString(reader.GetOrdinal("artwork_url")))
                        }
                    End If
                End Using
            End Using
        End Using

        Return Nothing
    End Function

    ''' <summary>
    ''' Get all songs in the queue
    ''' </summary>
    Public Function GetQueue() As List(Of SongInfo)
        Dim songs As New List(Of SongInfo)

        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()

            Dim query As String = "
                SELECT s.* FROM songs s
                INNER JOIN queue q ON s.id = q.song_id
                WHERE q.status = 'pending'
                ORDER BY q.position ASC"

            Using cmd As New SQLiteCommand(query, conn)
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        songs.Add(New SongInfo With {
                            .Id = reader.GetInt64(reader.GetOrdinal("id")),
                            .Requester = reader.GetString(reader.GetOrdinal("requester")),
                            .OriginalUrl = reader.GetString(reader.GetOrdinal("original_url")),
                            .YoutubeUrl = If(reader.IsDBNull(reader.GetOrdinal("youtube_url")), Nothing, reader.GetString(reader.GetOrdinal("youtube_url"))),
                            .YoutubeVideoId = If(reader.IsDBNull(reader.GetOrdinal("youtube_video_id")), Nothing, reader.GetString(reader.GetOrdinal("youtube_video_id"))),
                            .SongName = If(reader.IsDBNull(reader.GetOrdinal("song_name")), Nothing, reader.GetString(reader.GetOrdinal("song_name"))),
                            .ArtistName = If(reader.IsDBNull(reader.GetOrdinal("artist_name")), Nothing, reader.GetString(reader.GetOrdinal("artist_name"))),
                            .DurationSeconds = If(reader.IsDBNull(reader.GetOrdinal("duration_seconds")), 0, reader.GetInt32(reader.GetOrdinal("duration_seconds"))),
                            .ArtworkUrl = If(reader.IsDBNull(reader.GetOrdinal("artwork_url")), Nothing, reader.GetString(reader.GetOrdinal("artwork_url"))),
                            .RequestedAt = If(reader.IsDBNull(reader.GetOrdinal("requested_at")), DateTime.Now, DateTime.Parse(reader.GetString(reader.GetOrdinal("requested_at"))))
                        })
                    End While
                End Using
            End Using
        End Using

        Return songs
    End Function

    ''' <summary>
    ''' Mark the current song as completed and remove from queue
    ''' </summary>
    Public Sub CompleteCurrentSong()
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()

            Dim query As String = "
                UPDATE queue
                SET status = 'completed'
                WHERE id = (
                    SELECT id FROM queue
                    WHERE status = 'pending'
                    ORDER BY position ASC
                    LIMIT 1
                )"

            Using cmd As New SQLiteCommand(query, conn)
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' Clear all songs from the queue including currently playing
    ''' </summary>
    Public Sub ClearQueue()
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()

            Dim query As String = "DELETE FROM queue WHERE status IN ('pending', 'playing')"

            Using cmd As New SQLiteCommand(query, conn)
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' Remove a specific song from the queue by YouTube video ID
    ''' </summary>
    Public Sub RemoveSongByVideoId(videoId As String)
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()

            Dim query As String = "DELETE FROM queue WHERE song_id IN (SELECT id FROM songs WHERE youtube_video_id = @videoId) AND status IN ('pending', 'playing')"

            Using cmd As New SQLiteCommand(query, conn)
                cmd.Parameters.AddWithValue("@videoId", videoId)
                Dim rowsAffected = cmd.ExecuteNonQuery()
                If rowsAffected > 0 Then
                    _sdk.LogInfo("DatabaseHelper", $"Removed song with video ID: {videoId}")
                End If
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' Get total count of played songs
    ''' </summary>
    Public Function GetTotalPlayedCount() As Integer
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()

            Dim query As String = "SELECT COUNT(*) FROM queue WHERE status = 'completed'"

            Using cmd As New SQLiteCommand(query, conn)
                Return Convert.ToInt32(cmd.ExecuteScalar())
            End Using
        End Using
    End Function

    ''' <summary>
    ''' Get total count of all song requests
    ''' </summary>
    Public Function GetTotalRequestCount() As Integer
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()

            Dim query As String = "SELECT COUNT(*) FROM queue"

            Using cmd As New SQLiteCommand(query, conn)
                Return Convert.ToInt32(cmd.ExecuteScalar())
            End Using
        End Using
    End Function
End Class

''' <summary>
''' Represents a song in the database
''' </summary>
Public Class SongInfo
    Public Property Id As Long
    Public Property Requester As String
    Public Property OriginalUrl As String
    Public Property YoutubeUrl As String
    Public Property YoutubeVideoId As String
    Public Property SongName As String
    Public Property ArtistName As String
    Public Property DurationSeconds As Integer
    Public Property ArtworkUrl As String
    Public Property RequestedAt As DateTime

    ' Helper properties for compatibility
    Public ReadOnly Property Title As String
        Get
            Return SongName
        End Get
    End Property

    Public ReadOnly Property Artist As String
        Get
            Return ArtistName
        End Get
    End Property

    Public ReadOnly Property RequestedBy As String
        Get
            Return Requester
        End Get
    End Property
End Class
