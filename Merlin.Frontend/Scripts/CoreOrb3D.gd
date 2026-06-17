extends Control
class_name CoreOrb3D

signal state_changed(state)

const ORGANISM_SCRIPT_PATH := "res://Scripts/MerlinOrganism3D.gd"
var _viewport_container: SubViewportContainer
var _viewport: SubViewport
var _organism: Node
var _temporary_state_token := 0
var visual_state := {
	"mode": "idle",
	"energy": 0.0,
	"speech_energy": 0.0,
	"thinking_intensity": 0.0,
	"error_intensity": 0.0,
	"confirmation_intensity": 0.0,
	"tool_intensity": 0.0,
}


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_build_viewport()
	resized.connect(_sync_viewport_size)
	_sync_viewport_size()


func set_idle() -> void:
	update_visual_state({ "mode": "idle", "energy": 0.0, "thinking_intensity": 0.0, "tool_intensity": 0.0 })
	state_changed.emit("idle")


func set_thinking() -> void:
	update_visual_state({ "mode": "thinking", "energy": 0.55, "thinking_intensity": 1.0 })
	state_changed.emit("thinking")


func set_listening() -> void:
	update_visual_state({ "mode": "listening", "energy": 0.35 })
	state_changed.emit("listening")


func set_speaking() -> void:
	update_visual_state({ "mode": "speaking", "energy": 0.72, "speech_energy": maxf(float(visual_state.get("speech_energy", 0.0)), 0.62) })
	state_changed.emit("speaking")


func start_speaking_startup_profile() -> void:
	if _organism != null:
		_organism.call("start_speaking_startup_profile")


func play_tool_execution(duration: float = 2.5) -> void:
	_temporary_state_token += 1
	var token := _temporary_state_token
	update_visual_state({ "mode": "tool", "energy": 0.68, "tool_intensity": 1.0 })
	state_changed.emit("executing_tool")
	await get_tree().create_timer(duration).timeout
	if token == _temporary_state_token:
		set_idle()


func play_error(duration: float = 3.0) -> void:
	_temporary_state_token += 1
	update_visual_state({ "energy": maxf(float(visual_state.get("energy", 0.0)), 0.62), "error_intensity": 1.0 })
	state_changed.emit("error")


func play_confirmation() -> void:
	_temporary_state_token += 1
	update_visual_state({ "energy": maxf(float(visual_state.get("energy", 0.0)), 0.36), "confirmation_intensity": 1.0 })
	state_changed.emit("confirmation")


func set_overlay_intensity(kind: String, strength: float) -> void:
	var clamped := clampf(strength, 0.0, 1.0)
	match kind:
		"error":
			update_visual_state({ "error_intensity": clamped })
		"confirmation":
			update_visual_state({ "confirmation_intensity": clamped })


func notify_speech_tick(character: String = "", delay: float = 0.0, progress: float = 0.0) -> void:
	update_visual_state({ "mode": "speaking", "speech_energy": maxf(float(visual_state.get("speech_energy", 0.0)), 0.62), "energy": 0.72 })
	if _organism != null:
		_organism.call("notify_speech_tick", character, delay, progress)


func set_speech_energy_override(value: float) -> void:
	update_visual_state({ "speech_energy": clampf(value, 0.0, 1.0) })
	if _organism != null:
		_organism.call("set_speech_energy_override", value)


func update_visual_state(patch: Dictionary) -> void:
	for key in patch.keys():
		visual_state[key] = patch[key]
	if _organism != null:
		_organism.call("set_visual_state", visual_state)


func set_manual_rotation_enabled(enabled: bool) -> void:
	if _organism != null:
		_organism.call("set_manual_rotation_enabled", enabled)


func set_manual_rotation(yaw: float, pitch: float) -> void:
	if _organism != null:
		_organism.call("set_manual_rotation", yaw, pitch)


func set_orb_lab_zoom(value: float) -> void:
	if _organism != null:
		_organism.call("set_orb_lab_zoom", value)


func get_orb_lab_parameters() -> Array:
	if _organism == null:
		return []
	return _organism.call("get_orb_lab_parameters")


