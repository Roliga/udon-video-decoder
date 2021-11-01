using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components.Video;

// This example loads and decodes a random color from random-data-api.com and prints it out on the console.
// 
// It depends on:
//     * Udon Video Decoder
//     * UdonDecodeLib
//     * UdonXML
//
// Attach these dependencies to a gameobject in your scene, and reference them in the variables defined bellow.
// Make sure to add a Render Texture and Blank Texture to the Video Decoder.
public class VideoDecodeExample : UdonSharpBehaviour
{
    // Libraries. Remember to link these up in Unity.
    public VideoDecoder videoDecoder;
    public UdonDecodeLib decodeLib;
    public UdonXML udonXML;

    // URL to request, must be defined at compile time, can't be generated at runtime.
    public VRCUrl url = new VRCUrl(
        "https://[PIXEL PROXY HOST]/proxy?auth=[PIXEL PROXY KEY]&rt=mp4&df=xml&et=binary&url="
        + "https%3A//random-data-api.com/api/color/random_color"
        );

    // Place to put returned data or error code.
    private byte[] data;
    private VideoError errorCode;

    void Start()
    {
        // Must give video player some time to initialize.
        SendCustomEventDelayedSeconds("LoadStuff", 5);
    }

    public void LoadStuff()
    {
        Debug.Log("Loading...");

        // Where the magic happens.
        // Reference *this*, as callback parameters are on this script.
        // Callback variables and methods are referenced by name as a string.
        videoDecoder.LoadURL(url, this, "data", "OnLoaded", "errorCode", "OnError");
    }

    // Called when data is successfully loaded.
    public void OnLoaded()
    {
        // Pixel proxy data is set to be base64 encoded XML, so decode that.
        var xml = udonXML.LoadXml(
            decodeLib.DecodeBase64String(
                decodeLib.DecodeByteArray(data)));

        // Decoding methods return null on failure.
        if(xml == null)
        {
            Debug.Log("Failed decoding data!");
            return;
        }

        Debug.Log("Got XML: \n" + udonXML.SaveXmlWithIdent(xml, "    "));

        // Get data->hex_value from XML.
        object hex_value = udonXML.GetChildNodeByName(
            udonXML.GetChildNodeByName(xml, "data"), "hex_value");
        if(hex_value != null)
        {
            float[] f = decodeLib.DecodeHexColor(udonXML.GetNodeValue(hex_value));
            Color color = new Color(f[0], f[1], f[2], f[3]);

            Debug.Log("Got color: " + color);
        }
    }

    // Called on video player error
    public void OnError()
    {
        // Retry automatically if rate limited.
        // VRChat limits requests to one every 5 seconds.
        if (errorCode == VideoError.RateLimited)
        {
            Debug.Log("Rate limited, retrying in 5...");
            SendCustomEventDelayedSeconds("LoadStuff", 5);
        }
        else
        {
            Debug.Log("Failed loading!");
        }
    }
}