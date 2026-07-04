extends Control

const MerlinWindowScript := preload("res://Scripts/UI/Windows/MerlinWindow.gd")
const MerlinWindowManagerScript := preload("res://Scripts/UI/Windows/MerlinWindowManager.gd")
const MerlinWindowDefinitionScript := preload("res://Scripts/UI/Windows/MerlinWindowDefinition.gd")
const MerlinWindowConstantsScript := preload("res://Scripts/UI/Windows/MerlinWindowConstants.gd")
const CrumpleSheetProxyScript := preload("res://Scripts/UI/Windows/CrumpleSheetProxy.gd")
const ChatLogContentScript := preload("res://Scripts/UI/ChatLog/ChatLogContent.gd")

enum MerlinState {
	SLEEPING,
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
const CHATLOG_DEFAULT_WIDTH := 420.0
const CHATLOG_DEFAULT_HEIGHT := 720.0
const CHATLOG_MIN_WIDTH := 280.0
const CHATLOG_MIN_HEIGHT := 220.0
const CHATLOG_MAX_WIDTH := 900.0
const CHATLOG_MAX_HEIGHT := 1100.0
const CHATLOG_INITIAL_LEFT := 24.0
const CHATLOG_INITIAL_TOP := 76.0
const GESTURE_CURSOR_SIZE := 34.0
const GESTURE_CURSOR_CANVAS_LAYER := 128
const GESTURE_POINTER_PRIMARY := "primary"
const GESTURE_POINTER_SECONDARY := "secondary"
const PINCH_TAP_MAX_DURATION_MS := 220
const PINCH_TAP_MAX_MOVEMENT_PX := 18.0
const PINCH_HOLD_TO_GRAB_MS := 260
const DRAG_START_MOVEMENT_PX := 20.0
const GESTURE_MIN_RESIZE_START_DISTANCE_PX := 56.0
const GESTURE_MIN_RESIZE_AXIS_DISTANCE_PX := 40.0
const GESTURE_DIAGONAL_RESIZE_RATIO := 0.45
const GESTURE_RESIZE_AXIS_WIDTH := "width"
const GESTURE_RESIZE_AXIS_HEIGHT := "height"
const GESTURE_RESIZE_AXIS_BOTH := "both"
const GESTURE_RESIZE_SMOOTHING := 0.25
const BALL_FORM_TARGET_PADDING_PX := 180.0
const BALL_FORM_MIN_DURATION_MS := 450
const BALL_FORM_CANCEL_TIMEOUT_MS := 3200
const BALL_FORM_MIN_MOTION_PX := 180.0
const BALL_FORM_MIN_CONFIDENCE := 1.0
const BALL_FORM_COMPACT_RATIO := 0.72
const BALL_FORM_THROW_LOCKOUT_MS := 220
const BALL_FORM_TWO_HAND_FRESH_MS := 260
const THROW_VELOCITY_THRESHOLD_PX_PER_SEC := 980.0
const THROW_HISTORY_WINDOW_MS := 180
const THROW_RIGHT_SWIPE_HISTORY_WINDOW_MS := 320
const THROW_RIGHT_SWIPE_MIN_DISTANCE_PX := 360.0
const THROW_RIGHT_SWIPE_MIN_X_VELOCITY_PX_PER_SEC := 650.0
const THROW_RIGHT_SWIPE_MAX_VERTICAL_RATIO := 0.62
const THROW_ANIMATION_MIN_MS := 850
const THROW_ANIMATION_MAX_MS := 1250
const VOICE_TRANSCRIBE_URL := "http://localhost:5000/api/voice/transcribe?extension=.wav"
const VOICE_SYNTHESIS_STREAM_HOST := "localhost"
const VOICE_SYNTHESIS_STREAM_PORT := 5000
const VOICE_SYNTHESIS_STREAM_PATH := "/api/voice/synthesize-stream"
const VOICE_STREAM_POC_HOST := "localhost"
const VOICE_STREAM_POC_PORT := 5000
const VOICE_STREAM_POC_PATH := "/api/voice/stream-pcm-test"
const VOICE_GENERATOR_BUFFER_SECONDS := 0.50
const VOICE_OUTPUT_DRAIN_SECONDS := 0.12
const VOICE_SPEECH_TICK_FRAMES := 2048
const RECORD_BUS_NAME := "MerlinRecord"
const FRAME_PROFILER_ENABLED := false
const FRAME_PROFILER_REPORT_SECONDS := 1.0
const FRAME_PROFILER_SPIKE_MS := 33.0
const WAKE_RECORD_IDLE_WINDOW_SECONDS := 4.2
const WAKE_RECORD_POLL_SECONDS := 0.20
const WAKE_RECORD_MIN_ACTIVE_SECONDS := 1.2
const WAKE_RECORD_END_SILENCE_SECONDS := 1.4
const WAKE_RECORD_SLEEP_MAX_ACTIVE_SECONDS := 16.0
const WAKE_RECORD_RESTART_DELAY_SECONDS := 0.08
const WAKE_ARMED_LISTEN_SECONDS := 5.0
const WAKE_ARMED_PREROLL_SECONDS := 1.00
const WAKE_MIN_AUDIO_BYTES := 4096
const WAKE_MIN_RMS := 0.010
const WAKE_SPEECH_RMS := 0.010
const WAKE_SPEECH_PEAK := 0.045
const WAKE_RECENT_WINDOW_SECONDS := 0.35
const VOICE_STREAM_CHUNK_FRAMES := 2048
const VOICE_STREAM_PREROLL_SECONDS := 0.50
const FRONTEND_VOICE_INPUT_ENABLED := false
const WAKE_CLAP_RMS_THRESHOLD := 0.080
const WAKE_CLAP_PEAK_THRESHOLD := 0.18
const WAKE_CLAP_PEAK_TO_RMS_RATIO := 2.2
const WAKE_CLAP_MIN_GAP_SECONDS := 0.12
const WAKE_CLAP_MAX_GAP_SECONDS := 1.35
const MERLIN_AWAKE_TIMEOUT_SECONDS := 600.0

const COLOR_BACKGROUND := Color(0.000, 0.008, 0.026, 1.0)
const COLOR_PANEL := Color(0.002, 0.024, 0.070, 0.40)
const COLOR_PANEL_DARK := Color(0.001, 0.014, 0.044, 0.64)
const COLOR_BLUE := Color(0.08, 0.42, 1.00, 1.0)
const COLOR_CYAN := Color(0.24, 0.72, 1.0, 1.0)
const COLOR_WHITE := Color(0.88, 0.96, 1.0, 1.0)
const COLOR_MUTED := Color(0.50, 0.62, 0.70, 1.0)
const COLOR_AMBER := Color(1.0, 0.68, 0.28, 1.0)
const COLOR_RED := Color(1.0, 0.28, 0.34, 1.0)
const VISUAL_OVERLAY_FADE_SECONDS := 1.2
const VISUAL_OVERLAY_CONFIRMATION_HOLD_SECONDS := 2.0
const VOICE_ACKNOWLEDGEMENT_MAX_WORDS := 36
const BARGE_IN_DEBUG_OVERLAY_SCRIPT := preload("res://Scripts/BargeInDebugOverlay.gd")

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
@onready var overlay_container: Control = $OverlayContainer
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
var _merlin_state: int = MerlinState.SLEEPING
var _last_assistant_ui_state_sequence := -1
var _canonical_ui_state_active := false
var _focus_request_id := 0
var _record_effect: AudioEffectRecord
var _capture_effect: AudioEffectCapture
var _is_recording := false
var _wake_listening_enabled := false
var _wake_cycle_active := false
var _merlin_awake := false
var _last_merlin_activity_usec := 0
var _sleep_clap_times: Array[float] = []
var _last_sleep_capture_debug_usec := 0
var _record_bus_index := -1
var _speech_turn_active := false
var _speaking_startup_profile_started_usec := 0
var _speaking_startup_profile_energy_count := 0
var _speaking_startup_profile_first_energy_logged := false
var _visual_overlay_kind := ""
var _visual_overlay_strength := 0.0
var _visual_overlay_target := 0.0
var _visual_overlay_hold_until_usec := 0
var _visual_overlay_hold_until_speech_end := false
var _visual_overlay_waiting_for_confirmation := false
var _application_choice_panel: PanelContainer
var _window_manager
var _chatlog_panel
var _chatlog_content
var _ui_control_mode_active := false
var _gesture_cursor_layer: CanvasLayer
var _gesture_cursor: PanelContainer
var _gesture_visual_pointer_id := ""
var _gesture_pointer_positions := {}
var _gesture_pointer_pinched := {}
var _gesture_pending_pinches := {}
var _gesture_pointer_history := {}
var _gesture_grabs := {}
var _gesture_hover_surface_id := ""
var _gesture_hover_surface_by_pointer := {}
var _gesture_resize_state := {}
var _selected_surface_id := ""
var _crumple_state := {}
var _crumple_proxy
var _last_throw_velocity := Vector2.ZERO
var _gesture_ignore_logged := false
var _voice_turn_started_usec := 0
var _voice_stream_active := false
var _voice_stream_correlation_id := ""
var _voice_stream_sample_rate := 48000
var _voice_stream_chunks_sent := 0
var _voice_stream_bytes_sent := 0
var _voice_stream_preroll_frames := PackedVector2Array()
var _wake_armed_preroll_frames := PackedVector2Array()
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
var _frame_profile_pcm_convert_ms := 0.0
var _frame_profile_audio_push_ms := 0.0
var _frame_profile_websocket_packets := 0
var _frame_profile_websocket_work_ms := 0.0
var _barge_in_debug_overlay: BargeInDebugOverlay


func _ready() -> void:
	_apply_visual_theme()
	_setup_voice_mode()
	_set_merlin_state(MerlinState.SLEEPING)
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
	_setup_chatlog_panel()
	_setup_gesture_cursor()

	web_socket_client.connection_state_changed.connect(_profiled_connection_state_changed)
	web_socket_client.visual_state_received.connect(_profiled_visual_state_received)
	web_socket_client.assistant_ui_state_received.connect(_profiled_assistant_ui_state_received)
	web_socket_client.response_received.connect(_profiled_response_received)
	web_socket_client.voice_transcript_received.connect(_profiled_voice_transcript_received)
	web_socket_client.visual_event_received.connect(_profiled_visual_event_received)
	web_socket_client.barge_in_debug_snapshot_received.connect(_profiled_barge_in_debug_snapshot_received)
	web_socket_client.malformed_response.connect(_profiled_malformed_response)
	web_socket_client.socket_closed.connect(_profiled_socket_closed)
	web_socket_client.frontend_work_observed.connect(_profiled_frontend_work_observed)

	_add_system_message("Connecting to Merlin.Backend...")
	_add_notification("Connecting to Merlin.Backend", "system")
	_update_pending_state()
	web_socket_client.connect_to_backend()
	_focus_message_input()


func _input(event: InputEvent) -> void:
	if _handle_synthetic_gesture_input(event):
		get_viewport().set_input_as_handled()


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_F9:
		_start_streaming_pcm_poc()
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_F10:
		_toggle_backend_stress_fake_mode()


func _process(delta: float) -> void:
	_frame_profile_begin_frame()
	_update_merlin_awake_timeout()
	_update_visual_overlay(delta)
	_update_pending_gesture_pinches()
	_update_selected_ball_forming()
	_update_crumple_timeout()
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


func _toggle_backend_stress_fake_mode() -> void:
	if web_socket_client == null or not web_socket_client.has_method("set_backend_stress_fake_mode"):
		return
	var enabled := not bool(web_socket_client.call("is_backend_stress_fake_mode_enabled"))
	web_socket_client.call("set_backend_stress_fake_mode", enabled)
	_add_notification("Backend stress fake mode %s" % ("on" if enabled else "off"), "system")



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
	_mark_merlin_activity()
	if show_user_message:
		_add_user_message(message)
		message_input.clear()

	_clear_transient_visual_overlay()
	_update_pending_state()
	if _legacy_state_changes_enabled():
		_set_merlin_state(MerlinState.THINKING)
	_clear_error()

	var interaction_source := "text" if show_user_message else "voice"
	var client_mode := "chat" if chat_panel.visible else "orb"
	var sent := web_socket_client.send_message(message, correlation_id, interaction_source, client_mode)
	if not sent:
		_pending_requests.erase(correlation_id)
		_add_system_message("Message was not sent.")
		_add_notification("Message was not sent", "error")
		_set_merlin_state(MerlinState.ERROR)
		_update_pending_state()
	elif not show_user_message:
		_add_notification("Sent to Merlin.Backend", "system")

	_focus_message_input()


func _send_application_choice(display_name: String) -> void:
	var choice := display_name.strip_edges()
	if choice.is_empty():
		return
	_hide_application_choice_panel()
	_send_backend_message(choice, true)


func _setup_chatlog_panel() -> void:
	if is_instance_valid(_chatlog_panel):
		return

	if not is_instance_valid(_window_manager):
		_window_manager = MerlinWindowManagerScript.new()
		_window_manager.name = "MerlinWindowManager"
		add_child(_window_manager)

	var definition := MerlinWindowDefinitionScript.new()
	definition.apply_chatlog_defaults()
	_window_manager.register_window_definition(definition)

	_chatlog_panel = MerlinWindowScript.new()
	_chatlog_panel.configure(definition)
	add_child(_chatlog_panel)

	_chatlog_content = ChatLogContentScript.new()
	_chatlog_panel.set_content(_chatlog_content)
	_window_manager.register_window_instance(_chatlog_panel)


func _setup_voice_mode() -> void:
	chat_panel.visible = false
	command_input_panel.visible = false
	voice_control.visible = true
	voice_playback.bus = "Master"
	voice_playback.volume_db = 0.0
	voice_button.text = "Wake Merlin"
	voice_control.add_theme_stylebox_override("panel", _panel_style(Color(0.010, 0.052, 0.125, 0.64), COLOR_CYAN, 1.0, 10))
	_style_button(voice_button)
	if FRONTEND_VOICE_INPUT_ENABLED:
		_setup_microphone_recording()
	else:
		print("FrontendVoiceInputDisabled. Backend-owned voice input mode is active.")
		voice_button.text = "Backend voice"


func _setup_barge_in_debug_overlay() -> void:
	if is_instance_valid(_barge_in_debug_overlay) or overlay_container == null:
		return
	_barge_in_debug_overlay = BARGE_IN_DEBUG_OVERLAY_SCRIPT.new()
	_barge_in_debug_overlay.manual_speech_start_marker_requested.connect(_on_manual_speech_start_marker_requested)
	overlay_container.add_child(_barge_in_debug_overlay)


func _setup_microphone_recording() -> void:
	_record_bus_index = AudioServer.get_bus_index(RECORD_BUS_NAME)
	if _record_bus_index == -1:
		AudioServer.add_bus()
		_record_bus_index = AudioServer.get_bus_count() - 1
		AudioServer.set_bus_name(_record_bus_index, RECORD_BUS_NAME)
		AudioServer.set_bus_send(_record_bus_index, "Master")

	_record_effect = AudioEffectRecord.new()
	AudioServer.add_bus_effect(_record_bus_index, _record_effect)
	_capture_effect = AudioEffectCapture.new()
	AudioServer.add_bus_effect(_record_bus_index, _capture_effect)
	AudioServer.set_bus_mute(_record_bus_index, false)
	AudioServer.set_bus_volume_db(_record_bus_index, -80.0)

