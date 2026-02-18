using UnityEngine;
using UnityEngine.Android; // Required for Permission
using System.Collections;
using System;
using System.Runtime.InteropServices;
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
    private Coroutine _resumeCoroutine;
    private const float RESUME_DELAY = 0.5f;

    [Header("External Depth (Optional)")]
    public GameObject externalDepthSource; 
    private QuestGear3D.Scan.Sensors.IDepthProvider _externalDepthProvider;


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
        
        // Initialize External Depth if assigned
        if (externalDepthSource != null)
        {
            _externalDepthProvider = externalDepthSource.GetComponent<QuestGear3D.Scan.Sensors.IDepthProvider>();
            if (_externalDepthProvider != null)
            {
                _externalDepthProvider.Initialize();
                Debug.Log("[QuestProvider] External Depth Provider Initialized.");
            }
            else
            {
                Debug.LogWarning("[QuestProvider] External Depth Source assigned but no IDepthProvider found!");
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
        if (devices.Length > 1) // Try Camera 1 (tracking camera) instead of Camera 0
        {
            string camName = devices[1].name; // Camera 1 instead of 0
            _webCamTexture = new WebCamTexture(camName, requestedWidth, requestedHeight, requestedFPS);
            
            _latestColorTexture = new Texture2D(16, 16, TextureFormat.RGB24, false);
            _latestDepthTexture = new Texture2D(16, 16, TextureFormat.R16, false);
            
            Debug.Log($"[QuestProvider] Initialized with camera: {camName} (INDEX 1)");
            _isInitialized = true;
        }
        else if (devices.Length > 0)
        {
            // Fallback to Camera 0 if only one exists
            string camName = devices[0].name;
            _webCamTexture = new WebCamTexture(camName, requestedWidth, requestedHeight, requestedFPS);
            
            _latestColorTexture = new Texture2D(16, 16, TextureFormat.RGB24, false);
            _latestDepthTexture = new Texture2D(16, 16, TextureFormat.R16, false);
            
            Debug.Log($"[QuestProvider] Initialized with camera: {camName} (INDEX 0 FALLBACK)");
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
            // Still proceed to start stream coroutine, Initialize is async-ish
        }
        
        _isStreaming = true;
        StartCoroutine(StartStreamRoutine());
    }

    private IEnumerator StartStreamRoutine()
    {
        int attempts = 0;
        while (_isStreaming && attempts < 5)
        {
            if (_webCamTexture != null)
            {
                if (!_webCamTexture.isPlaying)
                {
                    _webCamTexture.Play();
                    Debug.Log($"[QuestProvider] Requesting WebCam Play... (Attempt {attempts + 1})");
                }
                else
                {
                    Debug.Log("[QuestProvider] Stream is PLAYING!");
                    yield break; // Success
                }
            }
            else
            {
                // Re-init if null
                InitializeCameraDevice();
            }
            
            yield return new WaitForSeconds(1.0f);
            attempts++;
        }
        
        if (_isStreaming && (_webCamTexture == null || !_webCamTexture.isPlaying))
        {
            Debug.LogError("[QuestProvider] FAILED TO START WEBCAM STREAM! Check Permissions/Device Support.");
            // We might still proceed with Depth if available?
        }
    }

    public void StopStream()
    {
        _isStreaming = false;
        StopAllCoroutines(); // Stop retry loop
        if (_webCamTexture != null)
        {
            _webCamTexture.Stop();
        }
        Debug.Log("[QuestProvider] Stream Stopped");
    }

    public bool HasNewFrame()
    {
        return _hasNewFrame;
    }

    public FrameData GetLatestFrame()
    {
        // 1. Process Color (if available)
        if (_webCamTexture != null && _webCamTexture.isPlaying)
        {
            if (_latestColorTexture.width != _webCamTexture.width || _latestColorTexture.height != _webCamTexture.height)
            {
                _latestColorTexture.Reinitialize(_webCamTexture.width, _webCamTexture.height);
            }
            _latestColorTexture.SetPixels32(_webCamTexture.GetPixels32());
            _latestColorTexture.Apply();
        }
        else
        {
            // Fallback Visualization if Camera is BLOCKED/DEAD
            if (_latestColorTexture.width != requestedWidth) 
                _latestColorTexture.Reinitialize(requestedWidth, requestedHeight);
            
            // Fill with solid color to prove pipeline is alive
            // Optimized: Create array once if possible, but here just simplistic fill
            var colors = _latestColorTexture.GetPixels32();
            Color32 fill = new Color32(0, 0, 200, 255); // Dark Blue
            for(int i=0; i<colors.Length; i++) colors[i] = fill;
            _latestColorTexture.SetPixels32(colors);
            _latestColorTexture.Apply();
        }

        // 2. Process Depth (Priority: External > Internal)
        if (_externalDepthProvider != null && _externalDepthProvider.IsReady())
        {
            _readableDepth = _externalDepthProvider.GetDepthTexture();
        }
        else if (_isDepthSupported && _depthManager != null && _depthManager.IsDepthAvailable)
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
            else
            {
                // Debug.LogWarning("[QuestProvider] _EnvironmentDepthTexture is NULL!"); 
                // This might happen if Depth API didn't init fully yet.
            }
        }
        
        return new FrameData
        {
            ColorTexture = _latestColorTexture,
            DepthTexture = _readableDepth != null ? _readableDepth : _latestDepthTexture,
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
        if (_isStreaming)
        {
            // If WebCam has new frame OR we have Depth available and some time passed (simulate 30fps)
            // Ideally we sync with Color. If Color is dead, we rely on Depth.
            bool colorReady = (_webCamTexture != null && _webCamTexture.didUpdateThisFrame);
            bool depthReady = (_isDepthSupported && _depthManager != null && _depthManager.IsDepthAvailable);
            
            // If color is ready, great.
            if (colorReady) 
            {
                _hasNewFrame = true;
            }
            // If color failed but depth is ready, treat as new frame (maybe throttle?)
            else if (depthReady && !_hasNewFrame)
            {
                // Simple throttle to avoid spamming if color is dead
                 _hasNewFrame = true; 
            }
        }
        else
        {
            _hasNewFrame = false;
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
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass cameraManagerClass = new AndroidJavaClass("android.hardware.camera2.CameraManager"))
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject cameraManager = activity.Call<AndroidJavaObject>("getSystemService", "camera"))
            {
                string[] cameraIds = cameraManager.Call<string[]>("getCameraIdList");
                if (cameraIds != null && cameraIds.Length > 0)
                {
                    cameraManager.Call("setTorchMode", cameraIds[0], enabled);
                    Debug.Log($"[QuestCameraProvider] Flashlight {(enabled ? "ON" : "OFF")}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[QuestCameraProvider] Flashlight failed: {e.Message}");
        }
#else
        Debug.Log($"[QuestCameraProvider] Flashlight {(enabled ? "ON" : "OFF")} (Editor — no-op)");
#endif
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

        // Try OVRPlugin API first for real intrinsics
        try
        {
            bool success = OVRPlugin.GetNodeFrustum2(OVRPlugin.Node.EyeCenter, out OVRPlugin.Frustumf2 frustum);
            if (success && frustum.Fov.UpTan > 0)
            {
                float width = (_latestColorTexture != null && _latestColorTexture.width > 16) ? _latestColorTexture.width : requestedWidth;
                float height = (_latestColorTexture != null && _latestColorTexture.height > 16) ? _latestColorTexture.height : requestedHeight;

                // Derive focal length from frustum tangent values
                float fy = height / (frustum.Fov.UpTan + frustum.Fov.DownTan);
                float fx = width / (frustum.Fov.LeftTan + frustum.Fov.RightTan);
                float cx = frustum.Fov.LeftTan * fx;
                float cy = frustum.Fov.UpTan * fy;

                _cachedIntrinsics = new PinholeCameraIntrinsic((int)width, (int)height, fx, fy, cx, cy);
                Debug.Log($"[QuestProvider] Intrinsics from OVRPlugin: fx={fx:F1}, fy={fy:F1}, cx={cx:F1}, cy={cy:F1}");
                return _cachedIntrinsics;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[QuestProvider] OVRPlugin intrinsics failed, using fallback: {e.Message}");
        }

        // Fallback: estimate from FOV
        float w = (_latestColorTexture != null && _latestColorTexture.width > 16) ? _latestColorTexture.width : requestedWidth;
        float h = (_latestColorTexture != null && _latestColorTexture.height > 16) ? _latestColorTexture.height : requestedHeight;
        
        float fovY = fallbackFOV;
        if (centerEyeAnchor != null)
        {
            var cam = centerEyeAnchor.GetComponent<Camera>();
            if (cam != null) fovY = cam.fieldOfView;
        }

        float fallbackFy = (h / 2.0f) / Mathf.Tan(fovY * 0.5f * Mathf.Deg2Rad);
        float fallbackFx = fallbackFy;
        float fallbackCx = w / 2.0f;
        float fallbackCy = h / 2.0f;

        _cachedIntrinsics = new PinholeCameraIntrinsic((int)w, (int)h, fallbackFx, fallbackFy, fallbackCx, fallbackCy);
        Debug.Log($"[QuestProvider] Intrinsics from FOV fallback: fx={fallbackFx:F1}, fy={fallbackFy:F1}");
        return _cachedIntrinsics;
    }

    public void LogDiagnostics(string folderPath)
    {
        string logPath = System.IO.Path.Combine(folderPath, "debug_camera_log.txt");
        using (System.IO.StreamWriter writer = new System.IO.StreamWriter(logPath))
        {
            writer.WriteLine($"--- QuestCameraProvider Diagnostic Log ---");
            writer.WriteLine($"Time: {System.DateTime.Now}");
            writer.WriteLine($"Initialized: {_isInitialized}");
            writer.WriteLine($"Selected Camera: {(_webCamTexture != null ? _webCamTexture.deviceName : "NONE")}");
            writer.WriteLine($"Requested Size: {requestedWidth}x{requestedHeight} @ {requestedFPS}fps");
            if (_webCamTexture != null)
            {
                writer.WriteLine($"Actual Texture Size: {_webCamTexture.width}x{_webCamTexture.height}");
                writer.WriteLine($"IsPlaying: {_webCamTexture.isPlaying}");
            }

            writer.WriteLine($"\n--- Available Devices ---");
            var devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                writer.WriteLine("NO DEVICES FOUND via WebCamTexture.devices!");
            }
            else
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    writer.WriteLine($"Device [{i}]: {devices[i].name} (FrontFacing: {devices[i].isFrontFacing})");
                }
            }

            writer.WriteLine($"\n--- Depth Status ---");
            writer.WriteLine($"IsDepthSupported: {_isDepthSupported}");
            if (_depthManager != null)
            {
                writer.WriteLine($"Manager Enabled: {_depthManager.enabled}");
                writer.WriteLine($"IsDepthAvailable: {_depthManager.IsDepthAvailable}");
            }
            else
            {
                writer.WriteLine("DepthManager is NULL");
            }
            
            writer.WriteLine($"\n--- Permission Status ---");
            writer.WriteLine($"Camera Permission Granted: {Permission.HasUserAuthorizedPermission(Permission.Camera)}");
        }
        Debug.Log($"[QuestProvider] Diagnostics written to: {logPath}");
    }

    /// <summary>
    /// Handles app pause/resume lifecycle for camera resource management.
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // App going to background — release camera
            if (_resumeCoroutine != null)
            {
                StopCoroutine(_resumeCoroutine);
                _resumeCoroutine = null;
            }
            
            if (_isStreaming)
            {
                Debug.Log("[QuestProvider] App pausing — stopping camera stream");
                if (_webCamTexture != null && _webCamTexture.isPlaying)
                {
                    _webCamTexture.Stop();
                }
            }
        }
        else
        {
            // App resuming — delay camera restart to avoid rapid cycles
            if (_isStreaming)
            {
                if (_resumeCoroutine != null)
                {
                    StopCoroutine(_resumeCoroutine);
                }
                _resumeCoroutine = StartCoroutine(DelayedResume());
            }
        }
    }

    private IEnumerator DelayedResume()
    {
        Debug.Log($"[QuestProvider] App resuming — waiting {RESUME_DELAY}s before restarting camera...");
        yield return new WaitForSeconds(RESUME_DELAY);
        
        if (_isStreaming && _webCamTexture != null && !_webCamTexture.isPlaying)
        {
            _webCamTexture.Play();
            Debug.Log("[QuestProvider] Camera restarted after resume");
        }
        
        _resumeCoroutine = null;
    }

    private void OnDestroy()
    {
        if (_isStreaming)
        {
            StopStream();
        }
        
        if (_depthRT != null)
        {
            _depthRT.Release();
        }
    }
}
