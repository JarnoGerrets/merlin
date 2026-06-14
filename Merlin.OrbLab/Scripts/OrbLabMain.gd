extends Control

const AUTO_TICK_INTERVAL := 0.11
const STATE_FEATURE_ORDER := [
	"breathing",
	"rotation",
	"pulses",
	"pulse_connections",
	"speech_motion",
	"node_motion",
	"dust_motion",
	"cluster_halos",
	"core_glow",
]
const ORB_SLOT_COUNT := 5
const ORB_SLOT_FILE := "res://../Merlin.OrbLabSlots.json"

@onready var core_orb = $CoreOrb
@onready var state_row: HBoxContainer = $Controls/ControlMargin/ControlRows/StateRow
@onready var speech_row: HBoxContainer = $Controls/ControlMargin/ControlRows/SpeechRow
@onready var parameter_controls: PanelContainer = $ParameterControls
@onready var rotation_controls: PanelContainer = $RotationControls
@onready var rotation_rows: VBoxContainer = $RotationControls/RotationMargin/RotationRows
@onready var export_button: Button = $ParameterControls/ParameterMargin/ParameterRows/ExportButton
@onready var parameter_tabs: TabContainer = $ParameterControls/ParameterMargin/ParameterRows/Tabs
@onready var status_label: Label = $Status

var _auto_speech := false
var _auto_tick_timer := 0.0
var _speech_progress := 0.0
var _energy_slider: HSlider
var _energy_value_label: Label
var _auto_button: Button
var _rotation_enabled_toggle: CheckBox
var _yaw_slider: HSlider
var _pitch_slider: HSlider
var _yaw_value_label: Label
var _pitch_value_label: Label
var _parameter_inputs := {}
var _parameter_defaults := {}
var _parameter_types := {}
var _parameter_categories := {}
var _parameter_rebuild_flags := {}
var _state_edit_enabled := false
var _selected_edit_state := "IDLE"
var _state_edit_left_panel: PanelContainer
var _state_edit_right_panel: PanelContainer
var _state_edit_state_rows: VBoxContainer
var _state_edit_feature_rows: VBoxContainer
var _state_edit_title: Label
var _state_feature_toggles := {}
var _builder_enabled := false
var _builder_left_panel: PanelContainer
var _builder_right_panel: PanelContainer
var _builder_slot_rows: VBoxContainer
var _builder_slot_name_inputs: Array[LineEdit] = []
var _builder_tool := "cluster"
var _builder_add_mode := true
var _cluster_edit_enabled := false
var _selected_cluster_id := -1
var _cluster_edit_left_panel: PanelContainer
var _cluster_edit_right_panel: PanelContainer
var _cluster_list_rows: VBoxContainer
var _cluster_edit_title: Label
var _cluster_halo_scale_input: LineEdit
var _cluster_brightness_scale_input: LineEdit
var _selected_connection_id := -1
var _connection_width_scale_input: LineEdit
var _connection_alpha_scale_input: LineEdit
var _connection_glow_scale_input: LineEdit
var _connection_hot_mix_input: LineEdit
var _connection_route_toggle: CheckBox
var _orb_dragging := false
var _orb_drag_distance := 0.0
var _mouse_yaw := 0.0
var _mouse_pitch := 0.0
var _mouse_zoom := 1.0


func _ready() -> void:
	_build_state_buttons()
	_build_speech_controls()
	_build_rotation_controls()
	_build_builder_panels()
	_build_state_edit_panels()
	_build_cluster_edit_panels()
	if export_button != null:
		export_button.pressed.connect(_export_settings_markdown)
	_build_parameter_controls()
	_set_status("Idle")
	core_orb.set_idle()


func _process(delta: float) -> void:
	if not _auto_speech:
		return
	_auto_tick_timer -= delta
	if _auto_tick_timer > 0.0:
		return
	_auto_tick_timer = AUTO_TICK_INTERVAL
	_send_speech_tick()


func _input(event: InputEvent) -> void:
	if core_orb == null:
		return
	if event is InputEventMouseButton:
		var mouse_button := event as InputEventMouseButton
		if mouse_button.button_index == MOUSE_BUTTON_LEFT and not mouse_button.pressed and _orb_dragging:
			if _orb_drag_distance < 6.0 and _mouse_over_orb(mouse_button.position):
				_pick_orb_item(mouse_button.position)
			_orb_dragging = false
			get_viewport().set_input_as_handled()
			return
		if _mouse_over_editor_ui(mouse_button.position):
			return
		if not _mouse_over_orb(mouse_button.position):
			return
		if mouse_button.button_index == MOUSE_BUTTON_WHEEL_UP and mouse_button.pressed:
			_apply_mouse_zoom(_mouse_zoom * 1.08)
			get_viewport().set_input_as_handled()
		elif mouse_button.button_index == MOUSE_BUTTON_WHEEL_DOWN and mouse_button.pressed:
			_apply_mouse_zoom(_mouse_zoom / 1.08)
			get_viewport().set_input_as_handled()
		elif mouse_button.button_index == MOUSE_BUTTON_LEFT:
			if mouse_button.pressed:
				_orb_dragging = true
				_orb_drag_distance = 0.0
				get_viewport().set_input_as_handled()
			else:
				if _orb_dragging and _orb_drag_distance < 6.0:
					_pick_orb_item(mouse_button.position)
				_orb_dragging = false
				get_viewport().set_input_as_handled()
	elif event is InputEventMouseMotion and _orb_dragging:
		var motion := event as InputEventMouseMotion
		_orb_drag_distance += motion.relative.length()
		if motion.relative.length() > 1.0:
			_mouse_yaw += motion.relative.x * 0.006
			_mouse_pitch = clampf(_mouse_pitch + motion.relative.y * 0.004, -0.95, 0.95)
			core_orb.set_manual_rotation_enabled(true)
			core_orb.set_manual_rotation(_mouse_yaw, _mouse_pitch)
			_sync_rotation_sliders()
		get_viewport().set_input_as_handled()


func _build_state_buttons() -> void:
	_add_state_button("Idle", func() -> void:
		_auto_speech = false
		_update_auto_button()
		core_orb.set_idle()
		_set_status("Idle")
	)
	_add_state_button("Listening", func() -> void:
		_auto_speech = false
		_update_auto_button()
		core_orb.set_listening()
		_set_status("Listening")
	)
	_add_state_button("Thinking", func() -> void:
		_auto_speech = false
		_update_auto_button()
		core_orb.set_thinking()
		_set_status("Thinking")
	)
	_add_state_button("Speaking", func() -> void:
		core_orb.set_speaking()
		_set_status("Speaking")
	)
	_add_state_button("Confirm", func() -> void:
		_auto_speech = false
		_update_auto_button()
		core_orb.play_confirmation()
		_set_status("Confirmation")
	)
	_add_state_button("Error", func() -> void:
		_auto_speech = false
		_update_auto_button()
		core_orb.play_error()
		_set_status("Error")
	)
	_add_state_button("Birth / Death", func() -> void:
		core_orb.trigger_cluster_birth_death()
		_set_status("Cluster birth/death")
	)
	_add_state_button("Edit States", func() -> void:
		_set_state_edit_enabled(not _state_edit_enabled)
	)
	_add_state_button("Orb Builder", func() -> void:
		_set_builder_enabled(not _builder_enabled)
	)
	_add_state_button("Edit Orb", func() -> void:
		_set_cluster_edit_enabled(not _cluster_edit_enabled)
	)


