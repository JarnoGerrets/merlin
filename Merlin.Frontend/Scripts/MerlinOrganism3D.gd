extends Node3D
class_name MerlinOrganism3D

enum OrganismState { IDLE, THINKING, SPEAKING, EXECUTING, ERROR, CONFIRMATION }

class OrganismNode:
	var base_position := Vector3.ZERO
	var current_position := Vector3.ZERO
	var radius := 0.035
	var brightness := 1.0
	var phase := 0.0
	var hub := false
	var source_node := -1


class OrganismConnection:
	var a := 0
	var b := 0
	var base_alpha := 0.25
	var width := 0.006
	var phase := 0.0
	var route := false


class EnergyPulse:
	var connection_index := 0
	var from_node := -1
	var to_node := -1
	var progress := 0.0
	var speed := 0.45
	var brightness := 1.0
	var generation := 0
	var can_split := true
	var tail := 0.22


class DestinationFlash:
	var node_index := -1
	var age := 0.0
	var duration := 0.26
	var brightness := 1.0


class SpeechRipple:
	var axis := Vector3.UP
	var phase := 0.0
	var age := 0.0
	var duration := 0.45
	var width := 0.32
	var strength := 1.0
	var spin := 1.0


const STRUCTURAL_NODE_COUNT := 1700
const HUB_NODE_COUNT := 30
const BRIGHT_CLUSTER_COUNT := 52
const STRUCTURAL_FEATURE_CLUSTER_COUNT := 11
const AMBIENT_DUST_NODE_COUNT := 1800
const HUB_CLUSTER_PARTICLE_COUNT := 56
const BRIGHT_CLUSTER_PARTICLE_COUNT := 58
const CORE_CLUSTER_PARTICLE_COUNT := 1580
const DUST_NODE_COUNT := AMBIENT_DUST_NODE_COUNT + HUB_NODE_COUNT * HUB_CLUSTER_PARTICLE_COUNT + BRIGHT_CLUSTER_COUNT * BRIGHT_CLUSTER_PARTICLE_COUNT + CORE_CLUSTER_PARTICLE_COUNT
const ORB_RADIUS := 2.65
const CORE_RADIUS := ORB_RADIUS * 0.48
const MAX_CONNECTION_DISTANCE := 0.92
const HUB_CONNECTION_DISTANCE := 1.20
const MAX_PULSES := 36
const MAX_DESTINATION_FLASHES := 24
const ORGANISM_DISPLAY_SCALE := 0.76

const CYAN := {
	"hot": Color("#F4FBFF"),
	"node": Color("#4DAEFF"),
	"line": Color("#5EC4FF"),
	"dim": Color("#0A3F7A"),
	"dust": Color("#2E8CFF"),
}

const RED := {
	"hot": Color("#FFDDDD"),
	"node": Color("#FF4545"),
	"line": Color("#FF5050"),
	"dim": Color("#8A1010"),
	"dust": Color("#AA2020"),
}

const AMBER := {
	"hot": Color("#FFF0C8"),
	"node": Color("#FFBC3D"),
	"line": Color("#FFC44F"),
	"dim": Color("#8C5600"),
	"dust": Color("#B87718"),
}

var current_state := OrganismState.IDLE

var _rng := RandomNumberGenerator.new()
var _time := 0.0
var _nodes: Array[OrganismNode] = []
var _dust: Array[OrganismNode] = []
var _connections: Array[OrganismConnection] = []
var _pulses: Array[EnergyPulse] = []
var _destination_flashes: Array[DestinationFlash] = []
var _node_connections: Array = []
var _node_dust_indices: Array = []
var _route_connection_indices: Array[int] = []
var _cluster_node_indices: Array[int] = []
var _structural_cluster_anchors: Array[Vector3] = []
var _node_speech_light := {}
var _dust_speech_light := {}
var _connection_speech_light := {}
var _palette := CYAN.duplicate(true)
var _target_palette := CYAN.duplicate(true)
var _activity := 0.18
var _target_activity := 0.18
var _brightness := 1.0
var _target_brightness := 1.0
var _rotation_y := 0.0
var _pulse_timer := 0.0
var _speech_energy := 0.0
var _speech_global_morph := 0.0
var _speech_tick_count := 0
var _speech_region_timer := 0.0
var _speech_global_timer := 0.0
var _speech_ripple_timer := 0.0
var _speech_region_axes: Array[Vector3] = []
var _speech_region_targets: Array[Vector3] = []
var _speech_region_weights: Array[float] = []
var _speech_ripples: Array[SpeechRipple] = []

var _graph_root: Node3D
var _node_multimesh: MultiMeshInstance3D
var _hub_multimesh: MultiMeshInstance3D
var _dust_multimesh: MultiMeshInstance3D
var _pulse_multimesh: MultiMeshInstance3D
var _core_multimesh: MultiMeshInstance3D
var _line_mesh_instance: MeshInstance3D
var _glow_line_mesh_instance: MeshInstance3D
var _camera: Camera3D
var _line_material: StandardMaterial3D
var _glow_line_material: StandardMaterial3D
var _node_material: StandardMaterial3D
var _hub_material: StandardMaterial3D
var _dust_material: StandardMaterial3D
var _pulse_material: StandardMaterial3D
var _core_material: StandardMaterial3D


func _ready() -> void:
	_rng.seed = 26062026
	_reset_speech_regions()
	_build_scene()
	_generate_nodes()
	_build_node_dust_indices()
	_generate_connections()
	_setup_multimeshes()
	set_process(true)


func set_idle() -> void:
	_set_state(OrganismState.IDLE, CYAN, 0.18, 1.00)


func set_thinking() -> void:
	_set_state(OrganismState.THINKING, CYAN, 0.42, 1.12)


func set_speaking() -> void:
	_set_state(OrganismState.SPEAKING, CYAN, 0.58, 1.00)
	_speech_energy = maxf(_speech_energy, 0.48)
	_speech_region_timer = 0.0
	_speech_global_timer = 0.0
	_speech_ripple_timer = 0.0
	_pick_speech_region()


func play_tool_execution(_duration: float = 2.5) -> void:
	_set_state(OrganismState.EXECUTING, CYAN, 0.54, 1.20)


func play_error(_duration: float = 3.0) -> void:
	_set_state(OrganismState.ERROR, RED, 0.48, 1.18)


func play_confirmation() -> void:
	_set_state(OrganismState.CONFIRMATION, AMBER, 0.24, 1.04)


func notify_speech_tick(_character: String = "", delay: float = 0.0, progress: float = 0.0) -> void:
	_target_activity = maxf(_target_activity, 0.64)
	_speech_tick_count += 1
	_speech_energy = clampf(_speech_energy + 0.018 + clampf(delay, 0.0, 0.08) * 0.12, 0.0, 0.82)
	if _speech_tick_count % 12 == 0:
		_pick_speech_region(progress)
	if _speech_tick_count % 72 == 0:
		_speech_global_morph = clampf(_speech_global_morph + 0.08, 0.0, 0.34)
	if _speech_tick_count % 18 == 0:
		_pick_speech_region(progress)


func _set_state(state: int, palette: Dictionary, activity: float, brightness: float) -> void:
	current_state = state
	_target_palette = palette.duplicate(true)
	_target_activity = activity
	_target_brightness = brightness


