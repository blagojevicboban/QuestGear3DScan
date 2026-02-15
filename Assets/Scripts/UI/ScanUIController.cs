using UnityEngine;
using UnityEngine.UI;
using TMPro; // Added for TextMeshPro
using QuestGear3D.Scan.Core;
using UnityEngine.EventSystems;
using QuestGear3D.Scan.Data;

namespace QuestGear3D.Scan.UI
{
    public class ScanUIController : MonoBehaviour
    {
        [Header("Controllers")]
        public ScanController scanController;
        public QuestCameraProvider cameraProvider;

        [Header("UI Elements")]
        public Button mainActionButton;
        
        // Use TMP_Text if available, or just TextMeshProUGUI
        public TMP_Text buttonText; 
        public TMP_Text statusText;
        
        public RawImage cameraPreview;
        public Canvas mainCanvas;

        private bool _isScanning = false;

        void Start()
        {
            // Auto-find dependencies if missing
            if (scanController == null) scanController = FindObjectOfType<ScanController>();
            if (cameraProvider == null) cameraProvider = FindObjectOfType<QuestCameraProvider>();

            if (mainActionButton != null)
            {
                mainActionButton.onClick.AddListener(OnMainActionClicked);
            }

            SetupUI();
            
            // Set initial status
            UpdateStatus("System Initializing...");

            // HAPTIC FEEDBACK ON START (Confirm script runs)
            OVRInput.SetControllerVibration(1, 1, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(1, 1, OVRInput.Controller.LTouch);
            Invoke("StopVibration", 0.5f);
        }

        void StopVibration()
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        }

        void Update()
        {
            // Update Preview
            if (cameraProvider != null && cameraPreview != null)
            {
                var tex = cameraProvider.GetPreviewTexture();
                if (tex != null && cameraPreview.texture != tex)
                {
                    cameraPreview.texture = tex;
                }
            }
            
            // Controller Input Check (X / B for Mode Switch)
            // Log raw input for debug
            if (OVRInput.GetDown(OVRInput.Button.Three)) Debug.Log("[ScanUI] Button Three (X) Pressed");
            if (OVRInput.GetDown(OVRInput.Button.One)) Debug.Log("[ScanUI] Button One (A) Pressed");

            if (!scanController.IsScanning && (OVRInput.GetDown(OVRInput.Button.Three) || OVRInput.GetDown(OVRInput.Button.Two) || Input.GetMouseButtonDown(1)))
            {
                Debug.Log("[ScanUI] Toggle Mode Input Detected");
                // Toggle Mode
                ScanMode newMode = (scanController.CurrentScanMode == ScanMode.Object) ? ScanMode.Space : ScanMode.Object;
                scanController.SetScanMode(newMode);
                
                // Feedback
                OVRInput.SetControllerVibration(0.5f, 0.5f, OVRInput.Controller.RTouch);
                Invoke("StopVibration", 0.1f);
            }

            // Controller Input Check (A / Trigger / Space / Click)
            // Controller Input Check (A / Space / Click)
            // Removed Triggers to prevent double-fire with UI Buttons.
            if (OVRInput.GetDown(OVRInput.Button.One) || 
                Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log($"[ScanUI] Main Action Input Detected. IsScanning: {scanController.IsScanning}");
                // HAPTIC FEEDBACK ON CLICK
                OVRInput.SetControllerVibration(0.5f, 0.5f, OVRInput.Controller.RTouch);
                OVRInput.SetControllerVibration(0.5f, 0.5f, OVRInput.Controller.LTouch);
                Invoke("StopVibration", 0.2f);
                
                // VISUAL FLASH (On Camera Background)
                if (Camera.main != null)
                {
                    // Use Transparent for Passthrough Underlay compatibility
                    Camera.main.clearFlags = CameraClearFlags.SolidColor; 
                    // Flash semi-transparent green briefly, then return to clear (black transparent)
                    Camera.main.backgroundColor = new Color(0, 1, 0, 0.3f); 
                    Invoke("ResetCameraBackground", 0.1f);
                }

                OnMainActionClicked();
            }

            // Update Status based on Controller state
            if (scanController != null)
            {
                string modeText = $"MODE: {scanController.CurrentScanMode.ToString().ToUpper()}";
                
                if (scanController.IsScanning)
                {
                    UpdateStatus($"{modeText}\nScanning... (Active)\nPress 'A' to Stop");
                    UpdateButtonText("STOP SCAN");
                    _isScanning = true;
                }
                else
                {
                    if (_isScanning) // Just stopped
                    {
                        StartCoroutine(ShowSavedStatus());
                        _isScanning = false;
                    }
                    else if (!_isShowingSavedMessage) // Only show Ready if not showing saved message
                    {
                            UpdateStatus($"{modeText}\nReady to Scan\nPress 'A' to Start\nPress 'X' to Switch Mode");
                    }
                    UpdateButtonText("START SCAN");
                }
            }
        }

