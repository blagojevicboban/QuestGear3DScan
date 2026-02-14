using UnityEngine;
using UnityEngine.UI;

public class DepthAccessTest : MonoBehaviour
{
    public Text statusText;
    private string _logBuffer = "";

    void Start()
    {
        if (statusText == null)
        {
            CreateDebugUI();
        }
    }

    void Update()
    {
        if (Time.frameCount % 30 == 0) // Update every 30 frames
        {
            UpdateStatus();
        }
    }

    void UpdateStatus()
    {
        string msg = "DEPTH TEST RUNNING\n";
        msg += "--------------------\n";
        msg += $"Headset: {OVRPlugin.GetSystemHeadsetType()}\n";
        msg += $"OVRManager: {(OVRManager.instance != null ? "FOUND" : "MISSING")}\n";
        
        bool ptSupported = OVRPlugin.IsInsightPassthroughSupported();
        msg += $"Passthrough Supported: {(ptSupported ? "<color=green>YES</color>" : "<color=red>NO</color>")}\n";

        if (OVRManager.instance != null)
        {
            msg += $"Passthrough Enabled: {OVRManager.instance.isInsightPassthroughEnabled}\n";
        }

        // Check specifically for Environment Depth availability
        // Note: Raw depth API access usually requires specific OVRCameraRig setup
        // But here we check if the system reports capability.
        
        // This is a basic check.
        // True depth requires OVRDepth or Passthrough layers.
        
        statusText.text = msg;
    }

    void CreateDebugUI()
    {
        GameObject canvasObj = new GameObject("DebugCanvas_Depth");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        if (Camera.main != null)
        {
            canvasObj.transform.SetParent(Camera.main.transform, false);
            canvasObj.transform.localPosition = new Vector3(0, 0, 0.5f);
            canvasObj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            canvasObj.transform.position = new Vector3(0, 0, 0.5f);
        }
        
        canvasObj.transform.localScale = Vector3.one * 0.001f;
        
        GameObject textObj = new GameObject("DebugText");
        textObj.transform.SetParent(canvasObj.transform, false);
        
        Text txt = textObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 26;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.rectTransform.sizeDelta = new Vector2(800, 600);
        txt.color = Color.cyan; // Cyan for Depth Test
        txt.supportRichText = true;

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        bgObj.transform.SetAsFirstSibling();
        Image img = bgObj.AddComponent<Image>();
        img.color = new Color(0, 0, 0.2f, 0.8f); // Dark Blue BG
        img.rectTransform.sizeDelta = new Vector2(820, 620);

        statusText = txt;
    }
}
