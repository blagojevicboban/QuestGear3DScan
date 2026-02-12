using UnityEngine;
using QuestGear3D.Scan.Data;

namespace QuestGear3D.Scan.Core
{
    public class ScanController : MonoBehaviour
    {
        [Header("Dependencies")]
        public ScanDataManager dataManager;
        // Interface reference - assigned via dragging a MonoBehaviour that implements it
        // Unity doesn't serialize interfaces well, so we use a Component reference and validate on Awake
        public MonoBehaviour frameProviderObject; 
        private IFrameProvider _frameProvider;

        [Header("Settings")]
        public float captureFPS = 5f; // Target capture rate
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
            
            _captureInterval = 1f / captureFPS;
        }

        void Start()
        {
             if (_frameProvider != null)
             {
                 _frameProvider.Initialize();
             }
        }

        public void StartScan()
        {
            if (_isScanning) return;
            
            if (dataManager != null) dataManager.StartNewScan();
            if (_frameProvider != null) _frameProvider.StartStream();
            
            _isScanning = true;
            _timer = 0f;
            Debug.Log("[ScanController] Scan Started");
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
