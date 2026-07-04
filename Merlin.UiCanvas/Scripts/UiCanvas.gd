extends Control

const ChatWindowSurface := preload("res://Scripts/ChatWindowSurface.gd")

const COLOR_BACKGROUND := Color(0.000, 0.008, 0.026, 1.0)
const COLOR_WHITE := Color(0.88, 0.96, 1.0, 1.0)

const WINDOW_SIZE := Vector2(525.6, 828.0)
const FLAT_SECONDS := 0.8
const CRUMPLE_SECONDS := 2.4
const BALL_HOLD_SECONDS := 0.45
const THROW_SECONDS := 1.05
const RESET_PAUSE_SECONDS := 0.35
const GRID_COLUMNS := 14
const GRID_ROWS := 9

var _time: float = 0.0
var _cycle_seconds: float = FLAT_SECONDS + CRUMPLE_SECONDS + BALL_HOLD_SECONDS + THROW_SECONDS + RESET_PAUSE_SECONDS
var _cells: Array[Dictionary] = []
var _window_viewport: SubViewport


func _ready() -> void:
	_create_window_texture()
	_build_pieces()


func _process(delta: float) -> void:
	_time = fmod(_time + delta, _cycle_seconds)
	queue_redraw()


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), COLOR_BACKGROUND, true)
	var center: Vector2 = size * 0.5
	var window_rect: Rect2 = Rect2(center - WINDOW_SIZE * 0.5, WINDOW_SIZE)
	var phase: Dictionary = _phase_values()
	var crumple: float = float(phase["crumple"])
	var throw_t: float = float(phase["throw"])
	var ball_formed: bool = bool(phase["ball_formed"])
	var throw_vector: Vector2 = Vector2(1.0, -0.32).normalized()
	var throw_offset: Vector2 = throw_vector * _ease_out_cubic(throw_t) * maxf(size.x, size.y) * 0.9
	var ball_center: Vector2 = window_rect.get_center() + throw_offset

	if crumple < 0.015 and throw_t <= 0.0:
		_draw_flat_window(window_rect, 1.0)
		if crumple <= 0.0:
			return

	_draw_shadow_cluster(ball_center, crumple, throw_t)
	var sheet_vertices: Array[Vector2] = _build_sheet_vertices(window_rect, ball_center, crumple, throw_t)
	for cell in _cells:
		_draw_sheet_cell(cell, sheet_vertices, crumple, throw_t)

	if ball_formed:
		_draw_ball_core(ball_center, crumple, throw_t)


func _create_window_texture() -> void:
	_window_viewport = SubViewport.new()
	_window_viewport.size = Vector2i(int(WINDOW_SIZE.x), int(WINDOW_SIZE.y))
	_window_viewport.transparent_bg = true
	_window_viewport.render_target_clear_mode = SubViewport.CLEAR_MODE_ALWAYS
	_window_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(_window_viewport)

	var surface: Control = ChatWindowSurface.new()
	surface.size = WINDOW_SIZE
	surface.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_window_viewport.add_child(surface)


func _build_pieces() -> void:
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


func _phase_values() -> Dictionary:
	var local: float = _time
	if local < FLAT_SECONDS:
		return {"crumple": 0.0, "throw": 0.0, "ball_formed": false}
	local -= FLAT_SECONDS
	if local < CRUMPLE_SECONDS:
		var crumple: float = _ease_in_out_cubic(local / CRUMPLE_SECONDS)
		return {"crumple": crumple, "throw": 0.0, "ball_formed": crumple > 0.82}
	local -= CRUMPLE_SECONDS
	if local < BALL_HOLD_SECONDS:
		return {"crumple": 1.0, "throw": 0.0, "ball_formed": true}
	local -= BALL_HOLD_SECONDS
	if local < THROW_SECONDS:
		return {"crumple": 1.0, "throw": local / THROW_SECONDS, "ball_formed": true}
	return {"crumple": 0.0, "throw": 0.0, "ball_formed": false}


func _draw_flat_window(rect: Rect2, alpha: float) -> void:
	var texture: Texture2D = _window_texture()
	if texture == null:
		return
	draw_texture_rect(texture, rect, false, Color(1.0, 1.0, 1.0, alpha))


func _draw_shadow_cluster(center: Vector2, crumple: float, throw_t: float) -> void:
	var radius: float = lerpf(280.0, 72.0, crumple) * lerpf(1.0, 0.7, throw_t)
	draw_circle(center + Vector2(0.0, 20.0), radius, Color(0.0, 0.0, 0.0, 0.12 * crumple))


func _draw_ball_core(center: Vector2, _crumple: float, throw_t: float) -> void:
	var radius: float = 58.0 * lerpf(1.0, 0.74, throw_t)
	var points: PackedVector2Array = PackedVector2Array()
	for index in range(18):
		var angle: float = TAU * float(index) / 18.0
		var wobble: float = 1.0 + sin(angle * 3.0 + _time * 1.7) * 0.09 + cos(angle * 5.0) * 0.06
		points.append(center + Vector2(cos(angle), sin(angle)) * radius * wobble)
	draw_colored_polygon(points, Color(0.006, 0.028, 0.066, 0.28))


