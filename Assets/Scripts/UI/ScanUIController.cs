using UnityEngine;
using UnityEngine.UI;
using TMPro; // Added for TextMeshPro
using QuestGear3D.Scan.Core;

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
            
            // Controller Input Check (A / Trigger / X / Any Trigger)
            if (OVRInput.GetDown(OVRInput.Button.One) || 
                OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) || 
                OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger) ||
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetMouseButtonDown(0))
            {
                // HAPTIC FEEDBACK ON CLICK
                OVRInput.SetControllerVibration(0.5f, 0.5f, OVRInput.Controller.RTouch);
                OVRInput.SetControllerVibration(0.5f, 0.5f, OVRInput.Controller.LTouch);
                Invoke("StopVibration", 0.2f);
                
                // VISUAL FLASH (On Camera Background)
                if (Camera.main != null)
                {
                    Camera.main.backgroundColor = _isScanning ? Color.black : Color.green;
                    Camera.main.clearFlags = CameraClearFlags.SolidColor; 
                    // Note: This overrides passthrough momentarily if successful, which is good for debug
                }

                OnMainActionClicked();
            }

            // Update Status based on Controller state
            if (scanController != null)
            {
                if (scanController.IsScanning)
                {
                    UpdateStatus("Scanning... (Active)\nPress 'A' or Trigger to Stop");
                    UpdateButtonText("STOP SCAN");
                    _isScanning = true;
                }
                else
                {
                    if (_isScanning) // Just stopped
                    {
                        UpdateStatus("Saving Data...");
                        _isScanning = false;
                    }
                    else
                    {
                         UpdateStatus("Ready to Scan\nPress 'A' or Trigger to Start");
                    }
                    UpdateButtonText("START SCAN");
                }
            }
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
               
                // Find ANY camera
                Camera cam = Camera.main;
                if (cam == null) cam = FindObjectOfType<Camera>();
                
                if (cam != null)
                {
                    Debug.Log($"[ScanUI] Found Camera: {cam.name}");
                    mainCanvas.transform.SetParent(cam.transform, false);
                    mainCanvas.transform.localPosition = new Vector3(0, -0.1f, 0.5f);
                    mainCanvas.transform.localRotation = Quaternion.identity;
                    mainCanvas.transform.localScale = Vector3.one * 0.001f;
                }
                else
                {
                    Debug.LogError("[ScanUI] NO CAMERA FOUND!");
                }
            }
        }

        void LateUpdate()
        {
             // FORCE position every frame for debugging
             if (mainCanvas != null && mainCanvas.transform.parent != null)
             {
                 mainCanvas.transform.localPosition = new Vector3(0, -0.1f, 0.5f);
                 mainCanvas.transform.localRotation = Quaternion.identity;
             }
        }
    }
}
