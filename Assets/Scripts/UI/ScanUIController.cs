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
        }

        void SetupUI()
        {
            if (mainCanvas != null)
            {
                mainCanvas.renderMode = RenderMode.WorldSpace;
                
                // Force parent to Main Camera (Head-Locked)
                if (Camera.main != null)
                {
                    mainCanvas.transform.SetParent(Camera.main.transform, false);
                    mainCanvas.transform.localPosition = new Vector3(0, -0.1f, 0.5f); // 50cm in front, slightly down
                    mainCanvas.transform.localRotation = Quaternion.identity;
                    mainCanvas.transform.localScale = Vector3.one * 0.001f; // Correct scale for World Space
                }
                else
                {
                    // Fallback if MainCamera not tagged
                    var cam = FindObjectOfType<Camera>();
                    if (cam != null)
                    {
                        mainCanvas.transform.SetParent(cam.transform, false);
                        mainCanvas.transform.localPosition = new Vector3(0, -0.1f, 0.5f);
                        mainCanvas.transform.localRotation = Quaternion.identity;
                        mainCanvas.transform.localScale = Vector3.one * 0.001f;
                    }
                }
            }
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

            // Update Status based on Controller state
            if (scanController != null)
            {
                if (scanController.IsScanning)
                {
                    UpdateStatus("Scanning... (Active)");
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
                         UpdateStatus("Ready to Scan");
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
    }
}
