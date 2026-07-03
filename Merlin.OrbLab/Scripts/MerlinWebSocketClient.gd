extends Node
class_name MerlinWebSocketClient

signal connection_state_changed(state: String, detail: String)
signal visual_state_received(state: Dictionary)
signal response_received(response: Dictionary)
signal assistant_ui_state_received(state: Dictionary)
signal voice_transcript_received(transcript: Dictionary)
signal visual_event_received(event: Dictionary)
signal barge_in_debug_snapshot_received(snapshot: Dictionary)
signal malformed_response(raw_message: String, detail: String)
signal socket_closed(code: int, reason: String)
signal frontend_work_observed(metrics: Dictionary)

const DEFAULT_URL := "ws://localhost:5000/ws"
const MAX_BACKEND_MESSAGES_PER_FRAME := 3
const MAX_BACKEND_BYTES_PER_FRAME := 65536
const MAX_BACKEND_PARSE_MS_PER_FRAME := 2.0
const MAX_BACKEND_SIGNAL_MS_PER_FRAME := 2.0
const WEBSOCKET_PERF_WARN_MS := 2.0
const VISUAL_PACKET_MAX_BYTES := 4096
const MALFORMED_RAW_PREVIEW_CHARS := 2048
const FAKE_STRESS_PAYLOAD_BYTES := 128000
const FAKE_STRESS_PAYLOAD_INTERVAL := 0.05
const FRONTEND_VOICE_STREAM_ENABLED := false

var url := DEFAULT_URL
var _socket := WebSocketPeer.new()
var _state := "disconnected"
var _last_peer_state := WebSocketPeer.STATE_CLOSED
var _payload_parse_thread := Thread.new()
var _payload_parse_mutex := Mutex.new()
var _payload_parse_semaphore := Semaphore.new()
var _payload_parse_queue: Array = []
var _payload_parsed_queue: Array = []
var _payload_worker_running := false
var _backend_stress_fake_mode := false
var _fake_stress_payload := ""
var _fake_stress_timer := 0.0
var _fake_stress_sequence := 0
var last_assistant_ui_state_sequence := -1


func _ready() -> void:
	_start_payload_worker()
	set_process(false)


func _exit_tree() -> void:
	_stop_payload_worker()


func connect_to_backend(target_url: String = DEFAULT_URL) -> void:
	url = target_url
	_socket = WebSocketPeer.new()
	_last_peer_state = WebSocketPeer.STATE_CONNECTING

	var error := _socket.connect_to_url(url)
	if error != OK:
		_set_state("error", "Could not start WebSocket connection. Error code: %s" % error)
		set_process(_backend_stress_fake_mode)
		return

	_set_state("connecting", "Connecting to %s" % url)
	set_process(true)


func disconnect_from_backend() -> void:
	if _socket.get_ready_state() == WebSocketPeer.STATE_OPEN:
		_socket.close(1000, "Client disconnecting.")

	_set_state("disconnected", "Disconnected.")
	set_process(_backend_stress_fake_mode)


func send_message(
	message: String,
	correlation_id: String,
	interaction_source: String = "unknown",
	client_mode: String = "api"
) -> bool:
	if _socket.get_ready_state() != WebSocketPeer.STATE_OPEN:
		_set_state("error", "Cannot send message because Merlin.Backend is not connected.")
		return false

	var payload := {
		"message": message,
		"correlationId": correlation_id,
		"interactionSource": interaction_source,
		"clientMode": client_mode
	}

	var json := JSON.stringify(payload)
	var error := _socket.send_text(json)
	if error != OK:
		_set_state("error", "Failed to send WebSocket message. Error code: %s" % error)
		return false

	return true


func send_voice_stream_start(correlation_id: String, sample_rate: int, channels: int, client_mode: String = "orb") -> bool:
	if not FRONTEND_VOICE_STREAM_ENABLED:
		print("VoiceStreamStartSuppressedBackendOwnedMode")
		return false
	return _send_json({
		"type": "voice_stream_start",
		"correlationId": correlation_id,
		"sampleRate": sample_rate,
		"channels": channels,
		"format": "pcm_s16le",
		"clientMode": client_mode
	})


