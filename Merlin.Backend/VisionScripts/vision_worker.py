import json
import math
import os
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
        self.write({"type": "vision.tracking_stopped"})

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
            self.landmarker = self.create_landmarker(model_path)
        except Exception as exc:  # noqa: BLE001
            self.write_error("MODEL_LOAD_FAILED", str(exc))
            return

        camera_index = int(self.config.get("cameraIndex", 0) or 0)
        width = int(self.config.get("width", 1280) or 1280)
        height = int(self.config.get("height", 720) or 720)
        fps = int(self.config.get("fps", 30) or 30)
        self.capture = cv2.VideoCapture(camera_index)
        if not self.capture.isOpened():
            self.write_error("CAMERA_OPEN_FAILED", str(camera_index))
            self.release_camera()
            self.close_landmarker()
            return

        self.capture.set(cv2.CAP_PROP_FRAME_WIDTH, width)
        self.capture.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
        self.capture.set(cv2.CAP_PROP_FPS, fps)
        actual_width = int(self.capture.get(cv2.CAP_PROP_FRAME_WIDTH))
        actual_height = int(self.capture.get(cv2.CAP_PROP_FRAME_HEIGHT))
        actual_fps = float(self.capture.get(cv2.CAP_PROP_FPS) or fps)
        self.write({
            "type": "vision.tracking_started",
            "cameraName": f"camera:{camera_index}",
            "actualWidth": actual_width,
            "actualHeight": actual_height,
            "actualFps": actual_fps,
        })

        while not self.stop_requested.is_set():
            try:
                ok, frame = self.capture.read()
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
        return clamp01(index_tip.x), clamp01(index_tip.y)

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

    def write_error(self, code, message=None):
        payload = {"type": "vision.error", "error": code, "code": code}
        if message is not None:
            payload["message"] = message
        self.write(payload)


def clamp01(value):
    return max(0.0, min(1.0, float(value)))


def distance(ax, ay, bx, by):
    return math.sqrt((ax - bx) ** 2 + (ay - by) ** 2)


def safe_preview(value, limit=500):
    preview = repr(value)
    if len(preview) <= limit:
        return preview
    return preview[:limit] + "...<truncated>"


if __name__ == "__main__":
    VisionWorker().run()
