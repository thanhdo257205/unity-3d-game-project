using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Bounded, timed reference spell visuals. Gameplay hits use swept/occluded shapes, never VFX colliders.</summary>
public class ReferenceSkillFx : MonoBehaviour
{
    private sealed class Stroke
    {
        public LineRenderer line;
        public Vector3[] points;
        public Color color;
        public float width;
    }
    private readonly List<Stroke> strokes = new List<Stroke>();
    private readonly List<Transform> relics = new List<Transform>();
    private readonly List<Renderer> surfaces = new List<Renderer>();
    private readonly Dictionary<Damageable, float> hitAgain = new Dictionary<Damageable, float>();
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private NamCungCombat caster;
    private ReferenceSkillData data;
    private Transform hand, otherHand;
    private Vector3 origin, destination, direction;
    private bool charging, preview, done, damageFired;
    private float age, duration, reach, nextSpark;
    private ParticleSystem particles;
    private MaterialPropertyBlock properties;
    private static Mesh diskMesh;

    public ReferenceSkillKind Kind => data != null ? data.kind : ReferenceSkillKind.FireIce;
    public bool IsCharging => charging;
    public bool IsDone => done;
    public bool WallClipped { get; private set; }

    public void Initialize(NamCungCombat owner, ReferenceSkillData skill, Vector3 from, Vector3 to, bool charge, bool forPreview = false)
    {
        caster = owner; data = skill; charging = charge; preview = forPreview;
        duration = charge ? skill.chargeTime + .2f : skill.duration;
        origin = from; direction = (to - from).sqrMagnitude > .01f ? (to - from).normalized : owner.transform.forward;
        destination = ClipToEnvironment(from, from + direction * Mathf.Min(skill.range, Vector3.Distance(from, to)));
        reach = Mathf.Max(.2f, Vector3.Distance(origin, destination));
        var animator = owner.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        { hand = animator.GetBoneTransform(HumanBodyBones.RightHand); otherHand = animator.GetBoneTransform(HumanBodyBones.LeftHand); }
        transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
        properties = new MaterialPropertyBlock();
        if (charging) BuildCharge(); else BuildReleased();
        if (owner.energyMaterial != null)
        {
            particles = TienHiepVfx.Particles(transform, "LinhKhi", owner.energyMaterial, skill.primary, 96, .6f, true);
            particles.GetComponent<ParticleSystemRenderer>().lengthScale = 1.4f;
        }
        Draw(0);
    }

    private Vector3 ClipToEnvironment(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from; float length = delta.magnitude;
        if (length < .01f) return to;
        int count = Physics.SphereCastNonAlloc(from, .07f, delta / length, hits, length, ~0, QueryTriggerInteraction.Ignore);
        float nearest = length;
        for (int i = 0; i < count; i++)
        {
            var collider = hits[i].collider;
            if (collider == null || collider.transform.IsChildOf(caster.transform) || collider.GetComponentInParent<FlyingSword>() != null
                || collider.GetComponentInParent<EnemyBlade>() != null || collider.GetComponentInParent<Damageable>() != null) continue;
            nearest = Mathf.Min(nearest, Mathf.Max(.05f, hits[i].distance - .1f));
            WallClipped = true;
        }
        return from + delta.normalized * nearest;
    }

    private void Update()
    {
        if (preview || done) return;
        if (caster == null || !caster.isActiveAndEnabled) { Destroy(gameObject); return; }
        float previous = age;
        age += Time.deltaTime;
        if (data.kind == ReferenceSkillKind.BloodSword && !charging) AdvanceProjectile(previous, age);
        if (done) return;
        Draw(age);
        if (!charging) ApplyGameplay();
        if (age >= duration) { done = true; Destroy(gameObject); }
    }

    /// <summary>Editor visual comparison without damage, mana use or timers affecting the game.</summary>
    public void SampleForPreview(float seconds)
    {
        preview = true; age = Mathf.Max(0, seconds); Draw(age);
        if (particles != null) { EmitPlasma(40); particles.Simulate(.08f, true, false, true); }
    }