func _build_speech_controls() -> void:
	var energy_label := Label.new()
	energy_label.text = "Energy"
	energy_label.custom_minimum_size = Vector2(58.0, 0.0)
	speech_row.add_child(energy_label)

	_energy_slider = HSlider.new()
	_energy_slider.min_value = 0.0
	_energy_slider.max_value = 1.0
	_energy_slider.step = 0.01
	_energy_slider.value = 0.45
	_energy_slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_energy_slider.value_changed.connect(func(_value: float) -> void:
		_apply_energy_slider()
	)
	speech_row.add_child(_energy_slider)

	_energy_value_label = Label.new()
	_energy_value_label.custom_minimum_size = Vector2(48.0, 0.0)
	_energy_value_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	speech_row.add_child(_energy_value_label)

	var tick_button := Button.new()
	tick_button.text = "Speech Tick"
	tick_button.pressed.connect(func() -> void:
		core_orb.set_speaking()
		_send_speech_tick()
		_set_status("Speaking tick %.2f" % _energy_slider.value)
	)
	speech_row.add_child(tick_button)

	_auto_button = Button.new()
	_auto_button.toggle_mode = true
	_auto_button.text = "Auto Speech"
	_auto_button.pressed.connect(func() -> void:
		_auto_speech = _auto_button.button_pressed
		_auto_tick_timer = 0.0
		if _auto_speech:
			core_orb.set_speaking()
			_set_status("Speaking auto")
		else:
			_set_status("Speaking paused")
	)
	speech_row.add_child(_auto_button)
	_apply_energy_slider()


func _build_rotation_controls() -> void:
	var title := Label.new()
	title.text = "Manual Rotation"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	rotation_rows.add_child(title)

	_rotation_enabled_toggle = CheckBox.new()
	_rotation_enabled_toggle.text = "Stop auto rotation"
	_rotation_enabled_toggle.toggled.connect(func(_enabled: bool) -> void:
		_apply_manual_rotation()
	)
	rotation_rows.add_child(_rotation_enabled_toggle)

	_yaw_slider = _add_rotation_slider("Left / Right", -180.0, 180.0, 0.0, func(_value: float) -> void:
		_apply_manual_rotation()
	)
	_yaw_value_label = _yaw_slider.get_meta("value_label") as Label

	_pitch_slider = _add_rotation_slider("Up / Down", -20.0, 20.0, 0.0, func(_value: float) -> void:
		_apply_manual_rotation()
	)
	_pitch_value_label = _pitch_slider.get_meta("value_label") as Label
	_apply_manual_rotation()


func _add_rotation_slider(label: String, min_value: float, max_value: float, value: float, callback: Callable) -> HSlider:
	var row := HBoxContainer.new()
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 8)
	rotation_rows.add_child(row)

	var name_label := Label.new()
	name_label.text = label
	name_label.custom_minimum_size = Vector2(84.0, 0.0)
	row.add_child(name_label)

	var slider := HSlider.new()
	slider.min_value = min_value
	slider.max_value = max_value
	slider.step = 1.0
	slider.value = value
	slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	slider.value_changed.connect(callback)
	row.add_child(slider)

	var value_label := Label.new()
	value_label.custom_minimum_size = Vector2(52.0, 0.0)
	value_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	row.add_child(value_label)
	slider.set_meta("value_label", value_label)
	return slider


func _build_parameter_controls() -> void:
	if parameter_tabs == null or core_orb == null:
		return
	_parameter_inputs.clear()
	_parameter_defaults.clear()
	_parameter_types.clear()
	_parameter_categories.clear()
	_parameter_rebuild_flags.clear()
	for child in parameter_tabs.get_children():
		child.queue_free()
	var parameters: Array = core_orb.get_orb_lab_parameters()
	var category_rows := {}
	for parameter in parameters:
		var category := String(parameter.get("category", "General"))
		if not category_rows.has(category):
			category_rows[category] = _add_parameter_tab(category)
		_add_parameter_row(parameter, category_rows[category] as VBoxContainer)


func _add_parameter_tab(category: String) -> VBoxContainer:
	var scroll := ScrollContainer.new()
	scroll.name = category
	scroll.horizontal_scroll_mode = 0
	scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	parameter_tabs.add_child(scroll)

	var rows := VBoxContainer.new()
	rows.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	rows.add_theme_constant_override("separation", 8)
	scroll.add_child(rows)
	return rows


func _add_parameter_row(parameter: Dictionary, rows: VBoxContainer) -> void:
	var parameter_name := String(parameter["name"])
	var parameter_type := String(parameter.get("type", "float"))
	var requires_rebuild := bool(parameter.get("requires_rebuild", false))
	var row := HBoxContainer.new()
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 8)
	rows.add_child(row)

	var name_label := Label.new()
	name_label.text = "%s%s" % [parameter_name, " *" if requires_rebuild else ""]
	name_label.tooltip_text = "Default: %s" % _format_parameter_variant(parameter["default"], parameter_type)
	if requires_rebuild:
		name_label.tooltip_text += "\nApplies by rebuilding the generated orb."
	name_label.custom_minimum_size = Vector2(132.0, 0.0)
	row.add_child(name_label)

	var input: Control
	if parameter_type == "color":
		var color_picker := ColorPickerButton.new()
		color_picker.color = parameter["value"]
		color_picker.custom_minimum_size = Vector2(58.0, 32.0)
		input = color_picker
	elif parameter_type == "bool":
		var checkbox := CheckBox.new()
		checkbox.button_pressed = bool(parameter["value"])
		checkbox.custom_minimum_size = Vector2(58.0, 32.0)
		input = checkbox
	else:
		var line_edit := LineEdit.new()
		line_edit.text = _format_parameter_variant(parameter["value"], parameter_type)
		line_edit.custom_minimum_size = Vector2(58.0, 0.0)
		line_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		line_edit.text_submitted.connect(func(_text: String) -> void:
			_apply_parameter(parameter_name)
		)
		input = line_edit
	row.add_child(input)
	_parameter_inputs[parameter_name] = input
	_parameter_defaults[parameter_name] = parameter["default"]
	_parameter_types[parameter_name] = parameter_type
	_parameter_categories[parameter_name] = String(parameter.get("category", "General"))
	_parameter_rebuild_flags[parameter_name] = requires_rebuild

	var apply_button := Button.new()
	apply_button.text = "Apply"
	apply_button.custom_minimum_size = Vector2(50.0, 32.0)
	apply_button.pressed.connect(func() -> void:
		_apply_parameter(parameter_name)
	)
	row.add_child(apply_button)

	var reset_button := Button.new()
	reset_button.text = "Reset"
	reset_button.custom_minimum_size = Vector2(50.0, 32.0)
	reset_button.pressed.connect(func() -> void:
		_reset_parameter(parameter_name)
	)
	row.add_child(reset_button)


func _add_state_button(label: String, callback: Callable) -> void:
	var button := Button.new()
	button.text = label
	button.custom_minimum_size = Vector2(112.0, 36.0)
	button.pressed.connect(callback)
	state_row.add_child(button)


