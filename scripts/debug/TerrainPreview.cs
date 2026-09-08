using Godot;

namespace Wallbreaker;

/// <summary>
/// Editor/runtime preview of a fixed 3x3 chunk neighborhood.
/// Assign a TerrainNoise resource and edit it in the inspector to rebuild.
/// </summary>
[Tool]
public partial class TerrainPreview : Node3D
{
    [ExportGroup("Chunks")]
    [Export] public float ChunkSize { get; set; } = 10f;
    [Export] public int CellsXz { get; set; } = 64;
    [Export] public int CellsY { get; set; } = 32;

    [ExportGroup("Noise")]
    [Export] public TerrainNoise? Noise { get; set; }

    [ExportGroup("Slice")]
    [Export] public bool ClipFront { get; set; } = true;
    [Export] public float RebuildDelay { get; set; } = 0.15f;

    private const int Extent = 1;
    private const string DefaultNoisePath = "res://terrain/default_noise.tres";

    private readonly Dictionary<(int X, int Z), TerrainChunk> _chunks = [];
    private SdfSampler _sampler = null!;
    private ShaderMaterial _material = null!;
    private MeshInstance3D _grid = null!;
    private Node3D? _cameraRig;
    private bool _hasBuilt;
    private float _sinceChange;
    private int _appliedNoiseHash;
    private float _appliedChunkSize;
    private int _appliedCellsXz;
    private int _appliedCellsY;

    public override void _Ready()
    {
        SetProcess(true);
        Node? world = GetParent();
        _cameraRig = world?.GetNodeOrNull<Node3D>("CameraRig");
        EnsureMaterial();
        EnsureGrid();
        Rebuild();
    }

    public override void _Process(double delta)
    {
        UpdateClip();
        UpdateCameraRig();
        if (!SettingsDiffer())
        {
            _sinceChange = 0f;
            return;
        }

        _sinceChange += (float)delta;
        if (_hasBuilt && _sinceChange < Mathf.Max(RebuildDelay, 0.01f))
        {
            return;
        }

        _sinceChange = 0f;
        Rebuild();
    }

    private void Rebuild()
    {
        ClearChunks();
        ClearOrphanChunks();
        ChunkSize = Mathf.Max(ChunkSize, 1f);
        CellsXz = Mathf.Max(CellsXz, 8);
        CellsY = Mathf.Max(CellsY, 4);

        TerrainNoise noise = ResolveNoise();
        _sampler = new SdfSampler(noise);

        EnsureMaterial();
        for (int iz = -Extent; iz <= Extent; iz++)
        {
            for (int ix = -Extent; ix <= Extent; ix++)
            {
                EnsureChunk(ix, iz);
            }
        }

        RebuildGridOverlay();
        UpdateClip();
        UpdateCameraRig();
        StoreApplied();
        _hasBuilt = true;
    }

