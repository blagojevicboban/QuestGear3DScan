# QuestGear 3D Scan - Feasibility Testing Guide

This document outlines the procedures for testing critical hardware access on the Meta Quest 3 for the 3D scanning project.

## 1. Camera Access (RGB)
**Script:** `Assets/Scripts/CameraAccessTest.cs`

### Goal
Verify if the application can access the raw RGB camera feed using standard Android `WebCamTexture`.

### Setup
1.  Add `CameraAccessTest` script to a GameObject in the scene.
2.  Assign a UI Text component to the `Status Text` field for logs.
3.  Ensure `AndroidManifest.xml` includes `<uses-permission android:name="android.permission.CAMERA" />`.

### Expected Results
-   **Success:** Log shows "First Camera frame received! Res: 1280x720" (or similar).
-   **Blocked (Privacy):** Log shows "Camera.Play() called" but `didUpdateThisFrame` never becomes true, or texture remains black.
    -   *Mitigation:* If blocked, we must use **Passthrough API** or **MRC** tools.

## 2. Depth API (Environment Depth)
**Script:** `Assets/Scripts/DepthAccessTest.cs`

### Goal
Verify if the application can retrieve depth data (Environment Depth) from the headset.

### Setup
1.  Add `DepthAccessTest` script to a GameObject.
2.  **Project Settings > Oculus > Quest 3 > Experimental Features:** Enable "Passthrough Support" and "Insight Passthrough".
3.  Ensure `OVRManager` is present in the scene.

### Expected Results
-   **Success:** Log shows "Passthrough Supported: True".
-   **Failure:** Log shows "Passthrough Supported: False" or "OVRManager missing".

## 3. Scene API (Room Scan)
**Script:** `Assets/Scripts/SceneApiTest.cs`

### Goal
Verify if the application can trigger the system-level Room Setup and load the resulting Scene Model (walls, tables, etc.).

### Setup
1.  Add `SceneApiTest` script to a GameObject.
2.  Add `OVRSceneManager` prefab/script to the scene.
3.  (Optional) Assign "Plane" and "Volume" prefabs to `OVRSceneManager` to visualize the room after loading.

### Testing
-   Call `RequestSceneCapture()` (can be linked to a button).
-   **Result:** The system "Room Setup" scanning flow should start.
-   After setup, call `LoadScene()`.
-   **Result:** Simple meshes representing walls and furniture should appear in the scene.

## Troubleshooting
-   **Black Screen:** Common with Passthrough if "Passthrough Support" is not enabled in OVRManager.
-   **No Logs:** Ensure the UI Text is correctly linked in the Inspector. Use `adb logcat -s Unity` to see logs on PC.
