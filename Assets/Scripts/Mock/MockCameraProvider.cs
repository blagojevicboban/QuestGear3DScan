using UnityEngine;
using QuestGear3D.Scan.Data;
using QuestGear3D.Scan.Core;

// Rename class or keep same? better keep same but implement interface
public class MockCameraProvider : MonoBehaviour, IFrameProvider
{
    // Removed direct reference to dataManager. Controller handles logic.
    // public ScanDataManager dataManager; 
    
    public Transform targetPivot;
    public float rotationSpeed = 20f;
    
    public Camera mockCamera;
    public RenderTexture colorRT;
    public RenderTexture depthRT; // Optional
    
    private bool _isStreaming = false; // Changed from _isScanning
    private bool _hasNewFrame = false;

    // Interface Implementation
    public void Initialize()
    {
        // Setup RTs if not assigned
        if (colorRT == null)
        {
            colorRT = new RenderTexture(1280, 720, 24);
            colorRT.Create();
        }
        if (mockCamera != null)
        {
            mockCamera.targetTexture = colorRT;
        }
        Debug.Log("[MockProvider] Initialized");
    }

    public void StartStream()
    {
        _isStreaming = true;
        Debug.Log("[MockProvider] Stream Started");
    }

    public void StopStream()
    {
        _isStreaming = false;
        Debug.Log("[MockProvider] Stream Stopped");
    }

    public bool HasNewFrame()
    {
        return _hasNewFrame;
    }

    public FrameData GetLatestFrame()
    {
        _hasNewFrame = false; // Reset flag
        
        // Snapshot the RT
        // Note: Creating new Texture2D every frame is perf heavy, but for Mock/Editor it's fine.
        // In production we would reuse buffers.
        Texture2D colorTex = ToTexture2D(colorRT);
        
        // Mock Depth (16-bit)
        Texture2D depthTex = new Texture2D(1280, 720, TextureFormat.R16, false);
        
        return new FrameData
        {
            ColorTexture = colorTex,
            DepthTexture = depthTex,
            CameraPose = mockCamera.transform.localToWorldMatrix,
            Timestamp = Time.realtimeSinceStartupAsDouble
        };
    }

    void Update()
    {
        // Simulate Camera Movement regardless of scanning state?
        // Or only when streaming? Let's do only when streaming for now.
        if (_isStreaming)
        {
            // Orbit
            if (targetPivot != null)
            {
                transform.RotateAround(targetPivot.position, Vector3.up, rotationSpeed * Time.deltaTime);
                transform.LookAt(targetPivot);
            }
            
            // Mark new frame available every frame (or throttle if needed)
            _hasNewFrame = true;
        }
    }

    Texture2D ToTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGB24, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;
    }

    // New Interface Methods
    public void SetResolution(int width, int height)
    {
        if (colorRT != null && (colorRT.width != width || colorRT.height != height))
        {
            colorRT.Release();
            colorRT = new RenderTexture(width, height, 24);
            colorRT.Create();
            if (mockCamera != null) mockCamera.targetTexture = colorRT;
            Debug.Log($"[MockProvider] Set Resolution: {width}x{height}");
        }
    }

    public void SetFPS(int fps)
    {
         Debug.Log($"[MockProvider] Set FPS: {fps}");
    }

    public void SetFlashlight(bool enabled)
    {
         Debug.Log($"[MockProvider] Set Flashlight: {enabled}");
    }
}
