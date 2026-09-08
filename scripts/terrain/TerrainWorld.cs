using Godot;

namespace Wallbreaker;

public partial class TerrainWorld : Node3D
{
    [Export] public float ChunkSize { get; set; } = 10f;
    [Export] public int CellsXz { get; set; } = 64;
    [Export] public int CellsY { get; set; } = 32;
    [Export] public int NoiseSeed { get; set; } = 17;
    [Export] public float NoiseFrequency { get; set; } = 0.28f;
    [Export] public float NoiseAmplitude { get; set; } = 0.95f;
    [Export] public float CrustHeight { get; set; } = 5f;
    [Export] public float FloorThickness { get; set; } = 0.45f;
    [Export] public float CeilingThickness { get; set; } = 0.55f;
    [Export] public float SpawnClearRadius { get; set; } = 2.0f;
    [Export] public Color TerrainColor { get; set; } = new(0.55f, 0.45f, 0.32f);
    [Export] public float CutRadius { get; set; } = 1.6f;
    [Export] public float CutSoftness { get; set; } = 0.6f;

    private readonly Dictionary<(int X, int Z), TerrainChunk> _chunks = [];
    private SdfSampler _sampler = null!;
    private ShaderMaterial _material = null!;
    private Aabb _bounds;
    private Node3D? _player;
    private Camera3D? _camera;

    public override void _Ready()
    {
        _sampler = new SdfSampler(
            NoiseSeed,
            NoiseFrequency,
            NoiseAmplitude,
            CrustHeight,
            FloorThickness,
            CeilingThickness);
        _material = CreateCutawayMaterial();
        _bounds = new Aabb(Vector3.Zero, Vector3.Zero);
        EnsureChunk(0, 0);
        AddWalkFloor();
        ClearSpawn(GetSpawnPoint(), SpawnClearRadius);

        Node? world = GetParent();
        _player = world?.GetNodeOrNull<Node3D>("Player");
        _camera = world?.GetNodeOrNull<Camera3D>("CameraRig/Camera3D");
    }

    public override void _Process(double _delta)
    {
        UpdateCutaway();
    }

    public float FloorTop => _sampler?.FloorTop ?? FloorThickness;

    public Vector3 GetSpawnPoint()
    {
        return new Vector3(ChunkSize * 0.5f, FloorTop, ChunkSize * 0.5f);
    }

    public void ClearSpawn(Vector3 worldPoint, float radius)
    {
        float spacing = Mathf.Max(radius * 0.85f, 0.25f);
        float y1 = CrustHeight + radius;
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
        foreach (TerrainChunk chunk in _chunks.Values)
        {
            if (chunk.OverlapsBrush(worldPoint, radius))
            {
                chunk.SubtractSphere(worldPoint, radius);
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

        var chunk = new TerrainChunk
        {
            Name = $"Chunk_{ix}_{iz}"
        };
        AddChild(chunk);
        chunk.Build(ix, iz, ChunkSize, CellsXz, CellsY, _sampler, _material);
        _chunks[key] = chunk;
        RebuildBounds();
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

        _bounds = first ? new Aabb(Vector3.Zero, new Vector3(ChunkSize, CrustHeight, ChunkSize)) : merged;
    }

    private ShaderMaterial CreateCutawayMaterial()
    {
        var shader = GD.Load<Shader>("res://shaders/terrain_cutaway.gdshader");
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("albedo", TerrainColor);
        material.SetShaderParameter("roughness_v", 0.85f);
        material.SetShaderParameter("cut_radius", CutRadius);
        material.SetShaderParameter("cut_softness", CutSoftness);
        return material;
    }

    private void UpdateCutaway()
    {
        if (_player == null || _camera == null)
        {
            return;
        }

        Vector3 center = _player.GlobalPosition + new Vector3(0f, 0.8f, 0f);
        Vector3 axis = -_camera.GlobalBasis.Z;
        if (axis.LengthSquared() < 1e-8f)
        {
            return;
        }

        _material.SetShaderParameter("cut_center", center);
        _material.SetShaderParameter("cut_axis", axis.Normalized());
        _material.SetShaderParameter("cut_radius", CutRadius);
        _material.SetShaderParameter("cut_softness", CutSoftness);
    }

    private void AddWalkFloor()
    {
        var body = new StaticBody3D { Name = "WalkFloor" };
        var shape = new CollisionShape3D
        {
            Shape = new BoxShape3D
            {
                Size = new Vector3(ChunkSize, FloorThickness, ChunkSize)
            }
        };
        body.Position = new Vector3(ChunkSize * 0.5f, FloorThickness * 0.5f, ChunkSize * 0.5f);
        body.AddChild(shape);
        AddChild(body);
    }
}
