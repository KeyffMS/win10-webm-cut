# System requirements

[Documentation](README.md) · [Polski](../REQUIREMENTS.md)

| Component | Requirement |
|---|---|
| OS | **Windows 10/11 x64** is the intended WPF target; Windows 10 Pro 22H2 x64 has been manually used |
| Building | **.NET 10 SDK** with Windows Desktop/WPF support |
| Running framework-dependent builds | **.NET 10 Desktop Runtime x64**; the base .NET runtime without Desktop components is insufficient |
| Running a self-contained win-x64 publication | The .NET runtime is included; FFmpeg and FFprobe are still required |
| Media tools | Trusted **ffmpeg.exe** and **ffprobe.exe** executables |
| Not needed | Electron, Node.js, npm, a user account or additional NuGet dependencies |

The application processes user-selected local media; it does not download missing tools. Benchmark-based minimum CPU, RAM, GPU and free disk requirements have **not** been established. You need sufficient space for an intermediate export file in the output directory. Linux, macOS and Windows ARM are not supported targets; compatibility with every Windows 11 build has not been established.

## FFmpeg capabilities

There is **no qualified minimum FFmpeg version**. Features depend on how the tools were compiled:

- Preview needs decoding, rotation/scaling, select and showinfo filters, and raw BGRA frames.
- **Copy** needs a muxer compatible with the original streams, but no video/audio encoder.
- **FFV1/MKV** needs the ffv1 encoder, matroska muxer and exact source pixel-format support; audio is stream-copied.
- **WebM VP9** needs the libvpx-vp9 encoder and webm muxer, plus libopus when input audio exists.
- **FFprobe** reads source streams, duration, metadata and image geometry.

To inspect installed tools in PowerShell:

~~~powershell
ffmpeg -version
ffprobe -version
ffmpeg -hide_banner -encoders
ffmpeg -hide_banner -muxers
~~~

Two builds with the same version number can provide different codecs and filters.

## Tool lookup

The application first searches the **tools directory next to the running EXE** for each executable, then the first matching **absolute PATH directory**. The source repository's tools folder is **not** copied automatically. A shell alias, current directory or relative PATH entry does not count. More: [Build and installation](BUILD.md).

## Input and export compatibility

Not every stream can be exported with every profile. Copy validates input streams against a conservative container compatibility table; encoding profiles support a narrower SDR and geometry contract. HDR and unsupported rotation may leave Copy as the only usable profile. See the [Export contract](EXPORT-CONTRACT.md) for precise rules.

The app needs read permissions for its input and write permissions for the output directory, including enough space for a working file. It is not a backup solution and cannot guarantee survival of power loss, hardware failures or unusual network filesystem behavior.
