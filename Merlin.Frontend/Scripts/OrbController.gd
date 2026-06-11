extends Node3D
class_name OrbController

enum OrbState { IDLE, THINKING, SPEAKING, EXECUTING, ERROR, CONFIRMATION }

const COLOR_CORE_PRIMARY := Color(0.00, 0.81, 1.00)
const COLOR_CORE_HOT := Color(0.20, 0.90, 1.00)
const COLOR_SHELL := Color(0.05, 0.35, 0.85)
const COLOR_RING_PRIMARY := Color(0.08, 0.50, 1.00)
const COLOR_RING_OUTER := Color(0.04, 0.30, 0.80)
const COLOR_AMBIENT_LIGHT := Color(0.00, 0.09, 0.16)
const COLOR_THINKING := Color(0.10, 0.50, 1.00)
const COLOR_EXECUTING := Color(1.00, 0.42, 0.00)
const COLOR_EXECUTING_CORE := Color(0.80, 0.27, 0.00)
const COLOR_ERROR := Color(0.85, 0.04, 0.04)
const COLOR_ERROR_SOFT := Color(0.60, 0.02, 0.02)
const COLOR_AMBER := Color(1.00, 0.50, 0.00)
const COLOR_AMBER_SOFT := Color(0.85, 0.38, 0.00)

const CORE_PARAMS := {
	OrbState.IDLE: [1.00, 0.75, 0.022, 0.00, 0.70, 0.010, -0.008, 0.006],
	OrbState.THINKING: [1.30, 1.05, 0.040, 0.025, 1.20, 0.014, -0.010, 0.007],
	OrbState.SPEAKING: [1.40, 1.28, 0.055, 0.240, 1.85, 0.008, -0.006, 0.004],
	OrbState.EXECUTING: [2.05, 1.55, 0.065, 0.080, 1.45, 0.018, -0.014, 0.010],
	OrbState.ERROR: [1.75, 0.65, 0.040, 0.020, 0.75, 0.006, -0.005, 0.004],
	OrbState.CONFIRMATION: [1.10, 0.70, 0.030, 0.00, 0.80, 0.006, -0.005, 0.004],
}

const LIGHT_PARAMS := {
	OrbState.IDLE: [COLOR_CORE_PRIMARY, 3.0],
	OrbState.THINKING: [COLOR_THINKING, 4.0],
	OrbState.SPEAKING: [COLOR_CORE_PRIMARY, 3.6],
	OrbState.EXECUTING: [COLOR_EXECUTING, 6.0],
	OrbState.ERROR: [COLOR_ERROR, 5.5],
	OrbState.CONFIRMATION: [COLOR_AMBER, 3.8],
}

const CORE_COLORS := {
	OrbState.IDLE: [COLOR_CORE_PRIMARY, COLOR_CORE_HOT],
	OrbState.THINKING: [COLOR_THINKING, COLOR_CORE_HOT],
	OrbState.SPEAKING: [COLOR_CORE_PRIMARY, COLOR_CORE_HOT],
	OrbState.EXECUTING: [COLOR_EXECUTING_CORE, COLOR_EXECUTING],
	OrbState.ERROR: [COLOR_ERROR, COLOR_ERROR_SOFT],
	OrbState.CONFIRMATION: [COLOR_AMBER_SOFT, COLOR_AMBER],
}

const SPEECH_PULSE_ADD := 0.24
const SPEECH_PULSE_MAX := 1.0
const SPEECH_PULSE_DECAY := 3.0
const SPEECH_PULSE_MAX_EXTRA := 0.28
const MAX_PULSE_INTENSITY := 2.5
const ERROR_HOLD_DURATION := 2.5
const CONFIRMATION_PULSE_PERIOD := 2.2
const RINGS_ENABLED := false
const RING_PATH_SEGMENTS_MIN := 192
const RING_TUBE_SEGMENTS_MIN := 32

