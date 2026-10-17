using UnityEngine;

/// <summary>Bounded world-space wisps and converging motes shared by the lotus's charge and flight.</summary>
public class TienHiepVfx : MonoBehaviour
{
    public Material material;
    private ParticleSystem mist, sparks;
    private LineRenderer[] streams;
    private Transform heart;
    private MaterialPropertyBlock heartLook;
    private MeshRenderer heartRenderer;
    private float charge, mistClock, sparkClock;
    private bool flying;
    private Vector3 direction = Vector3.forward;
    private static readonly Color Gold = new Color(1f, 0.48f, 0.07f, 0.6f);

    public static ParticleSystem Particles(Transform parent, string name, Material material, Color color, int maximum, float life, bool streaks)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.playOnAwake = false; main.loop = true; main.maxParticles = maximum;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.startLifetime = life;
        main.startSize = new ParticleSystem.MinMaxCurve(streaks ? 0.025f : 0.09f, streaks ? 0.055f : 0.24f);
        main.startSpeed = 0; main.startColor = color;
        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var fade = ps.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.8f, 0.12f), new GradientAlphaKey(0.45f, 0.5f), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(0.25f, 1), new Keyframe(1, streaks ? 0 : 1.5f)));
        var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
        renderer.renderMode = streaks ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        renderer.lengthScale = streaks ? 2.2f : 1; renderer.velocityScale = streaks ? 0.05f : 0;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        var properties = new MaterialPropertyBlock(); properties.SetFloat("_Mode", 3); renderer.SetPropertyBlock(properties);
        ps.Play(); return ps;
    }

    public static void Emit(ParticleSystem ps, Vector3 point, Vector3 velocity, float size, float life)
    {
        var emit = new ParticleSystem.EmitParams { position = point, velocity = velocity, startSize = size, startLifetime = life };
        ps.Emit(emit, 1);
    }

    public static MeshRenderer Heart(Transform parent, Material material, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "LoiLinhLuc";
        var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        go.transform.SetParent(parent, false); go.transform.localPosition = Vector3.up * 0.18f; go.transform.localScale = Vector3.one * 0.28f;
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        var properties = new MaterialPropertyBlock(); properties.SetFloat("_Mode", 2); properties.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(properties); return renderer;
    }

    private void Awake()
    {
        mist = Particles(transform, "KimLien_KhiLuu", material, new Color(1f, 0.48f, 0.08f, 0.3f), 96, 0.65f, false);
        sparks = Particles(transform, "KimLien_TuLuc", material, Gold, 96, 0.55f, true);
        heartRenderer = Heart(transform, material, new Color(1.5f, 0.8f, 0.22f, 0.6f)); heart = heartRenderer.transform;
        heartLook = new MaterialPropertyBlock(); heartRenderer.GetPropertyBlock(heartLook);
        streams = new LineRenderer[3];
        for (int k = 0; k < streams.Length; k++)
        {
            var line = LinhPhapVisual.Ring(transform, material, 1, 0.2f, new Color(1, 0.5f, 0.08f, 0.22f), 0.055f);
            line.positionCount = 80; streams[k] = line;
            for (int j = 0; j < 80; j++)
            {
                float a = j * Mathf.PI * 2 / 80;
                line.SetPosition(j, new Vector3(Mathf.Cos(a) * (0.95f + k * 0.12f), 0.18f + Mathf.Sin(a * 2 + k) * 0.16f, Mathf.Sin(a) * (0.95f + k * 0.12f)));
            }
            line.transform.localRotation = Quaternion.Euler(k * 17, k * 71, k * 13);
        }
    }

    public void SetCharge(float value) { charge = Mathf.Clamp01(value); }
    public void Fly(Vector3 forward) { flying = true; direction = forward; foreach (var line in streams) line.enabled = false; }
    public void SetFlightDirection(Vector3 forward) { direction = forward; }
    private void Update()
    {
        float dt = Time.deltaTime;
        heart.localScale = Vector3.one * (flying ? 0.3f : Mathf.Lerp(0.16f, 0.45f, charge)) * (1 + 0.06f * Mathf.Sin(Time.time * 10));
        heartLook.SetColor("_BaseColor", new Color(1.3f, 0.75f, 0.22f, flying ? 0.5f : 0.35f + charge * 0.3f)); heartRenderer.SetPropertyBlock(heartLook);
        if (!flying)
            for (int k = 0; k < streams.Length; k++)
            {
                streams[k].transform.Rotate(Vector3.up, (k % 2 == 0 ? 65 : -45) * dt, Space.Self);
                var c = Gold; c.a = 0.12f + charge * 0.22f; streams[k].startColor = streams[k].endColor = c;
            }
        sparkClock += dt * (flying ? 32 : 55);
        for (int i = 0; sparkClock >= 1 && i < 8; i++, sparkClock--)
        {
            Vector3 offset = Random.onUnitSphere;
            Vector3 origin = transform.position + Vector3.up * 0.2f;
            if (flying) Emit(sparks, origin + offset * 0.18f, -direction * Random.Range(2f, 5f) + offset, Random.Range(0.025f, 0.055f), 0.5f);
            else Emit(sparks, origin + offset * (1.3f + charge * 0.3f), -offset * (2.3f + charge), Random.Range(0.025f, 0.05f), 0.5f);
        }
        mistClock += dt * 16;
        for (int i = 0; mistClock >= 1 && i < 4; i++, mistClock--)
        {
            Vector3 offset = Random.insideUnitSphere;
            Emit(mist, transform.position + offset * 0.3f, flying ? -direction * 2 + offset : Vector3.up * 0.4f + offset * 0.4f,
                Random.Range(0.14f, 0.28f), 0.6f);
        }
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        foreach (var ps in new[] { mist, sparks })
            if (ps != null) { ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); ps.transform.SetParent(null, true); Destroy(ps.gameObject, 1.3f); }
    }
}
