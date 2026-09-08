using Godot;

namespace Wallbreaker;

public partial class TerrainWorld : Node3D
{
    [Export] public float ChunkSize { get; set; } = 10f;
    [Export] public int CellsXz { get; set; } = 64;
    [Export] public int CellsY { get; set; } = 32;
    [Export] public TerrainNoise? Noise { get; set; }
    [Export] public float SpawnClearRadius { get; set; } = 2.0f;
    [Export] public ShaderMaterial? TerrainMaterial { get; set; }
    [Export] public int LoadRadius { get; set; } = 1;
    [Export] public int UnloadRadius { get; set; } = 2;
    [Export] public int MaxChunkBuildsPerFrame { get; set; } = 2;
    [Export] public float EdgePreload { get; set; } = 3.5f;

    private readonly Dictionary<(int X, int Z), TerrainChunk> _chunks = [];
    private readonly Dictionary<(int X, int Z), ChunkVolume> _cache = [];
    private readonly List<(int X, int Z)> _unloadScratch = [];
    private SdfSampler _sampler = null!;
    private ShaderMaterial _material = null!;
    private Aabb _bounds;
    private Node3D? _player;
    private Camera3D? _camera;
    private StaticBody3D _walkFloor = null!;
    private MeshInstance3D _ground = null!;
    private BoxShape3D _walkFloorShape = null!;
    private PlaneMesh _groundMesh = null!;

    public override void _Ready()
    {
        _sampler = new SdfSampler(ResolveNoise());
        _material = ResolveTerrainMaterial();
        _bounds = new Aabb(
            new Vector3(0f, _sampler.BandMinY, 0f),
            new Vector3(ChunkSize, _sampler.CrustHeight, ChunkSize));
        AddWalkFloor();

        Node? world = GetParent();
        _player = world?.GetNodeOrNull<Node3D>("Player");
        _camera = world?.GetNodeOrNull<Camera3D>("CameraRig/Camera3D");

        EnsureChunk(0, 0);
        ClearSpawn(GetSpawnPoint(), SpawnClearRadius);
        StreamChunks();
    }

    public override void _Process(double _delta)
    {
        UpdateCutaway();
    }

    public override void _PhysicsProcess(double _delta)
    {
        StreamChunks();
    }

    public float FloorTop => _sampler?.FloorTop ?? ResolveNoise().BandMinY + ResolveNoise().FloorThickness;

    public Vector3 GetSpawnPoint()
    {
        return new Vector3(ChunkSize * 0.5f, FloorTop, ChunkSize * 0.5f);
    }

    public void ClearSpawn(Vector3 worldPoint, float radius)
    {
        float spacing = Mathf.Max(radius * 0.85f, 0.25f);
        float y1 = _sampler.BandMaxY + radius;
        for (float y = FloorTop; y <= y1; y += spacing)
        {
            Carve(new Vector3(worldPoint.X, y, worldPoint.Z), radius);
        }

        CommitCarve();
    }

    public Aabb GetBounds()
    {
        return _bounds;
    }

    public void Carve(Vector3 worldPoint, float radius)
    {
        int x0 = Mathf.FloorToInt((worldPoint.X - radius) / ChunkSize) - 1;
        int x1 = Mathf.FloorToInt((worldPoint.X + radius) / ChunkSize) + 1;
        int z0 = Mathf.FloorToInt((worldPoint.Z - radius) / ChunkSize) - 1;
        int z1 = Mathf.FloorToInt((worldPoint.Z + radius) / ChunkSize) + 1;
        for (int iz = z0; iz <= z1; iz++)
        {
            for (int ix = x0; ix <= x1; ix++)
            {
                CarveChunk(ix, iz, worldPoint, radius);
            }
        }
    }

    public void CommitCarve()
    {
        foreach (TerrainChunk chunk in _chunks.Values)
        {
            chunk.RemeshDirty();
        }
    }

    public bool Raycast(Vector3 origin, Vector3 dir, float maxDistance, out Vector3 hit, out Vector3 normal)
    {
        hit = default;
        normal = Vector3.Up;
        float best = maxDistance;
        bool found = false;
        foreach (TerrainChunk chunk in _chunks.Values)
        {
            if (chunk.Raycast(origin, dir, best, out Vector3 candidate, out Vector3 n, out float t) && t < best)
            {
                best = t;
                hit = candidate;
                normal = n;
                found = true;
            }
        }

        return found;
    }

    public bool RaycastFromView(Vector3 origin, Vector3 dir, float maxDistance, float headY, out Vector3 hit, out Vector3 normal)
    {
        dir = dir.Normalized();
        Vector3 start = origin;
        float remain = maxDistance;
        if (Mathf.Abs(dir.Y) > 1e-5f)
        {
            float t0 = (headY - origin.Y) / dir.Y;
            if (t0 > 0f)
            {
                start = origin + dir * t0;
                remain = Mathf.Max(0f, maxDistance - t0);
            }
        }

        return Raycast(start, dir, remain, out hit, out normal);
    }

