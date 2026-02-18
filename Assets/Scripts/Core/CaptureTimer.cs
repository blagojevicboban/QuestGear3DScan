using UnityEngine;

namespace QuestGear3D.Scan.Core
{
    /// <summary>
    /// Manages FPS-based timing for synchronized data capture.
    /// Provides a frame-accurate timing signal for camera and depth capture.
    /// Ported from OpenQuestCapture's CaptureTimer.
    /// </summary>
    public class CaptureTimer : MonoBehaviour
    {
        [Header("Capture Timing")]
        [Tooltip("Target FPS for synchronized camera and depth capture. Set to 0 to capture at maximum rate.")]
        [SerializeField] private float targetCaptureFPS = 3f;

        private float lastCaptureTime = 0f;
        private float captureInterval = 0f;
        private bool shouldCaptureThisFrame = false;
        private bool isCapturing = false;

        /// <summary>
        /// Returns true if a capture should happen this frame.
        /// Both camera and depth should check this flag.
        /// </summary>
        public bool ShouldCaptureThisFrame => shouldCaptureThisFrame;

        /// <summary>
        /// Returns true if the timer is currently active.
        /// </summary>
        public bool IsCapturing => isCapturing;

        /// <summary>
        /// Gets the target capture FPS setting.
        /// </summary>
        public float TargetCaptureFPS => targetCaptureFPS;

        /// <summary>
        /// Sets the target capture FPS at runtime.
        /// </summary>
        public void SetTargetFPS(float fps)
        {
            targetCaptureFPS = fps;
            captureInterval = (fps > 0) ? (1f / fps) : 0f;
        }

        /// <summary>
        /// Starts the capture timer.
        /// </summary>
        public void StartCapture()
        {
            isCapturing = true;
            captureInterval = (targetCaptureFPS > 0) ? (1f / targetCaptureFPS) : 0f;
            // Set lastCaptureTime so first Update() triggers immediately
            lastCaptureTime = Time.unscaledTime - captureInterval;
            shouldCaptureThisFrame = false;

            Debug.Log($"[CaptureTimer] Started at {targetCaptureFPS} FPS (interval: {captureInterval}s)");
        }

        /// <summary>
        /// Stops the capture timer.
        /// </summary>
        public void StopCapture()
        {
            isCapturing = false;
            shouldCaptureThisFrame = false;

            Debug.Log("[CaptureTimer] Stopped");
        }

        private void Update()
        {
            if (!isCapturing)
            {
                shouldCaptureThisFrame = false;
                return;
            }

            // If no FPS limit (interval == 0), always capture
            if (captureInterval <= 0f)
            {
                shouldCaptureThisFrame = true;
                return;
            }

            float currentTime = Time.unscaledTime;

            if ((currentTime - lastCaptureTime) >= captureInterval)
            {
                shouldCaptureThisFrame = true;
                lastCaptureTime = currentTime;
            }
            else
            {
                shouldCaptureThisFrame = false;
            }
        }
    }
}
