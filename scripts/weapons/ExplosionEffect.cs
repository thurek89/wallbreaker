using Godot;

namespace Wallbreaker;

public partial class ExplosionEffect : Node3D
{
    public override void _Ready()
    {
        var core = GetNode<MeshInstance3D>("Core");
        var wave = GetNode<MeshInstance3D>("Shockwave");
        var light = GetNode<OmniLight3D>("Light");

        StandardMaterial3D coreMat = DuplicateMat(core);
        StandardMaterial3D waveMat = DuplicateMat(wave);

        core.Scale = Vector3.One * 0.18f;
        wave.Scale = new Vector3(0.25f, 1f, 0.25f);
        light.LightEnergy = 5.5f;

        Color coreEnd = new(coreMat.AlbedoColor.R, coreMat.AlbedoColor.G, coreMat.AlbedoColor.B, 0f);
        Color waveEnd = new(waveMat.AlbedoColor.R, waveMat.AlbedoColor.G, waveMat.AlbedoColor.B, 0f);

        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(core, "scale", Vector3.One * 1.7f, 0.22f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(wave, "scale", new Vector3(2.6f, 1f, 2.6f), 0.34f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(light, "light_energy", 0f, 0.34f);
        tween.TweenProperty(coreMat, "albedo_color", coreEnd, 0.34f);
        tween.TweenProperty(waveMat, "albedo_color", waveEnd, 0.34f);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }

    private static StandardMaterial3D DuplicateMat(MeshInstance3D mesh)
    {
        var mat = (StandardMaterial3D)mesh.GetActiveMaterial(0)!.Duplicate();
        mesh.SetSurfaceOverrideMaterial(0, mat);
        return mat;
    }
}
