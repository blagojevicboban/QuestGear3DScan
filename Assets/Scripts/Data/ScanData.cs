using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestGear3D.Scan.Data
{
    [Serializable]
    public class ScanData
    {
        public PinholeCameraIntrinsic intrinsic;
        public List<ScanFrameMetadata> frames = new List<ScanFrameMetadata>();
    }

    [Serializable]
    public class PinholeCameraIntrinsic
    {
        public int width;
        public int height;
        public CameraMatrix intrinsic_matrix;

        public PinholeCameraIntrinsic(int w, int h, float fx, float fy, float cx, float cy)
        {
            width = w;
            height = h;
            intrinsic_matrix = new CameraMatrix
            {
                fx = fx,
                fy = fy,
                cx = cx,
                cy = cy
            };
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
