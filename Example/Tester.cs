using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;

public class Tester : UdonSharpBehaviour
{
    public VideoDecoder videoDecoder;
    public VRCUrl URL;
    public byte[] dataBytes;
    public string dataString;

    public void LoadData()
    {
        if (videoDecoder != null)
            videoDecoder.LoadURL(URL, this, "dataBytes", "OnURLLoaded", "OnLoadError");
    }

    public void OnURLLoaded()
    {
        dataString = videoDecoder.StringFromByteArray(dataBytes);
        Debug.Log("Data [str]: " + dataString);
        dataString = dataString.Substring(4, dataString.IndexOf("==") - 2);
        Debug.Log("Data [sub]: " + dataString);
        dataString = videoDecoder.StringFromByteArray(Convert.FromBase64String(dataString));
        Debug.Log("Data [b64]: " + dataString);
    }

    public void OnLoadError()
    {
        Debug.LogWarning("Failed to load data from " + URL.Get());
    }
}