	microphone_input.stream = AudioStreamMicrophone.new()
	microphone_input.bus = RECORD_BUS_NAME
	microphone_input.play()


func _on_voice_button_down() -> void:
	if not FRONTEND_VOICE_INPUT_ENABLED:
		print("FrontendWakeRecordingSuppressedBackendOwnedMode")
		_add_notification("Backend voice input is active", "system")
		return
	if not web_socket_client.is_backend_connected():
		_show_error("Cannot listen: Merlin.Backend is not connected.")
		_add_notification("Backend offline", "error")
		return
	if _record_effect == null:
		return

	if _wake_listening_enabled:
		_stop_wake_listening()
	else:
		_start_wake_listening()


func _on_voice_button_up() -> void:
	if _wake_listening_enabled:
		return
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
	if not FRONTEND_VOICE_INPUT_ENABLED:
		print("FrontendVoiceTranscriptSubmitSuppressedBackendOwnedMode")
		_reset_voice_button()
		return
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

	if not FRONTEND_VOICE_INPUT_ENABLED:
		print("FrontendVoiceTranscriptSubmitSuppressedBackendOwnedMode")
		_reset_voice_button()
		return

	_add_notification("Heard: %s" % transcript, "system")
	_send_backend_message(transcript, false)
	_set_voice_phase("waiting_llm")
	_show_question_acknowledgement(transcript)
	_reset_voice_button()


func _reset_voice_button() -> void:
	voice_button.disabled = not FRONTEND_VOICE_INPUT_ENABLED
	if not FRONTEND_VOICE_INPUT_ENABLED:
		voice_button.text = "Backend voice"
		return
	if _wake_listening_enabled:
		voice_button.text = "Awake: listening" if _merlin_awake else "Sleeping: wake ready"
	else:
		voice_button.text = "Wake Merlin"


func _start_wake_listening() -> void:
	if not FRONTEND_VOICE_INPUT_ENABLED:
		print("FrontendWakeRecordingSuppressedBackendOwnedMode")
		_reset_voice_button()
		return
	if _wake_listening_enabled:
		return
	_clear_transient_visual_overlay()
	_hide_application_choice_panel()
	_wake_listening_enabled = true
	_merlin_awake = false
	_last_merlin_activity_usec = 0
	_sleep_clap_times.clear()
	_reset_voice_button()
	_add_notification("Wake listening enabled. Merlin is asleep.", "system")
	_set_voice_phase("wake_listening")
	_set_merlin_state(MerlinState.LISTENING)
	call_deferred("_wake_listen_loop")


func _stop_wake_listening() -> void:
	_cancel_voice_stream()
	_wake_listening_enabled = false
	_merlin_awake = false
	_last_merlin_activity_usec = 0
	_sleep_clap_times.clear()
	if _record_effect != null:
		_record_effect.set_recording_active(false)
	_is_recording = false
	_set_voice_phase("idle")
	_reset_voice_button()
	if _pending_requests.is_empty() and not _speech_turn_active:
		_set_merlin_state(MerlinState.IDLE)
	_add_notification("Wake listening disabled", "system")


func _wake_listen_loop() -> void:
	if _wake_cycle_active:
		return
	_wake_cycle_active = true
	while _wake_listening_enabled:
		if not web_socket_client.is_backend_connected() or _speech_turn_active or not _pending_requests.is_empty():
			await get_tree().create_timer(0.25).timeout
			continue
		if _record_effect == null:
			break

		if _merlin_awake:
			_is_recording = true
			_set_voice_phase("wake_listening")
			if _merlin_state != MerlinState.LISTENING:
				_set_merlin_state(MerlinState.LISTENING)
			_record_effect.set_recording_active(false)
			_record_effect.set_recording_active(true)
			await _wait_for_wake_recording_turn()
			if not _wake_listening_enabled or _record_effect == null:
				break

			_record_effect.set_recording_active(false)
			_is_recording = false
			if not _finish_voice_stream():
				var recording := _record_effect.get_recording()
				await _process_wake_recording(recording, true)
		else:
			var armed := await _wait_for_sleep_double_clap()
			if not armed:
				continue
			if not _wake_listening_enabled or _record_effect == null:
				break
			await _record_armed_wake_phrase()
		if _wake_listening_enabled and _pending_requests.is_empty() and not _speech_turn_active:
			_set_voice_phase("idle")
			_set_merlin_state(MerlinState.IDLE)
		await get_tree().create_timer(WAKE_RECORD_RESTART_DELAY_SECONDS).timeout

	_wake_cycle_active = false
	_is_recording = false
	if _record_effect != null:
		_record_effect.set_recording_active(false)
	_reset_voice_button()


func _wait_for_wake_recording_turn() -> void:
	var started_usec := Time.get_ticks_usec()
	var first_speech_usec := 0
	var last_speech_usec := 0
	var allow_active_cap := not _merlin_awake
	_voice_stream_preroll_frames = PackedVector2Array()
	if _capture_effect != null and _capture_effect.has_method("clear_buffer"):
		_capture_effect.call("clear_buffer")
	while _wake_listening_enabled and _record_effect != null:
		await get_tree().create_timer(WAKE_RECORD_POLL_SECONDS).timeout
		if not _wake_listening_enabled or _record_effect == null:
			return

		var elapsed_seconds := float(Time.get_ticks_usec() - started_usec) / 1000000.0
		var recording := _record_effect.get_recording()
		var recent_features := _analyze_recording_recent_audio(recording)
		var has_recent_speech := _is_recent_wake_speech(recent_features)
		if not _voice_stream_active:
			_capture_voice_stream_preroll()
		if has_recent_speech:
			last_speech_usec = Time.get_ticks_usec()
			if first_speech_usec <= 0:
				first_speech_usec = last_speech_usec
				if _merlin_awake:
					_start_voice_stream()

		if _voice_stream_active:
			_send_available_voice_stream_chunks()

		if first_speech_usec <= 0:
			if elapsed_seconds >= WAKE_RECORD_IDLE_WINDOW_SECONDS:
				return
			continue

		var active_seconds := float(Time.get_ticks_usec() - first_speech_usec) / 1000000.0
		var silent_seconds := float(Time.get_ticks_usec() - last_speech_usec) / 1000000.0
		if active_seconds >= WAKE_RECORD_MIN_ACTIVE_SECONDS and silent_seconds >= WAKE_RECORD_END_SILENCE_SECONDS:
			print("Wake listening: end-of-speech detected. ActiveSeconds: %.2f SilenceSeconds: %.2f" % [active_seconds, silent_seconds])
			return

		if allow_active_cap and active_seconds >= WAKE_RECORD_SLEEP_MAX_ACTIVE_SECONDS:
			print("Wake listening: sleep max active recording reached. ActiveSeconds: %.2f" % active_seconds)
			return


func _wait_for_sleep_double_clap() -> bool:
	_wake_armed_preroll_frames = PackedVector2Array()
	if _capture_effect != null and _capture_effect.has_method("clear_buffer"):
		_capture_effect.call("clear_buffer")

	_set_voice_phase("wake_listening")
	if _merlin_state != MerlinState.LISTENING:
		_set_merlin_state(MerlinState.LISTENING)

	while _wake_listening_enabled and not _merlin_awake and _capture_effect != null:
		await get_tree().create_timer(WAKE_RECORD_POLL_SECONDS).timeout
		if not _wake_listening_enabled or _merlin_awake or _capture_effect == null:
			return false

		var frames_available := int(_capture_effect.get_frames_available())
		if frames_available > 0 and Time.get_ticks_usec() - _last_sleep_capture_debug_usec >= 2000000:
			_last_sleep_capture_debug_usec = Time.get_ticks_usec()
			print("Wake listening: capture active. FramesAvailable: %s" % frames_available)
		if frames_available <= 0:
			continue

		var frames: PackedVector2Array = _capture_effect.get_buffer(frames_available)
		_capture_wake_armed_preroll(frames)
		if _detect_sleep_double_clap(frames):
			print("Wake listening: double clap armed. WindowSeconds: %.1f" % WAKE_ARMED_LISTEN_SECONDS)
			_add_notification("Wake armed", "system")
			return true

	return false


func _record_armed_wake_phrase() -> void:
	_is_recording = true
	_set_voice_phase("wake_armed")
	var frames := _wake_armed_preroll_frames
	_wake_armed_preroll_frames = PackedVector2Array()
	if _capture_effect != null:
		var initial_frames_available := int(_capture_effect.get_frames_available())
		if initial_frames_available > 0:
			frames.append_array(_capture_effect.get_buffer(initial_frames_available))

	var started_usec := Time.get_ticks_usec()
	while _wake_listening_enabled and _capture_effect != null:
		await get_tree().create_timer(WAKE_RECORD_POLL_SECONDS).timeout
		if not _wake_listening_enabled or _capture_effect == null:
			break

		var frames_available := int(_capture_effect.get_frames_available())
		if frames_available > 0:
			frames.append_array(_capture_effect.get_buffer(frames_available))

		var elapsed_seconds := float(Time.get_ticks_usec() - started_usec) / 1000000.0
		if elapsed_seconds >= WAKE_ARMED_LISTEN_SECONDS:
			break

	_is_recording = false
	if not _wake_listening_enabled:
		return
	await _process_wake_audio_bytes(_frames_to_wav_bytes(frames, int(AudioServer.get_mix_rate())), false)


func _start_voice_stream() -> void:
	if not FRONTEND_VOICE_INPUT_ENABLED:
		print("VoiceStreamStartSuppressedBackendOwnedMode")
		return
	if _voice_stream_active or _capture_effect == null or not web_socket_client.is_backend_connected():
		return

	_voice_stream_correlation_id = _generate_correlation_id()
	_voice_stream_sample_rate = int(AudioServer.get_mix_rate())
	_voice_stream_chunks_sent = 0
	_voice_stream_bytes_sent = 0

	var client_mode := "chat" if chat_panel.visible else "orb"
	if not web_socket_client.send_voice_stream_start(_voice_stream_correlation_id, _voice_stream_sample_rate, 1, client_mode):
		_voice_stream_correlation_id = ""
		return

	_voice_stream_active = true
	_pending_requests[_voice_stream_correlation_id] = "voice stream"
	_voice_turn_started_usec = Time.get_ticks_usec()
	_update_pending_state()
	_set_voice_phase("streaming_stt")
	_send_voice_stream_preroll()
	print("Voice stream: started. CorrelationId: %s SampleRate: %s" % [_voice_stream_correlation_id, _voice_stream_sample_rate])


func _finish_voice_stream() -> bool:
	if not FRONTEND_VOICE_INPUT_ENABLED:
		if _voice_stream_active:
			print("VoiceStreamEndSuppressedBackendOwnedMode")
		_voice_stream_active = false
		_voice_stream_correlation_id = ""
		_voice_stream_preroll_frames = PackedVector2Array()
		return false
	if not _voice_stream_active:
		return false

	_send_available_voice_stream_chunks()
	var correlation_id := _voice_stream_correlation_id
	var client_mode := "chat" if chat_panel.visible else "orb"
	_voice_stream_active = false
	_voice_stream_correlation_id = ""
	_set_voice_phase("waiting_stt")
	print("Voice stream: finished. CorrelationId: %s Chunks: %s Bytes: %s" % [correlation_id, _voice_stream_chunks_sent, _voice_stream_bytes_sent])
	var sent := web_socket_client.send_voice_stream_end(correlation_id, client_mode)
	if not sent:
		_pending_requests.erase(correlation_id)
		_update_pending_state()
	return sent


func _cancel_voice_stream() -> void:
	if not _voice_stream_active:
		return

	var correlation_id := _voice_stream_correlation_id
	_voice_stream_active = false
	_voice_stream_correlation_id = ""
	_voice_stream_preroll_frames = PackedVector2Array()
	_pending_requests.erase(correlation_id)
	if not FRONTEND_VOICE_INPUT_ENABLED:
		print("VoiceStreamEndSuppressedBackendOwnedMode")
		_update_pending_state()
		return
	web_socket_client.send_voice_stream_cancel(correlation_id)
	_update_pending_state()


func _send_available_voice_stream_chunks() -> void:
	if not FRONTEND_VOICE_INPUT_ENABLED:
		if _voice_stream_active:
			print("VoiceStreamChunkSuppressedBackendOwnedMode")
		return
	if not _voice_stream_active or _capture_effect == null:
		return

	var frames_available := int(_capture_effect.get_frames_available())
	while frames_available >= VOICE_STREAM_CHUNK_FRAMES:
		var frames: PackedVector2Array = _capture_effect.get_buffer(VOICE_STREAM_CHUNK_FRAMES)
		if not _send_voice_stream_frames(frames):
			return
		frames_available = int(_capture_effect.get_frames_available())


func _capture_voice_stream_preroll() -> void:
	if not FRONTEND_VOICE_INPUT_ENABLED:
		return
	if _capture_effect == null:
		return

	var frames_available := int(_capture_effect.get_frames_available())
	if frames_available <= 0:
		return

	var frames: PackedVector2Array = _capture_effect.get_buffer(frames_available)
	if frames.is_empty():
		return

	_voice_stream_preroll_frames.append_array(frames)
	var max_frames := int(float(AudioServer.get_mix_rate()) * VOICE_STREAM_PREROLL_SECONDS)
	if _voice_stream_preroll_frames.size() > max_frames:
		_voice_stream_preroll_frames = _voice_stream_preroll_frames.slice(_voice_stream_preroll_frames.size() - max_frames, _voice_stream_preroll_frames.size())


func _send_voice_stream_preroll() -> void:
	if not FRONTEND_VOICE_INPUT_ENABLED:
		if not _voice_stream_preroll_frames.is_empty():
			print("VoiceStreamChunkSuppressedBackendOwnedMode")
		_voice_stream_preroll_frames = PackedVector2Array()
		return
	if _voice_stream_preroll_frames.is_empty():
		return

	var frames := _voice_stream_preroll_frames
	_voice_stream_preroll_frames = PackedVector2Array()
	var sent := _send_voice_stream_frames(frames)
	if sent:
		print("Voice stream: pre-roll sent. Frames: %s DurationMs: %.1f" % [frames.size(), float(frames.size()) * 1000.0 / float(maxi(_voice_stream_sample_rate, 1))])


func _send_voice_stream_frames(frames: PackedVector2Array) -> bool:
	if not FRONTEND_VOICE_INPUT_ENABLED:
		print("VoiceStreamChunkSuppressedBackendOwnedMode")
		return false
	var offset := 0
	while offset < frames.size():
		var end := mini(offset + VOICE_STREAM_CHUNK_FRAMES, frames.size())
		var chunk := frames.slice(offset, end)
		var pcm_bytes := _frames_to_mono_pcm16(chunk)
		if not web_socket_client.send_voice_stream_chunk(_voice_stream_correlation_id, pcm_bytes):
			_cancel_voice_stream()
			return false
		_voice_stream_chunks_sent += 1
		_voice_stream_bytes_sent += pcm_bytes.size()
		offset = end
	return true


func _frames_to_mono_pcm16(frames: PackedVector2Array) -> PackedByteArray:
	var bytes := PackedByteArray()
	bytes.resize(frames.size() * 2)
	var offset := 0
	for frame in frames:
		var mono := clampf((frame.x + frame.y) * 0.5, -1.0, 1.0)
		var sample := int(round(mono * 32767.0))
		if sample < 0:
			sample += 65536
		bytes[offset] = sample & 0xff
		bytes[offset + 1] = (sample >> 8) & 0xff
		offset += 2
	return bytes


func _frames_to_wav_bytes(frames: PackedVector2Array, sample_rate: int) -> PackedByteArray:
	var pcm := _frames_to_mono_pcm16(frames)
	var data_size := pcm.size()
	var bytes := PackedByteArray()
	bytes.resize(44 + data_size)
	_write_ascii(bytes, 0, "RIFF")
	_write_le_u32(bytes, 4, 36 + data_size)
	_write_ascii(bytes, 8, "WAVE")
	_write_ascii(bytes, 12, "fmt ")
	_write_le_u32(bytes, 16, 16)
	_write_le_u16(bytes, 20, 1)
	_write_le_u16(bytes, 22, 1)
	_write_le_u32(bytes, 24, maxi(8000, sample_rate))
	_write_le_u32(bytes, 28, maxi(8000, sample_rate) * 2)
	_write_le_u16(bytes, 32, 2)
	_write_le_u16(bytes, 34, 16)
	_write_ascii(bytes, 36, "data")
	_write_le_u32(bytes, 40, data_size)
	for index in range(data_size):
		bytes[44 + index] = pcm[index]
	return bytes


func _process_wake_recording(recording: AudioStreamWAV, already_awake: bool = false) -> void:
	if recording == null:
		return