    private void EnsureChunk(int ix, int iz)
    {
        var key = (ix, iz);
        if (_chunks.ContainsKey(key))
        {
            return;
        }

        var chunk = new TerrainChunk { Name = $"Chunk_{ix}_{iz}" };
        AddChild(chunk);
        chunk.Build(ix, iz, ChunkSize, CellsXz, CellsY, _sampler, _material);
        StitchWithNeighbors(chunk);
        chunk.RebuildMeshes();
        _chunks[key] = chunk;
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
                if (!_chunks.TryGetValue(key, out TerrainChunk? neighbor))
                {
                    continue;
                }

                chunk.StitchFrom(neighbor.Volume);
                if (neighbor.StitchFrom(chunk.Volume))
                {
                    neighbor.RebuildMeshes();
                }
            }
        }
    }

    private void ClearChunks()
    {
        foreach (TerrainChunk chunk in _chunks.Values)
        {
            if (!GodotObject.IsInstanceValid(chunk))
            {
                continue;
            }

            if (chunk.GetParent() == this)
            {
                RemoveChild(chunk);
            }

            chunk.Free();
        }

        _chunks.Clear();
    }

    private void ClearOrphanChunks()
    {
        List<TerrainChunk> orphans = [];
        foreach (Node child in GetChildren())
        {
            if (child is TerrainChunk chunk)
            {
                orphans.Add(chunk);
            }
        }

        foreach (TerrainChunk chunk in orphans)
        {
            RemoveChild(chunk);
            chunk.Free();
        }
    }

    private void EnsureMaterial()
    {
        if (GodotObject.IsInstanceValid(_material))
        {
            return;
        }

        _material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://shaders/terrain_preview.gdshader")
        };
        _material.SetShaderParameter("floor_albedo", new Color(0.40f, 0.32f, 0.22f));
        _material.SetShaderParameter("wall_albedo", new Color(0.64f, 0.56f, 0.48f));
        _material.SetShaderParameter("ceiling_albedo", new Color(0.26f, 0.23f, 0.22f));
        _material.SetShaderParameter("roughness_v", 0.85f);
    }

    private void EnsureGrid()
    {
        if (GodotObject.IsInstanceValid(_grid) && _grid.GetParent() == this)
        {
            return;
        }

        _grid = GetNodeOrNull<MeshInstance3D>("ChunkGrid")!;
        if (GodotObject.IsInstanceValid(_grid))
        {
            return;
        }

        _grid = new MeshInstance3D
        {
            Name = "ChunkGrid",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.18f, 0.92f, 0.45f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                DisableReceiveShadows = true
            }
        };
        AddChild(_grid);
    }

    private void RebuildGridOverlay()
    {
        EnsureGrid();
        var vertices = new List<Vector3>((2 * Extent + 1) * (2 * Extent + 1) * 24);
        for (int iz = -Extent; iz <= Extent; iz++)
        {
            for (int ix = -Extent; ix <= Extent; ix++)
            {
                AddBoxLines(vertices, ix, iz);
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
        _grid.Mesh = mesh;
    }

    private void AddBoxLines(List<Vector3> vertices, int ix, int iz)
    {
        float x0 = ix * ChunkSize;
        float z0 = iz * ChunkSize;
        float x1 = x0 + ChunkSize;
        float z1 = z0 + ChunkSize;
        float y0 = _sampler.BandMinY;
        float y1 = _sampler.BandMaxY;
        Vector3 a = new(x0, y0, z0);
        Vector3 b = new(x1, y0, z0);
        Vector3 c = new(x1, y0, z1);
        Vector3 d = new(x0, y0, z1);
        Vector3 e = new(x0, y1, z0);
        Vector3 f = new(x1, y1, z0);
        Vector3 g = new(x1, y1, z1);
        Vector3 h = new(x0, y1, z1);
        AddLine(vertices, a, b);
        AddLine(vertices, b, c);
        AddLine(vertices, c, d);
        AddLine(vertices, d, a);
        AddLine(vertices, e, f);
        AddLine(vertices, f, g);
        AddLine(vertices, g, h);
        AddLine(vertices, h, e);
        AddLine(vertices, a, e);
        AddLine(vertices, b, f);
        AddLine(vertices, c, g);
        AddLine(vertices, d, h);
    }

    private static void AddLine(List<Vector3> vertices, Vector3 from, Vector3 to)
    {
        vertices.Add(from);
        vertices.Add(to);
    }

    private void UpdateClip()
    {
        if (!GodotObject.IsInstanceValid(_material))
        {
            return;
        }

        _material.SetShaderParameter("clip_enabled", ClipFront);
        _material.SetShaderParameter("clip_center", GridCenter());
        _material.SetShaderParameter("clip_normal", new Vector3(1f, 0f, 1f));
    }

    private void UpdateCameraRig()
    {
        if (!GodotObject.IsInstanceValid(_cameraRig))
        {
            _cameraRig = GetParent()?.GetNodeOrNull<Node3D>("CameraRig");
        }

        if (!GodotObject.IsInstanceValid(_cameraRig))
        {
            return;
        }

        TerrainNoise noise = ResolveNoise();
        Vector3 center = GridCenter();
        _cameraRig.GlobalPosition = new Vector3(center.X, noise.BandMinY, center.Z);
    }

    private Vector3 GridCenter()
    {
        float min = -Extent * ChunkSize;
        float max = (Extent + 1) * ChunkSize;
        float mid = (min + max) * 0.5f;
        TerrainNoise noise = ResolveNoise();
        return new Vector3(mid, noise.BandMinY + noise.CrustHeight * 0.5f, mid);
    }

    private bool SettingsDiffer()
    {
        return _appliedNoiseHash != ResolveNoise().ContentHash()
            || !Mathf.IsEqualApprox(_appliedChunkSize, ChunkSize)
            || _appliedCellsXz != CellsXz
            || _appliedCellsY != CellsY;
    }

    private void StoreApplied()
    {
        _appliedNoiseHash = ResolveNoise().ContentHash();
        _appliedChunkSize = ChunkSize;
        _appliedCellsXz = CellsXz;
        _appliedCellsY = CellsY;
    }

    private TerrainNoise ResolveNoise()
    {
        if (GodotObject.IsInstanceValid(Noise))
        {
            return Noise!;
        }

        TerrainNoise? loaded = GD.Load<TerrainNoise>(DefaultNoisePath);
        return loaded ?? new TerrainNoise();
    }
}
