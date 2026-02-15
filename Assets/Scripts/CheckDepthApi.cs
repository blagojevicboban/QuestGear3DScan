using UnityEngine;

public class CheckDepthApi : MonoBehaviour
{
    void Start()
    {
        int id = 0;
        // Uncomment the line below to check compilation. 
        // If it compiles, we are good.
        /* 
        if (OVRPlugin.GetEnvironmentDepthTextureId(ref id))
        {
            Debug.Log("Depth API exists!");
        }
        */
        
        // Actually, let's try to access it via reflection to be safe about compilation
        // But for development, direct call is better.
    }
}