@onready var camera: Camera3D = $Camera3D
@onready var world_environment: WorldEnvironment = $WorldEnvironment
@onready var core_sphere: MeshInstance3D = $CoreSphere
@onready var glass_shell: MeshInstance3D = $GlassShell
@onready var inner_glass_shell: MeshInstance3D = $InnerGlassShell
@onready var inner_system: Node3D = $InnerRingSystem
@onready var middle_system: Node3D = $MiddleRingSystem
@onready var outer_system: Node3D = $OuterRingSystem
@onready var orbit_system: Node3D = $OrbitRingSystem
@onready var segment_system: Node3D = $SegmentRingSystem
@onready var omni_light: OmniLight3D = $OmniLight3D
@onready var spot_light: SpotLight3D = $SpotLight3D
@onready var ambient_field: GPUParticles3D = $ParticleSystem/AmbientField
@onready var orbit_particles: GPUParticles3D = $ParticleSystem/OrbitParticles
@onready var pulse_burst: GPUParticles3D = $ParticleSystem/PulseBurst
@onready var ground_glow: MeshInstance3D = $GroundGlow
@onready var animation_controller: AnimationPlayer = $AnimationController

var current_state := OrbState.IDLE
var previous_state := OrbState.IDLE
var core_mat: ShaderMaterial
var ground_mat: ShaderMaterial
var _speech_pulse := 0.0
var _temporary_state_token := 0
var _target_core_pulse_intensity := 1.0
var _target_core_time_scale := 0.8
var _target_surface_displacement := 0.025
var _target_spike_strength := 0.0
var _target_surface_activity := 0.7
var _target_core_turn_speed := 0.010
var _target_shell_turn_speed := -0.008
var _target_inner_shell_turn_speed := 0.006
var _core_turn_speed := 0.010
var _shell_turn_speed := -0.008
var _inner_shell_turn_speed := 0.006
var _core_phase := 0.0
var _shell_phase := 0.0
var _inner_shell_phase := 0.0
var _target_light_energy := 3.0
var _target_light_color := COLOR_CORE_PRIMARY
var _target_core_color := COLOR_CORE_PRIMARY
var _target_plasma_color := COLOR_CORE_HOT
var _ring_depth_offset_index := 0
var _state_elapsed := 0.0
var _error_recover_state := OrbState.IDLE


func _ready() -> void:
	_configure_scene()
	_build_core()
	_build_glass()
	if RINGS_ENABLED:
		_build_rings()
		_build_segment_details()
	_build_particles()
	_build_ground_glow()
	_build_animation_placeholders()

	_transition_to(OrbState.IDLE, true)
	animation_controller.play("idle_breathe")
	set_process(true)


func _process(delta: float) -> void:
	_state_elapsed += delta
	_update_core_and_light(delta)
	_update_system_motion(delta)
	_update_speech_pulse(delta)


func set_idle() -> void:
	_transition_to(OrbState.IDLE)


func set_thinking() -> void:
	_transition_to(OrbState.THINKING)


func set_speaking() -> void:
	_transition_to(OrbState.SPEAKING)


func play_tool_execution(duration: float = 2.5) -> void:
	_temporary_state_token += 1
	var token := _temporary_state_token
	_transition_to(OrbState.EXECUTING)
	await get_tree().create_timer(duration).timeout
	if token == _temporary_state_token and current_state == OrbState.EXECUTING:
		_transition_to(previous_state)


func play_error(duration: float = 3.0) -> void:
	_temporary_state_token += 1
	var token := _temporary_state_token
	_error_recover_state = OrbState.IDLE if current_state == OrbState.ERROR else current_state
	_transition_to(OrbState.ERROR)
	await get_tree().create_timer(minf(duration, ERROR_HOLD_DURATION)).timeout
	if token == _temporary_state_token and current_state == OrbState.ERROR:
		_transition_to(_error_recover_state)


func play_confirmation() -> void:
	_transition_to(OrbState.CONFIRMATION)


func notify_speech_tick() -> void:
	if current_state != OrbState.SPEAKING:
		return

	_speech_pulse = clampf(_speech_pulse + SPEECH_PULSE_ADD, 0.0, SPEECH_PULSE_MAX)


