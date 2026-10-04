#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tạo vật phẩm "Phong Lôi Sí" (loại Pháp bảo) và biểu tượng của nó (menu "Phàm Kiếm Lục/Dựng vật phẩm Phong Lôi Sí").
/// Biểu tượng vẽ bằng code: đôi cánh nhỏ xanh lục (các chiếc lông tỏa ra) có tia sét vàng ở giữa, nền trong suốt.
/// Không ghi đè file đã có.
/// </summary>
public static class PhongLoiSiItemBuilder
{
    private const string IconPath = "Assets/_Game/UI/Icons/Icon_PhongLoiSi.png";
    private const string AssetPath = "Assets/_Game/Data/Items/PhongLoiSi.asset";
    private const int Size = 256;

    [MenuItem("Phàm Kiếm Lục/Dựng vật phẩm Phong Lôi Sí (biểu tượng + ItemData)")]
    public static void Build()
    {
        if (!File.Exists(IconPath)) DrawIcon();
        AssetDatabase.ImportAsset(IconPath);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();

        if (AssetDatabase.LoadAssetAtPath<ItemData>(AssetPath) == null)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.displayName = "Phong Lôi Sí";
            item.type = ItemType.PhapBao;
            item.maxStack = 1;
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            AssetDatabase.CreateAsset(item, AssetPath);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Phong Lôi Sí] Đã dựng biểu tượng và ItemData.");
    }

    // ----- Vẽ biểu tượng -----
    private struct Capsule
    {
        public Vector2 a, b;
        public float r0, r1;
        public Color c0, c1;
    }

    private static void DrawIcon()
    {
        List<Capsule> layers = new List<Capsule>();
        Color dark = new Color(0.02f, 0.16f, 0.09f, 1f);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 baseP = new Vector2(128f + side * 12f, 72f);
            // Từ lông thấp tới lông cao: lông cao dài hơn, vẽ sau cùng để nằm trên
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float ang = Mathf.Lerp(8f, 78f, t) * Mathf.Deg2Rad;
                float len = Mathf.Lerp(86f, 118f, Mathf.Sin(t * Mathf.PI * 0.5f));
                Vector2 dir = new Vector2(side * Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 tip = baseP + dir * len;
                // viền tối (to hơn) rồi thân lông xanh lục
                layers.Add(new Capsule { a = baseP, b = tip, r0 = 13f, r1 = 5f, c0 = dark, c1 = dark });
                layers.Add(new Capsule
                {
                    a = baseP, b = tip, r0 = 10.5f, r1 = 2.8f,
                    c0 = new Color(0.10f, 0.52f, 0.28f, 1f), c1 = new Color(0.45f, 0.98f, 0.62f, 1f)
                });
            }
        }
        // Tia sét vàng ở giữa (đường gãy khúc) kèm quầng
        Vector2[] bolt = { new Vector2(138, 232), new Vector2(116, 168), new Vector2(140, 156), new Vector2(110, 92), new Vector2(134, 82), new Vector2(120, 22) };
        List<Capsule> glow = new List<Capsule>(), core = new List<Capsule>();
        for (int i = 0; i < bolt.Length - 1; i++)
        {
            glow.Add(new Capsule { a = bolt[i], b = bolt[i + 1], r0 = 11f, r1 = 11f, c0 = new Color(1f, 0.8f, 0.2f, 0.35f), c1 = new Color(1f, 0.8f, 0.2f, 0.35f) });
            core.Add(new Capsule { a = bolt[i], b = bolt[i + 1], r0 = 5f, r1 = 5f, c0 = new Color(1f, 0.95f, 0.55f, 1f), c1 = new Color(1f, 0.95f, 0.55f, 1f) });
        }
        layers.AddRange(glow);
        layers.AddRange(core);

        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        const int ss = 3;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Color acc = new Color(0, 0, 0, 0);
                for (int sy = 0; sy < ss; sy++)
                {
                    for (int sx = 0; sx < ss; sx++)
                    {
                        Vector2 p = new Vector2(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss);
                        Color px = new Color(0, 0, 0, 0);
                        for (int k = 0; k < layers.Count; k++)
                        {
                            Capsule c = layers[k];
                            Vector2 ab = c.b - c.a;
                            float t = Mathf.Clamp01(Vector2.Dot(p - c.a, ab) / ab.sqrMagnitude);
                            float d = (p - (c.a + ab * t)).magnitude;
                            if (d <= Mathf.Lerp(c.r0, c.r1, t))
                            {
                                Color col = Color.Lerp(c.c0, c.c1, t);
                                // chồng lớp theo alpha
                                float a = col.a;
                                px = new Color(col.r * a + px.r * (1 - a), col.g * a + px.g * (1 - a), col.b * a + px.b * (1 - a), a + px.a * (1 - a));
                            }
                        }
                        acc += px;
                    }
                }
                acc /= ss * ss;
                if (acc.a > 0.001f) acc = new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a);   // trả về alpha thẳng
                tex.SetPixel(x, y, acc);
            }
        }
        tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
        File.WriteAllBytes(IconPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
#endif
