#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Integration checks and exact-time visual samples; run in a fresh SanTap Play session.</summary>
public static class ReferenceCombatValidation
{
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static string LastReport { get; private set; }
    private static readonly StringBuilder Report = new StringBuilder();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("[NCK FAIL] " + message);
        Report.AppendLine("PASS " + message);
    }
    private static void Invoke(object instance, string name) => instance.GetType().GetMethod(name, Private).Invoke(instance, null);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Private).SetValue(instance, value);

    private sealed class ProbeProjectile : IEnemyProjectile
    {
        public Vector3 Position => Vector3.zero;
        public float Radius => .15f;
        public bool IsDone { get; private set; }
        public bool HasGuard { get; set; }
        public Vector3 Direction => Vector3.back;
        public bool IsApproaching(Vector3 point) => true;
        public void Block() { IsDone = true; }
    }

    [MenuItem("Phàm Kiếm Lục/Chiêu theo video/Kiểm tra gameplay trong Play")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new Exception("Cần Play trong SanTap.");
        Report.Clear();
        var caster = Object.FindFirstObjectByType<NamCungCombat>();
        Check(caster != null && caster.skills.Length == 7, "seven player skills installed");
        var stats = caster.GetComponent<PlayerStats>(); var player = caster.GetComponent<PlayerController>();
        bool ai = EnemyBase.AIEnabled, god = AdminPanel.MauVoHan, manaGod = AdminPanel.PhapLucVoHan, cooldownGod = AdminPanel.KhongHoiChieu;
        Vector3 previousPosition = player.transform.position; Quaternion previousRotation = player.transform.rotation;
        var spawned = new System.Collections.Generic.List<GameObject>();
        try
        {
            EnemyBase.AIEnabled = false; AdminPanel.MauVoHan = true; AdminPanel.PhapLucVoHan = false; AdminPanel.KhongHoiChieu = false;
            player.TeleportTo(new Vector3(30, 20, -45), Quaternion.identity);
            Check(caster.skills.Select(s => s.key).Distinct().Count() == 7, "all spell keys unique");
            Check(caster.skills.All(s => s.motion != null && s.motion.humanMotion), "all Blender clips are humanoid");
            foreach (var skill in caster.skills)
            {
                int index = Array.IndexOf(caster.skills, skill);
                stats.RestoreMana(stats.MaxMana); float before = stats.Mana;
                Check(caster.TryCast(index), skill.kind + " starts");
                Check(Mathf.Abs((before - stats.Mana) - skill.manaCost) < .01f, skill.kind + " exact mana cost");
                Check(player.ControlLocked, skill.kind + " cast owns movement lock");
                caster.CancelCast();
                Check(!player.ControlLocked && !caster.IsCasting, skill.kind + " cancel returns control");
                Check(!caster.TryCast(index), skill.kind + " cooldown rejects repeat");
            }

            Vector3 origin = player.transform.position + Vector3.up * 1.3f;
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube); target.name = "NCK_TestTarget"; spawned.Add(target);
            target.transform.position = origin + Vector3.forward * 6;
            var life = target.AddComponent<Damageable>();
            var health = new SerializedObject(life); health.FindProperty("maxHealth").floatValue = 1000; health.ApplyModifiedPropertiesWithoutUndo(); life.Revive();
            Physics.SyncTransforms();
            foreach (var skill in caster.skills)
            {
                var go = new GameObject("NCK_TestFx"); spawned.Add(go);
                var fx = go.AddComponent<ReferenceSkillFx>(); fx.Initialize(caster, skill, origin, target.transform.position, false);
                fx.enabled = false; float before = life.Health;
                if (skill.kind == ReferenceSkillKind.BloodSword)
                    typeof(ReferenceSkillFx).GetMethod("AdvanceProjectile", Private).Invoke(fx, new object[] { 0f, .4f });
                else
                {
                    Set(fx, "age", skill.kind == ReferenceSkillKind.Reincarnation ? 1.2f : skill.kind == ReferenceSkillKind.Mirrors ? .9f : .4f);
                    Invoke(fx, "ApplyGameplay");
                }
                Check(life.Health < before, skill.kind + " gameplay reaches target");
                float after = life.Health; Invoke(fx, "ApplyGameplay");
                Check(Mathf.Abs(after - life.Health) < .001f, skill.kind + " no duplicate hit in same tick");
                Object.DestroyImmediate(go); spawned.Remove(go);

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "NCK_TestWall"; spawned.Add(wall);
                wall.transform.position = origin + Vector3.forward * 3; wall.transform.localScale = new Vector3(8, 12, .5f); Physics.SyncTransforms();
                go = new GameObject("NCK_BlockedFx"); spawned.Add(go); fx = go.AddComponent<ReferenceSkillFx>();
                fx.Initialize(caster, skill, origin, target.transform.position, false); fx.enabled = false;
                before = life.Health;
                if (skill.kind == ReferenceSkillKind.BloodSword)
                    typeof(ReferenceSkillFx).GetMethod("AdvanceProjectile", Private).Invoke(fx, new object[] { 0f, .4f });
                else { Set(fx, "age", 1.2f); Invoke(fx, "ApplyGameplay"); }
                Check(fx.WallClipped && Mathf.Abs(before - life.Health) < .001f, skill.kind + " wall blocks damage");
                Object.DestroyImmediate(go); spawned.Remove(go); Object.DestroyImmediate(wall); spawned.Remove(wall);
            }

            var animator = caster.GetComponentInChildren<Animator>(); int layer = animator.GetLayerIndex("NCK_Reference");
            Vector3 leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            Vector3 rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
            animator.SetLayerWeight(layer, 1);
            foreach (var skill in caster.skills)
            {
                animator.SetFloat("NCKTime", .56f); animator.Play(skill.kind.ToString(), layer, 0); animator.Update(.001f);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                Check(!float.IsNaN(head.position.x) && Vector3.Distance(head.position, player.transform.position) < 3,
                    skill.kind + " finite retarget pose");
                Check(Vector3.Distance(leftFoot, animator.GetBoneTransform(HumanBodyBones.LeftFoot).position) < .01f
                    && Vector3.Distance(rightFoot, animator.GetBoneTransform(HumanBodyBones.RightFoot).position) < .01f,
                    skill.kind + " upper-body mask preserves feet");
            }
            animator.SetLayerWeight(layer, 0);

            AdminPanel.KhongHoiChieu = true; stats.RestoreMana(stats.MaxMana);
            Check(caster.TryCast(0), "release gate test starts");
            Set(caster, "started", Time.time - caster.skills[0].chargeTime * .5f); Invoke(caster, "Update");
            Check(!(bool)typeof(NamCungCombat).GetField("released", Private).GetValue(caster), "no release before charge marker");
            Set(caster, "started", Time.time - caster.skills[0].chargeTime - .01f); Invoke(caster, "Update");
            Check((bool)typeof(NamCungCombat).GetField("released", Private).GetValue(caster), "VFX released at Blender pose marker");
            Set(caster, "started", Time.time - caster.skills[0].chargeTime - caster.skills[0].recoveryTime - .01f); Invoke(caster, "Update");
            Check(!caster.IsCasting && !player.ControlLocked, "recovery returns movement control");

            stats.RestoreMana(stats.MaxMana); Check(caster.TryCast(1), "mirror guard test starts");
            Set(caster, "started", Time.time - caster.skills[1].chargeTime - .01f); Invoke(caster, "Update");
            var projectile = new ProbeProjectile(); Vector3 center = player.transform.position + Vector3.up * 1.3f;
            Check(ThanThongCombat.TryBlockProjectile(projectile, center + Vector3.forward * 5, center - Vector3.forward) && projectile.IsDone,
                "new mirror guard blocks through existing projectile pipeline");
            projectile = new ProbeProjectile();
            Check(!ThanThongCombat.TryBlockProjectile(projectile, center + Vector3.right * 8 + Vector3.forward * 5, center + Vector3.right * 8 - Vector3.forward),
                "guard does not block distant projectiles");
            caster.CancelCast();
            stats.RestoreMana(stats.MaxMana); Check(caster.TryCast(2), "damage interrupt test starts");
            Check(stats.TakeDamage(1) && !caster.IsCasting && !player.ControlLocked, "taking damage interrupts cast and releases control");
        }
        finally
        {
            caster.CancelCast();
            foreach (var go in spawned) if (go != null) Object.DestroyImmediate(go);
            player.TeleportTo(previousPosition, previousRotation);
            EnemyBase.AIEnabled = ai; AdminPanel.MauVoHan = god; AdminPanel.PhapLucVoHan = manaGod; AdminPanel.KhongHoiChieu = cooldownGod;
            LastReport = Report.ToString(); Directory.CreateDirectory("Library/ReferenceCombat");
            File.WriteAllText("Library/ReferenceCombat/validation.txt", LastReport);
        }
        Debug.Log("[NCK] " + Report.ToString().Split('\n').Count(s => s.StartsWith("PASS")) + " checks passed.");
    }

    [MenuItem("Phàm Kiếm Lục/Chiêu theo video/Chụp bảy chiêu trong Play")]
    public static void CaptureAll()
    {
        if (!Application.isPlaying) throw new Exception("Cần Play trong SanTap.");
        var caster = Object.FindFirstObjectByType<NamCungCombat>(); var player = caster.GetComponent<PlayerController>();
        var animator = caster.GetComponentInChildren<Animator>(); int layer = animator.GetLayerIndex("NCK_Reference");
        Vector3 oldPosition = player.transform.position; Quaternion oldRotation = player.transform.rotation;
        float oldWeight = animator.GetLayerWeight(layer);
        var cameraGo = new GameObject("NCK_CaptureCamera"); var camera = cameraGo.AddComponent<Camera>();
        camera.CopyFrom(Camera.main); camera.enabled = false; camera.fieldOfView = 52;
        cameraGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        Directory.CreateDirectory("Library/ReferenceCombat");
        try
        {
            player.TeleportTo(new Vector3(30, .1f, -45), Quaternion.identity);
            animator.SetLayerWeight(layer, 1);
            foreach (var skill in caster.skills)
            {
                animator.SetFloat("NCKTime", .56f); animator.Play(skill.kind.ToString(), layer, 0); animator.Update(.001f);
                Vector3 origin = player.transform.position + Vector3.up * 1.3f;
                var go = new GameObject("NCK_Preview"); var fx = go.AddComponent<ReferenceSkillFx>();
                GameObject victim = null;
                try
                {
                    bool hasVictim = skill.kind == ReferenceSkillKind.GoldenNet || skill.kind == ReferenceSkillKind.MindPearl;
                    if (hasVictim)
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/YeuThu.prefab");
                        victim = Object.Instantiate(prefab, player.transform.position + Vector3.forward * 8, Quaternion.Euler(0, 180, 0));
                        var pose = victim.GetComponentInChildren<Animator>(); pose.Play("Locomotion"); pose.Update(.01f);
                    }
                    fx.Initialize(caster, skill, origin, origin + Vector3.forward * 8, false, true);
                    fx.SampleForPreview(skill.kind == ReferenceSkillKind.BloodSword ? .15f : 1f);
                    bool cage = skill.kind == ReferenceSkillKind.GoldenNet || skill.kind == ReferenceSkillKind.MindPearl;
                    bool halo = skill.kind == ReferenceSkillKind.Mirrors || skill.kind == ReferenceSkillKind.Reincarnation;
                    Vector3 aim = player.transform.position + new Vector3(0, skill.kind == ReferenceSkillKind.ThunderSword ? 2.7f : 1.6f, cage ? 4.5f : halo ? 0 : 3.5f);
                    camera.transform.position = player.transform.position + (cage ? new Vector3(-8, 4, 13) : halo ? new Vector3(-2.5f, 2.1f, 5.4f) : skill.kind == ReferenceSkillKind.ThunderSword ? new Vector3(-8, 4, 8) : new Vector3(-7, 3.5f, 5));
                    camera.transform.LookAt(aim);
                    // Unity can reuse the same-frame GPU skin buffer across Camera.Render calls.
                    // A CPU-baked snapshot makes each sampled clip visible in its own still.
                    SavePose(camera, animator, "Library/ReferenceCombat/unity_" + skill.kind + ".png");
                }
                finally { Object.DestroyImmediate(go); if (victim != null) Object.DestroyImmediate(victim); }
            }
        }
        finally
        { animator.SetLayerWeight(layer, oldWeight); player.TeleportTo(oldPosition, oldRotation); Object.DestroyImmediate(cameraGo); }
    }

    private static void SavePose(Camera camera, Animator animator, string path)
    {
        var skins = animator.GetComponentsInChildren<SkinnedMeshRenderer>();
        var baked = new System.Collections.Generic.List<GameObject>();
        var meshes = new System.Collections.Generic.List<Mesh>();
        try
        {
            foreach (var skin in skins)
            {
                var mesh = new Mesh(); skin.BakeMesh(mesh); meshes.Add(mesh);
                var go = new GameObject("NCK_PoseSnapshot"); go.transform.SetParent(skin.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                baked.Add(go); skin.enabled = false;
            }
            Save(camera, path);
        }
        finally
        {
            foreach (var skin in skins) if (skin != null) skin.enabled = true;
            foreach (var go in baked) Object.DestroyImmediate(go);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
        }
    }

    private static void Save(Camera camera, string path)
    {
        var rt = RenderTexture.GetTemporary(1000, 650, 24); var previous = RenderTexture.active;
        var image = new Texture2D(1000, 650, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1000, 650), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); }
    }
}
#endif
