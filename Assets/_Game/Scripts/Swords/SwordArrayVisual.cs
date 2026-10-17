using UnityEngine;

/// <summary>Counter-rotating rings and sword-shaped seals inspired by the reference's rear sword mandala.</summary>
[DisallowMultipleComponent]
public class SwordArrayVisual : MonoBehaviour
{
    public Material material;
    public Color tint = new Color(0.05f, 1f, 0.65f, 1f);
    private SwordSwarm swarm;
    private TaTu enemy;
    private Damageable life;
    private Transform root;
    private readonly LineRenderer[] rings = new LineRenderer[4];
    private float presence;

    private void Awake()
    {
        swarm = GetComponent<SwordSwarm>();
        enemy = GetComponent<TaTu>();
        life = GetComponent<Damageable>();
        root = new GameObject("TranDo_LinhKiem").transform;
        root.SetParent(transform, false);
        for (int k = 0; k < rings.Length; k++)
        {
            var line = new GameObject("PhongAn_" + k).AddComponent<LineRenderer>();
            line.transform.SetParent(root, false);
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = k == 0 ? 0.032f : 0.018f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            int n = k == 3 ? 192 : 144;
            line.positionCount = n;
            for (int j = 0; j < n; j++)
            {
                float angle = 2f * Mathf.PI * j / n;
                float r = k == 0 ? 1f : k == 1 ? 0.82f + 0.025f * Mathf.Sin(angle * 48f)
                    : k == 2 ? 0.43f + 0.018f * Mathf.Sin(angle * 24f)
                    : j % 8 == 3 ? 1.14f : j % 8 == 4 ? 1.32f : 0.97f;
                line.SetPosition(j, new Vector3(Mathf.Sin(angle) * r, Mathf.Cos(angle) * r, 0));
            }
            rings[k] = line;
        }
    }

    private void LateUpdate()
    {
        bool active = swarm != null ? swarm.IsActive && swarm.CurrentFormation == FormationType.Wheel
            : enemy != null && enemy.isActiveAndEnabled && life != null && life.IsAlive;
        presence = Mathf.MoveTowards(presence, active ? 1f : 0f, Time.deltaTime * 3f);
        float opacity = presence * (swarm != null ? swarm.FormationOpacity : 1f);
        root.gameObject.SetActive(opacity > 0.005f);
        if (opacity <= 0.005f) return;
        Transform owner = swarm != null ? swarm.Owner : transform;
        root.position = swarm != null ? swarm.AnchorPosition - owner.forward * 0.32f
            : transform.position + Vector3.up * 1.5f - transform.forward * 0.6f;
        root.rotation = Quaternion.Euler(0, owner.eulerAngles.y, 0);
        float radius = swarm != null ? Mathf.Max(1.1f, swarm.WheelOuterRadius * 0.83f) : 1.65f;
        root.localScale = Vector3.one * radius;
        for (int k = 0; k < rings.Length; k++)
        {
            rings[k].transform.localRotation = Quaternion.Euler(0, 0, Time.time * (k % 2 == 0 ? 9f : -13f));
            Color color = tint * (k == 0 ? 2.3f : 1.5f);
            color.a = opacity * (k == 3 ? 0.55f : 0.7f);
            rings[k].startColor = rings[k].endColor = color;
        }
    }
}
