using Godot;

namespace Wallbreaker;

/// <summary>
/// World-space signed distance for a noisy crust sitting on the y=0 plane.
/// Negative values are solid.
/// </summary>
public sealed class SdfSampler
{
    private readonly FastNoiseLite _noise;

    public SdfSampler(int seed, float frequency, float amplitude, float crustHeight, float minThickness)
    {
        Amplitude = amplitude;
        CrustHeight = Mathf.Max(crustHeight, 0.01f);
        MinThickness = minThickness;

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
    public float MinThickness { get; }

    public float Sample(Vector3 p)
    {
        float n = _noise.GetNoise3D(p.X, p.Y * 0.65f, p.Z) * Amplitude;
        float envelope = p.Y / CrustHeight * 2f - 1f;
        float noisy = n + envelope;
        float baseCrust = Mathf.Max(-p.Y, p.Y - MinThickness);
        float d = Mathf.Min(noisy, baseCrust);
        return Mathf.Max(d, -p.Y);
    }

    public Vector3 Gradient(Vector3 p)
    {
        const float e = 0.04f;
        float dx = Sample(p + new Vector3(e, 0f, 0f)) - Sample(p - new Vector3(e, 0f, 0f));
        float dy = Sample(p + new Vector3(0f, e, 0f)) - Sample(p - new Vector3(0f, e, 0f));
        float dz = Sample(p + new Vector3(0f, 0f, e)) - Sample(p - new Vector3(0f, 0f, e));
        Vector3 g = new(dx, dy, dz);
        return g.LengthSquared() > 1e-8f ? g.Normalized() : Vector3.Up;
    }
}
