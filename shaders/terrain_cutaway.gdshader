shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

group_uniforms Appearance;
uniform vec3 albedo : source_color = vec3(0.55, 0.45, 0.32);
uniform float roughness_v : hint_range(0.0, 1.0, 0.01) = 0.85;

group_uniforms Cutaway;
uniform vec3 cut_center = vec3(0.0);
uniform vec3 cut_axis = vec3(0.0, 0.0, -1.0);
uniform float cut_radius : hint_range(0.0, 8.0, 0.05) = 1.6;
uniform float cut_softness : hint_range(0.0, 4.0, 0.05) = 0.6;
uniform float cut_floor_y = 0.0;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float bayer4(vec2 frag) {
	ivec2 p = ivec2(mod(frag, 4.0));
	mat4 m = mat4(
		vec4(0.0, 12.0, 3.0, 15.0),
		vec4(8.0, 4.0, 11.0, 7.0),
		vec4(2.0, 14.0, 1.0, 13.0),
		vec4(10.0, 6.0, 9.0, 5.0)
	);
	return (m[p.x][p.y] + 0.5) / 16.0;
}

void fragment() {
	vec3 frag_view = VERTEX;
	vec3 center_view = (VIEW_MATRIX * vec4(cut_center, 1.0)).xyz;
	float r = length(frag_view.xy - center_view.xy);
	float inner = max(cut_radius - cut_softness, 0.0);
	float radial = smoothstep(inner, cut_radius, r);
	float nearer = step(center_view.z, frag_view.z);
	float above_floor = smoothstep(cut_floor_y, cut_floor_y + 0.2, world_pos.y);
	float vis = mix(1.0, radial, nearer * above_floor);

	if (vis < bayer4(FRAGCOORD.xy)) {
		discard;
	}

	ALBEDO = albedo;
	ROUGHNESS = roughness_v;
	METALLIC = 0.0;
}
