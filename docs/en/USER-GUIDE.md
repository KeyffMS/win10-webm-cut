# User guide

[Documentation](README.md) · [Polski](../USER-GUIDE.md)

**UI language:** the interface currently uses Polish labels; the English equivalents are listed where needed.

## Export workflow

1. Click **Otwórz wideo…** (Open video) and select a completed, local media file.
2. Use **Odtwarzaj** (Play), the slider or arrow keys to find the relevant section.
3. Set **Początek (s)** (Start) and **Koniec (s), wyłączny** (End, exclusive), or mark positions with the buttons.
4. Select **1. Przycinanie czasu** (Time trim) or **2. Crop obrazu** (Image crop).
5. Choose a profile from **Eksport** (Export format) and click **Eksportuj** (Export).
6. Review the format/stream notice, choose a new destination and explicitly confirm any overwrite.

Opening a different file resets the range, playhead and crop. Do not modify a source file in another application while exporting. Media is not sent to a cloud service.

## Preview and seeking

Preview is decoded by FFmpeg and displays **video without audio playback**. Preview bitmaps are bounded by **960 × 540 and 518,400 BGRA pixels**. Playback samples approximately up to 12 visible frames per second; this does **not** alter the output file's frame rate or image resolution.

**Obraz** (Image) displays the PTS of the frame that has actually reached the screen. **Żądano** (Requested) is the latest seek target while decoding is pending; the requested time can differ from the still-visible frame. Seeking pauses playback.

| Shortcut | Result |
|---|---|
| Left / Right | Approximately one frame based on source FPS |
| Shift + Left / Right | Approximately ten frames |
| Ctrl + Left / Right | One second even without known FPS; Ctrl takes precedence |
| Esc during crop drag | Cancel the unfinished gesture |
| Esc during slider drag | Return to the position before the drag |

Arrow keys keep their ordinary meaning inside editable text fields and combo boxes. Alt/Windows modifiers disable video frame stepping. Frame labels containing **≈** are **estimated** from FPS, not true frame indices for variable-frame-rate (VFR) media. If FPS is unknown, use the slider or Ctrl + arrows.

## Time trim

Select **Przycinanie czasu** (Time trim). Enter times in seconds using a dot or comma decimal separator. **Początek tutaj** (Start here) and **Koniec przed tą klatką** (End before this frame) take their times from an **already displayed frame**; they are disabled while the next requested frame is pending.

The range is **half-open [start, end)**: start is included, end is excluded. An end equal to the full duration means up to EOF. With **Copy**, the source streams are not re-encoded, but actual boundaries depend on keyframes and timestamps; arbitrary frame-accurate cuts are not guaranteed.

A remembered crop is **not applied when exporting in Time trim mode**.

## Rectangular crop

Select **Crop obrazu** (Image crop). Draw a rectangle over the picture or drag one of its corner handles. You may enter:

- **X/Y:** top-left position;
- **W/H:** width and height.

All values are in **image pixels after supported rotation and before SAR aspect correction** — not display-screen pixels. An actual crop requires even X, Y, W and H, at least **2 × 2 pixels**, and must be fully inside the source. Mouse-drawn coordinates snap to the even grid. Empty clicks do not replace a saved crop, and the black letterbox region is not selectable.

Typing X/Y/W/H creates an **unapplied draft**. The displayed rectangle continues to represent the last accepted crop. Use **Zastosuj** (Apply) to validate and commit or **Odrzuć zmiany** (Discard changes) to revert. An unapplied draft prevents mode changes and export.

**Cały obraz** (Full image) removes the crop and discards the draft. **Obszar środkowy** (Central area) selects a separate preset covering roughly 75% of each dimension; it is **not** the reset control. Selecting Crop mode alone does not automatically choose a central crop.

When an actual crop is selected with the Copy profile, the application switches to FFV1 because cropping requires encoding. You may switch to a VP9 profile. The selected start/end range **also applies in Crop mode**. Switching back to Time trim retains the selected crop in memory but does not use it for that export.

## Export profiles

| Polish UI label | Output |
|---|---|
| Bez rekompresji — oryginalny format | Stream copy in the original supported container; cuts can depend on keyframes |
| FFV1 / MKV — obraz bezstratny, audio copy | Losslessly encoded image in MKV; compatible audio streams copied |
| WebM VP9 — wymiary 1:1 | Lossy VP9 and Opus, original image size after crop |
| WebM VP9 — 1/2 szerokości i wysokości | Each image dimension halved, rounded down to even |
| WebM VP9 — 1/4 szerokości i wysokości | Each image dimension quartered, rounded down to even |

Smaller pixel counts **do not guarantee** a proportional decrease in file size. Encoding profiles drop some extra streams, chapters, subtitles and metadata; review the confirmation notice. The full specification is in [Export contract](EXPORT-CONTRACT.md).

## Cancellation, saving and shutdown

While exporting, editing and reopening are disabled and **Anuluj** (Cancel) is available. The application first writes a uniquely named working file into the destination directory. After FFmpeg succeeds and basic output checks pass, the file is moved into place or replaces the previous target. Replacing an existing result needs explicit approval.

An error or cancellation should not delete the previous target. Cancellation **after a completed replacement** cannot restore the old file. A forced exit, disk failure or power loss can leave a working file behind; inspect such leftovers before deletion and keep backups of important source material.

**Diagnostyka** (Diagnostics) displays environment and log information. See [Diagnostics](DIAGNOSTICS.md) and [FAQ](FAQ.md).

## Limits

The interface is Polish-language only; the standard file chooser follows Windows localization. Preview is silent and does not tone-map HDR. Available features depend on supported image geometry, media metadata and the installed FFmpeg/FFprobe build.
