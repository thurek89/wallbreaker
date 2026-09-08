using Godot;

namespace Wallbreaker;

/// <summary>
/// Key light offset from the isometric camera so walls and dug rooms throw readable shadows.
/// </summary>
public partial class SunLight : DirectionalLight3D
{
    [Export] public Vector3 SunRotationDegrees { get; set; } = new(-35f, 80f, 0f);
    [Export] public float MaxShadowDistance { get; set; } = 50f;

    public override void _Ready()
    {
        RotationDegrees = SunRotationDegrees;
        LightEnergy = 1.55f;
        LightColor = new Color(1f, 0.96f, 0.88f);
        LightSpecular = 0.35f;
        ShadowEnabled = true;
        ShadowBias = 0.04f;
        ShadowNormalBias = 1.0f;
        ShadowBlur = 1.0f;
        ShadowOpacity = 0.9f;
        DirectionalShadowMode = ShadowMode.Orthogonal;
        DirectionalShadowMaxDistance = MaxShadowDistance;
        DirectionalShadowPancakeSize = 8f;
    }
}
