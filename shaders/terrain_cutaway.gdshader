shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec3 albedo : source_color = vec3(0.55, 0.45, 0.32);
uniform float roughness_v = 0.85;
uniform vec3 cut_center = vec3(0.0);
uniform vec3 cut_axis = vec3(0.0, 0.0, -1.0);
uniform float cut_radius = 1.6;
uniform float cut_softness = 0.6;

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
	vec3 axis = cut_axis;
	float axis_len = length(axis);
	float vis = 1.0;
	if (axis_len > 1e-5) {
		axis /= axis_len;
		vec3 rel = world_pos - cut_center;
		float along = dot(rel, axis);
		float r = length(rel - along * axis);
		float inner = max(cut_radius - cut_softness, 0.0);
		vis = along < 0.0 ? smoothstep(inner, cut_radius, r) : 1.0;
	}

	if (vis < bayer4(FRAGCOORD.xy)) {
		discard;
	}

	ALBEDO = albedo;
	ROUGHNESS = roughness_v;
	METALLIC = 0.0;
}
