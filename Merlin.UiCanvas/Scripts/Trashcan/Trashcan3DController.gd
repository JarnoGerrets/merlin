extends Node3D
class_name Trashcan3DController

const CYAN := Color(0.18, 0.86, 1.0, 1.0)
const HOT := Color(0.84, 0.98, 1.0, 1.0)
const BLUE := Color(0.05, 0.36, 1.0, 1.0)

var armed_strength: float:
	get:
		return _armed_strength
	set(value):
		_armed_strength = clampf(value, 0.0, 1.0)
		_update_state_visuals()
var lid_open_progress: float:
	get:
		return _lid_open_progress
	set(value):
		_lid_open_progress = clampf(value, 0.0, 1.0)
		_update_state_visuals()
var consume_flash: float:
	get:
		return _consume_flash
	set(value):
		_consume_flash = clampf(value, 0.0, 1.0)
		_update_state_visuals()

var _armed_strength := 0.0
var _lid_open_progress := 0.0
var _consume_flash := 0.0
var _time := 0.0
var _body_material: StandardMaterial3D
var _rim_material: StandardMaterial3D
var _rib_material: StandardMaterial3D
var _dim_material: StandardMaterial3D
var _glow_material: StandardMaterial3D
var _base_material: StandardMaterial3D
var _lid_pivot: Node3D
var _inner_glow: MeshInstance3D
var _base_rings: Array[MeshInstance3D] = []
var _rim_nodes: Array[MeshInstance3D] = []
var _rib_nodes: Array[MeshInstance3D] = []


func _ready() -> void:
	_build_materials()
	_build_trashcan()
	set_process(true)


func _process(delta: float) -> void:
	_time += delta
	rotation_degrees.y = sin(_time * 0.55) * 2.0
	consume_flash = maxf(consume_flash - delta * 3.4, 0.0)
	_update_state_visuals()


func _build_materials() -> void:
	_body_material = _hologram_material(Color(0.06, 0.46, 0.85, 0.22), 0.75, BaseMaterial3D.BLEND_MODE_ADD)
	_rim_material = _hologram_material(Color(CYAN.r, CYAN.g, CYAN.b, 0.72), 1.45, BaseMaterial3D.BLEND_MODE_ADD)
	_rib_material = _hologram_material(Color(CYAN.r, CYAN.g, CYAN.b, 0.52), 1.10, BaseMaterial3D.BLEND_MODE_ADD)
	_dim_material = _hologram_material(Color(0.07, 0.38, 0.78, 0.18), 0.38, BaseMaterial3D.BLEND_MODE_ADD)
	_glow_material = _hologram_material(Color(0.34, 0.92, 1.0, 0.28), 1.20, BaseMaterial3D.BLEND_MODE_ADD)
	_base_material = _hologram_material(Color(0.16, 0.78, 1.0, 0.42), 0.95, BaseMaterial3D.BLEND_MODE_ADD)


func _build_trashcan() -> void:
	_add_body()
	_add_rims()
	_add_ribs()
	_add_lid()
	_add_inner_glow()
	_add_base_projection()
	_update_state_visuals()


func _add_body() -> void:
	var body := MeshInstance3D.new()
	body.name = "BodyMesh"
	body.mesh = _frustum_mesh(0.82, 0.62, 0.39, 0.30, 1.90, 80)
	body.material_override = _body_material
	body.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(body)

	var rear_grid := MeshInstance3D.new()
	rear_grid.name = "FaintRearConstructionLines"
	rear_grid.mesh = _body_grid_mesh(0.78, 0.58, 0.37, 0.28, 1.88)
	rear_grid.material_override = _dim_material
	rear_grid.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(rear_grid)


