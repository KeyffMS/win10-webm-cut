#!/usr/bin/env python3
"""Deterministic audit models, NOT application tests or benchmarks.

Standard-library arithmetic and illustrative event ordering only.
Does not import/execute the application, WPF, .NET or FFmpeg, start processes,
use a network, or modify media. Output is JSON on stdout.
Python round uses ties-to-even, as does the default Math.Round used here.
Event schedules are assumptions, not measurements or a .NET runtime emulator.
"""
from __future__ import annotations

import json
import math

SOURCE_COMMIT = "a124e98ea9171b2f24e2ea1555801fffd5634580"
MIB = 1024 * 1024


def preview_size(width: int, height: int) -> dict:
    """Transcription of PreviewDecoder.DecodeAsync size arithmetic."""
    if width <= 0 or height <= 0:
        raise ValueError("Dimensions must be positive")
    w = max(2, round(min(width, 960) / 2.0) * 2)
    h = max(2, round(w * height / width / 2.0) * 2)
    size = w * h * 4
    return {
        "input": [width, height], "preview": [w, h],
        "bgra_frame_bytes": size,
        "bgra_frame_mib": round(size / MIB, 6),
        "raw_mib_per_second_at_12fps": round(size * 12 / MIB, 6),
        "queued_payload_mib_if_ui_blocked_5s": round(size * 60 / MIB, 6),
    }


def frame_label(seconds: float, fps: float, duration: float) -> dict:
    total = max(1, math.ceil(duration * fps))
    index = min(max(0, round(seconds * fps)), total)
    return {"time": seconds, "fps": fps, "duration": duration,
            "display_index": index, "estimated_total": total,
            "inside_zero_based_range": 0 <= index < total}


def initial_crop(width: int, height: int) -> list[int]:
    x, y = (width // 8) & ~1, (height // 8) & ~1
    return [x, y, (width - 2 * x) & ~1, (height - 2 * y) & ~1]


def scaled(width: int, height: int, divisor: int) -> dict:
    w = math.trunc(width / divisor / 2) * 2
    h = math.trunc(height / divisor / 2) * 2
    return {"input_crop": [width, height], "divisor": divisor,
            "filter_expression_result": [w, h],
            "strictly_positive": w > 0 and h > 0}


def open_race() -> dict:
    # The ordering is hypothetical. It is possible because Open_Click has no
    # in-flight-open guard or request-generation check at its await boundary.
    current = None
    events = []
    for t, event in [(0, "start_A"), (10, "start_B"),
                     (20, "complete_B"), (100, "complete_A")]:
        if event.startswith("complete_"):
            current = event[-1]
        events.append({"time_units": t, "event": event, "active_file": current})
    return {"assumption": "B selected after A; A probe completes last",
            "events": events, "last_selected": "B", "final_active": current}


def export_overwrite() -> dict:
    # Pure in-memory state changes; no media file is created, removed or opened.
    destination = "previous_valid_output"
    states = [{"event": "before_export", "destination": destination}]
    destination = "new_partial_output"
    states.append({"event": "ffmpeg_y_opens_destination", "destination": destination})
    destination = None
    states.append({"event": "ui_cancellation_handler_deletes_destination",
                   "destination": destination})
    return {"assumption": "Overwrite confirmed; FFmpeg already replaced the old output",
            "states": states, "old_output_preserved": False}


def lifecycle_schedule() -> dict:
    # Does not claim which concrete exception .NET will throw.
    return {"assumption": "Worker has not completed when UI cancels",
            "events": ["worker_running", "UI_clears_current_request",
                       "UI_Cancel_returns", "UI_Dispose", "worker_cleanup_finishes"],
            "dispose_before_worker_completion": True,
            "runtime_exception_reproduced": False}


def vfr_step() -> dict:
    # Illustrative frame timestamps, not read from a user video.
    pts = [0.0, 0.01, 0.02, 0.20, 0.21]
    avg_fps = len(pts) / 0.25
    target = 1 / avg_fps
    index = next(i for i, t in enumerate(pts) if t >= target)
    return {"assumption": "Illustrative VFR; decoder chooses first PTS >= seek target",
            "pts_seconds": pts, "avg_fps": avg_fps,
            "nominal_one_frame_target": target, "actual_next_pts": pts[1],
            "model_first_frame_at_or_after_target": pts[index],
            "model_selected_index": index,
            "application_display_index": round(target * avg_fps),
            "ffmpeg_executed": False}


def results() -> dict:
    return {
        "source_commit": SOURCE_COMMIT,
        "method": "arithmetic and assumed event sequences; NOT tests or benchmarks",
        "application_executed": False, "ffmpeg_executed": False,
        "dotnet_executed": False, "github_actions_used": False,
        "S01_preview_payload": {
            "assumptions": "BGRA=4 bytes/pixel; 12 frames/s; 5s blocked UI; one payload per queued frame; excludes native/managed overhead, GC, copies and process memory",
            "cases": [preview_size(1920, 948), preview_size(3840, 2160),
                      preview_size(1080, 1920), preview_size(960, 10000)]},
        "S02_idle_timer": {"interval_ms": 180,
            "nominal_ticks_per_hour": 3600 / 0.18,
            "conditions": "Dispatcher delivers ticks; loaded file; no slider drag",
            "cpu_usage_measured": False},
        "S03_key_repeat": {"assumed_key_events_per_second": 30, "duration_seconds": 2,
            "StartPreview_requests": 60, "coalescing_in_source": False,
            "actual_process_count_measured": False},
        "S04_frame_labels": [frame_label(s, 30, 1) for s in (0, 0.5/30, 1.5/30, 0.99, 1)],
        "S05_vfr_step": vfr_step(),
        "S06_crop_and_scale": {
            "initial_and_reset_crop_1920x948": initial_crop(1920, 948),
            "cases": [scaled(2, 2, 2), scaled(6, 6, 4),
                      scaled(1440, 712, 2), scaled(1440, 712, 4)]},
        "S07_open_race": open_race(),
        "S08_overwrite_cancel": export_overwrite(),
        "S09_CTS_lifetime": lifecycle_schedule(),
        "S10_unapplied_crop": {"model_crop": [240, 118, 1440, 712],
            "edited_text_fields": [0, 0, 1000, 500],
            "apply_button_pressed": False, "exported_crop": [240, 118, 1440, 712]},
        "S11_ctrl_without_fps": {"fps": 0, "key": "Ctrl+Right",
            "requested_step_seconds": 1, "source_handler_returns_before_step": True},
        "S12_clock_latency": {"assumed_startup_latency_seconds": 0.5,
            "UI_clock_seconds_at_first_frame": 0.5, "first_frame_relative_pts": 0,
            "latency_measured": False},
    }


if __name__ == "__main__":
    print(json.dumps(results(), ensure_ascii=False, indent=2, allow_nan=False))
