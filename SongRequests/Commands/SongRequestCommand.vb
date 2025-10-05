Imports TwitchChatBot
Imports TwitchLib.Client
Imports TwitchLib.Client.Events

''' <summary>
''' Command for requesting songs via Spotify or YouTube URLs
''' Usage: !sr <spotify/youtube url>
''' </summary>
Public Class SongRequestCommand
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
            Return 30 ' 30 second cooldown per user
        End Get
    End Property

    Public ReadOnly Property GlobalCooldownSeconds As Integer Implements ICommand.GlobalCooldownSeconds
        Get
            Return 0
        End Get
    End Property

    Public Sub Execute(client As TwitchClient, e As OnMessageReceivedArgs, args As String()) Implements ICommand.Execute
        If args.Length = 0 Then
            client.SendMessage(e.ChatMessage.Channel, $"@{e.ChatMessage.DisplayName} Usage: !sr <spotify/youtube url>")
            Return
        End If

        Dim url = args(0)
        Dim requester = e.ChatMessage.DisplayName

        ' Process the request asynchronously
        Task.Run(Async Function()
                     Dim result = Await _queueManager.ProcessRequestAsync(requester, url)
                     client.SendMessage(e.ChatMessage.Channel, $"@{requester} {result.Message}")
                 End Function).ConfigureAwait(False)
    End Sub
End Class
