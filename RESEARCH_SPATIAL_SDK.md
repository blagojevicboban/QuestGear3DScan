# Research: Meta Spatial SDK & Gaussian Splatting

## Overview
Meta has introduced native support for **Gaussian Splatting** in the **Meta Spatial SDK**. This technology allows for photorealistic rendering of captured scenes on Quest 3 and Quest 3S.

## Key Findings

### 1. Visualization on Quest 3 (Meta Spatial SDK)
- **Support**: Native support for rendering `.ply` and `.spz` (Scaniverse compressed) files.
- **Performance**: Recommended limit is **~150,000 splats** for stable performance on standalone Quest 3.
- **Usage**: Can load and position Gaussian Splats in the scene.
- **Limitations**: Currently single splat support (one active model), transparency sorting issues may occur with complex scenes.

### 2. Alternative: Unity Gaussian Splatting (Community)
- **Solution**: Aras Pranckevičius's package + VR fork (ninjamode).
- **Pros**: More control, open source, supports URP.
- **Cons**: Requires careful optimization for Quest 3 (400k splats @ 72fps reported with aggressive tuning).

## Proposed Workflow for QuestGear 3D

Since training Gaussian Splats on-device is not performance-viable yet, the workflow should be:

1.  **Capture (QuestGear 3D Scan)**
    *   Capture RGB Images + Camera Analysis (Poses).
    *   *Improvement*: Export directly to **NerfStudio** compatible `transforms.json` format to skip COLMAP step on PC.
    *   Save to `/sdcard/.../Scan_X`.

2.  **Process (PC / Cloud)**
    *   Transfer data to PC.
    *   Train model using **NerfStudio** (`ns-train splatfacto`) or **Postshot**.
    *   Export trained model as `.ply` or `.spz`.

3.  **View (QuestGear 3D Studio)**
    *   Import `.ply` file.
    *   Use **Meta Spatial SDK** to render the environment in Mixed Reality.

## Implementation Steps for "Scan" App
To support this workflow, we need to ensure our data matches the requirements:
- **Sharp Images**: Motion blur ruins Splats. Ensure fast shutter or "Stop-and-Capture" mode (already have Object Mode).
- **Accurate Poses**: OVR poses are good, but need conversion to Computer Vision frame (Y-down vs Y-up).
- **Export Format**: Create a `SplatDataExporter` that generates `transforms.json` for NerfStudio.

## Conclusion
The Spatial SDK is a viable *viewer* solution. For the *scanner*, our priority is high-quality data export.