	var path := "user://merlin-wake-recording.wav"
	var save_error := recording.save_to_wav(path)
	if save_error != OK:
		print("Wake listening: could not save recording. Error: %s" % save_error)
		return

	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		print("Wake listening: could not read recording.")
		return

	var audio_bytes := file.get_buffer(file.get_length())
	file.close()
	await _process_wake_audio_bytes(audio_bytes, already_awake)


func _process_wake_audio_bytes(audio_bytes: PackedByteArray, already_awake: bool = false) -> void:
	if not FRONTEND_VOICE_INPUT_ENABLED:
		print("FrontendVoiceTranscriptSubmitSuppressedBackendOwnedMode")
		return
	if audio_bytes.size() < WAKE_MIN_AUDIO_BYTES:
		return

	var features := _analyze_wake_audio(audio_bytes)
	if float(features.get("rms", 0.0)) < WAKE_MIN_RMS and not bool(features.get("double_clap", false)):
		return

	var transcript := await _transcribe_wake_audio_bytes(audio_bytes)
	if transcript.is_empty():
		return

	var clap_accepted := bool(features.get("double_clap", false)) or (not _merlin_awake and not already_awake)
	var wake_request := _extract_active_voice_request(transcript, clap_accepted)
	if wake_request.is_empty():
		print("Wake listening: rejected transcript='%s' awake=%s doubleClap=%s clapAccepted=%s" % [transcript, str(_merlin_awake), str(features.get("double_clap", false)), str(clap_accepted)])
		return

	var was_awake := _merlin_awake
	print("Wake listening: accepted transcript='%s' request='%s' awake=%s doubleClap=%s" % [transcript, wake_request, str(was_awake), str(features.get("double_clap", false))])
	_add_notification(("Heard: %s" if was_awake else "Wake: %s") % wake_request, "system")
	_mark_merlin_awake()
	_voice_turn_started_usec = Time.get_ticks_usec()
	_send_backend_message(wake_request, false)
	_set_voice_phase("waiting_llm")
	_show_question_acknowledgement(wake_request)


func _transcribe_wake_audio_bytes(audio_bytes: PackedByteArray) -> String:
	_set_voice_phase("wake_transcribing")
	var request_error := voice_transcribe_request.request_raw(
		VOICE_TRANSCRIBE_URL,
		PackedStringArray(["Content-Type: audio/wav"]),
		HTTPClient.METHOD_POST,
		audio_bytes
	)
	if request_error != OK:
		print("Wake listening: transcription request failed. Error: %s" % request_error)
		return ""

	var result = await voice_transcribe_request.request_completed
	var request_result: int = int(result[0])
	var response_code: int = int(result[1])
	var body: PackedByteArray = result[3]
	if request_result != HTTPRequest.RESULT_SUCCESS or response_code < 200 or response_code >= 300:
		print("Wake listening: transcription failed. Result: %s HTTP: %s" % [request_result, response_code])
		return ""

	var parsed = JSON.parse_string(body.get_string_from_utf8())
	if typeof(parsed) != TYPE_DICTIONARY:
		return ""

	return str(parsed.get("text", "")).strip_edges()


func _extract_active_voice_request(transcript: String, has_double_clap: bool) -> String:
	if _merlin_awake:
		return _extract_awake_request(transcript)
	return _extract_wake_request(transcript, has_double_clap)


func _extract_wake_request(transcript: String, has_double_clap: bool) -> String:
	var normalized := _normalize_wake_text(transcript)
	if normalized.is_empty():
		return ""

	var merlin_index := normalized.find("merlin")
	if merlin_index < 0:
		return ""
	if not _has_wake_greeting_before_merlin(normalized, merlin_index):
		return ""

	var after_merlin := normalized.substr(merlin_index + "merlin".length()).strip_edges()
	if not has_double_clap:
		return ""

	if after_merlin.is_empty():
		return "are you there?"
	return _clean_wake_connection_text(after_merlin)


func _extract_awake_request(transcript: String) -> String:
	var text := transcript.strip_edges()
	if text.is_empty():
		return ""

	var wake_request := _extract_wake_request(text, true)
	if not wake_request.is_empty():
		return wake_request

	return text


func _normalize_wake_text(text: String) -> String:
	var normalized := text.to_lower()
	for character in [".", ",", "?", "!", ":", ";", "\"", "'", "(", ")", "[", "]", "{", "}", "\n", "\r", "\t"]:
		normalized = normalized.replace(character, " ")
	return " ".join(normalized.split(" ", false))


func _has_wake_greeting_before_merlin(normalized: String, merlin_index: int) -> bool:
	var before := normalized.substr(0, merlin_index).strip_edges()
	var greetings := [
		"hey",
		"hi",
		"hello",
		"howdy",
		"hola",
		"yo",
		"hiya",
		"heya",
		"morning",
		"evening",
		"good morning",
		"good afternoon",
		"good evening",
		"hey there",
		"hi there",
		"hello there"
	]
	for greeting in greetings:
		if before == greeting or before.ends_with(" " + greeting):
			return true
	return false


func _clean_wake_connection_text(text: String) -> String:
	var cleaned := text.strip_edges()
	if cleaned.is_empty():
		return "are you there?"
	return cleaned + ("?" if cleaned.begins_with("are you ") else "")


func _mark_merlin_activity() -> void:
	if _merlin_awake:
		_last_merlin_activity_usec = Time.get_ticks_usec()
		_reset_voice_button()


func _mark_merlin_awake() -> void:
	if not _merlin_awake:
		_add_notification("Merlin is awake", "system")
	_merlin_awake = true
	_last_merlin_activity_usec = Time.get_ticks_usec()
	_reset_voice_button()


func _sleep_merlin_due_to_inactivity() -> void:
	if not _merlin_awake:
		return
	_merlin_awake = false
	_last_merlin_activity_usec = 0
	_reset_voice_button()
	_add_notification("Merlin went back to sleep", "system")
	if _wake_listening_enabled and _pending_requests.is_empty() and not _speech_turn_active:
		_set_voice_phase("wake_listening")


func _update_merlin_awake_timeout() -> void:
	if not _wake_listening_enabled or not _merlin_awake or _last_merlin_activity_usec <= 0:
		return
	if _pending_requests.is_empty() and not _speech_turn_active:
		var inactive_seconds := float(Time.get_ticks_usec() - _last_merlin_activity_usec) / 1000000.0
		if inactive_seconds >= MERLIN_AWAKE_TIMEOUT_SECONDS:
			_sleep_merlin_due_to_inactivity()


func _analyze_wake_audio(bytes: PackedByteArray) -> Dictionary:
	if bytes.size() < 44:
		return { "rms": 0.0, "peak": 0.0, "double_clap": false }

	var channels := maxi(1, _read_le_u16(bytes, 22))
	var sample_rate := maxi(8000, _read_le_u32(bytes, 24))
	var bits_per_sample := _read_le_u16(bytes, 34)
	var data := _find_wav_data_chunk(bytes)
	var data_offset := int(data.get("offset", 44))
	var data_size := int(data.get("size", bytes.size() - data_offset))
	if bits_per_sample != 16 or data_offset >= bytes.size() or data_size <= 0:
		return { "rms": 0.0, "peak": 0.0, "double_clap": false }

	var frame_bytes := channels * 2
	var data_end := mini(bytes.size(), data_offset + data_size)
	var sum_squares := 0.0
	var sample_count := 0
	var peak := 0.0
	var window_frames := maxi(1, int(float(sample_rate) * 0.018))
	var window_sum := 0.0
	var window_peak := 0.0
	var window_count := 0
	var frame_index := 0
	var clap_times: Array[float] = []

	for offset in range(data_offset, data_end - frame_bytes + 1, frame_bytes):
		var mixed := 0.0
		for channel in range(channels):
			mixed += float(_read_le_i16(bytes, offset + channel * 2)) / 32768.0
		var sample := mixed / float(channels)
		var abs_sample := absf(sample)
		peak = maxf(peak, abs_sample)
		sum_squares += sample * sample
		sample_count += 1
		window_sum += sample * sample
		window_count += 1
		frame_index += 1

		if window_count >= window_frames:
			var window_rms := sqrt(window_sum / float(window_count))
			if window_rms >= WAKE_CLAP_RMS_THRESHOLD:
				var clap_time := float(frame_index) / float(sample_rate)
				if clap_times.is_empty() or clap_time - clap_times[clap_times.size() - 1] >= WAKE_CLAP_MIN_GAP_SECONDS:
					clap_times.append(clap_time)
			window_sum = 0.0
			window_count = 0

	var rms := sqrt(sum_squares / float(maxi(1, sample_count)))
	var double_clap := false
	for index in range(1, clap_times.size()):
		var gap := clap_times[index] - clap_times[index - 1]
		if gap >= WAKE_CLAP_MIN_GAP_SECONDS and gap <= WAKE_CLAP_MAX_GAP_SECONDS:
			double_clap = true
			break

	return { "rms": rms, "peak": peak, "double_clap": double_clap }


func _detect_sleep_double_clap(frames: PackedVector2Array) -> bool:
	if frames.is_empty():
		return false

	var sample_rate := maxi(8000, int(AudioServer.get_mix_rate()))
	var window_frames := maxi(1, int(float(sample_rate) * 0.018))
	var window_sum := 0.0
	var window_peak := 0.0
	var window_count := 0
	var frame_index := 0
	var now_seconds := float(Time.get_ticks_usec()) / 1000000.0
	var frames_duration := float(frames.size()) / float(sample_rate)
	var buffer_started_seconds := now_seconds - frames_duration

	for frame in frames:
		var sample := (frame.x + frame.y) * 0.5
		window_peak = maxf(window_peak, absf(sample))
		window_sum += sample * sample
		window_count += 1
		frame_index += 1

		if window_count >= window_frames:
			var window_rms := sqrt(window_sum / float(window_count))
			var peak_to_rms := window_peak / maxf(window_rms, 0.0001)
			var is_clap_candidate := window_rms >= WAKE_CLAP_RMS_THRESHOLD or (window_peak >= WAKE_CLAP_PEAK_THRESHOLD and peak_to_rms >= WAKE_CLAP_PEAK_TO_RMS_RATIO)
			if is_clap_candidate:
				var clap_time := buffer_started_seconds + (float(frame_index) / float(sample_rate))
				if _sleep_clap_times.is_empty() or clap_time - _sleep_clap_times[_sleep_clap_times.size() - 1] >= WAKE_CLAP_MIN_GAP_SECONDS:
					_sleep_clap_times.append(clap_time)
					print("Wake listening: clap candidate. rms=%.3f peak=%.3f ratio=%.2f stored=%s" % [window_rms, window_peak, peak_to_rms, _sleep_clap_times.size()])
			window_sum = 0.0
			window_peak = 0.0
			window_count = 0

	while not _sleep_clap_times.is_empty() and now_seconds - _sleep_clap_times[0] > WAKE_CLAP_MAX_GAP_SECONDS:
		_sleep_clap_times.remove_at(0)

	for index in range(1, _sleep_clap_times.size()):
		var gap := _sleep_clap_times[index] - _sleep_clap_times[index - 1]
		if gap >= WAKE_CLAP_MIN_GAP_SECONDS and gap <= WAKE_CLAP_MAX_GAP_SECONDS:
			print("Wake listening: double clap detected. GapSeconds: %.3f" % gap)
			_sleep_clap_times.clear()
			return true

	return false


func _capture_wake_armed_preroll(frames: PackedVector2Array) -> void:
	if frames.is_empty():
		return

	_wake_armed_preroll_frames.append_array(frames)
	var max_frames := int(float(AudioServer.get_mix_rate()) * WAKE_ARMED_PREROLL_SECONDS)
	if _wake_armed_preroll_frames.size() > max_frames:
		_wake_armed_preroll_frames = _wake_armed_preroll_frames.slice(_wake_armed_preroll_frames.size() - max_frames, _wake_armed_preroll_frames.size())


func _analyze_recording_recent_audio(recording: AudioStreamWAV) -> Dictionary:
	if recording == null:
		return { "rms": 0.0, "peak": 0.0 }

	var path := "user://merlin-wake-monitor.wav"
	var save_error := recording.save_to_wav(path)
	if save_error != OK:
		return { "rms": 0.0, "peak": 0.0 }

	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return { "rms": 0.0, "peak": 0.0 }

	var bytes := file.get_buffer(file.get_length())
	file.close()
	return _analyze_recent_wav_audio(bytes, WAKE_RECENT_WINDOW_SECONDS)


func _analyze_recent_wav_audio(bytes: PackedByteArray, recent_seconds: float) -> Dictionary:
	if bytes.size() < 44:
		return { "rms": 0.0, "peak": 0.0 }

	var channels := maxi(1, _read_le_u16(bytes, 22))
	var sample_rate := maxi(8000, _read_le_u32(bytes, 24))
	var bits_per_sample := _read_le_u16(bytes, 34)
	var data := _find_wav_data_chunk(bytes)
	var data_offset := int(data.get("offset", 44))
	var data_size := int(data.get("size", bytes.size() - data_offset))
	if bits_per_sample != 16 or data_offset >= bytes.size() or data_size <= 0:
		return { "rms": 0.0, "peak": 0.0 }

	var frame_bytes := channels * 2
	var data_end := mini(bytes.size(), data_offset + data_size)
	var recent_frames := maxi(1, int(float(sample_rate) * recent_seconds))
	var recent_bytes := recent_frames * frame_bytes
	var start_offset := maxi(data_offset, data_end - recent_bytes)
	var sum_squares := 0.0
	var sample_count := 0
	var peak := 0.0

	for offset in range(start_offset, data_end - frame_bytes + 1, frame_bytes):
		var mixed := 0.0
		for channel in range(channels):
			mixed += float(_read_le_i16(bytes, offset + channel * 2)) / 32768.0
		var sample := mixed / float(channels)
		var abs_sample := absf(sample)
		peak = maxf(peak, abs_sample)
		sum_squares += sample * sample
		sample_count += 1