func _build_state_edit_panels() -> void:
	_state_edit_left_panel = _make_floating_panel("StateEditLeft", 24.0, -250.0, 286.0, 250.0)
	add_child(_state_edit_left_panel)
	var left_rows := _panel_rows(_state_edit_left_panel)
	var left_title := Label.new()
	left_title.text = "State Editor"
	left_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	left_rows.add_child(left_title)

	_state_edit_state_rows = VBoxContainer.new()
	_state_edit_state_rows.add_theme_constant_override("separation", 6)
	left_rows.add_child(_state_edit_state_rows)
	for state_name in ["IDLE", "LISTENING", "THINKING", "SPEAKING", "EXECUTING", "CONFIRMATION", "ERROR"]:
		var captured_state: String = String(state_name)
		var state_button := Button.new()
		state_button.text = captured_state
		state_button.custom_minimum_size = Vector2(0.0, 34.0)
		state_button.pressed.connect(func() -> void:
			_select_state_edit_state(captured_state)
		)
		_state_edit_state_rows.add_child(state_button)

	var reset_state_button := Button.new()
	reset_state_button.text = "Reset Selected"
	reset_state_button.pressed.connect(func() -> void:
		if core_orb.reset_orb_lab_state_features(_selected_edit_state):
			_refresh_state_feature_controls()
			_set_status("Reset %s state features" % _selected_edit_state)
	)
	left_rows.add_child(reset_state_button)

	var reset_all_button := Button.new()
	reset_all_button.text = "Reset All States"
	reset_all_button.pressed.connect(func() -> void:
		if core_orb.reset_orb_lab_state_features(""):
			_refresh_state_feature_controls()
			_set_status("Reset all state features")
	)
	left_rows.add_child(reset_all_button)

	var close_button := Button.new()
	close_button.text = "Back To Params"
	close_button.pressed.connect(func() -> void:
		_set_state_edit_enabled(false)
	)
	left_rows.add_child(close_button)

	_state_edit_right_panel = _make_floating_panel("StateEditRight", -336.0, -250.0, -24.0, 250.0, true)
	add_child(_state_edit_right_panel)
	var right_rows := _panel_rows(_state_edit_right_panel)
	_state_edit_title = Label.new()
	_state_edit_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	right_rows.add_child(_state_edit_title)

	_state_edit_feature_rows = VBoxContainer.new()
	_state_edit_feature_rows.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_state_edit_feature_rows.add_theme_constant_override("separation", 8)
	right_rows.add_child(_state_edit_feature_rows)
	_refresh_state_feature_controls()
	_set_state_edit_enabled(false)


func _make_floating_panel(node_name: String, left: float, top: float, right: float, bottom: float, right_anchor: bool = false) -> PanelContainer:
	var panel := PanelContainer.new()
	panel.name = node_name
	panel.layout_mode = 1
	panel.anchor_top = 0.5
	panel.anchor_bottom = 0.5
	if right_anchor:
		panel.anchor_left = 1.0
		panel.anchor_right = 1.0
	else:
		panel.anchor_left = 0.0
		panel.anchor_right = 0.0
	panel.offset_left = left
	panel.offset_top = top
	panel.offset_right = right
	panel.offset_bottom = bottom
	panel.grow_vertical = Control.GROW_DIRECTION_BOTH
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	return panel


func _panel_rows(panel: PanelContainer) -> VBoxContainer:
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 14)
	margin.add_theme_constant_override("margin_top", 12)
	margin.add_theme_constant_override("margin_right", 14)
	margin.add_theme_constant_override("margin_bottom", 12)
	panel.add_child(margin)
	var rows := VBoxContainer.new()
	rows.add_theme_constant_override("separation", 10)
	margin.add_child(rows)
	return rows


func _set_state_edit_enabled(enabled: bool) -> void:
	_state_edit_enabled = enabled
	if enabled:
		_builder_enabled = false
		_cluster_edit_enabled = false
	if parameter_controls != null:
		parameter_controls.visible = not enabled
	if rotation_controls != null:
		rotation_controls.visible = not enabled
	if _state_edit_left_panel != null:
		_state_edit_left_panel.visible = enabled
	if _state_edit_right_panel != null:
		_state_edit_right_panel.visible = enabled
	if _builder_left_panel != null:
		_builder_left_panel.visible = false
	if _builder_right_panel != null:
		_builder_right_panel.visible = false
	if _cluster_edit_left_panel != null:
		_cluster_edit_left_panel.visible = false
	if _cluster_edit_right_panel != null:
		_cluster_edit_right_panel.visible = false
	_refresh_state_feature_controls()
	var status := "Parameters"
	if enabled:
		status = "State edit: %s" % _selected_edit_state
	_set_status(status)


func _select_state_edit_state(state_name: String) -> void:
	_selected_edit_state = state_name
	_preview_state(state_name)
	_refresh_state_feature_controls()
	_set_status("Editing %s" % state_name)


func _preview_state(state_name: String) -> void:
	match state_name:
		"IDLE":
			core_orb.set_idle()
		"LISTENING":
			core_orb.set_listening()
		"THINKING":
			core_orb.set_thinking()
		"SPEAKING":
			core_orb.set_speaking()
		"EXECUTING":
			core_orb.play_tool_execution()
		"CONFIRMATION":
			core_orb.play_confirmation()
		"ERROR":
			core_orb.play_error()


func _refresh_state_feature_controls() -> void:
	if _state_edit_feature_rows == null or core_orb == null:
		return
	_state_feature_toggles.clear()
	for child in _state_edit_feature_rows.get_children():
		child.queue_free()
	var states: Dictionary = core_orb.get_orb_lab_state_features()
	var labels: Dictionary = core_orb.get_orb_lab_state_feature_labels()
	var selected_features: Dictionary = states.get(_selected_edit_state, {})
	if _state_edit_title != null:
		_state_edit_title.text = "%s Features" % _selected_edit_state
	for feature_name in STATE_FEATURE_ORDER:
		var captured_feature: String = String(feature_name)
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 8)
		_state_edit_feature_rows.add_child(row)
		var toggle := CheckBox.new()
		toggle.button_pressed = bool(selected_features.get(captured_feature, true))
		toggle.toggled.connect(func(enabled: bool) -> void:
			if core_orb.apply_orb_lab_state_feature(_selected_edit_state, captured_feature, enabled):
				_set_status("%s %s = %s" % [_selected_edit_state, captured_feature, "on" if enabled else "off"])
		)
		row.add_child(toggle)
		var label := Label.new()
		label.text = String(labels.get(captured_feature, captured_feature))
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(label)
		_state_feature_toggles[captured_feature] = toggle


