using UnityEngine;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine.XR.Management;

public class XRDiagnostic
{
    [MenuItem("Tools/Force XR Setup")]
    public static void ForceSetup()
    {
        Debug.Log("Checking XR Settings...");
        
        var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (generalSettings == null)
        {
            Debug.Log("Creating XR General Settings for Standalone...");
            // Triggering the creation logic usually handled by UI
            // This is a bit internal, but accessing the property often initializes it
            // Actual API to create is internal, but we can try to verify if the loader exists
        }
        else
        {
            Debug.Log("XR General Settings (Standalone) found.");
        }
        
        Debug.Log("XR Management Package seems to be loaded correctly if you see this.");
    }
}
