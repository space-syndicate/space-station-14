using System;
using System.IO;
using Robust.Client.WebView;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;
using Robust.Shared.IoC;
using Robust.Shared.ContentPack;
using Robust.Shared.Logging;

namespace Content.Client.Lobby.UI;

/// <summary>
/// A control that displays video backgrounds using WebView and HTML5 video.
/// </summary>
public sealed class LobbyVideoBackgroundControl : Control, IDisposable
{
    private WebViewControl? _webView;
    private bool _isDisposed;
    private ResPath? _currentVideoPath;
    private bool _isLooping = true;
    private float _volume = 0.5f;
    private bool _webViewInitialized;

    /// <summary>
    /// Event raised when the video finishes playing (if not looping).
    /// </summary>
    public event Action? VideoEnded;

    protected override void EnteredTree()
    {
        base.EnteredTree();
        InitializeWebView();
    }

    private void InitializeWebView()
    {
        if (_webView != null || _webViewInitialized)
            return;

        _webViewInitialized = true;

        try
        {
            _webView = new WebViewControl();
            _webView.AlwaysActive = true;
            AddChild(_webView);

            // Set up resource handler for video files
            _webView.AddResourceRequestHandler(HandleResourceRequest);

            // Initialize with blank page that has video element
            LoadBlankPage();

            Logger.Debug($"[LobbyVideo] WebView initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.Error($"[LobbyVideo] Failed to initialize WebView: {ex.Message}");
            _webViewInitialized = false;
            _webView = null;
        }
    }

    private void HandleResourceRequest(IRequestHandlerContext context)
    {
        if (_currentVideoPath == null)
            return;

        // Check if this is a request for our video file
        // The HTML video src will be "lobbyvideo://play" and we'll intercept it
        if (context.Url.StartsWith("lobbyvideo://"))
        {
            var resourceManager = IoCManager.Resolve<IResourceManager>();
            var videoPath = _currentVideoPath.Value;

            if (resourceManager.ContentFileExists(videoPath))
            {
                // Determine MIME type based on extension
                var mimeType = GetMimeType(videoPath.Extension);

                // Create the stream from the video file
                var memStream = new MemoryStream();
                using var fileStream = resourceManager.ContentFileRead(videoPath);
                fileStream.CopyTo(memStream);
                memStream.Seek(0, SeekOrigin.Begin);

                context.DoRespondStream(memStream, mimeType);
            }
        }
    }

    private static string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".webm" => "video/webm",
            ".mp4" => "video/mp4",
            ".ogg" => "video/ogg",
            ".ogv" => "video/ogg",
            _ => "application/octet-stream"
        };
    }

    private void LoadBlankPage()
    {
        if (_webView == null)
            return;

        var html = @"<!DOCTYPE html>
<html>
<head>
    <style>
        * { margin: 0; padding: 0; }
        body { background: #000; overflow: hidden; }
        video {
            position: absolute;
            top: 50%;
            left: 50%;
            min-width: 100%;
            min-height: 100%;
            width: auto;
            height: auto;
            transform: translate(-50%, -50%);
            object-fit: cover;
        }
    </style>
</head>
<body>
    <video id='lobbyVideo' playsinline loop muted></video>
    <script>
        var video = document.getElementById('lobbyVideo');

        video.addEventListener('ended', function() {
            // This will be handled by the loop attribute
        });

        function loadVideo(url) {
            video.src = url;
            video.load();
        }

        function playVideo() {
            video.play().catch(e => console.log('Play failed:', e));
        }

        function pauseVideo() {
            video.pause();
        }

        function setVolume(vol) {
            video.volume = vol;
        }

        function setLoop(loop) {
            video.loop = loop;
        }
    </script>
</body>
</html>";

        // Create a data URL with the HTML
        var base64Html = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html));
        _webView.Url = $"data:text/html;base64,{base64Html}";
    }

    /// <summary>
    /// Loads and plays a video from the specified path.
    /// </summary>
    /// <param name="videoPath">Path to the video file relative to the Content directory.</param>
    /// <param name="loop">Whether to loop the video.</param>
    /// <param name="volume">Volume level (0.0 to 1.0).</param>
    public void PlayVideo(ResPath videoPath, bool loop = true, float volume = 0.5f)
    {
        if (_webView == null)
        {
            InitializeWebView();
        }

        if (_webView == null)
            return;

        _currentVideoPath = videoPath;
        _isLooping = loop;
        _volume = volume;

        // Wait for page to load, then set up video
        _webView.ExecuteJavaScript($@"
            var video = document.getElementById('lobbyVideo');
            video.loop = {loop.ToString().ToLower()};
            video.volume = {volume};
            video.src = '{GetVideoUrl(videoPath)}';
            video.load();
            video.play().catch(e => console.log('Auto-play failed:', e));
        ");
    }

    private string GetVideoUrl(ResPath path)
    {
        // Use a custom protocol that we'll handle via the resource request handler
        return $"lobbyvideo://play/{path}";
    }

    /// <summary>
    /// Pauses the currently playing video.
    /// </summary>
    public void Pause()
    {
        _webView?.ExecuteJavaScript("document.getElementById('lobbyVideo').pause();");
    }

    /// <summary>
    /// Resumes video playback.
    /// </summary>
    public void Play()
    {
        _webView?.ExecuteJavaScript("document.getElementById('lobbyVideo').play().catch(e => console.log('Play failed:', e));");
    }

    /// <summary>
    /// Stops video playback and resets to the beginning.
    /// </summary>
    public void Stop()
    {
        _webView?.ExecuteJavaScript(@"
            var video = document.getElementById('lobbyVideo');
            video.pause();
            video.currentTime = 0;
        ");
    }

    /// <summary>
    /// Sets the volume level.
    /// </summary>
    /// <param name="volume">Volume level between 0.0 and 1.0.</param>
    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
        _webView?.ExecuteJavaScript($"document.getElementById('lobbyVideo').volume = {_volume};");
    }

    /// <summary>
    /// Updates the looping state.
    /// </summary>
    /// <param name="loop">Whether to loop the video.</param>
    public void SetLooping(bool loop)
    {
        _isLooping = loop;
        _webView?.ExecuteJavaScript($"document.getElementById('lobbyVideo').loop = {loop.ToString().ToLower()};");
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        if (_webView != null)
        {
            _webView.RemoveResourceRequestHandler(HandleResourceRequest);
            _webView.Dispose();
            _webView = null;
        }
    }
}

