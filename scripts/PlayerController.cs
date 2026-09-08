using Godot;

namespace Wallbreaker;

public partial class PlayerController : CharacterBody3D
{
    [Export] public float MoveSpeed { get; set; } = 4.5f;

    private Camera3D? _camera;
    private TerrainWorld? _terrain;

    public override void _Ready()
    {
        FloorSnapLength = 0.25f;
        Node? world = GetParent();
        _terrain = world?.GetNodeOrNull<TerrainWorld>("TerrainWorld");
        _camera = world?.GetNodeOrNull<Camera3D>("CameraRig/Camera3D");

        if (_terrain != null)
        {
            GlobalPosition = _terrain.GetSpawnPoint();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 velocity = Velocity;
        float dt = (float)delta;

        if (!IsOnFloor())
        {
            velocity += GetGravity() * dt;
        }
        else if (velocity.Y < 0f)
        {
            velocity.Y = 0f;
        }

        Vector2 input = Input.GetVector("move_left", "move_right", "move_back", "move_forward");
        Vector3 wish = ScreenRelativeFlat(input);
        Vector3 planar = wish.LengthSquared() > 0.0001f ? wish.Normalized() * MoveSpeed : Vector3.Zero;
        velocity.X = planar.X;
        velocity.Z = planar.Z;

        Velocity = velocity;
        MoveAndSlide();
    }

    private Vector3 ScreenRelativeFlat(Vector2 input)
    {
        if (input == Vector2.Zero)
        {
            return Vector3.Zero;
        }

        if (_camera == null)
        {
            return new Vector3(input.X, 0f, -input.Y);
        }

        Vector3 right = Flatten(_camera.GlobalBasis.X);
        Vector3 forward = Flatten(-_camera.GlobalBasis.Z);
        return right * input.X + forward * input.Y;
    }

    private static Vector3 Flatten(Vector3 axis)
    {
        axis.Y = 0f;
        return axis.LengthSquared() > 0.0001f ? axis.Normalized() : Vector3.Zero;
    }
}
