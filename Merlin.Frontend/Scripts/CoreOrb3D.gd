extends Control
class_name CoreOrb3D

signal state_changed(state)

enum CloudState { IDLE, THINKING, SPEAKING, EXECUTING, ERROR, CONFIRMATION }

class CloudParticle:
	var id := 0
	var seed := 0.0
	var base_position := Vector3.ZERO
	var current_position := Vector3.ZERO
	var projected_position := Vector2.ZERO
	var size := 1.0
	var base_brightness := 0.5
	var brightness := 0.5
	var depth := 0.0
	var layer := 0
	var cluster_id := -1
	var cluster := false
	var dust := false
	var density_weight := 0.0
	var cluster_weight := 1.0
	var noise_offset := Vector3.ZERO
	var pulse := 0.0


class CloudLink:
	var a := 0
	var b := 0
	var base_opacity := 0.0
	var opacity := 0.0
	var phase := 0.0
	var speed := 1.0
	var active_bias := 0.0
	var structural := false


class CloudBridge:
	var a_cluster := 0
	var b_cluster := 0
	var phase := 0.0
	var speed := 1.0
	var strength := 1.0
	var width := 1.0
	var curve_side := 1.0
	var lane_offset := 0.0
	var stack_count := 1


class SpeechMorphField:
	var axis := Vector3.RIGHT
	var center_dir := Vector3.RIGHT
	var depth_axis := Vector3.FORWARD
	var radius_center := 0.5
	var radius_width := 0.35
	var strength := 1.0
	var age := 0.0
	var lifetime := 0.45
	var twist := 0.0
	var pull := false
	var dramatic := false


class SpeechGlobalShift:
	var axis := Vector3.RIGHT
	var cross_axis := Vector3.UP
	var depth_axis := Vector3.FORWARD
	var age := 0.0
	var lifetime := 1.0
	var strength := 1.0
	var twist := 0.0
	var compression := 0.0


const CORE_PARTICLES := 2800
const FIELD_PARTICLES := 1700
const DUST_PARTICLES := 650
const PARTICLE_COUNT := CORE_PARTICLES + FIELD_PARTICLES + DUST_PARTICLES
const CLUSTER_COUNT := 28
const MAX_LINKS := 6200
const CLOUD_RADIUS := 165.0
const FIELD_RADIUS := 190.0
const DUST_RADIUS := 205.0
const VISUAL_SCALE := 1.95
const MAX_SPARKS := 520
const LINK_CELL_SIZE := 76.0
const LINK_DISTANCE_CORE := 92.0
const LINK_DISTANCE_FIELD := 116.0
const LINK_DISTANCE_BRIDGE := 168.0
const MAX_LINKS_PER_PARTICLE := 2
const BRIDGE_STRIDE := 6
const STRUCTURAL_LINKS_PER_CLUSTER := 3
const SPEECH_ENERGY_ADD := 0.22
const SPEECH_ENERGY_DECAY := 2.8
const SPEECH_MORPH_PROFILE_COUNT := 6
const SPEECH_AUTONOMOUS_INTERVAL_MIN := 0.070
const SPEECH_AUTONOMOUS_INTERVAL_MAX := 0.145
const SPEECH_GLOBAL_SHIFT_INTERVAL_MIN := 0.85
const SPEECH_GLOBAL_SHIFT_INTERVAL_MAX := 1.75
const SPEECH_ARTICULATION_DECAY := 5.8
const SPEECH_RANDOM_BLEND_SPEED := 6.2
const SPEECH_MAX_MORPH_FIELDS := 5
const SPEECH_PAUSE_DECAY := 3.6
const SPEAKING_LINGER_HOLD := 1.8
const SPEAKING_LINGER_FADE := 1.8
const THINKING_PULSE_INTERVAL_MIN := 0.65
const THINKING_PULSE_INTERVAL_MAX := 1.8

const CYAN := {
	"hot": Color("#E6FBFF"),
	"primary": Color("#5CCEFF"),
	"dim": Color("#0D5F8D"),
	"line": Color("#9DEBFF"),
	"line_dim": Color("#1B6FA3"),
	"glow": Color("#B8F4FF"),
}

const RED := {
	"hot": Color("#FF2222"),
	"primary": Color("#AA1111"),
	"dim": Color("#660A0A"),
	"line": Color("#FF3333"),
	"line_dim": Color("#660A0A"),
	"glow": Color("#FF2222"),
}

const AMBER := {
	"hot": Color("#FFAA22"),
	"primary": Color("#CC7700"),
	"dim": Color("#664400"),
	"line": Color("#FFBB33"),
	"line_dim": Color("#664400"),
	"glow": Color("#FFAA22"),
}

const STATE_PARAMS := {
	CloudState.IDLE: {
		"activity": 0.16,
		"drift_speed": 0.26,
		"brightness": 1.18,
		"glow": 0.72,
		"breath_speed": 0.34,
		"breath_depth": 0.035,
		"link_visibility": 0.72,
		"link_motion": 0.34,
	},
	CloudState.THINKING: {
		"activity": 0.36,
		"drift_speed": 0.52,
		"brightness": 1.42,
		"glow": 0.96,
		"breath_speed": 0.56,
		"breath_depth": 0.052,
		"link_visibility": 0.78,
		"link_motion": 1.15,
	},
	CloudState.SPEAKING: {
		"activity": 0.58,
		"drift_speed": 0.72,
		"brightness": 1.82,
		"glow": 1.30,
		"breath_speed": 0.92,
		"breath_depth": 0.0,
		"link_visibility": 1.08,
		"link_motion": 1.85,
	},
	CloudState.EXECUTING: {
		"activity": 0.50,
		"drift_speed": 0.78,
		"brightness": 1.55,
		"glow": 1.12,
		"breath_speed": 0.80,
		"breath_depth": 0.080,
		"link_visibility": 0.82,
		"link_motion": 1.45,
	},
	CloudState.ERROR: {
		"activity": 0.46,
		"drift_speed": 0.70,
		"brightness": 1.62,
		"glow": 1.35,
		"breath_speed": 0.82,
		"breath_depth": 0.070,
		"link_visibility": 0.72,
		"link_motion": 1.20,
	},
	CloudState.CONFIRMATION: {
		"activity": 0.18,
		"drift_speed": 0.24,
		"brightness": 1.18,
		"glow": 0.82,
		"breath_speed": 0.26,
		"breath_depth": 0.050,
		"link_visibility": 0.48,
		"link_motion": 0.38,
	},
}

var current_state := CloudState.IDLE
var _particles: Array[CloudParticle] = []
var _links: Array[CloudLink] = []
var _bridges: Array[CloudBridge] = []
var _cluster_anchors: Array[Vector3] = []
var _cluster_offsets: Array[Vector3] = []
var _cluster_projected_positions: Array[Vector2] = []
var _cluster_weights: Array[float] = []
var _cluster_particle_indices: Array = []
var _speech_particle_indices: Array[int] = []
var _params := {}
var _target_params := {}
var _palette := {}
var _target_palette := {}
var _rng := RandomNumberGenerator.new()
var _time := 0.0
var _center := Vector2.ZERO
var _speech_energy := 0.0
var _speaking_linger := 0.0
var _thinking_timer := 1.0
var _temporary_state_token := 0
var _shape_morph: float = 0.0
var _target_shape_morph: float = 0.0
var _speech_autonomous_timer := 0.0
var _speech_flow_phase := 0.0
var _speech_articulation := 0.0
var _speech_random_axis := Vector3.RIGHT
var _target_speech_random_axis := Vector3.RIGHT
var _speech_depth_axis := Vector3.FORWARD
var _target_speech_depth_axis := Vector3.FORWARD
var _speech_random_twist := 0.0
var _target_speech_random_twist := 0.0
var _speech_pause_weight := 0.0
var _speech_fields: Array = []
var _speech_global_shift: SpeechGlobalShift
var _speech_global_shift_timer := 0.0
var _particle_texture: ImageTexture
var _dust_mesh := ArrayMesh.new()
var _link_glow_mesh := ArrayMesh.new()
var _link_mesh := ArrayMesh.new()
var _spark_mesh := ArrayMesh.new()
var _aura_mesh := ArrayMesh.new()
var _density_mesh := ArrayMesh.new()
var _particle_mesh := ArrayMesh.new()
var _cluster_mesh := ArrayMesh.new()


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_rng.seed = 26062026
	_params = STATE_PARAMS[CloudState.IDLE].duplicate(true)
	_target_params = STATE_PARAMS[CloudState.IDLE].duplicate(true)
	_palette = CYAN.duplicate(true)
	_target_palette = CYAN.duplicate(true)
	_create_particle_texture()
	_create_particles()
	_create_links()
	_create_bridges()
	set_process(true)


func _process(delta: float) -> void:
	_time += delta
	_center = size * 0.5
	_update_linger(delta)
	_update_params(delta)
	_update_palette(delta)
	_update_speech_energy(delta)
	_update_speech_morph(delta)
	_update_shape_morph(delta)
	_update_clusters(delta)
	_update_particles(delta)
	_update_thinking(delta)
	_update_links(delta)
	_rebuild_render_meshes()
	queue_redraw()


