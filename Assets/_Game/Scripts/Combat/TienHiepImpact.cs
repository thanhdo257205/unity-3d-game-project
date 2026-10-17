using UnityEngine;

/// <summary>Layered impact: a brief core, delayed pressure fronts, hot shards and slower drifting residue.</summary>
public class TienHiepImpact : MonoBehaviour
{
    private LineRenderer[] fronts;
    private MeshRenderer core;
    private MaterialPropertyBlock coreLook;
    private Color tint;
    private float age, life, size, endAge;

    public static void Play(Vector3 point, Material material, Color color, float radius, Vector3 direction, bool strong, float seconds = 0.7f)
    {
        if (material == null) return;
        var go = new GameObject(strong ? "PhapBao_VaCham" : "LinhLuc_GonSong");
        go.transform.position = point;
        if (direction.sqrMagnitude > 0.001f) go.transform.rotation = Quaternion.LookRotation(direction);
        var impact = go.AddComponent<TienHiepImpact>(); impact.tint = color; impact.size = radius; impact.life = seconds;
        impact.endAge = strong ? 1.3f : 0.9f;
        impact.fronts = new LineRenderer[strong ? 3 : 1];
        for (int k = 0; k < impact.fronts.Length; k++)
        {
            var line = LinhPhapVisual.Ring(go.transform, material, 1, 0, color, strong ? 0.12f : 0.06f);
            line.transform.localRotation = k == 1 ? Quaternion.Euler(0, 0, 25) : Quaternion.Euler(90, 0, k * 17);
            for (int j = 0; j < line.positionCount; j++)
            {
                float a = j * Mathf.PI * 2 / line.positionCount;
                float r = 1 + 0.018f * Mathf.Sin(a * 7 + k) + 0.012f * Mathf.Sin(a * 13 - k);
                line.SetPosition(j, new Vector3(Mathf.Cos(a) * r, 0.012f * Mathf.Sin(a * 3), Mathf.Sin(a) * r));
            }
            impact.fronts[k] = line;
        }
        impact.core = TienHiepVfx.Heart(go.transform, material, new Color(color.r, color.g, color.b, strong ? 0.5f : 0.22f));
        impact.coreLook = new MaterialPropertyBlock(); impact.core.GetPropertyBlock(impact.coreLook);
        var sparks = TienHiepVfx.Particles(go.transform, "HoangQuang_Manh", material, new Color(color.r, color.g, color.b, 0.75f), strong ? 120 : 24, 0.65f, true);
        var mist = TienHiepVfx.Particles(go.transform, "DuKhi", material, new Color(color.r, color.g, color.b, strong ? 0.27f : 0.18f), strong ? 64 : 12, 1.1f, false);
        var gravity = sparks.main; gravity.gravityModifier = strong ? 0.6f : 0;
        for (int i = 0; i < (strong ? 96 : 10); i++)
        {
            Vector3 v = Random.onUnitSphere;
            TienHiepVfx.Emit(sparks, point + v * 0.08f, v * Random.Range(strong ? 3f : 0.5f, strong ? 9f : 2f) + direction * (strong ? 2f : 0), Random.Range(0.025f, 0.065f), Random.Range(0.25f, 0.7f));
        }
        for (int i = 0; i < (strong ? 48 : 4); i++)
        {
            Vector3 v = Random.onUnitSphere;
            TienHiepVfx.Emit(mist, point + v * 0.12f, v * Random.Range(0.5f, strong ? 2.3f : 0.8f), Random.Range(strong ? 0.35f : 0.2f, strong ? 1.0f : 0.3f), strong ? 1.1f : 0.7f);
        }
    }

    private void Update()
    {
        if (fronts == null) return;
        age += Time.deltaTime;
        for (int k = 0; k < fronts.Length; k++)
        {
            float u = Mathf.Clamp01((age - k * 0.065f) / life);
            fronts[k].enabled = age >= k * 0.065f;
            float r = Mathf.Lerp(0.12f, size * (k == 1 ? 0.72f : 1f), 1 - (1 - u) * (1 - u));
            fronts[k].transform.localScale = Vector3.one * r;
            Color c = tint; c.a *= (1 - u) * (1 - u) * (k == 0 ? 0.8f : 0.5f);
            fronts[k].startColor = fronts[k].endColor = c;
        }
        float flash = Mathf.Clamp01(1 - age / 0.23f);
        core.enabled = flash > 0;
        core.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, size * 0.4f, 1 - flash);
        Color light = tint; light.a *= flash * 0.45f; coreLook.SetColor("_BaseColor", light); core.SetPropertyBlock(coreLook);
        if (age >= Mathf.Max(life, endAge)) Destroy(gameObject);
    }
}