func _add_rims() -> void:
	_add_ellipse_tube("TopRimMesh", Vector3(0, 0.95, 0), 0.86, 0.41, 0.035, _rim_material, _rim_nodes)
	_add_ellipse_tube("TopRimOuterGlow", Vector3(0, 0.95, 0), 0.91, 0.45, 0.020, _glow_material, _rim_nodes)
	_add_ellipse_tube("BottomRimMesh", Vector3(0, -0.95, 0), 0.64, 0.31, 0.032, _rim_material, _rim_nodes)
	_add_ellipse_tube("BottomRimInnerGlow", Vector3(0, -0.93, 0), 0.48, 0.22, 0.014, _glow_material, _rim_nodes)


func _add_ribs() -> void:
	var rib_count := 5
	for i in range(rib_count):
		var t := float(i) / float(rib_count - 1)
		var x_top := lerpf(-0.50, 0.50, t)
		var x_bottom := lerpf(-0.42, 0.42, t)
		var fade := clampf(1.0 - absf(t - 0.5) * 0.35, 0.70, 1.0)
		var points := _rib_capsule_points(Vector2(x_top, 0.52), Vector2(x_bottom, -0.62), 0.14, 18)
		var rib := MeshInstance3D.new()
		rib.name = "PillRibMesh%d" % i
		rib.mesh = _front_surface_tube(points, 0.013 + fade * 0.004, 10)
		rib.material_override = _rib_material
		rib.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(rib)
		_rib_nodes.append(rib)


func _add_lid() -> void:
	_lid_pivot = Node3D.new()
	_lid_pivot.name = "LidPivot"
	_lid_pivot.position = Vector3(0.0, 1.00, 0.0)
	add_child(_lid_pivot)

	var lid := MeshInstance3D.new()
	lid.name = "LidMesh"
	lid.position = Vector3(0, 0.045, 0)
	lid.mesh = _flattened_disc_mesh(0.98, 0.48, 0.055, 80)
	lid.material_override = _body_material
	lid.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_lid_pivot.add_child(lid)

	_add_ellipse_tube("LidRimMesh", Vector3(0, 0.085, 0), 0.98, 0.48, 0.035, _rim_material, _rim_nodes, _lid_pivot)
	_add_ellipse_tube("LidInnerLine", Vector3(0, 0.115, 0), 0.78, 0.34, 0.012, _dim_material, _rim_nodes, _lid_pivot)

	var handle_points := PackedVector3Array([
		Vector3(-0.27, 0.16, -0.07),
		Vector3(-0.23, 0.31, -0.09),
		Vector3(0.23, 0.31, -0.09),
		Vector3(0.27, 0.16, -0.07)
	])
	var handle := MeshInstance3D.new()
	handle.name = "HandleMesh"
	handle.mesh = _tube_along_points(handle_points, 0.027, 10)
	handle.material_override = _rim_material
	handle.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_lid_pivot.add_child(handle)


func _add_inner_glow() -> void:
	_inner_glow = MeshInstance3D.new()
	_inner_glow.name = "InnerGlow"
	var mesh := SphereMesh.new()
	mesh.radius = 0.52
	mesh.height = 0.84
	mesh.radial_segments = 32
	mesh.rings = 12
	_inner_glow.mesh = mesh
	_inner_glow.position = Vector3(0, -0.42, 0.04)
	_inner_glow.scale = Vector3(0.80, 1.25, 0.55)
	_inner_glow.material_override = _glow_material
	_inner_glow.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(_inner_glow)


func _add_base_projection() -> void:
	for i in range(6):
		var t := float(i) / 5.0
		var ring_list: Array[MeshInstance3D] = _base_rings
		_add_ellipse_tube("BaseProjectionRing%d" % i, Vector3(0, -1.10, 0), 0.62 + t * 0.80, 0.30 + t * 0.38, 0.006 + t * 0.002, _base_material, ring_list)


