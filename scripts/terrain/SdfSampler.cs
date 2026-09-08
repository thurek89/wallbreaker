using Godot;

namespace Wallbreaker;

/// <summary>
/// World-space signed distance for occupied earth from y=0 to y=CrustHeight.
/// The top of the slab is the "ceiling": it means this column is still occupied.
/// Digging clears that column down to an immortal floor; the ceiling there is removed.
/// Negative values are solid.
/// </summary>
public sealed class SdfSampler
{
    private readonly FastNoiseLite _noise;
    private readonly float _interiorFade;

    public SdfSampler(
        int seed,
        float frequency,
        float amplitude,
        float crustHeight,
        float floorThickness,
        float ceilingThickness)
    {
        Amplitude = amplitude;
        CrustHeight = Mathf.Max(crustHeight, 0.05f);
        FloorThickness = Mathf.Clamp(floorThickness, 0.05f, CrustHeight * 0.45f);
        CeilingThickness = Mathf.Clamp(ceilingThickness, 0.05f, CrustHeight - FloorThickness - 0.05f);
        _interiorFade = Mathf.Max(0.25f, Mathf.Min(FloorThickness, CeilingThickness));

        _noise = new FastNoiseLite
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 4,
            FractalLacunarity = 2f,
            FractalGain = 0.5f,
            Frequency = frequency
        };
    }

    public float Amplitude { get; }
    public float CrustHeight { get; }
    public float FloorThickness { get; }
    public float CeilingThickness { get; }
    public float FloorTop => FloorThickness;
    public float CeilingBottom => CrustHeight - CeilingThickness;

    public float Sample(Vector3 p)
    {
        float slab = Mathf.Max(-p.Y, p.Y - CrustHeight);
        float interior = InteriorWeight(p.Y);
        float n = _noise.GetNoise3D(p.X, p.Y * 0.65f, p.Z) * Amplitude * 0.35f * interior;
        return PreserveFloor(slab + n, p);
    }

    /// <summary>
    /// Keeps the walkable floor after a carve. The ceiling is not restored:
    /// clearing a column means that spot is no longer occupied.
    /// </summary>
    public float PreserveFloor(float distance, Vector3 p)
    {
        float d = Mathf.Min(distance, FloorSdf(p));
        d = Mathf.Max(d, -p.Y);
        d = Mathf.Max(d, p.Y - CrustHeight);
        return d;
    }

    public float FloorSdf(Vector3 p)
    {
        return Mathf.Max(-p.Y, p.Y - FloorThickness);
    }

    private float InteriorWeight(float y)
    {
        float fromFloor = Mathf.Clamp((y - FloorThickness) / _interiorFade, 0f, 1f);
        float fromCeiling = Mathf.Clamp((CeilingBottom - y) / _interiorFade, 0f, 1f);
        return fromFloor * fromCeiling;
    }
}
