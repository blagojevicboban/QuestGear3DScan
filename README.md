# QuestGear 3D Scan

**QuestGear 3D Scan** is a specialized Unity application for the **Meta Quest 3** headset, designed to capture synchronized RGB-D data for high-fidelity 3D reconstruction. This is the companion app to the **QuestGear 3D Studio** desktop suite.

## 🚀 Features

### Core Capture
-   **Real-time RGB-D Capture**: Synchronized Color (RGB) and Depth streams.
-   **6DoF Tracking**: Precise camera pose recording for every frame.
-   **NerfStudio Ready**: Automatically exports `transforms.json` for direct training of Gaussian Splats.
-   **Async I/O**: High-performance non-blocking data serialization via `ConcurrentQueue`.

### Depth & Visualization
-   **Environment Depth API**: Utilizes Meta's `EnvironmentDepthManager` for accurate geometry.
-   **Real-time Depth Point Cloud**: Live visualization using `EnvironmentRaycastManager` — color-coded by viewing angle (white = head-on/good, vivid = grazing/poor).
-   **External Depth Support**: Pluggable `IDepthProvider` interface for professional sensors (RealSense, Structure).
-   **QuestDepthProvider**: Concrete `IDepthProvider` implementation for Quest's built-in depth.

### Camera System
-   **Synchronized Capture Timer**: Centralized FPS-based timing (`CaptureTimer`) ensures camera and depth are captured in the same frame.
-   **OVRPlugin Intrinsics**: Real camera intrinsics from `OVRPlugin.GetNodeFrustum2()` with FOV-based fallback.
-   **Flashlight Control**: Android JNI torch mode for low-light environments.
-   **Lifecycle Management**: Proper camera pause/resume handling when removing/putting on headset.

### Data Management
-   **Wi-Fi Data Export**: Built-in HTTP server with styled dark-theme UI for wireless download.
-   **Session Management**: List, browse, and delete scan sessions from the web UI.
-   **ZIP Export**: Async background compression to Quest Downloads folder (`RecordingExporter`).
-   **Scene Capture**: Automatic extraction of room geometry via Meta Scene API.

### Development
-   **Offline Dev Mode**: Full mock implementation (`MockCameraProvider`) for testing without a headset in Unity Editor.
-   **Automated E2E Tests**: `AutomatedWorkflowTest` for basic scan-to-export validation.

## 📷 Scan Modes

-   **Object Mode**: 
    -   Optimized for capturing small to medium-sized objects.
    -   Focuses on detailed geometry and texture of a centered subject.
    -   Real-time depth point cloud shows capture coverage.
    -   Ideal for product scanning, artifacts, and props.

-   **Space Mode**: 
    -   Designed for room-scale scanning and environment capture.
    -   Captures broader geometry with a focus on spatial layout.
    -   Utilizes Meta Scene API for semantic understanding.
    -   Ideal for architectural surveys, room walkthroughs, and VR environment creation.

## 📦 Installation

### Prerequisites
-   Unity 6 (6000.3.x)
-   Meta Quest 3 Headset (Developer Mode enabled)
-   Android Build Support
-   Meta XR SDK v83+

### Building for Quest
1.  Open Project in Unity.
2.  Go to `File > Build Settings`.
3.  Switch platform to **Android**.
4.  Click **Build & Run**.

### Android Permissions
The following permissions are configured in `AndroidManifest.xml`:
-   `android.permission.CAMERA` — Camera access
-   `horizonos.permission.HEADSET_CAMERA` — Quest passthrough camera access
-   `android.permission.WRITE_EXTERNAL_STORAGE` — File export
-   `com.oculus.feature.BOUNDARYLESS_APP` — Passthrough mode support

## 🎮 How to Use

### Configuration
Before scanning, configure the following via the dashboard UI or `ScanController` inspector:
-   **Resolution**: Default `1280x720`. Higher resolutions provide more detail.
-   **Target FPS**: Capture frame rate (Object mode: 30, Space mode: 5).
-   **Flashlight**: Toggle device flashlight for dim environments.
-   **Start Delay**: Position yourself before scanning begins (0-10s).

### 1. Scanning
1.  Launch the app on your Quest 3.
2.  Select **Object Mode** or **Space Mode**.
3.  Position yourself in front of the object/room.
4.  Press **Start Scan** (Right Controller 'A' or UI Button).
5.  Move slowly around the subject. Watch the depth point cloud for coverage feedback.
6.  Press **Stop Scan** when finished.

### 2. Exporting Data (Wi-Fi)
After scanning, the app starts a local file server:
1.  Look at the dashboard status text for the IP address (e.g., `http://192.168.1.5:8080/`).
2.  Open that URL in your PC browser — you'll see a styled session list with file sizes.
3.  Browse and download individual files, or delete sessions directly from the web UI.

### 3. ZIP Export
Use `RecordingExporter` to compress scan sessions:
-   ZIPs are saved to `/sdcard/Download/Export/` on the Quest.
-   Export is async to avoid blocking the app.

### 4. Wired Export (USB)
Connect via USB-C:
```bash
adb pull /sdcard/Android/data/com.QuestGear3D.Scan/files/Scans/ ./MyScans/
```

## 📂 Data Format

Each scan is saved in a timestamped folder containing:
-   `scan_data.json`: Comprehensive metadata including camera intrinsics and frame poses.
-   `transforms.json`: **NerfStudio** compatible file for training Gaussian Splats.
-   `color/`: Directory containing RGB frames (`frame_XXXXXX.jpg`).
-   `depth/`: Directory containing 16-bit Depth Maps (`frame_XXXXXX.png`).
-   `debug_camera_log.txt`: Camera diagnostic info (devices, permissions, depth status).

## 🏗 Architecture

```
Assets/Scripts/
├── Core/
│   ├── ScanController.cs      # Main scan orchestrator
│   ├── QuestCameraProvider.cs  # Camera access + depth + intrinsics
│   ├── CaptureTimer.cs         # Synchronized FPS timing
│   ├── IFrameProvider.cs       # Frame provider interface
│   └── IDepthProvider.cs       # Depth provider interface
├── Data/
│   ├── ScanDataManager.cs      # Async data serialization
│   ├── ScanData.cs             # Data structures
│   ├── NerfStudioExporter.cs   # transforms.json export
│   └── RecordingExporter.cs    # ZIP export + session management
├── Integration/
│   └── ScanFileServer.cs       # HTTP server for Wi-Fi download
├── Mock/
│   └── MockCameraProvider.cs   # Editor testing without headset
├── Scan/Sensors/
│   ├── IDepthProvider.cs       # Depth sensor interface
│   └── QuestDepthProvider.cs   # Quest Environment Depth implementation
├── UI/
│   ├── ScanDashboard.cs        # Dashboard UI controller
│   └── ScanVisualization.cs    # Real-time depth point cloud
└── Tests/
    └── AutomatedWorkflowTest.cs # E2E test
```

## 📚 Documentation

-   [Offline Development Guide](docs/OFFLINE_DEV_GUIDE.md): How to work without a headset.
-   [Research: Spatial SDK & Splatting](RESEARCH_SPATIAL_SDK.md): Evaluation of Meta's visualization capabilities.
-   [Data Verification Guide](VerifyData_Guide.md): How to inspect and validate captured data.
