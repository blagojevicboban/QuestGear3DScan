using UnityEngine;
using QuestGear3D.Scan.Data;
using Meta.XR.EnvironmentDepth;

namespace QuestGear3D.Scan.Sensors
{
    /// <summary>
    /// Concrete IDepthProvider implementation using Meta Quest's Environment Depth API.
    /// Reads the depth texture from EnvironmentDepthManager and converts it to CPU-readable format.
    /// </summary>
    public class QuestDepthProvider : MonoBehaviour, IDepthProvider
    {
        [Header("Depth Settings")]
        [Tooltip("Reference to EnvironmentDepthManager. Auto-found if null.")]
        [SerializeField] private EnvironmentDepthManager depthManager;
        
        [Tooltip("Reference to OVRCameraRig's center eye. Auto-found if null.")]
        [SerializeField] private Transform centerEyeAnchor;

        private RenderTexture _depthRT;
        private Texture2D _readableDepth;
        private bool _isReady = false;
        private PinholeCameraIntrinsic _cachedIntrinsics;

        public void Initialize()
        {
            // Find EnvironmentDepthManager
            if (depthManager == null)
            {
                depthManager = FindObjectOfType<EnvironmentDepthManager>();
            }

            if (depthManager == null)
            {
                // Try to create one if supported
                if (EnvironmentDepthManager.IsSupported)
                {
                    var obj = new GameObject("QuestDepthManager");
                    depthManager = obj.AddComponent<EnvironmentDepthManager>();
                    Debug.Log("[QuestDepthProvider] Created EnvironmentDepthManager automatically.");
                }
                else
                {
                    Debug.LogWarning("[QuestDepthProvider] Environment Depth NOT supported on this device.");
                    return;
                }
            }

            depthManager.enabled = true;

            // Find CenterEyeAnchor
            if (centerEyeAnchor == null)
            {
                var cameraRig = FindObjectOfType<OVRCameraRig>();
                if (cameraRig != null)
                {
                    centerEyeAnchor = cameraRig.centerEyeAnchor;
                }
                else if (Camera.main != null)
                {
                    centerEyeAnchor = Camera.main.transform;
                }
            }

            _isReady = true;
            Debug.Log("[QuestDepthProvider] Initialized successfully.");
        }

        public Texture2D GetDepthTexture()
        {
            if (!_isReady || depthManager == null || !depthManager.IsDepthAvailable)
            {
                return null;
            }

            // Retrieve depth texture from shader global property
            var globalDepthTex = Shader.GetGlobalTexture("_EnvironmentDepthTexture") as RenderTexture;

            if (globalDepthTex == null)
            {
                return null;
            }

            // Copy to CPU-readable texture
            if (_depthRT == null || _depthRT.width != globalDepthTex.width || _depthRT.height != globalDepthTex.height)
            {
                if (_depthRT != null) _depthRT.Release();
                _depthRT = new RenderTexture(globalDepthTex.width, globalDepthTex.height, 0, RenderTextureFormat.R16);
            }

            Graphics.Blit(globalDepthTex, _depthRT);

            if (_readableDepth == null || _readableDepth.width != _depthRT.width || _readableDepth.height != _depthRT.height)
            {
                _readableDepth = new Texture2D(_depthRT.width, _depthRT.height, TextureFormat.R16, false);
            }

            RenderTexture.active = _depthRT;
            _readableDepth.ReadPixels(new Rect(0, 0, _depthRT.width, _depthRT.height), 0, 0);
            _readableDepth.Apply();
            RenderTexture.active = null;

            return _readableDepth;
        }

        public PinholeCameraIntrinsic GetIntrinsics()
        {
            if (_cachedIntrinsics != null) return _cachedIntrinsics;

            // Try to get depth sensor intrinsics from OVRPlugin
            try
            {
                bool success = OVRPlugin.GetNodeFrustum2(OVRPlugin.Node.EyeCenter, out OVRPlugin.Frustumf2 frustum);
                if (success && frustum.Fov.UpTan > 0)
                {
                    int width = _readableDepth != null ? _readableDepth.width : 256;
                    int height = _readableDepth != null ? _readableDepth.height : 256;

                    float fy = height / (frustum.Fov.UpTan + frustum.Fov.DownTan);
                    float fx = width / (frustum.Fov.LeftTan + frustum.Fov.RightTan);
                    float cx = frustum.Fov.LeftTan * fx;
                    float cy = frustum.Fov.UpTan * fy;

                    _cachedIntrinsics = new PinholeCameraIntrinsic(width, height, fx, fy, cx, cy);
                    return _cachedIntrinsics;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[QuestDepthProvider] Intrinsics lookup failed: {e.Message}");
            }

            // Fallback
            _cachedIntrinsics = new PinholeCameraIntrinsic(256, 256, 128f, 128f, 128f, 128f);
            return _cachedIntrinsics;
        }

        public Matrix4x4 GetCameraToWorldMatrix()
        {
            if (centerEyeAnchor != null)
            {
                return centerEyeAnchor.localToWorldMatrix;
            }
            return Matrix4x4.identity;
        }

        public bool IsReady()
        {
            return _isReady && depthManager != null && depthManager.IsDepthAvailable;
        }

        private void OnDestroy()
        {
            if (_depthRT != null)
            {
                _depthRT.Release();
                _depthRT = null;
            }
        }
    }
}
