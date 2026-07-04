extends Control
class_name HolographicTrashcan

signal consume_finished(window)

const Trashcan3DControllerScript := preload("res://Scripts/Trashcan/Trashcan3DController.gd")

const STATE_HIDDEN := "Hidden"
const STATE_IDLE := "Idle"
const STATE_HOVER_ARMED := "HoverArmed"
const STATE_CONSUMING_SURFACE := "ConsumingSurface"
const STATE_CLOSING := "Closing"
const CYAN := Color(0.22, 0.86, 1.0, 1.0)
const DROP_PADDING := 44.0
const VIEWPORT_SIZE := Vector2i(384, 384)

@export var debug_show_active_area: bool:
	get:
		return _debug_show_active_area
	set(value):
		_debug_show_active_area = value
		queue_redraw()

var state := STATE_IDLE
var visible_progress: float:
	get:
		return _visible_progress
	set(value):
		_visible_progress = clampf(value, 0.0, 1.0)
		modulate.a = _visible_progress
		scale = Vector2.ONE * lerpf(0.92, 1.0, _visible_progress)
		_update_debug_label()
var armed_strength: float:
	get:
		return _armed_strength
	set(value):
		_armed_strength = clampf(value, 0.0, 1.0)
		if _trashcan_3d != null:
			_trashcan_3d.armed_strength = _armed_strength
		_update_debug_label()
var lid_open_progress: float:
	get:
		return _lid_open_progress
	set(value):
		_lid_open_progress = clampf(value, 0.0, 1.0)
		if _trashcan_3d != null:
			_trashcan_3d.lid_open_progress = _lid_open_progress
var consume_flash: float:
	get:
		return _consume_flash
	set(value):
		_consume_flash = clampf(value, 0.0, 1.0)
		if _trashcan_3d != null:
			_trashcan_3d.consume_flash = _consume_flash

var _debug_show_active_area := false
var _visible_progress := 1.0
var _armed_strength := 0.0
var _lid_open_progress := 0.0
var _consume_flash := 0.0
var _active_drop_area: Control
var _viewport_container: SubViewportContainer
var _viewport: SubViewport
var _camera: Camera3D
var _trashcan_3d: Trashcan3DController
var _label: Label
var _debug_label: Label
var _fake_card: Control
var _active_tween: Tween


func _ready() -> void:
	custom_minimum_size = Vector2(360, 560)
	size = custom_minimum_size
	pivot_offset = size * 0.5
	mouse_filter = Control.MOUSE_FILTER_PASS
	_build_active_area()
	_build_viewport()
	_build_labels()
	set_process(true)
	_set_viewport_rendering(true)


func _process(delta: float) -> void:
	consume_flash = maxf(consume_flash - delta * 3.4, 0.0)
	pivot_offset = size * 0.5
	if state == STATE_HIDDEN:
		_set_viewport_rendering(false)


func show_drop_zone() -> void:
	_kill_tween()
	state = STATE_IDLE
	show()
	_set_viewport_rendering(true)
	visible_progress = 0.0
	_active_tween = create_tween()
	_active_tween.set_parallel(true)
	_active_tween.set_trans(Tween.TRANS_SINE)
	_active_tween.set_ease(Tween.EASE_OUT)
	_active_tween.tween_property(self, "visible_progress", 1.0, 0.22)


func show_zone() -> void:
	show_drop_zone()


func hide_drop_zone() -> void:
	_kill_tween()
	state = STATE_CLOSING
	_active_tween = create_tween()
	_active_tween.set_parallel(true)
	_active_tween.set_trans(Tween.TRANS_SINE)
	_active_tween.set_ease(Tween.EASE_IN_OUT)
	_active_tween.tween_property(self, "visible_progress", 0.0, 0.20)
	_active_tween.tween_property(self, "armed_strength", 0.0, 0.16)
	_active_tween.tween_property(self, "lid_open_progress", 0.0, 0.16)
	_active_tween.set_parallel(false)
	_active_tween.tween_callback(func() -> void:
		state = STATE_HIDDEN
		hide()
		_set_viewport_rendering(false)
	)


func hide_zone(immediate: bool = false) -> void:
	if immediate:
		_kill_tween()
		state = STATE_HIDDEN
		visible_progress = 0.0
		armed_strength = 0.0
		lid_open_progress = 0.0
		hide()
		_set_viewport_rendering(false)
		return
	hide_drop_zone()


