using Godot;

namespace Wallbreaker;

/// <summary>
/// Terrain type at a world point. Depth drives wavy bands of sand over dirt
/// over stone; a separate vein noise cuts ice through all of them. Weights come
/// out as one RGBA set in TerrainType order, ready to bake into vertex colors.
/// </summary>
public sealed class TypeSampler
{
    private readonly FastNoiseLite _bands;
    private readonly FastNoiseLite _iceVeins;
    private readonly float _bandTopY;
    private readonly float _crustHeight;
    private readonly float _bandWobble;
    private readonly float _sandDepth;
    private readonly float _dirtDepth;
    private readonly float _bandBlend;
    private readonly float _iceThreshold;
    private readonly float _iceBlend;

    public TypeSampler(TerrainNoise noise)
    {
        noise.EnsureLayers();
        _bands = noise.TypeBands;
        _iceVeins = noise.IceVeins;
        _crustHeight = Mathf.Max(noise.CrustHeight, 0.05f);
        _bandTopY = noise.BandMinY + _crustHeight;
        _bandWobble = noise.BandWobble;
        _sandDepth = noise.SandDepth;
        _dirtDepth = Mathf.Max(noise.DirtDepth, noise.SandDepth);
        _bandBlend = Mathf.Max(noise.BandBlend, 0.001f);
        _iceThreshold = noise.IceThreshold;
        _iceBlend = Mathf.Max(noise.IceBlend, 0.001f);
    }

    public Color Sample(Vector3 p)
    {
        float depth = (_bandTopY - p.Y) / _crustHeight;
        depth += _bands.GetNoise3D(p.X, p.Y * 0.5f, p.Z) * _bandWobble;

        float sand = 1f - Band(depth, _sandDepth);
        float stone = Band(depth, _dirtDepth);
        float dirt = Mathf.Max(1f - sand - stone, 0f);

        float vein = _iceVeins.GetNoise3D(p.X, p.Y, p.Z) * 0.5f + 0.5f;
        float ice = Mathf.SmoothStep(_iceThreshold - _iceBlend, _iceThreshold + _iceBlend, vein);
        float rest = 1f - ice;

        return Normalized(stone * rest, dirt * rest, ice, sand * rest);
    }

    public TerrainType Dominant(Vector3 p)
    {
        Color w = Sample(p);
        TerrainType best = TerrainType.Stone;
        float bestWeight = w.R;
        if (w.G > bestWeight)
        {
            best = TerrainType.Dirt;
            bestWeight = w.G;
        }

        if (w.B > bestWeight)
        {
            best = TerrainType.Ice;
            bestWeight = w.B;
        }

        return w.A > bestWeight ? TerrainType.Sand : best;
    }

    private float Band(float depth, float edge)
    {
        return Mathf.SmoothStep(edge - _bandBlend, edge + _bandBlend, depth);
    }

    private static Color Normalized(float stone, float dirt, float ice, float sand)
    {
        float sum = Mathf.Max(stone + dirt + ice + sand, 1e-4f);
        return new Color(stone / sum, dirt / sum, ice / sum, sand / sum);
    }
}
