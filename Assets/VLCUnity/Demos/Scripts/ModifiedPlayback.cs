using UnityEngine;
using System;
using LibVLCSharp;

public class ModifiedPlayback : MonoBehaviour
{
    LibVLC _libVLC;
    MediaPlayer _mediaPlayer;
    Texture2D tex = null;
    bool playing;
    
    void Awake()
    {
        Core.Initialize(Application.dataPath);
        
        _libVLC = new LibVLC("--no-osd", "--verbose=3");

        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        _libVLC.Log += (s, e) => UnityEngine.Debug.Log(e.FormattedLog); // enable this for logs in the editor

        Play();
    }

    void OnDisable() 
    {
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;

        _libVLC?.Dispose();
        _libVLC = null;
    }

    public async void Play()
    {
        if (_mediaPlayer == null)
        {
            _mediaPlayer = new MediaPlayer(_libVLC);
        }

        var media = new Media(_libVLC, new Uri("http://www.youtube.com/watch?v=NTZlfJ2Tpy0"));

        // Parse with ParseNetwork appears to lock the editor from closing after first run,
        // and locks completely trying to run in editor a second time. 2019.4.18f1 LTS
        await media.Parse(MediaParseOptions.ParseNetwork);

        if (media.SubItems.Count > 0)
        {
            _mediaPlayer.Media = media.SubItems[0];
        }
        else
        {
            _mediaPlayer.Media = media;
        }

        media.Dispose();

        _mediaPlayer.Play();
        playing = true;
    }

    void Update()
    {
        if(!playing) return;

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
}