func _build_scene() -> void:
	_camera = Camera3D.new()
	_camera.name = "Camera3D"
	_camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	_camera.size = 5.35
	_camera.position = Vector3(0.0, 0.0, 7.2)
	_camera.look_at(Vector3.ZERO, Vector3.UP)
	add_child(_camera)

	var environment := WorldEnvironment.new()
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.0, 0.006, 0.018, 0.0)
	env.glow_enabled = true
	env.glow_intensity = 0.46
	env.glow_strength = 0.38
	env.glow_bloom = 0.08
	environment.environment = env
	add_child(environment)

	_graph_root = Node3D.new()
	_graph_root.name = "GraphRoot"
	add_child(_graph_root)

	_glow_line_mesh_instance = MeshInstance3D.new()
	_glow_line_mesh_instance.name = "LineGlowMesh"
	_glow_line_material = _material(CYAN["line"], 0.10, true, true)
	_glow_line_mesh_instance.material_override = _glow_line_material
	_graph_root.add_child(_glow_line_mesh_instance)

	_line_mesh_instance = MeshInstance3D.new()
	_line_mesh_instance.name = "LineMesh"
	_line_material = _material(CYAN["line"], 1.0, true, true)
	_line_mesh_instance.material_override = _line_material
	_graph_root.add_child(_line_mesh_instance)

	_node_multimesh = MultiMeshInstance3D.new()
	_node_multimesh.name = "Nodes"
	_node_material = _material(CYAN["node"], 1.0, true, true)
	_node_multimesh.material_override = _node_material
	_graph_root.add_child(_node_multimesh)

	_hub_multimesh = MultiMeshInstance3D.new()
	_hub_multimesh.name = "Hubs"
	_hub_material = _material(CYAN["hot"], 1.0, true, true)
	_hub_multimesh.material_override = _hub_material
	_graph_root.add_child(_hub_multimesh)

	_dust_multimesh = MultiMeshInstance3D.new()
	_dust_multimesh.name = "Dust"
	_dust_material = _material(CYAN["dust"], 0.28, true, true)
	_dust_multimesh.material_override = _dust_material
	_graph_root.add_child(_dust_multimesh)

	_pulse_multimesh = MultiMeshInstance3D.new()
	_pulse_multimesh.name = "EnergyPulses"
	_pulse_material = _material(CYAN["hot"], 1.0, true, true)
	_pulse_multimesh.material_override = _pulse_material
	_graph_root.add_child(_pulse_multimesh)

	_core_multimesh = MultiMeshInstance3D.new()
	_core_multimesh.name = "CoreGlow"
	_core_material = _material(CYAN["hot"], 0.32, true, true)
	_core_multimesh.material_override = _core_material
	_graph_root.add_child(_core_multimesh)


func _material(color: Color, alpha: float, additive: bool, through_depth: bool = false) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.blend_mode = BaseMaterial3D.BLEND_MODE_ADD if additive else BaseMaterial3D.BLEND_MODE_MIX
	material.albedo_color = Color(color.r, color.g, color.b, alpha)
	material.emission_enabled = true
	material.emission = color
	material.emission_energy_multiplier = 0.92
	material.vertex_color_use_as_albedo = true
	material.no_depth_test = through_depth
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	return material


func _generate_nodes() -> void:
	_nodes.clear()
	_dust.clear()
	_cluster_node_indices.clear()
	_build_structural_cluster_anchors()

	for index in range(STRUCTURAL_NODE_COUNT):
		var node := OrganismNode.new()
		node.hub = false
		node.phase = _rng.randf() * TAU
		node.base_position = _network_position(index)
		node.current_position = node.base_position
		var center_t := 1.0 - clampf(node.base_position.length() / ORB_RADIUS, 0.0, 1.0)
		node.radius = _rng.randf_range(0.0048, 0.012) * lerpf(0.92, 1.10, center_t)
		node.brightness = lerpf(0.74, 1.24, pow(center_t, 0.62))
		_nodes.append(node)

	for index in range(HUB_NODE_COUNT):
		var hub := OrganismNode.new()
		hub.hub = true
		hub.phase = _rng.randf() * TAU
		hub.base_position = _hub_position(index)
		hub.current_position = hub.base_position
		var center_t := 1.0 - clampf(hub.base_position.length() / ORB_RADIUS, 0.0, 1.0)
		var hub_importance := _hub_importance(index)
		hub.radius = _rng.randf_range(0.009, 0.017) * lerpf(0.95, 1.18, center_t) * hub_importance
		hub.brightness = lerpf(1.35, 2.15, pow(center_t, 0.52)) * lerpf(1.0, 1.14, hub_importance - 1.0)
		_cluster_node_indices.append(_nodes.size())
		_nodes.append(hub)

	for hub_index in range(STRUCTURAL_NODE_COUNT, _nodes.size()):
		var hub_node := _nodes[hub_index]
		var hub_order := hub_index - STRUCTURAL_NODE_COUNT
		var hub_importance := _hub_importance(hub_order)
		for cluster_index in range(HUB_CLUSTER_PARTICLE_COUNT):
			var spark := OrganismNode.new()
			spark.hub = true
			spark.phase = _rng.randf() * TAU
			var hub_cluster_radius: float = 0.30 * lerpf(1.0, 1.18, hub_importance - 1.0)
			var cluster_radius: float = pow(_rng.randf(), 1.18) * _rng.randf_range(0.035, hub_cluster_radius)
			var radial_jitter: float = _rng.randf_range(-0.018, 0.026) if hub_order >= 6 else _rng.randf_range(-0.055, 0.070)
			spark.base_position = _surface_patch_position(hub_node.base_position, cluster_radius, radial_jitter)
			spark.current_position = spark.base_position
			spark.source_node = hub_index
			var cluster_t := cluster_radius / hub_cluster_radius
			spark.radius = _rng.randf_range(0.0028, 0.0072) * lerpf(1.18, 0.72, cluster_t) * lerpf(1.0, 1.10, hub_importance - 1.0)
			spark.brightness = _rng.randf_range(0.68, 1.45) * lerpf(1.46, 0.70, cluster_t) * lerpf(1.0, 1.12, hub_importance - 1.0)
			_dust.append(spark)

	for cluster_index in range(BRIGHT_CLUSTER_COUNT):
		var anchor_index := _rng.randi_range(0, STRUCTURAL_NODE_COUNT - 1)
		var anchor := _nodes[anchor_index]
		var feature_cluster := cluster_index % 5 == 0
		for spark_index in range(BRIGHT_CLUSTER_PARTICLE_COUNT):
			var spark := OrganismNode.new()
			spark.hub = true
			spark.phase = _rng.randf() * TAU
			var cluster_max_radius := 0.34 if feature_cluster else 0.20
			var cluster_radius := pow(_rng.randf(), 1.65 if feature_cluster else 1.50) * _rng.randf_range(0.030 if feature_cluster else 0.030, cluster_max_radius)
			spark.base_position = anchor.base_position + _random_unit_vector() * cluster_radius
			spark.current_position = spark.base_position
			spark.source_node = anchor_index
			var cluster_t := cluster_radius / cluster_max_radius
			var feature_size := 1.65 if feature_cluster else 1.0
			var feature_brightness := 1.62 if feature_cluster else 1.0
			spark.radius = _rng.randf_range(0.0030, 0.0078) * lerpf(1.40, 0.84, cluster_t) * feature_size
			spark.brightness = _rng.randf_range(0.74, 1.68) * lerpf(1.92, 0.88, cluster_t) * feature_brightness
			_dust.append(spark)

	for core_index in range(CORE_CLUSTER_PARTICLE_COUNT):
		var spark := OrganismNode.new()
		spark.hub = true
		spark.phase = _rng.randf() * TAU
		var core_radius := pow(_rng.randf(), 1.72) * CORE_RADIUS
		spark.base_position = _irregular_orb_position(core_radius, 0.30)
		spark.current_position = spark.base_position
		spark.radius = _rng.randf_range(0.0030, 0.0088) * lerpf(1.62, 0.78, core_radius / CORE_RADIUS)
		spark.brightness = _rng.randf_range(0.90, 2.14) * lerpf(2.10, 0.78, core_radius / CORE_RADIUS)
		_dust.append(spark)

	for index in range(AMBIENT_DUST_NODE_COUNT):
		var dust := OrganismNode.new()
		dust.phase = _rng.randf() * TAU
		dust.base_position = _dust_position()
		dust.current_position = dust.base_position
		dust.radius = _rng.randf_range(0.0018, 0.0042)
		dust.brightness = _rng.randf_range(0.08, 0.26)
		_dust.append(dust)