func _draw() -> void:
	draw_mesh(_dust_mesh, _particle_texture)
	draw_mesh(_aura_mesh, _particle_texture)
	draw_mesh(_particle_mesh, _particle_texture)
	draw_mesh(_density_mesh, _particle_texture)
	draw_mesh(_link_glow_mesh, null)
	draw_mesh(_link_mesh, null)
	draw_mesh(_spark_mesh, _particle_texture)
	draw_mesh(_cluster_mesh, _particle_texture)


func set_idle() -> void:
	if current_state == CloudState.SPEAKING:
		_clear_speech_motion()
		_set_state(CloudState.IDLE, "idle")
		return

	_set_state(CloudState.IDLE, "idle")


func set_thinking() -> void:
	_set_state(CloudState.THINKING, "thinking")


func set_speaking() -> void:
	_set_state(CloudState.SPEAKING, "speaking")


func play_tool_execution(duration: float = 2.5) -> void:
	_temporary_state_token += 1
	var token := _temporary_state_token
	_set_state(CloudState.EXECUTING, "executing_tool")
	_speech_energy = maxf(_speech_energy, 0.45)
	await get_tree().create_timer(duration).timeout
	if token == _temporary_state_token and current_state == CloudState.EXECUTING:
		set_idle()


func play_error(_duration: float = 3.0) -> void:
	_temporary_state_token += 1
	_set_state(CloudState.ERROR, "error")
	_set_palette(RED)
	_speech_energy = maxf(_speech_energy, 0.65)


func play_confirmation() -> void:
	_set_state(CloudState.CONFIRMATION, "confirmation")
	_set_palette(AMBER)


func notify_speech_tick(_character: String = "", _delay: float = 0.0, _progress: float = 0.0) -> void:
	if current_state != CloudState.SPEAKING:
		return

	var intensity := 0.48
	_speech_energy = clampf(_speech_energy + SPEECH_ENERGY_ADD * intensity * 0.35, 0.0, 1.0)
	for particle_index in _speech_particle_indices:
		_particles[particle_index].pulse = maxf(_particles[particle_index].pulse, 0.18 + intensity * 0.12)


func _clear_speech_motion() -> void:
	_speaking_linger = 0.0
	_speech_energy = 0.0
	_speech_autonomous_timer = 0.0
	_speech_articulation = 0.0
	_speech_pause_weight = 0.0
	_speech_fields.clear()
	_speech_global_shift = null
	_speech_global_shift_timer = 0.0


func _set_state(state: int, label: String) -> void:
	current_state = state
	_speaking_linger = 0.0
	_target_params = STATE_PARAMS[state].duplicate(true)
	if state == CloudState.ERROR:
		_set_palette(RED)
	elif state == CloudState.CONFIRMATION:
		_set_palette(AMBER)
	else:
		_set_palette(CYAN)
	state_changed.emit(label)


func _create_particle_texture() -> void:
	var image := Image.create(32, 32, false, Image.FORMAT_RGBA8)
	var center := Vector2(15.5, 15.5)
	for y in range(32):
		for x in range(32):
			var distance := Vector2(x, y).distance_to(center) / 15.5
			var alpha := pow(maxf(0.0, 1.0 - distance), 2.6)
			image.set_pixel(x, y, Color(1.0, 1.0, 1.0, alpha))
	_particle_texture = ImageTexture.create_from_image(image)


func _create_particles() -> void:
	_particles.clear()
	_speech_particle_indices.clear()
	_create_cluster_anchors()
	_cluster_particle_indices.clear()
	for cluster_index in range(CLUSTER_COUNT):
		_cluster_particle_indices.append([])
	for index in range(PARTICLE_COUNT):
		var particle := CloudParticle.new()
		particle.id = index
		particle.seed = _rng.randf()
		particle.layer = 0 if index < CORE_PARTICLES else 1 if index < CORE_PARTICLES + FIELD_PARTICLES else 2
		particle.dust = particle.layer == 2
		particle.cluster = index < CLUSTER_COUNT
		particle.cluster_id = index if particle.cluster else _weighted_cluster_id()
		particle.cluster_weight = _cluster_weights[particle.cluster_id]
		particle.base_position = _generate_position(particle.layer, particle.cluster, particle.cluster_id)
		particle.current_position = particle.base_position
		particle.depth = clampf(particle.base_position.z / DUST_RADIUS, -1.0, 1.0)
		particle.density_weight = _density_weight_for_position(particle.base_position, particle.cluster_id, particle.layer)
		particle.size = _particle_size(particle.layer, particle.cluster, particle.cluster_weight)
		particle.base_brightness = _particle_brightness(particle.layer, particle.cluster, particle.cluster_weight)
		particle.brightness = particle.base_brightness
		particle.noise_offset = Vector3(
			_rng.randf_range(-100.0, 100.0),
			_rng.randf_range(-100.0, 100.0),
			_rng.randf_range(-100.0, 100.0)
		)
		_particles.append(particle)
		if not particle.dust:
			_cluster_particle_indices[particle.cluster_id].append(index)
		if particle.layer == 0 and particle.base_position.length_squared() < CLOUD_RADIUS * CLOUD_RADIUS * 0.18:
			_speech_particle_indices.append(index)


func _create_cluster_anchors() -> void:
	_cluster_anchors.clear()
	_cluster_weights.clear()
	_cluster_offsets.clear()
	_cluster_projected_positions.clear()
	for index in range(CLUSTER_COUNT):
		var anchor: Vector3 = Vector3.ZERO
		var weight: float = 1.0
		if index == 0:
			anchor = Vector3(
				_rng.randf_range(-7.0, 7.0),
				_rng.randf_range(-6.0, 6.0),
				_rng.randf_range(-5.0, 5.0)
			)
			weight = 3.8
		else:
			var shell_mix: float = float(index - 1) / maxf(float(CLUSTER_COUNT - 2), 1.0)
			var band: int = index % 3
			var radius_min: float = CLOUD_RADIUS * (0.20 if band == 0 else 0.36 if band == 1 else 0.52)
			var radius_max: float = CLOUD_RADIUS * (0.38 if band == 0 else 0.58 if band == 1 else 0.76)
			var radius: float = lerpf(radius_min, radius_max, pow(shell_mix, 0.78))
			radius *= _rng.randf_range(0.84, 1.08)
			var theta: float = (float(index) / CLUSTER_COUNT) * TAU * 2.618 + _rng.randf_range(-0.46, 0.46)
			var phi: float = acos(_rng.randf_range(-0.72, 0.72))
			anchor = Vector3(
				radius * sin(phi) * cos(theta),
				radius * sin(phi) * sin(theta),
				radius * cos(phi) * 0.66
			)
			anchor += Vector3(
				_rng.randf_range(-10.0, 10.0),
				_rng.randf_range(-9.0, 9.0),
				_rng.randf_range(-8.0, 8.0)
			)
			weight = _rng.randf_range(1.05, 1.85)
			if band == 1:
				weight += 0.35
		_cluster_anchors.append(anchor)
		_cluster_weights.append(weight)
		_cluster_offsets.append(Vector3.ZERO)
		_cluster_projected_positions.append(_project(anchor))


func _generate_position(layer: int, cluster: bool, cluster_id: int) -> Vector3:
	var radius_limit: float = CLOUD_RADIUS if layer == 0 else FIELD_RADIUS if layer == 1 else DUST_RADIUS
	if cluster:
		return _cluster_anchors[cluster_id]

	if layer != 2 and _rng.randf() < (0.72 if layer == 0 else 0.50):
		var anchor: Vector3 = _cluster_anchors[cluster_id]
		var cluster_spread: float = _rng.randf_range(24.0, 82.0) if layer == 0 else _rng.randf_range(44.0, 118.0)
		var offset: Vector3 = _random_unit_vector() * pow(_rng.randf(), 1.18) * cluster_spread
		var blended: Vector3 = (anchor + offset).lerp(_random_orb_position(radius_limit, layer), 0.14 if layer == 0 else 0.24)
		return _shape_position(blended, layer, radius_limit)

	return _random_orb_position(radius_limit, layer)


func _random_orb_position(radius_limit: float, layer: int) -> Vector3:
	var exponent: float = 2.95 if layer == 0 else 2.55 if layer == 1 else 3.10
	var radius: float = pow(_rng.randf(), exponent) * radius_limit

	var theta: float = _rng.randf() * TAU
	var phi: float = acos(_rng.randf_range(-1.0, 1.0))
	var position: Vector3 = Vector3(
		radius * sin(phi) * cos(theta),
		radius * sin(phi) * sin(theta),
		radius * cos(phi)
	)
	var irregular: Vector3 = Vector3(
		_rng.randf_range(-0.075, 0.075),
		_rng.randf_range(-0.065, 0.065),
		_rng.randf_range(-0.070, 0.070)
	) * radius_limit
	position += irregular
	return _shape_position(position, layer, radius_limit)


func _shape_position(position: Vector3, layer: int, radius_limit: float) -> Vector3:
	if layer == 0:
		position *= Vector3(1.02, 0.98, 0.90)
	elif layer == 1:
		position *= Vector3(1.04, 1.00, 0.84)
	else:
		position *= Vector3(1.06, 1.02, 0.74)

	return position.limit_length(radius_limit * _rng.randf_range(0.56, 0.82))


func _random_unit_vector() -> Vector3:
	var theta := _rng.randf() * TAU
	var phi := acos(_rng.randf_range(-1.0, 1.0))
	return Vector3(sin(phi) * cos(theta), sin(phi) * sin(theta), cos(phi))


