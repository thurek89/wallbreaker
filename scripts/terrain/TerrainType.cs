namespace Wallbreaker;

/// <summary>
/// Values double as vertex color channels (r, g, b, a) on the mesh, so the
/// order is load bearing: stone=R, dirt=G, ice=B, sand=A.
/// </summary>
public enum TerrainType
{
    Stone = 0,
    Dirt = 1,
    Ice = 2,
    Sand = 3
}