func _network_position(index: int) -> Vector3:
	if index < int(STRUCTURAL_NODE_COUNT * 0.24):
		return _irregular_orb_position(_rng.randf_range(0.03, CORE_RADIUS), 0.42)

	var cluster_start := int(STRUCTURAL_NODE_COUNT * 0.24)
	var cluster_end := int(STRUCTURAL_NODE_COUNT * 0.46)
	if index < cluster_end and not _structural_cluster_anchors.is_empty():
		var local_index := index - cluster_start
		var anchor_index := local_index % _structural_cluster_anchors.size()
		var anchor := _structural_cluster_anchors[anchor_index]
		var radius_t := clampf(anchor.length() / ORB_RADIUS, 0.0, 1.0)
		var cluster_radius := _rng.randf_range(0.10, 0.34) * lerpf(1.10, 0.78, 1.0 - radius_t)
		var local_position := anchor + _random_unit_vector() * pow(_rng.randf(), 1.45) * cluster_radius
		return _shape_local_position(local_position)

	var shell_roll := _rng.randf()
	if shell_roll < 0.56:
		return _irregular_orb_position(_rng.randf_range(ORB_RADIUS * 0.70, ORB_RADIUS * 1.04), 0.20)
	return _irregular_orb_position(pow(_rng.randf(), 1.05) * ORB_RADIUS * 0.94, 0.30)


func _build_structural_cluster_anchors() -> void:
	_structural_cluster_anchors.clear()
	for index in range(STRUCTURAL_FEATURE_CLUSTER_COUNT):
		var radius_min := ORB_RADIUS * (0.40 if index % 3 == 0 else 0.56)
		var radius_max := ORB_RADIUS * (0.76 if index % 3 == 0 else 1.03)
		var anchor := _irregular_orb_position(_rng.randf_range(radius_min, radius_max), 0.18)
		_structural_cluster_anchors.append(anchor)


func _hub_position(index: int) -> Vector3:
	if index == 0:
		return _irregular_orb_position(_rng.randf_range(ORB_RADIUS * 0.04, ORB_RADIUS * 0.16), 0.20)
	if index < 6:
		return _irregular_orb_position(_rng.randf_range(ORB_RADIUS * 0.18, ORB_RADIUS * 0.58), 0.18)
	return _irregular_orb_position(_rng.randf_range(ORB_RADIUS * 0.62, ORB_RADIUS * 1.04), 0.16)


func _hub_importance(index: int) -> float:
	if index == 0:
		return 1.22
	if index < 6:
		return 1.12
	return 1.24 if index % 4 == 0 else 1.14


func _dust_position() -> Vector3:
	return _irregular_orb_position(_rng.randf_range(ORB_RADIUS * 0.35, ORB_RADIUS * 1.18), 0.36)


func _surface_patch_position(anchor: Vector3, spread: float, radial_jitter: float) -> Vector3:
	var normal: Vector3 = anchor.normalized() if anchor.length_squared() > 0.001 else Vector3.UP
	var tangent_a: Vector3 = normal.cross(Vector3.UP)
	if tangent_a.length_squared() < 0.001:
		tangent_a = normal.cross(Vector3.RIGHT)
	tangent_a = tangent_a.normalized()
	var tangent_b: Vector3 = normal.cross(tangent_a).normalized()
	var angle: float = _rng.randf() * TAU
	var oval: Vector2 = Vector2(cos(angle), sin(angle) * _rng.randf_range(0.55, 1.0)) * spread
	var tangent_offset: Vector3 = tangent_a * oval.x + tangent_b * oval.y
	var surface_radius: float = anchor.length() + radial_jitter
	return (normal * surface_radius + tangent_offset).limit_length(ORB_RADIUS * 1.12)


func _irregular_orb_position(radius: float, noise: float) -> Vector3:
	var direction := _random_unit_vector()
	var theta := atan2(direction.y, direction.x)
	var organic := 1.0
	organic += sin(theta * 3.0 + direction.z * 2.1) * noise * 0.26
	organic += cos(theta * 5.0 - direction.y * 1.7) * noise * 0.18
	var position := direction * radius * organic
	position.x *= 1.04
	position.y *= 0.96
	position.z *= 1.02
	return position


func _shape_local_position(position: Vector3) -> Vector3:
	position.x *= 1.04
	position.y *= 0.96
	position.z *= 1.02
	return position.limit_length(ORB_RADIUS * 1.10)


func _random_unit_vector() -> Vector3:
	var theta := _rng.randf() * TAU
	var z := _rng.randf_range(-1.0, 1.0)
	var r := sqrt(maxf(0.0, 1.0 - z * z))
	return Vector3(r * cos(theta), r * sin(theta), z)


func _balanced_depth_light(position: Vector3, minimum: float, maximum: float) -> float:
	var radius_t := clampf(position.length() / ORB_RADIUS, 0.0, 1.0)
	var core_fill := pow(1.0 - radius_t, 1.6)
	var shell_fill := pow(radius_t, 0.85)
	var neutral_fill := clampf(0.64 + core_fill * 0.22 + shell_fill * 0.24, 0.0, 1.0)
	return lerpf(minimum, maximum, neutral_fill)


func _generate_connections() -> void:
	_connections.clear()
	_seen_connection_keys.clear()
	_route_connection_indices.clear()
	_node_connections.clear()
	for index in range(_nodes.size()):
		_node_connections.append([])
	for index in range(_nodes.size()):
		var node := _nodes[index]
		var ranked: Array[Dictionary] = []
		for other_index in range(_nodes.size()):
			if other_index == index:
				continue
			var distance := node.base_position.distance_to(_nodes[other_index].base_position)
			var max_distance := HUB_CONNECTION_DISTANCE if node.hub else MAX_CONNECTION_DISTANCE
			if distance <= max_distance:
				ranked.append({ "index": other_index, "distance": distance })

		ranked.sort_custom(func(left, right): return left["distance"] < right["distance"])
		var radius_t := clampf(node.base_position.length() / ORB_RADIUS, 0.0, 1.0)
		var neighbor_min := 8 if radius_t < 0.72 else 9
		var neighbor_max := 10 if radius_t < 0.72 else 11
		var count := mini((11 if node.hub else _rng.randi_range(neighbor_min, neighbor_max)), ranked.size())
		for slot in range(count):
			_add_connection(index, int(ranked[slot]["index"]), float(ranked[slot]["distance"]))

	for index in range(STRUCTURAL_NODE_COUNT, _nodes.size()):
		for other_index in range(index + 1, _nodes.size()):
			var distance := _nodes[index].base_position.distance_to(_nodes[other_index].base_position)
			if distance <= HUB_CONNECTION_DISTANCE * 0.92 and _rng.randf() < 0.10:
				_add_connection(index, other_index, distance, true)