func apply_orb_lab_parameter(parameter_name: String, value) -> bool:
	if _organism == null:
		return false
	return bool(_organism.call("apply_orb_lab_parameter", parameter_name, value))


func reset_orb_lab_parameter(parameter_name: String) -> bool:
	if _organism == null:
		return false
	return bool(_organism.call("reset_orb_lab_parameter", parameter_name))


func trigger_cluster_birth_death() -> void:
	if _organism != null:
		_organism.call("trigger_cluster_birth_death")


func get_orb_lab_state_features() -> Dictionary:
	if _organism == null:
		return {}
	return _organism.call("get_orb_lab_state_features")


func get_orb_lab_state_feature_labels() -> Dictionary:
	if _organism == null:
		return {}
	return _organism.call("get_orb_lab_state_feature_labels")


func apply_orb_lab_state_feature(state_name: String, feature_name: String, enabled: bool) -> bool:
	if _organism == null:
		return false
	return bool(_organism.call("apply_orb_lab_state_feature", state_name, feature_name, enabled))


func reset_orb_lab_state_features(state_name: String = "") -> bool:
	if _organism == null:
		return false
	return bool(_organism.call("reset_orb_lab_state_features", state_name))


func get_orb_lab_clusters() -> Array:
	if _organism == null:
		return []
	return _organism.call("get_orb_lab_clusters")


func select_orb_lab_cluster(cluster_id: int) -> bool:
	if _organism == null:
		return false
	return bool(_organism.call("select_orb_lab_cluster", cluster_id))


func apply_orb_lab_cluster_parameter(cluster_id: int, parameter_name: String, value: float) -> bool:
	if _organism == null:
		return false
	return bool(_organism.call("apply_orb_lab_cluster_parameter", cluster_id, parameter_name, value))


func reset_orb_lab_cluster(cluster_id: int) -> bool:
	if _organism == null:
		return false
	return bool(_organism.call("reset_orb_lab_cluster", cluster_id))


func get_orb_lab_connections() -> Array:
	if _organism == null:
		return []
	return _organism.call("get_orb_lab_connections")


func get_orb_lab_connection_overrides() -> Array:
	if _organism == null:
		return []
	var result: Array = _organism.call("get_orb_lab_connection_overrides") as Array
	return result


func select_orb_lab_connection(connection_id: int) -> bool:
	if _organism == null:
		return false
	return bool(_organism.call("select_orb_lab_connection", connection_id))


func apply_orb_lab_connection_parameter(connection_id: int, parameter_name: String, value) -> bool:
	if _organism == null:
		return false
	return bool(_organism.call("apply_orb_lab_connection_parameter", connection_id, parameter_name, value))


func reset_orb_lab_connection(connection_id: int) -> bool:
	if _organism == null:
		return false
	return bool(_organism.call("reset_orb_lab_connection", connection_id))


func pick_orb_lab_item(local_position: Vector2) -> Dictionary:
	if _organism == null or _viewport == null:
		return {}
	var render_scale := Vector2(float(_viewport.size.x) / maxf(size.x, 1.0), float(_viewport.size.y) / maxf(size.y, 1.0))
	var result: Dictionary = _organism.call("pick_orb_lab_item", local_position * render_scale, Vector2(_viewport.size)) as Dictionary
	return result


func apply_orb_builder_tool(tool_name: String, local_position: Vector2, depth: float = 0.35) -> Dictionary:
	if _organism == null or _viewport == null:
		return {}
	var render_scale := Vector2(float(_viewport.size.x) / maxf(size.x, 1.0), float(_viewport.size.y) / maxf(size.y, 1.0))
	var result: Dictionary = _organism.call("apply_orb_builder_tool", tool_name, local_position * render_scale, Vector2(_viewport.size), depth) as Dictionary
	return result


func get_orb_builder_snapshot() -> Dictionary:
	if _organism == null:
		return {}
	var result: Dictionary = _organism.call("get_orb_builder_snapshot") as Dictionary
	return result


func apply_orb_builder_snapshot(snapshot: Dictionary) -> bool:
	if _organism == null:
		return false
	return bool(_organism.call("apply_orb_builder_snapshot", snapshot))


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
