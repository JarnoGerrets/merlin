extends Node3D
class_name OrbController

enum OrbState { IDLE, THINKING, SPEAKING, EXECUTING, ERROR }

const CORE_PRIMARY := Color(0.0, 0.81, 1.0)
const CORE_SECONDARY := Color(0.10, 0.56, 1.0)
const CORE_HOT := Color(0.30, 0.87, 1.0)
const NEAR_WHITE := Color(0.80, 0.94, 1.0)
const AMBER := Color(1.0, 0.42, 0.0)
const ERROR_RED := Color(0.8, 0.07, 0.07)

const RING_SPEEDS := {
	OrbState.IDLE: [0.30, -0.20, 0.15, 0.08, 0.10, -0.05, 0.04],
	OrbState.THINKING: [1.80, -2.50, 1.20, 0.60, 0.80, -0.30, 0.15],
	OrbState.SPEAKING: [1.00, -1.40, 0.80, 0.35, 0.55, -0.20, 0.10],
	OrbState.EXECUTING: [3.00, -3.80, 2.00, 1.00, 1.40, -0.60, 0.25],
	OrbState.ERROR: [0.60, -0.40, 0.30, 0.15, 0.20, -0.08, 0.05],
}

const CORE_PARAMS := {
	OrbState.IDLE: [1.00, 0.80],
	OrbState.THINKING: [1.60, 1.30],
	OrbState.SPEAKING: [1.30, 1.10],
	OrbState.EXECUTING: [2.20, 1.60],
	OrbState.ERROR: [0.50, 0.60],
}

const LIGHT_PARAMS := {
	OrbState.IDLE: [CORE_PRIMARY, 3.0],
	OrbState.THINKING: [Color(0.15, 0.70, 1.00), 5.0],
	OrbState.SPEAKING: [Color(0.00, 0.90, 1.00), 4.5],
	OrbState.EXECUTING: [AMBER, 7.0],
	OrbState.ERROR: [ERROR_RED, 4.0],
}

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
var state_tween: Tween
var speech_pulse_timer := 0.0
var core_mat: ShaderMaterial
var ground_mat: ShaderMaterial
var ring_nodes: Array[MeshInstance3D] = []
var ring_speeds: Array[float] = [0.30, -0.20, 0.15, 0.08, 0.10, -0.05, 0.04]
var _temporary_state_token := 0


func _ready() -> void:
	_configure_scene()
	_build_core()
	_build_glass()
	_build_rings()
	_build_segment_details()
	_build_particles()
	_build_ground_glow()
	_build_animation_placeholders()

	ring_nodes = [
		inner_system.get_node("EnergyRing"),
		inner_system.get_node("PulseRing"),
		middle_system.get_node("DataRing"),
		middle_system.get_node("SegmentRing"),
		outer_system.get_node("HUDRing"),
		outer_system.get_node("TechMarkerRing"),
		orbit_system.get_node("OrbitRing"),
	]

	_transition_to(OrbState.IDLE, true)
	animation_controller.play("idle_breathe")
	set_process(true)


func _process(delta: float) -> void:
	_update_ring_rotations(delta)
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
	_transition_to(OrbState.ERROR)
	await get_tree().create_timer(duration).timeout
	if token == _temporary_state_token and current_state == OrbState.ERROR:
		_transition_to(OrbState.IDLE)


func notify_speech_tick() -> void:
	if current_state != OrbState.SPEAKING:
		return

	speech_pulse_timer = 0.12
	var pulse_tween := create_tween()
	var current_energy := omni_light.light_energy
	var current_pulse = core_mat.get_shader_parameter("pulse_intensity")
	pulse_tween.tween_property(omni_light, "light_energy", current_energy * 1.18, 0.06)
	pulse_tween.tween_property(omni_light, "light_energy", current_energy, 0.10)
	pulse_tween.parallel().tween_method(
		func(value: float) -> void: core_mat.set_shader_parameter("pulse_intensity", value),
		current_pulse,
		current_pulse * 1.15,
		0.06)
	pulse_tween.parallel().tween_method(
		func(value: float) -> void: core_mat.set_shader_parameter("pulse_intensity", value),
		current_pulse * 1.15,
		current_pulse,
		0.10)


