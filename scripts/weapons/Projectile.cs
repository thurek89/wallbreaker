using Godot;

namespace Wallbreaker;

public partial class Projectile : Node3D
{
    [Export] public float Speed { get; set; } = 18f;
    [Export] public float MaxLifetime { get; set; } = 2.5f;

    protected TerrainWorld? Terrain;

    private Vector3 _velocity;
    private float _alive;

    public void Launch(Vector3 origin, Vector3 direction, TerrainWorld terrain)
    {
        Terrain = terrain;
        GlobalPosition = origin;
        Vector3 dir = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector3.Forward;
        _velocity = dir * Speed;
        AlignToVelocity();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _alive += dt;
        if (_alive >= MaxLifetime)
        {
            QueueFree();
            return;
        }

        Vector3 from = GlobalPosition;
        Vector3 motion = _velocity * dt;
        float distance = motion.Length();
        if (distance > 1e-6f
            && Terrain != null
            && Terrain.Raycast(from, motion / distance, distance, out Vector3 hit, out Vector3 normal))
        {
            OnHit(hit, normal);
            return;
        }

        GlobalPosition = from + motion;
        AlignToVelocity();
    }

    protected virtual void OnHit(Vector3 point, Vector3 normal)
    {
        QueueFree();
    }

    private void AlignToVelocity()
    {
        if (_velocity.LengthSquared() < 0.0001f)
        {
            return;
        }

        Vector3 ahead = GlobalPosition + _velocity;
        if (ahead.IsEqualApprox(GlobalPosition))
        {
            return;
        }

        LookAt(ahead, Vector3.Up);
    }
}