func _weighted_cluster_id() -> int:
	var total := 0.0
	for weight in _cluster_weights:
		total += weight
	var roll := _rng.randf() * total
	for index in range(_cluster_weights.size()):
		roll -= _cluster_weights[index]
		if roll <= 0.0:
			return index
	return _cluster_weights.size() - 1


func _particle_size(layer: int, cluster: bool, cluster_weight: float) -> float:
	var weight_t: float = clampf((cluster_weight - 1.0) / 2.8, 0.0, 1.0)
	if cluster:
		return _rng.randf_range(1.35, 2.25) * lerpf(0.96, 1.48, weight_t)
	if layer == 0:
		return _rng.randf_range(0.78, 1.65) * lerpf(0.96, 1.18, weight_t)
	if layer == 1:
		return _rng.randf_range(0.54, 1.25)
	return _rng.randf_range(0.32, 0.72)


func _particle_brightness(layer: int, cluster: bool, cluster_weight: float) -> float:
	var weight_t: float = clampf((cluster_weight - 1.0) / 2.8, 0.0, 1.0)
	if cluster:
		return _rng.randf_range(3.20, 4.80) * lerpf(0.95, 1.85, weight_t)
	if layer == 0:
		return _rng.randf_range(1.10, 2.45) * lerpf(0.92, 1.32, weight_t)
	if layer == 1:
		return _rng.randf_range(0.58, 1.55) * lerpf(0.92, 1.18, weight_t)
	return _rng.randf_range(0.18, 0.58)


func _density_weight_for_position(position: Vector3, cluster_id: int, layer: int) -> float:
	if layer == 2:
		return 0.0
	var anchor := _cluster_anchors[cluster_id]
	var distance := position.distance_to(anchor)
	var cluster_weight := _cluster_weights[cluster_id]
	var radius: float = (82.0 if layer == 0 else 132.0) * lerpf(0.92, 1.28, clampf((cluster_weight - 1.0) / 2.8, 0.0, 1.0))
	return pow(1.0 - clampf(distance / radius, 0.0, 1.0), 1.55) * lerpf(0.92, 1.75, clampf((cluster_weight - 1.0) / 2.8, 0.0, 1.0))


func _create_links() -> void:
	_links.clear()
	var candidates: Array[Dictionary] = []
	var seen := {}
	var grid := _build_link_grid()

	for particle_index in range(CORE_PARTICLES + FIELD_PARTICLES):
		var particle := _particles[particle_index]
		var cell: Vector3i = _grid_cell(particle.base_position)
		var local_limit: int = 1 if particle.layer == 1 and not particle.cluster else MAX_LINKS_PER_PARTICLE
		var best_indices: Array[int] = []
		var best_distances: Array[float] = []
		for x in range(cell.x - 1, cell.x + 2):
			for y in range(cell.y - 1, cell.y + 2):
				for z in range(cell.z - 1, cell.z + 2):
					var key := _grid_key(Vector3i(x, y, z))
					if not grid.has(key):
						continue
					var cell_particles: Array = grid[key]
					for other_index: int in cell_particles:
						if other_index <= particle_index:
							continue
						var other := _particles[other_index]
						var max_distance: float = LINK_DISTANCE_FIELD if particle.layer == 1 or other.layer == 1 else LINK_DISTANCE_CORE
						var distance_squared: float = particle.base_position.distance_squared_to(other.base_position)
						if distance_squared <= max_distance * max_distance:
							var bias: float = 0.82 if particle.cluster or other.cluster else 1.0
							_track_best_neighbor(best_indices, best_distances, other_index, distance_squared * bias, local_limit)
		for local_index in range(best_indices.size()):
			_add_link_candidate(candidates, seen, particle_index, best_indices[local_index], LINK_DISTANCE_FIELD, 1.0, false)

	candidates.sort_custom(func(left, right): return left["d"] < right["d"])
	var link_count := mini(MAX_LINKS, candidates.size())
	for index in range(link_count):
		var candidate: Dictionary = candidates[index]
		var link := CloudLink.new()
		link.a = candidate["a"]
		link.b = candidate["b"]
		link.structural = bool(candidate.get("structural", false))
		var closeness := 1.0 - clampf(float(candidate["d"]) / (LINK_DISTANCE_BRIDGE * LINK_DISTANCE_BRIDGE), 0.0, 1.0)
		if link.structural:
			link.base_opacity = 0.060 + closeness * 0.155
		else:
			link.base_opacity = 0.012 + closeness * 0.035
			if _particles[link.a].cluster or _particles[link.b].cluster:
				link.base_opacity += 0.018
		link.phase = _rng.randf() * TAU
		link.speed = _rng.randf_range(0.18, 0.72) if not link.structural else _rng.randf_range(0.08, 0.32)
		link.active_bias = _rng.randf_range(0.72, 1.0) if link.structural else _rng.randf_range(0.25, 0.75)
		_links.append(link)


func _create_bridges() -> void:
	_bridges.clear()
	var seen := {}
	for cluster_index in range(CLUSTER_COUNT):
		var ranked: Array[Dictionary] = []
		var origin := _cluster_anchors[cluster_index]
		for other_index in range(CLUSTER_COUNT):
			if other_index == cluster_index:
				continue
			var target := _cluster_anchors[other_index]
			var distance_squared: float = origin.distance_squared_to(target)
			if distance_squared > LINK_DISTANCE_BRIDGE * LINK_DISTANCE_BRIDGE * 1.8:
				continue
			var weight_bonus := (_cluster_weights[cluster_index] + _cluster_weights[other_index]) * 0.12
			var radial_bias := 1.0 - clampf(abs(origin.length() - target.length()) / CLOUD_RADIUS, 0.0, 0.55)
			ranked.append({
				"index": other_index,
				"d": distance_squared * lerpf(1.18, 0.76, radial_bias) / (1.0 + weight_bonus)
			})
		ranked.sort_custom(func(left, right): return left["d"] < right["d"])
		var count: int = mini(STRUCTURAL_LINKS_PER_CLUSTER, ranked.size())
		for link_index in range(count):
			_add_bridge_candidate(seen, cluster_index, int(ranked[link_index]["index"]))


func _add_bridge_candidate(seen: Dictionary, a_cluster: int, b_cluster: int) -> void:
	var low: int = mini(a_cluster, b_cluster)
	var high: int = maxi(a_cluster, b_cluster)
	var key := "%s:%s" % [low, high]
	if seen.has(key):
		var existing: CloudBridge = seen[key]
		existing.stack_count += 1
		existing.strength = clampf(existing.strength + 0.22, 0.70, 2.2)
		existing.width = clampf(existing.width + 0.18, 0.80, 2.4)
		return
	var bridge := CloudBridge.new()
	bridge.a_cluster = low
	bridge.b_cluster = high
	bridge.phase = _rng.randf() * TAU
	bridge.speed = _rng.randf_range(0.10, 0.42)
	var weight_sum: float = _cluster_weights[low] + _cluster_weights[high]
	bridge.strength = clampf(0.55 + weight_sum * 0.16, 0.70, 1.35)
	bridge.width = _rng.randf_range(0.80, 1.24) * bridge.strength
	bridge.curve_side = -1.0 if _rng.randf() < 0.5 else 1.0
	bridge.lane_offset = _rng.randf_range(-1.0, 1.0)
	_bridges.append(bridge)
	seen[key] = bridge


func _build_link_grid() -> Dictionary:
	var grid := {}
	for particle_index in range(CORE_PARTICLES + FIELD_PARTICLES):
		var key: String = _grid_key(_grid_cell(_particles[particle_index].base_position))
		if not grid.has(key):
			grid[key] = []
		grid[key].append(particle_index)
	return grid


func _add_structural_cluster_links(candidates: Array[Dictionary], seen: Dictionary) -> void:
	for cluster_index in range(CLUSTER_COUNT):
		var ranked: Array[Dictionary] = []
		var origin := _particles[cluster_index]
		for other_index in range(CLUSTER_COUNT):
			if other_index == cluster_index:
				continue
			var other := _particles[other_index]
			var distance_squared: float = origin.base_position.distance_squared_to(other.base_position)
			var radial_bias := 1.0 - clampf(abs(origin.base_position.length() - other.base_position.length()) / CLOUD_RADIUS, 0.0, 0.45)
			ranked.append({
				"index": other_index,
				"d": distance_squared * lerpf(1.18, 0.72, radial_bias)
			})
		ranked.sort_custom(func(left, right): return left["d"] < right["d"])
		var count: int = mini(STRUCTURAL_LINKS_PER_CLUSTER, ranked.size())
		for link_index in range(count):
			_add_link_candidate(candidates, seen, cluster_index, int(ranked[link_index]["index"]), LINK_DISTANCE_BRIDGE * 1.10, 0.55, true)


func _track_best_neighbor(indices: Array[int], distances: Array[float], candidate_index: int, distance: float, limit: int) -> void:
	if limit <= 0:
		return
	var insert_at := distances.size()
	for index in range(distances.size()):
		if distance < distances[index]:
			insert_at = index
			break
	if insert_at >= limit:
		return
	indices.insert(insert_at, candidate_index)
	distances.insert(insert_at, distance)
	if indices.size() > limit:
		indices.pop_back()
		distances.pop_back()


