# Offline Development & Testing Guide

Since we don't always have the Quest 3 headset available, we have implemented an **Offline/Mock Mode** that allows developing and testing the scanning pipeline directly in the Unity Editor.

## Components
-   **`ScanData.cs`**: Defines the JSON structure for the scan data (intrinsics, poses, frames).
-   **`ScanDataManager.cs`**: Handles saving images and JSON metadata to disk.
-   **`MockCameraProvider.cs`**: Simulates a moving camera and generates fake "Camera" and "Depth" frames.
-   **`ScanDashboard.cs`**: Simple UI to Start/Stop the scan.

## Setup Instructions (Unity Editor)

1.  **Create a Scan Manager:**
    -   Create an Empty GameObject named `[ScanManager]`.
    -   Add the `ScanDataManager` script to it.
    -   (Optional) Change `Scan Root Directory` if needed (default: "Scans").

2.  **Create a Mock Camera:**
    -   Create a Camera in the scene (or use Main Camera).
    -   Add the `MockCameraProvider` script to it.
    -   Create a target object (e.g., a Cube at 0,0,0) and assign it to `Target Pivot`.
    -   *Note*: Do not link Data Manager here, Controller handles it.

3.  **Create Scan Controller:**
    -   Create an Empty GameObject `[ScanController]`.
    -   Add `ScanController` script.
    -   Assign `[ScanManager]` to `Data Manager`.
    -   Assign `[MockCamera]` (the object with MockCameraProvider) to `Frame Provider Object`.

4.  **Setup UI Dashboard:**
    -   Create a Canvas.
    -   Add buttons for "Start Scan" and "Stop Scan".
    -   Add a Text element for Status.
    -   Create an Empty GameObject `[Dashboard]` and add `ScanDashboard` script.
    -   Link the Buttons and Text to the script.
    -   Link `[ScanController]` to the script.

5.  **Setup Data Export Server (Optional):**
    -   Create an Empty GameObject `[FileServer]`.
    -   Add the `ScanFileServer` script.
    -   (Optional) Change port if needed (default 8080).
    -   (Optional) Change port if needed (default 8080).
    -   When running, the IP address will appear on the Dashboard.

## D. Testing with Meta XR Simulator (Recommended)

The **Meta XR Simulator** allows you to test the app on your PC while simulating headset tracking.

### Setup
1.  Go to `Edit > Project Settings > XR Plug-in Management`.
2.  Enable the **Oculus** checkbox for the **Windows/PC** tab (Monitor icon).
3.  Launch the **Meta XR Simulator** from Unity or external tool.

### How to Run
1.  In `ScanController`, ensure `QuestCameraProvider` is assigned (instead of Mock).
    -   *Note*: Simulator provides tracking data via `OVRCameraRig`.
    -   *Note*: `WebCamTexture` will likely use your PC's webcam if available.
2.  Press **Play** in Unity.
3.  Use the Simulator controls (WASD + Mouse) to move the "Headset".
4.  Scanning will record data using the simulated position and PC Webcam feed.

## E. Automated Verification
To run an automatic health check:
1.  Create an empty GameObject `[AutoTest]`.
2.  Add `AutomatedWorkflowTest.cs` script.
3.  Press Play.
4.  Watch the Console for `[TEST] ALL CHECKS PASSED`.

## How to Test (Manual)
1.  Press **Play** in Unity Editor.
2.  Click **Start Scan** (or press Space if Mock Provider has input enabled).
3.  The Camera should assume an orbit around the target.
4.  Logs will show "Started scan: ...".
5.  Wait a few seconds, then click **Stop Scan**.
6.  Navigate to `C:/Users/[User]/AppData/LocalLow/DefaultCompany/QuestGear3DScan/Scans/` (or similar persistent path) to see the generated frames and JSON.
