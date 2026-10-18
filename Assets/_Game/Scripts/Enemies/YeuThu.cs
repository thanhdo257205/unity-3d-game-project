using UnityEngine;

/// <summary>Yêu thú Warrok: tuần tra, đuổi theo và đánh cận chiến bằng các clip Creature Pack.</summary>
public class YeuThu : EnemyBase
{
    [SerializeField] private Animator animator;
    [SerializeField, Min(0.1f)] private float chaseSpeed = 4.5f;
    [SerializeField, Min(0.5f)] private float attackRange = 2.6f;
    [SerializeField, Min(0f)] private float attackDamage = 14f;
    [SerializeField, Min(0f)] private float recoveryTime = 0.65f;
    [SerializeField] private float[] attackDurations = { 2.67f, 1.1f, 3.7f, 3.8f };
    [SerializeField] private float[] hitFractions = { 0.43f, 0.45f, 0.52f, 0.5f };

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly string[] AttackStates = { "Swipe", "Punch", "JumpAttack", "Slam" };
    private bool attacking, hitDealt;
    private float attackStarted, attackDuration, hitTime, nextAttack, nextPath;
    private int attackIndex;

    public bool AttackInProgress => attacking;

    protected override void Awake()
    {
        base.Awake();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        agent.acceleration = 24f;
        agent.angularSpeed = 420f;
    }

    protected override void OnAttack()
    {
        if (attacking)
        {
            StopMoving();
            float elapsed = Time.time - attackStarted;
            // Khóa hướng sau động tác chuẩn bị để người chơi có thể né sang bên.
            if (elapsed < 0.25f) FacePlayer(360f);
            if (!hitDealt && elapsed >= hitTime)
            {
                hitDealt = true;
                if (CanHitPlayer()) DealDamageToPlayer(attackDamage * (attackIndex >= 2 ? 1.4f : 1f));
            }
            if (elapsed >= attackDuration)
            {
                attacking = false;
                nextAttack = Time.time + recoveryTime;
                attackIndex = (attackIndex + 1) % AttackStates.Length;
                Play("Locomotion");
            }
            return;
        }

        Vector3 delta = playerT.position - transform.position;
        float horizontal = new Vector2(delta.x, delta.z).magnitude;
        if (horizontal > attackRange || Mathf.Abs(delta.y) > 2.2f)
        {
            if (Time.time >= nextPath) { MoveTo(playerT.position, chaseSpeed); nextPath = Time.time + 0.2f; }
            return;
        }
        StopMoving();
        FacePlayer(420f);
        if (Time.time < nextAttack) return;

        attacking = true;
        hitDealt = false;
        attackStarted = Time.time;
        attackDuration = attackDurations != null && attackIndex < attackDurations.Length
            ? Mathf.Max(0.2f, attackDurations[attackIndex]) : 2f;
        float fraction = hitFractions != null && attackIndex < hitFractions.Length ? hitFractions[attackIndex] : 0.5f;
        hitTime = attackDuration * Mathf.Clamp01(fraction);
        Play(AttackStates[attackIndex]);
    }

    private bool CanHitPlayer()
    {
        if (player == null || !player.IsAlive) return false;
        Vector3 delta = PlayerChest - Eye;
        if (delta.magnitude > attackRange + 0.55f) return false;
        Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
        if (flat.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, flat) > 65f) return false;
        if (!Physics.Linecast(Eye, PlayerChest, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)) return true;
        return hit.transform.IsChildOf(playerT) || hit.transform.IsChildOf(transform);
    }

    protected override void OnEnterState(EnemyState from, EnemyState to)
    {
        if (to == EnemyState.Detect) { StopMoving(); Play("Roar"); }
        else if (to == EnemyState.Attack || to == EnemyState.Patrol || to == EnemyState.Staggered) Play("Locomotion");
    }

    protected override void CancelActions()
    {
        attacking = false;
        hitDealt = true;
        nextAttack = Time.time + recoveryTime;
    }

    protected override void OnDiedExtra() { Play("Die", 0.12f); }

    protected override void OnRevivedExtra()
    {
        attacking = false;
        attackIndex = 0;
        nextAttack = nextPath = 0f;
        Play("Locomotion");
    }

    private void LateUpdate()
    {
        if (animator == null) return;
        float speed = IsAlive && !attacking && AIEnabled && agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
        animator.SetFloat(SpeedId, speed, 0.12f, Time.deltaTime);
        animator.speed = AIEnabled || !IsAlive ? 1f : 0f;
    }

    private void Play(string stateName, float fade = 0.15f)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.CrossFadeInFixedTime(stateName, fade, 0);
    }
}
