# Frequently asked questions

[Documentation](README.md) · [Polski](../FAQ.md)

### Does the app require an internet connection or an account?

No. It processes user-selected local files. FFmpeg and FFprobe are separate programs that you install yourself.

### Can I run it without installing the SDK?

A framework-dependent build requires the **.NET 10 Desktop Runtime**, whereas a self-contained publication includes the runtime. Both still need FFmpeg and FFprobe.

### Why can it not find ffmpeg.exe in the source repo's tools folder?

It looks for the **tools folder next to the running EXE** or absolute PATH directories. The repository folder is not copied automatically.

### Is Copy frame accurate?

No arbitrary-frame accuracy is guaranteed. Copy avoids re-encoding but still depends on source keyframes and timestamps; the resulting container is not a byte-for-byte identical source file.

### Does FFV1 preserve everything?

FFV1 is lossless for supported image samples **after crop/rotation**. Compatible audio is copied, while subtitles, extra video, chapters and some metadata are omitted.

### Does 1/2 width and height mean a file four times smaller?

No. It yields roughly one quarter of the pixels, but final size depends on scene complexity, quality settings and audio.

### Why is there a ≈ sign before frame numbers?

The numbers are estimated using source FPS, not derived from a complete frame index. In variable-frame-rate media this can differ from actual frame indices.

### Does preview play audio or tone-map HDR?

No. Preview displays silent video. HDR preview is disabled rather than converted to SDR implicitly.

### What happens if an export is cancelled?

The app attempts to stop safely and preserve an existing destination. A successfully committed replacement cannot be undone by a late cancel request. System failure can leave temporary working files.

### Is there an installer or automatic updater?

No automatic updater is built into the application. The repository provides source code and local publishing instructions; check the GitHub Releases page separately for any downloadable packages.
