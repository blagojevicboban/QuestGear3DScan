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
        
        // Concurrent queue for async saving? 
        // Or just fire and forget tasks
        
        public void StartNewScan()
        {
            string scanName = $"Scan_{System.DateTime.Now:yyyyMMdd_HHmmss}";
            // On Android/Quest, use persistentDataPath
            string basePath = Application.persistentDataPath;
            _currentScanFolder = Path.Combine(basePath, scanRootDirectory, scanName);
            
            if (!Directory.Exists(_currentScanFolder))
            {
                Directory.CreateDirectory(_currentScanFolder);
                Directory.CreateDirectory(Path.Combine(_currentScanFolder, "color"));
                Directory.CreateDirectory(Path.Combine(_currentScanFolder, "depth"));
            }

            _currentScanData = new ScanData();
            // TODO: Set intrinsics from actual camera
            _currentScanData.intrinsic = new PinholeCameraIntrinsic(1280, 720, 1000, 1000, 640, 360); // Placeholder
            
            _isScanning = true;
            Debug.Log($"Started scan: {_currentScanFolder}");
        }

        public void StopScan()
        {
            _isScanning = false;
            SaveMetadata();
            Debug.Log($"Stopped scan. Saved {_currentScanData.frames.Count} frames.");
        }

        public void CaptureFrame(Texture2D colorParams, Texture2D depthParams, Matrix4x4 cameraToWorldMatrix)
        {
            if (!_isScanning) return;

            int frameId = _currentScanData.frames.Count;
            double timestamp = Time.realtimeSinceStartupAsDouble;
            
            string colorFileName = $"color/frame_{frameId:D6}.jpg";
            string depthFileName = $"depth/frame_{frameId:D6}.png"; // usually 16-bit PNG for depth
            
            // In a real app, we need to read pixels from GPU to CPU here.
            // This is a bottleneck. We assume textures are already readable or we use AsyncGPUReadback.
            // For now, simple implementation:
            
            byte[] colorBytes = colorParams.EncodeToJPG(90);
            byte[] depthBytes = depthParams.EncodeToPNG(); // Basic PNG
            
            // Save async
            string fullColorPath = Path.Combine(_currentScanFolder, colorFileName);
            string fullDepthPath = Path.Combine(_currentScanFolder, depthFileName);
            
            Task.Run(() => File.WriteAllBytes(fullColorPath, colorBytes));
            Task.Run(() => File.WriteAllBytes(fullDepthPath, depthBytes));

            // Metadata
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

        private void SaveMetadata()
        {
            string json = JsonUtility.ToJson(_currentScanData, true);
            string path = Path.Combine(_currentScanFolder, "scan_data.json");
            File.WriteAllText(path, json);
        }

        private float[] MatrixToFloatArray(Matrix4x4 m)
        {
            // Flatten generic matrix
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
