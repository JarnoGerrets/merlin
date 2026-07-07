---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# vision_worker.py

## File

`Merlin.Backend/VisionScripts/vision_worker.py`

Verified present in current repo.

## Purpose

Python sidecar worker for camera-based motion control. It reads JSON commands from stdin, opens/configures OpenCV capture profiles, runs MediaPipe hand landmarker detection, maps hand landmarks to normalized pointer coordinates, detects pinch gestures, performs pinch/motion-region calibration, and writes JSON events/logs/errors to stdout.

## Related Features

- [[Vision Sidecar]]
- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Dashboard UI Control]]
- [[Browser Control]]

## Main Types / Classes

- `VisionWorker`
- module helper functions for math, normalization, camera device discovery, fourcc conversion, black-frame detection, and percentiles.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `run` | public method | Reads stdin JSON lines, parses commands, delegates to `handle`, exits on shutdown. | `json.loads`; `handle`; `write_error` | Python process entry | Backend writes one command per line. |
| `handle` | public method | Dispatches `vision.start_tracking`, `vision.stop_tracking`, `vision.calibrate_pinch`, `vision.calibrate_motion_region`, and `vision.shutdown`. | start/stop/calibration methods | `run` | Command protocol switch. |
| `start_tracking` | public method | Stores config, clears gesture/calibration state, starts capture loop thread if needed. | `capture_loop` | backend `VisionSidecarHost.StartTrackingAsync` | Starts camera processing. |
| `stop_tracking` | public method | Stops tracking, releases camera, closes landmarker, releases active pinch states. | `release_camera`; `close_landmarker`; `release_all_pinches` | backend stop/shutdown | Emits pinch end for active pinches. |
| `capture_loop` | public method | Loads calibration overrides, selects/open camera, creates landmarker, reads frames, calls MediaPipe, and handles failures. | `open_configured_camera`; `create_landmarker`; `process_hands`; `write_error` | tracking thread | Main frame loop. |
| `open_configured_camera` | public method | Resolves preferred camera name/index and falls back to camera 0 if configured camera fails. | `find_directshow_camera_index`; `select_camera_backend` | capture loop | Preserves fallback semantics. |
| `select_camera_backend` | public method | Benchmarks capture profiles, chooses best sustained FPS/profile, logs rejected candidates, reopens selected profile if needed. | `capture_profile_candidates`; `benchmark_capture_profile`; `choose_benchmark_result`; `open_selected_capture_profile` | camera open path | Auto profile selection prefers valid throughput. |
| `capture_profile_candidates` | public method | Builds candidate list for Auto/manual backends and profiles: `DSHOW_MJPG_CONSTRUCTOR`, `DSHOW_MJPG_SET_BEFORE_AFTER`, `DSHOW_DEFAULT`, `MSMF_DEFAULT`, `DEFAULT`. | OpenCV capability checks | backend selection | Manual override uses requested profile/backend. |
| `benchmark_capture_profile` | public method | Opens candidate, configures capture, reads first valid frame, measures startup, actual resolution/fps/fourcc, read fps, avg read ms, failed reads, black frames. | `open_capture_for_profile`; `configure_capture_for_profile`; `read_timed`; `is_black_frame` | `select_camera_backend` | Rejects slow/black/bad profiles. |
| `choose_benchmark_result` | public method | Scores valid candidates by measured FPS, avg read time, startup, and preferred profile order. | `capture_profile_score`; `capture_profile_is_close_enough` | `select_camera_backend` | Keeps DSHOW+MJPG fast path preferred when valid. |
| `process_hands` | public method | Assigns logical hands, emits pointer events, updates calibration, updates pinch state, releases missing pinches. | `assign_logical_hands`; `emit_pointer`; `update_pinch`; calibration methods | capture loop callback | Handles primary/secondary pointers. |
| `assign_logical_hands` and candidate helpers | public methods | Select primary and secondary hand candidates using confidence/continuity/handedness heuristics. | candidate helpers | `process_hands` | Stabilizes pointer id assignment. |
| `pointer_position` / `map_pointer_position` / `smooth_position` | public methods | Convert landmarks to normalized pointer, apply calibrated control region/gain, and smooth per pointer. | math helpers | `process_hands` | Mapping happens before backend receives gestures. |
| `emit_pointer` | public method | Rate-limits and writes `gesture.pointer.move` JSON. | `write` | `process_hands` | Uses `emitRateHz`. |
| `update_pinch` | public method | Computes thumb/index ratio, applies start/hold/release thresholds and debounce, emits `gesture.pinch.start/move/end`. | `pinch_ratio`; `write` | `process_hands` | Uses loaded/calibrated thresholds. |
| pinch calibration methods | public methods | Play phase cues, collect open/pinched/release samples, compute thresholds, save JSON, emit completion/failure. | `play_pinch_calibration_cue`; `complete_pinch_calibration`; `save_pinch_calibration` | calibration command / process_hands | Beep-guided calibration. |
| motion-region calibration methods | public methods | Collect corner samples, compute control region, save JSON, apply mapping config, emit completion/failure. | cue/sample/save helpers | calibration command / process_hands | Improves reach/corner mapping. |
| `write`, `write_log`, `write_error` | public methods | Serialize JSON payloads/logs/errors to stdout and flush. | `json.dumps`; `print` | all worker paths | Backend parses these lines. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `tracking` | bool | Whether capture loop should keep reading frames. | capture loop | start/stop/shutdown | false after stop |
| `config` | dict | Current camera/profile/pointer/pinch/calibration options from backend. | most methods | `start_tracking`, calibration loading | per tracking session |
| `capture` | OpenCV VideoCapture | Active camera device. | capture loop | camera open/release | released on stop/failure |
| `landmarker` | MediaPipe hand landmarker | Hand detector instance. | capture loop | create/close | closed on stop |
| `pinched_by_pointer` / `pinch_candidate_since_by_pointer` | dicts | Current pinch state and debounce timers by pointer id. | pinch methods | update/release | cleared on start/stop |
| `smoothed_positions` / `last_emit_by_pointer` | dicts | Pointer smoothing and rate limiting state. | mapping/emit | start/process/emit | cleared on start |
| `pinch_calibration` / motion calibration state | dict or None | Active calibration phase/sample collection. | calibration update/complete | calibration command/complete/fail | None when idle |
| camera benchmark metrics | local dicts | Candidate open/read/fourcc/fps/black-frame metrics. | selection helpers | benchmark helpers | per camera open |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `cv2` / OpenCV | Camera capture, backend constants, frame reads, MJPG/DSHOW/MSMF profile selection. |
| MediaPipe tasks vision | Hand landmark detection. |
| Python stdlib JSON/threading/time/pathlib/subprocess | Protocol I/O, loops, timing, file saves, DirectShow device listing. |
| Windows DirectShow device query helpers | Preferred camera name resolution. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `vision.started` / `vision.stopped` style status/logs | backend stdout parser | Health and lifecycle diagnostics. |
| `gesture.pointer.move` | backend `VisionSidecarHost` | pointerId, x, y, confidence, source. |
| `gesture.pinch.start` / `gesture.pinch.move` / `gesture.pinch.end` | backend gesture router | pointer id, coordinates/confidence where applicable. |
| `vision.pinch_calibration_started/completed` | backend calibration awaiter | status, thresholds, samples, path, message. |
| `vision.motion_region_calibration_started/completed` | backend calibration awaiter | region bounds, samples, path, message. |
| `vision.error` | backend | code and message for camera/tracking/protocol failures. |
| `VisionCameraProfileBenchmarkResult` logs | backend logs | open success, actual width/height/fps/fourcc, measured fps, read time, black frames, rejection reasons. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `vision.start_tracking` | backend stdin | `handle` -> `start_tracking` |
| `vision.stop_tracking` | backend stdin | `handle` -> `stop_tracking` |
| `vision.calibrate_pinch` | backend stdin | `handle` -> `start_pinch_calibration` |
| `vision.calibrate_motion_region` | backend stdin | `handle` -> `start_motion_region_calibration` |
| `vision.shutdown` | backend stdin | `handle` / run loop shutdown |

