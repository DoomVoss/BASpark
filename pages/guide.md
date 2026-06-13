# Documentation

Welcome to the BASpark Telemetry use and development documentation.

---

## Software Overview

BASpark is a Windows-based desktop mouse effects utility that deeply replicates the click visual effects style of *Blue Archive*.

The software adopts a hybrid architecture of **"WPF Frame + WebView2 Rendering"**, featuring lightweight performance, high efficiency, and low resource consumption. This software is completely open-source under the MIT License.

---

## Installation & Uninstallation

### Installation Steps
1. After downloading, double-click to run `BASpark_Installer_vX.X.X_x64.exe`.
2. Follow the installation wizard prompts, click "Next", and select the installation directory.
3. Once installation is complete, the software will run automatically and reside in the system tray.

### Uninstallation Steps
1. Open Windows **Settings** → **Apps** → **Installed Apps**.
2. Find **BASpark** in the list and click **Uninstall**.

---

## Basic Settings

### Switch Controls
* **Independent Switches**: You can independently choose to enable "Mouse Click Effects" or "Persistent Mouse Trail".
* **Button Filtering**: Supports "Left Click Only", "Right Click Only", "Left & Right Clicks", and "Middle Click Trigger".
* **Apply Changes**: ⚠️ **Important!** After modifying parameters, you must click the **"Apply Changes"** button in the top right corner for the settings to take effect.

### Advanced Features
* **Network Source Selection**: Choose your preferred server cluster for updates, announcements, and anonymous telemetry. Options include:
  * **Mainland China**: Optimized for users inside the regional firewall.
  * **Global**: Optimized for international networks.
  * **Follow Language (Default)**: Automatically selects the **Global** nodes if your language is non-Chinese, ensuring stable connectivity out of the box.
* **Run as Administrator**: Check this option if you need to display effects over high-privilege windows like the *Task Manager*.
* **Touchscreen Mode**: When enabled, mouse pointer effects remain active even if the system hides the cursor. This is ideal for touchscreens or specific full-screen application environments.
* **Launch on System Startup**: When enabled, the software will automatically run when Windows boots up.
* **Silent Launch**: When enabled, the software will automatically minimize to the system tray upon startup without popping up the main window.

---

## Visual Adjustments

In the "Visual Performance" sub-settings menu, you can adjust the following parameters using sliders or by **entering precise numbers (press Enter to confirm)**:

* **Scale Ratio**: Supports 0.5x to 3.0x to adapt to different screen resolutions.
* **Global Opacity**: Adjusts the overall visual intensity and transparency of the effects.
* **Animation Speed**: Adjusts the overall playback speed of the effects.
* **Speed Synchronization**: Enabled by default. Unchecking this allows you to independently adjust "Click Speed" and "Trail Speed".
* **Trail Refresh Rate**: Supports up to 240Hz. Higher values offer smoother paths but consume relatively more system resources.

### Theme Colors
Click the **"Change Color"** button to bring up the color palette and customize the core color of the particles.

### Reset to Default
Click the **"Restore Default Settings"** button to selectively reset items to their default values.

---

## Intelligent Environment Filter

### Context Awareness Settings
* **Smart Environment Detection**: Automatically senses the window currently underneath the mouse in real time.
* **Auto-Pause on Full Screen**: Automatically suspends effect rendering when a full-screen game or video is detected to save performance and prevent distractions.
* **Desktop Effects Toggle**: Choose whether to display particles on the Windows desktop background.

### Multi-Profile Management
Supports creating multiple independent sets of filtering rules for different scenarios (e.g., "Office", "Gaming"), with the following operations:

| Action | Description |
| :--- | :--- |
| **New** | Create a blank configuration profile |
| **Rename** | Customize the profile name for easy identification |
| **Delete** | Remove configuration profiles that are no longer needed |

### Process Filter Mode
Within your currently selected profile, you can switch between three modes:
* 🚫 **Off**: Global activation. No list rules are applied.
* ❌ **Blacklist Mode**: Effects will be hidden *only* in listed applications.
* ✅ **Whitelist Mode**: Effects will be shown *only* in listed applications.

### Ways to Add Processes
Supports three flexible methods to input applications for filtering:
1. Manually type `xxx.exe`
2. Select via local file browser
3. Capture from the active running process list

---

## Multi-Screen Management

BASpark features monitor recognition and memory retention to cater to multi-monitor users.

1. **Hardware ID Memory**
   The software records the resolution and unique hardware ID of each monitor, automatically restoring configurations after disconnection and reconnection.
2. **Independent Switches**
   You can manually check which screen displays the effects, preventing any unwanted interference on secondary monitors.
3. **Dynamic Refresh**
   After plugging or unplugging monitors, click **"Refresh Monitors"** to rescan the current display topology.

---

## Real-Time Notices & Auto Updates

### Cloud Real-Time Notices
The software home page integrates a **live announcement system**, which we may use to publish:
* Feature introductions and optimization details of new releases.
* Temporary workarounds for known issues.
* Community event information or important announcements.
* Holiday easter eggs and more...

### Automatic Update Checks
No need to visit the website manually. In the **"About"** page of the software, click the **"Check for Updates"** button:
* **Auto Comparison**: The system automatically detects version discrepancies between your local build and the server.
* **One-Click Upgrade**: Provides automatic pop-up notifications containing update changelogs when a new version is detected.

*Note: The automatic update feature requires an internet connection. Data fetching might fail under strict firewall environments.*