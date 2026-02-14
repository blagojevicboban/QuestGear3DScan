using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;
using System.Collections;

public class CameraAccessTest : MonoBehaviour
{
    public Text statusText; // Assign a UI Text in inspector
    private WebCamTexture _webCamTexture;
    private string _logBuffer = "";

    void Start()
    {
        // Force create a World Space canvas if UI is missing (common issue in VR)
        if (statusText == null)
        {
            CreateDebugUI();
        }

        Log("Initializing Camera Test...");
        
        StartCoroutine(AskAndCheckPermission());
    }

    IEnumerator AskAndCheckPermission()
    {
        if (Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Log("Permission ALREADY Granted!");
            InitializeCamera();
            yield break;
        }

        Log("Requesting Permission... PLEASE CLICK ALLOW!");
        Permission.RequestUserPermission(Permission.Camera);

        // Wait up to 60 seconds for user to click Allow
        float timeout = 60f;
        while (timeout > 0)
        {
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Log("Permission Granted! Starting Camera...");
                InitializeCamera();
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
            timeout -= 0.5f;
        }

        Log("ERROR: Permission Timed Out. Please restart and Allow.");
    }

    void CreateDebugUI()
    {
        GameObject canvasObj = new GameObject("DebugCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Always attach to Main Camera (Head-Locked UI for debugging)
        if (Camera.main != null)
        {
            canvasObj.transform.SetParent(Camera.main.transform, false);
            canvasObj.transform.localPosition = new Vector3(0, 0, 0.5f); // 50cm in front
            canvasObj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            canvasObj.transform.position = new Vector3(0, 0, 0.5f);
        }
        
        canvasObj.transform.localScale = Vector3.one * 0.001f; // Smaller scale (0.001 -> 80cm wide text area)
        
        GameObject textObj = new GameObject("DebugText");
        textObj.transform.SetParent(canvasObj.transform, false);
        
        Text txt = textObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 24;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
        txt.rectTransform.sizeDelta = new Vector2(800, 600);
        txt.color = Color.green; // Green text for visibility
        
        // Add minimal background for contrast
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        bgObj.transform.SetAsFirstSibling();
        Image img = bgObj.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.5f);
        img.rectTransform.sizeDelta = new Vector2(820, 620);

        statusText = txt;
        Debug.Log("Created Head-Locked Debug UI");
    }

    public void InitializeCamera()
    {
        Log("Checking for devices...");
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Log("ERROR: No Camera devices found!");
            return;
        }

        Log($"Found {devices.Length} devices.");
        foreach (var device in devices)
        {
            Log($"- {device.name} (Front: {device.isFrontFacing})");
        }

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
            Log($"EXCEPTION: {e.Message}");
        }
    }

    private bool _hasReceivedFrame = false;

    void Update()
    {
        // No longer need LookAt since it is parented to camera

        if (!_hasReceivedFrame && _webCamTexture != null && _webCamTexture.isPlaying)
        {
            if (_webCamTexture.didUpdateThisFrame)
            {
                _hasReceivedFrame = true;
                Log($"SUCCESS: Frame received! Res: {_webCamTexture.width}x{_webCamTexture.height}");
                
                var renderer = GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.mainTexture = _webCamTexture;
                    Log("Texture assigned to Renderer.");
                }
            }
        }
    }

    void Log(string msg)
    {
        Debug.Log($"[CamTest] {msg}");
        _logBuffer = msg + "\n" + _logBuffer; // Prepends new messages at TOP
        
        if (_logBuffer.Length > 2000) 
            _logBuffer = _logBuffer.Substring(0, 2000);

        if (statusText != null)
        {
            statusText.text = _logBuffer;
        }
    }
}
