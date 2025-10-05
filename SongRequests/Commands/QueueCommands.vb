Imports TwitchChatBot
Imports TwitchLib.Client
Imports TwitchLib.Client.Events

''' <summary>
''' Command to display the current song queue
''' Usage: !queue
''' </summary>
Public Class QueueCommand
    Implements ICommand

    Private ReadOnly _queueManager As QueueManager

    Public Sub New(queueManager As QueueManager)
        _queueManager = queueManager
    End Sub

    Public ReadOnly Property RequiredRole As Role Implements ICommand.RequiredRole
        Get
            Return Role.Everyone
        End Get
    End Property

    Public ReadOnly Property UserCooldownSeconds As Integer Implements ICommand.UserCooldownSeconds
        Get
            Return 10
        End Get
    End Property

    Public ReadOnly Property GlobalCooldownSeconds As Integer Implements ICommand.GlobalCooldownSeconds
        Get
            Return 5
        End Get
    End Property

    Public Sub Execute(client As TwitchClient, e As OnMessageReceivedArgs, args As String()) Implements ICommand.Execute
        Dim queue = _queueManager.GetQueue()

        If queue.Count = 0 Then
            client.SendMessage(e.ChatMessage.Channel, "The song queue is empty. Request a song with !sr <url>")
            Return
        End If

        ' Show first 5 songs
        Dim message = "Queue: "
        Dim limit = Math.Min(5, queue.Count)

        For i As Integer = 0 To limit - 1
            Dim song = queue(i)
            message &= $"{i + 1}. {song.ArtistName} - {song.SongName}"
            If i < limit - 1 Then message &= " | "
        Next

        If queue.Count > 5 Then
            message &= $" (+{queue.Count - 5} more)"
        End If

        client.SendMessage(e.ChatMessage.Channel, message)
    End Sub
End Class

''' <summary>
''' Command to display the currently playing song
''' Usage: !nowplaying or !np
''' </summary>
Public Class NowPlayingCommand
    Implements ICommand

    Private ReadOnly _queueManager As QueueManager

    Public Sub New(queueManager As QueueManager)
        _queueManager = queueManager
    End Sub

    Public ReadOnly Property RequiredRole As Role Implements ICommand.RequiredRole
        Get
            Return Role.Everyone
        End Get
    End Property

    Public ReadOnly Property UserCooldownSeconds As Integer Implements ICommand.UserCooldownSeconds
        Get
            Return 5
        End Get
    End Property

    Public ReadOnly Property GlobalCooldownSeconds As Integer Implements ICommand.GlobalCooldownSeconds
        Get
            Return 3
        End Get
    End Property

    Public Sub Execute(client As TwitchClient, e As OnMessageReceivedArgs, args As String()) Implements ICommand.Execute
        Dim current = _queueManager.GetCurrentSong()

        If current Is Nothing Then
            client.SendMessage(e.ChatMessage.Channel, "No song is currently playing.")
            Return
        End If

        Dim message = $"Now playing: {current.ArtistName} - {current.SongName} (requested by {current.Requester})"
        client.SendMessage(e.ChatMessage.Channel, message)
    End Sub
End Class

''' <summary>
''' Command to skip the current song (moderator only)
''' Usage: !skip
''' </summary>
Public Class SkipCommand
    Implements ICommand

    Private ReadOnly _queueManager As QueueManager

    Public Sub New(queueManager As QueueManager)
        _queueManager = queueManager
    End Sub

    Public ReadOnly Property RequiredRole As Role Implements ICommand.RequiredRole
        Get
            Return Role.Moderator
        End Get
    End Property

    Public ReadOnly Property UserCooldownSeconds As Integer Implements ICommand.UserCooldownSeconds
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property GlobalCooldownSeconds As Integer Implements ICommand.GlobalCooldownSeconds
        Get
            Return 0
        End Get
    End Property

    Public Sub Execute(client As TwitchClient, e As OnMessageReceivedArgs, args As String()) Implements ICommand.Execute
        Dim current = _queueManager.GetCurrentSong()

        If current Is Nothing Then
            client.SendMessage(e.ChatMessage.Channel, "No song is currently playing.")
            Return
        End If

        _queueManager.SkipCurrentSong()
        client.SendMessage(e.ChatMessage.Channel, $"Skipped: {current.ArtistName} - {current.SongName}")
    End Sub
End Class

''' <summary>
''' Command to clear the entire queue (moderator only)
''' Usage: !clearqueue
''' </summary>
Public Class ClearQueueCommand
    Implements ICommand

    Private ReadOnly _queueManager As QueueManager

    Public Sub New(queueManager As QueueManager)
        _queueManager = queueManager
    End Sub

    Public ReadOnly Property RequiredRole As Role Implements ICommand.RequiredRole
        Get
            Return Role.Moderator
        End Get
    End Property

    Public ReadOnly Property UserCooldownSeconds As Integer Implements ICommand.UserCooldownSeconds
        Get
            Return 0
        End Get
    End Property

    Public ReadOnly Property GlobalCooldownSeconds As Integer Implements ICommand.GlobalCooldownSeconds
        Get
            Return 0
        End Get
    End Property

    Public Sub Execute(client As TwitchClient, e As OnMessageReceivedArgs, args As String()) Implements ICommand.Execute
        _queueManager.ClearQueue()
        client.SendMessage(e.ChatMessage.Channel, "Song queue has been cleared.")
    End Sub
End Class
