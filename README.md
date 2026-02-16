# QuestGear 3D Scan

**QuestGear 3D Scan** is a specialized Unity application for the **Meta Quest 3** headset, designed to capture synchronized RGB-D data for high-fidelity 3D reconstruction. This is the companion app to the **QuestGear 3D Studio** desktop suite.

## 🚀 Features

-   **Real-time RGB-D Capture**: Synchronized Color (RGB) and Depth streams.
-   **6DoF Tracking**: Precise camera pose recording for every frame.
-   **Wi-Fi Data Export**: Built-in HTTP server for wireless download of scan data.
-   **Offline Dev Mode**: Full mock implementation for testing without a headset in Unity Editor.
-   **Async I/O**: High-performance non-blocking data serialization.
-   **NerfStudio Ready**: Automatically exports `transforms.json` for direct training of Gaussian Splats.
-   **Depth API Integration**: Utilizes Meta's Environment Depth for accurate geometry.
-   **[NEW] External Depth Support**: Pluggable interface for professional sensors (RealSense/Structure).
-   **[NEW] Scene Capture**: Automatic extraction of room geometry via Meta Scene API.

## 📷 Scan Modes

The application supports two distinct scanning modes tailored for different use cases:

-   **Object Mode**: 
    -   Optimized for capturing small to medium-sized objects.
    -   Focuses on detailed geometry and texture of a centered subject.
    -   Ideal for product scanning, artifacts, and props.

-   **Space Mode**: 
    -   Designed for room-scale scanning and environment capture.
    -   Captures broader geometry with a focus on spatial layout.
-   **Space Mode**: 
    -   Designed for room-scale scanning and environment capture.
    -   Captures broader geometry with a focus on spatial layout.
    -   Ideal for architectural surveys, room walkthroughs, and VR environment creation.
    -   *Note: Utilizes Meta Scene API for semantic understanding.*

## 📦 Installation

### Prerequisites
-   Unity 6 (6000.0.x)
-   Meta Quest 3 Headset (Developer Mode enabled)
-   Android Build Support

### Building for Quest
1.  Open Project in Unity.
2.  Go to `File > Build Settings`.
3.  Switch platform to **Android**.
4.  Click **Build & Run**.

## 🎮 How to Use

### Configuration
Before scanning, you can configure the following settings via the inspector (on `ScanController`) or UI (if exposed):
-   **Resolution**: Default is `1920x1080`. Higher resolutions provide more detail but may impact performance.
-   **Target FPS**: Adjust the capture frame rate (default broadcast is 30 FPS).
-   **Flashlight**: Toggle to enable the device flashlight for better lighting in dim environments.
-   **Start Delay**: Set a delay (in seconds) to position yourself before scanning starts.

### 1. Scanning
1.  Launch the app on your Quest 3.
2.  Position yourself in front of the object/room.
3.  Press **Start Scan** (Right Controller 'A' or UI Button).
4.  Move slowly around the subject. The UI shows the buffer status.
5.  Press **Stop Scan** when finished.

### 2. Exporting Data (Wi-Fi)
After scanning, the app starts a local file server.
1.  Look at the dashboard status text for the IP address (e.g., `http://192.168.1.5:8080/`).
2.  Open that URL in your PC browser.
3.  Navigate to the `Scans` folder and download your data.

### 3. Wired Export (USB)
Alternatively, connect via USB-C:
```bash
adb pull /sdcard/Android/data/com.QuestGear3D.Scan/files/Scans/ ./MyScans/
```

## 📂 Data Format

Each scan is saved in a timestamped folder containing:
-   `scan_data.json`: Comprehensive metadata including camera intrinsics and frame poses.
-   `transforms.json`: **NerfStudio** compatible file for training Gaussian Splats.
-   `color/`: Directory containing RGB frames (`frame_XXXXXX.jpg`).
-   `depth/`: Directory containing 16-bit Depth Maps (`frame_XXXXXX.png`).

## 📚 Documentation

-   [Offline Development Guide](docs/OFFLINE_DEV_GUIDE.md): How to work without a headset.
-   [Research: Spatial SDK & Splatting](RESEARCH_SPATIAL_SDK.md): Evaluation of Meta's visualization capabilities.
-   [Data Verification Guide](VerifyData_Guide.md): How to inspect and validate captured data.
