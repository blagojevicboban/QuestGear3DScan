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

### 1. Space Mode (Room Scanning)
Use this mode to capture walls, floors, ceilings, and furniture (boxes) in your room.

1. App starts in **Object Mode** by default. Press **X** to switch to **SPACE**.
2. Press **A (Start)**. The app will launch the system **Room Setup**.
3. Follow the Quest instructions to map your room (look around, mark walls/furniture).
4. When finished, you will return to the app. Wait a few seconds for the room model (grey walls, blue boxes) to load.
5. Press **A (Stop)** to save the room geometry.
   - Screen will flash "SCAN SAVED".

**Output:** 
- File: `scene_data.json` (contains room dimensions, positions, and UUIDs).

### 2. Object Mode (Photogrammetry)
Use this mode to capture images of a specific object for 3D reconstruction.

1. Ensure mode is **OBJECT**.
2. Position yourself looking at the object.
3. Press **A (Start)**. The app will start capturing images continuously.
4. Move slowly around the object to capture all angles.
5. Press **A (Stop)** to finish.
   - Screen will flash "SCAN SAVED".

**Output:**
- Folder: `color/` (RGB images, may be placeholder if camera permission restricted).
- Folder: `depth/` (Depth maps, if supported).
- File: `scan_data.json` (Camera poses).

---

## Accessing Data

All scan data is saved on the headset in the app's private storage folder.

**Path on Quest:**
`Inside Quest > Android > data > com.Mejkerslab.QuestGear3DScan > files > Scans > [Scan_Date_Time]`

**How to copy to PC:**
1. Connect Quest 3 to PC via USB.
2. Allow File Access in headset.
3. On PC, open File Explorer -> Quest 3 -> Internal Shared Storage.
4. Navigate to the path above.
5. Copy the scan folders to your PC for processing.

---

## Troubleshooting

- **I don't see the menu**: Look at your left wrist (watch position). If not visible, press **Y** on the left controller to reset it in front of your face.
- **Camera is Black/Pink**: The app might lack permission to access the camera or Quest 3 privacy settings block RGB access. Check Settings > Apps > Permissions.
- **Room not loading**: Ensure you completed Room Setup fully. Try restarting the app.