    public void EnsureChunk(int ix, int iz)
    {
        var key = (ix, iz);
        if (_chunks.ContainsKey(key))
        {
            return;
        }

        _cache.Remove(key, out ChunkVolume? restored);
        var chunk = new TerrainChunk
        {
            Name = $"Chunk_{ix}_{iz}"
        };
        AddChild(chunk);
        chunk.Build(ix, iz, ChunkSize, CellsXz, CellsY, _sampler, _material, restored);
        StitchWithNeighbors(chunk);
        chunk.RebuildMeshes();
        _chunks[key] = chunk;
        RebuildBounds();
    }

    private void StreamChunks()
    {
        Vector3 focus = _player?.GlobalPosition ?? GetSpawnPoint();
        GetLoadRange(focus, out int minX, out int maxX, out int minZ, out int maxZ);

        int budget = Mathf.Max(1, MaxChunkBuildsPerFrame);
        for (int iz = minZ; iz <= maxZ && budget > 0; iz++)
        {
            for (int ix = minX; ix <= maxX && budget > 0; ix++)
            {
                if (_chunks.ContainsKey((ix, iz)))
                {
                    continue;
                }

                EnsureChunk(ix, iz);
                budget--;
            }
        }

        int cx = Mathf.FloorToInt(focus.X / ChunkSize);
        int cz = Mathf.FloorToInt(focus.Z / ChunkSize);
        int keep = Mathf.Max(UnloadRadius, LoadRadius);
        _unloadScratch.Clear();
        foreach ((int X, int Z) key in _chunks.Keys)
        {
            bool wanted = key.X >= minX && key.X <= maxX && key.Z >= minZ && key.Z <= maxZ;
            bool near = Mathf.Abs(key.X - cx) <= keep && Mathf.Abs(key.Z - cz) <= keep;
            if (!wanted && !near)
            {
                _unloadScratch.Add(key);
            }
        }

        foreach ((int X, int Z) key in _unloadScratch)
        {
            UnloadChunk(key);
        }

        if (_unloadScratch.Count > 0)
        {
            RebuildBounds();
        }

        UpdateFollowFloor(focus);
    }

    private void UnloadChunk((int X, int Z) key)
    {
        if (!_chunks.Remove(key, out TerrainChunk? chunk))
        {
            return;
        }

        _cache[key] = chunk.TakeVolume();
        RemoveChild(chunk);
        chunk.Free();
    }

    private void StitchWithNeighbors(TerrainChunk chunk)
    {
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0)
                {
                    continue;
                }

