using UnityEngine;

namespace QuestGear3D.Scan.Sensors
{
    /// <summary>
    /// Interface for depth data providers.
    /// Allows swapping between Quest internal depth and external sensors (e.g., Structure, RealSense).
    /// </summary>
    public interface IDepthProvider
    {
        /// <summary>
        /// Initializes the depth provider.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Returns the current depth texture.
        /// </summary>
        Texture2D GetDepthTexture();

        /// <summary>
        /// Returns the camera intrinsics for the depth sensor.
        /// </summary>
        /// <returns>Pinhole camera intrinsics</returns>
        Data.PinholeCameraIntrinsic GetIntrinsics();

        /// <summary>
        /// Returns the transformation matrix from Depth Camera to World space.
        /// </summary>
        Matrix4x4 GetCameraToWorldMatrix();

        /// <summary>
        /// Checks if the provider is ready and has valid data.
        /// </summary>
        bool IsReady();
    }
}