func send_voice_stream_chunk(correlation_id: String, pcm_bytes: PackedByteArray) -> bool:
	if not FRONTEND_VOICE_STREAM_ENABLED:
		print("VoiceStreamChunkSuppressedBackendOwnedMode")
		return false
	if pcm_bytes.is_empty():
		return true
	return _send_json({
		"type": "voice_stream_chunk",
		"correlationId": correlation_id,
		"data": Marshalls.raw_to_base64(pcm_bytes)
	})


func send_voice_stream_end(correlation_id: String, client_mode: String = "orb") -> bool:
	if not FRONTEND_VOICE_STREAM_ENABLED:
		print("VoiceStreamEndSuppressedBackendOwnedMode")
		return false
	return _send_json({
		"type": "voice_stream_end",
		"correlationId": correlation_id,
		"clientMode": client_mode
	})


func send_voice_stream_cancel(correlation_id: String) -> bool:
	if not FRONTEND_VOICE_STREAM_ENABLED:
		print("VoiceStreamEndSuppressedBackendOwnedMode")
		return false
	return _send_json({
		"type": "voice_stream_cancel",
		"correlationId": correlation_id
	})


func send_speech_presence_marker(marker_type: String = "user_started_speaking") -> bool:
	return _send_json({
		"type": "speech_presence_marker",
		"markerType": marker_type,
		"clientTimestampUtc": _utc_timestamp_string(),
		"source": "frontend_debug_button",
	})


func _send_json(payload: Dictionary) -> bool:
	if _socket.get_ready_state() != WebSocketPeer.STATE_OPEN:
		_set_state("error", "Cannot send message because Merlin.Backend is not connected.")
		return false

	var error := _socket.send_text(JSON.stringify(payload))
	if error != OK:
		_set_state("error", "Failed to send WebSocket message. Error code: %s" % error)
		return false

	return true


func _utc_timestamp_string() -> String:
	var timestamp := Time.get_datetime_string_from_system(true)
	if not timestamp.ends_with("Z"):
		timestamp += "Z"
	return timestamp


func is_backend_connected() -> bool:
	return _socket.get_ready_state() == WebSocketPeer.STATE_OPEN


func get_connection_state() -> String:
	return _state


func set_backend_stress_fake_mode(enabled: bool) -> void:
	_backend_stress_fake_mode = enabled
	if enabled:
		if _fake_stress_payload.is_empty():
			_fake_stress_payload = _build_fake_stress_payload(FAKE_STRESS_PAYLOAD_BYTES)
		_fake_stress_timer = 0.0
		print("Backend stress fake mode enabled. PayloadBytes=%s" % _fake_stress_payload.to_utf8_buffer().size())
		set_process(true)
	else:
		print("Backend stress fake mode disabled.")
		if _socket.get_ready_state() == WebSocketPeer.STATE_CLOSED:
			set_process(false)


func is_backend_stress_fake_mode_enabled() -> bool:
	return _backend_stress_fake_mode