func _grid_cell(position: Vector3) -> Vector3i:
	return Vector3i(
		floori(position.x / LINK_CELL_SIZE),
		floori(position.y / LINK_CELL_SIZE),
		floori(position.z / LINK_CELL_SIZE)
	)


func _grid_key(cell: Vector3i) -> String:
	return "%s:%s:%s" % [cell.x, cell.y, cell.z]


func _find_bridge_target(source_index: int, source_position: Vector3) -> int:
	var best_index: int = -1
	var best_score: float = INF
	var source_cluster: int = _particles[source_index].cluster_id
	for attempt in range(18):
		var candidate_index: int = _rng.randi_range(0, CORE_PARTICLES + FIELD_PARTICLES - 1)
		if candidate_index == source_index:
			continue
		var candidate := _particles[candidate_index]
		if candidate.cluster_id == source_cluster and attempt < 12:
			continue
		var distance_squared: float = source_position.distance_squared_to(candidate.base_position)
		if distance_squared < LINK_DISTANCE_CORE * LINK_DISTANCE_CORE or distance_squared > LINK_DISTANCE_BRIDGE * LINK_DISTANCE_BRIDGE:
			continue
		var score: float = distance_squared * (0.70 if candidate.cluster else 1.0)
		if score < best_score:
			best_score = score
			best_index = candidate_index
	return best_index


func _add_link_candidate(candidates: Array[Dictionary], seen: Dictionary, a: int, b: int, max_distance: float, bias: float, structural: bool) -> void:
	if a == b:
		return
	var low: int = mini(a, b)
	var high: int = maxi(a, b)
	var key := "%s:%s" % [low, high]
	if seen.has(key):
		return
	var distance_squared := _particles[low].base_position.distance_squared_to(_particles[high].base_position)
	if distance_squared > max_distance * max_distance:
		return
	seen[key] = true
	var cluster_bias: float = 0.62 if _particles[low].cluster or _particles[high].cluster else 1.0
	candidates.append({ "a": low, "b": high, "d": distance_squared * bias * cluster_bias, "structural": structural })


func _update_linger(delta: float) -> void:
	if _speaking_linger <= 0.0:
		return

	_speaking_linger = maxf(_speaking_linger - delta, 0.0)
	if _speaking_linger > SPEAKING_LINGER_FADE:
		return

	var fade_t := 1.0 - (_speaking_linger / SPEAKING_LINGER_FADE)
	for key in _target_params:
		_target_params[key] = lerpf(STATE_PARAMS[CloudState.SPEAKING][key], STATE_PARAMS[CloudState.IDLE][key], fade_t)


func _update_params(delta: float) -> void:
	for key in _target_params:
		_params[key] = lerpf(_params[key], _target_params[key], minf(delta * 1.7, 1.0))


func _set_palette(palette: Dictionary) -> void:
	_target_palette = palette.duplicate(true)


func _update_palette(delta: float) -> void:
	var speed := 2.0
	if current_state == CloudState.ERROR:
		speed = 8.0
	elif current_state == CloudState.CONFIRMATION:
		speed = 4.0
	for key in _target_palette:
		_palette[key] = _palette[key].lerp(_target_palette[key], minf(delta * speed, 1.0))


func _update_speech_energy(delta: float) -> void:
	_speech_energy = move_toward(_speech_energy, 0.0, SPEECH_ENERGY_DECAY * delta)


func _update_speech_morph(delta: float) -> void:
	var flow_speed: float = 0.52 + _speech_energy * 0.40 + _speech_articulation * 0.62
	_speech_flow_phase = fmod(_speech_flow_phase + delta * flow_speed, 1.0)
	_update_autonomous_speech(delta)
	_update_speech_fields(delta)
	_update_global_shift(delta)
	_speech_articulation = move_toward(_speech_articulation, 0.0, delta * SPEECH_ARTICULATION_DECAY)
	_speech_random_axis = _safe_normalized(_speech_random_axis.lerp(_target_speech_random_axis, minf(delta * SPEECH_RANDOM_BLEND_SPEED, 1.0)), _target_speech_random_axis)
	_speech_depth_axis = _safe_normalized(_speech_depth_axis.lerp(_target_speech_depth_axis, minf(delta * SPEECH_RANDOM_BLEND_SPEED, 1.0)), _target_speech_depth_axis)
	_speech_random_twist = lerpf(_speech_random_twist, _target_speech_random_twist, minf(delta * SPEECH_RANDOM_BLEND_SPEED, 1.0))
	_speech_pause_weight = move_toward(_speech_pause_weight, 0.0, delta * SPEECH_PAUSE_DECAY)
	if current_state != CloudState.SPEAKING:
		_speech_autonomous_timer = 0.0
		_speech_global_shift_timer = 0.0
		_speech_global_shift = null
		_speech_articulation = 0.0
		_speech_fields.clear()


func _update_autonomous_speech(delta: float) -> void:
	if current_state != CloudState.SPEAKING:
		return

	_speech_autonomous_timer -= delta
	if _speech_autonomous_timer > 0.0:
		return

	var intensity := _rng.randf_range(0.46, 0.86)
	var punctuation_like := _rng.randf() < 0.16
	_speech_energy = clampf(_speech_energy + SPEECH_ENERGY_ADD * intensity * 0.72, 0.0, 1.0)
	_speech_articulation = clampf(_speech_articulation + intensity * (0.36 if punctuation_like else 0.56), 0.0, 1.0)
	_speech_flow_phase = fmod(_speech_flow_phase + _rng.randf_range(0.018, 0.055) + intensity * 0.018, 1.0)
	_retarget_speech_shape(intensity, punctuation_like)
	if punctuation_like:
		_speech_pause_weight = maxf(_speech_pause_weight, _rng.randf_range(0.18, 0.34))

	_speech_autonomous_timer = _rng.randf_range(SPEECH_AUTONOMOUS_INTERVAL_MIN, SPEECH_AUTONOMOUS_INTERVAL_MAX)


func _retarget_speech_shape(intensity: float, punctuation: bool) -> void:
	_target_speech_random_axis = _softened_random_axis(0.46)
	_target_speech_depth_axis = _softened_random_axis(0.18)
	_target_speech_random_twist = _rng.randf_range(-PI, PI)
	var dramatic := (not punctuation) and _rng.randf() < 0.18
	_spawn_speech_field(intensity, punctuation, dramatic)
	if dramatic and _rng.randf() < 0.45:
		_spawn_speech_field(intensity * 0.74, false, false)


func _update_speech_fields(delta: float) -> void:
	var index := _speech_fields.size() - 1
	while index >= 0:
		var field: SpeechMorphField = _speech_fields[index]
		field.age += delta
		if field.age >= field.lifetime:
			_speech_fields.remove_at(index)
		index -= 1


func _update_global_shift(delta: float) -> void:
	if current_state != CloudState.SPEAKING:
		return

	if _speech_global_shift != null:
		_speech_global_shift.age += delta
		if _speech_global_shift.age >= _speech_global_shift.lifetime:
			_speech_global_shift = null

	_speech_global_shift_timer -= delta
	if _speech_global_shift_timer > 0.0:
		return

	if _speech_global_shift == null and _rng.randf() < 0.62:
		_spawn_global_shift()
	_speech_global_shift_timer = _rng.randf_range(SPEECH_GLOBAL_SHIFT_INTERVAL_MIN, SPEECH_GLOBAL_SHIFT_INTERVAL_MAX)


func _spawn_global_shift() -> void:
	var shift := SpeechGlobalShift.new()
	shift.axis = _softened_random_axis(0.42)
	shift.cross_axis = _safe_normalized(shift.axis.cross(_softened_random_axis(0.38)), Vector3.UP)
	shift.depth_axis = _softened_random_axis(0.30)
	shift.lifetime = _rng.randf_range(0.72, 1.28)
	shift.strength = _rng.randf_range(0.48, 0.86)
	shift.twist = _rng.randf_range(-1.0, 1.0)
	shift.compression = _rng.randf_range(-0.55, 0.45)
	_speech_global_shift = shift


func _spawn_speech_field(intensity: float, punctuation: bool, dramatic: bool) -> void:
	var field := SpeechMorphField.new()
	field.center_dir = _softened_random_axis(0.34)
	field.axis = _softened_random_axis(0.52)
	field.depth_axis = _softened_random_axis(0.22)
	field.radius_center = _rng.randf_range(0.12, 0.62) if dramatic else _rng.randf_range(0.20, 0.84)
	field.radius_width = _rng.randf_range(0.26, 0.48) if dramatic else _rng.randf_range(0.18, 0.34)
	field.strength = (1.18 + intensity * 1.05) if dramatic else (0.54 + intensity * 0.58)
	if punctuation:
		field.strength *= 0.58
		field.radius_width *= 1.18
	field.lifetime = _rng.randf_range(0.34, 0.58) if dramatic else _rng.randf_range(0.22, 0.42)
	field.twist = _rng.randf_range(-1.0, 1.0)
	field.pull = _rng.randf() < (0.36 if dramatic else 0.24)
	field.dramatic = dramatic
	_speech_fields.append(field)
	while _speech_fields.size() > SPEECH_MAX_MORPH_FIELDS:
		_speech_fields.remove_at(0)


func _softened_random_axis(z_scale: float) -> Vector3:
	var axis := _random_unit_vector()
	axis.z *= z_scale
	if axis.length_squared() < 0.01:
		return Vector3.RIGHT
	return axis.normalized()