func _add_connection(a: int, b: int, distance: float, force_route: bool = false) -> void:
	var low := mini(a, b)
	var high := maxi(a, b)
	var key := "%s:%s" % [low, high]
	if _seen_connection_keys.has(key):
		return
	_seen_connection_keys[key] = true

	var connection := OrganismConnection.new()
	connection.a = low
	connection.b = high
	connection.phase = _rng.randf() * TAU
	var midpoint := (_nodes[low].base_position + _nodes[high].base_position) * 0.5
	var center_t := 1.0 - clampf(midpoint.length() / ORB_RADIUS, 0.0, 1.0)
	var hub_t := 1.0 if _nodes[low].hub or _nodes[high].hub else 0.0
	var closeness := 1.0 - clampf(distance / HUB_CONNECTION_DISTANCE, 0.0, 1.0)
	var shell_t := clampf(midpoint.length() / ORB_RADIUS, 0.0, 1.0)
	var shell_band := pow(clampf((shell_t - 0.52) / 0.48, 0.0, 1.0), 0.78)
	connection.route = force_route or hub_t > 0.0 and closeness > 0.24 and _rng.randf() < 0.16
	connection.base_alpha = 0.150 + closeness * 0.24 + pow(center_t, 0.7) * 0.105 + shell_band * 0.110 + hub_t * 0.035
	if connection.route:
		connection.base_alpha += 0.058 + hub_t * 0.020
	connection.width = lerpf(0.0028, 0.0049, pow(center_t, 0.7)) + shell_band * 0.00085 + hub_t * 0.00022
	if connection.route:
		connection.width *= 1.18
	var connection_index := _connections.size()
	_connections.append(connection)
	var a_connections: Array = _node_connections[connection.a]
	var b_connections: Array = _node_connections[connection.b]
	a_connections.append(connection_index)
	b_connections.append(connection_index)
	if connection.route:
		_route_connection_indices.append(connection_index)


var _seen_connection_keys := {}


func _build_node_dust_indices() -> void:
	_node_dust_indices.clear()
	for index in range(_nodes.size()):
		_node_dust_indices.append([])
	for index in range(_dust.size()):
		var source_node: int = _dust[index].source_node
		if source_node >= 0 and source_node < _node_dust_indices.size():
			var dust_indices: Array = _node_dust_indices[source_node]
			dust_indices.append(index)


func _setup_multimeshes() -> void:
	_node_multimesh.multimesh = _new_multimesh(_node_mesh(1.0), STRUCTURAL_NODE_COUNT)
	_hub_multimesh.multimesh = _new_multimesh(_node_mesh(1.0), HUB_NODE_COUNT)
	_dust_multimesh.multimesh = _new_multimesh(_node_mesh(1.0), DUST_NODE_COUNT)
	_pulse_multimesh.multimesh = _new_multimesh(_node_mesh(1.0), MAX_PULSES)
	_core_multimesh.multimesh = _new_multimesh(_node_mesh(1.0), 1)


func _node_mesh(radius: float) -> SphereMesh:
	var mesh := SphereMesh.new()
	mesh.radius = radius
	mesh.height = radius * 2.0
	mesh.radial_segments = 8
	mesh.rings = 4
	return mesh


func _new_multimesh(mesh: Mesh, count: int) -> MultiMesh:
	var multimesh := MultiMesh.new()
	multimesh.transform_format = MultiMesh.TRANSFORM_3D
	multimesh.use_colors = true
	multimesh.mesh = mesh
	multimesh.instance_count = count
	return multimesh


func _process(delta: float) -> void:
	_time += delta
	_activity = lerpf(_activity, _target_activity, minf(delta * 2.6, 1.0))
	_brightness = lerpf(_brightness, _target_brightness, minf(delta * 2.6, 1.0))
	_palette = _lerp_palette(_palette, _target_palette, minf(delta * 3.2, 1.0))
	_target_activity = lerpf(_target_activity, _state_activity_floor(), minf(delta * 0.9, 1.0))
	_target_brightness = lerpf(_target_brightness, _state_brightness_floor(), minf(delta * 0.9, 1.0))
	_update_autonomous_speech_motion(delta)
	_speech_global_morph = move_toward(_speech_global_morph, 0.0, delta * 2.4)
	_update_speech_region_axes(delta)

	var breath := 1.0 + sin(_time * 0.82) * (0.020 + _activity * 0.012)
	_graph_root.scale = Vector3.ONE * ORGANISM_DISPLAY_SCALE * breath
	_rotation_y += delta * 0.054
	_graph_root.rotation.y = _rotation_y
	_graph_root.rotation.x = sin(_time * 0.21) * 0.055
	_graph_root.rotation.z = sin(_time * 0.13) * 0.020

	_update_pulses(delta)
	_update_nodes(delta)
	_update_dust(delta)
	_update_connections()
	_update_core_glow()
	_update_materials()


func _state_activity_floor() -> float:
	match current_state:
		OrganismState.THINKING:
			return 0.42
		OrganismState.SPEAKING:
			return 0.58
		OrganismState.EXECUTING:
			return 0.50
		OrganismState.ERROR:
			return 0.46
		OrganismState.CONFIRMATION:
			return 0.22
		_:
			return 0.18


func _state_brightness_floor() -> float:
	match current_state:
		OrganismState.SPEAKING:
			return 1.00
		OrganismState.THINKING:
			return 1.12
		OrganismState.EXECUTING:
			return 1.18
		OrganismState.ERROR:
			return 1.16
		_:
			return 1.0


func _lerp_palette(from_palette: Dictionary, to_palette: Dictionary, amount: float) -> Dictionary:
	return {
		"hot": Color(from_palette["hot"]).lerp(Color(to_palette["hot"]), amount),
		"node": Color(from_palette["node"]).lerp(Color(to_palette["node"]), amount),
		"line": Color(from_palette["line"]).lerp(Color(to_palette["line"]), amount),
		"dim": Color(from_palette["dim"]).lerp(Color(to_palette["dim"]), amount),
		"dust": Color(from_palette["dust"]).lerp(Color(to_palette["dust"]), amount),
	}


func _update_autonomous_speech_motion(delta: float) -> void:
	if current_state != OrganismState.SPEAKING:
		_speech_energy = move_toward(_speech_energy, 0.0, delta * 2.4)
		return

	var voice_wave := 0.5 + sin(_time * 5.2 + sin(_time * 1.7) * 0.8) * 0.5
	var energy_target := 0.34 + voice_wave * 0.28
	_speech_energy = lerpf(_speech_energy, energy_target, minf(delta * 4.6, 1.0))

	_speech_region_timer -= delta
	if _speech_region_timer <= 0.0:
		_speech_region_timer = _rng.randf_range(0.85, 1.45)
		_pick_speech_region(_rng.randf())

	_speech_global_timer -= delta
	if _speech_global_timer <= 0.0:
		_speech_global_timer = _rng.randf_range(2.4, 4.2)
		_speech_global_morph = clampf(_speech_global_morph + _rng.randf_range(0.06, 0.16), 0.0, 0.34)


