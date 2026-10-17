#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Rebuild the reference sword presentation from the Blender FBX without changing melee weapons.</summary>
public static class SwordReferenceBuilder
{
    private const string Materials = "Assets/_Game/Materials/";
    private const string Prefabs = "Assets/_Game/Prefabs/";
    private static Material Body(string name, Color color, Color emission)
    {
        string path = Materials + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", 0.72f);
        mat.SetFloat("_Smoothness", 0.72f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emission);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        MaterialEditor.FixupEmissiveFlag(mat);
        mat.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material Ribbon(string name)
    {
        string path = Materials + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(AssetDatabase.LoadAssetAtPath<Material>(Materials + "PhiKiem_DauKiem_Vet.mat"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = Shader.Find("PhamKiemLuc/TienHiepEnergy");
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Intensity", 2.3f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static GameObject Build(string source, string destination, Mesh mesh, Material[] palette, Material ribbon, Color tint, bool player)
    {
        var root = PrefabUtility.LoadPrefabContents(Prefabs + source + ".prefab");
        try
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                if (filter.gameObject != root) Object.DestroyImmediate(filter.gameObject);
            var model = new GameObject("Model_NguyenAnh");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterials = palette;
            var trail = root.GetComponent<TrailRenderer>();
            if (trail != null) { trail.enabled = false; trail.emitting = false; }
            var spirit = root.GetComponent<SwordSpiritVisual>();
            if (spirit == null) spirit = root.AddComponent<SwordSpiritVisual>();
            spirit.ribbonMaterial = ribbon;
            spirit.tint = tint;
            if (player)
            {
                var serialized = new SerializedObject(root.GetComponent<FlyingSword>());
                serialized.FindProperty("model").objectReferenceValue = model.transform;
                serialized.FindProperty("tipOffset").floatValue = 1.34f;
                serialized.FindProperty("tailOffset").floatValue = -0.255f;
                string fadePath = Materials + "LinhKiem_JadeFade.mat";
                Material fade = AssetDatabase.LoadAssetAtPath<Material>(fadePath);
                if (fade == null)
                {
                    fade = new Material(AssetDatabase.LoadAssetAtPath<Material>(Materials + "ThanhTrucKiem_Mo.mat"));
                    AssetDatabase.CreateAsset(fade, fadePath);
                }
                fade.SetColor("_BaseColor", palette[0].GetColor("_BaseColor"));
                fade.SetColor("_EmissionColor", palette[0].GetColor("_EmissionColor"));
                fade.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                MaterialEditor.FixupEmissiveFlag(fade); fade.EnableKeyword("_EMISSION");
                EditorUtility.SetDirty(fade);
                serialized.FindProperty("fadeMaterial").objectReferenceValue = fade;
                serialized.FindProperty("strikeStretch").floatValue = 1.08f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                var serialized = new SerializedObject(root.GetComponent<EnemyBlade>());
                serialized.FindProperty("bladeTipOffset").floatValue = 1.34f;
                serialized.FindProperty("bladeTailOffset").floatValue = 0.255f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return PrefabUtility.SaveAsPrefabAsset(root, Prefabs + destination + ".prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Phàm Kiếm Lục/Đấu phi kiếm/Cập nhật mẫu kiếm Blender")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) return;
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Models/Swords/NguyenAnh/PhiKiem_NguyenAnh.fbx");
        var source = fbx.GetComponentInChildren<MeshFilter>().sharedMesh;
        string meshPath = "Assets/_Game/Models/Swords/NguyenAnh/PhiKiem_Unity.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null) { mesh = Object.Instantiate(source); AssetDatabase.CreateAsset(mesh, meshPath); }
        else EditorUtility.CopySerialized(source, mesh);
        Vector3[] vertices = mesh.vertices;
        Quaternion rotation = Quaternion.Euler(90, 0, 0);
        for (int i = 0; i < vertices.Length; i++) vertices[i] = rotation * vertices[i];
        mesh.vertices = vertices;
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        var green = new Color(0.03f, 1f, 0.65f, 1);
        var gold = new Color(1f, 0.56f, 0.08f, 1);
        Material ribbon = Ribbon("LinhKiem_Ribbon");
        Material trim = Body("LinhKiem_GoldTrim", new Color(0.75f, 0.46f, 0.1f), gold * 0.65f);
        Material jade = Body("LinhKiem_JadeCore", green * 0.55f, green * 2.6f);
        Material silver = Body("LinhKiem_JadeBlade", new Color(0.5f, 0.85f, 0.72f), green * 0.85f);
        Material goldBlade = Body("LinhKiem_GoldBlade", new Color(0.95f, 0.72f, 0.26f), gold * 1.1f);
        Material goldCore = Body("LinhKiem_GoldCore", Color.white, new Color(1f, 0.85f, 0.4f) * 2.2f);
        GameObject playerPrefab = Build("ThanhTrucKiem", "PhiKiemNgocAnh", mesh, new[] { silver, trim, jade }, ribbon, green, true);
        Build("PhiKiemDauKiem", "PhiKiemDauKiem", mesh, new[] { goldBlade, trim, goldCore }, ribbon, gold, false);
        var enemyRoot = PrefabUtility.LoadPrefabContents(Prefabs + "TaTu.prefab");
        try
        {
            var array = enemyRoot.GetComponent<SwordArrayVisual>();
            if (array == null) array = enemyRoot.AddComponent<SwordArrayVisual>();
            array.material = ribbon; array.tint = gold;
            PrefabUtility.SaveAsPrefabAsset(enemyRoot, Prefabs + "TaTu.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(enemyRoot); }
        foreach (var swarm in Object.FindObjectsByType<SwordSwarm>())
        {
            var serialized = new SerializedObject(swarm);
            serialized.FindProperty("swordPrefab").objectReferenceValue = playerPrefab;
            serialized.ApplyModifiedProperties();
            var array = swarm.GetComponent<SwordArrayVisual>();
            if (array == null) array = Undo.AddComponent<SwordArrayVisual>(swarm.gameObject);
            array.material = ribbon; array.tint = green;
            EditorUtility.SetDirty(array);
        }
        foreach (var volume in Object.FindObjectsByType<UnityEngine.Rendering.Volume>())
        {
            if (volume.name != "Volume_BloomToanCuc" || volume.sharedProfile == null) continue;
            UnityEngine.Rendering.Universal.Bloom bloom;
            if (!volume.sharedProfile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out bloom)) continue;
            bloom.intensity.Override(0.9f);
            bloom.threshold.Override(0.7f);
            bloom.scatter.Override(0.75f);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(volume.sharedProfile);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Reference sword: Blender mesh, jade/gold palettes, layered flight trails and sword arrays installed.");
    }
}
#endif