func _update_state_visuals() -> void:
	if not is_inside_tree():
		return
	var state_boost := _armed_strength + _consume_flash * 0.85
	if _lid_pivot != null:
		_lid_pivot.position = Vector3(0.0, 1.00, 0.0).lerp(Vector3(0.18, 1.10, 0.04), _lid_open_progress)
		_lid_pivot.rotation_degrees.x = lerpf(0.0, -58.0, _lid_open_progress)
		_lid_pivot.rotation_degrees.z = lerpf(0.0, -7.0, _lid_open_progress)
	if _inner_glow != null:
		_inner_glow.scale = Vector3(0.80, 1.25 + state_boost * 0.55, 0.55)
		_glow_material.albedo_color = Color(0.34, 0.92, 1.0, 0.16 + _armed_strength * 0.22 + _consume_flash * 0.42)
		_glow_material.emission_energy_multiplier = 1.0 + state_boost * 1.7
	_rim_material.emission_energy_multiplier = 1.45 + state_boost * 0.75
	_rib_material.emission_energy_multiplier = 1.05 + state_boost * 0.60
	_base_material.emission_energy_multiplier = 0.80 + state_boost * 1.0
	var pulse := sin(_time * 2.2) * 0.5 + 0.5
	for ring in _base_rings:
		ring.scale = Vector3.ONE * (1.0 + _armed_strength * 0.05 + pulse * 0.010)


func _hologram_material(color: Color, emission: float, blend_mode: int) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.blend_mode = blend_mode
	material.no_depth_test = false
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	material.albedo_color = color
	material.emission_enabled = true
	material.emission = Color(color.r, color.g, color.b, 1.0)
	material.emission_energy_multiplier = emission
	return material


func _frustum_mesh(top_rx: float, bottom_rx: float, top_rz: float, bottom_rz: float, height: float, segments: int) -> ArrayMesh:
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var indices := PackedInt32Array()
	var top_y := height * 0.5
	var bottom_y := -height * 0.5
	for i in range(segments):
		var a := TAU * float(i) / float(segments)
		vertices.append(Vector3(cos(a) * top_rx, top_y, sin(a) * top_rz))
		vertices.append(Vector3(cos(a) * bottom_rx, bottom_y, sin(a) * bottom_rz))
		var n := Vector3(cos(a), 0.18, sin(a)).normalized()
		normals.append(n)
		normals.append(n)
	for i in range(segments):
		var n := (i + 1) % segments
		_append_triangle(indices, i * 2, n * 2, i * 2 + 1)
		_append_triangle(indices, n * 2, n * 2 + 1, i * 2 + 1)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh


func _body_grid_mesh(top_rx: float, bottom_rx: float, top_rz: float, bottom_rz: float, height: float) -> ArrayMesh:
	var paths: Array[PackedVector3Array] = []
	for i in range(12):
		var a := TAU * float(i) / 12.0
		var pts := PackedVector3Array()
		for j in range(7):
			var t := float(j) / 6.0
			var y := lerpf(height * 0.5, -height * 0.5, t)
			var rx := lerpf(top_rx, bottom_rx, t)
			var rz := lerpf(top_rz, bottom_rz, t)
			pts.append(Vector3(cos(a) * rx, y, sin(a) * rz))
		paths.append(pts)
	for j in range(4):
		var t := float(j + 1) / 5.0
		var y := lerpf(height * 0.5, -height * 0.5, t)
		var rx := lerpf(top_rx, bottom_rx, t)
		var rz := lerpf(top_rz, bottom_rz, t)
		var pts := PackedVector3Array()
		for i in range(49):
			var a := TAU * float(i) / 48.0
			pts.append(Vector3(cos(a) * rx, y, sin(a) * rz))
		paths.append(pts)
	return _multi_polyline_tube(paths, 0.004, 5)


func _flattened_disc_mesh(rx: float, rz: float, thickness: float, segments: int) -> ArrayMesh:
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	var vertices := PackedVector3Array()
	var indices := PackedInt32Array()
	vertices.append(Vector3.ZERO)
	for i in range(segments):
		var a := TAU * float(i) / float(segments)
		vertices.append(Vector3(cos(a) * rx, 0, sin(a) * rz))
	for i in range(segments):
		var n := 1 + ((i + 1) % segments)
		_append_triangle(indices, 0, i + 1, n)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh


