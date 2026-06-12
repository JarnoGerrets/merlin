extends Control

enum MerlinState {
	IDLE,
	THINKING,
	SPEAKING,
	EXECUTING_TOOL,
	ERROR,
	LISTENING,
	MEMORY_UPDATE,
	UPDATING,
	LOADING_MODEL
}

const TYPEWRITER_CHARS_PER_SECOND := 104.0
const TYPEWRITER_PUNCTUATION_DELAY := 0.030
const TYPEWRITER_PARAGRAPH_DELAY := 0.060
const MAX_NOTIFICATIONS := 5
const VOICE_TRANSCRIBE_URL := "http://localhost:5000/api/voice/transcribe?extension=.wav"
const VOICE_SYNTHESIS_STREAM_HOST := "localhost"
const VOICE_SYNTHESIS_STREAM_PORT := 5000
const VOICE_SYNTHESIS_STREAM_PATH := "/api/voice/synthesize-stream"
const VOICE_STREAM_POC_HOST := "localhost"
const VOICE_STREAM_POC_PORT := 5000
const VOICE_STREAM_POC_PATH := "/api/voice/stream-pcm-test"
const VOICE_GENERATOR_BUFFER_SECONDS := 0.50
const VOICE_OUTPUT_DRAIN_SECONDS := 0.12
const RECORD_BUS_NAME := "MerlinRecord"
const FRAME_PROFILER_ENABLED := true
const FRAME_PROFILER_REPORT_SECONDS := 1.0
const FRAME_PROFILER_SPIKE_MS := 33.0

const COLOR_BACKGROUND := Color(0.000, 0.008, 0.026, 1.0)
const COLOR_PANEL := Color(0.002, 0.024, 0.070, 0.40)
const COLOR_PANEL_DARK := Color(0.001, 0.014, 0.044, 0.64)
const COLOR_BLUE := Color(0.08, 0.42, 1.00, 1.0)
const COLOR_CYAN := Color(0.24, 0.72, 1.0, 1.0)
const COLOR_WHITE := Color(0.88, 0.96, 1.0, 1.0)
const COLOR_MUTED := Color(0.50, 0.62, 0.70, 1.0)
const COLOR_AMBER := Color(1.0, 0.68, 0.28, 1.0)
const COLOR_RED := Color(1.0, 0.28, 0.34, 1.0)

@onready var web_socket_client: MerlinWebSocketClient = $MerlinWebSocketClient
@onready var voice_transcribe_request: HTTPRequest = $VoiceTranscribeRequest
@onready var voice_synthesis_request: HTTPRequest = $VoiceSynthesisRequest
@onready var voice_playback: AudioStreamPlayer = $VoicePlayback
@onready var microphone_input: AudioStreamPlayer = $MicrophoneInput
@onready var background: ColorRect = $Background
@onready var core_orb = $CoreOrb
@onready var status_panel: PanelContainer = $StatusPanel
@onready var connection_state_label: Label = $StatusPanel/Header/ConnectionStateLabel
@onready var reconnect_button: Button = $StatusPanel/Header/ReconnectButton
@onready var show_debug_check_box: CheckBox = $StatusPanel/Header/ShowDebugCheckBox
@onready var activity_panel: PanelContainer = $ActivityPanel
@onready var activity_label: Label = $ActivityPanel/ActivityMargin/ActivityLabel
@onready var notification_panel: PanelContainer = $NotificationPanel
@onready var notification_list: VBoxContainer = $NotificationPanel/NotificationMargin/NotificationList
@onready var error_label: Label = $OverlayContainer/ErrorLabel
@onready var chat_panel: PanelContainer = $ChatPanel
@onready var history_panel: PanelContainer = $ChatPanel/Content/ChatColumn/HistoryPanel
@onready var message_scroll: ScrollContainer = $ChatPanel/Content/ChatColumn/HistoryPanel/HistoryMargin/MessageScroll
@onready var message_list: VBoxContainer = $ChatPanel/Content/ChatColumn/HistoryPanel/HistoryMargin/MessageScroll/MessageList
@onready var thinking_label: Label = $ChatPanel/Content/ChatColumn/ThinkingLabel
@onready var command_input_panel: PanelContainer = $CommandInput
@onready var message_input: LineEdit = $CommandInput/InputRow/MessageInput
@onready var send_button: Button = $CommandInput/InputRow/SendButton
@onready var voice_control: PanelContainer = $VoiceControl
@onready var voice_button: Button = $VoiceControl/VoiceButton

var _pending_requests := {}
var _merlin_state: int = MerlinState.IDLE
var _focus_request_id := 0
var _record_effect: AudioEffectRecord
var _is_recording := false
var _record_bus_index := -1
var _speech_turn_active := false
var _voice_turn_started_usec := 0
var _llm_response_received_usec := 0
var _stream_poc_client: HTTPClient
var _stream_poc_active := false
var _stream_poc_header_complete := false
var _stream_poc_header_bytes := PackedByteArray()
var _stream_poc_pcm_bytes := PackedByteArray()
var _stream_poc_playback: AudioStreamGeneratorPlayback
var _stream_poc_channels := 1
var _stream_poc_sample_rate := 24000
var _stream_poc_started_usec := 0
var _stream_poc_first_byte_logged := false
var _stream_poc_first_audio_logged := false
var _stream_poc_request_sent := false
var _stream_poc_stream_complete := false
var _stream_poc_body_started := false
var _voice_phase := "idle"
var _frame_profile_window_started_usec := 0
var _frame_profile_last_report_usec := 0
var _frame_profile_frame_count := 0
var _frame_profile_total_ms := 0.0
var _frame_profile_max_ms := 0.0
var _frame_profile_over_16 := 0
var _frame_profile_over_33 := 0
var _frame_profile_over_50 := 0
var _frame_profile_over_100 := 0
var _frame_profile_http_polled := false
var _frame_profile_bytes := 0
var _frame_profile_pcm_frames := 0
var _frame_profile_json_parse_count := 0
var _frame_profile_json_parse_ms := 0.0
var _frame_profile_large_copy_count := 0
var _frame_profile_large_copy_bytes := 0
var _frame_profile_sync_work_ms := 0.0
var _frame_profile_sync_work_label := ""
var _frame_profile_audio_push_ms := 0.0
var _frame_profile_websocket_packets := 0
var _frame_profile_websocket_work_ms := 0.0


func _ready() -> void:
	_apply_visual_theme()
	_setup_voice_mode()
	message_input.focus_mode = Control.FOCUS_ALL
	message_input.keep_editing_on_text_submit = true
	message_scroll.focus_mode = Control.FOCUS_NONE
	message_list.focus_mode = Control.FOCUS_NONE
	thinking_label.focus_mode = Control.FOCUS_NONE
	error_label.focus_mode = Control.FOCUS_NONE
	send_button.pressed.connect(_on_send_pressed)
	voice_button.button_down.connect(_on_voice_button_down)
	voice_button.button_up.connect(_on_voice_button_up)
	reconnect_button.pressed.connect(_on_reconnect_pressed)
	show_debug_check_box.toggled.connect(_on_show_debug_check_box_toggled)
	message_input.text_submitted.connect(_on_message_submitted)

	web_socket_client.connection_state_changed.connect(_on_connection_state_changed)
	web_socket_client.response_received.connect(_on_response_received)
	web_socket_client.malformed_response.connect(_on_malformed_response)
	web_socket_client.socket_closed.connect(_on_socket_closed)
	web_socket_client.frontend_work_observed.connect(_on_frontend_work_observed)

	_add_system_message("Connecting to Merlin.Backend...")
	_add_notification("Connecting to Merlin.Backend", "system")
	_update_pending_state()
	web_socket_client.connect_to_backend()
	_focus_message_input()


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_F9:
		_start_streaming_pcm_poc()


func _process(delta: float) -> void:
	_frame_profile_begin_frame()
	if _stream_poc_active:
		_poll_streaming_pcm_poc()
	_frame_profile_end_frame(delta)


func _on_reconnect_pressed() -> void:
	_clear_error()
	_pending_requests.clear()
	_update_pending_state()
	_add_system_message("Reconnecting...")
	_add_notification("Reconnecting", "system")
	web_socket_client.connect_to_backend()
	_update_send_button()
	_focus_message_input()


func _on_send_pressed() -> void:
	_send_current_message()


func _on_message_submitted(_text: String) -> void:
	_send_current_message()
	_focus_message_input()



func _send_current_message() -> void:
	var message := message_input.text.strip_edges()
	if message.is_empty():
		return

	_send_backend_message(message, true)


