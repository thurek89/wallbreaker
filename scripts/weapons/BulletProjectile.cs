using Godot;

namespace Wallbreaker;

public partial class BulletProjectile : Projectile
{
    [Export] public float ChipRadius { get; set; } = 0.16f;
    [Export] public float ChipDepthBias { get; set; } = 0.45f;

    protected override void OnHit(Vector3 point, Vector3 normal)
    {
        Vector3 center = point - normal * ChipRadius * ChipDepthBias;
        Terrain?.Carve(center, ChipRadius);
        Terrain?.CommitCarve();
        QueueFree();
    }
}