func _build_builder_panels() -> void:
	_builder_left_panel = _make_floating_panel("OrbBuilderLeft", 24.0, -310.0, 336.0, 270.0)
	add_child(_builder_left_panel)
	var left_rows := _panel_rows(_builder_left_panel)
	var left_title := Label.new()
	left_title.text = "Orb Builder"
	left_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	left_rows.add_child(left_title)

	var hint := Label.new()
	hint.text = "Build from a tiny seed, then add layers. Save up to five named versions for comparison."
	hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	left_rows.add_child(hint)

	var tool_title := Label.new()
	tool_title.text = "Manual Tools"
	tool_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	left_rows.add_child(tool_title)

	_add_builder_button(left_rows, "Cluster Brush", func() -> void:
		_builder_tool = "cluster"
		_set_status("Builder tool: click to add/grow clusters")
	)
	_add_builder_button(left_rows, "Particle Brush", func() -> void:
		_builder_tool = "particle"
		_set_status("Builder tool: click to add body particles")
	)
	_add_builder_button(left_rows, "Highway Brush", func() -> void:
		_builder_tool = "highway"
		_set_status("Builder tool: click between nodes to add bundled connections")
	)
	var mode_toggle := CheckBox.new()
	mode_toggle.text = "Remove mode"
	mode_toggle.button_pressed = false
	mode_toggle.toggled.connect(func(enabled: bool) -> void:
		_builder_add_mode = not enabled
		_set_status("Builder mode: %s" % ("remove" if enabled else "add"))
	)
	left_rows.add_child(mode_toggle)

	var step_separator := HSeparator.new()
	left_rows.add_child(step_separator)

	_add_builder_button(left_rows, "1. Seed Particle", func() -> void:
		_apply_builder_recipe({
			"structural_node_count": 1,
			"hub_node_count": 0,
			"bright_cluster_count": 0,
			"structural_feature_cluster_count": 0,
			"ambient_dust_node_count": 0,
			"hub_cluster_particle_count": 0,
			"bright_cluster_particle_count": 0,
			"core_cluster_particle_count": 1,
			"max_connection_distance": 0.05,
			"hub_connection_distance": 0.05,
			"heavy_cluster_connection_chance": 0.0,
			"route_connection_chance": 0.0,
			"cluster_halo_intensity": 0.0,
		}, "Builder seed")
	)
	_add_builder_button(left_rows, "2. Add Core", func() -> void:
		_apply_builder_recipe({
			"structural_node_count": 40,
			"hub_node_count": 1,
			"core_cluster_particle_count": 220,
			"center_visual_size": 0.72,
			"core_radius_factor": 0.32,
			"core_particle_brightness_scale": 0.58,
			"max_connection_distance": 0.34,
			"hub_connection_distance": 0.48,
			"cluster_halo_intensity": 1.3,
		}, "Builder core")
	)
	_add_builder_button(left_rows, "3. Add Body", func() -> void:
		_apply_builder_recipe({
			"structural_node_count": 520,
			"hub_node_count": 8,
			"structural_feature_cluster_count": 4,
			"structural_core_fraction": 0.14,
			"structural_mid_fraction": 0.28,
			"structural_shell_probability": 0.44,
			"structural_fill_radius_scale": 1.02,
			"ambient_dust_node_count": 700,
			"hub_cluster_particle_count": 24,
			"core_cluster_particle_count": 520,
			"max_connection_distance": 0.56,
			"hub_connection_distance": 0.78,
		}, "Builder body")
	)
	_add_builder_button(left_rows, "4. Add Clusters", func() -> void:
		_apply_builder_recipe({
			"structural_node_count": 1050,
			"hub_node_count": 18,
			"bright_cluster_count": 24,
			"structural_feature_cluster_count": 8,
			"ambient_dust_node_count": 1250,
			"hub_cluster_particle_count": 42,
			"bright_cluster_particle_count": 42,
			"core_cluster_particle_count": 760,
			"cluster_halo_intensity": 2.8,
			"cluster_halo_radius_scale": 1.55,
			"natural_halo_min_score": 0.22,
		}, "Builder clusters")
	)
	_add_builder_button(left_rows, "5. Add Highways", func() -> void:
		_apply_builder_recipe({
			"structural_node_count": 1400,
			"hub_node_count": 24,
			"bright_cluster_count": 38,
			"ambient_dust_node_count": 1600,
			"hub_cluster_particle_count": 50,
			"bright_cluster_particle_count": 52,
			"max_connection_distance": 0.80,
			"hub_connection_distance": 1.05,
			"heavy_cluster_connection_chance": 0.08,
			"route_connection_chance": 0.13,
			"route_connection_alpha_scale": 1.12,
			"route_connection_width_scale": 1.10,
		}, "Builder highways")
	)
	_add_builder_button(left_rows, "6. Full Living Orb", func() -> void:
		_apply_builder_recipe({
			"structural_node_count": 1700,
			"hub_node_count": 30,
			"bright_cluster_count": 52,
			"structural_feature_cluster_count": 11,
			"ambient_dust_node_count": 1800,
			"hub_cluster_particle_count": 56,
			"bright_cluster_particle_count": 58,
			"core_cluster_particle_count": 1580,
			"max_connection_distance": 0.92,
			"hub_connection_distance": 1.20,
			"cluster_halo_intensity": 5.4,
			"cluster_halo_radius_scale": 2.05,
			"natural_halo_min_score": 0.16,
		}, "Builder full orb")
	)

	var separator := HSeparator.new()
	left_rows.add_child(separator)

	_add_builder_button(left_rows, "Randomize Seed", func() -> void:
		var seed_value: int = int(Time.get_unix_time_from_system()) % 99999999
		_apply_builder_recipe({ "generation_seed": seed_value }, "Random seed %s" % seed_value)
	)
	_add_builder_button(left_rows, "Back To Params", func() -> void:
		_set_builder_enabled(false)
	)

	_builder_right_panel = _make_floating_panel("OrbBuilderRight", -336.0, -310.0, -24.0, 270.0, true)
	add_child(_builder_right_panel)
	var right_rows := _panel_rows(_builder_right_panel)
	var right_title := Label.new()
	right_title.text = "Saved Orbs"
	right_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	right_rows.add_child(right_title)

	_builder_slot_rows = VBoxContainer.new()
	_builder_slot_rows.add_theme_constant_override("separation", 8)
	right_rows.add_child(_builder_slot_rows)
	_build_orb_slot_rows()

	_set_builder_enabled(false)


func _add_builder_button(rows: VBoxContainer, label: String, callback: Callable) -> void:
	var button := Button.new()
	button.text = label
	button.custom_minimum_size = Vector2(0.0, 34.0)
	button.pressed.connect(callback)
	rows.add_child(button)


func _build_orb_slot_rows() -> void:
	if _builder_slot_rows == null:
		return
	_builder_slot_name_inputs.clear()
	for child in _builder_slot_rows.get_children():
		child.queue_free()
	var saved_slots: Array = _read_orb_slots()
	for slot_index in range(ORB_SLOT_COUNT):
		var slot_data: Dictionary = {}
		if slot_index < saved_slots.size():
			slot_data = saved_slots[slot_index] as Dictionary
		var slot_panel := PanelContainer.new()
		_builder_slot_rows.add_child(slot_panel)
		var slot_rows := VBoxContainer.new()
		slot_rows.add_theme_constant_override("separation", 6)
		var margin := MarginContainer.new()
		margin.add_theme_constant_override("margin_left", 8)
		margin.add_theme_constant_override("margin_top", 8)
		margin.add_theme_constant_override("margin_right", 8)
		margin.add_theme_constant_override("margin_bottom", 8)
		slot_panel.add_child(margin)
		margin.add_child(slot_rows)

		var name_input := LineEdit.new()
		name_input.text = String(slot_data.get("name", "Orb %s" % (slot_index + 1)))
		name_input.placeholder_text = "Orb name"
		slot_rows.add_child(name_input)
		_builder_slot_name_inputs.append(name_input)

		var action_row := HBoxContainer.new()
		action_row.add_theme_constant_override("separation", 6)
		slot_rows.add_child(action_row)

		var captured_slot: int = slot_index
		var save_button := Button.new()
		save_button.text = "Save"
		save_button.pressed.connect(func() -> void:
			_save_orb_slot(captured_slot)
		)
		action_row.add_child(save_button)

		var load_button := Button.new()
		load_button.text = "Load"
		load_button.disabled = not bool(slot_data.get("has_data", false))
		load_button.pressed.connect(func() -> void:
			_load_orb_slot(captured_slot)
		)
		action_row.add_child(load_button)

		var compare_button := Button.new()
		compare_button.text = "Compare"
		compare_button.disabled = not bool(slot_data.get("has_data", false))
		compare_button.pressed.connect(func() -> void:
			_load_orb_slot(captured_slot)
			_set_builder_enabled(true)
		)
		action_row.add_child(compare_button)

		var stamp := Label.new()
		stamp.text = String(slot_data.get("saved_at", "empty"))
		stamp.modulate = Color(0.68, 0.78, 0.88, 0.78)
		slot_rows.add_child(stamp)