func _send_backend_message(message: String, show_user_message: bool) -> void:
	if not web_socket_client.is_backend_connected():
		_show_error("Cannot send: Merlin.Backend is not connected.")
		_add_notification("Backend offline", "error")
		_set_merlin_state(MerlinState.ERROR)
		_update_send_button()
		_focus_message_input()
		return

	var correlation_id := _generate_correlation_id()
	_pending_requests[correlation_id] = message
	if show_user_message:
		_add_user_message(message)
		message_input.clear()

	_update_pending_state()
	_set_merlin_state(MerlinState.THINKING)
	_clear_error()

	var sent := web_socket_client.send_message(message, correlation_id)
	if not sent:
		_pending_requests.erase(correlation_id)
		_add_system_message("Message was not sent.")
		_add_notification("Message was not sent", "error")
		_set_merlin_state(MerlinState.ERROR)
		_update_pending_state()
	elif not show_user_message:
		_add_notification("Sent to Merlin.Backend", "system")

	_focus_message_input()


func _setup_voice_mode() -> void:
	chat_panel.visible = false
	command_input_panel.visible = false
	voice_control.visible = true
	voice_playback.bus = "Master"
	voice_playback.volume_db = 0.0
	voice_control.add_theme_stylebox_override("panel", _panel_style(Color(0.010, 0.052, 0.125, 0.64), COLOR_CYAN, 1.0, 10))
	_style_button(voice_button)
	_setup_microphone_recording()


func _setup_microphone_recording() -> void:
	_record_bus_index = AudioServer.get_bus_index(RECORD_BUS_NAME)
	if _record_bus_index == -1:
		AudioServer.add_bus()
		_record_bus_index = AudioServer.get_bus_count() - 1
		AudioServer.set_bus_name(_record_bus_index, RECORD_BUS_NAME)
		AudioServer.set_bus_send(_record_bus_index, "Master")

	_record_effect = AudioEffectRecord.new()
	AudioServer.add_bus_effect(_record_bus_index, _record_effect)
	AudioServer.set_bus_mute(_record_bus_index, false)
	AudioServer.set_bus_volume_db(_record_bus_index, -80.0)

	microphone_input.stream = AudioStreamMicrophone.new()
	microphone_input.bus = RECORD_BUS_NAME
	microphone_input.play()


func _on_voice_button_down() -> void:
	if not web_socket_client.is_backend_connected():
		_show_error("Cannot listen: Merlin.Backend is not connected.")
		_add_notification("Backend offline", "error")
		return
	if _is_recording or _record_effect == null:
		return

	_is_recording = true
	voice_button.text = "Listening..."
	_set_voice_phase("recording")
	_set_merlin_state(MerlinState.LISTENING)
	_record_effect.set_recording_active(true)


func _on_voice_button_up() -> void:
	if not _is_recording or _record_effect == null:
		return

	_is_recording = false
	voice_button.disabled = true
	voice_button.text = "Transcribing..."
	_record_effect.set_recording_active(false)
	_voice_turn_started_usec = Time.get_ticks_usec()
	print("Voice timing: recording finished.")
	_set_voice_phase("recording_finished")
	var recording_started_usec := Time.get_ticks_usec()
	var recording := _record_effect.get_recording()
	_record_sync_work("get_recording", recording_started_usec)
	await _send_recording_for_transcription(recording)


func _send_recording_for_transcription(recording: AudioStreamWAV) -> void:
	if recording == null:
		_add_notification("No audio captured", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.IDLE)
		return

	_set_voice_phase("upload_prepare")
	var path := "user://merlin-recording.wav"
	var save_started_usec := Time.get_ticks_usec()
	var save_error := recording.save_to_wav(path)
	_record_sync_work("save_recording_wav", save_started_usec)
	if save_error != OK:
		_show_error("Could not save microphone recording. Error code: %s" % save_error)
		_add_notification("Recording failed", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.ERROR)
		return

	var open_started_usec := Time.get_ticks_usec()
	var file := FileAccess.open(path, FileAccess.READ)
	_record_sync_work("open_recording_wav", open_started_usec)
	if file == null:
		_show_error("Could not read microphone recording.")
		_add_notification("Recording failed", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.ERROR)
		return

	var read_started_usec := Time.get_ticks_usec()
	var audio_bytes := file.get_buffer(file.get_length())
	_record_sync_work("read_recording_wav", read_started_usec)
	_record_large_copy(audio_bytes.size())
	file.close()
	if audio_bytes.size() < 2048:
		_show_error("Microphone recording was empty or too small. Check Godot microphone permission and input device.")
		_add_notification("No microphone audio captured", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.ERROR)
		return

	_add_notification("Captured %.1f KB of microphone audio" % (float(audio_bytes.size()) / 1024.0), "system")
	print("Voice timing: upload/send start. Bytes: %s. ElapsedMs: %.1f" % [audio_bytes.size(), _voice_elapsed_ms()])
	_set_voice_phase("upload_send_start")
	var request_started_usec := Time.get_ticks_usec()
	var request_error := voice_transcribe_request.request_raw(
		VOICE_TRANSCRIBE_URL,
		PackedStringArray(["Content-Type: audio/wav"]),
		HTTPClient.METHOD_POST,
		audio_bytes
	)
	_record_sync_work("stt_request_raw", request_started_usec)
	if request_error != OK:
		_show_error("Could not send audio to Merlin.Backend. Error code: %s" % request_error)
		_add_notification("Transcription failed", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.ERROR)
		return

	_set_voice_phase("waiting_stt")
	var result = await voice_transcribe_request.request_completed
	_set_voice_phase("stt_response")
	var request_result: int = int(result[0])
	var response_code: int = int(result[1])
	var body: PackedByteArray = result[3]
	var response_text := body.get_string_from_utf8()
	print("Voice timing: STT response received. HTTP: %s. ElapsedMs: %.1f" % [response_code, _voice_elapsed_ms()])
	if request_result != HTTPRequest.RESULT_SUCCESS:
		_show_error("Transcription request failed. Result: %s" % request_result)
		_add_notification("Transcription failed", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.ERROR)
		return
	if response_code < 200 or response_code >= 300:
		_show_error("Transcription failed. HTTP %s %s" % [response_code, response_text])
		_add_notification("Transcription failed", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.ERROR)
		return

	var stt_parse_started_usec := Time.get_ticks_usec()
	var parsed = JSON.parse_string(response_text)
	_record_json_parse(stt_parse_started_usec)
	if typeof(parsed) != TYPE_DICTIONARY:
		_show_error("Transcription response was not valid JSON.")
		_add_notification("Transcription failed", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.ERROR)
		return

	var transcript := str(parsed.get("text", "")).strip_edges()
	if transcript.is_empty():
		_add_notification("I did not catch that", "system")
		_reset_voice_button()
		_set_merlin_state(MerlinState.IDLE)
		return

	_add_notification("Heard: %s" % transcript, "system")
	_send_backend_message(transcript, false)
	_set_voice_phase("waiting_llm")
	_show_question_acknowledgement(transcript)
	_reset_voice_button()


func _reset_voice_button() -> void:
	voice_button.disabled = false
	voice_button.text = "Hold to talk"


func _start_streaming_pcm_poc() -> void:
	if _stream_poc_active:
		print("Voice stream POC: already active.")
		return

	_stream_poc_client = HTTPClient.new()
	_stream_poc_active = true
	_stream_poc_header_complete = false
	_stream_poc_header_bytes.clear()
	_stream_poc_pcm_bytes.clear()
	_stream_poc_playback = null
	_stream_poc_channels = 1
	_stream_poc_sample_rate = 24000
	_stream_poc_started_usec = Time.get_ticks_usec()
	_stream_poc_first_byte_logged = false
	_stream_poc_first_audio_logged = false
	_stream_poc_request_sent = false
	_stream_poc_stream_complete = false
	_stream_poc_body_started = false

	print("Voice stream POC: request start.")
	var error := _stream_poc_client.connect_to_host(VOICE_STREAM_POC_HOST, VOICE_STREAM_POC_PORT)
	if error != OK:
		_stop_streaming_pcm_poc("connect failed: %s" % error)


func _poll_streaming_pcm_poc() -> void:
	if _stream_poc_client == null:
		_stop_streaming_pcm_poc("client missing")
		return

	var poll_error := _stream_poc_client.poll()
	if poll_error != OK:
		if _stream_poc_body_started:
			_mark_streaming_pcm_poc_complete()
			_stream_poc_client.close()
			_stream_poc_active = false
			return
		_stop_streaming_pcm_poc("poll failed: %s" % poll_error)
		return

	var status := _stream_poc_client.get_status()
	match status:
		HTTPClient.STATUS_CONNECTED:
			if not _stream_poc_request_sent:
				var request_error := _stream_poc_client.request(
					HTTPClient.METHOD_GET,
					VOICE_STREAM_POC_PATH,
					PackedStringArray([
						"Accept: application/octet-stream",
						"Connection: close"
					])
				)
				if request_error != OK:
					_stop_streaming_pcm_poc("request failed: %s" % request_error)
					return
				_stream_poc_request_sent = true
			elif _stream_poc_body_started:
				_mark_streaming_pcm_poc_complete()
		HTTPClient.STATUS_BODY:
			_stream_poc_body_started = true
			_read_streaming_pcm_poc_body()
		HTTPClient.STATUS_DISCONNECTED:
			if _stream_poc_request_sent:
				_mark_streaming_pcm_poc_complete()
			if _stream_poc_pcm_bytes.is_empty():
				_stream_poc_active = false
		HTTPClient.STATUS_CANT_CONNECT, HTTPClient.STATUS_CANT_RESOLVE, HTTPClient.STATUS_CONNECTION_ERROR, HTTPClient.STATUS_TLS_HANDSHAKE_ERROR:
			_stop_streaming_pcm_poc("connection status failed: %s" % status)

	if _stream_poc_playback != null and not _stream_poc_pcm_bytes.is_empty():
		_push_streaming_pcm_poc_frames()


