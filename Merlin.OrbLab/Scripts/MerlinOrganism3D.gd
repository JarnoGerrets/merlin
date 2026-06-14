extends Node3D
class_name MerlinOrganism3D

enum OrganismState { IDLE, LISTENING, THINKING, SPEAKING, EXECUTING, ERROR, CONFIRMATION }

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
	var speed_wobble := 0.0
	var speed_phase := 0.0
	var brightness := 1.0
	var generation := 0
	var can_split := true
	var tail := 0.22


class PendingEnergyBranch:
	var from_node := -1
	var node_index := -1
	var brightness := 1.0
	var generation := 0
	var can_split := true
	var age := 0.0
	var delay := 1.0


class ClusterActivation:
	var node_index := -1
	var age := 0.0
	var duration := 1.55
	var attack_duration := 0.16
	var hold_duration := 1.0
	var strength := 1.0
	var expansion := 0.0


class ClusterLifecycle:
	var birth_node := -1
	var death_node := -1
	var age := 0.0
	var duration := 3.2
	var birth_strength := 1.2
	var death_strength := 0.7


class ClusterSelectionFlash:
	var node_index := -1
	var age := 0.0
	var duration := 1.0


class ConnectionSelectionFlash:
	var connection_index := -1
	var age := 0.0
	var duration := 1.0


class SpeechRipple:
	var axis := Vector3.UP
	var phase := 0.0
	var age := 0.0
	var duration := 0.45
	var width := 0.32
	var strength := 1.0
	var spin := 1.0


class SpeechRegionBurst:
	var axis := Vector3.UP
	var age := 0.0
	var duration := 0.55
	var strength := 0.12
	var width := 0.52
	var direction := 1.0
	var phase := 0.0


const DEFAULT_STRUCTURAL_NODE_COUNT := 1700
const DEFAULT_GENERATION_SEED := 26062026
const DEFAULT_HUB_NODE_COUNT := 30
const DEFAULT_BRIGHT_CLUSTER_COUNT := 52
const DEFAULT_STRUCTURAL_FEATURE_CLUSTER_COUNT := 11
const DEFAULT_AMBIENT_DUST_NODE_COUNT := 1800
const DEFAULT_HUB_CLUSTER_PARTICLE_COUNT := 56
const DEFAULT_BRIGHT_CLUSTER_PARTICLE_COUNT := 58
const DEFAULT_CORE_CLUSTER_PARTICLE_COUNT := 1580
const DEFAULT_ORB_RADIUS := 2.65
const DEFAULT_CORE_RADIUS_FACTOR := 0.48
const DEFAULT_CENTER_VISUAL_SIZE := 1.0
const DEFAULT_STRUCTURAL_CORE_FRACTION := 0.24
const DEFAULT_STRUCTURAL_MID_FRACTION := 0.22
const DEFAULT_STRUCTURAL_SHELL_PROBABILITY := 0.56
const DEFAULT_STRUCTURAL_FILL_RADIUS_SCALE := 0.94
const DEFAULT_AMBIENT_DUST_INNER_RADIUS := 0.35
const DEFAULT_AMBIENT_DUST_OUTER_RADIUS := 1.18
const DEFAULT_MAX_CONNECTION_DISTANCE := 0.92
const DEFAULT_HUB_CONNECTION_DISTANCE := 1.20
const DEFAULT_SHAPE_X_SCALE := 1.0
const DEFAULT_SHAPE_Y_SCALE := 1.0
const DEFAULT_SHAPE_Z_SCALE := 1.0
const DEFAULT_CAMERA_PERSPECTIVE_ENABLED := true
const DEFAULT_CAMERA_FOV := 44.0
const DEFAULT_CAMERA_DISTANCE := 7.4
const MAX_PULSES := 36
const MAX_PENDING_ENERGY_BRANCHES := 32
const MAX_DESTINATION_FLASHES := 24
const MAX_CLUSTER_HALOS := 44
const DEFAULT_CLUSTER_HALO_INTENSITY := 5.4
const DEFAULT_CLUSTER_HALO_RADIUS_SCALE := 2.05
const DEFAULT_NATURAL_HALO_DUST_WEIGHT := 1.0
const DEFAULT_NATURAL_HALO_CONNECTION_WEIGHT := 1.0
const DEFAULT_NATURAL_HALO_ROUTE_WEIGHT := 1.15
const DEFAULT_NATURAL_HALO_BRIGHTNESS_WEIGHT := 1.0
const DEFAULT_NATURAL_HALO_MIN_SCORE := 0.16
const DEFAULT_SHARP_GEOMETRY_ALPHA_SCALE := 0.78
const DEFAULT_SHARP_HOT_MIX_SCALE := 0.80
const DEFAULT_CORE_GLOW_ALPHA_SCALE := 0.42
const DEFAULT_CORE_GLOW_RADIUS_SCALE := 1.0
const DEFAULT_CORE_PARTICLE_BRIGHTNESS_SCALE := 1.0
const DEFAULT_CORE_PARTICLE_SIZE_SCALE := 1.0
const DEFAULT_CACHED_SPEECH_MOTION_ENABLED := true
const DEFAULT_REAL_SPEECH_MOTION_ENABLED := false
const DEFAULT_REAL_SPEECH_MOTION_STRENGTH := 0.18
const DEFAULT_REAL_SPEECH_MOTION_SPEED := 3.2
const DEFAULT_REAL_SPEECH_MOTION_SMOOTHING := 4.8
const DEFAULT_REAL_SPEECH_MOTION_REGION_BLEND := 0.34
const DEFAULT_ENERGY_PULSE_MIN_SPEED := 0.34
const DEFAULT_ENERGY_PULSE_MAX_SPEED := 1.12
const DEFAULT_ENERGY_PULSE_SPEED_WOBBLE_AMOUNT := 0.10
const DEFAULT_CLUSTER_ACTIVATION_BASE_LIFT := 0.34
const DEFAULT_CLUSTER_ACTIVATION_BLOOM_BOOST := 0.46
const DEFAULT_CLUSTER_ACTIVATION_COOLDOWN_SECONDS := 3.0
const DEFAULT_CLUSTER_ACTIVATION_STRENGTH_SCALE := 1.0
const DEFAULT_CLUSTER_ACTIVATION_ATTACK_MIN := 0.12
const DEFAULT_CLUSTER_ACTIVATION_ATTACK_MAX := 0.18
const DEFAULT_CLUSTER_ACTIVATION_HOLD_MIN := 1.00
const DEFAULT_CLUSTER_ACTIVATION_HOLD_MAX := 1.18
const DEFAULT_CLUSTER_ACTIVATION_FADE_MIN := 1.05
const DEFAULT_CLUSTER_ACTIVATION_FADE_MAX := 1.45
const DEFAULT_CLUSTER_ACTIVATION_CORE_LIGHT := 2.65
const DEFAULT_CLUSTER_ACTIVATION_DUST_MIN := 18.0
const DEFAULT_CLUSTER_ACTIVATION_DUST_MAX := 140.0
const DEFAULT_CLUSTER_ACTIVATION_CONNECTION_MIN := 3.0
const DEFAULT_CLUSTER_ACTIVATION_CONNECTION_MAX := 16.0
const DEFAULT_CLUSTER_ACTIVATION_SEGMENT_MIN := 4.0
const DEFAULT_CLUSTER_ACTIVATION_SEGMENT_MAX := 18.0
const DEFAULT_HEAVY_CLUSTER_CONNECTION_CHANCE := 0.10
const DEFAULT_HEAVY_CLUSTER_CONNECTION_DISTANCE_SCALE := 0.92
const DEFAULT_HEAVY_CLUSTER_CONNECTION_WIDTH_SCALE := 1.0
const DEFAULT_CONNECTION_ALPHA_SCALE := 1.0
const DEFAULT_CONNECTION_WIDTH_SCALE := 1.0
const DEFAULT_ROUTE_CONNECTION_CHANCE := 0.16
const DEFAULT_ROUTE_CONNECTION_CLOSENESS_MIN := 0.24
const DEFAULT_ROUTE_CONNECTION_ALPHA_SCALE := 1.0
const DEFAULT_ROUTE_CONNECTION_WIDTH_SCALE := 1.0
const DEFAULT_ROUTE_CONNECTION_RENDER_BOOST := 0.18
const DEFAULT_ROUTE_CONNECTION_GLOW_ALPHA_SCALE := 1.0
const DEFAULT_ROUTE_CONNECTION_CORE_WIDTH_SCALE := 1.0
const DEFAULT_ROUTE_CONNECTION_GLOW_WIDTH_SCALE := 1.0
const DEFAULT_ROUTE_CONNECTION_HOT_MIX := 0.0
const DEFAULT_CLUSTER_LIFECYCLE_DURATION := 3.2
const DEFAULT_CLUSTER_LIFECYCLE_BIRTH_STRENGTH := 1.30
const DEFAULT_CLUSTER_LIFECYCLE_DEATH_STRENGTH := 0.72
const MAX_SPEECH_REGION_BURSTS := 7
const SPEECH_MORPH_REGION_COUNT := 48
const DEFAULT_DISPLAY_SCALE := 0.76
const ORB_FRAME_PROFILER_ENABLED := false
const ORB_FRAME_PROFILER_REPORT_SECONDS := 1.0
const ORB_FRAME_PROFILER_SPIKE_MS := 20.0
const SPEAKING_STARTUP_PROFILER_ENABLED := false
const SPEAKING_STARTUP_PROFILE_BUCKETS_MS := [0, 100, 250, 500, 1000, 2000, 3000, 4000, 5000, 6000, 7000]
const ORB_QUALITY_HIGH := 0
const ORB_QUALITY_SAFE := 1
const ORB_QUALITY_EMERGENCY := 2
const ORB_QUALITY_MODE := ORB_QUALITY_SAFE
const PRESENTATION_ROTATION_LIMIT := Vector3(0.34, 100000.0, 0.26)

