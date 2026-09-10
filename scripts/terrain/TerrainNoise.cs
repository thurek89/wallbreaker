using Godot;

namespace Wallbreaker;

/// <summary>
/// Standalone terrain noise recipe: cavern, tunnel, and warp layers inside a playable Y band.
/// </summary>
[GlobalClass]
[Tool]
public partial class TerrainNoise : Resource
{
    [ExportGroup("Playable Band")]
    [Export] public float BandMinY { get; set; } = 0f;
    [Export] public float CrustHeight { get; set; } = 5f;
    [Export] public float FloorThickness { get; set; } = 0.45f;

    [ExportGroup("Caverns")]
    [Export] public FastNoiseLite Caverns { get; set; } = MakeNoise(17, 0.07f, 2, 2f, 0.4f);
    [Export] public float Amplitude { get; set; } = 0.9f;
    [Export] public float Threshold { get; set; } = 0.1f;
    [Export] public float YScale { get; set; } = 0.85f;

    [ExportGroup("Tunnels")]
    [Export] public FastNoiseLite Tunnels { get; set; } = MakeNoise(36, 0.055f, 2, 2f, 0.35f);
    [Export] public float TunnelWidth { get; set; } = 0.38f;

    [ExportGroup("Warp")]
    [Export] public FastNoiseLite Warp { get; set; } = MakeNoise(58, 0.04f, 1, 2f, 0.35f);
    [Export] public float WarpStrength { get; set; } = 0.14f;

    [ExportGroup("Cleanup")]
    [Export] public float AirPadding { get; set; } = 0.08f;

    [ExportGroup("Terrain Types")]
    [Export] public FastNoiseLite TypeBands { get; set; } = MakeNoise(83, 0.045f, 2, 2f, 0.45f);
    /// <summary>How far the sand/dirt/stone boundaries bend away from flat, as a fraction of the crust.</summary>
    [Export] public float BandWobble { get; set; } = 0.18f;
    /// <summary>Depth below the crust top where sand gives way to dirt, 0..1.</summary>
    [Export] public float SandDepth { get; set; } = 0.24f;
    /// <summary>Depth below the crust top where dirt gives way to stone, 0..1.</summary>
    [Export] public float DirtDepth { get; set; } = 0.58f;
    [Export] public float BandBlend { get; set; } = 0.12f;
    [Export] public FastNoiseLite IceVeins { get; set; } = MakeNoise(104, 0.06f, 3, 2f, 0.4f);
    [Export] public float IceThreshold { get; set; } = 0.62f;
    [Export] public float IceBlend { get; set; } = 0.07f;

    public TerrainNoise()
    {
        EnsureLayers();
    }

    public void EnsureLayers()
    {
        Caverns ??= MakeNoise(17, 0.07f, 2, 2f, 0.4f);
        Tunnels ??= MakeNoise(36, 0.055f, 2, 2f, 0.35f);
        Warp ??= MakeNoise(58, 0.04f, 1, 2f, 0.35f);
        TypeBands ??= MakeNoise(83, 0.045f, 2, 2f, 0.45f);
        IceVeins ??= MakeNoise(104, 0.06f, 3, 2f, 0.4f);
    }

    public int ContentHash()
    {
        EnsureLayers();
        HashCode hash = new();
        hash.Add(BandMinY);
        hash.Add(CrustHeight);
        hash.Add(FloorThickness);
        hash.Add(Amplitude);
        hash.Add(Threshold);
        hash.Add(YScale);
        hash.Add(TunnelWidth);
        hash.Add(WarpStrength);
        hash.Add(AirPadding);
        hash.Add(BandWobble);
        hash.Add(SandDepth);
        hash.Add(DirtDepth);
        hash.Add(BandBlend);
        hash.Add(IceThreshold);
        hash.Add(IceBlend);
        AddNoise(ref hash, Caverns);
        AddNoise(ref hash, Tunnels);
        AddNoise(ref hash, Warp);
        AddNoise(ref hash, TypeBands);
        AddNoise(ref hash, IceVeins);
        return hash.ToHashCode();
    }

    private static void AddNoise(ref HashCode hash, FastNoiseLite noise)
    {
        hash.Add(noise.Seed);
        hash.Add(noise.Frequency);
        hash.Add((int)noise.NoiseType);
        hash.Add((int)noise.FractalType);
        hash.Add(noise.FractalOctaves);
        hash.Add(noise.FractalLacunarity);
        hash.Add(noise.FractalGain);
        hash.Add(noise.Offset);
    }

    private static FastNoiseLite MakeNoise(int seed, float frequency, int octaves, float lacunarity, float gain)
    {
        return new FastNoiseLite
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = octaves,
            FractalLacunarity = lacunarity,
            FractalGain = gain,
            Frequency = frequency
        };
    }
}
