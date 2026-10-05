#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dựng bảng Admin: "Phàm Kiếm Lục/Bảng Admin/Dựng prefab và đặt vào SanTap" tạo Assets/_Game/UI/AdminPanel.prefab (script AdminPanel + tham chiếu phông, prefab Thanh Lang, Tà tu)
/// rồi đặt một bản vào scene SanTap (thay bản cũ nếu có).
/// </summary>
public static class AdminPanelBuilder
{
    private const string PrefabPath = "Assets/_Game/UI/AdminPanel.prefab";
    private const string ScenePath = "Assets/_Game/Scenes/SanTap.unity";

    [MenuItem("Phàm Kiếm Lục/Bảng Admin/Dựng prefab và đặt vào SanTap")]
    public static void Build()
    {
        GameObject root = new GameObject("AdminPanel");
        AdminPanel ap = root.AddComponent<AdminPanel>();
        SerializedObject so = new SerializedObject(ap);
        so.FindProperty("fontAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/UI/Fonts/Inter_Regular_SDF.asset");
        so.FindProperty("thanhLangPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/ThanhLang.prefab");
        so.FindProperty("taTuPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/TaTu.prefab");
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject old = GameObject.Find("AdminPanel");
        if (old != null) Object.DestroyImmediate(old);
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
        inst.name = "AdminPanel";
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Admin] Đã dựng " + PrefabPath + " và đặt vào SanTap.");
    }
}
#endif
