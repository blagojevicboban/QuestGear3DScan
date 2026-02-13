using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using QuestGear3D.Scan.Core;
using QuestGear3D.Scan.Data;
using QuestGear3D.Scan.Integration;

public class SceneSetupTool : EditorWindow
{
    [MenuItem("Tools/Setup Scan Scene")]
    public static void SetupScene()
    {
        Debug.Log("Setting up Scene for QuestGear 3D Scan...");

        // 1. Create Core Controller
        GameObject controllerObj = new GameObject("[ScanController]");
        var controller = controllerObj.AddComponent<ScanController>();
        var dataManager = controllerObj.AddComponent<ScanDataManager>();
        controller.dataManager = dataManager;

        // 2. Create Camera Provider (Quest Camera)
        GameObject cameraObj = new GameObject("[QuestCamera]");
        var provider = cameraObj.AddComponent<QuestCameraProvider>();
        
        // Link Controller to Provider
        controller.frameProviderObject = provider;

        // Ensure we have a Camera component for visualization (optional in production, good for dev)
        if (Camera.main == null)
        {
             var cam = cameraObj.AddComponent<Camera>();
             cam.tag = "MainCamera";
             cameraObj.AddComponent<AudioListener>();
        }
        else
        {
             // If main camera exists, maybe attach provider there? 
             // For now, let's keep separate logic or reuse existing main camera
             provider.centerEyeAnchor = Camera.main.transform;
        }

        // 3. Create File Server
        GameObject serverObj = new GameObject("[FileServer]");
        var server = serverObj.AddComponent<ScanFileServer>();

        // 4. Create UI (Canvas)
        GameObject canvasObj = new GameObject("[UI_Canvas]");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Dashboard Logic
        GameObject dashboardObj = new GameObject("ScanDashboard");
        dashboardObj.transform.SetParent(canvasObj.transform, false);
        var dashboard = dashboardObj.AddComponent<ScanDashboard>();
        dashboard.scanController = controller;
        dashboard.fileServer = server;

        // Create UI Elements
        // Background Panel
        GameObject panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(dashboardObj.transform, false);
        var panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.5f);
        panelObj.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
        panelObj.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.2f); // Bottom 20%
        panelObj.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        panelObj.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        // Status Text
        GameObject textObj = new GameObject("StatusText");
        textObj.transform.SetParent(panelObj.transform, false);
        var statusText = textObj.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.text = "Ready to Scan";
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = Color.white;
        statusText.rectTransform.anchorMin = new Vector2(0, 0.5f);
        statusText.rectTransform.anchorMax = new Vector2(1, 1);
        statusText.rectTransform.offsetMin = new Vector2(20, 0);
        statusText.rectTransform.offsetMax = new Vector2(-20, 0);
        dashboard.statusText = statusText;

        // Start Button
        GameObject startBtnObj = CreateButton("StartButton", "START SCAN", new Vector2(-100, -20));
        startBtnObj.transform.SetParent(panelObj.transform, false);
        var startBtn = startBtnObj.GetComponent<Button>();
        startBtn.onClick.AddListener(() => controller.StartScan());
        dashboard.startButton = startBtn;

        // Stop Button
        GameObject stopBtnObj = CreateButton("StopButton", "STOP SCAN", new Vector2(100, -20));
        stopBtnObj.transform.SetParent(panelObj.transform, false);
        var stopBtn = stopBtnObj.GetComponent<Button>();
        stopBtn.onClick.AddListener(() => controller.StopScan());
        dashboard.stopButton = stopBtn; // Note: UI script might need re-linking in editor for onClick, verify functionality

        // 5. Create EventSystem
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        Debug.Log("Scene Setup Complete! Hierarchy Created.");
    }

    private static GameObject CreateButton(string name, string text, Vector2 anchoredPos)
    {
        GameObject buttonObj = new GameObject(name);
        var img = buttonObj.AddComponent<Image>();
        img.color = Color.white;
        var btn = buttonObj.AddComponent<Button>();
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        var txt = textObj.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.color = Color.black;
        txt.alignment = TextAnchor.MiddleCenter;
        
        RectTransform rt = buttonObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160, 40);
        rt.anchoredPosition = anchoredPos;
        
        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        
        return buttonObj;
    }
}