func _configure_scene() -> void:
	position = Vector3.ZERO
	inner_system.position = Vector3.ZERO
	middle_system.position = Vector3.ZERO
	outer_system.position = Vector3.ZERO
	orbit_system.position = Vector3.ZERO
	segment_system.position = Vector3.ZERO
	inner_system.visible = RINGS_ENABLED
	middle_system.visible = RINGS_ENABLED
	outer_system.visible = RINGS_ENABLED
	orbit_system.visible = RINGS_ENABLED
	segment_system.visible = RINGS_ENABLED

	camera.position = Vector3(0.0, 0.0, 3.65)
	camera.rotation_degrees = Vector3.ZERO
	camera.fov = 42.0
	camera.near = 0.1
	camera.far = 100.0
	camera.current = true

	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color(0.0, 0.031, 0.063)
	environment.ambient_light_color = COLOR_AMBIENT_LIGHT
	environment.ambient_light_energy = 0.25
	environment.glow_enabled = true
	environment.glow_intensity = 1.4
	environment.glow_strength = 1.0
	environment.glow_bloom = 0.2
	environment.glow_blend_mode = Environment.GLOW_BLEND_MODE_ADDITIVE
	environment.glow_hdr_threshold = 0.85
	environment.glow_hdr_scale = 2.0
	environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	environment.tonemap_exposure = 1.0
	environment.tonemap_white = 6.0
	environment.ssr_enabled = true
	environment.ssr_max_steps = 64
	environment.ssr_fade_in = 0.15
	environment.ssr_fade_out = 2.0
	environment.ssr_depth_tolerance = 0.2
	environment.ssao_enabled = true
	environment.ssao_radius = 0.5
	environment.ssao_intensity = 0.8
	world_environment.environment = environment

	omni_light.position = Vector3.ZERO
	omni_light.light_color = COLOR_CORE_PRIMARY
	omni_light.light_energy = 3.0
	omni_light.omni_range = 4.0
	omni_light.omni_attenuation = 0.5
	omni_light.shadow_enabled = false

	spot_light.position = Vector3(0.0, 4.0, 1.0)
	spot_light.rotation_degrees = Vector3(-75.0, 0.0, 0.0)
	spot_light.light_color = COLOR_RING_PRIMARY
	spot_light.light_energy = 1.5
	spot_light.spot_range = 8.0
	spot_light.spot_angle = 35.0
	spot_light.shadow_enabled = false


func _build_core() -> void:
	var mesh := SphereMesh.new()
	mesh.radius = 0.74
	mesh.height = 1.48
	mesh.radial_segments = 192
	mesh.rings = 96
	core_sphere.mesh = mesh
	core_mat = ShaderMaterial.new()
	core_mat.shader = load("res://Shaders/core_plasma.gdshader")
	core_mat.set_shader_parameter("core_color", COLOR_CORE_PRIMARY)
	core_mat.set_shader_parameter("plasma_color", COLOR_CORE_HOT)
	core_mat.set_shader_parameter("pulse_intensity", 1.0)
	core_mat.set_shader_parameter("time_scale", 0.8)
	core_mat.set_shader_parameter("noise_scale", 6.0)
	core_mat.set_shader_parameter("fresnel_power", 3.0)
	core_mat.set_shader_parameter("vein_sharpness", 8.0)
	core_mat.set_shader_parameter("surface_displacement", 0.025)
	core_mat.set_shader_parameter("spike_strength", 0.0)
	core_mat.set_shader_parameter("surface_activity", 0.7)
	core_mat.set_shader_parameter("speech_drive", 0.0)
	core_sphere.material_override = core_mat


func _build_glass() -> void:
	_configure_shell(glass_shell, 0.93, 0.10, 0.03)
	_configure_shell(inner_glass_shell, 0.80, 0.05, 0.02)


func _configure_shell(node: MeshInstance3D, radius: float, opacity: float, grid_opacity: float) -> void:
	var mesh := SphereMesh.new()
	mesh.radius = radius
	mesh.height = radius * 2.0
	mesh.radial_segments = 128
	mesh.rings = 64
	node.mesh = mesh
	var mat := ShaderMaterial.new()
	mat.shader = load("res://Shaders/glass_shell.gdshader")
	mat.set_shader_parameter("opacity", opacity)
	mat.set_shader_parameter("grid_opacity", grid_opacity)
	mat.set_shader_parameter("shell_color", COLOR_SHELL)
	node.material_override = mat