func _reset_speech_regions() -> void:
	var default_axis := Vector3(0.35, 0.82, 0.45).normalized()
	_speech_region_axes.clear()
	_speech_region_targets.clear()
	_speech_region_weights.clear()
	_speech_region_axes.append(default_axis)
	_speech_region_targets.append(default_axis)
	_speech_region_weights.append(1.0)


func _spawn_speech_ripple() -> void:
	if _speech_ripples.size() >= 18:
		_speech_ripples.remove_at(0)
	var ripple: SpeechRipple = SpeechRipple.new()
	var use_region := not _speech_region_axes.is_empty() and _rng.randf() > 0.30
	var axis := Vector3.UP
	if use_region:
		axis = _speech_region_axes[_rng.randi_range(0, _speech_region_axes.size() - 1)]
	else:
		axis = _speech_region_vector(_rng.randf(), 0, 1)
	ripple.axis = axis.normalized()
	ripple.phase = _rng.randf() * TAU
	ripple.duration = _rng.randf_range(0.26, 0.72)
	ripple.width = _rng.randf_range(0.18, 0.42)
	ripple.strength = _rng.randf_range(0.34, 0.82)
	ripple.spin = 1.0
	if _rng.randf() < 0.5:
		ripple.spin = -1.0
	_speech_ripples.append(ripple)


func _update_speech_ripples(delta: float) -> void:
	for index in range(_speech_ripples.size() - 1, -1, -1):
		var ripple: SpeechRipple = _speech_ripples[index]
		ripple.age += delta
		if ripple.age >= ripple.duration:
			_speech_ripples.remove_at(index)


func _update_speech_region_axes(delta: float) -> void:
	var amount := minf(delta * 4.6, 1.0)
	var count := mini(_speech_region_axes.size(), _speech_region_targets.size())
	for index in range(count):
		var axis: Vector3 = _speech_region_axes[index]
		var target: Vector3 = _speech_region_targets[index]
		_speech_region_axes[index] = axis.lerp(target, amount).normalized()


func _pick_speech_region(progress: float = -1.0) -> void:
	var roll := _rng.randf()
	var region_count := 2
	if roll > 0.95:
		region_count = 5
		_speech_global_morph = clampf(_speech_global_morph + _rng.randf_range(0.10, 0.20), 0.0, 0.68)
	elif roll > 0.82:
		region_count = 4
	elif roll > 0.58:
		region_count = 3
	elif roll > 0.30:
		region_count = 2

	_speech_region_targets.clear()
	_speech_region_weights.clear()
	for index in range(region_count):
		_speech_region_targets.append(_speech_region_vector(progress, index, region_count))
		_speech_region_weights.append(_rng.randf_range(0.72, 1.0))

	while _speech_region_axes.size() < region_count:
		_speech_region_axes.append(_speech_region_targets[_speech_region_axes.size()])
	while _speech_region_axes.size() > region_count:
		_speech_region_axes.remove_at(_speech_region_axes.size() - 1)


func _speech_region_vector(progress: float, index: int, count: int) -> Vector3:
	var base := progress
	if progress < 0.0:
		base = _rng.randf()
	var angle := base * TAU * 2.35 + float(index) * TAU / maxf(float(count), 1.0) + _rng.randf_range(-0.75, 0.75)
	var z := sin(angle * 0.47 + _time * 0.31 + float(index) * 0.63) * _rng.randf_range(0.40, 0.86)
	var r := sqrt(maxf(0.0, 1.0 - z * z))
	return Vector3(cos(angle) * r, sin(angle * 0.82 + 0.65) * r, z).normalized()


func _speech_influence(position: Vector3) -> float:
	if _speech_energy <= 0.001 and _speech_global_morph <= 0.001:
		return 0.0
	var fallback_axis := Vector3.UP
	if not _speech_region_axes.is_empty():
		fallback_axis = _speech_region_axes[0]
	var radial := fallback_axis
	if position.length_squared() > 0.001:
		radial = position.normalized()
	var regional := 0.0
	for index in range(_speech_region_axes.size()):
		var weight := 1.0
		if index < _speech_region_weights.size():
			weight = _speech_region_weights[index]
		var axis: Vector3 = _speech_region_axes[index]
		regional += pow(clampf(radial.dot(axis) * 0.5 + 0.5, 0.0, 1.0), 12.0) * weight
	regional = clampf(regional, 0.0, 1.05) * _speech_energy
	var center_t := 1.0 - clampf(position.length() / ORB_RADIUS, 0.0, 1.0)
	return clampf(regional * 0.92 + _speech_global_morph * (0.10 + center_t * 0.18), 0.0, 1.0)


func _speech_ripple_influence(radial: Vector3) -> float:
	var ripple_signal := 0.0
	for index in range(_speech_ripples.size()):
		var ripple: SpeechRipple = _speech_ripples[index]
		var life := clampf(ripple.age / maxf(ripple.duration, 0.001), 0.0, 1.0)
		var envelope := sin(life * PI)
		var angular_distance := 1.0 - clampf(radial.dot(ripple.axis) * 0.5 + 0.5, 0.0, 1.0)
		var wave_center := life * 0.72
		var distance_t := (angular_distance - wave_center) / ripple.width
		var band := pow(2.7182818, -distance_t * distance_t)
		var chatter := 0.70 + sin(_time * 18.0 + ripple.phase) * 0.30
		ripple_signal += band * envelope * chatter * ripple.strength * _speech_energy
	return clampf(ripple_signal, 0.0, 1.0)


func _speech_morph_offset(position: Vector3, phase: float, amount_scale: float) -> Vector3:
	var influence := _speech_influence(position)
	if influence <= 0.001:
		return Vector3.ZERO
	var fallback_axis := Vector3.UP
	if not _speech_region_axes.is_empty():
		fallback_axis = _speech_region_axes[0]
	var radial := fallback_axis
	if position.length_squared() > 0.001:
		radial = position.normalized()
	var tangent := _speech_tangent_axis(radial)
	if tangent.length_squared() < 0.001:
		tangent = radial.cross(Vector3.UP)
	if tangent.length_squared() < 0.001:
		tangent = Vector3.RIGHT
	tangent = tangent.normalized()
	var wave := sin(_time * 4.2 + phase * 1.7)
	var smooth_wave := signf(wave) * pow(absf(wave), 0.72)
	var regional_push := radial * influence * smooth_wave * 0.145
	var lateral_pull := tangent * influence * sin(_time * 4.8 + phase) * 0.018
	return (regional_push + lateral_pull) * amount_scale


func _speech_tangent_axis(radial: Vector3) -> Vector3:
	var tangent := Vector3.ZERO
	for index in range(_speech_region_axes.size()):
		var weight := 1.0
		if index < _speech_region_weights.size():
			weight = _speech_region_weights[index]
		var axis: Vector3 = _speech_region_axes[index]
		var local := axis.cross(radial)
		if local.length_squared() > 0.001:
			tangent += local.normalized() * weight
	return tangent


func _node_light(index: int) -> float:
	return float(_node_speech_light.get(index, 0.0))


