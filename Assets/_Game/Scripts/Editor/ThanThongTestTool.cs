#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Live checks for costs, actual projectile interception, counter damage and splash behind cover.</summary>
public static class ThanThongTestTool
{
    private class Wait { public float end; public Wait(float seconds) { end = Time.time + seconds; } }
    private static IEnumerator test;
    private static object step;
    private static float deadline;
    private static StringBuilder report = new StringBuilder();
    public static bool Running => test != null;
    public static string Report => report.ToString();
    [MenuItem("Phàm Kiếm Lục/Thần thông/Kiểm thử trong Play Mode")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || Running) return;
        report.Clear(); Application.runInBackground = true; deadline = Time.realtimeSinceStartup + 40;
        test = Tests(); step = null; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying || Time.realtimeSinceStartup > deadline) throw new Exception("Test timed out or Play stopped");
            EditorApplication.QueuePlayerLoopUpdate();
            if (step is Wait && Time.time < ((Wait)step).end) return;
            if (!test.MoveNext()) { Finish(); return; }
            step = test.Current;
        }
        catch (Exception e) { Check(false, e.ToString()); Finish(); }
    }
    private static void Finish()
    {
        EditorApplication.update -= Tick;
        if (test is IDisposable) ((IDisposable)test).Dispose(); test = null; step = null;
        SessionState.SetString("ThanThongTestReport", Report);
    }
    private static void Check(bool result, string label) { report.AppendLine((result ? "PASS " : "FAIL ") + label); Debug.Log("[ThanThong] " + (result ? "PASS " : "FAIL ") + label); }
    private static GameObject Target(Vector3 position, List<GameObject> temporary)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "ThanThong_TestTarget"; go.transform.position = position;
        go.AddComponent<Damageable>(); Physics.SyncTransforms(); temporary.Add(go); return go;
    }
    private static IEnumerator Tests()
    {
        var skill = ThanThongCombat.Instance; var stats = PlayerStats.Instance; var player = stats.GetComponent<PlayerController>();
        var original = player.transform.position; var rotation = player.transform.rotation;
        bool mana = AdminPanel.PhapLucVoHan, god = AdminPanel.MauVoHan, enemyGod = AdminPanel.MauDichVoHan, active = SwordSwarm.Instance.IsActive;
        bool noCooldown = AdminPanel.KhongHoiChieu;
        var enemies = new List<GameObject>(); var temporary = new List<GameObject>();
        try
        {
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyBase>()) { enemies.Add(enemy.gameObject); enemy.gameObject.SetActive(false); }
            AdminPanel.PhapLucVoHan = AdminPanel.MauVoHan = AdminPanel.MauDichVoHan = false;
            SwordSwarm.Instance.SetActive(false, false); skill.EndWard(false);
            Vector3 home = new Vector3(-30, 0.1f, -30); player.TeleportTo(home, Quaternion.identity);
            stats.DrainMana(stats.MaxMana);
            Check(!skill.BeginWard(), "empty mana refuses ward"); stats.RestoreMana(stats.MaxMana);
            float before = stats.Mana;
            Check(skill.BeginWard() && stats.Mana <= before - 7.9f, "ward spends activation mana");
            float health = stats.Health;
            var orbGo = new GameObject("ThanThong_TestOrb"); temporary.Add(orbGo); var orb = orbGo.AddComponent<EnemyOrb>();
            orb.Launch(home + Vector3.up + Vector3.forward * 6, home + Vector3.up, 12, 10, stats);
            yield return new Wait(0.65f);
            Check(orb == null || orb.IsDone, "live orb intercepted before reaching player");
            Check(skill.Absorbed == 1 && Mathf.Approximately(stats.Health, health), "ward absorbs damage and stores one counter charge");
            var missedGo = new GameObject("ThanThong_TestMiss"); temporary.Add(missedGo); var missed = missedGo.AddComponent<EnemyOrb>();
            missed.Launch(home + Vector3.right * 10, home + Vector3.right * 15, 5, 10, stats);
            Check(!ThanThongCombat.TryBlockProjectile(missed, home + Vector3.right * 10, home + Vector3.right * 11), "ward does not block distant projectiles");
            UnityEngine.Object.Destroy(missedGo);
            var enemyOwner = Target(home + Vector3.right * 15, temporary);
            var bladeGo = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/PhiKiemDauKiem.prefab")); temporary.Add(bladeGo);
            var blade = bladeGo.GetComponent<EnemyBlade>(); blade.Setup(enemyOwner.transform, 0, 1, stats);
            blade.transform.position = home + Vector3.up + Vector3.forward * 6; blade.Launch(home + Vector3.up, 14, 10);
            yield return new Wait(0.65f);
            Check(blade.IsDone && skill.Absorbed == 2, "live enemy sword is also intercepted");
            var targetGo = Target(home + Vector3.up + Vector3.forward * 9, temporary); var target = targetGo.GetComponent<Damageable>();
            SwordSwarm.Instance.LockTarget(target);
            skill.EndWard(true);
            Check(!skill.IsGuarding && UnityEngine.Object.FindObjectsByType<LinhPhapProjectile>().Length > 0, "release ward launches stored counterattack");
            yield return new Wait(0.85f);
            Check(target.Health < target.MaxHealth, "counterattack damages locked enemy, health=" + target.Health);
            yield return new Wait(0.5f);
            before = stats.Mana;
            Check(skill.CastLotus() && skill.IsCharging && stats.Mana <= before - 34.9f, "gold lotus charges and spends 35 mana");
            Check(!skill.CastLotus(), "cannot stack casts during charge");
            yield return new Wait(0.2f); Check(skill.IsCharging, "cast has visible windup instead of instant damage");
            float targetBefore = target.Health; yield return new Wait(1.2f);
            Check(target.Health < targetBefore, "charged lotus hits and deals damage");
            Check(!skill.CastLotus() && skill.LotusRemaining > 0, "eight second cooldown enforced");
            SwordSwarm.Instance.LockTarget(null); target.Revive();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); temporary.Add(wall);
            wall.transform.position = home + Vector3.up * 2 + Vector3.forward * 6; wall.transform.localScale = new Vector3(9, 6, 0.6f);
            targetGo.transform.position = home + Vector3.up + Vector3.forward * 8;
            var frontGo = Target(home + Vector3.up + Vector3.forward * 4.8f, temporary); var front = frontGo.GetComponent<Damageable>();
            var shot = UnityEngine.Object.Instantiate(skill.lotusPrefab, home + Vector3.up + Vector3.forward * 2, Quaternion.identity); temporary.Add(shot);
            shot.AddComponent<LinhPhapProjectile>().Launch(stats.transform, target, target.AimPoint, 25, 5, skill.auraMaterial);
            yield return new Wait(0.5f);
            Check(Mathf.Approximately(target.Health, target.MaxHealth), "wall blocks both direct hit and splash behind cover");
            Check(front.Health < front.MaxHealth, "splash still damages exposed enemy on near side of wall");
            frontGo.SetActive(false); target.Revive();
            wall.transform.position = home + Vector3.up * 2 + Vector3.forward * 0.95f;
            targetGo.transform.position = home + Vector3.up + Vector3.forward * 3;
            Physics.SyncTransforms(); SwordSwarm.Instance.LockTarget(target);
            AdminPanel.KhongHoiChieu = true; stats.RestoreMana(stats.MaxMana);
            skill.CastLotus(); yield return new Wait(1.3f);
            Check(Mathf.Approximately(target.Health, target.MaxHealth), "casting against adjacent wall cannot spawn a relic beyond it");
            AdminPanel.KhongHoiChieu = noCooldown;
            stats.RestoreMana(stats.MaxMana); yield return new Wait(0.8f);
            skill.BeginWard(); stats.TakeDamage(stats.Health + 10000); yield return new Wait(0.15f);
            Check(!skill.IsGuarding && !skill.IsCharging, "player death cancels skills");
        }
        finally
        {
            skill.EndWard(false); SwordSwarm.Instance.LockTarget(null);
            foreach (var go in temporary) if (go != null) UnityEngine.Object.Destroy(go);
            foreach (var go in enemies) if (go != null) go.SetActive(true);
            stats.Revive(); player.TeleportTo(original, rotation);
            AdminPanel.PhapLucVoHan = mana; AdminPanel.MauVoHan = god; AdminPanel.MauDichVoHan = enemyGod;
            AdminPanel.KhongHoiChieu = noCooldown;
            SwordSwarm.Instance.SetActive(active, false);
        }
    }
}
#endif
