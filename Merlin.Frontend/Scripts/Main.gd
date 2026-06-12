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
const VOICE_WARMUP_URL := "http://localhost:5000/api/voice/warmup"
const VOICE_SYNTHESIS_URL := "http://localhost:5000/api/voice/synthesize"
const RECORD_BUS_NAME := "MerlinRecord"
const VOICE_RESPONSE_PATH := "user://merlin-response.wav"
const SPEECH_CHUNK_TARGET_CHARS := 260

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
var _voice_warmup_complete := false
var _voice_warmup_in_progress := false
var _speech_turn_active := false


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

	_add_system_message("Connecting to Merlin.Backend...")
	_add_notification("Connecting to Merlin.Backend", "system")
	_update_pending_state()
	web_socket_client.connect_to_backend()
	_warmup_voice_worker()
	_focus_message_input()


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
	_set_merlin_state(MerlinState.LISTENING)
	_record_effect.set_recording_active(true)


func _on_voice_button_up() -> void:
	if not _is_recording or _record_effect == null:
		return

	_is_recording = false
	voice_button.disabled = true
	voice_button.text = "Transcribing..."
	_record_effect.set_recording_active(false)
	var recording := _record_effect.get_recording()
	await _send_recording_for_transcription(recording)


func _send_recording_for_transcription(recording: AudioStreamWAV) -> void:
	if recording == null:
		_add_notification("No audio captured", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.IDLE)
		return

	var path := "user://merlin-recording.wav"
	var save_error := recording.save_to_wav(path)
	if save_error != OK:
		_show_error("Could not save microphone recording. Error code: %s" % save_error)
		_add_notification("Recording failed", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.ERROR)
		return

	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		_show_error("Could not read microphone recording.")
		_add_notification("Recording failed", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.ERROR)
		return

	var audio_bytes := file.get_buffer(file.get_length())
	file.close()
	if audio_bytes.size() < 2048:
		_show_error("Microphone recording was empty or too small. Check Godot microphone permission and input device.")
		_add_notification("No microphone audio captured", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.ERROR)
		return

	_add_notification("Captured %.1f KB of microphone audio" % (float(audio_bytes.size()) / 1024.0), "system")
	var request_error := voice_transcribe_request.request_raw(
		VOICE_TRANSCRIBE_URL,
		PackedStringArray(["Content-Type: audio/wav"]),
		HTTPClient.METHOD_POST,
		audio_bytes
	)
	if request_error != OK:
		_show_error("Could not send audio to Merlin.Backend. Error code: %s" % request_error)
		_add_notification("Transcription failed", "error")
		_reset_voice_button()
		_set_merlin_state(MerlinState.ERROR)
		return

	var result = await voice_transcribe_request.request_completed
	var request_result: int = int(result[0])
	var response_code: int = int(result[1])
	var body: PackedByteArray = result[3]
	var response_text := body.get_string_from_utf8()
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

	var parsed = JSON.parse_string(response_text)
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
	_show_question_acknowledgement(transcript)
	_reset_voice_button()


func _reset_voice_button() -> void:
	voice_button.disabled = false
	voice_button.text = "Hold to talk"


func _warmup_voice_worker() -> void:
	if _voice_warmup_complete or _voice_warmup_in_progress:
		return

	_voice_warmup_in_progress = true
	var request := HTTPRequest.new()
	add_child(request)
	var request_error := request.request(
		VOICE_WARMUP_URL,
		PackedStringArray(["Content-Type: application/json"]),
		HTTPClient.METHOD_POST,
		"{}"
	)
	if request_error != OK:
		_voice_warmup_in_progress = false
		request.queue_free()
		_add_notification("Voice warmup request failed", "error")
		return

	var result = await request.request_completed
	_voice_warmup_in_progress = false
	request.queue_free()
	var response_code: int = int(result[1])
	if response_code >= 200 and response_code < 300:
		_voice_warmup_complete = true
		_add_notification("Voice warmed", "system")
	else:
		_add_notification("Voice warmup failed", "error")


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
			if not _voice_warmup_complete:
				_warmup_voice_worker()
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
	await _speak_text_unlocked(spoken_text)
	_speech_turn_active = false
	_settle_orb_after_response()


func _speak_text_unlocked(spoken_text: String) -> void:
	if spoken_text.is_empty():
		return

	_set_merlin_state(MerlinState.SPEAKING)
	var chunks := _split_spoken_text(spoken_text)
	var current_request := _begin_speech_synthesis(chunks[0])
	if current_request == null:
		return

	for index in range(chunks.size()):
		var audio := await _finish_speech_synthesis(current_request)
		if audio.is_empty():
			return

		var next_request: HTTPRequest = null
		if index + 1 < chunks.size():
			next_request = _begin_speech_synthesis(chunks[index + 1])

		var played := await _play_speech_audio(audio)
		if not played:
			if next_request != null:
				next_request.queue_free()
			return

		current_request = next_request