        private bool _isShowingSavedMessage = false;
        System.Collections.IEnumerator ShowSavedStatus()
        {
            _isShowingSavedMessage = true;
            UpdateStatus("<color=green>SCAN SAVED!</color>\nCheck: Android/data/.../files/Scans/");
            // Vibrate to confirm save
            OVRInput.SetControllerVibration(1, 1, OVRInput.Controller.RTouch);
            yield return new WaitForSeconds(0.5f);
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            
            yield return new WaitForSeconds(3.0f);
            _isShowingSavedMessage = false;
        }

        public void OnMainActionClicked()
        {
            if (scanController == null) return;

            if (scanController.IsScanning)
            {
                scanController.StopScan();
            }
            else
            {
                scanController.StartScan();
            }
        }

        void UpdateStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        void UpdateButtonText(string text)
        {
            if (buttonText != null) buttonText.text = text;
        }
        void SetupUI()
        {
            if (mainCanvas != null)
            {
                mainCanvas.renderMode = RenderMode.WorldSpace;
               
                // Find OVRCameraRig anchors (Optional)
                Transform anchor = null;
                OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
                
                // DEFAULT TO HEAD (Safe)
                Camera cam = Camera.main;
                if (cam == null) cam = FindObjectOfType<Camera>();
                if (cam != null) anchor = cam.transform;

                // Note: We can switch to Hand via Y button if needed, but start with Head to ensure visibility
                if (anchor != null)
                {
                    mainCanvas.transform.SetParent(anchor, false);
                    ResetUIPosition(); // Apply initial position
                }
                else
                {
                    Debug.LogError("[ScanUI] NO ANCHOR FOUND!");
                }
                
                FixPassthrough();
            }
        }

        void FixPassthrough()
        {
            var layer = FindObjectOfType<OVRPassthroughLayer>();
            if (layer != null)
            {
                Debug.Log("[ScanUI] Setting OVRPassthroughLayer to UNDERLAY.");
                // Note: overlayType property uses the OVROverlay.OverlayType enum in some SDK versions
                layer.overlayType = OVROverlay.OverlayType.Underlay;
            }
        }

        public void ResetUIPosition()
        {
            if (mainCanvas == null) return;
            
            // Force Resolution/Size
            RectTransform rt = mainCanvas.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(600, 400);

            // Default: Float in front of Head
            mainCanvas.transform.localPosition = new Vector3(0, -0.1f, 0.6f); 
            mainCanvas.transform.localRotation = Quaternion.identity;
            mainCanvas.transform.localScale = Vector3.one * 0.001f;
        }

        void LateUpdate()
        {
             // Debug.Log($"[ScanUI DEBUG] Canvas LocalPos: {mainCanvas.transform.localPosition} Active: {mainCanvas.gameObject.activeSelf}");

             // Recenter UI (Y Button) - Force to Head
             if (OVRInput.GetDown(OVRInput.Button.Four))
             {
                 Debug.Log("[ScanUI] Recenter UI Requested");
                 Camera cam = Camera.main;
                 if (cam != null)
                 {
                     mainCanvas.transform.SetParent(cam.transform, false);
                     ResetUIPosition();
                     OVRInput.SetControllerVibration(0.5f, 0.5f, OVRInput.Controller.LTouch);
                     Invoke("StopVibration", 0.2f);
                 }
             }
        }

        void ResetCameraBackground()
        {
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = new Color(0, 0, 0, 0f); // Fully Transparent Black
            }
        }
    }
}
