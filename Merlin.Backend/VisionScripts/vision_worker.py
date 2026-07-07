import json
import math
import os
import re
import shutil
import subprocess
import sys
import threading
import time


class VisionWorker:
    def __init__(self):
        self.cv2 = None
        self.mp = None
        self.BaseOptions = None
        self.HandLandmarker = None
        self.HandLandmarkerOptions = None
        self.VisionRunningMode = None
        self.import_error = None
        self.tracking = threading.Event()
        self.stop_requested = threading.Event()
        self.thread = None
        self.capture = None
        self.landmarker = None
        self.pinched_by_pointer = {}
        self.pinch_candidate_since_by_pointer = {}
        self.smoothed_by_pointer = {}
        self.last_emit_by_pointer = {}
        self.last_timestamp_ms = 0
        self.primary_position = None
        self.primary_last_seen = 0.0
        self.primary_lost_since = None
        self.config = {}
        self.pinch_calibration = None
        self.motion_region_calibration = None

    def load(self):
        try:
            import cv2  # type: ignore
            import mediapipe as mp  # type: ignore
            from mediapipe.tasks import python  # type: ignore
            from mediapipe.tasks.python import vision  # type: ignore

            self.cv2 = cv2
            self.mp = mp
            self.BaseOptions = python.BaseOptions
            self.HandLandmarker = vision.HandLandmarker
            self.HandLandmarkerOptions = vision.HandLandmarkerOptions
            self.VisionRunningMode = vision.RunningMode
        except Exception as exc:  # noqa: BLE001
            self.import_error = str(exc)
            self.write_error("IMPORT_FAILED", self.import_error)

        self.write({"type": "vision.ready", "version": 1})

    def run(self):
        self.load()
        for line in sys.stdin:
            if not line.strip():
                continue
            try:
                command = json.loads(line)
                self.handle(command)
            except Exception as exc:  # noqa: BLE001
                self.write_error("COMMAND_FAILED", f"{exc}. line_length={len(line)} line_preview={safe_preview(line)}")

    def handle(self, command):
        command_type = command.get("type", "")
        if command_type == "vision.start_tracking":
            self.start_tracking(command)
        elif command_type == "vision.calibrate_pinch":
            self.start_pinch_calibration(command)
        elif command_type == "vision.calibrate_motion_region":
            self.start_motion_region_calibration(command)
        elif command_type == "vision.stop_tracking":
            self.stop_tracking()
        elif command_type == "vision.shutdown":
            self.stop_tracking()
            sys.exit(0)
        else:
            self.write_error("UNKNOWN_COMMAND", command_type)

    def start_tracking(self, command):
        if self.import_error:
            self.write_error("VISION_UNAVAILABLE", self.import_error)
            return
        if self.thread and self.thread.is_alive():
            return

        self.config = command
        self.stop_requested.clear()
        self.tracking.set()
        self.pinched_by_pointer.clear()
        self.pinch_candidate_since_by_pointer.clear()
        self.smoothed_by_pointer.clear()
        self.last_emit_by_pointer.clear()
        self.last_timestamp_ms = 0
        self.primary_position = None
        self.primary_last_seen = 0.0
        self.primary_lost_since = None
        self.pinch_calibration = None
        self.motion_region_calibration = None
        self.thread = threading.Thread(target=self.capture_loop, daemon=True)
        self.thread.start()

    def stop_tracking(self):
        self.stop_requested.set()
        self.tracking.clear()
        if self.thread and self.thread.is_alive():
            self.thread.join(timeout=2.0)
        self.release_camera()
        self.close_landmarker()
        self.release_all_pinches()
        self.pinch_calibration = None
        self.motion_region_calibration = None
        self.write({"type": "vision.tracking_stopped"})

    def start_pinch_calibration(self, command):
        if not self.tracking.is_set() or not (self.thread and self.thread.is_alive()):
            self.write({
                "type": "vision.pinch_calibration_completed",
                "status": "failed",
                "message": "Vision tracking is not active.",
            })
            return

        self.release_all_pinches()
        now = time.monotonic()
        self.pinch_calibration = {
            "started": now,
            "lead_in": max(0.0, float(command.get("leadInSeconds", 3.0) or 3.0)),
            "open_seconds": max(0.5, float(command.get("openSeconds", 2.0) or 2.0)),
            "pinched_seconds": max(0.5, float(command.get("pinchedSeconds", 2.0) or 2.0)),
            "release_seconds": max(0.5, float(command.get("releaseSeconds", 1.5) or 1.5)),
            "phase_pause_seconds": max(0.0, float(command.get("phasePauseSeconds", 1.0) or 1.0)),
            "calibration_path": str(command.get("calibrationPath", "") or "").strip(),
            "open": [],
            "pinched": [],
            "release": [],
            "last_phase": "",
        }
        self.write({
            "type": "vision.pinch_calibration_started",
            "message": "One beep means open hand. Two beeps means pinch. Three beeps means release.",
        })

    def start_motion_region_calibration(self, command):
        if not self.tracking.is_set() or not (self.thread and self.thread.is_alive()):
            self.write({
                "type": "vision.motion_region_calibration_completed",
                "status": "failed",
                "message": "Vision tracking is not active.",
            })
            return

        self.release_all_pinches()
        now = time.monotonic()
        self.motion_region_calibration = {
            "started": now,
            "lead_in": max(0.0, float(command.get("leadInSeconds", 1.0) or 1.0)),
            "corner_seconds": max(0.5, float(command.get("cornerSeconds", 2.0) or 2.0)),
            "phase_pause_seconds": max(0.0, float(command.get("phasePauseSeconds", 1.0) or 1.0)),
            "padding": clamp(float(command.get("padding", 0.04) or 0.04), 0.0, 0.20),
            "calibration_path": str(command.get("calibrationPath", "") or "").strip(),
            "top_left": [],
            "top_right": [],
            "bottom_right": [],
            "bottom_left": [],
            "last_phase": "",
        }
        self.write({
            "type": "vision.motion_region_calibration_started",
            "message": "Move your hand to top left, top right, bottom right, then bottom left after each beep cue.",
        })

    def capture_loop(self):
        cv2 = self.cv2
        if cv2 is None or self.HandLandmarker is None:
            self.write_error("VISION_UNAVAILABLE")
            return

        model_path = str(self.config.get("modelAssetPath", "") or "").strip()
        if not model_path or not os.path.isfile(model_path):
            self.write_error("MODEL_NOT_FOUND", f"Hand landmarker model asset not found: {model_path}")
            return

        try:
            model_start = time.perf_counter()
            self.write_log(f"VisionCameraModelLoadStarted path={model_path}")
            self.landmarker = self.create_landmarker(model_path)
            self.write_log(f"VisionCameraModelLoadCompleted elapsedMs={elapsed_ms(model_start):.1f}")
        except Exception as exc:  # noqa: BLE001
            self.write_error("MODEL_LOAD_FAILED", str(exc))
            return

        camera_index = int(self.config.get("cameraIndex", 0) or 0)
        preferred_camera_name = str(self.config.get("cameraName", "") or "").strip()
        backend_preference = str(self.config.get("backend", "Auto") or "Auto").strip()
        profile_preference = str(self.config.get("captureProfile", "Auto") or "Auto").strip()
        width = int(self.config.get("width", 1280) or 1280)
        height = int(self.config.get("height", 720) or 720)
        fps = int(self.config.get("fps", 30) or 30)
        self.load_pinch_calibration_override()
        self.load_motion_region_calibration_override()
        self.write_log(
            "VisionPointerMappingConfigured "
            f"gainX={float(self.config.get('pointerGainX', 1.0) or 1.0):.2f} "
            f"gainY={float(self.config.get('pointerGainY', 1.0) or 1.0):.2f} "
            f"regionLeft={float(self.config.get('controlRegionLeft', 0.0) or 0.0):.3f} "
            f"regionTop={float(self.config.get('controlRegionTop', 0.0) or 0.0):.3f} "
            f"regionRight={float(self.config.get('controlRegionRight', 1.0) or 1.0):.3f} "
            f"regionBottom={float(self.config.get('controlRegionBottom', 1.0) or 1.0):.3f}")
        self.write_log(
            "VisionCameraOpenRequested "
            f"cameraName={preferred_camera_name!r} cameraIndex={camera_index} backend={backend_preference!r} "
            f"captureProfile={profile_preference!r} requestedWidth={width} requestedHeight={height} requestedFps={fps}")
        open_start = time.perf_counter()
        self.capture, camera_label, selected_metrics = self.open_configured_camera(
            camera_index,
            preferred_camera_name,
            backend_preference,
            profile_preference,
            width,
            height,
            fps)
        self.write_log(
            f"VisionCameraOpenCompleted cameraLabel={camera_label!r} "
            f"opened={self.capture.isOpened()} elapsedMs={elapsed_ms(open_start):.1f}")
        if not self.capture.isOpened():
            self.write_error("CAMERA_OPEN_FAILED", camera_label)
            self.release_camera()
            self.close_landmarker()
            return

        actual_width = int(selected_metrics.get("actualWidth") or self.capture.get(cv2.CAP_PROP_FRAME_WIDTH))
        actual_height = int(selected_metrics.get("actualHeight") or self.capture.get(cv2.CAP_PROP_FRAME_HEIGHT))
        actual_fps = float(selected_metrics.get("actualFps") or self.capture.get(cv2.CAP_PROP_FPS) or fps)
        self.write({
            "type": "vision.tracking_started",
            "cameraName": camera_label,
            "actualWidth": actual_width,
            "actualHeight": actual_height,
            "actualFps": actual_fps,
        })

        first_frame_logged = False
        while not self.stop_requested.is_set():
            try:
                read_start = time.perf_counter()
                ok, frame = self.capture.read()
                if not first_frame_logged:
                    frame_shape = None if frame is None else list(frame.shape)
                    self.write_log(
                        f"VisionCameraFirstFrameRead ok={ok} frameShape={frame_shape} "
                        f"elapsedMs={elapsed_ms(read_start):.1f}")
                    first_frame_logged = True
                if not ok:
                    time.sleep(0.02)
                    continue

                if bool(self.config.get("mirrorPreview", True)):
                    frame = cv2.flip(frame, 1)

                rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                image = self.mp.Image(image_format=self.mp.ImageFormat.SRGB, data=rgb)
                timestamp_ms = self.next_timestamp_ms()
                try:
                    result = self.landmarker.detect_for_video(image, timestamp_ms)
                except Exception as exc:  # noqa: BLE001
                    self.write_error("TRACKING_FAILED", str(exc))
                    continue
                self.process_hands(result)

                if bool(self.config.get("debugPreview", False)):
                    cv2.imshow("Merlin Vision", frame)
                    cv2.waitKey(1)
            except Exception as exc:  # noqa: BLE001
                self.write_error("TRACKING_FAILED", str(exc))
                break

        self.release_camera()
        self.close_landmarker()

    def open_configured_camera(self, camera_index, preferred_camera_name, backend_preference, profile_preference, width, height, fps):
        cv2 = self.cv2
        if cv2 is None:
            return None, "camera unavailable", {}

        requested_backend = normalize_backend_name(backend_preference)
        benchmark_index = camera_index
        if preferred_camera_name:
            preferred_index = self.find_directshow_camera_index(preferred_camera_name)
            if preferred_index is not None:
                benchmark_index = preferred_index
                self.write_log(
                    f"VisionCameraPreferredNameResolved cameraName={preferred_camera_name!r} "
                    f"cameraIndex={benchmark_index}")
            else:
                self.write_log(
                    f"VisionCameraPreferredNameNotResolved cameraName={preferred_camera_name!r} "
                    "using configured cameraIndex")

        capture, metrics = self.select_camera_backend(
            benchmark_index,
            requested_backend,
            normalize_profile_name(profile_preference),
            width,
            height,
            fps,
            preferred_camera_name)
        if capture is not None:
            return capture, self.camera_label(benchmark_index, capture, metrics), metrics

        if benchmark_index != 0:
            capture, metrics = self.select_camera_backend(
                0,
                requested_backend,
                normalize_profile_name(profile_preference),
                width,
                height,
                fps,
                "")
            if capture is not None:
                return capture, self.camera_label(0, capture, metrics), metrics

        return cv2.VideoCapture(), f"{preferred_camera_name or 'camera'} unavailable; fallback camera:0 failed", {}

    def select_camera_backend(self, camera_index, requested_backend, requested_profile, width, height, fps, preferred_camera_name):
        candidates = self.capture_profile_candidates(requested_backend, requested_profile)
        if not candidates:
            self.write_log(
                f"VisionCameraBackendSelectionFailed requestedBackend={requested_backend!r} "
                f"requestedProfile={requested_profile!r} reason=no_supported_profiles")
            return None, {}

        benchmark_results = []
        last_capture = None
        last_metrics = None
        early_accept = False
        for index, profile in enumerate(candidates):
            if last_capture is not None:
                last_capture.release()
                last_capture = None
            capture, metrics = self.benchmark_capture_profile(
                camera_index,
                preferred_camera_name,
                profile["backendName"],
                profile,
                width,
                height,
                fps)
            benchmark_results.append(metrics)
            if capture is not None:
                last_capture = capture
                last_metrics = metrics
            if self.can_early_accept_profile(metrics, fps, requested_backend, requested_profile):
                early_accept = True
                for skipped_profile in candidates[index + 1:]:
                    benchmark_results.append({
                        "backend": skipped_profile["backendName"],
                        "profile": skipped_profile["name"],
                        "opened": False,
                        "reason": "skipped_preferred_profile_valid",
                        "measuredFps": None,
                        "failedReadCount": None,
                        "blackFrameCount": None,
                    })
                break

        selected_metrics = self.choose_benchmark_result(benchmark_results, fps)
        rejected = []
        for metrics in benchmark_results:
            profile_name = metrics.get("profile", "UNKNOWN")
            if selected_metrics and profile_name == selected_metrics.get("profile"):
                continue
            rejected.append({
                "backend": metrics.get("backend", "UNKNOWN"),
                "profile": profile_name,
                "reason": metrics.get("reason", "not_selected"),
                "measuredFps": metrics.get("measuredFps"),
                "startupMs": metrics.get("startupMs"),
                "averageReadMs": metrics.get("averageReadMs"),
                "actualFourcc": metrics.get("actualFourcc"),
                "actualResolution": metrics.get("actualResolution"),
                "failedReadCount": metrics.get("failedReadCount"),
                "blackFrameCount": metrics.get("blackFrameCount"),
            })

        if selected_metrics is None:
            self.write_log(
                "VisionCameraBackendSelectionCompleted "
                f"selectedBackend=None rejectedBackends={json.dumps(rejected, separators=(',', ':'))}")
            return None, {}

        acceptable_fps = self.acceptable_measured_fps(fps)
        reason = str(selected_metrics.get("selectionReason") or "best_sustained_fps")
        if float(selected_metrics.get("measuredFps") or 0.0) < acceptable_fps:
            reason = "best_available_below_acceptability_threshold"

        self.write_log(
            "VisionCameraBackendSelectionCompleted "
            f"selectedBackend={selected_metrics.get('backend')} "
            f"selectedProfile={selected_metrics.get('profile')} "
            f"reason={reason} "
            f"startupMs={float(selected_metrics.get('startupMs') or 0.0):.1f} "
            f"measuredFps={float(selected_metrics.get('measuredFps') or 0.0):.2f} "
            f"avgReadMs={float(selected_metrics.get('averageReadMs') or 0.0):.1f} "
            f"acceptableFps={acceptable_fps:.2f} "
            f"selectedFourcc={selected_metrics.get('actualFourcc')} "
            f"actualResolution={selected_metrics.get('actualResolution')} "
            f"rejectedBackends={json.dumps(rejected, separators=(',', ':'))}")

        if selected_metrics is last_metrics and last_capture is not None:
            selected_capture = last_capture
            self.write_log(
                f"VisionCameraSelectedProfileReused source=camera:{camera_index} "
                f"backend={selected_metrics.get('backend')} profile={selected_metrics.get('profile')}")
        else:
            if last_capture is not None:
                last_capture.release()
            selected_capture = self.open_selected_capture_profile(
                camera_index,
                selected_metrics.get("backend"),
                selected_metrics.get("profile"),
                width,
                height,
                fps)
        if selected_capture is None:
            selected_metrics["reason"] = "selected_backend_reopen_failed"
            return None, selected_metrics

        return selected_capture, selected_metrics

    def capture_profile_candidates(self, requested_backend, requested_profile):
        cv2 = self.cv2
        profiles = []
        def add(name, backend_name, backend, mode):
            profiles.append({
                "name": name,
                "backendName": backend_name,
                "backend": backend,
                "mode": mode,
            })

        if requested_profile != "AUTO":
            if requested_profile.startswith("DSHOW") and requested_backend not in {"AUTO", "DSHOW"}:
                return []
            if requested_profile.startswith("MSMF") and requested_backend not in {"AUTO", "MSMF"}:
                return []
            if requested_profile == "DEFAULT" and requested_backend not in {"AUTO", "DEFAULT"}:
                return []
            if requested_profile == "DSHOW_MJPG_CONSTRUCTOR" and hasattr(cv2, "CAP_DSHOW"):
                add("DSHOW_MJPG_CONSTRUCTOR", "DSHOW", cv2.CAP_DSHOW, "dshow_mjpg_constructor")
            elif requested_profile == "DSHOW_MJPG_SET_BEFORE_AFTER" and hasattr(cv2, "CAP_DSHOW"):
                add("DSHOW_MJPG_SET_BEFORE_AFTER", "DSHOW", cv2.CAP_DSHOW, "dshow_mjpg_set_before_after")
            elif requested_profile == "DSHOW_DEFAULT" and hasattr(cv2, "CAP_DSHOW"):
                add("DSHOW_DEFAULT", "DSHOW", cv2.CAP_DSHOW, "default")
            elif requested_profile == "MSMF_DEFAULT" and hasattr(cv2, "CAP_MSMF"):
                add("MSMF_DEFAULT", "MSMF", cv2.CAP_MSMF, "default")
            elif requested_profile == "DEFAULT":
                add("DEFAULT", "DEFAULT", None, "default")
            else:
                self.write_log(f"VisionCameraProfileUnknown requestedProfile={requested_profile!r} fallback=AUTO")
                return self.capture_profile_candidates(requested_backend, "AUTO")
            return profiles

        if requested_backend == "AUTO":
            if hasattr(cv2, "CAP_DSHOW"):
                add("DSHOW_MJPG_CONSTRUCTOR", "DSHOW", cv2.CAP_DSHOW, "dshow_mjpg_constructor")
                add("DSHOW_MJPG_SET_BEFORE_AFTER", "DSHOW", cv2.CAP_DSHOW, "dshow_mjpg_set_before_after")
                add("DSHOW_DEFAULT", "DSHOW", cv2.CAP_DSHOW, "default")
            if hasattr(cv2, "CAP_MSMF"):
                add("MSMF_DEFAULT", "MSMF", cv2.CAP_MSMF, "default")
            add("DEFAULT", "DEFAULT", None, "default")
            return profiles

        if requested_backend == "MSMF" and hasattr(cv2, "CAP_MSMF"):
            add("MSMF_DEFAULT", "MSMF", cv2.CAP_MSMF, "default")
            return profiles
        if requested_backend == "DSHOW" and hasattr(cv2, "CAP_DSHOW"):
            add("DSHOW_MJPG_CONSTRUCTOR", "DSHOW", cv2.CAP_DSHOW, "dshow_mjpg_constructor")
            add("DSHOW_MJPG_SET_BEFORE_AFTER", "DSHOW", cv2.CAP_DSHOW, "dshow_mjpg_set_before_after")
            add("DSHOW_DEFAULT", "DSHOW", cv2.CAP_DSHOW, "default")
            return profiles
        if requested_backend == "DEFAULT":
            add("DEFAULT", "DEFAULT", None, "default")
            return profiles

        self.write_log(f"VisionCameraBackendUnknown requestedBackend={requested_backend!r} fallback=AUTO")
        return self.capture_profile_candidates("AUTO", requested_profile)

    def can_early_accept_profile(self, metrics, target_fps, requested_backend, requested_profile):
        if requested_backend != "AUTO" or requested_profile != "AUTO":
            return False
        if metrics.get("profile") != "DSHOW_MJPG_CONSTRUCTOR":
            return False
        if metrics.get("reason") != "candidate":
            return False
        if str(metrics.get("actualFourcc", "")).upper() != "MJPG":
            return False
        if not bool(metrics.get("exactResolution")):
            return False
        if float(metrics.get("measuredFps") or 0.0) < self.acceptable_measured_fps(target_fps):
            return False
        if float(metrics.get("averageReadMs") or 9999.0) > self.slow_average_read_threshold_ms(target_fps):
            return False
        return True

    def benchmark_capture_profile(self, camera_index, preferred_camera_name, backend_name, profile, width, height, fps):
        cv2 = self.cv2
        profile_name = profile["name"]
        metrics = {
            "backend": backend_name,
            "profile": profile_name,
            "opened": False,
            "reason": "open_failed",
            "measuredFps": 0.0,
            "failedReadCount": 0,
            "blackFrameCount": 0,
        }

        source = camera_index
        if preferred_camera_name and backend_name == "DSHOW":
            source = camera_index

        self.write_log(
            f"VisionCameraProfileBenchmarkStarted source=camera:{camera_index} backend={backend_name} "
            f"profile={profile_name} "
            f"requestedWidth={width} requestedHeight={height} requestedFps={fps}")
        open_start = time.perf_counter()
        capture = self.open_capture_for_profile(source, profile, width, height, fps)
        open_ms = elapsed_ms(open_start)
        metrics["openMs"] = round(open_ms, 1)
        metrics["opened"] = bool(capture.isOpened())
        if not capture.isOpened():
            self.write_log(
                f"VisionCameraProfileBenchmarkResult backend={backend_name} profile={profile_name} opened=False "
                f"openMs={open_ms:.1f} reason=open_failed")
            capture.release()
            return None, metrics

        set_start = time.perf_counter()
        self.configure_capture_for_profile(capture, profile, width, height, fps)
        set_ms = elapsed_ms(set_start)

        actual_width = int(capture.get(cv2.CAP_PROP_FRAME_WIDTH))
        actual_height = int(capture.get(cv2.CAP_PROP_FRAME_HEIGHT))
        actual_fps = float(capture.get(cv2.CAP_PROP_FPS) or 0.0)
        reported_fps = actual_fps if actual_fps > 0 else float(fps)
        actual_fourcc = fourcc_text(capture.get(cv2.CAP_PROP_FOURCC))
        metrics.update({
            "actualWidth": actual_width,
            "actualHeight": actual_height,
            "actualFps": round(reported_fps, 3),
            "actualFourcc": actual_fourcc,
            "actualResolution": f"{actual_width}x{actual_height}",
            "formatSetMs": round(set_ms, 1),
        })

        first_ok, first_frame, first_frame_ms = self.read_timed(capture)
        metrics["firstFrameMs"] = round(first_frame_ms, 1)
        metrics["startupMs"] = round(open_ms + set_ms + first_frame_ms, 1)
        if not first_ok or first_frame is None:
            metrics["reason"] = "first_frame_failed"
            self.write_log(
                f"VisionCameraProfileBenchmarkResult backend={backend_name} profile={profile_name} opened=True "
                f"actualWidth={actual_width} actualHeight={actual_height} actualFps={reported_fps:.2f} "
                f"actualFourcc={actual_fourcc} firstFrameMs={first_frame_ms:.1f} "
                "measuredFps=0.00 reason=first_frame_failed")
            capture.release()
            return None, metrics

        for _ in range(2):
            capture.read()

        read_count = 30
        early_abort_count = 6
        slow_read_threshold_ms = self.slow_average_read_threshold_ms(fps)
        successful_reads = 0
        failed_reads = 0
        black_frames = 0
        total_read_ms = 0.0
        measurement_start = time.perf_counter()
        measured_reads = 0
        early_abort = False
        for index in range(read_count):
            ok, frame, read_ms = self.read_timed(capture)
            measured_reads += 1
            total_read_ms += read_ms
            if not ok or frame is None:
                failed_reads += 1
            else:
                successful_reads += 1
                if is_black_frame(frame):
                    black_frames += 1
            if index + 1 >= early_abort_count:
                current_avg = total_read_ms / measured_reads
                if current_avg > slow_read_threshold_ms and target_fps_is_realtime(fps):
                    early_abort = True
                    break

        measurement_elapsed = time.perf_counter() - measurement_start
        measured_fps = successful_reads / measurement_elapsed if measurement_elapsed > 0 else 0.0
        avg_read_ms = total_read_ms / measured_reads if measured_reads > 0 else 0.0
        metrics.update({
            "successfulReadCount": successful_reads,
            "failedReadCount": failed_reads,
            "blackFrameCount": black_frames,
            "measuredFps": round(measured_fps, 3),
            "averageReadMs": round(avg_read_ms, 1),
            "measurementFrames": measured_reads,
            "measurementMs": round(measurement_elapsed * 1000.0, 1),
            "exactResolution": actual_width == width and actual_height == height,
        })

        if successful_reads == 0:
            metrics["reason"] = "all_reads_failed"
        elif black_frames >= max(3, successful_reads * 0.8):
            metrics["reason"] = "black_frames"
        elif actual_width <= 0 or actual_height <= 0:
            metrics["reason"] = "invalid_resolution"
        elif early_abort:
            metrics["reason"] = "slow_reads"
        else:
            metrics["reason"] = "candidate"

        self.write_log(
            f"VisionCameraProfileBenchmarkResult backend={backend_name} profile={profile_name} opened=True "
            f"openMs={open_ms:.1f} formatSetMs={set_ms:.1f} "
            f"startupMs={float(metrics.get('startupMs') or 0.0):.1f} "
            f"actualWidth={actual_width} actualHeight={actual_height} actualFps={reported_fps:.2f} "
            f"actualFourcc={actual_fourcc} firstFrameMs={first_frame_ms:.1f} "
            f"measuredFps={measured_fps:.2f} averageReadMs={avg_read_ms:.1f} "
            f"failedReadCount={failed_reads} blackFrameCount={black_frames} "
            f"measurementFrames={measured_reads} reason={metrics['reason']}")

        if metrics["reason"] != "candidate":
            capture.release()
            return None, metrics

        return capture, metrics

    def open_selected_capture_profile(self, camera_index, backend_name, profile_name, width, height, fps):
        matching_profiles = [
            profile for profile in self.capture_profile_candidates(str(backend_name or "DEFAULT"), str(profile_name or "AUTO"))
            if profile["name"] == profile_name
        ]
        if not matching_profiles:
            return None
        profile = matching_profiles[0]

        self.write_log(
            f"VisionCameraSelectedProfileOpenStarted source=camera:{camera_index} backend={backend_name} "
            f"profile={profile_name} "
            f"width={width} height={height} fps={fps}")
        start = time.perf_counter()
        capture = self.open_capture_for_profile(camera_index, profile, width, height, fps)
        if not capture.isOpened():
            self.write_log(
                f"VisionCameraSelectedProfileOpenCompleted source=camera:{camera_index} backend={backend_name} "
                f"profile={profile_name} "
                f"opened=False elapsedMs={elapsed_ms(start):.1f}")
            capture.release()
            return None

        self.configure_capture_for_profile(capture, profile, width, height, fps)
        self.write_log(
            f"VisionCameraSelectedProfileOpenCompleted source=camera:{camera_index} backend={backend_name} "
            f"profile={profile_name} "
            f"opened=True elapsedMs={elapsed_ms(start):.1f} "
            f"actualWidth={int(capture.get(self.cv2.CAP_PROP_FRAME_WIDTH))} "
            f"actualHeight={int(capture.get(self.cv2.CAP_PROP_FRAME_HEIGHT))} "
            f"actualFps={float(capture.get(self.cv2.CAP_PROP_FPS) or 0.0):.2f} "
            f"actualFourcc={fourcc_text(capture.get(self.cv2.CAP_PROP_FOURCC))}")
        return capture

    def open_capture_for_profile(self, source, profile, width, height, fps):
        mode = profile["mode"]
        backend = profile["backend"]
        cv2 = self.cv2
        if mode == "dshow_mjpg_constructor":
            return cv2.VideoCapture(source, backend, [
                cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*"MJPG"),
                cv2.CAP_PROP_FRAME_WIDTH, width,
                cv2.CAP_PROP_FRAME_HEIGHT, height,
                cv2.CAP_PROP_FPS, fps,
            ])
        return cv2.VideoCapture(source, backend) if backend is not None else cv2.VideoCapture(source)

    def configure_capture_for_profile(self, capture, profile, width, height, fps):
        cv2 = self.cv2
        mode = profile["mode"]
        if mode == "dshow_mjpg_set_before_after":
            capture.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*"MJPG"))
        if mode != "dshow_mjpg_constructor":
            capture.set(cv2.CAP_PROP_FRAME_WIDTH, width)
            capture.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
            capture.set(cv2.CAP_PROP_FPS, fps)
        if mode == "dshow_mjpg_set_before_after":
            capture.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*"MJPG"))

    def choose_benchmark_result(self, results, target_fps):
        candidates = [
            item for item in results
            if item.get("reason") == "candidate"
        ]
        if not candidates:
            return None

        acceptable = self.acceptable_measured_fps(target_fps)
        acceptable_results = [
            item for item in candidates
            if float(item.get("measuredFps") or 0.0) >= acceptable
        ]
        pool = acceptable_results if acceptable_results else candidates
        exact_pool = [item for item in pool if bool(item.get("exactResolution"))]
        if exact_pool:
            pool = exact_pool

        best = max(pool, key=self.capture_profile_score)
        best_score = self.capture_profile_score(best)
        preferred_order = [
            "DSHOW_MJPG_CONSTRUCTOR",
            "DSHOW_MJPG_SET_BEFORE_AFTER",
            "MSMF_DEFAULT",
            "DEFAULT",
            "DSHOW_DEFAULT",
        ]
        for profile_name in preferred_order:
            preferred = next(
                (
                    item for item in pool
                    if item.get("profile") == profile_name
                    and self.capture_profile_is_close_enough(item, best)
                ),
                None)
            if preferred is not None:
                preferred["selectionReason"] = (
                    "best_sustained_fps"
                    if self.capture_profile_score(preferred) == best_score
                    else "preferred_profile_within_score_tolerance")
                return preferred

        best["selectionReason"] = "best_sustained_fps"
        return best

    def capture_profile_score(self, metrics):
        measured_fps = float(metrics.get("measuredFps") or 0.0)
        average_read_ms = float(metrics.get("averageReadMs") or 9999.0)
        startup_ms = float(metrics.get("startupMs") or 999999.0)
        exact_resolution = 1 if bool(metrics.get("exactResolution")) else 0
        is_mjpg = 1 if str(metrics.get("actualFourcc", "")).upper() == "MJPG" else 0
        return (
            exact_resolution,
            measured_fps,
            is_mjpg,
            -average_read_ms,
            -startup_ms,
        )

    def capture_profile_is_close_enough(self, candidate, best):
        candidate_fps = float(candidate.get("measuredFps") or 0.0)
        best_fps = float(best.get("measuredFps") or 0.0)
        candidate_read_ms = float(candidate.get("averageReadMs") or 9999.0)
        best_read_ms = float(best.get("averageReadMs") or 9999.0)
        candidate_startup_ms = float(candidate.get("startupMs") or 999999.0)
        best_startup_ms = float(best.get("startupMs") or 999999.0)
        if bool(candidate.get("exactResolution")) != bool(best.get("exactResolution")):
            return False
        if candidate_fps < best_fps - 3.0:
            return False
        if candidate_read_ms > best_read_ms + 12.0:
            return False
        if str(candidate.get("actualFourcc", "")).upper() == "MJPG":
            return True
        return candidate_startup_ms <= best_startup_ms + 500.0

    def acceptable_measured_fps(self, target_fps):
        if target_fps <= 0:
            return 0.0
        return min(float(target_fps) * 0.8, max(float(target_fps) - 6.0, 1.0))

    def slow_average_read_threshold_ms(self, target_fps):
        if target_fps >= 24:
            return 80.0
        if target_fps > 0:
            return max(1000.0 / float(target_fps) * 2.5, 80.0)
        return 120.0

    def read_timed(self, capture):
        start = time.perf_counter()
        ok, frame = capture.read()
        return ok, frame, elapsed_ms(start)

    def camera_label(self, camera_index, capture, metrics):
        try:
            backend_name = capture.getBackendName()
        except Exception:
            backend_name = str(metrics.get("backend", "unknown"))
        return f"camera:{camera_index} via {backend_name}"

    def find_directshow_camera_index(self, preferred_camera_name):
        devices = list_directshow_video_devices()
        preferred = normalize_device_name(preferred_camera_name)
        if not preferred:
            return None

        for index, device_name in enumerate(devices):
            normalized = normalize_device_name(device_name)
            if preferred in normalized or normalized in preferred:
                return index

        return None

    def create_landmarker(self, model_path):
        options = self.HandLandmarkerOptions(
            base_options=self.BaseOptions(model_asset_path=model_path),
            running_mode=self.VisionRunningMode.VIDEO,
            num_hands=max(1, int(self.config.get("maxHands", 2) or 2)),
            min_hand_detection_confidence=0.55,
            min_hand_presence_confidence=0.55,
            min_tracking_confidence=0.55,
        )
        return self.HandLandmarker.create_from_options(options)

    def next_timestamp_ms(self):
        now_ms = int(time.monotonic() * 1000)
        timestamp_ms = max(now_ms, self.last_timestamp_ms + 1)
        self.last_timestamp_ms = timestamp_ms
        return timestamp_ms

    def process_hands(self, result):
        if not getattr(result, "hand_landmarks", None):
            self.update_pinch_calibration(None, 0.0)
            self.update_motion_region_calibration(None, 0.0)
            self.handle_primary_missing(set())
            return

        hands = self.assign_logical_hands(result)
        visible_pointer_ids = set()
        for hand in hands:
            pointer_id = hand["pointer_id"]
            landmarks = hand["landmarks"]
            confidence = hand["confidence"]
            x, y = self.pointer_position(landmarks)
            x, y = self.smooth_position(pointer_id, x, y)
            visible_pointer_ids.add(pointer_id)
            self.emit_pointer(pointer_id, x, y, confidence)
            if pointer_id == "primary":
                try:
                    if self.update_motion_region_calibration(landmarks, confidence):
                        continue
                except Exception as exc:  # noqa: BLE001
                    self.fail_motion_region_calibration(f"Motion region calibration sampling failed: {exc}")
                try:
                    if self.update_pinch_calibration(landmarks, confidence):
                        continue
                except Exception as exc:  # noqa: BLE001
                    self.fail_pinch_calibration(f"Calibration sampling failed: {exc}")
            self.update_pinch(pointer_id, landmarks, x, y, confidence)

        self.release_missing_pinches(visible_pointer_ids)

    def assign_logical_hands(self, result):
        candidates = self.detected_hand_candidates(result)
        if not candidates:
            self.handle_primary_missing(set())
            return []

        primary_candidate = self.select_primary_candidate(candidates)
        assigned = []
        assigned_indices = set()
        if primary_candidate is not None:
            primary_candidate["pointer_id"] = "primary"
            assigned.append(primary_candidate)
            assigned_indices.add(primary_candidate["index"])
            self.primary_position = (primary_candidate["x"], primary_candidate["y"])
            self.primary_last_seen = time.monotonic()
            self.primary_lost_since = None
        else:
            self.handle_primary_missing({"secondary"})

        secondary_candidate = self.select_secondary_candidate(candidates, assigned_indices)
        if secondary_candidate is not None:
            secondary_candidate["pointer_id"] = "secondary"
            assigned.append(secondary_candidate)

        return assigned

    def detected_hand_candidates(self, result):
        hand_landmarks = list(getattr(result, "hand_landmarks", []) or [])[:2]
        candidates = []
        for index, landmarks in enumerate(hand_landmarks):
            handedness = self.handedness_label(result, index)
            confidence = self.hand_confidence(result, index)
            x, _ = self.pointer_position(landmarks)
            _, y = self.pointer_position(landmarks)
            candidates.append({
                "index": index,
                "handedness": handedness,
                "landmarks": landmarks,
                "confidence": confidence,
                "x": x,
                "y": y,
            })

        return candidates

    def select_primary_candidate(self, candidates):
        now = time.monotonic()
        grace_seconds = float(self.config.get("primaryLostGraceMs", 600) or 600) / 1000.0
        switch_distance_threshold = float(self.config.get("primarySwitchDistanceThreshold", 0.18) or 0.18)

        if self.primary_position is None:
            selected = self.best_initial_candidate(candidates)
            return selected

        closest = min(
            candidates,
            key=lambda candidate: distance(
                self.primary_position[0],
                self.primary_position[1],
                candidate["x"],
                candidate["y"]))
        closest_distance = distance(
            self.primary_position[0],
            self.primary_position[1],
            closest["x"],
            closest["y"])
        if closest_distance <= switch_distance_threshold:
            return closest

        if self.primary_lost_since is None:
            self.primary_lost_since = now

        if now - self.primary_lost_since <= grace_seconds:
            return None

        selected = self.best_initial_candidate(candidates)
        return selected

    def best_initial_candidate(self, candidates):
        return max(
            candidates,
            key=lambda candidate: (
                candidate["confidence"],
                -abs(candidate["x"] - 0.5),
                -candidate["index"]))

    def select_secondary_candidate(self, candidates, assigned_indices):
        remaining = [candidate for candidate in candidates if candidate["index"] not in assigned_indices]
        if not remaining:
            return None
        return max(remaining, key=lambda candidate: candidate["confidence"])

    def handle_primary_missing(self, visible_pointer_ids):
        if self.primary_position is None:
            self.release_missing_pinches(visible_pointer_ids)
            return

        if self.primary_lost_since is None:
            self.primary_lost_since = time.monotonic()

        grace_seconds = float(self.config.get("primaryLostGraceMs", 600) or 600) / 1000.0
        if time.monotonic() - self.primary_lost_since <= grace_seconds:
            protected = set(visible_pointer_ids)
            protected.add("primary")
            self.release_missing_pinches(protected)
            return

        self.release_missing_pinches(visible_pointer_ids)

    def handedness_label(self, result, index):
        handedness = getattr(result, "handedness", None)
        if handedness and index < len(handedness) and handedness[index]:
            category = handedness[index][0]
            category_name = getattr(category, "category_name", "") or getattr(category, "display_name", "")
            return str(category_name).strip()
        return ""

    def hand_confidence(self, result, index):
        handedness = getattr(result, "handedness", None)
        if handedness and index < len(handedness) and handedness[index]:
            return float(handedness[index][0].score)
        return 0.8

    def pointer_position(self, landmarks):
        index_tip = landmarks[8]
        return self.map_pointer_position(clamp01(index_tip.x), clamp01(index_tip.y))

    def map_pointer_position(self, x, y):
        left = clamp01(float(self.config.get("controlRegionLeft", 0.0) or 0.0))
        top = clamp01(float(self.config.get("controlRegionTop", 0.0) or 0.0))
        right = clamp01(float(self.config.get("controlRegionRight", 1.0) or 1.0))
        bottom = clamp01(float(self.config.get("controlRegionBottom", 1.0) or 1.0))
        if right <= left:
            left, right = 0.0, 1.0
        if bottom <= top:
            top, bottom = 0.0, 1.0

        region_x = clamp01((x - left) / max(right - left, 0.0001))
        region_y = clamp01((y - top) / max(bottom - top, 0.0001))
        gain_x = max(0.01, float(self.config.get("pointerGainX", 1.0) or 1.0))
        gain_y = max(0.01, float(self.config.get("pointerGainY", 1.0) or 1.0))
        mapped_x = 0.5 + (region_x - 0.5) * gain_x
        mapped_y = 0.5 + (region_y - 0.5) * gain_y
        return clamp01(mapped_x), clamp01(mapped_y)

    def smooth_position(self, pointer_id, x, y):
        alpha = float(self.config.get("smoothingAlpha", 0.25) or 0.25)
        deadzone = float(self.config.get("pointerDeadzone", 0.003) or 0.003)
        smoothed = self.smoothed_by_pointer.get(pointer_id)
        if smoothed is None:
            self.smoothed_by_pointer[pointer_id] = (x, y)
            return x, y
        previous_x, previous_y = smoothed
        smoothed_x = previous_x * (1.0 - alpha) + x * alpha
        smoothed_y = previous_y * (1.0 - alpha) + y * alpha
        if distance(previous_x, previous_y, smoothed_x, smoothed_y) < deadzone:
            return previous_x, previous_y
        self.smoothed_by_pointer[pointer_id] = (smoothed_x, smoothed_y)
        return smoothed_x, smoothed_y

    def emit_pointer(self, pointer_id, x, y, confidence):
        emit_rate = max(1, int(self.config.get("emitRateHz", 30) or 30))
        now = time.monotonic()
        last_emit = float(self.last_emit_by_pointer.get(pointer_id, 0.0))
        if now - last_emit < 1.0 / emit_rate:
            return
        self.last_emit_by_pointer[pointer_id] = now
        self.write({
            "type": "gesture.pointer.move",
            "pointerId": pointer_id,
            "x": x,
            "y": y,
            "confidence": confidence,
            "source": "webcam",
            "handedness": self.handedness_for_pointer(pointer_id),
        })

    def update_pinch_calibration(self, landmarks, confidence):
        calibration = self.pinch_calibration
        if not calibration:
            return False

        now = time.monotonic()
        elapsed = now - float(calibration.get("started", now))
        lead_in = float(calibration.get("lead_in", 3.0))
        open_end = lead_in + float(calibration.get("open_seconds", 2.0))
        pinched_end = open_end + float(calibration.get("pinched_seconds", 2.0))
        release_end = pinched_end + float(calibration.get("release_seconds", 1.5))

        if elapsed < lead_in:
            phase = "lead_in"
        elif elapsed < open_end:
            phase = "open"
        elif elapsed < pinched_end:
            phase = "pinched"
        elif elapsed < release_end:
            phase = "release"
        else:
            self.complete_pinch_calibration()
            return False

        if phase != calibration.get("last_phase", ""):
            calibration["last_phase"] = phase
            self.write_log(f"VisionPinchCalibrationPhase phase={phase}")
            cue_elapsed = self.play_pinch_calibration_cue(phase)
            if cue_elapsed is not None and cue_elapsed > 0.0:
                calibration["started"] = float(calibration.get("started", now)) + cue_elapsed

        if phase in ("open", "pinched", "release") and landmarks is not None:
            confidence_value = self.valid_calibration_confidence(confidence, phase)
            if confidence_value is not None and confidence_value >= 0.35:
                calibration[phase].append(self.pinch_ratio(landmarks))

        return True

    def valid_calibration_confidence(self, confidence, phase):
        if confidence is None:
            self.write_log(f"VisionPinchCalibrationSampleSkipped phase={phase} reason=missing_confidence")
            return None
        try:
            confidence_value = float(confidence)
        except Exception:  # noqa: BLE001
            self.write_log(
                "VisionPinchCalibrationSampleSkipped "
                f"phase={phase} reason=invalid_confidence value={safe_preview(confidence, 80)}")
            return None
        if math.isnan(confidence_value):
            self.write_log(f"VisionPinchCalibrationSampleSkipped phase={phase} reason=nan_confidence")
            return None
        return confidence_value

    def update_motion_region_calibration(self, landmarks, confidence):
        calibration = self.motion_region_calibration
        if not calibration:
            return False

        now = time.monotonic()
        elapsed = now - float(calibration.get("started", now))
        lead_in = float(calibration.get("lead_in", 1.0))
        corner_seconds = float(calibration.get("corner_seconds", 2.0))
        top_left_end = lead_in + corner_seconds
        top_right_end = top_left_end + corner_seconds
        bottom_right_end = top_right_end + corner_seconds
        bottom_left_end = bottom_right_end + corner_seconds

        if elapsed < lead_in:
            phase = "lead_in"
        elif elapsed < top_left_end:
            phase = "top_left"
        elif elapsed < top_right_end:
            phase = "top_right"
        elif elapsed < bottom_right_end:
            phase = "bottom_right"
        elif elapsed < bottom_left_end:
            phase = "bottom_left"
        else:
            self.complete_motion_region_calibration()
            return False

        if phase != calibration.get("last_phase", ""):
            calibration["last_phase"] = phase
            self.write_log(f"VisionMotionRegionCalibrationPhase phase={phase}")
            cue_elapsed = self.play_motion_region_calibration_cue(phase)
            if cue_elapsed is not None and cue_elapsed > 0.0:
                calibration["started"] = float(calibration.get("started", now)) + cue_elapsed

        if phase in ("top_left", "top_right", "bottom_right", "bottom_left") and landmarks is not None:
            confidence_value = self.valid_motion_region_calibration_confidence(confidence, phase)
            if confidence_value is not None and confidence_value >= 0.35:
                index_tip = landmarks[8]
                calibration[phase].append((clamp01(index_tip.x), clamp01(index_tip.y)))

        return True

    def valid_motion_region_calibration_confidence(self, confidence, phase):
        if confidence is None:
            self.write_log(f"VisionMotionRegionCalibrationSampleSkipped phase={phase} reason=missing_confidence")
            return None
        try:
            confidence_value = float(confidence)
        except Exception:  # noqa: BLE001
            self.write_log(
                "VisionMotionRegionCalibrationSampleSkipped "
                f"phase={phase} reason=invalid_confidence value={safe_preview(confidence, 80)}")
            return None
        if math.isnan(confidence_value):
            self.write_log(f"VisionMotionRegionCalibrationSampleSkipped phase={phase} reason=nan_confidence")
            return None
        return confidence_value

    def complete_motion_region_calibration(self):
        calibration = self.motion_region_calibration
        self.motion_region_calibration = None
        if not calibration:
            return

        top_left_samples = list(calibration.get("top_left", []))
        top_right_samples = list(calibration.get("top_right", []))
        bottom_right_samples = list(calibration.get("bottom_right", []))
        bottom_left_samples = list(calibration.get("bottom_left", []))
        sample_counts = {
            "top_left": len(top_left_samples),
            "top_right": len(top_right_samples),
            "bottom_right": len(bottom_right_samples),
            "bottom_left": len(bottom_left_samples),
        }
        if any(count < 5 for count in sample_counts.values()):
            self.write({
                "type": "vision.motion_region_calibration_completed",
                "status": "failed",
                "message": (
                    "Not enough motion region samples. "
                    f"top_left={sample_counts['top_left']} top_right={sample_counts['top_right']} "
                    f"bottom_right={sample_counts['bottom_right']} bottom_left={sample_counts['bottom_left']}"
                ),
                "topLeftSamples": sample_counts["top_left"],
                "topRightSamples": sample_counts["top_right"],
                "bottomRightSamples": sample_counts["bottom_right"],
                "bottomLeftSamples": sample_counts["bottom_left"],
            })
            return

        left_values = [point[0] for point in top_left_samples + bottom_left_samples]
        right_values = [point[0] for point in top_right_samples + bottom_right_samples]
        top_values = [point[1] for point in top_left_samples + top_right_samples]
        bottom_values = [point[1] for point in bottom_left_samples + bottom_right_samples]
        padding = float(calibration.get("padding", 0.04) or 0.04)
        left = clamp01(percentile(left_values, 20) - padding)
        right = clamp01(percentile(right_values, 80) + padding)
        top = clamp01(percentile(top_values, 20) - padding)
        bottom = clamp01(percentile(bottom_values, 80) + padding)

        if right - left < 0.15 or bottom - top < 0.15:
            self.write({
                "type": "vision.motion_region_calibration_completed",
                "status": "failed",
                "message": f"Motion region was too small. width={right - left:.3f} height={bottom - top:.3f}",
                "topLeftSamples": sample_counts["top_left"],
                "topRightSamples": sample_counts["top_right"],
                "bottomRightSamples": sample_counts["bottom_right"],
                "bottomLeftSamples": sample_counts["bottom_left"],
            })
            return

        self.config["controlRegionLeft"] = left
        self.config["controlRegionTop"] = top
        self.config["controlRegionRight"] = right
        self.config["controlRegionBottom"] = bottom

        calibration_path = str(calibration.get("calibration_path", "") or "").strip()
        if calibration_path:
            self.save_motion_region_calibration(
                calibration_path,
                left,
                top,
                right,
                bottom,
                sample_counts,
                top_left_samples,
                top_right_samples,
                bottom_right_samples,
                bottom_left_samples,
                padding)

        self.write_log(
            "VisionMotionRegionCalibrationRegionApplied "
            f"left={left:.4f} top={top:.4f} right={right:.4f} bottom={bottom:.4f} "
            f"topLeftSamples={sample_counts['top_left']} topRightSamples={sample_counts['top_right']} "
            f"bottomRightSamples={sample_counts['bottom_right']} bottomLeftSamples={sample_counts['bottom_left']}")
        self.write({
            "type": "vision.motion_region_calibration_completed",
            "status": "success",
            "message": "Motion region calibration saved.",
            "controlRegionLeft": left,
            "controlRegionTop": top,
            "controlRegionRight": right,
            "controlRegionBottom": bottom,
            "topLeftSamples": sample_counts["top_left"],
            "topRightSamples": sample_counts["top_right"],
            "bottomRightSamples": sample_counts["bottom_right"],
            "bottomLeftSamples": sample_counts["bottom_left"],
            "calibrationPath": calibration_path,
        })

    def fail_motion_region_calibration(self, message):
        if not self.motion_region_calibration:
            return
        calibration = self.motion_region_calibration
        self.motion_region_calibration = None
        self.write_log(f"VisionMotionRegionCalibrationFailedGracefully message={message}")
        self.write({
            "type": "vision.motion_region_calibration_completed",
            "status": "failed",
            "message": message,
            "topLeftSamples": len(calibration.get("top_left", [])),
            "topRightSamples": len(calibration.get("top_right", [])),
            "bottomRightSamples": len(calibration.get("bottom_right", [])),
            "bottomLeftSamples": len(calibration.get("bottom_left", [])),
        })

    def complete_pinch_calibration(self):
        calibration = self.pinch_calibration
        self.pinch_calibration = None
        if not calibration:
            return

        open_samples = list(calibration.get("open", []))
        pinch_samples = list(calibration.get("pinched", []))
        release_samples = list(calibration.get("release", []))
        not_pinched_samples = open_samples + release_samples
        if len(open_samples) < 8 or len(pinch_samples) < 8 or len(release_samples) < 5:
            self.write({
                "type": "vision.pinch_calibration_completed",
                "status": "failed",
                "message": (
                    "Not enough calibration samples. "
                    f"open={len(open_samples)} pinched={len(pinch_samples)} release={len(release_samples)}"
                ),
                "openSamples": len(open_samples),
                "pinchSamples": len(pinch_samples),
                "releaseSamples": len(release_samples),
            })
            return

        open_floor = percentile(not_pinched_samples, 20)
        pinch_ceiling = percentile(pinch_samples, 80)
        gap = open_floor - pinch_ceiling
        if gap < 0.03:
            self.write({
                "type": "vision.pinch_calibration_completed",
                "status": "failed",
                "message": f"Open and pinched samples were too close together. gap={gap:.3f}",
                "openSamples": len(open_samples),
                "pinchSamples": len(pinch_samples),
                "releaseSamples": len(release_samples),
            })
            return

        start_ratio = clamp(pinch_ceiling + gap * 0.25, 0.03, 2.0)
        hold_ratio = clamp(pinch_ceiling + gap * 0.40, start_ratio + 0.01, 2.0)
        release_ratio = clamp(pinch_ceiling + gap * 0.70, hold_ratio + 0.01, 2.0)
        self.config["pinchStartRatio"] = start_ratio
        self.config["pinchHoldRatio"] = hold_ratio
        self.config["pinchReleaseRatio"] = release_ratio

        calibration_path = str(calibration.get("calibration_path", "") or "").strip()
        if calibration_path:
            self.save_pinch_calibration(
                calibration_path,
                start_ratio,
                hold_ratio,
                release_ratio,
                open_samples,
                pinch_samples,
                release_samples)

        self.write_log(
            "VisionPinchCalibrationThresholdsApplied "
            f"start={start_ratio:.4f} hold={hold_ratio:.4f} release={release_ratio:.4f} "
            f"openMedian={median(open_samples):.4f} pinchedMedian={median(pinch_samples):.4f} "
            f"releaseMedian={median(release_samples):.4f} openFloor={open_floor:.4f} "
            f"pinchCeiling={pinch_ceiling:.4f} gap={gap:.4f}")
        self.write({
            "type": "vision.pinch_calibration_completed",
            "status": "success",
            "message": "Pinch calibration saved.",
            "pinchStartRatio": start_ratio,
            "pinchHoldRatio": hold_ratio,
            "pinchReleaseRatio": release_ratio,
            "openSamples": len(open_samples),
            "pinchSamples": len(pinch_samples),
            "releaseSamples": len(release_samples),
            "calibrationPath": calibration_path,
        })

    def fail_pinch_calibration(self, message):
        if not self.pinch_calibration:
            return
        calibration = self.pinch_calibration
        self.pinch_calibration = None
        self.write_log(f"VisionPinchCalibrationFailedGracefully message={message}")
        self.write({
            "type": "vision.pinch_calibration_completed",
            "status": "failed",
            "message": message,
            "openSamples": len(calibration.get("open", [])),
            "pinchSamples": len(calibration.get("pinched", [])),
            "releaseSamples": len(calibration.get("release", [])),
        })

    def save_pinch_calibration(self, calibration_path, start_ratio, hold_ratio, release_ratio, open_samples, pinch_samples, release_samples):
        try:
            directory = os.path.dirname(calibration_path)
            if directory:
                os.makedirs(directory, exist_ok=True)
            payload = {
                "createdAtUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                "pinchStartRatio": start_ratio,
                "pinchHoldRatio": hold_ratio,
                "pinchReleaseRatio": release_ratio,
                "sampleCounts": {
                    "open": len(open_samples),
                    "pinched": len(pinch_samples),
                    "release": len(release_samples),
                },
                "diagnostics": {
                    "openMedian": median(open_samples),
                    "pinchedMedian": median(pinch_samples),
                    "releaseMedian": median(release_samples),
                    "openP20": percentile(open_samples, 20),
                    "pinchedP80": percentile(pinch_samples, 80),
                    "releaseP20": percentile(release_samples, 20),
                },
            }
            with open(calibration_path, "w", encoding="utf-8") as file:
                json.dump(payload, file, indent=2, sort_keys=True)
            self.write_log(f"VisionPinchCalibrationSaved path={calibration_path!r}")
        except Exception as exc:  # noqa: BLE001
            self.write_log(f"VisionPinchCalibrationSaveFailed path={calibration_path!r} error={exc}")

    def save_motion_region_calibration(
            self,
            calibration_path,
            left,
            top,
            right,
            bottom,
            sample_counts,
            top_left_samples,
            top_right_samples,
            bottom_right_samples,
            bottom_left_samples,
            padding):
        try:
            directory = os.path.dirname(calibration_path)
            if directory:
                os.makedirs(directory, exist_ok=True)
            payload = {
                "createdAtUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                "controlRegionLeft": left,
                "controlRegionTop": top,
                "controlRegionRight": right,
                "controlRegionBottom": bottom,
                "padding": padding,
                "sampleCounts": sample_counts,
                "diagnostics": {
                    "topLeftMedian": [median([point[0] for point in top_left_samples]), median([point[1] for point in top_left_samples])],
                    "topRightMedian": [median([point[0] for point in top_right_samples]), median([point[1] for point in top_right_samples])],
                    "bottomRightMedian": [median([point[0] for point in bottom_right_samples]), median([point[1] for point in bottom_right_samples])],
                    "bottomLeftMedian": [median([point[0] for point in bottom_left_samples]), median([point[1] for point in bottom_left_samples])],
                },
            }
            with open(calibration_path, "w", encoding="utf-8") as file:
                json.dump(payload, file, indent=2, sort_keys=True)
            self.write_log(f"VisionMotionRegionCalibrationSaved path={calibration_path!r}")
        except Exception as exc:  # noqa: BLE001
            self.write_log(f"VisionMotionRegionCalibrationSaveFailed path={calibration_path!r} error={exc}")

    def load_pinch_calibration_override(self):
        calibration_path = str(self.config.get("pinchCalibrationPath", "") or "").strip()
        if not calibration_path or not os.path.isfile(calibration_path):
            return

        try:
            with open(calibration_path, "r", encoding="utf-8") as file:
                payload = json.load(file)
            start_ratio = float(payload.get("pinchStartRatio", 0.0) or 0.0)
            hold_ratio = float(payload.get("pinchHoldRatio", 0.0) or 0.0)
            release_ratio = float(payload.get("pinchReleaseRatio", 0.0) or 0.0)
            if start_ratio <= 0.0 or hold_ratio <= start_ratio or release_ratio <= hold_ratio:
                self.write_log(f"VisionPinchCalibrationIgnored path={calibration_path!r} reason=invalid_threshold_order")
                return
            self.config["pinchStartRatio"] = start_ratio
            self.config["pinchHoldRatio"] = hold_ratio
            self.config["pinchReleaseRatio"] = release_ratio
            self.write_log(
                "VisionPinchCalibrationLoaded "
                f"path={calibration_path!r} start={start_ratio:.4f} hold={hold_ratio:.4f} release={release_ratio:.4f}")
        except Exception as exc:  # noqa: BLE001
            self.write_log(f"VisionPinchCalibrationLoadFailed path={calibration_path!r} error={exc}")

    def load_motion_region_calibration_override(self):
        calibration_path = str(self.config.get("motionRegionCalibrationPath", "") or "").strip()
        if not calibration_path or not os.path.isfile(calibration_path):
            return

        try:
            with open(calibration_path, "r", encoding="utf-8") as file:
                payload = json.load(file)
            left = clamp01(float(payload.get("controlRegionLeft", 0.0) or 0.0))
            top = clamp01(float(payload.get("controlRegionTop", 0.0) or 0.0))
            right = clamp01(float(payload.get("controlRegionRight", 1.0) or 1.0))
            bottom = clamp01(float(payload.get("controlRegionBottom", 1.0) or 1.0))
            if right - left < 0.15 or bottom - top < 0.15:
                self.write_log(f"VisionMotionRegionCalibrationIgnored path={calibration_path!r} reason=invalid_region")
                return
            self.config["controlRegionLeft"] = left
            self.config["controlRegionTop"] = top
            self.config["controlRegionRight"] = right
            self.config["controlRegionBottom"] = bottom
            self.write_log(
                "VisionMotionRegionCalibrationLoaded "
                f"path={calibration_path!r} left={left:.4f} top={top:.4f} right={right:.4f} bottom={bottom:.4f}")
        except Exception as exc:  # noqa: BLE001
            self.write_log(f"VisionMotionRegionCalibrationLoadFailed path={calibration_path!r} error={exc}")

    def play_pinch_calibration_cue(self, phase):
        start = time.monotonic()
        if phase == "open":
            beep_pattern = [880]
        elif phase == "pinched":
            beep_pattern = [980, 980]
        elif phase == "release":
            beep_pattern = [760, 760, 760]
        else:
            return 0.0

        try:
            import winsound  # type: ignore
            for index, frequency in enumerate(beep_pattern):
                winsound.Beep(frequency, 120)
                if index < len(beep_pattern) - 1:
                    time.sleep(0.35)
            pause_seconds = float(self.pinch_calibration.get("phase_pause_seconds", 1.0) or 1.0) if self.pinch_calibration else 1.0
            if pause_seconds > 0.0:
                time.sleep(pause_seconds)
        except Exception:
            pass
        return time.monotonic() - start

    def play_motion_region_calibration_cue(self, phase):
        start = time.monotonic()
        if phase == "top_left":
            beep_pattern = [880]
        elif phase == "top_right":
            beep_pattern = [960, 960]
        elif phase == "bottom_right":
            beep_pattern = [1040, 1040, 1040]
        elif phase == "bottom_left":
            beep_pattern = [760, 760, 760, 760]
        else:
            return 0.0

        try:
            import winsound  # type: ignore
            for index, frequency in enumerate(beep_pattern):
                winsound.Beep(frequency, 120)
                if index < len(beep_pattern) - 1:
                    time.sleep(0.25)
            pause_seconds = (
                float(self.motion_region_calibration.get("phase_pause_seconds", 1.0) or 1.0)
                if self.motion_region_calibration else 1.0)
            if pause_seconds > 0.0:
                time.sleep(pause_seconds)
        except Exception:
            pass
        return time.monotonic() - start

    def update_pinch(self, pointer_id, landmarks, x, y, confidence):
        ratio = self.pinch_ratio(landmarks)
        start_ratio = float(self.config.get("pinchStartRatio", 0.25) or 0.25)
        hold_ratio = float(self.config.get("pinchHoldRatio", 0.32) or 0.32)
        release_ratio = float(self.config.get("pinchReleaseRatio", 0.40) or 0.40)
        debounce = float(self.config.get("pinchDebounceMs", 150) or 150) / 1000.0
        now = time.monotonic()
        is_pinched = bool(self.pinched_by_pointer.get(pointer_id, False))

        if not is_pinched:
            if ratio < start_ratio:
                candidate_since = self.pinch_candidate_since_by_pointer.get(pointer_id)
                if candidate_since is None:
                    self.pinch_candidate_since_by_pointer[pointer_id] = now
                elif now - candidate_since >= debounce:
                    self.pinched_by_pointer[pointer_id] = True
                    self.write({
                        "type": "gesture.pinch.start",
                        "pointerId": pointer_id,
                        "x": x,
                        "y": y,
                        "confidence": confidence,
                        "source": "webcam",
                        "handedness": self.handedness_for_pointer(pointer_id),
                    })
            else:
                self.pinch_candidate_since_by_pointer.pop(pointer_id, None)
            return

        if ratio > release_ratio:
            self.pinched_by_pointer[pointer_id] = False
            self.pinch_candidate_since_by_pointer.pop(pointer_id, None)
            self.write({"type": "gesture.pinch.end", "pointerId": pointer_id, "source": "webcam"})
        elif ratio < hold_ratio:
            self.write({
                "type": "gesture.pinch.move",
                "pointerId": pointer_id,
                "x": x,
                "y": y,
                "confidence": confidence,
                "source": "webcam",
                "handedness": self.handedness_for_pointer(pointer_id),
            })

    def release_missing_pinches(self, visible_pointer_ids):
        for pointer_id, is_pinched in list(self.pinched_by_pointer.items()):
            if is_pinched and pointer_id not in visible_pointer_ids:
                self.pinched_by_pointer[pointer_id] = False
                self.write({"type": "gesture.pinch.end", "pointerId": pointer_id, "source": "webcam"})
        for pointer_id in list(self.pinch_candidate_since_by_pointer.keys()):
            if pointer_id not in visible_pointer_ids:
                self.pinch_candidate_since_by_pointer.pop(pointer_id, None)

    def release_all_pinches(self):
        for pointer_id, is_pinched in list(self.pinched_by_pointer.items()):
            if is_pinched:
                self.write({"type": "gesture.pinch.end", "pointerId": pointer_id, "source": "webcam"})
        self.pinched_by_pointer.clear()
        self.pinch_candidate_since_by_pointer.clear()

    def handedness_for_pointer(self, pointer_id):
        return ""

    def pinch_ratio(self, landmarks):
        thumb = landmarks[4]
        index = landmarks[8]
        wrist = landmarks[0]
        middle_mcp = landmarks[9]
        pinch_distance = distance(thumb.x, thumb.y, index.x, index.y)
        hand_scale = max(distance(wrist.x, wrist.y, middle_mcp.x, middle_mcp.y), 0.0001)
        return pinch_distance / hand_scale

    def release_camera(self):
        if self.capture is not None:
            self.capture.release()
            self.capture = None
        if self.cv2 is not None:
            try:
                self.cv2.destroyAllWindows()
            except Exception:
                pass

    def close_landmarker(self):
        if self.landmarker is not None:
            try:
                self.landmarker.close()
            except Exception:
                pass
            self.landmarker = None

    def write(self, payload):
        sys.stdout.write(json.dumps(payload, separators=(",", ":")) + "\n")
        sys.stdout.flush()

    def write_log(self, message):
        sys.stdout.write(str(message) + "\n")
        sys.stdout.flush()

    def write_error(self, code, message=None):
        payload = {"type": "vision.error", "error": code, "code": code}
        if message is not None:
            payload["message"] = message
        self.write(payload)


