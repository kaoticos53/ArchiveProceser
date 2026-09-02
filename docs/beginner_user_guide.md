# 📘 Beginner's Guide: FileFlow Studio
## *A Step-by-Step, Jargon-Free Guide to Organizing, Renaming, and Automating Your Files*

---

## 🌟 1. What is FileFlow Studio? (In Plain English)

Imagine you have thousands of photos, songs, invoices, or movies scattered across your computer—disorganized, with messy names, or taking up way too much storage space.

Doing all of this by hand (copying, pasting, renaming one by one, unzipping...) would take days of boring work.

**FileFlow Studio** is like a **smart factory conveyor belt**:
1. On one end, you place a folder full of files.
2. In the middle, you connect *"workstations"* (called **Boxes** or **Nodes**) that perform tasks: renaming, sorting by date, compressing, converting videos, etc.
3. Your files travel through connections ("wires") from one box to another.
4. At the end, everything is clean, organized, and in its exact place in just a few seconds.

> [!TIP]
> **You do not need to know any programming or advanced computer skills.** Everything is done with your mouse: drag, drop, and connect wires.

---

## 🛡️ 2. Peace of Mind First: The Maximum Safety Principle

Many people are afraid of using automation software in case it deletes or messes up their important photos or documents. With **FileFlow Studio**, you are 100% protected:

1. **Your original files are never altered by default:** All transformations create neat copies in new folders.
2. **The Magic "Virtual Simulation" Button (Dry Run):** You can test any workflow without moving a single real file. The app shows you an exact simulation of what it would do before you press the real button.
3. **The "Undo" Button (Ctrl+Z / Rollback):** If you run a flow and change your mind, click the **Undo** button and all files immediately return to their original names and locations.
4. **Windows Recycle Bin:** If you ever decide to delete files on purpose, the program never permanently destroys them; it sends them to the Windows Recycle Bin so you can recover them at any time.

---

## 🖥️ 3. Explore the Screen (The 4 Key Areas)

When you open FileFlow Studio, the screen is organized into 4 intuitive sections:

```
+-----------------------------------------------------------------------------------+
|  Top Toolbar: [ ▶ Run Workflow ]  [ 🔍 Dry Run (Simulate) ]  [ ↩ Undo ]  [ 🎨 Theme ] |
+-----------------------+-----------------------------------+-----------------------+
|  LEFT                 |  CENTER (Workspace Canvas)        |  RIGHT                |
|  Toolbox              |                                   |  Node Inspector       |
|  (The Nodes)          |  [Folder Source] -> [Rename Node] |                       |
|                       |                           \       |  Configure options    |
|  • Read Folders       |                            v      |  for the selected     |
|  • Rename Files       |                       [Move To...] |  box here.            |
|  • Compress Archives  |                                   |                       |
|  • Convert Videos     |                                   |                       |
+-----------------------+-----------------------------------+-----------------------+
|  BOTTOM: Live Output & Log Console showing real-time processing telemetry          |
+-----------------------------------------------------------------------------------+
```

1. **Top Toolbar:** Large buttons to **Run**, **Simulate (Dry Run)**, **Pause**, **Undo**, **Save**, and toggle **Languages (English/Spanish)** or **Themes**.
2. **Left Panel (Toolbox):** Your piece catalog. Search for what you want to do (e.g., *"Photos"*, *"Rename"*, *"Unpack"*) and drag it to the center.
3. **Center (Canvas / Workspace):** Where you drop boxes and connect them with wires.
4. **Right Panel (Node Inspector):** When you click any box on the canvas, its options appear here in clear language (e.g., *"Where do you want to save the photos?"* or *"What format do you prefer?"*).
5. **Bottom Panel (Console):** Displays live, millisecond-by-millisecond progress for every processed file.

---

## 🔌 4. How to Connect Boxes (Editor Basics)

* **Add a box:** Click an item in the left list, hold down your mouse button, and drag it onto the center canvas.
* **Move a box:** Click and drag the box header.
* **Connect two boxes:** Click the circle on the right side of a box (**Output**), drag the wire, and drop it onto the left circle (**Input**) of the next box.
* **Delete a wire or box:** Click it and press the `Delete` key on your keyboard.

---

## 📚 5. Four Practical Step-by-Step Recipes

---

### 📷 Recipe 1: Rename all photos with their capture date & camera model

**The Problem:** Your phone or camera names photos `IMG_0042.JPG`, `DSC_9843.JPG` and you cannot tell when they were taken.

**The Solution in 3 easy steps:**
1. Drag the **"Folder Source"** box (`FolderSourceNode`) onto the canvas.
   - In the right panel, click **Browse...** and select your photos folder.
