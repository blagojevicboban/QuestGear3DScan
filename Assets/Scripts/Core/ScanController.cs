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

        private float _lastCaptureTime;
        private float _captureInterval;

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
        }

        void Start()
        {
             if (_frameProvider != null)
             {
                 _frameProvider.Initialize();
             }
             _captureInterval = 1f / targetFPS;
             
             // Ensure legacy field matches property if set in inspector
             // currentScanMode field was removed to use Property directly
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
            }

            IsScanning = true;
            _lastCaptureTime = Time.time;
            Debug.Log($"[ScanController] Scan STARTED (Mode: {CurrentScanMode})");

            // Mode Logic
            if (CurrentScanMode == ScanMode.Space)
            {
                // Space Mode: Trigger Scene Capture (Force new scan)
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
            
            if (CurrentScanMode == ScanMode.Object)
            {
                if (_frameProvider != null) _frameProvider.StopStream();
            }
            else 
            {
                // Space Mode: Capture logical completion
                // Ideally we hook into OVRSceneManager events to know when capture is done
                // For now, we manually "Stop" to save data and finish session
                CaptureRoomData();
            }

            if (dataManager != null) dataManager.StopScan();
            
            Debug.Log("[ScanController] Scan STOPPED");
        }
        
        private void CaptureRoomData()
        {
            // Try to find the loaded room
            var rooms = FindObjectsOfType<OVRSceneRoom>();
            if (rooms.Length > 0 && dataManager != null)
            {
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

                // Simple FPS throttle
                if (Time.time - _lastCaptureTime >= _captureInterval)
                {
                    if (_frameProvider.HasNewFrame())
                    {
                        var frame = _frameProvider.GetLatestFrame();
                        if (frame.ColorTexture != null)
                        {
                            dataManager.CaptureFrame(frame.ColorTexture, frame.DepthTexture, frame.CameraPose);
                            _lastCaptureTime = Time.time;
                        }
                    }
                }
            }
        }
    }
}
