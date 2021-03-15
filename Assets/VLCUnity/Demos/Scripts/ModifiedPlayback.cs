using UnityEngine;
using System;
using LibVLCSharp;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

public class ModifiedPlayback : MonoBehaviour
{
    LibVLC _libVLC = null;
    MediaPlayer _mediaPlayer = null;
    Texture2D tex = null;
    bool playing;
    bool shuttingDown = false;
    ConcurrentQueue<string> workQueue = new ConcurrentQueue<string>();
    ConcurrentQueue<Media> mediaQueue = new ConcurrentQueue<Media>();

    void Awake()
    {
        Core.Initialize(Application.dataPath);
        _libVLC = new LibVLC("--no-osd", "--verbose=3");
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        _libVLC.Log += (s, e) => UnityEngine.Debug.Log(e.FormattedLog); // enable this for logs in the editor

        StartThread();
    }

    void OnDisable()
    {
        _mediaPlayer.Stop();

        // Aggresively trying to dispose all the things  
        if (_mediaPlayer.Media != null)
        {
            for (int i = 0; i < _mediaPlayer.Media.SubItems.Count; i++)
            {
                _mediaPlayer.Media.SubItems[i]?.Dispose();
            }
        }

        // Force disposable of any floating media objects in our queue
        for (int i = 0; i < mediaQueue.Count; i++)
        {
            Media m;
            if (mediaQueue.TryDequeue(out m))
                m.Dispose();
        }

        // Explicitly remove the current media item
        _mediaPlayer?.Media?.Dispose();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
    }

    public void StartThread()
    {
        workQueue.Enqueue("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4");
        Thread t = new Thread(new ThreadStart(ThreadedMediaParse));
        t.Start();
    }

    void PlayFromQueue()
    {
        if (_mediaPlayer == null)
        {
            _mediaPlayer = new MediaPlayer(_libVLC);
        }

        Media m;
        if (mediaQueue.TryDequeue(out m))
        {
            // Unable to correctly dispose this media object if sent to the player
            // Explicitly destroying the media object as its dequeued also toggles the locking

            // Assigning the media object to the media player then causes the lock 
            // - presumably the created object isn't getting cleaned up
            //_mediaPlayer.Media = m;
            //_mediaPlayer.Play();
            //playing = true;

            // If m.Dispose is removed we still lock regardless
            m.Dispose();
        }
    }

    void Update()
    {
        // Check for media items to start playing
        PlayFromQueue();

        if (!playing) return;

        if (tex == null)
        {
            uint i_videoHeight = 0;
            uint i_videoWidth = 0;

            _mediaPlayer.Size(0, ref i_videoWidth, ref i_videoHeight);
            var texptr = _mediaPlayer.GetTexture(out bool updated);
            if (i_videoWidth != 0 && i_videoHeight != 0 && updated && texptr != IntPtr.Zero)
            {
                Debug.Log("Creating texture with height " + i_videoHeight + " and width " + i_videoWidth);
                tex = Texture2D.CreateExternalTexture((int)i_videoWidth,
                    (int)i_videoHeight,
                    TextureFormat.RGBA32,
                    false,
                    true,
                    texptr);

                GetComponent<Renderer>().material.mainTexture = tex;
            }
        }
        else
        {
            var texptr = _mediaPlayer.GetTexture(out bool updated);
            if (updated)
            {
                tex.UpdateExternalTexture(texptr);
            }
        }
    }

    private void OnDestroy()
    {
        shuttingDown = true;
    }

    /// <summary>
    /// Use this to try and parse media items in isolation
    /// </summary>
    public void ThreadedMediaParse()
    {
        while (shuttingDown == false)
        {
            string url;

            while (_libVLC != null && workQueue.TryDequeue(out url))
            {
                var media = new Media(_libVLC, new Uri(url));
                var result = media.Parse(MediaParseOptions.ParseNetwork);

                while (media.ParsedStatus != MediaParsedStatus.Done)
                {
                    // Should use the parsed event but fine for testing
                    Thread.Sleep(1);
                }

                if (media.SubItems.Count > 0)
                {
                    mediaQueue.Enqueue(media.SubItems[0].Duplicate());

                    // Try and clean up anything unwanted asap except our clean copy in the queue
                    for (int i = 0; i < media.SubItems.Count; i++)
                        media.SubItems[i].Dispose();
                }
                else
                {
                    mediaQueue.Enqueue(media.Duplicate());
                }

                media.Dispose();
            }

            Thread.Sleep(50);
        }
    }
}

