# AutoPause Monitor Guard 🛡️

A lightweight C# system utility that automatically pauses active applications (via `ESC` key injection) when a monitor disconnection is detected.

## 🚀 The Problem
Gamers and power users with multi-monitor setups often face issues where a faulty cable or accidental disconnection causes the primary screen to black out. 
In gaming scenarios, this often leads to "death by AFK" because the game continues running while the user struggles to restore the display.

## 💡 The Solution
This tool runs in the background and hooks into Windows Management Instrumentation (WMI). 
It listens for low-level **PnP (Plug and Play)** device removal events targeting the Monitor device class. 
When a disconnection occurs, it instantly injects an `ESC` key press directly into the Windows input stream, pausing most games and applications.

## 🛠️ Tech Stack
* **Language:** C# (.NET)
* **Core API:** Windows Management Instrumentation (WMI)
* **Input Simulation:** `user32.dll` (P/Invoke `SendInput`)
* **Privilege Management:** Application Manifest (Admin requirement)

## 🧩 How it works
1.  **Event Listener:** The app creates a `ManagementEventWatcher` subscribed to `__InstanceDeletionEvent` for the `Win32_PnPEntity` class, specifically filtering for Display devices (GUID `{4d36e96e-e325-11ce-bfc1-08002be10318}`).
2.  **Debouncing:** A built-in debounce mechanism prevents multiple triggers during the microseconds of a disconnection event.
3.  **Action:** Upon detection, the `SendInput` WinAPI function constructs a hardware-level keyboard event (ESC down + ESC up).

## ⚠️ Known Limitations (The "Witcher 3" Case)
Some older game engines running in **Exclusive Fullscreen Mode** (e.g., The Witcher 3) handle display loss by initiating an immediate graphical pipeline reload (stutter). 
Due to race conditions between the engine's internal recovery and the OS input queue, the `SendInput` command may occasionally be ignored by the engine during this specific "reload" frame.
* **Workaround:** Running games in *Borderless Windowed* mode resolves this completely.

## 📦 How to Build
```bash
csc Program.cs /r:System.Management.dll /win32manifest:app.manifest
