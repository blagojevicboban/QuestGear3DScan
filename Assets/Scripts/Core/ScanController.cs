using UnityEngine;
using QuestGear3D.Scan.Data;

namespace QuestGear3D.Scan.Core
{
    public class ScanController : MonoBehaviour
    {
        [Header("Scan Configuration")]
        public ScanMode currentScanMode = ScanMode.Object;
        public Vector2Int captureResolution = new Vector2Int(1920, 1080);
        [Range(1, 60)] public int targetFPS = 30;
        public bool useFlashlight = false;
        [Range(0f, 10f)] public float startDelay = 0f;

        [Header("Dependencies")]
        public ScanDataManager dataManager;
        // Interface reference - assigned via dragging a MonoBehaviour that implements it
        // Unity doesn't serialize interfaces well, so we use a Component reference and validate on Awake
        public MonoBehaviour frameProviderObject; 
        private IFrameProvider _frameProvider;

        [Header("Internal")]
        // public float captureFPS = 5f; // REPLACED by targetFPS
        private float _captureInterval;
        private float _timer;
        private bool _isScanning = false;

        void Awake()
        {
            if (frameProviderObject != null && frameProviderObject is IFrameProvider)
            {
                _frameProvider = (IFrameProvider)frameProviderObject;
            }
            else
            {
                Debug.LogError("[ScanController] Frame Provider Object does not implement IFrameProvider!");
            }
        }

        void Start()
        {
             if (_frameProvider != null)
             {
                 _frameProvider.Initialize();
             }
             UpdateCaptureInterval();
        }

        private void UpdateCaptureInterval()
        {
             _captureInterval = 1f / targetFPS;
        }

        public float CurrentCountdown { get; private set; }

        public void StartScan()
        {
            if (_isScanning) return;
            StartCoroutine(StartScanRoutine());
        }

        private System.Collections.IEnumerator StartScanRoutine()
        {
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

            // Apply Settings
            if (_frameProvider != null)
            {
                _frameProvider.SetResolution(captureResolution.x, captureResolution.y);
                _frameProvider.SetFPS(targetFPS);
                _frameProvider.SetFlashlight(useFlashlight);
                _frameProvider.StartStream();
            }

            UpdateCaptureInterval();

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
            
            _isScanning = true;
            _timer = 0f;
            Debug.Log($"[ScanController] Scan Started (Mode: {currentScanMode})");
        }

        public void StopScan()
        {
            if (!_isScanning) return;
            
            _isScanning = false;
            
            if (_frameProvider != null) _frameProvider.StopStream();
            if (dataManager != null) dataManager.StopScan();
            
            Debug.Log("[ScanController] Scan Stopped");
        }

        void Update()
        {
            if (!_isScanning || _frameProvider == null || dataManager == null) return;

            _timer += Time.deltaTime;
            if (_timer >= _captureInterval)
            {
                if (_frameProvider.HasNewFrame())
                {
                    FrameData frame = _frameProvider.GetLatestFrame();
                    if (frame.ColorTexture != null)
                    {
                        dataManager.CaptureFrame(frame.ColorTexture, frame.DepthTexture, frame.CameraPose);
                        _timer = 0f;
                    }
                }
            }
        }
        
        public bool IsScanning => _isScanning;
    }
}
