shader_type spatial;
render_mode blend_add, cull_disabled, depth_draw_never, unshaded;

uniform vec4 ring_color : source_color = vec4(0.1, 0.55, 1.0, 1.0);
uniform float opacity : hint_range(0.0, 1.0) = 0.75;
uniform float data_speed : hint_range(0.0, 8.0) = 1.0;
uniform float segment_count : hint_range(2.0, 128.0) = 48.0;
uniform float gap_ratio : hint_range(0.05, 0.9) = 0.35;
uniform float brightness_variance : hint_range(0.0, 1.0) = 0.4;

void fragment() {
	float angle = atan(UV.x - 0.5, UV.y - 0.5) / (2.0 * 3.14159265);
	float animated = fract(angle + TIME * data_speed * 0.08);
	float seg = fract(animated * segment_count);
	float dash = step(gap_ratio, seg);

	float seg_id = floor(animated * segment_count);
	float variance = fract(sin(seg_id * 127.1 + TIME * 0.3) * 43758.5);
	float bright = mix(1.0 - brightness_variance, 1.0, variance);
	float rim = 1.0 - pow(abs(UV.y - 0.5) * 2.0, 1.5);
	float alpha = dash * rim * opacity * bright;

	ALBEDO = ring_color.rgb;
	ALPHA = alpha;
	EMISSION = ring_color.rgb * dash * rim * bright * 1.5;
}
