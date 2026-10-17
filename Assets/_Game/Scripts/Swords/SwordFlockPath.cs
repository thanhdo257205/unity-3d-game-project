using UnityEngine;

/// <summary>One shared flight lane: straight entry, then a smooth horizontal loop through the target.</summary>
public sealed class SwordFlockPath
{
    private const int Samples = 128;
    private readonly float[] lengths = new float[Samples + 1];
    private Vector3 forward, right, center, entryOffset;
    private float started, entryLength, lastTime;
    private int nextSlot;
    private bool initialized;
    private Damageable target;

    public int Join(SwordSwarm swarm, FlyingSword sword, Damageable destination, Vector3 aimPoint)
    {
        bool existing = false;
        foreach (FlyingSword other in swarm.Swords)
            if (other != sword && other.HasPursuitCommand && other.PursuitTarget == destination)
            { existing = true; break; }
        if (!initialized || !existing || target != destination)
        {
            initialized = true;
            target = destination;
            center = aimPoint;
            forward = Vector3.ProjectOnPlane(center - sword.transform.position, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            right = Vector3.Cross(Vector3.up, forward);
            entryOffset = sword.transform.position - center;
            entryLength = Mathf.Max(0.1f, entryOffset.magnitude);
            started = lastTime = Time.time;
            nextSlot = 0;
            lengths[0] = 0f;
            Vector3 previous = Loop(0f);
            for (int i = 1; i <= Samples; i++)
            {
                Vector3 point = Loop(i * Mathf.PI * 2f / Samples);
                lengths[i] = lengths[i - 1] + Vector3.Distance(previous, point);
                previous = point;
            }
        }
        return nextSlot++;
    }

    // Three narrow files share the same route, staggered along it rather than independent oscillations.
    public Vector3 Sample(Damageable destination, int slot, float speed, out Vector3 tangent)
    {
        if (destination != null && Time.time > lastTime)
        {
            center = Vector3.Lerp(center, destination.AimPoint, 1f - Mathf.Exp(-16f * (Time.time - lastTime)));
            lastTime = Time.time;
        }
        float distance = (Time.time - started) * speed - (slot / 3) * 0.8f;
        Vector3 point = AtDistance(distance);
        tangent = (AtDistance(distance + 0.08f) - point).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
        return point + side * ((slot % 3 - 1) * 0.18f);
    }

    private Vector3 AtDistance(float distance)
    {
        if (distance < entryLength) return center + entryOffset * (1f - distance / entryLength);
        float along = Mathf.Repeat(distance - entryLength, lengths[Samples]);
        int low = 0, high = Samples;
        while (high - low > 1)
        {
            int mid = (low + high) / 2;
            if (lengths[mid] < along) low = mid; else high = mid;
        }
        float fraction = Mathf.InverseLerp(lengths[low], lengths[high], along);
        return center + Loop((low + fraction) * Mathf.PI * 2f / Samples);
    }

    private Vector3 Loop(float angle)
    {
        return forward * (5f * Mathf.Sin(angle)) + right * (2.4f * (1f - Mathf.Cos(angle)));
    }
}
