using System.Collections.Generic;
using UnityEngine;

/// <summary>Spinning lotus relic, swept collision, one blast per flight and line-of-sight limited splash.</summary>
public class LinhPhapProjectile : MonoBehaviour
{
    private Transform owner;
    private Damageable target;
    private Vector3 direction;
    private Material ribbon;
    private float damage, blastRadius, age;
    private bool done;
    private TienHiepVfx flightFx;
    private static readonly RaycastHit[] Hits = new RaycastHit[40];
    private readonly HashSet<Damageable> struck = new HashSet<Damageable>();
    public bool Finished => done;

    public void Launch(Transform caster, Damageable lockTarget, Vector3 aim, float amount, float radius, Material trailMaterial)
    {
        owner = caster; target = lockTarget; damage = amount; blastRadius = radius; ribbon = trailMaterial;
        direction = (aim - transform.position).normalized;
        if (direction.sqrMagnitude < 0.001f) direction = caster.forward;
        flightFx = GetComponent<TienHiepVfx>(); if (flightFx != null) flightFx.Fly(direction);
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("KimLien_Vet" + i); go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(i == 2 ? 0.13f : 0, 0.16f, 0);
            var trail = go.AddComponent<TrailRenderer>(); trail.sharedMaterial = ribbon;
            trail.time = i == 0 ? 0.32f : 0.65f; trail.minVertexDistance = 0.035f; trail.numCornerVertices = 3;
            trail.widthMultiplier = i == 0 ? 0.035f : i == 1 ? 0.38f : 0.065f;
            trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            trail.startColor = new Color(1f, i == 0 ? 0.8f : 0.48f, i == 0 ? 0.3f : 0.07f, i == 1 ? 0.3f : 0.65f);
            trail.endColor = new Color(1f, 0.35f, 0.05f, 0);
        }
    }

    private void Update()
    {
        if (done) return;
        if (owner == null || (owner.GetComponent<PlayerStats>() != null && !owner.GetComponent<PlayerStats>().IsAlive))
        { Destroy(gameObject); return; }
        // Sphere casts cover the entire frame, so low frame rates must not slow the relic down.
        float dt = Time.deltaTime; age += dt;
        if (age > 4f) { Destroy(gameObject); return; }
        if (target != null && target.IsAlive && target.isActiveAndEnabled)
            direction = Vector3.RotateTowards(direction, (target.AimPoint - transform.position).normalized, 2.4f * dt, 0f);
        Vector3 from = transform.position, to = from + direction * 27f * dt;
        int n = Physics.SphereCastNonAlloc(from, 0.4f, direction, Hits, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore);
        float closest = float.MaxValue; Vector3 contact = to; bool collided = false;
        for (int i = 0; i < n; i++)
        {
            var hit = Hits[i];
            if (hit.collider.transform.IsChildOf(owner) || hit.collider.GetComponentInParent<FlyingSword>() != null) continue;
            if (hit.distance >= closest) continue;
            closest = hit.distance; contact = from + direction * hit.distance; collided = true;
        }
        if (collided) { Impact(contact); return; }
        transform.position = to;
        if (flightFx != null) flightFx.SetFlightDirection(direction);
        if (transform.childCount > 0) transform.GetChild(0).Rotate(Vector3.up, 320f * dt, Space.World);
    }

    private void Impact(Vector3 point)
    {
        if (done) return; done = true;
        // Evaluate a snapshot; damage callbacks may remove entries from Damageable.All.
        var candidates = Damageable.All.ToArray();
        foreach (var life in candidates)
        {
            if (life == null || !life.IsAlive || life.transform.IsChildOf(owner) || struck.Contains(life)) continue;
            if ((life.AimPoint - point).sqrMagnitude > blastRadius * blastRadius) continue;
            if (SwordFlightMath.HitsEnvironment(point, life.AimPoint, 0.03f, owner)) continue;
            struck.Add(life);
            Vector3 away = life.AimPoint - point; if (away.sqrMagnitude < 0.001f) away = direction;
            life.TakeDamage(new DamageInfo { amount = damage, point = life.AimPoint, direction = away.normalized, melee = false });
            if (life.IsAlive) life.Knockback(away.normalized * 1.2f);
        }
        LinhPhapVisual.Impact(point, ribbon, new Color(1f, 0.52f, 0.08f, 0.65f), blastRadius, direction);
        var feedback = owner != null ? owner.GetComponent<HitFeedback>() : null;
        if (feedback != null) feedback.Shake(0.045f);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        foreach (var trail in GetComponentsInChildren<TrailRenderer>())
        {
            trail.emitting = false; trail.transform.SetParent(null, true); Destroy(trail.gameObject, trail.time + 0.1f);
        }
    }
}
