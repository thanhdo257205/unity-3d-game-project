#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Công cụ trong Editor: chụp prefab ra ảnh PNG nhỏ nền trong suốt để làm biểu tượng ô đồ.
/// Cách làm: dựng camera tạm nhìn thẳng vào prefab, chụp hai lần (nền đen và nền trắng) rồi tính độ trong suốt từ hai ảnh,
/// nên ra ảnh nền trong suốt chuẩn dù pipeline có làm hỏng kênh alpha.
/// Menu: Phàm Kiếm Lục → Chụp biểu tượng vật phẩm (đang chọn trong Project).
/// </summary>
public static class ItemIconBaker
{
    private const int Size = 256;

    [MenuItem("Phàm Kiếm Lục/Chụp biểu tượng vật phẩm (prefab đang chọn)")]
    private static void BakeSelected()
    {
        foreach (Object o in Selection.objects)
        {
            GameObject prefab = o as GameObject;
            if (prefab == null) continue;
            string dir = "Assets/_Game/UI/Icons";
            Bake(prefab, dir + "/Icon_" + prefab.name + ".png", Quaternion.Euler(20f, 0f, 0f));
        }
    }

    /// <summary>Chụp prefab, lưu PNG tại pngPath (đường dẫn trong Assets), nhập thành Sprite và trả về Sprite đó.</summary>
    public static Sprite Bake(GameObject prefab, string pngPath, Quaternion rotation)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(pngPath));

        GameObject stage = new GameObject("ChupBieuTuong") { hideFlags = HideFlags.HideAndDontSave };
        stage.transform.position = new Vector3(5000f, 5000f, 5000f);
        GameObject instance = Object.Instantiate(prefab, stage.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = rotation;
        foreach (MonoBehaviour mb in instance.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
        foreach (TrailRenderer tr in instance.GetComponentsInChildren<TrailRenderer>()) tr.enabled = false;

        // Khung hình vừa khít vật thể
        Bounds b = new Bounds(instance.transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
        {
            if (r is TrailRenderer) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        float half = Mathf.Max(b.extents.x, b.extents.y) * 1.12f;

        GameObject camGo = new GameObject("CamChup") { hideFlags = HideFlags.HideAndDontSave };
        camGo.transform.SetParent(stage.transform, false);
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = half;
        cam.transform.position = b.center - Vector3.forward * 20f;
        cam.transform.rotation = Quaternion.identity;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 60f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;
        data.renderShadows = false;

        GameObject lightGo = new GameObject("DenChup") { hideFlags = HideFlags.HideAndDontSave };
        lightGo.transform.SetParent(stage.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.None;

        RenderTexture rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        Texture2D onBlack = Capture(cam, rt, Color.black);
        Texture2D onWhite = Capture(cam, rt, Color.white);

        Texture2D result = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        Color[] pb = onBlack.GetPixels();
        Color[] pw = onWhite.GetPixels();
        Color[] outp = new Color[pb.Length];
        for (int i = 0; i < pb.Length; i++)
        {
            // nền trắng - nền đen = (1 - alpha) ở mỗi kênh
            float a = 1f - ((pw[i].r - pb[i].r) + (pw[i].g - pb[i].g) + (pw[i].b - pb[i].b)) / 3f;
            a = Mathf.Clamp01(a);
            Color c = a > 0.001f ? new Color(pb[i].r / a, pb[i].g / a, pb[i].b / a, a) : new Color(0, 0, 0, 0);
            c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b);
            outp[i] = c;
        }
        result.SetPixels(outp);
        result.Apply();
        File.WriteAllBytes(pngPath, result.EncodeToPNG());

        cam.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(onBlack);
        Object.DestroyImmediate(onWhite);
        Object.DestroyImmediate(result);
        Object.DestroyImmediate(stage);

        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(pngPath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
    }

    private static Texture2D Capture(Camera cam, RenderTexture rt, Color background)
    {
        cam.backgroundColor = background;
        cam.Render();
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D t = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        t.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        t.Apply();
        RenderTexture.active = prev;
        return t;
    }
}
#endif
