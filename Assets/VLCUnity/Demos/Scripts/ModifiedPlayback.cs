using UnityEngine;
using System;
using LibVLCSharp;
using System.Runtime.InteropServices;

public class ModifiedPlayback : MonoBehaviour
{
    [SerializeField] private bool playNewMedia = false;
    [SerializeField] private string playNext;

    LibVLC _libVLC = null;
    MediaPlayer _mediaPlayer = null;
    Texture2D tex = null;
    bool currentMediaWasParsed = false;

    [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl,
               EntryPoint = "libvlc_media_release")]
    internal static extern void LibVLCMediaRelease(IntPtr media);

    void Awake()
    {
        Core.Initialize(Application.dataPath);
        _libVLC = new LibVLC("--no-osd", "--verbose=3");
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        _libVLC.Log += (s, e) => UnityEngine.Debug.Log(e.FormattedLog); // enable this for logs in the editor

        ChangeMedia("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4");
    }

    void OnDisable()
    {
        _mediaPlayer.Stop();

        if (currentMediaWasParsed) // Extra ref decrement if the currently playing media was a parsed one
            ClearCurrentMediaExtra();

        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
        _libVLC?.Dispose();
        _libVLC = null;
    }

    // Makes an extra reference release on the media item to account for a bug in parsed streams (perhaps other media?)
    private void ClearCurrentMediaExtra()
    {
        Media media = _mediaPlayer.Media;
        if (media != null)
        {
            LibVLCMediaRelease(media.NativeReference);
            media.Dispose();
        }
    }

    public async void ChangeMedia(string url)
    {
        if (_mediaPlayer == null)
        {
            _mediaPlayer = new MediaPlayer(_libVLC);
        }

        Media media = new Media(_libVLC, new Uri(url));

        await media.Parse(MediaParseOptions.ParseNetwork);

        if (media.ParsedStatus == MediaParsedStatus.Done)
        {
            // First do a deeper cleanup of any existing media item - a VLC bug means it won't fully release the existing media on a mediaplayer
            if (currentMediaWasParsed)
            {
                ClearCurrentMediaExtra();
            }

            MediaList subItems = media.SubItems;

            if (subItems.Count > 0)
            {
                // As used by a youtube url
                Media newMedia = subItems[0]; // reference increment 
                _mediaPlayer.Media = newMedia;
                newMedia.Dispose();
            }
            else
            {
                // As used by bunny film
                _mediaPlayer.Media = media;
            }

            subItems.Dispose();
        }

        media.Dispose();

        currentMediaWasParsed = true;     

        _mediaPlayer.Play();        
    }


    void Update()
    {
        if (playNewMedia) // Change next url in unity inspector
        {
            ChangeMedia(playNext);
            playNewMedia = false;
        }

#if UNITY_EDITOR
        _mediaPlayer.Mute = UnityEditor.EditorUtility.audioMasterMute;
#endif

        if (_mediaPlayer == null || !_mediaPlayer.IsPlaying) return;

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
