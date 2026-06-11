shader_type spatial;
render_mode blend_add, cull_back, depth_prepass_alpha, unshaded;

uniform float time_scale : hint_range(0.1, 5.0) = 1.2;
uniform float pulse_intensity : hint_range(0.0, 3.0) = 1.0;
uniform vec4 core_color : source_color = vec4(0.0, 0.81, 1.0, 1.0);
uniform vec4 plasma_color : source_color = vec4(0.3, 0.97, 1.0, 1.0);
uniform float noise_scale : hint_range(1.0, 20.0) = 6.0;
uniform float fresnel_power : hint_range(1.0, 8.0) = 3.0;
uniform float vein_sharpness : hint_range(2.0, 20.0) = 8.0;
uniform float surface_displacement : hint_range(0.0, 0.18) = 0.035;
uniform float spike_strength : hint_range(0.0, 0.35) = 0.0;
uniform float surface_activity : hint_range(0.0, 3.0) = 0.8;
uniform float speech_drive : hint_range(0.0, 1.0) = 0.0;

float hash(vec3 p) {
	p = fract(p * vec3(443.8975, 397.2973, 491.1871));
	p += dot(p.zxy, p.yxz + 19.19);
	return fract(p.x * p.y * p.z);
}

float vnoise(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(mix(hash(i), hash(i + vec3(1, 0, 0)), f.x),
			mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
		mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x),
			mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y),
		f.z);
}

float fbm(vec3 p) {
	float v = 0.0;
	float a = 0.5;
	mat3 rot = mat3(
		vec3(0.0, 0.8, 0.6),
		vec3(-0.8, 0.36, -0.48),
		vec3(-0.6, -0.48, 0.64)
	);
	for (int i = 0; i < 6; i++) {
		v += a * vnoise(p);
		p = rot * p * 2.0 + vec3(1.7, 9.2, 3.5);
		a *= 0.5;
	}
	return v;
}

void vertex() {
	float t = TIME * time_scale;
	float soft_motion = fbm(NORMAL * noise_scale + vec3(t * 0.3)) * surface_displacement * pulse_intensity;
	float angle = atan(NORMAL.z, NORMAL.x);
	float latitude = asin(clamp(NORMAL.y, -1.0, 1.0));
	float angular_noise = fbm(vec3(cos(angle) * 3.7, sin(angle) * 3.7, t * 0.85));
	float angular_a = pow(max(0.0, sin(angle * 18.0 + t * 5.5 + angular_noise * 2.4)), 3.0);
	float angular_b = pow(max(0.0, sin(angle * 31.0 - t * 7.1 + angular_noise * 3.1)), 4.0);
	float angular_c = pow(max(0.0, sin(angle * 47.0 + t * 9.3 - angular_noise * 1.7)), 5.0);
	float latitude_variation = 0.72 + 0.28 * pow(abs(cos(latitude * 2.0)), 0.7);
	float speech_mask = clamp(max(max(angular_a, angular_b * 0.82), angular_c * 0.66) * latitude_variation, 0.0, 1.0);
	float spikes = speech_mask * spike_strength * (0.75 + speech_drive * 0.25);
	float d = soft_motion + spikes;
	VERTEX += NORMAL * d;
}

void fragment() {
	float t = TIME * time_scale;
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), fresnel_power);
	float plasma = fbm(NORMAL * noise_scale + vec3(t * 0.5, t * 0.3, t * 0.35) * surface_activity);
	plasma = pow(plasma, 1.4);

	float angle = atan(NORMAL.z, NORMAL.x);
	float vein_noise = fbm(NORMAL * (12.0 + surface_activity * 3.0) + vec3(t * (0.45 + surface_activity * 0.18)));
	float veins = abs(sin(vein_noise * 18.0));
	veins = pow(1.0 - veins, vein_sharpness) * (2.0 + speech_drive * 1.2);
	float edge_activity_a = pow(max(0.0, sin(angle * 22.0 + t * 6.7 + vein_noise * 2.0)), 4.0);
	float edge_activity_b = pow(max(0.0, sin(angle * 37.0 - t * 8.4 - vein_noise * 1.5)), 5.0);
	float edge_energy = max(edge_activity_a, edge_activity_b * 0.8) * speech_drive;

	float radial = pow(max(0.0, dot(NORMAL, vec3(0.0, 0.0, 1.0))), 2.0);
	vec3 col = mix(core_color.rgb, plasma_color.rgb, plasma);
	col += veins * vec3(0.7, 0.95, 1.0);
	col += edge_energy * plasma_color.rgb * 0.45;
	col += radial * core_color.rgb * 0.5;
	col *= (fresnel * 0.4 + 0.65);

	float alpha = clamp(plasma * 0.7 + fresnel * 0.7 + veins * 0.4 + radial * 0.3 + edge_energy * 0.2, 0.0, 1.0);

	ALBEDO = col * 0.6;
	ALPHA = alpha;
	EMISSION = col * pulse_intensity * (1.25 + speech_drive * 0.45);
}
