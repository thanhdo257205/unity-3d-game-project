#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Live integration checks in SanTap Play Mode. Never saves test actors into the scene.</summary>
public static class SwordDuelTestTool
{
    private sealed class Wait
    {
        public readonly float Until;
        public Wait(float seconds) { Until = Time.time + seconds; }
    }
    private static IEnumerator routine;
    private static object current;
    private static float deadline;
    private static readonly StringBuilder report = new StringBuilder();
    public static bool Running => routine != null;
    public static string Report => report.ToString();

    [MenuItem("Phàm Kiếm Lục/Đấu phi kiếm/Kiểm thử trong Play Mode")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || Running) return;
        report.Clear();
        Application.runInBackground = true;
        deadline = Time.realtimeSinceStartup + 90f;
        routine = Tests();
        current = null;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying || Time.realtimeSinceStartup > deadline)
            {
                Check(false, "test run", "Play Mode stopped or timed out");
                Finish();
                return;
            }
            EditorApplication.QueuePlayerLoopUpdate();
            if (current is Wait && Time.time < ((Wait)current).Until) return;
            if (!routine.MoveNext()) { Finish(); return; }
            current = routine.Current;
        }
        catch (Exception error)
        {
            Check(false, "exception", error.ToString());
            Finish();
        }
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        if (routine is IDisposable) ((IDisposable)routine).Dispose();
        routine = null;
        current = null;
        SessionState.SetString("SwordDuelReport", report.ToString());
    }

    private static void Check(bool ok, string name, string detail)
    {
        string line = (ok ? "PASS " : "FAIL ") + name + ": " + detail;
        report.AppendLine(line);
        Debug.Log("[SwordDuel] " + line);
        SessionState.SetString("SwordDuelReport", report.ToString());
    }

    private static void Set(object obj, string name, object value)
    {
        obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    }

    private static IEnumerator Tests()
    {
        PlayerStats stats = PlayerStats.Instance;
        PlayerController player = stats.GetComponent<PlayerController>();
        SwordSwarm swarm = SwordSwarm.Instance;
        Vector3 originalPosition = player.transform.position;
        Quaternion originalRotation = player.transform.rotation;
        bool wasActive = swarm.IsActive, oldMana = AdminPanel.PhapLucVoHan, oldHealth = AdminPanel.MauVoHan;
        bool oldAI = EnemyBase.AIEnabled;
        var disabled = new List<GameObject>();
        var temporary = new List<GameObject>();
        int playerHits = 0;
        Action<float> onDamage = value => playerHits++;
        stats.Damaged += onDamage;
        try
        {
            foreach (EnemyBase enemy in UnityEngine.Object.FindObjectsByType<EnemyBase>())
            { disabled.Add(enemy.gameObject); enemy.gameObject.SetActive(false); }
            AdminPanel.PhapLucVoHan = AdminPanel.MauVoHan = true;
            EnemyBase.AIEnabled = true;
            Vector3 home = new Vector3(-20f, 0.1f, -25f);
            player.TeleportTo(home, Quaternion.identity);
            swarm.SetFormation(FormationType.Wheel);
            swarm.SetActive(true, true);
            swarm.SwordCount = 20;
            yield return new Wait(2f);

            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy.name = "Duel_TestMovingTarget";
            dummy.transform.localScale = new Vector3(0.9f, 1.7f, 0.9f);
            dummy.transform.position = home + new Vector3(0f, 1f, 13f);
            Damageable target = dummy.AddComponent<Damageable>();
            Set(target, "maxHealth", 1000f);
            target.Revive();
            temporary.Add(dummy);
            FlyingSword sword = swarm.Swords[0];
            sword.LaunchPursuit(target, 26f, 8f, 0.25f);
            float start = Time.time;
            while (Time.time - start < 4.5f)
            {
                dummy.transform.position = home + new Vector3(Mathf.Min(5f, (Time.time - start) * 3f), 1f, 13f);
                yield return null;
            }
            Check(sword.SuccessfulPasses >= 2 && target.Health < target.MaxHealth,
                "player moving-target pursuit", "passes=" + sword.SuccessfulPasses + ", health=" + target.Health
                + ", mode=" + sword.Mode + ", position=" + sword.transform.position);
            yield return new Wait(9f);
            Check(sword.HasPursuitCommand && target.Health <= target.MaxHealth - 48f,
                "pursuit continues beyond twelve seconds", "command=" + sword.HasPursuitCommand + ", health=" + target.Health);
            sword.Deflect(0.22f);
            Check(sword.HasPursuitCommand && sword.Mode == SwordMode.Strike, "deflection stays at battle without recall",
                "mode=" + sword.Mode + ", command=" + sword.HasPursuitCommand);
            yield return new Wait(1.8f);
            Check(sword.IsAttacking && sword.PursuitTarget == target, "deflected sword relaunches automatically",
                "mode=" + sword.Mode + ", target=" + sword.PursuitTarget);
            swarm.RecallAll();
            yield return new Wait(2f);
            Check(!sword.IsAttacking && !sword.HasPursuitCommand && sword.PursuitTarget == null,
                "manual recall cancels pursuit order", "mode=" + sword.Mode);

            dummy.transform.position = home + new Vector3(0f, 1f, 70f);
            sword.LaunchPursuit(target, 26f, 8f, 0.25f);
            yield return new Wait(0.15f);
            Check(sword.IsAttacking && sword.HasPursuitCommand, "existing pursuit survives lock-range limit",
                "distance=" + Vector3.Distance(dummy.transform.position, player.transform.position));
            swarm.RecallAll();

            dummy.SetActive(false);
            GameObject actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/TaTu.prefab"),
                home + Vector3.forward * 12f, Quaternion.Euler(0f, 180f, 0f));
            actor.name = "Duel_TestTaTu";
            temporary.Add(actor);
            yield return new Wait(0.4f);
            TaTu enemyActor = actor.GetComponent<TaTu>();
            actor.GetComponent<NavMeshAgent>().enabled = false;
            Set(enemyActor, "nextSwordTime", Time.time + 999f);
            Set(enemyActor, "nextFireballTime", Time.time + 999f);
            Damageable enemyLife = actor.GetComponent<Damageable>();
            Set(enemyLife, "maxHealth", 1000f);
            enemyLife.Revive();
            float health = enemyLife.Health;
            int enemyBlocks = enemyActor.BlocksMade;
            sword.LaunchPursuit(enemyLife, 26f, 8f, 0.25f);
            yield return new Wait(1.8f);
            Check(enemyActor.BlocksMade > enemyBlocks && Mathf.Approximately(health, enemyLife.Health),
                "TaTu intercepts player sword", "blocks=" + enemyActor.BlocksMade + ", health=" + enemyLife.Health);
            swarm.RecallAll();
            yield return new Wait(1f);

            swarm.LockTarget(enemyLife);
            Set(enemyActor, "nextSwordTime", Time.time + 0.1f);
            swarm.FireVolley();
            bool counterattacked = false;
            int maxGuards = 0;
            start = Time.time;
            while (Time.time - start < 1.2f)
            {
                int guards = 0;
                for (int i = 0; i < enemyActor.Blades.Count; i++)
                {
                    if (!enemyActor.Blades[i].IsDone) counterattacked = true;
                    if (enemyActor.Blades[i].IsGuarding) guards++;
                }
                maxGuards = Mathf.Max(maxGuards, guards);
                yield return null;
            }
            Check(counterattacked && maxGuards <= 2, "enemy counterattacks under pressure",
                "counterattacked=" + counterattacked + ", maximum active guards=" + maxGuards);
            Set(enemyActor, "nextSwordTime", Time.time + 999f);
            swarm.RecallAll();
            swarm.LockTarget(null);
            EnemyBlade.RecallAll();
            yield return new Wait(1f);

            Set(enemyActor, "swordDefense", false);
            health = enemyLife.Health;
            sword.LaunchPursuit(enemyLife, 26f, 8f, 0.25f);
            yield return new Wait(1.8f);
            Check(enemyLife.Health < health, "unprotected enemy takes damage", "health=" + enemyLife.Health);
            swarm.RecallAll();
            yield return new Wait(1f);

            int playerBlocks = swarm.BlocksMade;
            int beforeHits = playerHits;
            EnemyBlade enemySword = enemyActor.Blades[0];
            enemySword.ResetToOrbit();
            enemySword.Launch(stats.transform.position + Vector3.up, 24f, 8f);
            yield return new Wait(1.5f);
            Check(swarm.BlocksMade > playerBlocks && playerHits == beforeHits,
                "player front interception", "blocks=" + (swarm.BlocksMade - playerBlocks) + ", incoming hits=" + (playerHits - beforeHits));

            EnemyOrb orb = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/EnemyOrb.prefab"))
                .GetComponent<EnemyOrb>();
            playerBlocks = swarm.BlocksMade;
            beforeHits = playerHits;
            orb.Launch(home + new Vector3(0f, 1.1f, 10f), home + Vector3.up * 1.1f, 10f, 8f, stats);
            yield return new Wait(1.4f);
            Check(swarm.BlocksMade > playerBlocks && playerHits == beforeHits, "orb interception remains functional",
                "blocks=" + (swarm.BlocksMade - playerBlocks) + ", hits=" + (playerHits - beforeHits));

            swarm.SetActive(false, false);
            enemySword.ResetToOrbit();
            beforeHits = playerHits;
            enemySword.Launch(stats.transform.position + Vector3.up, 24f, 8f);
            start = Time.time;
            while (Time.time - start < 4f)
            {
                player.TeleportTo(home + Vector3.right * Mathf.Min(4f, (Time.time - start) * 2.5f), Quaternion.identity);
                yield return null;
            }
            Check(enemySword.SuccessfulPasses >= 2 && playerHits > beforeHits,
                "enemy moving-player pursuit", "passes=" + enemySword.SuccessfulPasses + ", hits=" + (playerHits - beforeHits));
            EnemyBlade.RecallAll();
            player.TeleportTo(home, Quaternion.identity);
            swarm.SetActive(true, true);
            yield return new Wait(2f);
            sword = swarm.Swords[0];
            dummy.SetActive(true);
            dummy.transform.position = home + new Vector3(0f, 1f, 13f);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Duel_TestWall";
            wall.transform.position = home + new Vector3(0f, 2f, 6f);
            wall.transform.localScale = new Vector3(10f, 5f, 1f);
            temporary.Add(wall);
            health = target.Health;
            sword.LaunchPursuit(target, 26f, 8f, 0.25f);
            yield return new Wait(1.3f);
            Check(sword.HasPursuitCommand && Mathf.Approximately(health, target.Health),
                "sword cannot damage through wall while retrying", "mode=" + sword.Mode + ", target health=" + target.Health);
            wall.SetActive(false);
            dummy.SetActive(false);
            swarm.RecallAll();
            yield return new Wait(1f);

            Vector3 center = home + Vector3.up * 5f;
            player.TeleportTo(center - Vector3.right * 5f - Vector3.up, Quaternion.identity);
            player.FlightActive = true;
            sword.transform.position = center - Vector3.right;
            sword.LaunchStrike(center + Vector3.right * 3f, 12f, 8f, 0.25f);
            int deflectionsBeforeClash = sword.Deflections;
            enemySword.ResetToOrbit();
            enemySword.transform.position = center + Vector3.right;
            enemySword.Launch(center - Vector3.right * 3f, 12f, 8f);
            bool sweptClash = EnemyBlade.TryClash(sword, center - Vector3.right, center + Vector3.right * 2f);
            Check(sweptClash && sword.Deflections > deflectionsBeforeClash && sword.HasPursuitCommand && enemySword.IsDone, "mid-air sword clash preserves deployed command",
                "player=" + sword.Mode + ", enemy done=" + enemySword.IsDone);
            player.FlightActive = false;
            player.TeleportTo(home, Quaternion.identity);
            swarm.RecallAll();
            EnemyBlade.RecallAll();
            yield return new Wait(1f);

            swarm.LockTarget(enemyLife);
            bool fired = swarm.FireVolley();
            yield return new Wait(0.7f);
            Check(fired && swarm.ReadySwordCount >= 4, "volley leaves defensive reserve",
                "ready=" + swarm.ReadySwordCount + "/" + swarm.ActualSwordCount);
            swarm.RecallAll();
            enemyLife.TakeDamage(new DamageInfo { amount = enemyLife.Health * 100f });
            yield return new Wait(0.3f);
            bool stopped = true;
            for (int i = 0; i < enemyActor.Blades.Count; i++)
                if (enemyActor.Blades[i].gameObject.activeInHierarchy) stopped = false;
            Check(stopped, "dead enemy removes sword threats", "all enemy blades inactive=" + stopped);
            Check(swarm.LockedTarget == null, "dead target unlocks", "locked=" + swarm.LockedTarget);

            yield return new Wait(1f);
            FlyingSword pending = swarm.Swords[0];
            pending.ScheduleStrike(Time.time + 0.3f, enemyLife.AimPoint, 3f, 26f, 8f, 0.25f, 0f, enemyLife);
            yield return new Wait(0.6f);
            Check(!pending.IsAttacking && !pending.HasScheduledStrike, "dead target cancels scheduled launch",
                "mode=" + pending.Mode + ", scheduled=" + pending.HasScheduledStrike);

            dummy.SetActive(true);
            if (swarm.Swords.Count > 0) swarm.Swords[0].LaunchPursuit(target, 26f, 8f, 0.25f);
            AdminPanel.PhapLucVoHan = false;
            stats.DrainMana(stats.MaxMana);
            yield return new Wait(1.5f);
            Check(swarm.IsActive && swarm.Swords[0].HasPursuitCommand, "mana exhaustion does not recall deployed swords",
                "active=" + swarm.IsActive + ", swords=" + swarm.ActualSwordCount);
            swarm.Toggle();
            Check(swarm.IsActive, "Q cannot recall a deployed attack", "active=" + swarm.IsActive);
            AdminPanel.PhapLucVoHan = true;
            target.TakeDamage(new DamageInfo { amount = target.Health + 100f });
            yield return new Wait(0.4f);
            Check(!swarm.Swords[0].HasPursuitCommand && swarm.Swords[0].Mode != SwordMode.Strike,
                "dead target automatically recalls its sword", "mode=" + swarm.Swords[0].Mode);
            AdminPanel.PhapLucVoHan = true;
            var replacement = GameObject.CreatePrimitive(PrimitiveType.Cube);
            replacement.name = "Duel_ReplacementTarget";
            replacement.transform.position = home + new Vector3(3f, 1f, 10f);
            temporary.Add(replacement);
            var replacementLife = replacement.AddComponent<Damageable>();
            swarm.Swords[0].LaunchStrike(replacement.transform.position, 26f, 8f, 0.25f);
            swarm.LockTarget(replacementLife);
            yield return new Wait(0.3f);
            Check(swarm.Swords[0].PursuitTarget == replacementLife, "deployed sword accepts a new locked target",
                "target=" + swarm.Swords[0].PursuitTarget);
            swarm.RecallAll();
            yield return new Wait(1.5f);
            Check(!swarm.Swords[0].HasPursuitCommand, "R ends the persistent command", "mode=" + swarm.Swords[0].Mode);
            swarm.LockTarget(null);
            sword.LaunchStrike(home + new Vector3(0, 3, 7), 26f, 8f, 0.25f);
            yield return new Wait(1f);
            Check(sword.Mode == SwordMode.Strike && sword.HasPursuitCommand,
                "untargeted launch remains in the battle", "mode=" + sword.Mode);

            swarm.RecallAll();
            yield return new Wait(1.5f);
            target.Revive();
            dummy.transform.position = home + new Vector3(0, 1, 13);
            Set(target, "maxHealth", 10000f);
            target.Revive();
            var flock = new List<FlyingSword>();
            for (int k = 0; k < Mathf.Min(12, swarm.Swords.Count); k++)
            {
                flock.Add(swarm.Swords[k]);
                swarm.Swords[k].LaunchPursuit(target, 26f, 2f, 0.25f);
            }
            yield return new Wait(2f);
            float largestSpan = 0f;
            for (int tick = 0; tick < 50; tick++)
            {
                for (int a = 0; a < flock.Count; a++)
                    for (int b = a + 1; b < flock.Count; b++)
                        largestSpan = Mathf.Max(largestSpan,
                            Vector3.Distance(flock[a].transform.position, flock[b].transform.position));
                yield return null;
            }
            Check(largestSpan < 4.5f, "twelve swords keep a compact flock through turns", "maximum span=" + largestSpan);
            flock[3].Deflect(0.25f);
            yield return new Wait(0.8f);
            Check(Vector3.Distance(flock[3].transform.position, flock[4].transform.position) < 2.5f,
                "blocked blade stays with its flock", "separation=" + Vector3.Distance(flock[3].transform.position, flock[4].transform.position));

        }
        finally
        {
            stats.Damaged -= onDamage;
            player.FlightActive = false;
            swarm.RecallAll();
            swarm.LockTarget(null);
            EnemyBlade.RecallAll();
            foreach (GameObject go in temporary) if (go != null) UnityEngine.Object.Destroy(go);
            foreach (GameObject go in disabled) if (go != null) go.SetActive(true);
            AdminPanel.PhapLucVoHan = oldMana;
            AdminPanel.MauVoHan = oldHealth;
            EnemyBase.AIEnabled = oldAI;
            player.TeleportTo(originalPosition, originalRotation);
            swarm.SetActive(wasActive, false);
        }
    }
}
#endif
