group_uniforms Appearance;
uniform vec3 floor_albedo : source_color = vec3(0.40, 0.32, 0.22);
uniform vec3 wall_albedo : source_color = vec3(0.64, 0.56, 0.48);
uniform vec3 ceiling_albedo : source_color = vec3(0.26, 0.23, 0.22);
uniform float roughness_v : hint_range(0.0, 1.0, 0.01) = 0.85;
uniform float slope_start : hint_range(0.0, 1.0, 0.01) = 0.32;
uniform float slope_end : hint_range(0.0, 1.0, 0.01) = 0.75;

group_uniforms FormCues;
uniform float strata_freq : hint_range(0.0, 40.0, 0.1) = 14.0;
uniform float strata_strength : hint_range(0.0, 1.0, 0.01) = 0.28;
uniform float grain_scale : hint_range(0.5, 16.0, 0.1) = 4.0;
uniform float grain_strength : hint_range(0.0, 0.5, 0.01) = 0.08;
uniform vec3 hemi_down : source_color = vec3(0.78, 0.76, 0.74);
uniform vec3 hemi_up : source_color = vec3(1.06, 1.04, 1.00);

#ifdef TERRAIN_PLAYER_HEIGHT_TINT
group_uniforms HeightCue;
uniform float height_tint_start : hint_range(0.0, 4.0, 0.05) = 1.0;
uniform float height_tint_end : hint_range(0.0, 6.0, 0.05) = 2.4;
uniform vec3 height_tint_color : source_color = vec3(0.72, 0.80, 0.94);
uniform float height_tint_strength : hint_range(0.0, 1.0, 0.01) = 0.40;
#endif

float terrain_hash(vec3 p) {
	p = fract(p * 0.1031);
	p += dot(p, p.yzx + 33.33);
	return fract((p.x + p.y) * p.z);
}

float terrain_value_noise(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float n000 = terrain_hash(i);
	float n100 = terrain_hash(i + vec3(1.0, 0.0, 0.0));
	float n010 = terrain_hash(i + vec3(0.0, 1.0, 0.0));
	float n110 = terrain_hash(i + vec3(1.0, 1.0, 0.0));
	float n001 = terrain_hash(i + vec3(0.0, 0.0, 1.0));
	float n101 = terrain_hash(i + vec3(1.0, 0.0, 1.0));
	float n011 = terrain_hash(i + vec3(0.0, 1.0, 1.0));
	float n111 = terrain_hash(i + vec3(1.0, 1.0, 1.0));
	float nx00 = mix(n000, n100, f.x);
	float nx10 = mix(n010, n110, f.x);
	float nx01 = mix(n001, n101, f.x);
	float nx11 = mix(n011, n111, f.x);
	return mix(mix(nx00, nx10, f.y), mix(nx01, nx11, f.y), f.z);
}

vec3 terrain_albedo(vec3 pos, vec3 nrm, float player_floor_y) {
	float ny = nrm.y;
	float lo = min(slope_start, slope_end);
	float hi = max(slope_start, slope_end);
	float floor_w = smoothstep(lo, hi, ny);
	float ceil_w = smoothstep(lo, hi, -ny);
	float wall_w = max(1.0 - floor_w - ceil_w, 0.0);
	vec3 color = floor_albedo * floor_w + wall_albedo * wall_w + ceiling_albedo * ceil_w;

	float y = pos.y;
	float strata = sin(y * strata_freq) * 0.55 + sin(y * strata_freq * 2.35 + 1.7) * 0.30 + sin(y * strata_freq * 0.47) * 0.15;
	strata = strata * 0.5 + 0.5;
	color = mix(color, color * 0.72, wall_w * strata * strata_strength);

	float grain = terrain_value_noise(pos * grain_scale);
	color *= mix(1.0 - grain_strength, 1.0 + grain_strength, grain);

	float hemi = ny * 0.5 + 0.5;
	color *= mix(hemi_down, hemi_up, hemi);

#ifdef TERRAIN_PLAYER_HEIGHT_TINT
	float above = smoothstep(player_floor_y + height_tint_start, player_floor_y + height_tint_end, pos.y);
	color = mix(color, color * height_tint_color, above * height_tint_strength);
#endif

	return color;
}