	var rms := sqrt(sum_squares / float(maxi(1, sample_count)))
	return { "rms": rms, "peak": peak }


func _is_recent_wake_speech(features: Dictionary) -> bool:
	return float(features.get("rms", 0.0)) >= WAKE_SPEECH_RMS or float(features.get("peak", 0.0)) >= WAKE_SPEECH_PEAK


func _find_wav_data_chunk(bytes: PackedByteArray) -> Dictionary:
	var offset := 12
	while offset + 8 <= bytes.size():
		var chunk_size := _read_le_u32(bytes, offset + 4)
		if int(bytes[offset]) == 100 and int(bytes[offset + 1]) == 97 and int(bytes[offset + 2]) == 116 and int(bytes[offset + 3]) == 97:
			return { "offset": offset + 8, "size": chunk_size }
		offset += 8 + chunk_size + (chunk_size % 2)
	return { "offset": 44, "size": bytes.size() - 44 }


func _read_le_u16(bytes: PackedByteArray, offset: int) -> int:
	if offset + 1 >= bytes.size():
		return 0
	return int(bytes[offset]) | (int(bytes[offset + 1]) << 8)


func _read_le_u32(bytes: PackedByteArray, offset: int) -> int:
	if offset + 3 >= bytes.size():
		return 0
	return int(bytes[offset]) | (int(bytes[offset + 1]) << 8) | (int(bytes[offset + 2]) << 16) | (int(bytes[offset + 3]) << 24)


func _read_le_i16(bytes: PackedByteArray, offset: int) -> int:
	var value := _read_le_u16(bytes, offset)
	if value >= 32768:
		value -= 65536
	return value


func _write_ascii(bytes: PackedByteArray, offset: int, value: String) -> void:
	var encoded := value.to_ascii_buffer()
	for index in range(encoded.size()):
		if offset + index < bytes.size():
			bytes[offset + index] = encoded[index]


func _write_le_u16(bytes: PackedByteArray, offset: int, value: int) -> void:
	if offset + 1 >= bytes.size():
		return
	bytes[offset] = value & 0xff
	bytes[offset + 1] = (value >> 8) & 0xff


func _write_le_u32(bytes: PackedByteArray, offset: int, value: int) -> void:
	if offset + 3 >= bytes.size():
		return
	bytes[offset] = value & 0xff
	bytes[offset + 1] = (value >> 8) & 0xff
	bytes[offset + 2] = (value >> 16) & 0xff
	bytes[offset + 3] = (value >> 24) & 0xff


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
	if words.size() <= VOICE_ACKNOWLEDGEMENT_MAX_WORDS:
		return text

	var summary_words := PackedStringArray()
	for index in range(VOICE_ACKNOWLEDGEMENT_MAX_WORDS):
		summary_words.append(words[index])
	return "%s..." % " ".join(summary_words)


func _profiled_connection_state_changed(state: String, detail: String) -> void:
	_profile_signal_handler("_on_connection_state_changed", Callable(self, "_on_connection_state_changed"), [state, detail])


func _profiled_visual_state_received(state: Dictionary) -> void:
	_profile_signal_handler("_on_visual_state_received", Callable(self, "_on_visual_state_received"), [state])


func _profiled_assistant_ui_state_received(state: Dictionary) -> void:
	_profile_signal_handler("_on_assistant_ui_state_received", Callable(self, "_on_assistant_ui_state_received"), [state])


func _profiled_response_received(response: Dictionary) -> void:
	_profile_signal_handler("_on_response_received", Callable(self, "_on_response_received"), [response])


func _profiled_voice_transcript_received(transcript: Dictionary) -> void:
	_profile_signal_handler("_on_voice_transcript_received", Callable(self, "_on_voice_transcript_received"), [transcript])


func _profiled_visual_event_received(event: Dictionary) -> void:
	_profile_signal_handler("_on_visual_event_received", Callable(self, "_on_visual_event_received"), [event])


func _profiled_barge_in_debug_snapshot_received(snapshot: Dictionary) -> void:
	_profile_signal_handler("_on_barge_in_debug_snapshot_received", Callable(self, "_on_barge_in_debug_snapshot_received"), [snapshot])


func _profiled_malformed_response(raw_message: String, detail: String) -> void:
	_profile_signal_handler("_on_malformed_response", Callable(self, "_on_malformed_response"), [raw_message, detail])


func _profiled_socket_closed(code: int, reason: String) -> void:
	_profile_signal_handler("_on_socket_closed", Callable(self, "_on_socket_closed"), [code, reason])


func _profiled_frontend_work_observed(metrics: Dictionary) -> void:
	_profile_signal_handler("_on_frontend_work_observed", Callable(self, "_on_frontend_work_observed"), [metrics])


func _profile_signal_handler(handler_name: String, handler: Callable, args: Array) -> void:
	var started_usec := Time.get_ticks_usec()
	handler.callv(args)
	var elapsed_ms := _elapsed_ms_since(started_usec)
	if elapsed_ms >= 2.0:
		print("SignalHandlerPerf slow handler=%s ms=%.2f" % [handler_name, elapsed_ms])


func _on_connection_state_changed(state: String, detail: String) -> void:
	connection_state_label.text = _format_connection_state(state, detail)

	match state:
		"connected":
			_clear_error()
			_add_system_message("Connected to Merlin.Backend.")
			_add_notification("Connected", "system")
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
			_set_merlin_state(MerlinState.SLEEPING)

	_update_send_button()


func _on_visual_state_received(state: Dictionary) -> void:
	var mode := String(state.get("mode", ""))
	if _merlin_state == MerlinState.SLEEPING and (mode.is_empty() or mode == "idle"):
		return
	if not _pending_requests.is_empty():
		if mode.is_empty() or mode == "idle":
			core_orb.set_thinking()
			return
	if core_orb != null and core_orb.has_method("update_visual_state"):
		core_orb.update_visual_state(state)


func _on_assistant_ui_state_received(state: Dictionary) -> void:
	var sequence := int(state.get("sequence", -1))
	if sequence <= _last_assistant_ui_state_sequence:
		return

	_last_assistant_ui_state_sequence = sequence
	_canonical_ui_state_active = true

	_apply_assistant_ui_overlay_state(String(state.get("overlayState", "none")))

	var base_state := String(state.get("baseState", "")).to_lower()
	if _merlin_state == MerlinState.SLEEPING and base_state == "idle":
		return
	var mapped_state := _merlin_state_from_assistant_ui_base_state(base_state)
	if mapped_state == -1:
		print("assistant_ui_state ignored unknown baseState=%s sequence=%s" % [base_state, sequence])
		return

	_set_merlin_state(mapped_state)


func _merlin_state_from_assistant_ui_base_state(base_state: String) -> int:
	match base_state:
		"sleeping":
			return MerlinState.SLEEPING
		"idle":
			return MerlinState.IDLE
		"listening":
			return MerlinState.LISTENING
		"thinking":
			return MerlinState.THINKING
		"speaking":
			return MerlinState.SPEAKING
		_:
			return -1


func _apply_assistant_ui_overlay_state(overlay_state: String) -> void:
	match overlay_state.to_lower():
		"none", "":
			if _visual_overlay_kind == "confirmation" or _visual_overlay_kind == "error":
				_release_visual_overlay(_visual_overlay_kind)
		"confirmation":
			_start_visual_overlay("confirmation", 0.0, false, true)
		"error":
			_start_visual_overlay("error", 0.0, true)
		_:
			print("assistant_ui_state ignored unknown overlayState=%s" % overlay_state)


func _on_barge_in_debug_snapshot_received(snapshot: Dictionary) -> void:
	if not is_instance_valid(_barge_in_debug_overlay):
		_setup_barge_in_debug_overlay()
	if is_instance_valid(_barge_in_debug_overlay):
		_barge_in_debug_overlay.update_snapshot(snapshot)


func _on_manual_speech_start_marker_requested() -> void:
	if web_socket_client.send_speech_presence_marker():
		_add_notification("Speech start marker logged", "system")


func _on_response_received(response: Dictionary) -> void:
	_llm_response_received_usec = Time.get_ticks_usec()
	print("Voice timing: LLM response received. ElapsedMs: %.1f" % _voice_elapsed_ms())
	_set_voice_phase("llm_response")
	await get_tree().process_frame
	var prepare_started_usec := Time.get_ticks_usec()
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
	var prepare_ms := _elapsed_ms_since(prepare_started_usec)
	if prepare_ms >= 2.0:
		print("SignalHandlerPerf slow handler=_on_response_received.prepare ms=%.2f" % prepare_ms)

	if success:
		_release_visual_overlay()
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
	_focus_message_input()


func _on_voice_transcript_received(transcript: Dictionary) -> void:
	var correlation_id := str(transcript.get("correlationId", ""))
	var text := str(transcript.get("text", "")).strip_edges()
	if text.is_empty():
		return

	if not correlation_id.is_empty() and _pending_requests.has(correlation_id):
		_pending_requests[correlation_id] = text

	_add_notification("Heard: %s" % text, "system")
	_append_chatlog_message("user", text, "stt", str(transcript.get("timestampUtc", "")))
	_show_question_acknowledgement(text)


func _on_malformed_response(raw_message: String, detail: String) -> void:
	_pending_requests.clear()
	_update_pending_state()
	var message := "Malformed response JSON: %s" % detail
	_show_error(message)
	_add_system_message("%s Raw: %s" % [message, raw_message])
	_add_notification("Malformed backend response", "error")
	_set_merlin_state(MerlinState.ERROR)
	_focus_message_input()


func _handle_chatlog_visual_event(event_name: String, event: Dictionary) -> bool:
	var panel_id := str(event.get("panelId", ""))
	var window_type := str(event.get("windowType", panel_id))
	match event_name:
		"UI_WINDOW_SHOW":
			if window_type == MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG:
				_show_chatlog_panel()
				return true
		"UI_WINDOW_HIDE":
			if window_type == MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG:
				_hide_chatlog_panel()
				return true
		"UI_WINDOW_FOCUS":
			if window_type == MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG:
				_show_chatlog_panel()
				return true
		"UI_PANEL_SHOW":
			if panel_id == "chatlog":
				_show_chatlog_panel()
				return true
		"UI_PANEL_HIDE":
			if panel_id == "chatlog":
				_hide_chatlog_panel()
				return true
		"UI_CHATLOG_APPEND":
			if panel_id == "chatlog":
				_append_chatlog_message(
					str(event.get("role", "")),
					str(event.get("text", "")),
					str(event.get("source", "")),
					str(event.get("timestampUtc", "")))
				return true
		"UI_CHATLOG_CLEAR":
			if panel_id == "chatlog":
				if is_instance_valid(_chatlog_content):
					_chatlog_content.clear_messages()
				return true
	return false


func _on_visual_event_received(event: Dictionary) -> void:
	var event_name := str(event.get("event", "")).to_upper()
	var event_started_usec := Time.get_ticks_usec()
	if _handle_ui_control_visual_event(event_name):
		return
	if _handle_gesture_visual_event(event_name, event):
		return
	if _handle_chatlog_visual_event(event_name, event):
		return
	match event_name:
		"SPEAKING_START":
			_speaking_startup_profile_started_usec = event_started_usec
			_speaking_startup_profile_energy_count = 0
			_speaking_startup_profile_first_energy_logged = false
			if core_orb != null and core_orb.has_method("start_speaking_startup_profile"):
				core_orb.start_speaking_startup_profile()
			_speech_turn_active = true
			_set_voice_phase("backend_playback_started")
			var state_started_usec := Time.get_ticks_usec()
			if _legacy_state_changes_enabled():
				_set_merlin_state(MerlinState.SPEAKING)
			var state_ms := float(Time.get_ticks_usec() - state_started_usec) / 1000.0
			var total_event_ms := float(Time.get_ticks_usec() - event_started_usec) / 1000.0
			print("Speaking startup event: SPEAKING_START received. StateTransitionMs: %.3f. EventHandlingMs: %.3f. TotalVoiceTurnMs: %.1f" % [
				state_ms,
				total_event_ms,
				_voice_elapsed_ms()
			])
			print("Voice timing: backend playback started. TotalVoiceTurnMs: %.1f" % _voice_elapsed_ms())
		"SPEECH_ENERGY":
			var energy := clampf(float(event.get("value", 0.0)), 0.0, 1.0)
			_speaking_startup_profile_energy_count += 1
			if not _speaking_startup_profile_first_energy_logged:
				_speaking_startup_profile_first_energy_logged = true
				var first_energy_ms := 0.0
				if _speaking_startup_profile_started_usec > 0:
					first_energy_ms = float(event_started_usec - _speaking_startup_profile_started_usec) / 1000.0
				print("Speaking startup event: first SPEECH_ENERGY received. SinceSpeakingStartMs: %.3f. Energy: %.3f" % [
					first_energy_ms,
					energy
				])
			if _legacy_state_changes_enabled() and _merlin_state != MerlinState.SPEAKING:
				_speech_turn_active = true
				var late_state_started_usec := Time.get_ticks_usec()
				_set_merlin_state(MerlinState.SPEAKING)
				print("Speaking startup event: SPEECH_ENERGY forced SPEAKING state. StateTransitionMs: %.3f. EnergyEventCount: %s" % [
					float(Time.get_ticks_usec() - late_state_started_usec) / 1000.0,
					_speaking_startup_profile_energy_count
				])
			core_orb.notify_speech_tick("", 0.0, energy)
		"SPEAKING_END":
			var speaking_total_ms := 0.0
			if _speaking_startup_profile_started_usec > 0:
				speaking_total_ms = float(event_started_usec - _speaking_startup_profile_started_usec) / 1000.0
			print("Speaking startup event: SPEAKING_END received. SinceSpeakingStartMs: %.3f. EnergyEvents: %s" % [
				speaking_total_ms,
				_speaking_startup_profile_energy_count
			])
			_speech_turn_active = false
			if _visual_overlay_hold_until_speech_end:
				_release_visual_overlay(_visual_overlay_kind)
			_set_voice_phase("backend_playback_complete")
			print("Voice timing: backend playback complete. TotalVoiceTurnMs: %.1f" % _voice_elapsed_ms())
			if _legacy_state_changes_enabled():
				_settle_orb_after_response()
		"SPEAKING_CANCELLED":
			var speaking_cancel_ms := 0.0
			if _speaking_startup_profile_started_usec > 0:
				speaking_cancel_ms = float(event_started_usec - _speaking_startup_profile_started_usec) / 1000.0
			print("Speaking startup event: SPEAKING_CANCELLED received. SinceSpeakingStartMs: %.3f. EnergyEvents: %s" % [
				speaking_cancel_ms,
				_speaking_startup_profile_energy_count
			])
			_speech_turn_active = false
			if _visual_overlay_hold_until_speech_end:
				_release_visual_overlay(_visual_overlay_kind)
			_set_voice_phase("backend_playback_cancelled")
			if _legacy_state_changes_enabled():
				_settle_orb_after_response()


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
		"Requested: %s" % str(confirmation.get("originalUserCommand", "")),
		"Target: %s" % str(confirmation.get("displayName", "")),
		"Expires: %s" % str(confirmation.get("expiresAtUtc", "")),
		"Type confirm to approve."
	])

	if typeof(application_candidates) == TYPE_ARRAY and application_candidates.size() > 1:
		lines.append("")
		lines.append("Application choices:")
		for candidate in application_candidates:
			if typeof(candidate) == TYPE_DICTIONARY:
				lines.append("- %s" % str(candidate.get("displayName", "")))
		lines.append("Say the full application name shown, or select it from the choices.")

	return "\n".join(lines).strip_edges()


