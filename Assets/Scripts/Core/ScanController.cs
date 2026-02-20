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
        
        /// <summary>Current phase in Space scan (Geometry or Appearance).</summary>
        public SpaceScanPhase CurrentPhase { get; private set; } = SpaceScanPhase.Geometry;
        
        /// <summary>True when Space Mode scene model has loaded and is ready to capture.</summary>
        public bool IsSceneModelLoaded { get; private set; }
        private bool _waitingForSceneCapture = false;
        private Coroutine _roomCaptureRetry;
        private float _phase2StartTime = 0f;
        private const float PHASE2_MIN_DURATION = 2.0f; // Prevent accidental stop from input bleed-through

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
                // Check if scene model is already available (common: Meta caches Room Setup)
                var existingRooms = FindObjectsOfType<OVRSceneRoom>();
                if (IsSceneModelLoaded || existingRooms.Length > 0)
                {
                    // Scene already available — skip Phase 1, capture geometry + start Phase 2 immediately
                    Debug.Log($"[Scan] Scene model already loaded ({existingRooms.Length} rooms). Skipping Room Setup.");
                    IsSceneModelLoaded = true;
                    CaptureRoomData();
                    StartAppearanceCapture();
                }
                else
                {
                    // No scene model — trigger Room Setup (Phase 1)
                    _waitingForSceneCapture = true;
                    
                    if (sceneManager != null)
                    {
                        Debug.Log("[Scan] No existing scene model. Requesting Room Setup...");
                        sceneManager.RequestSceneCapture();
                        
                        // Start failsafe polling in case events are missed
                        StartCoroutine(PollForSceneModel());
                    }
                    else
                    {
                        Debug.LogError("[Scan] OVRSceneManager missing for Space Mode!");
                        StopScan(); // Abort — won't be blocked since we allow abort when sceneManager is null
                    }
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
            
            // If we're in an active Space scan, capture geometry then transition to Phase 2
            if (IsScanning && CurrentScanMode == ScanMode.Space)
            {
                Debug.Log("[ScanController] Auto-capturing room data after scene model load.");
                CaptureRoomData();
                
                // Transition to Phase 2: Appearance capture
                StartAppearanceCapture();
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
            
            // GUARD: During Space Phase 1 (Room Setup), ignore stop requests.
            // Input can bleed through when returning from system Room Setup overlay.
            if (CurrentScanMode == ScanMode.Space && CurrentPhase == SpaceScanPhase.Geometry)
            {
                Debug.Log("[ScanController] Ignoring StopScan during Space Phase 1 (Room Setup in progress).");
                return;
            }
            
            // GUARD: Prevent accidental Phase 2 stop from input bleed-through.
            // Require minimum capture duration before allowing stop.
            if (CurrentScanMode == ScanMode.Space && CurrentPhase == SpaceScanPhase.Appearance)
            {
                float elapsed = Time.unscaledTime - _phase2StartTime;
                if (elapsed < PHASE2_MIN_DURATION)
                {
                    Debug.Log($"[ScanController] Ignoring StopScan — Phase 2 just started ({elapsed:F1}s < {PHASE2_MIN_DURATION}s).");
                    return;
                }
            }
            
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
                // Space Mode — must be Phase 2 at this point (Phase 1 is guarded above)
                Debug.Log("[Scan] Stopping Space Appearance Capture (Phase 2).");
                if (_frameProvider != null) _frameProvider.StopStream();
                if (dataManager != null) dataManager.StopScan();
                
                // Reset phase for next scan
                CurrentPhase = SpaceScanPhase.Geometry;
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
                
                // Transition to Phase 2 if scan is still active
                if (IsScanning)
                {
                    StartAppearanceCapture();
                }
                else
                {
                    if (dataManager != null) dataManager.StopScan();
                }
            }
            else
            {
                Debug.LogWarning($"[Scan] Timed out waiting for scene model ({timeout}s). Room data not captured.");
                if (dataManager != null) dataManager.StopScan();
            }
            
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

        /// <summary>
        /// Failsafe polling: checks every 2s for OVRSceneRoom objects.
        /// After Scene Capture completes, explicitly calls LoadSceneModel()
        /// since the OVR event can be lost during session restart.
        /// </summary>
        private IEnumerator PollForSceneModel()
        {
            float timeout = 60f;
            float waited = 0f;
            bool hasCalledLoad = false;
            
            Debug.Log("[Scan] Starting failsafe polling for scene model...");
            
            while (waited < timeout && IsScanning && CurrentPhase == SpaceScanPhase.Geometry)
            {
                yield return new WaitForSeconds(2f);
                waited += 2f;
                
                // Check if rooms appeared in hierarchy
                var rooms = FindObjectsOfType<OVRSceneRoom>();
                if (rooms.Length > 0)
                {
                    Debug.Log($"[Scan] Failsafe poll: Found {rooms.Length} room(s) after {waited:F0}s. Starting Phase 2.");
                    IsSceneModelLoaded = true;
                    CaptureRoomData();
                    StartAppearanceCapture();
                    yield break;
                }
                
                // After app regains focus (waited > 4s should cover the resume),
                // explicitly call LoadSceneModel() to force anchor loading.
                // The OVR event is often lost during session restart.
                if (waited >= 4f && !hasCalledLoad && sceneManager != null)
                {
                    Debug.Log("[Scan] Failsafe poll: Explicitly calling LoadSceneModel()...");
                    sceneManager.LoadSceneModel();
                    hasCalledLoad = true;
                }
                else if (waited >= 10f && hasCalledLoad && sceneManager != null)
                {
                    // Retry once more after 10s in case first attempt was too early
                    Debug.Log("[Scan] Failsafe poll: Retrying LoadSceneModel()...");
                    sceneManager.LoadSceneModel();
                    hasCalledLoad = false; // Allow one more retry at 16s
                }
                
                Debug.Log($"[Scan] Failsafe poll: No rooms yet ({waited:F0}s / {timeout}s)...");
            }
            
            if (CurrentPhase == SpaceScanPhase.Geometry && IsScanning)
            {
                Debug.LogWarning("[Scan] Failsafe poll timed out. No scene model found after 60s.");
                // Allow user to stop the stuck scan — temporarily set to Appearance so StopScan works
                CurrentPhase = SpaceScanPhase.Appearance;
                _phase2StartTime = Time.unscaledTime - PHASE2_MIN_DURATION - 1f; // ensure stop allowed
            }
        }

        /// <summary>
        /// Starts Phase 2 (Appearance Capture) after room geometry has been captured.
        /// Activates camera stream and capture timer for continuous frame recording.
        /// </summary>
        private void StartAppearanceCapture()
        {
            CurrentPhase = SpaceScanPhase.Appearance;
            _phase2StartTime = Time.unscaledTime;
            Debug.Log("[ScanController] === PHASE 2: Appearance Capture Started ===");
            Debug.Log("[ScanController] Walk around the room. Press A to finish when done.");
            
            // Start camera stream (reuse Object mode pipeline)
            if (_frameProvider != null)
            {
                _frameProvider.SetResolution(captureResolution.x, captureResolution.y);
                _frameProvider.SetFPS(targetFPS);
                _frameProvider.StartStream();
                Debug.Log("[ScanController] Camera stream started for appearance capture.");
            }
            else
            {
                Debug.LogError("[ScanController] Frame provider is NULL — cannot start appearance capture!");
            }
            
            // Restart capture timer for frame timing
            if (captureTimer != null)
            {
                captureTimer.SetTargetFPS(targetFPS);
                captureTimer.StartCapture();
            }
        }

        void Update()
        {
            if (!IsScanning || dataManager == null) return;

            // Capture frames in Object mode OR Space mode Phase 2 (Appearance)
            bool shouldCaptureFrames = (CurrentScanMode == ScanMode.Object) ||
                (CurrentScanMode == ScanMode.Space && CurrentPhase == SpaceScanPhase.Appearance);

            if (shouldCaptureFrames)
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