func _add_ellipse_tube(node_name: String, center: Vector3, rx: float, rz: float, tube_radius: float, material: Material, list: Array[MeshInstance3D], parent: Node = null) -> void:
	var target_parent: Node = self if parent == null else parent
	var pts := PackedVector3Array()
	for i in range(97):
		var a := TAU * float(i) / 96.0
		pts.append(center + Vector3(cos(a) * rx, 0.0, sin(a) * rz))
	var mesh_instance := MeshInstance3D.new()
	mesh_instance.name = node_name
	mesh_instance.mesh = _tube_along_points(pts, tube_radius, 8)
	mesh_instance.material_override = material
	mesh_instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	target_parent.add_child(mesh_instance)
	list.append(mesh_instance)


func _rib_capsule_points(top: Vector2, bottom: Vector2, width: float, segments_per_cap: int) -> PackedVector2Array:
	var radius := width * 0.5
	var points := PackedVector2Array()
	var top_center := Vector2(top.x, top.y - radius)
	var bottom_center := Vector2(bottom.x, bottom.y + radius)
	for i in range(segments_per_cap + 1):
		var a := PI - PI * float(i) / float(segments_per_cap)
		points.append(top_center + Vector2(cos(a) * radius, sin(a) * radius))
	for i in range(segments_per_cap + 1):
		var a := -PI * float(i) / float(segments_per_cap)
		points.append(bottom_center + Vector2(cos(a) * radius, sin(a) * radius))
	points.append(points[0])
	return points


func _front_surface_tube(points_2d: PackedVector2Array, radius: float, sides: int) -> ArrayMesh:
	var pts := PackedVector3Array()
	for p in points_2d:
		var y := p.y
		var t := clampf((0.95 - y) / 1.90, 0.0, 1.0)
		var rx := lerpf(0.82, 0.62, t)
		var rz := lerpf(0.39, 0.30, t)
		var x := clampf(p.x, -rx * 0.92, rx * 0.92)
		var z := sqrt(maxf(0.0, 1.0 - pow(x / maxf(rx, 0.01), 2.0))) * rz + 0.020
		pts.append(Vector3(x, y, z))
	return _tube_along_points(pts, radius, sides)


func _multi_polyline_tube(paths: Array[PackedVector3Array], radius: float, sides: int) -> ArrayMesh:
	var combined := ArrayMesh.new()
	for path in paths:
		var mesh := _tube_along_points(path, radius, sides)
		for surface in range(mesh.get_surface_count()):
			var arrays := mesh.surface_get_arrays(surface)
			combined.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return combined


func _tube_along_points(points: PackedVector3Array, radius: float, sides: int) -> ArrayMesh:
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var indices := PackedInt32Array()
	if points.size() < 2:
		return ArrayMesh.new()
	for i in range(points.size()):
		var tangent: Vector3
		if i == 0:
			tangent = (points[1] - points[0]).normalized()
		elif i == points.size() - 1:
			tangent = (points[i] - points[i - 1]).normalized()
		else:
			tangent = (points[i + 1] - points[i - 1]).normalized()
		var up := Vector3.UP
		if absf(tangent.dot(up)) > 0.92:
			up = Vector3.RIGHT
		var normal := tangent.cross(up).normalized()
		var binormal := tangent.cross(normal).normalized()
		for side in range(sides):
			var a := TAU * float(side) / float(sides)
			var n := (normal * cos(a) + binormal * sin(a)).normalized()
			vertices.append(points[i] + n * radius)
			normals.append(n)
	for i in range(points.size() - 1):
		for side in range(sides):
			var next_side := (side + 1) % sides
			var a0 := i * sides + side
			var a1 := i * sides + next_side
			var b0 := (i + 1) * sides + side
			var b1 := (i + 1) * sides + next_side
			_append_triangle(indices, a0, b0, a1)
			_append_triangle(indices, a1, b0, b1)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh


func _append_triangle(indices: PackedInt32Array, a: int, b: int, c: int) -> void:
	indices.append(a)
	indices.append(b)
	indices.append(c)
