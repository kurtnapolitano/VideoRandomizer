# VideoRandomizer — User Guide

VideoRandomizer plays a folder full of videos on a full screen display, in a random order, with a
still image ("bumper") shown between each video. It keeps going until you stop it.

This guide is for setting up and running a display. No programming needed.

---

## 1. Install

1. Copy the `VideoRandomizer` folder onto the display PC — anywhere is fine, e.g. `C:\VideoRandomizer`.
2. Keep the folder together. `VideoRandomizer.exe` needs the files next to it, especially the
   `libvlc` folder, which is the video engine.
3. If the app refuses to start with a message about .NET, install the **.NET 10 Desktop Runtime**
   from Microsoft. (If your copy was published as "self-contained", you can skip this.)

You do **not** need VLC or a codec pack installed. Everything needed to play video is included.

---

## 2. Set up your folders

Create two folders — they can live anywhere, including on a network share:

- **A video folder** — put the videos you want to play here.
- **A bumper folder** — put the still images you want shown between videos here.

Example:

```
C:\Signage\Videos     ad-spring.mp4, promo.mkv, tour.avi
C:\Signage\Bumpers    logo.png, hours.jpg, wifi-info.png
```

Tips:

- Bumper images look best at the same shape as your screen (16:9, e.g. 1920×1080). Images of a
  different shape are scaled to fit and get black bars — nothing is cropped or stretched.
- You can add or remove videos while the app is running. New videos are picked up automatically
  the next time the playlist restarts.

---

## 3. Edit the configuration

Open `config.json` (next to `VideoRandomizer.exe`) in Notepad. It looks like this:

```json
{
  // Folder containing the videos to play.
  "videoFolder": "C:/Signage/Videos",

  // Which files in that folder count as videos.
  "videoExtensions": [ ".mp4", ".mkv", ".avi", ".mov", ".wmv" ],

  // Folder containing the bumper images shown between videos.
  "bumperFolder": "C:/Signage/Bumpers",

  // Which files in that folder count as bumper images.
  "bumperExtensions": [ ".jpg", ".jpeg", ".png", ".bmp" ],

  // How long each bumper image stays on screen, in milliseconds. 5000 = 5 seconds.
  "bumperDurationMs": 5000,

  // Also look inside subfolders.
  "searchSubfolders": false,

  // Play videos with the sound off.
  "muteAudio": false
}
```

Change the values, save the file, and restart the app.

### What each setting does

| Setting | What it does |
|---|---|
| `videoFolder` | Where your videos are. |
| `videoExtensions` | The kinds of video files to play. Anything else in the folder is ignored. |
| `bumperFolder` | Where your bumper images are. Set to `""` to turn bumpers off completely. |
| `bumperExtensions` | The kinds of image files to show. |
| `bumperDurationMs` | Bumper time on screen, in **milliseconds**. 3000 = 3 seconds, 10000 = 10 seconds. |
| `searchSubfolders` | `true` also looks inside subfolders; `false` only the folder itself. |
| `muteAudio` | `true` plays everything silently — handy for a shop window. |

### The one rule about paths

In this file, write paths with **forward slashes**:

```
"videoFolder": "C:/Signage/Videos"        ✅ correct
"videoFolder": "C:\Signage\Videos"        ❌ will not work
"videoFolder": "C:\\Signage\\Videos"      ✅ also correct
```

A single backslash has a special meaning in this file format. Forward slashes are the easy option
and work fine on Windows.

Network shares work too: `"//server/share/Videos"`.

---

## 4. Run it

Double-click `VideoRandomizer.exe`. The screen goes full screen immediately and playback starts.

What you will see:

1. A bumper image for the configured time.
2. A video, played to the end.
3. Another bumper image.
4. Another video… and so on.

When every video has played once, the app rechecks the folder for new files, reshuffles, and
starts over. It will never play the same video twice in a row.

**To exit, press ESC.**

While running, the mouse cursor is hidden and the screen will not go to sleep.

---

## 5. Start it automatically when Windows starts

For an unattended display:

1. Press `Windows + R`, type `shell:startup`, press Enter.
2. Right-click `VideoRandomizer.exe` → **Copy**, then right-click inside the Startup folder →
   **Paste shortcut**.

The app will now launch whenever that user signs in. For a true kiosk, also set the PC to sign in
automatically and turn off the screen saver and sleep timers.

### Running several displays from different playlists

You can point the app at a different configuration file by adding the path to the shortcut target:

```
C:\VideoRandomizer\VideoRandomizer.exe  C:\Signage\lobby.json
```

---

## 6. Troubleshooting

**"No video files found in…" appears on screen**
The folder is empty, the path is wrong, or the file types are not in `videoExtensions`. The app
rechecks every 10 seconds, so fixing the folder gets it going without a restart. Check that
`videoFolder` matches the real path and that your files' extensions are listed.

**A dialog says the configuration file is not valid JSON**
Usually a single backslash in a path (see the path rule above), or a missing comma or quote.
The message includes the line number.

**A dialog says a folder does not exist**
The path is misspelled, or it is a network share this PC cannot currently reach.

**One video is skipped every time**
It is probably corrupt or partially copied. Try opening it in a media player; re-export or
re-copy it if it will not play.

**The app starts but the screen stays black**
Give it a moment — a large video on a network share can take a few seconds to start. If it stays
black, check that the `libvlc` folder is still next to `VideoRandomizer.exe`.

**Bumpers never appear**
Check that `bumperFolder` is not blank, that the folder has images, that their extensions are in
`bumperExtensions`, and that `bumperDurationMs` is greater than 0.

**No sound**
Check `muteAudio` is `false`, and check the Windows volume mixer for the display PC.

**ESC does not exit**
Click the video once to make sure the app has keyboard focus, then press ESC. If the app is
genuinely stuck, press `Ctrl + Shift + Esc` to open Task Manager and end `VideoRandomizer`.
