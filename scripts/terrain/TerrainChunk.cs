using Godot;

namespace Wallbreaker;

public partial class TerrainChunk : Node3D
{
    public const int Skirt = 1;

    public int ChunkX { get; private set; }
    public int ChunkZ { get; private set; }
    public Aabb Bounds { get; private set; }

    private float[] _sdf = [];
    private int _nx;
    private int _ny;
    private int _nz;
    private int _cellsX;
    private int _cellsY;
    private int _cellsZ;
    private float _voxelSize;
    private Vector3 _gridOrigin;
    private SdfSampler? _sampler;
    private MeshInstance3D _meshInstance = null!;
    private CollisionShape3D _collisionShape = null!;
    private Material? _material;

    public void Build(
        int chunkX,
        int chunkZ,
        float chunkSize,
        int cellsXz,
        int cellsY,
        SdfSampler sampler,
        Material material)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        _sampler = sampler;
        _material = material;
        _cellsX = cellsXz;
        _cellsY = cellsY;
        _cellsZ = cellsXz;
        _voxelSize = chunkSize / cellsXz;
        _nx = _cellsX + 1 + Skirt * 2;
        _ny = _cellsY + 1;
        _nz = _cellsZ + 1 + Skirt * 2;
        _sdf = new float[_nx * _ny * _nz];

        Vector3 chunkOrigin = new(chunkX * chunkSize, 0f, chunkZ * chunkSize);
        _gridOrigin = chunkOrigin - new Vector3(Skirt * _voxelSize, 0f, Skirt * _voxelSize);
        Bounds = new Aabb(chunkOrigin, new Vector3(chunkSize, _cellsY * _voxelSize, chunkSize));

        EnsureChildren();
        FillVolume();
        Remesh();
    }

    /// <summary>
    /// Rebuilds the render mesh and collision from the stored SDF volume.
    /// Later destruction writes into the grid then calls this.
    /// </summary>
    public void Remesh()
    {
        if (_sampler == null || _sdf.Length == 0)
        {
            return;
        }

        var triangles = new List<Vector3>(4096);
        MarchingCubes.Generate(
            _sdf,
            _nx,
            _ny,
            _nz,
            _gridOrigin,
            _voxelSize,
            Skirt,
            0,
            Skirt,
            _cellsX,
            _cellsY,
            _cellsZ,
            triangles);

        if (triangles.Count == 0)
        {
            _meshInstance.Mesh = null;
            _collisionShape.Shape = null;
            return;
        }

        var vertices = triangles.ToArray();
        var normals = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            normals[i] = _sampler.Gradient(vertices[i]);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _meshInstance.Mesh = mesh;
        _meshInstance.MaterialOverride = _material;

        _collisionShape.Shape = new ConcavePolygonShape3D { Data = vertices };
    }

    private void FillVolume()
    {
        if (_sampler == null)
        {
            return;
        }

        for (int z = 0; z < _nz; z++)
        {
            for (int y = 0; y < _ny; y++)
            {
                for (int x = 0; x < _nx; x++)
                {
                    Vector3 world = _gridOrigin + new Vector3(x, y, z) * _voxelSize;
                    _sdf[x + _nx * (y + _ny * z)] = _sampler.Sample(world);
                }
            }
        }
    }

    private void EnsureChildren()
    {
        if (_meshInstance != null)
        {
            return;
        }

        _meshInstance = new MeshInstance3D { Name = "Mesh" };
        AddChild(_meshInstance);

        var body = new StaticBody3D { Name = "Body" };
        AddChild(body);
        _collisionShape = new CollisionShape3D { Name = "Shape" };
        body.AddChild(_collisionShape);
    }
}
