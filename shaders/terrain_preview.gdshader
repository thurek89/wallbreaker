shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

#include "res://shaders/terrain_shading.gdshaderinc"

group_uniforms Slice;
uniform bool clip_enabled = true;
uniform vec3 clip_center = vec3(5.0, 2.5, 5.0);
uniform vec3 clip_normal = vec3(1.0, 0.0, 1.0);

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = MODEL_NORMAL_MATRIX * NORMAL;
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

	ALBEDO = terrain_albedo(world_pos, normalize(world_normal), 0.0);
	ROUGHNESS = roughness_v;
	METALLIC = 0.0;
}
