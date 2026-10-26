# Architecture

[Documentation](README.md) · [Polski](../ARCHITECTURE.md)

## Stack and flow

Win10 WebM Cut is a C# / .NET 10 / WPF application. It uses neither Electron nor an additional MVVM/DI framework. MainWindow owns view events, EditorController owns editor state, and specialized components own media, processes and exports.

~~~text
MainWindow.xaml/.cs -> EditorController -> Timeline / CropEditor
                              |               VideoGeometry
                              +-> LatestOperation -> PreviewDecoder
                              |                        |
                              |                 LatestFramePresenter -> WPF
                              +-> ExportValidation / ExportProfiles
                                         |
                                    FfmpegService
                                         |
                                    ToolProcess -> FFmpeg/FFprobe
                                         |
                                   ExportTransaction
~~~

## Responsibilities

| Domain | Owner |
|---|---|
| Editor state, opening, exporting, closing | EditorController |
| WPF events, mouse gestures, focus and dialogs | MainWindow |
| Input metadata and streams | MediaProbe / VideoInfo / StreamInfo |
| Requested and presented time, frame-step estimates | Timeline / TimePolicy |
| One active worker, newest requests, generations | LatestOperation |
| BGRA decoding and source PTS | PreviewDecoder |
| One pending frame for the UI | LatestFramePresenter |
| Preview pixel budget | PreviewSizing |
| Rotation, SAR and image coordinates | VideoGeometry |
| Draft and applied crop | CropEditor |
| Fixed profiles and container allowlists | ExportProfiles / Containers |
| Validating time, format and output geometry | ExportValidation |
| FFmpeg export arguments and execution | FfmpegService |
| Working file and final commit | ExportTransaction |
| Windows file identity and source revisions | FileRevision |
| Process lifecycle, stdout/stderr, cancellation, timeout | ToolProcess |
| Locating tools, encoders and muxers | ToolResolver / ToolCapabilities |
| Local logging, retention and diagnostic messages | AppLog |

## State model

~~~text
Empty -> Loading -> Ready <-> Playing
                   |
                   +-> Exporting -> Ready
Any state -> Closing
~~~

An opening error returns Empty, while a preview error returns Ready with a message. Selecting a newer file while Loading supersedes older results. Exporting blocks editing, playback and new file selection while allowing cancellation. Closing rejects new work. The controller enforces CanOpen, CanEdit, CanPreview, CanMark and CanExport independently of UI button state.

## Async work, preview and queues

LatestOperation has one worker, one active operation and one replaceable queued request. Every request gets a generation, so outdated callbacks cannot restore stale media or frames. Fast still-frame seeking coalesces queued targets while allowing a running short decode to finish. The minimum start interval is 80 ms; continuous input does not indefinitely postpone progress.

PreviewDecoder pairs each BGRA rawvideo frame with its actual showinfo PTS, checking ordering and completeness. PTS and image are accepted in the same WPF Dispatcher operation. LatestFramePresenter holds a single latest frame slot and pending Dispatcher callback rather than an unbounded frame queue. Preview is bounded to 960 × 540 and at most 518,400 BGRA pixels. Playback is paced by PTS, without a permanent paused-GUI refresh timer.

## Tool execution

ToolResolver looks for tools next to the EXE or in absolute PATH directories. ToolCapabilities verifies the declared muxers, encoders and pixel formats. ToolProcess launches programs by full path with ArgumentList and UseShellExecute=false, reading stdout and stderr concurrently with bounded diagnostic text.

Typical timeout policies are 20 seconds for metadata or still-frame operations, 15 seconds for capability queries, and 5 seconds for cleanup after a termination request. These are process policies, not guarantees of Windows scheduling. If cleanup cannot be confirmed, the process owner may block later tool invocations in that app instance.

## Export transaction

The immutable ExportRequest is created before the save dialog. Disk and encoding work run outside the UI thread. ExportTransaction makes a unique working output in the target directory, performs basic result and source-revision checks, then moves or replaces the destination. A rejected or cancelled export must not delete an existing target. Limitations are described in [Export contract](EXPORT-CONTRACT.md).

## Extending the code

Keep profile parameters in ExportProfiles/ExportValidation, geometry rules in VideoGeometry, tool discovery in ToolResolver and process lifetime control in ToolProcess. Update **both** language versions of relevant docs when changing behavior. Avoid duplicating business rules across MainWindow and the controller.
