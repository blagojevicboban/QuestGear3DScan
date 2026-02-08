using UnityEngine;
using UnityEngine.UI;

public class DepthAccessTest : MonoBehaviour
{
    public Text statusText;
    
    void Start()
    {
        Log("Initializing Depth Test...");
        Log($"Headset: {OVRPlugin.GetSystemHeadsetType()}");
    }

    void Update()
    {
        // Check Passthrough status
        // bool passthroughActive = OVRManager.instance != null; 
        
        // ...
        
        if (Time.frameCount % 60 == 0) // once per second
        {
            LogStatus();
        }
    }

    void LogStatus()
    {
        string msg = "Depth Test Running\n";
        msg += $"OVRManager: {(OVRManager.instance != null ? "Found" : "Missing")}\n";
        msg += $"Headset: {OVRPlugin.GetSystemHeadsetType()}\n";
        
        // Try to get hand tracking status just to confirm sensor access
        // msg += $"Hands Active: {OVRInput.GetActiveController() == OVRInput.Controller.Hands}\n";

        if (statusText != null) statusText.text = msg;
    }

    void Log(string msg)
    {
        Debug.Log($"[DepthTest] {msg}");
        if (statusText != null) statusText.text += msg + "\n";
    }
}
