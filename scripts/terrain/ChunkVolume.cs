using Godot;

namespace Wallbreaker;

/// <summary>
/// SDF voxel grid for one XZ chunk. Shared by loaded meshes and the unload cache
/// so carves survive streaming.
/// </summary>
public sealed class ChunkVolume
{
    public const int Skirt = 1;

    public ChunkVolume(
        int chunkX,
        int chunkZ,
        float[] sdf,
        int nx,
        int ny,
        int nz,
        int cellsX,
        int cellsY,
        int cellsZ,
        float voxelSize,
        Vector3 gridOrigin,
        Aabb bounds)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        Sdf = sdf;
        Nx = nx;
        Ny = ny;
        Nz = nz;
        CellsX = cellsX;
        CellsY = cellsY;
        CellsZ = cellsZ;
        VoxelSize = voxelSize;
        GridOrigin = gridOrigin;
        Bounds = bounds;
    }

    public int ChunkX { get; }
    public int ChunkZ { get; }
    public float[] Sdf { get; }
    public int Nx { get; }
    public int Ny { get; }
    public int Nz { get; }
    public int CellsX { get; }
    public int CellsY { get; }
    public int CellsZ { get; }
    public float VoxelSize { get; }
    public Vector3 GridOrigin { get; }
    public Aabb Bounds { get; }

    public static ChunkVolume CreateFilled(
        int chunkX,
        int chunkZ,
        float chunkSize,
        int cellsXz,
        int cellsY,
        SdfSampler sampler)
    {
        ChunkVolume volume = CreateEmpty(chunkX, chunkZ, chunkSize, cellsXz, cellsY, sampler.BandMinY);
        volume.Fill(sampler);
        return volume;
    }

    public static ChunkVolume CreateEmpty(
        int chunkX,
        int chunkZ,
        float chunkSize,
        int cellsXz,
        int cellsY,
        float originY = 0f)
    {
        float voxelSize = chunkSize / cellsXz;
        int nx = cellsXz + 1 + Skirt * 2;
        int ny = cellsY + 1;
        int nz = cellsXz + 1 + Skirt * 2;
        Vector3 chunkOrigin = new(chunkX * chunkSize, originY, chunkZ * chunkSize);
        Vector3 gridOrigin = chunkOrigin - new Vector3(Skirt * voxelSize, 0f, Skirt * voxelSize);
        var bounds = new Aabb(chunkOrigin, new Vector3(chunkSize, cellsY * voxelSize, chunkSize));
        return new ChunkVolume(
            chunkX,
            chunkZ,
            new float[nx * ny * nz],
            nx,
            ny,
            nz,
            cellsXz,
            cellsY,
            cellsXz,
            voxelSize,
            gridOrigin,
            bounds);
    }

    public void Fill(SdfSampler sampler)
    {
        for (int z = 0; z < Nz; z++)
        {
            for (int y = 0; y < Ny; y++)
            {
                for (int x = 0; x < Nx; x++)
                {
                    Vector3 world = GridOrigin + new Vector3(x, y, z) * VoxelSize;
                    Sdf[x + Nx * (y + Ny * z)] = sampler.Sample(world);
                }
            }
        }
    }

    public bool OverlapsBrush(Vector3 center, float radius)
    {
        var brush = new Aabb(
            center - new Vector3(radius, radius, radius),
            new Vector3(radius * 2f, radius * 2f, radius * 2f));
        return Bounds.Grow(VoxelSize * (Skirt + 1)).Intersects(brush);
    }

    public bool SubtractSphere(Vector3 center, float radius, SdfSampler sampler)
    {
        return SubtractSphere(center, radius, sampler, out _, out _, out _, out _, out _, out _);
    }

    public bool SubtractSphere(
        Vector3 center,
        float radius,
        SdfSampler sampler,
        out int x0,
        out int y0,
        out int z0,
        out int x1,
        out int y1,
        out int z1)
    {
        x0 = y0 = z0 = x1 = y1 = z1 = 0;
        if (Sdf.Length == 0 || radius <= 0f)
        {
            return false;
        }

        float pad = VoxelSize;
        float reach = radius + pad;
        Vector3 min = (center - new Vector3(reach, reach, reach) - GridOrigin) / VoxelSize;
        Vector3 max = (center + new Vector3(reach, reach, reach) - GridOrigin) / VoxelSize;
        x0 = Mathf.Clamp(Mathf.FloorToInt(min.X), 0, Nx - 1);
        y0 = Mathf.Clamp(Mathf.FloorToInt(min.Y), 0, Ny - 1);
        z0 = Mathf.Clamp(Mathf.FloorToInt(min.Z), 0, Nz - 1);
        x1 = Mathf.Clamp(Mathf.CeilToInt(max.X), 0, Nx - 1);
        y1 = Mathf.Clamp(Mathf.CeilToInt(max.Y), 0, Ny - 1);
        z1 = Mathf.Clamp(Mathf.CeilToInt(max.Z), 0, Nz - 1);

        bool changed = false;
        for (int z = z0; z <= z1; z++)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector3 world = GridOrigin + new Vector3(x, y, z) * VoxelSize;
                    int i = x + Nx * (y + Ny * z);
                    float sphere = world.DistanceTo(center) - radius;
                    float carved = sampler.PreserveFloor(Mathf.Max(Sdf[i], -sphere), world);
                    if (Mathf.Abs(carved - Sdf[i]) > 1e-5f)
                    {
                        Sdf[i] = carved;
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    public bool CopyOverlappingFrom(ChunkVolume other)
    {
        if (ReferenceEquals(this, other) || Sdf.Length == 0 || other.Sdf.Length == 0)
        {
            return false;
        }

        if (!Mathf.IsEqualApprox(VoxelSize, other.VoxelSize))
        {
            return false;
        }

        int shiftX = Mathf.RoundToInt((GridOrigin.X - other.GridOrigin.X) / VoxelSize);
        int shiftY = Mathf.RoundToInt((GridOrigin.Y - other.GridOrigin.Y) / VoxelSize);
        int shiftZ = Mathf.RoundToInt((GridOrigin.Z - other.GridOrigin.Z) / VoxelSize);
        int x0 = Mathf.Max(0, -shiftX);
        int x1 = Mathf.Min(Nx, other.Nx - shiftX);
        int y0 = Mathf.Max(0, -shiftY);
        int y1 = Mathf.Min(Ny, other.Ny - shiftY);
        int z0 = Mathf.Max(0, -shiftZ);
        int z1 = Mathf.Min(Nz, other.Nz - shiftZ);
        if (x0 >= x1 || y0 >= y1 || z0 >= z1)
        {
            return false;
        }

        bool changed = false;
        for (int z = z0; z < z1; z++)
        {
            int oz = z + shiftZ;
            for (int y = y0; y < y1; y++)
            {
                int oy = y + shiftY;
                int dstRow = Nx * (y + Ny * z);
                int srcRow = other.Nx * (oy + other.Ny * oz);
                for (int x = x0; x < x1; x++)
                {
                    float v = other.Sdf[x + shiftX + srcRow];
                    int di = x + dstRow;
                    if (v > Sdf[di] + 1e-5f)
                    {
                        Sdf[di] = v;
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }
}