func _apply_builder_recipe(recipe: Dictionary, status_text: String) -> void:
	if core_orb == null:
		return
	for parameter_name_variant in recipe.keys():
		var parameter_name: String = String(parameter_name_variant)
		core_orb.apply_orb_lab_parameter(parameter_name, recipe[parameter_name_variant])
	_build_parameter_controls()
	_refresh_cluster_controls()
	_refresh_connection_controls()
	_set_status(status_text)


func _capture_orb_configuration(slot_name: String) -> Dictionary:
	var parameters := {}
	for parameter_variant in core_orb.get_orb_lab_parameters():
		var parameter: Dictionary = parameter_variant as Dictionary
		var parameter_name: String = String(parameter.get("name", ""))
		var parameter_type: String = String(parameter.get("type", "float"))
		parameters[parameter_name] = _json_safe_parameter_value(parameter.get("value"), parameter_type)
	return {
		"name": slot_name,
		"has_data": true,
		"saved_at": Time.get_datetime_string_from_system(),
		"parameters": parameters,
		"state_features": core_orb.get_orb_lab_state_features(),
		"connection_overrides": core_orb.get_orb_lab_connection_overrides(),
		"cluster_overrides": _capture_cluster_overrides(),
		"geometry": core_orb.get_orb_builder_snapshot(),
	}


func _capture_cluster_overrides() -> Array:
	var items: Array = []
	for cluster_variant in core_orb.get_orb_lab_clusters():
		var cluster: Dictionary = cluster_variant as Dictionary
		var halo_scale: float = float(cluster.get("halo_scale", 1.0))
		var brightness_scale: float = float(cluster.get("brightness_scale", 1.0))
		if not is_equal_approx(halo_scale, 1.0) or not is_equal_approx(brightness_scale, 1.0):
			items.append({
				"id": int(cluster.get("id", -1)),
				"halo_scale": halo_scale,
				"brightness_scale": brightness_scale,
			})
	return items


func _json_safe_parameter_value(value, parameter_type: String):
	if parameter_type == "color":
		var color: Color = value
		return color.to_html(false)
	if parameter_type == "bool":
		return bool(value)
	if parameter_type == "int":
		return int(value)
	return float(value)


func _restore_parameter_value(value, parameter_type: String):
	if parameter_type == "color":
		return Color("#%s" % String(value))
	if parameter_type == "bool":
		return bool(value)
	if parameter_type == "int":
		return int(value)
	return float(value)


func _save_orb_slot(slot_index: int) -> void:
	var slots: Array = _read_orb_slots()
	while slots.size() < ORB_SLOT_COUNT:
		slots.append({})
	var slot_name := "Orb %s" % (slot_index + 1)
	if slot_index < _builder_slot_name_inputs.size():
		slot_name = _builder_slot_name_inputs[slot_index].text.strip_edges()
		if slot_name.is_empty():
			slot_name = "Orb %s" % (slot_index + 1)
	slots[slot_index] = _capture_orb_configuration(slot_name)
	if _write_orb_slots(slots):
		_build_orb_slot_rows()
		_set_status("Saved %s" % slot_name)
	else:
		_set_status("Save failed")


func _load_orb_slot(slot_index: int) -> void:
	var slots: Array = _read_orb_slots()
	if slot_index < 0 or slot_index >= slots.size():
		_set_status("Slot %s is empty" % (slot_index + 1))
		return
	var slot_data: Dictionary = slots[slot_index] as Dictionary
	if not bool(slot_data.get("has_data", false)):
		_set_status("Slot %s is empty" % (slot_index + 1))
		return
	var parameter_types := {}
	for parameter_variant in core_orb.get_orb_lab_parameters():
		var parameter: Dictionary = parameter_variant as Dictionary
		parameter_types[String(parameter.get("name", ""))] = String(parameter.get("type", "float"))
	var parameters: Dictionary = slot_data.get("parameters", {})
	for parameter_name_variant in parameters.keys():
		var parameter_name: String = String(parameter_name_variant)
		var parameter_type: String = String(parameter_types.get(parameter_name, "float"))
		core_orb.apply_orb_lab_parameter(parameter_name, _restore_parameter_value(parameters[parameter_name_variant], parameter_type))
	var geometry: Dictionary = slot_data.get("geometry", {})
	if not geometry.is_empty():
		core_orb.apply_orb_builder_snapshot(geometry)
	var cluster_overrides: Array = slot_data.get("cluster_overrides", [])
	for cluster_variant in cluster_overrides:
		var cluster: Dictionary = cluster_variant as Dictionary
		var cluster_id: int = int(cluster.get("id", -1))
		core_orb.apply_orb_lab_cluster_parameter(cluster_id, "halo_scale", float(cluster.get("halo_scale", 1.0)))
		core_orb.apply_orb_lab_cluster_parameter(cluster_id, "brightness_scale", float(cluster.get("brightness_scale", 1.0)))
	var connection_overrides: Array = slot_data.get("connection_overrides", [])
	for connection_variant in connection_overrides:
		var connection: Dictionary = connection_variant as Dictionary
		var connection_id: int = int(connection.get("id", -1))
		core_orb.apply_orb_lab_connection_parameter(connection_id, "route", bool(connection.get("route", false)))
		core_orb.apply_orb_lab_connection_parameter(connection_id, "width_scale", float(connection.get("width_scale", 1.0)))
		core_orb.apply_orb_lab_connection_parameter(connection_id, "alpha_scale", float(connection.get("alpha_scale", 1.0)))
		core_orb.apply_orb_lab_connection_parameter(connection_id, "glow_scale", float(connection.get("glow_scale", 1.0)))
		core_orb.apply_orb_lab_connection_parameter(connection_id, "hot_mix", float(connection.get("hot_mix", 0.0)))
	var state_features: Dictionary = slot_data.get("state_features", {})
	for state_name_variant in state_features.keys():
		var state_name: String = String(state_name_variant)
		var features: Dictionary = state_features[state_name_variant] as Dictionary
		for feature_name_variant in features.keys():
			core_orb.apply_orb_lab_state_feature(state_name, String(feature_name_variant), bool(features[feature_name_variant]))
	_build_parameter_controls()
	_refresh_cluster_controls()
	_refresh_connection_controls()
	_refresh_state_feature_controls()
	_set_status("Loaded %s" % String(slot_data.get("name", "Orb %s" % (slot_index + 1))))


func _read_orb_slots() -> Array:
	var path: String = ProjectSettings.globalize_path(ORB_SLOT_FILE)
	if not FileAccess.file_exists(path):
		return []
	var text: String = FileAccess.get_file_as_string(path)
	if text.is_empty():
		return []
	var parsed = JSON.parse_string(text)
	if parsed is Array:
		return parsed as Array
	return []


func _write_orb_slots(slots: Array) -> bool:
	var path: String = ProjectSettings.globalize_path(ORB_SLOT_FILE)
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(JSON.stringify(slots, "\t"))
	file.close()
	return true


func _set_builder_enabled(enabled: bool) -> void:
	_builder_enabled = enabled
	if enabled:
		_state_edit_enabled = false
		_cluster_edit_enabled = false
	if parameter_controls != null:
		parameter_controls.visible = not enabled
	if rotation_controls != null:
		rotation_controls.visible = true
	if _builder_left_panel != null:
		_builder_left_panel.visible = enabled
	if _builder_right_panel != null:
		_builder_right_panel.visible = enabled
	if _state_edit_left_panel != null:
		_state_edit_left_panel.visible = false
	if _state_edit_right_panel != null:
		_state_edit_right_panel.visible = false
	if _cluster_edit_left_panel != null:
		_cluster_edit_left_panel.visible = false
	if _cluster_edit_right_panel != null:
		_cluster_edit_right_panel.visible = false
	_set_status("Orb Builder" if enabled else "Parameters")


