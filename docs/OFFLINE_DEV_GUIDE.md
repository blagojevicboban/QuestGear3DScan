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
    -   Assign the `[ScanManager]` object to the `Data Manager` field.
    -   Create a target object (e.g., a Cube at 0,0,0) and assign it to `Target Pivot`.

3.  **Setup UI Dashboard:**
    -   Create a Canvas.
    -   Add buttons for "Start Scan" and "Stop Scan".
    -   Add a Text element for Status.
    -   Create an Empty GameObject `[Dashboard]` and add `ScanDashboard` script.
    -   Link the Buttons and Text to the script.
    -   Link `[ScanManager]` and `[MockCameraProvider]` to the script.

## How to Test
1.  Press **Play** in Unity Editor.
2.  Click **Start Scan** (or press Space if Mock Provider has input enabled).
3.  The Camera should assume an orbit around the target.
4.  Logs will show "Started scan: ...".
5.  Wait a few seconds, then click **Stop Scan**.
6.  Navigate to `C:/Users/[User]/AppData/LocalLow/DefaultCompany/QuestGear3DScan/Scans/` (or similar persistent path) to see the generated frames and JSON.
