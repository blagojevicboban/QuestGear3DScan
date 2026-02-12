using UnityEngine;
using UnityEngine.UI;
using QuestGear3D.Scan.Data;

public class ScanDashboard : MonoBehaviour
{
    public ScanDataManager scanManager;
    public Text statusText;
    public Button startButton;
    public Button stopButton;
    
    // Optional: Reference to Mock provider to toggle it directly
    public MockCameraProvider mockProvider;

    void Start()
    {
        if (scanManager == null) scanManager = FindObjectOfType<ScanDataManager>();
        
        if (startButton != null) startButton.onClick.AddListener(OnStartScan);
        if (stopButton != null) stopButton.onClick.AddListener(OnStopScan);
        
        UpdateUI(false);
    }

    void OnStartScan()
    {
        if (mockProvider != null)
        {
            mockProvider.ToggleScan(); // Mock handles start internally
        }
        else
        {
            scanManager.StartNewScan();
        }
        UpdateUI(true);
    }

    void OnStopScan()
    {
        if (mockProvider != null)
        {
            mockProvider.ToggleScan(); // Mock handles stop
        }
        else
        {
            scanManager.StopScan();
        }
        UpdateUI(false);
    }

    void UpdateUI(bool isScanning)
    {
        if (startButton) startButton.interactable = !isScanning;
        if (stopButton) stopButton.interactable = isScanning;
        if (statusText) statusText.text = isScanning ? "Scanning..." : "Ready to Scan";
    }

    void Update()
    {
        // Update FPS or Frame count if scanning
        // We'd need to expose frame count from manager
    }
}
