shader_type spatial;
render_mode blend_add, cull_back, depth_draw_never, unshaded;

uniform float time_scale : hint_range(0.1, 5.0) = 1.2;
uniform float pulse_intensity : hint_range(0.0, 3.0) = 1.0;
uniform vec4 core_color : source_color = vec4(0.0, 0.81, 1.0, 1.0);
uniform vec4 plasma_color : source_color = vec4(0.3, 0.97, 1.0, 1.0);
uniform float noise_scale : hint_range(1.0, 20.0) = 6.0;
uniform float fresnel_power : hint_range(1.0, 8.0) = 3.0;
uniform float vein_sharpness : hint_range(2.0, 20.0) = 8.0;

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
	float d = fbm(NORMAL * noise_scale + vec3(t * 0.3)) * 0.045 * pulse_intensity;
	VERTEX += NORMAL * d;
}

void fragment() {
	float t = TIME * time_scale;
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), fresnel_power);
	float plasma = fbm(NORMAL * noise_scale + vec3(t * 0.5, t * 0.3, t * 0.35));
	plasma = pow(plasma, 1.4);

	float vein_noise = fbm(NORMAL * 14.0 + t * 0.55);
	float veins = abs(sin(vein_noise * 18.0));
	veins = pow(1.0 - veins, vein_sharpness) * 2.5;

	float radial = pow(max(0.0, dot(NORMAL, vec3(0.0, 0.0, 1.0))), 2.0);
	vec3 col = mix(core_color.rgb, plasma_color.rgb, plasma);
	col += veins * vec3(0.7, 0.95, 1.0);
	col += radial * core_color.rgb * 0.5;
	col *= (fresnel * 0.4 + 0.65);

	float alpha = clamp(plasma * 0.7 + fresnel * 0.7 + veins * 0.4 + radial * 0.3, 0.0, 1.0);

	ALBEDO = col * pulse_intensity;
	ALPHA = alpha;
	EMISSION = col * pulse_intensity * 2.0;
}
