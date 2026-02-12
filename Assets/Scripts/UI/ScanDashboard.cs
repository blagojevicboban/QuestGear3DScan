using UnityEngine;
using UnityEngine.UI;
using QuestGear3D.Scan.Core;

public class ScanDashboard : MonoBehaviour
{
    public ScanController scanController;
    public Text statusText;
    public Button startButton;
    public Button stopButton;

    void Start()
    {
        if (scanController == null) scanController = FindObjectOfType<ScanController>();
        
        if (startButton != null) startButton.onClick.AddListener(OnStartScan);
        if (stopButton != null) stopButton.onClick.AddListener(OnStopScan);
        
        UpdateUI();
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

    void UpdateUI()
    {
        if (scanController == null) return;
        
        bool isScanning = scanController.IsScanning;
        
        if (startButton) startButton.interactable = !isScanning;
        if (stopButton) stopButton.interactable = isScanning;
        if (statusText) statusText.text = isScanning ? "Scanning..." : "Ready to Scan";
    }

    void Update()
    {
        // Poll state just in case it changes externally
        if (scanController != null && Time.frameCount % 10 == 0)
        {
            UpdateUI();
        }
    }
}
