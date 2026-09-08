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
    [Export] public Color TerrainColor { get; set; } = new(0.55f, 0.45f, 0.32f);

    private readonly Dictionary<(int X, int Z), TerrainChunk> _chunks = [];
    private SdfSampler _sampler = null!;
    private StandardMaterial3D _material = null!;
    private Aabb _bounds;

    public override void _Ready()
    {
        _sampler = new SdfSampler(
            NoiseSeed,
            NoiseFrequency,
            NoiseAmplitude,
            CrustHeight,
            FloorThickness,
            CeilingThickness);
        _material = new StandardMaterial3D
        {
            AlbedoColor = TerrainColor,
            Roughness = 0.85f,
            Metallic = 0f,
            DisableReceiveShadows = false
        };
        _bounds = new Aabb(Vector3.Zero, Vector3.Zero);
        EnsureChunk(0, 0);
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
                chunk.SubtractColumn(worldPoint, radius);
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
}
