# Export profiles and media compatibility

[Documentation](README.md) · [Polski](../EXPORT-CONTRACT.md)

Actual rules are implemented in ExportProfiles, Containers, ExportValidation, VideoGeometry, FfmpegService and ExportTransaction.

## Profiles

| ID | Container / video | Audio | Dimensions |
|---|---|---|---|
| Copy | Original extension and compatible muxer; stream copy | stream copy | Unchanged |
| Ffv1 | Matroska (MKV), FFV1 level 3 and source pixel format supported by encoder | stream copy | After crop/rotation; no scaling |
| WebmFull | WebM, libvpx-vp9, CRF 32, good/cpu-used 4, row-mt, yuv420p | libopus 96 kb/s per track, 48 kHz | 1:1 |
| WebmHalf | Same as WebmFull | Same as WebmFull | Both dimensions /2 |
| WebmQuarter | Same as WebmFull | Same as WebmFull | Both dimensions /4 |

The scaled WebM dimensions are computed **after** rotation and optional crop, then rounded down to even numbers. A result smaller than 2 × 2 is rejected. Profile codec settings are fixed; there is no separate CRF slider.

**Copy** avoids recompression but is not a byte-for-byte copy of the original container and does not guarantee arbitrary frame-accurate cut points. **FFV1** preserves supported image samples **after transformations**, not every original stream. **WebM** is lossy; neither encoding time nor output size is guaranteed.

## Stream mapping

**Copy** maps all input streams and metadata/chapters under a conservative container compatibility policy. An unsupported stream makes Copy fail rather than be silently discarded. Recognized extensions include MKV, WebM, MP4/M4V, MOV, AVI, TS/MTS/M2TS, WMV and MPG/MPEG. The precise stream-to-muxer allowlist is implemented in Containers.CanCopyStream.

**FFV1 / WebM encoding** includes the first non-cover video and all audio streams. Additional video streams, cover art, subtitles, data, attachments, chapters and some metadata **are not kept**. The pre-export notice warns about this. Lossless *video* encoding is not the same as preserving the entire input file.

## Supported pixel and audio formats

**WebM VP9:** accepted source video is **8-bit SDR** with one of yuv420p, yuv422p, yuv444p, nv12 or nv21. Output is yuv420p, so higher chroma detail can be lost. RGB, alpha and higher bit depths are not implicitly converted into this profile. HDR tone mapping is not performed automatically.

**FFV1:** the installed FFV1 encoder must explicitly support the source pixel format. Automatic pixel-format conversion is disabled. For a 90°/270° rotation, asymmetric chroma subsampling grids 422/440/411 are rejected rather than promising a lossless resample.

**WebM audio:** tracks are encoded using libopus at 48 kHz / **96 kb/s per track**. A known source sample rate and 1–8 channels are required. Unusual channel layouts may still be rejected by FFmpeg.

**FFV1 audio:** audio is stream-copied to Matroska only if its codec is in the application's allowed MKV audio list (including AAC, Opus, Vorbis, MP3, FLAC, AC3 and compatible PCM); there is no silent fallback to a lossy codec.

## Rotation, sample aspect ratio and crop

The app supports rotations of **0°, 90°, 180° and 270°** through explicit image transforms in preview and encoding. Unknown transform matrices, mirroring or intermediate-angle rotations are not approximated. Encoded output rotation metadata is cleared after pixels have been rotated.

Crop coordinates are in pixels **after rotation but before Sample Aspect Ratio (SAR) display correction**. SAR affects screen aspect ratio, not crop pixels. Allowed SAR values are 0.01–100; absent / N/A / 0:1 means square pixels are assumed.

Source geometry is bounded to **32,768 pixels per dimension and 128 Mi pixels overall**. Actual crop rectangles require even X/Y/W/H and dimensions at least 2 × 2 fully within the source. **Full image** uses no crop filter, including on odd-sized inputs.

Known HDR, Dolby Vision and BT.2020 wide-gamut sources are rejected for encoding profiles. Certain sources may remain usable for Copy if their stream/container combination is supported.

## Time ranges and frames

A trim range is **[start, end)**, with an exclusive end; the full duration is the EOF boundary. Copy uses stream copy so actual cutting points can depend on keyframes and input timestamps. UI frame numbers marked ≈ are FPS-based estimates, not indexed VFR frame numbers.

## Output transaction

The application records source revision and Windows file identity. A unique working file is created in the same directory as the destination. After the encoding process succeeds, it checks basic nonempty output, metadata and geometry, and verifies the source revision before using Move or Replace.

Cancellation is accepted until the final pre-commit check. Once Move/Replace has succeeded, cancellation cannot undo the replacement. The application does not delete the old target as a fallback and rejects certain unsafe aliases or filesystem conditions.

This design **does not guarantee** recovery from hardware or power failure, atomic writes on every network filesystem, a lock against external writers, exhaustive output decoding, audio synchronization or visual quality. Keep the original file and inspect critical exports independently.