func _spoken_confirmation_prompt(message: String, confirmation: Dictionary, application_candidates = null) -> String:
	if typeof(application_candidates) == TYPE_ARRAY and application_candidates.size() > 1:
		return "I found multiple apps matching that description, sir. Please choose which app you want to open."

	var display_name := str(confirmation.get("displayName", "")).strip_edges()
	if not display_name.is_empty():
		return "I found %s, but I have not handled this specific application before. Please confirm before I open it." % display_name

	return message


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
	var has_application_choices: bool = typeof(confirmation) == TYPE_DICTIONARY and typeof(application_candidates) == TYPE_ARRAY and application_candidates.size() > 1
	if has_application_choices:
		_show_application_choice_panel(confirmation, application_candidates)
	elif response_type == "confirmation" or typeof(confirmation) != TYPE_DICTIONARY:
		_hide_application_choice_panel()

	if success:
		_release_visual_overlay()
		var spoken_message := _format_success_response(message, available_tools, diagnostics, confirmation)
		if chat_panel.visible:
			await _add_typed_chat_line("Merlin", spoken_message, debug_text, _response_kind(response, success, response_type))
		_clear_error()
	else:
		var formatted_error := _format_error_response(error_code, message)
		if typeof(confirmation) == TYPE_DICTIONARY:
			var confirmation_message := _format_confirmation(message, confirmation, application_candidates)
			if chat_panel.visible:
				await _add_typed_chat_line("Merlin", confirmation_message, debug_text, "confirmation")
			_add_notification("Confirmation required", "confirmation")
			_clear_error()
			activity_label.text = "Waiting for confirmation"
			_start_visual_overlay("confirmation", 0.0, false, true)
			if has_application_choices:
				_show_application_choice_panel(confirmation, application_candidates)
			_focus_message_input()
			return
		elif response_type == "limitation" or response_type == "safety":
			var kind := _response_kind(response, success, response_type)
			if chat_panel.visible:
				await _add_typed_chat_line("Merlin", message, debug_text, kind)
			_add_notification("Capability unavailable" if kind == "limitation" else "Safety boundary", kind)
			_clear_error()
		elif response_type == "system":
			_add_system_message(message)
			_add_notification(message, "system")
			_clear_error()
		else:
			if chat_panel.visible:
				await _add_typed_chat_line("Error", formatted_error, debug_text, "error")
			_add_notification("Error", "error")
			_clear_error()

	if chat_panel.visible and _visual_overlay_hold_until_speech_end:
		_release_visual_overlay(_visual_overlay_kind)

	if not _speech_turn_active:
		_settle_orb_after_response()
	_focus_message_input()


