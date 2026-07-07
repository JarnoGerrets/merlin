extends RefCounted
class_name MerlinWindowDefinition

const MerlinWindowCapabilitiesScript := preload("res://Scripts/UI/Windows/MerlinWindowCapabilities.gd")
const MerlinWindowConstantsScript := preload("res://Scripts/UI/Windows/MerlinWindowConstants.gd")

var window_type := ""
var title := ""
var default_size := Vector2.ZERO
var min_size := Vector2.ZERO
var max_size := Vector2.ZERO
var default_position := Vector2.ZERO
var default_position_mode := "absolute"
var remember_position := false
var remember_size := false
var layer_group := MerlinWindowConstantsScript.LAYER_GROUP_NORMAL_WINDOWS
var capabilities := MerlinWindowCapabilitiesScript.new()


func apply_chatlog_defaults() -> void:
	window_type = MerlinWindowConstantsScript.WINDOW_TYPE_CHATLOG
	title = "Chat"
	default_size = Vector2(420.0, 720.0)
	min_size = Vector2(280.0, 220.0)
	max_size = Vector2(900.0, 1100.0)
	default_position = Vector2.ZERO
	layer_group = MerlinWindowConstantsScript.LAYER_GROUP_NORMAL_WINDOWS
	capabilities.can_move = true
	capabilities.can_resize = true
	capabilities.can_dismiss = true
	capabilities.can_focus = true
	capabilities.accepts_gesture_grab = true
	capabilities.accepts_gesture_resize = true
	capabilities.accepts_gesture_dismiss = true
	capabilities.dismiss_mode = MerlinWindowConstantsScript.DISMISS_MODE_HIDE