func _update_shape_morph(delta: float) -> void:
	match current_state:
		CloudState.SPEAKING:
			_target_shape_morph = 0.52 + _speech_articulation * 0.18
		CloudState.THINKING:
			_target_shape_morph = 0.24
		CloudState.EXECUTING:
			_target_shape_morph = 0.30
		CloudState.ERROR:
			_target_shape_morph = 0.20
		_:
			_target_shape_morph = 0.04
	_shape_morph = lerpf(_shape_morph, _target_shape_morph, minf(delta * 2.4, 1.0))


func _shape_deformation(position: Vector3, cluster_id: int, strength: float) -> Vector3:
	if strength <= 0.001:
		return Vector3.ZERO
	var radius_ratio: float = clampf(position.length() / DUST_RADIUS, 0.0, 1.0)
	var radial: Vector3 = position.normalized() if position.length_squared() > 0.01 else Vector3.ZERO
	var tangent: Vector3 = Vector3(-radial.y, radial.x, 0.0)
	if tangent.length_squared() < 0.01:
		tangent = Vector3.RIGHT
	tangent = tangent.normalized()
	var phase: float = float(cluster_id) * 1.173 + radius_ratio * 2.7
	var slow_a: float = sin(_time * 0.37 + phase)
	var slow_b: float = cos(_time * 0.29 + phase * 1.61)
	var lobe: float = sin(atan2(position.y, position.x) * 3.0 + _time * 0.18 + float(cluster_id) * 0.37)
	var radial_amount: float = (slow_a * 3.0 + lobe * 2.8) * strength * radius_ratio
	var tangent_amount: float = slow_b * 4.2 * strength * (0.35 + radius_ratio)
	var z_amount: float = sin(_time * 0.23 + phase * 0.71) * 2.2 * strength
	return radial * radial_amount + tangent * tangent_amount + Vector3(0.0, 0.0, z_amount)


func _speech_form_deformation(position: Vector3, cluster_id: int, strength: float) -> Vector3:
	if strength <= 0.001:
		return Vector3.ZERO
	if current_state != CloudState.SPEAKING and _speech_pause_weight <= 0.001:
		return _speech_form_profile_deformation(position, cluster_id, strength * 0.58, -1)
	var profile_position: float = _speech_flow_phase * float(SPEECH_MORPH_PROFILE_COUNT - 1)
	var profile_a: int = int(floor(profile_position)) % (SPEECH_MORPH_PROFILE_COUNT - 1)
	var profile_b: int = (profile_a + 1) % (SPEECH_MORPH_PROFILE_COUNT - 1)
	var blend_t := _smoother_step(profile_position - floor(profile_position))
	var articulation_strength: float = strength * (0.42 + _speech_articulation * 0.36 + _speech_energy * 0.16)
	var from_form := _speech_form_profile_deformation(position, cluster_id, articulation_strength, profile_a)
	var to_form := _speech_form_profile_deformation(position, cluster_id, articulation_strength, profile_b)
	var base_form := _speech_form_profile_deformation(position, cluster_id, strength * 0.18, -1)
	var pause_form := _speech_form_profile_deformation(position, cluster_id, strength * 0.30, 5)
	var growth_form := _speech_growth_deformation(position, cluster_id, strength)
	var global_form := _speech_global_deformation(position, cluster_id, strength)
	return global_form + growth_form + base_form + from_form.lerp(to_form, blend_t).lerp(pause_form, clampf(_speech_pause_weight, 0.0, 0.62))


func _speech_global_deformation(position: Vector3, cluster_id: int, strength: float) -> Vector3:
	if _speech_global_shift == null:
		return Vector3.ZERO

	var shift: SpeechGlobalShift = _speech_global_shift
	var life_t: float = clampf(shift.age / maxf(shift.lifetime, 0.001), 0.0, 1.0)
	var envelope: float = sin(life_t * PI)
	var radius_ratio: float = clampf(position.length() / CLOUD_RADIUS, 0.0, 1.25)
	var radial: Vector3 = position.normalized() if position.length_squared() > 0.01 else shift.axis
	var axial: float = radial.dot(shift.axis)
	var cross: float = radial.dot(shift.cross_axis)
	var phase: float = float(cluster_id) * 1.613 + shift.twist * PI + radius_ratio * TAU
	var shear: Vector3 = shift.cross_axis * axial * (5.8 + radius_ratio * 5.2)
	var fold: Vector3 = shift.axis * sin(cross * PI + phase) * (2.4 + radius_ratio * 4.4)
	var depth: Vector3 = shift.depth_axis * sin(phase * 0.67 + life_t * PI) * (2.8 + radius_ratio * 3.8)
	var compression: Vector3 = -radial * axial * shift.compression * (2.0 + radius_ratio * 4.0)
	return (shear + fold + depth + compression) * envelope * shift.strength * strength


func _speech_growth_deformation(position: Vector3, cluster_id: int, strength: float) -> Vector3:
	if _speech_fields.is_empty():
		return Vector3.ZERO

	var radius_ratio: float = clampf(position.length() / CLOUD_RADIUS, 0.0, 1.25)
	var shell_ratio: float = clampf(radius_ratio, 0.0, 1.0)
	var radial: Vector3 = position.normalized() if position.length_squared() > 0.01 else _speech_random_axis
	var tangent: Vector3 = Vector3(-radial.y, radial.x, 0.0)
	if tangent.length_squared() < 0.01:
		tangent = Vector3.UP
	tangent = tangent.normalized()

	var result := Vector3.ZERO
	for raw_field in _speech_fields:
		var field: SpeechMorphField = raw_field
		var life_t: float = clampf(field.age / maxf(field.lifetime, 0.001), 0.0, 1.0)
		var envelope: float = sin(life_t * PI)
		var radial_distance: float = abs(shell_ratio - field.radius_center) / maxf(field.radius_width, 0.001)
		var radial_mask: float = pow(maxf(0.0, 1.0 - radial_distance), 1.8)
		var directional_mask: float = clampf((radial.dot(field.center_dir) + 1.0) * 0.5, 0.0, 1.0)
		directional_mask = pow(directional_mask, 1.35 if field.dramatic else 1.9)
		var core_reach: float = pow(1.0 - shell_ratio, 1.75) * (0.36 if field.dramatic else 0.16)
		var mask: float = clampf(radial_mask * (0.28 + directional_mask * 0.92) + core_reach, 0.0, 1.0)
		if mask <= 0.001:
			continue

		var phase: float = float(cluster_id) * 1.217 + field.twist * PI + _speech_flow_phase * TAU
		var sign := -1.0 if field.pull else 1.0
		var pressure: float = envelope * mask * field.strength * strength
		var field_axis: Vector3 = _safe_normalized(field.axis.lerp(radial, 0.28 + shell_ratio * 0.34), field.axis)
		var body_push: Vector3 = field_axis * sign * (7.4 if field.dramatic else 4.4)
		var counter_pull: Vector3 = -radial * (1.8 + directional_mask * 3.6) * (1.0 if field.pull else 0.34)
		var depth_push: Vector3 = field.depth_axis * sin(phase + life_t * PI) * (5.2 if field.dramatic else 2.7)
		var twist_push: Vector3 = tangent * sin(phase * 0.71 + life_t * TAU) * field.twist * (6.4 if field.dramatic else 3.2)
		result += (body_push + counter_pull + depth_push + twist_push) * pressure

	return result


func _speech_form_profile_deformation(position: Vector3, cluster_id: int, strength: float, profile: int) -> Vector3:
	var radius_ratio: float = clampf(position.length() / CLOUD_RADIUS, 0.0, 1.35)
	var outer_weight: float = pow(radius_ratio, 0.82)
	var central_hold: float = 0.22 + clampf(radius_ratio * 1.05, 0.0, 0.78)
	var phase: float = float(cluster_id) * 1.941 + float(profile) * 0.911
	var compliance: float = clampf(0.82 + sin(phase + _time * (0.19 + float(profile) * 0.035)) * 0.26 + cos(phase * 0.47) * 0.18, 0.46, 1.28)
	var vertical_bias: float = clampf(abs(position.y) / maxf(CLOUD_RADIUS * 0.62, 1.0), 0.0, 1.35)
	var horizontal_bias: float = clampf(abs(position.x) / maxf(CLOUD_RADIUS * 0.58, 1.0), 0.0, 1.35)
	var profile_phase: float = atan2(position.y, position.x) + float(profile) * 0.42
	var vertical_shape: float = 1.0
	var spread_shape: float = 1.0
	var shear_shape: float = 1.0
	var fold_shape: float = 1.0
	var z_shape: float = 1.0
	match profile:
		-1:
			vertical_shape = 1.0
			spread_shape = 1.0
			shear_shape = 1.0
			fold_shape = 1.0
			z_shape = 1.0
		0:
			vertical_shape = 0.92
			spread_shape = 0.76
			shear_shape = -0.22
			fold_shape = 0.36
			z_shape = 0.52
		1:
			vertical_shape = 0.58
			spread_shape = 1.02
			shear_shape = 0.42
			fold_shape = 0.70
			z_shape = -0.36
		2:
			vertical_shape = 0.72
			spread_shape = -0.20
			shear_shape = 0.78
			fold_shape = 0.82
			z_shape = 0.68
		3:
			vertical_shape = -0.14
			spread_shape = 0.82
			shear_shape = -0.72
			fold_shape = 0.56
			z_shape = 0.78
		4:
			vertical_shape = 0.82
			spread_shape = 0.72
			shear_shape = 0.20
			fold_shape = -0.64
			z_shape = -0.58
		_:
			vertical_shape = 0.20
			spread_shape = -0.12
			shear_shape = 0.08
			fold_shape = 0.12
			z_shape = -0.16
	var upper_lower_pull: float = (0.18 + vertical_bias * 0.28) * compliance * outer_weight * strength * vertical_shape
	var side_spread: float = (0.13 + horizontal_bias * 0.16) * compliance * outer_weight * strength * spread_shape
	var diagonal_shear: float = sin(phase * 0.73 + _time * 0.13) * 8.0 * outer_weight * strength * shear_shape
	var fold: float = sin(profile_phase * 2.0 + phase + _time * 0.16) * 5.5 * outer_weight * strength * fold_shape
	var random_body: Vector3 = _speech_random_axis * sin(profile_phase * 1.30 + phase) * (1.4 + outer_weight * 3.2) * strength
	var random_depth: Vector3 = _speech_depth_axis * cos(profile_phase * 0.80 - phase * 0.37) * (0.8 + outer_weight * 2.4) * strength
	var x_sign: float = -1.0 if position.x < 0.0 else 1.0
	var x_delta: float = position.x * side_spread + x_sign * vertical_bias * 7.0 * compliance * strength * spread_shape + fold
	var y_delta: float = -position.y * upper_lower_pull + diagonal_shear * 0.32
	var z_delta: float = (sin(phase + _time * 0.21) * 9.0 * z_shape - position.z * 0.08) * outer_weight * strength
	return (Vector3(x_delta, y_delta, z_delta) + random_body + random_depth) * central_hold


