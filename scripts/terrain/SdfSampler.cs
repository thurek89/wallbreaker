using Godot;

namespace Wallbreaker;

/// <summary>
/// World-space signed distance for occupied earth from y=0 to y=CrustHeight.
/// Interior caverns come from 3D noise; carves subtract extra volumes.
/// The walkable floor band stays solid. Negative values are solid.
/// </summary>
public sealed class SdfSampler
{
    private readonly FastNoiseLite _caverns;
    private readonly FastNoiseLite _tunnels;
    private readonly FastNoiseLite _warp;
    private readonly float _interiorFade;
    private readonly float _warpAmp;

    public SdfSampler(
        int seed,
        float frequency,
        float amplitude,
        float crustHeight,
        float floorThickness,
        float ceilingThickness,
        float cavernThreshold,
        float tunnelWidth,
        float cavernYScale)
    {
        Amplitude = Mathf.Max(amplitude, 0.01f);
        CrustHeight = Mathf.Max(crustHeight, 0.05f);
        FloorThickness = Mathf.Clamp(floorThickness, 0.05f, CrustHeight * 0.45f);
        CeilingThickness = Mathf.Clamp(ceilingThickness, 0.05f, CrustHeight - FloorThickness - 0.05f);
        CavernThreshold = cavernThreshold;
        TunnelWidth = Mathf.Max(tunnelWidth, 0f);
        CavernYScale = Mathf.Max(cavernYScale, 0.05f);
        _interiorFade = Mathf.Max(0.25f, Mathf.Min(FloorThickness, CeilingThickness));
        frequency = Mathf.Max(frequency, 0.001f);
        _warpAmp = 0.28f / frequency;

        _caverns = new FastNoiseLite
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 4,
            FractalLacunarity = 2f,
            FractalGain = 0.5f,
            Frequency = frequency
        };
        _tunnels = new FastNoiseLite
        {
            Seed = seed + 19,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 3,
            FractalLacunarity = 2.1f,
            FractalGain = 0.45f,
            Frequency = frequency * 1.35f
        };
        _warp = new FastNoiseLite
        {
            Seed = seed + 41,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 2,
            FractalLacunarity = 2f,
            FractalGain = 0.5f,
            Frequency = frequency * 0.55f
        };
    }

    public float Amplitude { get; }
    public float CrustHeight { get; }
    public float FloorThickness { get; }
    public float CeilingThickness { get; }
    public float CavernThreshold { get; }
    public float TunnelWidth { get; }
    public float CavernYScale { get; }
    public float FloorTop => FloorThickness;
    public float CeilingBottom => CrustHeight - CeilingThickness;

    public float Sample(Vector3 p)
    {
        float slab = Mathf.Max(-p.Y, p.Y - CrustHeight);
        float interior = InteriorWeight(p.Y);
        if (interior <= 0f)
        {
            return PreserveShell(slab, p);
        }

        float y = p.Y * CavernYScale;
        float wx = _warp.GetNoise3D(p.X, y, p.Z) * _warpAmp;
        float wz = _warp.GetNoise3D(p.X + 37.2f, y, p.Z + 18.4f) * _warpAmp;
        float qx = p.X + wx;
        float qz = p.Z + wz;

        float chambers = _caverns.GetNoise3D(qx, y, qz) * Amplitude - CavernThreshold;
        float tunnels = TunnelWidth - Mathf.Abs(_tunnels.GetNoise3D(qx, y, qz));
        float opening = Mathf.Max(chambers, tunnels) * interior;
        return PreserveShell(Mathf.Max(slab, opening), p);
    }

    /// <summary>
    /// Keeps the walkable floor after a carve. Removed crust is not restored.
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

    public float CeilingSdf(Vector3 p)
    {
        return Mathf.Max(CeilingBottom - p.Y, p.Y - CrustHeight);
    }

    private float PreserveShell(float distance, Vector3 p)
    {
        float d = Mathf.Min(distance, FloorSdf(p));
        d = Mathf.Min(d, CeilingSdf(p));
        d = Mathf.Max(d, -p.Y);
        d = Mathf.Max(d, p.Y - CrustHeight);
        return d;
    }

    private float InteriorWeight(float y)
    {
        float fromFloor = Mathf.Clamp((y - FloorThickness) / _interiorFade, 0f, 1f);
        float fromCeiling = Mathf.Clamp((CeilingBottom - y) / _interiorFade, 0f, 1f);
        return fromFloor * fromCeiling;
    }
}
