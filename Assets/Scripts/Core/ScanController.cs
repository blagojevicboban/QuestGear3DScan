using UnityEngine;
using System.Collections;
using QuestGear3D.Scan.Data;

namespace QuestGear3D.Scan.Core
{
    public class ScanController : MonoBehaviour
    {
        [Header("Scan Configuration")]
        // public ScanMode currentScanMode = ScanMode.Object; 
        // Keeping as field for Inspector, but maybe add property if needed
        public ScanMode currentScanMode = ScanMode.Object; 

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
        }

        void Start()
        {
             if (_frameProvider != null)
             {
                 _frameProvider.Initialize();
             }
             _captureInterval = 1f / targetFPS;
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
                dataManager.StartNewScan(currentScanMode, settings);
            }

            // Start Camera Stream
            if (_frameProvider != null)
            {
                _frameProvider.SetResolution(captureResolution.x, captureResolution.y);
                _frameProvider.SetFPS(targetFPS);
                _frameProvider.StartStream();
            }

            IsScanning = true;
            _lastCaptureTime = Time.time;
            Debug.Log($"[ScanController] Scan STARTED (Mode: {currentScanMode})");
        }

        public void StopScan()
        {
            if (!IsScanning) return;
            
            IsScanning = false;
            
            if (_frameProvider != null) _frameProvider.StopStream();
            if (dataManager != null) dataManager.StopScan();
            
            Debug.Log("[ScanController] Scan STOPPED");
        }

        void Update()
        {
            if (!IsScanning || _frameProvider == null || dataManager == null) return;

            // Simple FPS throttle
            if (Time.time - _lastCaptureTime >= _captureInterval)
            {
                if (_frameProvider.HasNewFrame())
                {
                    var frame = _frameProvider.GetLatestFrame();
                    // We only save if we have a valid color texture
                    if (frame.ColorTexture != null)
                    {
                        // TODO: Add ScanFrameMetadata if needed
                        dataManager.CaptureFrame(frame.ColorTexture, frame.DepthTexture, frame.CameraPose);
                        _lastCaptureTime = Time.time;
                    }
                }
            }
        }
    }
}