def clamp01(value):
    return max(0.0, min(1.0, float(value)))


def clamp(value, minimum, maximum):
    return max(minimum, min(maximum, float(value)))


def median(values):
    return percentile(values, 50)


def percentile(values, percent):
    samples = sorted(float(value) for value in values)
    if not samples:
        return 0.0
    if len(samples) == 1:
        return samples[0]
    position = (len(samples) - 1) * clamp(percent, 0.0, 100.0) / 100.0
    lower = int(math.floor(position))
    upper = int(math.ceil(position))
    if lower == upper:
        return samples[lower]
    weight = position - lower
    return samples[lower] * (1.0 - weight) + samples[upper] * weight


def distance(ax, ay, bx, by):
    return math.sqrt((ax - bx) ** 2 + (ay - by) ** 2)


def elapsed_ms(start):
    return (time.perf_counter() - start) * 1000.0


def normalize_backend_name(value):
    normalized = str(value or "Auto").strip().upper()
    if normalized in {"AUTO", "MSMF", "DSHOW", "DEFAULT"}:
        return normalized
    return "AUTO"


def normalize_profile_name(value):
    normalized = str(value or "Auto").strip().upper()
    if normalized in {
        "AUTO",
        "DSHOW_MJPG_CONSTRUCTOR",
        "DSHOW_MJPG_SET_BEFORE_AFTER",
        "DSHOW_DEFAULT",
        "MSMF_DEFAULT",
        "DEFAULT",
    }:
        return normalized
    return "AUTO"


