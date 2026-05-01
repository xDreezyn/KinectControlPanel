# \# 🎥 Kinect Control Panel (KinectCam Custom)

# 

# A \*\*Windows Forms (C#)\*\* application for controlling the Kinect sensor with extended features, custom UI, and virtual camera support.

# 

# > ⚠️ \*\*Important:\*\* The application interface is entirely \*\*in Portuguese\*\*, even though this documentation is in English.

# 

# \---

# 

# \## 🚀 Features

# 

# \* 📷 Camera Mode (borderless window)

# \* 🎥 OBS Mode (sidebar controls for streaming)

# \* 📡 Virtual Camera (shared memory integration)

# \* 🌡️ Infrared (IR) mode with \*\*correct conversion\*\* (based on Microsoft's official sample)

# \* 🎮 Kinect angle control

# \* 🎨 Xbox 360–inspired UI (Dark/Light theme)

# \* 📐 Resolution selector (RGB + IR)

# \* 👁️ Preview toggle

# 

# \---

# 

# \## 🧠 About

# 

# This project is based on:

# 

# 👉 https://github.com/VisualError/KinectCam

# 

# But heavily modified with:

# 

# \* UI redesign (Xbox 360 style)

# \* New control modes (Camera / OBS)

# \* Virtual camera implementation

# \* Fixed Infrared rendering (matching \*Infrared Basics WPF\*)

# \* General stability improvements

# 

# \---

# 

# \## ⚠️ Disclaimer

# 

# \* 🤖 This project was developed \*\*100% with AI assistance\*\*

# \* 🌍 The UI is \*\*Portuguese only\*\* (no English translation inside the app)

# \* 🧪 Built for experimentation and personal use

# 

# \---

# 

# \## 📦 Requirements

# 

# You \*\*must install everything below\*\*, or the project will NOT work.

# 

# \---

# 

# \### 1. Kinect SDK 1.8

# 

# Download:

# https://www.microsoft.com/en-us/download/details.aspx?id=40278

# 

# ✔ Required for:

# 

# \* `Microsoft.Kinect.dll`

# \* Device drivers

# 

# \---

# 

# \### 2. Kinect Hardware

# 

# \* Kinect for Xbox 360

# \* USB adapter + external power supply

# 

# \---

# 

# \### 3. .NET Framework

# 

# \* Recommended: \*\*.NET Framework 4.7+\*\*

# 

# \---

# 

# \### 4. Visual Studio

# 

# \* Visual Studio 2019 or 2022 recommended

# \* Must support Windows Forms

# 

# \---

# 

# \## ⚠️ Common Problems (IMPORTANT)

# 

# \### ❌ "No Kinect detected"

# 

# \* Check power supply (Kinect needs external power)

# \* Try different USB ports

# \* Reinstall Kinect SDK

# \* Open \*\*Kinect Studio\*\* to confirm detection

# 

# \---

# 

# \### ❌ Build errors (`Microsoft.Kinect` not found)

# 

# \* You did NOT install Kinect SDK 1.8

# \* Or Visual Studio didn't reference it correctly

# 

# Fix:

# 

# \* Add reference manually:

# 

# &#x20; ```

# &#x20; Microsoft.Kinect

# &#x20; ```

# \* Usually located in:

# 

# &#x20; ```

# &#x20; C:\\Program Files\\Microsoft SDKs\\Kinect\\v1.8\\

# &#x20; ```

# 

# \---

# 

# \### ❌ Virtual Camera not working

# 

# This app uses \*\*shared memory\*\*, not a direct driver.

# 

# To fix:

# 

# 1\. Open OBS

# 2\. Go to:

# 

# &#x20;  ```

# &#x20;  Tools → Virtual Camera

# &#x20;  ```

# 3\. Click \*\*Start\*\*

# 4\. Close OBS

# 5\. Run this app again

# 

# \---

# 

# \### ❌ IR Mode looks wrong

# 

# If IR looks broken, it's usually because:

# 

# \* You're not using the correct resolution

# \* Kinect was not restarted

# 

# This project already applies the correct conversion:

# 

# \* Reads 16-bit IR

# \* Converts using `value >> 8`

# \* Outputs proper grayscale BGRA

# 

# \---

# 

# \## 🎮 Controls Overview

# 

# | Button | Function             |

# | ------ | -------------------- |

# | ▲ ▼ ○  | Adjust Kinect angle  |

# | IR     | Toggle Infrared mode |

# | 👁     | Toggle preview       |

# | 📐     | Open resolution menu |

# | 🌙     | Toggle theme         |

# | 📷     | Camera Mode          |

# | 🎥     | OBS Mode             |

# | 📡     | Virtual Camera       |

# 

# \---

# 

# \## 🧩 Modes

# 

# \### Normal Mode

# 

# Full UI with all controls.

# 

# \### Camera Mode

# 

# \* Borderless window

# \* Only video feed

# \* Double-click to exit

# 

# \### OBS Mode

# 

# \* Sidebar with compact controls

# \* Designed for streaming setups

# 

# \---

# 

# \## 📁 Config File

# 

# The app automatically creates:

# 

# ```

# config.txt

# ```

# 

# Stores:

# 

# \* Theme

# \* IR mode

# \* Resolution

# \* Angle

# \* Preview state

# 

# \---

# 

# \## 🛠️ Technical Notes

# 

# \* Uses `ColorImageStream` from Kinect SDK

# \* IR conversion follows Microsoft's \*\*Infrared Basics WPF\*\*

# \* Virtual camera uses:

# 

# &#x20; \* `CreateFileMapping`

# &#x20; \* Shared memory buffer (BGRA)

# 

# \---

# 

# \## 📜 Credits

# 

# \* Original base:

# &#x20; https://github.com/VisualError/KinectCam

# 

# \* Microsoft Kinect SDK samples (Infrared logic)

# 

# \---

# 

# \## ⚠️ Final Notes

# 

# This project is:

# 

# \* ❗ Not production-ready

# \* 🧪 Experimental

# \* 🎯 Focused on learning and customization

# 

# \---

# 

# If something breaks… that's normal with Kinect 😄

# The SDK is old, fragile, and VERY sensitive to setup.

# 

# \---