func _build_rings() -> void:
	_add_ring(inner_system, "EnergyRing", 0.68, 0.73, 192, 32, Vector3(12, 0, 0), COLOR_CORE_PRIMARY, 24, 1.8, 0.25, 0.62, 0.32)
	_add_ring(inner_system, "PulseRing", 0.77, 0.80, 192, 32, Vector3(-8, 30, 5), COLOR_CORE_HOT, 64, 2.0, 0.55, 0.50, 0.20)
	_add_ring(middle_system, "DataRing", 0.88, 0.93, 224, 32, Vector3(5, 60, -10), COLOR_RING_PRIMARY, 48, 1.2, 0.35, 0.52, 0.32)
	_add_ring(middle_system, "SegmentRing", 0.98, 1.00, 256, 32, Vector3(-15, 120, 8), COLOR_CORE_PRIMARY, 16, 0.4, 0.15, 0.46, 0.45)
	_add_ring(outer_system, "HUDRing", 1.09, 1.12, 256, 32, Vector3(20, 0, -5), COLOR_RING_PRIMARY, 32, 0.7, 0.4, 0.34, 0.30)
	_add_ring(outer_system, "TechMarkerRing", 1.18, 1.20, 512, 32, Vector3(-5, 45, 12), COLOR_RING_OUTER, 96, 0.3, 0.6, 0.22, 0.30)
	_add_ring(orbit_system, "OrbitRing", 1.32, 1.34, 256, 32, Vector3(75, 0, 20), COLOR_RING_PRIMARY, 12, 0.15, 0.2, 0.18, 0.25)


func _add_ring(
	parent: Node3D,
	node_name: String,
	inner_radius: float,
	outer_radius: float,
	path_segments: int,
	tube_segments: int,
	rotation: Vector3,
	color: Color,
	segment_count: float,
	data_speed: float,
	gap_ratio: float,
	opacity: float,
	brightness_variance: float) -> void:
	var ring := MeshInstance3D.new()
	ring.name = node_name
	var mesh := TorusMesh.new()
	mesh.inner_radius = inner_radius
	mesh.outer_radius = outer_radius
	mesh.rings = maxi(path_segments, RING_PATH_SEGMENTS_MIN)
	mesh.ring_segments = maxi(tube_segments, RING_TUBE_SEGMENTS_MIN)
	ring.mesh = mesh
	ring.rotation_degrees = rotation
	ring.position.z = 0.001 + float(_ring_depth_offset_index) * 0.003
	_ring_depth_offset_index += 1
	var mat := ShaderMaterial.new()
	mat.shader = load("res://Shaders/ring_data.gdshader")
	mat.set_shader_parameter("ring_color", color)
	mat.set_shader_parameter("segment_count", segment_count)
	mat.set_shader_parameter("data_speed", data_speed)
	mat.set_shader_parameter("gap_ratio", gap_ratio)
	mat.set_shader_parameter("opacity", opacity)
	mat.set_shader_parameter("brightness_variance", brightness_variance)
	mat.set_shader_parameter("pulse_boost", 0.0)
	ring.material_override = mat
	parent.add_child(ring)


func _build_segment_details() -> void:
	for index in range(4):
		_add_ring(segment_system, "ArcSegment%s" % index, 1.23 + index * 0.04, 1.236 + index * 0.04, 512, 32, Vector3(index * 7.0, index * 29.0, index * 17.0), COLOR_CORE_HOT.lerp(COLOR_CORE_PRIMARY, index * 0.22), 128, 0.05 + index * 0.02, 0.08, 0.22, 0.35)
	_generate_tick_marks()


