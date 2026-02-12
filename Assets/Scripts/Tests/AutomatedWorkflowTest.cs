using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using QuestGear3D.Scan.Core;
using QuestGear3D.Scan.Integration;

public class AutomatedWorkflowTest : MonoBehaviour
{
    public ScanController scanController;
    public ScanFileServer fileServer;
    public float testDuration = 3.0f; // Seconds to scan

    void Start()
    {
        StartCoroutine(RunTest());
    }

    IEnumerator RunTest()
    {
        Debug.Log("<b>[TEST] Starting E2E Workflow Test...</b>");

        // 1. Dependency Check
        if (scanController == null) scanController = FindObjectOfType<ScanController>();
        if (fileServer == null) fileServer = FindObjectOfType<ScanFileServer>();

        if (scanController == null) { Fail("ScanController not found!"); yield break; }
        
        // 2. Start Scan
        Debug.Log("[TEST] Starting Scan...");
        scanController.StartScan();
        
        if (!scanController.IsScanning) { Fail("Failed to start scan!"); yield break; }
        Debug.Log("[TEST] Scan Started. Waiting...");

        yield return new WaitForSeconds(testDuration);

        // 3. Stop Scan
        Debug.Log("[TEST] Stopping Scan...");
        scanController.StopScan();
        
        if (scanController.IsScanning) { Fail("Failed to stop scan!"); yield break; }

        // 4. Wait for Save Queue (Manual delay for now, ideally check PendingSaveCount)
        Debug.Log("[TEST] Waiting for IO...");
        yield return new WaitForSeconds(2.0f);
        
        // 5. Verify Files
        string path = Application.persistentDataPath + "/Scans";
        if (!Directory.Exists(path)) { Fail("Scans directory not created!"); yield break; }

        string[] dirs = Directory.GetDirectories(path);
        if (dirs.Length == 0) { Fail("No scan folder created!"); yield break; }
        
        // Get latest
        string latestScan = dirs[dirs.Length - 1];
        int fileCount = Directory.GetFiles(latestScan, "*", SearchOption.AllDirectories).Length;
        Debug.Log($"[TEST] Files found in {latestScan}: {fileCount}");
        
        if (fileCount < 5) { Fail("Too fewer files created! (Exp > 5)"); yield break; }

        // 6. Verify Server
        if (fileServer != null)
        {
            Debug.Log("[TEST] Testing HTTP Server...");
            string url = "http://localhost:8080/";
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Fail($"Server Request Failed: {www.error}");
                }
                else
                {
                    Debug.Log($"[TEST] Server Response: {www.downloadHandler.text.Substring(0, 50)}...");
                }
            }
        }
        else
        {
            Debug.LogWarning("[TEST] ScanFileServer not found, skipping HTTP test.");
        }

        Debug.Log("<b><color=green>[TEST] ALL CHECKS PASSED!</color></b>");
    }

    void Fail(string msg)
    {
        Debug.LogError($"<b><color=red>[TEST] FAILED: {msg}</color></b>");
    }
}