func _configure_scene() -> void:
	camera.position = Vector3(0.0, 0.4, 3.5)
	camera.rotation_degrees = Vector3(-6.5, 0.0, 0.0)
	camera.fov = 42.0
	camera.near = 0.1
	camera.far = 100.0
	camera.current = true

	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color(0.0, 0.031, 0.063)
	environment.ambient_light_color = Color(0.0, 0.094, 0.157)
	environment.ambient_light_energy = 0.25
	environment.glow_enabled = true
	environment.glow_intensity = 1.8
	environment.glow_strength = 1.2
	environment.glow_bloom = 0.3
	environment.glow_blend_mode = Environment.GLOW_BLEND_MODE_ADDITIVE
	environment.glow_hdr_threshold = 0.7
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
	omni_light.light_color = CORE_PRIMARY
	omni_light.light_energy = 3.0
	omni_light.omni_range = 4.0
	omni_light.omni_attenuation = 0.5
	omni_light.shadow_enabled = false

	spot_light.position = Vector3(0.0, 4.0, 1.0)
	spot_light.rotation_degrees = Vector3(-75.0, 0.0, 0.0)
	spot_light.light_color = Color(0.2, 0.6, 1.0)
	spot_light.light_energy = 1.5
	spot_light.spot_range = 8.0
	spot_light.spot_angle = 35.0
	spot_light.shadow_enabled = false


func _build_core() -> void:
	var mesh := SphereMesh.new()
	mesh.radius = 0.52
	mesh.height = 1.04
	mesh.radial_segments = 128
	mesh.rings = 64
	core_sphere.mesh = mesh
	core_mat = ShaderMaterial.new()
	core_mat.shader = load("res://Shaders/core_plasma.gdshader")
	core_mat.set_shader_parameter("core_color", CORE_PRIMARY)
	core_mat.set_shader_parameter("plasma_color", CORE_HOT)
	core_mat.set_shader_parameter("pulse_intensity", 1.0)
	core_mat.set_shader_parameter("time_scale", 0.8)
	core_mat.set_shader_parameter("noise_scale", 6.0)
	core_mat.set_shader_parameter("fresnel_power", 3.0)
	core_mat.set_shader_parameter("vein_sharpness", 8.0)
	core_sphere.material_override = core_mat


func _build_glass() -> void:
	_configure_shell(glass_shell, 1.12, 0.12, 0.08)
	_configure_shell(inner_glass_shell, 0.72, 0.06, 0.04)


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
	mat.set_shader_parameter("shell_color", Color(0.05, 0.29, 0.88))
	node.material_override = mat


func _build_rings() -> void:
	_add_ring(inner_system, "EnergyRing", 0.68, 0.73, 128, 6, Vector3(12, 0, 0), CORE_PRIMARY, 24, 1.8, 0.25, 0.75, 0.4)
	_add_ring(inner_system, "PulseRing", 0.76, 0.79, 128, 4, Vector3(-8, 30, 5), NEAR_WHITE, 64, 2.5, 0.55, 0.70, 0.25)
	_add_ring(middle_system, "DataRing", 0.88, 0.93, 192, 6, Vector3(5, 60, -10), CORE_SECONDARY, 48, 1.2, 0.35, 0.75, 0.4)
	_add_ring(middle_system, "SegmentRing", 0.95, 0.97, 256, 4, Vector3(-15, 120, 8), CORE_PRIMARY, 16, 0.4, 0.15, 0.70, 0.6)
	_add_ring(outer_system, "HUDRing", 1.06, 1.10, 256, 6, Vector3(20, 0, -5), CORE_SECONDARY, 32, 0.7, 0.4, 0.62, 0.4)
	_add_ring(outer_system, "TechMarkerRing", 1.13, 1.14, 512, 3, Vector3(-5, 45, 12), Color(0.05, 0.4, 0.85), 96, 0.3, 0.6, 0.4, 0.45)
	_add_ring(orbit_system, "OrbitRing", 1.28, 1.30, 256, 4, Vector3(75, 0, 20), Color(0.2, 0.6, 1.0), 12, 0.15, 0.2, 0.35, 0.35)


