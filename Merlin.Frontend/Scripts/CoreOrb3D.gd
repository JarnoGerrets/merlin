extends Control
class_name CoreOrb3D

signal state_changed(state)

const ORGANISM_SCRIPT_PATH := "res://Scripts/MerlinOrganism3D.gd"

var _viewport_container: SubViewportContainer
var _viewport: SubViewport
var _organism: Node
var _temporary_state_token := 0


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_build_viewport()
	resized.connect(_sync_viewport_size)
	_sync_viewport_size()


func set_idle() -> void:
	if _organism != null:
		_organism.call("set_idle")
	state_changed.emit("idle")


func set_thinking() -> void:
	if _organism != null:
		_organism.call("set_thinking")
	state_changed.emit("thinking")


func set_listening() -> void:
	if _organism != null:
		_organism.call("set_listening")
	state_changed.emit("listening")


func set_speaking() -> void:
	if _organism != null:
		_organism.call("set_speaking")
	state_changed.emit("speaking")


func start_speaking_startup_profile() -> void:
	if _organism != null:
		_organism.call("start_speaking_startup_profile")


func play_tool_execution(duration: float = 2.5) -> void:
	_temporary_state_token += 1
	var token := _temporary_state_token
	if _organism != null:
		_organism.call("play_tool_execution", duration)
	state_changed.emit("executing_tool")
	await get_tree().create_timer(duration).timeout
	if token == _temporary_state_token:
		set_idle()


func play_error(duration: float = 3.0) -> void:
	_temporary_state_token += 1
	if _organism != null:
		_organism.call("play_error", duration)
	state_changed.emit("error")


func play_confirmation() -> void:
	_temporary_state_token += 1
	if _organism != null:
		_organism.call("play_confirmation")
	state_changed.emit("confirmation")


func notify_speech_tick(character: String = "", delay: float = 0.0, progress: float = 0.0) -> void:
	if _organism != null:
		_organism.call("notify_speech_tick", character, delay, progress)


func _build_viewport() -> void:
	_viewport_container = SubViewportContainer.new()
	_viewport_container.name = "OrganismViewportContainer"
	_viewport_container.set_anchors_preset(Control.PRESET_FULL_RECT)
	_viewport_container.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_viewport_container.stretch = true
	add_child(_viewport_container)

	_viewport = SubViewport.new()
	_viewport.name = "OrganismViewport"
	_viewport.transparent_bg = true
	_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	_viewport.msaa_3d = Viewport.MSAA_4X
	_viewport.screen_space_aa = Viewport.SCREEN_SPACE_AA_DISABLED
	_viewport_container.add_child(_viewport)

	var organism_script: Script = load(ORGANISM_SCRIPT_PATH) as Script
	if organism_script == null:
		push_error("Unable to load MerlinOrganism3D script.")
		return
	_organism = organism_script.new() as Node
	if _organism == null:
		push_error("MerlinOrganism3D script did not create a Node.")
		return
	_organism.name = "MerlinOrganism3D"
	_viewport.add_child(_organism)


func _sync_viewport_size() -> void:
	if _viewport == null:
		return
	var render_scale := 2.0
	var next_size := Vector2i(maxi(1, int(size.x * render_scale)), maxi(1, int(size.y * render_scale)))
	_viewport.size = next_size