func _generate_tick_marks() -> void:
	var tick_count := 72
	var radius := 1.18
	for index in range(tick_count):
		var angle := (float(index) / tick_count) * TAU
		var tick := MeshInstance3D.new()
		tick.name = "Tick%s" % index
		var mesh := BoxMesh.new()
		mesh.size = Vector3(0.002, 0.012 if index % 6 == 0 else 0.006, 0.001)
		tick.mesh = mesh
		tick.position = Vector3(sin(angle) * radius, cos(angle) * radius, 0.0)
		tick.rotation.z = -angle
		tick.material_override = _unshaded_material(Color(COLOR_RING_PRIMARY.r, COLOR_RING_PRIMARY.g, COLOR_RING_PRIMARY.b, 0.55 if index % 6 == 0 else 0.28), 1.1 if index % 6 == 0 else 0.55)
		segment_system.add_child(tick)


func _build_particles() -> void:
	_configure_particle_system(ambient_field, 140, 5.0, 0.45, 0.95, 0.02, 0.08, 0.006, 0.018, COLOR_CORE_PRIMARY, true)
	_configure_particle_system(orbit_particles, 1, 3.0, 1.0, 1.1, 0.8, 1.4, 0.010, 0.030, COLOR_RING_PRIMARY, false)
	_configure_particle_system(pulse_burst, 100, 1.0, 1.0, 0.55, 0.8, 2.5, 0.008, 0.025, COLOR_CORE_HOT, false)
	pulse_burst.one_shot = false
	pulse_burst.explosiveness = 0.15


func _configure_particle_system(
	particles: GPUParticles3D,
	amount: int,
	lifetime: float,
	speed_scale: float,
	emission_radius: float,
	velocity_min: float,
	velocity_max: float,
	scale_min: float,
	scale_max: float,
	color: Color,
	emitting: bool) -> void:
	particles.amount = amount
	particles.lifetime = lifetime
	particles.speed_scale = speed_scale
	particles.emitting = emitting
	var material := ParticleProcessMaterial.new()
	material.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	material.emission_sphere_radius = emission_radius
	material.direction = Vector3.ZERO
	material.spread = 180.0
	material.gravity = Vector3.ZERO
	material.initial_velocity_min = velocity_min
	material.initial_velocity_max = velocity_max
	material.scale_min = scale_min
	material.scale_max = scale_max
	material.color = color
	particles.process_material = material
	var draw_mesh := SphereMesh.new()
	draw_mesh.radius = 0.008
	draw_mesh.height = 0.016
	draw_mesh.radial_segments = 12
	draw_mesh.rings = 6
	particles.draw_pass_1 = draw_mesh
	particles.material_override = _unshaded_material(color, 4.0)


func _build_ground_glow() -> void:
	var mesh := CylinderMesh.new()
	mesh.top_radius = 0.0
	mesh.bottom_radius = 1.4
	mesh.height = 0.01
	mesh.radial_segments = 96
	ground_glow.mesh = mesh
	ground_glow.position = Vector3(0.0, -1.3, 0.0)
	ground_mat = ShaderMaterial.new()
	ground_mat.shader = load("res://Shaders/ground_glow.gdshader")
	ground_mat.set_shader_parameter("glow_color", COLOR_RING_PRIMARY)
	ground_mat.set_shader_parameter("intensity", 0.6)
	ground_mat.set_shader_parameter("ring_count", 0.0)
	ground_glow.material_override = ground_mat


func _build_animation_placeholders() -> void:
	if not animation_controller.has_animation_library(""):
		animation_controller.add_animation_library("", AnimationLibrary.new())

	var library := animation_controller.get_animation_library("")
	for animation_name in ["idle_breathe", "thinking_scan", "execute_surge", "error_glitch"]:
		if library.has_animation(animation_name):
			continue
		var animation := Animation.new()
		animation.length = 3.5 if animation_name == "idle_breathe" else 1.5
		animation.loop_mode = Animation.LOOP_LINEAR if animation_name in ["idle_breathe", "thinking_scan", "error_glitch"] else Animation.LOOP_NONE
		library.add_animation(animation_name, animation)