func _process(delta: float) -> void:
	var frame_started_usec := Time.get_ticks_usec()
	var metrics := {
		"source": "websocket",
		"http_polled": false,
		"bytes": 0,
		"json_parse_count": 0,
		"json_parse_ms": 0.0,
		"packets": 0,
		"work_ms": 0.0,
		"poll_ms": 0.0,
		"read_ms": 0.0,
		"signal_ms": 0.0,
		"deferred_packets": 0,
		"queued_payloads": 0,
		"parsed_payloads": 0,
	}

	var poll_started_usec := Time.get_ticks_usec()
	_socket.poll()
	metrics["poll_ms"] = _elapsed_ms_since(poll_started_usec)
	_warn_slow("poll", float(metrics["poll_ms"]))

	var peer_state := _socket.get_ready_state()
	if peer_state != _last_peer_state:
		_handle_peer_state_changed(peer_state)
		_last_peer_state = peer_state

	if _backend_stress_fake_mode:
		_produce_fake_backend_stress(delta, metrics)

	_read_available_packets(metrics)
	_drain_parsed_payloads(metrics)

	if peer_state == WebSocketPeer.STATE_CLOSED and not _backend_stress_fake_mode and _payload_pending_count() == 0:
		set_process(false)

	metrics["work_ms"] = _elapsed_ms_since(frame_started_usec)
	if int(metrics["packets"]) > 0 or int(metrics["parsed_payloads"]) > 0 or float(metrics["work_ms"]) >= 1.0:
		frontend_work_observed.emit(metrics)


func _read_available_packets(metrics: Dictionary) -> void:
	var packets_processed := 0
	var bytes_processed := 0
	var parse_ms := 0.0

	while _socket.get_available_packet_count() > 0:
		if packets_processed >= MAX_BACKEND_MESSAGES_PER_FRAME:
			metrics["deferred_packets"] = _socket.get_available_packet_count()
			break
		if bytes_processed >= MAX_BACKEND_BYTES_PER_FRAME:
			metrics["deferred_packets"] = _socket.get_available_packet_count()
			break
		if parse_ms >= MAX_BACKEND_PARSE_MS_PER_FRAME:
			metrics["deferred_packets"] = _socket.get_available_packet_count()
			break

		var read_started_usec := Time.get_ticks_usec()
		var packet := _socket.get_packet()
		var packet_size := packet.size()
		var read_ms := _elapsed_ms_since(read_started_usec)
		metrics["read_ms"] = float(metrics["read_ms"]) + read_ms
		_warn_slow("read", read_ms, packet_size)

		packets_processed += 1
		bytes_processed += packet_size
		if packet_size <= VISUAL_PACKET_MAX_BYTES:
			var decode_started_usec := Time.get_ticks_usec()
			var raw_message := packet.get_string_from_utf8()
			var decode_ms := _elapsed_ms_since(decode_started_usec)
			_warn_slow("decode", decode_ms, packet_size)
			_route_raw_message(raw_message, packet_size, metrics)
		else:
			_queue_payload_parse(packet, packet_size)
			metrics["queued_payloads"] = int(metrics["queued_payloads"]) + 1
		parse_ms = float(metrics["json_parse_ms"])

	metrics["packets"] = int(metrics["packets"]) + packets_processed
	metrics["bytes"] = int(metrics["bytes"]) + bytes_processed


func _route_raw_message(raw_message: String, packet_size: int, metrics: Dictionary) -> void:
	if _looks_like_control_packet(raw_message, packet_size):
		var parse_started_usec := Time.get_ticks_usec()
		var parsed = JSON.parse_string(raw_message)
		var parse_ms := _elapsed_ms_since(parse_started_usec)
		metrics["json_parse_count"] = int(metrics["json_parse_count"]) + 1
		metrics["json_parse_ms"] = float(metrics["json_parse_ms"]) + parse_ms
		_warn_slow("json_parse", parse_ms, packet_size)
		if typeof(parsed) != TYPE_DICTIONARY:
			_emit_timed("malformed_response", [raw_message.left(MALFORMED_RAW_PREVIEW_CHARS), "Response was not valid JSON object."], packet_size, metrics)
			return
		var packet: Dictionary = parsed
		if _is_visual_state_packet(packet):
			_emit_timed("visual_state_received", [_extract_visual_state(packet)], packet_size, metrics)
			return
		if String(packet.get("type", "")) == "assistant_ui_state":
			_route_assistant_ui_state_packet(packet, packet_size, metrics)
			return
		if String(packet.get("type", "")) == "voice_transcript":
			_emit_timed("voice_transcript_received", [packet], packet_size, metrics)
			return
		if String(packet.get("type", "")) == "voice_stream_ack":
			return
		if String(packet.get("type", "")) == "barge_in_debug_snapshot":
			_emit_timed("barge_in_debug_snapshot_received", [packet], packet_size, metrics)
			return
		if packet.has("event"):
			_emit_timed("visual_event_received", [packet], packet_size, metrics)
			return

	_queue_payload_parse(raw_message, packet_size)
	metrics["queued_payloads"] = int(metrics["queued_payloads"]) + 1