func _smoother_step(value: float) -> float:
	return value * value * value * (value * (value * 6.0 - 15.0) + 10.0)


func _safe_normalized(vector: Vector3, fallback: Vector3) -> Vector3:
	if vector.length_squared() >= 0.01:
		return vector.normalized()
	if fallback.length_squared() >= 0.01:
		return fallback.normalized()
	return Vector3.RIGHT


func _update_clusters(delta: float) -> void:
	var activity: float = _params["activity"]
	var drift_speed: float = _params["drift_speed"]
	var global_phase: float = _time * (0.34 + drift_speed * 0.18)
	var speech_drive: float = 0.0 if current_state == CloudState.SPEAKING else _speech_energy * 0.25
	for index in range(CLUSTER_COUNT):
		var weight: float = _cluster_weights[index]
		var anchor: Vector3 = _cluster_anchors[index]
		var radius_ratio: float = clampf(anchor.length() / CLOUD_RADIUS, 0.0, 1.0)
		var phase: float = global_phase - radius_ratio * 2.1 + weight * 0.23
		var state_drive: float = 1.0 + activity * 0.8
		var central_damping: float = lerpf(1.0, 0.45, clampf((weight - 1.0) / 2.8, 0.0, 1.0))
		var radial: Vector3 = anchor.normalized() if anchor.length_squared() > 0.01 else Vector3.ZERO
		var tangent: Vector3 = Vector3(-radial.y, radial.x, 0.0)
		if tangent.length_squared() < 0.01:
			tangent = Vector3.RIGHT
		tangent = tangent.normalized()
		var activation: float = pow(maxf(0.0, sin(phase)), 2.4)
		var breath_pull: float = sin(global_phase * 0.72 + radius_ratio * 0.8) * (1.2 + activity * 2.0)
		var wave_push: float = activation * speech_drive * (5.5 + weight * 1.7)
		var soft_morph: Vector3 = _shape_deformation(anchor, index, _shape_morph * (0.28 + radius_ratio * 0.34))
		var form_morph: Vector3 = _speech_form_deformation(anchor, index, _shape_morph * (0.46 + radius_ratio * 0.24))
		var target: Vector3 = ((radial * (breath_pull + wave_push) + tangent * sin(global_phase * 0.53 + radius_ratio * TAU) * (0.9 + activity * 1.8)) * state_drive + soft_morph + form_morph) * central_damping
		var cluster_follow_speed: float = 3.85 if current_state == CloudState.SPEAKING else 3.2
		_cluster_offsets[index] = _cluster_offsets[index].lerp(target, minf(delta * cluster_follow_speed, 1.0))
		_cluster_projected_positions[index] = _project(anchor + _cluster_offsets[index])


func _update_particles(delta: float) -> void:
	var activity: float = _params["activity"]
	var drift_speed: float = _params["drift_speed"]
	var brightness_mult: float = _params["brightness"]
	var breath_depth: float = 0.0 if current_state == CloudState.SPEAKING else float(_params["breath_depth"])
	var breath: float = 1.0 + sin(_time * TAU * float(_params["breath_speed"]) * 0.18) * breath_depth
	var speech_expansion: float = 0.0

	for particle in _particles:
		var layer_scale: float = 0.55 if particle.layer == 0 else 0.85 if particle.layer == 1 else 1.15
		var drift_phase: float = _time * drift_speed
		var seed_phase: float = particle.seed * TAU
		var noise_position: Vector3 = Vector3(
			sin(seed_phase + particle.noise_offset.x * 0.07 + drift_phase * 1.07) + cos(particle.noise_offset.z * 0.05 + drift_phase * 0.43),
			cos(seed_phase * 1.37 + particle.noise_offset.y * 0.06 + drift_phase * 0.91) + sin(particle.noise_offset.x * 0.04 + drift_phase * 0.37),
			sin(seed_phase * 0.73 + particle.noise_offset.z * 0.05 + drift_phase * 0.71) + cos(particle.noise_offset.y * 0.03 + drift_phase * 0.29)
		) * 0.5
		var speech_wave: float = 0.0
		if current_state == CloudState.SPEAKING:
			var distance_ratio: float = clampf(particle.base_position.length() / DUST_RADIUS, 0.0, 1.0)
			var wave_position: float = fmod(_time * 0.42, 1.0)
			speech_wave = pow(maxf(0.0, 1.0 - abs(distance_ratio - wave_position) * 4.0), 2.0) * _speech_energy

		var soft_morph: Vector3 = _shape_deformation(particle.base_position, particle.cluster_id, _shape_morph * (0.18 + particle.density_weight * 0.22))
		var form_morph: Vector3 = _speech_form_deformation(particle.base_position, particle.cluster_id, _shape_morph * (0.36 + particle.density_weight * 0.22))
		var target: Vector3 = (particle.base_position + soft_morph + form_morph) * (breath + speech_expansion * layer_scale)
		var cluster_motion: Vector3 = _cluster_offsets[particle.cluster_id]
		var inherit: float = 1.0 if particle.cluster else clampf(0.46 + particle.density_weight * 0.50, 0.22, 0.92)
		target += cluster_motion * inherit
		target += noise_position * (0.85 + activity * 2.9) * layer_scale * (1.0 - inherit * 0.68)
		var particle_follow_speed: float = 2.78 if current_state == CloudState.SPEAKING else 2.35
		particle.current_position = particle.current_position.lerp(target, minf(delta * particle_follow_speed, 1.0))
		particle.depth = clampf(particle.current_position.z / DUST_RADIUS, -1.0, 1.0)
		particle.projected_position = _project(particle.current_position)

		var center_factor := 1.0 - clampf(particle.current_position.length() / (DUST_RADIUS * 1.05), 0.0, 1.0)
		var density_light := particle.density_weight * (1.80 + center_factor * 1.18)
		var target_brightness := (particle.base_brightness + density_light + center_factor * 1.08 + speech_wave * 0.90 + particle.pulse) * brightness_mult * 1.42
		particle.brightness = lerpf(particle.brightness, clampf(target_brightness, 0.0, 5.8), minf(delta * 4.2, 1.0))
		particle.pulse = move_toward(particle.pulse, 0.0, delta * 2.4)


func _project(position: Vector3) -> Vector2:
	var perspective := 1.0 + position.z / (DUST_RADIUS * 3.3)
	return size * 0.5 + Vector2(position.x, position.y) * perspective * VISUAL_SCALE


func _update_thinking(delta: float) -> void:
	if current_state != CloudState.THINKING:
		return

	_thinking_timer -= delta
	if _thinking_timer > 0.0:
		return

	_thinking_timer = _rng.randf_range(THINKING_PULSE_INTERVAL_MIN, THINKING_PULSE_INTERVAL_MAX)
	for index in range(3):
		var particle := _particles[_rng.randi_range(0, CLUSTER_COUNT - 1)]
		particle.pulse = maxf(particle.pulse, 0.65 - index * 0.12)
		_boost_nearby_links(particle.id, 0.45)


func _boost_nearby_links(particle_id: int, amount: float) -> void:
	for link in _links:
		if link.a == particle_id or link.b == particle_id:
			link.opacity = maxf(link.opacity, amount)


func _update_links(delta: float) -> void:
	var visibility: float = _params["link_visibility"]
	var motion: float = _params["link_motion"]
	for link in _links:
		var wave := (sin(_time * link.speed * motion + link.phase) + 1.0) * 0.5
		var target := link.base_opacity * visibility * (0.45 + wave * 0.75) * link.active_bias
		if current_state == CloudState.SPEAKING:
			target += _speech_energy * link.base_opacity * 0.85
		link.opacity = lerpf(link.opacity, target, minf(delta * 3.5, 1.0))


