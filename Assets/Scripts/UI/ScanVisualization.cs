using UnityEngine;
using QuestGear3D.Scan.Core;
using Meta.XR.EnvironmentDepth;

namespace QuestGear3D.Scan.UI
{
    /// <summary>
    /// Point cloud visualizer using depth texture unprojection.
    /// Reads the Environment Depth texture and unprojects sampled pixels into 3D world points.
    /// No dependency on MRUK / EnvironmentRaycastManager.
    /// </summary>
    public class ScanVisualization : MonoBehaviour
    {
        [Header("References")]
        public ScanController scanController;
        public ParticleSystem pointCloudParticles;
        public GameObject sceneManagerObject;
        public CaptureTimer captureTimer;
        public Camera vrCamera;

        [Header("Depth Sampling Grid")]
        [Range(4, 32)] public int gridWidth = 12;
        [Range(4, 24)] public int gridHeight = 8;
        public float minDepth = 0.25f;
        public float maxDepth = 10f;

        [Header("Particle Settings")]
        public float particleSize = 0.01f;
        public float particleLifetime = 1000f;
        public int maxParticles = 100000;

        private EnvironmentDepthManager _depthManager;
        private Texture2D _depthReadback;
        private RenderTexture _depthCopy;
        private bool _depthAvailable = false;

        void Start()
        {
            if (scanController == null) scanController = FindObjectOfType<ScanController>();
            if (captureTimer == null) captureTimer = FindObjectOfType<CaptureTimer>();
            if (vrCamera == null) vrCamera = Camera.main;

            // Setup particle system for persistent world-space points
            if (pointCloudParticles != null)
            {
                var main = pointCloudParticles.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startSpeed = 0.00001f;
                main.startLifetime = particleLifetime;
                main.maxParticles = maxParticles;

                var emission = pointCloudParticles.emission;
                emission.enabled = false;
            }

            // Find EnvironmentDepthManager
            _depthManager = FindObjectOfType<EnvironmentDepthManager>();
            if (_depthManager != null && EnvironmentDepthManager.IsSupported)
            {
                _depthAvailable = true;
                _depthManager.enabled = true;
                Debug.Log("[ScanViz] EnvironmentDepthManager found — real depth visualization enabled.");
            }
            else
            {
                Debug.LogWarning("[ScanViz] EnvironmentDepthManager not found or not supported. Using mock visualization.");
            }
        }

        /// <summary>
        /// Clears all accumulated point cloud particles.
        /// </summary>
        public void ClearPointCloud()
        {
            if (pointCloudParticles != null)
            {
                pointCloudParticles.Clear();
            }
        }

        void Update()
        {
            if (scanController == null) return;

            bool isSpaceMode = scanController.CurrentScanMode == QuestGear3D.Scan.Data.ScanMode.Space;
            bool isScanning = scanController.IsScanning;

            // Space Mode: Enable Scene Manager visualization (room mesh)
            if (sceneManagerObject != null)
            {
                bool shouldShowRoom = isSpaceMode;
                if (sceneManagerObject.activeSelf != shouldShowRoom)
                {
                    sceneManagerObject.SetActive(shouldShowRoom);
                }
            }

            // Object Mode: depth point cloud or mock fallback
            if (pointCloudParticles == null || isSpaceMode || !isScanning) return;

            // Use CaptureTimer if available for synchronized visualization
            bool shouldVisualize = true;
            if (captureTimer != null)
            {
                shouldVisualize = captureTimer.IsCapturing && captureTimer.ShouldCaptureThisFrame;
            }

            if (!shouldVisualize) return;

            if (_depthAvailable && _depthManager != null && _depthManager.IsDepthAvailable)
            {
                SampleDepthTexture();
            }
            else
            {
                MockPointCloud();
            }
        }

