shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

#define TERRAIN_PLAYER_HEIGHT_TINT
#include "res://shaders/terrain_shading.gdshaderinc"
#include "res://shaders/cutaway.gdshaderinc"

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = MODEL_NORMAL_MATRIX * NORMAL;
}

void fragment() {
	ALPHA = cutaway_keep(world_pos, FRAGCOORD.xy);
	ALPHA_SCISSOR_THRESHOLD = 0.5;

	vec4 weights = terrain_type_weights(COLOR);
	vec3 shaded_normal;
	ALBEDO = terrain_surface(world_pos, normalize(world_normal), weights, cut_floor_y, shaded_normal);
	NORMAL = normalize((VIEW_MATRIX * vec4(shaded_normal, 0.0)).xyz);
	ROUGHNESS = terrain_roughness(weights);
	METALLIC = 0.0;
}