func _begin_speech_synthesis(text: String) -> HTTPRequest:
	var request := HTTPRequest.new()
	add_child(request)
	var payload := JSON.stringify({ "text": text })
	var request_error := request.request(
		VOICE_SYNTHESIS_URL,
		PackedStringArray(["Content-Type: application/json"]),
		HTTPClient.METHOD_POST,
		payload
	)
	if request_error != OK:
		request.queue_free()
		_show_error("Could not request speech synthesis. Error code: %s" % request_error)
		_add_notification("Speech synthesis failed", "error")
		return null

	return request


func _finish_speech_synthesis(request: HTTPRequest) -> PackedByteArray:
	if request == null:
		return PackedByteArray()

	var result = await request.request_completed
	request.queue_free()
	var response_code: int = int(result[1])
	var body: PackedByteArray = result[3]
	if response_code < 200 or response_code >= 300:
		_show_error("Speech synthesis failed. HTTP %s" % response_code)
		_add_notification("Speech synthesis failed", "error")
		return PackedByteArray()
	if body.size() < 44:
		_show_error("Speech synthesis returned an empty audio response.")
		_add_notification("Speech playback failed", "error")
		return PackedByteArray()

	return body


func _play_speech_audio(body: PackedByteArray) -> bool:
	var stream := _load_synthesized_wav(body)
	if stream == null:
		_show_error("Could not load synthesized speech.")
		_add_notification("Speech playback failed", "error")
		return false

	voice_playback.stream = stream
	voice_playback.bus = "Master"
	voice_playback.volume_db = 0.0
	voice_playback.play()
	await voice_playback.finished
	return true


func _split_spoken_text(text: String) -> PackedStringArray:
	var chunks := PackedStringArray()
	var current := ""
	var normalized := text.replace("\r\n", "\n").replace("\r", "\n")
	for index in range(normalized.length()):
		var character := normalized.substr(index, 1)
		current += " " if character == "\n" else character
		if character in [".", "!", "?", "\n"]:
			_append_spoken_chunk(chunks, current)
			current = ""

	_append_spoken_chunk(chunks, current)

	if chunks.is_empty():
		chunks.append(text)
	return chunks


func _append_spoken_chunk(chunks: PackedStringArray, text: String) -> void:
	var chunk := text.strip_edges()
	if chunk.is_empty():
		return
	if chunk.length() <= SPEECH_CHUNK_TARGET_CHARS:
		chunks.append(chunk)
		return

	for hard_chunk in _split_long_spoken_part(chunk):
		chunks.append(hard_chunk)


func _split_long_spoken_part(text: String) -> PackedStringArray:
	var chunks := PackedStringArray()
	var words := text.split(" ", false)
	var current := ""
	for word in words:
		if current.length() + word.length() + 1 > SPEECH_CHUNK_TARGET_CHARS and not current.is_empty():
			chunks.append(current.strip_edges())
			current = ""
		current = word if current.is_empty() else "%s %s" % [current, word]
	if not current.strip_edges().is_empty():
		chunks.append(current.strip_edges())
	return chunks


func _load_synthesized_wav(body: PackedByteArray) -> AudioStreamWAV:
	var file := FileAccess.open(VOICE_RESPONSE_PATH, FileAccess.WRITE)
	if file == null:
		return _parse_pcm_wav(body)

	file.store_buffer(body)
	file.close()

	var stream := AudioStreamWAV.load_from_file(VOICE_RESPONSE_PATH)
	if stream != null and stream.get_length() > 0.0:
		return stream

	return _parse_pcm_wav(body)


func _parse_pcm_wav(body: PackedByteArray) -> AudioStreamWAV:
	if body.size() < 44:
		return null
	if body.slice(0, 4).get_string_from_ascii() != "RIFF" or body.slice(8, 12).get_string_from_ascii() != "WAVE":
		return null

	var offset := 12
	var audio_format := 0
	var channels := 0
	var sample_rate := 0
	var bits_per_sample := 0
	var data_offset := -1
	var data_size := 0

	while offset + 8 <= body.size():
		var chunk_id := body.slice(offset, offset + 4).get_string_from_ascii()
		var chunk_size := int(body.decode_u32(offset + 4))
		var chunk_data_offset := offset + 8
		if chunk_data_offset + chunk_size > body.size():
			break

		match chunk_id:
			"fmt ":
				if chunk_size >= 16:
					audio_format = int(body.decode_u16(chunk_data_offset))
					channels = int(body.decode_u16(chunk_data_offset + 2))
					sample_rate = int(body.decode_u32(chunk_data_offset + 4))
					bits_per_sample = int(body.decode_u16(chunk_data_offset + 14))
			"data":
				data_offset = chunk_data_offset
				data_size = chunk_size

		offset = chunk_data_offset + chunk_size
		if offset % 2 == 1:
			offset += 1

	if audio_format != 1 or data_offset < 0 or data_size <= 0 or sample_rate <= 0:
		return null
	if channels < 1 or channels > 2:
		return null
	if bits_per_sample != 8 and bits_per_sample != 16:
		return null

	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_8_BITS if bits_per_sample == 8 else AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = sample_rate
	stream.stereo = channels == 2
	stream.data = body.slice(data_offset, data_offset + data_size)
	return stream


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
	else:
		_set_merlin_state(MerlinState.THINKING)


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