func _rebuild_render_meshes() -> void:
	var dust_vertices := PackedVector3Array()
	var dust_uvs := PackedVector2Array()
	var dust_colors := PackedColorArray()
	var dust_indices := PackedInt32Array()
	var particle_vertices := PackedVector3Array()
	var particle_uvs := PackedVector2Array()
	var particle_colors := PackedColorArray()
	var particle_indices := PackedInt32Array()
	var cluster_vertices := PackedVector3Array()
	var cluster_uvs := PackedVector2Array()
	var cluster_colors := PackedColorArray()
	var cluster_indices := PackedInt32Array()
	var aura_vertices := PackedVector3Array()
	var aura_uvs := PackedVector2Array()
	var aura_colors := PackedColorArray()
	var aura_indices := PackedInt32Array()
	var density_vertices := PackedVector3Array()
	var density_uvs := PackedVector2Array()
	var density_colors := PackedColorArray()
	var density_indices := PackedInt32Array()
	var spark_vertices := PackedVector3Array()
	var spark_uvs := PackedVector2Array()
	var spark_colors := PackedColorArray()
	var spark_indices := PackedInt32Array()
	var link_glow_vertices := PackedVector3Array()
	var link_glow_colors := PackedColorArray()
	var link_glow_indices := PackedInt32Array()
	var link_vertices := PackedVector3Array()
	var link_colors := PackedColorArray()
	var link_indices := PackedInt32Array()

	_build_particle_meshes(
		dust_vertices,
		dust_uvs,
		dust_colors,
		dust_indices,
		particle_vertices,
		particle_uvs,
		particle_colors,
		particle_indices,
		cluster_vertices,
		cluster_uvs,
		cluster_colors,
		cluster_indices,
		aura_vertices,
		aura_uvs,
		aura_colors,
		aura_indices,
		density_vertices,
		density_uvs,
		density_colors,
		density_indices
	)
	_build_link_meshes(link_glow_vertices, link_glow_colors, link_glow_indices, link_vertices, link_colors, link_indices, spark_vertices, spark_uvs, spark_colors, spark_indices)
	_build_bridge_meshes(link_glow_vertices, link_glow_colors, link_glow_indices, link_vertices, link_colors, link_indices, spark_vertices, spark_uvs, spark_colors, spark_indices)

	_commit_mesh(_dust_mesh, dust_vertices, dust_colors, dust_indices, dust_uvs)
	_commit_mesh(_link_glow_mesh, link_glow_vertices, link_glow_colors, link_glow_indices, PackedVector2Array())
	_commit_mesh(_link_mesh, link_vertices, link_colors, link_indices, PackedVector2Array())
	_commit_mesh(_spark_mesh, spark_vertices, spark_colors, spark_indices, spark_uvs)
	_commit_mesh(_aura_mesh, aura_vertices, aura_colors, aura_indices, aura_uvs)
	_commit_mesh(_particle_mesh, particle_vertices, particle_colors, particle_indices, particle_uvs)
	_commit_mesh(_density_mesh, density_vertices, density_colors, density_indices, density_uvs)
	_commit_mesh(_cluster_mesh, cluster_vertices, cluster_colors, cluster_indices, cluster_uvs)


func _build_particle_meshes(
	dust_vertices: PackedVector3Array,
	dust_uvs: PackedVector2Array,
	dust_colors: PackedColorArray,
	dust_indices: PackedInt32Array,
	particle_vertices: PackedVector3Array,
	particle_uvs: PackedVector2Array,
	particle_colors: PackedColorArray,
	particle_indices: PackedInt32Array,
	cluster_vertices: PackedVector3Array,
	cluster_uvs: PackedVector2Array,
	cluster_colors: PackedColorArray,
	cluster_indices: PackedInt32Array,
	aura_vertices: PackedVector3Array,
	aura_uvs: PackedVector2Array,
	aura_colors: PackedColorArray,
	aura_indices: PackedInt32Array,
	density_vertices: PackedVector3Array,
	density_uvs: PackedVector2Array,
	density_colors: PackedColorArray,
	density_indices: PackedInt32Array
) -> void:
	var hot: Color = _palette["hot"]
	var primary: Color = _palette["primary"]
	var dim: Color = _palette["dim"]
	var glow: Color = _palette["glow"]
	for index in range(_particles.size()):
		var particle := _particles[index]
		var depth_factor: float = clampf((particle.depth + 1.0) * 0.5, 0.22, 1.20)
		if particle.dust:
			var radial_fade: float = pow(1.0 - clampf(particle.current_position.length() / (DUST_RADIUS * 0.78), 0.0, 1.0), 1.45)
			var dust_alpha: float = clampf(particle.brightness * depth_factor * radial_fade * 0.82, 0.0, 0.34)
			if dust_alpha < 0.024:
				continue
			var dust_radius := maxf(0.46, particle.size * depth_factor * VISUAL_SCALE * 0.78)
			_add_particle_quad(dust_vertices, dust_uvs, dust_colors, dust_indices, particle.projected_position, dust_radius, Color(dim.r, dim.g, dim.b, dust_alpha))
			continue

		var body_fade: float = pow(1.0 - clampf(particle.current_position.length() / (FIELD_RADIUS * 1.02), 0.0, 1.0), 0.34)
		if body_fade < 0.12 and not particle.cluster:
			continue
		var brightness: float = clampf(particle.brightness * depth_factor * 1.95, 0.0, 7.5)
		brightness *= lerpf(0.34, 1.0, body_fade)
		var particle_color: Color = dim.lerp(primary, clampf(brightness * 0.82, 0.0, 1.0))
		if particle.cluster:
			particle_color = particle_color.lerp(hot, clampf((brightness - 0.72) * 0.16, 0.0, 0.46))
		particle_color.a = clampf((0.22 + brightness * (0.56 if particle.layer == 0 else 0.40)) * lerpf(0.48, 1.0, body_fade), 0.075, 0.96)
		var radius := particle.size * VISUAL_SCALE * (0.50 + depth_factor * 0.58) * (1.0 + particle.pulse * 0.12)
		if particle.layer == 0 and particle.density_weight >= 0.28:
			radius *= 1.0 + particle.density_weight * 0.20

		if particle.cluster:
			var cluster_t: float = clampf((particle.cluster_weight - 1.0) / 2.8, 0.0, 1.0)
			var cluster_glow: Color = glow.lerp(hot, clampf(cluster_t * 0.18, 0.0, 0.18))
			cluster_glow.a = clampf(particle_color.a * lerpf(0.18, 0.64, cluster_t), 0.10, 0.62)
			_add_particle_quad(aura_vertices, aura_uvs, aura_colors, aura_indices, particle.projected_position, radius * lerpf(2.8, 6.0, cluster_t), cluster_glow)
			_add_particle_quad(cluster_vertices, cluster_uvs, cluster_colors, cluster_indices, particle.projected_position, radius * 3.15, particle_color)
		else:
			_add_particle_quad(particle_vertices, particle_uvs, particle_colors, particle_indices, particle.projected_position, radius, particle_color)

		if index % 2 == 0 and brightness >= 0.58:
			var phase := particle.seed * TAU + _time * (0.24 + particle.seed * 0.18)
			var offset_distance := 2.0 + particle.seed * 7.0
			var aura_point := particle.projected_position + Vector2(cos(phase), sin(phase * 1.37)) * offset_distance
			var aura_color: Color = primary.lerp(hot, clampf((brightness - 0.62) * 0.24, 0.0, 0.58))
			aura_color.a = clampf(brightness * depth_factor * 0.13, 0.030, 0.24)
			_add_particle_quad(aura_vertices, aura_uvs, aura_colors, aura_indices, aura_point, 0.48 + particle.seed * 0.72, aura_color)

		if particle.layer == 0 and index % 2 == 0 and particle.density_weight >= 0.18:
			var density_brightness: float = clampf(particle.brightness * particle.density_weight * depth_factor * 1.8, 0.0, 6.0)
			if density_brightness >= 0.18:
				var density_color: Color = primary.lerp(glow, clampf(density_brightness * 0.18, 0.0, 0.55))
				density_color.a = clampf(density_brightness * 0.30, 0.070, 0.62)
				var density_phase := particle.seed * TAU
				var density_offset: Vector2 = Vector2(cos(density_phase), sin(density_phase)) * (1.2 + particle.seed * 3.2)
				_add_particle_quad(density_vertices, density_uvs, density_colors, density_indices, particle.projected_position + density_offset, 0.72 + particle.density_weight * 1.45, density_color)


