# VideoRandomizer

A Windows kiosk player for ads and other videos. It launches straight into full screen, shuffles
every video in a folder, and shows a randomly chosen bumper image between each one. It runs
forever until someone presses **ESC**.

Playback is handled by the VLC engine, which ships inside the app — the machine does **not** need
VLC installed or any codec pack, and it plays `.avi`, `.mkv`, `.mp4`, `.mov`, `.wmv` and friends
out of the box.

- **Using the app?** See the [User Guide](docs/USER-GUIDE.md).
- **Building it?** Read on.

## How it behaves

1. On start it scans the video folder and shuffles the whole list.
2. It shows a random bumper image, then plays a video, then another random bumper, then the next
   video — a bumper before every video, including the first.
3. When the last video finishes, it rescans the folder from disk, so videos added while it was
   running get picked up, and shuffles again.
4. If the reshuffle would start with the video that just played, it reshuffles until it does not.
   The same video never plays twice in a row.
5. **ESC** exits.

## Requirements

| | |
|---|---|
| Build | .NET 10 SDK |
| Run | .NET 10 Desktop Runtime (or publish self-contained — see below) |
| Platform | Windows, x64 |

## Build

```powershell
dotnet build VideoRandomizer.slnx -c Release
```

Or just the app, without the tests:

```powershell
dotnet build src\VideoRandomizer\VideoRandomizer.csproj -c Release
```

The executable lands in:

```
src\VideoRandomizer\bin\Release\net10.0-windows\win-x64\VideoRandomizer.exe
```

`config.json` is copied next to the exe on every build. Note that the output folder also contains
a `libvlc\` directory with the bundled VLC engine — it must travel with the exe.

## Test

```powershell
dotnet test tests\VideoRandomizer.Tests\VideoRandomizer.Tests.csproj
```

Covers folder scanning, extension normalization, config validation, and the no-repeat shuffle
guarantee.

## Run

```powershell
.\VideoRandomizer.exe
```

It reads `config.json` from beside the exe. To use a different file, pass the path:

```powershell
.\VideoRandomizer.exe D:\kiosk\lobby-display.json
```

That makes it easy to drive several displays from one install with different playlists.

## Publish for a display machine

To produce a folder that runs on a PC with no .NET installed:

```powershell
dotnet publish src\VideoRandomizer\VideoRandomizer.csproj -c Release -r win-x64 --self-contained true -o dist
```

Copy the whole `dist` folder to the target machine and run `VideoRandomizer.exe` from it.

## Configuration

`config.json` accepts `//` comments and trailing commas, so it can be annotated freely.

| Setting | Meaning |
|---|---|
| `videoFolder` | Folder holding the videos to play. |
| `videoExtensions` | Which files count as videos. `"mp4"`, `".MP4"` and `"*.mp4"` all work. |
| `bumperFolder` | Folder holding the bumper images. Set to `""` to turn bumpers off. |
| `bumperExtensions` | Which files count as bumper images. |
| `bumperDurationMs` | How long each bumper stays on screen, in milliseconds. |
| `searchSubfolders` | Also scan subfolders of the two folders above. |
| `muteAudio` | Play videos with the sound off. |

Paths need forward slashes (`C:/Videos`) or doubled backslashes (`C:\\Videos`), because a single
backslash is an escape character in JSON. UNC paths like `//server/share/Videos` work.

If anything in the file is wrong — bad JSON, a folder that does not exist, an empty extension
list — the app explains the problem in a dialog and exits instead of starting up blank.

## Project layout

```
VideoRandomizer.slnx
src/VideoRandomizer/
  Program.cs        entry point: load config, start VLC, run the form
  PlayerForm.cs     full screen window, playback loop, ESC handling
  MediaLibrary.cs   folder scanning and the no-repeat shuffle
  AppConfig.cs      config model, loading, normalization, validation
  config.json       the configuration file, copied next to the exe
tests/VideoRandomizer.Tests/
docs/USER-GUIDE.md
```

## Notes on behaviour

- The app runs on the **primary monitor**.
- The mouse cursor is hidden while it runs.
- It keeps the display awake, so Windows power settings will not blank the screen mid-playlist.
- If a video is corrupt or unreadable, it is skipped and the loop continues to the next one.
- If the video folder is empty or unreachable, the app shows a diagnostic message on screen and
  rechecks every 10 seconds rather than exiting — useful when videos live on a network share that
  is briefly unavailable.
