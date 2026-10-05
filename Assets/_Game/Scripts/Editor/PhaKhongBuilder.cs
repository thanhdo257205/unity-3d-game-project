#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dựng kỹ năng Phá Không (lượt 3B):
/// - "Phàm Kiếm Lục/Phá Không/Đặt khoảng cách theo cảnh giới": ghi RealmData.phaKhongDistance cho 10 cảnh giới (15/25/40, 60/80/100, 150/200/250, 400 mét).
/// - "Phàm Kiếm Lục/Phá Không/Gắn vào NhanVat trong SanTap": tạo vật liệu (sóng xung kích, chớp sáng), mở scene SanTap, thêm PhaKhong vào NhanVat và gán tham chiếu.
/// </summary>
public static class PhaKhongBuilder
{
    private const string SanTapPath = "Assets/_Game/Scenes/SanTap.unity";
    private const string PrefabPath = "Assets/_Game/VFX/CongKhongGian.prefab";
    private const string MatDir = "Assets/_Game/Materials/";

    private static readonly string[] RealmFiles =
    {
        "01_LuyenKhi_1", "02_LuyenKhi_4", "03_LuyenKhi_7", "04_TrucCo_So", "05_TrucCo_Trung", "06_TrucCo_Hau", "07_KetDan_So", "08_KetDan_Trung", "09_KetDan_Hau", "10_NguyenAnh"
    };
    private static readonly float[] Distances = { 15f, 25f, 40f, 60f, 80f, 100f, 150f, 200f, 250f, 400f };

    [MenuItem("Phàm Kiếm Lục/Phá Không/Đặt khoảng cách theo cảnh giới")]
    public static void SetRealmDistances()
    {
        for (int i = 0; i < RealmFiles.Length; i++)
        {
            string path = "Assets/_Game/Data/Realms/" + RealmFiles[i] + ".asset";
            RealmData r = AssetDatabase.LoadAssetAtPath<RealmData>(path);
            if (r == null) { Debug.LogError("[Phá Không] Thiếu " + path); continue; }
            SerializedObject so = new SerializedObject(r);
            so.FindProperty("phaKhongDistance").floatValue = Distances[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(r);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Phá Không] Đã đặt khoảng cách Phá Không cho 10 cảnh giới.");
    }

    [MenuItem("Phàm Kiếm Lục/Phá Không/Gắn vào NhanVat trong SanTap")]
    public static void AttachToPlayer()
    {
        SetRealmDistances();
        Shader shock = Shader.Find("PhamKiemLuc/SongXungKich");
        if (shock == null) { Debug.LogError("[Phá Không] Không thấy shader SongXungKich."); return; }
        string shockPath = MatDir + "VFX_SongXungKich.mat";
        Material mShock = AssetDatabase.LoadAssetAtPath<Material>(shockPath);
        if (mShock == null) { mShock = new Material(shock) { name = "VFX_SongXungKich" }; AssetDatabase.CreateAsset(mShock, shockPath); }
        mShock.shader = shock;
        EditorUtility.SetDirty(mShock);
        Material mFlash = WingLightningBuilder.MakeRoundMat("VFX_ChopSang", true, new Color(3.2f, 2.8f, 2.0f), new Color(1.2f, 2.0f, 3.2f), 0.9f, 2.0f, 3005);
        Material mAim = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "VFX_SetXanhCong.mat");
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.OpenScene(SanTapPath, OpenSceneMode.Single);
        GameObject nv = GameObject.Find("NhanVat");
        if (nv == null) { Debug.LogError("[Phá Không] Không thấy NhanVat trong SanTap."); return; }
        PhaKhong pk = nv.GetComponent<PhaKhong>();
        if (pk == null) pk = nv.AddComponent<PhaKhong>();
        SerializedObject so = new SerializedObject(pk);
        so.FindProperty("wings").objectReferenceValue = nv.GetComponent<PhongLoiSi>();
        so.FindProperty("congPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<CongKhongGian>();
        so.FindProperty("feedback").objectReferenceValue = Object.FindAnyObjectByType<HitFeedback>();
        so.FindProperty("matVongNham").objectReferenceValue = mAim;
        so.FindProperty("matSongXungKich").objectReferenceValue = mShock;
        so.FindProperty("matChopSang").objectReferenceValue = mFlash;
        so.FindProperty("obstacleMask").intValue = 1 << LayerMask.NameToLayer("MoiTruong");
        so.ApplyModifiedPropertiesWithoutUndo();

        HUD hud = Object.FindAnyObjectByType<HUD>();
        if (hud != null)
        {
            SerializedObject hs = new SerializedObject(hud);
            hs.FindProperty("phaKhong").objectReferenceValue = pk;
            hs.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Phá Không] Đã gắn PhaKhong vào NhanVat trong SanTap.");
    }
}
#endif
