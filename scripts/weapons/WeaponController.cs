using Godot;

namespace Wallbreaker;

public partial class WeaponController : Node3D
{
    [Export] public PackedScene? BulletScene { get; set; }
    [Export] public PackedScene? RocketScene { get; set; }
    [Export] public float MachineGunInterval { get; set; } = 0.09f;
    [Export] public float RocketInterval { get; set; } = 0.7f;
    [Export] public WeaponId Current { get; set; } = WeaponId.MachineGun;

    private Camera3D? _camera;
    private TerrainWorld? _terrain;
    private Node? _host;
    private Marker3D _muzzle = null!;
    private MeshInstance3D _machineGun = null!;
    private MeshInstance3D _rocketLauncher = null!;
    private float _cooldown;

    public override void _Ready()
    {
        _muzzle = GetNode<Marker3D>("Muzzle");
        _machineGun = GetNode<MeshInstance3D>("MachineGun");
        _rocketLauncher = GetNode<MeshInstance3D>("RocketLauncher");

        Node? world = GetParent()?.GetParent();
        _host = world;
        _terrain = world?.GetNodeOrNull<TerrainWorld>("TerrainWorld");
        _camera = world?.GetNodeOrNull<Camera3D>("CameraRig/Camera3D");
        ApplyWeapon();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouse || !mouse.Pressed)
        {
            return;
        }

        if (mouse.ButtonIndex == MouseButton.WheelUp)
        {
            Cycle(-1);
            GetViewport().SetInputAsHandled();
        }
        else if (mouse.ButtonIndex == MouseButton.WheelDown)
        {
            Cycle(1);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _cooldown = Mathf.Max(0f, _cooldown - (float)delta);
        AimAtCursor();

        if (!Input.IsActionPressed("fire") || _terrain == null || _host == null)
        {
            return;
        }

        if (_cooldown > 0f)
        {
            return;
        }

        Fire();
    }

    private void Cycle(int step)
    {
        int count = Enum.GetValues<WeaponId>().Length;
        int next = ((int)Current + step) % count;
        if (next < 0)
        {
            next += count;
        }

        Current = (WeaponId)next;
        ApplyWeapon();
    }

    private void ApplyWeapon()
    {
        _machineGun.Visible = Current == WeaponId.MachineGun;
        _rocketLauncher.Visible = Current == WeaponId.RocketLauncher;
        _cooldown = 0f;
    }

    private void AimAtCursor()
    {
        if (!TryGetAimPoint(out Vector3 aim))
        {
            return;
        }

        Vector3 look = new(aim.X, GlobalPosition.Y, aim.Z);
        if (look.DistanceSquaredTo(GlobalPosition) < 0.0025f)
        {
            return;
        }

        LookAt(look, Vector3.Up);
    }

    private void Fire()
    {
        PackedScene? scene = Current == WeaponId.MachineGun ? BulletScene : RocketScene;
        if (scene == null || _terrain == null || _host == null)
        {
            return;
        }

        Vector3 origin = _muzzle.GlobalPosition;
        Vector3 dir = FlatAimDirection(origin);
        var projectile = scene.Instantiate<Projectile>();
        _host.AddChild(projectile);
        projectile.Launch(origin, dir, _terrain);
        _cooldown = Current == WeaponId.MachineGun ? MachineGunInterval : RocketInterval;
    }

    private Vector3 FlatAimDirection(Vector3 origin)
    {
        if (TryGetAimPoint(out Vector3 aim))
        {
            Vector3 flat = new(aim.X - origin.X, 0f, aim.Z - origin.Z);
            if (flat.LengthSquared() > 0.04f)
            {
                return flat.Normalized();
            }
        }

        if (_camera != null)
        {
            Vector3 forward = _camera.GlobalBasis.Z;
            forward.Y = 0f;
            if (forward.LengthSquared() > 0.0001f)
            {
                return -forward.Normalized();
            }
        }

        return Vector3.Forward;
    }

    private bool TryGetAimPoint(out Vector3 point)
    {
        point = default;
        if (_camera == null)
        {
            return false;
        }

        Vector2 mouse = GetViewport().GetMousePosition();
        Vector3 from = _camera.ProjectRayOrigin(mouse);
        Vector3 dir = _camera.ProjectRayNormal(mouse);
        if (dir.LengthSquared() < 1e-8f)
        {
            return false;
        }

        if (_terrain != null && _terrain.Raycast(from, dir, 500f, out point, out Vector3 _))
        {
            return true;
        }

        float floorY = _terrain?.FloorTop ?? 0.45f;
        if (Mathf.Abs(dir.Y) < 1e-5f)
        {
            return false;
        }

        float t = (floorY - from.Y) / dir.Y;
        if (t <= 0f)
        {
            return false;
        }

        point = from + dir * t;
        return true;
    }
}