func _add_ring(
	parent: Node3D,
	node_name: String,
	inner_radius: float,
	outer_radius: float,
	ring_segments: int,
	sections: int,
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
	mesh.ring_segments = ring_segments
	mesh.rings = sections
	ring.mesh = mesh
	ring.rotation_degrees = rotation
	var mat := ShaderMaterial.new()
	mat.shader = load("res://Shaders/ring_data.gdshader")
	mat.set_shader_parameter("ring_color", color)
	mat.set_shader_parameter("segment_count", segment_count)
	mat.set_shader_parameter("data_speed", data_speed)
	mat.set_shader_parameter("gap_ratio", gap_ratio)
	mat.set_shader_parameter("opacity", opacity)
	mat.set_shader_parameter("brightness_variance", brightness_variance)
	ring.material_override = mat
	parent.add_child(ring)


func _build_segment_details() -> void:
	for index in range(4):
		_add_ring(segment_system, "ArcSegment%s" % index, 1.065 + index * 0.025, 1.070 + index * 0.025, 512, 3, Vector3(index * 7.0, index * 29.0, index * 17.0), NEAR_WHITE.lerp(CORE_PRIMARY, index * 0.22), 128, 0.05 + index * 0.02, 0.08, 0.45, 0.55)
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
		tick.material_override = _unshaded_material(Color(0.3, 0.75, 1.0, 0.7 if index % 6 == 0 else 0.4), 1.5 if index % 6 == 0 else 0.8)
		segment_system.add_child(tick)


func _build_particles() -> void:
	_configure_particle_system(ambient_field, 180, 5.0, 0.5, 1.6, 0.03, 0.12, 0.008, 0.025, CORE_PRIMARY, true)
	_configure_particle_system(orbit_particles, 60, 3.0, 1.0, 1.1, 0.8, 1.4, 0.010, 0.030, CORE_SECONDARY, true)
	_configure_particle_system(pulse_burst, 100, 1.0, 1.0, 0.55, 0.8, 2.5, 0.008, 0.025, CORE_HOT, false)
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
	ground_mat.set_shader_parameter("intensity", 0.6)
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

	if state_tween:
		state_tween.kill()
	state_tween = create_tween().set_parallel(true)

	var core_params: Array = CORE_PARAMS[new_state]
	var light_params: Array = LIGHT_PARAMS[new_state]
	state_tween.tween_method(
		func(value: float) -> void: core_mat.set_shader_parameter("pulse_intensity", value),
		core_mat.get_shader_parameter("pulse_intensity"),
		core_params[0],
		0.7)
	state_tween.tween_method(
		func(value: float) -> void: core_mat.set_shader_parameter("time_scale", value),
		core_mat.get_shader_parameter("time_scale"),
		core_params[1],
		0.7)
	state_tween.tween_property(omni_light, "light_color", light_params[0], 0.5)
	state_tween.tween_property(omni_light, "light_energy", light_params[1], 0.5)

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


func _update_ring_rotations(delta: float) -> void:
	var targets: Array = RING_SPEEDS[current_state]
	for index in range(ring_nodes.size()):
		ring_speeds[index] = lerpf(ring_speeds[index], targets[index], delta * 2.5)

	ring_nodes[0].rotate_y(ring_speeds[0] * delta)
	ring_nodes[0].rotate_x(ring_speeds[0] * 0.25 * delta)
	ring_nodes[1].rotate_z(ring_speeds[1] * delta)
	ring_nodes[1].rotate_y(ring_speeds[1] * 0.3 * delta)
	ring_nodes[2].rotate_y(ring_speeds[2] * delta)
	ring_nodes[2].rotate_z(ring_speeds[2] * 0.2 * delta)
	ring_nodes[3].rotate_x(ring_speeds[3] * delta)
	ring_nodes[3].rotate_z(ring_speeds[3] * 0.15 * delta)
	ring_nodes[4].rotate_y(ring_speeds[4] * delta)
	ring_nodes[4].rotate_x(ring_speeds[4] * 0.1 * delta)
	ring_nodes[5].rotate_z(ring_speeds[5] * delta)
	ring_nodes[6].rotate_y(ring_speeds[6] * delta)
	ring_nodes[6].rotate_z(ring_speeds[6] * 0.4 * delta)


func _update_system_motion(delta: float) -> void:
	inner_system.rotate_y(0.05 * delta)
	middle_system.rotate_x(0.025 * delta)
	outer_system.rotate_z(-0.018 * delta)
	orbit_system.rotate_y(0.012 * delta)
	segment_system.rotate_z(0.006 * delta)


func _update_speech_pulse(delta: float) -> void:
	if current_state != OrbState.SPEAKING:
		return
	speech_pulse_timer = maxf(0.0, speech_pulse_timer - delta)


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
