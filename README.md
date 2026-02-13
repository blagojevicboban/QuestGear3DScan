# QuestGear 3D Scan

**QuestGear 3D Scan** is a specialized Unity application for the **Meta Quest 3** headset, designed to capture synchronized RGB-D data for high-fidelity 3D reconstruction. This is the companion app to the **QuestGear 3D Studio** desktop suite.

## 🚀 Features

-   **Real-time RGB-D Capture**: Synchronized Color (RGB) and Depth streams.
-   **6DoF Tracking**: Precise camera pose recording for every frame.
-   **Wi-Fi Data Export**: Built-in HTTP server for wireless download of scan data.
-   **Offline Dev Mode**: Full mock implementation for testing without a headset in Unity Editor.
-   **Async I/O**: High-performance non-blocking data serialization.

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
-   `scan_meta.json`: Camera intrinsics and frame metadata.
-   `frames/`: Directory containing:
    -   `color_XXXX.jpg`: RGB Frame.
    -   `depth_XXXX.png`: 16-bit Depth Map (millimeter scale).

## 📚 Documentation

-   [Offline Development Guide](docs/OFFLINE_DEV_GUIDE.md): How to work without a headset.
-   [Feasibility Tests](docs/FEASIBILITY_TESTS.md): API verification details.