func _dust_light(index: int) -> float:
	return float(_dust_speech_light.get(index, 0.0))


func _connection_light(index: int) -> float:
	return float(_connection_speech_light.get(index, 0.0))


func _update_nodes(_delta: float) -> void:
	var node_index := 0
	var hub_index := 0
	for index in range(_nodes.size()):
		var node := _nodes[index]
		var drift_scale := 0.030 + _activity * 0.045
		var drift := Vector3(
			sin(_time * 0.37 + node.phase),
			cos(_time * 0.29 + node.phase * 1.37),
			sin(_time * 0.23 + node.phase * 0.73)
		) * drift_scale
		var radial := node.base_position.normalized() if node.base_position.length_squared() > 0.001 else Vector3.UP
		var local_breath := radial * sin(_time * 0.56 + node.phase) * (0.012 + _activity * 0.018)
		var speech_morph: Vector3 = _speech_morph_offset(node.base_position, node.phase, 1.0) if current_state == OrganismState.SPEAKING else Vector3.ZERO
		node.current_position = node.base_position + drift + local_breath + speech_morph

		var center_t := 1.0 - clampf(node.current_position.length() / ORB_RADIUS, 0.0, 1.0)
		var depth_t := _balanced_depth_light(node.current_position, 0.94, 1.16)
		var pulse := 0.0
		if node.hub:
			pulse = pow(maxf(0.0, sin(_time * (0.95 + node.phase * 0.02) + node.phase)), 8.0) * 0.75
		var speech_light := _node_light(index)
		var shell_t := clampf(node.current_position.length() / ORB_RADIUS, 0.0, 1.0)
		var shell_light := pow(clampf((shell_t - 0.58) / 0.42, 0.0, 1.0), 0.9) * 0.28
		var alpha := clampf((0.38 + center_t * 0.34 + shell_light + pulse * 0.18 + speech_light * 0.62) * depth_t * _brightness, 0.16, 1.0)
		var color := Color(_palette["node"]).lerp(Color(_palette["hot"]), clampf(center_t * 0.48 + shell_light * 0.9 + pulse + speech_light * 0.72, 0.0, 1.0))
		color.a = alpha
		var scale := node.radius * (1.0 + pulse * 0.14 + speech_light * 0.42)
		var transform := Transform3D(Basis().scaled(Vector3.ONE * scale), node.current_position)
		if node.hub:
			_hub_multimesh.multimesh.set_instance_transform(hub_index, transform)
			_hub_multimesh.multimesh.set_instance_color(hub_index, color)
			hub_index += 1
		else:
			_node_multimesh.multimesh.set_instance_transform(node_index, transform)
			_node_multimesh.multimesh.set_instance_color(node_index, color)
			node_index += 1


func _update_dust(_delta: float) -> void:
	for index in range(_dust.size()):
		var dust := _dust[index]
		var orbit := Vector3(
			cos(_time * 0.055 + dust.phase),
			sin(_time * 0.071 + dust.phase * 1.2),
			cos(_time * 0.043 + dust.phase * 0.7)
		) * 0.045
		var can_morph: bool = dust.hub or dust.source_node >= 0
		var speech_morph: Vector3 = _speech_morph_offset(dust.base_position, dust.phase, 0.52) if current_state == OrganismState.SPEAKING and can_morph else Vector3.ZERO
		dust.current_position = dust.base_position + orbit + speech_morph
		var depth_t := _balanced_depth_light(dust.current_position, 0.82, 1.06)
		var cluster_pulse := 0.0
		if dust.hub:
			cluster_pulse = pow(maxf(0.0, sin(_time * 1.05 + dust.phase)), 5.0) * 0.45
		var speech_light := _dust_light(index)
		var color := Color(_palette["dust"]).lerp(Color(_palette["hot"]), 0.78 if dust.hub else 0.0)
		color = color.lerp(Color(_palette["hot"]), clampf(speech_light * 0.85, 0.0, 0.75))
		color.a = dust.brightness * depth_t * (0.76 + cluster_pulse * 0.30 + speech_light * 0.72 if dust.hub else 0.24 + speech_light * 0.18)
		var radius := dust.radius * (1.0 + cluster_pulse * 0.38 + speech_light * 0.55)
		_dust_multimesh.multimesh.set_instance_transform(index, Transform3D(Basis().scaled(Vector3.ONE * radius), dust.current_position))
		_dust_multimesh.multimesh.set_instance_color(index, color)


func _update_connections() -> void:
	var core_vertices := PackedVector3Array()
	var core_colors := PackedColorArray()
	var core_indices := PackedInt32Array()
	var glow_vertices := PackedVector3Array()
	var glow_colors := PackedColorArray()
	var glow_indices := PackedInt32Array()
	var local_camera_forward := _graph_root.global_transform.basis.inverse() * _camera.global_transform.basis.z
	var local_camera_up := _graph_root.global_transform.basis.inverse() * _camera.global_transform.basis.y
	local_camera_forward = local_camera_forward.normalized()
	local_camera_up = local_camera_up.normalized()

	for connection_index in range(_connections.size()):
		var connection: OrganismConnection = _connections[connection_index]
		var a := _nodes[connection.a]
		var b := _nodes[connection.b]
		var midpoint := (a.current_position + b.current_position) * 0.5
		var center_t := 1.0 - clampf(midpoint.length() / ORB_RADIUS, 0.0, 1.0)
		var depth_t := _balanced_depth_light(midpoint, 0.90, 1.14)
		var route_t := 1.0 if connection.route else 0.0
		var shell_t := clampf(midpoint.length() / ORB_RADIUS, 0.0, 1.0)
		var shell_band := pow(clampf((shell_t - 0.52) / 0.48, 0.0, 1.0), 0.72)
		var flicker := 0.84 + pow(maxf(0.0, sin(_time * 1.2 + connection.phase)), 3.2) * 0.18
		var speech_light := _connection_light(connection_index)
		var route_boost := 1.08 + route_t * 0.18
		var alpha := clampf(connection.base_alpha * flicker * depth_t * route_boost * _brightness * (1.0 + _activity * 0.08 + speech_light * 1.15), 0.075, 0.88)
		var color := Color(_palette["dim"]).lerp(Color(_palette["line"]), clampf(0.36 + center_t * 0.20 + shell_band * 0.26 + route_t * 0.14, 0.0, 1.0))
		color = color.lerp(Color(_palette["hot"]), clampf(center_t * 0.13 + shell_band * 0.10 + route_t * 0.14 + speech_light * 0.68, 0.0, 0.72))
		color.a = alpha
		var glow_color := color
		glow_color.a = alpha * (0.040 + center_t * 0.018 + shell_band * 0.028 + route_t * 0.030 + speech_light * 0.090)
		_add_line_quad_3d(glow_vertices, glow_colors, glow_indices, a.current_position, b.current_position, connection.width * (1.75 + route_t * 0.40 + speech_light * 1.35), glow_color, local_camera_forward, local_camera_up)
		_add_line_quad_3d(core_vertices, core_colors, core_indices, a.current_position, b.current_position, connection.width * (1.08 + route_t * 0.12 + speech_light * 0.74), color, local_camera_forward, local_camera_up)

	_add_traveling_light_segments(core_vertices, core_colors, core_indices, glow_vertices, glow_colors, glow_indices, local_camera_forward, local_camera_up)

	_line_mesh_instance.mesh = _mesh_from_arrays(core_vertices, core_colors, core_indices)
	_glow_line_mesh_instance.mesh = _mesh_from_arrays(glow_vertices, glow_colors, glow_indices)