func set_armed(is_armed: bool) -> void:
	if state == STATE_CONSUMING_SURFACE:
		return
	state = STATE_HOVER_ARMED if is_armed else STATE_IDLE
	_set_viewport_rendering(true)
	var target_strength: float = 1.0 if is_armed else 0.0
	_kill_tween()
	_active_tween = create_tween()
	_active_tween.set_parallel(true)
	_active_tween.set_trans(Tween.TRANS_SINE)
	_active_tween.set_ease(Tween.EASE_OUT)
	_active_tween.tween_property(self, "armed_strength", target_strength, 0.15)
	_active_tween.tween_property(self, "lid_open_progress", target_strength, 0.17)


func set_hover_armed(is_armed: bool) -> void:
	set_armed(is_armed)


func is_hover_armed() -> bool:
	return state == STATE_HOVER_ARMED


func is_active() -> bool:
	return visible and state != STATE_HIDDEN and state != STATE_CLOSING


func play_consume_preview() -> void:
	show_drop_zone()
	state = STATE_CONSUMING_SURFACE
	_set_viewport_rendering(true)
	_kill_tween()
	_active_tween = create_tween()
	_active_tween.set_parallel(true)
	_active_tween.tween_property(self, "armed_strength", 1.0, 0.12)
	_active_tween.tween_property(self, "lid_open_progress", 1.0, 0.12)

	if is_instance_valid(_fake_card):
		_fake_card.queue_free()
	_fake_card = _ConsumePreviewCard.new()
	_fake_card.name = "ConsumePreviewCard"
	_fake_card.size = Vector2(124, 78)
	_fake_card.position = Vector2(22, 240)
	_fake_card.modulate.a = 0.0
	add_child(_fake_card)

	var target := Vector2(size.x * 0.50 - _fake_card.size.x * 0.10, size.y * 0.44 - _fake_card.size.y * 0.10)
	var tween := create_tween()
	tween.set_trans(Tween.TRANS_CUBIC)
	tween.set_ease(Tween.EASE_IN_OUT)
	tween.tween_property(_fake_card, "modulate:a", 1.0, 0.09)
	tween.parallel().tween_property(_fake_card, "scale", Vector2.ONE, 0.09).from(Vector2(0.86, 0.86))
	tween.tween_property(_fake_card, "position", target, 0.34)
	tween.parallel().tween_property(_fake_card, "scale", Vector2(0.10, 0.10), 0.34)
	tween.parallel().tween_property(_fake_card, "modulate:a", 0.0, 0.28)
	tween.tween_callback(func() -> void:
		consume_flash = 1.0
		if is_instance_valid(_fake_card):
			_fake_card.queue_free()
	)
	tween.tween_interval(0.18)
	tween.tween_callback(func() -> void:
		state = STATE_IDLE
		set_armed(false)
	)


func consume_window(window: Control) -> void:
	if not is_instance_valid(window):
		return
	state = STATE_CONSUMING_SURFACE
	_set_viewport_rendering(true)
	armed_strength = 1.0
	lid_open_progress = 1.0
	var original_mouse_filter: int = window.mouse_filter
	var target: Vector2 = global_position + Vector2(size.x * 0.50, size.y * 0.44) - window.size * 0.12
	window.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var tween := create_tween()
	tween.set_parallel(true)
	tween.tween_property(window, "global_position", target, 0.34).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN)
	tween.tween_property(window, "scale", Vector2(0.12, 0.12), 0.34).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN)
	tween.tween_property(window, "modulate:a", 0.0, 0.28).set_delay(0.06)
	tween.set_parallel(false)
	tween.tween_callback(func() -> void:
		window.mouse_filter = original_mouse_filter
		consume_flash = 1.0
		consume_finished.emit(window)
		state = STATE_IDLE
		set_armed(false)
	)


func reset() -> void:
	_kill_tween()
	if is_instance_valid(_fake_card):
		_fake_card.queue_free()
	state = STATE_IDLE
	show()
	_set_viewport_rendering(true)
	visible_progress = 1.0
	armed_strength = 0.0
	lid_open_progress = 0.0
	consume_flash = 0.0


func get_active_drop_rect() -> Rect2:
	return Rect2(global_position, size).grow(DROP_PADDING)


func intersects_drop_rect(rect: Rect2) -> bool:
	return is_active() and get_active_drop_rect().intersects(rect, true)


func contains_drop_point(point: Vector2) -> bool:
	return is_active() and get_active_drop_rect().has_point(point)


func get_viewport_status() -> String:
	if _viewport == null:
		return "viewport: missing"
	return "viewport: %dx%d mode=%s" % [_viewport.size.x, _viewport.size.y, str(_viewport.render_target_update_mode)]


