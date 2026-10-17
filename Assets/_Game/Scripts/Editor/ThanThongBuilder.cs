#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ThanThongBuilder
{
    [MenuItem("Phàm Kiếm Lục/Thần thông/Dựng Kim Liên và Hộ Thể")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) return;
        const string root = "Assets/_Game/";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(root + "Models/PhapBao/KimLien/KimLien.fbx");
        var mesh = source.GetComponentInChildren<MeshFilter>().sharedMesh;
        string matPath = root + "Materials/KimLien_Gold.mat";
        var gold = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (gold == null) { gold = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(gold, matPath); }
        gold.shader = Shader.Find("PhamKiemLuc/KimLienGold");
        gold.shaderKeywords = new string[0];
        gold.SetColor("_BaseColor", new Color(0.55f, 0.23f, 0.035f));
        gold.SetColor("_GlowColor", new Color(2f, 0.85f, 0.12f));
        EditorUtility.SetDirty(gold);
        string auraPath = root + "Materials/ThanThong_Aura.mat";
        var aura = AssetDatabase.LoadAssetAtPath<Material>(auraPath);
        if (aura == null) { aura = new Material(Shader.Find("PhamKiemLuc/LinhKiemRibbon")); AssetDatabase.CreateAsset(aura, auraPath); }
        aura.shader = Shader.Find("PhamKiemLuc/TienHiepEnergy"); aura.shaderKeywords = new string[0];
        aura.SetColor("_BaseColor", Color.white); aura.SetFloat("_Intensity", 1.8f); aura.SetFloat("_Mode", 0); EditorUtility.SetDirty(aura);
        var swordRibbon = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/LinhKiem_Ribbon.mat");
        if (swordRibbon != null)
        {
            swordRibbon.shader = aura.shader; swordRibbon.shaderKeywords = new string[0];
            swordRibbon.SetFloat("_Intensity", 2.3f); swordRibbon.SetFloat("_Mode", 0); EditorUtility.SetDirty(swordRibbon);
        }
        var relic = new GameObject("KimLienTien");
        GameObject prefab;
        try
        {
            var model = new GameObject("CanhSen_24"); model.transform.SetParent(relic.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterial = gold;
            relic.AddComponent<TienHiepVfx>().material = aura;
            prefab = PrefabUtility.SaveAsPrefabAsset(relic, root + "Prefabs/KimLienTien.prefab");
        }
        finally { Object.DestroyImmediate(relic); }
        foreach (var stats in Object.FindObjectsByType<PlayerStats>())
        {
            var skill = stats.GetComponent<ThanThongCombat>();
            if (skill == null) skill = Undo.AddComponent<ThanThongCombat>(stats.gameObject);
            skill.lotusPrefab = prefab; skill.auraMaterial = aura;
            skill.hudFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(root + "UI/Fonts/Inter_Regular_SDF.asset");
            EditorUtility.SetDirty(skill);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Installed jade ward, counterattack, charged golden lotus and skill HUD.");
    }
}
#endif