def target_fps_is_realtime(value):
    try:
        return float(value) >= 24.0
    except Exception:
        return False


def fourcc_text(value):
    try:
        code = int(value or 0)
    except Exception:
        return "unknown"
    chars = []
    for index in range(4):
        char_code = (code >> (8 * index)) & 0xFF
        chars.append(chr(char_code) if 32 <= char_code <= 126 else "?")
    text = "".join(chars)
    return text if text.strip("?") else str(code)


def is_black_frame(frame):
    try:
        return float(frame.mean()) < 2.0
    except Exception:
        return False


def safe_preview(value, limit=500):
    preview = repr(value)
    if len(preview) <= limit:
        return preview
    return preview[:limit] + "...<truncated>"


def list_directshow_video_devices():
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        return []

    try:
        completed = subprocess.run(
            [ffmpeg, "-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"],
            capture_output=True,
            text=True,
            timeout=5,
            check=False)
    except Exception:
        return []

    devices = []
    in_video_section = False
    for line in (completed.stderr or "").splitlines():
        if "DirectShow video devices" in line:
            in_video_section = True
            continue
        if "DirectShow audio devices" in line:
            in_video_section = False
            continue
        if not in_video_section or "Alternative name" in line:
            continue

        match = re.search(r'"([^"]+)"', line)
        if match:
            devices.append(match.group(1))

    return devices


def normalize_device_name(value):
    return re.sub(r"[^a-z0-9]+", "", str(value or "").lower())


if __name__ == "__main__":
    VisionWorker().run()
