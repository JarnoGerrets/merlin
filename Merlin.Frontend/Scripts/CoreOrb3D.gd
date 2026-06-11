extends Control
class_name CoreOrb3D

signal state_changed(state)

@onready var orb_controller = $SubViewportContainer/SubViewport/OrbRoot


func set_idle() -> void:
	orb_controller.set_idle()
	state_changed.emit("idle")


func set_thinking() -> void:
	orb_controller.set_thinking()
	state_changed.emit("thinking")


func set_speaking() -> void:
	orb_controller.set_speaking()
	state_changed.emit("speaking")


func play_tool_execution(duration: float = 2.5) -> void:
	orb_controller.play_tool_execution(duration)
	state_changed.emit("executing_tool")


func play_error(duration: float = 3.0) -> void:
	orb_controller.play_error(duration)
	state_changed.emit("error")


func notify_speech_tick() -> void:
	orb_controller.notify_speech_tick()
