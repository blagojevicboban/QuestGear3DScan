using UnityEngine;
using System.Collections;
using QuestGear3D.Scan.Data;

namespace QuestGear3D.Scan.Core
{
    public class ScanController : MonoBehaviour
    {
        [Header("Scan Configuration")]
        [field: SerializeField] // Show property in inspector
        public ScanMode CurrentScanMode { get; private set; } = ScanMode.Object;

        public Vector2Int captureResolution = new Vector2Int(1280, 720); // Default to verified HD
        [Range(1, 60)] public int targetFPS = 30;
        public bool useFlashlight = false;
        [Range(0f, 10f)] public float startDelay = 0f;

        [Header("Dependencies")]
        public ScanDataManager dataManager;
        
        [Tooltip("Centralized capture timer for synchronized frame timing.")]
        public CaptureTimer captureTimer;
        
        // Use direct reference to concrete component or interface wrapper if possible
        // For simplicity with Inspector, we use MonoBehaviour then cast.
        public Component frameProviderObject; 
        private IFrameProvider _frameProvider;
        
        // Scene API Reference
        public OVRSceneManager sceneManager;

        // Corrected Properties to match existing API (Capitalized)
        [field: Header("Status")]
        public bool IsScanning { get; private set; }
        public float CurrentCountdown { get; private set; }
        
        /// <summary>True when Space Mode scene model has loaded and is ready to capture.</summary>
        public bool IsSceneModelLoaded { get; private set; }
        private bool _waitingForSceneCapture = false;
        private Coroutine _roomCaptureRetry;

        void Awake()
        {
            if (frameProviderObject != null)
            {
                _frameProvider = frameProviderObject as IFrameProvider;
                if (_frameProvider == null)
                {
                    Debug.LogError("[ScanController] Frame Provider Object assigned but does not implement IFrameProvider!");
                }
            }
            else
            {
                // Auto-find if missing
                _frameProvider = GetComponentInChildren<IFrameProvider>();
                if (_frameProvider == null) _frameProvider = FindObjectOfType<QuestCameraProvider>();
                
                if (_frameProvider != null)
                {
                    Debug.Log("[ScanController] Auto-found Frame Provider.");
                }
            }
            
            if (dataManager == null)
            {
                dataManager = FindObjectOfType<ScanDataManager>();
            }

            if (sceneManager == null)
            {
                sceneManager = FindObjectOfType<OVRSceneManager>();
            }

            if (captureTimer == null)
            {
                captureTimer = FindObjectOfType<CaptureTimer>();
            }
        }

        void Start()
        {
             if (_frameProvider != null)
             {
                 _frameProvider.Initialize();
             }
             // Set CaptureTimer FPS to match scan settings
             if (captureTimer != null)
             {
                 captureTimer.SetTargetFPS(targetFPS);
             }
        }

        public void SetScanMode(ScanMode mode)
        {
            if (IsScanning) return; // Cannot change while scanning
            CurrentScanMode = mode;
            Debug.Log($"[ScanController] Mode set to: {CurrentScanMode}");
        }

        public void StartScan()
        {
            if (IsScanning) return;
            StartCoroutine(StartScanRoutine());
        }

        private IEnumerator StartScanRoutine()
        {
            // Apply delay
            if (startDelay > 0)
            {
                CurrentCountdown = startDelay;
                while (CurrentCountdown > 0)
                {
                    yield return null;
                    CurrentCountdown -= Time.deltaTime;
                }
                CurrentCountdown = 0f;
            }

            // Init session
            if (dataManager != null)
            {
                ScanSettings settings = new ScanSettings
                {
                    resolution = $"{captureResolution.x}x{captureResolution.y}",
                    targetFPS = targetFPS,
                    useFlashlight = useFlashlight
                };
                dataManager.StartNewScan(CurrentScanMode, settings);
                
                // Set accurate intrinsics if available
                if (_frameProvider != null)
                {
                    dataManager.SetIntrinsics(_frameProvider.GetIntrinsics());

                    // LOG DIAGNOSTICS FOR CAMERA
                    if (_frameProvider is QuestCameraProvider qcp)
                    {
                        qcp.LogDiagnostics(dataManager.CurrentScanFolder);
                    }
                }
            }

            IsScanning = true;
            
            // Start synchronized capture timer
            if (captureTimer != null)
            {
                captureTimer.SetTargetFPS(targetFPS);
                captureTimer.StartCapture();
            }
            
            Debug.Log($"[ScanController] Scan STARTED (Mode: {CurrentScanMode})");

            // Mode Logic
            if (CurrentScanMode == ScanMode.Space)
            {
                // Space Mode: Trigger Scene Capture (Force new scan)
                IsSceneModelLoaded = false;
                _waitingForSceneCapture = true;
                
                if (sceneManager != null)
                {
                    Debug.Log("[Scan] Requesting Scene Capture... (Forcing Room Setup)");
                    sceneManager.RequestSceneCapture();
                }
                else
                {
                    Debug.LogError("[Scan] OVRSceneManager missing for Space Mode!");
                    StopScan(); // Abort
                }
            }
            else
            {
                // Object Mode: Start Continuous Stream
                if (_frameProvider != null)
                {
                    _frameProvider.SetResolution(captureResolution.x, captureResolution.y);
                    _frameProvider.SetFPS(targetFPS);
                    _frameProvider.StartStream();
                }
            }
        }

