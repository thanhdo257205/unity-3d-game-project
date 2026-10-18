#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Dựng prefab và đặt yêu thú đã giảm tam giác vào sân tập; không dựng lại phần sân hiện có.</summary>
public static class YeuThuBuilder
{
    private const string Root = "Assets/_Game/Models/Enemies/YeuThu/";
    public const string PrefabPath = "Assets/_Game/Prefabs/YeuThu.prefab";

    private static AnimationClip Clip(string name)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(Root + "Animations/" + name + ".fbx")
            .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
        if (clip == null || !clip.humanMotion) throw new InvalidOperationException("Thiếu clip Humanoid: " + name);
        return clip;
    }

    [MenuItem("Phàm Kiếm Lục/Yêu thú/Dựng prefab và thêm vào sân tập")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Thoát Play trước khi dựng yêu thú.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "YeuThu.fbx");
        if (model == null || model.GetComponent<Animator>() == null || !model.GetComponent<Animator>().avatar.isHuman)
            throw new InvalidOperationException("Model yêu thú chưa có Avatar Humanoid hợp lệ.");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        string matPath = Root + "YeuThu.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, matPath); }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/bear_diffuse.png"));
        material.SetColor("_BaseColor", Color.white);
        string normalPath = Root + "Textures/bear_normal.png";
        var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        if (normalImporter.textureType != TextureImporterType.NormalMap)
        { normalImporter.textureType = TextureImporterType.NormalMap; normalImporter.SaveAndReimport(); }
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        material.EnableKeyword("_NORMALMAP");
        material.SetFloat("_Smoothness", 0.22f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);

        string controllerPath = Root + "YeuThu.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(controllerPath).OfType<BlendTree>().ToArray()) Object.DestroyImmediate(tree, true);
        if (!controller.parameters.Any(p => p.name == "Speed")) controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var locomotion = machine.AddState("Locomotion", new Vector3(250, 80));
        var blend = new BlendTree { name = "Đứng - Đi - Chạy", blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(blend, controller);
        blend.AddChild(Clip("mutant_breathing_idle"), 0f);
        blend.AddChild(Clip("mutant_walking"), 1.7f);
        blend.AddChild(Clip("mutant_run"), 4.5f);
        locomotion.motion = blend;
        machine.defaultState = locomotion;
        string[] stateNames = { "Swipe", "Punch", "JumpAttack", "Slam", "Roar", "Die" };
        string[] clipNames = { "mutant_swiping", "mutant_punch", "mutant_jump_attack", "jump_attack", "mutant_roaring", "mutant_dying" };
        for (int i = 0; i < stateNames.Length; i++)
        {
            var state = machine.AddState(stateNames[i], new Vector3(520, 60 + 65 * i));
            state.motion = Clip(clipNames[i]);
            if (stateNames[i] == "Roar") state.speed = 1.5f;
        }
        EditorUtility.SetDirty(controller);

        var root = new GameObject("YeuThu_Warrok");
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.transform.SetParent(root.transform, false);
            visual.name = "YeuThu_Model";
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
            var animator = visual.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var bounds = visual.GetComponentInChildren<Renderer>().bounds;
            visual.transform.localPosition = Vector3.up * -bounds.min.y;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 2.35f; capsule.radius = 0.58f; capsule.center = new Vector3(0, 1.175f, 0);
            var damageable = root.AddComponent<Damageable>();
            var damageProps = new SerializedObject(damageable);
            damageProps.FindProperty("maxHealth").floatValue = 180f;
            damageProps.ApplyModifiedPropertiesWithoutUndo();
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.6f; agent.height = 2.35f; agent.stoppingDistance = 2.1f; agent.speed = 4.5f;
            var beast = root.AddComponent<YeuThu>();
            var props = new SerializedObject(beast);
            props.FindProperty("animator").objectReferenceValue = animator;
            props.FindProperty("displayName").stringValue = "Yêu thú Warrok";
            props.FindProperty("realm").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RealmData>("Assets/_Game/Data/Realms/04_TrucCo_So.asset");
            props.FindProperty("lootPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/TuiTruVat.prefab").GetComponent<LootPouch>();
            props.FindProperty("barHeight").floatValue = 2.7f;
            props.FindProperty("eyeHeight").floatValue = 1.9f;
            props.FindProperty("detectRange").floatValue = 12f;
            props.FindProperty("detectPause").floatValue = Clip("mutant_roaring").length / 1.5f;
            props.FindProperty("patrolSpeed").floatValue = 1.7f;
            props.FindProperty("patrolRadius").floatValue = 4f;
            var durations = props.FindProperty("attackDurations");
            durations.arraySize = 4;
            for (int i = 0; i < 4; i++) durations.GetArrayElementAtIndex(i).floatValue = Clip(clipNames[i]).length;
            props.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name == "SanTap")
        {
            if (Object.FindFirstObjectByType<YeuThu>() == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = new Vector3(-8, 0.1f, 12);
                instance.transform.rotation = Quaternion.Euler(0, 150, 0);
            }
            foreach (var panel in Object.FindObjectsByType<AdminPanel>(FindObjectsSortMode.None))
            {
                var props = new SerializedObject(panel);
                props.FindProperty("yeuThuPrefab").objectReferenceValue = prefab;
                props.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[YeuThu] Đã dựng Warrok 6.059 tam giác, 4 đòn đánh, Animator và prefab.");
    }
}
#endif