func _build_cluster_edit_panels() -> void:
	_cluster_edit_left_panel = _make_floating_panel("ClusterEditLeft", 24.0, -310.0, 336.0, 270.0)
	add_child(_cluster_edit_left_panel)
	var left_rows := _panel_rows(_cluster_edit_left_panel)
	var left_title := Label.new()
	left_title.text = "Edit Orb"
	left_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	left_rows.add_child(left_title)

	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	left_rows.add_child(scroll)
	_cluster_list_rows = VBoxContainer.new()
	_cluster_list_rows.add_theme_constant_override("separation", 6)
	scroll.add_child(_cluster_list_rows)

	var refresh_button := Button.new()
	refresh_button.text = "Refresh List"
	refresh_button.pressed.connect(func() -> void:
		_refresh_cluster_controls()
	)
	left_rows.add_child(refresh_button)

	var close_button := Button.new()
	close_button.text = "Back To Params"
	close_button.pressed.connect(func() -> void:
		_set_cluster_edit_enabled(false)
	)
	left_rows.add_child(close_button)

	_cluster_edit_right_panel = _make_floating_panel("ClusterEditRight", -336.0, -310.0, -24.0, 270.0, true)
	add_child(_cluster_edit_right_panel)
	var right_rows := _panel_rows(_cluster_edit_right_panel)
	_cluster_edit_title = Label.new()
	_cluster_edit_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	right_rows.add_child(_cluster_edit_title)

	_cluster_halo_scale_input = _add_cluster_parameter_row(right_rows, "Halo Scale", "halo_scale")
	_cluster_brightness_scale_input = _add_cluster_parameter_row(right_rows, "Brightness", "brightness_scale")

	var reset_button := Button.new()
	reset_button.text = "Reset Cluster"
	reset_button.pressed.connect(func() -> void:
		if _selected_cluster_id >= 0 and core_orb.reset_orb_lab_cluster(_selected_cluster_id):
			_refresh_cluster_controls()
			_set_status("Reset cluster %s" % _selected_cluster_id)
	)
	right_rows.add_child(reset_button)

	var separator := HSeparator.new()
	right_rows.add_child(separator)

	var connection_title := Label.new()
	connection_title.text = "Selected Connection"
	connection_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	right_rows.add_child(connection_title)

	_connection_route_toggle = CheckBox.new()
	_connection_route_toggle.text = "Highway / route"
	_connection_route_toggle.toggled.connect(func(enabled: bool) -> void:
		_apply_connection_parameter("route", enabled)
	)
	right_rows.add_child(_connection_route_toggle)
	_connection_width_scale_input = _add_connection_parameter_row(right_rows, "Width", "width_scale")
	_connection_alpha_scale_input = _add_connection_parameter_row(right_rows, "Brightness", "alpha_scale")
	_connection_glow_scale_input = _add_connection_parameter_row(right_rows, "Glow", "glow_scale")
	_connection_hot_mix_input = _add_connection_parameter_row(right_rows, "Hot Mix", "hot_mix")

	var reset_connection_button := Button.new()
	reset_connection_button.text = "Reset Connection"
	reset_connection_button.pressed.connect(func() -> void:
		if _selected_connection_id >= 0 and core_orb.reset_orb_lab_connection(_selected_connection_id):
			_refresh_connection_controls()
			_set_status("Reset connection %s" % _selected_connection_id)
	)
	right_rows.add_child(reset_connection_button)

	_refresh_cluster_controls()
	_set_cluster_edit_enabled(false)


func _add_cluster_parameter_row(rows: VBoxContainer, label_text: String, parameter_name: String) -> LineEdit:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)
	rows.add_child(row)

	var label := Label.new()
	label.text = label_text
	label.custom_minimum_size = Vector2(104.0, 0.0)
	row.add_child(label)

	var input := LineEdit.new()
	input.text = "1.0000"
	input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	input.text_submitted.connect(func(_text: String) -> void:
		_apply_cluster_parameter(parameter_name)
	)
	row.add_child(input)

	var apply_button := Button.new()
	apply_button.text = "Apply"
	apply_button.pressed.connect(func() -> void:
		_apply_cluster_parameter(parameter_name)
	)
	row.add_child(apply_button)
	return input


func _add_connection_parameter_row(rows: VBoxContainer, label_text: String, parameter_name: String) -> LineEdit:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)
	rows.add_child(row)

	var label := Label.new()
	label.text = label_text
	label.custom_minimum_size = Vector2(104.0, 0.0)
	row.add_child(label)

	var input := LineEdit.new()
	input.text = "1.0000"
	input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	input.text_submitted.connect(func(_text: String) -> void:
		_apply_connection_parameter(parameter_name, input.text.to_float())
	)
	row.add_child(input)

	var apply_button := Button.new()
	apply_button.text = "Apply"
	apply_button.pressed.connect(func() -> void:
		_apply_connection_parameter(parameter_name, input.text.to_float())
	)
	row.add_child(apply_button)
	return input


func _set_cluster_edit_enabled(enabled: bool) -> void:
	_cluster_edit_enabled = enabled
	if enabled:
		_builder_enabled = false
		_state_edit_enabled = false
	if parameter_controls != null:
		parameter_controls.visible = not enabled
	if rotation_controls != null:
		rotation_controls.visible = true
	if _state_edit_left_panel != null:
		_state_edit_left_panel.visible = false
	if _state_edit_right_panel != null:
		_state_edit_right_panel.visible = false
	if _builder_left_panel != null:
		_builder_left_panel.visible = false
	if _builder_right_panel != null:
		_builder_right_panel.visible = false
	if _cluster_edit_left_panel != null:
		_cluster_edit_left_panel.visible = enabled
	if _cluster_edit_right_panel != null:
		_cluster_edit_right_panel.visible = enabled
	_refresh_cluster_controls()
	var status := "Parameters"
	if enabled:
		status = "Edit Orb: drag to rotate, click clusters/connections"
	_set_status(status)


func _select_cluster(cluster_id: int) -> void:
	_selected_cluster_id = cluster_id
	core_orb.select_orb_lab_cluster(cluster_id)
	_refresh_cluster_controls()
	_set_status("Selected cluster %s" % cluster_id)


func _refresh_cluster_controls() -> void:
	if core_orb == null or _cluster_list_rows == null:
		return
	for child in _cluster_list_rows.get_children():
		child.queue_free()
	var clusters: Array = core_orb.get_orb_lab_clusters()
	if _selected_cluster_id < 0 and not clusters.is_empty():
		var first_cluster: Dictionary = clusters[0] as Dictionary
		_selected_cluster_id = int(first_cluster.get("id", -1))
	var selected_cluster: Dictionary = {}
	for cluster_variant in clusters:
		var cluster: Dictionary = cluster_variant as Dictionary
		var cluster_id: int = int(cluster.get("id", -1))
		if cluster_id == _selected_cluster_id:
			selected_cluster = cluster
		var captured_cluster_id: int = cluster_id
		var button := Button.new()
		button.text = "%s  node:%s  dust:%s" % [String(cluster.get("label", "Cluster")), cluster_id, int(cluster.get("dust_count", 0))]
		button.custom_minimum_size = Vector2(0.0, 34.0)
		button.pressed.connect(func() -> void:
			_select_cluster(captured_cluster_id)
		)
		_cluster_list_rows.add_child(button)

	if selected_cluster.is_empty():
		_cluster_edit_title.text = "No Cluster Selected"
		_cluster_halo_scale_input.text = "1.0000"
		_cluster_brightness_scale_input.text = "1.0000"
		return
	_cluster_edit_title.text = "%s  node:%s" % [String(selected_cluster.get("label", "Cluster")), int(selected_cluster.get("id", -1))]
	_cluster_halo_scale_input.text = _format_parameter_value(float(selected_cluster.get("halo_scale", 1.0)))
	_cluster_brightness_scale_input.text = _format_parameter_value(float(selected_cluster.get("brightness_scale", 1.0)))