func _read_streaming_pcm_poc_body() -> void:
	while _stream_poc_client.get_status() == HTTPClient.STATUS_BODY:
		var chunk := _stream_poc_client.read_response_body_chunk()
		if chunk.is_empty():
			break

		if not _stream_poc_first_byte_logged:
			_stream_poc_first_byte_logged = true
			print("Voice stream POC: first byte received. ElapsedMs: %.1f" % _elapsed_ms_since(_stream_poc_started_usec))

		_consume_streaming_pcm_poc_bytes(chunk)


func _mark_streaming_pcm_poc_complete() -> void:
	if _stream_poc_stream_complete:
		return
	_stream_poc_stream_complete = true
	print("Voice stream POC: stream complete. ElapsedMs: %.1f" % _elapsed_ms_since(_stream_poc_started_usec))


func _consume_streaming_pcm_poc_bytes(chunk: PackedByteArray) -> void:
	if _stream_poc_header_complete:
		_stream_poc_pcm_bytes.append_array(chunk)
		return

	var pcm_start := -1
	for index in range(chunk.size()):
		var value := int(chunk[index])
		if value == 10:
			_stream_poc_header_complete = true
			pcm_start = index + 1
			break
		_stream_poc_header_bytes.append(value)

	if not _stream_poc_header_complete:
		return

	var metadata_text := _stream_poc_header_bytes.get_string_from_utf8().strip_edges()
	var metadata = JSON.parse_string(metadata_text)
	if typeof(metadata) != TYPE_DICTIONARY:
		_stop_streaming_pcm_poc("invalid metadata: %s" % metadata_text)
		return

	_stream_poc_sample_rate = int(metadata.get("sampleRate", 24000))
	_stream_poc_channels = int(metadata.get("channels", 1))
	var format := str(metadata.get("format", ""))
	if format != "s16le" or _stream_poc_sample_rate <= 0 or _stream_poc_channels < 1 or _stream_poc_channels > 2:
		_stop_streaming_pcm_poc("unsupported metadata: %s" % metadata_text)
		return

	print("Voice stream POC: metadata received. SampleRate: %s. Channels: %s. Format: %s" % [_stream_poc_sample_rate, _stream_poc_channels, format])
	_start_streaming_pcm_poc_playback()

	if pcm_start >= 0 and pcm_start < chunk.size():
		_stream_poc_pcm_bytes.append_array(chunk.slice(pcm_start))
		_push_streaming_pcm_poc_frames()


func _start_streaming_pcm_poc_playback() -> void:
	var stream := AudioStreamGenerator.new()
	stream.mix_rate = float(_stream_poc_sample_rate)
	stream.buffer_length = 0.50
	voice_playback.stream = stream
	voice_playback.bus = "Master"
	voice_playback.volume_db = 0.0
	voice_playback.play()
	_stream_poc_playback = voice_playback.get_stream_playback() as AudioStreamGeneratorPlayback


func _push_streaming_pcm_poc_frames() -> void:
	if _stream_poc_playback == null:
		return

	var frame_size := _stream_poc_channels * 2
	var available_frames := _stream_poc_playback.get_frames_available()
	if available_frames <= 0 or _stream_poc_pcm_bytes.size() < frame_size:
		return

	var frames := PackedVector2Array()
	var offset := 0
	while available_frames > 0 and offset + frame_size <= _stream_poc_pcm_bytes.size():
		if _stream_poc_channels == 1:
			var mono := _decode_streaming_pcm_poc_sample(_stream_poc_pcm_bytes, offset)
			frames.append(Vector2(mono, mono))
		else:
			var left := _decode_streaming_pcm_poc_sample(_stream_poc_pcm_bytes, offset)
			var right := _decode_streaming_pcm_poc_sample(_stream_poc_pcm_bytes, offset + 2)
			frames.append(Vector2(left, right))
		offset += frame_size
		available_frames -= 1

	if frames.is_empty():
		return

	_stream_poc_playback.push_buffer(frames)
	if not _stream_poc_first_audio_logged:
		_stream_poc_first_audio_logged = true
		print("Voice stream POC: first audio playback. ElapsedMs: %.1f. Frames: %s" % [_elapsed_ms_since(_stream_poc_started_usec), frames.size()])

	_stream_poc_pcm_bytes = _stream_poc_pcm_bytes.slice(offset)


func _decode_streaming_pcm_poc_sample(bytes: PackedByteArray, offset: int) -> float:
	var unsigned := int(bytes[offset]) | (int(bytes[offset + 1]) << 8)
	if unsigned >= 32768:
		unsigned -= 65536
	return clampf(float(unsigned) / 32768.0, -1.0, 1.0)


func _stop_streaming_pcm_poc(reason: String) -> void:
	print("Voice stream POC: stopped. Reason: %s" % reason)
	if _stream_poc_client != null:
		_stream_poc_client.close()
	_stream_poc_client = null
	_stream_poc_active = false


func _show_question_acknowledgement(transcript: String) -> void:
	if chat_panel.visible:
		return

	var summary := _summarize_transcript_for_acknowledgement(transcript)
	if summary.is_empty():
		return

	activity_label.text = "Thinking about: %s" % summary
	_add_notification("Thinking about: %s" % summary, "system")


func _summarize_transcript_for_acknowledgement(transcript: String) -> String:
	var text := transcript.strip_edges()
	if text.is_empty():
		return ""

	text = text.trim_suffix(".").trim_suffix("?").trim_suffix("!").trim_suffix(",").strip_edges()
	var lower_text := text.to_lower()
	for prefix in [
		"hey merlin ",
		"merlin ",
		"can you ",
		"could you ",
		"would you ",
		"please ",
		"i want you to "
	]:
		if lower_text.begins_with(prefix):
			text = text.substr(prefix.length()).strip_edges()
			lower_text = text.to_lower()
			break

	var words := text.split(" ", false)
	if words.size() <= 18:
		return text

	var summary_words := PackedStringArray()
	for index in range(18):
		summary_words.append(words[index])
	return " ".join(summary_words)


func _on_connection_state_changed(state: String, detail: String) -> void:
	connection_state_label.text = _format_connection_state(state, detail)

	match state:
		"connected":
			_clear_error()
			_add_system_message("Connected to Merlin.Backend.")
			_add_notification("Connected", "system")
			if _pending_requests.is_empty():
				_set_merlin_state(MerlinState.IDLE)
			_focus_message_input()
		"connecting":
			_clear_error()
		"error":
			_show_error(detail)
			_add_system_message("Connection error: %s" % detail)
			_add_notification("Connection error", "error")
			_pending_requests.clear()
			_update_pending_state()
			_set_merlin_state(MerlinState.ERROR)
		"disconnected":
			if not detail.is_empty():
				_add_system_message(detail)
			_add_notification("Disconnected", "system")
			_pending_requests.clear()
			_update_pending_state()
			_set_merlin_state(MerlinState.IDLE)

	_update_send_button()


func _on_response_received(response: Dictionary) -> void:
	_llm_response_received_usec = Time.get_ticks_usec()
	print("Voice timing: LLM response received. ElapsedMs: %.1f" % _voice_elapsed_ms())
	_set_voice_phase("llm_response")
	var correlation_id := str(response.get("correlationId", ""))
	if not correlation_id.is_empty():
		_pending_requests.erase(correlation_id)

	var success := bool(response.get("success", false))
	var message := str(response.get("message", ""))
	var error_code = response.get("errorCode", null)
	var response_type := str(response.get("responseType", "assistant" if success else "error"))
	var available_tools = response.get("availableTools", null)
	var diagnostics = response.get("diagnostics", null)
	var confirmation = response.get("confirmation", null)
	var application_candidates = response.get("applicationCandidates", null)
	var debug_text := _format_debug_info(response)

	_update_pending_state()
	_update_send_button()
	_set_voice_phase("display_response")
	await _display_backend_response(
		response,
		success,
		message,
		error_code,
		response_type,
		available_tools,
		diagnostics,
		confirmation,
		application_candidates,
		debug_text
	)
	if _speech_turn_active:
		_set_voice_phase("playback_completion")
	_focus_message_input()


func _on_malformed_response(raw_message: String, detail: String) -> void:
	_pending_requests.clear()
	_update_pending_state()
	var message := "Malformed response JSON: %s" % detail
	_show_error(message)
	_add_system_message("%s Raw: %s" % [message, raw_message])
	_add_notification("Malformed backend response", "error")
	_set_merlin_state(MerlinState.ERROR)
	_focus_message_input()