func _speak_text(text: String) -> void:
	var spoken_text := text.strip_edges()
	if spoken_text.is_empty():
		return

	_set_voice_phase("backend_tts_queued")
	await get_tree().process_frame
	await _speak_text_unlocked(spoken_text)


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
	var next_speech_tick_frame := VOICE_SPEECH_TICK_FRAMES
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
							while frames_pushed >= next_speech_tick_frame:
								core_orb.notify_speech_tick("", 0.0, fmod(float(next_speech_tick_frame) / float(maxi(stream_sample_rate, 1)), 1.0))
								next_speech_tick_frame += VOICE_SPEECH_TICK_FRAMES
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
				while frames_pushed >= next_speech_tick_frame:
					core_orb.notify_speech_tick("", 0.0, fmod(float(next_speech_tick_frame) / float(maxi(stream_sample_rate, 1)), 1.0))
					next_speech_tick_frame += VOICE_SPEECH_TICK_FRAMES
				if not first_audio_submitted_logged:
					first_audio_submitted_logged = true
					print("Voice timing: first audio submitted to Godot playback. ConsumedBytes: %s. FramesPushed: %s. SinceLlmMs: %.1f. RequestMs: %.1f" % [consumed_after_poll, frames_pushed, _elapsed_ms_since(_llm_response_received_usec), _elapsed_ms_since(started_usec)])

		if first_audio_submitted_logged and not first_audible_logged and voice_playback.get_playback_position() > 0.0:
			first_audible_logged = true
			if _legacy_state_changes_enabled():
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
				while frames_pushed >= next_speech_tick_frame:
					core_orb.notify_speech_tick("", 0.0, fmod(float(next_speech_tick_frame) / float(maxi(stream_sample_rate, 1)), 1.0))
					next_speech_tick_frame += VOICE_SPEECH_TICK_FRAMES
		await get_tree().process_frame

		if first_audio_submitted_logged and not first_audible_logged and voice_playback.get_playback_position() > 0.0:
			first_audible_logged = true
			if _legacy_state_changes_enabled():
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
	var convert_started_usec := Time.get_ticks_usec()
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
	_frame_profile_pcm_convert_ms += _elapsed_ms_since(convert_started_usec)

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
	likely_gc_or_engine_work = likely_gc_or_engine_work and _frame_profile_pcm_convert_ms < 1.0
	likely_gc_or_engine_work = likely_gc_or_engine_work and _frame_profile_audio_push_ms < 1.0
	likely_gc_or_engine_work = likely_gc_or_engine_work and _frame_profile_websocket_work_ms < 1.0
	print("Voice frame profile: reason=%s state=%s phase=%s frameMs=%.2f avgMs=%.2f maxMs=%.2f frames=%s over16=%s over33=%s over50=%s over100=%s httpPolled=%s bytes=%s pcmFrames=%s jsonParses=%s jsonMs=%.2f largeCopies=%s largeCopyBytes=%s syncWorkMs=%.2f syncLabel=%s pcmConvertMs=%.2f audioPushMs=%.2f websocketPackets=%s websocketWorkMs=%.2f likelyGcOrEngineWork=%s" % [
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
		_frame_profile_pcm_convert_ms,
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
	_frame_profile_pcm_convert_ms = 0.0
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
		MerlinState.SLEEPING:
			return "SLEEPING"
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
	var application_candidates = response.get("applicationCandidates", null)
	var has_application_choices: bool = has_confirmation and typeof(application_candidates) == TYPE_ARRAY and application_candidates.size() > 1
	if has_confirmation:
		activity_label.text = "Waiting for confirmation"
		_start_visual_overlay("confirmation", 0.0, false, true)
		await get_tree().create_timer(0.28).timeout
		return

	if response_type == "confirmation":
		_hide_application_choice_panel()
		_start_visual_overlay("confirmation", VISUAL_OVERLAY_CONFIRMATION_HOLD_SECONDS)
		await get_tree().create_timer(0.28).timeout
		return

	if not has_application_choices:
		_hide_application_choice_panel()

	if _visual_overlay_waiting_for_confirmation:
		_release_visual_overlay("confirmation")

	if response_type == "error" or response_type == "limitation" or response_type == "safety" or (not success and not has_confirmation):
		_start_visual_overlay("error", 0.0, true)
		await get_tree().create_timer(0.28).timeout
		return

	if success and _is_tool_execution_response(response):
		if _legacy_state_changes_enabled():
			_set_merlin_state(MerlinState.EXECUTING_TOOL)
		await get_tree().create_timer(0.36).timeout


func _settle_orb_after_response() -> void:
	if _visual_overlay_hold_until_speech_end:
		_release_visual_overlay(_visual_overlay_kind)
	if not _legacy_state_changes_enabled():
		return
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
		"confirmation":
			return "confirmation"
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


func _show_application_choice_panel(confirmation: Dictionary, application_candidates: Array) -> void:
	_hide_application_choice_panel()
	if overlay_container == null or application_candidates.size() <= 1:
		return

	var panel := PanelContainer.new()
	_application_choice_panel = panel
	panel.name = "ApplicationChoicePanel"
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	panel.custom_minimum_size = Vector2(430, 0)
	panel.anchor_left = 0.5
	panel.anchor_right = 0.5
	panel.anchor_top = 0.12
	panel.anchor_bottom = 0.12
	panel.offset_left = -215
	panel.offset_right = 215
	panel.offset_top = 0
	panel.offset_bottom = 0
	panel.add_theme_stylebox_override("panel", _floating_choice_panel_style())

	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 14)
	margin.add_theme_constant_override("margin_top", 12)
	margin.add_theme_constant_override("margin_right", 14)
	margin.add_theme_constant_override("margin_bottom", 12)
	panel.add_child(margin)

	var stack := VBoxContainer.new()
	stack.add_theme_constant_override("separation", 8)
	margin.add_child(stack)

	var title := Label.new()
	title.text = "Choose application"
	title.add_theme_color_override("font_color", COLOR_WHITE)
	title.add_theme_font_size_override("font_size", 16)
	stack.add_child(title)

	var requested := str(confirmation.get("originalUserCommand", "")).strip_edges()
	var subtitle := Label.new()
	subtitle.text = "Say the full name shown, or select one."
	if not requested.is_empty():
		subtitle.text = "For: %s" % requested
	subtitle.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	subtitle.add_theme_color_override("font_color", COLOR_MUTED)
	subtitle.add_theme_font_size_override("font_size", 12)
	stack.add_child(subtitle)

	for candidate in application_candidates:
		if typeof(candidate) != TYPE_DICTIONARY:
			continue
		var display_name := str(candidate.get("displayName", "")).strip_edges()
		if display_name.is_empty():
			continue
		stack.add_child(_create_application_choice_row(display_name, str(candidate.get("source", ""))))

	overlay_container.add_child(panel)
	panel.modulate = Color(1, 1, 1, 0)
	var tween := create_tween()
	tween.tween_property(panel, "modulate:a", 1.0, 0.16)


func _hide_application_choice_panel() -> void:
	if not is_instance_valid(_application_choice_panel):
		_application_choice_panel = null
		return

	var panel := _application_choice_panel
	_application_choice_panel = null
	var tween := create_tween()
	tween.tween_property(panel, "modulate:a", 0.0, 0.12)
	tween.tween_callback(panel.queue_free)


func _setup_gesture_cursor() -> void:
	if not is_instance_valid(_gesture_cursor_layer):
		_gesture_cursor_layer = CanvasLayer.new()
		_gesture_cursor_layer.name = "GestureCursorLayer"
		_gesture_cursor_layer.layer = GESTURE_CURSOR_CANVAS_LAYER
		add_child(_gesture_cursor_layer)

	_ensure_gesture_cursor(GESTURE_POINTER_PRIMARY)


func _ensure_gesture_cursor(pointer_id: String) -> PanelContainer:
	if not is_instance_valid(_gesture_cursor_layer):
		_gesture_cursor_layer = CanvasLayer.new()
		_gesture_cursor_layer.name = "GestureCursorLayer"
		_gesture_cursor_layer.layer = GESTURE_CURSOR_CANVAS_LAYER
		add_child(_gesture_cursor_layer)

	if is_instance_valid(_gesture_cursor):
		return _gesture_cursor

	var cursor := PanelContainer.new()
	cursor.name = "GestureCursor"
	cursor.visible = false
	cursor.mouse_filter = Control.MOUSE_FILTER_IGNORE
	cursor.custom_minimum_size = Vector2(GESTURE_CURSOR_SIZE, GESTURE_CURSOR_SIZE)
	cursor.size = Vector2(GESTURE_CURSOR_SIZE, GESTURE_CURSOR_SIZE)
	cursor.z_index = 4096
	cursor.z_as_relative = false
	cursor.add_theme_stylebox_override("panel", _gesture_cursor_style(false))
	_gesture_cursor_layer.add_child(cursor)
	_gesture_cursor = cursor
	_set_gesture_cursor_position(pointer_id, get_viewport_rect().size * 0.5)
	return cursor


func _handle_ui_control_visual_event(event_name: String) -> bool:
	match event_name:
		"UI_CONTROL_MODE_STARTED":
			_set_ui_control_mode_active(true)
			return true
		"UI_CONTROL_MODE_STOPPED":
			_set_ui_control_mode_active(false)
			return true
	return false


func _handle_gesture_visual_event(event_name: String, event: Dictionary) -> bool:
	var pointer_id := str(event.get("pointerId", GESTURE_POINTER_PRIMARY))
	if pointer_id.is_empty():
		pointer_id = GESTURE_POINTER_PRIMARY
	match event_name:
		"GESTURE_POINTER_MOVE", "GESTURE_PINCH_MOVE":
			_gesture_pointer_move(pointer_id, _gesture_event_position(event))
			return true
		"GESTURE_PINCH_START":
			_gesture_pointer_move(pointer_id, _gesture_event_position(event))
			_gesture_pinch_start(pointer_id)
			return true
		"GESTURE_PINCH_END":
			_gesture_pinch_end(pointer_id)
			return true
	return false


func _gesture_event_position(event: Dictionary) -> Vector2:
	var viewport_size := get_viewport_rect().size
	var x := clampf(float(event.get("x", 0.5)), 0.0, 1.0)
	var y := clampf(float(event.get("y", 0.5)), 0.0, 1.0)
	return Vector2(x * viewport_size.x, y * viewport_size.y)


func _set_ui_control_mode_active(active: bool) -> void:
	if _ui_control_mode_active == active:
		return
	_ui_control_mode_active = active
	_gesture_ignore_logged = false
	if active:
		_setup_gesture_cursor()
		if not _gesture_pointer_positions.has(GESTURE_POINTER_PRIMARY):
			_gesture_pointer_positions[GESTURE_POINTER_PRIMARY] = get_viewport_rect().size * 0.5
		_gesture_visual_pointer_id = GESTURE_POINTER_PRIMARY
		_set_gesture_cursor_visible(GESTURE_POINTER_PRIMARY, true)
		_set_gesture_cursor_position(GESTURE_POINTER_PRIMARY, _gesture_pointer_positions[GESTURE_POINTER_PRIMARY])
		_update_gesture_hover(GESTURE_POINTER_PRIMARY)
	else:
		_cancel_crumple(false)
		_release_all_gesture_grabs()
		_gesture_hover_surface_id = ""
		_gesture_hover_surface_by_pointer.clear()
		_gesture_pointer_pinched.clear()
		_gesture_pending_pinches.clear()
		_gesture_pointer_history.clear()
		_clear_selected_surface()
		_gesture_visual_pointer_id = ""
		_set_gesture_cursor_visible(GESTURE_POINTER_PRIMARY, false)
		_set_gesture_cursor_pinched(GESTURE_POINTER_PRIMARY, false)
		_apply_chatlog_gesture_state()


func _handle_synthetic_gesture_input(event: InputEvent) -> bool:
	var is_synthetic_event := false
	if event is InputEventMouseMotion:
		is_synthetic_event = event.alt_pressed
	elif event is InputEventMouseButton:
		is_synthetic_event = event.alt_pressed and event.button_index == MOUSE_BUTTON_LEFT

	if not is_synthetic_event:
		return false
	if not _ui_control_mode_active:
		if not _gesture_ignore_logged:
			print("GestureEventIgnoredBecauseUiControlModeOff")
			_gesture_ignore_logged = true
		return false

	if event is InputEventMouseMotion:
		_gesture_pointer_move(GESTURE_POINTER_PRIMARY, event.position)
		return true
	if event is InputEventMouseButton:
		if event.pressed:
			_gesture_pinch_start(GESTURE_POINTER_PRIMARY)
		else:
			_gesture_pinch_end(GESTURE_POINTER_PRIMARY)
		return true
	return false


func _gesture_pointer_move(pointer_id: String, position: Vector2) -> void:
	if not _ui_control_mode_active:
		print("GestureEventIgnoredBecauseUiControlModeOff")
		return

	_gesture_pointer_positions[pointer_id] = position
	_record_gesture_pointer_history(pointer_id, position)
	if pointer_id == GESTURE_POINTER_PRIMARY:
		_set_active_gesture_cursor_pointer(pointer_id)
		_set_gesture_cursor_visible(pointer_id, true)
		_set_gesture_cursor_position(pointer_id, position)
	if _crumple_is_active():
		_update_crumple()
	elif _gesture_resize_is_active():
		_update_gesture_resize()
	elif pointer_id == GESTURE_POINTER_PRIMARY and _gesture_grabs.has(pointer_id):
		_move_gesture_grab(pointer_id, position)
		_try_start_gesture_resize()
	elif pointer_id == GESTURE_POINTER_PRIMARY:
		_maybe_promote_pending_pinch(pointer_id)
		_update_gesture_hover(pointer_id)
	elif bool(_gesture_pointer_pinched.get(pointer_id, false)):
		_maybe_promote_pending_pinch(pointer_id)
		_try_start_gesture_resize()


func _gesture_pinch_start(pointer_id: String) -> void:
	if not _ui_control_mode_active:
		print("GestureEventIgnoredBecauseUiControlModeOff")
		return
	if not _gesture_pointer_positions.has(pointer_id):
		return

	_gesture_pointer_pinched[pointer_id] = true
	var surface_id: String = _combined_gesture_hover_surface() if pointer_id == GESTURE_POINTER_PRIMARY else _surface_at_gesture_position(_gesture_pointer_positions[pointer_id])
	if pointer_id == GESTURE_POINTER_PRIMARY and surface_id.is_empty():
		surface_id = _surface_at_gesture_position(_gesture_pointer_positions[pointer_id])
	_gesture_hover_surface_by_pointer[pointer_id] = surface_id
	_gesture_pending_pinches[pointer_id] = {
		"start_usec": Time.get_ticks_usec(),
		"start_position": _gesture_pointer_positions[pointer_id],
		"target_surface_id": surface_id
	}
	_record_gesture_pointer_history(pointer_id, _gesture_pointer_positions[pointer_id])
	_try_start_gesture_resize()
	_apply_chatlog_gesture_state()
	if pointer_id == GESTURE_POINTER_PRIMARY:
		_set_gesture_cursor_pinched(pointer_id, true)


func _gesture_pinch_end(pointer_id: String) -> void:
	var released_usec := Time.get_ticks_usec()
	_gesture_pointer_pinched.erase(pointer_id)
	if _gesture_resize_uses_pointer(pointer_id):
		_finish_gesture_resize(pointer_id)
	elif _gesture_pending_pinches.has(pointer_id):
		_finish_pending_pinch(pointer_id, released_usec)
	if _gesture_grabs.has(pointer_id):
		_gesture_grabs.erase(pointer_id)
	_gesture_pending_pinches.erase(pointer_id)
	_gesture_hover_surface_by_pointer.erase(pointer_id)
	if pointer_id == GESTURE_POINTER_PRIMARY:
		_set_gesture_cursor_pinched(pointer_id, false)
		_update_gesture_hover(pointer_id)
	_apply_chatlog_gesture_state()


func _update_pending_gesture_pinches() -> void:
	if not _ui_control_mode_active:
		return
	for pointer_id in _gesture_pending_pinches.keys():
		_maybe_promote_pending_pinch(str(pointer_id))


func _maybe_promote_pending_pinch(pointer_id: String) -> void:
	if _crumple_is_active() or not _selected_surface_id.is_empty() or not _gesture_pending_pinches.has(pointer_id) or _gesture_grabs.has(pointer_id):
		return
	var pending: Dictionary = _gesture_pending_pinches.get(pointer_id, {})
	var position: Vector2 = _gesture_pointer_positions.get(pointer_id, pending.get("start_position", Vector2.ZERO))
	var start_position: Vector2 = pending.get("start_position", position)
	var elapsed_ms := float(Time.get_ticks_usec() - int(pending.get("start_usec", Time.get_ticks_usec()))) / 1000.0
	var moved_px := start_position.distance_to(position)
	if moved_px < DRAG_START_MOVEMENT_PX and elapsed_ms < PINCH_HOLD_TO_GRAB_MS:
		return
	_promote_pending_pinch_to_grab(pointer_id)


func _promote_pending_pinch_to_grab(pointer_id: String) -> void:
	if not _gesture_pending_pinches.has(pointer_id) or not _gesture_pointer_positions.has(pointer_id):
		return
	var pending: Dictionary = _gesture_pending_pinches.get(pointer_id, {})
	var surface_id := str(pending.get("target_surface_id", ""))
	var target_window = _window_for_surface_id(surface_id)
	if pointer_id == GESTURE_POINTER_PRIMARY and is_instance_valid(target_window):
		if is_instance_valid(_window_manager):
			_window_manager.focus_window(target_window)
		_gesture_grabs[pointer_id] = {
			"surface_id": target_window.window_type,
			"start_position": _gesture_pointer_positions[pointer_id],
			"panel_start_position": target_window.position
		}
	_gesture_pending_pinches.erase(pointer_id)
	_try_start_gesture_resize()
	_apply_chatlog_gesture_state()


func _finish_pending_pinch(pointer_id: String, released_usec: int) -> void:
	if pointer_id != GESTURE_POINTER_PRIMARY:
		return
	var pending: Dictionary = _gesture_pending_pinches.get(pointer_id, {})
	var start_position: Vector2 = pending.get("start_position", _gesture_pointer_positions.get(pointer_id, Vector2.ZERO))
	var release_position: Vector2 = _gesture_pointer_positions.get(pointer_id, start_position)
	var duration_ms := float(released_usec - int(pending.get("start_usec", released_usec))) / 1000.0
	var moved_px := start_position.distance_to(release_position)
	if duration_ms <= PINCH_TAP_MAX_DURATION_MS and moved_px <= PINCH_TAP_MAX_MOVEMENT_PX:
		_select_surface(str(pending.get("target_surface_id", "")))


func _move_gesture_grab(pointer_id: String, position: Vector2) -> void:
	var grab: Dictionary = _gesture_grabs.get(pointer_id, {})
	var grabbed_window = _window_for_surface_id(str(grab.get("surface_id", "")))
	if not is_instance_valid(grabbed_window):
		return
	if grabbed_window.window_type == _selected_surface_id:
		_gesture_grabs.erase(pointer_id)
		return

	var start_position: Vector2 = grab.get("start_position", position)
	var panel_start_position: Vector2 = grab.get("panel_start_position", grabbed_window.position)
	var delta := position - start_position
	grabbed_window.move_to(panel_start_position + delta)


func _try_start_gesture_resize() -> void:
	if _gesture_resize_is_active() or _crumple_is_active() or not _selected_surface_id.is_empty():
		return
	if not _gesture_grabs.has(GESTURE_POINTER_PRIMARY) or not bool(_gesture_pointer_pinched.get(GESTURE_POINTER_PRIMARY, false)):
		return
	var primary_grab: Dictionary = _gesture_grabs.get(GESTURE_POINTER_PRIMARY, {})
	var grabbed_window = _window_for_surface_id(str(primary_grab.get("surface_id", "")))
	if not is_instance_valid(grabbed_window) or not grabbed_window.has_capability(MerlinWindowConstantsScript.CAPABILITY_GESTURE_RESIZE):
		return
	if not _gesture_pointer_positions.has(GESTURE_POINTER_PRIMARY) or not _gesture_pointer_positions.has(GESTURE_POINTER_SECONDARY):
		return
	if not bool(_gesture_pointer_pinched.get(GESTURE_POINTER_SECONDARY, false)):
		return

	var pointer_a := GESTURE_POINTER_PRIMARY
	var pointer_b := GESTURE_POINTER_SECONDARY
	var position_a: Vector2 = _gesture_pointer_positions[pointer_a]
	var position_b: Vector2 = _gesture_pointer_positions[pointer_b]
	var start_distance := position_a.distance_to(position_b)
	if start_distance < GESTURE_MIN_RESIZE_START_DISTANCE_PX:
		return
	var start_delta := position_b - position_a
	var resize_axes := _gesture_resize_axes_for_delta(start_delta)
	if resize_axes.is_empty():
		return

	_gesture_resize_state = {
		"surface_id": grabbed_window.window_type,
		"pointer_a": pointer_a,
		"pointer_b": pointer_b,
		"start_a": position_a,
		"start_b": position_b,
		"start_delta": start_delta,
		"start_distance": start_distance,
		"start_midpoint": (position_a + position_b) * 0.5,
		"start_rect": grabbed_window.get_window_rect(),
		"resize_axes": resize_axes
	}
	_apply_chatlog_gesture_state()


func _gesture_resize_is_active() -> bool:
	return not _gesture_resize_state.is_empty()


func _gesture_resize_uses_pointer(pointer_id: String) -> bool:
	if not _gesture_resize_is_active():
		return false
	return str(_gesture_resize_state.get("pointer_a", "")) == pointer_id or str(_gesture_resize_state.get("pointer_b", "")) == pointer_id


func _gesture_resize_axes_for_delta(start_delta: Vector2) -> String:
	var abs_dx := absf(start_delta.x)
	var abs_dy := absf(start_delta.y)
	if abs_dx < GESTURE_MIN_RESIZE_AXIS_DISTANCE_PX and abs_dy < GESTURE_MIN_RESIZE_AXIS_DISTANCE_PX:
		return ""

	if abs_dx >= GESTURE_MIN_RESIZE_AXIS_DISTANCE_PX and abs_dy >= GESTURE_MIN_RESIZE_AXIS_DISTANCE_PX:
		var smaller_axis := minf(abs_dx, abs_dy)
		var larger_axis := maxf(abs_dx, abs_dy)
		if larger_axis > 0.0 and smaller_axis / larger_axis >= GESTURE_DIAGONAL_RESIZE_RATIO:
			return GESTURE_RESIZE_AXIS_BOTH

	if abs_dx >= abs_dy and abs_dx >= GESTURE_MIN_RESIZE_AXIS_DISTANCE_PX:
		return GESTURE_RESIZE_AXIS_WIDTH
	if abs_dy >= GESTURE_MIN_RESIZE_AXIS_DISTANCE_PX:
		return GESTURE_RESIZE_AXIS_HEIGHT
	return ""


func _update_gesture_resize() -> void:
	if not _gesture_resize_is_active():
		return
	var target_window = _window_for_surface_id(str(_gesture_resize_state.get("surface_id", "")))
	if not is_instance_valid(target_window):
		return

	var pointer_a := str(_gesture_resize_state.get("pointer_a", ""))
	var pointer_b := str(_gesture_resize_state.get("pointer_b", ""))
	if not _gesture_pointer_positions.has(pointer_a) or not _gesture_pointer_positions.has(pointer_b):
		return

	var position_a: Vector2 = _gesture_pointer_positions[pointer_a]
	var position_b: Vector2 = _gesture_pointer_positions[pointer_b]
	var start_midpoint: Vector2 = _gesture_resize_state.get("start_midpoint", (position_a + position_b) * 0.5)
	var start_rect: Rect2 = _gesture_resize_state.get("start_rect", target_window.get_window_rect())
	var start_delta: Vector2 = _gesture_resize_state.get("start_delta", position_b - position_a)
	var resize_axes := str(_gesture_resize_state.get("resize_axes", GESTURE_RESIZE_AXIS_BOTH))
	var current_delta := position_b - position_a
	var current_midpoint := (position_a + position_b) * 0.5
	var width_scale := 1.0
	var height_scale := 1.0
	if resize_axes == GESTURE_RESIZE_AXIS_WIDTH or resize_axes == GESTURE_RESIZE_AXIS_BOTH:
		var start_dx := maxf(absf(start_delta.x), GESTURE_MIN_RESIZE_AXIS_DISTANCE_PX)
		width_scale = absf(current_delta.x) / start_dx
	if resize_axes == GESTURE_RESIZE_AXIS_HEIGHT or resize_axes == GESTURE_RESIZE_AXIS_BOTH:
		var start_dy := maxf(absf(start_delta.y), GESTURE_MIN_RESIZE_AXIS_DISTANCE_PX)
		height_scale = absf(current_delta.y) / start_dy
	_set_gesture_cursor_size_multiplier(clampf(maxf(width_scale, height_scale), 0.75, 1.8))
	var target_size := Vector2(
		clampf(start_rect.size.x * width_scale, CHATLOG_MIN_WIDTH, CHATLOG_MAX_WIDTH),
		clampf(start_rect.size.y * height_scale, CHATLOG_MIN_HEIGHT, CHATLOG_MAX_HEIGHT)
	)
	var start_center := start_rect.position + start_rect.size * 0.5
	var target_center := start_center + (current_midpoint - start_midpoint)
	var target_position := target_center - target_size * 0.5

	target_window.set_window_rect(Rect2(
		target_window.position.lerp(target_position, GESTURE_RESIZE_SMOOTHING),
		target_window.size.lerp(target_size, GESTURE_RESIZE_SMOOTHING)
	))


func _finish_gesture_resize(released_pointer_id: String) -> void:
	if not _gesture_resize_is_active():
		return

	var pointer_a := str(_gesture_resize_state.get("pointer_a", ""))
	var resized_window = _window_for_surface_id(str(_gesture_resize_state.get("surface_id", MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG)))
	_gesture_resize_state.clear()
	_set_gesture_cursor_size_multiplier(1.0)

	if released_pointer_id == pointer_a or released_pointer_id == GESTURE_POINTER_PRIMARY:
		_gesture_grabs.erase(GESTURE_POINTER_PRIMARY)
		_set_gesture_cursor_pinched(GESTURE_POINTER_PRIMARY, false)
		_apply_chatlog_gesture_state()
		return

	if not _gesture_pointer_pinched.has(GESTURE_POINTER_PRIMARY) or not _gesture_pointer_positions.has(GESTURE_POINTER_PRIMARY):
		_apply_chatlog_gesture_state()
		return

	if is_instance_valid(resized_window):
		_gesture_grabs[GESTURE_POINTER_PRIMARY] = {
			"surface_id": resized_window.window_type,
			"start_position": _gesture_pointer_positions[GESTURE_POINTER_PRIMARY],
			"panel_start_position": resized_window.position
		}
	_apply_chatlog_gesture_state()


func _update_selected_ball_forming() -> void:
	if not _ui_control_mode_active or _selected_surface_id.is_empty():
		return
	if bool(_gesture_pointer_pinched.get(GESTURE_POINTER_PRIMARY, false)) or bool(_gesture_pointer_pinched.get(GESTURE_POINTER_SECONDARY, false)):
		return
	if not _both_ball_form_hands_are_fresh():
		if _crumple_is_active() and not bool(_crumple_state.get("ball_formed", false)):
			_cancel_crumple(true)
		return
	if _gesture_resize_is_active() or _gesture_grabs.has(GESTURE_POINTER_PRIMARY):
		_release_all_gesture_grabs()
	if _crumple_is_active():
		_update_crumple()
	else:
		_try_start_crumple_candidate()


func _try_start_crumple_candidate() -> void:
	if _crumple_is_active() or _selected_surface_id.is_empty():
		return
	if not _gesture_pointer_positions.has(GESTURE_POINTER_PRIMARY) or not _gesture_pointer_positions.has(GESTURE_POINTER_SECONDARY):
		return
	if not _both_ball_form_hands_are_fresh():
		return
	var selected_window = _window_for_surface_id(_selected_surface_id)
	if not _is_gesture_dismissible_window(selected_window):
		return
	var target_rect: Rect2 = selected_window.get_global_rect().grow(BALL_FORM_TARGET_PADDING_PX)
	var primary_position: Vector2 = _gesture_pointer_positions[GESTURE_POINTER_PRIMARY]
	var secondary_position: Vector2 = _gesture_pointer_positions[GESTURE_POINTER_SECONDARY]
	if not target_rect.has_point(primary_position) or not target_rect.has_point(secondary_position):
		return
	var initial_distance: float = maxf(primary_position.distance_to(secondary_position), GESTURE_MIN_RESIZE_START_DISTANCE_PX)
	var midpoint: Vector2 = (primary_position + secondary_position) * 0.5
	_crumple_state = {
		"surface_id": _selected_surface_id,
		"pointer_a": GESTURE_POINTER_PRIMARY,
		"pointer_b": GESTURE_POINTER_SECONDARY,
		"start_usec": Time.get_ticks_usec(),
		"formed_usec": 0,
		"initial_distance": initial_distance,
		"min_distance": initial_distance,
		"accumulated_motion": 0.0,
		"orbit_motion": 0.0,
		"confidence": 0.0,
		"ball_formed": false,
		"last_a": primary_position,
		"last_b": secondary_position,
		"last_midpoint": midpoint,
		"last_angle": (secondary_position - primary_position).angle(),
		"original_rect": selected_window.get_window_rect(),
		"original_scale": selected_window.scale,
		"original_rotation": selected_window.rotation,
		"original_modulate": selected_window.modulate
	}
	selected_window.pivot_offset = selected_window.size * 0.5
	_create_crumple_proxy(selected_window)
	selected_window.modulate = Color(1.0, 1.0, 1.0, 0.0)
	_apply_chatlog_gesture_state()


func _crumple_is_active() -> bool:
	return not _crumple_state.is_empty()


func _crumple_uses_pointer(pointer_id: String) -> bool:
	if not _crumple_is_active():
		return false
	return str(_crumple_state.get("pointer_a", "")) == pointer_id or str(_crumple_state.get("pointer_b", "")) == pointer_id


func _update_crumple() -> void:
	if not _crumple_is_active():
		return
	var target_window = _window_for_surface_id(str(_crumple_state.get("surface_id", "")))
	if not is_instance_valid(target_window):
		_cancel_crumple(false)
		return
	var pointer_a: String = str(_crumple_state.get("pointer_a", ""))
	var pointer_b: String = str(_crumple_state.get("pointer_b", ""))
	var ball_formed: bool = bool(_crumple_state.get("ball_formed", false))
	if not ball_formed and not _both_ball_form_hands_are_fresh():
		_cancel_crumple(true)
		return
	if ball_formed:
		_update_formed_crumple_ball(target_window, pointer_a, pointer_b)
		return
	if not _gesture_pointer_positions.has(pointer_a) or not _gesture_pointer_positions.has(pointer_b):
		_cancel_crumple(true)
		return
	var position_a: Vector2 = _gesture_pointer_positions[pointer_a]
	var position_b: Vector2 = _gesture_pointer_positions[pointer_b]

	var now_usec: int = Time.get_ticks_usec()
	var start_usec: int = int(_crumple_state.get("start_usec", now_usec))
	var elapsed_ms: float = float(now_usec - start_usec) / 1000.0
	var original_rect: Rect2 = _crumple_state.get("original_rect", target_window.get_window_rect())
	var target_rect: Rect2 = original_rect.grow(BALL_FORM_TARGET_PADDING_PX * 1.4)
	if not ball_formed and (not target_rect.has_point(position_a) or not target_rect.has_point(position_b)):
		_cancel_crumple(true)
		return

	var last_a: Vector2 = _crumple_state.get("last_a", position_a)
	var last_b: Vector2 = _crumple_state.get("last_b", position_b)
	var current_distance: float = position_a.distance_to(position_b)
	var initial_distance: float = maxf(float(_crumple_state.get("initial_distance", current_distance)), 1.0)
	var min_distance: float = minf(float(_crumple_state.get("min_distance", initial_distance)), current_distance)
	var move_a: float = position_a.distance_to(last_a)
	var move_b: float = position_b.distance_to(last_b)
	var accumulated_motion: float = float(_crumple_state.get("accumulated_motion", 0.0)) + move_a + move_b
	var current_angle: float = (position_b - position_a).angle()
	var last_angle: float = float(_crumple_state.get("last_angle", current_angle))
	var angle_delta: float = absf(wrapf(current_angle - last_angle, -PI, PI))
	var orbit_motion: float = float(_crumple_state.get("orbit_motion", 0.0)) + angle_delta
	var compact_ratio: float = clampf(min_distance / initial_distance, 0.35, 1.0)
	var motion_score: float = clampf(accumulated_motion / BALL_FORM_MIN_MOTION_PX, 0.0, 1.0)
	var compact_score: float = clampf((1.0 - compact_ratio) / (1.0 - BALL_FORM_COMPACT_RATIO), 0.0, 1.0)
	var orbit_score: float = clampf(orbit_motion / 2.2, 0.0, 1.0)
	var confidence: float = clampf(motion_score * 0.45 + compact_score * 0.35 + orbit_score * 0.20, 0.0, 1.25)
	if not ball_formed and elapsed_ms >= BALL_FORM_MIN_DURATION_MS and confidence >= BALL_FORM_MIN_CONFIDENCE:
		ball_formed = true
		_crumple_state["ball_formed"] = true
		_crumple_state["formed_usec"] = now_usec

	_crumple_state["min_distance"] = min_distance
	_crumple_state["accumulated_motion"] = accumulated_motion
	_crumple_state["orbit_motion"] = orbit_motion
	_crumple_state["confidence"] = confidence
	_crumple_state["last_a"] = position_a
	_crumple_state["last_b"] = position_b
	_crumple_state["last_midpoint"] = (position_a + position_b) * 0.5
	_crumple_state["last_angle"] = current_angle

	target_window.scale = Vector2.ONE
	target_window.rotation = 0.0
	target_window.modulate = Color(1.0, 1.0, 1.0, 0.0)
	if is_instance_valid(_crumple_proxy):
		_crumple_proxy.set_crumple_progress(clampf(confidence, 0.0, 1.0), ball_formed)
		if ball_formed:
			_crumple_proxy.bake_final_ball_async()
	var throw_velocity: Vector2 = _rightward_swipe_velocity([GESTURE_POINTER_PRIMARY])
	_last_throw_velocity = throw_velocity
	var formed_usec: int = int(_crumple_state.get("formed_usec", 0))
	var throw_unlocked: bool = ball_formed and formed_usec > 0 and float(now_usec - formed_usec) / 1000.0 >= BALL_FORM_THROW_LOCKOUT_MS
	if throw_unlocked and throw_velocity != Vector2.ZERO:
		_commit_crumple_dismiss(throw_velocity)
		return
	_apply_chatlog_gesture_state()


func _update_formed_crumple_ball(target_window, _pointer_a: String, _pointer_b: String) -> void:
	target_window.scale = Vector2.ONE
	target_window.rotation = 0.0
	target_window.modulate = Color(1.0, 1.0, 1.0, 0.0)
	if is_instance_valid(_crumple_proxy):
		_crumple_proxy.set_crumple_progress(1.0, true)
		_crumple_proxy.bake_final_ball_async()
	if not _gesture_pointer_positions.has(GESTURE_POINTER_PRIMARY):
		_cancel_crumple(true)
		return
	var pointer_position: Vector2 = _gesture_pointer_positions[GESTURE_POINTER_PRIMARY]
	var desired_proxy_position: Vector2 = pointer_position - (_crumple_proxy.size * 0.5 if is_instance_valid(_crumple_proxy) else Vector2.ZERO)
	if is_instance_valid(_crumple_proxy):
		_crumple_proxy.position = desired_proxy_position
		if not _crumple_proxy.is_baked():
			_crumple_proxy.queue_redraw()
	var throw_velocity: Vector2 = _rightward_swipe_velocity([GESTURE_POINTER_PRIMARY])
	_last_throw_velocity = throw_velocity
	var now_usec: int = Time.get_ticks_usec()
	var formed_usec: int = int(_crumple_state.get("formed_usec", 0))
	var throw_unlocked: bool = formed_usec > 0 and float(now_usec - formed_usec) / 1000.0 >= BALL_FORM_THROW_LOCKOUT_MS
	if throw_unlocked and throw_velocity != Vector2.ZERO:
		_commit_crumple_dismiss(throw_velocity)
		return
	_apply_chatlog_gesture_state()


func _update_crumple_timeout() -> void:
	if not _crumple_is_active():
		return
	var elapsed_ms: float = float(Time.get_ticks_usec() - int(_crumple_state.get("start_usec", Time.get_ticks_usec()))) / 1000.0
	if elapsed_ms > BALL_FORM_CANCEL_TIMEOUT_MS and not bool(_crumple_state.get("ball_formed", false)):
		_cancel_crumple(true)


func _cancel_crumple(keep_selection: bool) -> void:
	if not _crumple_is_active():
		return
	var surface_id := str(_crumple_state.get("surface_id", ""))
	var target_window = _window_for_surface_id(surface_id)
	if is_instance_valid(target_window):
		target_window.scale = _crumple_state.get("original_scale", Vector2.ONE)
		target_window.rotation = float(_crumple_state.get("original_rotation", 0.0))
		target_window.modulate = _crumple_state.get("original_modulate", Color.WHITE)
	if is_instance_valid(_crumple_proxy):
		_crumple_proxy.queue_free()
	_crumple_proxy = null
	_crumple_state.clear()
	if not keep_selection:
		_clear_selected_surface()
	_apply_chatlog_gesture_state()


func _commit_crumple_dismiss(throw_velocity: Vector2) -> void:
	var surface_id := str(_crumple_state.get("surface_id", ""))
	var target_window = _window_for_surface_id(surface_id)
	if not _is_gesture_dismissible_window(target_window):
		_cancel_crumple(true)
		return
	_throw_crumple_proxy(target_window, throw_velocity)
	target_window.dismiss()
	target_window.scale = _crumple_state.get("original_scale", Vector2.ONE)
	target_window.rotation = float(_crumple_state.get("original_rotation", 0.0))
	target_window.modulate = _crumple_state.get("original_modulate", Color.WHITE)
	_crumple_state.clear()
	_crumple_proxy = null
	_clear_selected_surface()
	_gesture_grabs.clear()
	_gesture_pending_pinches.clear()
	_apply_chatlog_gesture_state()


func _create_crumple_proxy(source_window) -> void:
	if not is_instance_valid(source_window):
		return
	if is_instance_valid(_crumple_proxy):
		_crumple_proxy.queue_free()
	var proxy = CrumpleSheetProxyScript.new()
	proxy.name = "CrumpleSheetProxy"
	add_child(proxy)
	proxy.configure_from_window(source_window)
	proxy.set_crumple_progress(0.0, false)
	_crumple_proxy = proxy


func _throw_crumple_proxy(source_window, throw_velocity: Vector2) -> void:
	if not is_instance_valid(_crumple_proxy):
		_create_crumple_proxy(source_window)
	if not is_instance_valid(_crumple_proxy):
		_spawn_crumple_throw_proxy(source_window, throw_velocity)
		return
	_crumple_proxy.set_crumple_progress(1.0, true)
	_crumple_proxy.bake_final_ball_async()
	_crumple_proxy.throw_and_free(
		throw_velocity,
		get_viewport_rect().size,
		THROW_ANIMATION_MIN_MS,
		THROW_ANIMATION_MAX_MS)


func _spawn_crumple_throw_proxy(source_window, throw_velocity: Vector2) -> void:
	if not is_instance_valid(source_window):
		return
	var proxy := PanelContainer.new()
	proxy.name = "CrumpledWindowProxy"
	proxy.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var source_rect: Rect2 = source_window.get_global_rect()
	proxy.position = source_rect.position + source_rect.size * 0.5 - Vector2(42.0, 30.0)
	proxy.size = Vector2(84.0, 60.0)
	proxy.pivot_offset = proxy.size * 0.5
	proxy.z_index = 4095
	proxy.z_as_relative = false
	var style := _panel_style(Color(0.012, 0.040, 0.088, 0.86), COLOR_AMBER, 2.0, 8)
	style.shadow_color = Color(COLOR_AMBER.r, COLOR_AMBER.g, COLOR_AMBER.b, 0.58)
	style.shadow_size = 24
	proxy.add_theme_stylebox_override("panel", style)
	add_child(proxy)
	var direction := throw_velocity.normalized() if throw_velocity.length() > 0.0 else Vector2(1.0, -0.35).normalized()
	var viewport_size := get_viewport_rect().size
	var distance := maxf(viewport_size.x, viewport_size.y) + 260.0
	var duration_ms := clampf(520000.0 / maxf(throw_velocity.length(), 1.0), THROW_ANIMATION_MIN_MS, THROW_ANIMATION_MAX_MS)
	var tween := create_tween()
	tween.set_parallel(true)
	tween.tween_property(proxy, "position", proxy.position + direction * distance, duration_ms / 1000.0).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_property(proxy, "rotation", proxy.rotation + direction.x * 1.35, duration_ms / 1000.0)
	tween.tween_property(proxy, "modulate:a", 0.0, duration_ms / 1000.0).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN)
	tween.tween_property(proxy, "scale", Vector2(0.45, 0.45), duration_ms / 1000.0)
	tween.set_parallel(false)
	tween.tween_callback(proxy.queue_free)