const CYAN := {
	"hot": Color("#EAF8FF"),
	"node": Color("#4DBDFF"),
	"line": Color("#6799EE"),
	"dim": Color("#0A3F7A"),
	"dust": Color("#2F54EF"),
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
var _state_feature_overrides := {}
var _generation_seed := DEFAULT_GENERATION_SEED
var _structural_node_count := DEFAULT_STRUCTURAL_NODE_COUNT
var _hub_node_count := DEFAULT_HUB_NODE_COUNT
var _bright_cluster_count := DEFAULT_BRIGHT_CLUSTER_COUNT
var _structural_feature_cluster_count := DEFAULT_STRUCTURAL_FEATURE_CLUSTER_COUNT
var _ambient_dust_node_count := DEFAULT_AMBIENT_DUST_NODE_COUNT
var _hub_cluster_particle_count := DEFAULT_HUB_CLUSTER_PARTICLE_COUNT
var _bright_cluster_particle_count := DEFAULT_BRIGHT_CLUSTER_PARTICLE_COUNT
var _core_cluster_particle_count := DEFAULT_CORE_CLUSTER_PARTICLE_COUNT
var _orb_radius := DEFAULT_ORB_RADIUS
var _core_radius_factor := DEFAULT_CORE_RADIUS_FACTOR
var _center_visual_size := DEFAULT_CENTER_VISUAL_SIZE
var _structural_core_fraction := DEFAULT_STRUCTURAL_CORE_FRACTION
var _structural_mid_fraction := DEFAULT_STRUCTURAL_MID_FRACTION
var _structural_shell_probability := DEFAULT_STRUCTURAL_SHELL_PROBABILITY
var _structural_fill_radius_scale := DEFAULT_STRUCTURAL_FILL_RADIUS_SCALE
var _ambient_dust_inner_radius := DEFAULT_AMBIENT_DUST_INNER_RADIUS
var _ambient_dust_outer_radius := DEFAULT_AMBIENT_DUST_OUTER_RADIUS
var _max_connection_distance := DEFAULT_MAX_CONNECTION_DISTANCE
var _hub_connection_distance := DEFAULT_HUB_CONNECTION_DISTANCE
var _display_scale := DEFAULT_DISPLAY_SCALE
var _shape_x_scale := DEFAULT_SHAPE_X_SCALE
var _shape_y_scale := DEFAULT_SHAPE_Y_SCALE
var _shape_z_scale := DEFAULT_SHAPE_Z_SCALE
var _camera_perspective_enabled := DEFAULT_CAMERA_PERSPECTIVE_ENABLED
var _camera_fov := DEFAULT_CAMERA_FOV
var _camera_distance := DEFAULT_CAMERA_DISTANCE
var _nodes: Array[OrganismNode] = []
var _dust: Array[OrganismNode] = []
var _connections: Array[OrganismConnection] = []
var _pulses: Array[EnergyPulse] = []
var _pending_energy_branches: Array[PendingEnergyBranch] = []
var _cluster_activations: Array[ClusterActivation] = []
var _cluster_lifecycle_events: Array[ClusterLifecycle] = []
var _cluster_selection_flashes: Array[ClusterSelectionFlash] = []
var _connection_selection_flashes: Array[ConnectionSelectionFlash] = []
var _cluster_activation_cooldowns := {}
var _cluster_lab_overrides := {}
var _connection_lab_overrides := {}
var _node_connections: Array = []
var _node_dust_indices: Array = []
var _node_cluster_flash_targets: Array[int] = []
var _route_connection_indices: Array[int] = []
var _cluster_node_indices: Array[int] = []
var _large_thinking_cluster_indices: Array[int] = []
var _cluster_halo_node_indices: Array[int] = []
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
var _cluster_halo_intensity := DEFAULT_CLUSTER_HALO_INTENSITY
var _cluster_halo_radius_scale := DEFAULT_CLUSTER_HALO_RADIUS_SCALE
var _natural_halo_dust_weight := DEFAULT_NATURAL_HALO_DUST_WEIGHT
var _natural_halo_connection_weight := DEFAULT_NATURAL_HALO_CONNECTION_WEIGHT
var _natural_halo_route_weight := DEFAULT_NATURAL_HALO_ROUTE_WEIGHT
var _natural_halo_brightness_weight := DEFAULT_NATURAL_HALO_BRIGHTNESS_WEIGHT
var _natural_halo_min_score := DEFAULT_NATURAL_HALO_MIN_SCORE
var _sharp_geometry_alpha_scale := DEFAULT_SHARP_GEOMETRY_ALPHA_SCALE
var _sharp_hot_mix_scale := DEFAULT_SHARP_HOT_MIX_SCALE
var _core_glow_alpha_scale := DEFAULT_CORE_GLOW_ALPHA_SCALE
var _core_glow_radius_scale := DEFAULT_CORE_GLOW_RADIUS_SCALE
var _core_particle_brightness_scale := DEFAULT_CORE_PARTICLE_BRIGHTNESS_SCALE
var _core_particle_size_scale := DEFAULT_CORE_PARTICLE_SIZE_SCALE
var _cached_speech_motion_enabled := DEFAULT_CACHED_SPEECH_MOTION_ENABLED
var _real_speech_motion_enabled := DEFAULT_REAL_SPEECH_MOTION_ENABLED
var _real_speech_motion_strength := DEFAULT_REAL_SPEECH_MOTION_STRENGTH
var _real_speech_motion_speed := DEFAULT_REAL_SPEECH_MOTION_SPEED
var _real_speech_motion_smoothing := DEFAULT_REAL_SPEECH_MOTION_SMOOTHING
var _real_speech_motion_region_blend := DEFAULT_REAL_SPEECH_MOTION_REGION_BLEND
var _cyan_hot := Color(CYAN["hot"])
var _cyan_node := Color(CYAN["node"])
var _cyan_line := Color(CYAN["line"])
var _cyan_dust := Color(CYAN["dust"])
var _halo_outer_color := Color("#2824F3")
var _halo_inner_color := Color("#ACAAFB")
var _energy_pulse_min_speed := DEFAULT_ENERGY_PULSE_MIN_SPEED
var _energy_pulse_max_speed := DEFAULT_ENERGY_PULSE_MAX_SPEED
var _energy_pulse_speed_wobble_amount := DEFAULT_ENERGY_PULSE_SPEED_WOBBLE_AMOUNT
var _cluster_activation_base_lift := DEFAULT_CLUSTER_ACTIVATION_BASE_LIFT
var _cluster_activation_bloom_boost := DEFAULT_CLUSTER_ACTIVATION_BLOOM_BOOST
var _cluster_activation_cooldown_seconds := DEFAULT_CLUSTER_ACTIVATION_COOLDOWN_SECONDS
var _cluster_activation_strength_scale := DEFAULT_CLUSTER_ACTIVATION_STRENGTH_SCALE
var _cluster_activation_attack_min := DEFAULT_CLUSTER_ACTIVATION_ATTACK_MIN
var _cluster_activation_attack_max := DEFAULT_CLUSTER_ACTIVATION_ATTACK_MAX
var _cluster_activation_hold_min := DEFAULT_CLUSTER_ACTIVATION_HOLD_MIN
var _cluster_activation_hold_max := DEFAULT_CLUSTER_ACTIVATION_HOLD_MAX
var _cluster_activation_fade_min := DEFAULT_CLUSTER_ACTIVATION_FADE_MIN
var _cluster_activation_fade_max := DEFAULT_CLUSTER_ACTIVATION_FADE_MAX
var _cluster_activation_core_light := DEFAULT_CLUSTER_ACTIVATION_CORE_LIGHT
var _cluster_activation_dust_min := DEFAULT_CLUSTER_ACTIVATION_DUST_MIN
var _cluster_activation_dust_max := DEFAULT_CLUSTER_ACTIVATION_DUST_MAX
var _cluster_activation_connection_min := DEFAULT_CLUSTER_ACTIVATION_CONNECTION_MIN
var _cluster_activation_connection_max := DEFAULT_CLUSTER_ACTIVATION_CONNECTION_MAX
var _cluster_activation_segment_min := DEFAULT_CLUSTER_ACTIVATION_SEGMENT_MIN
var _cluster_activation_segment_max := DEFAULT_CLUSTER_ACTIVATION_SEGMENT_MAX
var _heavy_cluster_connection_chance := DEFAULT_HEAVY_CLUSTER_CONNECTION_CHANCE
var _heavy_cluster_connection_distance_scale := DEFAULT_HEAVY_CLUSTER_CONNECTION_DISTANCE_SCALE
var _heavy_cluster_connection_width_scale := DEFAULT_HEAVY_CLUSTER_CONNECTION_WIDTH_SCALE
var _connection_alpha_scale := DEFAULT_CONNECTION_ALPHA_SCALE
var _connection_width_scale := DEFAULT_CONNECTION_WIDTH_SCALE
var _route_connection_chance := DEFAULT_ROUTE_CONNECTION_CHANCE
var _route_connection_closeness_min := DEFAULT_ROUTE_CONNECTION_CLOSENESS_MIN
var _route_connection_alpha_scale := DEFAULT_ROUTE_CONNECTION_ALPHA_SCALE
var _route_connection_width_scale := DEFAULT_ROUTE_CONNECTION_WIDTH_SCALE
var _route_connection_render_boost := DEFAULT_ROUTE_CONNECTION_RENDER_BOOST
var _route_connection_glow_alpha_scale := DEFAULT_ROUTE_CONNECTION_GLOW_ALPHA_SCALE
var _route_connection_core_width_scale := DEFAULT_ROUTE_CONNECTION_CORE_WIDTH_SCALE
var _route_connection_glow_width_scale := DEFAULT_ROUTE_CONNECTION_GLOW_WIDTH_SCALE
var _route_connection_hot_mix := DEFAULT_ROUTE_CONNECTION_HOT_MIX
var _cluster_lifecycle_duration := DEFAULT_CLUSTER_LIFECYCLE_DURATION
var _cluster_lifecycle_birth_strength := DEFAULT_CLUSTER_LIFECYCLE_BIRTH_STRENGTH
var _cluster_lifecycle_death_strength := DEFAULT_CLUSTER_LIFECYCLE_DEATH_STRENGTH
var _rotation := Vector3.ZERO
var _rotation_velocity := Vector3.ZERO
var _rotation_target_velocity := Vector3(0.006, 0.040, 0.004)
var _rotation_target_timer := 0.0
var _manual_rotation_enabled := false
var _manual_rotation := Vector3.ZERO
var _lab_zoom := 1.0
var _pulse_timer := 0.0
var _speech_energy := 0.0
var _speech_energy_override := -1.0
var _speech_global_morph := 0.0
var _speech_tick_count := 0
var _speech_region_timer := 0.0
var _speech_global_timer := 0.0
var _speech_ripple_timer := 0.0
var _speech_region_axes: Array[Vector3] = []
var _speech_region_targets: Array[Vector3] = []
var _speech_region_weights: Array[float] = []
var _speech_region_directions: Array[float] = []
var _speech_ripples: Array[SpeechRipple] = []
var _speech_region_bursts: Array[SpeechRegionBurst] = []
var _speech_morph_region_axes: Array[Vector3] = []
var _speech_morph_region_offsets: Array[Vector3] = []
var _node_speech_primary_regions: Array[int] = []
var _node_speech_secondary_regions: Array[int] = []
var _node_speech_secondary_weights: Array[float] = []
var _node_speech_local_weights: Array[float] = []
var _dust_speech_primary_regions: Array[int] = []
var _dust_speech_secondary_regions: Array[int] = []
var _dust_speech_secondary_weights: Array[float] = []
var _dust_speech_local_weights: Array[float] = []

var _graph_root: Node3D
var _node_multimesh: MultiMeshInstance3D
var _hub_multimesh: MultiMeshInstance3D
var _dust_multimesh: MultiMeshInstance3D
var _cluster_halo_multimesh: MultiMeshInstance3D
var _pulse_multimesh: MultiMeshInstance3D
var _core_multimesh: MultiMeshInstance3D
var _line_mesh_instance: MeshInstance3D
var _glow_line_mesh_instance: MeshInstance3D
var _pulse_line_mesh_instance: MeshInstance3D
var _pulse_glow_line_mesh_instance: MeshInstance3D
var _camera: Camera3D
var _line_material: StandardMaterial3D
var _glow_line_material: StandardMaterial3D
var _pulse_line_material: StandardMaterial3D
var _pulse_glow_line_material: StandardMaterial3D
var _node_material: StandardMaterial3D
var _hub_material: StandardMaterial3D
var _dust_material: StandardMaterial3D
var _pulse_material: StandardMaterial3D
var _core_material: StandardMaterial3D
var _cluster_halo_material: ShaderMaterial
var _orb_frame_index := 0
var _pulse_connection_mesh_active := false
var _orb_profile_last_report_usec := 0
var _orb_profile_frames := 0
var _orb_profile_total_ms := 0.0
var _orb_profile_max_ms := 0.0
var _orb_profile_nodes_ms := 0.0
var _orb_profile_dust_ms := 0.0
var _orb_profile_connections_ms := 0.0
var _orb_profile_pulses_ms := 0.0
var _orb_profile_materials_ms := 0.0
var _orb_profile_palette_ms := 0.0
var _orb_profile_speech_maps_ms := 0.0
var _orb_profile_pulse_mesh_ms := 0.0
var _orb_profile_node_transform_sets := 0
var _orb_profile_node_color_sets := 0
var _orb_profile_dust_transform_sets := 0
var _orb_profile_dust_color_sets := 0
var _orb_profile_pulse_mesh_builds := 0
var _orb_profile_spikes := 0
var _speaking_profile_active := false
var _speaking_profile_started_usec := 0
var _speaking_profile_next_bucket_index := 0
var _speaking_profile_frame_index := 0
var _speaking_profile_energy_events := 0
var _speaking_profile_first_energy_usec := 0
var _speaking_profile_peak_frame_ms := 0.0
var _speaking_profile_peak_frame_index := 0
var _speaking_profile_region_pick_count := 0
var _speaking_profile_ripple_spawn_count := 0
var _speaking_profile_speech_map_rebuild_count := 0
var _speaking_profile_pulse_mesh_build_count := 0
var _speaking_profile_array_mesh_resource_count := 0
var _speaking_profile_packed_array_count := 0
var _speaking_profile_speech_map_clear_count := 0
var _speaking_profile_region_array_reset_count := 0
var _speaking_profile_frame_node_transform_sets := 0
var _speaking_profile_frame_node_color_sets := 0
var _speaking_profile_frame_dust_transform_sets := 0
var _speaking_profile_frame_dust_color_sets := 0
var _speaking_profile_frame_pulse_transform_sets := 0
var _speaking_profile_frame_pulse_color_sets := 0
var _speaking_profile_frame_pulse_mesh_builds := 0
var _speaking_profile_frame_array_mesh_resources := 0
var _speaking_profile_frame_packed_arrays := 0
var _speaking_profile_frame_speech_map_rebuilds := 0
var _speaking_profile_frame_region_picks := 0
var _speaking_profile_frame_region_array_resets := 0
var _speaking_profile_frame_ripple_spawns := 0
var _speaking_profile_frame_node_morph_ms := 0.0
var _speaking_profile_frame_node_position_ms := 0.0
var _speaking_profile_frame_node_speech_lookup_ms := 0.0
var _speaking_profile_frame_node_color_calc_ms := 0.0
var _speaking_profile_frame_node_transform_upload_ms := 0.0
var _speaking_profile_frame_node_color_upload_ms := 0.0
var _speaking_profile_frame_dust_morph_ms := 0.0
var _speaking_profile_frame_dust_position_ms := 0.0
var _speaking_profile_frame_dust_speech_lookup_ms := 0.0
var _speaking_profile_frame_dust_color_calc_ms := 0.0
var _speaking_profile_frame_dust_transform_upload_ms := 0.0
var _speaking_profile_frame_dust_color_upload_ms := 0.0
var _speaking_profile_frame_node_displacement_total := 0.0
var _speaking_profile_frame_node_displacement_max := 0.0
var _speaking_profile_frame_node_displacement_count := 0
var _speaking_profile_frame_dust_displacement_total := 0.0
var _speaking_profile_frame_dust_displacement_max := 0.0
var _speaking_profile_frame_dust_displacement_count := 0
var _speaking_profile_frame_node_color_delta_total := 0.0
var _speaking_profile_frame_node_color_delta_max := 0.0
var _speaking_profile_frame_node_color_delta_count := 0
var _speaking_profile_frame_dust_color_delta_total := 0.0
var _speaking_profile_frame_dust_color_delta_max := 0.0
var _speaking_profile_frame_dust_color_delta_count := 0
var _speaking_profile_energy_first_values: Array[float] = []
var _speaking_profile_max_energy_first_second := 0.0
var _speaking_profile_segment_frame_counts := [0, 0, 0]
var _speaking_profile_segment_frame_ms := [0.0, 0.0, 0.0]
var _speaking_profile_segment_nodes_ms := [0.0, 0.0, 0.0]
var _speaking_profile_segment_dust_ms := [0.0, 0.0, 0.0]
var _speaking_profile_segment_node_morph_ms := [0.0, 0.0, 0.0]
var _speaking_profile_segment_dust_morph_ms := [0.0, 0.0, 0.0]
var _speaking_profile_segment_node_transform_upload_ms := [0.0, 0.0, 0.0]
var _speaking_profile_segment_dust_transform_upload_ms := [0.0, 0.0, 0.0]
var _speaking_profile_segment_node_color_upload_ms := [0.0, 0.0, 0.0]
var _speaking_profile_segment_dust_color_upload_ms := [0.0, 0.0, 0.0]
var _speaking_profile_segment_energy_total := [0.0, 0.0, 0.0]
var _speaking_profile_segment_energy_max := [0.0, 0.0, 0.0]
var _speaking_profile_previous_node_colors: Array[Color] = []
var _speaking_profile_previous_dust_colors: Array[Color] = []

const STATE_FEATURES := [
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

const STATE_FEATURE_LABELS := {
	"breathing": "Breathing Scale",
	"rotation": "Organic Rotation",
	"pulses": "Thinking Pulses",
	"pulse_connections": "Pulse Lines",
	"speech_motion": "Speech Motion",
	"node_motion": "Node Movement",
	"dust_motion": "Dust Movement",
	"cluster_halos": "Cluster Halos",
	"core_glow": "Core Glow",
}


func _ready() -> void:
	_rng.seed = _generation_seed
	_reset_speech_regions()
	_build_scene()
	_rebuild_generated_orb()
	set_process(true)


func _rebuild_generated_orb() -> void:
	_rng.seed = _generation_seed
	_pulses.clear()
	_pending_energy_branches.clear()
	_cluster_activations.clear()
	_cluster_lifecycle_events.clear()
	_cluster_selection_flashes.clear()
	_connection_selection_flashes.clear()
	_cluster_activation_cooldowns.clear()
	_node_speech_light.clear()
	_dust_speech_light.clear()
	_connection_speech_light.clear()
	_reset_speech_regions()
	_generate_nodes()
	_build_speech_morph_regions()
	_build_node_dust_indices()
	_build_node_cluster_flash_targets()
	_generate_connections()
	_build_cluster_halo_indices()
	_setup_multimeshes()


func _dust_node_count() -> int:
	return _ambient_dust_node_count + _hub_node_count * _hub_cluster_particle_count + _bright_cluster_count * _bright_cluster_particle_count + _core_cluster_particle_count


func _core_radius() -> float:
	return _orb_radius * _core_radius_factor


func _visual_core_radius() -> float:
	return _core_radius() * _center_visual_size


func _center_visual_t(position: Vector3) -> float:
	return 1.0 - clampf(position.length() / maxf(_visual_core_radius(), 0.001), 0.0, 1.0)


func set_idle() -> void:
	_set_state(OrganismState.IDLE, _cyan_palette(), 0.18, 1.00)


func set_thinking() -> void:
	_set_state(OrganismState.THINKING, _cyan_palette(), 0.42, 1.12)


func set_listening() -> void:
	_set_state(OrganismState.LISTENING, _cyan_palette(), 0.30, 1.06)


func start_speaking_startup_profile() -> void:
	if not SPEAKING_STARTUP_PROFILER_ENABLED:
		return
	_speaking_profile_active = true
	_speaking_profile_started_usec = Time.get_ticks_usec()
	_speaking_profile_next_bucket_index = 0
	_speaking_profile_frame_index = 0
	_speaking_profile_energy_events = 0
	_speaking_profile_first_energy_usec = 0
	_speaking_profile_peak_frame_ms = 0.0
	_speaking_profile_peak_frame_index = 0
	_speaking_profile_region_pick_count = 0
	_speaking_profile_ripple_spawn_count = 0
	_speaking_profile_speech_map_rebuild_count = 0
	_speaking_profile_pulse_mesh_build_count = 0
	_speaking_profile_array_mesh_resource_count = 0
	_speaking_profile_packed_array_count = 0
	_speaking_profile_speech_map_clear_count = 0
	_speaking_profile_region_array_reset_count = 0
	_speaking_profile_energy_first_values.clear()
	_speaking_profile_max_energy_first_second = 0.0
	for index in range(3):
		_speaking_profile_segment_frame_counts[index] = 0
		_speaking_profile_segment_frame_ms[index] = 0.0
		_speaking_profile_segment_nodes_ms[index] = 0.0
		_speaking_profile_segment_dust_ms[index] = 0.0
		_speaking_profile_segment_node_morph_ms[index] = 0.0
		_speaking_profile_segment_dust_morph_ms[index] = 0.0
		_speaking_profile_segment_node_transform_upload_ms[index] = 0.0
		_speaking_profile_segment_dust_transform_upload_ms[index] = 0.0
		_speaking_profile_segment_node_color_upload_ms[index] = 0.0
		_speaking_profile_segment_dust_color_upload_ms[index] = 0.0
		_speaking_profile_segment_energy_total[index] = 0.0
		_speaking_profile_segment_energy_max[index] = 0.0
	var snapshot_started_usec := Time.get_ticks_usec()
	_snapshot_speaking_profile_colors()
	var snapshot_ms := _elapsed_ms_since_usec(snapshot_started_usec)
	var node_instance_count := 0
	if _node_multimesh != null and _node_multimesh.multimesh != null:
		node_instance_count += _node_multimesh.multimesh.instance_count
	if _hub_multimesh != null and _hub_multimesh.multimesh != null:
		node_instance_count += _hub_multimesh.multimesh.instance_count
	var dust_instance_count := 0
	if _dust_multimesh != null and _dust_multimesh.multimesh != null:
		dust_instance_count = _dust_multimesh.multimesh.instance_count
	var pulse_instance_count := 0
	if _pulse_multimesh != null and _pulse_multimesh.multimesh != null:
		pulse_instance_count = _pulse_multimesh.multimesh.instance_count
	print("Speaking startup profile: begin. StateBefore: %s. Quality: %s. Nodes: %s. Dust: %s. Connections: %s. Pulses: %s. Ripples: %s. NodeInstances: %s. DustInstances: %s. PulseInstances: %s. ColorSnapshotMs: %.3f" % [
		_organism_state_name(current_state),
		_orb_quality_name(),
		_nodes.size(),
		_dust.size(),
		_connections.size(),
		_pulses.size(),
		_speech_ripples.size(),
		node_instance_count,
		dust_instance_count,
		pulse_instance_count,
		snapshot_ms
	])


func set_speaking() -> void:
	var started_usec := Time.get_ticks_usec()
	var state_started_usec := started_usec
	_set_state(OrganismState.SPEAKING, _cyan_palette(), 0.58, 1.00)
	var state_ms := _elapsed_ms_since_usec(state_started_usec)
	var activation_started_usec := Time.get_ticks_usec()
	_speech_energy = maxf(_speech_energy, 0.62)
	_speech_global_morph = maxf(_speech_global_morph, 0.18)
	_speech_region_timer = 0.0
	_speech_global_timer = 0.0
	_speech_ripple_timer = 0.0
	var activation_ms := _elapsed_ms_since_usec(activation_started_usec)
	var region_started_usec := Time.get_ticks_usec()
	_pick_speech_region()
	_spawn_speech_region_burst(_rng.randf(), 1.0)
	_spawn_speech_region_burst(_rng.randf(), -1.0)
	var region_ms := _elapsed_ms_since_usec(region_started_usec)
	if _speaking_profile_active:
		print("Speaking startup profile: set_speaking. StateMs: %.3f. ActivationMs: %.3f. InitialRegionPickMs: %.3f. TotalMs: %.3f. SpeechEnergy: %.3f. RegionCount: %s" % [
			state_ms,
			activation_ms,
			region_ms,
			_elapsed_ms_since_usec(started_usec),
			_speech_energy,
			_speech_region_axes.size()
		])


func play_tool_execution(_duration: float = 2.5) -> void:
	_set_state(OrganismState.EXECUTING, _cyan_palette(), 0.54, 1.20)


func play_error(_duration: float = 3.0) -> void:
	_set_state(OrganismState.ERROR, RED, 0.48, 1.18)


func play_confirmation() -> void:
	_set_state(OrganismState.CONFIRMATION, AMBER, 0.24, 1.04)


func set_speech_energy_override(value: float) -> void:
	_speech_energy_override = clampf(value, -1.0, 1.0)
	if current_state == OrganismState.SPEAKING and _speech_energy_override >= 0.0:
		_speech_energy = clampf(_speech_energy_override, 0.0, 0.96)
		_target_activity = maxf(_target_activity, lerpf(0.58, 0.92, _speech_energy_override))
		_speech_global_morph = maxf(_speech_global_morph, lerpf(0.10, 0.58, _speech_energy_override))


func set_manual_rotation_enabled(enabled: bool) -> void:
	_manual_rotation_enabled = enabled
	if enabled:
		_rotation_velocity = Vector3.ZERO


func set_manual_rotation(yaw: float, pitch: float) -> void:
	_manual_rotation.y = yaw
	_manual_rotation.x = clampf(pitch, -PRESENTATION_ROTATION_LIMIT.x, PRESENTATION_ROTATION_LIMIT.x)


func set_orb_lab_zoom(value: float) -> void:
	_lab_zoom = clampf(value, 0.35, 3.0)
	if _camera != null:
		_apply_camera_settings()


func _apply_camera_settings() -> void:
	if _camera == null:
		return
	_camera.position = Vector3(0.0, 0.0, _camera_distance)
	if _camera_perspective_enabled:
		_camera.projection = Camera3D.PROJECTION_PERSPECTIVE
		_camera.fov = clampf(_camera_fov / _lab_zoom, 12.0, 95.0)
	else:
		_camera.projection = Camera3D.PROJECTION_ORTHOGONAL
		_camera.size = 5.35 / _lab_zoom
	_camera.look_at(Vector3.ZERO, Vector3.UP)


func trigger_cluster_birth_death() -> void:
	var candidates: Array[int] = []
	for node_index in _large_thinking_cluster_indices:
		if node_index >= 0 and node_index < _nodes.size() and not candidates.has(node_index):
			candidates.append(node_index)
	for node_index in _cluster_halo_node_indices:
		if node_index >= 0 and node_index < _nodes.size() and not candidates.has(node_index):
			candidates.append(node_index)
	if candidates.size() < 2:
		return
	candidates.shuffle()
	var event := ClusterLifecycle.new()
	event.birth_node = candidates[0]
	event.death_node = candidates[1]
	event.age = 0.0
	event.duration = _cluster_lifecycle_duration
	event.birth_strength = _cluster_lifecycle_birth_strength
	event.death_strength = _cluster_lifecycle_death_strength
	_cluster_lifecycle_events.append(event)
	_add_cluster_activation(event.birth_node, event.birth_strength)


func get_orb_lab_state_features() -> Dictionary:
	var result := {}
	for state_name in _orb_lab_state_names():
		var state_map := {}
		for feature_name in STATE_FEATURES:
			state_map[feature_name] = _state_feature_enabled_for_name(state_name, feature_name)
		result[state_name] = state_map
	return result


func get_orb_lab_state_feature_labels() -> Dictionary:
	return STATE_FEATURE_LABELS.duplicate(true)


func apply_orb_lab_state_feature(state_name: String, feature_name: String, enabled: bool) -> bool:
	if not _orb_lab_state_names().has(state_name):
		return false
	if not STATE_FEATURES.has(feature_name):
		return false
	if not _state_feature_overrides.has(state_name):
		_state_feature_overrides[state_name] = {}
	var state_map: Dictionary = _state_feature_overrides[state_name]
	state_map[feature_name] = enabled
	_state_feature_overrides[state_name] = state_map
	if state_name == _organism_state_name(current_state):
		_apply_current_state_feature_side_effects()
	return true


func reset_orb_lab_state_features(state_name: String = "") -> bool:
	if state_name.is_empty():
		_state_feature_overrides.clear()
		_apply_current_state_feature_side_effects()
		return true
	if not _orb_lab_state_names().has(state_name):
		return false
	_state_feature_overrides.erase(state_name)
	if state_name == _organism_state_name(current_state):
		_apply_current_state_feature_side_effects()
	return true


func get_orb_lab_clusters() -> Array[Dictionary]:
	var clusters: Array[Dictionary] = []
	var display_index: int = 1
	for node_index in _cluster_halo_node_indices:
		if node_index < 0 or node_index >= _nodes.size():
			continue
		var dust_count := 0
		if node_index < _node_dust_indices.size():
			dust_count = _node_dust_indices[node_index].size()
		var override: Dictionary = {}
		if _cluster_lab_overrides.has(node_index):
			override = _cluster_lab_overrides[node_index] as Dictionary
		clusters.append({
			"id": node_index,
			"label": "Cluster %02d" % display_index,
			"node_index": node_index,
			"dust_count": dust_count,
			"halo_scale": float(override.get("halo_scale", 1.0)),
			"brightness_scale": float(override.get("brightness_scale", 1.0)),
		})
		display_index += 1
	return clusters


func select_orb_lab_cluster(cluster_id: int) -> bool:
	if cluster_id < 0 or cluster_id >= _nodes.size():
		return false
	if not _cluster_halo_node_indices.has(cluster_id):
		return false
	var flash: ClusterSelectionFlash = ClusterSelectionFlash.new()
	flash.node_index = cluster_id
	flash.age = 0.0
	flash.duration = 1.0
	_cluster_selection_flashes.append(flash)
	return true


func apply_orb_lab_cluster_parameter(cluster_id: int, parameter_name: String, value: float) -> bool:
	if cluster_id < 0 or cluster_id >= _nodes.size() or not _cluster_halo_node_indices.has(cluster_id):
		return false
	var override: Dictionary = {}
	if _cluster_lab_overrides.has(cluster_id):
		override = _cluster_lab_overrides[cluster_id] as Dictionary
	if parameter_name == "halo_scale":
		override["halo_scale"] = clampf(value, 0.1, 5.0)
	elif parameter_name == "brightness_scale":
		override["brightness_scale"] = clampf(value, 0.0, 5.0)
	else:
		return false
	_cluster_lab_overrides[cluster_id] = override
	return true


func reset_orb_lab_cluster(cluster_id: int) -> bool:
	if not _cluster_halo_node_indices.has(cluster_id):
		return false
	_cluster_lab_overrides.erase(cluster_id)
	select_orb_lab_cluster(cluster_id)
	return true


func get_orb_lab_connections() -> Array[Dictionary]:
	var items: Array[Dictionary] = []
	for connection_index in range(_connections.size()):
		var connection: OrganismConnection = _connections[connection_index]
		var override: Dictionary = {}
		if _connection_lab_overrides.has(connection_index):
			override = _connection_lab_overrides[connection_index] as Dictionary
		items.append({
			"id": connection_index,
			"label": "Connection %04d" % connection_index,
			"a": connection.a,
			"b": connection.b,
			"route": bool(override.get("route", connection.route)),
			"alpha_scale": float(override.get("alpha_scale", 1.0)),
			"width_scale": float(override.get("width_scale", 1.0)),
			"glow_scale": float(override.get("glow_scale", 1.0)),
			"hot_mix": float(override.get("hot_mix", 0.0)),
		})
	return items


func get_orb_lab_connection_overrides() -> Array[Dictionary]:
	var items: Array[Dictionary] = []
	for connection_id_variant in _connection_lab_overrides.keys():
		var connection_index: int = int(connection_id_variant)
		if connection_index < 0 or connection_index >= _connections.size():
			continue
		var connection: OrganismConnection = _connections[connection_index]
		var override: Dictionary = _connection_lab_overrides[connection_id_variant] as Dictionary
		items.append({
			"id": connection_index,
			"a": connection.a,
			"b": connection.b,
			"route": bool(override.get("route", connection.route)),
			"alpha_scale": float(override.get("alpha_scale", 1.0)),
			"width_scale": float(override.get("width_scale", 1.0)),
			"glow_scale": float(override.get("glow_scale", 1.0)),
			"hot_mix": float(override.get("hot_mix", 0.0)),
		})
	return items


func select_orb_lab_connection(connection_id: int) -> bool:
	if connection_id < 0 or connection_id >= _connections.size():
		return false
	var flash: ConnectionSelectionFlash = ConnectionSelectionFlash.new()
	flash.connection_index = connection_id
	flash.age = 0.0
	flash.duration = 1.0
	_connection_selection_flashes.append(flash)
	return true


func apply_orb_lab_connection_parameter(connection_id: int, parameter_name: String, value) -> bool:
	if connection_id < 0 or connection_id >= _connections.size():
		return false
	var override: Dictionary = {}
	if _connection_lab_overrides.has(connection_id):
		override = _connection_lab_overrides[connection_id] as Dictionary
	if parameter_name == "route":
		override["route"] = bool(value)
	elif parameter_name == "alpha_scale":
		override["alpha_scale"] = clampf(float(value), 0.0, 8.0)
	elif parameter_name == "width_scale":
		override["width_scale"] = clampf(float(value), 0.1, 12.0)
	elif parameter_name == "glow_scale":
		override["glow_scale"] = clampf(float(value), 0.0, 12.0)
	elif parameter_name == "hot_mix":
		override["hot_mix"] = clampf(float(value), 0.0, 1.0)
	else:
		return false
	_connection_lab_overrides[connection_id] = override
	_rebuild_static_connection_meshes()
	return true


func reset_orb_lab_connection(connection_id: int) -> bool:
	if connection_id < 0 or connection_id >= _connections.size():
		return false
	_connection_lab_overrides.erase(connection_id)
	_rebuild_static_connection_meshes()
	select_orb_lab_connection(connection_id)
	return true


func pick_orb_lab_item(screen_position: Vector2, viewport_size: Vector2) -> Dictionary:
	var cluster_pick := _pick_orb_lab_cluster(screen_position, viewport_size)
	var connection_pick := _pick_orb_lab_connection(screen_position, viewport_size)
	if not cluster_pick.is_empty() and connection_pick.is_empty():
		return cluster_pick
	if cluster_pick.is_empty() and not connection_pick.is_empty():
		return connection_pick
	if not cluster_pick.is_empty() and not connection_pick.is_empty():
		if float(cluster_pick.get("distance", 99999.0)) <= float(connection_pick.get("distance", 99999.0)) * 1.35:
			return cluster_pick
		return connection_pick
	return {}


func apply_orb_builder_tool(tool_name: String, screen_position: Vector2, viewport_size: Vector2) -> Dictionary:
	match tool_name:
		"cluster":
			return _apply_builder_cluster_brush(screen_position, viewport_size)
		"cluster_remove":
			return _apply_builder_cluster_remove_brush(screen_position, viewport_size)
		"particle":
			return _apply_builder_particle_brush(screen_position)
		"particle_remove":
			return _apply_builder_particle_remove_brush(screen_position)
		"highway":
			return _apply_builder_highway_brush(screen_position, viewport_size)
		"highway_remove":
			return _apply_builder_highway_remove_brush(screen_position, viewport_size)
		_:
			return { "message": "Unknown builder tool" }


func get_orb_builder_snapshot() -> Dictionary:
	var node_items: Array = []
	for node in _nodes:
		node_items.append(_serialize_builder_node(node))
	var dust_items: Array = []
	for dust in _dust:
		dust_items.append(_serialize_builder_node(dust))
	var connection_items: Array = []
	for connection in _connections:
		connection_items.append({
			"a": connection.a,
			"b": connection.b,
			"base_alpha": connection.base_alpha,
			"width": connection.width,
			"phase": connection.phase,
			"route": connection.route,
		})
	return {
		"nodes": node_items,
		"dust": dust_items,
		"connections": connection_items,
	}


func apply_orb_builder_snapshot(snapshot: Dictionary) -> bool:
	if not snapshot.has("nodes") or not snapshot.has("dust") or not snapshot.has("connections"):
		return false
	_nodes.clear()
	_dust.clear()
	_connections.clear()
	_seen_connection_keys.clear()
	_node_connections.clear()
	_route_connection_indices.clear()
	_cluster_node_indices.clear()
	_large_thinking_cluster_indices.clear()

	var node_items: Array = snapshot.get("nodes", [])
	for node_variant in node_items:
		var node_data: Dictionary = node_variant as Dictionary
		_nodes.append(_deserialize_builder_node(node_data))
	for index in range(_nodes.size()):
		_node_connections.append([])
		if _nodes[index].hub:
			_cluster_node_indices.append(index)
			_large_thinking_cluster_indices.append(index)

	var dust_items: Array = snapshot.get("dust", [])
	for dust_variant in dust_items:
		var dust_data: Dictionary = dust_variant as Dictionary
		_dust.append(_deserialize_builder_node(dust_data))

	var connection_items: Array = snapshot.get("connections", [])
	for connection_variant in connection_items:
		var connection_data: Dictionary = connection_variant as Dictionary
		var connection := OrganismConnection.new()
		connection.a = int(connection_data.get("a", 0))
		connection.b = int(connection_data.get("b", 0))
		if connection.a < 0 or connection.b < 0 or connection.a >= _nodes.size() or connection.b >= _nodes.size() or connection.a == connection.b:
			continue
		connection.base_alpha = float(connection_data.get("base_alpha", 0.25))
		connection.width = float(connection_data.get("width", 0.006))
		connection.phase = float(connection_data.get("phase", 0.0))
		connection.route = bool(connection_data.get("route", false))
		var low := mini(connection.a, connection.b)
		var high := maxi(connection.a, connection.b)
		var key := "%s:%s" % [low, high]
		if _seen_connection_keys.has(key):
			continue
		connection.a = low
		connection.b = high
		_seen_connection_keys[key] = true
		var connection_index := _connections.size()
		_connections.append(connection)
		var a_connections: Array = _node_connections[connection.a]
		var b_connections: Array = _node_connections[connection.b]
		a_connections.append(connection_index)
		b_connections.append(connection_index)
		if connection.route:
			_route_connection_indices.append(connection_index)
	_rebuild_after_builder_mutation(true)
	return true


func _serialize_builder_node(node: OrganismNode) -> Dictionary:
	return {
		"base_position": [node.base_position.x, node.base_position.y, node.base_position.z],
		"radius": node.radius,
		"brightness": node.brightness,
		"phase": node.phase,
		"hub": node.hub,
		"source_node": node.source_node,
	}


func _deserialize_builder_node(data: Dictionary) -> OrganismNode:
	var node := OrganismNode.new()
	var position_data: Array = data.get("base_position", [0.0, 0.0, 0.0])
	node.base_position = Vector3(
		float(position_data[0]) if position_data.size() > 0 else 0.0,
		float(position_data[1]) if position_data.size() > 1 else 0.0,
		float(position_data[2]) if position_data.size() > 2 else 0.0
	)
	node.current_position = node.base_position
	node.radius = float(data.get("radius", 0.035))
	node.brightness = float(data.get("brightness", 1.0))
	node.phase = float(data.get("phase", 0.0))
	node.hub = bool(data.get("hub", false))
	node.source_node = int(data.get("source_node", -1))
	return node


func get_orb_lab_parameters() -> Array[Dictionary]:
	return [
		_lab_int_parameter("generation_seed", _generation_seed, DEFAULT_GENERATION_SEED, "Generation", true),
		_lab_parameter("cluster_halo_intensity", _cluster_halo_intensity, DEFAULT_CLUSTER_HALO_INTENSITY, "Glow"),
		_lab_parameter("cluster_halo_radius_scale", _cluster_halo_radius_scale, DEFAULT_CLUSTER_HALO_RADIUS_SCALE, "Glow"),
		_lab_parameter("natural_halo_dust_weight", _natural_halo_dust_weight, DEFAULT_NATURAL_HALO_DUST_WEIGHT, "Orb Builder"),
		_lab_parameter("natural_halo_connection_weight", _natural_halo_connection_weight, DEFAULT_NATURAL_HALO_CONNECTION_WEIGHT, "Orb Builder"),
		_lab_parameter("natural_halo_route_weight", _natural_halo_route_weight, DEFAULT_NATURAL_HALO_ROUTE_WEIGHT, "Orb Builder"),
		_lab_parameter("natural_halo_brightness_weight", _natural_halo_brightness_weight, DEFAULT_NATURAL_HALO_BRIGHTNESS_WEIGHT, "Orb Builder"),
		_lab_parameter("natural_halo_min_score", _natural_halo_min_score, DEFAULT_NATURAL_HALO_MIN_SCORE, "Orb Builder"),
		_lab_parameter("sharp_geometry_alpha_scale", _sharp_geometry_alpha_scale, DEFAULT_SHARP_GEOMETRY_ALPHA_SCALE, "Glow"),
		_lab_parameter("sharp_hot_mix_scale", _sharp_hot_mix_scale, DEFAULT_SHARP_HOT_MIX_SCALE, "Glow"),
		_lab_parameter("core_glow_alpha_scale", _core_glow_alpha_scale, DEFAULT_CORE_GLOW_ALPHA_SCALE, "Center"),
		_lab_parameter("core_glow_radius_scale", _core_glow_radius_scale, DEFAULT_CORE_GLOW_RADIUS_SCALE, "Center"),
		_lab_parameter("core_particle_brightness_scale", _core_particle_brightness_scale, DEFAULT_CORE_PARTICLE_BRIGHTNESS_SCALE, "Center", true),
		_lab_parameter("core_particle_size_scale", _core_particle_size_scale, DEFAULT_CORE_PARTICLE_SIZE_SCALE, "Center", true),
		_lab_parameter("center_visual_size", _center_visual_size, DEFAULT_CENTER_VISUAL_SIZE, "Center", true),
		_lab_parameter("structural_core_fraction", _structural_core_fraction, DEFAULT_STRUCTURAL_CORE_FRACTION, "Distribution", true),
		_lab_parameter("structural_mid_fraction", _structural_mid_fraction, DEFAULT_STRUCTURAL_MID_FRACTION, "Distribution", true),
		_lab_parameter("structural_shell_probability", _structural_shell_probability, DEFAULT_STRUCTURAL_SHELL_PROBABILITY, "Distribution", true),
		_lab_parameter("structural_fill_radius_scale", _structural_fill_radius_scale, DEFAULT_STRUCTURAL_FILL_RADIUS_SCALE, "Distribution", true),
		_lab_parameter("ambient_dust_inner_radius", _ambient_dust_inner_radius, DEFAULT_AMBIENT_DUST_INNER_RADIUS, "Distribution", true),
		_lab_parameter("ambient_dust_outer_radius", _ambient_dust_outer_radius, DEFAULT_AMBIENT_DUST_OUTER_RADIUS, "Distribution", true),
		_lab_bool_parameter("cached_speech_motion_enabled", _cached_speech_motion_enabled, DEFAULT_CACHED_SPEECH_MOTION_ENABLED, "Motion"),
		_lab_bool_parameter("real_speech_motion_enabled", _real_speech_motion_enabled, DEFAULT_REAL_SPEECH_MOTION_ENABLED, "Motion"),
		_lab_parameter("real_speech_motion_strength", _real_speech_motion_strength, DEFAULT_REAL_SPEECH_MOTION_STRENGTH, "Motion"),
		_lab_parameter("real_speech_motion_speed", _real_speech_motion_speed, DEFAULT_REAL_SPEECH_MOTION_SPEED, "Motion"),
		_lab_parameter("real_speech_motion_smoothing", _real_speech_motion_smoothing, DEFAULT_REAL_SPEECH_MOTION_SMOOTHING, "Motion"),
		_lab_parameter("real_speech_motion_region_blend", _real_speech_motion_region_blend, DEFAULT_REAL_SPEECH_MOTION_REGION_BLEND, "Motion"),
		_lab_parameter("energy_pulse_min_speed", _energy_pulse_min_speed, DEFAULT_ENERGY_PULSE_MIN_SPEED, "Motion"),
		_lab_parameter("energy_pulse_max_speed", _energy_pulse_max_speed, DEFAULT_ENERGY_PULSE_MAX_SPEED, "Motion"),
		_lab_parameter("energy_pulse_speed_wobble", _energy_pulse_speed_wobble_amount, DEFAULT_ENERGY_PULSE_SPEED_WOBBLE_AMOUNT, "Motion"),
		_lab_parameter("cluster_activation_base_lift", _cluster_activation_base_lift, DEFAULT_CLUSTER_ACTIVATION_BASE_LIFT, "Thinking"),
		_lab_parameter("cluster_activation_bloom_boost", _cluster_activation_bloom_boost, DEFAULT_CLUSTER_ACTIVATION_BLOOM_BOOST, "Thinking"),
		_lab_parameter("cluster_activation_cooldown_seconds", _cluster_activation_cooldown_seconds, DEFAULT_CLUSTER_ACTIVATION_COOLDOWN_SECONDS, "Thinking"),
		_lab_parameter("cluster_activation_strength_scale", _cluster_activation_strength_scale, DEFAULT_CLUSTER_ACTIVATION_STRENGTH_SCALE, "Thinking"),
		_lab_parameter("cluster_activation_attack_min", _cluster_activation_attack_min, DEFAULT_CLUSTER_ACTIVATION_ATTACK_MIN, "Thinking"),
		_lab_parameter("cluster_activation_attack_max", _cluster_activation_attack_max, DEFAULT_CLUSTER_ACTIVATION_ATTACK_MAX, "Thinking"),
		_lab_parameter("cluster_activation_hold_min", _cluster_activation_hold_min, DEFAULT_CLUSTER_ACTIVATION_HOLD_MIN, "Thinking"),
		_lab_parameter("cluster_activation_hold_max", _cluster_activation_hold_max, DEFAULT_CLUSTER_ACTIVATION_HOLD_MAX, "Thinking"),
		_lab_parameter("cluster_activation_fade_min", _cluster_activation_fade_min, DEFAULT_CLUSTER_ACTIVATION_FADE_MIN, "Thinking"),
		_lab_parameter("cluster_activation_fade_max", _cluster_activation_fade_max, DEFAULT_CLUSTER_ACTIVATION_FADE_MAX, "Thinking"),
		_lab_parameter("cluster_activation_core_light", _cluster_activation_core_light, DEFAULT_CLUSTER_ACTIVATION_CORE_LIGHT, "Thinking"),
		_lab_parameter("cluster_activation_dust_min", _cluster_activation_dust_min, DEFAULT_CLUSTER_ACTIVATION_DUST_MIN, "Thinking"),
		_lab_parameter("cluster_activation_dust_max", _cluster_activation_dust_max, DEFAULT_CLUSTER_ACTIVATION_DUST_MAX, "Thinking"),
		_lab_parameter("cluster_activation_connection_min", _cluster_activation_connection_min, DEFAULT_CLUSTER_ACTIVATION_CONNECTION_MIN, "Thinking"),
		_lab_parameter("cluster_activation_connection_max", _cluster_activation_connection_max, DEFAULT_CLUSTER_ACTIVATION_CONNECTION_MAX, "Thinking"),
		_lab_parameter("cluster_activation_segment_min", _cluster_activation_segment_min, DEFAULT_CLUSTER_ACTIVATION_SEGMENT_MIN, "Thinking"),
		_lab_parameter("cluster_activation_segment_max", _cluster_activation_segment_max, DEFAULT_CLUSTER_ACTIVATION_SEGMENT_MAX, "Thinking"),
		_lab_parameter("cluster_lifecycle_duration", _cluster_lifecycle_duration, DEFAULT_CLUSTER_LIFECYCLE_DURATION, "Cluster Life"),
		_lab_parameter("cluster_lifecycle_birth_strength", _cluster_lifecycle_birth_strength, DEFAULT_CLUSTER_LIFECYCLE_BIRTH_STRENGTH, "Cluster Life"),
		_lab_parameter("cluster_lifecycle_death_strength", _cluster_lifecycle_death_strength, DEFAULT_CLUSTER_LIFECYCLE_DEATH_STRENGTH, "Cluster Life"),
		_lab_color_parameter("cyan_hot", _cyan_hot, Color(CYAN["hot"]), "Colors"),
		_lab_color_parameter("cyan_node", _cyan_node, Color(CYAN["node"]), "Colors"),
		_lab_color_parameter("cyan_line", _cyan_line, Color(CYAN["line"]), "Colors"),
		_lab_color_parameter("cyan_dust", _cyan_dust, Color(CYAN["dust"]), "Colors"),
		_lab_color_parameter("halo_outer_color", _halo_outer_color, Color("#2824F3"), "Colors"),
		_lab_color_parameter("halo_inner_color", _halo_inner_color, Color("#ACAAFB"), "Colors"),
		_lab_parameter("structural_node_count", _structural_node_count, DEFAULT_STRUCTURAL_NODE_COUNT, "Structure", true),
		_lab_parameter("hub_node_count", _hub_node_count, DEFAULT_HUB_NODE_COUNT, "Structure", true),
		_lab_parameter("bright_cluster_count", _bright_cluster_count, DEFAULT_BRIGHT_CLUSTER_COUNT, "Structure", true),
		_lab_parameter("structural_feature_cluster_count", _structural_feature_cluster_count, DEFAULT_STRUCTURAL_FEATURE_CLUSTER_COUNT, "Structure", true),
		_lab_parameter("ambient_dust_node_count", _ambient_dust_node_count, DEFAULT_AMBIENT_DUST_NODE_COUNT, "Structure", true),
		_lab_parameter("hub_cluster_particle_count", _hub_cluster_particle_count, DEFAULT_HUB_CLUSTER_PARTICLE_COUNT, "Structure", true),
		_lab_parameter("bright_cluster_particle_count", _bright_cluster_particle_count, DEFAULT_BRIGHT_CLUSTER_PARTICLE_COUNT, "Structure", true),
		_lab_parameter("core_cluster_particle_count", _core_cluster_particle_count, DEFAULT_CORE_CLUSTER_PARTICLE_COUNT, "Center", true),
		_lab_parameter("orb_radius", _orb_radius, DEFAULT_ORB_RADIUS, "Shape", true),
		_lab_parameter("core_radius_factor", _core_radius_factor, DEFAULT_CORE_RADIUS_FACTOR, "Center", true),
		_lab_parameter("display_scale", _display_scale, DEFAULT_DISPLAY_SCALE, "Shape"),
		_lab_parameter("shape_x_scale", _shape_x_scale, DEFAULT_SHAPE_X_SCALE, "Shape", true),
		_lab_parameter("shape_y_scale", _shape_y_scale, DEFAULT_SHAPE_Y_SCALE, "Shape", true),
		_lab_parameter("shape_z_scale", _shape_z_scale, DEFAULT_SHAPE_Z_SCALE, "Shape", true),
		_lab_bool_parameter("camera_perspective_enabled", _camera_perspective_enabled, DEFAULT_CAMERA_PERSPECTIVE_ENABLED, "Camera"),
		_lab_parameter("camera_fov", _camera_fov, DEFAULT_CAMERA_FOV, "Camera"),
		_lab_parameter("camera_distance", _camera_distance, DEFAULT_CAMERA_DISTANCE, "Camera"),
		_lab_parameter("max_connection_distance", _max_connection_distance, DEFAULT_MAX_CONNECTION_DISTANCE, "Connections", true),
		_lab_parameter("hub_connection_distance", _hub_connection_distance, DEFAULT_HUB_CONNECTION_DISTANCE, "Connections", true),
		_lab_parameter("heavy_cluster_connection_chance", _heavy_cluster_connection_chance, DEFAULT_HEAVY_CLUSTER_CONNECTION_CHANCE, "Connections", true),
		_lab_parameter("heavy_cluster_connection_distance_scale", _heavy_cluster_connection_distance_scale, DEFAULT_HEAVY_CLUSTER_CONNECTION_DISTANCE_SCALE, "Connections", true),
		_lab_parameter("heavy_cluster_connection_width_scale", _heavy_cluster_connection_width_scale, DEFAULT_HEAVY_CLUSTER_CONNECTION_WIDTH_SCALE, "Connections", true),
		_lab_parameter("connection_alpha_scale", _connection_alpha_scale, DEFAULT_CONNECTION_ALPHA_SCALE, "Connection Highways", true),
		_lab_parameter("connection_width_scale", _connection_width_scale, DEFAULT_CONNECTION_WIDTH_SCALE, "Connection Highways", true),
		_lab_parameter("route_connection_chance", _route_connection_chance, DEFAULT_ROUTE_CONNECTION_CHANCE, "Connection Highways", true),
		_lab_parameter("route_connection_closeness_min", _route_connection_closeness_min, DEFAULT_ROUTE_CONNECTION_CLOSENESS_MIN, "Connection Highways", true),
		_lab_parameter("route_connection_alpha_scale", _route_connection_alpha_scale, DEFAULT_ROUTE_CONNECTION_ALPHA_SCALE, "Connection Highways", true),
		_lab_parameter("route_connection_width_scale", _route_connection_width_scale, DEFAULT_ROUTE_CONNECTION_WIDTH_SCALE, "Connection Highways", true),
		_lab_parameter("route_connection_render_boost", _route_connection_render_boost, DEFAULT_ROUTE_CONNECTION_RENDER_BOOST, "Connection Highways", true),
		_lab_parameter("route_connection_glow_alpha_scale", _route_connection_glow_alpha_scale, DEFAULT_ROUTE_CONNECTION_GLOW_ALPHA_SCALE, "Connection Highways", true),
		_lab_parameter("route_connection_core_width_scale", _route_connection_core_width_scale, DEFAULT_ROUTE_CONNECTION_CORE_WIDTH_SCALE, "Connection Highways", true),
		_lab_parameter("route_connection_glow_width_scale", _route_connection_glow_width_scale, DEFAULT_ROUTE_CONNECTION_GLOW_WIDTH_SCALE, "Connection Highways", true),
		_lab_parameter("route_connection_hot_mix", _route_connection_hot_mix, DEFAULT_ROUTE_CONNECTION_HOT_MIX, "Connection Highways", true),
	]


func apply_orb_lab_parameter(parameter_name: String, value) -> bool:
	if parameter_name == "generation_seed":
		_generation_seed = int(round(float(value)))
		_rebuild_generated_orb()
	elif parameter_name == "cluster_halo_intensity":
		_cluster_halo_intensity = maxf(float(value), 0.0)
	elif parameter_name == "cluster_halo_radius_scale":
		_cluster_halo_radius_scale = maxf(float(value), 0.0)
	elif parameter_name == "natural_halo_dust_weight":
		_natural_halo_dust_weight = clampf(float(value), 0.0, 5.0)
		_build_cluster_halo_indices()
	elif parameter_name == "natural_halo_connection_weight":
		_natural_halo_connection_weight = clampf(float(value), 0.0, 5.0)
		_build_cluster_halo_indices()
	elif parameter_name == "natural_halo_route_weight":
		_natural_halo_route_weight = clampf(float(value), 0.0, 5.0)
		_build_cluster_halo_indices()
	elif parameter_name == "natural_halo_brightness_weight":
		_natural_halo_brightness_weight = clampf(float(value), 0.0, 5.0)
		_build_cluster_halo_indices()
	elif parameter_name == "natural_halo_min_score":
		_natural_halo_min_score = clampf(float(value), 0.0, 1.0)
		_build_cluster_halo_indices()
	elif parameter_name == "sharp_geometry_alpha_scale":
		_sharp_geometry_alpha_scale = clampf(float(value), 0.0, 2.0)
	elif parameter_name == "sharp_hot_mix_scale":
		_sharp_hot_mix_scale = clampf(float(value), 0.0, 2.0)
	elif parameter_name == "core_glow_alpha_scale":
		_core_glow_alpha_scale = clampf(float(value), 0.0, 2.0)
	elif parameter_name == "core_glow_radius_scale":
		_core_glow_radius_scale = clampf(float(value), 0.05, 4.0)
	elif parameter_name == "core_particle_brightness_scale":
		_core_particle_brightness_scale = clampf(float(value), 0.0, 3.0)
		_rebuild_generated_orb()
	elif parameter_name == "core_particle_size_scale":
		_core_particle_size_scale = clampf(float(value), 0.05, 5.0)
		_rebuild_generated_orb()
	elif parameter_name == "center_visual_size":
		_center_visual_size = clampf(float(value), 0.10, 3.0)
		_rebuild_generated_orb()
	elif parameter_name == "structural_core_fraction":
		_structural_core_fraction = clampf(float(value), 0.0, 0.75)
		_rebuild_generated_orb()
	elif parameter_name == "structural_mid_fraction":
		_structural_mid_fraction = clampf(float(value), 0.0, 0.75)
		_rebuild_generated_orb()
	elif parameter_name == "structural_shell_probability":
		_structural_shell_probability = clampf(float(value), 0.0, 1.0)
		_rebuild_generated_orb()
	elif parameter_name == "structural_fill_radius_scale":
		_structural_fill_radius_scale = clampf(float(value), 0.10, 1.35)
		_rebuild_generated_orb()
	elif parameter_name == "ambient_dust_inner_radius":
		_ambient_dust_inner_radius = clampf(float(value), 0.0, 1.5)
		_rebuild_generated_orb()
	elif parameter_name == "ambient_dust_outer_radius":
		_ambient_dust_outer_radius = clampf(float(value), 0.05, 2.0)
		_rebuild_generated_orb()
	elif parameter_name == "cached_speech_motion_enabled":
		_cached_speech_motion_enabled = bool(value)
	elif parameter_name == "real_speech_motion_enabled":
		_real_speech_motion_enabled = bool(value)
	elif parameter_name == "real_speech_motion_strength":
		_real_speech_motion_strength = clampf(float(value), 0.0, 1.5)
	elif parameter_name == "real_speech_motion_speed":
		_real_speech_motion_speed = clampf(float(value), 0.0, 16.0)
	elif parameter_name == "real_speech_motion_smoothing":
		_real_speech_motion_smoothing = clampf(float(value), 0.0, 24.0)
	elif parameter_name == "real_speech_motion_region_blend":
		_real_speech_motion_region_blend = clampf(float(value), 0.0, 1.0)
	elif parameter_name == "energy_pulse_min_speed":
		_energy_pulse_min_speed = maxf(float(value), 0.01)
		_energy_pulse_max_speed = maxf(_energy_pulse_max_speed, _energy_pulse_min_speed)
	elif parameter_name == "energy_pulse_max_speed":
		_energy_pulse_max_speed = maxf(float(value), _energy_pulse_min_speed)
	elif parameter_name == "energy_pulse_speed_wobble":
		_energy_pulse_speed_wobble_amount = clampf(float(value), 0.0, 0.95)
	elif parameter_name == "cluster_activation_base_lift":
		_cluster_activation_base_lift = maxf(float(value), 0.0)
	elif parameter_name == "cluster_activation_bloom_boost":
		_cluster_activation_bloom_boost = maxf(float(value), 0.0)
	elif parameter_name == "cluster_activation_cooldown_seconds":
		_cluster_activation_cooldown_seconds = maxf(float(value), 0.0)
	elif parameter_name == "cluster_activation_strength_scale":
		_cluster_activation_strength_scale = clampf(float(value), 0.0, 4.0)
	elif parameter_name == "cluster_activation_attack_min":
		_cluster_activation_attack_min = maxf(float(value), 0.001)
	elif parameter_name == "cluster_activation_attack_max":
		_cluster_activation_attack_max = maxf(float(value), 0.001)
	elif parameter_name == "cluster_activation_hold_min":
		_cluster_activation_hold_min = maxf(float(value), 0.0)
	elif parameter_name == "cluster_activation_hold_max":
		_cluster_activation_hold_max = maxf(float(value), 0.0)
	elif parameter_name == "cluster_activation_fade_min":
		_cluster_activation_fade_min = maxf(float(value), 0.001)
	elif parameter_name == "cluster_activation_fade_max":
		_cluster_activation_fade_max = maxf(float(value), 0.001)
	elif parameter_name == "cluster_activation_core_light":
		_cluster_activation_core_light = maxf(float(value), 0.0)
	elif parameter_name == "cluster_activation_dust_min":
		_cluster_activation_dust_min = maxf(float(value), 0.0)
	elif parameter_name == "cluster_activation_dust_max":
		_cluster_activation_dust_max = maxf(float(value), 0.0)
	elif parameter_name == "cluster_activation_connection_min":
		_cluster_activation_connection_min = maxf(float(value), 0.0)
	elif parameter_name == "cluster_activation_connection_max":
		_cluster_activation_connection_max = maxf(float(value), 0.0)
	elif parameter_name == "cluster_activation_segment_min":
		_cluster_activation_segment_min = maxf(float(value), 0.0)
	elif parameter_name == "cluster_activation_segment_max":
		_cluster_activation_segment_max = maxf(float(value), 0.0)
	elif parameter_name == "cluster_lifecycle_duration":
		_cluster_lifecycle_duration = maxf(float(value), 0.2)
	elif parameter_name == "cluster_lifecycle_birth_strength":
		_cluster_lifecycle_birth_strength = maxf(float(value), 0.0)
	elif parameter_name == "cluster_lifecycle_death_strength":
		_cluster_lifecycle_death_strength = clampf(float(value), 0.0, 1.0)
	elif parameter_name == "structural_node_count":
		_structural_node_count = clampi(int(round(float(value))), 1, 5000)
		_rebuild_generated_orb()
	elif parameter_name == "hub_node_count":
		_hub_node_count = clampi(int(round(float(value))), 0, 120)
		_rebuild_generated_orb()
	elif parameter_name == "bright_cluster_count":
		_bright_cluster_count = clampi(int(round(float(value))), 0, 180)
		_rebuild_generated_orb()
	elif parameter_name == "structural_feature_cluster_count":
		_structural_feature_cluster_count = clampi(int(round(float(value))), 0, 64)
		_rebuild_generated_orb()
	elif parameter_name == "ambient_dust_node_count":
		_ambient_dust_node_count = clampi(int(round(float(value))), 0, 12000)
		_rebuild_generated_orb()
	elif parameter_name == "hub_cluster_particle_count":
		_hub_cluster_particle_count = clampi(int(round(float(value))), 0, 400)
		_rebuild_generated_orb()
	elif parameter_name == "bright_cluster_particle_count":
		_bright_cluster_particle_count = clampi(int(round(float(value))), 0, 400)
		_rebuild_generated_orb()
	elif parameter_name == "core_cluster_particle_count":
		_core_cluster_particle_count = clampi(int(round(float(value))), 0, 8000)
		_rebuild_generated_orb()
	elif parameter_name == "orb_radius":
		_orb_radius = clampf(float(value), 0.8, 5.0)
		_rebuild_generated_orb()
	elif parameter_name == "core_radius_factor":
		_core_radius_factor = clampf(float(value), 0.08, 0.95)
		_rebuild_generated_orb()
	elif parameter_name == "display_scale":
		_display_scale = clampf(float(value), 0.25, 1.5)
	elif parameter_name == "shape_x_scale":
		_shape_x_scale = clampf(float(value), 0.3, 2.0)
		_rebuild_generated_orb()
	elif parameter_name == "shape_y_scale":
		_shape_y_scale = clampf(float(value), 0.3, 2.0)
		_rebuild_generated_orb()
	elif parameter_name == "shape_z_scale":
		_shape_z_scale = clampf(float(value), 0.3, 2.0)
		_rebuild_generated_orb()
	elif parameter_name == "camera_perspective_enabled":
		_camera_perspective_enabled = bool(value)
		_apply_camera_settings()
	elif parameter_name == "camera_fov":
		_camera_fov = clampf(float(value), 18.0, 85.0)
		_apply_camera_settings()
	elif parameter_name == "camera_distance":
		_camera_distance = clampf(float(value), 3.0, 14.0)
		_apply_camera_settings()
	elif parameter_name == "max_connection_distance":
		_max_connection_distance = clampf(float(value), 0.05, 3.0)
		_rebuild_generated_orb()
	elif parameter_name == "hub_connection_distance":
		_hub_connection_distance = clampf(float(value), 0.05, 3.5)
		_rebuild_generated_orb()
	elif parameter_name == "heavy_cluster_connection_chance":
		_heavy_cluster_connection_chance = clampf(float(value), 0.0, 1.0)
		_rebuild_generated_orb()
	elif parameter_name == "heavy_cluster_connection_distance_scale":
		_heavy_cluster_connection_distance_scale = clampf(float(value), 0.05, 3.0)
		_rebuild_generated_orb()
	elif parameter_name == "heavy_cluster_connection_width_scale":
		_heavy_cluster_connection_width_scale = clampf(float(value), 0.1, 8.0)
		_rebuild_generated_orb()
	elif parameter_name == "connection_alpha_scale":
		_connection_alpha_scale = clampf(float(value), 0.0, 4.0)
		_rebuild_generated_orb()
	elif parameter_name == "connection_width_scale":
		_connection_width_scale = clampf(float(value), 0.1, 8.0)
		_rebuild_generated_orb()
	elif parameter_name == "route_connection_chance":
		_route_connection_chance = clampf(float(value), 0.0, 1.0)
		_rebuild_generated_orb()
	elif parameter_name == "route_connection_closeness_min":
		_route_connection_closeness_min = clampf(float(value), 0.0, 1.0)
		_rebuild_generated_orb()
	elif parameter_name == "route_connection_alpha_scale":
		_route_connection_alpha_scale = clampf(float(value), 0.0, 8.0)
		_rebuild_generated_orb()
	elif parameter_name == "route_connection_width_scale":
		_route_connection_width_scale = clampf(float(value), 0.1, 10.0)
		_rebuild_generated_orb()
	elif parameter_name == "route_connection_render_boost":
		_route_connection_render_boost = clampf(float(value), 0.0, 4.0)
		_rebuild_generated_orb()
	elif parameter_name == "route_connection_glow_alpha_scale":
		_route_connection_glow_alpha_scale = clampf(float(value), 0.0, 8.0)
		_rebuild_generated_orb()
	elif parameter_name == "route_connection_core_width_scale":
		_route_connection_core_width_scale = clampf(float(value), 0.1, 10.0)
		_rebuild_generated_orb()
	elif parameter_name == "route_connection_glow_width_scale":
		_route_connection_glow_width_scale = clampf(float(value), 0.1, 12.0)
		_rebuild_generated_orb()
	elif parameter_name == "route_connection_hot_mix":
		_route_connection_hot_mix = clampf(float(value), 0.0, 1.0)
		_rebuild_generated_orb()
	elif parameter_name == "cyan_hot" and value is Color:
		_cyan_hot = Color(value)
		_refresh_cyan_palette_if_active()
	elif parameter_name == "cyan_node" and value is Color:
		_cyan_node = Color(value)
		_refresh_cyan_palette_if_active()
	elif parameter_name == "cyan_line" and value is Color:
		_cyan_line = Color(value)
		_refresh_cyan_palette_if_active()
	elif parameter_name == "cyan_dust" and value is Color:
		_cyan_dust = Color(value)
		_refresh_cyan_palette_if_active()
	elif parameter_name == "halo_outer_color" and value is Color:
		_halo_outer_color = Color(value)
	elif parameter_name == "halo_inner_color" and value is Color:
		_halo_inner_color = Color(value)
	else:
		return false
	return true


func reset_orb_lab_parameter(parameter_name: String) -> bool:
	for parameter in get_orb_lab_parameters():
		if String(parameter["name"]) == parameter_name:
			return apply_orb_lab_parameter(parameter_name, parameter["default"])
	return false


func _lab_parameter(parameter_name: String, value, default_value, category: String, requires_rebuild: bool = false) -> Dictionary:
	return {
		"name": parameter_name,
		"value": value,
		"default": default_value,
		"type": "float",
		"category": category,
		"requires_rebuild": requires_rebuild,
	}


func _lab_int_parameter(parameter_name: String, value: int, default_value: int, category: String, requires_rebuild: bool = false) -> Dictionary:
	return {
		"name": parameter_name,
		"value": value,
		"default": default_value,
		"type": "int",
		"category": category,
		"requires_rebuild": requires_rebuild,
	}


func _lab_color_parameter(parameter_name: String, value: Color, default_value: Color, category: String) -> Dictionary:
	return {
		"name": parameter_name,
		"value": value,
		"default": default_value,
		"type": "color",
		"category": category,
		"requires_rebuild": false,
	}


func _lab_bool_parameter(parameter_name: String, value: bool, default_value: bool, category: String) -> Dictionary:
	return {
		"name": parameter_name,
		"value": value,
		"default": default_value,
		"type": "bool",
		"category": category,
		"requires_rebuild": false,
	}


func _cyan_palette() -> Dictionary:
	return {
		"hot": _cyan_hot,
		"node": _cyan_node,
		"line": _cyan_line,
		"dim": Color(CYAN["dim"]),
		"dust": _cyan_dust,
	}


func _refresh_cyan_palette_if_active() -> void:
	if current_state == OrganismState.IDLE or current_state == OrganismState.THINKING or current_state == OrganismState.LISTENING or current_state == OrganismState.SPEAKING or current_state == OrganismState.EXECUTING:
		_target_palette = _cyan_palette()
		_palette = _cyan_palette()


func notify_speech_tick(_character: String = "", delay: float = 0.0, progress: float = 0.0) -> void:
	var tick_started_usec := Time.get_ticks_usec()
	_target_activity = maxf(_target_activity, 0.72)
	_speech_tick_count += 1
	if _speaking_profile_active:
		_speaking_profile_energy_events += 1
		if _speaking_profile_first_energy_usec <= 0:
			_speaking_profile_first_energy_usec = tick_started_usec
		if _speaking_profile_energy_first_values.size() < 3:
			_speaking_profile_energy_first_values.append(progress)
		var energy_elapsed_ms := float(tick_started_usec - _speaking_profile_started_usec) / 1000.0
		if energy_elapsed_ms <= 1000.0:
			_speaking_profile_max_energy_first_second = maxf(_speaking_profile_max_energy_first_second, progress)
	if _speech_energy_override >= 0.0:
		_speech_energy = clampf(_speech_energy_override, 0.0, 0.96)
	else:
		_speech_energy = clampf(_speech_energy + 0.040 + clampf(delay, 0.0, 0.08) * 0.16, 0.0, 0.96)
	if _speech_tick_count % 4 == 0:
		_spawn_speech_ripple()
	if _speech_tick_count % 6 == 0:
		_pick_speech_region(progress)
		_spawn_speech_region_burst(progress, 0.0)
	if _speech_tick_count % 28 == 0:
		_speech_global_morph = clampf(_speech_global_morph + 0.10, 0.0, 0.46)
	if _speech_tick_count % 10 == 0:
		_pick_speech_region(progress)
		_spawn_speech_region_burst(progress + 0.37, 0.0)
	if _speaking_profile_active and _speaking_profile_energy_events == 1:
		var since_start_ms := float(tick_started_usec - _speaking_profile_started_usec) / 1000.0
		print("Speaking startup profile: first speech energy applied. SinceStartMs: %.3f. TickMs: %.3f. Energy: %.3f. Progress: %.3f" % [
			since_start_ms,
			_elapsed_ms_since_usec(tick_started_usec),
			_speech_energy,
			progress
		])


func _set_state(state: int, palette: Dictionary, activity: float, brightness: float) -> void:
	current_state = state
	_target_palette = palette.duplicate(true)
	_target_activity = activity
	_target_brightness = brightness
	_apply_current_state_feature_side_effects()


func _build_scene() -> void:
	_camera = Camera3D.new()
	_camera.name = "Camera3D"
	_apply_camera_settings()
	add_child(_camera)
	_camera.look_at(Vector3.ZERO, Vector3.UP)

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

	_pulse_glow_line_mesh_instance = MeshInstance3D.new()
	_pulse_glow_line_mesh_instance.name = "PulseLineGlowMesh"
	_pulse_glow_line_material = _material(CYAN["hot"], 0.36, true, true)
	_pulse_glow_line_mesh_instance.material_override = _pulse_glow_line_material
	_graph_root.add_child(_pulse_glow_line_mesh_instance)

	_pulse_line_mesh_instance = MeshInstance3D.new()
	_pulse_line_mesh_instance.name = "PulseLineMesh"
	_pulse_line_material = _material(CYAN["hot"], 1.0, true, true)
	_pulse_line_mesh_instance.material_override = _pulse_line_material
	_graph_root.add_child(_pulse_line_mesh_instance)

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

	_cluster_halo_multimesh = MultiMeshInstance3D.new()
	_cluster_halo_multimesh.name = "ClusterHalos"
	_cluster_halo_material = _cluster_halo_shader_material(CYAN["hot"])
	_cluster_halo_multimesh.material_override = _cluster_halo_material
	_graph_root.add_child(_cluster_halo_multimesh)

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


func _cluster_halo_shader_material(_color: Color) -> ShaderMaterial:
	var shader := Shader.new()
	shader.code = """
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, depth_test_disabled;

void fragment() {
	vec2 centered_uv = UV * 2.0 - vec2(1.0);
	float radius = length(centered_uv);
	float halo = pow(smoothstep(1.0, 0.0, radius), 1.45);
	float feather = smoothstep(1.0, 0.02, radius);
	ALBEDO = COLOR.rgb;
	EMISSION = COLOR.rgb * 2.4;
	ALPHA = COLOR.a * halo * feather;
}
"""
	var material := ShaderMaterial.new()
	material.shader = shader
	return material


func _generate_nodes() -> void:
	_nodes.clear()
	_dust.clear()
	_cluster_node_indices.clear()
	_large_thinking_cluster_indices.clear()
	_cluster_halo_node_indices.clear()
	_build_structural_cluster_anchors()

	for index in range(_structural_node_count):
		var node := OrganismNode.new()
		node.hub = false
		node.phase = _rng.randf() * TAU
		node.base_position = _network_position(index)
		node.current_position = node.base_position
		var center_t := _center_visual_t(node.base_position)
		node.radius = _rng.randf_range(0.0048, 0.012) * lerpf(0.92, 1.10, center_t)
		node.brightness = lerpf(0.74, 1.24, pow(center_t, 0.62))
		_nodes.append(node)

	for index in range(_hub_node_count):
		var hub := OrganismNode.new()
		hub.hub = true
		hub.phase = _rng.randf() * TAU
		hub.base_position = _hub_position(index)
		hub.current_position = hub.base_position
		var center_t := _center_visual_t(hub.base_position)
		var hub_importance := _hub_importance(index)
		hub.radius = _rng.randf_range(0.009, 0.017) * lerpf(0.95, 1.18, center_t) * hub_importance
		hub.brightness = lerpf(1.35, 2.15, pow(center_t, 0.52)) * lerpf(1.0, 1.14, hub_importance - 1.0)
		_cluster_node_indices.append(_nodes.size())
		if index == 0 or index < 6 or index % 4 == 0:
			_large_thinking_cluster_indices.append(_nodes.size())
		_nodes.append(hub)

	for hub_index in range(_structural_node_count, _nodes.size()):
		var hub_node := _nodes[hub_index]
		var hub_order := hub_index - _structural_node_count
		var hub_importance := _hub_importance(hub_order)
		for cluster_index in range(_hub_cluster_particle_count):
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

	for cluster_index in range(_bright_cluster_count):
		var anchor_index := _rng.randi_range(0, _structural_node_count - 1)
		var anchor := _nodes[anchor_index]
		var feature_cluster := cluster_index % 5 == 0
		if feature_cluster and not _large_thinking_cluster_indices.has(anchor_index):
			_large_thinking_cluster_indices.append(anchor_index)
		for spark_index in range(_bright_cluster_particle_count):
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

	for core_index in range(_core_cluster_particle_count):
		var spark := OrganismNode.new()
		spark.hub = true
		spark.phase = _rng.randf() * TAU
		var core_radius := pow(_rng.randf(), 1.72) * _visual_core_radius()
		spark.base_position = _irregular_orb_position(core_radius, 0.30)
		spark.current_position = spark.base_position
		var core_t := clampf(core_radius / maxf(_visual_core_radius(), 0.001), 0.0, 1.0)
		spark.radius = _rng.randf_range(0.0030, 0.0088) * lerpf(1.62, 0.78, core_t) * _core_particle_size_scale
		spark.brightness = _rng.randf_range(0.90, 2.14) * lerpf(2.10, 0.78, core_t) * _core_particle_brightness_scale
		_dust.append(spark)

	for index in range(_ambient_dust_node_count):
		var dust := OrganismNode.new()
		dust.phase = _rng.randf() * TAU
		dust.base_position = _dust_position()
		dust.current_position = dust.base_position
		dust.radius = _rng.randf_range(0.0018, 0.0042)
		dust.brightness = _rng.randf_range(0.08, 0.26)
		_dust.append(dust)


func _build_speech_morph_regions() -> void:
	_speech_morph_region_axes.clear()
	_speech_morph_region_offsets.clear()
	for index in range(SPEECH_MORPH_REGION_COUNT):
		var t := (float(index) + 0.5) / float(SPEECH_MORPH_REGION_COUNT)
		var z := 1.0 - 2.0 * t
		var radius := sqrt(maxf(0.0, 1.0 - z * z))
		var angle := float(index) * 2.399963229728653
		var axis := Vector3(cos(angle) * radius, sin(angle) * radius, z).normalized()
		_speech_morph_region_axes.append(axis)
		_speech_morph_region_offsets.append(Vector3.ZERO)
	_precompute_speech_membership(_nodes, _node_speech_primary_regions, _node_speech_secondary_regions, _node_speech_secondary_weights, _node_speech_local_weights, false)
	_precompute_speech_membership(_dust, _dust_speech_primary_regions, _dust_speech_secondary_regions, _dust_speech_secondary_weights, _dust_speech_local_weights, true)


func _precompute_speech_membership(
	items: Array[OrganismNode],
	primary_regions: Array[int],
	secondary_regions: Array[int],
	secondary_weights: Array[float],
	local_weights: Array[float],
	dust_items: bool
) -> void:
	primary_regions.clear()
	secondary_regions.clear()
	secondary_weights.clear()
	local_weights.clear()
	for item in items:
		var radial := item.base_position.normalized() if item.base_position.length_squared() > 0.001 else Vector3.UP
		var best_index := 0
		var second_index := 0
		var best_dot := -2.0
		var second_dot := -2.0
		for region_index in range(_speech_morph_region_axes.size()):
			var dot_value := radial.dot(_speech_morph_region_axes[region_index])
			if dot_value > best_dot:
				second_dot = best_dot
				second_index = best_index
				best_dot = dot_value
				best_index = region_index
			elif dot_value > second_dot:
				second_dot = dot_value
				second_index = region_index
		var blend := clampf((second_dot + 1.0) / maxf(best_dot + second_dot + 2.0, 0.001), 0.12, 0.42)
		var radius_t := clampf(item.base_position.length() / _orb_radius, 0.0, 1.22)
		var local_weight := 0.86 + sin(item.phase * 1.71) * 0.14
		if dust_items:
			local_weight = 0.0 if not item.hub and item.source_node < 0 else 0.42 + sin(item.phase * 1.37) * 0.10
			local_weight *= lerpf(1.05, 0.74, clampf(radius_t, 0.0, 1.0))
		else:
			local_weight *= lerpf(1.12, 0.84, clampf(radius_t, 0.0, 1.0))
		primary_regions.append(best_index)
		secondary_regions.append(second_index)
		secondary_weights.append(blend)
		local_weights.append(maxf(0.0, local_weight))


func _network_position(index: int) -> Vector3:
	var core_end := int(float(_structural_node_count) * _structural_core_fraction)
	var mid_end := core_end + int(float(_structural_node_count) * _structural_mid_fraction)
	mid_end = mini(mid_end, _structural_node_count)
	if index < core_end:
		return _irregular_orb_position(_rng.randf_range(0.03, _visual_core_radius()), 0.42)

	if index < mid_end and not _structural_cluster_anchors.is_empty():
		var local_index := index - core_end
		var anchor_index := local_index % _structural_cluster_anchors.size()
		var anchor := _structural_cluster_anchors[anchor_index]
		var radius_t := clampf(anchor.length() / _orb_radius, 0.0, 1.0)
		var cluster_radius := _rng.randf_range(0.10, 0.34) * lerpf(1.10, 0.78, 1.0 - radius_t)
		var local_position := anchor + _random_unit_vector() * pow(_rng.randf(), 1.45) * cluster_radius
		return _shape_local_position(local_position)

	var shell_roll := _rng.randf()
	if shell_roll < _structural_shell_probability:
		return _irregular_orb_position(_rng.randf_range(_orb_radius * 0.70, _orb_radius * 1.04), 0.20)
	return _irregular_orb_position(pow(_rng.randf(), 1.05) * _orb_radius * _structural_fill_radius_scale, 0.30)


func _build_structural_cluster_anchors() -> void:
	_structural_cluster_anchors.clear()
	for index in range(_structural_feature_cluster_count):
		var radius_min := _orb_radius * (0.40 if index % 3 == 0 else 0.56)
		var radius_max := _orb_radius * (0.76 if index % 3 == 0 else 1.03)
		var anchor := _irregular_orb_position(_rng.randf_range(radius_min, radius_max), 0.18)
		_structural_cluster_anchors.append(anchor)


func _hub_position(index: int) -> Vector3:
	if index == 0:
		return _irregular_orb_position(_rng.randf_range(_orb_radius * 0.04, _orb_radius * 0.16), 0.20)
	if index < 6:
		return _irregular_orb_position(_rng.randf_range(_orb_radius * 0.18, _orb_radius * 0.58), 0.18)
	return _irregular_orb_position(_rng.randf_range(_orb_radius * 0.62, _orb_radius * 1.04), 0.16)


func _hub_importance(index: int) -> float:
	if index == 0:
		return 1.22
	if index < 6:
		return 1.12
	return 1.24 if index % 4 == 0 else 1.14


func _dust_position() -> Vector3:
	var inner := minf(_ambient_dust_inner_radius, _ambient_dust_outer_radius)
	var outer := maxf(_ambient_dust_inner_radius, _ambient_dust_outer_radius)
	return _irregular_orb_position(_rng.randf_range(_orb_radius * inner, _orb_radius * outer), 0.36)


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
	return (normal * surface_radius + tangent_offset).limit_length(_orb_radius * 1.12)


func _irregular_orb_position(radius: float, noise: float) -> Vector3:
	var direction := _random_unit_vector()
	var theta := atan2(direction.y, direction.x)
	var organic := 1.0
	organic += sin(theta * 3.0 + direction.z * 2.1) * noise * 0.26
	organic += cos(theta * 5.0 - direction.y * 1.7) * noise * 0.18
	var position := direction * radius * organic
	position.x *= _shape_x_scale
	position.y *= _shape_y_scale
	position.z *= _shape_z_scale
	return position


func _shape_local_position(position: Vector3) -> Vector3:
	position.x *= _shape_x_scale
	position.y *= _shape_y_scale
	position.z *= _shape_z_scale
	return position.limit_length(_orb_radius * 1.10)


func _random_unit_vector() -> Vector3:
	var theta := _rng.randf() * TAU
	var z := _rng.randf_range(-1.0, 1.0)
	var r := sqrt(maxf(0.0, 1.0 - z * z))
	return Vector3(r * cos(theta), r * sin(theta), z)


func _randf_between(a: float, b: float) -> float:
	return _rng.randf_range(minf(a, b), maxf(a, b))


func _balanced_depth_light(position: Vector3, minimum: float, maximum: float) -> float:
	var radius_t := clampf(position.length() / _orb_radius, 0.0, 1.0)
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
			var max_distance := _hub_connection_distance if node.hub else _max_connection_distance
			if distance <= max_distance:
				ranked.append({ "index": other_index, "distance": distance })

		ranked.sort_custom(func(left, right): return left["distance"] < right["distance"])
		var radius_t := clampf(node.base_position.length() / _orb_radius, 0.0, 1.0)
		var neighbor_min := 8 if radius_t < 0.72 else 9
		var neighbor_max := 10 if radius_t < 0.72 else 11
		var count := mini((11 if node.hub else _rng.randi_range(neighbor_min, neighbor_max)), ranked.size())
		for slot in range(count):
			_add_connection(index, int(ranked[slot]["index"]), float(ranked[slot]["distance"]))

	for index in range(_structural_node_count, _nodes.size()):
		for other_index in range(index + 1, _nodes.size()):
			var distance := _nodes[index].base_position.distance_to(_nodes[other_index].base_position)
			if distance <= _hub_connection_distance * _heavy_cluster_connection_distance_scale and _rng.randf() < _heavy_cluster_connection_chance:
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
	var center_t := _center_visual_t(midpoint)
	var hub_t := 1.0 if _nodes[low].hub or _nodes[high].hub else 0.0
	var closeness := 1.0 - clampf(distance / _hub_connection_distance, 0.0, 1.0)
	var shell_t := clampf(midpoint.length() / _orb_radius, 0.0, 1.0)
	var shell_band := pow(clampf((shell_t - 0.52) / 0.48, 0.0, 1.0), 0.78)
	connection.route = force_route or hub_t > 0.0 and closeness > _route_connection_closeness_min and _rng.randf() < _route_connection_chance
	connection.base_alpha = 0.150 + closeness * 0.24 + pow(center_t, 0.7) * 0.105 + shell_band * 0.110 + hub_t * 0.035
	if connection.route:
		connection.base_alpha += 0.058 + hub_t * 0.020
	connection.base_alpha *= _connection_alpha_scale
	if connection.route:
		connection.base_alpha *= _route_connection_alpha_scale
	connection.width = lerpf(0.0028, 0.0049, pow(center_t, 0.7)) + shell_band * 0.00085 + hub_t * 0.00022
	connection.width *= _connection_width_scale
	if connection.route:
		connection.width *= 1.18 * _heavy_cluster_connection_width_scale * _route_connection_width_scale
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


func _build_node_cluster_flash_targets() -> void:
	_node_cluster_flash_targets.clear()
	var cluster_targets: Array[int] = []
	for index in range(_nodes.size()):
		_node_cluster_flash_targets.append(-1)
	for index in _large_thinking_cluster_indices:
		if index >= 0 and index < _nodes.size() and not cluster_targets.has(index):
			cluster_targets.append(index)
			_node_cluster_flash_targets[index] = index

	for node_index in range(_nodes.size()):
		if _node_cluster_flash_targets[node_index] >= 0:
			continue
		var node_position := _nodes[node_index].base_position
		var best_target := -1
		var best_score := 1000000.0
		for target_index in cluster_targets:
			var target := _nodes[target_index]
			var distance := node_position.distance_to(target.base_position)
			var threshold := 0.62 if target.hub else 0.54
			if distance > threshold:
				continue
			var dust_count := 0
			if target_index < _node_dust_indices.size():
				dust_count = _node_dust_indices[target_index].size()
			var cluster_weight := 1.0 + minf(float(dust_count) / 64.0, 1.0) * 0.28 + (0.18 if target.hub else 0.0)
			var score := distance / cluster_weight
			if score < best_score:
				best_score = score
				best_target = target_index
		_node_cluster_flash_targets[node_index] = best_target


func _build_cluster_halo_indices() -> void:
	_cluster_halo_node_indices.clear()
	_cluster_halo_node_indices.append(-1)
	var scored_clusters: Array[Dictionary] = []
	var candidates: Array[int] = []
	for node_index in _cluster_node_indices:
		if node_index >= 0 and node_index < _nodes.size() and not candidates.has(node_index):
			candidates.append(node_index)
	for node_index in _large_thinking_cluster_indices:
		if node_index >= 0 and node_index < _nodes.size() and not candidates.has(node_index):
			candidates.append(node_index)
	for node_index in candidates:
		var score := _cluster_luminosity_score(node_index)
		if score >= _natural_halo_min_score:
			scored_clusters.append({
				"node_index": node_index,
				"score": score,
			})
	scored_clusters.sort_custom(func(left, right):
		return float(left["score"]) > float(right["score"])
	)
	for cluster_variant in scored_clusters:
		if _cluster_halo_node_indices.size() >= MAX_CLUSTER_HALOS:
			return
		var node_index: int = int(cluster_variant["node_index"])
		if node_index >= 0 and node_index < _nodes.size() and not _cluster_halo_node_indices.has(node_index):
			_cluster_halo_node_indices.append(node_index)


func _cluster_luminosity_score(node_index: int) -> float:
	if node_index < 0 or node_index >= _nodes.size():
		return 0.0
	var node: OrganismNode = _nodes[node_index]
	var dust_count := 0
	if node_index < _node_dust_indices.size():
		dust_count = _node_dust_indices[node_index].size()
	var connection_count := 0
	var route_count := 0
	if node_index < _node_connections.size():
		var connection_indices: Array = _node_connections[node_index]
		connection_count = connection_indices.size()
		for connection_index_variant in connection_indices:
			var connection_index: int = int(connection_index_variant)
			if connection_index >= 0 and connection_index < _connections.size() and _connections[connection_index].route:
				route_count += 1
	var dust_score := clampf(float(dust_count) / 120.0, 0.0, 1.6) * _natural_halo_dust_weight
	var connection_score := clampf(float(connection_count) / 20.0, 0.0, 1.3) * _natural_halo_connection_weight
	var route_score := clampf(float(route_count) / 5.0, 0.0, 1.5) * _natural_halo_route_weight
	var brightness_score := clampf((node.brightness - 0.65) / 1.7, 0.0, 1.2) * _natural_halo_brightness_weight
	var hub_bonus := 0.16 if node.hub else 0.0
	return dust_score * 0.42 + connection_score * 0.24 + route_score * 0.22 + brightness_score * 0.20 + hub_bonus


func _active_halo_outer_color() -> Color:
	if current_state == OrganismState.ERROR or current_state == OrganismState.CONFIRMATION:
		return Color(_palette["line"]).lerp(Color(_palette["dim"]), 0.30)
	return _halo_outer_color


func _active_halo_inner_color() -> Color:
	if current_state == OrganismState.ERROR or current_state == OrganismState.CONFIRMATION:
		return Color(_palette["hot"])
	return _halo_inner_color


func _setup_multimeshes() -> void:
	_node_multimesh.multimesh = _new_multimesh(_node_mesh(1.0), _non_hub_node_count())
	_hub_multimesh.multimesh = _new_multimesh(_node_mesh(1.0), _hub_render_node_count())
	_dust_multimesh.multimesh = _new_multimesh(_node_mesh(1.0), _dust.size())
	_cluster_halo_multimesh.multimesh = _new_multimesh(_halo_quad_mesh(), MAX_CLUSTER_HALOS)
	_pulse_multimesh.multimesh = _new_multimesh(_node_mesh(1.0), MAX_PULSES)
	_core_multimesh.multimesh = _new_multimesh(_node_mesh(1.0), 1)
	_rebuild_static_connection_meshes()
	_update_nodes(0.0)
	_update_dust(0.0, 0, 1)
	_update_cluster_halos()
	_update_core_glow()
	_update_materials()


func _non_hub_node_count() -> int:
	var count := 0
	for node in _nodes:
		if not node.hub:
			count += 1
	return count


func _hub_render_node_count() -> int:
	var count := 0
	for node in _nodes:
		if node.hub:
			count += 1
	return count


func _node_mesh(radius: float) -> SphereMesh:
	var mesh := SphereMesh.new()
	mesh.radius = radius
	mesh.height = radius * 2.0
	mesh.radial_segments = 8
	mesh.rings = 4
	return mesh


func _halo_quad_mesh() -> QuadMesh:
	var mesh := QuadMesh.new()
	mesh.size = Vector2.ONE
	return mesh


func _new_multimesh(mesh: Mesh, count: int) -> MultiMesh:
	var multimesh := MultiMesh.new()
	multimesh.transform_format = MultiMesh.TRANSFORM_3D
	multimesh.use_colors = true
	multimesh.mesh = mesh
	multimesh.instance_count = count
	return multimesh


func _process(delta: float) -> void:
	var profile_started_usec := Time.get_ticks_usec()
	_reset_speaking_profile_frame_counters()
	_orb_frame_index += 1
	_time += delta
	_activity = lerpf(_activity, _target_activity, minf(delta * 2.6, 1.0))
	_brightness = lerpf(_brightness, _target_brightness, minf(delta * 2.6, 1.0))
	var palette_started_usec := Time.get_ticks_usec()
	_update_palette(minf(delta * 3.2, 1.0))
	var palette_ms := _elapsed_ms_since_usec(palette_started_usec)
	_orb_profile_palette_ms += palette_ms
	_target_activity = lerpf(_target_activity, _state_activity_floor(), minf(delta * 0.9, 1.0))
	_target_brightness = lerpf(_target_brightness, _state_brightness_floor(), minf(delta * 0.9, 1.0))
	var speech_motion_started_usec := Time.get_ticks_usec()
	if _state_feature_enabled("speech_motion"):
		_update_autonomous_speech_motion(delta)
		_update_speech_ripples(delta)
		_update_speech_region_bursts(delta)
		_speech_global_morph = move_toward(_speech_global_morph, 0.0, delta * 2.4)
		_update_speech_region_axes(delta)
		_update_cached_speech_morph_regions()
	else:
		_speech_global_morph = 0.0
		for index in range(_speech_morph_region_offsets.size()):
			_speech_morph_region_offsets[index] = Vector3.ZERO
	_update_cluster_selection_flashes(delta)
	var speech_motion_ms := _elapsed_ms_since_usec(speech_motion_started_usec)

	var breath := 1.0 + sin(_time * 0.82) * (0.020 + _activity * 0.012) if _state_feature_enabled("breathing") else 1.0
	_graph_root.scale = Vector3.ONE * _display_scale * breath
	var rotation_started_usec := Time.get_ticks_usec()
	if _state_feature_enabled("rotation"):
		_update_organic_rotation(delta)
	var rotation_ms := _elapsed_ms_since_usec(rotation_started_usec)

	var section_started_usec := Time.get_ticks_usec()
	if _state_feature_enabled("pulses"):
		_update_pulses(delta)
	else:
		_clear_pulses_and_lights()
	var pulses_ms := _elapsed_ms_since_usec(section_started_usec)
	_orb_profile_pulses_ms += pulses_ms
	section_started_usec = Time.get_ticks_usec()
	_update_nodes(delta)
	var nodes_ms := _elapsed_ms_since_usec(section_started_usec)
	_orb_profile_nodes_ms += nodes_ms
	if _state_feature_enabled("cluster_halos"):
		_update_cluster_halos()
	else:
		_clear_cluster_halos()
	section_started_usec = Time.get_ticks_usec()
	var dust_stride := _dust_update_stride()
	_update_dust(delta, _orb_frame_index % dust_stride, dust_stride)
	var dust_ms := _elapsed_ms_since_usec(section_started_usec)
	_orb_profile_dust_ms += dust_ms
	section_started_usec = Time.get_ticks_usec()
	if _state_feature_enabled("pulse_connections") and (_orb_frame_index % _pulse_connection_update_stride() == 0 or (_pulse_connection_mesh_active and _pulses.is_empty() and _cluster_activations.is_empty())):
		_update_pulse_connections()
	elif not _state_feature_enabled("pulse_connections") and _pulse_connection_mesh_active:
		_clear_pulse_connection_meshes()
	if not _connection_selection_flashes.is_empty():
		_rebuild_static_connection_meshes()
	var connections_ms := _elapsed_ms_since_usec(section_started_usec)
	_orb_profile_connections_ms += connections_ms
	section_started_usec = Time.get_ticks_usec()
	if _state_feature_enabled("core_glow"):
		_update_core_glow()
	else:
		_clear_core_glow()
	_update_materials()
	var materials_ms := _elapsed_ms_since_usec(section_started_usec)
	_orb_profile_materials_ms += materials_ms
	_record_speaking_startup_frame(
		profile_started_usec,
		delta,
		palette_ms,
		speech_motion_ms,
		rotation_ms,
		pulses_ms,
		nodes_ms,
		dust_ms,
		connections_ms,
		materials_ms
	)
	_record_orb_frame(profile_started_usec)


func _update_organic_rotation(delta: float) -> void:
	if _manual_rotation_enabled:
		_rotation = _manual_rotation
		_graph_root.rotation = _manual_rotation
		return
	_rotation_target_timer -= delta
	if _rotation_target_timer <= 0.0:
		_rotation_target_timer = _rng.randf_range(1.6, 3.8)
		var state_energy := 1.0 + _activity * 0.55
		_rotation_target_velocity = Vector3(
			_rng.randf_range(-0.014, 0.014),
			_rng.randf_range(-0.052, 0.058),
			_rng.randf_range(-0.010, 0.010)
		) * state_energy

	_rotation_velocity = _rotation_velocity.lerp(_rotation_target_velocity, minf(delta * 0.85, 1.0))
	_rotation += _rotation_velocity * delta
	_rotation.x = clampf(_rotation.x, -PRESENTATION_ROTATION_LIMIT.x, PRESENTATION_ROTATION_LIMIT.x)
	_rotation.z = clampf(_rotation.z, -PRESENTATION_ROTATION_LIMIT.z, PRESENTATION_ROTATION_LIMIT.z)
	if absf(_rotation.x) >= PRESENTATION_ROTATION_LIMIT.x:
		_rotation_velocity.x *= -0.35
	if absf(_rotation.z) >= PRESENTATION_ROTATION_LIMIT.z:
		_rotation_velocity.z *= -0.35
	_graph_root.rotation = _rotation + Vector3(
		sin(_time * 0.31) * 0.018,
		sin(_time * 0.17) * 0.018,
		sin(_time * 0.23) * 0.014
	)


func _elapsed_ms_since_usec(started_usec: int) -> float:
	return float(Time.get_ticks_usec() - started_usec) / 1000.0


func _reset_speaking_profile_frame_counters() -> void:
	if not _speaking_profile_active:
		return
	_speaking_profile_frame_node_transform_sets = 0
	_speaking_profile_frame_node_color_sets = 0
	_speaking_profile_frame_dust_transform_sets = 0
	_speaking_profile_frame_dust_color_sets = 0
	_speaking_profile_frame_pulse_transform_sets = 0
	_speaking_profile_frame_pulse_color_sets = 0
	_speaking_profile_frame_pulse_mesh_builds = 0
	_speaking_profile_frame_array_mesh_resources = 0
	_speaking_profile_frame_packed_arrays = 0
	_speaking_profile_frame_speech_map_rebuilds = 0
	_speaking_profile_frame_region_picks = 0
	_speaking_profile_frame_region_array_resets = 0
	_speaking_profile_frame_ripple_spawns = 0
	_speaking_profile_frame_node_morph_ms = 0.0
	_speaking_profile_frame_node_position_ms = 0.0
	_speaking_profile_frame_node_speech_lookup_ms = 0.0
	_speaking_profile_frame_node_color_calc_ms = 0.0
	_speaking_profile_frame_node_transform_upload_ms = 0.0
	_speaking_profile_frame_node_color_upload_ms = 0.0
	_speaking_profile_frame_dust_morph_ms = 0.0
	_speaking_profile_frame_dust_position_ms = 0.0
	_speaking_profile_frame_dust_speech_lookup_ms = 0.0
	_speaking_profile_frame_dust_color_calc_ms = 0.0
	_speaking_profile_frame_dust_transform_upload_ms = 0.0
	_speaking_profile_frame_dust_color_upload_ms = 0.0
	_speaking_profile_frame_node_displacement_total = 0.0
	_speaking_profile_frame_node_displacement_max = 0.0
	_speaking_profile_frame_node_displacement_count = 0
	_speaking_profile_frame_dust_displacement_total = 0.0
	_speaking_profile_frame_dust_displacement_max = 0.0
	_speaking_profile_frame_dust_displacement_count = 0
	_speaking_profile_frame_node_color_delta_total = 0.0
	_speaking_profile_frame_node_color_delta_max = 0.0
	_speaking_profile_frame_node_color_delta_count = 0
	_speaking_profile_frame_dust_color_delta_total = 0.0
	_speaking_profile_frame_dust_color_delta_max = 0.0
	_speaking_profile_frame_dust_color_delta_count = 0


func _snapshot_speaking_profile_colors() -> void:
	_speaking_profile_previous_node_colors.clear()
	_speaking_profile_previous_dust_colors.clear()
	if _node_multimesh != null and _node_multimesh.multimesh != null:
		for index in range(_node_multimesh.multimesh.instance_count):
			_speaking_profile_previous_node_colors.append(_node_multimesh.multimesh.get_instance_color(index))
	if _hub_multimesh != null and _hub_multimesh.multimesh != null:
		for index in range(_hub_multimesh.multimesh.instance_count):
			_speaking_profile_previous_node_colors.append(_hub_multimesh.multimesh.get_instance_color(index))
	if _dust_multimesh != null and _dust_multimesh.multimesh != null:
		for index in range(_dust_multimesh.multimesh.instance_count):
			_speaking_profile_previous_dust_colors.append(_dust_multimesh.multimesh.get_instance_color(index))


func _profile_color_delta(a: Color, b: Color) -> float:
	return absf(a.r - b.r) + absf(a.g - b.g) + absf(a.b - b.b) + absf(a.a - b.a)


func _speaking_profile_segment_index(elapsed_ms: float) -> int:
	if elapsed_ms < 1000.0:
		return 0
	if elapsed_ms < 3000.0:
		return 1
	return 2


func _record_speaking_startup_frame(
	started_usec: int,
	delta: float,
	palette_ms: float,
	speech_motion_ms: float,
	rotation_ms: float,
	pulses_ms: float,
	nodes_ms: float,
	dust_ms: float,
	connections_ms: float,
	materials_ms: float
) -> void:
	if not _speaking_profile_active:
		return

	var now_usec := Time.get_ticks_usec()
	var frame_ms := float(now_usec - started_usec) / 1000.0
	_speaking_profile_frame_index += 1
	if frame_ms > _speaking_profile_peak_frame_ms:
		_speaking_profile_peak_frame_ms = frame_ms
		_speaking_profile_peak_frame_index = _speaking_profile_frame_index

	var elapsed_ms := float(now_usec - _speaking_profile_started_usec) / 1000.0
	var segment_index := _speaking_profile_segment_index(elapsed_ms)
	_speaking_profile_segment_frame_counts[segment_index] += 1
	_speaking_profile_segment_frame_ms[segment_index] += frame_ms
	_speaking_profile_segment_nodes_ms[segment_index] += nodes_ms
	_speaking_profile_segment_dust_ms[segment_index] += dust_ms
	_speaking_profile_segment_node_morph_ms[segment_index] += _speaking_profile_frame_node_morph_ms
	_speaking_profile_segment_dust_morph_ms[segment_index] += _speaking_profile_frame_dust_morph_ms
	_speaking_profile_segment_node_transform_upload_ms[segment_index] += _speaking_profile_frame_node_transform_upload_ms
	_speaking_profile_segment_dust_transform_upload_ms[segment_index] += _speaking_profile_frame_dust_transform_upload_ms
	_speaking_profile_segment_node_color_upload_ms[segment_index] += _speaking_profile_frame_node_color_upload_ms
	_speaking_profile_segment_dust_color_upload_ms[segment_index] += _speaking_profile_frame_dust_color_upload_ms
	_speaking_profile_segment_energy_total[segment_index] += _speech_energy
	_speaking_profile_segment_energy_max[segment_index] = maxf(float(_speaking_profile_segment_energy_max[segment_index]), _speech_energy)
	while _speaking_profile_next_bucket_index < SPEAKING_STARTUP_PROFILE_BUCKETS_MS.size() and elapsed_ms >= float(SPEAKING_STARTUP_PROFILE_BUCKETS_MS[_speaking_profile_next_bucket_index]):
		var bucket_ms: int = SPEAKING_STARTUP_PROFILE_BUCKETS_MS[_speaking_profile_next_bucket_index]
		var first_energy_ms := -1.0
		if _speaking_profile_first_energy_usec > 0:
			first_energy_ms = float(_speaking_profile_first_energy_usec - _speaking_profile_started_usec) / 1000.0
		var node_displacement_avg := _speaking_profile_frame_node_displacement_total / maxf(float(_speaking_profile_frame_node_displacement_count), 1.0)
		var dust_displacement_avg := _speaking_profile_frame_dust_displacement_total / maxf(float(_speaking_profile_frame_dust_displacement_count), 1.0)
		var node_color_delta_avg := _speaking_profile_frame_node_color_delta_total / maxf(float(_speaking_profile_frame_node_color_delta_count), 1.0)
		var dust_color_delta_avg := _speaking_profile_frame_dust_color_delta_total / maxf(float(_speaking_profile_frame_dust_color_delta_count), 1.0)
		print("Speaking startup timeline: bucketMs=%s elapsedMs=%.3f frame=%s frameMs=%.3f deltaMs=%.3f paletteMs=%.3f speechMotionMs=%.3f rotationMs=%.3f pulsesMs=%.3f nodesMs=%.3f dustMs=%.3f connectionMs=%.3f materialMs=%.3f nodeTransformSets=%s nodeColorSets=%s dustTransformSets=%s dustColorSets=%s pulseTransformSets=%s pulseColorSets=%s pulseMeshBuildsFrame=%s arrayMeshResourcesFrame=%s packedArraysFrame=%s speechMapRebuildsFrame=%s regionPicksFrame=%s regionArrayResetsFrame=%s rippleSpawnsFrame=%s energyEvents=%s firstEnergyMs=%.3f totalRegionPicks=%s totalRipples=%s totalSpeechMapRebuilds=%s totalPulseMeshBuilds=%s totalArrayMeshResources=%s totalPackedArrays=%s speechMapClears=%s regionArrayResets=%s pulses=%s ripples=%s pulseMeshActive=%s state=%s gcIndicator=not_exposed" % [
			bucket_ms,
			elapsed_ms,
			_speaking_profile_frame_index,
			frame_ms,
			delta * 1000.0,
			palette_ms,
			speech_motion_ms,
			rotation_ms,
			pulses_ms,
			nodes_ms,
			dust_ms,
			connections_ms,
			materials_ms,
			_speaking_profile_frame_node_transform_sets,
			_speaking_profile_frame_node_color_sets,
			_speaking_profile_frame_dust_transform_sets,
			_speaking_profile_frame_dust_color_sets,
			_speaking_profile_frame_pulse_transform_sets,
			_speaking_profile_frame_pulse_color_sets,
			_speaking_profile_frame_pulse_mesh_builds,
			_speaking_profile_frame_array_mesh_resources,
			_speaking_profile_frame_packed_arrays,
			_speaking_profile_frame_speech_map_rebuilds,
			_speaking_profile_frame_region_picks,
			_speaking_profile_frame_region_array_resets,
			_speaking_profile_frame_ripple_spawns,
			_speaking_profile_energy_events,
			first_energy_ms,
			_speaking_profile_region_pick_count,
			_speaking_profile_ripple_spawn_count,
			_speaking_profile_speech_map_rebuild_count,
			_speaking_profile_pulse_mesh_build_count,
			_speaking_profile_array_mesh_resource_count,
			_speaking_profile_packed_array_count,
			_speaking_profile_speech_map_clear_count,
			_speaking_profile_region_array_reset_count,
			_pulses.size(),
			_speech_ripples.size(),
			_pulse_connection_mesh_active,
			_organism_state_name(current_state)
		])
		print("Speaking startup deep: bucketMs=%s nodesMorphMs=%.3f nodesPositionMs=%.3f nodesSpeechLookupMs=%.3f nodesColorCalcMs=%.3f nodesTransformUploadMs=%.3f nodesColorUploadMs=%.3f dustMorphMs=%.3f dustPositionMs=%.3f dustSpeechLookupMs=%.3f dustColorCalcMs=%.3f dustTransformUploadMs=%.3f dustColorUploadMs=%.3f incomingEnergyFirst3=%s incomingEnergyMaxFirstSecond=%.3f appliedSpeechEnergy=%.3f nodeDisplacementAvg=%.5f nodeDisplacementMax=%.5f dustDisplacementAvg=%.5f dustDisplacementMax=%.5f nodeColorDeltaAvg=%.5f nodeColorDeltaMax=%.5f dustColorDeltaAvg=%.5f dustColorDeltaMax=%.5f colorDeltaSamplesNodes=%s colorDeltaSamplesDust=%s" % [
			bucket_ms,
			_speaking_profile_frame_node_morph_ms,
			_speaking_profile_frame_node_position_ms,
			_speaking_profile_frame_node_speech_lookup_ms,
			_speaking_profile_frame_node_color_calc_ms,
			_speaking_profile_frame_node_transform_upload_ms,
			_speaking_profile_frame_node_color_upload_ms,
			_speaking_profile_frame_dust_morph_ms,
			_speaking_profile_frame_dust_position_ms,
			_speaking_profile_frame_dust_speech_lookup_ms,
			_speaking_profile_frame_dust_color_calc_ms,
			_speaking_profile_frame_dust_transform_upload_ms,
			_speaking_profile_frame_dust_color_upload_ms,
			str(_speaking_profile_energy_first_values),
			_speaking_profile_max_energy_first_second,
			_speech_energy,
			node_displacement_avg,
			_speaking_profile_frame_node_displacement_max,
			dust_displacement_avg,
			_speaking_profile_frame_dust_displacement_max,
			node_color_delta_avg,
			_speaking_profile_frame_node_color_delta_max,
			dust_color_delta_avg,
			_speaking_profile_frame_dust_color_delta_max,
			_speaking_profile_frame_node_color_delta_count,
			_speaking_profile_frame_dust_color_delta_count
		])
		_speaking_profile_next_bucket_index += 1

	if elapsed_ms >= 7000.0:
		var summary_first_energy_ms := -1.0
		if _speaking_profile_first_energy_usec > 0:
			summary_first_energy_ms = float(_speaking_profile_first_energy_usec - _speaking_profile_started_usec) / 1000.0
		print("Speaking startup profile: complete. DurationMs=%.3f frames=%s peakFrameMs=%.3f peakFrame=%s energyEvents=%s firstEnergyMs=%.3f totalRegionPicks=%s totalRipples=%s totalSpeechMapRebuilds=%s totalPulseMeshBuilds=%s totalArrayMeshResources=%s totalPackedArrays=%s speechMapClears=%s regionArrayResets=%s finalPulses=%s finalRipples=%s" % [
			elapsed_ms,
			_speaking_profile_frame_index,
			_speaking_profile_peak_frame_ms,
			_speaking_profile_peak_frame_index,
			_speaking_profile_energy_events,
			summary_first_energy_ms,
			_speaking_profile_region_pick_count,
			_speaking_profile_ripple_spawn_count,
			_speaking_profile_speech_map_rebuild_count,
			_speaking_profile_pulse_mesh_build_count,
			_speaking_profile_array_mesh_resource_count,
			_speaking_profile_packed_array_count,
			_speaking_profile_speech_map_clear_count,
			_speaking_profile_region_array_reset_count,
			_pulses.size(),
			_speech_ripples.size()
		])
		var segment_labels := ["0-1s", "1-3s", "3-7s"]
		for index in range(3):
			var frame_count := maxf(float(_speaking_profile_segment_frame_counts[index]), 1.0)
			print("Speaking startup segment: range=%s frames=%s avgFrameMs=%.3f avgNodesMs=%.3f avgDustMs=%.3f avgNodeMorphMs=%.3f avgDustMorphMs=%.3f avgNodeTransformUploadMs=%.3f avgDustTransformUploadMs=%.3f avgNodeColorUploadMs=%.3f avgDustColorUploadMs=%.3f avgAppliedEnergy=%.3f maxAppliedEnergy=%.3f" % [
				segment_labels[index],
				_speaking_profile_segment_frame_counts[index],
				float(_speaking_profile_segment_frame_ms[index]) / frame_count,
				float(_speaking_profile_segment_nodes_ms[index]) / frame_count,
				float(_speaking_profile_segment_dust_ms[index]) / frame_count,
				float(_speaking_profile_segment_node_morph_ms[index]) / frame_count,
				float(_speaking_profile_segment_dust_morph_ms[index]) / frame_count,
				float(_speaking_profile_segment_node_transform_upload_ms[index]) / frame_count,
				float(_speaking_profile_segment_dust_transform_upload_ms[index]) / frame_count,
				float(_speaking_profile_segment_node_color_upload_ms[index]) / frame_count,
				float(_speaking_profile_segment_dust_color_upload_ms[index]) / frame_count,
				float(_speaking_profile_segment_energy_total[index]) / frame_count,
				float(_speaking_profile_segment_energy_max[index])
			])
		_speaking_profile_active = false


func _dust_update_stride() -> int:
	match ORB_QUALITY_MODE:
		ORB_QUALITY_HIGH:
			return 2
		ORB_QUALITY_EMERGENCY:
			return 5
		_:
			return 3


func _pulse_connection_update_stride() -> int:
	match ORB_QUALITY_MODE:
		ORB_QUALITY_HIGH:
			return 1
		ORB_QUALITY_EMERGENCY:
			return 4
		_:
			return 2


func _node_color_update_stride() -> int:
	match ORB_QUALITY_MODE:
		ORB_QUALITY_HIGH:
			return 1
		ORB_QUALITY_EMERGENCY:
			return 4
		_:
			return 2


func _dust_color_update_stride() -> int:
	match ORB_QUALITY_MODE:
		ORB_QUALITY_HIGH:
			return 1
		ORB_QUALITY_EMERGENCY:
			return 4
		_:
			return 2


func _record_orb_frame(started_usec: int) -> void:
	if not ORB_FRAME_PROFILER_ENABLED:
		return

	var now_usec := Time.get_ticks_usec()
	if _orb_profile_last_report_usec <= 0:
		_orb_profile_last_report_usec = now_usec

	var elapsed_ms := _elapsed_ms_since_usec(started_usec)
	_orb_profile_frames += 1
	_orb_profile_total_ms += elapsed_ms
	_orb_profile_max_ms = maxf(_orb_profile_max_ms, elapsed_ms)
	if elapsed_ms >= ORB_FRAME_PROFILER_SPIKE_MS:
		_orb_profile_spikes += 1

	var report_due := float(now_usec - _orb_profile_last_report_usec) / 1000000.0 >= ORB_FRAME_PROFILER_REPORT_SECONDS
	if report_due and (_orb_profile_spikes > 0 or _orb_profile_max_ms >= ORB_FRAME_PROFILER_SPIKE_MS):
		var average_ms := _orb_profile_total_ms / maxf(float(_orb_profile_frames), 1.0)
		print("Orb frame profile: state=%s quality=%s frames=%s avgMs=%.2f maxMs=%.2f spikes=%s nodesMs=%.2f dustMs=%.2f pulseLinesMs=%.2f pulsesMs=%.2f materialsMs=%.2f paletteMs=%.2f speechMapsMs=%.2f pulseMeshMs=%.2f nodeTransformSets=%s nodeColorSets=%s dustTransformSets=%s dustColorSets=%s pulseMeshBuilds=%s nodes=%s dust=%s dustStride=%s pulseStride=%s connections=%s" % [
			_organism_state_name(current_state),
			_orb_quality_name(),
			_orb_profile_frames,
			average_ms,
			_orb_profile_max_ms,
			_orb_profile_spikes,
			_orb_profile_nodes_ms,
			_orb_profile_dust_ms,
			_orb_profile_connections_ms,
			_orb_profile_pulses_ms,
			_orb_profile_materials_ms,
			_orb_profile_palette_ms,
			_orb_profile_speech_maps_ms,
			_orb_profile_pulse_mesh_ms,
			_orb_profile_node_transform_sets,
			_orb_profile_node_color_sets,
			_orb_profile_dust_transform_sets,
			_orb_profile_dust_color_sets,
			_orb_profile_pulse_mesh_builds,
			_nodes.size(),
			_dust.size(),
			_dust_update_stride(),
			_pulse_connection_update_stride(),
			_connections.size()
		])
		_orb_profile_last_report_usec = now_usec
		_orb_profile_frames = 0
		_orb_profile_total_ms = 0.0
		_orb_profile_max_ms = 0.0
		_orb_profile_nodes_ms = 0.0
		_orb_profile_dust_ms = 0.0
		_orb_profile_connections_ms = 0.0
		_orb_profile_pulses_ms = 0.0
		_orb_profile_materials_ms = 0.0
		_orb_profile_palette_ms = 0.0
		_orb_profile_speech_maps_ms = 0.0
		_orb_profile_pulse_mesh_ms = 0.0
		_orb_profile_node_transform_sets = 0
		_orb_profile_node_color_sets = 0
		_orb_profile_dust_transform_sets = 0
		_orb_profile_dust_color_sets = 0
		_orb_profile_pulse_mesh_builds = 0
		_orb_profile_spikes = 0


func _orb_quality_name() -> String:
	match ORB_QUALITY_MODE:
		ORB_QUALITY_HIGH:
			return "HIGH"
		ORB_QUALITY_EMERGENCY:
			return "EMERGENCY"
		_:
			return "SAFE"


func _state_feature_enabled(feature_name: String) -> bool:
	return _state_feature_enabled_for_name(_organism_state_name(current_state), feature_name)


func _state_feature_enabled_for_name(state_name: String, feature_name: String) -> bool:
	if not STATE_FEATURES.has(feature_name):
		return true
	if not _state_feature_overrides.has(state_name):
		return true
	var state_map: Dictionary = _state_feature_overrides[state_name]
	return bool(state_map.get(feature_name, true))


func _orb_lab_state_names() -> Array[String]:
	return ["IDLE", "LISTENING", "THINKING", "SPEAKING", "EXECUTING", "ERROR", "CONFIRMATION"]


func _apply_current_state_feature_side_effects() -> void:
	if not _state_feature_enabled("pulses"):
		_clear_pulses_and_lights()
	if not _state_feature_enabled("pulse_connections"):
		_clear_pulse_connection_meshes()
	if not _state_feature_enabled("cluster_halos"):
		_clear_cluster_halos()
	if not _state_feature_enabled("core_glow"):
		_clear_core_glow()


func _clear_pulses_and_lights() -> void:
	_pulses.clear()
	_pending_energy_branches.clear()
	_cluster_activations.clear()
	_cluster_activation_cooldowns.clear()
	_node_speech_light.clear()
	_dust_speech_light.clear()
	_connection_speech_light.clear()
	if _pulse_multimesh != null and _pulse_multimesh.multimesh != null:
		for index in range(MAX_PULSES):
			_pulse_multimesh.multimesh.set_instance_transform(index, Transform3D(Basis().scaled(Vector3.ZERO), Vector3.ZERO))
			_pulse_multimesh.multimesh.set_instance_color(index, Color(0, 0, 0, 0))
	_clear_pulse_connection_meshes()


func _clear_pulse_connection_meshes() -> void:
	if _pulse_line_mesh_instance != null:
		_pulse_line_mesh_instance.mesh = ArrayMesh.new()
	if _pulse_glow_line_mesh_instance != null:
		_pulse_glow_line_mesh_instance.mesh = ArrayMesh.new()
	_pulse_connection_mesh_active = false


func _clear_cluster_halos() -> void:
	if _cluster_halo_multimesh == null or _cluster_halo_multimesh.multimesh == null:
		return
	for index in range(MAX_CLUSTER_HALOS):
		_cluster_halo_multimesh.multimesh.set_instance_transform(index, Transform3D(Basis().scaled(Vector3.ZERO), Vector3.ZERO))
		_cluster_halo_multimesh.multimesh.set_instance_color(index, Color(0, 0, 0, 0))


func _clear_core_glow() -> void:
	if _core_multimesh == null or _core_multimesh.multimesh == null:
		return
	_core_multimesh.multimesh.set_instance_transform(0, Transform3D(Basis().scaled(Vector3.ZERO), Vector3.ZERO))
	_core_multimesh.multimesh.set_instance_color(0, Color(0, 0, 0, 0))


func _update_cluster_selection_flashes(delta: float) -> void:
	for index in range(_cluster_selection_flashes.size() - 1, -1, -1):
		var flash: ClusterSelectionFlash = _cluster_selection_flashes[index]
		flash.age += delta
		if flash.age >= flash.duration:
			_cluster_selection_flashes.remove_at(index)
	for index in range(_connection_selection_flashes.size() - 1, -1, -1):
		var connection_flash: ConnectionSelectionFlash = _connection_selection_flashes[index]
		connection_flash.age += delta
		if connection_flash.age >= connection_flash.duration:
			_connection_selection_flashes.remove_at(index)


func _cluster_selection_flash_amount(node_index: int) -> float:
	var amount := 0.0
	for flash_index in range(_cluster_selection_flashes.size()):
		var flash: ClusterSelectionFlash = _cluster_selection_flashes[flash_index]
		if flash.node_index != node_index:
			continue
		var life := clampf(flash.age / maxf(flash.duration, 0.001), 0.0, 1.0)
		amount = maxf(amount, sin(life * PI))
	return amount


func _connection_selection_flash_amount(connection_index: int) -> float:
	var amount := 0.0
	for flash_index in range(_connection_selection_flashes.size()):
		var flash: ConnectionSelectionFlash = _connection_selection_flashes[flash_index]
		if flash.connection_index != connection_index:
			continue
		var life := clampf(flash.age / maxf(flash.duration, 0.001), 0.0, 1.0)
		amount = maxf(amount, sin(life * PI))
	return amount


func _cluster_lab_override(node_index: int, parameter_name: String, default_value: float = 1.0) -> float:
	if not _cluster_lab_overrides.has(node_index):
		return default_value
	var override: Dictionary = _cluster_lab_overrides[node_index] as Dictionary
	return float(override.get(parameter_name, default_value))


func _connection_lab_override(connection_index: int, parameter_name: String, default_value: float = 1.0) -> float:
	if not _connection_lab_overrides.has(connection_index):
		return default_value
	var override: Dictionary = _connection_lab_overrides[connection_index] as Dictionary
	return float(override.get(parameter_name, default_value))


func _connection_lab_route(connection_index: int, default_value: bool) -> bool:
	if not _connection_lab_overrides.has(connection_index):
		return default_value
	var override: Dictionary = _connection_lab_overrides[connection_index] as Dictionary
	return bool(override.get("route", default_value))


func _project_orb_position(local_position: Vector3) -> Vector2:
	var world_position := _graph_root.global_transform * local_position
	return _camera.unproject_position(world_position)


func _pick_orb_lab_cluster(screen_position: Vector2, _viewport_size: Vector2) -> Dictionary:
	var best_distance := 99999.0
	var best_node := -1
	var best_label := ""
	var display_index := 1
	for node_index in _cluster_halo_node_indices:
		if node_index < 0 or node_index >= _nodes.size():
			continue
		var projected := _project_orb_position(_nodes[node_index].current_position)
		var distance := projected.distance_to(screen_position)
		if distance < best_distance:
			best_distance = distance
			best_node = node_index
			best_label = "Cluster %02d" % display_index
		display_index += 1
	if best_node >= 0 and best_distance <= 38.0:
		return {
			"type": "cluster",
			"id": best_node,
			"label": best_label,
			"distance": best_distance,
		}
	return {}


func _pick_orb_lab_connection(screen_position: Vector2, _viewport_size: Vector2) -> Dictionary:
	var best_distance := 99999.0
	var best_connection := -1
	for connection_index in range(_connections.size()):
		var connection: OrganismConnection = _connections[connection_index]
		var a := _project_orb_position(_nodes[connection.a].current_position)
		var b := _project_orb_position(_nodes[connection.b].current_position)
		var distance := _distance_to_screen_segment(screen_position, a, b)
		var route_bonus := 0.72 if _connection_lab_route(connection_index, connection.route) else 1.0
		distance *= route_bonus
		if distance < best_distance:
			best_distance = distance
			best_connection = connection_index
	if best_connection >= 0 and best_distance <= 14.0:
		return {
			"type": "connection",
			"id": best_connection,
			"label": "Connection %04d" % best_connection,
			"distance": best_distance,
		}
	return {}


func _apply_builder_cluster_brush(screen_position: Vector2, viewport_size: Vector2) -> Dictionary:
	var cluster_pick := _pick_orb_lab_cluster(screen_position, viewport_size)
	if not cluster_pick.is_empty() and float(cluster_pick.get("distance", 99999.0)) <= 42.0:
		var cluster_id: int = int(cluster_pick.get("id", -1))
		if cluster_id >= 0:
			_grow_builder_cluster(cluster_id, 18, 0.16)
			select_orb_lab_cluster(cluster_id)
			return { "message": "grew cluster %s" % cluster_id }
	var position := _screen_to_orb_builder_position(screen_position)
	var node_index := _create_builder_cluster(position)
	select_orb_lab_cluster(node_index)
	return { "message": "added cluster %s" % node_index }


func _apply_builder_particle_brush(screen_position: Vector2) -> Dictionary:
	var position := _screen_to_orb_builder_position(screen_position)
	var inward := (-position.normalized()) if position.length_squared() > 0.001 else Vector3.FORWARD
	for index in range(36):
		var dust := OrganismNode.new()
		dust.phase = _rng.randf() * TAU
		var depth := pow(_rng.randf(), 0.78) * _orb_radius * 0.72
		var lateral := _random_unit_vector() * _rng.randf_range(0.018, 0.20)
		dust.base_position = (position + inward * depth + lateral).limit_length(_orb_radius * 1.16)
		dust.current_position = dust.base_position
		dust.radius = _rng.randf_range(0.0022, 0.0048)
		dust.brightness = _rng.randf_range(0.22, 0.58)
		_dust.append(dust)
	_rebuild_after_builder_mutation(false)
	return { "message": "added body particles" }


func _apply_builder_particle_remove_brush(screen_position: Vector2) -> Dictionary:
	var position := _screen_to_orb_builder_position(screen_position)
	var removed := _remove_nearby_dust(position, 0.34, 42, false)
	_rebuild_after_builder_mutation(false)
	return { "message": "removed %s particles" % removed }


func _apply_builder_highway_brush(screen_position: Vector2, viewport_size: Vector2) -> Dictionary:
	var connection_pick := _pick_orb_lab_connection(screen_position, viewport_size)
	if not connection_pick.is_empty():
		var connection_id: int = int(connection_pick.get("id", -1))
		if connection_id >= 0 and connection_id < _connections.size():
			_create_builder_highway_bundle(_connections[connection_id].a, _connections[connection_id].b)
			select_orb_lab_connection(connection_id)
			return { "message": "added highway bundle" }
	var position := _screen_to_orb_builder_position(screen_position)
	var pair := _nearest_builder_node_pair(position)
	if pair.size() >= 2:
		_create_builder_highway_bundle(int(pair[0]), int(pair[1]))
		return { "message": "added highway between nearby nodes" }
	return { "message": "no nearby nodes" }


func _apply_builder_highway_remove_brush(screen_position: Vector2, viewport_size: Vector2) -> Dictionary:
	var connection_pick := _pick_orb_lab_connection(screen_position, viewport_size)
	if connection_pick.is_empty():
		return { "message": "no highway nearby" }
	var connection_id: int = int(connection_pick.get("id", -1))
	var removed := _remove_builder_highway_near_connection(connection_id)
	_rebuild_after_builder_mutation(true)
	return { "message": "removed %s highway connections" % removed }


func _apply_builder_cluster_remove_brush(screen_position: Vector2, viewport_size: Vector2) -> Dictionary:
	var cluster_pick := _pick_orb_lab_cluster(screen_position, viewport_size)
	if cluster_pick.is_empty():
		return { "message": "no cluster nearby" }
	var cluster_id: int = int(cluster_pick.get("id", -1))
	if cluster_id < 0 or cluster_id >= _nodes.size():
		return { "message": "no cluster nearby" }
	var removed_dust := _remove_nearby_dust(_nodes[cluster_id].base_position, 0.42, 80, true, cluster_id)
	var removed_connections := _remove_builder_connections_for_node(cluster_id, 4)
	_nodes[cluster_id].radius = maxf(_nodes[cluster_id].radius * 0.82, 0.006)
	_nodes[cluster_id].brightness = maxf(_nodes[cluster_id].brightness * 0.82, 0.74)
	_rebuild_after_builder_mutation(true)
	select_orb_lab_cluster(cluster_id)
	return { "message": "thinned cluster: %s particles, %s connections" % [removed_dust, removed_connections] }


func _screen_to_orb_builder_position(screen_position: Vector2) -> Vector3:
	if _camera == null or _graph_root == null:
		return Vector3.ZERO
	var ray_origin_world: Vector3 = _camera.project_ray_origin(screen_position)
	var ray_direction_world: Vector3 = _camera.project_ray_normal(screen_position).normalized()
	var inverse_graph: Transform3D = _graph_root.global_transform.affine_inverse()
	var origin: Vector3 = inverse_graph * ray_origin_world
	var direction: Vector3 = (inverse_graph.basis * ray_direction_world).normalized()
	var radius := _orb_radius * 0.96
	var a := direction.dot(direction)
	var b := 2.0 * origin.dot(direction)
	var c := origin.dot(origin) - radius * radius
	var discriminant := b * b - 4.0 * a * c
	if discriminant >= 0.0:
		var sqrt_d := sqrt(discriminant)
		var t1 := (-b - sqrt_d) / (2.0 * a)
		var t2 := (-b + sqrt_d) / (2.0 * a)
		var t := t1 if t1 > 0.0 else t2
		if t > 0.0:
			return origin + direction * t
	var closest_t := -origin.dot(direction) / maxf(a, 0.001)
	return (origin + direction * maxf(closest_t, 0.0)).limit_length(radius)


func _create_builder_cluster(position: Vector3) -> int:
	var hub := OrganismNode.new()
	hub.hub = true
	hub.phase = _rng.randf() * TAU
	hub.base_position = position.limit_length(_orb_radius * 1.08)
	hub.current_position = hub.base_position
	hub.radius = _rng.randf_range(0.009, 0.014)
	hub.brightness = _rng.randf_range(1.18, 1.56)
	var node_index := _nodes.size()
	_nodes.append(hub)
	_cluster_node_indices.append(node_index)
	_large_thinking_cluster_indices.append(node_index)
	if _node_connections.size() <= node_index:
		_node_connections.append([])
	_grow_builder_cluster(node_index, 20, 0.14, false)
	_connect_builder_cluster(node_index, 7)
	_rebuild_after_builder_mutation(true)
	return node_index


func _grow_builder_cluster(node_index: int, amount: int, spread: float, rebuild: bool = true) -> void:
	if node_index < 0 or node_index >= _nodes.size():
		return
	var hub := _nodes[node_index]
	var local_node_indices: Array[int] = []
	for node_offset in range(maxi(3, int(amount / 5))):
		var micro_node := OrganismNode.new()
		micro_node.hub = false
		micro_node.phase = _rng.randf() * TAU
		var micro_radius := pow(_rng.randf(), 1.35) * _rng.randf_range(0.025, spread * 0.92)
		micro_node.base_position = (hub.base_position + _random_unit_vector() * micro_radius).limit_length(_orb_radius * 1.16)
		micro_node.current_position = micro_node.base_position
		micro_node.radius = _rng.randf_range(0.0042, 0.0074)
		micro_node.brightness = _rng.randf_range(0.92, 1.42)
		var micro_index := _nodes.size()
		_nodes.append(micro_node)
		_node_connections.append([])
		local_node_indices.append(micro_index)
		_add_connection(node_index, micro_index, hub.base_position.distance_to(micro_node.base_position), true)
	for index in range(amount):
		var spark := OrganismNode.new()
		spark.hub = true
		spark.phase = _rng.randf() * TAU
		var cluster_radius := pow(_rng.randf(), 1.45) * _rng.randf_range(0.025, spread)
		spark.base_position = (hub.base_position + _random_unit_vector() * cluster_radius).limit_length(_orb_radius * 1.16)
		spark.current_position = spark.base_position
		spark.source_node = node_index
		var cluster_t := clampf(cluster_radius / maxf(spread, 0.001), 0.0, 1.0)
		spark.radius = _rng.randf_range(0.0028, 0.0064) * lerpf(1.20, 0.74, cluster_t)
		spark.brightness = _rng.randf_range(0.62, 1.36) * lerpf(1.45, 0.76, cluster_t)
		_dust.append(spark)
	for index in range(local_node_indices.size()):
		for other_index in range(index + 1, local_node_indices.size()):
			var a: int = local_node_indices[index]
			var b: int = local_node_indices[other_index]
			if _rng.randf() < 0.58:
				_add_connection(a, b, _nodes[a].base_position.distance_to(_nodes[b].base_position), true)
	hub.radius = minf(hub.radius + 0.00035, 0.018)
	hub.brightness = minf(hub.brightness + 0.045, 2.1)
	if rebuild:
		_connect_builder_cluster(node_index, 5)
		_rebuild_after_builder_mutation(true)


func _connect_builder_cluster(node_index: int, max_connections: int) -> void:
	if node_index < 0 or node_index >= _nodes.size():
		return
	while _node_connections.size() < _nodes.size():
		_node_connections.append([])
	var ranked: Array[Dictionary] = []
	for other_index in range(_nodes.size()):
		if other_index == node_index:
			continue
		var distance := _nodes[node_index].base_position.distance_to(_nodes[other_index].base_position)
		ranked.append({ "index": other_index, "distance": distance })
	ranked.sort_custom(func(left, right): return float(left["distance"]) < float(right["distance"]))
	for index in range(mini(max_connections, ranked.size())):
		var other_index: int = int(ranked[index]["index"])
		var distance: float = float(ranked[index]["distance"])
		_add_connection(node_index, other_index, distance, index < 2)


func _nearest_builder_node_pair(position: Vector3) -> Array[int]:
	var ranked: Array[Dictionary] = []
	for node_index in range(_nodes.size()):
		var distance := position.distance_to(_nodes[node_index].base_position)
		ranked.append({ "index": node_index, "distance": distance })
	ranked.sort_custom(func(left, right): return float(left["distance"]) < float(right["distance"]))
	if ranked.size() < 2:
		return []
	return [int(ranked[0]["index"]), int(ranked[1]["index"])]


func _create_builder_highway_bundle(a: int, b: int) -> void:
	if a < 0 or b < 0 or a >= _nodes.size() or b >= _nodes.size() or a == b:
		return
	var point_a := _nodes[a].base_position
	var point_b := _nodes[b].base_position
	var direction := (point_b - point_a).normalized()
	var tangent := direction.cross(Vector3.UP)
	if tangent.length_squared() < 0.001:
		tangent = direction.cross(Vector3.RIGHT)
	tangent = tangent.normalized()
	var bitangent := direction.cross(tangent).normalized()
	var previous_node := a
	for index in range(3):
		var t := float(index + 1) / 4.0
		var bundle_node := OrganismNode.new()
		bundle_node.hub = false
		bundle_node.phase = _rng.randf() * TAU
		var offset := tangent * _rng.randf_range(-0.055, 0.055) + bitangent * _rng.randf_range(-0.055, 0.055)
		bundle_node.base_position = point_a.lerp(point_b, t) + offset
		bundle_node.current_position = bundle_node.base_position
		bundle_node.radius = _rng.randf_range(0.0048, 0.0088)
		bundle_node.brightness = _rng.randf_range(1.12, 1.58)
		var new_index := _nodes.size()
		_nodes.append(bundle_node)
		_node_connections.append([])
		_add_connection(previous_node, new_index, _nodes[previous_node].base_position.distance_to(bundle_node.base_position), true)
		previous_node = new_index
	_add_connection(previous_node, b, _nodes[previous_node].base_position.distance_to(_nodes[b].base_position), true)
	_add_connection(a, b, point_a.distance_to(point_b), true)
	_rebuild_after_builder_mutation(true)


func _remove_nearby_dust(position: Vector3, radius: float, max_count: int, prefer_sourced: bool, source_node: int = -1) -> int:
	var ranked: Array[Dictionary] = []
	for dust_index in range(_dust.size()):
		var dust: OrganismNode = _dust[dust_index]
		if prefer_sourced and source_node >= 0 and dust.source_node != source_node:
			continue
		var distance := position.distance_to(dust.base_position)
		if distance <= radius:
			ranked.append({ "index": dust_index, "distance": distance })
	ranked.sort_custom(func(left, right): return float(left["distance"]) < float(right["distance"]))
	var remove_indices: Array[int] = []
	for index in range(mini(max_count, ranked.size())):
		remove_indices.append(int(ranked[index]["index"]))
	remove_indices.sort()
	remove_indices.reverse()
	for dust_index in remove_indices:
		_dust.remove_at(dust_index)
	return remove_indices.size()


func _remove_builder_highway_near_connection(connection_id: int) -> int:
	if connection_id < 0 or connection_id >= _connections.size():
		return 0
	var connection: OrganismConnection = _connections[connection_id]
	var midpoint := (_nodes[connection.a].base_position + _nodes[connection.b].base_position) * 0.5
	var removed := 0
	for index in range(_connections.size() - 1, -1, -1):
		var candidate: OrganismConnection = _connections[index]
		if not candidate.route:
			continue
		var candidate_midpoint := (_nodes[candidate.a].base_position + _nodes[candidate.b].base_position) * 0.5
		if candidate_midpoint.distance_to(midpoint) <= 0.42:
			_connections.remove_at(index)
			removed += 1
			if removed >= 10:
				break
	_rebuild_connection_indices()
	return removed


func _remove_builder_connections_for_node(node_index: int, max_count: int) -> int:
	if node_index < 0 or node_index >= _node_connections.size():
		return 0
	var connection_ids: Array = _node_connections[node_index].duplicate()
	var removed := 0
	for index in range(_connections.size() - 1, -1, -1):
		if not connection_ids.has(index):
			continue
		_connections.remove_at(index)
		removed += 1
		if removed >= max_count:
			break
	_rebuild_connection_indices()
	return removed


func _rebuild_connection_indices() -> void:
	_seen_connection_keys.clear()
	_node_connections.clear()
	_route_connection_indices.clear()
	for index in range(_nodes.size()):
		_node_connections.append([])
	for connection_index in range(_connections.size()):
		var connection: OrganismConnection = _connections[connection_index]
		if connection.a < 0 or connection.b < 0 or connection.a >= _nodes.size() or connection.b >= _nodes.size() or connection.a == connection.b:
			continue
		var low := mini(connection.a, connection.b)
		var high := maxi(connection.a, connection.b)
		connection.a = low
		connection.b = high
		var key := "%s:%s" % [low, high]
		_seen_connection_keys[key] = true
		var a_connections: Array = _node_connections[connection.a]
		var b_connections: Array = _node_connections[connection.b]
		a_connections.append(connection_index)
		b_connections.append(connection_index)
		if connection.route:
			_route_connection_indices.append(connection_index)


func _rebuild_after_builder_mutation(rebuild_connections_mesh: bool) -> void:
	_build_speech_morph_regions()
	_build_node_dust_indices()
	_build_node_cluster_flash_targets()
	_build_cluster_halo_indices()
	_setup_multimeshes()
	if rebuild_connections_mesh:
		_rebuild_static_connection_meshes()


func _distance_to_screen_segment(point: Vector2, a: Vector2, b: Vector2) -> float:
	var ab := b - a
	var length_squared := ab.length_squared()
	if length_squared <= 0.0001:
		return point.distance_to(a)
	var t := clampf((point - a).dot(ab) / length_squared, 0.0, 1.0)
	return point.distance_to(a + ab * t)


func _organism_state_name(state: int) -> String:
	match state:
		OrganismState.IDLE:
			return "IDLE"
		OrganismState.LISTENING:
			return "LISTENING"
		OrganismState.THINKING:
			return "THINKING"
		OrganismState.SPEAKING:
			return "SPEAKING"
		OrganismState.EXECUTING:
			return "EXECUTING"
		OrganismState.ERROR:
			return "ERROR"
		OrganismState.CONFIRMATION:
			return "CONFIRMATION"
		_:
			return "UNKNOWN"


func _state_activity_floor() -> float:
	match current_state:
		OrganismState.THINKING:
			return 0.42
		OrganismState.LISTENING:
			return 0.30
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
		OrganismState.LISTENING:
			return 1.06
		OrganismState.EXECUTING:
			return 1.18
		OrganismState.ERROR:
			return 1.16
		_:
			return 1.0


func _update_palette(amount: float) -> void:
	_palette["hot"] = Color(_palette["hot"]).lerp(Color(_target_palette["hot"]), amount)
	_palette["node"] = Color(_palette["node"]).lerp(Color(_target_palette["node"]), amount)
	_palette["line"] = Color(_palette["line"]).lerp(Color(_target_palette["line"]), amount)
	_palette["dim"] = Color(_palette["dim"]).lerp(Color(_target_palette["dim"]), amount)
	_palette["dust"] = Color(_palette["dust"]).lerp(Color(_target_palette["dust"]), amount)


func _update_autonomous_speech_motion(delta: float) -> void:
	if current_state != OrganismState.SPEAKING:
		_speech_energy = move_toward(_speech_energy, 0.0, delta * 2.4)
		return

	var energy_target := _speech_energy_override
	if energy_target < 0.0:
		var voice_wave := 0.5 + sin(_time * 5.2 + sin(_time * 1.7) * 0.8) * 0.5
		energy_target = 0.34 + voice_wave * 0.28
	_speech_energy = lerpf(_speech_energy, energy_target, minf(delta * 4.6, 1.0))

	_speech_region_timer -= delta
	if _speech_region_timer <= 0.0:
		_speech_region_timer = _rng.randf_range(0.22, 0.55)
		_pick_speech_region(_rng.randf())
		_spawn_speech_region_burst(_rng.randf(), 0.0)

	_speech_global_timer -= delta
	if _speech_global_timer <= 0.0:
		_speech_global_timer = _rng.randf_range(0.70, 1.35)
		_speech_global_morph = clampf(_speech_global_morph + _rng.randf_range(0.07, 0.18), 0.0, 0.52)

	_speech_ripple_timer -= delta
	if _speech_ripple_timer <= 0.0:
		_speech_ripple_timer = _rng.randf_range(0.12, 0.28)
		_spawn_speech_ripple()
		if _rng.randf() > 0.58:
			_spawn_speech_region_burst(_rng.randf(), 0.0)


func _reset_speech_regions() -> void:
	var default_axis := Vector3(0.35, 0.82, 0.45).normalized()
	_speech_region_axes.clear()
	_speech_region_targets.clear()
	_speech_region_weights.clear()
	_speech_region_directions.clear()
	_speech_region_axes.append(default_axis)
	_speech_region_targets.append(default_axis)
	_speech_region_weights.append(1.0)
	_speech_region_directions.append(1.0)


func _spawn_speech_ripple() -> void:
	if _speaking_profile_active:
		_speaking_profile_ripple_spawn_count += 1
		_speaking_profile_frame_ripple_spawns += 1
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


func _spawn_speech_region_burst(progress: float = -1.0, preferred_direction: float = 0.0) -> void:
	if current_state != OrganismState.SPEAKING:
		return
	if _speech_region_bursts.size() >= MAX_SPEECH_REGION_BURSTS:
		_speech_region_bursts.remove_at(0)
	var burst := SpeechRegionBurst.new()
	if not _speech_region_axes.is_empty() and _rng.randf() > 0.30:
		burst.axis = _speech_region_axes[_rng.randi_range(0, _speech_region_axes.size() - 1)]
	else:
		burst.axis = _speech_region_vector(progress, _rng.randi_range(0, 4), 5)
	burst.axis = burst.axis.normalized()
	burst.duration = _rng.randf_range(0.38, 0.92)
	burst.strength = _rng.randf_range(0.075, 0.185) * lerpf(0.72, 1.20, _speech_energy)
	burst.width = _rng.randf_range(0.36, 0.64)
	burst.direction = preferred_direction
	if absf(burst.direction) <= 0.001:
		burst.direction = 1.0 if _rng.randf() > 0.48 else -1.0
	burst.phase = _rng.randf() * TAU
	_speech_region_bursts.append(burst)


func _update_speech_ripples(delta: float) -> void:
	for index in range(_speech_ripples.size() - 1, -1, -1):
		var ripple: SpeechRipple = _speech_ripples[index]
		ripple.age += delta
		if ripple.age >= ripple.duration:
			_speech_ripples.remove_at(index)


func _update_speech_region_bursts(delta: float) -> void:
	for index in range(_speech_region_bursts.size() - 1, -1, -1):
		var burst: SpeechRegionBurst = _speech_region_bursts[index]
		burst.age += delta
		if burst.age >= burst.duration:
			_speech_region_bursts.remove_at(index)


func _update_speech_region_axes(delta: float) -> void:
	var amount := minf(delta * 4.6, 1.0)
	var count := mini(_speech_region_axes.size(), _speech_region_targets.size())
	for index in range(count):
		var axis: Vector3 = _speech_region_axes[index]
		var target: Vector3 = _speech_region_targets[index]
		_speech_region_axes[index] = axis.lerp(target, amount).normalized()


func _pick_speech_region(progress: float = -1.0) -> void:
	if _speaking_profile_active:
		_speaking_profile_region_pick_count += 1
		_speaking_profile_frame_region_picks += 1
	var roll := _rng.randf()
	var region_count := 3
	if roll > 0.90:
		region_count = 5
		_speech_global_morph = clampf(_speech_global_morph + _rng.randf_range(0.10, 0.20), 0.0, 0.72)
	elif roll > 0.66:
		region_count = 4
	elif roll > 0.28:
		region_count = 3
	else:
		region_count = 2

	_speech_region_targets.clear()
	_speech_region_weights.clear()
	_speech_region_directions.clear()
	if _speaking_profile_active:
		_speaking_profile_region_array_reset_count += 3
		_speaking_profile_frame_region_array_resets += 3
	for index in range(region_count):
		_speech_region_targets.append(_speech_region_vector(progress, index, region_count))
		_speech_region_weights.append(_rng.randf_range(0.72, 1.0))
		var direction: float = 1.0 if _rng.randf() > 0.5 else -1.0
		if index == 0 and region_count > 1:
			direction = 1.0
		elif index == 1 and region_count > 1:
			direction = -1.0
		_speech_region_directions.append(direction)

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
	var ripple := _speech_ripple_influence(radial)
	var center_t := 1.0 - clampf(position.length() / _orb_radius, 0.0, 1.0)
	return clampf(regional * 0.98 + ripple * 0.78 + _speech_global_morph * (0.12 + center_t * 0.22), 0.0, 1.0)


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
	var signed_influence := _speech_signed_influence(radial)
	var ripple_influence := _speech_ripple_influence(radial)
	if ripple_influence > 0.001:
		signed_influence += ripple_influence * sin(_time * 7.8 + phase) * 0.75
	if absf(signed_influence) <= 0.001 and influence <= 0.001:
		return Vector3.ZERO
	var tangent := _speech_tangent_axis(radial)
	if tangent.length_squared() < 0.001:
		tangent = radial.cross(Vector3.UP)
	if tangent.length_squared() < 0.001:
		tangent = Vector3.RIGHT
	tangent = tangent.normalized()
	var wave := sin(_time * 4.2 + phase * 1.7) * 0.5 + 0.5
	var smooth_wave := pow(wave, 0.72)
	var regional_push := radial * signed_influence * smooth_wave * 0.235
	var lateral_pull := tangent * influence * sin(_time * 5.8 + phase) * 0.045
	return (regional_push + lateral_pull) * amount_scale


func _update_cached_speech_morph_regions() -> void:
	if _speech_morph_region_offsets.is_empty():
		return
	if current_state != OrganismState.SPEAKING:
		for index in range(_speech_morph_region_offsets.size()):
			_speech_morph_region_offsets[index] = Vector3.ZERO
		return
	for index in range(_speech_morph_region_axes.size()):
		var axis := _speech_morph_region_axes[index]
		var phase := float(index) * 1.137 + _time * 0.11
		var offset := _speech_morph_offset(axis * _orb_radius, phase, 1.0)
		offset += _speech_region_burst_offset(axis)
		_speech_morph_region_offsets[index] = offset


func _speech_region_burst_offset(axis: Vector3) -> Vector3:
	if _speech_region_bursts.is_empty():
		return Vector3.ZERO
	var offset := Vector3.ZERO
	for burst in _speech_region_bursts:
		var life := clampf(burst.age / maxf(burst.duration, 0.001), 0.0, 1.0)
		var envelope := sin(life * PI)
		var closeness := clampf(axis.dot(burst.axis) * 0.5 + 0.5, 0.0, 1.0)
		var regional := pow(closeness, lerpf(7.0, 13.0, burst.width))
		if regional <= 0.002:
			continue
		var chatter := 0.78 + sin(_time * 8.5 + burst.phase) * 0.22
		var radial_push := axis * burst.direction * burst.strength * regional * envelope * chatter
		var tangent := burst.axis.cross(axis)
		if tangent.length_squared() > 0.001:
			tangent = tangent.normalized() * sin(life * TAU + burst.phase) * burst.strength * regional * envelope * 0.24
		else:
			tangent = Vector3.ZERO
		offset += radial_push + tangent
	return offset


func _cached_node_speech_morph(index: int, phase: float) -> Vector3:
	if not _cached_speech_motion_enabled:
		return Vector3.ZERO
	if index < 0 or index >= _node_speech_primary_regions.size():
		return Vector3.ZERO
	var primary_index := _node_speech_primary_regions[index]
	var secondary_index := _node_speech_secondary_regions[index]
	if primary_index < 0 or primary_index >= _speech_morph_region_offsets.size():
		return Vector3.ZERO
	var primary := _speech_morph_region_offsets[primary_index]
	var secondary := primary
	if secondary_index >= 0 and secondary_index < _speech_morph_region_offsets.size():
		secondary = _speech_morph_region_offsets[secondary_index]
	var blend := _node_speech_secondary_weights[index]
	var local_weight := _node_speech_local_weights[index]
	var chatter := 0.78 + sin(_time * 4.2 + phase * 1.7) * 0.22
	return primary.lerp(secondary, blend) * local_weight * chatter


func _cached_dust_speech_morph(index: int, phase: float) -> Vector3:
	if not _cached_speech_motion_enabled:
		return Vector3.ZERO
	if index < 0 or index >= _dust_speech_primary_regions.size():
		return Vector3.ZERO
	var local_weight := _dust_speech_local_weights[index]
	if local_weight <= 0.001:
		return Vector3.ZERO
	var primary_index := _dust_speech_primary_regions[index]
	var secondary_index := _dust_speech_secondary_regions[index]
	if primary_index < 0 or primary_index >= _speech_morph_region_offsets.size():
		return Vector3.ZERO
	var primary := _speech_morph_region_offsets[primary_index]
	var secondary := primary
	if secondary_index >= 0 and secondary_index < _speech_morph_region_offsets.size():
		secondary = _speech_morph_region_offsets[secondary_index]
	var blend := _dust_speech_secondary_weights[index]
	var chatter := 0.68 + sin(_time * 3.1 + phase * 1.23) * 0.14
	return primary.lerp(secondary, blend) * local_weight * chatter


func _real_node_speech_motion(index: int, phase: float) -> Vector3:
	if not _real_speech_motion_enabled or current_state != OrganismState.SPEAKING:
		return Vector3.ZERO
	if index < 0 or index >= _node_speech_primary_regions.size():
		return Vector3.ZERO
	var node := _nodes[index]
	var radial := node.base_position.normalized() if node.base_position.length_squared() > 0.001 else Vector3.UP
	var local_weight := _node_speech_local_weights[index]
	return _real_speech_motion_for_membership(
		radial,
		phase,
		_node_speech_primary_regions[index],
		_node_speech_secondary_regions[index],
		_node_speech_secondary_weights[index],
		local_weight
	)


func _real_dust_speech_motion(index: int, phase: float) -> Vector3:
	if not _real_speech_motion_enabled or current_state != OrganismState.SPEAKING:
		return Vector3.ZERO
	if index < 0 or index >= _dust_speech_primary_regions.size():
		return Vector3.ZERO
	var local_weight := _dust_speech_local_weights[index]
	if local_weight <= 0.001:
		return Vector3.ZERO
	var dust := _dust[index]
	var radial := dust.base_position.normalized() if dust.base_position.length_squared() > 0.001 else Vector3.UP
	return _real_speech_motion_for_membership(
		radial,
		phase,
		_dust_speech_primary_regions[index],
		_dust_speech_secondary_regions[index],
		_dust_speech_secondary_weights[index],
		local_weight * 0.62
	)


func _real_speech_motion_for_membership(radial: Vector3, phase: float, primary_index: int, secondary_index: int, secondary_weight: float, local_weight: float) -> Vector3:
	if primary_index < 0 or primary_index >= _speech_morph_region_axes.size():
		return Vector3.ZERO
	var primary_axis: Vector3 = _speech_morph_region_axes[primary_index]
	var secondary_axis := primary_axis
	if secondary_index >= 0 and secondary_index < _speech_morph_region_axes.size():
		secondary_axis = _speech_morph_region_axes[secondary_index]
	var blend := clampf(secondary_weight * _real_speech_motion_region_blend, 0.0, 1.0)
	var axis := primary_axis.lerp(secondary_axis, blend).normalized()
	var closeness := pow(clampf(radial.dot(axis) * 0.5 + 0.5, 0.0, 1.0), 5.5)
	var energy := _speech_energy * _real_speech_motion_strength * local_weight * closeness
	if energy <= 0.0005:
		return Vector3.ZERO
	var wave := sin(_time * _real_speech_motion_speed + phase * 1.41 + float(primary_index) * 0.73)
	var envelope := 0.56 + wave * 0.44
	var direction := 1.0
	if primary_index < _speech_region_directions.size():
		direction = _speech_region_directions[primary_index]
	var radial_push := radial * direction * envelope * energy
	var tangent := axis.cross(radial)
	if tangent.length_squared() > 0.001:
		tangent = tangent.normalized() * sin(_time * (_real_speech_motion_speed * 1.23) + phase) * energy * 0.28
	else:
		tangent = Vector3.ZERO
	return radial_push + tangent


func _speech_signed_influence(radial: Vector3) -> float:
	var signed_regional := 0.0
	for index in range(_speech_region_axes.size()):
		var weight := 1.0
		if index < _speech_region_weights.size():
			weight = _speech_region_weights[index]
		var direction: float = 1.0
		if index < _speech_region_directions.size():
			direction = _speech_region_directions[index]
		var axis: Vector3 = _speech_region_axes[index]
		var local: float = pow(clampf(radial.dot(axis) * 0.5 + 0.5, 0.0, 1.0), 12.0) * weight
		signed_regional += local * direction
	return clampf(signed_regional, -1.05, 1.05) * _speech_energy


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
	var update_color := _orb_frame_index == 0 or _orb_frame_index % _node_color_update_stride() == 0
	var profile_active := _speaking_profile_active
	var motion_enabled := _state_feature_enabled("node_motion")
	for index in range(_nodes.size()):
		var node := _nodes[index]
		var position_started_usec := Time.get_ticks_usec() if profile_active else 0
		var drift := Vector3.ZERO
		if motion_enabled:
			var drift_scale := 0.030 + _activity * 0.045
			drift = Vector3(
				sin(_time * 0.37 + node.phase),
				cos(_time * 0.29 + node.phase * 1.37),
				sin(_time * 0.23 + node.phase * 0.73)
			) * drift_scale
		var radial := node.base_position.normalized() if node.base_position.length_squared() > 0.001 else Vector3.UP
		var local_breath := radial * sin(_time * 0.56 + node.phase) * (0.012 + _activity * 0.018) if motion_enabled else Vector3.ZERO
		if profile_active:
			_speaking_profile_frame_node_position_ms += _elapsed_ms_since_usec(position_started_usec)
		var morph_started_usec := Time.get_ticks_usec() if profile_active else 0
		var speech_morph: Vector3 = _cached_node_speech_morph(index, node.phase) if motion_enabled and current_state == OrganismState.SPEAKING else Vector3.ZERO
		if profile_active:
			_speaking_profile_frame_node_morph_ms += _elapsed_ms_since_usec(morph_started_usec)
		var previous_position := node.current_position
		position_started_usec = Time.get_ticks_usec() if profile_active else 0
		var target_position := node.base_position + drift + local_breath + speech_morph + (_real_node_speech_motion(index, node.phase) if motion_enabled else Vector3.ZERO)
		if _real_speech_motion_enabled and current_state == OrganismState.SPEAKING:
			node.current_position = node.current_position.lerp(target_position, minf(_delta * _real_speech_motion_smoothing, 1.0))
		else:
			node.current_position = target_position
		if profile_active:
			var displacement := previous_position.distance_to(node.current_position)
			_speaking_profile_frame_node_displacement_total += displacement
			_speaking_profile_frame_node_displacement_max = maxf(_speaking_profile_frame_node_displacement_max, displacement)
			_speaking_profile_frame_node_displacement_count += 1

		var center_t := _center_visual_t(node.current_position)
		var depth_t := _balanced_depth_light(node.current_position, 0.94, 1.16)
		var pulse := 0.0
		if node.hub:
			pulse = pow(maxf(0.0, sin(_time * (0.95 + node.phase * 0.02) + node.phase)), 8.0) * 0.75
		if profile_active:
			_speaking_profile_frame_node_position_ms += _elapsed_ms_since_usec(position_started_usec)
		var speech_lookup_started_usec := Time.get_ticks_usec() if profile_active else 0
		var speech_light := _node_light(index)
		if profile_active:
			_speaking_profile_frame_node_speech_lookup_ms += _elapsed_ms_since_usec(speech_lookup_started_usec)
		var lifecycle_dim := _cluster_lifecycle_node_dim(index)
		position_started_usec = Time.get_ticks_usec() if profile_active else 0
		var shell_t := clampf(node.current_position.length() / _orb_radius, 0.0, 1.0)
		var shell_light := pow(clampf((shell_t - 0.58) / 0.42, 0.0, 1.0), 0.9) * 0.28
		var scale := node.radius * (1.0 + pulse * 0.14 + speech_light * 0.42) * (1.0 - lifecycle_dim * 0.28)
		var transform := Transform3D(Basis().scaled(Vector3.ONE * scale), node.current_position)
		if profile_active:
			_speaking_profile_frame_node_position_ms += _elapsed_ms_since_usec(position_started_usec)
		if node.hub:
			var upload_started_usec := Time.get_ticks_usec() if profile_active else 0
			_hub_multimesh.multimesh.set_instance_transform(hub_index, transform)
			if profile_active:
				_speaking_profile_frame_node_transform_upload_ms += _elapsed_ms_since_usec(upload_started_usec)
			_orb_profile_node_transform_sets += 1
			if _speaking_profile_active:
				_speaking_profile_frame_node_transform_sets += 1
			if update_color:
				var color_started_usec := Time.get_ticks_usec() if profile_active else 0
				var alpha := clampf((0.38 + center_t * 0.34 + shell_light + pulse * 0.18 + speech_light * 0.62) * depth_t * _brightness * _sharp_geometry_alpha_scale * (1.0 - lifecycle_dim), 0.04, 0.88)
				var color := Color(_palette["node"]).lerp(Color(_palette["hot"]), clampf((center_t * 0.48 + shell_light * 0.9 + pulse + speech_light * 0.72) * _sharp_hot_mix_scale, 0.0, 0.86))
				color.a = alpha
				if profile_active:
					_speaking_profile_frame_node_color_calc_ms += _elapsed_ms_since_usec(color_started_usec)
					var previous_color_index := _node_multimesh.multimesh.instance_count + hub_index
					if previous_color_index < _speaking_profile_previous_node_colors.size():
						var color_delta := _profile_color_delta(_speaking_profile_previous_node_colors[previous_color_index], color)
						_speaking_profile_frame_node_color_delta_total += color_delta
						_speaking_profile_frame_node_color_delta_max = maxf(_speaking_profile_frame_node_color_delta_max, color_delta)
						_speaking_profile_frame_node_color_delta_count += 1
						_speaking_profile_previous_node_colors[previous_color_index] = color
				upload_started_usec = Time.get_ticks_usec() if profile_active else 0
				_hub_multimesh.multimesh.set_instance_color(hub_index, color)
				if profile_active:
					_speaking_profile_frame_node_color_upload_ms += _elapsed_ms_since_usec(upload_started_usec)
				_orb_profile_node_color_sets += 1
				if _speaking_profile_active:
					_speaking_profile_frame_node_color_sets += 1
			hub_index += 1
		else:
			var upload_started_usec := Time.get_ticks_usec() if profile_active else 0
			_node_multimesh.multimesh.set_instance_transform(node_index, transform)
			if profile_active:
				_speaking_profile_frame_node_transform_upload_ms += _elapsed_ms_since_usec(upload_started_usec)
			_orb_profile_node_transform_sets += 1
			if _speaking_profile_active:
				_speaking_profile_frame_node_transform_sets += 1
			if update_color:
				var color_started_usec := Time.get_ticks_usec() if profile_active else 0
				var alpha := clampf((0.38 + center_t * 0.34 + shell_light + pulse * 0.18 + speech_light * 0.62) * depth_t * _brightness * _sharp_geometry_alpha_scale * (1.0 - lifecycle_dim), 0.04, 0.88)
				var color := Color(_palette["node"]).lerp(Color(_palette["hot"]), clampf((center_t * 0.48 + shell_light * 0.9 + pulse + speech_light * 0.72) * _sharp_hot_mix_scale, 0.0, 0.86))
				color.a = alpha
				if profile_active:
					_speaking_profile_frame_node_color_calc_ms += _elapsed_ms_since_usec(color_started_usec)
					if node_index < _speaking_profile_previous_node_colors.size():
						var color_delta := _profile_color_delta(_speaking_profile_previous_node_colors[node_index], color)
						_speaking_profile_frame_node_color_delta_total += color_delta
						_speaking_profile_frame_node_color_delta_max = maxf(_speaking_profile_frame_node_color_delta_max, color_delta)
						_speaking_profile_frame_node_color_delta_count += 1
						_speaking_profile_previous_node_colors[node_index] = color
				upload_started_usec = Time.get_ticks_usec() if profile_active else 0
				_node_multimesh.multimesh.set_instance_color(node_index, color)
				if profile_active:
					_speaking_profile_frame_node_color_upload_ms += _elapsed_ms_since_usec(upload_started_usec)
				_orb_profile_node_color_sets += 1
				if _speaking_profile_active:
					_speaking_profile_frame_node_color_sets += 1
			node_index += 1


func _update_dust(_delta: float, start_index: int = 0, stride: int = 1) -> void:
	var update_color := _orb_frame_index == 0 or _orb_frame_index % _dust_color_update_stride() == 0
	var profile_active := _speaking_profile_active
	var motion_enabled := _state_feature_enabled("dust_motion")
	for index in range(start_index, _dust.size(), stride):
		var dust := _dust[index]
		var position_started_usec := Time.get_ticks_usec() if profile_active else 0
		var orbit := Vector3.ZERO
		if motion_enabled:
			orbit = Vector3(
				cos(_time * 0.055 + dust.phase),
				sin(_time * 0.071 + dust.phase * 1.2),
				cos(_time * 0.043 + dust.phase * 0.7)
			) * 0.045
		var can_morph: bool = dust.hub or dust.source_node >= 0
		if profile_active:
			_speaking_profile_frame_dust_position_ms += _elapsed_ms_since_usec(position_started_usec)
		var morph_started_usec := Time.get_ticks_usec() if profile_active else 0
		var speech_morph: Vector3 = _cached_dust_speech_morph(index, dust.phase) if motion_enabled and current_state == OrganismState.SPEAKING and can_morph else Vector3.ZERO
		if profile_active:
			_speaking_profile_frame_dust_morph_ms += _elapsed_ms_since_usec(morph_started_usec)
		var previous_position := dust.current_position
		position_started_usec = Time.get_ticks_usec() if profile_active else 0
		var target_position := dust.base_position + orbit + speech_morph + (_real_dust_speech_motion(index, dust.phase) if motion_enabled else Vector3.ZERO)
		if _real_speech_motion_enabled and current_state == OrganismState.SPEAKING:
			dust.current_position = dust.current_position.lerp(target_position, minf(_delta * _real_speech_motion_smoothing, 1.0))
		else:
			dust.current_position = target_position
		var depth_t := _balanced_depth_light(dust.current_position, 0.82, 1.06)
		var cluster_pulse := 0.0
		if dust.hub:
			cluster_pulse = pow(maxf(0.0, sin(_time * 1.05 + dust.phase)), 5.0) * 0.45
		if profile_active:
			var displacement := previous_position.distance_to(dust.current_position)
			_speaking_profile_frame_dust_displacement_total += displacement
			_speaking_profile_frame_dust_displacement_max = maxf(_speaking_profile_frame_dust_displacement_max, displacement)
			_speaking_profile_frame_dust_displacement_count += 1
			_speaking_profile_frame_dust_position_ms += _elapsed_ms_since_usec(position_started_usec)
		var speech_lookup_started_usec := Time.get_ticks_usec() if profile_active else 0
		var speech_light := _dust_light(index)
		if profile_active:
			_speaking_profile_frame_dust_speech_lookup_ms += _elapsed_ms_since_usec(speech_lookup_started_usec)
		var lifecycle_dim := _cluster_lifecycle_dust_dim(dust)
		position_started_usec = Time.get_ticks_usec() if profile_active else 0
		var radius := dust.radius * (1.0 + cluster_pulse * 0.38 + speech_light * 0.55) * (1.0 - lifecycle_dim * 0.40)
		var transform := Transform3D(Basis().scaled(Vector3.ONE * radius), dust.current_position)
		if profile_active:
			_speaking_profile_frame_dust_position_ms += _elapsed_ms_since_usec(position_started_usec)
		var upload_started_usec := Time.get_ticks_usec() if profile_active else 0
		_dust_multimesh.multimesh.set_instance_transform(index, transform)
		if profile_active:
			_speaking_profile_frame_dust_transform_upload_ms += _elapsed_ms_since_usec(upload_started_usec)
		_orb_profile_dust_transform_sets += 1
		if _speaking_profile_active:
			_speaking_profile_frame_dust_transform_sets += 1
		if update_color:
			var color_started_usec := Time.get_ticks_usec() if profile_active else 0
			var color := Color(_palette["dust"]).lerp(Color(_palette["hot"]), 0.78 if dust.hub else 0.0)
			color = color.lerp(Color(_palette["hot"]), clampf(speech_light * 0.85, 0.0, 0.75))
			color.a = dust.brightness * depth_t * (0.66 + cluster_pulse * 0.22 + speech_light * 0.62 if dust.hub else 0.21 + speech_light * 0.16) * (1.0 - lifecycle_dim)
			if profile_active:
				_speaking_profile_frame_dust_color_calc_ms += _elapsed_ms_since_usec(color_started_usec)
				if index < _speaking_profile_previous_dust_colors.size():
					var color_delta := _profile_color_delta(_speaking_profile_previous_dust_colors[index], color)
					_speaking_profile_frame_dust_color_delta_total += color_delta
					_speaking_profile_frame_dust_color_delta_max = maxf(_speaking_profile_frame_dust_color_delta_max, color_delta)
					_speaking_profile_frame_dust_color_delta_count += 1
					_speaking_profile_previous_dust_colors[index] = color
			upload_started_usec = Time.get_ticks_usec() if profile_active else 0
			_dust_multimesh.multimesh.set_instance_color(index, color)
			if profile_active:
				_speaking_profile_frame_dust_color_upload_ms += _elapsed_ms_since_usec(upload_started_usec)
			_orb_profile_dust_color_sets += 1
			if _speaking_profile_active:
				_speaking_profile_frame_dust_color_sets += 1


func _rebuild_static_connection_meshes() -> void:
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
		var midpoint := (a.base_position + b.base_position) * 0.5
		var center_t := _center_visual_t(midpoint)
		var depth_t := _balanced_depth_light(midpoint, 0.90, 1.14)
		var route_enabled := _connection_lab_route(connection_index, connection.route)
		var route_t := 1.0 if route_enabled else 0.0
		var shell_t := clampf(midpoint.length() / _orb_radius, 0.0, 1.0)
		var shell_band := pow(clampf((shell_t - 0.52) / 0.48, 0.0, 1.0), 0.72)
		var route_boost := 1.08 + route_t * _route_connection_render_boost
		var selection_flash := _connection_selection_flash_amount(connection_index)
		var alpha_scale := _connection_lab_override(connection_index, "alpha_scale", 1.0)
		var width_scale := _connection_lab_override(connection_index, "width_scale", 1.0)
		var glow_scale := _connection_lab_override(connection_index, "glow_scale", 1.0)
		var hot_mix := clampf(route_t * _route_connection_hot_mix + _connection_lab_override(connection_index, "hot_mix", 0.0), 0.0, 1.0)
		var alpha := clampf(connection.base_alpha * depth_t * route_boost * alpha_scale + selection_flash * 0.55, 0.075, 1.0)
		var color := Color.WHITE.lerp(Color(_palette["hot"]), hot_mix)
		if selection_flash > 0.0:
			color = color.lerp(Color(1.0, 0.05, 0.04, 1.0), selection_flash)
		color.a = alpha
		var glow_color := Color.WHITE.lerp(Color(_palette["hot"]), hot_mix)
		if selection_flash > 0.0:
			glow_color = glow_color.lerp(Color(1.0, 0.05, 0.04, 1.0), selection_flash)
		var glow_alpha_scale := 1.0 + route_t * (_route_connection_glow_alpha_scale - 1.0)
		glow_color.a = alpha * (0.040 + center_t * 0.018 + shell_band * 0.028 + route_t * 0.030 + selection_flash * 0.18) * glow_alpha_scale * glow_scale
		var glow_width := connection.width * width_scale * (1.75 + route_t * 0.40 + selection_flash * 1.30) * (1.0 + route_t * (_route_connection_glow_width_scale - 1.0))
		var core_width := connection.width * width_scale * (1.08 + route_t * 0.12 + selection_flash * 0.65) * (1.0 + route_t * (_route_connection_core_width_scale - 1.0))
		_add_line_quad_3d(glow_vertices, glow_colors, glow_indices, a.base_position, b.base_position, glow_width, glow_color, local_camera_forward, local_camera_up)
		_add_line_quad_3d(core_vertices, core_colors, core_indices, a.base_position, b.base_position, core_width, color, local_camera_forward, local_camera_up)

	_line_mesh_instance.mesh = _mesh_from_arrays(core_vertices, core_colors, core_indices)
	_glow_line_mesh_instance.mesh = _mesh_from_arrays(glow_vertices, glow_colors, glow_indices)


func _update_pulse_connections() -> void:
	if _pulses.is_empty() and _cluster_activations.is_empty():
		if _pulse_connection_mesh_active:
			_pulse_line_mesh_instance.mesh = ArrayMesh.new()
			_pulse_glow_line_mesh_instance.mesh = ArrayMesh.new()
			_pulse_connection_mesh_active = false
			_orb_profile_pulse_mesh_builds += 1
			if _speaking_profile_active:
				_speaking_profile_pulse_mesh_build_count += 1
				_speaking_profile_array_mesh_resource_count += 2
				_speaking_profile_frame_pulse_mesh_builds += 1
				_speaking_profile_frame_array_mesh_resources += 2
		return

	var core_vertices := PackedVector3Array()
	var core_colors := PackedColorArray()
	var core_indices := PackedInt32Array()
	var glow_vertices := PackedVector3Array()
	var glow_colors := PackedColorArray()
	var glow_indices := PackedInt32Array()
	if _speaking_profile_active:
		_speaking_profile_packed_array_count += 6
		_speaking_profile_frame_packed_arrays += 6
	var local_camera_forward := _graph_root.global_transform.basis.inverse() * _camera.global_transform.basis.z
	var local_camera_up := _graph_root.global_transform.basis.inverse() * _camera.global_transform.basis.y
	local_camera_forward = local_camera_forward.normalized()
	local_camera_up = local_camera_up.normalized()

	_add_traveling_light_segments(core_vertices, core_colors, core_indices, glow_vertices, glow_colors, glow_indices, local_camera_forward, local_camera_up)
	_add_cluster_activation_segments(core_vertices, core_colors, core_indices, glow_vertices, glow_colors, glow_indices, local_camera_forward, local_camera_up)

	var mesh_started_usec := Time.get_ticks_usec()
	_pulse_line_mesh_instance.mesh = _mesh_from_arrays(core_vertices, core_colors, core_indices)
	_pulse_glow_line_mesh_instance.mesh = _mesh_from_arrays(glow_vertices, glow_colors, glow_indices)
	_orb_profile_pulse_mesh_ms += _elapsed_ms_since_usec(mesh_started_usec)
	_orb_profile_pulse_mesh_builds += 1
	if _speaking_profile_active:
		_speaking_profile_pulse_mesh_build_count += 1
		_speaking_profile_array_mesh_resource_count += 2
		_speaking_profile_frame_pulse_mesh_builds += 1
		_speaking_profile_frame_array_mesh_resources += 2
	_pulse_connection_mesh_active = not core_vertices.is_empty() or not glow_vertices.is_empty()


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


func _add_cluster_activation_segments(
	core_vertices: PackedVector3Array,
	core_colors: PackedColorArray,
	core_indices: PackedInt32Array,
	glow_vertices: PackedVector3Array,
	glow_colors: PackedColorArray,
	glow_indices: PackedInt32Array,
	local_camera_forward: Vector3,
	local_camera_up: Vector3
) -> void:
	for activation in _cluster_activations:
		if activation.node_index < 0 or activation.node_index >= _node_connections.size():
			continue
		var amount := _cluster_activation_amount(activation)
		if amount <= 0.01:
			continue
		var expansion := _cluster_activation_expansion(activation)
		var connection_indices: Array = _node_connections[activation.node_index]
		var limit := mini(connection_indices.size(), int(lerpf(_cluster_activation_segment_min, _cluster_activation_segment_max, expansion)))
		for index in range(limit):
			var connection_index: int = connection_indices[index]
			var connection: OrganismConnection = _connections[connection_index]
			var a := _nodes[connection.a].current_position
			var b := _nodes[connection.b].current_position
			var falloff := float(index) / maxf(float(limit), 1.0)
			var intensity := amount * lerpf(0.94, 0.28, falloff)
			var core_color := Color(_palette["hot"])
			core_color.a = clampf(intensity * (0.70 if connection.route else 0.54), 0.0, 0.92)
			var glow_color := Color(_palette["hot"])
			glow_color.a = clampf(intensity * 0.32, 0.0, 0.44)
			_add_line_quad_3d(glow_vertices, glow_colors, glow_indices, a, b, connection.width * 4.8, glow_color, local_camera_forward, local_camera_up)
			_add_line_quad_3d(core_vertices, core_colors, core_indices, a, b, connection.width * 1.95, core_color, local_camera_forward, local_camera_up)


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
		elif current_state == OrganismState.LISTENING:
			_pulse_timer = _rng.randf_range(0.58, 1.05)
			_spawn_energy_burst(_rng.randi_range(2, 3))
		else:
			_pulse_timer = _rng.randf_range(0.18, 0.48)

	for index in range(_pulses.size() - 1, -1, -1):
		var pulse: EnergyPulse = _pulses[index]
		var speed_wobble := 1.0 + sin(_time * pulse.speed_wobble + pulse.speed_phase) * _energy_pulse_speed_wobble_amount
		pulse.progress += pulse.speed * maxf(speed_wobble, 0.72) * delta
		if pulse.progress >= 1.0:
			_arrive_energy_pulse(pulse)
			_pulses.remove_at(index)

	for index in range(_cluster_activations.size() - 1, -1, -1):
		var activation: ClusterActivation = _cluster_activations[index]
		activation.age += delta
		if activation.age >= activation.duration:
			_cluster_activation_cooldowns[activation.node_index] = _cluster_activation_cooldown_seconds
			_cluster_activations.remove_at(index)

	for index in range(_cluster_lifecycle_events.size() - 1, -1, -1):
		var event: ClusterLifecycle = _cluster_lifecycle_events[index]
		event.age += delta
		if event.age >= event.duration:
			_cluster_lifecycle_events.remove_at(index)

	if not _cluster_activation_cooldowns.is_empty():
		for cluster_node_index in _cluster_activation_cooldowns.keys():
			var remaining := float(_cluster_activation_cooldowns[cluster_node_index]) - delta
			if remaining <= 0.0:
				_cluster_activation_cooldowns.erase(cluster_node_index)
			else:
				_cluster_activation_cooldowns[cluster_node_index] = remaining

	for index in range(_pending_energy_branches.size() - 1, -1, -1):
		var pending: PendingEnergyBranch = _pending_energy_branches[index]
		pending.age += delta
		if pending.age >= pending.delay:
			_continue_energy_branch(pending)
			_pending_energy_branches.remove_at(index)

	for index in range(MAX_PULSES):
		if index >= _pulses.size():
			_pulse_multimesh.multimesh.set_instance_transform(index, Transform3D(Basis().scaled(Vector3.ZERO), Vector3.ZERO))
			_pulse_multimesh.multimesh.set_instance_color(index, Color(0, 0, 0, 0))
			if _speaking_profile_active:
				_speaking_profile_frame_pulse_transform_sets += 1
				_speaking_profile_frame_pulse_color_sets += 1
			continue
		var pulse: EnergyPulse = _pulses[index]
		var position := _pulse_position(pulse)
		var fade := sin(pulse.progress * PI)
		var radius := 0.010 + pulse.brightness * 0.013
		var color := Color(_palette["hot"])
		color.a = clampf(fade * pulse.brightness * 0.86, 0.0, 1.0)
		_pulse_multimesh.multimesh.set_instance_transform(index, Transform3D(Basis().scaled(Vector3.ONE * radius), position))
		_pulse_multimesh.multimesh.set_instance_color(index, color)
		if _speaking_profile_active:
			_speaking_profile_frame_pulse_transform_sets += 1
			_speaking_profile_frame_pulse_color_sets += 1
	var speech_maps_started_usec := Time.get_ticks_usec()
	_rebuild_speech_light_maps()
	_orb_profile_speech_maps_ms += _elapsed_ms_since_usec(speech_maps_started_usec)


func _rebuild_speech_light_maps() -> void:
	_node_speech_light.clear()
	_dust_speech_light.clear()
	_connection_speech_light.clear()
	if _speaking_profile_active:
		_speaking_profile_speech_map_rebuild_count += 1
		_speaking_profile_speech_map_clear_count += 3
		_speaking_profile_frame_speech_map_rebuilds += 1
	if current_state != OrganismState.THINKING and current_state != OrganismState.LISTENING and _cluster_lifecycle_events.is_empty():
		return
	var state_scale := 0.50 if current_state == OrganismState.LISTENING else 1.0
	for event in _cluster_lifecycle_events:
		var amount := _cluster_lifecycle_birth_amount(event) * state_scale
		if amount > 0.01:
			_add_cluster_activation_light(event.birth_node, amount, _cluster_lifecycle_birth_expansion(event))
	for index in range(_cluster_activations.size()):
		var activation: ClusterActivation = _cluster_activations[index]
		var amount := _cluster_activation_amount(activation) * state_scale
		activation.expansion = _cluster_activation_expansion(activation)
		_add_cluster_activation_light(activation.node_index, amount, activation.expansion)
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


func _cluster_activation_amount(activation: ClusterActivation) -> float:
	var lift := activation.strength * _cluster_activation_base_lift
	var bloom := activation.strength * _cluster_activation_bloom_boost
	if activation.age < activation.attack_duration:
		var attack_t := clampf(activation.age / maxf(activation.attack_duration, 0.001), 0.0, 1.0)
		return lift + smoothstep(0.0, 1.0, attack_t) * (activation.strength + bloom - lift)
	if activation.age < activation.hold_duration:
		return activation.strength + bloom
	var fade_duration := maxf(activation.duration - activation.hold_duration, 0.001)
	var fade_t := clampf((activation.age - activation.hold_duration) / fade_duration, 0.0, 1.0)
	return (1.0 - smoothstep(0.0, 1.0, fade_t)) * (activation.strength + bloom)


func _cluster_activation_expansion(activation: ClusterActivation) -> float:
	var expand_t := clampf(activation.age / maxf(activation.attack_duration + 0.34, 0.001), 0.0, 1.0)
	var expanded := smoothstep(0.0, 1.0, expand_t)
	if activation.age <= activation.hold_duration:
		return expanded
	var fade_duration := maxf(activation.duration - activation.hold_duration, 0.001)
	var fade_t := clampf((activation.age - activation.hold_duration) / fade_duration, 0.0, 1.0)
	return expanded * (1.0 - smoothstep(0.0, 1.0, fade_t))


func _add_cluster_activation_light(node_index: int, amount: float, expansion: float) -> void:
	if node_index < 0 or node_index >= _nodes.size() or amount <= 0.0:
		return
	var node := _nodes[node_index]
	var dust_indices: Array = []
	if node_index < _node_dust_indices.size():
		dust_indices = _node_dust_indices[node_index]
	if not node.hub and dust_indices.is_empty():
		return
	_node_speech_light[node_index] = maxf(float(_node_speech_light.get(node_index, 0.0)), amount * _cluster_activation_core_light)

	if not dust_indices.is_empty():
		var limit := mini(dust_indices.size(), int(lerpf(_cluster_activation_dust_min, _cluster_activation_dust_max, expansion)))
		for index in range(limit):
			var dust_index: int = dust_indices[index]
			var dust_falloff := float(index) / maxf(float(limit), 1.0)
			var local_amount := amount * lerpf(1.08, 0.42, dust_falloff)
			_dust_speech_light[dust_index] = maxf(float(_dust_speech_light.get(dust_index, 0.0)), local_amount)

	if node_index < _node_connections.size():
		var connection_indices: Array = _node_connections[node_index]
		var limit := mini(connection_indices.size(), int(lerpf(_cluster_activation_connection_min, _cluster_activation_connection_max, expansion)))
		for index in range(limit):
			var connection_index: int = connection_indices[index]
			var connection: OrganismConnection = _connections[connection_index]
			var connection_falloff := float(index) / maxf(float(limit), 1.0)
			var connection_amount := amount * lerpf(1.05 if connection.route else 0.82, 0.34, connection_falloff)
			_add_connection_region_light(connection_index, connection_amount)


func _add_node_region_light(node_index: int, amount: float, dust_limit_override: int = 28) -> void:
	if node_index < 0 or node_index >= _nodes.size() or amount <= 0.0:
		return
	_node_speech_light[node_index] = maxf(float(_node_speech_light.get(node_index, 0.0)), amount)
	if node_index >= _node_dust_indices.size():
		return
	var dust_indices: Array = _node_dust_indices[node_index]
	var limit := mini(dust_indices.size(), dust_limit_override)
	for index in range(limit):
		var dust_index: int = dust_indices[index]
		var local_amount := amount * lerpf(1.10, 0.34, float(index) / maxf(float(limit), 1.0))
		_dust_speech_light[dust_index] = maxf(float(_dust_speech_light.get(dust_index, 0.0)), local_amount)


func _cluster_lifecycle_birth_amount(event: ClusterLifecycle) -> float:
	var life := clampf(event.age / maxf(event.duration, 0.001), 0.0, 1.0)
	var rise := smoothstep(0.0, 1.0, clampf(life / 0.34, 0.0, 1.0))
	var settle := 1.0 - smoothstep(0.72, 1.0, life) * 0.38
	return event.birth_strength * rise * settle


func _cluster_lifecycle_birth_expansion(event: ClusterLifecycle) -> float:
	var life := clampf(event.age / maxf(event.duration, 0.001), 0.0, 1.0)
	return smoothstep(0.0, 1.0, clampf(life / 0.54, 0.0, 1.0))


func _cluster_lifecycle_death_amount(event: ClusterLifecycle) -> float:
	var life := clampf(event.age / maxf(event.duration, 0.001), 0.0, 1.0)
	return smoothstep(0.10, 0.92, life) * event.death_strength


func _cluster_lifecycle_node_dim(node_index: int) -> float:
	var dim := 0.0
	for event in _cluster_lifecycle_events:
		if event.death_node == node_index:
			dim = maxf(dim, _cluster_lifecycle_death_amount(event))
	return clampf(dim, 0.0, 0.92)


func _cluster_lifecycle_dust_dim(dust: OrganismNode) -> float:
	if dust.source_node < 0:
		return 0.0
	var dim := 0.0
	for event in _cluster_lifecycle_events:
		if event.death_node == dust.source_node:
			dim = maxf(dim, _cluster_lifecycle_death_amount(event))
	return clampf(dim, 0.0, 0.92)


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
	var launch_count := 2 if start_node == _structural_node_count or _rng.randf() < 0.38 else 1
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
	var triggered_activation := _add_cluster_activation(pulse.to_node, pulse.brightness)
	if triggered_activation:
		_queue_energy_branch(pulse.from_node, pulse.to_node, pulse.brightness, pulse.generation, pulse.can_split)
		return
	_continue_energy_branch_from(pulse.from_node, pulse.to_node, pulse.brightness, pulse.generation, pulse.can_split)


func _queue_energy_branch(from_node: int, node_index: int, brightness: float, generation: int, can_split: bool) -> void:
	if _pending_energy_branches.size() >= MAX_PENDING_ENERGY_BRANCHES:
		_pending_energy_branches.remove_at(0)
	var pending := PendingEnergyBranch.new()
	pending.from_node = from_node
	pending.node_index = node_index
	pending.brightness = brightness
	pending.generation = generation
	pending.can_split = can_split
	pending.age = 0.0
	pending.delay = _rng.randf_range(1.00, 1.24)
	_pending_energy_branches.append(pending)


func _continue_energy_branch(pending: PendingEnergyBranch) -> void:
	_continue_energy_branch_from(pending.from_node, pending.node_index, pending.brightness, pending.generation, pending.can_split)


func _continue_energy_branch_from(from_node: int, node_index: int, brightness: float, generation: int, can_split: bool) -> void:
	if current_state != OrganismState.THINKING:
		return
	if generation >= 2 or _pulses.size() >= MAX_PULSES:
		return
	var branch_count := 1
	if generation == 0 and _rng.randf() < _rng.randf_range(0.66, 0.78):
		branch_count = 2
		if _rng.randf() < 0.24:
			branch_count = 3
	elif generation == 1 and can_split and _rng.randf() < 0.72:
		branch_count = 2
	var candidates: Array[int] = _connected_activity_connections(node_index, from_node)
	for branch_index in range(mini(branch_count, candidates.size())):
		if _pulses.size() >= MAX_PULSES:
			return
		var candidate_index: int = candidates[branch_index]
		var connection: OrganismConnection = _connections[candidate_index]
		var next_node: int = connection.b if connection.a == node_index else connection.a
		var next_brightness := brightness * _rng.randf_range(0.72, 0.94)
		if next_brightness < 0.22:
			continue
		var child_can_split := false
		if generation == 0 and branch_count > 1 and branch_index == 0:
			child_can_split = true
		elif generation == 0 and branch_count == 1:
			child_can_split = true
		_add_energy_pulse(candidate_index, node_index, next_node, next_brightness, generation + 1, child_can_split)


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


func _add_cluster_activation(node_index: int, brightness: float) -> bool:
	if node_index < 0 or node_index >= _nodes.size():
		return false
	var cluster_node_index := _cluster_flash_target_for_node(node_index)
	if cluster_node_index < 0:
		return false
	if float(_cluster_activation_cooldowns.get(cluster_node_index, 0.0)) > 0.0:
		return false
	var node := _nodes[cluster_node_index]
	var dust_count := 0
	if cluster_node_index < _node_dust_indices.size():
		dust_count = _node_dust_indices[cluster_node_index].size()
	var strength := brightness * (1.16 if current_state == OrganismState.THINKING else 0.82) * _cluster_activation_strength_scale
	strength *= (1.08 if node.hub else 1.0) + minf(float(dust_count) / 96.0, 1.0) * 0.20
	for activation in _cluster_activations:
		if activation.node_index == cluster_node_index:
			return false
	if _cluster_activations.size() >= MAX_DESTINATION_FLASHES:
		var removed_activation: ClusterActivation = _cluster_activations[0]
		_cluster_activation_cooldowns[removed_activation.node_index] = _cluster_activation_cooldown_seconds
		_cluster_activations.remove_at(0)
	var activation := ClusterActivation.new()
	activation.node_index = cluster_node_index
	activation.age = 0.0
	activation.attack_duration = _randf_between(_cluster_activation_attack_min, _cluster_activation_attack_max)
	activation.hold_duration = _randf_between(_cluster_activation_hold_min, _cluster_activation_hold_max)
	activation.duration = activation.hold_duration + _randf_between(_cluster_activation_fade_min, _cluster_activation_fade_max)
	activation.strength = strength
	activation.expansion = 0.0
	_cluster_activations.append(activation)
	return true


func _cluster_flash_target_for_node(node_index: int) -> int:
	if node_index < 0 or node_index >= _node_cluster_flash_targets.size():
		return -1
	return _node_cluster_flash_targets[node_index]


func _add_energy_pulse(connection_index: int, from_node: int, to_node: int, brightness: float, generation: int, can_split: bool) -> void:
	if _pulses.size() >= MAX_PULSES:
		return
	var pulse := EnergyPulse.new()
	pulse.connection_index = connection_index
	pulse.from_node = from_node
	pulse.to_node = to_node
	pulse.progress = 0.0
	pulse.speed = _rng.randf_range(_energy_pulse_min_speed, _energy_pulse_max_speed) * (1.0 + _activity * 0.18)
	pulse.speed_wobble = _rng.randf_range(0.65, 1.55)
	pulse.speed_phase = _rng.randf_range(0.0, TAU)
	pulse.brightness = brightness
	pulse.generation = generation
	pulse.can_split = can_split
	var speed_t := clampf(inverse_lerp(_energy_pulse_min_speed, _energy_pulse_max_speed, pulse.speed), 0.0, 1.0)
	pulse.tail = _rng.randf_range(lerpf(0.16, 0.24, speed_t), lerpf(0.24, 0.34, speed_t))
	_pulses.append(pulse)


func _update_core_glow() -> void:
	var pulse := 0.5 + sin(_time * 1.05) * 0.5
	var radius := (0.26 + pulse * 0.055 + _activity * 0.070) * _core_glow_radius_scale * _center_visual_size
	var color := Color(_palette["hot"])
	color.a = clampf((0.36 + pulse * 0.14) * _brightness * _core_glow_alpha_scale, 0.0, 0.62)
	_core_multimesh.multimesh.set_instance_transform(0, Transform3D(Basis().scaled(Vector3.ONE * radius), Vector3.ZERO))
	_core_multimesh.multimesh.set_instance_color(0, color)


func _update_cluster_halos() -> void:
	if _cluster_halo_multimesh == null or _cluster_halo_multimesh.multimesh == null:
		return
	var inverse_graph_basis := _graph_root.global_transform.basis.inverse()
	var local_camera_right := inverse_graph_basis * _camera.global_transform.basis.x
	var local_camera_up := inverse_graph_basis * _camera.global_transform.basis.y
	local_camera_right = local_camera_right.normalized()
	local_camera_up = local_camera_up.normalized()
	var local_camera_forward := local_camera_right.cross(local_camera_up).normalized()
	for index in range(MAX_CLUSTER_HALOS):
		if index >= _cluster_halo_node_indices.size():
			_cluster_halo_multimesh.multimesh.set_instance_transform(index, Transform3D(Basis().scaled(Vector3.ZERO), Vector3.ZERO))
			_cluster_halo_multimesh.multimesh.set_instance_color(index, Color(0, 0, 0, 0))
			continue

		var source_node := _cluster_halo_node_indices[index]
		var position := Vector3.ZERO
		var radius := 0.70
		var alpha := 0.06
		if source_node == -1:
			var core_pulse := 0.5 + sin(_time * 0.95) * 0.5
			radius = (1.34 + core_pulse * 0.24 + _activity * 0.24) * _cluster_halo_radius_scale * _center_visual_size
			alpha = clampf((0.20 + core_pulse * 0.075 + _activity * 0.110) * _brightness * _cluster_halo_intensity, 0.0, 0.96)
		elif source_node >= 0 and source_node < _nodes.size():
			var node := _nodes[source_node]
			position = node.current_position
			var center_t := _center_visual_t(position)
			var dust_count := 0
			if source_node < _node_dust_indices.size():
				dust_count = _node_dust_indices[source_node].size()
			var density_t := clampf(float(dust_count) / 140.0, 0.0, 1.0)
			var speech_light := _node_light(source_node)
			var lifecycle_dim := _cluster_lifecycle_node_dim(source_node)
			var living_pulse := pow(maxf(0.0, sin(_time * 0.72 + node.phase)), 3.0)
			var override_halo_scale := _cluster_lab_override(source_node, "halo_scale", 1.0)
			var override_brightness_scale := _cluster_lab_override(source_node, "brightness_scale", 1.0)
			var selection_flash := _cluster_selection_flash_amount(source_node)
			var luminosity_t := clampf(_cluster_luminosity_score(source_node), 0.0, 1.65)
			radius = (0.42 + luminosity_t * 0.86 + density_t * 0.34 + center_t * 0.20 + speech_light * 0.42 + selection_flash * 0.36) * _cluster_halo_radius_scale * override_halo_scale * (1.0 - lifecycle_dim * 0.26)
			alpha = clampf((0.040 + luminosity_t * 0.145 + living_pulse * 0.026 + speech_light * 0.190 + selection_flash * 0.42) * _brightness * _cluster_halo_intensity * override_brightness_scale * (1.0 - lifecycle_dim), 0.0, 1.0)
		else:
			_cluster_halo_multimesh.multimesh.set_instance_transform(index, Transform3D(Basis().scaled(Vector3.ZERO), Vector3.ZERO))
			_cluster_halo_multimesh.multimesh.set_instance_color(index, Color(0, 0, 0, 0))
			continue

		var basis := Basis(local_camera_right * radius, local_camera_up * radius, local_camera_forward * radius)
		var glow_blue := _active_halo_outer_color().lerp(_active_halo_inner_color(), clampf(alpha * 0.34, 0.0, 0.26))
		var selection_flash_render := _cluster_selection_flash_amount(source_node)
		if selection_flash_render > 0.0:
			glow_blue = glow_blue.lerp(Color(1.0, 0.05, 0.04, 1.0), selection_flash_render)
		var color := glow_blue
		color.a = alpha
		_cluster_halo_multimesh.multimesh.set_instance_transform(index, Transform3D(basis, position))
		_cluster_halo_multimesh.multimesh.set_instance_color(index, color)


func _update_materials() -> void:
	_update_material_color(_line_material, _palette["line"], 1.0)
	_update_material_color(_glow_line_material, _palette["line"], 0.10)
	_update_material_color(_pulse_line_material, _palette["hot"], 1.0)
	_update_material_color(_pulse_glow_line_material, _palette["hot"], 0.36)
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