                var key = (chunk.ChunkX + dx, chunk.ChunkZ + dz);
                if (_chunks.TryGetValue(key, out TerrainChunk? neighbor))
                {
                    chunk.StitchFrom(neighbor.Volume);
                    if (neighbor.StitchFrom(chunk.Volume))
                    {
                        neighbor.RebuildMeshes();
                    }
                }
                else if (_cache.TryGetValue(key, out ChunkVolume? cached))
                {
                    chunk.StitchFrom(cached);
                    cached.CopyOverlappingFrom(chunk.Volume);
                }
            }
        }
    }

    private void CarveChunk(int ix, int iz, Vector3 worldPoint, float radius)
    {
        var key = (ix, iz);
        if (_chunks.TryGetValue(key, out TerrainChunk? chunk))
        {
            if (chunk.OverlapsBrush(worldPoint, radius))
            {
                chunk.SubtractSphere(worldPoint, radius);
            }

            return;
        }

        if (_cache.TryGetValue(key, out ChunkVolume? cached))
        {
            if (cached.OverlapsBrush(worldPoint, radius))
            {
                cached.SubtractSphere(worldPoint, radius, _sampler);
            }

            return;
        }

        if (!ChunkWouldOverlapBrush(ix, iz, worldPoint, radius))
        {
            return;
        }

        ChunkVolume volume = ChunkVolume.CreateFilled(ix, iz, ChunkSize, CellsXz, CellsY, _sampler);
        volume.SubtractSphere(worldPoint, radius, _sampler);
        _cache[key] = volume;
    }

    private bool ChunkWouldOverlapBrush(int ix, int iz, Vector3 center, float radius)
    {
        float voxel = ChunkSize / CellsXz;
        Vector3 origin = new(ix * ChunkSize, _sampler.BandMinY, iz * ChunkSize);
        var bounds = new Aabb(origin, new Vector3(ChunkSize, CellsY * voxel, ChunkSize));
        var brush = new Aabb(
            center - new Vector3(radius, radius, radius),
            new Vector3(radius * 2f, radius * 2f, radius * 2f));
        return bounds.Grow(voxel * (ChunkVolume.Skirt + 1)).Intersects(brush);
    }

    private void GetLoadRange(Vector3 focus, out int minX, out int maxX, out int minZ, out int maxZ)
    {
        int radius = Mathf.Max(1, LoadRadius);
        float preload = Mathf.Clamp(EdgePreload, 0f, ChunkSize);
        minX = Mathf.FloorToInt((focus.X - preload) / ChunkSize) - radius;
        maxX = Mathf.FloorToInt((focus.X + preload) / ChunkSize) + radius;
        minZ = Mathf.FloorToInt((focus.Z - preload) / ChunkSize) - radius;
        maxZ = Mathf.FloorToInt((focus.Z + preload) / ChunkSize) + radius;
    }

    private void RebuildBounds()
    {
        bool first = true;
        Aabb merged = default;
        foreach (TerrainChunk chunk in _chunks.Values)
        {
            if (first)
            {
                merged = chunk.Bounds;
                first = false;
            }
            else
            {
                merged = merged.Merge(chunk.Bounds);
            }
        }

        _bounds = first
            ? new Aabb(
                new Vector3(0f, _sampler.BandMinY, 0f),
                new Vector3(ChunkSize, _sampler.CrustHeight, ChunkSize))
            : merged;
    }

    private TerrainNoise ResolveNoise()
    {
        if (GodotObject.IsInstanceValid(Noise))
        {
            return Noise!;
        }

        TerrainNoise? loaded = GD.Load<TerrainNoise>("res://terrain/default_noise.tres");
        return loaded ?? new TerrainNoise();
    }

    private ShaderMaterial ResolveTerrainMaterial()
    {
        ShaderMaterial material = TerrainMaterial ?? new ShaderMaterial();
        if (material.Shader == null)
        {
            material.Shader = GD.Load<Shader>("res://shaders/terrain_cutaway.gdshader");
        }

        if (material.NextPass is not ShaderMaterial)
        {
            material.NextPass = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://shaders/terrain_cutaway_ghost.gdshader")
            };
        }

        return material;
    }

    private void UpdateCutaway()
    {
        if (_player == null || _camera == null)
        {
            return;
        }

        Vector3 feet = _player.GlobalPosition;
        Vector3 center = feet + new Vector3(0f, 0.8f, 0f);
        Vector3 axis = -_camera.GlobalBasis.Z;
        if (axis.LengthSquared() < 1e-8f)
        {
            return;
        }

        ApplyCutaway(_material, center, axis.Normalized(), feet.Y);
        if (_material.NextPass is ShaderMaterial ghost)
        {
            ApplyCutaway(ghost, center, axis.Normalized(), feet.Y);
            ghost.SetShaderParameter("cut_radius", _material.GetShaderParameter("cut_radius"));
            ghost.SetShaderParameter("cut_softness", _material.GetShaderParameter("cut_softness"));
        }
    }

    private static void ApplyCutaway(ShaderMaterial material, Vector3 center, Vector3 axis, float floorY)
    {
        material.SetShaderParameter("cut_center", center);
        material.SetShaderParameter("cut_axis", axis);
        material.SetShaderParameter("cut_floor_y", floorY);
    }

    private void AddWalkFloor()
    {
        float floor = _sampler.FloorThickness;
        float span = FollowFloorSpan();
        _walkFloorShape = new BoxShape3D { Size = new Vector3(span, floor, span) };
        _walkFloor = new StaticBody3D { Name = "WalkFloor" };
        _walkFloor.AddChild(new CollisionShape3D { Shape = _walkFloorShape });
        AddChild(_walkFloor);

        _groundMesh = new PlaneMesh { Size = new Vector2(span, span) };
        _ground = new MeshInstance3D
        {
            Name = "Ground",
            Mesh = _groundMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.22f, 0.24f, 0.21f)
            }
        };
        AddChild(_ground);
        UpdateFollowFloor(GetSpawnPoint());
    }

    private void UpdateFollowFloor(Vector3 focus)
    {
        float y0 = _sampler.BandMinY;
        float floor = _sampler.FloorThickness;
        float span = FollowFloorSpan();
        _walkFloorShape.Size = new Vector3(span, floor, span);
        _groundMesh.Size = new Vector2(span, span);
        _walkFloor.Position = new Vector3(focus.X, y0 + floor * 0.5f, focus.Z);
        _ground.Position = new Vector3(focus.X, y0, focus.Z);
    }

    private float FollowFloorSpan()
    {
        return ChunkSize * (2 * Mathf.Max(UnloadRadius, LoadRadius) + 3);
    }
}
