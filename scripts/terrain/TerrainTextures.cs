using Godot;

namespace Wallbreaker;

/// <summary>
/// Placeholder terrain art. Each TerrainType has an albedo and a normal map
/// under texture/, and the terrain material has a slot per map.
/// </summary>
public static class TerrainTextures
{
    public const int Size = 256;

    private static readonly string[] Names = ["stone", "dirt", "ice", "sand"];

    private readonly record struct Recipe(
        Color Dark,
        Color Light,
        int Period,
        int Octaves,
        float Contrast,
        float Speckle,
        float Bump,
        int Seed);

    private static readonly Recipe[] Recipes =
    [
        new(new Color(0.32f, 0.31f, 0.30f), new Color(0.64f, 0.63f, 0.61f), 8, 5, 1.10f, 0.10f, 14f, 11),
        new(new Color(0.22f, 0.15f, 0.09f), new Color(0.51f, 0.38f, 0.25f), 10, 5, 1.20f, 0.14f, 18f, 23),
        new(new Color(0.58f, 0.74f, 0.86f), new Color(0.92f, 0.97f, 1.00f), 5, 4, 0.75f, 0.04f, 5f, 37),
        new(new Color(0.60f, 0.49f, 0.30f), new Color(0.88f, 0.78f, 0.55f), 16, 4, 0.85f, 0.16f, 11f, 51)
    ];

    public static void ApplyDefaults(ShaderMaterial material)
    {
        foreach (string name in Names)
        {
            material.SetShaderParameter($"{name}_albedo", GD.Load<Texture2D>($"res://texture/{name}_albedo.png"));
            material.SetShaderParameter($"{name}_normal", GD.Load<Texture2D>($"res://texture/{name}_normal.png"));
        }
    }

    /// <summary>
    /// Regenerates every placeholder map on disk. Run once to author the assets;
    /// they are checked in afterwards and meant to be replaced with real art.
    /// </summary>
    public static void BakePlaceholders()
    {
        for (int i = 0; i < Recipes.Length; i++)
        {
            float[] height = BakeHeight(Recipes[i]);
            Save(BakeAlbedo(Recipes[i], height), $"res://texture/{Names[i]}_albedo.png");
            Save(BakeNormal(Recipes[i], height), $"res://texture/{Names[i]}_normal.png");
        }
    }

    private static void Save(Image image, string resPath)
    {
        Error error = image.SavePng(ProjectSettings.GlobalizePath(resPath));
        if (error != Error.Ok)
        {
            GD.PushError($"Could not write {resPath}: {error}");
        }
    }

    /// <summary>
    /// Smooth height only. The albedo adds per pixel speckle on top, but the
    /// normals must not see it: single pixel noise produces huge gradients that
    /// read as sparkle once the surface is lit.
    /// </summary>
    private static float[] BakeHeight(Recipe recipe)
    {
        var height = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float n = Fbm((float)x / Size, (float)y / Size, recipe.Period, recipe.Octaves, recipe.Seed);
                height[(y * Size) + x] = Mathf.Clamp(((n - 0.5f) * recipe.Contrast) + 0.5f, 0f, 1f);
            }
        }

        return height;
    }

    private static Image BakeAlbedo(Recipe recipe, float[] height)
    {
        var data = new byte[Size * Size * 3];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int i = (y * Size) + x;
                float speckle = (Hash(x, y, recipe.Seed + 977) - 0.5f) * recipe.Speckle;
                Color color = recipe.Dark.Lerp(recipe.Light, Mathf.Clamp(height[i] + speckle, 0f, 1f));
                data[i * 3] = Byte(color.R);
                data[(i * 3) + 1] = Byte(color.G);
                data[(i * 3) + 2] = Byte(color.B);
            }
        }

        return Image.CreateFromData(Size, Size, false, Image.Format.Rgb8, data);
    }

    /// <summary>
    /// Tangent space normals from the height field, wrapping at the edges so the
    /// map tiles the same way the albedo does.
    /// </summary>
    private static Image BakeNormal(Recipe recipe, float[] height)
    {
        var data = new byte[Size * Size * 3];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float left = height[(y * Size) + Wrap(x - 1, Size)];
                float right = height[(y * Size) + Wrap(x + 1, Size)];
                float up = height[(Wrap(y - 1, Size) * Size) + x];
                float down = height[(Wrap(y + 1, Size) * Size) + x];
                var normal = new Vector3((left - right) * recipe.Bump, (up - down) * recipe.Bump, 1f).Normalized();

                int i = ((y * Size) + x) * 3;
                data[i] = Byte((normal.X * 0.5f) + 0.5f);
                data[i + 1] = Byte((normal.Y * 0.5f) + 0.5f);
                data[i + 2] = Byte((normal.Z * 0.5f) + 0.5f);
            }
        }

        return Image.CreateFromData(Size, Size, false, Image.Format.Rgb8, data);
    }

    private static float Fbm(float u, float v, int period, int octaves, int seed)
    {
        float sum = 0f;
        float amplitude = 0.5f;
        float total = 0f;
        int cells = period;
        for (int i = 0; i < octaves; i++)
        {
            sum += ValueNoise(u * cells, v * cells, cells, seed + (i * 131)) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            cells *= 2;
        }

        return sum / Mathf.Max(total, 1e-4f);
    }

    /// <summary>
    /// Value noise on a lattice that wraps at <paramref name="cells"/>, so every
    /// octave tiles across the texture and the result repeats seamlessly.
    /// </summary>
    private static float ValueNoise(float x, float y, int cells, int seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float tx = Smooth(x - x0);
        float ty = Smooth(y - y0);

        float c00 = Hash(Wrap(x0, cells), Wrap(y0, cells), seed);
        float c10 = Hash(Wrap(x0 + 1, cells), Wrap(y0, cells), seed);
        float c01 = Hash(Wrap(x0, cells), Wrap(y0 + 1, cells), seed);
        float c11 = Hash(Wrap(x0 + 1, cells), Wrap(y0 + 1, cells), seed);
        return Mathf.Lerp(Mathf.Lerp(c00, c10, tx), Mathf.Lerp(c01, c11, tx), ty);
    }

    private static float Smooth(float t)
    {
        return t * t * (3f - (2f * t));
    }

    private static int Wrap(int v, int period)
    {
        return ((v % period) + period) % period;
    }

    private static float Hash(int x, int y, int seed)
    {
        int h = (x * 374761393) + (y * 668265263) + (seed * 1274126177);
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
    }

    private static byte Byte(float v)
    {
        return (byte)Mathf.RoundToInt(Mathf.Clamp(v, 0f, 1f) * 255f);
    }
}
