## Vodič za verifikaciju podataka

### 1. Pokretanje skeniranja
1. Pokrenite aplikaciju na Quest 3
2. Odaberite **"Object"** mod skeniranja
3. Usmerite kameru ka objektu i kliknite **Start Scan**
4. Pomerajte polako oko objekta (trajanje: 10-15 sekundi)
5. Kliknite **Stop Scan**

### 2. Preuzimanje podataka
Podaci se čuvaju u:
`/sdcard/Android/data/com.QuestGear.QuestGear3DScan/files/Scans/`

Koristite SideQuest ili `adb` da prebacite folder skeniranja na PC:
```bash
adb pull /sdcard/Android/data/com.QuestGear.QuestGear3DScan/files/Scans/LastScan ./MyScanData
```

### 3. Sadržaj skeniranja
Folder treba da sadrži:
- `scan_data.json`: Metapodaci (pozicije kamere, timestamps)
- `transforms.json`: Podaci za NerfStudio trening
- `color/`: Folder sa JPG slikama (frame_000000.jpg, ...)
- `depth/`: Folder sa PNG slikama (frame_000000.png, ...) - **VAŽNO:** Proverite da nisu crne/prazne. 16-bitni PNG može izgledati tamno u običnom vieweru, koristite Photoshop/GIMP/ImageJ i podesite Levels.

### 4. Provera JSON strukture
`scan_data.json` mora imati:
```json
{
  "scanId": "Scan_...",
  "frames": [
    {
      "frame_id": 0,
      "timestamp": 123.45,
      "color_file": "color/frame_000000.jpg",
      "depth_file": "depth/frame_000000.png",
      "pose": [ 1, 0, 0, 0, ... ] // 4x4 matrica, ne sme biti identity stalno
    },
    ...
  ]
}
```

### 5. Brza vizuelizacija (Python)
Kreirajte `verify_scan.py` u istom folderu i pokrenite:
```python
import json
import os
import cv2
import numpy as np

scan_path = "./MyScanData" # Podesite putanju

with open(os.path.join(scan_path, "scan_data.json")) as f:
    data = json.load(f)

print(f"Scan ID: {data['scanId']}")
print(f"Frames: {len(data['frames'])}")

for frame in data['frames'][:5]: # Prvih 5
    img_path = os.path.join(scan_path, frame['color_file'])
    depth_path = os.path.join(scan_path, frame['depth_file'])
    
    if not os.path.exists(img_path): print(f"MISSING COLOR: {img_path}")
    if not os.path.exists(depth_path): print(f"MISSING DEPTH: {depth_path}")
    
    # Provera depth opsega
    depth_img = cv2.imread(depth_path, cv2.IMREAD_UNCHANGED)
    if depth_img is not None:
        min_val, max_val, _, _ = cv2.minMaxLoc(depth_img)
        print(f"Frame {frame['frame_id']} Depth Range: {min_val} - {max_val}")
    else:
        print("Failed to load depth image")
```
