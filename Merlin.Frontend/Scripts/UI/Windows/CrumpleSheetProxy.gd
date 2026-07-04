extends Control

const COLOR_WHITE := Color(0.88, 0.96, 1.0, 1.0)
const GRID_COLUMNS := 14
const GRID_ROWS := 9
const SURFACE_PADDING := 36.0

var _source_viewport: SubViewport
var _texture_size: Vector2 = Vector2.ZERO
var _source_size: Vector2 = Vector2.ZERO
var _cells: Array[Dictionary] = []
var _crumple: float = 0.0
var _throw_t: float = 0.0
var _throw_offset: Vector2 = Vector2.ZERO
var _ball_formed: bool = false
var _texture_override: Texture2D
var _baked_texture: Texture2D
var _bake_in_progress: bool = false
var _is_bake_renderer: bool = false


func configure_from_window(source_window: Control) -> void:
	if not is_instance_valid(source_window):
		return
	_source_size = source_window.size
	_texture_size = _source_size + Vector2(SURFACE_PADDING * 2.0, SURFACE_PADDING * 2.0)
	size = _texture_size
	pivot_offset = size * 0.5
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	z_index = 4095
	z_as_relative = false
	position = source_window.get_global_rect().position - Vector2(SURFACE_PADDING, SURFACE_PADDING)
	_build_source_texture(source_window)
	_build_cells()
	queue_redraw()


func set_crumple_progress(progress: float, ball_formed: bool) -> void:
	if _baked_texture != null:
		return
	_crumple = clampf(progress, 0.0, 1.0)
	_ball_formed = ball_formed
	queue_redraw()


func throw_and_free(throw_velocity: Vector2, viewport_size: Vector2, min_ms: float, max_ms: float) -> void:
	var direction: Vector2 = throw_velocity.normalized() if throw_velocity.length() > 0.0 else Vector2(1.0, -0.35).normalized()
	var distance: float = maxf(viewport_size.x, viewport_size.y) + 260.0
	var duration_ms: float = clampf(520000.0 / maxf(throw_velocity.length(), 1.0), min_ms, max_ms)
	var start_position: Vector2 = position
	var tween: Tween = create_tween()
	tween.set_parallel(true)
	if _baked_texture != null:
		tween.tween_property(self, "position", start_position + direction * distance, duration_ms / 1000.0).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		tween.tween_property(self, "rotation", direction.x * 1.35, duration_ms / 1000.0).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		tween.tween_property(self, "modulate:a", 0.0, duration_ms / 1000.0).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN)
	else:
		tween.tween_method(func(value: float) -> void:
			_throw_t = value
			_throw_offset = direction * distance * smoothstep(0.0, 1.0, value)
			position = start_position + _throw_offset
			rotation = direction.x * 1.35 * value
			modulate.a = 1.0 - smoothstep(0.62, 1.0, value)
			queue_redraw()
		, 0.0, 1.0, duration_ms / 1000.0).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.set_parallel(false)
	tween.tween_callback(queue_free)


func bake_final_ball_async() -> void:
	if _baked_texture != null or _bake_in_progress or _is_bake_renderer:
		return
	var texture: Texture2D = _window_texture()
	if texture == null:
		return

	_bake_in_progress = true
	var bake_viewport := SubViewport.new()
	bake_viewport.size = Vector2i(ceili(size.x), ceili(size.y))
	bake_viewport.transparent_bg = true
	bake_viewport.render_target_clear_mode = SubViewport.CLEAR_MODE_ALWAYS
	bake_viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	add_child(bake_viewport)

	var renderer: Control = get_script().new() as Control
	var baked_cells: Array[Dictionary] = _cells.duplicate(true)
	renderer.call("configure_as_bake_renderer", texture, _texture_size, _source_size, baked_cells)
	renderer.size = size
	renderer.position = Vector2.ZERO
	renderer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bake_viewport.add_child(renderer)
	renderer.queue_redraw()

	await RenderingServer.frame_post_draw
	if not is_instance_valid(self) or not is_instance_valid(bake_viewport):
		return
	var image: Image = bake_viewport.get_texture().get_image()
	_baked_texture = ImageTexture.create_from_image(image)
	if is_instance_valid(_source_viewport):
		_source_viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
	bake_viewport.queue_free()
	_bake_in_progress = false
	queue_redraw()


func configure_as_bake_renderer(texture: Texture2D, texture_size: Vector2, source_size: Vector2, cells: Array[Dictionary]) -> void:
	_is_bake_renderer = true
	_texture_override = texture
	_texture_size = texture_size
	_source_size = source_size
	_cells = cells
	_crumple = 1.0
	_throw_t = 0.0
	_ball_formed = true


