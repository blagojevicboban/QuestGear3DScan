using UnityEngine;
using QuestGear3D.Scan.Data;

public class MockCameraProvider : MonoBehaviour
{
    public ScanDataManager dataManager;
    public Transform targetPivot;
    public float rotationSpeed = 20f;
    
    public Camera mockCamera;
    public RenderTexture colorRT;
    public RenderTexture depthRT;
    
    private bool _isScanning = false;
    private float _timer = 0f;
    public float captureInterval = 0.5f; // 2 FPS for testing

    void Start()
    {
        if (dataManager == null) dataManager = FindObjectOfType<ScanDataManager>();
        
        // Setup RTs if not assigned
        if (colorRT == null)
        {
            colorRT = new RenderTexture(1280, 720, 24);
            colorRT.Create();
        }
        mockCamera.targetTexture = colorRT;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleScan();
        }

        if (_isScanning)
        {
            // Orbit
            if (targetPivot != null)
            {
                transform.RotateAround(targetPivot.position, Vector3.up, rotationSpeed * Time.deltaTime);
                transform.LookAt(targetPivot);
            }

            _timer += Time.deltaTime;
            if (_timer >= captureInterval)
            {
                Capture();
                _timer = 0f;
            }
        }
    }

    public void ToggleScan()
    {
        _isScanning = !_isScanning;
        if (_isScanning)
        {
            dataManager.StartNewScan();
            Debug.Log("Mock Scan Started (Press Space to Stop)");
        }
        else
        {
            dataManager.StopScan();
            Debug.Log("Mock Scan Stopped");
        }
    }

    void Capture()
    {
        // Snapshot the RT
        Texture2D colorTex = ToTexture2D(colorRT);
        // For depth, we just use the same image for now or a solid color if depthRT is null
        Texture2D depthTex = new Texture2D(1280, 720, TextureFormat.R16, false); // 16-bit mock
        
        // Pass to manager
        // CameraToWorld matrix
        Matrix4x4 camToWorld = mockCamera.transform.localToWorldMatrix;
        
        dataManager.CaptureFrame(colorTex, depthTex, camToWorld);
        
        // Cleanup temp textures? 
        // In real app we reuse buffers, here we rely on GC for simplicity in mock
        // destroy immediate to avoid leak in editor
        Destroy(colorTex);
        Destroy(depthTex);
    }
    
    Texture2D ToTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGB24, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;
    }
}
