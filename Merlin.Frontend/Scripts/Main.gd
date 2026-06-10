extends Control

@onready var web_socket_client: MerlinWebSocketClient = $MerlinWebSocketClient
@onready var connection_state_label: Label = $RootMargin/Layout/Header/ConnectionStateLabel
@onready var reconnect_button: Button = $RootMargin/Layout/Header/ReconnectButton
@onready var show_debug_check_box: CheckBox = $RootMargin/Layout/Header/ShowDebugCheckBox
@onready var error_label: Label = $RootMargin/Layout/ErrorLabel
@onready var message_scroll: ScrollContainer = $RootMargin/Layout/Content/ChatColumn/HistoryPanel/HistoryMargin/MessageScroll
@onready var message_list: VBoxContainer = $RootMargin/Layout/Content/ChatColumn/HistoryPanel/HistoryMargin/MessageScroll/MessageList
@onready var thinking_label: Label = $RootMargin/Layout/Content/ChatColumn/ThinkingLabel
@onready var refresh_tools_button: Button = $RootMargin/Layout/Content/ToolsPanel/ToolsMargin/ToolsLayout/ToolsHeader/RefreshToolsButton
@onready var tools_list: VBoxContainer = $RootMargin/Layout/Content/ToolsPanel/ToolsMargin/ToolsLayout/ToolsScroll/ToolsList
@onready var message_input: LineEdit = $RootMargin/Layout/InputRow/MessageInput
@onready var send_button: Button = $RootMargin/Layout/InputRow/SendButton

var _pending_requests := {}


func _ready() -> void:
	send_button.pressed.connect(_on_send_pressed)
	reconnect_button.pressed.connect(_on_reconnect_pressed)
	refresh_tools_button.pressed.connect(_on_refresh_tools_pressed)
	show_debug_check_box.toggled.connect(_on_show_debug_check_box_toggled)
	message_input.text_submitted.connect(_on_message_submitted)

	web_socket_client.connection_state_changed.connect(_on_connection_state_changed)
	web_socket_client.response_received.connect(_on_response_received)
	web_socket_client.malformed_response.connect(_on_malformed_response)
	web_socket_client.socket_closed.connect(_on_socket_closed)

	_add_system_message("Connecting to Merlin.Backend...")
	_update_pending_state()
	_set_tools_placeholder("Click Refresh Tools after connecting.")
	web_socket_client.connect_to_backend()


func _on_reconnect_pressed() -> void:
	_clear_error()
	_pending_requests.clear()
	_update_pending_state()
	_add_system_message("Reconnecting...")
	web_socket_client.connect_to_backend()
	_update_send_button()


func _on_send_pressed() -> void:
	_send_current_message()


func _on_message_submitted(_text: String) -> void:
	_send_current_message()


func _on_refresh_tools_pressed() -> void:
	_send_backend_message("list tools", false)


func _send_current_message() -> void:
	var message := message_input.text.strip_edges()
	if message.is_empty():
		return

	_send_backend_message(message, true)


func _send_backend_message(message: String, show_user_message: bool) -> void:
	if not web_socket_client.is_backend_connected():
		_show_error("Cannot send: Merlin.Backend is not connected.")
		_update_send_button()
		return

	var correlation_id := _generate_correlation_id()
	_pending_requests[correlation_id] = message
	if show_user_message:
		_add_user_message(message)
		message_input.clear()

	_update_pending_state()
	_clear_error()

	var sent := web_socket_client.send_message(message, correlation_id)
	if not sent:
		_pending_requests.erase(correlation_id)
		_add_system_message("Message was not sent.")
		_update_pending_state()


