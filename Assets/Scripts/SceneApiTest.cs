using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class SceneApiTest : MonoBehaviour
{
    public Text statusText;
    private string _logBuffer = "";
    private OVRSceneManager _sceneManager;

    void Start()
    {
        // Auto-create UI if missing
        if (statusText == null)
        {
            CreateDebugUI();
        }

        Log("Initializing Scene API Test...");
        _sceneManager = FindObjectOfType<OVRSceneManager>();

        if (_sceneManager != null)
        {
            Log("OVRSceneManager found.");
            _sceneManager.SceneModelLoadedSuccessfully += OnSceneModelLoadedSuccessfully;
            _sceneManager.NoSceneModelToLoad += OnNoSceneModelToLoad;
            _sceneManager.NewSceneModelAvailable += OnNewSceneModelAvailable;
            // _sceneManager.LoadSceneModelFailed += OnLoadSceneModelFailed; // Does not exist
            
            // Start the process automatically
            StartCoroutine(StartSceneProcess());
        }
        else
        {
            Log("ERROR: OVRSceneManager missing from scene!");
            Log("Please ensure OVRCameraRig is present and has OVRSceneManager.");
        }
    }

    void OnDestroy()
    {
        if (_sceneManager != null)
        {
            _sceneManager.SceneModelLoadedSuccessfully -= OnSceneModelLoadedSuccessfully;
            _sceneManager.NoSceneModelToLoad -= OnNoSceneModelToLoad;
            _sceneManager.NewSceneModelAvailable -= OnNewSceneModelAvailable;
// _sceneManager.LoadSceneModelFailed -= OnLoadSceneModelFailed;
        }
    }

    IEnumerator StartSceneProcess()
    {
        yield return new WaitForSeconds(2.0f);
        Log("Attempting to Load Scene Model...");
        _sceneManager.LoadSceneModel();
    }

    private void OnSceneModelLoadedSuccessfully()
    {
        Log("<color=green>Scene Model Loaded Successfully!</color>");
        StartCoroutine(AnalyzeSceneCoroutine());
    }

    private void OnNoSceneModelToLoad()
    {
        Log("<color=yellow>No Scene Model found.</color>");
        Log("Requesting Scene Capture (Room Setup)...");
        _sceneManager.RequestSceneCapture();
    }

    private void OnNewSceneModelAvailable()
    {
        Log("New Scene Model available. Reloading...");
        _sceneManager.LoadSceneModel();
    }

/*
    private void OnLoadSceneModelFailed()
    {
        Log("<color=red>Failed to load Scene Model.</color>");
    }
*/

    private IEnumerator AnalyzeSceneCoroutine()
    {
        yield return new WaitForSeconds(1.0f); // Wait for instantiation

        var rooms = FindObjectsOfType<OVRSceneRoom>();
        Log($"Found {rooms.Length} Room(s).");

        var planes = FindObjectsOfType<OVRScenePlane>();
        Log($"Found {planes.Length} Plane(s) (Walls/Floors).");
        
        var volumes = FindObjectsOfType<OVRSceneVolume>();
        Log($"Found {volumes.Length} Volume(s) (Furniture).");

        if (planes.Length > 0)
        {
            var p = planes[0];
            Log($"First Plane: {p.Width:F2}x{p.Height:F2}m");
            Log($"Has Mesh Filter: {(p.GetComponent<MeshFilter>() != null)}");
            Log($"Has Collider: {(p.GetComponent<Collider>() != null)}");
        }
    }

    void CreateDebugUI()
    {
        GameObject canvasObj = new GameObject("DebugCanvas_Scene");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        if (Camera.main != null)
        {
            canvasObj.transform.SetParent(Camera.main.transform, false);
            canvasObj.transform.localPosition = new Vector3(0, 0, 0.6f); // Slightly further
            canvasObj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            canvasObj.transform.position = new Vector3(0, 0, 0.6f);
        }
        
        canvasObj.transform.localScale = Vector3.one * 0.001f;
        
        GameObject textObj = new GameObject("DebugText");
        textObj.transform.SetParent(canvasObj.transform, false);
        
        Text txt = textObj.AddComponent<Text>();
        // Use standard font fallback
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 24;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
        txt.rectTransform.sizeDelta = new Vector2(800, 600);
        txt.color = Color.yellow; // Yellow/Orange for Scene Test
        txt.supportRichText = true;

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        bgObj.transform.SetAsFirstSibling();
        Image img = bgObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0, 0, 0.8f); // Dark Red BG
        img.rectTransform.sizeDelta = new Vector2(820, 620);

        statusText = txt;
    }

    void Log(string msg)
    {
        Debug.Log($"[SceneTest] {msg}");
        _logBuffer = msg + "\n" + _logBuffer;
        if (_logBuffer.Length > 2000) _logBuffer = _logBuffer.Substring(0, 2000);
        if (statusText != null) statusText.text = _logBuffer;
    }
}