func _add_traveling_light_segments(
	core_vertices: PackedVector3Array,
	core_colors: PackedColorArray,
	core_indices: PackedInt32Array,
	glow_vertices: PackedVector3Array,
	glow_colors: PackedColorArray,
	glow_indices: PackedInt32Array,
	local_camera_forward: Vector3,
	local_camera_up: Vector3
) -> void:
	for index in range(_pulses.size()):
		var pulse: EnergyPulse = _pulses[index]
		var connection: OrganismConnection = _connections[pulse.connection_index]
		var from_position: Vector3 = _nodes[pulse.from_node].current_position
		var to_position: Vector3 = _nodes[pulse.to_node].current_position
		var head := clampf(pulse.progress, 0.0, 1.0)
		var tail := clampf(head - pulse.tail, 0.0, 1.0)
		if head <= 0.01:
			continue
		var fade := sin(head * PI)
		var brightness := clampf((0.55 + fade * 0.86) * pulse.brightness, 0.0, 1.18)
		var segment_count := 4
		for segment_index in range(segment_count):
			var start_t := lerpf(tail, head, float(segment_index) / float(segment_count))
			var end_t := lerpf(tail, head, float(segment_index + 1) / float(segment_count))
			var segment_t := float(segment_index + 1) / float(segment_count)
			var segment_light := brightness * pow(segment_t, 1.6)
			var start_position := from_position.lerp(to_position, start_t)
			var end_position := from_position.lerp(to_position, end_t)
			var core_color := Color(_palette["hot"])
			core_color.a = segment_light * 0.76
			var glow_color := Color(_palette["hot"])
			glow_color.a = segment_light * 0.30
			_add_line_quad_3d(glow_vertices, glow_colors, glow_indices, start_position, end_position, connection.width * (3.2 + segment_t * 2.2), glow_color, local_camera_forward, local_camera_up)
			_add_line_quad_3d(core_vertices, core_colors, core_indices, start_position, end_position, connection.width * (1.4 + segment_t * 1.4), core_color, local_camera_forward, local_camera_up)


func _add_line_quad_3d(
	vertices: PackedVector3Array,
	colors: PackedColorArray,
	indices: PackedInt32Array,
	a: Vector3,
	b: Vector3,
	width: float,
	color: Color,
	local_camera_forward: Vector3,
	local_camera_up: Vector3
) -> void:
	var direction := b - a
	if direction.length_squared() < 0.0001:
		return
	var view_axis := local_camera_forward
	if view_axis.length_squared() < 0.0001:
		view_axis = Vector3.FORWARD
	var normal := direction.cross(view_axis)
	if normal.length_squared() < 0.0001:
		normal = direction.cross(local_camera_up)
	if normal.length_squared() < 0.0001:
		normal = direction.cross(Vector3.UP)
	normal = normal.normalized() * width
	var start := vertices.size()
	vertices.append(a - normal)
	vertices.append(a + normal)
	vertices.append(b + normal)
	vertices.append(b - normal)
	for _i in range(4):
		colors.append(color)
	indices.append(start)
	indices.append(start + 1)
	indices.append(start + 2)
	indices.append(start)
	indices.append(start + 2)
	indices.append(start + 3)


func _mesh_from_arrays(vertices: PackedVector3Array, colors: PackedColorArray, indices: PackedInt32Array) -> ArrayMesh:
	var mesh := ArrayMesh.new()
	if vertices.is_empty():
		return mesh
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_COLOR] = colors
	arrays[Mesh.ARRAY_INDEX] = indices
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh


func _update_pulses(delta: float) -> void:
	_pulse_timer -= delta
	if _pulse_timer <= 0.0:
		if current_state == OrganismState.THINKING:
			_pulse_timer = _rng.randf_range(0.34, 0.68)
			_spawn_energy_burst(_rng.randi_range(4, 6))
		else:
			_pulse_timer = _rng.randf_range(0.18, 0.48)

	for index in range(_pulses.size() - 1, -1, -1):
		var pulse: EnergyPulse = _pulses[index]
		pulse.progress += pulse.speed * delta
		if pulse.progress >= 1.0:
			_arrive_energy_pulse(pulse)
			_pulses.remove_at(index)

	for index in range(_destination_flashes.size() - 1, -1, -1):
		var flash: DestinationFlash = _destination_flashes[index]
		flash.age += delta
		if flash.age >= flash.duration:
			_destination_flashes.remove_at(index)

	for index in range(MAX_PULSES):
		if index >= _pulses.size():
			_pulse_multimesh.multimesh.set_instance_transform(index, Transform3D(Basis().scaled(Vector3.ZERO), Vector3.ZERO))
			_pulse_multimesh.multimesh.set_instance_color(index, Color(0, 0, 0, 0))
			continue
		var pulse: EnergyPulse = _pulses[index]
		var position := _pulse_position(pulse)
		var fade := sin(pulse.progress * PI)
		var radius := 0.010 + pulse.brightness * 0.013
		var color := Color(_palette["hot"])
		color.a = clampf(fade * pulse.brightness * 0.86, 0.0, 1.0)
		_pulse_multimesh.multimesh.set_instance_transform(index, Transform3D(Basis().scaled(Vector3.ONE * radius), position))
		_pulse_multimesh.multimesh.set_instance_color(index, color)
	_rebuild_speech_light_maps()


func _rebuild_speech_light_maps() -> void:
	_node_speech_light.clear()
	_dust_speech_light.clear()
	_connection_speech_light.clear()
	if current_state != OrganismState.THINKING:
		return
	for index in range(_destination_flashes.size()):
		var flash: DestinationFlash = _destination_flashes[index]
		var life := clampf(flash.age / maxf(flash.duration, 0.001), 0.0, 1.0)
		var amount := pow(1.0 - life, 1.8) * flash.brightness
		_add_node_region_light(flash.node_index, amount * 2.10)
		_add_neighbor_region_light(flash.node_index, -1, amount * 0.42, 4)
	for index in range(_pulses.size()):
		var pulse: EnergyPulse = _pulses[index]
		var head := clampf(pulse.progress, 0.0, 1.0)
		var fade := sin(head * PI)
		var amount := fade * pulse.brightness
		if amount <= 0.015:
			continue
		_add_node_region_light(pulse.from_node, amount * maxf(0.0, 1.0 - head * 1.45) * 0.46)
		_add_node_region_light(pulse.to_node, amount * pow(head, 2.0) * 0.72)


func _add_connection_region_light(connection_index: int, amount: float) -> void:
	if connection_index < 0 or connection_index >= _connections.size():
		return
	_connection_speech_light[connection_index] = maxf(float(_connection_speech_light.get(connection_index, 0.0)), amount)


func _add_node_region_light(node_index: int, amount: float) -> void:
	if node_index < 0 or node_index >= _nodes.size() or amount <= 0.0:
		return
	_node_speech_light[node_index] = maxf(float(_node_speech_light.get(node_index, 0.0)), amount)
	if node_index >= _node_dust_indices.size():
		return
	var dust_indices: Array = _node_dust_indices[node_index]
	var limit := mini(dust_indices.size(), 28)
	for index in range(limit):
		var dust_index: int = dust_indices[index]
		var local_amount := amount * lerpf(0.92, 0.48, float(index) / maxf(float(limit), 1.0))
		_dust_speech_light[dust_index] = maxf(float(_dust_speech_light.get(dust_index, 0.0)), local_amount)


