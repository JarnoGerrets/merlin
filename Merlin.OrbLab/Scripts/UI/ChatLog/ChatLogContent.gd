extends VBoxContainer
class_name ChatLogContent

const COLOR_WHITE := Color(0.88, 0.96, 1.0, 1.0)
const COLOR_CYAN := Color(0.24, 0.72, 1.0, 1.0)
const COLOR_MUTED := Color(0.50, 0.62, 0.70, 1.0)
const CHATLOG_MAX_MESSAGES := 500
const CHATLOG_BOTTOM_SCROLL_THRESHOLD := 36

var messages: Array[Dictionary] = []
var message_scroll: ScrollContainer
var message_list: VBoxContainer


func _ready() -> void:
	if message_scroll == null:
		_build_content()


func _build_content() -> void:
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_theme_constant_override("separation", 8)

	message_scroll = ScrollContainer.new()
	message_scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	message_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	message_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(message_scroll)

	message_list = VBoxContainer.new()
	message_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	message_list.add_theme_constant_override("separation", 8)
	message_scroll.add_child(message_list)


func append_message(role: String, text: String, source: String = "", timestamp_utc: String = "") -> void:
	if message_scroll == null:
		_build_content()
	var clean_text := text.strip_edges()
	if clean_text.is_empty():
		return

	var entry := {
		"role": role,
		"text": clean_text,
		"source": source,
		"timestampUtc": timestamp_utc,
	}
	messages.append(entry)
	while messages.size() > CHATLOG_MAX_MESSAGES:
		messages.pop_front()
		if is_instance_valid(message_list) and message_list.get_child_count() > 0:
			var oldest := message_list.get_child(0)
			message_list.remove_child(oldest)
			oldest.queue_free()

	var should_scroll := should_auto_scroll()
	message_list.add_child(_create_entry(entry))
	if should_scroll:
		call_deferred("scroll_to_bottom")


func clear_messages() -> void:
	messages.clear()
	if not is_instance_valid(message_list):
		return
	for child in message_list.get_children():
		message_list.remove_child(child)
		child.queue_free()


func should_auto_scroll() -> bool:
	if not is_instance_valid(message_scroll):
		return false
	var bar := message_scroll.get_v_scroll_bar()
	return bar.max_value - message_scroll.scroll_vertical <= CHATLOG_BOTTOM_SCROLL_THRESHOLD


func scroll_to_bottom() -> void:
	if is_instance_valid(message_scroll):
		message_scroll.scroll_vertical = int(message_scroll.get_v_scroll_bar().max_value)


func _create_entry(entry: Dictionary) -> Control:
	var role := str(entry.get("role", "assistant")).to_lower()
	var text := str(entry.get("text", ""))
	var author := "User" if role == "user" else "Merlin"
	var color := COLOR_WHITE if role == "user" else COLOR_CYAN
	var timestamp := _format_timestamp(str(entry.get("timestampUtc", "")))

	var container := VBoxContainer.new()
	container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	container.focus_mode = Control.FOCUS_NONE
	container.add_theme_constant_override("separation", 4)

	var heading := Label.new()
	heading.text = "[%s] %s" % [timestamp, author] if not timestamp.is_empty() else author
	heading.add_theme_color_override("font_color", color)
	heading.add_theme_font_size_override("font_size", 12)
	container.add_child(heading)

	var body := _create_selectable_text(text, COLOR_WHITE, 13)
	container.add_child(body)
	return container


func _format_timestamp(timestamp_utc: String) -> String:
	if timestamp_utc.is_empty():
		var now := Time.get_datetime_dict_from_system()
		return "%02d:%02d" % [int(now.get("hour", 0)), int(now.get("minute", 0))]
	var parsed := Time.get_datetime_dict_from_datetime_string(timestamp_utc, false)
	if parsed.is_empty():
		return ""
	return "%02d:%02d" % [int(parsed.get("hour", 0)), int(parsed.get("minute", 0))]


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