func _is_gesture_dismissible_window(window) -> bool:
	return is_instance_valid(window) and window.visible and window.has_capability(MerlinWindowConstantsScript.CAPABILITY_GESTURE_DISMISS)


func _both_ball_form_hands_are_fresh() -> bool:
	return _pointer_is_fresh(GESTURE_POINTER_PRIMARY, BALL_FORM_TWO_HAND_FRESH_MS) and _pointer_is_fresh(GESTURE_POINTER_SECONDARY, BALL_FORM_TWO_HAND_FRESH_MS)


func _pointer_is_fresh(pointer_id: String, max_age_ms: int) -> bool:
	var history: Array = _gesture_pointer_history.get(pointer_id, [])
	if history.is_empty():
		return false
	var latest: Dictionary = history[history.size() - 1]
	var age_ms: float = float(Time.get_ticks_usec() - int(latest.get("usec", 0))) / 1000.0
	return age_ms <= float(max_age_ms)


func _record_gesture_pointer_history(pointer_id: String, position: Vector2) -> void:
	var history: Array = _gesture_pointer_history.get(pointer_id, [])
	history.append({
		"usec": Time.get_ticks_usec(),
		"position": position
	})
	var cutoff_usec := Time.get_ticks_usec() - THROW_RIGHT_SWIPE_HISTORY_WINDOW_MS * 1000
	while history.size() > 0:
		var first_sample: Dictionary = history[0]
		if int(first_sample.get("usec", 0)) >= cutoff_usec:
			break
		history.remove_at(0)
	_gesture_pointer_history[pointer_id] = history


func _average_pointer_velocity(pointer_ids: Array, window_ms: int) -> Vector2:
	var total := Vector2.ZERO
	var count := 0
	for pointer_id in pointer_ids:
		var velocity: Vector2 = _pointer_velocity(str(pointer_id), window_ms)
		if velocity == Vector2.ZERO:
			continue
		total += velocity
		count += 1
	return total / float(count) if count > 0 else Vector2.ZERO


func _strongest_pointer_velocity(pointer_ids: Array, window_ms: int) -> Vector2:
	var strongest := Vector2.ZERO
	for pointer_id in pointer_ids:
		var velocity: Vector2 = _pointer_velocity(str(pointer_id), window_ms)
		if velocity.length() > strongest.length():
			strongest = velocity
	return strongest


func _rightward_swipe_velocity(pointer_ids: Array) -> Vector2:
	var strongest_velocity: Vector2 = Vector2.ZERO
	for pointer_id in pointer_ids:
		var candidate: String = str(pointer_id)
		if not _gesture_pointer_positions.has(candidate):
			continue
		var velocity: Vector2 = _pointer_velocity(candidate, THROW_RIGHT_SWIPE_HISTORY_WINDOW_MS)
		var delta: Vector2 = _pointer_delta(candidate, THROW_RIGHT_SWIPE_HISTORY_WINDOW_MS)
		if velocity.x < THROW_RIGHT_SWIPE_MIN_X_VELOCITY_PX_PER_SEC:
			continue
		if delta.x < THROW_RIGHT_SWIPE_MIN_DISTANCE_PX:
			continue
		if absf(delta.y) > maxf(24.0, delta.x * THROW_RIGHT_SWIPE_MAX_VERTICAL_RATIO):
			continue
		if velocity.length() > strongest_velocity.length():
			strongest_velocity = velocity
	return strongest_velocity


func _strongest_available_pointer(pointer_ids: Array, window_ms: int) -> String:
	var strongest_pointer := ""
	var strongest_speed := -1.0
	for pointer_id in pointer_ids:
		var candidate := str(pointer_id)
		if not _gesture_pointer_positions.has(candidate):
			continue
		var velocity: Vector2 = _pointer_velocity(candidate, window_ms)
		var speed := velocity.length()
		if speed > strongest_speed:
			strongest_pointer = candidate
			strongest_speed = speed
	return strongest_pointer


