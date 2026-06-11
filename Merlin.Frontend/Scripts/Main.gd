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

const COLOR_BACKGROUND := Color(0.000, 0.006, 0.012, 1.0)
const COLOR_PANEL := Color(0.004, 0.026, 0.042, 0.40)
const COLOR_PANEL_DARK := Color(0.002, 0.014, 0.026, 0.64)
const COLOR_BLUE := Color(0.18, 0.55, 0.86, 1.0)
const COLOR_CYAN := Color(0.42, 0.88, 1.0, 1.0)
const COLOR_WHITE := Color(0.88, 0.96, 1.0, 1.0)
const COLOR_MUTED := Color(0.50, 0.62, 0.70, 1.0)
const COLOR_AMBER := Color(1.0, 0.68, 0.28, 1.0)
const COLOR_RED := Color(1.0, 0.28, 0.34, 1.0)

@onready var web_socket_client: MerlinWebSocketClient = $MerlinWebSocketClient
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

var _pending_requests := {}
var _merlin_state: int = MerlinState.IDLE
var _focus_request_id := 0


func _ready() -> void:
	_apply_visual_theme()
	message_input.focus_mode = Control.FOCUS_ALL
	message_input.keep_editing_on_text_submit = true
	message_scroll.focus_mode = Control.FOCUS_NONE
	message_list.focus_mode = Control.FOCUS_NONE
	thinking_label.focus_mode = Control.FOCUS_NONE
	error_label.focus_mode = Control.FOCUS_NONE
	send_button.pressed.connect(_on_send_pressed)
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

	_focus_message_input()


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
	activity_panel.add_theme_stylebox_override("panel", _panel_style(Color(0.003, 0.030, 0.050, 0.30), COLOR_CYAN, 1.0, 8))
	notification_panel.add_theme_stylebox_override("panel", _panel_style(Color(0.001, 0.008, 0.016, 0.18), Color(0, 0, 0, 0), 0.0, 8))
	chat_panel.add_theme_stylebox_override("panel", _panel_style(COLOR_PANEL_DARK, Color(COLOR_BLUE.r, COLOR_BLUE.g, COLOR_BLUE.b, 0.45), 1.0, 8))
	history_panel.add_theme_stylebox_override("panel", _panel_style(Color(0.01, 0.025, 0.04, 0.36), Color(COLOR_CYAN.r, COLOR_CYAN.g, COLOR_CYAN.b, 0.28), 1.0, 6))
	command_input_panel.add_theme_stylebox_override("panel", _panel_style(Color(0.02, 0.08, 0.12, 0.64), COLOR_CYAN, 1.0, 10))

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
	button.add_theme_stylebox_override("normal", _panel_style(Color(0.004, 0.045, 0.055, 0.44), Color(COLOR_BLUE.r, COLOR_BLUE.g, COLOR_BLUE.b, 0.50), 1.0, 6))
	button.add_theme_stylebox_override("hover", _panel_style(Color(0.006, 0.065, 0.080, 0.64), COLOR_CYAN, 1.0, 6))
	button.add_theme_stylebox_override("pressed", _panel_style(Color(0.006, 0.085, 0.095, 0.72), COLOR_CYAN, 1.0, 6))
	button.add_theme_color_override("font_color", COLOR_WHITE)
	button.add_theme_color_override("font_hover_color", COLOR_WHITE)
	button.add_theme_color_override("font_pressed_color", COLOR_WHITE)


func _style_line_edit(line_edit: LineEdit) -> void:
	line_edit.add_theme_stylebox_override("normal", _panel_style(Color(0.0, 0.018, 0.030, 0.32), Color(COLOR_CYAN.r, COLOR_CYAN.g, COLOR_CYAN.b, 0.50), 1.0, 6))
	line_edit.add_theme_stylebox_override("focus", _panel_style(Color(0.0, 0.035, 0.055, 0.56), COLOR_CYAN, 1.0, 6))
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
		await _add_typed_chat_line("Merlin", _format_success_response(message, available_tools, diagnostics, confirmation), debug_text, _response_kind(response, success, response_type))
		_clear_error()
	else:
		var formatted_error := _format_error_response(error_code, message)
		if typeof(confirmation) == TYPE_DICTIONARY:
			await _add_typed_chat_line("Merlin", _format_confirmation(message, confirmation, application_candidates), debug_text, "confirmation")
			_add_notification("Confirmation required", "confirmation")
			_clear_error()
			activity_label.text = "Waiting for confirmation"
			core_orb.play_confirmation()
			_focus_message_input()
			return
		elif response_type == "limitation" or response_type == "safety":
			var kind := _response_kind(response, success, response_type)
			await _add_typed_chat_line("Merlin", message, debug_text, kind)
			_add_notification("Capability unavailable" if kind == "limitation" else "Safety boundary", kind)
			_clear_error()
		elif response_type == "system":
			_add_system_message(message)
			_add_notification(message, "system")
			_clear_error()
		else:
			await _add_typed_chat_line("Error", formatted_error, debug_text, "error")
			_add_notification("Error", "error")
			_clear_error()

	_settle_orb_after_response()
	_focus_message_input()


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
	notification.add_theme_stylebox_override("panel", _panel_style(Color(0.02, 0.06, 0.09, 0.62), _message_color(kind), 1.0, 6))

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
			return "Merlin is focusing"
		MerlinState.SPEAKING:
			return "Merlin is responding"
		MerlinState.EXECUTING_TOOL:
			return "Executing verified tool action"
		MerlinState.ERROR:
			return "Attention required"
		_:
			return "Merlin is standing by"


func _update_send_button() -> void:
	var connected := web_socket_client.is_backend_connected()
	send_button.disabled = not connected
	reconnect_button.disabled = web_socket_client.get_connection_state() == "connecting"


func _focus_message_input() -> void:
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