func _on_socket_closed(code: int, reason: String) -> void:
	_pending_requests.clear()
	_update_pending_state()
	if code != 1000:
		_show_error("WebSocket closed. Code: %s Reason: %s" % [code, reason])
		_add_notification("WebSocket closed unexpectedly", "error")
		_set_merlin_state(MerlinState.ERROR)
	else:
		_add_notification("WebSocket closed", "system")
		_set_merlin_state(MerlinState.IDLE)
	_focus_message_input()


func _apply_visual_theme() -> void:
	background.color = COLOR_BACKGROUND

	status_panel.add_theme_stylebox_override("panel", _panel_style(COLOR_PANEL, COLOR_BLUE, 1.0, 8))
	activity_panel.add_theme_stylebox_override("panel", _panel_style(Color(0.002, 0.026, 0.064, 0.30), COLOR_CYAN, 1.0, 8))
	notification_panel.add_theme_stylebox_override("panel", _panel_style(Color(0.001, 0.006, 0.020, 0.18), Color(0, 0, 0, 0), 0.0, 8))
	chat_panel.add_theme_stylebox_override("panel", _panel_style(COLOR_PANEL_DARK, Color(COLOR_BLUE.r, COLOR_BLUE.g, COLOR_BLUE.b, 0.45), 1.0, 8))
	history_panel.add_theme_stylebox_override("panel", _panel_style(Color(0.006, 0.022, 0.052, 0.36), Color(COLOR_CYAN.r, COLOR_CYAN.g, COLOR_CYAN.b, 0.28), 1.0, 6))
	command_input_panel.add_theme_stylebox_override("panel", _panel_style(Color(0.010, 0.052, 0.125, 0.64), COLOR_CYAN, 1.0, 10))

	_style_button(send_button)
	_style_button(reconnect_button)
	_style_line_edit(message_input)

	connection_state_label.add_theme_color_override("font_color", COLOR_CYAN)
	activity_label.add_theme_color_override("font_color", COLOR_WHITE)
	thinking_label.add_theme_color_override("font_color", COLOR_CYAN)
	error_label.add_theme_color_override("font_color", COLOR_RED)
	show_debug_check_box.add_theme_color_override("font_color", COLOR_MUTED)


func _panel_style(fill: Color, border: Color, border_width: float, radius: int) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = fill
	style.border_color = border
	style.set_border_width_all(int(border_width))
	style.set_corner_radius_all(radius)
	style.content_margin_left = 12
	style.content_margin_top = 10
	style.content_margin_right = 12
	style.content_margin_bottom = 10
	return style


func _style_button(button: Button) -> void:
	button.add_theme_stylebox_override("normal", _panel_style(Color(0.004, 0.034, 0.088, 0.44), Color(COLOR_BLUE.r, COLOR_BLUE.g, COLOR_BLUE.b, 0.50), 1.0, 6))
	button.add_theme_stylebox_override("hover", _panel_style(Color(0.006, 0.052, 0.120, 0.64), COLOR_CYAN, 1.0, 6))
	button.add_theme_stylebox_override("pressed", _panel_style(Color(0.006, 0.066, 0.150, 0.72), COLOR_CYAN, 1.0, 6))
	button.add_theme_color_override("font_color", COLOR_WHITE)
	button.add_theme_color_override("font_hover_color", COLOR_WHITE)
	button.add_theme_color_override("font_pressed_color", COLOR_WHITE)


func _style_line_edit(line_edit: LineEdit) -> void:
	line_edit.add_theme_stylebox_override("normal", _panel_style(Color(0.0, 0.014, 0.042, 0.32), Color(COLOR_CYAN.r, COLOR_CYAN.g, COLOR_CYAN.b, 0.50), 1.0, 6))
	line_edit.add_theme_stylebox_override("focus", _panel_style(Color(0.0, 0.028, 0.078, 0.56), COLOR_CYAN, 1.0, 6))
	line_edit.add_theme_color_override("font_color", COLOR_WHITE)
	line_edit.add_theme_color_override("font_placeholder_color", COLOR_MUTED)
	line_edit.add_theme_color_override("caret_color", COLOR_CYAN)


func _format_success_response(message: String, available_tools, diagnostics, confirmation) -> String:
	if typeof(diagnostics) == TYPE_DICTIONARY:
		return _format_diagnostics(message, diagnostics)
	if typeof(confirmation) == TYPE_DICTIONARY:
		return _format_confirmation(message, confirmation)

	if typeof(available_tools) != TYPE_ARRAY:
		return message

	var lines := PackedStringArray([message, ""])
	for tool in available_tools:
		if typeof(tool) != TYPE_DICTIONARY:
			continue

		lines.append("%s - %s" % [tool.get("name", "Unnamed Tool"), tool.get("description", "")])

		var examples = tool.get("examples", [])
		if typeof(examples) == TYPE_ARRAY and not examples.is_empty():
			lines.append("Examples: %s" % _join_values(examples, ", "))

		lines.append("")

	return "\n".join(lines).strip_edges()


func _format_diagnostics(message: String, diagnostics: Dictionary) -> String:
	var registered_tools = diagnostics.get("registeredTools", [])
	var tools_text := ""
	if typeof(registered_tools) == TYPE_ARRAY:
		tools_text = _join_values(registered_tools, ", ")

	var lines := PackedStringArray([
		"Merlin Status",
		"",
		"Uptime: %s" % str(diagnostics.get("uptime", "")),
		"Connections: %s" % str(diagnostics.get("activeWebSocketConnections", "")),
		"Requests: %s" % str(diagnostics.get("totalRequestsProcessed", "")),
		"Session: %s" % str(diagnostics.get("conversationSessionId", "")),
		"Session messages: %s" % str(diagnostics.get("conversationMessageCount", "")),
		"Session summary length: %s" % str(diagnostics.get("conversationSummaryLength", "")),
		"Stored summaries: %s" % str(diagnostics.get("conversationSummaryCount", "")),
		"Summary store healthy: %s" % str(diagnostics.get("conversationSummaryStoreHealthy", "")),
		"Memories: %s" % str(diagnostics.get("memoryCount", "")),
		"Memory candidates: %s" % str(diagnostics.get("memoryCandidateCount", "")),
		"Memory store healthy: %s" % str(diagnostics.get("memoryStoreHealthy", "")),
		"Supported capabilities: %s" % str(diagnostics.get("supportedCapabilityCount", "")),
		"Missing capability detection: %s" % str(diagnostics.get("missingCapabilityDetectionEnabled", "")),
		"Capability domains: %s" % str(diagnostics.get("capabilityDomainCount", "")),
		"Implemented capabilities: %s" % str(diagnostics.get("implementedCapabilityCount", "")),
		"Missing capabilities: %s" % str(diagnostics.get("missingCapabilityCount", "")),
		"Unsupported capabilities: %s" % str(diagnostics.get("unsupportedCapabilityCount", "")),
		"Successful tools: %s" % str(diagnostics.get("totalSuccessfulToolExecutions", "")),
		"Failed tools: %s" % str(diagnostics.get("totalFailedToolExecutions", "")),
		"Pending confirmations: %s" % str(diagnostics.get("pendingConfirmations", "")),
		"Confirmation expiry: %s" % str(diagnostics.get("confirmationExpiryDuration", "")),
		"Resolver: %s" % str(diagnostics.get("resolverStatus", "")),
		"Trusted apps: %s" % str(diagnostics.get("trustedApplicationCount", "")),
		"Trusted commands: %s" % str(diagnostics.get("trustedCommandCount", "")),
		"Last app resolution: %s" % str(diagnostics.get("lastApplicationResolutionStatus", "")),
		"Local AI enabled: %s" % str(diagnostics.get("localAiEnabled", "")),
		"Local AI available: %s" % str(diagnostics.get("localAiAvailable", "")),
		"Chat tool enabled: %s" % str(diagnostics.get("chatToolEnabled", "")),
		"Local AI provider: %s" % str(diagnostics.get("localAiProvider", "")),
		"Local AI model: %s" % str(diagnostics.get("localAiModel", "")),
		"Local AI last warmup UTC: %s" % str(diagnostics.get("localAiLastWarmupUtc", "")),
		"Local AI last latency ms: %s" % str(diagnostics.get("localAiLastLatencyMs", "")),
		"Local AI last error: %s" % str(diagnostics.get("localAiLastError", "")),
		"Last parser: %s" % str(diagnostics.get("lastIntentParserUsed", "")),
		"Environment: %s" % str(diagnostics.get("environment", "")),
		"Tools: %s" % tools_text
	])

	if not message.is_empty() and message != "Merlin diagnostics":
		lines.insert(0, message)

	return "\n".join(lines).strip_edges()