func _apply_cluster_parameter(parameter_name: String) -> void:
	if _selected_cluster_id < 0:
		return
	var input: LineEdit = _cluster_halo_scale_input if parameter_name == "halo_scale" else _cluster_brightness_scale_input
	var value: float = input.text.to_float()
	if core_orb.apply_orb_lab_cluster_parameter(_selected_cluster_id, parameter_name, value):
		core_orb.select_orb_lab_cluster(_selected_cluster_id)
		_refresh_cluster_controls()
		_set_status("Cluster %s %s = %.4f" % [_selected_cluster_id, parameter_name, value])


func _refresh_connection_controls() -> void:
	if core_orb == null or _connection_width_scale_input == null:
		return
	var selected_connection: Dictionary = {}
	var connections: Array = core_orb.get_orb_lab_connections()
	for connection_variant in connections:
		var connection: Dictionary = connection_variant as Dictionary
		var connection_id: int = int(connection.get("id", -1))
		if connection_id == _selected_connection_id:
			selected_connection = connection
			break
	if selected_connection.is_empty():
		_connection_route_toggle.button_pressed = false
		_connection_width_scale_input.text = "1.0000"
		_connection_alpha_scale_input.text = "1.0000"
		_connection_glow_scale_input.text = "1.0000"
		_connection_hot_mix_input.text = "0.0000"
		return
	_connection_route_toggle.set_pressed_no_signal(bool(selected_connection.get("route", false)))
	_connection_width_scale_input.text = _format_parameter_value(float(selected_connection.get("width_scale", 1.0)))
	_connection_alpha_scale_input.text = _format_parameter_value(float(selected_connection.get("alpha_scale", 1.0)))
	_connection_glow_scale_input.text = _format_parameter_value(float(selected_connection.get("glow_scale", 1.0)))
	_connection_hot_mix_input.text = _format_parameter_value(float(selected_connection.get("hot_mix", 0.0)))


func _apply_connection_parameter(parameter_name: String, value) -> void:
	if _selected_connection_id < 0:
		return
	if core_orb.apply_orb_lab_connection_parameter(_selected_connection_id, parameter_name, value):
		core_orb.select_orb_lab_connection(_selected_connection_id)
		_refresh_connection_controls()
		_set_status("Connection %s %s = %s" % [_selected_connection_id, parameter_name, str(value)])


func _mouse_over_orb(global_mouse_position: Vector2) -> bool:
	var local: Vector2 = core_orb.get_global_transform().affine_inverse() * global_mouse_position
	return Rect2(Vector2.ZERO, core_orb.size).has_point(local)


func _mouse_over_editor_ui(global_mouse_position: Vector2) -> bool:
	var controls: Array = [
		$Controls,
		parameter_controls,
		rotation_controls,
		_builder_left_panel,
		_builder_right_panel,
		_state_edit_left_panel,
		_state_edit_right_panel,
		_cluster_edit_left_panel,
		_cluster_edit_right_panel,
	]
	for control_variant in controls:
		var control: Control = control_variant as Control
		if control != null and control.visible:
			var local: Vector2 = control.get_global_transform().affine_inverse() * global_mouse_position
			if Rect2(Vector2.ZERO, control.size).has_point(local):
				return true
	return false


func _pick_orb_item(global_mouse_position: Vector2) -> void:
	var local: Vector2 = core_orb.get_global_transform().affine_inverse() * global_mouse_position
	if _builder_enabled:
		var tool_name := _builder_tool if _builder_add_mode else "%s_remove" % _builder_tool
		var result: Dictionary = core_orb.apply_orb_builder_tool(tool_name, local)
		if not result.is_empty():
			_refresh_cluster_controls()
			_refresh_connection_controls()
			_set_status("%s: %s" % [tool_name.capitalize(), String(result.get("message", "applied"))])
		return
	var pick: Dictionary = core_orb.pick_orb_lab_item(local)
	if pick.is_empty():
		return
	var pick_type := String(pick.get("type", ""))
	var pick_id := int(pick.get("id", -1))
	if pick_type == "cluster":
		_set_cluster_edit_enabled(true)
		_selected_cluster_id = pick_id
		_selected_connection_id = -1
		core_orb.select_orb_lab_cluster(pick_id)
		_refresh_cluster_controls()
		_refresh_connection_controls()
		_set_status("Picked %s" % String(pick.get("label", "cluster")))
	elif pick_type == "connection":
		_set_cluster_edit_enabled(true)
		_selected_connection_id = pick_id
		core_orb.select_orb_lab_connection(pick_id)
		_refresh_connection_controls()
		_set_status("Picked %s" % String(pick.get("label", "connection")))


func _apply_mouse_zoom(value: float) -> void:
	_mouse_zoom = clampf(value, 0.35, 3.0)
	core_orb.set_orb_lab_zoom(_mouse_zoom)
	_set_status("Zoom %.2f" % _mouse_zoom)


func _sync_rotation_sliders() -> void:
	if _rotation_enabled_toggle != null:
		_rotation_enabled_toggle.set_pressed_no_signal(true)
	if _yaw_slider != null:
		_yaw_slider.set_value_no_signal(rad_to_deg(_mouse_yaw))
	if _pitch_slider != null:
		_pitch_slider.set_value_no_signal(rad_to_deg(_mouse_pitch))
	if _yaw_value_label != null:
		_yaw_value_label.text = "%d deg" % int(rad_to_deg(_mouse_yaw))
	if _pitch_value_label != null:
		_pitch_value_label.text = "%d deg" % int(rad_to_deg(_mouse_pitch))


func _send_speech_tick() -> void:
	_speech_progress = fmod(_speech_progress + 0.071, 1.0)
	core_orb.set_speech_energy_override(float(_energy_slider.value))
	core_orb.notify_speech_tick("", 0.0, float(_energy_slider.value))


func _update_auto_button() -> void:
	if _auto_button != null:
		_auto_button.button_pressed = _auto_speech


func _set_status(value: String) -> void:
	status_label.text = "OrbLab - %s" % value


func _apply_energy_slider() -> void:
	if _energy_value_label != null:
		_energy_value_label.text = "%.2f" % _energy_slider.value
	if core_orb != null:
		core_orb.set_speech_energy_override(float(_energy_slider.value))


func _apply_manual_rotation() -> void:
	if _yaw_slider == null or _pitch_slider == null or _rotation_enabled_toggle == null:
		return
	var yaw_degrees := float(_yaw_slider.value)
	var pitch_degrees := float(_pitch_slider.value)
	if _yaw_value_label != null:
		_yaw_value_label.text = "%d deg" % int(yaw_degrees)
	if _pitch_value_label != null:
		_pitch_value_label.text = "%d deg" % int(pitch_degrees)
	if core_orb != null:
		core_orb.set_manual_rotation_enabled(_rotation_enabled_toggle.button_pressed)
		core_orb.set_manual_rotation(deg_to_rad(yaw_degrees), deg_to_rad(pitch_degrees))


