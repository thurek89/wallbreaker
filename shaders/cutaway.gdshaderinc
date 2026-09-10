// Cutaway window carved between the camera and the player. Any material that
// should be removed by it includes this file and scissors on cutaway_keep().
// Parameters are global shader uniforms declared in project.godot and pushed
// once per frame by TerrainWorld, so materials never carry cut state.

global uniform vec3 cut_center;
global uniform vec3 cut_axis;
global uniform float cut_radius;
global uniform float cut_softness;
global uniform float cut_floor_y;
global uniform float cut_clear_radius;
global uniform float cut_clear_softness;

float cutaway_bayer4(vec2 frag) {
	ivec2 p = ivec2(mod(frag, 4.0));
	mat4 m = mat4(
		vec4(0.0, 12.0, 3.0, 15.0),
		vec4(8.0, 4.0, 11.0, 7.0),
		vec4(2.0, 14.0, 1.0, 13.0),
		vec4(10.0, 6.0, 9.0, 5.0)
	);
	return (m[p.x][p.y] + 0.5) / 16.0;
}

void cutaway_shape(vec3 world_pos, out float hole, out float radial, out float clear) {
	vec3 delta = world_pos - cut_center;
	float along = dot(delta, cut_axis);
	float r = length(delta - cut_axis * along);
	float nearer = step(along, 0.0);
	float above_floor = smoothstep(cut_floor_y, cut_floor_y + 0.2, world_pos.y);
	hole = nearer * above_floor;
	radial = smoothstep(max(cut_radius - cut_softness, 0.0), cut_radius, r);
	clear = 1.0 - smoothstep(max(cut_clear_radius - cut_clear_softness, 0.0), cut_clear_radius, r);
}

float cutaway_coverage(vec3 world_pos) {
	float hole, radial, clear;
	cutaway_shape(world_pos, hole, radial, clear);
	return mix(1.0, radial, hole) * (1.0 - clear * hole);
}

float cutaway_keep(vec3 world_pos, vec2 frag_coord) {
	return cutaway_coverage(world_pos) < cutaway_bayer4(frag_coord) ? 0.0 : 1.0;
}

float cutaway_ghost(vec3 world_pos) {
	float hole, radial, clear;
	cutaway_shape(world_pos, hole, radial, clear);
	return hole * (1.0 - clear) * (1.0 - radial);
}
