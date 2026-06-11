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


const CORE_PARTICLES := 2400
const FIELD_PARTICLES := 1700
const DUST_PARTICLES := 2400
const PARTICLE_COUNT := CORE_PARTICLES + FIELD_PARTICLES + DUST_PARTICLES
const CLUSTER_COUNT := 24
const MAX_LINKS := 6200
const CLOUD_RADIUS := 260.0
const FIELD_RADIUS := 320.0
const DUST_RADIUS := 360.0
const MAX_SPARKS := 520
const LINK_CELL_SIZE := 76.0
const LINK_DISTANCE_CORE := 92.0
const LINK_DISTANCE_FIELD := 116.0
const LINK_DISTANCE_BRIDGE := 178.0
const MAX_LINKS_PER_PARTICLE := 4
const BRIDGE_STRIDE := 9
const SPEECH_ENERGY_ADD := 0.22
const SPEECH_ENERGY_DECAY := 2.8
const SPEAKING_LINGER_HOLD := 1.8
const SPEAKING_LINGER_FADE := 1.8
const THINKING_PULSE_INTERVAL_MIN := 0.65
const THINKING_PULSE_INTERVAL_MAX := 1.8

const CYAN := {
	"hot": Color("#E8FBFF"),
	"primary": Color("#66E2FF"),
	"dim": Color("#2297D0"),
	"line": Color("#7CE6FF"),
	"line_dim": Color("#176A9A"),
	"glow": Color("#66DDFF"),
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
		"link_visibility": 0.54,
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
		"brightness": 1.68,
		"glow": 1.18,
		"breath_speed": 0.92,
		"breath_depth": 0.115,
		"link_visibility": 0.92,
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
var _cluster_anchors: Array[Vector3] = []
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
	set_process(true)


func _process(delta: float) -> void:
	_time += delta
	_center = size * 0.5
	_update_linger(delta)
	_update_params(delta)
	_update_palette(delta)
	_update_speech_energy(delta)
	_update_particles(delta)
	_update_thinking(delta)
	_update_links(delta)
	_rebuild_render_meshes()
	queue_redraw()


func _draw() -> void:
	draw_mesh(_dust_mesh, _particle_texture)
	draw_mesh(_link_glow_mesh, null)
	draw_mesh(_link_mesh, null)
	draw_mesh(_spark_mesh, _particle_texture)
	draw_mesh(_aura_mesh, _particle_texture)
	draw_mesh(_particle_mesh, _particle_texture)
	draw_mesh(_density_mesh, _particle_texture)
	draw_mesh(_cluster_mesh, _particle_texture)


func set_idle() -> void:
	if current_state == CloudState.SPEAKING:
		current_state = CloudState.IDLE
		_speaking_linger = SPEAKING_LINGER_HOLD + SPEAKING_LINGER_FADE
		_target_params = STATE_PARAMS[CloudState.SPEAKING].duplicate(true)
		_set_palette(CYAN)
		state_changed.emit("idle")
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


func notify_speech_tick() -> void:
	if current_state != CloudState.SPEAKING:
		return

	_speech_energy = clampf(_speech_energy + SPEECH_ENERGY_ADD, 0.0, 1.0)
	for particle_index in _speech_particle_indices:
		_particles[particle_index].pulse = maxf(_particles[particle_index].pulse, 0.42)


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
		particle.cluster_id = index if particle.cluster else _rng.randi_range(0, CLUSTER_COUNT - 1)
		particle.base_position = _generate_position(particle.layer, particle.cluster, particle.cluster_id)
		particle.current_position = particle.base_position
		particle.depth = clampf(particle.base_position.z / DUST_RADIUS, -1.0, 1.0)
		particle.density_weight = _density_weight_for_position(particle.base_position, particle.cluster_id, particle.layer)
		particle.size = _particle_size(particle.layer, particle.cluster)
		particle.base_brightness = _particle_brightness(particle.layer, particle.cluster)
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
	for index in range(CLUSTER_COUNT):
		var shell_mix := float(index) / maxf(float(CLUSTER_COUNT - 1), 1.0)
		var radius := lerpf(CLOUD_RADIUS * 0.16, CLOUD_RADIUS * 0.88, pow(shell_mix, 0.72))
		radius *= _rng.randf_range(0.72, 1.08)
		var theta := (float(index) / CLUSTER_COUNT) * TAU * 2.618 + _rng.randf_range(-0.58, 0.58)
		var phi := acos(_rng.randf_range(-0.78, 0.78))
		var anchor := Vector3(
			radius * sin(phi) * cos(theta),
			radius * sin(phi) * sin(theta),
			radius * cos(phi) * 0.72
		)
		if index < 5:
			anchor *= 0.28
		anchor += Vector3(
			_rng.randf_range(-28.0, 28.0),
			_rng.randf_range(-24.0, 24.0),
			_rng.randf_range(-22.0, 22.0)
		)
		_cluster_anchors.append(anchor)


func _generate_position(layer: int, cluster: bool, cluster_id: int) -> Vector3:
	var radius_limit := CLOUD_RADIUS if layer == 0 else FIELD_RADIUS if layer == 1 else DUST_RADIUS
	if cluster:
		return _cluster_anchors[cluster_id]

	if layer != 2 and _rng.randf() < (0.58 if layer == 0 else 0.38):
		var anchor := _cluster_anchors[cluster_id]
		var cluster_spread := _rng.randf_range(42.0, 126.0) if layer == 0 else _rng.randf_range(74.0, 168.0)
		var offset := _random_unit_vector() * pow(_rng.randf(), 0.95) * cluster_spread
		var blended := (anchor + offset).lerp(_random_orb_position(radius_limit, layer), 0.24 if layer == 0 else 0.38)
		return _shape_position(blended, layer, radius_limit)

	return _random_orb_position(radius_limit, layer)


func _random_orb_position(radius_limit: float, layer: int) -> Vector3:
	var exponent := 1.45 if layer == 0 else 0.92 if layer == 1 else 0.58
	var radius := pow(_rng.randf(), exponent) * radius_limit

	var theta := _rng.randf() * TAU
	var phi := acos(_rng.randf_range(-1.0, 1.0))
	var position := Vector3(
		radius * sin(phi) * cos(theta),
		radius * sin(phi) * sin(theta),
		radius * cos(phi)
	)
	var irregular := Vector3(
		_rng.randf_range(-0.12, 0.12),
		_rng.randf_range(-0.10, 0.10),
		_rng.randf_range(-0.11, 0.11)
	) * radius_limit
	position += irregular
	return _shape_position(position, layer, radius_limit)


func _shape_position(position: Vector3, layer: int, radius_limit: float) -> Vector3:
	if layer == 0:
		position *= Vector3(1.04, 0.96, 0.92)
	elif layer == 1:
		position *= Vector3(1.09, 1.01, 0.86)
	else:
		position *= Vector3(1.14, 1.05, 0.76)

	return position.limit_length(radius_limit * _rng.randf_range(0.84, 1.05))


func _random_unit_vector() -> Vector3:
	var theta := _rng.randf() * TAU
	var phi := acos(_rng.randf_range(-1.0, 1.0))
	return Vector3(sin(phi) * cos(theta), sin(phi) * sin(theta), cos(phi))


func _particle_size(layer: int, cluster: bool) -> float:
	if cluster:
		return _rng.randf_range(0.78, 1.28)
	if layer == 0:
		return _rng.randf_range(0.30, 0.92)
	if layer == 1:
		return _rng.randf_range(0.22, 0.72)
	return _rng.randf_range(0.14, 0.34)


func _particle_brightness(layer: int, cluster: bool) -> float:
	if cluster:
		return _rng.randf_range(1.35, 1.85)
	if layer == 0:
		return _rng.randf_range(0.28, 0.88)
	if layer == 1:
		return _rng.randf_range(0.12, 0.48)
	return _rng.randf_range(0.030, 0.15)


func _density_weight_for_position(position: Vector3, cluster_id: int, layer: int) -> float:
	if layer == 2:
		return 0.0
	var anchor := _cluster_anchors[cluster_id]
	var distance := position.distance_to(anchor)
	var radius := 78.0 if layer == 0 else 128.0
	return pow(1.0 - clampf(distance / radius, 0.0, 1.0), 1.8)


func _create_links() -> void:
	_links.clear()
	var candidates: Array[Dictionary] = []
	var seen := {}
	var grid := _build_link_grid()

	for particle_index in range(CORE_PARTICLES + FIELD_PARTICLES):
		var particle := _particles[particle_index]
		var cell: Vector3i = _grid_cell(particle.base_position)
		var nearby: Array[Dictionary] = []
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
							nearby.append({ "index": other_index, "d": distance_squared * bias })
		nearby.sort_custom(func(left, right): return left["d"] < right["d"])
		var local_count: int = mini(MAX_LINKS_PER_PARTICLE, nearby.size())
		for local_index in range(local_count):
			_add_link_candidate(candidates, seen, particle_index, int(nearby[local_index]["index"]), LINK_DISTANCE_FIELD, 1.0)

	for particle_index in range(0, CORE_PARTICLES + FIELD_PARTICLES, BRIDGE_STRIDE):
		var particle := _particles[particle_index]
		var bridge_target: int = _find_bridge_target(particle_index, particle.base_position)
		if bridge_target >= 0:
			_add_link_candidate(candidates, seen, particle_index, bridge_target, LINK_DISTANCE_BRIDGE, 1.28)

	candidates.sort_custom(func(left, right): return left["d"] < right["d"])
	var link_count := mini(MAX_LINKS, candidates.size())
	for index in range(link_count):
		var candidate: Dictionary = candidates[index]
		var link := CloudLink.new()
		link.a = candidate["a"]
		link.b = candidate["b"]
		var closeness := 1.0 - clampf(float(candidate["d"]) / (LINK_DISTANCE_BRIDGE * LINK_DISTANCE_BRIDGE), 0.0, 1.0)
		link.base_opacity = 0.014 + closeness * 0.075
		if _particles[link.a].cluster or _particles[link.b].cluster:
			link.base_opacity += 0.046
		link.phase = _rng.randf() * TAU
		link.speed = _rng.randf_range(0.18, 0.72)
		link.active_bias = _rng.randf_range(0.35, 1.0)
		_links.append(link)


func _build_link_grid() -> Dictionary:
	var grid := {}
	for particle_index in range(CORE_PARTICLES + FIELD_PARTICLES):
		var key: String = _grid_key(_grid_cell(_particles[particle_index].base_position))
		if not grid.has(key):
			grid[key] = []
		grid[key].append(particle_index)
	return grid


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


func _add_link_candidate(candidates: Array[Dictionary], seen: Dictionary, a: int, b: int, max_distance: float, bias: float) -> void:
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
	var cluster_bias := 0.62 if _particles[low].cluster or _particles[high].cluster else 1.0
	candidates.append({ "a": low, "b": high, "d": distance_squared * bias * cluster_bias })


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


func _update_particles(delta: float) -> void:
	var activity: float = _params["activity"]
	var drift_speed: float = _params["drift_speed"]
	var brightness_mult: float = _params["brightness"]
	var breath := 1.0 + sin(_time * TAU * float(_params["breath_speed"]) * 0.18) * float(_params["breath_depth"])
	var speech_expansion := _speech_energy * 0.10 if current_state == CloudState.SPEAKING else 0.0

	for particle in _particles:
		var layer_scale := 0.55 if particle.layer == 0 else 0.85 if particle.layer == 1 else 1.15
		var drift_phase := _time * drift_speed
		var seed_phase := particle.seed * TAU
		var noise_position := Vector3(
			sin(seed_phase + particle.noise_offset.x * 0.07 + drift_phase * 1.07) + cos(particle.noise_offset.z * 0.05 + drift_phase * 0.43),
			cos(seed_phase * 1.37 + particle.noise_offset.y * 0.06 + drift_phase * 0.91) + sin(particle.noise_offset.x * 0.04 + drift_phase * 0.37),
			sin(seed_phase * 0.73 + particle.noise_offset.z * 0.05 + drift_phase * 0.71) + cos(particle.noise_offset.y * 0.03 + drift_phase * 0.29)
		) * 0.5
		var speech_wave := 0.0
		if current_state == CloudState.SPEAKING:
			var distance_ratio := clampf(particle.base_position.length() / DUST_RADIUS, 0.0, 1.0)
			speech_wave = maxf(0.0, 1.0 - abs(distance_ratio - (1.0 - _speech_energy * 0.75)) * 2.0) * _speech_energy

		var target := particle.base_position * (breath + speech_expansion * layer_scale)
		target += noise_position * (10.0 + activity * 36.0) * layer_scale
		if current_state == CloudState.SPEAKING:
			target += particle.base_position.normalized() * speech_wave * 34.0
		particle.current_position = particle.current_position.lerp(target, minf(delta * 2.35, 1.0))
		particle.depth = clampf(particle.current_position.z / DUST_RADIUS, -1.0, 1.0)
		particle.projected_position = _project(particle.current_position)

		var center_factor := 1.0 - clampf(particle.current_position.length() / (DUST_RADIUS * 1.05), 0.0, 1.0)
		var density_light := particle.density_weight * (0.75 + center_factor * 0.55)
		var target_brightness := (particle.base_brightness + density_light + center_factor * 0.35 + speech_wave * 0.50 + particle.pulse) * brightness_mult
		particle.brightness = lerpf(particle.brightness, clampf(target_brightness, 0.0, 2.4), minf(delta * 4.2, 1.0))
		particle.pulse = move_toward(particle.pulse, 0.0, delta * 2.4)


func _project(position: Vector3) -> Vector2:
	var perspective := 1.0 + position.z / (DUST_RADIUS * 3.3)
	return size * 0.5 + Vector2(position.x, position.y) * perspective


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
	for index in range(_particles.size()):
		var particle := _particles[index]
		var depth_factor := clampf((particle.depth + 1.0) * 0.5, 0.22, 1.20)
		if particle.dust:
			var dust_alpha := clampf(particle.brightness * depth_factor * 0.54, 0.012, 0.18)
			var dust_radius := maxf(0.34, particle.size * depth_factor * 0.92)
			_add_particle_quad(dust_vertices, dust_uvs, dust_colors, dust_indices, particle.projected_position, dust_radius, Color(dim.r, dim.g, dim.b, dust_alpha))
			continue

		var brightness := clampf(particle.brightness * depth_factor, 0.0, 2.5)
		var particle_color := dim.lerp(primary, clampf(brightness, 0.0, 1.0))
		if particle.cluster:
			particle_color = particle_color.lerp(hot, clampf(brightness * 0.8, 0.0, 1.0))
		particle_color.a = clampf(0.10 + brightness * (0.44 if particle.layer == 0 else 0.32), 0.045, 0.92)
		var radius := particle.size * (0.64 + depth_factor * 0.68) * (1.0 + particle.pulse * 0.14)

		if particle.cluster:
			_add_particle_quad(cluster_vertices, cluster_uvs, cluster_colors, cluster_indices, particle.projected_position, radius * 1.45, particle_color)
		else:
			_add_particle_quad(particle_vertices, particle_uvs, particle_colors, particle_indices, particle.projected_position, radius, particle_color)

		if index % 2 == 0 and brightness >= 0.58:
			var phase := particle.seed * TAU + _time * (0.24 + particle.seed * 0.18)
			var offset_distance := 2.0 + particle.seed * 7.0
			var aura_point := particle.projected_position + Vector2(cos(phase), sin(phase * 1.37)) * offset_distance
			var aura_color := primary.lerp(hot, clampf((brightness - 0.55) * 0.65, 0.0, 1.0))
			aura_color.a = clampf(brightness * depth_factor * 0.11, 0.018, 0.18)
			_add_particle_quad(aura_vertices, aura_uvs, aura_colors, aura_indices, aura_point, 0.42 + particle.seed * 0.62, aura_color)

		if particle.layer == 0 and index % 2 == 0 and particle.density_weight >= 0.18:
			var density_brightness := clampf(particle.brightness * particle.density_weight * depth_factor, 0.0, 2.4)
			if density_brightness >= 0.18:
				var density_color := primary.lerp(hot, clampf(density_brightness * 0.48, 0.0, 1.0))
				density_color.a = clampf(density_brightness * 0.12, 0.014, 0.20)
				var density_phase := particle.seed * TAU
				var density_offset := Vector2(cos(density_phase), sin(density_phase)) * (1.2 + particle.seed * 3.2)
				_add_particle_quad(density_vertices, density_uvs, density_colors, density_indices, particle.projected_position + density_offset, 0.56 + particle.density_weight * 0.95, density_color)


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
		var depth_factor := clampf(((a.depth + b.depth) * 0.5 + 1.0) * 0.5, 0.25, 1.0)
		var alpha := clampf(link.opacity * depth_factor * 2.05, 0.0, 0.24)
		var color := line_dim.lerp(line, alpha * 4.0)
		var glow_color := color
		glow_color.a = alpha * 0.16
		color.a = alpha
		_add_line_quad(link_glow_vertices, link_glow_colors, link_glow_indices, a.projected_position, b.projected_position, 1.05 + alpha * 1.15, glow_color)
		_add_line_quad(link_vertices, link_colors, link_indices, a.projected_position, b.projected_position, 0.34 + alpha * 0.88, color)

		if link.opacity > 0.025 and sparks_drawn < MAX_SPARKS:
			var t := fmod(link.phase * 0.137 + _time * link.speed * 0.17, 1.0)
			var point := a.projected_position.lerp(b.projected_position, t)
			var spark_alpha := clampf(link.opacity * 0.70, 0.025, 0.20)
			var spark_color := line.lerp(hot, clampf(spark_alpha * 4.0, 0.0, 1.0))
			spark_color.a = spark_alpha
			_add_particle_quad(spark_vertices, spark_uvs, spark_colors, spark_indices, point, 0.55 + spark_alpha * 2.4, spark_color)
			sparks_drawn += 1


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
	var half := Vector2(radius, radius)
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
	var normal := Vector2(-direction.y, direction.x).normalized() * width * 0.5
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
