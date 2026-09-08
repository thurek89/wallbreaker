using Godot;

namespace Wallbreaker;

/// <summary>
/// Signed distance for earth inside a playable Y band.
/// Caves may open through the top; solids that reach the cap get a top face.
/// GroundY/CapY are the hooks for future slopes and stacked levels.
/// Negative values are solid.
/// </summary>
public sealed class SdfSampler
{
    private readonly FastNoiseLite _caverns;
    private readonly FastNoiseLite _tunnels;
    private readonly FastNoiseLite _warp;
    private readonly float _interiorFade;
    private readonly float _warpAmp;

    public SdfSampler(TerrainNoise noise)
    {
        noise.EnsureLayers();
        Amplitude = Mathf.Max(noise.Amplitude, 0.01f);
        BandMinY = noise.BandMinY;
        CrustHeight = Mathf.Max(noise.CrustHeight, 0.05f);
        FloorThickness = Mathf.Clamp(noise.FloorThickness, 0.05f, CrustHeight * 0.45f);
        Threshold = noise.Threshold;
        TunnelWidth = Mathf.Max(noise.TunnelWidth, 0f);
        YScale = Mathf.Max(noise.YScale, 0.05f);
        AirPadding = Mathf.Max(noise.AirPadding, 0f);
        _interiorFade = Mathf.Max(0.25f, FloorThickness);
        _caverns = noise.Caverns;
        _tunnels = noise.Tunnels;
        _warp = noise.Warp;
        float frequency = Mathf.Max(_caverns.Frequency, 0.001f);
        _warpAmp = noise.WarpStrength / frequency;
    }

    public float Amplitude { get; }
    public float BandMinY { get; }
    public float CrustHeight { get; }
    public float BandMaxY => BandMinY + CrustHeight;
    public float FloorThickness { get; }
    public float Threshold { get; }
    public float TunnelWidth { get; }
    public float YScale { get; }
    public float AirPadding { get; }
    public float FloorTop => BandMinY + FloorThickness;

    public float GroundY(Vector3 p)
    {
        return BandMinY;
    }

    public float CapY(Vector3 p)
    {
        return BandMaxY;
    }

    public float Sample(Vector3 p)
    {
        float earth = EarthField(p);
        return ClipToBand(UnionFloor(earth, p), p);
    }

    /// <summary>
    /// Keeps the walkable floor after a carve. Caps are not restored.
    /// </summary>
    public float PreserveFloor(float distance, Vector3 p)
    {
        return ClipToBand(UnionFloor(distance, p), p);
    }

    public float FloorSdf(Vector3 p)
    {
        float y0 = GroundY(p);
        float y1 = y0 + FloorThickness;
        return Mathf.Max(y0 - p.Y, p.Y - y1);
    }

    private float EarthField(Vector3 p)
    {
        float slab = Mathf.Max(GroundY(p) - p.Y, p.Y - CapY(p));
        float interior = InteriorWeight(p);
        if (interior <= 0f)
        {
            return slab;
        }

        float y = p.Y * YScale;
        float wx = _warp.GetNoise3D(p.X, y, p.Z) * _warpAmp;
        float wz = _warp.GetNoise3D(p.X + 37.2f, y, p.Z + 18.4f) * _warpAmp;
        float qx = p.X + wx;
        float qz = p.Z + wz;

        float chambers = _caverns.GetNoise3D(qx, y, qz) * Amplitude - Threshold;
        float tunnels = TunnelWidth - Mathf.Abs(_tunnels.GetNoise3D(qx, y, qz));
        float opening = (Mathf.Max(chambers, tunnels) + AirPadding) * interior;
        return Mathf.Max(slab, opening);
    }

    private float UnionFloor(float distance, Vector3 p)
    {
        return Mathf.Min(distance, FloorSdf(p));
    }

    private float ClipToBand(float distance, Vector3 p)
    {
        distance = Mathf.Max(distance, GroundY(p) - p.Y);
        distance = Mathf.Max(distance, p.Y - CapY(p));
        return distance;
    }

    private float InteriorWeight(Vector3 p)
    {
        float floorTop = GroundY(p) + FloorThickness;
        return Mathf.Clamp((p.Y - floorTop) / _interiorFade, 0f, 1f);
    }
}