func is_baked() -> bool:
	return _baked_texture != null


func _draw() -> void:
	if _baked_texture != null:
		draw_texture(_baked_texture, Vector2.ZERO)
		return
	var texture: Texture2D = _window_texture()
	if texture == null:
		return
	var sheet_vertices: Array[Vector2] = _build_sheet_vertices()
	for cell in _cells:
		_draw_sheet_cell(cell, sheet_vertices, texture)


func _build_source_texture(source_window: Control) -> void:
	_source_viewport = SubViewport.new()
	_source_viewport.size = Vector2i(int(_texture_size.x), int(_texture_size.y))
	_source_viewport.transparent_bg = true
	_source_viewport.render_target_clear_mode = SubViewport.CLEAR_MODE_ALWAYS
	_source_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(_source_viewport)

	var clone: Node = source_window.duplicate()
	if clone is Control:
		var control_clone: Control = clone
		control_clone.position = Vector2(SURFACE_PADDING, SURFACE_PADDING)
		control_clone.size = _source_size
		control_clone.visible = true
		control_clone.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_disable_mouse_recursive(control_clone)
	_source_viewport.add_child(clone)


func _disable_mouse_recursive(node: Node) -> void:
	if node is Control:
		var control: Control = node
		control.mouse_filter = Control.MOUSE_FILTER_IGNORE
	for child in node.get_children():
		_disable_mouse_recursive(child)


func _build_cells() -> void:
	_cells.clear()
	for y in range(GRID_ROWS):
		for x in range(GRID_COLUMNS):
			var uv_pos: Vector2 = Vector2(float(x) / float(GRID_COLUMNS), float(y) / float(GRID_ROWS))
			var uv_size: Vector2 = Vector2(1.0 / float(GRID_COLUMNS), 1.0 / float(GRID_ROWS))
			var center_uv: Vector2 = uv_pos + uv_size * 0.5
			var centered: Vector2 = center_uv - Vector2(0.5, 0.5)
			var seed: float = float(x * 19 + y * 37)
			_cells.append({
				"x": x,
				"y": y,
				"delay": clampf((absf(centered.x) + absf(centered.y)) * 0.34 + fmod(seed, 5.0) * 0.025, 0.0, 0.42),
				"shade_phase": seed * 0.17
			})


func _build_sheet_vertices() -> Array[Vector2]:
	var vertices: Array[Vector2] = []
	var window_rect: Rect2 = Rect2(Vector2.ZERO, _texture_size)
	var ball_center: Vector2 = window_rect.get_center()
	for y in range(GRID_ROWS + 1):
		for x in range(GRID_COLUMNS + 1):
			vertices.append(_sheet_vertex_position(x, y, window_rect, ball_center))
	return vertices


func _sheet_vertex_position(x: int, y: int, window_rect: Rect2, ball_center: Vector2) -> Vector2:
	var uv: Vector2 = Vector2(float(x) / float(GRID_COLUMNS), float(y) / float(GRID_ROWS))
	var base_position: Vector2 = window_rect.position + uv * window_rect.size
	var centered: Vector2 = uv - Vector2(0.5, 0.5)
	var seed: float = float(x * 31 + y * 53)
	var delay: float = clampf((absf(centered.x) + absf(centered.y)) * 0.28 + fmod(seed, 7.0) * 0.018, 0.0, 0.40)
	var fold_t: float = _ease_in_out_cubic(clampf((_crumple - delay) / maxf(0.001, 1.0 - delay), 0.0, 1.0))
	var direction: Vector2 = centered.normalized() if centered.length() > 0.01 else Vector2(0.0, -1.0)
	var tangent: Vector2 = Vector2(-direction.y, direction.x)
	var ball_radius: float = lerpf(180.0, 104.0, _crumple) * lerpf(1.0, 0.78, _throw_t)
	var radial_variation: float = 0.76 + sin(seed * 1.71) * 0.16 + cos(seed * 0.47) * 0.10
	var folded_target: Vector2 = ball_center + direction * ball_radius * radial_variation
	folded_target += tangent * sin(seed * 0.37 + _crumple * 5.8) * ball_radius * 0.34
	folded_target += Vector2(
		sin(seed * 0.81 + minf(_crumple, 1.0) * 3.0),
		cos(seed * 0.59 + minf(_crumple, 1.0) * 2.4)
	) * 16.0 * _crumple
	var dragged_position: Vector2 = base_position.lerp(folded_target, fold_t)
	var wrinkle: Vector2 = Vector2(
		sin(uv.y * PI * 7.0 + minf(_crumple, 1.0) * 3.1 + seed * 0.03),
		cos(uv.x * PI * 5.0 + minf(_crumple, 1.0) * 2.7 + seed * 0.04)
	) * 10.0 * _crumple * (1.0 - _throw_t * 0.35)
	return dragged_position + wrinkle * fold_t


