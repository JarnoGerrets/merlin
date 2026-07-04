extends Control

const COLOR_CYAN := Color(0.24, 0.72, 1.0, 1.0)
const COLOR_WHITE := Color(0.88, 0.96, 1.0, 1.0)
const COLOR_MUTED := Color(0.50, 0.62, 0.70, 1.0)

const SURFACE_PADDING := 36.0
const LIVE_WINDOW_SIZE := Vector2(453.6, 756.0)
const HEADER_HEIGHT := 38.0
const RESIZE_HANDLE_HEIGHT := 18.0
const CONTENT_MARGIN := Vector2(10.0, 8.0)
const CHAT_MESSAGES := [
	{
		"role": "user",
		"time": "10:14",
		"text": "Can you keep the notes visible while I compare the next few options?"
	},
	{
		"role": "assistant",
		"time": "10:15",
		"text": "Sure. I will keep the chat pinned here and preserve the recent context while you work."
	},
	{
		"role": "user",
		"time": "10:16",
		"text": "Perfect. After this, fold the panel away like a sheet of paper."
	},
	{
		"role": "assistant",
		"time": "10:17",
		"text": "I can keep the whole visible surface intact and use that exact rendered state as the source for the crumple test."
	},
	{
		"role": "user",
		"time": "10:18",
		"text": "Good. I want the text, borders, header, glow, and resize handle to all remain recognizable as it starts folding."
	},
	{
		"role": "assistant",
		"time": "10:19",
		"text": "Then the animation should preserve the chat window as one connected sheet first, and only add stronger depth once the ball is nearly formed."
	},
	{
		"role": "user",
		"time": "10:20",
		"text": "Exactly. The first half should feel like a real window bending inward, not like separate tiles changing color."
	},
	{
		"role": "assistant",
		"time": "10:21",
		"text": "That gives us a useful preview: if the text stays continuous while folding, the live delete gesture will read much better."
	},
	{
		"role": "user",
		"time": "10:22",
		"text": "And once it is a ball, it should stop moving internally until the throw sends the whole thing away."
	},
	{
		"role": "assistant",
		"time": "10:23",
		"text": "Right. The final state should behave like a crumpled paper object: compact, stable, and ready to be thrown offscreen."
	}
]


func _draw() -> void:
	_draw_live_chatlog_window(Rect2(Vector2(SURFACE_PADDING, SURFACE_PADDING), LIVE_WINDOW_SIZE))


func _draw_live_chatlog_window(rect: Rect2) -> void:
	draw_style_box(_window_style(), rect)

	var font: Font = get_theme_default_font()
	var header_rect: Rect2 = Rect2(rect.position, Vector2(rect.size.x, HEADER_HEIGHT))
	_draw_header(header_rect, font)

	var content_rect: Rect2 = Rect2(
		rect.position + Vector2(CONTENT_MARGIN.x, HEADER_HEIGHT + CONTENT_MARGIN.y),
		Vector2(rect.size.x - CONTENT_MARGIN.x * 2.0, rect.size.y - HEADER_HEIGHT - RESIZE_HANDLE_HEIGHT - CONTENT_MARGIN.y * 2.0)
	)
	_draw_chatlog_content(content_rect, font)
	_draw_resize_handle(Rect2(rect.position + Vector2(0.0, rect.size.y - RESIZE_HANDLE_HEIGHT), Vector2(rect.size.x, RESIZE_HANDLE_HEIGHT)), font)


func _draw_header(rect: Rect2, font: Font) -> void:
	draw_string(font, rect.position + Vector2(12.0, 24.0), "Chat", HORIZONTAL_ALIGNMENT_LEFT, -1.0, 15, COLOR_WHITE)
	var close_rect: Rect2 = Rect2(rect.position + Vector2(rect.size.x - 34.0, 4.0), Vector2(34.0, 30.0))
	draw_style_box(_button_style(), close_rect)
	draw_string(font, close_rect.position + Vector2(12.0, 20.0), "X", HORIZONTAL_ALIGNMENT_LEFT, -1.0, 12, COLOR_WHITE)


func _draw_chatlog_content(rect: Rect2, font: Font) -> void:
	var cursor_y: float = rect.position.y
	for message in CHAT_MESSAGES:
		var message_dict: Dictionary = message
		var role: String = str(message_dict["role"])
		var author: String = "User" if role == "user" else "Merlin"
		var color: Color = COLOR_WHITE if role == "user" else COLOR_CYAN
		var heading: String = "[%s] %s" % [str(message_dict["time"]), author]
		draw_string(font, Vector2(rect.position.x, cursor_y + 13.0), heading, HORIZONTAL_ALIGNMENT_LEFT, rect.size.x, 12, color)
		cursor_y += 26.0
		var lines: Array[String] = _wrap_text(str(message_dict["text"]), 48)
		for line in lines:
			draw_string(font, Vector2(rect.position.x, cursor_y), line, HORIZONTAL_ALIGNMENT_LEFT, rect.size.x, 13, COLOR_WHITE)
			cursor_y += 18.0
		cursor_y += 14.0


func _draw_resize_handle(rect: Rect2, font: Font) -> void:
	draw_string(font, rect.position + Vector2(rect.size.x - 31.0, 13.0), "///", HORIZONTAL_ALIGNMENT_RIGHT, 24.0, 11, COLOR_MUTED)


func _window_style() -> StyleBoxFlat:
	var style: StyleBoxFlat = StyleBoxFlat.new()
	style.bg_color = Color(0.006, 0.032, 0.082, 0.96)
	style.border_color = COLOR_WHITE
	style.set_border_width_all(2)
	style.set_corner_radius_all(8)
	style.shadow_color = Color(COLOR_WHITE.r, COLOR_WHITE.g, COLOR_WHITE.b, 0.50)
	style.shadow_size = 30
	style.shadow_offset = Vector2.ZERO
	return style


func _button_style() -> StyleBoxFlat:
	var style: StyleBoxFlat = StyleBoxFlat.new()
	style.bg_color = Color(0.006, 0.038, 0.095, 0.84)
	style.border_color = Color(COLOR_CYAN.r, COLOR_CYAN.g, COLOR_CYAN.b, 0.46)
	style.set_border_width_all(1)
	style.set_corner_radius_all(6)
	return style


func _wrap_text(text: String, max_chars: int) -> Array[String]:
	var words: PackedStringArray = text.split(" ", false)
	var lines: Array[String] = []
	var current: String = ""
	for word in words:
		var candidate: String = word if current.is_empty() else "%s %s" % [current, word]
		if candidate.length() > max_chars and not current.is_empty():
			lines.append(current)
			current = word
		else:
			current = candidate
	if not current.is_empty():
		lines.append(current)
	return lines