func _on_connection_state_changed(state: String, detail: String) -> void:
	connection_state_label.text = _format_connection_state(state, detail)

	match state:
		"connected":
			_clear_error()
			_add_system_message("Connected to Merlin.Backend.")
		"connecting":
			_clear_error()
		"error":
			_show_error(detail)
			_add_system_message("Connection error: %s" % detail)
			_pending_requests.clear()
			_update_pending_state()
		"disconnected":
			if not detail.is_empty():
				_add_system_message(detail)
			_pending_requests.clear()
			_update_pending_state()

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

	if success:
		_add_assistant_message(_format_success_response(message, available_tools, diagnostics, confirmation), debug_text)
		_clear_error()
		if typeof(available_tools) == TYPE_ARRAY:
			_render_tools(available_tools)
	else:
		var formatted_error := _format_error_response(error_code, message)
		if typeof(confirmation) == TYPE_DICTIONARY:
			_add_assistant_message(_format_confirmation(message, confirmation, application_candidates), debug_text)
		elif response_type == "limitation" or response_type == "safety":
			_add_assistant_message(message, debug_text)
			_clear_error()
		elif response_type == "system":
			_add_system_message(message)
			_clear_error()
		else:
			_add_error_message(formatted_error, debug_text)
			_show_error(formatted_error)

	_update_pending_state()
	_update_send_button()


func _on_malformed_response(raw_message: String, detail: String) -> void:
	_pending_requests.clear()
	_update_pending_state()
	var message := "Malformed response JSON: %s" % detail
	_show_error(message)
	_add_system_message("%s Raw: %s" % [message, raw_message])


func _on_socket_closed(code: int, reason: String) -> void:
	_pending_requests.clear()
	_update_pending_state()
	if code != 1000:
		_show_error("WebSocket closed. Code: %s Reason: %s" % [code, reason])


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
	return "%s - %s" % [code, message]


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


func _add_chat_line(author: String, message: String, debug_text: String, kind: String) -> void:
	var container := VBoxContainer.new()
	container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	container.add_theme_constant_override("separation", 3)

	var label := _create_selectable_text("%s: %s" % [author, message], _message_color(kind))
	container.add_child(label)

	if not debug_text.is_empty():
		var debug_label := _create_selectable_text(debug_text, Color(0.55, 0.58, 0.62), 12)
		debug_label.visible = show_debug_check_box.button_pressed
		debug_label.set_meta("debug_label", true)
		container.add_child(debug_label)

	message_list.add_child(container)
	await get_tree().process_frame
	message_scroll.scroll_vertical = int(message_scroll.get_v_scroll_bar().max_value)


func _create_selectable_text(text: String, color: Color, font_size: int = 0) -> RichTextLabel:
	var label := RichTextLabel.new()
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


func _update_send_button() -> void:
	var connected := web_socket_client.is_backend_connected()
	send_button.disabled = not connected
	refresh_tools_button.disabled = not connected
	reconnect_button.disabled = web_socket_client.get_connection_state() == "connecting"


func _show_error(message: String) -> void:
	error_label.text = message
	error_label.visible = not message.is_empty()


func _clear_error() -> void:
	error_label.text = ""
	error_label.visible = false


func _render_tools(available_tools: Array) -> void:
	_clear_children(tools_list)

	for tool in available_tools:
		if typeof(tool) != TYPE_DICTIONARY:
			continue

		var tool_box := VBoxContainer.new()
		tool_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		tool_box.add_theme_constant_override("separation", 3)

		var name_label := Label.new()
		name_label.text = str(tool.get("name", "Unnamed Tool"))
		name_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		name_label.add_theme_font_size_override("font_size", 16)
		tool_box.add_child(name_label)

		var description_label := Label.new()
		description_label.text = str(tool.get("description", ""))
		description_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		tool_box.add_child(description_label)

		var examples = tool.get("examples", [])
		if typeof(examples) == TYPE_ARRAY and not examples.is_empty():
			var examples_label := Label.new()
			examples_label.text = "Examples: %s" % _join_values(examples, ", ")
			examples_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			examples_label.add_theme_font_size_override("font_size", 12)
			examples_label.add_theme_color_override("font_color", Color(0.55, 0.58, 0.62))
			tool_box.add_child(examples_label)

		tools_list.add_child(tool_box)


func _set_tools_placeholder(message: String) -> void:
	_clear_children(tools_list)
	var label := Label.new()
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.text = message
	tools_list.add_child(label)


func _clear_children(node: Node) -> void:
	for child in node.get_children():
		child.queue_free()


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
			return Color(0.82, 0.9, 1.0)
		"assistant":
			return Color(0.9, 0.95, 0.88)
		"error":
			return Color(1.0, 0.55, 0.55)
		_:
			return Color(0.72, 0.74, 0.78)


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
