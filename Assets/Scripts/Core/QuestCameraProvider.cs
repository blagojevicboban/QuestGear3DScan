using UnityEngine;
using UnityEngine.Android; // Required for Permission
using System.Collections;
using System;
using QuestGear3D.Scan.Core;
using QuestGear3D.Scan.Data;
// Note: We use reflection or loose coupling to avoid direct errors if namespace is missing during compile, 
// allows fallback. But for now we assume SDK is present as per task.
using Meta.XR.EnvironmentDepth; 

public class QuestCameraProvider : MonoBehaviour, IFrameProvider
{
    [Header("Camera Settings")]
    public int requestedWidth = 1280;
    public int requestedHeight = 720;
    public int requestedFPS = 30;

    [Header("Intrinsics")]
    // Fallback if API fails
    public float fallbackFOV = 90f; 
    private PinholeCameraIntrinsic _cachedIntrinsics;

    [Header("References")]
    public Transform centerEyeAnchor; // Assign OVRCameraRig -> CenterEyeAnchor
    
    private WebCamTexture _webCamTexture;
    private Texture2D _latestColorTexture;
    private Texture2D _latestDepthTexture;
    private bool _isStreaming = false;
    private bool _hasNewFrame = false;
    private bool _isInitialized = false;
    private bool _isDepthSupported = false;
    private EnvironmentDepthManager _depthManager;
    private RenderTexture _depthRT;
    private Texture2D _readableDepth;

    public void Initialize()
    {
        if (_isInitialized) return;

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

        StartCoroutine(AskAndCheckPermission());
    }

    IEnumerator AskAndCheckPermission()
    {
        if (Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            InitializeCameraDevice();
            CheckDepthSupport();
            yield break;
        }

        Debug.Log("[QuestProvider] Requesting Camera Permission...");
        Permission.RequestUserPermission(Permission.Camera);

        float timeout = 60f;
        while (timeout > 0)
        {
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("[QuestProvider] Permission Granted!");
                InitializeCameraDevice();
                CheckDepthSupport();
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
            timeout -= 0.5f;
        }

        Debug.LogError("[QuestProvider] Camera Permission Timed Out or Denied.");
    }