func _format_confirmation(message: String, confirmation: Dictionary, application_candidates = null) -> String:
	var lines := PackedStringArray([
		message,
		"",
		"Confirmation required",
		"Action: %s" % str(confirmation.get("action", "")),
		"Target: %s" % str(confirmation.get("displayName", "")),
		"Expires: %s" % str(confirmation.get("expiresAtUtc", "")),
		"Type confirm to approve."
	])

	if typeof(application_candidates) == TYPE_ARRAY and application_candidates.size() > 1:
		lines.append("")
		lines.append("Candidates:")
		var index := 1
		for candidate in application_candidates:
			if typeof(candidate) == TYPE_DICTIONARY:
				lines.append("%s. %s" % [index, str(candidate.get("displayName", ""))])
				index += 1
		lines.append("Type choose 1, choose 2, or confirm to approve the first option.")

	return "\n".join(lines).strip_edges()


func _format_error_response(error_code, message: String) -> String:
	var code := str(error_code) if error_code != null else "ERROR"
	if code in ["UNKNOWN_INPUT", "MISSING_CAPABILITY", "UNSUPPORTED_ACTION"]:
		return message

	return "%s - %s" % [code, message]


func _display_backend_response(
	response: Dictionary,
	success: bool,
	message: String,
	error_code,
	response_type: String,
	available_tools,
	diagnostics,
	confirmation,
	application_candidates,
	debug_text: String
) -> void:
	await _prepare_orb_for_response(response, success, response_type)
	_focus_message_input()

	if success:
		var spoken_message := _format_success_response(message, available_tools, diagnostics, confirmation)
		if chat_panel.visible:
			await _add_typed_chat_line("Merlin", spoken_message, debug_text, _response_kind(response, success, response_type))
		else:
			await _speak_text(spoken_message)
		_clear_error()
	else:
		var formatted_error := _format_error_response(error_code, message)
		if typeof(confirmation) == TYPE_DICTIONARY:
			var confirmation_message := _format_confirmation(message, confirmation, application_candidates)
			if chat_panel.visible:
				await _add_typed_chat_line("Merlin", confirmation_message, debug_text, "confirmation")
			else:
				await _speak_text(confirmation_message)
			_add_notification("Confirmation required", "confirmation")
			_clear_error()
			activity_label.text = "Waiting for confirmation"
			core_orb.play_confirmation()
			_focus_message_input()
			return
		elif response_type == "limitation" or response_type == "safety":
			var kind := _response_kind(response, success, response_type)
			if chat_panel.visible:
				await _add_typed_chat_line("Merlin", message, debug_text, kind)
			else:
				await _speak_text(message)
			_add_notification("Capability unavailable" if kind == "limitation" else "Safety boundary", kind)
			_clear_error()
		elif response_type == "system":
			_add_system_message(message)
			_add_notification(message, "system")
			if not chat_panel.visible:
				await _speak_text(message)
			_clear_error()
		else:
			if chat_panel.visible:
				await _add_typed_chat_line("Error", formatted_error, debug_text, "error")
			else:
				await _speak_text(formatted_error)
			_add_notification("Error", "error")
			_clear_error()

	_settle_orb_after_response()
	_focus_message_input()


func _speak_text(text: String) -> void:
	var spoken_text := text.strip_edges()
	if spoken_text.is_empty():
		return

	while _speech_turn_active:
		await get_tree().create_timer(0.05).timeout

	_speech_turn_active = true
	_set_voice_phase("tts_streaming")
	await _speak_text_unlocked(spoken_text)
	_speech_turn_active = false
	_set_voice_phase("playback_completion")
	_settle_orb_after_response()


func _speak_text_unlocked(spoken_text: String) -> void:
	if spoken_text.is_empty():
		return

	var streamed := await _stream_speech_text(spoken_text)
	if streamed:
		return

	_show_error("Speech streaming failed.")
	_add_notification("Speech playback failed", "error")


