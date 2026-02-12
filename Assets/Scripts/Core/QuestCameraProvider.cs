using UnityEngine;
using QuestGear3D.Scan.Core;

public class QuestCameraProvider : MonoBehaviour, IFrameProvider
{
    [Header("Camera Settings")]
    public int requestedWidth = 1280;
    public int requestedHeight = 720;
    public int requestedFPS = 30;

    [Header("References")]
    public Transform centerEyeAnchor; // Assign OVRCameraRig -> CenterEyeAnchor
    
    private WebCamTexture _webCamTexture;
    private Texture2D _latestColorTexture;
    private Texture2D _latestDepthTexture;
    private bool _isStreaming = false;
    private bool _hasNewFrame = false;

    // TODO: Implement OVRDepth access
    // private OVRDepth _ovrDepth;

    public void Initialize()
    {
        // Auto-find CenterEyeAnchor if not assigned
        if (centerEyeAnchor == null)
        {
            var cameraRig = FindObjectOfType<OVRCameraRig>();
            if (cameraRig != null)
            {
                centerEyeAnchor = cameraRig.centerEyeAnchor;
            }
            else
            {
                // Fallback to Main Camera
                if (Camera.main != null) centerEyeAnchor = Camera.main.transform;
            }
        }

        // Initialize WebCamTexture
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length > 0)
        {
            string camName = devices[0].name; // Use first available
            _webCamTexture = new WebCamTexture(camName, requestedWidth, requestedHeight, requestedFPS);
            
            // Create reusable texture buffer for output
            // Init with black, will resize on first frame
            _latestColorTexture = new Texture2D(16, 16, TextureFormat.RGB24, false);
            
            // Placeholder Depth (Gray)
            _latestDepthTexture = new Texture2D(16, 16, TextureFormat.R16, false); // 16-bit
            
            Debug.Log($"[QuestProvider] Initialized with camera: {camName}");
        }
        else
        {
            Debug.LogError("[QuestProvider] No Camera devices found!");
        }
    }

    public void StartStream()
    {
        if (_webCamTexture != null)
        {
            _webCamTexture.Play();
            _isStreaming = true;
            Debug.Log("[QuestProvider] Stream Started");
        }
    }

    public void StopStream()
    {
        if (_webCamTexture != null)
        {
            _webCamTexture.Stop();
        }
        _isStreaming = false;
        Debug.Log("[QuestProvider] Stream Stopped");
    }

    public bool HasNewFrame()
    {
        return _hasNewFrame;
    }

    public FrameData GetLatestFrame()
    {
        // Re-check stream status
        if (_webCamTexture == null || !_webCamTexture.isPlaying)
        {
            return new FrameData(); // Return empty struct
        }

        // Initialize textures if needed (lazy init)
        if (_latestColorTexture == null || _latestColorTexture.width != _webCamTexture.width || _latestColorTexture.height != _webCamTexture.height)
        {
             _latestColorTexture = new Texture2D(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGB24, false);
             _latestDepthTexture = new Texture2D(_webCamTexture.width, _webCamTexture.height, TextureFormat.R16, false);
        }

        // Apply pixels
        _latestColorTexture.SetPixels32(_webCamTexture.GetPixels32());
        _latestColorTexture.Apply();

        return new FrameData
        {
            ColorTexture = _latestColorTexture,
            DepthTexture = _latestDepthTexture,
            CameraPose = centerEyeAnchor != null ? centerEyeAnchor.localToWorldMatrix : Matrix4x4.identity,
            Timestamp = Time.realtimeSinceStartupAsDouble
        };
    }

    void Update()
    {
        if (_isStreaming && _webCamTexture != null && _webCamTexture.didUpdateThisFrame)
        {
            _hasNewFrame = true;
        }
    }
}
