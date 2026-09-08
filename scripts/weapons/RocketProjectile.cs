using Godot;

namespace Wallbreaker;

public partial class RocketProjectile : Projectile
{
    [Export] public float BlastRadius { get; set; } = 1.2f;
    [Export] public float BlastDepthBias { get; set; } = 0.5f;
    [Export] public PackedScene? ExplosionScene { get; set; }

    protected override void OnHit(Vector3 point, Vector3 normal)
    {
        Vector3 center = point - normal * BlastRadius * BlastDepthBias;
        Terrain?.Carve(center, BlastRadius);
        Terrain?.CommitCarve();
        SpawnExplosion(point);
        QueueFree();
    }

    private void SpawnExplosion(Vector3 point)
    {
        if (ExplosionScene == null)
        {
            return;
        }

        Node3D fx = ExplosionScene.Instantiate<Node3D>();
        GetParent()?.AddChild(fx);
        fx.GlobalPosition = point;
    }
}
