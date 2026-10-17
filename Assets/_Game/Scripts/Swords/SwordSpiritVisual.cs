using UnityEngine;

/// <summary>Three tapered ribbons: hot core, broad glow and a twisting filament, shared by both duelists.</summary>
[DisallowMultipleComponent]
public class SwordSpiritVisual : MonoBehaviour
{
    public Material ribbonMaterial;
    public Color tint = new Color(0.05f, 1f, 0.65f, 1f);
    private readonly TrailRenderer[] ribbons = new TrailRenderer[3];
    private FlyingSword sword;
    private EnemyBlade enemy;
    private Vector3 previous;
    private ParticleSystem motes;
    private float nextMote;
    private ParticleSystem wake;
    private float nextWake;

    private void Awake()
    {
        sword = GetComponent<FlyingSword>();
        enemy = GetComponent<EnemyBlade>();
        previous = transform.position;
        for (int i = 0; i < ribbons.Length; i++)
        {
            var go = new GameObject("LinhQuang_" + i);
            go.transform.SetParent(transform, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = ribbonMaterial;
            trail.minVertexDistance = 0.035f;
            trail.numCornerVertices = 3;
            trail.numCapVertices = 3;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.16f, 0.7f), new Keyframe(1, 0));
            var gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(i == 0 ? Color.Lerp(tint, Color.white, 0.8f) * 3f : tint * 2.1f, 0),
                new GradientColorKey(tint, 0.6f), new GradientColorKey(tint * 0.25f, 1) },
                new[] { new GradientAlphaKey(i == 1 ? 0.22f : 0.9f, 0), new GradientAlphaKey(0.35f, 0.35f), new GradientAlphaKey(0, 1) });
            trail.colorGradient = gradient;
            ribbons[i] = trail;
        }
        var particles = new GameObject("LinhQuang_TinhQuang");
        particles.transform.SetParent(transform, false);
        motes = particles.AddComponent<ParticleSystem>();
        var main = motes.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.55f;
        main.startSize = 0.035f;
        main.startSpeed = 0f;
        main.startColor = tint;
        main.maxParticles = 24;
        var emission = motes.emission; emission.enabled = false;
        var shape = motes.shape; shape.enabled = false;
        var renderer = motes.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = ribbonMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2f;
        renderer.velocityScale = 0.1f;
        var fade = motes.colorOverLifetime;
        fade.enabled = true;
        var fadeGradient = new Gradient();
        fadeGradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(tint, 1) },
            new[] { new GradientAlphaKey(0.8f, 0), new GradientAlphaKey(0, 1) });
        fade.color = fadeGradient;
        var particleLook = new MaterialPropertyBlock(); particleLook.SetFloat("_Mode", 3); renderer.SetPropertyBlock(particleLook);
        wake = TienHiepVfx.Particles(transform, "LinhQuang_KhiDuoi", ribbonMaterial,
            new Color(tint.r, tint.g, tint.b, 0.28f), 20, 0.55f, false);
    }

    private void LateUpdate()
    {
        float speed = (transform.position - previous).magnitude / Mathf.Max(0.001f, Time.deltaTime);
        previous = transform.position;
        bool attacking = sword != null ? sword.IsAttacking : enemy != null && !enemy.IsDone;
        if (attacking && Time.time >= nextWake)
        {
            nextWake = Time.time + 0.09f;
            TienHiepVfx.Emit(wake, transform.position - transform.forward * 0.25f,
                -transform.forward * 1.1f + Vector3.up * 0.2f, Random.Range(0.1f, 0.2f), 0.5f);
        }
        if (attacking && Time.time >= nextMote)
        {
            nextMote = Time.time + 0.06f;
            var emit = new ParticleSystem.EmitParams();
            emit.position = transform.position;
            emit.velocity = -transform.forward * 1.2f + transform.right * Mathf.Sin(Time.time * 29f);
            motes.Emit(emit, 1);
        }
        for (int i = 0; i < ribbons.Length; i++)
        {
            TrailRenderer t = ribbons[i];
            bool visible = ribbonMaterial != null && (attacking || speed > 1.2f);
            t.emitting = visible;
            t.time = attacking ? (i == 0 ? 0.34f : 0.65f) : 0.14f;
            t.widthMultiplier = attacking ? (i == 0 ? 0.028f : i == 1 ? 0.28f : 0.05f) : 0.018f;
            float angle = Time.time * 13f;
            t.transform.localPosition = i == 2 ? new Vector3(Mathf.Sin(angle) * 0.065f,
                Mathf.Cos(angle) * 0.065f, -0.12f) : new Vector3(0, 0, -0.12f);
        }
    }
}