        void OnEnable()
        {
            if (sceneManager != null)
            {
                sceneManager.SceneModelLoadedSuccessfully += OnSceneModelLoaded;
                sceneManager.NoSceneModelToLoad += OnNoSceneModelToLoad;
                sceneManager.NewSceneModelAvailable += OnNewSceneModelAvailable;
            }
        }

        void OnDisable()
        {
            if (sceneManager != null)
            {
                sceneManager.SceneModelLoadedSuccessfully -= OnSceneModelLoaded;
                sceneManager.NoSceneModelToLoad -= OnNoSceneModelToLoad;
                sceneManager.NewSceneModelAvailable -= OnNewSceneModelAvailable;
            }
        }

        private void OnSceneModelLoaded()
        {
            Debug.Log("[ScanController] Scene Model Loaded Successfully!");
            IsSceneModelLoaded = true;
            _waitingForSceneCapture = false;
            
            // If we're in an active Space scan, auto-capture the room data now
            if (IsScanning && CurrentScanMode == ScanMode.Space)
            {
                Debug.Log("[ScanController] Auto-capturing room data after scene model load.");
                CaptureRoomData();
            }
        }

        private void OnNoSceneModelToLoad()
        {
            if (IsScanning && CurrentScanMode == ScanMode.Space)
            {
                Debug.LogWarning("[ScanController] No Scene Model found. Requesting Room Setup...");
                sceneManager.RequestSceneCapture();
            }
        }

        private void OnNewSceneModelAvailable()
        {
             Debug.Log("[ScanController] New Scene Model Available. Loading...");
             sceneManager.LoadSceneModel();
        }

        public void StopScan()
        {
            if (!IsScanning) return;
            
            IsScanning = false;
            
            // Stop capture timer
            if (captureTimer != null)
            {
                captureTimer.StopCapture();
            }
            
            if (CurrentScanMode == ScanMode.Object)
            {
                if (_frameProvider != null) _frameProvider.StopStream();
                if (dataManager != null) dataManager.StopScan();
            }
            else 
            {
                // Space Mode: Try to capture room now, or wait for model to load
                if (IsSceneModelLoaded)
                {
                    // Already loaded — capture immediately
                    CaptureRoomData();
                    if (dataManager != null) dataManager.StopScan();
                }
                else if (_waitingForSceneCapture)
                {
                    // Scene is still loading — start retry coroutine
                    Debug.Log("[Scan] Scene model still loading. Waiting before capturing room data...");
                    _roomCaptureRetry = StartCoroutine(WaitForSceneAndCapture());
                }
                else
                {
                    Debug.LogWarning("[Scan] No scene capture was requested. Saving without room data.");
                    if (dataManager != null) dataManager.StopScan();
                }
            }
            
            Debug.Log("[ScanController] Scan STOPPED");
        }
        
        /// <summary>
        /// Waits up to 15 seconds for the scene model to load before capturing.
        /// </summary>
        private IEnumerator WaitForSceneAndCapture()
        {
            float timeout = 15f;
            float waited = 0f;
            
            while (!IsSceneModelLoaded && waited < timeout)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }
            
            if (IsSceneModelLoaded)
            {
                Debug.Log($"[Scan] Scene model loaded after {waited:F1}s wait.");
                CaptureRoomData();
            }
            else
            {
                Debug.LogWarning($"[Scan] Timed out waiting for scene model ({timeout}s). Room data not captured.");
            }
            
            if (dataManager != null) dataManager.StopScan();
            _roomCaptureRetry = null;
        }
        
        private void CaptureRoomData()
        {
            var rooms = FindObjectsOfType<OVRSceneRoom>();
            if (rooms.Length > 0 && dataManager != null)
            {
                Debug.Log($"[Scan] Found {rooms.Length} OVRSceneRoom(s). Capturing...");
                dataManager.CaptureSceneModel(rooms[0]);
            }
            else
            {
                Debug.LogWarning("[Scan] No OVRSceneRoom found to save.");
            }
        }

        void Update()
        {
            if (!IsScanning || dataManager == null) return;

            // Only Object mode needs continuous frame capture here
            if (CurrentScanMode == ScanMode.Object)
            {
                if (_frameProvider == null) return;

                // Use CaptureTimer for synchronized timing
                bool shouldCapture = true;
                if (captureTimer != null)
                {
                    shouldCapture = captureTimer.IsCapturing && captureTimer.ShouldCaptureThisFrame;
                }

                if (shouldCapture && _frameProvider.HasNewFrame())
                {
                    var frame = _frameProvider.GetLatestFrame();
                    if (frame.ColorTexture != null)
                    {
                        dataManager.CaptureFrame(frame.ColorTexture, frame.DepthTexture, frame.CameraPose);
                    }
                }
            }
        }
    }
}
