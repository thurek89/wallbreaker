shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled, ambient_light_disabled;

#include "res://shaders/cutaway.gdshaderinc"

group_uniforms Silhouette;
uniform vec3 silhouette_color : source_color = vec3(0.07, 0.08, 0.11);
uniform float silhouette_alpha : hint_range(0.0, 1.0, 0.01) = 0.48;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float ghost = cutaway_ghost(world_pos);
	if (ghost < 0.01) {
		discard;
	}

	float ndotv = clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0);
	float rim = pow(1.0 - ndotv, 1.6);
	float alpha = silhouette_alpha * ghost * mix(0.82, 1.35, rim);

	ALBEDO = silhouette_color;
	ALPHA = clamp(alpha, 0.0, 1.0);
}
