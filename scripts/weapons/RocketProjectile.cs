using Godot;

namespace Wallbreaker;

public partial class RocketProjectile : Projectile
{
    [Export] public float BlastRadius { get; set; } = 1.0f;
    [Export] public PackedScene? ExplosionScene { get; set; }

    protected override void OnHit(Vector3 point, Vector3 normal)
    {
        Terrain?.Carve(point, BlastRadius);
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
