using Godot;

namespace Wallbreaker;

/// <summary>
/// Debug-only dig: hold left mouse or a finger and paint over the terrain.
/// Off by default now that weapons carve; re-enable on the node to paint again.
/// </summary>
public partial class DebugDigController : Node3D
{
    [Export] public bool Enabled { get; set; } = false;
    [Export] public float BrushRadius { get; set; } = 0.75f;
    [Export] public float StampSpacing { get; set; } = 0.35f;

    private TerrainWorld? _terrain;
    private Camera3D? _camera;
    private bool _held;
    private bool _heldByTouch;
    private Vector2 _screenPos;
    private bool _hasStamp;
    private Vector3 _lastStamp;

    public override void _Ready()
    {
        Node? world = GetParent();
        _terrain = world?.GetNodeOrNull<TerrainWorld>("TerrainWorld");
        _camera = world?.GetNodeOrNull<Camera3D>("CameraRig/Camera3D");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Enabled)
        {
            return;
        }

        switch (@event)
        {
            case InputEventScreenTouch touch when touch.Index == 0:
                _screenPos = touch.Position;
                SetHeld(touch.Pressed, fromTouch: true);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventScreenDrag drag when drag.Index == 0 && _held:
                _screenPos = drag.Position;
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Left && !_heldByTouch:
                _screenPos = mouse.Position;
                SetHeld(mouse.Pressed, fromTouch: false);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseMotion motion when _held && !_heldByTouch:
                _screenPos = motion.Position;
                break;
        }
    }

    public override void _Process(double _delta)
    {
        if (!Enabled || !_held || _terrain == null || _camera == null)
        {
            return;
        }

        Vector2 screenPos = _heldByTouch ? _screenPos : GetViewport().GetMousePosition();
        if (!TryHit(screenPos, out Vector3 hit))
        {
            _hasStamp = false;
            return;
        }

        StampStroke(hit);
        _terrain.CommitCarve();
    }

    private void SetHeld(bool pressed, bool fromTouch)
    {
        _held = pressed;
        _heldByTouch = pressed && fromTouch;
        if (!pressed)
        {
            _hasStamp = false;
        }
    }

    private void StampStroke(Vector3 hit)
    {
        if (_terrain == null)
        {
            return;
        }

        if (!_hasStamp)
        {
            _terrain.Carve(hit, BrushRadius);
            _lastStamp = hit;
            _hasStamp = true;
            return;
        }

        float spacing = Mathf.Max(StampSpacing, BrushRadius * 0.35f);
        float distance = new Vector2(_lastStamp.X - hit.X, _lastStamp.Z - hit.Z).Length();
        if (distance < 0.0001f)
        {
            _terrain.Carve(hit, BrushRadius);
            _lastStamp = hit;
            return;
        }

        int steps = Mathf.Clamp(Mathf.CeilToInt(distance / spacing), 1, 24);
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            _terrain.Carve(_lastStamp.Lerp(hit, t), BrushRadius);
        }

        _lastStamp = hit;
    }

    private bool TryHit(Vector2 screenPos, out Vector3 point)
    {
        point = default;
        if (_terrain == null || _camera == null)
        {
            return false;
        }

        Vector3 from = _camera.ProjectRayOrigin(screenPos);
        Vector3 dir = _camera.ProjectRayNormal(screenPos);
        if (dir.LengthSquared() < 1e-8f)
        {
            return false;
        }

        Node3D? player = GetParent()?.GetNodeOrNull<Node3D>("Player");
        float headY = (player?.GlobalPosition.Y ?? _terrain.FloorTop) + 1.7f;
        return _terrain.RaycastFromView(from, dir, 500f, headY, out point, out Vector3 _);
    }
}
