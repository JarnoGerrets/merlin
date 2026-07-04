extends PanelContainer
class_name MerlinWindow

signal close_requested(window)
signal focus_requested(window)

const COLOR_CYAN := Color(0.24, 0.72, 1.0, 1.0)
const COLOR_WHITE := Color(0.88, 0.96, 1.0, 1.0)
const COLOR_MUTED := Color(0.50, 0.62, 0.70, 1.0)
const COLOR_AMBER := Color(1.0, 0.68, 0.28, 1.0)

var window_type := ""
var definition
var content_host: Control

var _layout: VBoxContainer
var _header: HBoxContainer
var _title_label: Label
var _close_button: Button
var _resize_handle: Control
var _dragging := false
var _resizing := false
var _drag_offset := Vector2.ZERO
var _resize_start_mouse := Vector2.ZERO
var _resize_start_size := Vector2.ZERO
var _gesture_hovered := false
var _gesture_selected := false
var _gesture_grabbed := false
var _gesture_resizing := false
var _gesture_crumpling := false


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_STOP


func configure(window_definition) -> void:
	definition = window_definition
	window_type = definition.window_type
	name = "%sWindow" % window_type.capitalize()
	visible = false
	mouse_filter = Control.MOUSE_FILTER_STOP
	custom_minimum_size = definition.min_size
	position = definition.default_position
	size = definition.default_size
	add_theme_stylebox_override("panel", _window_style())
	_build_shell()


func _build_shell() -> void:
	for child in get_children():
		remove_child(child)
		child.queue_free()

	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 0)
	margin.add_theme_constant_override("margin_top", 0)
	margin.add_theme_constant_override("margin_right", 0)
	margin.add_theme_constant_override("margin_bottom", 0)
	add_child(margin)

	_layout = VBoxContainer.new()
	_layout.add_theme_constant_override("separation", 0)
	margin.add_child(_layout)

	_header = HBoxContainer.new()
	_header.name = "DragHeader"
	_header.custom_minimum_size = Vector2(0, 38)
	_header.mouse_filter = Control.MOUSE_FILTER_STOP
	_header.add_theme_constant_override("separation", 8)
	_header.gui_input.connect(_on_header_gui_input)
	_layout.add_child(_header)

	var title_margin := MarginContainer.new()
	title_margin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title_margin.add_theme_constant_override("margin_left", 12)
	title_margin.add_theme_constant_override("margin_top", 7)
	title_margin.add_theme_constant_override("margin_bottom", 7)
	_header.add_child(title_margin)

	_title_label = Label.new()
	_title_label.text = definition.title
	_title_label.add_theme_color_override("font_color", COLOR_WHITE)
	_title_label.add_theme_font_size_override("font_size", 15)
	title_margin.add_child(_title_label)

	_close_button = Button.new()
	_close_button.text = "X"
	_close_button.tooltip_text = "Close %s" % definition.title
	_close_button.custom_minimum_size = Vector2(34, 30)
	_close_button.focus_mode = Control.FOCUS_NONE
	_close_button.pressed.connect(func(): dismiss())
	_style_button(_close_button)
	_header.add_child(_close_button)

	content_host = MarginContainer.new()
	content_host.name = "ContentHost"
	content_host.size_flags_vertical = Control.SIZE_EXPAND_FILL
	content_host.add_theme_constant_override("margin_left", 10)
	content_host.add_theme_constant_override("margin_top", 8)
	content_host.add_theme_constant_override("margin_right", 10)
	content_host.add_theme_constant_override("margin_bottom", 8)
	_layout.add_child(content_host)

	_resize_handle = Control.new()
	_resize_handle.name = "ResizeHandle"
	_resize_handle.custom_minimum_size = Vector2(22, 18)
	_resize_handle.mouse_filter = Control.MOUSE_FILTER_STOP
	_resize_handle.gui_input.connect(_on_resize_gui_input)
	_layout.add_child(_resize_handle)

	var resize_label := Label.new()
	resize_label.text = "///"
	resize_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	resize_label.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	resize_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	resize_label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	resize_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	resize_label.add_theme_color_override("font_color", COLOR_MUTED)
	resize_label.add_theme_font_size_override("font_size", 11)
	_resize_handle.add_child(resize_label)
	resize_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)


func set_content(node: Control) -> void:
	if not is_instance_valid(content_host):
		return
	for child in content_host.get_children():
		content_host.remove_child(child)
		child.queue_free()
	content_host.add_child(node)


func show_window() -> void:
	visible = true
	clamp_to_viewport()
	grab_focus()


func hide_window() -> void:
	visible = false
	_dragging = false
	_resizing = false


func dismiss() -> void:
	if definition != null and definition.capabilities.can_dismiss:
		close_requested.emit(self)


func focus_window() -> void:
	grab_focus()


func get_window_rect() -> Rect2:
	return Rect2(position, size)


