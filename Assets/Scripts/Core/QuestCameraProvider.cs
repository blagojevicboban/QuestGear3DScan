using UnityEngine;
using UnityEngine.Android; // Required for Permission
using System.Collections;
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
    private bool _isInitialized = false;

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
                _latestDepthTexture.Reinitialize(_webCamTexture.width, _webCamTexture.height);
            }
            _latestColorTexture.SetPixels32(_webCamTexture.GetPixels32());
            _latestColorTexture.Apply();
            
            // TODO: Fill _latestDepthTexture with real data if available
        }

        return new FrameData
        {
            ColorTexture = _latestColorTexture,
            DepthTexture = _latestDepthTexture,
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
}