func _apply_parameter(parameter_name: String) -> void:
	if core_orb == null or not _parameter_inputs.has(parameter_name):
		return
	var parameter_type := String(_parameter_types.get(parameter_name, "float"))
	var input := _parameter_inputs[parameter_name] as Control
	var value = _value_from_parameter_input(input, parameter_type)
	if core_orb.apply_orb_lab_parameter(parameter_name, value):
		var applied_value = _current_parameter_value(parameter_name, value)
		_set_parameter_input_value(input, parameter_type, applied_value)
		var rebuild_note := " (rebuilt)" if _parameter_requires_rebuild(parameter_name) else ""
		_set_status("Applied %s = %s%s" % [parameter_name, _format_parameter_variant(applied_value, parameter_type), rebuild_note])
	else:
		_set_status("Unknown parameter: %s" % parameter_name)


func _reset_parameter(parameter_name: String) -> void:
	if core_orb == null or not _parameter_inputs.has(parameter_name):
		return
	if core_orb.reset_orb_lab_parameter(parameter_name):
		var parameter_type := String(_parameter_types.get(parameter_name, "float"))
		var default_value = _parameter_defaults.get(parameter_name, 0.0)
		var input := _parameter_inputs[parameter_name] as Control
		_set_parameter_input_value(input, parameter_type, default_value)
		var rebuild_note := " (rebuilt)" if _parameter_requires_rebuild(parameter_name) else ""
		_set_status("Reset %s = %s%s" % [parameter_name, _format_parameter_variant(default_value, parameter_type), rebuild_note])


func _format_parameter_value(value: float) -> String:
	return "%.4f" % value


func _value_from_parameter_input(input: Control, parameter_type: String):
	if parameter_type == "color":
		return (input as ColorPickerButton).color
	if parameter_type == "bool":
		return (input as CheckBox).button_pressed
	if parameter_type == "int":
		return (input as LineEdit).text.to_int()
	return (input as LineEdit).text.to_float()


func _set_parameter_input_value(input: Control, parameter_type: String, value) -> void:
	if parameter_type == "color":
		(input as ColorPickerButton).color = value
	elif parameter_type == "bool":
		(input as CheckBox).button_pressed = bool(value)
	elif parameter_type == "int":
		(input as LineEdit).text = str(int(value))
	else:
		(input as LineEdit).text = _format_parameter_value(float(value))


func _format_parameter_variant(value, parameter_type: String) -> String:
	if parameter_type == "color":
		var color: Color = value
		return color.to_html(false)
	if parameter_type == "bool":
		return "on" if bool(value) else "off"
	if parameter_type == "int":
		return str(int(value))
	return _format_parameter_value(float(value))


func _parameter_requires_rebuild(parameter_name: String) -> bool:
	return bool(_parameter_rebuild_flags.get(parameter_name, false))


func _current_parameter_value(parameter_name: String, fallback):
	if core_orb == null:
		return fallback
	for parameter in core_orb.get_orb_lab_parameters():
		if String(parameter["name"]) == parameter_name:
			return parameter["value"]
	return fallback


func _export_settings_markdown() -> void:
	if core_orb == null:
		return
	var content := _build_settings_markdown()
	var root_path: String = ProjectSettings.globalize_path("res://../Merlin.OrbLabSettings.md")
	var path := _write_text_file(root_path, content)
	if path.is_empty():
		path = _write_text_file(ProjectSettings.globalize_path("res://Merlin.OrbLabSettings.md"), content)
	if path.is_empty():
		_set_status("Export failed")
	else:
		_set_status("Exported %s" % path)


func _build_settings_markdown() -> String:
	var parameters: Array = core_orb.get_orb_lab_parameters()
	var categories := {}
	var category_order: Array[String] = []
	for parameter in parameters:
		var category := String(parameter.get("category", "General"))
		if not categories.has(category):
			categories[category] = []
			category_order.append(category)
		categories[category].append(parameter)

	var lines: Array[String] = []
	lines.append("# Merlin OrbLab Settings")
	lines.append("")
	lines.append("Exported: `%s`" % Time.get_datetime_string_from_system())
	lines.append("")
	lines.append("Values marked with `requires_rebuild` rebuild the generated orb when applied.")
	lines.append("")
	for category in category_order:
		lines.append("## %s" % category)
		lines.append("")
		for parameter in categories[category]:
			var parameter_name := String(parameter["name"])
			var parameter_type := String(parameter.get("type", "float"))
			var value_text := _format_parameter_variant(parameter["value"], parameter_type)
			var rebuild_text := " requires_rebuild" if bool(parameter.get("requires_rebuild", false)) else ""
			lines.append("- `%s` = `%s`%s" % [parameter_name, value_text, rebuild_text])
		lines.append("")
	lines.append("## State Features")
	lines.append("")
	var state_features: Dictionary = core_orb.get_orb_lab_state_features()
	var feature_labels: Dictionary = core_orb.get_orb_lab_state_feature_labels()
	for state_name in ["IDLE", "LISTENING", "THINKING", "SPEAKING", "EXECUTING", "CONFIRMATION", "ERROR"]:
		lines.append("### %s" % state_name)
		lines.append("")
		var features: Dictionary = state_features.get(state_name, {})
		for feature_name in STATE_FEATURE_ORDER:
			var label := String(feature_labels.get(feature_name, feature_name))
			var enabled := bool(features.get(feature_name, true))
			lines.append("- `%s` (%s) = `%s`" % [feature_name, label, "on" if enabled else "off"])
		lines.append("")
	lines.append("## Cluster Overrides")
	lines.append("")
	var clusters: Array = core_orb.get_orb_lab_clusters()
	for cluster_variant in clusters:
		var cluster: Dictionary = cluster_variant as Dictionary
		var cluster_id: int = int(cluster.get("id", -1))
		var label: String = String(cluster.get("label", "Cluster"))
		lines.append("- `%s` node `%s`: `halo_scale=%.4f`, `brightness_scale=%.4f`, `dust_count=%s`" % [
			label,
			cluster_id,
			float(cluster.get("halo_scale", 1.0)),
			float(cluster.get("brightness_scale", 1.0)),
			int(cluster.get("dust_count", 0))
		])
	lines.append("")
	lines.append("## Connection Overrides")
	lines.append("")
	var connection_overrides: Array = core_orb.get_orb_lab_connection_overrides()
	if connection_overrides.is_empty():
		lines.append("- None")
	else:
		for connection_variant in connection_overrides:
			var connection: Dictionary = connection_variant as Dictionary
			lines.append("- `Connection %04d` nodes `%s -> %s`: `route=%s`, `width_scale=%.4f`, `alpha_scale=%.4f`, `glow_scale=%.4f`, `hot_mix=%.4f`" % [
				int(connection.get("id", -1)),
				int(connection.get("a", -1)),
				int(connection.get("b", -1)),
				"on" if bool(connection.get("route", false)) else "off",
				float(connection.get("width_scale", 1.0)),
				float(connection.get("alpha_scale", 1.0)),
				float(connection.get("glow_scale", 1.0)),
				float(connection.get("hot_mix", 0.0)),
			])
	lines.append("")
	return "\n".join(lines)


func _write_text_file(path: String, content: String) -> String:
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return ""
	file.store_string(content)
	file.close()
	return path