## External Side Effects

Opens and reads webcam devices, loads MediaPipe model files, writes calibration JSON files under backend logs, may play calibration beep cues, and writes stdout/stderr-style protocol logs.

## Safety / Guardrails

The worker should never execute application/browser actions. It only emits gesture/control data. Camera open failures must be reported as `vision.error` instead of crashing silently. Calibration failures must emit completion with `status=failed` so backend awaiters complete.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `VisionSidecarClientTests.cs` | Worker source checks for adaptive capture profile selection, monotonic timestamps, pointer mapping, pinch calibration, motion-region calibration, protocol parsing. | No live camera/OpenCV benchmark in CI. |
| `VisionGestureEventRouterTests.cs` | Backend handling of worker gesture messages after parsing. | Does not execute Python. |

## Known Risks / Fragility

Camera behavior is hardware/backend dependent. DSHOW default can choose slow YUY2; `DSHOW_MJPG_CONSTRUCTOR` is preferred only when benchmark proves sustained FPS. MediaPipe confidence and camera angle strongly affect pinch and corner reach. Bad calibration thresholds can make fists look like pinches or make pinches too insensitive.

## Change Notes for Agents

Keep worker stdout JSON protocol compatible with `VisionSidecarMessage` and `VisionSidecarHost.HandleOutputLineAsync`. Test Python syntax with `python -m py_compile Merlin.Backend\VisionScriptsision_worker.py` after changes.
