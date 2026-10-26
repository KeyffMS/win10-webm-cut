#!/usr/bin/env python3
"""Deterministic arithmetic/state models. No application, .NET, FFmpeg or OS emulation.
No network, subprocess, filesystem mutation (except the caller redirecting stdout).
Inputs are synthetic. No assertions and no test/benchmark pass/fail verdicts.
"""
from __future__ import annotations
import json
import math
import ntpath
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]


def transaction(old: str | None, event: str) -> dict:
    target, temporary, outcome = old, 'new-partial', 'not-committed'
    if event == 'overwrite-declined':
        temporary = None
        outcome = 'not-started'
    elif event in ('cancel', 'encoder-error', 'verification-error', 'replace-error'):
        temporary = None
        outcome = event
    elif event == 'target-changed':
        target, temporary, outcome = 'external-change', None, 'refused'
    else:
        target, temporary = 'new-complete', None
        outcome = 'committed'  # including cancellation AFTER the commit boundary
    return {'old': old, 'event': event, 'target': target, 'temporary': temporary, 'outcome': outcome}


def sizing(w: int, h: int, sar: float, vw: float = 960, vh: float = 540,
           dpi: float = 1) -> dict:
    aw = vw * dpi if math.isfinite(vw * dpi) and vw * dpi > 0 else 960
    ah = vh * dpi if math.isfinite(vh * dpi) and vh * dpi > 0 else 540
    dw = w * sar
    factor = min(1, max(1, min(960, aw)) / dw, max(1, min(540, ah)) / h,
                 math.sqrt(518400 / (dw * h)))
    width, height = max(1, math.floor(dw * factor)), max(1, math.floor(h * factor))
    return {'source': [w, h], 'sar': sar, 'viewportDip': [vw, vh], 'dpi': dpi,
            'bitmap': [width, height], 'bgraBytes': width * height * 4,
            'aspectRelativeError': abs((width / height) / (dw / h) - 1)}


def frame_label(t: float, duration: float, fps: float) -> dict:
    if fps <= 0:
        return {'fps': fps, 'index': None, 'controlStepSeconds': 1}
    total = max(1, math.ceil(duration * fps))
    return {'time': t, 'fps': fps, 'approximate': True,
            'index0': min(total - 1, max(0, math.floor(t * fps + 1e-7))),
            'countEstimate': total,
            'exclusiveBoundary': min(total, max(0, math.ceil(t * fps - 1e-7))),
            'controlStepSeconds': 1, 'frameStepSeconds': 1 / fps}


def coalesced_seek() -> dict:
    # A still-frame worker finishes in 130 model ms, starts at most every 80 ms.
    # A waiting request is replaceable. Input changes/cancel use another path.
    arrivals = {time: i for i, time in enumerate(range(0, 1201, 30))}
    pending = active = None
    finish = 0
    last_start = -80
    starts, shown = [], []
    max_waiting = max_active = 0
    for now in range(0, 1601):
        if active is not None and now == finish:
            shown.append({'atMs': now, 'request': active,
                          'keepsNewerTarget': pending is not None})
            active = None
        if now in arrivals:
            pending = arrivals[now]
        if active is None and pending is not None and now - last_start >= 80:
            active, pending = pending, None
            finish, last_start = now + 130, now
            starts.append({'atMs': now, 'request': active})
        max_waiting = max(max_waiting, int(pending is not None))
        max_active = max(max_active, int(active is not None))
    return {'assumedDecodeMs': 130, 'arrivals': len(arrivals), 'starts': starts,
            'presentations': shown, 'maxActive': max_active, 'maxWaiting': max_waiting,
            'lastRequested': max(arrivals.values()), 'lastPresented': shown[-1]['request']}


def geometry_case(rotation: int, sar: float, dpi: float) -> dict:
    w, h = (948, 1920) if rotation in (90, 270) else (1920, 948)
    normalized_sar = 1 / sar if rotation in (90, 270) else sar
    size = sizing(w, h, normalized_sar, 900, 500, dpi)['bitmap']
    scale = min(900 / size[0], 500 / size[1])
    bw, bh = size[0] * scale, size[1] * scale
    left, top = (900 - bw) / 2, (500 - bh) / 2
    crop = [100, 100, (w - 200) & ~1, (h - 200) & ~1]
    screen = [left + crop[0] * bw / w, top + crop[1] * bh / h,
              crop[2] * bw / w, crop[3] * bh / h]
    # Inverse float values, before the explicitly documented floor/even rounding.
    inverse = [(screen[0] - left) * w / bw, (screen[1] - top) * h / bh,
               screen[2] * w / bw, screen[3] * h / bh]
    return {'rotation': rotation, 'sarInput': sar, 'dpi': dpi,
            'normalized': [w, h, normalized_sar], 'bitmap': size,
            'letterboxDip': [left, top, bw, bh], 'cropPixels': crop,
            'screenDip': screen, 'inverseBeforeEvenFloor': inverse}


