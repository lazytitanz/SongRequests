Imports System

Public Class WebUI
    ''' <summary>
    ''' Returns the HTML content for the Song Requests web UI
    ''' </summary>
    Public Shared Function GetHTML() As String
        Return "<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Song Requests - Twitch Bot</title>
    <script src=""https://cdn.tailwindcss.com""></script>
    <script defer src=""https://cdn.jsdelivr.net/npm/alpinejs@3.x.x/dist/cdn.min.js""></script>
    <style>
        [x-cloak] { display: none !important; }
    </style>
</head>
<body class=""bg-gray-900 text-gray-100"">
    <div x-data=""songRequestApp()"" x-init=""init()"" class=""min-h-screen"">
        <!-- Header -->
        <header class=""bg-purple-600 shadow-lg"">
            <div class=""container mx-auto px-6 py-8"">
                <div>
                    <h1 class=""text-4xl font-bold"">Song Request Queue</h1>
                    <p class=""text-purple-200 mt-2"">Live song requests from Twitch chat</p>
                </div>
            </div>
        </header>

        <!-- Stats Bar -->
        <div class=""bg-gray-800 border-b border-gray-700"">
            <div class=""container mx-auto px-6 py-4"">
                <div class=""flex justify-around"">
                    <div class=""text-center"">
                        <div class=""text-3xl font-bold text-purple-400"" x-text=""queue.length"">0</div>
                        <div class=""text-sm text-gray-400"">In Queue</div>
                    </div>
                    <div class=""text-center"">
                        <div class=""text-3xl font-bold text-green-400"" x-text=""totalPlayed"">0</div>
                        <div class=""text-sm text-gray-400"">Total Played</div>
                    </div>
                    <div class=""text-center"">
                        <div class=""text-3xl font-bold text-blue-400"" x-text=""totalRequests"">0</div>
                        <div class=""text-sm text-gray-400"">Total Requests</div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Main Content -->
        <main class=""container mx-auto px-6 py-8 pb-32"">
            <!-- Queue Empty State -->
            <div x-show=""queue.length === 0"" x-cloak class=""text-center py-16"">
                <svg class=""mx-auto h-24 w-24 text-gray-600"" fill=""none"" stroke=""currentColor"" viewBox=""0 0 24 24"">
                    <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2""
                          d=""M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3""></path>
                </svg>
                <h3 class=""mt-4 text-xl font-medium text-gray-400"">No songs in queue</h3>
                <p class=""mt-2 text-gray-500"">Songs will appear here when viewers use !sr command</p>
            </div>

            <!-- Queue List -->
            <div x-show=""queue.length > 0"" class=""space-y-4"">
                <template x-for=""(song, index) in queue"" :key=""song.id"">
                    <div class=""bg-gray-800 rounded-lg shadow-lg p-6 border border-gray-700 hover:border-purple-500 transition-colors"">
                        <div class=""flex items-start gap-4"">
                            <!-- Album Artwork -->
                            <div class=""w-20 h-20 rounded-lg overflow-hidden flex-shrink-0"">
                                <img x-show=""song.artwork"" :src=""song.artwork"" alt=""Album Art"" class=""w-full h-full object-cover"">
                                <div x-show=""!song.artwork"" class=""w-full h-full bg-gray-700 flex items-center justify-center"">
                                    <svg class=""w-10 h-10 text-gray-500"" fill=""none"" stroke=""currentColor"" viewBox=""0 0 24 24"">
                                        <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3""></path>
                                    </svg>
                                </div>
                            </div>
                            <!-- Song Info -->
                            <div class=""flex-1"">
                                <div class=""flex items-center gap-3 mb-2"">
                                    <span class=""bg-purple-600 text-white px-3 py-1 rounded-full text-sm font-bold""
                                          x-text=""'#' + (index + 1)""></span>
                                    <h3 class=""text-xl font-semibold text-white"" x-text=""song.title""></h3>
                                </div>
                                <div class=""flex items-center gap-4 text-gray-400"">
                                    <span x-text=""song.artist""></span>
                                    <span class=""text-gray-600"">•</span>
                                    <span x-text=""song.duration""></span>
                                </div>
                                <div class=""mt-3 flex items-center gap-2"">
                                    <span class=""text-sm text-gray-500"">Requested by</span>
                                    <span class=""bg-gray-700 px-3 py-1 rounded-full text-sm text-purple-400""
                                          x-text=""song.requestedBy""></span>
                                    <span class=""text-gray-600"">•</span>
                                    <span class=""text-sm text-gray-500"" x-text=""song.requestedAt""></span>
                                </div>
                            </div>
                        </div>
                    </div>
                </template>
            </div>
        </main>

        <!-- Music Player Bar -->
        <div class=""fixed bottom-0 left-0 right-0 bg-gray-800 border-t border-gray-700 shadow-lg"" x-show=""nowPlaying.videoId"">
            <div class=""container mx-auto px-6 py-4"">
                <div class=""flex items-center gap-4"">
                    <!-- Album Art -->
                    <div class=""w-16 h-16 rounded overflow-hidden flex-shrink-0"">
                        <img x-show=""nowPlaying.artwork"" :src=""nowPlaying.artwork"" alt=""Album Art"" class=""w-full h-full object-cover"">
                        <div x-show=""!nowPlaying.artwork"" class=""w-full h-full bg-gray-700 flex items-center justify-center"">
                            <svg class=""w-8 h-8 text-gray-500"" fill=""none"" stroke=""currentColor"" viewBox=""0 0 24 24"">
                                <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3""></path>
                            </svg>
                        </div>
                    </div>

                    <!-- Song Info -->
                    <div class=""flex-1 min-w-0"">
                        <div class=""font-semibold text-white truncate"" x-text=""nowPlaying.title""></div>
                        <div class=""text-sm text-gray-400 truncate"" x-text=""nowPlaying.artist""></div>
                    </div>

                    <!-- Controls -->
                    <div class=""flex items-center gap-4"">
                        <!-- Play/Pause Button -->
                        <button @click=""togglePlayPause()"" class=""bg-purple-600 hover:bg-purple-700 text-white p-3 rounded-full transition-colors"">
                            <svg x-show=""!isPlaying"" class=""w-6 h-6"" fill=""currentColor"" viewBox=""0 0 24 24"">
                                <path d=""M8 5v14l11-7z""/>
                            </svg>
                            <svg x-show=""isPlaying"" class=""w-6 h-6"" fill=""currentColor"" viewBox=""0 0 24 24"">
                                <path d=""M6 4h4v16H6V4zm8 0h4v16h-4V4z""/>
                            </svg>
                        </button>

                        <!-- Next Button -->
                        <button @click=""playNext()"" class=""text-gray-400 hover:text-white transition-colors"">
                            <svg class=""w-6 h-6"" fill=""currentColor"" viewBox=""0 0 24 24"">
                                <path d=""M6 4l12 8-12 8V4zm13 0v16h2V4h-2z""/>
                            </svg>
                        </button>

                        <!-- Volume Control -->
                        <div class=""flex items-center gap-2"">
                            <svg class=""w-5 h-5 text-gray-400"" fill=""currentColor"" viewBox=""0 0 24 24"">
                                <path d=""M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02z""/>
                            </svg>
                            <input type=""range"" min=""0"" max=""100"" x-model=""volume"" @input=""updateVolume()"" class=""w-24 accent-purple-600"">
                        </div>
                    </div>
                </div>

                <!-- Progress Bar -->
                <div class=""mt-3"">
                    <div class=""flex items-center gap-2 text-xs text-gray-400"">
                        <span x-text=""formatTime(currentTime)"">0:00</span>
                        <div class=""flex-1 bg-gray-700 rounded-full h-1 cursor-pointer"" @click=""seek($event)"">
                            <div class=""bg-purple-600 h-1 rounded-full transition-all"" :style=""`width: ${progress}%`""></div>
                        </div>
                        <span x-text=""formatTime(duration)"">0:00</span>
                    </div>
                </div>
            </div>
        </div>

        <!-- Audio Element -->
        <audio x-ref=""audio"" @ended=""playNext()"" @timeupdate=""updateProgress()"" @loadedmetadata=""duration = $refs.audio.duration""></audio>
    </div>

    <script>
        function songRequestApp() {
            return {
                queue: [],
                nowPlaying: {},
                totalPlayed: 0,
                totalRequests: 0,
                eventSource: null,
                isPlaying: false,
                volume: 70,
                currentTime: 0,
                duration: 0,
                progress: 0,

                init() {
                    this.loadQueue();
                    this.connectSSE();
                },

                connectSSE() {
                    this.eventSource = new EventSource('/api/events');

                    this.eventSource.onmessage = (event) => {
                        if (event.data === 'refresh') {
                            this.loadQueue();
                        }
                    };

                    this.eventSource.onerror = () => {
                        console.log('SSE connection lost, reconnecting...');
                        this.eventSource.close();
                        setTimeout(() => this.connectSSE(), 3000);
                    };
                },

                async loadQueue() {
                    try {
                        const response = await fetch('/api/queue');

                        if (!response.ok) {
                            console.error('Failed to load queue: HTTP', response.status);
                            return;
                        }

                        const data = await response.json();

                        this.queue = data.queue || [];

                        const prevVideoId = this.nowPlaying.videoId;
                        this.nowPlaying = data.nowPlaying || {};
                        this.totalPlayed = data.totalPlayed || 0;
                        this.totalRequests = data.totalRequests || 0;

                        // Stop playback if queue was cleared
                        if (!this.nowPlaying.videoId && prevVideoId) {
                            this.stopPlayback();
                        }
                        // Auto-play if new song
                        else if (this.nowPlaying.videoId && this.nowPlaying.videoId !== prevVideoId) {
                            if (this.nowPlaying.audioUrl) {
                                this.playSong();
                            }
                        }
                    } catch (error) {
                        console.error('Failed to load queue:', error);
                    }
                },

                playSong() {
                    if (!this.nowPlaying.audioUrl) {
                        return;
                    }

                    const audio = this.$refs.audio;
                    audio.src = this.nowPlaying.audioUrl;
                    audio.volume = this.volume / 100;

                    audio.play()
                        .then(() => {
                            this.isPlaying = true;
                        })
                        .catch(err => {
                            console.error('Failed to play:', err);
                            this.isPlaying = false;
                        });
                },

                stopPlayback() {
                    const audio = this.$refs.audio;
                    audio.pause();
                    audio.src = '';
                    this.isPlaying = false;
                    this.currentTime = 0;
                    this.duration = 0;
                    this.progress = 0;
                },

                togglePlayPause() {
                    const audio = this.$refs.audio;
                    if (this.isPlaying) {
                        audio.pause();
                        this.isPlaying = false;
                    } else {
                        if (!audio.src && this.nowPlaying.audioUrl) {
                            this.playSong();
                        } else {
                            audio.play();
                            this.isPlaying = true;
                        }
                    }
                },

                playNext() {
                    // Skip to next song via API
                    fetch('/api/skip', { method: 'POST' })
                        .then(response => {
                            if (!response.ok) {
                                console.error('Failed to skip: HTTP', response.status);
                                return;
                            }
                            this.loadQueue();
                        })
                        .catch(err => console.error('Failed to skip:', err));
                },

                updateVolume() {
                    const audio = this.$refs.audio;
                    if (audio) {
                        audio.volume = this.volume / 100;
                    }
                },

                updateProgress() {
                    const audio = this.$refs.audio;
                    this.currentTime = audio.currentTime;
                    this.duration = audio.duration || 0;
                    this.progress = this.duration > 0 ? (this.currentTime / this.duration) * 100 : 0;
                },

                seek(event) {
                    const audio = this.$refs.audio;
                    const rect = event.currentTarget.getBoundingClientRect();
                    const percent = (event.clientX - rect.left) / rect.width;
                    audio.currentTime = percent * audio.duration;
                },

                formatTime(seconds) {
                    if (!seconds || isNaN(seconds)) return '0:00';
                    const mins = Math.floor(seconds / 60);
                    const secs = Math.floor(seconds % 60);
                    return `${mins}:${secs.toString().padStart(2, '0')}`;
                }
            }
        }
    </script>
</body>
</html>"
    End Function
End Class
