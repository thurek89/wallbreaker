using Godot;

namespace Wallbreaker;

public partial class TerrainChunk : Node3D
{
    public const int Skirt = ChunkVolume.Skirt;
    public const int BrickCellSize = 16;

    public int ChunkX { get; private set; }
    public int ChunkZ { get; private set; }
    public Aabb Bounds { get; private set; }
    public ChunkVolume Volume => _volume;

    private ChunkVolume _volume = null!;
    private float[] _sdf = [];
    private int _nx;
    private int _ny;
    private int _nz;
    private int _cellsX;
    private int _cellsY;
    private int _cellsZ;
    private int _bricksX;
    private int _bricksY;
    private int _bricksZ;
    private float _voxelSize;
    private Vector3 _gridOrigin;
    private SdfSampler? _sampler;
    private Material? _material;
    private Brick[] _bricks = [];
    private StaticBody3D _body = null!;
    private readonly List<Vector3> _triBuffer = new(2048);

    private sealed class Brick
    {
        public MeshInstance3D Mesh = null!;
        public CollisionShape3D Shape = null!;
        public bool Dirty;
    }

    public void Build(
        int chunkX,
        int chunkZ,
        float chunkSize,
        int cellsXz,
        int cellsY,
        SdfSampler sampler,
        Material material,
        ChunkVolume? restored = null)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        _sampler = sampler;
        _material = material;
        _volume = restored ?? ChunkVolume.CreateFilled(chunkX, chunkZ, chunkSize, cellsXz, cellsY, sampler);
        BindVolume(_volume);
        CreateBricks();
    }

    public ChunkVolume TakeVolume()
    {
        return _volume;
    }

    public bool StitchFrom(ChunkVolume other)
    {
        return _volume.CopyOverlappingFrom(other);
    }

    public void RebuildMeshes()
    {
        RemeshAll();
    }

    public bool SubtractSphere(Vector3 center, float radius)
    {
        if (_sampler == null)
        {
            return false;
        }

        if (!_volume.SubtractSphere(center, radius, _sampler, out int x0, out int y0, out int z0, out int x1, out int y1, out int z1))
        {
            return false;
        }

        MarkDirtyVoxelRange(x0, y0, z0, x1, y1, z1);
        return true;
    }

    public bool OverlapsBrush(Vector3 center, float radius)
    {
        return _volume.OverlapsBrush(center, radius);
    }

    public void RemeshDirty()
    {
        for (int i = 0; i < _bricks.Length; i++)
        {
            if (_bricks[i].Dirty)
            {
                RemeshBrick(i);
                _bricks[i].Dirty = false;
            }
        }
    }

    public bool Raycast(Vector3 origin, Vector3 dir, float maxDistance, out Vector3 hit, out Vector3 normal, out float tHit)
    {
        hit = default;
        normal = Vector3.Up;
        tHit = maxDistance;
        dir = dir.Normalized();

        Aabb box = Bounds.Grow(_voxelSize * 2f);
        if (!RayAabb(origin, dir, box, maxDistance, out float tEnter, out float tExit))
        {
            return false;
        }

        float t = Mathf.Max(tEnter, 0f);
        float minStep = _voxelSize * 0.2f;
        const float iso = 0.002f;
        const int maxSteps = 128;

        for (int step = 0; step < maxSteps && t <= tExit; step++)
        {
            Vector3 p = origin + dir * t;
            float d = SampleGridUnbounded(p);
            if (d <= iso)
            {
                hit = p;
                normal = GradientAt(p);
                tHit = t;
                return true;
            }

            t += Mathf.Max(d, minStep);
        }

        return false;
    }

    private void RemeshAll()
    {
        for (int i = 0; i < _bricks.Length; i++)
        {
            _bricks[i].Dirty = true;
        }

        RemeshDirty();
    }

    private void RemeshBrick(int index)
    {
        int bx = index % _bricksX;
        int by = index / _bricksX % _bricksY;
        int bz = index / (_bricksX * _bricksY);
        Brick brick = _bricks[index];

        int cellMinX = Skirt + bx * BrickCellSize;
        int cellMinY = by * BrickCellSize;
        int cellMinZ = Skirt + bz * BrickCellSize;
        int cellsX = Mathf.Min(BrickCellSize, _cellsX - bx * BrickCellSize);
        int cellsY = Mathf.Min(BrickCellSize, _cellsY - by * BrickCellSize);
        int cellsZ = Mathf.Min(BrickCellSize, _cellsZ - bz * BrickCellSize);

        _triBuffer.Clear();
        MarchingCubes.Generate(
            _sdf,
            _nx,
            _ny,
            _nz,
            _gridOrigin,
            _voxelSize,
            cellMinX,
            cellMinY,
            cellMinZ,
            cellsX,
            cellsY,
            cellsZ,
            _triBuffer);

        if (_triBuffer.Count == 0)
        {
            brick.Mesh.Mesh = null;
            brick.Shape.Shape = null;
            brick.Shape.Disabled = true;
            return;
        }

        var vertices = _triBuffer.ToArray();
        var normals = new Vector3[vertices.Length];
        for (int i = 0; i + 2 < vertices.Length; i += 3)
        {
            Vector3 n = (vertices[i + 1] - vertices[i]).Cross(vertices[i + 2] - vertices[i]);
            n = n.LengthSquared() > 1e-12f ? n.Normalized() : Vector3.Up;
            normals[i] = n;
            normals[i + 1] = n;
            normals[i + 2] = n;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;

        var mesh = brick.Mesh.Mesh as ArrayMesh ?? new ArrayMesh();
        if (mesh.GetSurfaceCount() > 0)
        {
            mesh.ClearSurfaces();
        }

        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        brick.Mesh.Mesh = mesh;
        brick.Mesh.MaterialOverride = _material;
        brick.Shape.Disabled = false;
        brick.Shape.Shape = new ConcavePolygonShape3D { Data = vertices };
    }

    private void MarkDirtyVoxelRange(int x0, int y0, int z0, int x1, int y1, int z1)
    {
        int cx0 = x0 - Skirt - 1;
        int cy0 = y0 - 1;
        int cz0 = z0 - Skirt - 1;
        int cx1 = x1 - Skirt;
        int cy1 = y1;
        int cz1 = z1 - Skirt;

        int bx0 = CellToBrick(cx0, _bricksX);
        int bx1 = CellToBrick(cx1, _bricksX);
        int by0 = CellToBrick(cy0, _bricksY);
        int by1 = CellToBrick(cy1, _bricksY);
        int bz0 = CellToBrick(cz0, _bricksZ);
        int bz1 = CellToBrick(cz1, _bricksZ);

        for (int bz = bz0; bz <= bz1; bz++)
        {
            for (int by = by0; by <= by1; by++)
            {
                for (int bx = bx0; bx <= bx1; bx++)
                {
                    _bricks[bx + _bricksX * (by + _bricksY * bz)].Dirty = true;
                }
            }
        }
    }

    private int CellToBrick(int cell, int brickCount)
    {
        if (cell < 0)
        {
            return 0;
        }

        return Mathf.Min(cell / BrickCellSize, brickCount - 1);
    }

    private void BindVolume(ChunkVolume volume)
    {
        _sdf = volume.Sdf;
        _nx = volume.Nx;
        _ny = volume.Ny;
        _nz = volume.Nz;
        _cellsX = volume.CellsX;
        _cellsY = volume.CellsY;
        _cellsZ = volume.CellsZ;
        _voxelSize = volume.VoxelSize;
        _gridOrigin = volume.GridOrigin;
        Bounds = volume.Bounds;
    }

    private void CreateBricks()
    {
        _bricksX = Mathf.Max(1, (_cellsX + BrickCellSize - 1) / BrickCellSize);
        _bricksY = Mathf.Max(1, (_cellsY + BrickCellSize - 1) / BrickCellSize);
        _bricksZ = Mathf.Max(1, (_cellsZ + BrickCellSize - 1) / BrickCellSize);
        _bricks = new Brick[_bricksX * _bricksY * _bricksZ];

        var meshes = new Node3D { Name = "Meshes" };
        AddChild(meshes);
        _body = new StaticBody3D { Name = "Body" };
        AddChild(_body);

        for (int bz = 0; bz < _bricksZ; bz++)
        {
            for (int by = 0; by < _bricksY; by++)
            {
                for (int bx = 0; bx < _bricksX; bx++)
                {
                    int i = bx + _bricksX * (by + _bricksY * bz);
                    var mesh = new MeshInstance3D
                    {
                        Name = $"Mesh_{bx}_{by}_{bz}",
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.On
                    };
                    meshes.AddChild(mesh);
                    var shape = new CollisionShape3D { Name = $"Shape_{bx}_{by}_{bz}" };
                    _body.AddChild(shape);
                    _bricks[i] = new Brick { Mesh = mesh, Shape = shape, Dirty = true };
                }
            }
        }
    }

    private Vector3 GradientAt(Vector3 world)
    {
        float e = Mathf.Max(_voxelSize, 0.02f);
        float dx = SampleGridUnbounded(world + new Vector3(e, 0f, 0f)) - SampleGridUnbounded(world - new Vector3(e, 0f, 0f));
        float dy = SampleGridUnbounded(world + new Vector3(0f, e, 0f)) - SampleGridUnbounded(world - new Vector3(0f, e, 0f));
        float dz = SampleGridUnbounded(world + new Vector3(0f, 0f, e)) - SampleGridUnbounded(world - new Vector3(0f, 0f, e));
        Vector3 g = new(dx, dy, dz);
        return g.LengthSquared() > 1e-8f ? g.Normalized() : Vector3.Up;
    }

    private float SampleGridUnbounded(Vector3 world)
    {
        Vector3 g = (world - _gridOrigin) / _voxelSize;
        if (g.X < 0f || g.Y < 0f || g.Z < 0f || g.X > _nx - 1 || g.Y > _ny - 1 || g.Z > _nz - 1)
        {
            return 1f;
        }

        int x0 = Mathf.FloorToInt(g.X);
        int y0 = Mathf.FloorToInt(g.Y);
        int z0 = Mathf.FloorToInt(g.Z);
        float tx = g.X - x0;
        float ty = g.Y - y0;
        float tz = g.Z - z0;

        float c000 = Voxel(x0, y0, z0);
        float c100 = Voxel(x0 + 1, y0, z0);
        float c010 = Voxel(x0, y0 + 1, z0);
        float c110 = Voxel(x0 + 1, y0 + 1, z0);
        float c001 = Voxel(x0, y0, z0 + 1);
        float c101 = Voxel(x0 + 1, y0, z0 + 1);
        float c011 = Voxel(x0, y0 + 1, z0 + 1);
        float c111 = Voxel(x0 + 1, y0 + 1, z0 + 1);

        float c00 = Mathf.Lerp(c000, c100, tx);
        float c10 = Mathf.Lerp(c010, c110, tx);
        float c01 = Mathf.Lerp(c001, c101, tx);
        float c11 = Mathf.Lerp(c011, c111, tx);
        return Mathf.Lerp(Mathf.Lerp(c00, c10, ty), Mathf.Lerp(c01, c11, ty), tz);
    }

    private float Voxel(int x, int y, int z)
    {
        x = Mathf.Clamp(x, 0, _nx - 1);
        y = Mathf.Clamp(y, 0, _ny - 1);
        z = Mathf.Clamp(z, 0, _nz - 1);
        return _sdf[x + _nx * (y + _ny * z)];
    }

    private static bool RayAabb(Vector3 origin, Vector3 dir, Aabb box, float maxDistance, out float tEnter, out float tExit)
    {
        tEnter = 0f;
        tExit = maxDistance;
        Vector3 min = box.Position;
        Vector3 max = box.End;
        for (int axis = 0; axis < 3; axis++)
        {
            float o = origin[axis];
            float d = dir[axis];
            if (Mathf.Abs(d) < 1e-8f)
            {
                if (o < min[axis] || o > max[axis])
                {
                    return false;
                }

                continue;
            }

            float t0 = (min[axis] - o) / d;
            float t1 = (max[axis] - o) / d;
            if (t0 > t1)
            {
                (t0, t1) = (t1, t0);
            }

            tEnter = Mathf.Max(tEnter, t0);
            tExit = Mathf.Min(tExit, t1);
            if (tEnter > tExit)
            {
                return false;
            }
        }

        return tExit >= 0f && tEnter <= maxDistance;
    }
}
