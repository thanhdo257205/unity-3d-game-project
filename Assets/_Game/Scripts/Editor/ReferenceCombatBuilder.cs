#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public static class ReferenceCombatBuilder
{
    public const string Models = "Assets/_Game/Models/ReferenceCombat/";
    public const string Data = "Assets/_Game/Data/ReferenceCombat/";
    public const string Controller = "Assets/_Game/Models/Characters/NhanVat/NhanVat_Animator.controller";

    [MenuItem("Phàm Kiếm Lục/Chiêu theo video/1. Nhập animation Blender")]
    public static void ConfigureImports()
    {
        if (Application.isPlaying) throw new Exception("Thoát Play trước khi nhập clip.");
        foreach (ReferenceSkillKind kind in Enum.GetValues(typeof(ReferenceSkillKind)))
        {
            string path = Models + "NCK_" + kind + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new Exception("Thiếu FBX Blender: " + path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = "NCK_" + kind;
                clip.loopTime = clip.loopPose = false;
                clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
        Debug.Log("[NCK] Đã nhập bảy animation Blender dạng Humanoid.");
    }

    private static T Store<T>(T value, string path) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) { EditorUtility.CopySerialized(value, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(value); return existing; }
        AssetDatabase.CreateAsset(value, path); return value;
    }

    private static Material Material(string name, string shader, Color tint)
    {
        string path = Models + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(material, path); }
        material.shader = Shader.Find(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        if (material.HasProperty("_Intensity")) material.SetFloat("_Intensity", 1.05f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .65f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .35f);
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", tint * 1.4f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.EnableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(material); return material;
    }

    [MenuItem("Phàm Kiếm Lục/Chiêu theo video/2. Dựng và gắn bảy chiêu cho người chơi")]
    public static void Build()
    {
        if (Application.isPlaying) throw new Exception("Thoát Play trước khi dựng chiêu.");
        Directory.CreateDirectory(Data); AssetDatabase.Refresh();
        var kinds = (ReferenceSkillKind[])Enum.GetValues(typeof(ReferenceSkillKind));
        string[] names = { "Âm Dương Băng Hỏa Kiếm", "Ngưng Quang Bảo Kính", "Luân Hồi Thần Quang", "Huyết Kiếm", "Kim Lôi Kiếm", "Kim Ti Linh Võng", "Khốn Tâm Châu" };
        var keys = new[] { Key.H, Key.J, Key.K, Key.L, Key.Y, Key.B, Key.U };
        float[] costs = { 28, 30, 38, 30, 38, 24, 20 }, cooldowns = { 6, 10, 12, 7, 10, 8, 7 };
        float[] charges = { 1.05f, .9f, 1.15f, .8f, 1.1f, .85f, .7f }, recoveries = { .55f, .45f, .55f, .45f, .5f, .45f, .4f };
        float[] durations = { 1.55f, 3.6f, 3.2f, 2.5f, 1.4f, 3.4f, 2.5f }, ranges = { 22, 22, 14, 32, 30, 20, 22 };
        float[] radii = { .8f, 2.4f, 5, 2.4f, 1.05f, 3.2f, 2.5f }, damage = { 44, 24, 48, 60, 65, 18, 24 }, stuns = { 0, 0, 0, 0, 1.2f, 2.8f, 1.8f };
        string[] times = { "02:10–02:18; 03:20–03:34", "04:29–04:47", "05:06–05:18; 09:42–10:12", "07:38–08:08", "09:54–10:06", "10:45–11:12", "00:46–00:58; 01:18–01:24" };
        Color[] primary = { new Color(1,.28f,.06f,.65f), new Color(.07f,.8f,.6f,.52f), new Color(1,.36f,.13f,.72f), new Color(.85f,.055f,.065f,.7f), new Color(1,.7f,.15f,.7f), new Color(.95f,.6f,.25f,.65f), new Color(.55f,.12f,.92f,.62f) };
        Color[] secondary = { new Color(.1f,.65f,1,.65f), new Color(.4f,1,.8f,.4f), new Color(.5f,.15f,1,.42f), new Color(.5f,.05f,.25f,.45f), new Color(1,.95f,.5f,.5f), new Color(.8f,.4f,1,.45f), new Color(.25f,.5f,1,.45f) };
        var skills = new ReferenceSkillData[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(Models + "NCK_" + kinds[i] + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            if (clip == null || !clip.humanMotion) throw new Exception("Clip Humanoid chưa hợp lệ: " + kinds[i]);
            var skill = ScriptableObject.CreateInstance<ReferenceSkillData>();
            skill.kind = kinds[i]; skill.displayName = names[i]; skill.key = keys[i]; skill.motion = clip;
            skill.manaCost = costs[i]; skill.cooldown = cooldowns[i]; skill.chargeTime = charges[i]; skill.recoveryTime = recoveries[i];
            skill.duration = durations[i]; skill.range = ranges[i]; skill.radius = radii[i]; skill.damage = damage[i]; skill.stunTime = stuns[i];
            skill.primary = primary[i]; skill.secondary = secondary[i]; skill.referenceTime = times[i];
            skills[i] = Store(skill, Data + kinds[i] + ".asset");
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        if (!controller.parameters.Any(p => p.name == "NCKTime")) controller.AddParameter("NCKTime", AnimatorControllerParameterType.Float);
        var layer = controller.layers.FirstOrDefault(l => l.name == "NCK_Reference");
        if (layer == null)
        {
            var machine = new AnimatorStateMachine { name = "NCK_Reference" };
            AssetDatabase.AddObjectToAsset(machine, controller);
            layer = new AnimatorControllerLayer { name = "NCK_Reference", stateMachine = machine, defaultWeight = 0, blendingMode = AnimatorLayerBlendingMode.Override };
            controller.AddLayer(layer);
        }
        var mask = new AvatarMask { name = "NCK_ThanTren" };
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,
            i != (int)AvatarMaskBodyPart.Root && i != (int)AvatarMaskBodyPart.LeftLeg && i != (int)AvatarMaskBodyPart.RightLeg
            && i != (int)AvatarMaskBodyPart.LeftFootIK && i != (int)AvatarMaskBodyPart.RightFootIK);
        var storedMask = Store(mask, Models + "NCK_ThanTren.mask");
        var layers = controller.layers;
        int index = Array.FindIndex(layers, l => l.name == "NCK_Reference");
        layers[index].avatarMask = storedMask; controller.layers = layers;
        foreach (var skill in skills)
        {
            var state = layer.stateMachine.states.Select(c => c.state).FirstOrDefault(s => s.name == skill.kind.ToString());
            if (state == null) state = layer.stateMachine.AddState(skill.kind.ToString());
            state.motion = skill.motion; state.timeParameterActive = true; state.timeParameter = "NCKTime";
            EditorUtility.SetDirty(state);
        }
        EditorUtility.SetDirty(controller);
        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null) throw new Exception("Mở SanTap có nhân vật người chơi trước khi gắn chiêu.");
        var combat = player.GetComponent<NamCungCombat>();
        if (combat == null) combat = player.gameObject.AddComponent<NamCungCombat>();
        combat.skills = skills;
        combat.energyMaterial = Material("NCK_Energy", "PhamKiemLuc/TienHiepEnergy", Color.white);
        combat.surfaceMaterial = Material("NCK_Arcana", "PhamKiemLuc/ReferenceArcana", Color.white);
        combat.jadeMaterial = Material("NCK_JadeMetal", "PhamKiemLuc/ReferenceRelic", new Color(.08f,.30f,.24f));
        combat.bloodMaterial = Material("NCK_BloodMetal", "PhamKiemLuc/ReferenceRelic", new Color(.36f,.035f,.045f));
        combat.goldMaterial = Material("NCK_GoldMetal", "PhamKiemLuc/ReferenceRelic", new Color(.72f,.40f,.09f));
        combat.relicModels = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "NCK_Relics.fbx");
        combat.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/UI/Fonts/Inter_Regular_SDF.asset");
        EditorUtility.SetDirty(combat);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene); EditorSceneManager.SaveScene(player.gameObject.scene);
        Debug.Log("[NCK] Bảy chiêu đã gắn vào người chơi: U/H/J/K/L/Y/B. F đổi camera, E Phá Không giữ nguyên.");
    }
}
#endif
