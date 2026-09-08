using Godot;

namespace Wallbreaker;

public partial class PlayerController : CharacterBody3D
{
    [Export] public float MoveSpeed { get; set; } = 4.5f;

    private const float StepHeightFactor = 0.25f;
    private const float JumpHeightFactor = 0.5f;
    private const float MinStep = 0.02f;

    private Camera3D? _camera;
    private TerrainWorld? _terrain;
    private float _bodyHeight = 1.6f;
    private float _maxStepHeight = 0.4f;
    private float _jumpSpeed;
    private readonly KinematicCollision3D _blockHit = new();
    private readonly KinematicCollision3D _upHit = new();
    private readonly KinematicCollision3D _downHit = new();

    public override void _Ready()
    {
        _bodyHeight = ResolveBodyHeight();
        _maxStepHeight = _bodyHeight * StepHeightFactor;
        FloorSnapLength = _maxStepHeight;
        FloorMaxAngle = Mathf.DegToRad(60f);

        float gravity = Mathf.Max(GetGravity().Length(), 9.8f);
        _jumpSpeed = Mathf.Sqrt(2f * gravity * _bodyHeight * JumpHeightFactor);

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
        bool onFloor = IsStandingOnFloor(velocity);
        bool jumping = false;

        if (onFloor && Input.IsActionJustPressed("jump"))
        {
            velocity.Y = _jumpSpeed;
            jumping = true;
        }
        else if (!onFloor)
        {
            velocity += GetGravity() * dt;
        }
        else if (velocity.Y < 0f)
        {
            velocity.Y = 0f;
        }

        FloorSnapLength = velocity.Y > 0.01f ? 0f : _maxStepHeight;

        Vector2 input = Input.GetVector("move_left", "move_right", "move_back", "move_forward");
        Vector3 wish = ScreenRelativeFlat(input);
        Vector3 planar = wish.LengthSquared() > 0.0001f ? wish.Normalized() * MoveSpeed : Vector3.Zero;
        velocity.X = planar.X;
        velocity.Z = planar.Z;

        if (onFloor && !jumping)
        {
            TryClimbStep(planar * dt);
        }

        Velocity = velocity;
        MoveAndSlide();
    }

    private bool IsStandingOnFloor(Vector3 velocity)
    {
        if (IsOnFloor())
        {
            return true;
        }

        if (velocity.Y > 0.01f)
        {
            return false;
        }

        return TestMove(GlobalTransform, Vector3.Down * Mathf.Max(MinStep * 2f, _maxStepHeight * 0.15f));
    }

    private void TryClimbStep(Vector3 horizontalMotion)
    {
        horizontalMotion.Y = 0f;
        if (horizontalMotion.LengthSquared() < 1e-8f)
        {
            return;
        }

        if (!TestMove(GlobalTransform, horizontalMotion, _blockHit))
        {
            return;
        }

        if (_blockHit.GetNormal().AngleTo(UpDirection) <= FloorMaxAngle)
        {
            return;
        }

        float up = _maxStepHeight;
        if (TestMove(GlobalTransform, Vector3.Up * _maxStepHeight, _upHit))
        {
            up = _upHit.GetTravel().Length();
        }

        if (up < MinStep)
        {
            return;
        }

        Transform3D raised = GlobalTransform.Translated(Vector3.Up * up);
        if (TestMove(raised, horizontalMotion))
        {
            return;
        }

        Transform3D dest = raised.Translated(horizontalMotion);
        if (!TestMove(dest, Vector3.Down * up, _downHit))
        {
            return;
        }

        if (_downHit.GetNormal().AngleTo(UpDirection) > FloorMaxAngle)
        {
            return;
        }

        float climbed = up - _downHit.GetTravel().Length();
        if (climbed < MinStep || climbed > _maxStepHeight + 0.001f)
        {
            return;
        }

        GlobalPosition += Vector3.Up * climbed;
    }

    private float ResolveBodyHeight()
    {
        CollisionShape3D? shapeNode = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        return shapeNode?.Shape switch
        {
            CapsuleShape3D capsule => capsule.Height,
            BoxShape3D box => box.Size.Y,
            CylinderShape3D cylinder => cylinder.Height,
            _ => 1.6f
        };
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
