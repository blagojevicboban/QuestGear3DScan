using UnityEngine;
using UnityEngine.UI;
using QuestGear3D.Scan.Core;
using QuestGear3D.Scan.Integration;
using QuestGear3D.Scan.Data;

public class ScanDashboard : MonoBehaviour
{
    public ScanController scanController;
    public ScanFileServer fileServer;
    
    public Text statusText;
    public Button startButton;
    public Button stopButton;

    [Header("Mode UI")]
    public Button objectModeButton;
    public Button spaceModeButton;
    
    [Header("Advanced UI")]
    public GameObject advancedPanel;
    public Button advancedToggleButton;
    public Dropdown resolutionDropdown; // Use UI.Dropdown or TMPro.TMP_Dropdown
    public Slider fpsSlider;
    public Text fpsValueText;
    public Toggle flashlightToggle;
    public Slider delaySlider;
    public Text delayValueText;

    void Start()
    {
        if (scanController == null) scanController = FindObjectOfType<ScanController>();
        if (fileServer == null) fileServer = FindObjectOfType<ScanFileServer>();
        
        if (startButton != null) startButton.onClick.AddListener(OnStartScan);
        if (stopButton != null) stopButton.onClick.AddListener(OnStopScan);

        // Mode Bindings
        if (objectModeButton) objectModeButton.onClick.AddListener(() => SetMode(ScanMode.Object));
        if (spaceModeButton) spaceModeButton.onClick.AddListener(() => SetMode(ScanMode.Space));

        // Advanced Bindings
        if (advancedToggleButton) advancedToggleButton.onClick.AddListener(ToggleAdvancedPanel);
        if (resolutionDropdown) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        if (fpsSlider) fpsSlider.onValueChanged.AddListener(OnFPSChanged);
        if (flashlightToggle) flashlightToggle.onValueChanged.AddListener(OnFlashlightChanged);
        if (delaySlider) delaySlider.onValueChanged.AddListener(OnDelayChanged);

        // Init UI values from Controller defaults
        if (scanController != null)
        {
            if (fpsSlider) fpsSlider.value = scanController.targetFPS;
            if (flashlightToggle) flashlightToggle.isOn = scanController.useFlashlight;
            if (delaySlider) delaySlider.value = scanController.startDelay;
            UpdateModeUI();
        }
        
        UpdateUI();
    }

    void SetMode(ScanMode mode)
    {
        if (scanController) 
        {
            scanController.SetScanMode(mode); // Use method instead of property set
            // Auto-adjust defaults based on mode
            if (mode == ScanMode.Object) scanController.targetFPS = 30;
            if (mode == ScanMode.Space) scanController.targetFPS = 5;
            
            // Refresh UI
            if (fpsSlider) fpsSlider.value = scanController.targetFPS;
            UpdateModeUI();
        }
    }

    void UpdateModeUI()
    {
        if (!scanController) return;
        // Visual feedback for selected mode (e.g. disable button or change color)
        // For now just relying on Unity Button transition, but could colorize
        if (objectModeButton) objectModeButton.interactable = scanController.CurrentScanMode != ScanMode.Object;
        if (spaceModeButton) spaceModeButton.interactable = scanController.CurrentScanMode != ScanMode.Space;
    }
    void ToggleAdvancedPanel()
    {
        if (advancedPanel) advancedPanel.SetActive(!advancedPanel.activeSelf);
    }

    void OnResolutionChanged(int index)
    {
        // simplistic 0=High, 1=Med, 2=Low mapping
    }

    void OnFPSChanged(float value)
    {
        if (scanController) scanController.targetFPS = (int)value;
        if (fpsValueText) fpsValueText.text = $"FPS: {(int)value}";
    }

    void OnFlashlightChanged(bool value)
    {
        if (scanController) scanController.useFlashlight = value;
    }

    void OnDelayChanged(float value)
    {
        if (scanController) scanController.startDelay = value;
        if (delayValueText) delayValueText.text = $"Delay: {value:F1}s";
    }

    void OnStartScan()
    {
        if (scanController != null)
        {
            scanController.StartScan();
            UpdateUI();
        }
    }

    void OnStopScan()
    {
        if (scanController != null)
        {
            scanController.StopScan();
            UpdateUI();
        }
    }

    void Update()
    {
        // Poll state just in case
        if (scanController != null)
        {
            UpdateUI();
        }
    }

    void UpdateUI()
    {
        if (scanController == null) return;
        
        bool isScanning = scanController.IsScanning;
        float countdown = scanController.CurrentCountdown;
        
        if (startButton) startButton.interactable = !isScanning && countdown <= 0;
        if (stopButton) stopButton.interactable = isScanning;
        
        if (statusText)
        {
            if (countdown > 0)
            {
                statusText.text = $"Starting in {countdown:F1}...";
            }
            else if (isScanning)
            {
                int bufferCount = 0;
                if (scanController.dataManager != null) 
                    bufferCount = scanController.dataManager.PendingSaveCount; // PendingSaveCount property check if it exists
                    
                statusText.text = $"Scanning... (Buffer: {bufferCount})";
            }
            else
            {
                string serverInfo = "";
                if (fileServer != null && !string.IsNullOrEmpty(fileServer.ServerAddress))
                {
                    serverInfo = $"\nServer: {fileServer.ServerAddress}";
                }
                statusText.text = $"Ready (Mode: {scanController.CurrentScanMode}){serverInfo}";
            }
        }
    }
}