func _drain_parsed_payloads(metrics: Dictionary) -> void:
	var signal_ms := 0.0
	while _payload_parsed_count() > 0:
		if signal_ms >= MAX_BACKEND_SIGNAL_MS_PER_FRAME:
			break
		var item := _pop_parsed_payload()
		if item.is_empty():
			break
		metrics["json_parse_count"] = int(metrics["json_parse_count"]) + 1
		metrics["json_parse_ms"] = float(metrics["json_parse_ms"]) + float(item.get("parse_ms", 0.0))
		metrics["parsed_payloads"] = int(metrics["parsed_payloads"]) + 1
		_warn_slow("worker_decode", float(item.get("decode_ms", 0.0)), int(item.get("packet_size", 0)))
		_warn_slow("json_parse", float(item.get("parse_ms", 0.0)), int(item.get("packet_size", 0)))
		var emit_started_usec := Time.get_ticks_usec()
		var kind := String(item.get("kind", ""))
		if kind == "stress":
			continue
		if kind == "malformed":
			_emit_timed("malformed_response", [String(item.get("raw", "")), String(item.get("detail", ""))], int(item.get("packet_size", 0)), metrics)
		elif kind == "assistant_ui_state":
			var assistant_ui_state_payload = item.get("payload", {})
			if typeof(assistant_ui_state_payload) == TYPE_DICTIONARY:
				_route_assistant_ui_state_packet(assistant_ui_state_payload, int(item.get("packet_size", 0)), metrics)
		else:
			_emit_timed("response_received", [item.get("payload", {})], int(item.get("packet_size", 0)), metrics)
		signal_ms += _elapsed_ms_since(emit_started_usec)


func _emit_timed(signal_name: String, args: Array, packet_size: int, metrics: Dictionary) -> void:
	var started_usec := Time.get_ticks_usec()
	match signal_name:
		"visual_state_received":
			visual_state_received.emit(args[0])
		"visual_event_received":
			visual_event_received.emit(args[0])
		"response_received":
			response_received.emit(args[0])
		"assistant_ui_state_received":
			assistant_ui_state_received.emit(args[0])
		"voice_transcript_received":
			voice_transcript_received.emit(args[0])
		"barge_in_debug_snapshot_received":
			barge_in_debug_snapshot_received.emit(args[0])
		"malformed_response":
			malformed_response.emit(args[0], args[1])
		"socket_closed":
			socket_closed.emit(args[0], args[1])
		"connection_state_changed":
			connection_state_changed.emit(args[0], args[1])
	var elapsed_ms := _elapsed_ms_since(started_usec)
	metrics["signal_ms"] = float(metrics.get("signal_ms", 0.0)) + elapsed_ms
	if elapsed_ms >= WEBSOCKET_PERF_WARN_MS:
		print("WebSocketPerf slow signal_emit=%.2fms signal=%s packet_size=%s" % [elapsed_ms, signal_name, packet_size])


func _looks_like_control_packet(raw_message: String, packet_size: int) -> bool:
	if packet_size > VISUAL_PACKET_MAX_BYTES:
		return false
	if raw_message.find("\"type\":\"visual_state\"") >= 0:
		return true
	if raw_message.find("\"type\": \"visual_state\"") >= 0:
		return true
	if raw_message.find("\"type\":\"assistant_ui_state\"") >= 0:
		return true
	if raw_message.find("\"type\": \"assistant_ui_state\"") >= 0:
		return true
	if raw_message.find("\"type\":\"voice_transcript\"") >= 0:
		return true
	if raw_message.find("\"type\": \"voice_transcript\"") >= 0:
		return true
	if raw_message.find("\"type\":\"voice_stream_ack\"") >= 0:
		return true
	if raw_message.find("\"type\": \"voice_stream_ack\"") >= 0:
		return true
	if raw_message.find("\"type\":\"barge_in_debug_snapshot\"") >= 0:
		return true
	if raw_message.find("\"type\": \"barge_in_debug_snapshot\"") >= 0:
		return true
	if raw_message.find("\"event\"") >= 0:
		return true
	return false


