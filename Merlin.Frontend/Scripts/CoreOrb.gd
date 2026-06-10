extends Control
class_name CoreOrb

signal state_changed(state)

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

const STATE_IDLE := MerlinState.IDLE
const STATE_THINKING := MerlinState.THINKING
const STATE_SPEAKING := MerlinState.SPEAKING
const STATE_EXECUTING_TOOL := MerlinState.EXECUTING_TOOL
const STATE_ERROR := MerlinState.ERROR

const BASE_COLOR := Color(0.48, 0.82, 1.0, 0.95)
const THINKING_COLOR := Color(0.36, 0.72, 1.0, 0.98)
const SPEAKING_COLOR := Color(0.42, 1.0, 0.86, 0.98)
const TOOL_COLOR := Color(0.52, 1.0, 0.66, 1.0)
const ERROR_COLOR := Color(1.0, 0.25, 0.34, 1.0)

var current_state: int = MerlinState.IDLE

var _phase := 0.0
var _breath := 0.0
var _target_activity := 0.65
var _activity := 0.65
var _state_intensity := 0.0
var _target_intensity := 0.0
var _speech_energy := 0.0
var _tool_surge := 0.0
var _error_flash := 0.0
var _temporary_state_token := 0


func _ready() -> void:
	custom_minimum_size = Vector2(220, 220)
	pivot_offset = size * 0.5
	set_process(true)


func _process(delta: float) -> void:
	pivot_offset = size * 0.5
	_activity = lerpf(_activity, _target_activity, minf(delta * 5.0, 1.0))
	_state_intensity = lerpf(_state_intensity, _target_intensity, minf(delta * 4.0, 1.0))
	_speech_energy = maxf(_speech_energy - delta * 2.8, 0.0)
	_tool_surge = maxf(_tool_surge - delta * 1.5, 0.0)
	_error_flash = maxf(_error_flash - delta * 2.2, 0.0)
	_phase += delta * _activity
	_breath += delta
	queue_redraw()


func _draw() -> void:
	var center := size * 0.5 + _state_offset()
	var radius := _base_radius()
	var color := _state_color()

	_draw_core_glow(center, radius, color)
	_draw_outer_ring(center, radius, color)
	_draw_inner_ring(center, radius, color)
	_draw_particle_layer(center, radius, color)
	_draw_state_effects(center, radius, color)


func set_state(state: int) -> void:
	if current_state == state:
		return

	current_state = state
	_apply_state_profile(state)
	state_changed.emit(current_state)
	queue_redraw()


func set_idle() -> void:
	set_state(MerlinState.IDLE)


func set_thinking() -> void:
	set_state(MerlinState.THINKING)


func set_speaking() -> void:
	set_state(MerlinState.SPEAKING)


func notify_speech_tick() -> void:
	if current_state == MerlinState.SPEAKING:
		_speech_energy = minf(_speech_energy + 0.18, 1.0)


func play_tool_execution(duration: float = 0.55) -> void:
	_tool_surge = 1.0
	_play_temporary_state(MerlinState.EXECUTING_TOOL, duration)


func play_error(duration: float = 0.95) -> void:
	_error_flash = 1.0
	_play_temporary_state(MerlinState.ERROR, duration)


func _play_temporary_state(state: int, duration: float) -> void:
	_temporary_state_token += 1
	var token := _temporary_state_token
	set_state(state)
	await get_tree().create_timer(duration).timeout
	if token == _temporary_state_token and current_state == state:
		set_idle()


func _apply_state_profile(state: int) -> void:
	var target_scale := Vector2.ONE

	match state:
		MerlinState.THINKING:
			_target_activity = 1.25
			_target_intensity = 0.45
			target_scale = Vector2(0.97, 0.97)
		MerlinState.SPEAKING:
			_target_activity = 1.65
			_target_intensity = 0.72
			target_scale = Vector2(1.02, 1.02)
		MerlinState.EXECUTING_TOOL:
			_target_activity = 2.1
			_target_intensity = 1.0
			target_scale = Vector2(1.08, 1.08)
		MerlinState.ERROR:
			_target_activity = 1.85
			_target_intensity = 0.9
			target_scale = Vector2(1.04, 1.04)
		_:
			_target_activity = 0.65
			_target_intensity = 0.12
			target_scale = Vector2.ONE

	_start_scale_tween(target_scale)


func _start_scale_tween(target_scale: Vector2) -> void:
	var tween := create_tween()
	tween.set_trans(Tween.TRANS_SINE)
	tween.set_ease(Tween.EASE_OUT)
	tween.tween_property(self, "scale", target_scale, 0.28)


func _draw_core_glow(center: Vector2, radius: float, color: Color) -> void:
	var breathing := sin(_breath * 1.15) * 0.5 + 0.5
	var speaking_boost := _speech_energy * 8.0
	var glow_radius := radius + 34.0 + breathing * 5.0 + _state_intensity * 10.0 + speaking_boost

	draw_circle(center, glow_radius, Color(color.r, color.g, color.b, 0.05 + _state_intensity * 0.04))
	draw_circle(center, radius + 16.0 + speaking_boost * 0.4, Color(color.r, color.g, color.b, 0.13))
	draw_circle(center, radius, Color(color.r, color.g, color.b, 0.72))
	draw_circle(center + Vector2(-radius * 0.22, -radius * 0.25), radius * 0.36, Color(1, 1, 1, 0.16))


