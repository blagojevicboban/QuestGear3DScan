using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace QuestGear3D.Scan.Data
{
    public class ScanDataManager : MonoBehaviour
    {
        public string scanRootDirectory = "Scans";
        private string _currentScanFolder;
        private ScanData _currentScanData;
        private bool _isScanning = false;
        
        // Queue for background saving
        private ConcurrentQueue<SaveRequest> _saveQueue = new ConcurrentQueue<SaveRequest>();
        private bool _isProcessingQueue = false;

        private struct SaveRequest
        {
            public string FilePath;
            public byte[] Data;
        }
        
        public void StartNewScan(ScanMode mode, ScanSettings settings)
        {
            string scanName = $"Scan_{System.DateTime.Now:yyyyMMdd_HHmmss}";
            string basePath = Application.persistentDataPath;
            _currentScanFolder = Path.Combine(basePath, scanRootDirectory, scanName);
            
            if (!Directory.Exists(_currentScanFolder))
            {
                Directory.CreateDirectory(_currentScanFolder);
                Directory.CreateDirectory(Path.Combine(_currentScanFolder, "color"));
                Directory.CreateDirectory(Path.Combine(_currentScanFolder, "depth"));
            }

            _currentScanData = new ScanData();
            _currentScanData.scanId = scanName;
            _currentScanData.scanMode = mode.ToString();
            _currentScanData.settings = settings;
            
            // TODO: Set intrinsics from actual camera
            _currentScanData.intrinsic = new PinholeCameraIntrinsic(1280, 720, 1000, 1000, 640, 360); 
            
            _isScanning = true;
            _isProcessingQueue = true;
            Task.Run(ProcessSaveQueue); // Start background consumer
            
            Debug.Log($"Started scan: {_currentScanFolder} (Mode: {mode})");
        }

        public void StopScan()
        {
            _isScanning = false;
            SaveMetadata(); // Save JSON immediately on main thread (fast enough) or queue it? Main thread ensures consistency.
            
            // Allow queue to finish processing in background
            // We don't set _isProcessingQueue to false immediately, we let it drain.
            // But for simplicity here we just leave the task running until app close or next scan?
            // Better to have a unified cancellation token or just let it loop given we might start scan again.
            // For this implementation, we'll let the while loop behave gracefully.
            
            Debug.Log($"Stopped scan. Captured {_currentScanData.frames.Count} frames. Saving remaining files in background...");
        }

        public void CaptureFrame(Texture2D colorParams, Texture2D depthParams, Matrix4x4 cameraToWorldMatrix)
        {
            if (!_isScanning) return;

            int frameId = _currentScanData.frames.Count;
            double timestamp = Time.realtimeSinceStartupAsDouble;
            
            string colorFileName = $"color/frame_{frameId:D6}.jpg";
            string depthFileName = $"depth/frame_{frameId:D6}.png"; 
            
            // Encode on Main Thread (ImageConversion is standard Unity API)
            // Ideally we move this to C++ plugin or job, but for now EncodeToJPG is the implementation.
            byte[] colorBytes = colorParams.EncodeToJPG(90);
            byte[] depthBytes = depthParams.EncodeToPNG(); 
            
            // Queue saving
            _saveQueue.Enqueue(new SaveRequest { FilePath = Path.Combine(_currentScanFolder, colorFileName), Data = colorBytes });
            _saveQueue.Enqueue(new SaveRequest { FilePath = Path.Combine(_currentScanFolder, depthFileName), Data = depthBytes });

            // Metadata update (Main Thread)
            ScanFrameMetadata metadata = new ScanFrameMetadata
            {
                frame_id = frameId,
                timestamp = timestamp,
                color_file = colorFileName,
                depth_file = depthFileName,
                pose = MatrixToFloatArray(cameraToWorldMatrix)
            };
            
            _currentScanData.frames.Add(metadata);
        }

        private async Task ProcessSaveQueue()
        {
            while (_isScanning || !_saveQueue.IsEmpty)
            {
                if (_saveQueue.TryDequeue(out SaveRequest request))
                {
                    await File.WriteAllBytesAsync(request.FilePath, request.Data);
                }
                else
                {
                    await Task.Delay(10); // Sleep if empty
                }
            }
            _isProcessingQueue = false;
            Debug.Log("Background Save Queue finished.");
        }

        private void SaveMetadata()
        {
            string json = JsonUtility.ToJson(_currentScanData, true);
            string path = Path.Combine(_currentScanFolder, "scan_data.json");
            File.WriteAllText(path, json);
        }

        public int PendingSaveCount => _saveQueue.Count;

        private float[] MatrixToFloatArray(Matrix4x4 m)
        {
            return new float[]
            {
                m.m00, m.m01, m.m02, m.m03,
                m.m10, m.m11, m.m12, m.m13,
                m.m20, m.m21, m.m22, m.m23,
                m.m30, m.m31, m.m32, m.m33
            };
        }
    }
}
