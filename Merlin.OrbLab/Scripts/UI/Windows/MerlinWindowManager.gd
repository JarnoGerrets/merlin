extends Node
class_name MerlinWindowManager

const MerlinWindowRegistryScript := preload("res://Scripts/UI/Windows/MerlinWindowRegistry.gd")

var registry := MerlinWindowRegistryScript.new()
var _definitions := {}
var _next_normal_z := 200


func register_window_definition(definition) -> void:
	if definition == null or definition.window_type.is_empty():
		return
	_definitions[definition.window_type] = definition


func get_window_definition(window_type: String):
	return _definitions.get(window_type)


func register_window_instance(window) -> void:
	if not is_instance_valid(window):
		return
	registry.register_window(window)
	window.focus_requested.connect(func(requested_window): focus_window(requested_window))
	window.close_requested.connect(func(requested_window): hide_window(requested_window.window_type))


func unregister_window_instance(window) -> void:
	registry.unregister_window(window)


func get_registered_window(window_type: String):
	return registry.get_window(window_type)


func show_window(window_type: String):
	var window = get_registered_window(window_type)
	if not is_instance_valid(window):
		return null
	window.show_window()
	focus_window(window)
	return window


func hide_window(window_type: String) -> void:
	var window = get_registered_window(window_type)
	if is_instance_valid(window):
		window.hide_window()


func focus_window(window) -> void:
	if not is_instance_valid(window):
		return
	bring_to_front(window)
	registry.set_focused_window(window)
	window.focus_window()


func bring_to_front(window) -> void:
	if not is_instance_valid(window):
		return
	_next_normal_z += 1
	window.z_index = _next_normal_z


func get_focused_window():
	return registry.get_focused_window()


func get_topmost_window_at(position: Vector2, capability: String = ""):
	if capability.is_empty():
		return registry.get_topmost_at_position(position)
	return registry.get_topmost_with_capability_at_position(position, capability)
