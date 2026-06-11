shader_type spatial;
render_mode blend_add, cull_back, depth_prepass_alpha, unshaded;

uniform vec4 glow_color : source_color = vec4(0.0, 0.42, 1.0, 1.0);
uniform float intensity : hint_range(0.0, 2.0) = 0.6;
uniform float ring_count : hint_range(1.0, 8.0) = 3.0;

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float dist = length(uv);
	float falloff = 1.0 - smoothstep(0.0, 1.0, dist);
	float rings = pow(abs(sin(dist * ring_count * 3.14159)), 8.0) * 0.4;
	float combined = (falloff * 0.6 + rings) * intensity;

	ALBEDO = glow_color.rgb;
	ALPHA = clamp(combined, 0.0, 0.8);
	EMISSION = glow_color.rgb * combined;
}