func _draw_inner_ring(center: Vector2, radius: float, color: Color) -> void:
	var ring_radius := radius + 11.0 + sin(_phase * TAU * 0.8) * (1.0 + _state_intensity)
	var alpha := 0.36 + _state_intensity * 0.16 + _speech_energy * 0.2

	draw_arc(center, ring_radius, 0.0, TAU, 128, Color(color.r, color.g, color.b, alpha), 2.0)

	var gap_angle := _phase * TAU * 0.28
	draw_arc(center, ring_radius + 4.0, gap_angle, gap_angle + PI * 0.85, 48, Color(1, 1, 1, 0.24 + _speech_energy * 0.15), 2.0)


func _draw_outer_ring(center: Vector2, radius: float, color: Color) -> void:
	var outer_radius := radius + 28.0 + _state_intensity * 5.0
	var rotation := _phase * TAU * _rotation_direction()
	var segment_alpha := 0.28 + _state_intensity * 0.22

	draw_arc(center, outer_radius, 0.0, TAU, 160, Color(color.r, color.g, color.b, 0.14), 1.0)

	for i in range(3):
		var start_angle := rotation + i * TAU / 3.0
		var end_angle := start_angle + PI * (0.34 + _state_intensity * 0.08)
		draw_arc(center, outer_radius, start_angle, end_angle, 28, Color(color.r, color.g, color.b, segment_alpha), 2.5)


func _draw_particle_layer(center: Vector2, radius: float, color: Color) -> void:
	var particle_count := 8
	var particle_radius := radius + 46.0
	var alpha := 0.14 + _state_intensity * 0.16

	for i in range(particle_count):
		var angle := _phase * TAU * 0.16 + i * TAU / particle_count
		var orbit := particle_radius + sin(_phase * TAU + i) * 3.0
		var point := center + Vector2(cos(angle), sin(angle)) * orbit
		var size_boost := 1.0 + _speech_energy * 1.5
		draw_circle(point, 1.4 * size_boost, Color(color.r, color.g, color.b, alpha))


func _draw_state_effects(center: Vector2, radius: float, color: Color) -> void:
	if current_state == MerlinState.SPEAKING:
		var pulse_radius := radius + 22.0 + _speech_energy * 22.0
		draw_arc(center, pulse_radius, 0.0, TAU, 128, Color(color.r, color.g, color.b, _speech_energy * 0.42), 3.0)

	if current_state == MerlinState.EXECUTING_TOOL or _tool_surge > 0.0:
		var scan_radius := radius + 18.0 + (1.0 - _tool_surge) * 38.0
		draw_arc(center, scan_radius, 0.0, TAU, 128, Color(TOOL_COLOR.r, TOOL_COLOR.g, TOOL_COLOR.b, _tool_surge * 0.48), 3.0)
		draw_circle(center, radius + 56.0, Color(TOOL_COLOR.r, TOOL_COLOR.g, TOOL_COLOR.b, _tool_surge * 0.06))

	if current_state == MerlinState.ERROR or _error_flash > 0.0:
		var wobble := sin(_phase * TAU * 3.0) * 3.0
		draw_arc(center + Vector2(wobble, 0), radius + 35.0, 0.0, TAU, 96, Color(ERROR_COLOR.r, ERROR_COLOR.g, ERROR_COLOR.b, 0.25 + _error_flash * 0.35), 3.0)


func _base_radius() -> float:
	var base := minf(size.x, size.y) * 0.19
	var breathing := sin(_breath * 1.15) * 2.4
	var focused := -2.5 if current_state == MerlinState.THINKING else 0.0
	var speaking := sin(_phase * TAU * 1.4) * (2.4 + _speech_energy * 5.5) if current_state == MerlinState.SPEAKING else 0.0

	return base + breathing + focused + speaking + _tool_surge * 5.0


func _state_offset() -> Vector2:
	if current_state == MerlinState.ERROR:
		return Vector2(sin(_phase * TAU * 3.0) * 2.0, 0.0)

	var drift_x := sin(_breath * 0.72) * 1.4
	var drift_y := cos(_breath * 0.61) * 1.1
	return Vector2(drift_x, drift_y)


func _rotation_direction() -> float:
	match current_state:
		MerlinState.THINKING:
			return 0.55
		MerlinState.SPEAKING:
			return 0.72
		MerlinState.EXECUTING_TOOL:
			return 1.1
		MerlinState.ERROR:
			return -0.75
		_:
			return 0.22


func _state_color() -> Color:
	match current_state:
		MerlinState.THINKING:
			return THINKING_COLOR
		MerlinState.SPEAKING:
			return SPEAKING_COLOR
		MerlinState.EXECUTING_TOOL:
			return TOOL_COLOR
		MerlinState.ERROR:
			return ERROR_COLOR
		_:
			return BASE_COLOR