func _build_link_meshes(
	link_glow_vertices: PackedVector3Array,
	link_glow_colors: PackedColorArray,
	link_glow_indices: PackedInt32Array,
	link_vertices: PackedVector3Array,
	link_colors: PackedColorArray,
	link_indices: PackedInt32Array,
	spark_vertices: PackedVector3Array,
	spark_uvs: PackedVector2Array,
	spark_colors: PackedColorArray,
	spark_indices: PackedInt32Array
) -> void:
	var line: Color = _palette["line"]
	var line_dim: Color = _palette["line_dim"]
	var hot: Color = _palette["hot"]
	var sparks_drawn := 0
	for link in _links:
		if link.opacity <= 0.004:
			continue
		var a := _particles[link.a]
		var b := _particles[link.b]
		var depth_factor: float = clampf(((a.depth + b.depth) * 0.5 + 1.0) * 0.5, 0.25, 1.0)
		var alpha: float = clampf(link.opacity * depth_factor * 1.55, 0.0, 0.18)
		var color: Color = line_dim.lerp(line, alpha * 4.0)
		var glow_color: Color = color
		glow_color.a = alpha * 0.11
		color.a = alpha
		var glow_width := 0.80 + alpha * 0.70
		var line_width := 0.18 + alpha * 0.42
		_add_line_quad(link_glow_vertices, link_glow_colors, link_glow_indices, a.projected_position, b.projected_position, glow_width, glow_color)
		_add_line_quad(link_vertices, link_colors, link_indices, a.projected_position, b.projected_position, line_width, color)

		if link.opacity > 0.055 and sparks_drawn < int(MAX_SPARKS / 5):
			var t: float = fmod(link.phase * 0.137 + _time * link.speed * 0.17, 1.0)
			var point: Vector2 = a.projected_position.lerp(b.projected_position, t)
			var spark_alpha: float = clampf(link.opacity * 0.92, 0.035, 0.26)
			var spark_color: Color = line.lerp(hot, clampf(spark_alpha * 1.7, 0.0, 0.42))
			spark_color.a = spark_alpha
			_add_particle_quad(spark_vertices, spark_uvs, spark_colors, spark_indices, point, 0.55 + spark_alpha * 2.4, spark_color)
			sparks_drawn += 1


func _build_bridge_meshes(
	link_glow_vertices: PackedVector3Array,
	link_glow_colors: PackedColorArray,
	link_glow_indices: PackedInt32Array,
	link_vertices: PackedVector3Array,
	link_colors: PackedColorArray,
	link_indices: PackedInt32Array,
	spark_vertices: PackedVector3Array,
	spark_uvs: PackedVector2Array,
	spark_colors: PackedColorArray,
	spark_indices: PackedInt32Array
) -> void:
	var line: Color = _palette["line"]
	var line_dim: Color = _palette["line_dim"]
	var hot: Color = _palette["hot"]
	for bridge in _bridges:
		var a: Vector2 = _cluster_projected_positions[bridge.a_cluster]
		var b: Vector2 = _cluster_projected_positions[bridge.b_cluster]
		var distance: float = a.distance_to(b)
		if distance < 12.0:
			continue
		var slow_wave: float = pow(maxf(0.0, sin(_time * bridge.speed + bridge.phase)), 2.6)
		var flicker_wave: float = pow(maxf(0.0, sin(_time * (1.1 + bridge.speed * 1.7) + bridge.phase * 2.31)), 7.0)
		var pulse: float = 0.35 + slow_wave * 0.30 + flicker_wave * 0.45
		if current_state == CloudState.SPEAKING:
			pulse += _speech_energy * 0.28
		var stack_boost: float = 1.0 + float(bridge.stack_count - 1) * 0.28
		var alpha: float = clampf(pulse * bridge.strength * stack_boost * float(_params["link_visibility"]) * 0.24, 0.035, 0.30)
		var color: Color = line_dim.lerp(line, clampf(alpha * 2.6, 0.0, 1.0)).lerp(hot, clampf(alpha * 0.24, 0.0, 0.14))
		color.a = alpha
		var glow_color: Color = color
		glow_color.a = alpha * 0.20
		var base_curve: Vector2 = _bridge_curve_offset(a, b, bridge.phase) * bridge.curve_side
		var normal: Vector2 = _line_normal(a, b)
		var pulse_position: float = fmod(_time * (0.10 + bridge.speed * 0.32) + bridge.phase * 0.071, 1.0)
		var segment_count: int = 12
		var lane_count: int = 3
		for lane in range(lane_count):
			var lane_t: float = float(lane) - 1.0
			var lane_offset: Vector2 = normal * (lane_t * 0.55 + bridge.lane_offset * 0.18)
			var lane_curve: Vector2 = base_curve + normal * lane_t * 0.70
			var previous: Vector2 = a + lane_offset
			for segment in range(1, segment_count + 1):
				var t: float = float(segment) / float(segment_count)
				var point: Vector2 = _curved_bridge_point(a + lane_offset, b + lane_offset, lane_curve, t)
				point += lane_curve.rotated(PI * 0.5) * sin(t * TAU * 2.0 + bridge.phase + lane_t) * 0.045
				var mid_t: float = (t + float(segment - 1) / float(segment_count)) * 0.5
				var pulse_distance: float = abs(mid_t - pulse_position)
				pulse_distance = minf(pulse_distance, 1.0 - pulse_distance)
				var travelling_pulse: float = pow(maxf(0.0, 1.0 - pulse_distance * 7.5), 2.3)
				var segment_alpha: float = alpha * (0.30 + travelling_pulse * 1.45)
				var segment_color: Color = line_dim.lerp(line, clampf(segment_alpha * 4.4, 0.0, 1.0)).lerp(hot, clampf(travelling_pulse * 0.18, 0.0, 0.18))
				segment_color.a = clampf(segment_alpha, 0.012, 0.36)
				var segment_glow: Color = segment_color
				segment_glow.a = segment_color.a * 0.22
				var width: float = bridge.width * (0.14 + travelling_pulse * 0.32 + float(bridge.stack_count - 1) * 0.04)
				_add_line_quad(link_glow_vertices, link_glow_colors, link_glow_indices, previous, point, width * 1.65, segment_glow)
				_add_line_quad(link_vertices, link_colors, link_indices, previous, point, width, segment_color)
				previous = point

		var spark_point: Vector2 = _curved_bridge_point(a, b, base_curve, pulse_position)
		var spark_color: Color = line.lerp(hot, 0.10)
		spark_color.a = clampf(alpha * 0.45, 0.035, 0.14)
		_add_particle_quad(spark_vertices, spark_uvs, spark_colors, spark_indices, spark_point, 0.58 + alpha * 1.20, spark_color)


func _add_particle_quad(
	vertices: PackedVector3Array,
	uvs: PackedVector2Array,
	colors: PackedColorArray,
	indices: PackedInt32Array,
	center: Vector2,
	radius: float,
	color: Color
) -> void:
	var start := vertices.size()
	var half: Vector2 = Vector2(radius, radius)
	vertices.append(_v3(center + Vector2(-half.x, -half.y)))
	vertices.append(_v3(center + Vector2(half.x, -half.y)))
	vertices.append(_v3(center + Vector2(half.x, half.y)))
	vertices.append(_v3(center + Vector2(-half.x, half.y)))
	uvs.append(Vector2(0.0, 0.0))
	uvs.append(Vector2(1.0, 0.0))
	uvs.append(Vector2(1.0, 1.0))
	uvs.append(Vector2(0.0, 1.0))
	for _i in range(4):
		colors.append(color)
	indices.append(start)
	indices.append(start + 1)
	indices.append(start + 2)
	indices.append(start)
	indices.append(start + 2)
	indices.append(start + 3)


func _add_line_quad(
	vertices: PackedVector3Array,
	colors: PackedColorArray,
	indices: PackedInt32Array,
	a: Vector2,
	b: Vector2,
	width: float,
	color: Color
) -> void:
	var direction := b - a
	var length_squared := direction.length_squared()
	if length_squared < 0.01:
		return
	var normal: Vector2 = Vector2(-direction.y, direction.x).normalized() * width * 0.5
	var start := vertices.size()
	vertices.append(_v3(a - normal))
	vertices.append(_v3(a + normal))
	vertices.append(_v3(b + normal))
	vertices.append(_v3(b - normal))
	for _i in range(4):
		colors.append(color)
	indices.append(start)
	indices.append(start + 1)
	indices.append(start + 2)
	indices.append(start)
	indices.append(start + 2)
	indices.append(start + 3)


func _curved_bridge_point(a: Vector2, b: Vector2, curve_offset: Vector2, t: float) -> Vector2:
	return a.lerp(b, t) + curve_offset * sin(t * PI)


func _bridge_curve_offset(a: Vector2, b: Vector2, phase: float) -> Vector2:
	var direction := b - a
	if direction.length_squared() < 0.01:
		return Vector2.ZERO
	var normal := _line_normal(a, b)
	var length_factor: float = clampf(direction.length() / 180.0, 0.0, 1.0)
	var side: float = -1.0 if sin(phase) < 0.0 else 1.0
	return normal * side * (7.0 + length_factor * 16.0)


func _line_normal(a: Vector2, b: Vector2) -> Vector2:
	var direction := b - a
	if direction.length_squared() < 0.01:
		return Vector2.UP
	return Vector2(-direction.y, direction.x).normalized()


func _v3(point: Vector2) -> Vector3:
	return Vector3(point.x, point.y, 0.0)


func _commit_mesh(
	mesh: ArrayMesh,
	vertices: PackedVector3Array,
	colors: PackedColorArray,
	indices: PackedInt32Array,
	uvs: PackedVector2Array
) -> void:
	mesh.clear_surfaces()
	if vertices.is_empty():
		return
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_COLOR] = colors
	arrays[Mesh.ARRAY_INDEX] = indices
	if not uvs.is_empty():
		arrays[Mesh.ARRAY_TEX_UV] = uvs
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
