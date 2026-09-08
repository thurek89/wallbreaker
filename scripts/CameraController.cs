using Godot;

namespace Wallbreaker;

public partial class CameraController : Node3D
{
    [Export] public float PanSpeed { get; set; } = 10f;
    [Export] public float BoundsMargin { get; set; } = 0.5f;
    [Export] public float OrthoSize { get; set; } = 12f;
    [Export] public float CameraDistance { get; set; } = 40f;

    private Camera3D _camera = null!;
    private TerrainWorld? _terrain;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        _terrain = GetParent()?.GetNodeOrNull<TerrainWorld>("TerrainWorld");

        float pitch = Mathf.RadToDeg(Mathf.Atan(1f / Mathf.Sqrt(2f)));
        _camera.Projection = Camera3D.ProjectionType.Orthogonal;
        _camera.Size = OrthoSize;
        _camera.Near = 5f;
        _camera.Far = 80f;
        _camera.Current = true;
        _camera.RotationDegrees = new Vector3(-pitch, 45f, 0f);
        _camera.Position = _camera.Basis * new Vector3(0f, 0f, CameraDistance);

        Aabb bounds = GetWorldBounds();
        Vector3 center = bounds.GetCenter();
        GlobalPosition = new Vector3(center.X, 0f, center.Z);
    }

    public override void _Process(double delta)
    {
        Vector2 input = Input.GetVector("camera_left", "camera_right", "camera_back", "camera_forward");
        if (input == Vector2.Zero)
        {
            return;
        }

        Vector3 right = FlattenOntoXz(_camera.GlobalBasis.X);
        Vector3 forward = FlattenOntoXz(-_camera.GlobalBasis.Z);
        Vector3 motion = (right * input.X + forward * input.Y) * PanSpeed * (float)delta;

        Vector3 next = GlobalPosition + motion;
        Aabb bounds = GetWorldBounds().Grow(BoundsMargin);
        next.X = Mathf.Clamp(next.X, bounds.Position.X, bounds.End.X);
        next.Y = 0f;
        next.Z = Mathf.Clamp(next.Z, bounds.Position.Z, bounds.End.Z);
        GlobalPosition = next;
    }

    private Aabb GetWorldBounds()
    {
        return _terrain?.GetBounds() ?? new Aabb(Vector3.Zero, new Vector3(10f, 5f, 10f));
    }

    private static Vector3 FlattenOntoXz(Vector3 axis)
    {
        axis.Y = 0f;
        return axis.LengthSquared() > 0.0001f ? axis.Normalized() : Vector3.Zero;
    }
}