func _is_visual_state_packet(packet: Dictionary) -> bool:
	return String(packet.get("type", "")) == "visual_state"


func _route_assistant_ui_state_packet(packet: Dictionary, packet_size: int, metrics: Dictionary) -> void:
	var sequence := int(packet.get("sequence", -1))
	if sequence <= last_assistant_ui_state_sequence:
		return
	last_assistant_ui_state_sequence = sequence
	_emit_timed("assistant_ui_state_received", [packet], packet_size, metrics)


func _extract_visual_state(packet: Dictionary) -> Dictionary:
	var allowed := [
		"mode",
		"energy",
		"speech_energy",
		"thinking_intensity",
		"error_intensity",
		"confirmation_intensity",
		"tool_intensity",
		"connection_state",
	]
	var state := {}
	for key in allowed:
		if packet.has(key):
			state[key] = packet[key]
	return state


func _handle_peer_state_changed(peer_state: int) -> void:
	match peer_state:
		WebSocketPeer.STATE_CONNECTING:
			_set_state("connecting", "Connecting to %s" % url)
		WebSocketPeer.STATE_OPEN:
			_set_state("connected", "Connected.")
		WebSocketPeer.STATE_CLOSING:
			_set_state("disconnected", "Connection closing.")
		WebSocketPeer.STATE_CLOSED:
			var code := _socket.get_close_code()
			var reason := _socket.get_close_reason()
			_emit_timed("socket_closed", [code, reason], 0, {})

			if _state == "connecting":
				_set_state("error", "Backend offline or connection refused.")
			else:
				_set_state("disconnected", "WebSocket closed. Code: %s Reason: %s" % [code, reason])


func _set_state(state: String, detail: String = "") -> void:
	_state = state
	_emit_timed("connection_state_changed", [state, detail], 0, {})


func _start_payload_worker() -> void:
	if _payload_worker_running:
		return
	_payload_worker_running = true
	_payload_parse_thread.start(Callable(self, "_payload_parse_worker"))


func _stop_payload_worker() -> void:
	if not _payload_worker_running:
		return
	_payload_parse_mutex.lock()
	_payload_worker_running = false
	_payload_parse_mutex.unlock()
	_payload_parse_semaphore.post()
	if _payload_parse_thread.is_started():
		_payload_parse_thread.wait_to_finish()


func _queue_payload_parse(raw_payload, packet_size: int) -> void:
	_payload_parse_mutex.lock()
	_payload_parse_queue.append({
		"raw": raw_payload,
		"packet_size": packet_size,
	})
	_payload_parse_mutex.unlock()
	_payload_parse_semaphore.post()


