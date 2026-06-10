extends Control
class_name CoreOrb

signal state_changed(state)

enum MerlinState {
	IDLE,
	THINKING,
	EXECUTING_TOOL,
	ERROR
}

const STATE_IDLE := MerlinState.IDLE
const STATE_THINKING := MerlinState.THINKING
const STATE_EXECUTING_TOOL := MerlinState.EXECUTING_TOOL
const STATE_ERROR := MerlinState.ERROR

var current_state: int = MerlinState.IDLE
var _phase := 0.0
var _activity := 1.0


func _ready() -> void:
	custom_minimum_size = Vector2(180, 180)
	pivot_offset = custom_minimum_size * 0.5
	set_process(true)


func _process(delta: float) -> void:
	_phase += delta * _activity
	queue_redraw()


func _draw() -> void:
	var center := size * 0.5
	var base_radius := minf(size.x, size.y) * 0.23
	var pulse := sin(_phase * TAU) * _pulse_amount()
	var radius := base_radius + pulse
	var color := _state_color()

	draw_circle(center, radius + 18.0, Color(color.r, color.g, color.b, 0.08))
	draw_arc(center, radius + 28.0, 0.0, TAU, 96, Color(color.r, color.g, color.b, 0.45), 2.0)
	draw_circle(center, radius, color)
	draw_circle(center + Vector2(-radius * 0.25, -radius * 0.25), radius * 0.35, Color(1, 1, 1, 0.18))


func set_state(state: int) -> void:
	if current_state == state:
		return

	current_state = state
	_activity = _activity_for_state(state)
	_start_scale_tween(_scale_for_state(state))
	state_changed.emit(current_state)
	queue_redraw()


func set_idle() -> void:
	set_state(MerlinState.IDLE)


func set_thinking() -> void:
	set_state(MerlinState.THINKING)


func play_tool_execution(duration: float = 0.75) -> void:
	_play_temporary_state(MerlinState.EXECUTING_TOOL, duration)


func play_error(duration: float = 1.0) -> void:
	_play_temporary_state(MerlinState.ERROR, duration)


func _play_temporary_state(state: int, duration: float) -> void:
	set_state(state)
	await get_tree().create_timer(duration).timeout
	if current_state == state:
		set_idle()


func _start_scale_tween(target_scale: Vector2) -> void:
	var tween := create_tween()
	tween.set_trans(Tween.TRANS_SINE)
	tween.set_ease(Tween.EASE_OUT)
	tween.tween_property(self, "scale", target_scale, 0.18)


func _activity_for_state(state: int) -> float:
	match state:
		MerlinState.THINKING:
			return 2.8
		MerlinState.EXECUTING_TOOL:
			return 4.2
		MerlinState.ERROR:
			return 5.0
		_:
			return 0.8


func _scale_for_state(state: int) -> Vector2:
	match state:
		MerlinState.THINKING:
			return Vector2(1.04, 1.04)
		MerlinState.EXECUTING_TOOL:
			return Vector2(1.12, 1.12)
		MerlinState.ERROR:
			return Vector2(1.08, 1.08)
		_:
			return Vector2.ONE


func _pulse_amount() -> float:
	match current_state:
		MerlinState.THINKING:
			return 8.0
		MerlinState.EXECUTING_TOOL:
			return 12.0
		MerlinState.ERROR:
			return 10.0
		_:
			return 4.0


func _state_color() -> Color:
	match current_state:
		MerlinState.THINKING:
			return Color(0.38, 0.72, 1.0, 0.95)
		MerlinState.EXECUTING_TOOL:
			return Color(0.45, 1.0, 0.72, 0.98)
		MerlinState.ERROR:
			return Color(1.0, 0.28, 0.34, 0.98)
		_:
			return Color(0.62, 0.86, 1.0, 0.9)
