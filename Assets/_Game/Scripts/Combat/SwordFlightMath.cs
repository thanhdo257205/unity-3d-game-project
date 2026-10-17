using UnityEngine;

/// <summary>Shared steering and swept collision for both sides of a flying-sword duel.</summary>
public static class SwordFlightMath
{
    private static readonly RaycastHit[] Obstacles = new RaycastHit[32];

    public static Vector3 Steer(Vector3 direction, Vector3 position, Vector3 target,
        float phase, float time, float turnDegrees, float arc, float dt)
    {
        Vector3 to = target - position;
        Vector3 side = Vector3.Cross(Vector3.up, to.normalized);
        if (side.sqrMagnitude < 0.01f) side = Vector3.right;
        // Each blade follows its own ribbon; converge near the target instead of orbiting forever.
        float spread = Mathf.Clamp01((to.magnitude - 2f) / 6f);
        Vector3 aim = target + side.normalized * (Mathf.Sin(time * 2.4f + phase) * arc * spread)
            + Vector3.up * (Mathf.Sin(time * 1.8f + phase) * arc * 0.35f * spread);
        Vector3 desired = aim - position;
        if (desired.sqrMagnitude < 0.0001f) return direction;
        if (direction.sqrMagnitude < 0.001f) return desired.normalized;
        float terminalTurn = turnDegrees * Mathf.Lerp(3f, 1f, Mathf.Clamp01(to.magnitude / 4f));
        return Vector3.RotateTowards(direction.normalized, desired.normalized,
            terminalTurn * Mathf.Deg2Rad * dt, 0f).normalized;
    }

    public static float SegmentDistanceSquared(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1,
        out Vector3 contact)
    {
        Vector3 u = p1 - p0, v = q1 - q0, w = p0 - q0;
        float a = Vector3.Dot(u, u), b = Vector3.Dot(u, v), c = Vector3.Dot(v, v);
        float d = Vector3.Dot(u, w), e = Vector3.Dot(v, w);
        float s, t;
        if (a < 0.000001f && c < 0.000001f) { s = t = 0f; }
        else if (a < 0.000001f) { s = 0f; t = Mathf.Clamp01(e / c); }
        else if (c < 0.000001f) { t = 0f; s = Mathf.Clamp01(-d / a); }
        else
        {
            float denom = a * c - b * b;
            s = denom > 0.000001f ? Mathf.Clamp01((b * e - c * d) / denom) : 0f;
            t = (b * s + e) / c;
            if (t < 0f) { t = 0f; s = Mathf.Clamp01(-d / a); }
            else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - d) / a); }
        }
        Vector3 p = p0 + u * s, q = q0 + v * t;
        contact = (p + q) * 0.5f;
        return (p - q).sqrMagnitude;
    }

    public static bool HitsBlade(Vector3 from, Vector3 to, float radius,
        Vector3 bladeStart, Vector3 bladeTip, float bladeRadius, out Vector3 contact)
    {
        float r = radius + bladeRadius;
        return SegmentDistanceSquared(from, to, bladeStart, bladeTip, out contact) <= r * r;
    }

    public static bool HitsPlayer(Vector3 from, Vector3 to, float radius, PlayerStats target,
        CharacterController body)
    {
        if (target == null || !target.IsAlive) return false;
        Vector3 center = body != null ? body.transform.TransformPoint(body.center)
            : target.transform.position + Vector3.up;
        float half = body != null ? Mathf.Max(0f, body.height * 0.5f - body.radius) : 0f;
        float r = radius + (body != null ? body.radius : 0.5f);
        Vector3 contact;
        return SegmentDistanceSquared(from, to, center - Vector3.up * half,
            center + Vector3.up * half, out contact) <= r * r;
    }

    public static bool HitsEnvironment(Vector3 from, Vector3 to, float radius, Transform owner)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.0001f) return false;
        int count = Physics.SphereCastNonAlloc(from, radius, delta / distance, Obstacles,
            distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = Obstacles[i].collider;
            if (owner != null && c.transform.IsChildOf(owner)) continue;
            Damageable d;
            if (Damageable.TryGet(c, out d)) continue;
            if (c.GetComponentInParent<PlayerStats>() != null
                || c.GetComponentInParent<FlyingSword>() != null
                || c.GetComponentInParent<EnemyBlade>() != null) continue;
            return true;
        }
        return false;
    }
}
