extends PanelContainer
class_name BargeInDebugOverlay

const ROWS := [
	{"label": "Mic RMS", "raw": "micRms", "percent": "micRmsPercent", "kind": "energy"},
	{"label": "Mic Peak", "raw": "micPeak", "percent": "micPeakPercent", "kind": "energy"},
	{"label": "Playback Ref", "raw": "playbackReferenceRms", "percent": "playbackReferencePercent", "kind": "energy"},
	{"label": "Playback Energy", "raw": "playbackEnergy", "percent": "", "kind": "energy"},
	{"label": "Expected Echo", "raw": "estimatedEchoRms", "percent": "expectedEchoPercent", "kind": "energy"},
	{"label": "Mic/Echo Ratio", "raw": "micToExpectedEchoRatio", "percent": "", "kind": "ratio"},
	{"label": "VAD Confidence", "raw": "vadConfidence", "percent": "vadPercent", "kind": "unit"},
	{"label": "Correlation", "raw": "correlationScore", "percent": "correlationPercent", "kind": "unit"},
	{"label": "User Dominance", "raw": "userDominanceScore", "percent": "userDominancePercent", "kind": "unit"},
	{"label": "Captured Window Corr", "raw": "capturedWindowBestCorrelation", "percent": "", "kind": "unit"},
]

const FIELDS := [
	["Assistant Speaking", "assistantWasSpeaking"],
	["Capture Source", "captureSource"],
	["Barge-In State", "bargeInState"],
	["Self-Speech Gate", "selfSpeechGateDecision"],
	["Gate Reason", "selfSpeechGateReason"],
	["Captured Window", "capturedWindowSelfPlaybackDecision"],
	["STT Source", "sttAudioSource"],
	["AEC Processed", "sttAudioIsAecProcessed"],
	["Final Decision", "finalBargeInDecision"],
	["Best Delay", "bestCorrelationDelayMs"],
	["VAD Speech", "vadIsSpeech"],
]

var _bar_controls := {}
var _field_labels := {}


func _ready() -> void:
	name = "BargeInDebugOverlay"
	visible = false
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	custom_minimum_size = Vector2(380, 0)
	anchor_left = 0.0
	anchor_top = 0.0
	anchor_right = 0.0
	anchor_bottom = 1.0
	offset_left = 14.0
	offset_top = 92.0
	offset_right = 394.0
	offset_bottom = -18.0
	add_theme_stylebox_override("panel", _panel_style())

	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 12)
	margin.add_theme_constant_override("margin_top", 10)
	margin.add_theme_constant_override("margin_right", 12)
	margin.add_theme_constant_override("margin_bottom", 10)
	add_child(margin)

	var scroll := ScrollContainer.new()
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	margin.add_child(scroll)

	var stack := VBoxContainer.new()
	stack.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	stack.add_theme_constant_override("separation", 7)
	scroll.add_child(stack)

	var title := Label.new()
	title.text = "Barge-In Debug"
	title.add_theme_font_size_override("font_size", 15)
	title.add_theme_color_override("font_color", Color(0.86, 0.96, 1.0, 1.0))
	stack.add_child(title)

	for row in ROWS:
		_add_bar_row(stack, row)

	var divider := HSeparator.new()
	stack.add_child(divider)

	for field in FIELDS:
		_add_field_row(stack, String(field[0]), String(field[1]))


func update_snapshot(packet: Dictionary) -> void:
	var snapshot = packet
	if packet.has("snapshot") and typeof(packet.get("snapshot")) == TYPE_DICTIONARY:
		snapshot = packet.get("snapshot")

	visible = true
	for row in ROWS:
		_update_bar_row(snapshot, row)
	for field in FIELDS:
		_update_field_row(snapshot, String(field[0]), String(field[1]))