    private void InitializeCameraDevice()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length > 0)
        {
            string camName = devices[0].name;
            _webCamTexture = new WebCamTexture(camName, requestedWidth, requestedHeight, requestedFPS);
            
            _latestColorTexture = new Texture2D(16, 16, TextureFormat.RGB24, false);
            _latestDepthTexture = new Texture2D(16, 16, TextureFormat.R16, false);
            
            Debug.Log($"[QuestProvider] Initialized with camera: {camName}");
            _isInitialized = true;
        }
        else
        {
            Debug.LogWarning("[QuestProvider] No Camera devices found! Using synthetic fallback.");
            _latestColorTexture = new Texture2D(requestedWidth, requestedHeight, TextureFormat.RGB24, false);
             // Fill magenta
            var cols = _latestColorTexture.GetPixels();
            for(int i=0; i<cols.Length; ++i) cols[i] = Color.magenta;
            _latestColorTexture.SetPixels(cols);
            _latestColorTexture.Apply();

            _latestDepthTexture = new Texture2D(requestedWidth, requestedHeight, TextureFormat.R16, false);
            _isInitialized = true;
        }
    }

    private void CheckDepthSupport()
    {
        // Check for EnvironmentDepthManager in scene
        _depthManager = FindObjectOfType<EnvironmentDepthManager>();
        if (_depthManager == null)
        {
            // Try to create it if supported? 
            // Better to warn user to setup or add it dynamically if we are sure.
            if (EnvironmentDepthManager.IsSupported)
            {
                var obj = new GameObject("EnvironmentDepthManager");
                _depthManager = obj.AddComponent<EnvironmentDepthManager>();
                Debug.Log("[QuestProvider] Created EnvironmentDepthManager.");
            }
        }

        if (_depthManager != null && EnvironmentDepthManager.IsSupported)
        {
            _isDepthSupported = true;
            _depthManager.enabled = true; // Ensure it's on
            Debug.Log("[QuestProvider] Environment Depth Supported and Manager found.");
        }
        else
        {
            Debug.LogWarning("[QuestProvider] Depth API NOT Supported or Manager missing.");
        }
    }


    public void StartStream()
    {
        if (!_isInitialized) 
        {
            Debug.LogWarning("[QuestProvider] Cannot start stream: Not Initialized.");
            Initialize(); // Try initializing
            return;
        }

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
        if (_webCamTexture != null && _webCamTexture.isPlaying)
        {
            // Only update texture on GPU if size changed or just to refresh
            // Ideally we blit or copy. For now setPixels is slow but functional.
            if (_latestColorTexture.width != _webCamTexture.width || _latestColorTexture.height != _webCamTexture.height)
            {
                _latestColorTexture.Reinitialize(_webCamTexture.width, _webCamTexture.height);
                // Depth texture initialization is handled separately via OVR API
            }
            _latestColorTexture.SetPixels32(_webCamTexture.GetPixels32());
            _latestColorTexture.Apply();

            // Fetch Environment Depth if available
            if (_isDepthSupported && _depthManager != null && _depthManager.IsDepthAvailable)
            {
                // Retrieve texture from Global Shader Property set by EnvironmentDepthManager
                var globalDepthTex = Shader.GetGlobalTexture("_EnvironmentDepthTexture") as RenderTexture;
                
                if (globalDepthTex != null)
                {
                    // Copy to CPU readable texture
                    if (_depthRT == null || _depthRT.width != globalDepthTex.width || _depthRT.height != globalDepthTex.height)
                    {
                        if (_depthRT != null) _depthRT.Release();
                        _depthRT = new RenderTexture(globalDepthTex.width, globalDepthTex.height, 0, RenderTextureFormat.R16);
                    }
                    
                    Graphics.Blit(globalDepthTex, _depthRT);
                    
                    if (_readableDepth == null || _readableDepth.width != _depthRT.width || _readableDepth.height != _depthRT.height)
                    {
                        _readableDepth = new Texture2D(_depthRT.width, _depthRT.height, TextureFormat.R16, false);
                    }
                    
                    RenderTexture.active = _depthRT;
                    _readableDepth.ReadPixels(new Rect(0, 0, _depthRT.width, _depthRT.height), 0, 0);
                    _readableDepth.Apply();
                    RenderTexture.active = null;
                }
            }
        }

        return new FrameData
        {
            ColorTexture = _latestColorTexture,
            DepthTexture = _readableDepth != null ? _readableDepth : _latestDepthTexture, // Fallback to non-readable if readable not valid
            CameraPose = centerEyeAnchor != null ? centerEyeAnchor.localToWorldMatrix : Matrix4x4.identity,
            Timestamp = Time.realtimeSinceStartupAsDouble
        };
    }

    // Helper for UI to preview the raw feed
    public Texture GetPreviewTexture()
    {
        return _webCamTexture != null ? _webCamTexture : _latestColorTexture;
    }

    void Update()
    {
        if (_isStreaming && _webCamTexture != null && _webCamTexture.didUpdateThisFrame)
        {
            _hasNewFrame = true;
        }
    }

    public void SetResolution(int width, int height)
    {
        if (requestedWidth == width && requestedHeight == height) return;
        requestedWidth = width;
        requestedHeight = height;
        if (_isStreaming) RestartStream();
    }

    public void SetFPS(int fps)
    {
        if (requestedFPS == fps) return;
        requestedFPS = fps;
        if (_isStreaming) RestartStream();
    }

    public void SetFlashlight(bool enabled)
    {
        Debug.Log($"[QuestCameraProvider] Flashlight {(enabled ? "ON" : "OFF")} (Not implemented)");
    }

    private void RestartStream()
    {
        StopStream();
        InitializeCameraDevice(); 
        StartStream();
    }

    public PinholeCameraIntrinsic GetIntrinsics()
    {
        if (_cachedIntrinsics != null) return _cachedIntrinsics;

        // Try to get from OVRPlugin
        // We use Node.EyeCenter as proxy for passthrough camera in many cases if they align?
        // Actually, passthrough is a separate system.
        // OVRPlugin.GetCameraDeviceIntrinsicsParameters is for Insight cameras (tracking).
        
        // Best approach for WebCamTexture in Unity:
        // Use the resolution we asked for.
        // Assume standard FOV or try to derive.
        // Quest 3 Passthrough FOV is roughly similar to display FOV but cropped.
        // A standard value often used for Quest Passthrough recordings is around 80-90 degrees depending on mode.
        
        // Let's create a reasonable approximation.
        float width = _latestColorTexture != null ? _latestColorTexture.width : requestedWidth;
        float height = _latestColorTexture != null ? _latestColorTexture.height : requestedHeight;
        
        // If centerEyeAnchor has a Camera component, we might use its FOV if it matches passthrough underlay.
        float fovY = fallbackFOV;
        if (centerEyeAnchor != null)
        {
            var cam = centerEyeAnchor.GetComponent<Camera>();
            if (cam != null) fovY = cam.fieldOfView;
        }

        // Calculate focal length from vertical FOV
        // f = (h / 2) / tan(fov / 2)
        float fy = (height / 2.0f) / Mathf.Tan(fovY * 0.5f * Mathf.Deg2Rad);
        float fx = fy; // Square pixels assumption
        float cx = width / 2.0f;
        float cy = height / 2.0f;

        _cachedIntrinsics = new PinholeCameraIntrinsic((int)width, (int)height, fx, fy, cx, cy);
        return _cachedIntrinsics;
    }
}