func set_window_rect(rect: Rect2) -> void:
	size = _clamp_size(rect.size)
	position = rect.position
	clamp_to_viewport()


func move_to(target_position: Vector2) -> void:
	position = target_position
	clamp_to_viewport()


func move_by(delta: Vector2) -> void:
	move_to(position + delta)


func resize_to(target_size: Vector2) -> void:
	size = _clamp_size(target_size)
	clamp_to_viewport()


func set_gesture_visual_state(hovered: bool, selected: bool, grabbed: bool, resizing: bool, crumpling: bool = false) -> void:
	_gesture_hovered = hovered
	_gesture_selected = selected
	_gesture_grabbed = grabbed
	_gesture_resizing = resizing
	_gesture_crumpling = crumpling
	add_theme_stylebox_override("panel", _window_style())


func contains_screen_point(screen_position: Vector2) -> bool:
	return Rect2(global_position, size).has_point(screen_position)


func has_capability(capability: String) -> bool:
	return definition != null and definition.capabilities.has_capability(capability)


func clamp_to_viewport() -> void:
	var viewport := get_viewport()
	if viewport == null:
		return
	var viewport_size := viewport.get_visible_rect().size
	var max_position := Vector2(
		maxf(0.0, viewport_size.x - 80.0),
		maxf(0.0, viewport_size.y - 60.0)
	)
	position = Vector2(
		clampf(position.x, 0.0, max_position.x),
		clampf(position.y, 0.0, max_position.y)
	)


func _on_header_gui_input(event: InputEvent) -> void:
	if definition != null and not definition.capabilities.can_move:
		return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			focus_requested.emit(self)
			_dragging = true
			_drag_offset = get_global_mouse_position() - position
			accept_event()
		else:
			_dragging = false
			accept_event()
	elif event is InputEventMouseMotion and _dragging:
		move_to(get_global_mouse_position() - _drag_offset)
		accept_event()


func _on_resize_gui_input(event: InputEvent) -> void:
	if definition != null and not definition.capabilities.can_resize:
		return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			focus_requested.emit(self)
			_resizing = true
			_resize_start_mouse = get_global_mouse_position()
			_resize_start_size = size
			accept_event()
		else:
			_resizing = false
			accept_event()
	elif event is InputEventMouseMotion and _resizing:
		var delta := get_global_mouse_position() - _resize_start_mouse
		resize_to(_resize_start_size + delta)
		accept_event()


func _clamp_size(target_size: Vector2) -> Vector2:
	if definition == null:
		return target_size
	return Vector2(
		clampf(target_size.x, definition.min_size.x, definition.max_size.x),
		clampf(target_size.y, definition.min_size.y, definition.max_size.y)
	)


func _window_style() -> StyleBoxFlat:
	var active_motion: bool = _gesture_grabbed or _gesture_resizing or _gesture_crumpling
	var border: Color = COLOR_AMBER if active_motion else (COLOR_WHITE if _gesture_selected else Color(COLOR_CYAN.r, COLOR_CYAN.g, COLOR_CYAN.b, 0.92 if _gesture_hovered else 0.72))
	var shadow: Color = COLOR_AMBER if active_motion else (COLOR_WHITE if _gesture_selected else COLOR_CYAN)
	var border_width: float = 2.5 if _gesture_crumpling else (2.0 if _gesture_resizing or _gesture_selected else (1.5 if _gesture_hovered else 1.0))
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.006, 0.032, 0.082, 0.96) if _gesture_selected and not active_motion else Color(0.002, 0.015, 0.046, 0.92)
	style.border_color = border
	style.set_border_width_all(int(border_width))
	style.set_corner_radius_all(8)
	style.shadow_color = Color(shadow.r, shadow.g, shadow.b, 0.58 if _gesture_crumpling else (0.52 if _gesture_resizing else (0.50 if _gesture_selected else (0.38 if _gesture_hovered or _gesture_grabbed else 0.22))))
	style.shadow_size = 32 if _gesture_crumpling else (30 if _gesture_selected else (28 if _gesture_resizing else (22 if _gesture_hovered or _gesture_grabbed else 16)))
	style.shadow_offset = Vector2(0, 0)
	return style


func _style_button(button: Button) -> void:
	var normal := StyleBoxFlat.new()
	normal.bg_color = Color(0.006, 0.038, 0.095, 0.84)
	normal.border_color = Color(COLOR_CYAN.r, COLOR_CYAN.g, COLOR_CYAN.b, 0.46)
	normal.set_border_width_all(1)
	normal.set_corner_radius_all(6)
	button.add_theme_stylebox_override("normal", normal)
	button.add_theme_stylebox_override("hover", normal.duplicate())
	button.add_theme_stylebox_override("pressed", normal.duplicate())
	button.add_theme_color_override("font_color", COLOR_WHITE)
