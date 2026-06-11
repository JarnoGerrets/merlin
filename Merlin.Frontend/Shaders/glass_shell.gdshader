shader_type spatial;
render_mode blend_mix, cull_back, depth_prepass_alpha;

uniform float fresnel_power : hint_range(1.0, 8.0) = 4.5;
uniform float opacity : hint_range(0.0, 1.0) = 0.12;
uniform vec4 shell_color : source_color = vec4(0.04, 0.30, 0.95, 1.0);
uniform float grid_density : hint_range(4.0, 48.0) = 22.0;
uniform float grid_opacity : hint_range(0.0, 1.0) = 0.08;

void fragment() {
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), fresnel_power);

	vec2 uv = UV * grid_density;
	float grid = max(step(0.97, fract(uv.x)), step(0.97, fract(uv.y)));
	float combined = clamp(fresnel * opacity + grid * grid_opacity, 0.0, 0.6);

	ALBEDO = shell_color.rgb;
	ALPHA = combined;
	EMISSION = shell_color.rgb * fresnel * 0.3;
	ROUGHNESS = 0.05;
	METALLIC = 0.0;
	SPECULAR = 0.9;
}