func _build_active_area() -> void:
	_active_drop_area = Control.new()
	_active_drop_area.name = "ActiveDropArea"
	_active_drop_area.mouse_filter = Control.MOUSE_FILTER_PASS
	_active_drop_area.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_active_drop_area)


func _build_viewport() -> void:
	_viewport_container = SubViewportContainer.new()
	_viewport_container.name = "Trashcan3DViewportContainer"
	_viewport_container.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_viewport_container.stretch = true
	_viewport_container.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_viewport_container)

	_viewport = SubViewport.new()
	_viewport.name = "Trashcan3DViewport"
	_viewport.size = VIEWPORT_SIZE
	_viewport.transparent_bg = true
	_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	_viewport.msaa_3d = Viewport.MSAA_2X
	_viewport.screen_space_aa = Viewport.SCREEN_SPACE_AA_DISABLED
	_viewport_container.add_child(_viewport)

	_camera = Camera3D.new()
	_camera.name = "Camera3D"
	_camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	_camera.size = 3.35
	_camera.position = Vector3(0.0, 0.35, 5.1)
	_camera.look_at(Vector3(0.0, -0.05, 0.0), Vector3.UP)
	_viewport.add_child(_camera)

	_trashcan_3d = Trashcan3DControllerScript.new()
	_trashcan_3d.name = "Trashcan3DRoot"
	_viewport.add_child(_trashcan_3d)


func _build_labels() -> void:
	_label = Label.new()
	_label.name = "Label"
	_label.text = "Drop to dismiss"
	_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_label.position = Vector2(0.0, size.y - 34.0)
	_label.size = Vector2(size.x, 22.0)
	_label.add_theme_color_override("font_color", Color(0.84, 0.97, 1.0, 0.76))
	_label.add_theme_font_size_override("font_size", 14)
	add_child(_label)

	_debug_label = Label.new()
	_debug_label.name = "ViewportDebugLabel"
	_debug_label.visible = false
	_debug_label.position = Vector2(12, 12)
	_debug_label.size = Vector2(size.x - 24, 42)
	_debug_label.add_theme_color_override("font_color", Color(0.52, 0.90, 1.0, 0.70))
	_debug_label.add_theme_font_size_override("font_size", 11)
	add_child(_debug_label)
	_update_debug_label()


func _draw() -> void:
	if _debug_show_active_area:
		var rect := Rect2(Vector2.ZERO, size)
		draw_rect(rect, Color(CYAN.r, CYAN.g, CYAN.b, 0.055), false, 2.0)
		draw_rect(rect.grow(DROP_PADDING), Color(CYAN.r, CYAN.g, CYAN.b, 0.10), false, 2.0)


func _set_viewport_rendering(enabled: bool) -> void:
	if _viewport == null:
		return
	# Rendering is cheap: a 384x384 SubViewport, no physics, no shadows, unshaded transparent materials.
	# When hidden we disable updates so the prototype does not keep drawing offscreen.
	_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS if enabled else SubViewport.UPDATE_DISABLED
	_update_debug_label()


func _update_debug_label() -> void:
	if not is_instance_valid(_debug_label):
		return
	_debug_label.visible = _debug_show_active_area
	_debug_label.text = "%s\nstate=%s armed=%.2f" % [get_viewport_status(), state, _armed_strength]
	if is_instance_valid(_label):
		_label.modulate.a = 0.55 + _armed_strength * 0.35
	queue_redraw()


func _kill_tween() -> void:
	if _active_tween and _active_tween.is_valid():
		_active_tween.kill()


class _ConsumePreviewCard:
	extends Control

	func _ready() -> void:
		pivot_offset = size * 0.5

	func _draw() -> void:
		var box := StyleBoxFlat.new()
		box.bg_color = Color(0.01, 0.05, 0.11, 0.84)
		box.border_color = Color(0.36, 0.88, 1.0, 0.72)
		box.set_border_width_all(1)
		box.set_corner_radius_all(8)
		box.shadow_color = Color(0.10, 0.56, 1.0, 0.30)
		box.shadow_size = 14
		draw_style_box(box, Rect2(Vector2.ZERO, size))
		draw_line(Vector2(16, 24), Vector2(size.x - 16, 24), Color(0.86, 0.98, 1.0, 0.42), 2.0)
		draw_line(Vector2(16, 45), Vector2(size.x - 34, 45), Color(0.44, 0.88, 1.0, 0.28), 2.0)