func _transition_to(new_state: int, force: bool = false) -> void:
	if new_state == current_state and not force:
		return

	previous_state = current_state
	current_state = new_state
	_state_elapsed = 0.0

	var core_params: Array = CORE_PARAMS[new_state]
	var light_params: Array = LIGHT_PARAMS[new_state]
	var core_colors: Array = CORE_COLORS[new_state]
	_target_core_pulse_intensity = minf(core_params[0], MAX_PULSE_INTENSITY)
	_target_core_time_scale = core_params[1]
	_target_surface_displacement = core_params[2]
	_target_spike_strength = core_params[3]
	_target_surface_activity = core_params[4]
	_target_core_turn_speed = core_params[5]
	_target_shell_turn_speed = core_params[6]
	_target_inner_shell_turn_speed = core_params[7]
	_target_light_color = light_params[0]
	_target_light_energy = light_params[1]
	_target_core_color = core_colors[0]
	_target_plasma_color = core_colors[1]

	match new_state:
		OrbState.IDLE:
			pulse_burst.emitting = false
			animation_controller.play("idle_breathe")
		OrbState.THINKING:
			pulse_burst.emitting = true
			animation_controller.play("thinking_scan")
		OrbState.SPEAKING:
			pulse_burst.emitting = true
			animation_controller.stop()
		OrbState.EXECUTING:
			pulse_burst.emitting = true
			animation_controller.play("execute_surge")
		OrbState.ERROR:
			pulse_burst.emitting = true
			animation_controller.play("error_glitch")
			core_mat.set_shader_parameter("core_color", COLOR_ERROR)
			core_mat.set_shader_parameter("plasma_color", COLOR_ERROR_SOFT)
			core_mat.set_shader_parameter("pulse_intensity", 1.8)
			omni_light.light_color = COLOR_ERROR
			omni_light.light_energy = 5.5
		OrbState.CONFIRMATION:
			pulse_burst.emitting = false
			animation_controller.stop()


func _update_ring_rotations(delta: float) -> void:
	pass


func _update_system_motion(delta: float) -> void:
	_core_turn_speed = lerpf(_core_turn_speed, _target_core_turn_speed, minf(delta * 1.8, 1.0))
	_shell_turn_speed = lerpf(_shell_turn_speed, _target_shell_turn_speed, minf(delta * 1.8, 1.0))
	_inner_shell_turn_speed = lerpf(_inner_shell_turn_speed, _target_inner_shell_turn_speed, minf(delta * 1.8, 1.0))

	_core_phase = fmod(_core_phase + _core_turn_speed * delta, TAU)
	_shell_phase = fmod(_shell_phase + _shell_turn_speed * delta, TAU)
	_inner_shell_phase = fmod(_inner_shell_phase + _inner_shell_turn_speed * delta, TAU)

	core_sphere.rotation = Vector3(0.0, _core_phase, 0.0)
	glass_shell.rotation = Vector3(0.0, _shell_phase, 0.0)
	inner_glass_shell.rotation = Vector3(_inner_shell_phase, 0.0, 0.0)


func _update_speech_pulse(delta: float) -> void:
	_speech_pulse = move_toward(_speech_pulse, 0.0, SPEECH_PULSE_DECAY * delta)


func _update_ring_materials(delta: float) -> void:
	pass


