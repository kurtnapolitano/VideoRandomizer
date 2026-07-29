# VideoRandomizer — User Guide

VideoRandomizer plays a folder full of videos on a full screen display, in a random order, with a
"bumper" shown between each video. A bumper can be a still image or a short video clip. It keeps
going until you stop it.

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
- **A bumper folder** — put the bumpers you want shown between videos here. These can be still
  images, short video clips, or a mix of both.

Example:

```
C:\Signage\Videos     ad-spring.mp4, promo.mkv, tour.avi
C:\Signage\Bumpers    logo.png, hours.jpg, wifi-info.png, station-ident.mp4
```

Tips:

- Bumpers look best at the same shape as your screen (16:9, e.g. 1920×1080). Anything a different
  shape is scaled to fit and gets black bars — nothing is cropped or stretched.
- **Still images** are held for the time you set in `bumperDurationMs`. **Video clips** ignore
  that setting and simply play to the end, so make them as long as you want them to run.
- Images and clips are drawn from the same pool. If the folder holds 9 images and 1 clip, the clip
  comes up about one time in ten.
- You can add or remove videos and bumpers while the app is running. New files are picked up
  automatically the next time the playlist restarts.

---

## 3. Edit the configuration

Open `config.json` (next to `VideoRandomizer.exe`) in Notepad. It looks like this:

```json
{
  // Folder containing the videos to play.
  "videoFolder": "C:/Signage/Videos",

  // Which files in that folder count as videos.
  "videoExtensions": [ ".mp4", ".mkv", ".avi", ".mov", ".wmv" ],

  // Folder containing the bumpers shown between videos.
  "bumperFolder": "C:/Signage/Bumpers",

  // Which files in that folder are shown as still images.
  "bumperImageExtensions": [ ".jpg", ".jpeg", ".png", ".bmp" ],

  // Which files in that folder are played as video clips.
  "bumperVideoExtensions": [ ".mp4", ".mkv", ".avi", ".mov", ".wmv" ],

  // How long each bumper IMAGE stays on screen, in milliseconds. 5000 = 5 seconds.
  // Video clips ignore this and play to their end.
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
| `bumperFolder` | Where your bumpers are. Set to `""` to turn bumpers off completely. |
| `bumperImageExtensions` | The kinds of image files to show as stills. Set to `[]` for video bumpers only. |
| `bumperVideoExtensions` | The kinds of video files to play as clips. Set to `[]` for still images only. |
| `bumperDurationMs` | How long a bumper **image** stays on screen, in **milliseconds**. 3000 = 3 seconds. Video clips ignore this. Set to `0` to skip stills entirely and use only clips. |
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

1. A bumper — a still image for the configured time, or a clip played to its end.
2. A video, played to the end.
3. Another bumper.
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
Check that `bumperFolder` is not blank, that the folder has files, and that their extensions are
listed in `bumperImageExtensions` or `bumperVideoExtensions`.

**Bumper images are skipped but clips still play**
`bumperDurationMs` is 0. Set it to the number of milliseconds you want stills held for.

**A dialog says an extension is in both bumper lists**
The same extension cannot be both a still and a clip. Remove it from one of the two lists — for
example `.gif` belongs in `bumperImageExtensions`, not both.

**No sound**
Check `muteAudio` is `false`, and check the Windows volume mixer for the display PC.

**ESC does not exit**
Click the video once to make sure the app has keyboard focus, then press ESC. If the app is
genuinely stuck, press `Ctrl + Shift + Esc` to open Task Manager and end `VideoRandomizer`.
