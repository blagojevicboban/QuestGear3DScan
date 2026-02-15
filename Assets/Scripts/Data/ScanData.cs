using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestGear3D.Scan.Data
{
    [Serializable]
    public enum ScanMode
    {
        Object,
        Space
    }

    [Serializable]
    public class ScanData
    {
        public string scanId;
        public string scanMode; // Store as string for JSON readability
        public ScanSettings settings;
        public PinholeCameraIntrinsic intrinsic;
        public List<ScanFrameMetadata> frames = new List<ScanFrameMetadata>();
    }

    [Serializable]
    public class ScanSettings
    {
        public string resolution;
        public int targetFPS;
        public bool useFlashlight;
    }

    [Serializable]
    public class PinholeCameraIntrinsic
    {
        public int width;
        public int height;
        // Direct access for convenience, or we use intrinsic_matrix.
        public float fx; 
        public float fy;
        public float cx;
        public float cy;

        public PinholeCameraIntrinsic(int w, int h, float fx, float fy, float cx, float cy)
        {
            this.width = w;
            this.height = h;
            this.fx = fx;
            this.fy = fy;
            this.cx = cx;
            this.cy = cy;
        }
    }

    [Serializable]
    public class CameraMatrix
    {
        // 3x3 matrix flattened or simplifed
        // [ fx  0  cx ]
        // [ 0  fy  cy ]
        // [ 0   0   1 ]
        public float fx;
        public float fy;
        public float cx;
        public float cy;
    }

    [Serializable]
    public class ScanFrameMetadata
    {
        public int frame_id;
        public double timestamp;
        public string color_file; // e.g. "color_0001.jpg"
        public string depth_file; // e.g. "depth_0001.png" (16-bit) or .raw
        public float[] pose; // 4x4 matrix flattened (16 floats) - WorldToCamera or CameraToWorld? 
                             // Open3D usually wants CameraToWorld (Extrinsics^-1)
    }
}