func _stream_speech_text(spoken_text: String, stream_path: String = VOICE_SYNTHESIS_STREAM_PATH, timing_label: String = "streaming TTS") -> bool:
	var client := HTTPClient.new()
	var connect_started_usec := Time.get_ticks_usec()
	var connect_error := client.connect_to_host(VOICE_SYNTHESIS_STREAM_HOST, VOICE_SYNTHESIS_STREAM_PORT)
	_record_sync_work("tts_connect_to_host", connect_started_usec)
	if connect_error != OK:
		print("Voice timing: %s connect failed. Error: %s" % [timing_label, connect_error])
		return false

	var started_usec := Time.get_ticks_usec()
	var request_sent := false
	var body_started := false
	var header_complete := false
	var header_bytes := PackedByteArray()
	var pcm_bytes := PackedByteArray()
	var playback: AudioStreamGeneratorPlayback = null
	var channels := 1
	var stream_sample_rate := 24000
	var frames_pushed := 0
	var first_byte_logged := false
	var first_pcm_byte_logged := false
	var first_pcm_buffered_logged := false
	var first_audio_submitted_logged := false
	var first_audible_logged := false
	var completed := false
	var payload := JSON.stringify({ "text": spoken_text })

	_set_voice_phase("tts_streaming")
	print("Voice timing: %s request start. Chars: %s. SinceLlmMs: %.1f" % [timing_label, spoken_text.length(), _elapsed_ms_since(_llm_response_received_usec)])

	while true:
		var poll_started_usec := Time.get_ticks_usec()
		var poll_error := client.poll()
		_record_http_poll(poll_started_usec)
		if poll_error != OK:
			if body_started:
				completed = true
				break
			print("Voice timing: %s poll failed. Error: %s" % [timing_label, poll_error])
			client.close()
			return false

		var status := client.get_status()
		match status:
			HTTPClient.STATUS_CONNECTED:
				if not request_sent:
					var tts_request_started_usec := Time.get_ticks_usec()
					var request_error := client.request(
						HTTPClient.METHOD_POST,
						stream_path,
						PackedStringArray([
							"Accept: application/octet-stream",
							"Content-Type: application/json",
							"Connection: close"
						]),
						payload
					)
					_record_sync_work("tts_request", tts_request_started_usec)
					if request_error != OK:
						print("Voice timing: %s request failed. Error: %s" % [timing_label, request_error])
						client.close()
						return false
					request_sent = true
				elif body_started:
					completed = true
					break
			HTTPClient.STATUS_BODY:
				body_started = true
				while client.get_status() == HTTPClient.STATUS_BODY:
					var chunk := client.read_response_body_chunk()
					if chunk.is_empty():
						break
					_record_bytes_processed(chunk.size())

					if not first_byte_logged:
						first_byte_logged = true
						print("Voice timing: %s first byte received. SinceLlmMs: %.1f. RequestMs: %.1f" % [timing_label, _elapsed_ms_since(_llm_response_received_usec), _elapsed_ms_since(started_usec)])

					if header_complete:
						_record_large_copy(chunk.size())
						pcm_bytes.append_array(chunk)
						if not first_pcm_byte_logged:
							first_pcm_byte_logged = true
							print("Voice timing: %s first PCM byte received. SinceLlmMs: %.1f. RequestMs: %.1f. BufferedBytes: %s" % [timing_label, _elapsed_ms_since(_llm_response_received_usec), _elapsed_ms_since(started_usec), pcm_bytes.size()])
					else:
						var pcm_start := -1
						for index in range(chunk.size()):
							var value := int(chunk[index])
							if value == 10:
								header_complete = true
								pcm_start = index + 1
								break
							header_bytes.append(value)

						if header_complete:
							var metadata_text := header_bytes.get_string_from_utf8().strip_edges()
							var metadata_parse_started_usec := Time.get_ticks_usec()
							var metadata = JSON.parse_string(metadata_text)
							_record_json_parse(metadata_parse_started_usec)
							if typeof(metadata) != TYPE_DICTIONARY:
								print("Voice timing: %s metadata invalid: %s" % [timing_label, metadata_text])
								client.close()
								return false

							var sample_rate := int(metadata.get("sampleRate", 24000))
							channels = int(metadata.get("channels", 1))
							var format := str(metadata.get("format", ""))
							if format != "s16le" or sample_rate <= 0 or channels < 1 or channels > 2:
								print("Voice timing: %s metadata unsupported: %s" % [timing_label, metadata_text])
								client.close()
								return false

							stream_sample_rate = sample_rate
							var stream := AudioStreamGenerator.new()
							stream.mix_rate = float(sample_rate)
							stream.buffer_length = VOICE_GENERATOR_BUFFER_SECONDS
							voice_playback.stream = stream
							voice_playback.bus = "Master"
							voice_playback.volume_db = 0.0
							voice_playback.play()
							playback = voice_playback.get_stream_playback() as AudioStreamGeneratorPlayback
							print("Voice timing: %s metadata received. SampleRate: %s. Channels: %s. Format: %s" % [timing_label, sample_rate, channels, format])

							if pcm_start >= 0 and pcm_start < chunk.size():
								var pcm_tail := chunk.slice(pcm_start)
								_record_large_copy(pcm_tail.size())
								pcm_bytes.append_array(pcm_tail)
								if not first_pcm_byte_logged:
									first_pcm_byte_logged = true
									print("Voice timing: %s first PCM byte received. SinceLlmMs: %.1f. RequestMs: %.1f. BufferedBytes: %s" % [timing_label, _elapsed_ms_since(_llm_response_received_usec), _elapsed_ms_since(started_usec), pcm_bytes.size()])

					if playback != null and not pcm_bytes.is_empty():
						if not first_pcm_buffered_logged:
							first_pcm_buffered_logged = true
							print("Voice timing: %s first PCM chunk buffered. Bytes: %s. SinceLlmMs: %.1f. RequestMs: %.1f" % [timing_label, pcm_bytes.size(), _elapsed_ms_since(_llm_response_received_usec), _elapsed_ms_since(started_usec)])
						var consumed := _push_pcm_frames(playback, pcm_bytes, channels)
						if consumed > 0:
							_record_large_copy(maxi(pcm_bytes.size() - consumed, 0))
							pcm_bytes = pcm_bytes.slice(consumed)
							frames_pushed += int(consumed / (channels * 2))
							if not first_audio_submitted_logged:
								first_audio_submitted_logged = true
								print("Voice timing: first audio submitted to Godot playback. ConsumedBytes: %s. FramesPushed: %s. SinceLlmMs: %.1f. RequestMs: %.1f" % [consumed, frames_pushed, _elapsed_ms_since(_llm_response_received_usec), _elapsed_ms_since(started_usec)])
			HTTPClient.STATUS_DISCONNECTED:
				if request_sent:
					completed = true
					break
				print("Voice timing: %s disconnected before request." % timing_label)
				client.close()
				return false
			HTTPClient.STATUS_CANT_CONNECT, HTTPClient.STATUS_CANT_RESOLVE, HTTPClient.STATUS_CONNECTION_ERROR, HTTPClient.STATUS_TLS_HANDSHAKE_ERROR:
				print("Voice timing: %s connection failed. Status: %s" % [timing_label, status])
				client.close()
				return false

		if playback != null and not pcm_bytes.is_empty():
			if not first_pcm_buffered_logged:
				first_pcm_buffered_logged = true
				print("Voice timing: %s first PCM chunk buffered. Bytes: %s. SinceLlmMs: %.1f. RequestMs: %.1f" % [timing_label, pcm_bytes.size(), _elapsed_ms_since(_llm_response_received_usec), _elapsed_ms_since(started_usec)])
			var consumed_after_poll := _push_pcm_frames(playback, pcm_bytes, channels)
			if consumed_after_poll > 0:
				_record_large_copy(maxi(pcm_bytes.size() - consumed_after_poll, 0))
				pcm_bytes = pcm_bytes.slice(consumed_after_poll)
				frames_pushed += int(consumed_after_poll / (channels * 2))
				if not first_audio_submitted_logged:
					first_audio_submitted_logged = true
					print("Voice timing: first audio submitted to Godot playback. ConsumedBytes: %s. FramesPushed: %s. SinceLlmMs: %.1f. RequestMs: %.1f" % [consumed_after_poll, frames_pushed, _elapsed_ms_since(_llm_response_received_usec), _elapsed_ms_since(started_usec)])

		if first_audio_submitted_logged and not first_audible_logged and voice_playback.get_playback_position() > 0.0:
			first_audible_logged = true
			_set_merlin_state(MerlinState.SPEAKING)
			print("Voice timing: playback first audio started. SinceLlmMs: %.1f. RequestMs: %.1f. TotalVoiceTurnMs: %.1f" % [_elapsed_ms_since(_llm_response_received_usec), _elapsed_ms_since(started_usec), _voice_elapsed_ms()])
			print("Voice timing: first audible playback position advanced. PositionMs: %.1f. SinceLlmMs: %.1f. RequestMs: %.1f" % [voice_playback.get_playback_position() * 1000.0, _elapsed_ms_since(_llm_response_received_usec), _elapsed_ms_since(started_usec)])

		await get_tree().process_frame

	client.close()
	if not first_audio_submitted_logged:
		return false

	while playback != null and not pcm_bytes.is_empty():
		if not pcm_bytes.is_empty():
			var consumed_tail := _push_pcm_frames(playback, pcm_bytes, channels)
			if consumed_tail > 0:
				_record_large_copy(maxi(pcm_bytes.size() - consumed_tail, 0))
				pcm_bytes = pcm_bytes.slice(consumed_tail)
				frames_pushed += int(consumed_tail / (channels * 2))
		await get_tree().process_frame

		if first_audio_submitted_logged and not first_audible_logged and voice_playback.get_playback_position() > 0.0:
			first_audible_logged = true
			_set_merlin_state(MerlinState.SPEAKING)
			print("Voice timing: playback first audio started. SinceLlmMs: %.1f. RequestMs: %.1f. TotalVoiceTurnMs: %.1f" % [_elapsed_ms_since(_llm_response_received_usec), _elapsed_ms_since(started_usec), _voice_elapsed_ms()])
			print("Voice timing: first audible playback position advanced. PositionMs: %.1f. SinceLlmMs: %.1f. RequestMs: %.1f" % [voice_playback.get_playback_position() * 1000.0, _elapsed_ms_since(_llm_response_received_usec), _elapsed_ms_since(started_usec)])

	var generator_buffer_frames := int(float(stream_sample_rate) * VOICE_GENERATOR_BUFFER_SECONDS)
	while voice_playback.playing:
		var played_frames := int(voice_playback.get_playback_position() * float(stream_sample_rate))
		var generator_drained := playback == null or playback.get_frames_available() >= generator_buffer_frames - 1
		if played_frames >= frames_pushed and generator_drained:
			break
		await get_tree().process_frame

	if voice_playback.playing:
		await get_tree().create_timer(VOICE_OUTPUT_DRAIN_SECONDS).timeout

	voice_playback.stop()

	print("Voice timing: full playback complete. StreamComplete: %s. TotalVoiceTurnMs: %.1f" % [completed, _voice_elapsed_ms()])
	_set_voice_phase("playback_complete")
	return true


func _push_pcm_frames(playback: AudioStreamGeneratorPlayback, pcm_bytes: PackedByteArray, channels: int) -> int:
	if playback == null:
		return 0

	var frame_size := channels * 2
	var available_frames := playback.get_frames_available()
	if available_frames <= 0 or pcm_bytes.size() < frame_size:
		return 0

	var frames := PackedVector2Array()
	var offset := 0
	while available_frames > 0 and offset + frame_size <= pcm_bytes.size():
		if channels == 1:
			var mono := _decode_pcm_s16le_sample(pcm_bytes, offset)
			frames.append(Vector2(mono, mono))
		else:
			var left := _decode_pcm_s16le_sample(pcm_bytes, offset)
			var right := _decode_pcm_s16le_sample(pcm_bytes, offset + 2)
			frames.append(Vector2(left, right))
		offset += frame_size
		available_frames -= 1

	if not frames.is_empty():
		var push_started_usec := Time.get_ticks_usec()
		playback.push_buffer(frames)
		_record_audio_push(frames.size(), push_started_usec)

	return offset


func _decode_pcm_s16le_sample(bytes: PackedByteArray, offset: int) -> float:
	var unsigned := int(bytes[offset]) | (int(bytes[offset + 1]) << 8)
	if unsigned >= 32768:
		unsigned -= 65536
	return clampf(float(unsigned) / 32768.0, -1.0, 1.0)


func _voice_elapsed_ms() -> float:
	return _elapsed_ms_since(_voice_turn_started_usec)


func _elapsed_ms_since(started_usec: int) -> float:
	if started_usec <= 0:
		return 0.0
	return float(Time.get_ticks_usec() - started_usec) / 1000.0


func _set_voice_phase(phase: String) -> void:
	if _voice_phase == phase:
		return
	_voice_phase = phase
	print("Voice frame profile: phase=%s state=%s elapsedMs=%.1f" % [_voice_phase, _merlin_state_name(_merlin_state), _voice_elapsed_ms()])


func _frame_profile_begin_frame() -> void:
	if not FRAME_PROFILER_ENABLED:
		return
	if _frame_profile_window_started_usec <= 0:
		_frame_profile_window_started_usec = Time.get_ticks_usec()
		_frame_profile_last_report_usec = _frame_profile_window_started_usec


func _frame_profile_end_frame(delta: float) -> void:
	if not FRAME_PROFILER_ENABLED:
		return

	var now_usec := Time.get_ticks_usec()
	if _frame_profile_window_started_usec <= 0:
		_frame_profile_window_started_usec = now_usec
		_frame_profile_last_report_usec = now_usec

	var frame_ms := delta * 1000.0
	_frame_profile_frame_count += 1
	_frame_profile_total_ms += frame_ms
	_frame_profile_max_ms = maxf(_frame_profile_max_ms, frame_ms)
	if frame_ms > 16.0:
		_frame_profile_over_16 += 1
	if frame_ms > 33.0:
		_frame_profile_over_33 += 1
	if frame_ms > 50.0:
		_frame_profile_over_50 += 1
	if frame_ms > 100.0:
		_frame_profile_over_100 += 1

	var report_due := float(now_usec - _frame_profile_last_report_usec) / 1000000.0 >= FRAME_PROFILER_REPORT_SECONDS
	var active_voice := _voice_phase != "idle" or _speech_turn_active or _is_recording or not _pending_requests.is_empty()
	var saw_spike := _frame_profile_over_33 > 0
	if report_due and (active_voice or saw_spike):
		_print_frame_profile("window", frame_ms)
		_reset_frame_profile_window(now_usec)


