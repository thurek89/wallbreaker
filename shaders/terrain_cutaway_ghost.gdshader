shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled, ambient_light_disabled;

group_uniforms Cutaway;
uniform vec3 cut_center = vec3(0.0);
uniform vec3 cut_axis = vec3(0.0, 0.0, -1.0);
uniform float cut_radius : hint_range(0.0, 8.0, 0.05) = 1.6;
uniform float cut_softness : hint_range(0.0, 4.0, 0.05) = 0.6;
uniform float cut_floor_y = 0.0;
uniform float cut_clear_radius : hint_range(0.0, 8.0, 0.05) = 2.0;
uniform float cut_clear_softness : hint_range(0.0, 4.0, 0.05) = 0.5;

group_uniforms Silhouette;
uniform vec3 silhouette_color : source_color = vec3(0.07, 0.08, 0.11);
uniform float silhouette_alpha : hint_range(0.0, 1.0, 0.01) = 0.48;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec3 frag_view = VERTEX;
	vec3 center_view = (VIEW_MATRIX * vec4(cut_center, 1.0)).xyz;
	float r = length(frag_view.xy - center_view.xy);
	float nearer = step(center_view.z, frag_view.z);
	float above_floor = smoothstep(cut_floor_y, cut_floor_y + 0.2, world_pos.y);
	float in_cut = nearer * above_floor;

	float clear_inner = max(cut_clear_radius - cut_clear_softness, 0.0);
	float clear_amount = 1.0 - smoothstep(clear_inner, cut_clear_radius, r);
	float solid_inner = max(cut_radius - cut_softness, 0.0);
	float solid_amount = smoothstep(solid_inner, cut_radius, r);
	float ghost = in_cut * (1.0 - clear_amount) * (1.0 - solid_amount);

	if (ghost < 0.01) {
		discard;
	}

	float ndotv = clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0);
	float rim = pow(1.0 - ndotv, 1.6);
	float alpha = silhouette_alpha * ghost * mix(0.82, 1.35, rim);

	ALBEDO = silhouette_color;
	ALPHA = clamp(alpha, 0.0, 1.0);
}