func _pointer_velocity(pointer_id: String, window_ms: int) -> Vector2:
	var history: Array = _gesture_pointer_history.get(pointer_id, [])
	if history.size() < 2:
		return Vector2.ZERO
	var latest: Dictionary = history[history.size() - 1]
	var latest_usec: int = int(latest.get("usec", Time.get_ticks_usec()))
	var earliest: Dictionary = history[0]
	for sample in history:
		var candidate: Dictionary = sample
		if latest_usec - int(candidate.get("usec", latest_usec)) <= window_ms * 1000:
			earliest = candidate
			break
	var delta_usec: int = latest_usec - int(earliest.get("usec", latest_usec))
	if delta_usec < 1:
		delta_usec = 1
	var delta_position: Vector2 = latest.get("position", Vector2.ZERO) - earliest.get("position", Vector2.ZERO)
	return delta_position / (float(delta_usec) / 1000000.0)


func _pointer_delta(pointer_id: String, window_ms: int) -> Vector2:
	var history: Array = _gesture_pointer_history.get(pointer_id, [])
	if history.size() < 2:
		return Vector2.ZERO
	var latest: Dictionary = history[history.size() - 1]
	var latest_usec: int = int(latest.get("usec", Time.get_ticks_usec()))
	var earliest: Dictionary = history[0]
	for sample in history:
		var candidate: Dictionary = sample
		if latest_usec - int(candidate.get("usec", latest_usec)) <= window_ms * 1000:
			earliest = candidate
			break
	return latest.get("position", Vector2.ZERO) - earliest.get("position", Vector2.ZERO)


func _update_gesture_hover(pointer_id: String) -> void:
	if pointer_id != GESTURE_POINTER_PRIMARY or not _ui_control_mode_active or _gesture_grabs.has(pointer_id):
		return

	var hovered_surface := _surface_at_gesture_position(_gesture_pointer_positions[pointer_id]) if _gesture_pointer_positions.has(pointer_id) else ""
	_gesture_hover_surface_by_pointer[pointer_id] = hovered_surface
	var combined_hover := _combined_gesture_hover_surface()
	if _gesture_hover_surface_id != combined_hover:
		_gesture_hover_surface_id = combined_hover
		_apply_chatlog_gesture_state()


func _surface_at_gesture_position(position: Vector2) -> String:
	if is_instance_valid(_window_manager):
		var window = _window_manager.get_topmost_window_at(position, MerlinWindowConstantsScript.CAPABILITY_GESTURE_GRAB)
		if is_instance_valid(window):
			return window.window_type
	return ""


func _window_for_surface_id(surface_id: String):
	if surface_id.is_empty() or not is_instance_valid(_window_manager):
		return null
	return _window_manager.get_registered_window(surface_id)


func _combined_gesture_hover_surface() -> String:
	return str(_gesture_hover_surface_by_pointer.get(GESTURE_POINTER_PRIMARY, ""))


func _is_gesture_over_chatlog_panel(position: Vector2) -> bool:
	var window = _window_for_surface_id(MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG)
	return is_instance_valid(window) and window.contains_screen_point(position)


func _set_gesture_cursor_position(pointer_id: String, position: Vector2) -> void:
	var cursor := _ensure_gesture_cursor(pointer_id)
	if not is_instance_valid(cursor):
		return
	cursor.position = position - cursor.size * 0.5


func _set_gesture_cursor_visible(pointer_id: String, visible: bool) -> void:
	var cursor := _ensure_gesture_cursor(pointer_id)
	if is_instance_valid(cursor):
		cursor.visible = visible


func _set_gesture_cursor_pinched(pointer_id: String, pinched: bool) -> void:
	if not _gesture_visual_pointer_id.is_empty() and pointer_id != _gesture_visual_pointer_id:
		return
	var cursor := _ensure_gesture_cursor(pointer_id)
	if is_instance_valid(cursor):
		cursor.add_theme_stylebox_override("panel", _gesture_cursor_style(pinched))


func _set_gesture_cursor_size_multiplier(multiplier: float) -> void:
	var cursor := _ensure_gesture_cursor(_gesture_visual_pointer_id if not _gesture_visual_pointer_id.is_empty() else GESTURE_POINTER_PRIMARY)
	if not is_instance_valid(cursor):
		return
	var size := Vector2(GESTURE_CURSOR_SIZE, GESTURE_CURSOR_SIZE) * multiplier
	cursor.custom_minimum_size = size
	cursor.size = size
	if not _gesture_visual_pointer_id.is_empty() and _gesture_pointer_positions.has(_gesture_visual_pointer_id):
		_set_gesture_cursor_position(_gesture_visual_pointer_id, _gesture_pointer_positions[_gesture_visual_pointer_id])


func _set_active_gesture_cursor_pointer(pointer_id: String) -> void:
	if pointer_id != GESTURE_POINTER_PRIMARY:
		return
	_gesture_visual_pointer_id = pointer_id
	if _gesture_pointer_positions.has(pointer_id):
		_set_gesture_cursor_position(pointer_id, _gesture_pointer_positions[pointer_id])
	_set_gesture_cursor_visible(pointer_id, true)
	_set_gesture_cursor_pinched(pointer_id, bool(_gesture_pointer_pinched.get(pointer_id, false)))


func _release_all_gesture_grabs() -> void:
	_gesture_grabs.clear()
	_gesture_resize_state.clear()
	_set_gesture_cursor_size_multiplier(1.0)


func _chatlog_is_gesture_grabbed() -> bool:
	if _gesture_resize_is_active():
		return true
	for grab in _gesture_grabs.values():
		if grab is Dictionary and str(grab.get("surface_id", "")) == MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG:
			return true
	return false


func _chatlog_is_gesture_resizing() -> bool:
	return _gesture_resize_is_active() and str(_gesture_resize_state.get("surface_id", "")) == MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG


func _chatlog_is_selected() -> bool:
	return _selected_surface_id == MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG


func _chatlog_is_crumpling() -> bool:
	return _crumple_is_active() and str(_crumple_state.get("surface_id", "")) == MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG


func _apply_chatlog_gesture_state() -> void:
	if is_instance_valid(_chatlog_panel):
		_chatlog_panel.set_gesture_visual_state(
			_gesture_hover_surface_id == MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG,
			_chatlog_is_selected(),
			_chatlog_is_gesture_grabbed(),
			_chatlog_is_gesture_resizing(),
			_chatlog_is_crumpling())


func _select_surface(surface_id: String) -> void:
	var target_window = _window_for_surface_id(surface_id)
	if not is_instance_valid(target_window):
		_clear_selected_surface()
		return
	_cancel_crumple(true)
	_release_all_gesture_grabs()
	_gesture_pending_pinches.clear()
	_selected_surface_id = target_window.window_type
	if is_instance_valid(_window_manager):
		_window_manager.focus_window(target_window)
	_apply_chatlog_gesture_state()


func _clear_selected_surface() -> void:
	if _selected_surface_id.is_empty():
		return
	_selected_surface_id = ""
	_apply_chatlog_gesture_state()


func _show_chatlog_panel() -> void:
	if not is_instance_valid(_chatlog_panel):
		_setup_chatlog_panel()
	if is_instance_valid(_window_manager):
		_window_manager.show_window(MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG)


func _hide_chatlog_panel() -> void:
	if is_instance_valid(_window_manager):
		_window_manager.hide_window(MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG)
	_release_all_gesture_grabs()
	_gesture_hover_surface_id = ""
	_gesture_hover_surface_by_pointer.clear()
	if _selected_surface_id == MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG:
		_clear_selected_surface()
	_apply_chatlog_gesture_state()


func _on_chatlog_header_gui_input(_event: InputEvent) -> void:
	# Mouse drag now lives inside MerlinWindow.
	pass


func _on_chatlog_resize_gui_input(_event: InputEvent) -> void:
	# Mouse resize now lives inside MerlinWindow.
	pass


func _clamp_chatlog_panel_to_viewport() -> void:
	if not is_instance_valid(_chatlog_panel):
		return
	_chatlog_panel.clamp_to_viewport()


func _append_chatlog_message(role: String, text: String, source: String = "", timestamp_utc: String = "") -> void:
	if is_instance_valid(_chatlog_content):
		_chatlog_content.append_message(role, text, source, timestamp_utc)
		return
	_setup_chatlog_panel()
	if is_instance_valid(_chatlog_content):
		_chatlog_content.append_message(role, text, source, timestamp_utc)


func _gesture_cursor_style(pinched: bool) -> StyleBoxFlat:
	var fill := Color(1.0, 0.68, 0.28, 0.34) if pinched else Color(0.24, 0.72, 1.0, 0.26)
	var border := COLOR_AMBER if pinched else COLOR_CYAN
	var style := _panel_style(fill, border, 2.0, int(GESTURE_CURSOR_SIZE * 0.5))
	style.shadow_color = Color(border.r, border.g, border.b, 0.72)
	style.shadow_size = 18
	style.shadow_offset = Vector2(0, 0)
	return style


func _create_application_choice_row(display_name: String, source: String) -> Button:
	var button := Button.new()
	button.focus_mode = Control.FOCUS_ALL
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.custom_minimum_size = Vector2(0, 48)
	button.text = "%s    %s" % [_application_icon_letters(display_name), display_name]
	button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	button.tooltip_text = "Select %s" % display_name
	button.add_theme_stylebox_override("normal", _choice_row_style(Color(0.004, 0.034, 0.088, 0.62), Color(COLOR_CYAN.r, COLOR_CYAN.g, COLOR_CYAN.b, 0.30)))
	button.add_theme_stylebox_override("hover", _choice_row_style(Color(0.010, 0.062, 0.130, 0.78), COLOR_CYAN))
	button.add_theme_stylebox_override("pressed", _choice_row_style(Color(0.020, 0.082, 0.160, 0.86), COLOR_AMBER))
	button.add_theme_color_override("font_color", COLOR_WHITE)
	button.add_theme_color_override("font_hover_color", COLOR_WHITE)
	button.add_theme_color_override("font_pressed_color", COLOR_WHITE)
	if not source.strip_edges().is_empty():
		button.tooltip_text = "%s - %s" % [display_name, source]
	button.pressed.connect(func(): _send_application_choice(display_name))
	return button


func _floating_choice_panel_style() -> StyleBoxFlat:
	var style := _panel_style(Color(0.002, 0.015, 0.046, 0.88), Color(COLOR_AMBER.r, COLOR_AMBER.g, COLOR_AMBER.b, 0.82), 1.0, 8)
	style.shadow_color = Color(COLOR_AMBER.r, COLOR_AMBER.g, COLOR_AMBER.b, 0.30)
	style.shadow_size = 18
	style.shadow_offset = Vector2(0, 0)
	return style


func _choice_row_style(fill: Color, border: Color) -> StyleBoxFlat:
	var style := _panel_style(fill, border, 1.0, 6)
	style.content_margin_left = 12
	style.content_margin_top = 8
	style.content_margin_right = 12
	style.content_margin_bottom = 8
	return style


func _application_icon_letters(display_name: String) -> String:
	var words := display_name.strip_edges().split(" ", false)
	var letters := ""
	for word in words:
		if letters.length() >= 2:
			break
		if not word.is_empty():
			letters += word.substr(0, 1).to_upper()
	return letters if not letters.is_empty() else "APP"


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
	if _legacy_state_changes_enabled():
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
	if _legacy_state_changes_enabled() and has_pending_requests and _merlin_state != MerlinState.LISTENING and _merlin_state != MerlinState.SPEAKING:
		_set_merlin_state(MerlinState.THINKING)
		return
	if not has_pending_requests and web_socket_client.is_backend_connected():
		_focus_message_input()


func _update_orb_from_response(response: Dictionary, success: bool, response_type: String) -> void:
	if not _legacy_state_changes_enabled():
		return

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


func _start_visual_overlay(kind: String, hold_seconds: float = 0.0, hold_until_speech_end: bool = false, waiting_for_confirmation: bool = false) -> void:
	_visual_overlay_kind = kind
	_visual_overlay_target = 1.0
	_visual_overlay_strength = maxf(_visual_overlay_strength, 0.18)
	_visual_overlay_hold_until_usec = Time.get_ticks_usec() + int(maxf(hold_seconds, 0.0) * 1000000.0)
	_visual_overlay_hold_until_speech_end = hold_until_speech_end
	_visual_overlay_waiting_for_confirmation = waiting_for_confirmation
	_apply_visual_overlay()


func _release_visual_overlay(kind: String = "") -> void:
	if not kind.is_empty() and _visual_overlay_kind != kind:
		return
	_visual_overlay_target = 0.0
	_visual_overlay_hold_until_usec = 0
	_visual_overlay_hold_until_speech_end = false
	_visual_overlay_waiting_for_confirmation = false


func _clear_transient_visual_overlay() -> void:
	_visual_overlay_kind = ""
	_visual_overlay_strength = 0.0
	_visual_overlay_target = 0.0
	_visual_overlay_hold_until_usec = 0
	_visual_overlay_hold_until_speech_end = false
	_visual_overlay_waiting_for_confirmation = false
	_apply_visual_overlay()


func _legacy_state_changes_enabled() -> bool:
	return not _canonical_ui_state_active


func _update_visual_overlay(delta: float) -> void:
	if _visual_overlay_kind.is_empty():
		return

	if _visual_overlay_target > 0.0 and not _visual_overlay_hold_until_speech_end and not _visual_overlay_waiting_for_confirmation:
		if _visual_overlay_hold_until_usec > 0 and Time.get_ticks_usec() >= _visual_overlay_hold_until_usec:
			_visual_overlay_target = 0.0

	var speed := 5.8 if _visual_overlay_target > _visual_overlay_strength else 1.0 / VISUAL_OVERLAY_FADE_SECONDS
	_visual_overlay_strength = move_toward(_visual_overlay_strength, _visual_overlay_target, delta * speed)
	_apply_visual_overlay()

	if _visual_overlay_target <= 0.0 and _visual_overlay_strength <= 0.001:
		_visual_overlay_kind = ""
		_visual_overlay_strength = 0.0
		_apply_visual_overlay()


func _apply_visual_overlay() -> void:
	if core_orb == null or not core_orb.has_method("set_overlay_intensity"):
		return
	core_orb.set_overlay_intensity("error", _visual_overlay_strength if _visual_overlay_kind == "error" else 0.0)
	core_orb.set_overlay_intensity("confirmation", _visual_overlay_strength if _visual_overlay_kind == "confirmation" else 0.0)


func _is_tool_execution_response(response: Dictionary) -> bool:
	var tool_name := str(response.get("toolName", ""))
	var intent := str(response.get("intent", ""))
	if tool_name.is_empty():
		return false

	return tool_name != "General Conversation" or intent == "system_resource_query"


func _set_merlin_state(state: int) -> void:
	var started_usec := Time.get_ticks_usec()
	_merlin_state = state
	activity_label.text = _activity_text_for_state(state)
	match state:
		MerlinState.SLEEPING:
			if core_orb.has_method("set_sleeping"):
				core_orb.set_sleeping()
			else:
				core_orb.set_idle()
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
	var elapsed_ms := _elapsed_ms_since(started_usec)
	if elapsed_ms >= 2.0:
		print("SignalHandlerPerf slow handler=_set_merlin_state state=%s ms=%.2f" % [_merlin_state_name(state), elapsed_ms])


func _activity_text_for_state(state: int) -> String:
	match state:
		MerlinState.SLEEPING:
			return "Merlin is sleeping"
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
	voice_button.disabled = not connected
	if connected:
		_reset_voice_button()
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