func _build_sheet_vertices(window_rect: Rect2, ball_center: Vector2, crumple: float, throw_t: float) -> Array[Vector2]:
	var vertices: Array[Vector2] = []
	for y in range(GRID_ROWS + 1):
		for x in range(GRID_COLUMNS + 1):
			vertices.append(_sheet_vertex_position(x, y, window_rect, ball_center, crumple, throw_t))
	return vertices


func _sheet_vertex_position(x: int, y: int, window_rect: Rect2, ball_center: Vector2, crumple: float, throw_t: float) -> Vector2:
	var uv: Vector2 = Vector2(float(x) / float(GRID_COLUMNS), float(y) / float(GRID_ROWS))
	var base_position: Vector2 = window_rect.position + uv * window_rect.size
	var centered: Vector2 = uv - Vector2(0.5, 0.5)
	var seed: float = float(x * 31 + y * 53)
	var delay: float = clampf((absf(centered.x) + absf(centered.y)) * 0.28 + fmod(seed, 7.0) * 0.018, 0.0, 0.40)
	var fold_t: float = _ease_in_out_cubic(clampf((crumple - delay) / maxf(0.001, 1.0 - delay), 0.0, 1.0))
	var direction: Vector2 = centered.normalized() if centered.length() > 0.01 else Vector2(0.0, -1.0)
	var tangent: Vector2 = Vector2(-direction.y, direction.x)
	var ball_radius: float = lerpf(180.0, 104.0, crumple) * lerpf(1.0, 0.78, throw_t)
	var radial_variation: float = 0.76 + sin(seed * 1.71) * 0.16 + cos(seed * 0.47) * 0.10
	var folded_target: Vector2 = ball_center + direction * ball_radius * radial_variation
	folded_target += tangent * sin(seed * 0.37 + crumple * 5.8) * ball_radius * 0.34
	folded_target += Vector2(
		sin(seed * 0.81 + minf(crumple, 1.0) * 3.0),
		cos(seed * 0.59 + minf(crumple, 1.0) * 2.4)
	) * 16.0 * crumple
	var dragged_position: Vector2 = base_position.lerp(folded_target, fold_t)
	var wrinkle: Vector2 = Vector2(
		sin(uv.y * PI * 7.0 + minf(crumple, 1.0) * 3.1 + seed * 0.03),
		cos(uv.x * PI * 5.0 + minf(crumple, 1.0) * 2.7 + seed * 0.04)
	) * 10.0 * crumple * (1.0 - throw_t * 0.35)
	return dragged_position + wrinkle * fold_t


func _draw_sheet_cell(cell: Dictionary, vertices: Array[Vector2], crumple: float, throw_t: float) -> void:
	var texture: Texture2D = _window_texture()
	if texture == null:
		return
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
	points = _expand_quad(points, lerpf(0.0, 0.85, clampf(crumple / 0.24, 0.0, 1.0)))
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
	var alpha: float = lerpf(1.0, 0.88, crumple) * lerpf(1.0, 0.78, throw_t)
	var shade_phase: float = float(cell["shade_phase"])
	var local_t: float = _ease_in_out_cubic(clampf((crumple - float(cell["delay"])) / maxf(0.001, 1.0 - float(cell["delay"])), 0.0, 1.0))
	var facing_t: float = smoothstep(0.22, 0.92, crumple)
	var face_wave: float = cos(local_t * PI * 1.28 + sin(shade_phase) * 1.45)
	var front_visibility: float = lerpf(1.0, smoothstep(-0.18, 0.42, face_wave), facing_t)
	var backside_alpha: float = alpha * (1.0 - front_visibility) * 0.72
	if backside_alpha > 0.01:
		draw_colored_polygon(points, Color(0.016, 0.064, 0.140, backside_alpha))
	var shade_t: float = smoothstep(0.80, 1.0, crumple)
	var shade: float = clampf(lerpf(1.0, 1.0 - crumple * 0.12 + sin(shade_phase + crumple * 6.0) * crumple * 0.08, shade_t), 0.84, 1.12)
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


func _draw_texture_piece(texture_region: Rect2, center: Vector2, rect_size: Vector2, rotation: float, alpha: float) -> void:
	var texture: Texture2D = _window_texture()
	if texture == null:
		return
	draw_set_transform(center, rotation, Vector2.ONE)
	draw_texture_rect_region(
		texture,
		Rect2(rect_size * -0.5, rect_size),
		texture_region,
		Color(1.0, 1.0, 1.0, alpha)
	)
	draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)


func _window_texture() -> Texture2D:
	if _window_viewport == null:
		return null
	return _window_viewport.get_texture()


func _ease_in_out_cubic(t: float) -> float:
	var x: float = clampf(t, 0.0, 1.0)
	if x < 0.5:
		return 4.0 * x * x * x
	return 1.0 - pow(-2.0 * x + 2.0, 3.0) * 0.5


func _ease_out_cubic(t: float) -> float:
	var x: float = clampf(t, 0.0, 1.0)
	return 1.0 - pow(1.0 - x, 3.0)
