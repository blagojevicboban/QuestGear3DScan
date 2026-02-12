using UnityEngine;
using UnityEngine.UI;

public class SceneApiTest : MonoBehaviour
{
    public Text statusText;

    void Start()
    {
        Log("Initializing Scene API Test...");
        
        // Ensure OVRSceneManager is available
        if (FindObjectOfType<OVRSceneManager>() == null)
        {
            Log("OVRSceneManager not found in scene! Please add OVRSceneManager prefab.");
        }
    }

    public void RequestSceneCapture()
    {
        Log("Requesting Scene Capture (Room Setup)...");
        // OVRSceneManager.RequestSceneCapture(); // This triggers the system UI for room scanning
        
        // Modern approach uses OVRSceneManager request
        // Note: IsSceneCaptureSupported might not be available in all SDK versions. 
        // We can just try to call RequestSceneCapture directly as it handles support checks internally.
        // Request Scene Capture using the instance method
        var sceneManager = FindObjectOfType<OVRSceneManager>();
        if (sceneManager != null)
        {
            // Request Scene Capture directly via OVRPlugin
            // The string argument is a JSON request string, empty for default behavior.
            if (OVRPlugin.RequestSceneCapture("", out var _))
            {
                Log("Scene Capture requested successfully.");
            }
            else
            {
                Log("Failed to request Scene Capture.");
            }
        }
        else
        {
            Log("OVRSceneManager instance missing!");
        }
    }

    public void LoadScene()
    {
        Log("Loading Scene Model...");
        var sceneManager = FindObjectOfType<OVRSceneManager>();
        if (sceneManager != null)
        {
            // Load the Scene Model (Room)
            // This requires the OVRSceneManager to have prefabs assigned for walls, floor, ceiling, etc.
            // in the Inspector.
            sceneManager.LoadSceneModel();
            Log("LoadSceneModel called.");
        }
        else
        {
             Log("SceneManager not found.");
        }
    }

    void Log(string msg)
    {
        Debug.Log($"[SceneTest] {msg}");
        if (statusText != null) statusText.text += msg + "\n";
    }
}