func _add_bar_row(parent: VBoxContainer, row: Dictionary) -> void:
	var line := HBoxContainer.new()
	line.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	line.add_theme_constant_override("separation", 8)
	parent.add_child(line)

	var label := Label.new()
	label.custom_minimum_size = Vector2(112, 0)
	label.text = String(row.get("label", ""))
	label.add_theme_font_size_override("font_size", 12)
	label.add_theme_color_override("font_color", Color(0.64, 0.78, 0.86, 1.0))
	line.add_child(label)

	var bar := ProgressBar.new()
	bar.min_value = 0.0
	bar.max_value = 100.0
	bar.value = 0.0
	bar.show_percentage = false
	bar.custom_minimum_size = Vector2(122, 10)
	bar.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar.add_theme_stylebox_override("background", _bar_background_style())
	bar.add_theme_stylebox_override("fill", _bar_fill_style())
	line.add_child(bar)

	var value := Label.new()
	value.custom_minimum_size = Vector2(105, 0)
	value.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	value.text = "n/a"
	value.add_theme_font_size_override("font_size", 12)
	value.add_theme_color_override("font_color", Color(0.88, 0.96, 1.0, 1.0))
	line.add_child(value)

	_bar_controls[String(row.get("raw", ""))] = {
		"bar": bar,
		"value": value,
	}


func _add_field_row(parent: VBoxContainer, label_text: String, key: String) -> void:
	var line := HBoxContainer.new()
	line.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	line.add_theme_constant_override("separation", 8)
	parent.add_child(line)

	var label := Label.new()
	label.custom_minimum_size = Vector2(126, 0)
	label.text = label_text
	label.add_theme_font_size_override("font_size", 11)
	label.add_theme_color_override("font_color", Color(0.50, 0.64, 0.72, 1.0))
	line.add_child(label)

	var value := Label.new()
	value.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	value.clip_text = true
	value.text = "n/a"
	value.add_theme_font_size_override("font_size", 11)
	value.add_theme_color_override("font_color", Color(0.92, 0.96, 1.0, 1.0))
	line.add_child(value)
	_field_labels[key] = value


func _update_bar_row(snapshot: Dictionary, row: Dictionary) -> void:
	var raw_key := String(row.get("raw", ""))
	var controls = _bar_controls.get(raw_key, null)
	if controls == null:
		return
	var raw_value = snapshot.get(raw_key, null)
	var percent_key := String(row.get("percent", ""))
	var percent_value = snapshot.get(percent_key, null) if not percent_key.is_empty() else null
	var kind := String(row.get("kind", ""))
	var bar: ProgressBar = controls["bar"]
	var label: Label = controls["value"]

	if raw_value == null:
		bar.value = 0.0
		label.text = "n/a"
		return

	var raw_float := float(raw_value)
	var bar_percent := _bar_percent(raw_float, percent_value, kind)
	bar.value = bar_percent
	label.text = _format_bar_value(raw_float, percent_value, kind)


func _update_field_row(snapshot: Dictionary, _label_text: String, key: String) -> void:
	if not _field_labels.has(key):
		return
	var label: Label = _field_labels[key]
	var value = snapshot.get(key, null)
	if value == null:
		label.text = "n/a"
		return
	if key.ends_with("DelayMs"):
		label.text = "%.1f ms" % float(value)
	else:
		label.text = str(value)


func _bar_percent(raw_value: float, percent_value, kind: String) -> float:
	if percent_value != null:
		return clampf(float(percent_value), 0.0, 100.0)
	match kind:
		"ratio":
			return clampf(raw_value / 8.0, 0.0, 1.0) * 100.0
		"unit":
			return clampf(raw_value, 0.0, 1.0) * 100.0
		_:
			return clampf(raw_value / 0.10, 0.0, 1.0) * 100.0


func _format_bar_value(raw_value: float, percent_value, kind: String) -> String:
	match kind:
		"ratio":
			return "%.2fx" % raw_value
		"unit":
			if percent_value != null:
				return "%.3f / %.1f%%" % [raw_value, float(percent_value)]
			return "%.3f" % raw_value
		_:
			if percent_value != null:
				return "%.4f / %.1f%%" % [raw_value, float(percent_value)]
			return "%.4f" % raw_value


func _panel_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.003, 0.018, 0.040, 0.88)
	style.border_color = Color(0.24, 0.72, 1.0, 0.58)
	style.set_border_width_all(1)
	style.set_corner_radius_all(6)
	style.shadow_color = Color(0, 0, 0, 0.32)
	style.shadow_size = 12
	return style


func _bar_background_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.04, 0.08, 0.12, 0.94)
	style.set_corner_radius_all(3)
	return style


func _bar_fill_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.18, 0.78, 0.92, 0.92)
	style.set_corner_radius_all(3)
	return style
