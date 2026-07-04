extends RefCounted
class_name MerlinWindowRegistry

var _windows := {}
var _focused_window_id := ""


func register_window(window) -> void:
	if not is_instance_valid(window):
		return
	_windows[window.window_type] = window


func unregister_window(window) -> void:
	if not is_instance_valid(window):
		return
	if _windows.get(window.window_type) == window:
		_windows.erase(window.window_type)
	if _focused_window_id == window.window_type:
		_focused_window_id = ""


func get_window(window_type: String):
	var window = _windows.get(window_type)
	return window if is_instance_valid(window) else null


func get_visible_windows() -> Array:
	var result := []
	for window in _windows.values():
		if is_instance_valid(window) and window.visible:
			result.append(window)
	return result


func set_focused_window(window) -> void:
	_focused_window_id = window.window_type if is_instance_valid(window) else ""


func get_focused_window():
	return get_window(_focused_window_id)


func get_topmost_at_position(position: Vector2):
	return _topmost_matching(position, "")


func get_topmost_with_capability_at_position(position: Vector2, capability: String):
	return _topmost_matching(position, capability)


func _topmost_matching(position: Vector2, capability: String):
	var best_window = null
	var best_z := -9223372036854775807
	for window in get_visible_windows():
		var candidate = window
		if not candidate.contains_screen_point(position):
			continue
		if not capability.is_empty() and not candidate.has_capability(capability):
			continue
		if candidate.z_index >= best_z:
			best_z = candidate.z_index
			best_window = candidate
	return best_window