def crop_validation(x: int, y: int, w: int, h: int, source=(1920, 948)) -> str:
    if (x, y, w, h) == (0, 0, *source):
        return 'full-image-no-filter'
    if min(x, y) < 0 or min(w, h) < 2 or x + w > source[0] or y + h > source[1] or (x | y | w | h) & 1:
        return 'rejected'
    return 'crop'


def model_report() -> dict:
    result = {'kind': 'models-not-application-tests', 'sourceBase': '51b014ed9810e7d9e44b39322377bdc581be7be5',
              'assumptions': ['No actual disk transaction, scheduler, PTS decoder or GUI is executed.',
                              'Arithmetic mirrors named source policies; it is not a C# interpreter.',
                              'Counts and byte volumes are not CPU/RAM benchmarks.'], 'models': {}}
    m = result['models']
    m['M01_transaction'] = [transaction(old, event) for old in (None, 'previous-complete')
        for event in ('overwrite-declined', 'cancel', 'encoder-error', 'verification-error',
                      'replace-error', 'target-changed', 'success', 'cancel-after-commit')]
    m['M02_lifecycle'] = {
        'cancelOrder': ['invalidate-generation', 'signal-cancel', 'await-worker-and-cleanup', 'dispose-source'],
        'cancelBeforeStart': {'processStarts': 0, 'sourceDisposedAfterOwnerEnd': True},
        'normalEnd': ['await-result', 'observe-result', 'dispose-source'],
        'cleanupTimeout': ['request-kill', 'cancel-reader-token', 'bounded-wait',
                           'retain-reader-and-process-owner', 'block-new-starts', 'log-warning'],
        'shutdown': ['closing', 'invalidate', 'stop-loader-preview-export', 'await-owned-operations', 'no-ui-callbacks'],
        'notProven': 'Actual .NET cancellation races and Windows kill semantics.'}
    latest = 3
    arrivals = [('B', 2, 'success'), ('A', 1, 'failure'), ('C', 3, 'success')]
    m['M03_latest_open'] = [{'file': name, 'generation': generation, 'outcome': status,
        'commitUi': generation == latest and status == 'success', 'errorObserved': status == 'failure'}
        for name, generation, status in arrivals]
    m['M04_time_protocol'] = [
        {'event': 'seek A', 'requested': 1.0, 'imagePts': None, 'pending': True},
        {'event': 'seek B queued', 'requested': 2.0, 'imagePts': None, 'pending': True},
        {'event': 'in-flight A accepted', 'requested': 2.0, 'imagePts': 1.033, 'pending': True, 'canMark': False},
        {'event': 'B accepted', 'requested': 2.017, 'imagePts': 2.017, 'pending': False, 'canMark': True},
        {'event': 'mark end', 'exclusiveEnd': 2.017, 'notWallClock': True}]
    m['M05_frame_semantics'] = [frame_label(t, 10, fps) for fps in (30, 30000 / 1001, 0) for t in (0, 1.001, 10)]
    slot, callbacks, peak = None, 0, 0
    for i in range(36):
        slot, callbacks = i, 1
        peak = max(peak, callbacks)
    m['M06_frame_slot'] = {'producerFramesWhileUiBlocked': 36, 'queuedCallbacks': peak,
                           'pendingFrame': slot, 'pendingBgraBytesAtCap': 960 * 540 * 4,
                           'notIncluded': 'active decoding buffer, WPF copies, FFmpeg and OS memory'}
    # Symbolic timestamp queue: producer cannot outrun a bounded PTS buffer.
    queued, produced, consumed, peak_pts, blocked = 0, 0, 0, 0, 0
    for tick in range(500):
        if produced < 100:
            if queued < 64:
                queued += 1; produced += 1
            else: blocked += 1
        if tick % 5 == 0 and queued:
            queued -= 1; consumed += 1
        peak_pts = max(peak_pts, queued)
    m['M06_frame_slot']['timestampBackPressure'] = dict(capacity=64, produced=produced,
        consumed=consumed, remaining=queued, peak=peak_pts, blockedProducerTicks=blocked,
        assumption='one attempt per model tick, one consumption per five ticks; no real pipes')
    m['M07_seek_coalescing'] = coalesced_seek()
    m['M08_size_budget'] = [sizing(w, h, sar, vw, vh, dpi) for w, h, sar, vw, vh, dpi in [
        (1920, 948, 1, 960, 540, 1), (3840, 2160, 1, 900, 500, 2),
        (1080, 1920, 1, 900, 500, 1), (960, 10000, 1, 900, 500, 1),
        (1, 1, 1, 0, 0, 1), (10, 20, 2, 200, 200, 1), (640, 480, .5, 900, 500, 1)]]
    m['M09_geometry'] = [geometry_case(rotation, sar, dpi) for rotation in (0, 90, 180, 270)
                        for sar in (1, 4 / 3) for dpi in (1, 2)]
    m['M10_crop_state'] = [
        {'event': 'full', 'applied': None, 'draftWidth': '1920', 'dirty': False, 'exportAllowed': True},
        {'event': 'edit width', 'applied': None, 'draftWidth': '800', 'dirty': True, 'exportAllowed': False},
        {'event': 'apply', 'applied': [0, 0, 800, 948], 'dirty': False, 'exportAllowed': True},
        {'event': 'invalid edit/apply', 'applied': [0, 0, 800, 948], 'draftWidth': '801', 'dirty': True, 'exportAllowed': False},
        {'event': 'discard', 'applied': [0, 0, 800, 948], 'dirty': False},
        {'event': 'full/reset', 'applied': None, 'dirty': False},
        {'event': 'central preset', 'applied': [240, 118, 1440, 712], 'dirty': False}]
    profiles = {'Copy': [None, 1], 'Ffv1': ['matroska', 1], 'WebmFull': ['webm', 1],
                'WebmHalf': ['webm', 2], 'WebmQuarter': ['webm', 4]}
    m['M11_profile_identity'] = {'reorderedUi': [dict(id=i, muxer=profiles[i][0], scale=profiles[i][1])
        for i in reversed(profiles)], 'unknownId': 'rejected', 'ffv1Audio': 'copy',
        'codecNegotiation': 'not-executed; capability and muxer checks remain runtime'}
    m['M12_validation'] = {
        'crops': [dict(values=v, result=crop_validation(*v)) for v in [(0,0,1920,948),(0,0,6,6),
                   (1,0,20,20),(-2,0,20,20),(1918,946,2,2),(0,0,2147483647,2)]],
        'reductions': [dict(input=[w,h], divisor=d, output=[w//d//2*2,h//d//2*2],
                           decision='rejected' if min(w//d//2*2,h//d//2*2)<2 else 'accepted')
                       for w,h in [(2,2),(6,6),(1920,948)] for d in (2,4)],
        'time': {'NaN': 'rejected', 'Infinity': 'rejected', 'endBeforeStart': 'rejected'},
        'onePixelImage': 'full image allowed as no-crop; transformed WebM minimum still applies'}
    tools = {r'C:\App\tools\ffmpeg.exe', r'D:\Tools\ffmpeg.exe'}
    def resolve(base, path):
        candidates = [ntpath.join(base, 'tools', 'ffmpeg.exe')]
        candidates += [ntpath.join(part, 'ffmpeg.exe') for part in path if ntpath.isabs(part) and ntpath.splitdrive(part)[0]]
        return next((v for v in candidates if v in tools), None)
    m['M13_resolver'] = [{'base': base, 'cwdIgnored': cwd, 'selected': resolve(base, [r'.\tools',r'D:\Tools'])}
                        for base in (r'C:\App',r'C:\Elsewhere') for cwd in (r'C:\Repo',r'Z:\Other')]
    m['M14_state'] = [dict(phase=s, canOpen=s not in ('Exporting','Closing'),
        canEdit=s in ('Ready','Playing'), cancelExport=s=='Exporting', idleTimer=False)
        for s in ('Empty','Loading','Ready','Playing','Exporting','Closing')]
    lengths = [0]
    rotations = 0
    for _ in range(100):
        incoming = 250000  # synthetic bounded UTF-8 entry, not an actual diagnostic file
        if lengths[0] + incoming > 1048576:
            lengths = [0] + lengths[:3]
            rotations += 1
        lengths[0] += incoming
    m['M15_log_policy'] = {'syntheticEntryBytes': 250000, 'rotations': rotations,
        'retainedFileBytes': lengths, 'writeFailure': 'report-unavailable-no-recursive-log',
        'redactionScope': 'drive/UNC paths only; manual review still required'}
    policy = json.loads((ROOT / 'docs/package-manifest.json').read_text(encoding='utf-8'))
    m['M16_package_policy'] = {'expectedFiles': policy['requiredPublishedFiles'],
        'runtimeDocumentsRequired': policy['runtimeDocuments'],
        'variants': ['external-tools', 'tools-plus-required-notices-and-provenance'],
        'unknown.dll': 'reject', 'private.log': 'reject', 'symbols.pdb': 'omit',
        'missingProvenance': 'reject', 'existingDestination': 'reject',
        'actualBuildOrArchive': 'not-created'}
    m['M17_layout_budget'] = [dict(screenPixels=[w,h], dpiScale=dpi, logical=[w/dpi,h/dpi],
        minimumWindowDip=[640,480], minimumFits=(w/dpi>=640 and h/dpi>=480),
        fixedWidestControlDip=365, wrapping='WrapPanel; vertically scrolling controls; no rendering simulated')
        for w,h,dpi in [(1920,1080,1),(1920,1080,2),(1280,1024,2),(1920,1080,3)]]
    return result

if __name__ == '__main__':
    print(json.dumps(model_report(), ensure_ascii=False, indent=2, allow_nan=False))