func _payload_parse_worker() -> void:
	while true:
		_payload_parse_semaphore.wait()
		_payload_parse_mutex.lock()
		var running := _payload_worker_running
		var item := {}
		if not _payload_parse_queue.is_empty():
			item = _payload_parse_queue.pop_front()
		_payload_parse_mutex.unlock()
		if not running and item.is_empty():
			return
		if item.is_empty():
			continue
		var raw_payload = item.get("raw", "")
		var packet_size := int(item.get("packet_size", 0))
		var decode_started_usec := Time.get_ticks_usec()
		var raw_message := ""
		if raw_payload is PackedByteArray:
			var raw_bytes: PackedByteArray = raw_payload
			raw_message = raw_bytes.get_string_from_utf8()
		else:
			raw_message = String(raw_payload)
		var decode_ms := _elapsed_ms_since(decode_started_usec)
		if packet_size <= 0:
			packet_size = raw_message.to_utf8_buffer().size()
		var parse_started_usec := Time.get_ticks_usec()
		var parsed = JSON.parse_string(raw_message)
		var parse_ms := _elapsed_ms_since(parse_started_usec)
		var result := {}
		if typeof(parsed) != TYPE_DICTIONARY:
			result = {
				"kind": "malformed",
				"raw": raw_message.left(MALFORMED_RAW_PREVIEW_CHARS),
				"detail": "Response was not valid JSON object.",
				"packet_size": packet_size,
				"decode_ms": decode_ms,
				"parse_ms": parse_ms,
			}
		else:
			var parsed_packet: Dictionary = parsed
			if String(parsed_packet.get("type", "")) == "stress_payload":
				result = {
					"kind": "stress",
					"packet_size": packet_size,
					"decode_ms": decode_ms,
					"parse_ms": parse_ms,
				}
			elif String(parsed_packet.get("type", "")) == "assistant_ui_state":
				result = {
					"kind": "assistant_ui_state",
					"payload": parsed_packet,
					"packet_size": packet_size,
					"decode_ms": decode_ms,
					"parse_ms": parse_ms,
				}
			else:
				result = {
					"kind": "response",
					"payload": parsed_packet,
					"packet_size": packet_size,
					"decode_ms": decode_ms,
					"parse_ms": parse_ms,
				}
		_payload_parse_mutex.lock()
		_payload_parsed_queue.append(result)
		_payload_parse_mutex.unlock()


func _payload_parsed_count() -> int:
	_payload_parse_mutex.lock()
	var count := _payload_parsed_queue.size()
	_payload_parse_mutex.unlock()
	return count


func _payload_pending_count() -> int:
	_payload_parse_mutex.lock()
	var count := _payload_parse_queue.size() + _payload_parsed_queue.size()
	_payload_parse_mutex.unlock()
	return count


func _pop_parsed_payload() -> Dictionary:
	_payload_parse_mutex.lock()
	var item := {}
	if not _payload_parsed_queue.is_empty():
		item = _payload_parsed_queue.pop_front()
	_payload_parse_mutex.unlock()
	return item


func _produce_fake_backend_stress(delta: float, metrics: Dictionary) -> void:
	_fake_stress_timer -= delta
	if _fake_stress_timer > 0.0:
		return
	_fake_stress_timer = FAKE_STRESS_PAYLOAD_INTERVAL
	_fake_stress_sequence += 1
	var visual_raw := "{\"type\":\"visual_state\",\"mode\":\"thinking\",\"energy\":0.4,\"thinking_intensity\":1.0}"
	_route_raw_message(visual_raw, visual_raw.to_utf8_buffer().size(), metrics)
	_queue_payload_parse(_fake_stress_payload, _fake_stress_payload.to_utf8_buffer().size())
	metrics["queued_payloads"] = int(metrics["queued_payloads"]) + 1


func _build_fake_stress_payload(target_bytes: int) -> String:
	var chunks := PackedStringArray()
	var chunk := "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
	var body_size := maxi(target_bytes - 256, 1024)
	var repeats := int(ceil(float(body_size) / float(chunk.length())))
	for _index in range(repeats):
		chunks.append(chunk)
	var text := "".join(chunks)
	return JSON.stringify({
		"type": "stress_payload",
		"sequence": _fake_stress_sequence,
		"message": text,
	})


func _warn_slow(step: String, elapsed_ms: float, packet_size: int = 0) -> void:
	if elapsed_ms >= WEBSOCKET_PERF_WARN_MS:
		print("WebSocketPerf slow %s=%.2fms packet_size=%s" % [step, elapsed_ms, packet_size])


func _elapsed_ms_since(started_usec: int) -> float:
	return float(Time.get_ticks_usec() - started_usec) / 1000.0
