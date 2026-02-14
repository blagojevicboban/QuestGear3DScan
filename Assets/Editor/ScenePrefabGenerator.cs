using UnityEngine;
using UnityEditor;
using System.IO;

public class ScenePrefabGenerator : MonoBehaviour
{
    [MenuItem("QuestGear/Create Scene Prefabs")]
    public static void CreatePrefabs()
    {
        string path = "Assets/Prefabs";
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        // --- Create Scene Plane Prefab ---
        GameObject planeObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        planeObj.name = "ScenePlane";
        
        // Add core components
        CheckComponent<OVRSceneAnchor>(planeObj);
        CheckComponent<OVRScenePlane>(planeObj);
        CheckComponent<OVRSemanticClassification>(planeObj);

        // Setup Visuals
        MeshRenderer mr = planeObj.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); // Or Standard
            mr.sharedMaterial.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Semi-transparent grey
        }
        
        // Save as Prefab
        string planePath = Path.Combine(path, "ScenePlane.prefab");
        PrefabUtility.SaveAsPrefabAsset(planeObj, planePath);
        DestroyImmediate(planeObj);
        Debug.Log($"Created Scene Plane Prefab at: {planePath}");

        // --- Create Scene Volume Prefab ---
        GameObject volObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        volObj.name = "SceneVolume";
        
        CheckComponent<OVRSceneAnchor>(volObj);
        CheckComponent<OVRSceneVolume>(volObj);
        CheckComponent<OVRSemanticClassification>(volObj);

        mr = volObj.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mr.sharedMaterial.color = new Color(0.2f, 0.2f, 1f, 0.5f); // Semi-transparent blue
        }

        string volPath = Path.Combine(path, "SceneVolume.prefab");
        PrefabUtility.SaveAsPrefabAsset(volObj, volPath);
        DestroyImmediate(volObj);
        Debug.Log($"Created Scene Volume Prefab at: {volPath}");
    }

    static void CheckComponent<T>(GameObject go) where T : Component
    {
        if (go.GetComponent<T>() == null) go.AddComponent<T>();
    }
}
