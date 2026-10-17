using UnityEngine;

/// <summary>Soft jade ward and gold pressure rings. All geometry is visual; collision is resolved by combat code.</summary>
public class LinhPhapVisual : MonoBehaviour
{
    private LineRenderer[] streams;
    private MeshRenderer shell;
    private MaterialPropertyBlock look;
    private ParticleSystem mist;
    private float mistClock, hitTime = -100f, fadeStarted = -1f;
    private Vector3 hitPosition;
    private Mesh ownedMesh;

    public static LineRenderer Ring(Transform parent, Material material, float radius, float height, Color color, float width = 0.035f)
    {
        var line = new GameObject("LinhVan").AddComponent<LineRenderer>();
        line.transform.SetParent(parent, false);
        line.useWorldSpace = false; line.loop = true; line.positionCount = 96;
        line.sharedMaterial = material; line.widthMultiplier = width;
        line.startColor = line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        for (int i = 0; i < 96; i++)
        {
            float a = i * Mathf.PI * 2f / 96f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius));
        }
        return line;
    }

    public static GameObject Ward(Transform owner, Material material)
    {
        var root = new GameObject("NgocQuang_HoThe");
        root.transform.SetParent(owner, false);
        Color jade = new Color(0.035f, 0.75f, 0.48f, 0.24f);
        Ring(root.transform, material, 1.75f, 0.12f, jade, 0.065f);
        Ring(root.transform, material, 1.46f, 0.16f, jade * 0.65f, 0.035f);
        var streams = new LineRenderer[7];
        for (int k = 0; k < streams.Length; k++)
        {
            var line = Ring(root.transform, material, 1f, 0, jade, k % 2 == 0 ? 0.12f : 0.07f);
            line.loop = false; line.positionCount = 80; streams[k] = line;
            line.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.22f, 0.8f), new Keyframe(0.62f, 1), new Keyframe(1, 0));
            for (int j = 0; j < 80; j++)
            {
                float u = j / 79f, angle = k * 2.39996f + u * Mathf.PI * (k % 2 == 0 ? 2.8f : -2f);
                float r = Mathf.Lerp(1.72f, 0.52f, u) + Mathf.Sin(u * Mathf.PI) * 0.16f;
                line.SetPosition(j, new Vector3(Mathf.Cos(angle) * r, 0.05f + u * 4.15f, Mathf.Sin(angle) * r));
            }
        }
        const int sides = 96, levels = 16;
        var vertices = new Vector3[(sides + 1) * (levels + 1)]; var uv = new Vector2[vertices.Length]; var colors = new Color[vertices.Length];
        var triangles = new int[sides * levels * 6];
        for (int j = 0; j <= levels; j++)
        {
            float u = j / (float)levels, r = Mathf.Lerp(1.75f, 0.52f, u) + Mathf.Sin(u * Mathf.PI) * 0.16f;
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides; int v = j * (sides + 1) + i;
                vertices[v] = new Vector3(Mathf.Cos(a) * r, 0.05f + u * 4.15f, Mathf.Sin(a) * r);
                uv[v] = new Vector2(i / (float)sides, u); colors[v] = Color.white;
                if (j == levels || i == sides) continue;
                int t = (j * sides + i) * 6;
                triangles[t] = v; triangles[t + 1] = v + sides + 1; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + sides + 1; triangles[t + 5] = v + sides + 2;
            }
        }
        var mesh = new Mesh { name = "NgocQuang_Cone", vertices = vertices, uv = uv, colors = colors, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var surface = new GameObject("ManNgoc"); surface.transform.SetParent(root.transform, false);
        surface.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = surface.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var effect = root.AddComponent<LinhPhapVisual>(); effect.ownedMesh = mesh; effect.shell = renderer; effect.streams = streams;
        effect.look = new MaterialPropertyBlock(); effect.look.SetFloat("_Mode", 1);
        effect.look.SetColor("_BaseColor", new Color(0.035f, 0.75f, 0.48f, 0.85f));
        renderer.SetPropertyBlock(effect.look);
        effect.mist = TienHiepVfx.Particles(root.transform, "HoThe_KhiNgoc", material, new Color(0.04f, 0.75f, 0.5f, 0.32f), 96, 1.3f, false);
        return root;
    }

    public static void Pulse(Vector3 point, Material material, Color color, float size, float seconds = 0.5f)
    {
        TienHiepImpact.Play(point, material, color, size, Vector3.up, false, seconds);
    }

    public static void Impact(Vector3 point, Material material, Color color, float size, Vector3 direction)
    { TienHiepImpact.Play(point, material, color, size, direction, true, 0.7f); }

    public void Hit(Vector3 point) { hitPosition = point; hitTime = Time.time; }
    public void Dissolve() { fadeStarted = Time.time; transform.SetParent(null, true); }

    private void Update()
    {
        if (shell == null) return;
        float presence = fadeStarted < 0 ? 1 : 1 - Mathf.Clamp01((Time.time - fadeStarted) / 0.3f);
        look.SetColor("_BaseColor", new Color(0.035f, 0.75f, 0.48f, 0.85f * presence));
        look.SetVector("_HitPosition", hitPosition); look.SetFloat("_HitTime", hitTime); shell.SetPropertyBlock(look);
        for (int k = 0; k < streams.Length; k++)
        {
            streams[k].transform.Rotate(Vector3.up, (k % 2 == 0 ? 16 : -22) * Time.deltaTime);
            Color c = new Color(0.04f, 0.85f, 0.6f, presence * (0.16f + 0.06f * Mathf.Sin(Time.time * 3 + k)));
            streams[k].startColor = streams[k].endColor = c;
        }
        mistClock += Time.deltaTime * 30 * presence;
        for (int i = 0; mistClock >= 1 && i < 6; i++, mistClock--)
        {
            float angle = Random.Range(0, Mathf.PI * 2), y = Random.Range(0.05f, 3.4f), r = Mathf.Lerp(1.7f, 0.6f, y / 4.2f);
            var offset = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
            Vector3 velocity = transform.TransformDirection(new Vector3(-Mathf.Sin(angle), 0.7f, Mathf.Cos(angle))) * 0.7f;
            TienHiepVfx.Emit(mist, transform.TransformPoint(offset), velocity, Random.Range(0.12f, 0.28f), 1.2f);
        }
        if (presence <= 0) Destroy(gameObject);
    }
    private void OnDestroy() { if (ownedMesh != null) Destroy(ownedMesh); }
}
