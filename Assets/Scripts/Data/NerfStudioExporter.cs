using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace QuestGear3D.Scan.Data
{
    // Simplified structures matching NerfStudio transforms.json format
    [System.Serializable]
    public class NerfStudioData
    {
        public float camera_angle_x;
        public float camera_angle_y;
        public float fl_x;
        public float fl_y;
        public float k1;
        public float k2;
        public float p1;
        public float p2;
        public float cx;
        public float cy;
        public float w;
        public float h;
        public string aabb_scale;
        public List<NerfFrame> frames = new List<NerfFrame>();
    }

    [System.Serializable]
    public class NerfFrame
    {
        public string file_path;
        public float[][] transform_matrix; // 4x4 matrix as array of arrays
    }

    public static class NerfStudioExporter
    {
        public static void Export(string outputFolder, ScanData scanData)
        {
            if (scanData == null || scanData.frames == null) return;

            NerfStudioData nsData = new NerfStudioData();
            
            // Camera Intrinsics 
            // Note: Currently ScanData.intrinsic is hardcoded in ScanDataManager. 
            // In a robust implementation, this should come from the Camera API.
            // Assuming 1280x720 and ~90 deg FOV for generic Quest camera if not provided.
            
            nsData.w = scanData.intrinsic.width;
            nsData.h = scanData.intrinsic.height;
            nsData.fl_x = scanData.intrinsic.fx;
            nsData.fl_y = scanData.intrinsic.fy;
            nsData.cx = scanData.intrinsic.cx;
            nsData.cy = scanData.intrinsic.cy;
            nsData.k1 = 0; // Distortion coefficients assumed 0 or rectilinear for Passthrough
            nsData.k2 = 0;
            nsData.p1 = 0;
            nsData.p2 = 0;
            nsData.camera_angle_x = 2 * Mathf.Atan(nsData.w / (2 * nsData.fl_x));
            nsData.camera_angle_y = 2 * Mathf.Atan(nsData.h / (2 * nsData.fl_y));
            nsData.aabb_scale = "16";

            foreach (var frame in scanData.frames)
            {
                if (frame.pose == null || frame.pose.Length != 16) continue;

                Matrix4x4 unityPose = FloatArrayToMatrix(frame.pose);
                
                // Convert Coordinate System
                // Unity: LHS, Y-up, Z-fwd
                // NerfStudio/OpenGL: RHS, Y-up, Z-back? Or OpenCV? 
                // Standard NerfStudio transforms.json expects:
                // Camera looking down +Z (or -Z depending on convention), X right, Y down (OpenCV convention usually preferred for NerfStudio)
                // Actually NerfStudio supports various, but standard is:
                // +X Right, +Y Up, +Z Back (OpenGL) OR +X Right, +Y Down, +Z Forward (OpenCV).
                // Usually Unity -> OpenGL conversion is needed.
                // Matrix4x4 (Unity) -> Matrix4x4 (OpenGL)
                
                // Simple conversion: Flip Z axis of position and rotation?
                // For now, let's just export the raw matrix and assume post-processing or strict Unity loader.
                // But generally:
                // UnityWorldToGLWorld = Scale(1,1,-1)
                // UnityCamToGLCam = Scale(1,1,-1) * (Usually flips Z)
                
                // Let's implement a standard conversion: Unity (LHS, Y-Up) -> OpenGL (RHS, Y-Up, Z-Back camera look)
                // 1. Flip Z column
                Matrix4x4 nsMatrix = ConvertUnityToNerfStudio(unityPose);

                NerfFrame nsFrame = new NerfFrame();
                nsFrame.file_path = frame.color_file; // Relative path
                nsFrame.transform_matrix = new float[4][];
                for(int i=0; i<4; i++)
                {
                    nsFrame.transform_matrix[i] = new float[4];
                    for(int j=0; j<4; j++)
                    {
                        nsFrame.transform_matrix[i][j] = nsMatrix[i, j];
                    }
                }
                nsData.frames.Add(nsFrame);
            }

            // Serialize with custom NewtonSoft or generic JsonUtility wrapper that support nested text?
            // JsonUtility has limits with nested arrays (float[][]). We might need a manual serializer or helper.
            // Or simple string builder for the matrix part.
            
            // Since we can't easily rely on Newtonsoft in a raw script without checking dependencies,
            // let's use a simple custom string builder for the final JSON to ensure format correctness.
            
            string json = SerializeNerfData(nsData);
            File.WriteAllText(Path.Combine(outputFolder, "transforms.json"), json);
        }

        private static Matrix4x4 FloatArrayToMatrix(float[] arr)
        {
            Matrix4x4 m = new Matrix4x4();
            m.m00 = arr[0]; m.m01 = arr[1]; m.m02 = arr[2]; m.m03 = arr[3];
            m.m10 = arr[4]; m.m11 = arr[5]; m.m12 = arr[6]; m.m13 = arr[7];
            m.m20 = arr[8]; m.m21 = arr[9]; m.m22 = arr[10]; m.m23 = arr[11];
            m.m30 = arr[12]; m.m31 = arr[13]; m.m32 = arr[14]; m.m33 = arr[15];
            return m;
        }

        private static Matrix4x4 ConvertUnityToNerfStudio(Matrix4x4 unityPose)
        {
            // Unity is LHS. NerfStudio usually expects OpenGL style RHS.
            // Conversion:
            // 1. Position: z -> -z
            // 2. Rotation: Invert Z axis implication.
            
            Matrix4x4 matrix = unityPose;
            
            // Flip Translation Z
            matrix.m23 *= -1; 
            
            // Flip Rotation Z columns/rows effectively?
            // The standard way:
            // m[0,2] *= -1;
            // m[1,2] *= -1;
            // m[2,0] *= -1;
            // m[2,1] *= -1;
            
            // Ideally:
            // Unity (LHS, Y-up, Z-fwd) -> OpenGL (RHS, Y-up, Z-back)
            
            // Step 1: Convert position vector from LHS to RHS.
            // Pos (x,y,z) -> (x,y,-z)
            // This is equivalent to scaling the third column of the translation (m03, m13, m23)
            // But since Matrix4x4 stores translation in column 3 (m03, m13, m23), we just negate m23? No.
            // m23 is Z translation. Yes.
            
            // Step 2: Convert Rotation.
            // Z-axis needs to be inverted. 
            // This means the Z-basis vector (3rd column: m02, m12, m22) needs to be inverted (scaled by -1).
            
            // Combining:
            // Scale(1,1,-1) * M * Scale(1,1,-1) ?
            // Let's verify:
            // M * S = [X, Y, Z, T] * diag(1,1,-1,1) = [X, Y, -Z, T] (Scales the Z-basis vector)
            // S * (M*S) = diag(1,1,-1,1) * [X, Y, -Z, T] = [1*X, 1*Y, -1*-Z, -1*T_z] = [X, Y, Z, -T_z]
            
            // This flips Z translation AND Z rotation basis.
            // This transforms the ENTIRE space (points and orientations) to RHS.
            
            Matrix4x4 convert = Matrix4x4.Scale(new Vector3(1, 1, -1));
            return convert * unityPose * convert; 
        }

        private static string SerializeNerfData(NerfStudioData data)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"camera_angle_x\": {data.camera_angle_x.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"camera_angle_y\": {data.camera_angle_y.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"fl_x\": {data.fl_x.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"fl_y\": {data.fl_y.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"k1\": {data.k1.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"k2\": {data.k2.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"p1\": {data.p1.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"p2\": {data.p2.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"cx\": {data.cx.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"cy\": {data.cy.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"w\": {data.w.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"h\": {data.h.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"aabb_scale\": {data.aabb_scale},");
            sb.AppendLine("  \"frames\": [");
            
            for(int i=0; i<data.frames.Count; i++)
            {
                var f = data.frames[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"file_path\": \"{f.file_path}\",");
                sb.AppendLine("      \"transform_matrix\": [");
                for(int r=0; r<4; r++)
                {
                    sb.Append("        [");
                    for(int c=0; c<4; c++)
                    {
                        sb.Append(f.transform_matrix[r][c].ToString(System.Globalization.CultureInfo.InvariantCulture));
                        if(c<3) sb.Append(", ");
                    }
                    sb.Append("]");
                    if(r<3) sb.AppendLine(","); else sb.AppendLine("");
                }
                sb.AppendLine("      ]");
                sb.Append("    }");
                if(i < data.frames.Count - 1) sb.AppendLine(","); else sb.AppendLine("");
            }
            
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