func _draw_sheet_cell(cell: Dictionary, vertices: Array[Vector2], texture: Texture2D) -> void:
	var x: int = int(cell["x"])
	var y: int = int(cell["y"])
	var top_left: int = _vertex_index(x, y)
	var top_right: int = _vertex_index(x + 1, y)
	var bottom_right: int = _vertex_index(x + 1, y + 1)
	var bottom_left: int = _vertex_index(x, y + 1)
	var points: PackedVector2Array = PackedVector2Array([
		vertices[top_left],
		vertices[top_right],
		vertices[bottom_right],
		vertices[bottom_left]
	])
	points = _expand_quad(points, lerpf(0.0, 0.85, clampf(_crumple / 0.24, 0.0, 1.0)))
	var uv_left: float = float(x) / float(GRID_COLUMNS)
	var uv_top: float = float(y) / float(GRID_ROWS)
	var uv_right: float = float(x + 1) / float(GRID_COLUMNS)
	var uv_bottom: float = float(y + 1) / float(GRID_ROWS)
	var uvs: PackedVector2Array = PackedVector2Array([
		Vector2(uv_left, uv_top),
		Vector2(uv_right, uv_top),
		Vector2(uv_right, uv_bottom),
		Vector2(uv_left, uv_bottom)
	])
	var alpha: float = lerpf(1.0, 0.88, _crumple) * lerpf(1.0, 0.78, _throw_t)
	var shade_phase: float = float(cell["shade_phase"])
	var local_t: float = _ease_in_out_cubic(clampf((_crumple - float(cell["delay"])) / maxf(0.001, 1.0 - float(cell["delay"])), 0.0, 1.0))
	var facing_t: float = smoothstep(0.22, 0.92, _crumple)
	var face_wave: float = cos(local_t * PI * 1.28 + sin(shade_phase) * 1.45)
	var front_visibility: float = lerpf(1.0, smoothstep(-0.18, 0.42, face_wave), facing_t)
	var backside_alpha: float = alpha * (1.0 - front_visibility) * 0.72
	if backside_alpha > 0.01:
		draw_colored_polygon(points, Color(0.016, 0.064, 0.140, backside_alpha))
	var shade_t: float = smoothstep(0.80, 1.0, _crumple)
	var shade: float = clampf(lerpf(1.0, 1.0 - _crumple * 0.12 + sin(shade_phase + _crumple * 6.0) * _crumple * 0.08, shade_t), 0.84, 1.12)
	var front_alpha: float = alpha * front_visibility
	var colors: PackedColorArray = PackedColorArray([
		Color(shade, shade, shade, front_alpha),
		Color(lerpf(shade, shade * 0.96, shade_t), lerpf(shade, shade * 0.96, shade_t), lerpf(shade, shade * 0.96, shade_t), front_alpha),
		Color(lerpf(shade, shade * 0.88, shade_t), lerpf(shade, shade * 0.88, shade_t), lerpf(shade, shade * 0.88, shade_t), front_alpha),
		Color(lerpf(shade, shade * 1.04, shade_t), lerpf(shade, shade * 1.04, shade_t), lerpf(shade, shade * 1.04, shade_t), front_alpha)
	])
	if front_alpha > 0.01:
		draw_polygon(points, colors, uvs, texture)


func _expand_quad(points: PackedVector2Array, amount: float) -> PackedVector2Array:
	if amount <= 0.0:
		return points
	var center: Vector2 = Vector2.ZERO
	for point in points:
		center += point
	center /= float(points.size())
	var expanded: PackedVector2Array = PackedVector2Array()
	for point in points:
		var direction: Vector2 = point - center
		expanded.append(point + direction.normalized() * amount if direction.length() > 0.001 else point)
	return expanded


func _vertex_index(x: int, y: int) -> int:
	return y * (GRID_COLUMNS + 1) + x


func _window_texture() -> Texture2D:
	if _texture_override != null:
		return _texture_override
	if _source_viewport == null:
		return null
	return _source_viewport.get_texture()


func _ease_in_out_cubic(t: float) -> float:
	var x: float = clampf(t, 0.0, 1.0)
	if x < 0.5:
		return 4.0 * x * x * x
	return 1.0 - pow(-2.0 * x + 2.0, 3.0) * 0.5


func _ease_out_cubic(t: float) -> float:
	var x: float = clampf(t, 0.0, 1.0)
	return 1.0 - pow(1.0 - x, 3.0)
