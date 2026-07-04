extends RefCounted
class_name MerlinWindowCapabilities

const MerlinWindowConstantsScript := preload("res://Scripts/UI/Windows/MerlinWindowConstants.gd")

var can_move := true
var can_resize := true
var can_dismiss := true
var can_focus := true
var accepts_gesture_grab := true
var accepts_gesture_resize := true
var accepts_gesture_dismiss := true
var preserve_aspect_ratio := false
var dismiss_mode := MerlinWindowConstantsScript.DISMISS_MODE_HIDE


func has_capability(capability: String) -> bool:
	match capability:
		MerlinWindowConstantsScript.CAPABILITY_GESTURE_GRAB:
			return accepts_gesture_grab
		MerlinWindowConstantsScript.CAPABILITY_GESTURE_RESIZE:
			return accepts_gesture_resize
		MerlinWindowConstantsScript.CAPABILITY_DISMISS:
			return can_dismiss
		MerlinWindowConstantsScript.CAPABILITY_GESTURE_DISMISS:
			return accepts_gesture_dismiss and can_dismiss
		_:
			return false
