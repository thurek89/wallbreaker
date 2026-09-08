shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

group_uniforms Appearance;
uniform vec3 albedo : source_color = vec3(0.55, 0.45, 0.32);
uniform float roughness_v : hint_range(0.0, 1.0, 0.01) = 0.85;

group_uniforms Slice;
uniform bool clip_enabled = true;
uniform vec3 clip_center = vec3(5.0, 2.5, 5.0);
uniform vec3 clip_normal = vec3(1.0, 0.0, 1.0);

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	if (clip_enabled) {
		vec3 n = clip_normal;
		float len2 = dot(n, n);
		if (len2 > 1e-8) {
			float side = dot(world_pos - clip_center, n * inversesqrt(len2));
			if (side > 0.0) {
				discard;
			}
		}
	}

	ALBEDO = albedo;
	ROUGHNESS = roughness_v;
	METALLIC = 0.0;
}
