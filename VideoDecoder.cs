//#define VIDEO_DECODER_DEBUG

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;
using System.Text;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Components.Video;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UdonSharpEditor;
#endif

[RequireComponent(typeof(VRCUnityVideoPlayer))]
[RequireComponent(typeof(Camera))]
public class VideoDecoder : UdonSharpBehaviour
{
    public Texture2D texture2D;

    [HideInInspector]
    public bool editorInitialized;

    private bool updateData;

    private UdonSharpBehaviour cbBehaviour;
    private string cbkVariable;
    private string cbMethod;
    private string cbMethodError;

    private VRCUnityVideoPlayer videoPlayer;

#if VIDEO_DECODER_DEBUG
    private DateTime videoLoadStart;
    private DateTime videoLoadEnd;
    private DateTime renderStart;
#endif

    public void Start()
    {
        // Move far away to ensure camera doesn't intersect with anything
        transform.position = new Vector3(0, float.MaxValue, 0);
        
        videoPlayer = (VRCUnityVideoPlayer)GetComponent(typeof(VRCUnityVideoPlayer));
    }

    public override void OnVideoReady()
    {
#if VIDEO_DECODER_DEBUG
        videoLoadEnd = DateTime.Now;
#endif

        updateData = true;
    }

    public override void OnVideoError(VideoError videoError)
    {
#if VIDEO_DECODER_DEBUG
        Debug.Log("[VideoDecoder] Download failed: " + videoError);
#endif

        cbBehaviour.SendCustomEvent(cbMethodError);
    }

    public void OnPostRender()
    {
        if (!updateData)
            return;
        updateData = false;

#if VIDEO_DECODER_DEBUG
        renderStart = DateTime.Now;
#endif

        texture2D.ReadPixels(new Rect(0, 0, texture2D.width, texture2D.height), 0, 0, false);
        Color[] textureData = texture2D.GetPixels();

        byte[] bytes = new byte[textureData.Length / 8];
        int byteIndex = 0;
        for (int y = texture2D.height - 1; y >= 0; y--)
        {
            int x = 0;
            while (x <= texture2D.width - 8)
            {
                byte b = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (textureData[y * texture2D.width + x].grayscale > 0.5)
                        b |= (byte)(1 << 7 - i);
                    x++;
                }

                bytes[byteIndex] = b;
                byteIndex++;
            }
        }

#if VIDEO_DECODER_DEBUG
        Debug.Log("[VideoDecoder] Got data: " + StringFromByteArray(bytes));
        Debug.Log("[VideoDecoder] Download took: "
            + (videoLoadEnd - videoLoadStart).TotalMilliseconds.ToString()
            + " ms, Decode took: "
            + (DateTime.Now - renderStart).TotalMilliseconds.ToString()
            + " ms");
#endif

        cbBehaviour.SetProgramVariable(cbkVariable, bytes);
        cbBehaviour.SendCustomEvent(cbMethod);
    }

    public string StringFromByteArray(byte[] bytes)
    {
        char[] c = new char[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
            c[i] = Convert.ToChar(bytes[i]);
        return new string(c);
    }

    public void LoadURL(VRCUrl url, UdonSharpBehaviour callbackBehaviour, string callbackVariable, string callbackMethod = null, string callbackMethodError = null)
    {
        cbBehaviour = callbackBehaviour;
        cbkVariable = callbackVariable;
        cbMethod = callbackMethod;
        cbMethodError = callbackMethodError;

#if VIDEO_DECODER_DEBUG
        videoLoadStart = DateTime.Now;
        Debug.Log("[VideoDecoder] Downloading URL: " + url.Get());
#endif

        videoPlayer.LoadURL(url);
    }
}

#if !COMPILER_UDONSHARP && UNITY_EDITOR
[CustomEditor(typeof(VideoDecoder))]
public class VideoDecoderEditor : Editor
{
    public void OnEnable()
    {
        var videoDecoder = (VideoDecoder)target;

        if (!videoDecoder.editorInitialized)
        {
            var videoPlayer = videoDecoder.GetComponent<VRCUnityVideoPlayer>();
            var camera = videoDecoder.GetComponent<Camera>();

            Undo.RecordObjects(new UnityEngine.Object[] { videoPlayer, camera }, "Set Initial Camera/Player Settings");
            SetCameraVideoSettings(camera, videoPlayer);

            videoDecoder.editorInitialized = true;
        }
    }

    public override void OnInspectorGUI()
    {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

        var videoDecoder = (VideoDecoder)target;
        var videoPlayer = videoDecoder.GetComponent<VRCUnityVideoPlayer>();
        var camera = videoDecoder.GetComponent<Camera>();

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        // Set settings button
        if(GUILayout.Button("Set Camera/Player Settings"))
        {
            Undo.RecordObjects(new UnityEngine.Object[] { videoPlayer, camera }, "Set Camera/Player Settings");
            SetCameraVideoSettings(camera, videoPlayer);
        }

        // Render texture
        EditorGUI.BeginChangeCheck();
        var newRenderTexture = (RenderTexture)EditorGUILayout.ObjectField("Render Texture",
            camera.targetTexture,
            typeof(RenderTexture),
            true);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(camera, "Change Camera Render Texture");
            camera.targetTexture = newRenderTexture;
        }

        if (camera.targetTexture != GetPrivateField<RenderTexture>(videoPlayer, "targetTexture"))
            SetPrivateField(videoPlayer, "targetTexture", camera.targetTexture);

        // Blank texture
        EditorGUI.BeginChangeCheck();
        var newTexture = (Texture2D)EditorGUILayout.ObjectField("Blank Texture",
            videoDecoder.texture2D,
            typeof(Texture2D),
            true);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(videoDecoder, "Change Blank Texture");
            videoDecoder.texture2D = newTexture;
        }

        EditorGUI.EndDisabledGroup();

        // Info boxes
        var rt = camera.targetTexture;
        var bt = videoDecoder.texture2D;
        if (rt != null && bt != null)
        {
            EditorGUILayout.HelpBox($"Blank Texture size: {bt.width}x{bt.height}\nRender Texture size: {rt.width}x{rt.height}", MessageType.None);

            if (rt.width != bt.width || rt.height != bt.height)
                EditorGUILayout.HelpBox($"Blank Texture and Render Texture sizes must match!", MessageType.Error);
            else if (bt.width % 8 != 0 || bt.height % 8 != 0)
                EditorGUILayout.HelpBox($"Texture width and height must be divisable by 8!", MessageType.Error);
        }
        else
        {
            EditorGUILayout.HelpBox($"Blank Texture and Render Texture must be set!", MessageType.Error);
        }
    }

    private static void SetCameraVideoSettings(Camera camera, VRCUnityVideoPlayer videoPlayer)
    {
        videoPlayer.Loop = false;
        SetPrivateField(videoPlayer, "autoPlay", false);
        SetPrivateField(videoPlayer, "aspectRatio", UnityEngine.Video.VideoAspectRatio.NoScaling);
        SetPrivateField(videoPlayer, "renderMode", 0);

        camera.clearFlags = CameraClearFlags.Nothing;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 0.02f;
    }

    private static void SetPrivateField(object obj, string field, object value)
    {
        obj.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(obj, value);
    }

    private static T GetPrivateField<T>(object obj, string field)
    {
        return (T)obj.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(obj);
    }
}
#endif