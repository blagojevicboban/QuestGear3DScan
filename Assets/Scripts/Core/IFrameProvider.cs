using UnityEngine;

using QuestGear3D.Scan.Data;

namespace QuestGear3D.Scan.Core
{
    public struct FrameData
    {
        public Texture2D ColorTexture;
        public Texture2D DepthTexture;
        public Matrix4x4 CameraPose; // Local to World
        public double Timestamp;
    }

    public interface IFrameProvider
    {
        // Initializes the provider (e.g. starts webcam, passthrough)
        void Initialize();

        // Starts data stream
        void StartStream();

        // Stops data stream
        void StopStream();

        // Returns true if a new frame is available since last check
        bool HasNewFrame();

        // Retrieves the latest frame data. 
        // Should return a FrameData with valid textures/matrices.
        // Retrieves the latest frame data. 
        // Should return a FrameData with valid textures/matrices.
        FrameData GetLatestFrame();

        // Configuration
        void SetResolution(int width, int height);
        void SetFPS(int fps);
        void SetFlashlight(bool enabled);

        // Intrinsics
        PinholeCameraIntrinsic GetIntrinsics();
    }
}
