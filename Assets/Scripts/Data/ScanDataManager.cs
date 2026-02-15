using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace QuestGear3D.Scan.Data
{
    // Simple structs for Scene Data Serialization
    [System.Serializable]
    public class SceneData
    {
        public string scanId;
        public List<SceneObjectData> objects = new List<SceneObjectData>();
    }

    [System.Serializable]
    public class SceneObjectData
    {
        public string classification; // e.g., "WALL", "FLOOR", "TABLE"
        public string uuid;
        public float[] position;
        public float[] rotation;
        public float[] scale;
        // For Planes
        public float[] plane_rect; // width, height (if applicable)
        // For Volumes
        public float[] volume_bounds; // center, size (if applicable)
    }

    public class ScanDataManager : MonoBehaviour
    {
        public string scanRootDirectory = "Scans";
        public string CurrentScanFolder { get; private set; }
        private ScanData _currentScanData;
        private SceneData _currentSceneData;
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
            CurrentScanFolder = Path.Combine(basePath, scanRootDirectory, scanName);
            
            if (!Directory.Exists(CurrentScanFolder))
            {
                Directory.CreateDirectory(CurrentScanFolder);
                Directory.CreateDirectory(Path.Combine(CurrentScanFolder, "color"));
                Directory.CreateDirectory(Path.Combine(CurrentScanFolder, "depth"));
            }

            // Init Object Scan Data
            _currentScanData = new ScanData();
            _currentScanData.scanId = scanName;
            _currentScanData.scanMode = mode.ToString();
            _currentScanData.settings = settings;
            _currentScanData.settings = settings;
            // Default intrinsics, should be updated by controller
            _currentScanData.intrinsic = new PinholeCameraIntrinsic(1280, 720, 1000, 1000, 640, 360); 
            
            // Init Scene Data
            _currentSceneData = new SceneData();
            _currentSceneData.scanId = scanName;

            _isScanning = true;
            _isProcessingQueue = true;
            Task.Run(ProcessSaveQueue); 
            
            Debug.Log($"Started scan: {CurrentScanFolder} (Mode: {mode})");
        }

        public void SetIntrinsics(PinholeCameraIntrinsic intrinsics)
        {
            if (_currentScanData != null)
            {
                _currentScanData.intrinsic = intrinsics;
            }
        }

        public void StopScan()
        {
            _isScanning = false;
            SaveMetadata(); 
            Debug.Log($"Stopped scan. Saving remaining files...");
        }

        public void CaptureFrame(Texture2D colorParams, Texture2D depthParams, Matrix4x4 cameraToWorldMatrix)
        {
            if (!_isScanning) return;

            int frameId = _currentScanData.frames.Count;
            double timestamp = Time.realtimeSinceStartupAsDouble;
            
            string colorFileName = $"color/frame_{frameId:D6}.jpg";
            string depthFileName = $"depth/frame_{frameId:D6}.png"; 
            
            byte[] colorBytes = colorParams.EncodeToJPG(90);
            byte[] depthBytes = depthParams.EncodeToPNG(); 
            
            _saveQueue.Enqueue(new SaveRequest { FilePath = Path.Combine(CurrentScanFolder, colorFileName), Data = colorBytes });
            _saveQueue.Enqueue(new SaveRequest { FilePath = Path.Combine(CurrentScanFolder, depthFileName), Data = depthBytes });

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

        public void CaptureSceneModel(OVRSceneRoom room) // Can be extended to generic anchors
        {
            if (room == null) return;

            // TODO: Iterate through anchors (Walls, Floors, etc.)
            // For now, this is a placeholder to show data flow.
            // We would need access to OVRSceneAnchor components.
            
            // Example: Find all OVRSceneAnchors in scene
            var anchors = FindObjectsOfType<OVRSceneAnchor>();
            foreach (var anchor in anchors)
            {
                string label = "UNKNOWN";
                var classification = anchor.GetComponent<OVRSemanticClassification>();
                if (classification != null && classification.Labels.Count > 0)
                {
                    label = classification.Labels[0];
                }

                SceneObjectData objData = new SceneObjectData
                {
                    classification = label,
                    uuid = anchor.Uuid.ToString(),
                    position = Float3(anchor.transform.position),
                    rotation = Float4(anchor.transform.rotation),
                    scale = Float3(anchor.transform.localScale)
                };

                // Add plane info if available
                var plane = anchor.GetComponent<OVRScenePlane>();
                if (plane != null)
                {
                    objData.plane_rect = new float[] { plane.Width, plane.Height };
                }
                
                // Add volume info if available
                var vol = anchor.GetComponent<OVRSceneVolume>();
                if (vol != null)
                {
                    objData.volume_bounds = new float[] { vol.Width, vol.Height, vol.Depth };
                }

                _currentSceneData.objects.Add(objData);
            }
            
            SaveSceneData();
            Debug.Log($"[DataManager] Captured Scene Model with {_currentSceneData.objects.Count} objects.");
        }

        private async Task ProcessSaveQueue()
        {
            while (_isScanning || !_saveQueue.IsEmpty)
            {
                if (_saveQueue.TryDequeue(out SaveRequest request))
                {
                    // Ensure directory exists (async safety)
                    string dir = Path.GetDirectoryName(request.FilePath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    await File.WriteAllBytesAsync(request.FilePath, request.Data);
                }
                else
                {
                    await Task.Delay(10);
                }
            }
            _isProcessingQueue = false;
            Debug.Log("Background Save Queue finished.");
        }

        private void SaveMetadata()
        {
            // Save Frame Data
            string json = JsonUtility.ToJson(_currentScanData, true);
            string path = Path.Combine(CurrentScanFolder, "scan_data.json");
            File.WriteAllText(path, json);

            // Export NerfStudio format
            try 
            {
               NerfStudioExporter.Export(CurrentScanFolder, _currentScanData);
               Debug.Log("Exported transforms.json for NerfStudio.");
            }
            catch(System.Exception e)
            {
                Debug.LogError($"Failed to export NerfStudio JSON: {e.Message}");
            }
        }

        private void SaveSceneData()
        {
            if (_currentSceneData != null && _currentSceneData.objects.Count > 0)
            {
                string json = JsonUtility.ToJson(_currentSceneData, true);
                string path = Path.Combine(CurrentScanFolder, "scene_data.json");
                File.WriteAllText(path, json);
            }
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
        
        private float[] Float3(Vector3 v) => new float[] { v.x, v.y, v.z };
        private float[] Float4(Quaternion q) => new float[] { q.x, q.y, q.z, q.w };
    }
}