func _print_frame_profile(reason: String, frame_ms: float) -> void:
	var average_ms := 0.0
	if _frame_profile_frame_count > 0:
		average_ms = _frame_profile_total_ms / float(_frame_profile_frame_count)
	var likely_gc_or_engine_work := frame_ms >= FRAME_PROFILER_SPIKE_MS
	likely_gc_or_engine_work = likely_gc_or_engine_work and _frame_profile_bytes == 0
	likely_gc_or_engine_work = likely_gc_or_engine_work and _frame_profile_pcm_frames == 0
	likely_gc_or_engine_work = likely_gc_or_engine_work and _frame_profile_json_parse_count == 0
	likely_gc_or_engine_work = likely_gc_or_engine_work and _frame_profile_sync_work_ms < 1.0
	likely_gc_or_engine_work = likely_gc_or_engine_work and _frame_profile_audio_push_ms < 1.0
	likely_gc_or_engine_work = likely_gc_or_engine_work and _frame_profile_websocket_work_ms < 1.0
	print("Voice frame profile: reason=%s state=%s phase=%s frameMs=%.2f avgMs=%.2f maxMs=%.2f frames=%s over16=%s over33=%s over50=%s over100=%s httpPolled=%s bytes=%s pcmFrames=%s jsonParses=%s jsonMs=%.2f largeCopies=%s largeCopyBytes=%s syncWorkMs=%.2f syncLabel=%s audioPushMs=%.2f websocketPackets=%s websocketWorkMs=%.2f likelyGcOrEngineWork=%s" % [
		reason,
		_merlin_state_name(_merlin_state),
		_voice_phase,
		frame_ms,
		average_ms,
		_frame_profile_max_ms,
		_frame_profile_frame_count,
		_frame_profile_over_16,
		_frame_profile_over_33,
		_frame_profile_over_50,
		_frame_profile_over_100,
		str(_frame_profile_http_polled),
		_frame_profile_bytes,
		_frame_profile_pcm_frames,
		_frame_profile_json_parse_count,
		_frame_profile_json_parse_ms,
		_frame_profile_large_copy_count,
		_frame_profile_large_copy_bytes,
		_frame_profile_sync_work_ms,
		_frame_profile_sync_work_label,
		_frame_profile_audio_push_ms,
		_frame_profile_websocket_packets,
		_frame_profile_websocket_work_ms,
		str(likely_gc_or_engine_work)
	])


func _reset_frame_profile_window(now_usec: int) -> void:
	_frame_profile_window_started_usec = now_usec
	_frame_profile_last_report_usec = now_usec
	_frame_profile_frame_count = 0
	_frame_profile_total_ms = 0.0
	_frame_profile_max_ms = 0.0
	_frame_profile_over_16 = 0
	_frame_profile_over_33 = 0
	_frame_profile_over_50 = 0
	_frame_profile_over_100 = 0
	_frame_profile_http_polled = false
	_frame_profile_bytes = 0
	_frame_profile_pcm_frames = 0
	_frame_profile_json_parse_count = 0
	_frame_profile_json_parse_ms = 0.0
	_frame_profile_large_copy_count = 0
	_frame_profile_large_copy_bytes = 0
	_frame_profile_sync_work_ms = 0.0
	_frame_profile_sync_work_label = ""
	_frame_profile_audio_push_ms = 0.0
	_frame_profile_websocket_packets = 0
	_frame_profile_websocket_work_ms = 0.0


func _record_http_poll(started_usec: int) -> void:
	_frame_profile_http_polled = true
	_record_sync_work("http_poll", started_usec)


func _record_bytes_processed(bytes: int) -> void:
	_frame_profile_bytes += maxi(bytes, 0)


func _record_pcm_frames_pushed(frames: int) -> void:
	_frame_profile_pcm_frames += maxi(frames, 0)


func _record_json_parse(started_usec: int) -> void:
	_frame_profile_json_parse_count += 1
	_frame_profile_json_parse_ms += _elapsed_ms_since(started_usec)


func _record_large_copy(bytes: int) -> void:
	if bytes < 16384:
		return
	_frame_profile_large_copy_count += 1
	_frame_profile_large_copy_bytes += bytes


func _record_sync_work(label: String, started_usec: int) -> void:
	var elapsed_ms := _elapsed_ms_since(started_usec)
	_frame_profile_sync_work_ms += elapsed_ms
	if elapsed_ms >= 1.0:
		_frame_profile_sync_work_label = label if _frame_profile_sync_work_label.is_empty() else "%s,%s" % [_frame_profile_sync_work_label, label]


func _record_audio_push(frames: int, started_usec: int) -> void:
	_record_pcm_frames_pushed(frames)
	_frame_profile_audio_push_ms += _elapsed_ms_since(started_usec)


func _on_frontend_work_observed(metrics: Dictionary) -> void:
	if bool(metrics.get("http_polled", false)):
		_frame_profile_http_polled = true
	_frame_profile_bytes += int(metrics.get("bytes", 0))
	_frame_profile_json_parse_count += int(metrics.get("json_parse_count", 0))
	_frame_profile_json_parse_ms += float(metrics.get("json_parse_ms", 0.0))
	_frame_profile_websocket_packets += int(metrics.get("packets", 0))
	_frame_profile_websocket_work_ms += float(metrics.get("work_ms", 0.0))


func _merlin_state_name(state: int) -> String:
	match state:
		MerlinState.IDLE:
			return "IDLE"
		MerlinState.THINKING:
			return "THINKING"
		MerlinState.SPEAKING:
			return "SPEAKING"
		MerlinState.EXECUTING_TOOL:
			return "EXECUTING_TOOL"
		MerlinState.ERROR:
			return "ERROR"
		MerlinState.LISTENING:
			return "LISTENING"
		MerlinState.MEMORY_UPDATE:
			return "MEMORY_UPDATE"
		MerlinState.UPDATING:
			return "UPDATING"
		MerlinState.LOADING_MODEL:
			return "LOADING_MODEL"
		_:
			return "UNKNOWN"


func _prepare_orb_for_response(response: Dictionary, success: bool, response_type: String) -> void:
	var has_confirmation := typeof(response.get("confirmation", null)) == TYPE_DICTIONARY
	if has_confirmation:
		activity_label.text = "Waiting for confirmation"
		core_orb.play_confirmation()
		await get_tree().create_timer(0.28).timeout
		return

	if response_type == "error" or (not success and not has_confirmation and response_type != "limitation" and response_type != "safety"):
		_set_merlin_state(MerlinState.ERROR)
		await get_tree().create_timer(0.28).timeout
		return

	if success and _is_tool_execution_response(response):
		_set_merlin_state(MerlinState.EXECUTING_TOOL)
		await get_tree().create_timer(0.36).timeout


func _settle_orb_after_response() -> void:
	if _pending_requests.is_empty():
		_set_merlin_state(MerlinState.IDLE)
		_set_voice_phase("idle")
	else:
		_set_merlin_state(MerlinState.THINKING)
		_set_voice_phase("waiting_llm")


func _response_kind(response: Dictionary, success: bool, response_type: String) -> String:
	if typeof(response.get("confirmation", null)) == TYPE_DICTIONARY:
		return "confirmation"

	match response_type:
		"limitation":
			return "limitation"
		"safety":
			return "safety"
		"system":
			return "system"
		"error":
			return "error"
		_:
			return "assistant" if success else "error"


func _format_connection_state(state: String, detail: String) -> String:
	match state:
		"connected":
			return "connected"
		"connecting":
			return "connecting..."
		"error":
			return "error"
		"disconnected":
			return "disconnected"
		_:
			return state if detail.is_empty() else "%s - %s" % [state, detail]


func _format_debug_info(response: Dictionary) -> String:
	var lines := PackedStringArray()
	var correlation_id := str(response.get("correlationId", ""))
	var tool_name = response.get("toolName", null)
	var intent = response.get("intent", null)
	var capability_id = response.get("capabilityId", null)
	var capability_name = response.get("capabilityName", null)
	var response_type = response.get("responseType", null)
	var error_code = response.get("errorCode", null)
	var parser_used = response.get("parserUsed", null)

	if not correlation_id.is_empty():
		lines.append("correlationId: %s" % correlation_id)
	if tool_name != null:
		lines.append("toolName: %s" % str(tool_name))
	if intent != null:
		lines.append("intent: %s" % str(intent))
	if capability_id != null:
		lines.append("capabilityId: %s" % str(capability_id))
	if capability_name != null:
		lines.append("capabilityName: %s" % str(capability_name))
	if response_type != null:
		lines.append("responseType: %s" % str(response_type))
	if error_code != null:
		lines.append("errorCode: %s" % str(error_code))
	if parser_used != null:
		lines.append("parserUsed: %s" % str(parser_used))

	return "\n".join(lines)


