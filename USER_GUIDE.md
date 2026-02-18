# QuestGear 3D Scan - User Guide

This application allows you to scan your environment (Space Mode) or individual objects (Object Mode) using the Meta Quest 3.

## Main Controls

| Action | Controller Button | Description |
|---|---|---|
| **Start Scan** | **A (Right)** | Starts scanning. In Space Mode, launches Scene Capture (Room Setup). In Object Mode, starts capturing frames. |
| **Stop Scan** | **A (Right)** | Stops active scan and saves data. |
| **Switch Mode** | **X (Left)** | Toggles between **SPACE** and **OBJECT** mode. |
| **Recenter UI** | **Y (Left)** | Moves the UI Menu from your Left Wrist to float in front of your head. Use if you cannot see the menu. |

---

## Scanning Modes

### 1. Object Mode (Photogrammetry)
Use this mode to capture images of a specific object for 3D reconstruction.

1. Ensure mode is **OBJECT** (default).
2. Position yourself looking at the object.
3. Press **A (Start)**. The app starts capturing synchronized RGB-D frames.
4. Move slowly around the object to capture all angles.
5. Watch the **depth point cloud** — white points mean good coverage, vivid colors indicate grazing angles (move to get a better view of those areas).
6. Press **A (Stop)** to finish.

**Output:**
- Folder: `color/` — RGB images (JPG).
- Folder: `depth/` — 16-bit Depth maps (PNG).
- File: `scan_data.json` — Camera intrinsics + frame poses.
- File: `transforms.json` — NerfStudio-compatible format for Gaussian Splat training.

### 2. Space Mode (Room Scanning)
Use this mode to capture walls, floors, ceilings, and furniture in your room.

1. Press **X** to switch to **SPACE**.
2. Press **A (Start)**. The app will launch the system **Room Setup**.
3. Follow the Quest instructions to map your room (look around, mark walls/furniture).
4. When finished, you will return to the app. Wait a few seconds for the room model to load.
5. Press **A (Stop)** to save the room geometry.

**Output:** 
- File: `scene_data.json` — Room dimensions, positions, and UUIDs.

---

## Dashboard Settings

| Setting | Default | Description |
|---|---|---|
| Resolution | 1280×720 | Capture resolution. Higher = more detail, lower = better performance. |
| Target FPS | 30 (Object) / 5 (Space) | Frames per second for capture. Auto-adjusted by mode. |
| Flashlight | Off | Toggle device torch for dark environments. |
| Start Delay | 0s | Countdown before scan starts (0-10s). |

---

## Accessing Data

### Wi-Fi Download (Recommended)
After scanning, a local HTTP server starts automatically.

1. Check the dashboard status for the server IP (e.g., `http://192.168.1.5:8080/`).
2. Open the URL in any browser on the same network.
3. Browse sessions — each shows file size and has a **Delete** button.
4. Click files to download, or use **Delete** to remove old sessions.

### ZIP Export
Scan sessions can be compressed and exported to the Quest Downloads folder:
- **Path:** `/sdcard/Download/Export/[SessionName].zip`
- Export runs in background without blocking the app.

### USB (Wired)
Connect via USB-C cable:
```bash
adb pull /sdcard/Android/data/com.Mejkerslab.QuestGear3DScan/files/Scans/ ./MyScans/
```

---

## Troubleshooting

| Problem | Solution |
|---|---|
| **Menu not visible** | Look at left wrist (watch position). Press **Y** to reset in front of face. |
| **Camera is black/pink** | Check Settings → Apps → QuestGear 3D Scan → Permissions. Enable Camera access. Ensure `horizonos.permission.HEADSET_CAMERA` is granted. |
| **Room not loading** | Complete Room Setup fully. Try restarting the app. |
| **No depth data** | Ensure Environment Depth is supported on your device (Quest 3/3S only). |
| **Camera stops after pause** | This is now auto-handled — camera restarts when you put the headset back on. |
| **External Depth** | Assign sensor to `QuestCameraProvider.externalDepthSource` in Inspector. The app auto-prioritizes it over internal depth. |