    private Stroke NewLine(float width, Color color, int count = 65, bool loop = false)
    {
        var go = new GameObject("LinhTuyen_" + strokes.Count); go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = caster.energyMaterial; line.useWorldSpace = false; line.positionCount = count; line.loop = loop;
        line.widthMultiplier = width; line.numCornerVertices = 2; line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
        line.startColor = line.endColor = color;
        var stroke = new Stroke { line = line, width = width, color = color, points = new Vector3[count] };
        strokes.Add(stroke); return stroke;
    }

    private Transform Relic(string name, Material material)
    {
        Transform template = null;
        if (caster.relicModels != null)
            foreach (var child in caster.relicModels.GetComponentsInChildren<Transform>(true))
                if (child.name == name) { template = child; break; }
        GameObject go;
        if (template != null)
        {
            // Imported FBX children retain the unit conversion and axis rotation on their transform.
            // Animate a parent handle so changing spell scale never discards that conversion.
            go = new GameObject(name); go.transform.SetParent(transform, false);
            var model = Instantiate(template.gameObject, go.transform, false);
            model.transform.localPosition = Vector3.zero;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.SetParent(transform, false);
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        }
        go.name = name;
        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        { renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; }
        relics.Add(go.transform); return go.transform;
    }

    private Renderer Surface(bool sphere, float mode, Color color)
    {
        GameObject go;
        if (sphere)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        }
        else
        {
            go = new GameObject("PhapLuan"); go.AddComponent<MeshFilter>().sharedMesh = Disk(); go.AddComponent<MeshRenderer>();
        }
        go.transform.SetParent(transform, false);
        var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = caster.surfaceMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        var block = new MaterialPropertyBlock(); block.SetFloat("_Mode", mode); block.SetColor("_Tint", color);
        renderer.SetPropertyBlock(block); surfaces.Add(renderer); return renderer;
    }

    private static Mesh Disk()
    {
        if (diskMesh != null) return diskMesh;
        var vertices = new Vector3[97]; var uv = new Vector2[97]; var triangles = new int[96 * 3];
        uv[0] = new Vector2(.5f, .5f);
        for (int i = 0; i < 96; i++)
        {
            float a = i * Mathf.PI * 2 / 96;
            vertices[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0); uv[i + 1] = new Vector2(vertices[i + 1].x, vertices[i + 1].y) * .5f + Vector2.one * .5f;
            triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % 96 + 1;
        }
        diskMesh = new Mesh { name = "Reference_ArcanaDisk", vertices = vertices, uv = uv, triangles = triangles };
        diskMesh.RecalculateNormals(); diskMesh.RecalculateBounds(); return diskMesh;
    }

    private void BuildCharge()
    {
        for (int i = 0; i < 3; i++) NewLine(.018f, i == 1 ? data.secondary : data.primary);
        Surface(true, 2, data.primary);
        if (Kind == ReferenceSkillKind.BloodSword) Relic("BloodBlade", caster.bloodMaterial);
        if (Kind == ReferenceSkillKind.ThunderSword) Relic("ThunderBlade", caster.goldMaterial);
        if (Kind == ReferenceSkillKind.FireIce)
        { Relic("ThunderBlade", caster.bloodMaterial); Relic("ThunderBlade", caster.jadeMaterial); }
    }

    private void BuildReleased()
    {
        switch (Kind)
        {
            case ReferenceSkillKind.FireIce:
                Relic("ThunderBlade", caster.bloodMaterial); Relic("ThunderBlade", caster.jadeMaterial);
                for (int i = 0; i < 10; i++) NewLine(i < 2 ? .40f : .075f, i % 2 == 0 ? data.primary : data.secondary);
                break;
            case ReferenceSkillKind.Mirrors:
                for (int i = 0; i < 5; i++) { Relic("MirrorFrame", caster.jadeMaterial); Surface(false, 1, data.primary); NewLine(.032f, data.primary); }
                Surface(true, 2, new Color(.08f, .7f, .45f, .11f));
                break;
            case ReferenceSkillKind.Reincarnation:
                Surface(false, 0, data.primary);
                for (int i = 0; i < 18; i++) NewLine(i == 0 ? .12f : .045f, i % 3 == 0 ? data.primary : data.secondary, 97, i < 3);
                break;
            case ReferenceSkillKind.BloodSword:
                Relic("BloodBlade", caster.bloodMaterial);
                for (int i = 0; i < 14; i++) NewLine(i < 3 ? .10f : .035f, i % 3 == 0 ? data.secondary : data.primary);
                break;
            case ReferenceSkillKind.ThunderSword:
                Relic("ThunderBlade", caster.goldMaterial);
                for (int i = 0; i < 11; i++) NewLine(i < 3 ? .10f : .035f, i % 4 == 0 ? data.secondary : data.primary, 49);
                break;
            case ReferenceSkillKind.GoldenNet:
                for (int i = 0; i < 56; i++) NewLine(.035f, i % 6 == 0 ? data.secondary : data.primary, 97, i >= 36);
                Surface(true, 2, new Color(.8f, .4f, .06f, .045f));
                break;
            case ReferenceSkillKind.MindPearl:
                Surface(true, 2, new Color(.45f, .13f, .9f, .2f));
                for (int i = 0; i < 12; i++) NewLine(i < 3 ? .06f : .025f, i % 3 == 0 ? data.secondary : data.primary);
                break;
        }
    }

    private void Circle(Stroke line, float radius, Vector3 center, Quaternion rotation)
    {
        for (int i = 0; i < line.points.Length; i++)
        {
            float a = i / (float)(line.points.Length - 1) * Mathf.PI * 2;
            line.points[i] = center + rotation * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
        }
        line.line.SetPositions(line.points);
    }

    private void Draw(float time)
    {
        if (caster == null || data == null) return;
        float fade = Mathf.Min(Mathf.Clamp01(time / .12f + .1f), Mathf.Clamp01((duration - time) / .4f));
        foreach (var stroke in strokes)
        {
            var color = stroke.color; color.a *= fade;
            stroke.line.startColor = stroke.line.endColor = color;
        }
        foreach (var surface in surfaces)
        { surface.GetPropertyBlock(properties); properties.SetFloat("_Fade", fade); properties.SetFloat("_Phase", time); surface.SetPropertyBlock(properties); }

        if (charging)
        {
            Vector3 center = hand != null ? hand.position : caster.transform.position + Vector3.up * 1.4f;
            if (Kind == ReferenceSkillKind.FireIce && otherHand != null) center = (center + otherHand.position) * .5f;
            transform.position = center + direction * .08f;
            float radius = .10f + .20f * Mathf.Clamp01(time / data.chargeTime);
            for (int i = 0; i < strokes.Count; i++) Circle(strokes[i], radius * (1 + .2f * i), Vector3.zero, Quaternion.Euler(i * 58, time * 120 + i * 35, time * 80));
            surfaces[0].transform.localScale = Vector3.one * radius * 1.8f;
            for (int i = 0; i < relics.Count; i++)
            {
                relics[i].localScale = Vector3.one * (Kind == ReferenceSkillKind.ThunderSword ? .9f : .45f);
                relics[i].localPosition = i == 0 ? Vector3.zero : Vector3.right * .2f;
                relics[i].localRotation = Quaternion.Euler(0, 0, i == 0 ? -12 : 12);
            }
            Sparks(time, .13f, true);
            return;
        }

        if (Kind == ReferenceSkillKind.Mirrors || Kind == ReferenceSkillKind.Reincarnation || Kind == ReferenceSkillKind.FireIce)
            transform.position = caster.transform.position + Vector3.up * 1.35f;
        if (Kind == ReferenceSkillKind.GoldenNet || Kind == ReferenceSkillKind.MindPearl) transform.position = destination;
        if (Kind == ReferenceSkillKind.ThunderSword) transform.position = origin;
        if (Kind == ReferenceSkillKind.BloodSword && preview) transform.position = origin + direction * Mathf.Min(reach, 28 * time);

        switch (Kind)
        {
            case ReferenceSkillKind.FireIce: DrawFireIce(time); break;
            case ReferenceSkillKind.Mirrors: DrawMirrors(time); break;
            case ReferenceSkillKind.Reincarnation: DrawReincarnation(time); break;
            case ReferenceSkillKind.BloodSword: DrawBlood(time); break;
            case ReferenceSkillKind.ThunderSword: DrawThunder(time); break;
            case ReferenceSkillKind.GoldenNet: DrawNet(time); break;
            case ReferenceSkillKind.MindPearl: DrawPearl(time); break;
        }
        Sparks(time, Kind == ReferenceSkillKind.GoldenNet ? data.radius : .5f, false);
    }

    private void DrawFireIce(float time)
    {
        for (int i = 0; i < relics.Count; i++)
        {
            relics[i].localPosition = new Vector3(i == 0 ? -.5f : .5f, .2f, .55f);
            relics[i].localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
            relics[i].localScale = Vector3.one * .65f;
        }
        for (int k = 0; k < strokes.Count; k++)
        {
            var stroke = strokes[k]; float side = k % 2 == 0 ? 1 : -1;
            for (int i = 0; i < stroke.points.Length; i++)
            {
                float u = i / (float)(stroke.points.Length - 1), envelope = Mathf.Sin(u * Mathf.PI);
                float a = u * 13 - time * 8 + k * .4f;
                stroke.points[i] = new Vector3(side * (.35f + Mathf.Sin(a) * envelope * (.3f + k * .035f)),
                    Mathf.Cos(a) * envelope * (.35f + k * .04f), .9f + u * (reach - .9f));
            }
            stroke.line.SetPositions(stroke.points);
        }
    }

    private void DrawMirrors(float time)
    {
        for (int k = 0; k < 5; k++)
        {
            float a = k * Mathf.PI * 2 / 5 + .3f;
            var position = new Vector3(Mathf.Sin(a) * 1.65f, Mathf.Cos(a) * 1.65f + .1f, -.7f);
            relics[k].localPosition = position; relics[k].localRotation = Quaternion.Euler(0, 0, time * 30); relics[k].localScale = Vector3.one * 1.05f;
            surfaces[k].transform.localPosition = position; surfaces[k].transform.localScale = Vector3.one * .47f;
            var ray = strokes[k];
            for (int i = 0; i < ray.points.Length; i++)
            {
                float u = i / (float)(ray.points.Length - 1);
                ray.points[i] = Vector3.Lerp(position, transform.InverseTransformPoint(destination), u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * .15f;
            }
            ray.line.SetPositions(ray.points);
            ray.line.enabled = time > .6f && time < 1.5f;
        }
        surfaces[5].transform.localScale = Vector3.one * 4.4f;
    }

    private void DrawReincarnation(float time)
    {
        var center = new Vector3(0, .7f, -1.15f);
        surfaces[0].transform.localPosition = center; surfaces[0].transform.localScale = Vector3.one * 1.7f;
        Circle(strokes[0], 1.76f, center, Quaternion.identity);
        Circle(strokes[1], 1.94f, center, Quaternion.Euler(0, 0, -time * 12));
        Circle(strokes[2], 2.06f, center, Quaternion.identity);
        for (int k = 3; k < strokes.Count - 1; k++)
        {
            var stroke = strokes[k];
            for (int i = 0; i < stroke.points.Length; i++)
            {
                float u = i / (float)(stroke.points.Length - 1), a = k * 2.39996f + u * .3f + time * .18f;
                float r = 1.7f + u * .9f + Mathf.Sin(u * 18 + time * 5) * .11f * u;
                stroke.points[i] = center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, Mathf.Sin(a * 3 + u * 5) * .13f);
            }
            stroke.line.SetPositions(stroke.points);
        }
        float pulse = Mathf.Clamp01((time - .85f) / .65f);
        var wave = strokes[strokes.Count - 1]; wave.line.enabled = pulse > 0 && pulse < 1;
        Circle(wave, .8f + pulse * 5, new Vector3(0, .25f, pulse * 7), Quaternion.identity);
    }

    private void DrawBlood(float time)
    {
        relics[0].localPosition = Vector3.zero; relics[0].localScale = Vector3.one * 1.65f;
        relics[0].localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward) * Quaternion.Euler(0, time * 150, 0);
        for (int k = 0; k < strokes.Count; k++)
        {
            var stroke = strokes[k];
            for (int i = 0; i < stroke.points.Length; i++)
            {
                float u = i / (float)(stroke.points.Length - 1), a = k * 2.39996f + u * 9 - time * 11;
                float r = Mathf.Sin(u * Mathf.PI) * (.3f + k * .045f);
                stroke.points[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 2 - u * (3.5f + time * 2));
            }
            stroke.line.SetPositions(stroke.points);
        }
    }

    private void DrawThunder(float time)
    {
        relics[0].localPosition = Vector3.up * .75f; relics[0].localScale = Vector3.one * 1.55f;
        relics[0].localRotation = Quaternion.Euler(0, time * 55, 0);
        var end = transform.InverseTransformPoint(destination);
        for (int k = 0; k < strokes.Count; k++)
        {
            var stroke = strokes[k];
            for (int i = 0; i < stroke.points.Length; i++)
            {
                float u = i / (float)(stroke.points.Length - 1);
                float amplitude = Mathf.Sin(u * Mathf.PI) * (k < 3 ? .10f : .38f);
                var noise = new Vector3(Mathf.Sin(i * 11.37f + k * 2.4f + Mathf.Floor(time * 18)) * amplitude,
                    Mathf.Cos(i * 7.71f + k * 1.9f + Mathf.Floor(time * 18)) * amplitude, 0);
                stroke.points[i] = Vector3.Lerp(Vector3.up * 3.5f, end, u) + noise;
            }
            stroke.line.SetPositions(stroke.points);
        }
    }

    private void DrawNet(float time)
    {
        float radius = data.radius * Mathf.SmoothStep(.22f, 1, Mathf.Clamp01(time / .45f));
        for (int k = 0; k < strokes.Count; k++)
        {
            var stroke = strokes[k];
            for (int i = 0; i < stroke.points.Length; i++)
            {
                float u = i / (float)(stroke.points.Length - 1), theta, phi;
                if (k < 36) { phi = .08f + u * (Mathf.PI - .16f); theta = k * Mathf.PI * 2 / 36 + Mathf.Sin(phi * 18) * .018f; }
                else { phi = (k - 35) * Mathf.PI / 21; theta = u * Mathf.PI * 2; }
                stroke.points[i] = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi) * 1.2f, Mathf.Sin(phi) * Mathf.Sin(theta)) * radius;
            }
            stroke.line.SetPositions(stroke.points);
        }
        surfaces[0].transform.localScale = new Vector3(radius * 2, radius * 2.4f, radius * 2);
    }

    private void DrawPearl(float time)
    {
        float radius = data.radius * Mathf.SmoothStep(.2f, 1, Mathf.Clamp01(time / .35f));
        surfaces[0].transform.localScale = Vector3.one * radius * 2;
        for (int k = 0; k < strokes.Count; k++) Circle(strokes[k], radius * (k < 3 ? 1 : .95f), Vector3.zero,
            Quaternion.Euler(k * 37 + time * (k % 2 == 0 ? 20 : -20), k * 51, time * 35));
    }

    private void Sparks(float time, float radius, bool inward)
    {
        if (particles == null || preview || time < nextSpark) return;
        nextSpark = time + .065f;
        if (!inward) EmitPlasma(4);
        for (int i = 0; i < 2; i++)
        {
            float a = time * 19 + i * 2.4f;
            var local = new Vector3(Mathf.Cos(a), Mathf.Sin(a), Mathf.Sin(a * 2) * .3f) * radius;
            var velocity = transform.TransformDirection(local.normalized) * (inward ? -1.4f : .65f);
            TienHiepVfx.Emit(particles, transform.TransformPoint(local), velocity, .025f, .4f);
        }
    }

    private void EmitPlasma(int count)
    {
        if (particles == null) return;
        for (int i = 0; i < count; i++)
        {
            float u = Mathf.Repeat(age * .35f + (i + .5f) / count, 1), a = i * 2.39996f + age * 7;
            Vector3 point;
            if (Kind == ReferenceSkillKind.FireIce)
            {
                float theta = u * 13 - age * 8;
                point = new Vector3((i % 2 == 0 ? 1 : -1) * (.35f + Mathf.Sin(theta) * .35f), Mathf.Cos(theta) * .4f, u * reach);
            }
            else if (Kind == ReferenceSkillKind.Reincarnation)
                point = new Vector3(Mathf.Cos(a) * 1.85f, .7f + Mathf.Sin(a) * 1.85f, -1.15f);
            else if (Kind == ReferenceSkillKind.BloodSword)
                point = new Vector3(Mathf.Cos(a) * .3f, Mathf.Sin(a) * .3f, -u * 3.5f);
            else if (Kind == ReferenceSkillKind.ThunderSword)
                point = Vector3.Lerp(Vector3.up * 3.5f, transform.InverseTransformPoint(destination), u);
            else if (Kind == ReferenceSkillKind.GoldenNet || Kind == ReferenceSkillKind.MindPearl)
                point = new Vector3(Mathf.Cos(a) * Mathf.Sin(u * Mathf.PI), Mathf.Cos(u * Mathf.PI), Mathf.Sin(a) * Mathf.Sin(u * Mathf.PI)) * data.radius;
            else point = new Vector3(Mathf.Cos(a), Mathf.Sin(a), -.6f) * 1.65f;
            var color = i % 2 == 0 ? data.primary : data.secondary; color.a = .4f;
            var emit = new ParticleSystem.EmitParams { position = transform.TransformPoint(point), velocity = transform.TransformDirection(new Vector3(Mathf.Sin(a), Mathf.Cos(a), .1f)) * .4f,
                startColor = color, startSize = Kind == ReferenceSkillKind.FireIce ? .32f : .17f, startLifetime = .45f };
            particles.Emit(emit, 1);
        }
    }

    private void AdvanceProjectile(float previous, float current)
    {
        Vector3 from = origin + direction * Mathf.Min(reach, previous * 28), to = origin + direction * Mathf.Min(reach, current * 28);
        float distance = Vector3.Distance(from, to), nearest = distance;
        bool collided = false;
        if (distance > .0001f)
        {
            int count = Physics.SphereCastNonAlloc(from, .35f, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(caster.transform) || collider.GetComponentInParent<FlyingSword>() != null) continue;
                if (hits[i].distance <= nearest) { nearest = hits[i].distance; collided = true; }
            }
        }
        transform.position = collided ? from + direction * nearest : to;
        if (collided || current * 28 >= reach)
        {
            HitSphere(transform.position, data.radius, data.damage, 0);
            LinhPhapVisual.Impact(transform.position, caster.energyMaterial, data.primary, data.radius, direction);
            done = true; Destroy(gameObject);
        }
    }

    private void ApplyGameplay()
    {
        if (preview || done) return;
        switch (Kind)
        {
            case ReferenceSkillKind.FireIce:
                if (age > .15f && age < 1.4f) HitBeam(transform.position, destination, .8f, data.damage / 4, .35f);
                break;
            case ReferenceSkillKind.Mirrors:
                if (!damageFired && age >= .8f) { damageFired = true; HitBeam(transform.position, destination, .65f, data.damage, 100); }
                break;
            case ReferenceSkillKind.Reincarnation:
                if (!damageFired && age >= 1.05f)
                {
                    damageFired = true;
                    foreach (var life in Damageable.All.ToArray())
                        if (Valid(life) && Vector3.Distance(life.AimPoint, transform.position) <= data.range
                            && Vector3.Angle(direction, life.AimPoint - transform.position) < 70)
                            Hit(life, data.damage, 100);
                }
                break;
            case ReferenceSkillKind.ThunderSword:
                if (!damageFired && age >= .25f) { damageFired = true; HitBeam(origin + Vector3.up * 3.5f, ClipToEnvironment(origin + Vector3.up * 3.5f, destination), 1.05f, data.damage, 100); }
                break;
            case ReferenceSkillKind.GoldenNet:
            case ReferenceSkillKind.MindPearl:
                if (!damageFired && age >= .35f) { damageFired = true; HitSphere(destination, data.radius, data.damage, data.stunTime); }
                break;
        }
    }

    private bool Valid(Damageable life) => life != null && life.IsAlive && life.isActiveAndEnabled && !life.transform.IsChildOf(caster.transform);

    private void HitSphere(Vector3 center, float radius, float damage, float stun)
    {
        foreach (var life in Damageable.All.ToArray())
        {
            if (!Valid(life) || Vector3.Distance(life.AimPoint, center) > radius) continue;
            if (SwordFlightMath.HitsEnvironment(center, life.AimPoint, .025f, caster.transform)) continue;
            Hit(life, damage, 100);
            var enemy = life.GetComponentInParent<EnemyBase>();
            if (enemy != null && enemy.IsAlive && stun > 0) enemy.Stun(stun);
        }
    }

    private void HitBeam(Vector3 from, Vector3 to, float radius, float damage, float interval)
    {
        Vector3 delta = to - from;
        foreach (var life in Damageable.All.ToArray())
        {
            if (!Valid(life)) continue;
            float u = delta.sqrMagnitude > .001f ? Mathf.Clamp01(Vector3.Dot(life.AimPoint - from, delta) / delta.sqrMagnitude) : 0;
            if (Vector3.Distance(life.AimPoint, from + delta * u) <= radius + .35f) Hit(life, damage, interval);
        }
    }

    private void Hit(Damageable life, float damage, float interval)
    {
        if (hitAgain.TryGetValue(life, out float next) && age < next) return;
        if (SwordFlightMath.HitsEnvironment(origin, life.AimPoint, .025f, caster.transform)) return;
        hitAgain[life] = age + interval;
        life.TakeDamage(new DamageInfo { amount = damage, point = life.AimPoint, direction = direction });
        if (Kind == ReferenceSkillKind.ThunderSword && data.stunTime > 0)
        { var enemy = life.GetComponentInParent<EnemyBase>(); if (enemy != null && enemy.IsAlive) enemy.Stun(data.stunTime); }
    }

    public bool BlockProjectile(IEnemyProjectile projectile, Vector3 from, Vector3 to)
    {
        if (charging || done || preview || age > duration - .2f || caster == null
            || (Kind != ReferenceSkillKind.Mirrors && Kind != ReferenceSkillKind.Reincarnation)) return false;
        Vector3 center = caster.transform.position + Vector3.up * 1.3f;
        Vector3 delta = to - from;
        float u = delta.sqrMagnitude > .001f ? Mathf.Clamp01(Vector3.Dot(center - from, delta) / delta.sqrMagnitude) : 0;
        Vector3 contact = from + delta * u;
        if (Vector3.Distance(contact, center) > 2.15f + projectile.Radius) return false;
        projectile.Block();
        LinhPhapVisual.Pulse(contact, caster.energyMaterial, data.primary, .6f, .25f);
        return true;
    }
}