func _update_core_and_light(delta: float) -> void:
	var pulse_target := _target_core_pulse_intensity
	if current_state == OrbState.THINKING:
		var thinking_wave := pow(maxf(0.0, sin((_state_elapsed / 3.0) * TAU)), 4.0)
		pulse_target += thinking_wave * 0.22
	if current_state == OrbState.SPEAKING:
		pulse_target = CORE_PARAMS[OrbState.SPEAKING][0] + _speech_pulse * SPEECH_PULSE_MAX_EXTRA
	if current_state == OrbState.CONFIRMATION:
		var confirmation_wave := (sin((_state_elapsed / CONFIRMATION_PULSE_PERIOD) * TAU) + 1.0) * 0.5
		pulse_target += confirmation_wave * 0.18
	pulse_target = clampf(pulse_target, 0.0, MAX_PULSE_INTENSITY)

	var current_pulse := _shader_float("pulse_intensity", pulse_target)
	var current_time_scale := _shader_float("time_scale", _target_core_time_scale)
	var speech_drive := _speech_pulse if current_state == OrbState.SPEAKING else 0.0
	var spike_target := _target_spike_strength
	var displacement_target := _target_surface_displacement
	var activity_target := _target_surface_activity
	if current_state == OrbState.SPEAKING:
		spike_target += _speech_pulse * 0.090
		displacement_target += _speech_pulse * 0.030
	if current_state == OrbState.THINKING:
		var localized_flare := pow(maxf(0.0, sin((_state_elapsed / 2.7) * TAU)), 6.0)
		displacement_target += localized_flare * 0.010
		activity_target += localized_flare * 0.20

	core_mat.set_shader_parameter("pulse_intensity", lerpf(current_pulse, pulse_target, minf(delta * 12.0, 1.0)))
	core_mat.set_shader_parameter("time_scale", lerpf(current_time_scale, _target_core_time_scale, minf(delta * 4.0, 1.0)))
	core_mat.set_shader_parameter("surface_displacement", lerpf(_shader_float("surface_displacement", displacement_target), displacement_target, minf(delta * 5.0, 1.0)))
	core_mat.set_shader_parameter("spike_strength", lerpf(_shader_float("spike_strength", spike_target), spike_target, minf(delta * 7.0, 1.0)))
	core_mat.set_shader_parameter("surface_activity", lerpf(_shader_float("surface_activity", activity_target), activity_target, minf(delta * 4.0, 1.0)))
	core_mat.set_shader_parameter("speech_drive", lerpf(_shader_float("speech_drive", speech_drive), speech_drive, minf(delta * 10.0, 1.0)))

	var light_target := _target_light_energy
	if current_state == OrbState.SPEAKING:
		light_target += _speech_pulse * 0.45
	if current_state == OrbState.THINKING:
		var thinking_light_wave := pow(maxf(0.0, sin((_state_elapsed / 3.0) * TAU)), 4.0)
		light_target += thinking_light_wave * 0.45
	if current_state == OrbState.CONFIRMATION:
		var confirmation_light_wave := (sin((_state_elapsed / CONFIRMATION_PULSE_PERIOD) * TAU) + 1.0) * 0.5
		light_target += confirmation_light_wave * 0.60
	omni_light.light_energy = lerpf(omni_light.light_energy, light_target, minf(delta * 2.0, 1.0))
	omni_light.light_color = omni_light.light_color.lerp(_target_light_color, minf(delta * 2.0, 1.0))

	var current_core_color = core_mat.get_shader_parameter("core_color")
	var core_color: Color = _target_core_color if current_core_color == null else current_core_color
	var current_plasma_color = core_mat.get_shader_parameter("plasma_color")
	var plasma_color: Color = _target_plasma_color if current_plasma_color == null else current_plasma_color
	core_mat.set_shader_parameter("core_color", core_color.lerp(_target_core_color, minf(delta * 2.0, 1.0)))
	core_mat.set_shader_parameter("plasma_color", plasma_color.lerp(_target_plasma_color, minf(delta * 2.0, 1.0)))

	if ground_mat:
		var target_ground := 0.50 + _speech_pulse * 0.12
		if current_state == OrbState.CONFIRMATION:
			target_ground += 0.12
		if current_state == OrbState.ERROR:
			target_ground += 0.18
		var current_ground_value = ground_mat.get_shader_parameter("intensity")
		var current_ground: float = 0.6 if current_ground_value == null else current_ground_value
		ground_mat.set_shader_parameter("intensity", lerpf(current_ground, target_ground, minf(delta * 3.0, 1.0)))


func _shader_float(parameter_name: String, fallback: float) -> float:
	var value = core_mat.get_shader_parameter(parameter_name)
	return fallback if value == null else value


func _unshaded_material(color: Color, emission_energy: float) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	material.albedo_color = color
	material.emission_enabled = true
	material.emission = Color(color.r, color.g, color.b)
	material.emission_energy_multiplier = emission_energy
	return material