func _add_neighbor_region_light(node_index: int, previous_node: int, amount: float, limit: int) -> void:
	if node_index < 0 or node_index >= _node_connections.size() or amount <= 0.0:
		return
	var connection_indices: Array = _node_connections[node_index]
	var added := 0
	for index in range(connection_indices.size()):
		if added >= limit:
			return
		var connection_index: int = connection_indices[index]
		var connection: OrganismConnection = _connections[connection_index]
		var other: int = connection.b if connection.a == node_index else connection.a
		if other == previous_node:
			continue
		var connection_amount := amount * (1.12 if connection.route else 0.62)
		_add_connection_region_light(connection_index, connection_amount)
		_add_node_region_light(other, amount * 0.58)
		added += 1


func _pulse_position(pulse: EnergyPulse) -> Vector3:
	var connection: OrganismConnection = _connections[pulse.connection_index]
	var from_index := pulse.from_node
	var to_index := pulse.to_node
	if from_index < 0 or to_index < 0:
		from_index = connection.a
		to_index = connection.b
	var a: Vector3 = _nodes[from_index].current_position
	var b: Vector3 = _nodes[to_index].current_position
	return a.lerp(b, clampf(pulse.progress, 0.0, 1.0))


func _spawn_energy_pulse() -> void:
	if _cluster_node_indices.is_empty() or _pulses.size() >= MAX_PULSES:
		return
	var start_node: int = _cluster_node_indices[_rng.randi_range(0, _cluster_node_indices.size() - 1)]
	var candidates: Array[int] = _connected_activity_connections(start_node, -1)
	if candidates.is_empty():
		return
	_add_destination_flash(start_node, 0.72)
	var launch_count := 2 if start_node == STRUCTURAL_NODE_COUNT or _rng.randf() < 0.38 else 1
	for index in range(mini(launch_count, candidates.size())):
		var connection_index: int = candidates[index]
		var connection: OrganismConnection = _connections[connection_index]
		var next_node: int = connection.b if connection.a == start_node else connection.a
		_add_energy_pulse(connection_index, start_node, next_node, _rng.randf_range(0.70, 0.96), 0, true)


func _spawn_energy_burst(count: int) -> void:
	var launch_limit: int = mini(count, MAX_PULSES - _pulses.size())
	for index in range(launch_limit):
		_spawn_energy_pulse()


func _arrive_energy_pulse(pulse: EnergyPulse) -> void:
	if current_state != OrganismState.THINKING:
		return
	_add_destination_flash(pulse.to_node, pulse.brightness)
	if pulse.generation >= 2 or _pulses.size() >= MAX_PULSES:
		return
	var branch_count := 1
	if pulse.generation == 0 and _rng.randf() < _rng.randf_range(0.66, 0.78):
		branch_count = 2
		if _rng.randf() < 0.24:
			branch_count = 3
	elif pulse.generation == 1 and pulse.can_split and _rng.randf() < 0.72:
		branch_count = 2
	var candidates: Array[int] = _connected_activity_connections(pulse.to_node, pulse.from_node)
	for branch_index in range(mini(branch_count, candidates.size())):
		if _pulses.size() >= MAX_PULSES:
			return
		var candidate_index: int = candidates[branch_index]
		var connection: OrganismConnection = _connections[candidate_index]
		var next_node: int = connection.b if connection.a == pulse.to_node else connection.a
		var next_brightness := pulse.brightness * _rng.randf_range(0.72, 0.94)
		if next_brightness < 0.22:
			continue
		var child_can_split := false
		if pulse.generation == 0 and branch_count > 1 and branch_index == 0:
			child_can_split = true
		elif pulse.generation == 0 and branch_count == 1:
			child_can_split = true
		_add_energy_pulse(candidate_index, pulse.to_node, next_node, next_brightness, pulse.generation + 1, child_can_split)


func _connected_activity_connections(node_index: int, previous_node: int) -> Array[int]:
	var preferred: Array[int] = []
	var fallback: Array[int] = []
	if node_index < 0 or node_index >= _node_connections.size():
		return preferred
	var connection_indices: Array = _node_connections[node_index]
	for index in range(connection_indices.size()):
		var connection_index: int = connection_indices[index]
		var connection: OrganismConnection = _connections[connection_index]
		if connection.a != node_index and connection.b != node_index:
			continue
		var other: int = connection.b if connection.a == node_index else connection.a
		if other == previous_node:
			continue
		if connection.route:
			preferred.append(connection_index)
		elif _nodes[other].hub or _rng.randf() > 0.68:
			fallback.append(connection_index)
	preferred.shuffle()
	fallback.shuffle()
	for index in range(fallback.size()):
		preferred.append(fallback[index])
	return preferred


func _add_destination_flash(node_index: int, brightness: float) -> void:
	if node_index < 0 or node_index >= _nodes.size():
		return
	if _destination_flashes.size() >= MAX_DESTINATION_FLASHES:
		_destination_flashes.remove_at(0)
	var flash := DestinationFlash.new()
	flash.node_index = node_index
	flash.age = 0.0
	flash.duration = _rng.randf_range(0.18, 0.34)
	flash.brightness = brightness
	_destination_flashes.append(flash)


func _add_energy_pulse(connection_index: int, from_node: int, to_node: int, brightness: float, generation: int, can_split: bool) -> void:
	if _pulses.size() >= MAX_PULSES:
		return
	var pulse := EnergyPulse.new()
	pulse.connection_index = connection_index
	pulse.from_node = from_node
	pulse.to_node = to_node
	pulse.progress = 0.0
	pulse.speed = _rng.randf_range(0.50, 0.86) * (1.0 + _activity * 0.18)
	pulse.brightness = brightness
	pulse.generation = generation
	pulse.can_split = can_split
	pulse.tail = _rng.randf_range(0.18, 0.28)
	_pulses.append(pulse)


func _update_core_glow() -> void:
	var pulse := 0.5 + sin(_time * 1.05) * 0.5
	var radius := 0.28 + pulse * 0.070 + _activity * 0.085
	var color := Color(_palette["hot"])
	color.a = clampf((0.56 + pulse * 0.22) * _brightness, 0.0, 0.92)
	_core_multimesh.multimesh.set_instance_transform(0, Transform3D(Basis().scaled(Vector3.ONE * radius), Vector3.ZERO))
	_core_multimesh.multimesh.set_instance_color(0, color)


func _update_materials() -> void:
	_update_material_color(_line_material, _palette["line"], 1.0)
	_update_material_color(_glow_line_material, _palette["line"], 0.10)
	_update_material_color(_node_material, _palette["node"], 1.0)
	_update_material_color(_hub_material, _palette["hot"], 1.0)
	_update_material_color(_dust_material, _palette["dust"], 0.24)
	_update_material_color(_pulse_material, _palette["hot"], 1.0)
	_update_material_color(_core_material, _palette["hot"], 0.32)


func _update_material_color(material: StandardMaterial3D, color: Color, alpha: float) -> void:
	if material == null:
		return
	material.albedo_color = Color(color.r, color.g, color.b, alpha)
	material.emission = color