func _add_user_message(message: String) -> void:
	_add_chat_line("You", message, "", "user")


func _add_assistant_message(message: String, debug_text: String = "") -> void:
	_add_chat_line("Merlin", message, debug_text, "assistant")


func _add_error_message(message: String, debug_text: String = "") -> void:
	_add_chat_line("Error", message, debug_text, "error")


func _add_system_message(message: String) -> void:
	_add_chat_line("System", message, "", "system")


func _add_notification(message: String, kind: String = "system") -> void:
	var text := message.strip_edges()
	if text.is_empty():
		return

	var notification := PanelContainer.new()
	notification.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	notification.focus_mode = Control.FOCUS_NONE
	notification.add_theme_stylebox_override("panel", _panel_style(Color(0.010, 0.046, 0.115, 0.62), _message_color(kind), 1.0, 6))

	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 10)
	margin.add_theme_constant_override("margin_top", 7)
	margin.add_theme_constant_override("margin_right", 10)
	margin.add_theme_constant_override("margin_bottom", 7)
	notification.add_child(margin)

	var label := Label.new()
	label.focus_mode = Control.FOCUS_NONE
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.text = text
	label.add_theme_color_override("font_color", _message_color(kind))
	label.add_theme_font_size_override("font_size", 12)
	margin.add_child(label)

	notification_list.add_child(notification)
	while notification_list.get_child_count() > MAX_NOTIFICATIONS:
		var oldest := notification_list.get_child(0)
		notification_list.remove_child(oldest)
		oldest.queue_free()


func _add_chat_line(author: String, message: String, debug_text: String, kind: String) -> void:
	var label := _create_chat_line(author, message, debug_text, kind)
	await get_tree().process_frame
	_scroll_messages_to_bottom()


func _create_chat_line(author: String, message: String, debug_text: String, kind: String) -> RichTextLabel:
	var container := VBoxContainer.new()
	container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	container.focus_mode = Control.FOCUS_NONE
	container.add_theme_constant_override("separation", 3)

	var label := _create_selectable_text("%s: %s" % [author, message], _message_color(kind))
	container.add_child(label)

	if not debug_text.is_empty():
		var debug_label := _create_selectable_text(debug_text, COLOR_MUTED, 12)
		debug_label.visible = show_debug_check_box.button_pressed
		debug_label.set_meta("debug_label", true)
		container.add_child(debug_label)

	message_list.add_child(container)
	return label


func _add_typed_chat_line(author: String, message: String, debug_text: String, kind: String) -> void:
	var label := _create_chat_line(author, "", debug_text, kind)
	await _typewriter_reveal(label, author, message)
	await get_tree().process_frame
	_scroll_messages_to_bottom()


func _typewriter_reveal(label: RichTextLabel, author: String, message: String) -> void:
	var visible_text := ""
	var character_delay := 1.0 / TYPEWRITER_CHARS_PER_SECOND
	var time_budget := character_delay
	var last_ticks_usec := Time.get_ticks_usec()
	var index := 0
	var frames_since_scroll := 0
	_set_merlin_state(MerlinState.SPEAKING)

	while index < message.length():
		var now_ticks_usec := Time.get_ticks_usec()
		time_budget += float(now_ticks_usec - last_ticks_usec) / 1000000.0
		last_ticks_usec = now_ticks_usec

		var revealed_count := 0
		while index < message.length():
			var next_character := message.substr(index, 1)
			var next_delay := _typewriter_delay_for_character(next_character)
			var reveal_delay := next_delay if next_delay > 0.0 else character_delay
			if time_budget < reveal_delay:
				break
			time_budget -= reveal_delay

			visible_text += next_character
			index += 1
			revealed_count += 1

		if revealed_count > 0:
			label.text = "%s: %s" % [author, visible_text]
			frames_since_scroll += 1
			if frames_since_scroll >= 4 or index >= message.length():
				_scroll_messages_to_bottom()
				frames_since_scroll = 0

		await get_tree().process_frame

	_scroll_messages_to_bottom()
	_settle_orb_after_response()


func _typewriter_delay_for_character(character: String) -> float:
	match character:
		".", ",", "!", "?", ":", ";":
			return TYPEWRITER_PUNCTUATION_DELAY
		"\n":
			return TYPEWRITER_PARAGRAPH_DELAY
		_:
			return 1.0 / TYPEWRITER_CHARS_PER_SECOND


func _scroll_messages_to_bottom() -> void:
	message_scroll.scroll_vertical = int(message_scroll.get_v_scroll_bar().max_value)


func _create_selectable_text(text: String, color: Color, font_size: int = 0) -> RichTextLabel:
	var label := RichTextLabel.new()
	label.focus_mode = Control.FOCUS_NONE
	label.bbcode_enabled = false
	label.fit_content = true
	label.scroll_active = false
	label.selection_enabled = true
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.text = text
	label.add_theme_color_override("default_color", color)
	if font_size > 0:
		label.add_theme_font_size_override("normal_font_size", font_size)

	return label


func _update_pending_state() -> void:
	var has_pending_requests := not _pending_requests.is_empty()
	thinking_label.visible = has_pending_requests
	_update_send_button()
	if not has_pending_requests and web_socket_client.is_backend_connected():
		_focus_message_input()


func _update_orb_from_response(response: Dictionary, success: bool, response_type: String) -> void:
	if response_type == "error" or not success and response_type != "limitation" and response_type != "safety":
		_set_merlin_state(MerlinState.ERROR)
		return

	if success and _is_tool_execution_response(response):
		_set_merlin_state(MerlinState.EXECUTING_TOOL)
		return

	if _pending_requests.is_empty():
		_set_merlin_state(MerlinState.IDLE)
	else:
		_set_merlin_state(MerlinState.THINKING)


func _is_tool_execution_response(response: Dictionary) -> bool:
	var tool_name := str(response.get("toolName", ""))
	var intent := str(response.get("intent", ""))
	if tool_name.is_empty():
		return false

	return tool_name != "General Conversation" or intent == "system_resource_query"


func _set_merlin_state(state: int) -> void:
	_merlin_state = state
	activity_label.text = _activity_text_for_state(state)
	match state:
		MerlinState.THINKING:
			core_orb.set_thinking()
		MerlinState.LISTENING:
			core_orb.set_listening()
		MerlinState.SPEAKING:
			core_orb.set_speaking()
		MerlinState.EXECUTING_TOOL:
			core_orb.play_tool_execution()
		MerlinState.ERROR:
			core_orb.play_error()
		_:
			core_orb.set_idle()


func _activity_text_for_state(state: int) -> String:
	match state:
		MerlinState.THINKING:
			return "Merlin is thinking"
		MerlinState.LISTENING:
			return "Merlin is listening"
		MerlinState.SPEAKING:
			return "Merlin is speaking"
		MerlinState.EXECUTING_TOOL:
			return "Executing verified tool action"
		MerlinState.ERROR:
			return "Attention required"
		_:
			return "Merlin is standing by"


func _update_send_button() -> void:
	var connected := web_socket_client.is_backend_connected()
	send_button.disabled = not connected
	voice_button.disabled = not connected or _is_recording
	reconnect_button.disabled = web_socket_client.get_connection_state() == "connecting"


func _focus_message_input() -> void:
	if not is_instance_valid(message_input) or not message_input.visible:
		return
	_focus_request_id += 1
	call_deferred("_apply_message_input_focus", _focus_request_id)


func _apply_message_input_focus(request_id: int) -> void:
	await get_tree().process_frame
	await get_tree().process_frame
	if request_id != _focus_request_id:
		return

	if not is_instance_valid(message_input):
		return

	message_input.focus_mode = Control.FOCUS_ALL
	message_input.grab_focus()
	message_input.caret_column = message_input.text.length()


func _show_error(message: String) -> void:
	error_label.text = message
	error_label.visible = not message.is_empty()


func _clear_error() -> void:
	error_label.text = ""
	error_label.visible = false

func _on_show_debug_check_box_toggled(enabled: bool) -> void:
	_set_debug_labels_visible(message_list, enabled)


func _set_debug_labels_visible(node: Node, enabled: bool) -> void:
	for child in node.get_children():
		if child.has_meta("debug_label"):
			child.visible = enabled
		_set_debug_labels_visible(child, enabled)


func _message_color(kind: String) -> Color:
	match kind:
		"user":
			return COLOR_WHITE
		"assistant":
			return COLOR_CYAN
		"limitation":
			return COLOR_BLUE
		"safety":
			return COLOR_AMBER
		"confirmation":
			return COLOR_AMBER
		"error":
			return COLOR_RED
		"system":
			return COLOR_MUTED
		_:
			return COLOR_MUTED


func _generate_correlation_id() -> String:
	return "%s-%s-%s" % [
		Time.get_unix_time_from_system(),
		Time.get_ticks_usec(),
		randi()
	]


func _join_values(values: Array, separator: String) -> String:
	var parts := PackedStringArray()
	for value in values:
		parts.append(str(value))

	return separator.join(parts)
