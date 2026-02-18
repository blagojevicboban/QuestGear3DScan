using UnityEngine;
using QuestGear3D.Scan.Data;
using Meta.XR.EnvironmentDepth;

namespace QuestGear3D.Scan.Sensors
{
    /// <summary>
    /// Concrete IDepthProvider implementation using Meta Quest's Environment Depth API.
    /// Uses a ComputeShader to read from Texture2DArray (Quest native format) into CPU-readable data.
    /// </summary>
    public class QuestDepthProvider : MonoBehaviour, IDepthProvider
    {
        [Header("Depth Settings")]
        [Tooltip("Reference to EnvironmentDepthManager. Auto-found if null.")]
        [SerializeField] private EnvironmentDepthManager depthManager;
        
        [Tooltip("Reference to OVRCameraRig's center eye. Auto-found if null.")]
        [SerializeField] private Transform centerEyeAnchor;
        
        [Tooltip("Compute shader for reading Texture2DArray depth. Auto-loaded from Resources if null.")]
        [SerializeField] private ComputeShader copyDepthShader;

        private Texture2D _readableDepth;
        private bool _isReady = false;
        private PinholeCameraIntrinsic _cachedIntrinsics;
        
        // Compute shader readback
        private ComputeBuffer _depthBuffer;
        private float[] _depthData;
        private int _depthKernel = -1;
        private bool _loggedDepthType = false;
        
        // RenderTexture fallback
        private RenderTexture _depthRT;

        public void Initialize()
        {
            // Find EnvironmentDepthManager
            if (depthManager == null)
            {
                depthManager = FindObjectOfType<EnvironmentDepthManager>();
            }

            if (depthManager == null)
            {
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
            
            // Auto-load compute shader
            if (copyDepthShader == null)
            {
                copyDepthShader = Resources.Load<ComputeShader>("CopyDepthSlice");
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

            var globalTex = Shader.GetGlobalTexture("_EnvironmentDepthTexture");
            if (globalTex == null) return null;
            
            // Log type once for diagnostics
            if (!_loggedDepthType)
            {
                _loggedDepthType = true;
                Debug.Log($"[QuestDepthProvider] _EnvironmentDepthTexture type: {globalTex.GetType().Name}, " +
                          $"dimension: {globalTex.dimension}, size: {globalTex.width}x{globalTex.height}");
            }
            
            // Strategy 1: Texture2DArray + ComputeShader (Quest native)
            if (globalTex.dimension == UnityEngine.Rendering.TextureDimension.Tex2DArray && copyDepthShader != null)
            {
                ReadViaComputeShader(globalTex);
                return _readableDepth;
            }
            
            // Strategy 2: RenderTexture blit 
            var rtTex = globalTex as RenderTexture;
            if (rtTex != null)
            {
                ReadViaRenderTexture(rtTex);
                return _readableDepth;
            }
            
            // Strategy 3: Direct Texture2D (unlikely but safe)
            var tex2D = globalTex as Texture2D;
            if (tex2D != null && tex2D.width > 16)
            {
                return tex2D;
            }

            return null;
        }
        
        private void ReadViaComputeShader(Texture globalTex)
        {
            int w = globalTex.width;
            int h = globalTex.height;
            int totalPixels = w * h;
            
            if (_depthKernel < 0)
            {
                _depthKernel = copyDepthShader.FindKernel("CopySlice");
            }
            
            if (_depthBuffer == null || _depthBuffer.count != totalPixels)
            {
                _depthBuffer?.Release();
                _depthBuffer = new ComputeBuffer(totalPixels, sizeof(float));
                _depthData = new float[totalPixels];
            }
            
            copyDepthShader.SetTexture(_depthKernel, "_InputTex", globalTex);
            copyDepthShader.SetBuffer(_depthKernel, "_OutputBuffer", _depthBuffer);
            copyDepthShader.SetInt("_Width", w);
            copyDepthShader.SetInt("_Height", h);
            copyDepthShader.SetInt("_SliceIndex", 0); // Left eye
            
            int groupsX = Mathf.CeilToInt(w / 8f);
            int groupsY = Mathf.CeilToInt(h / 8f);
            copyDepthShader.Dispatch(_depthKernel, groupsX, groupsY, 1);
            
            _depthBuffer.GetData(_depthData);
            
            if (_readableDepth == null || _readableDepth.width != w || _readableDepth.height != h)
            {
                _readableDepth = new Texture2D(w, h, TextureFormat.RFloat, false);
            }
            
            var nativeArray = _readableDepth.GetRawTextureData<float>();
            nativeArray.CopyFrom(_depthData);
            _readableDepth.Apply();
        }
        
        private void ReadViaRenderTexture(RenderTexture source)
        {
            if (_depthRT == null || _depthRT.width != source.width || _depthRT.height != source.height)
            {
                if (_depthRT != null) _depthRT.Release();
                _depthRT = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.RFloat);
            }
            
            Graphics.Blit(source, _depthRT);
            
            if (_readableDepth == null || _readableDepth.width != _depthRT.width || _readableDepth.height != _depthRT.height)
            {
                _readableDepth = new Texture2D(_depthRT.width, _depthRT.height, TextureFormat.RFloat, false);
            }
            
            RenderTexture.active = _depthRT;
            _readableDepth.ReadPixels(new Rect(0, 0, _depthRT.width, _depthRT.height), 0, 0);
            _readableDepth.Apply();
            RenderTexture.active = null;
        }

        public PinholeCameraIntrinsic GetIntrinsics()
        {
            if (_cachedIntrinsics != null) return _cachedIntrinsics;

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
            
            if (_depthBuffer != null)
            {
                _depthBuffer.Release();
                _depthBuffer = null;
            }
        }
    }
}