        /// <summary>
        /// Reads the depth texture and unprojects sampled grid points into world space particles.
        /// Uses the camera's inverse projection to convert depth pixels into 3D positions.
        /// </summary>
        private void SampleDepthTexture()
        {
            if (vrCamera == null) return;

            // Get the global depth texture set by EnvironmentDepthManager
            var globalDepthTex = Shader.GetGlobalTexture("_EnvironmentDepthTexture") as RenderTexture;
            if (globalDepthTex == null) return;

            // Copy to CPU-readable texture
            if (_depthCopy == null || _depthCopy.width != globalDepthTex.width || _depthCopy.height != globalDepthTex.height)
            {
                if (_depthCopy != null) _depthCopy.Release();
                _depthCopy = new RenderTexture(globalDepthTex.width, globalDepthTex.height, 0, RenderTextureFormat.RFloat);
            }
            Graphics.Blit(globalDepthTex, _depthCopy);

            if (_depthReadback == null || _depthReadback.width != _depthCopy.width || _depthReadback.height != _depthCopy.height)
            {
                _depthReadback = new Texture2D(_depthCopy.width, _depthCopy.height, TextureFormat.RFloat, false);
            }

            RenderTexture.active = _depthCopy;
            _depthReadback.ReadPixels(new Rect(0, 0, _depthCopy.width, _depthCopy.height), 0, 0);
            _depthReadback.Apply();
            RenderTexture.active = null;

            int texW = _depthReadback.width;
            int texH = _depthReadback.height;
            Transform camTransform = vrCamera.transform;

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    // Sample position in normalized grid
                    float u = (x + 0.5f) / gridWidth;
                    float v = (y + 0.5f) / gridHeight;

                    // Pixel coordinate in depth texture
                    int px = Mathf.Clamp((int)(u * texW), 0, texW - 1);
                    int py = Mathf.Clamp((int)(v * texH), 0, texH - 1);

                    // Read depth value (meters)
                    float depth = _depthReadback.GetPixel(px, py).r;

                    // Filter invalid depths
                    if (depth < minDepth || depth > maxDepth || float.IsNaN(depth) || float.IsInfinity(depth))
                    {
                        continue;
                    }

                    // Unproject: viewport (u,v) + depth -> world position
                    // Use the camera's viewport-to-ray to get direction, then scale by depth
                    Ray ray = vrCamera.ViewportPointToRay(new Vector3(u, v, 0f));
                    Vector3 worldPos = ray.origin + ray.direction.normalized * depth;

                    // Color based on depth distance (near = warm, far = cool)
                    float t = Mathf.InverseLerp(minDepth, maxDepth, depth);
                    Color pointColor = Color.Lerp(new Color(0f, 1f, 0.5f), new Color(0.2f, 0.5f, 1f), t);

                    var emitParams = new ParticleSystem.EmitParams();
                    emitParams.position = worldPos;
                    emitParams.startColor = pointColor;
                    emitParams.startSize = particleSize;
                    pointCloudParticles.Emit(emitParams, 1);
                }
            }
        }

        /// <summary>
        /// Fallback mock point cloud for Editor testing (no real depth available).
        /// </summary>
        private void MockPointCloud()
        {
            if (vrCamera == null) return;

            Transform camTransform = vrCamera.transform;
            int count = gridWidth * gridHeight;

            for (int i = 0; i < count; i++)
            {
                Vector3 randomPos = Random.insideUnitSphere * 0.5f;
                randomPos.z = Mathf.Abs(randomPos.z) + 0.3f;

                var emitParams = new ParticleSystem.EmitParams();
                emitParams.position = camTransform.TransformPoint(randomPos);
                emitParams.startColor = Color.Lerp(Color.blue, Color.cyan, Random.value);
                emitParams.startSize = particleSize;
                pointCloudParticles.Emit(emitParams, 1);
            }
        }

        private void OnDestroy()
        {
            if (_depthCopy != null)
            {
                _depthCopy.Release();
                _depthCopy = null;
            }
        }
    }
}