2. Drag the **"Advanced Renamer"** box (`AdvancedRenamerNode`).
   - Connect the output of the Folder Source to the input of the Renamer box.
   - Click the Renamer box and click the **"🏷️ Method Pipeline..."** button.
   - In the Presets dropdown, choose: **"📷 Digital Photography (EXIF Date + Camera + Counter)"**.
   - You will see an instant live preview table below showing old names vs. new names!
   - Click **Save & Apply**.
3. Click the top green button **"▶ Run Workflow"**.

Done! All your photos are now named `20260815_SonyA7_001.jpg`, neatly sorted by year, month, and day.

---

### 📂 Recipe 2: Organize the Downloads Folder (Sort Videos, Photos, and Documents)

**The Problem:** You have 500 mixed files cluttering your "Downloads" folder.

**The Solution:**
1. Place a **"Folder Source"** box pointing to your `Downloads` directory.
2. Add the **"Switch / Extension Filter"** box (`SwitchCaseNode`):
   - Set up categories: `.mp4, .mkv, .avi` for Videos, `.jpg, .png, .webp` for Photos, and `.pdf, .docx, .xlsx` for Documents.
3. Connect each output port to a **"Relocate / Copy File"** box (`FileRelocatorNode`):
   - Video Port $\rightarrow$ Destination: `D:\My Videos\`
   - Photo Port $\rightarrow$ Destination: `D:\My Photos\`
   - Document Port $\rightarrow$ Destination: `D:\My Documents\`
4. Click **"🔍 Dry Run (Simulate)"** to verify the plan in the console without touching real files.
5. Click **"▶ Run Workflow"**. In 2 seconds your Downloads folder is completely organized.

---

### 🗜️ Recipe 3: Batch Extract 20 ZIP or RAR Archives at Once

**The Problem:** You downloaded multiple compressed archives (some with passwords) and opening them one by one is tedious.

**The Solution:**
1. Place a **"Folder Source"** box selecting the folder where your ZIP files are located.
2. Add the **"Smart Archive Unpacker"** box (`SmartUnpackNode`).
   - Connect both boxes.
   - If archives have passwords, click **"Manage Passwords..."** and enter candidate keys (one per line). FileFlow Studio will automatically test each password until it finds the matching one!
   - Choose the destination folder where files should be extracted.
3. Click **"▶ Run Workflow"**.

---

### 🎬 Recipe 4: Compress Large Videos for WhatsApp or Mobile Sharing

**The Problem:** You have 1 GB camera videos and want them reduced to 50 MB for easy messaging or saving onto a small USB drive.

**The Solution:**
1. Place a **"Folder Source"** box pointing to your video files.
2. Add the **"Multimedia Transcoder"** box (`MediaTranscoderNode`).
   - Connect them.
   - In the right panel, under Quick Presets, pick: **"Mobile Ultra-Compressed H.264"** or **"Fast 720p H.264 (MP4)"**.
   - Choose where to store the converted videos.
3. Click **"▶ Run Workflow"**.

---

## ❓ 6. Frequently Asked Questions (FAQ)

### 1. What happens if the power goes out while processing?
FileFlow Studio processes each file individually in an atomic, safe transaction. Nothing is corrupted. When you restart the app, you can resume exactly where you left off.

### 2. How do I know when the flow is finished?
The top progress bar fills with green and the bottom console outputs a final summary: *"Workflow completed successfully! X files processed in Y seconds"*.

### 3. Can I save my workflows for daily use?
Yes! Click **"File ❯ Save Workflow As..."** (or press `Ctrl+S`). You can save as many workflows as you want (e.g., *"CleanDownloads.flow"* or *"OrganizePhotos.flow"*). To reuse it, simply open the saved file and click Run.

### 4. What do the colored status circles on each box mean?
* ⚪ **Gray (Idle):** The box is waiting for incoming files.
* 🟡 **Yellow / Blue (Working):** The box is actively processing an item right now.
* 🟢 **Green (Completed):** Finished processing all items successfully.
* 🔴 **Red (Faulted / Alert):** An issue occurred with a file (e.g., disk was full or a document was locked by Word). You can read the exact reason in the console below.

---

## 📖 7. Jargon Buster Glossary

| Term | What it Means in Plain English |
| :--- | :--- |
| **Node / Box** | A visual block that performs a specific task (rename, copy, filter, compress, etc.). |
| **Workflow / Pipeline** | The complete chain of connected boxes from start to finish. |
| **Dry Run / Simulation** | A safe test rehearsal: performs all calculations without modifying your real files. |
| **Rollback / Undo** | Rewinds the changes: restores your files back to their original names and locations. |
| **Metadata (EXIF / ID3)** | Hidden information inside a file (photo capture date, camera model, song artist, etc.). |
| **Portable Version** | A standalone version of FileFlow Studio you can carry on a USB drive and run on any PC without installation. |

---

Enjoy fast, safe, and effortless file automation with **FileFlow Studio**! 🎉
