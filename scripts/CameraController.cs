using Godot;

namespace Wallbreaker;

public partial class CameraController : Node3D
{
    [Export] public float OrthoSize { get; set; } = 12f;
    [Export] public float CameraDistance { get; set; } = 40f;
    [Export] public float CameraFar { get; set; } = 80f;
    [Export] public float FollowLerp { get; set; } = 12f;

    private Camera3D _camera = null!;
    private Node3D? _follow;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        _follow = GetParent()?.GetNodeOrNull<Node3D>("Player");

        float pitch = Mathf.RadToDeg(Mathf.Atan(1f / Mathf.Sqrt(2f)));
        _camera.Projection = Camera3D.ProjectionType.Orthogonal;
        _camera.Size = OrthoSize;
        _camera.Near = 5f;
        _camera.Far = CameraFar;
        _camera.Current = true;
        _camera.RotationDegrees = new Vector3(-pitch, 45f, 0f);
        _camera.Position = _camera.Basis * new Vector3(0f, 0f, CameraDistance);

        SnapToFollow();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_follow == null)
        {
            return;
        }

        Vector3 target = new(_follow.GlobalPosition.X, 0f, _follow.GlobalPosition.Z);
        float t = 1f - Mathf.Exp(-FollowLerp * (float)delta);
        GlobalPosition = GlobalPosition.Lerp(target, t);
    }

    private void SnapToFollow()
    {
        if (_follow == null)
        {
            return;
        }

        GlobalPosition = new Vector3(_follow.GlobalPosition.X, 0f, _follow.GlobalPosition.Z);
    }
}
