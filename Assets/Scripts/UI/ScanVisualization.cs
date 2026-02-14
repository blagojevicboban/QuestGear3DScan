using UnityEngine;
using QuestGear3D.Scan.Core;
using QuestGear3D.Scan.Data;

namespace QuestGear3D.Scan.UI
{
    public class ScanVisualization : MonoBehaviour
    {
        [Header("References")]
        public ScanController scanController;
        public ParticleSystem pointCloudParticles;
        public GameObject sceneManagerObject; // Reference to OVRSceneManager GameObject

        [Header("Settings")]
        public int particleCount = 1000;

        private ParticleSystem.Particle[] _particles;

        void Start()
        {
            if (scanController == null) scanController = FindObjectOfType<ScanController>();
            
            if (pointCloudParticles != null)
            {
                _particles = new ParticleSystem.Particle[particleCount];
            }
        }

        void Update()
        {
            if (scanController == null) return;

            bool isSpaceMode = scanController.CurrentScanMode == ScanMode.Space;
            bool isScanning = scanController.IsScanning;

            // Space Mode: Enable Scene Manager visualization (Walls/Room)
            // We enable it even if not scanning, to help seeing the room? 
            // Or only when scanning? Usually room mesh is always helpful in Space mode.
            if (sceneManagerObject != null)
            {
                bool shouldShowRoom = isSpaceMode;
                if (sceneManagerObject.activeSelf != shouldShowRoom)
                {
                    sceneManagerObject.SetActive(shouldShowRoom);
                }
            }

            // Object Mode: Enable Point Cloud
            if (pointCloudParticles != null)
            {
                 // precise control over emission
                 var emission = pointCloudParticles.emission;
                 bool shouldEmit = !isSpaceMode && isScanning;
                 emission.enabled = shouldEmit;
                 
                 if (shouldEmit)
                 {
                     UpdatePointCloud();
                 }
                 else
                 {
                     pointCloudParticles.Clear();
                 }
            }
        }

        void UpdatePointCloud()
        {
             // Mock Point Cloud Visualization
             // Real implementation would read depth texture and project points.
             // Here we just simulate "scanning" activity with random points in front of camera
             
             int count = particleCount;
             // Reuse array
             if (_particles == null || _particles.Length != count) _particles = new ParticleSystem.Particle[count];

             Transform camTransform = Camera.main ? Camera.main.transform : transform;
             
             for (int i = 0; i < count; i++)
             {
                 Vector3 randomPos = Random.insideUnitSphere * 0.5f; // reduced radius
                 randomPos.z = Mathf.Abs(randomPos.z) + 0.3f; // forward only
                 
                 _particles[i].position = camTransform.TransformPoint(randomPos);
                 _particles[i].startColor = Color.Lerp(Color.blue, Color.cyan, Random.value);
                 _particles[i].startSize = 0.01f;
             }
             
             pointCloudParticles.SetParticles(_particles, count);
        }
    }
}
