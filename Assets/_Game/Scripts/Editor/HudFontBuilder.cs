#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Công cụ trong Editor: dựng font TextMeshPro (chữ SDF sắc nét) từ phông Inter có đủ dấu tiếng Việt.
/// Inter là phông mã nguồn mở (giấy phép SIL OFL) đi kèm gói App UI của Unity, chép vào Assets/_Game/UI/Fonts.
/// Chạy: menu Phàm Kiếm Lục → Tạo font HUD (TextMeshPro). Kết quả: Assets/_Game/UI/Fonts/Inter_Regular_SDF.asset (đã nạp sẵn mọi chữ tiếng Việt).
/// </summary>
public static class HudFontBuilder
{
    private const string SourceTtf = "Packages/com.unity.dt.app-ui/PackageResources/Fonts/Inter-Regular.ttf";
    private const string Dir = "Assets/_Game/UI/Fonts";
    private const string Ttf = Dir + "/Inter-Regular.ttf";
    private const string AssetPath = Dir + "/Inter_Regular_SDF.asset";

    /// <summary>Toàn bộ chữ cần nạp sẵn: ASCII và các chữ cái tiếng Việt (có dấu) hoa và thường.</summary>
    private const string Vietnamese =
        "ÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬÈÉẺẼẸÊỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÙÚỦŨỤƯỪỨỬỮỰỲÝỶỸỴĐ" +
        "àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ";

    [MenuItem("Phàm Kiếm Lục/Tạo font HUD (TextMeshPro)")]
    public static void BuildMenu() { Build(); }

    public static TMP_FontAsset Build()
    {
        Directory.CreateDirectory(Dir);
        if (!File.Exists(Ttf))
        {
            if (!AssetDatabase.CopyAsset(SourceTtf, Ttf))
                throw new IOException("Không chép được phông Inter từ " + SourceTtf);
        }
        AssetDatabase.ImportAsset(Ttf, ImportAssetOptions.ForceSynchronousImport);
        Font font = AssetDatabase.LoadAssetAtPath<Font>(Ttf);

        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        if (existing != null) AssetDatabase.DeleteAsset(AssetPath);

        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        fa.name = "Inter_Regular_SDF";
        AssetDatabase.CreateAsset(fa, AssetPath);
        fa.material.name = "Inter_Regular_SDF Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        for (int i = 0; i < fa.atlasTextures.Length; i++)
        {
            fa.atlasTextures[i].name = "Inter_Regular_SDF Atlas" + (i == 0 ? "" : " " + i);
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], fa);
        }

        // Nạp sẵn ASCII + tiếng Việt vào atlas, lưu cùng asset (khỏi tạo lúc chạy game)
        string ascii = "";
        for (char c = ' '; c <= '~'; c++) ascii += c;
        string missing;
        fa.TryAddCharacters(ascii + Vietnamese + "·…–—‘’“”°×", out missing);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        Debug.Log("Đã tạo font HUD: " + AssetPath + (string.IsNullOrEmpty(missing) ? "" : " (thiếu ký tự: " + missing + ")"));
        return fa;
    }
}
#endif
