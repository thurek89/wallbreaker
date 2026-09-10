// One albedo and one normal map per TerrainType, in vertex color channel order.
group_uniforms Stone;
uniform sampler2D stone_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D stone_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;

group_uniforms Dirt;
uniform sampler2D dirt_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D dirt_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;

group_uniforms Ice;
uniform sampler2D ice_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D ice_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;

group_uniforms Sand;
uniform sampler2D sand_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D sand_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;

group_uniforms Appearance;
uniform float type_texture_scale : hint_range(0.05, 4.0, 0.01) = 0.5;
uniform float triplanar_sharpness : hint_range(1.0, 8.0, 0.1) = 4.0;
uniform float normal_strength : hint_range(0.0, 3.0, 0.01) = 0.7;
uniform vec4 type_roughness = vec4(0.85, 0.92, 0.25, 0.95);
uniform vec3 floor_tint : source_color = vec3(1.00, 0.98, 0.92);
uniform vec3 wall_tint : source_color = vec3(0.92, 0.90, 0.88);
uniform vec3 ceiling_tint : source_color = vec3(0.62, 0.60, 0.62);
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

/// Vertex colors carry the type mix baked by TypeSampler. Meshes without color
/// data arrive as white, which normalizes to an even blend of all four.
vec4 terrain_type_weights(vec4 vertex_color) {
	vec4 w = max(vertex_color, vec4(0.0));
	return w / max(w.r + w.g + w.b + w.a, 1e-4);
}

float terrain_roughness(vec4 weights) {
	return clamp(dot(weights, type_roughness), 0.0, 1.0);
}

vec3 terrain_triplanar(sampler2D tex, vec3 uvw, vec3 blend) {
	return texture(tex, uvw.zy).rgb * blend.x
		+ texture(tex, uvw.xz).rgb * blend.y
		+ texture(tex, uvw.xy).rgb * blend.z;
}

/// Whiteout blend: the mesh has no tangents, so each plane's tangent space
/// normal is folded into the geometric normal and swizzled back to world
/// orientation before the three planes are mixed.
vec3 terrain_triplanar_normal(sampler2D tex, vec3 uvw, vec3 nrm, vec3 blend) {
	vec3 tx = texture(tex, uvw.zy).rgb * 2.0 - 1.0;
	vec3 ty = texture(tex, uvw.xz).rgb * 2.0 - 1.0;
	vec3 tz = texture(tex, uvw.xy).rgb * 2.0 - 1.0;
	tx.xy *= normal_strength;
	ty.xy *= normal_strength;
	tz.xy *= normal_strength;
	tx = vec3(tx.xy + nrm.zy, abs(tx.z) * nrm.x);
	ty = vec3(ty.xy + nrm.xz, abs(ty.z) * nrm.y);
	tz = vec3(tz.xy + nrm.xy, abs(tz.z) * nrm.z);
	return tx.zyx * blend.x + ty.xzy * blend.y + tz.xyz * blend.z;
}

vec3 terrain_surface(vec3 pos, vec3 nrm, vec4 weights, float player_floor_y, out vec3 shaded_normal) {
	vec3 blend = pow(abs(nrm), vec3(triplanar_sharpness));
	blend /= max(blend.x + blend.y + blend.z, 1e-4);
	vec3 uvw = pos * type_texture_scale;

	vec3 color = terrain_triplanar(stone_albedo, uvw, blend) * weights.r
		+ terrain_triplanar(dirt_albedo, uvw, blend) * weights.g
		+ terrain_triplanar(ice_albedo, uvw, blend) * weights.b
		+ terrain_triplanar(sand_albedo, uvw, blend) * weights.a;

	shaded_normal = normalize(
		terrain_triplanar_normal(stone_normal, uvw, nrm, blend) * weights.r
		+ terrain_triplanar_normal(dirt_normal, uvw, nrm, blend) * weights.g
		+ terrain_triplanar_normal(ice_normal, uvw, nrm, blend) * weights.b
		+ terrain_triplanar_normal(sand_normal, uvw, nrm, blend) * weights.a);

	float ny = nrm.y;
	float lo = min(slope_start, slope_end);
	float hi = max(slope_start, slope_end);
	float floor_w = smoothstep(lo, hi, ny);
	float ceil_w = smoothstep(lo, hi, -ny);
	float wall_w = max(1.0 - floor_w - ceil_w, 0.0);
	color *= floor_tint * floor_w + wall_tint * wall_w + ceiling_tint * ceil_w;

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
