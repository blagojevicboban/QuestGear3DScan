using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;

public class CameraAccessTest : MonoBehaviour
{
    public Text statusText; // Assign a Legacy UI Text in inspector
    private WebCamTexture _webCamTexture;

    void Start()
    {
        Log("Initializing...");
        
        // 1. Request Permission (Android 10+ needs this explicit call often)
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Log("Requesting Camera Permission...");
            Permission.RequestUserPermission(Permission.Camera);
        }
        else
        {
            InitializeCamera();
        }
    }

    void Update()
    {
        // Check permission polling if needed, but usually callbacks or restart handles it.
        // For prototype, we just wait a bit or re-trigger.
        
        if (_webCamTexture != null && _webCamTexture.isPlaying)
        {
             // Optional: Update some texture on a Quad to see the feed
        }
    }

    public void InitializeCamera()
    {
        Log("Checking for devices...");
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Log("No Camera devices found!");
            return;
        }

        Log($"Found {devices.Length} devices:");
        foreach (var device in devices)
        {
            Log($"- {device.name} (Front: {device.isFrontFacing})");
        }

        // Try to find the RGB camera
        // On Quest 3, it might not be explicitly named "RGB".
        // functionality might be blocked.
        string camName = devices[0].name;
        Log($"Attempting to start: {camName}");

        _webCamTexture = new WebCamTexture(camName, 1280, 720, 30);
        
        try
        {
            _webCamTexture.Play();
            Log("Camera.Play() called. Waiting for pixels...");
        }
        catch (System.Exception e)
        {
            Log($"Error starting camera: {e.Message}");
        }
    }

    void OnGUI()
    {
        // Fallback debug if UI Text is not linked
        if (statusText == null)
        {
            GUI.Label(new Rect(10, 10, 800, 1000), _logBuffer);
        }
    }

    private string _logBuffer = "";
    void Log(string msg)
    {
        Debug.Log($"[CamTest] {msg}");
        _logBuffer += msg + "\n";
        if (statusText != null)
        {
            statusText.text = _logBuffer;
        }
    }
}
