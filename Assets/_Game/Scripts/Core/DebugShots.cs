#if UNITY_EDITOR
using System.IO;
using UnityEngine;

/// <summary>
/// Công cụ kiểm thử trong Editor (không vào game): chụp ảnh cận cảnh một nhân vật bằng camera tạm, không có HUD.
/// Dùng qua MCP lúc đang Play: DebugShots.Shot("ten", nhanVat, new Vector3(phải, cao, trước), độCaoNhìn).
/// </summary>
public static class DebugShots
{
    /// <summary>Chụp từ vị trí cách target (theo trục phải/cao/trước của target) nhìn vào điểm cao lookHeight. Lưu ở Assets/Screenshots/name.png.</summary>
    public static string Shot(string name, Transform target, Vector3 offset, float lookHeight, int size = 900, float fov = 35f)
    {
        GameObject go = new GameObject("TmpCam");
        Camera cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov;
        go.transform.position = target.position + target.right * offset.x + Vector3.up * offset.y + target.forward * offset.z;
        go.transform.LookAt(target.position + Vector3.up * lookHeight);
        RenderTexture rt = new RenderTexture(size, size, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        string path = "Assets/Screenshots/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = null;
        Object.Destroy(go);
        Object.Destroy(rt);
        Object.Destroy(tex);
        return path;
    }

    /// <summary>Ghép nhiều ảnh PNG (cùng cỡ) thành một tấm lưới cols cột, thu nhỏ còn cell điểm ảnh mỗi ô. Dùng để xem nhiều khung hình một lúc.</summary>
    public static string Combine(string[] paths, int cols, string outName, int cell = 450)
    {
        int rows = (paths.Length + cols - 1) / cols;
        Texture2D sheet = new Texture2D(cols * cell, rows * cell, TextureFormat.RGB24, false);
        for (int i = 0; i < paths.Length; i++)
        {
            Texture2D t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(paths[i]));
            RenderTexture rt = RenderTexture.GetTemporary(cell, cell);
            Graphics.Blit(t, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D small = new Texture2D(cell, cell, TextureFormat.RGB24, false);
            small.ReadPixels(new Rect(0, 0, cell, cell), 0, 0);
            small.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            int cx = (i % cols) * cell;
            int cy = (rows - 1 - i / cols) * cell;
            sheet.SetPixels(cx, cy, cell, cell, small.GetPixels());
            Object.Destroy(t);
            Object.Destroy(small);
        }
        sheet.Apply();
        string path = "Assets/Screenshots/" + outName + ".png";
        File.WriteAllBytes(path, sheet.EncodeToPNG());
        Object.Destroy(sheet);
        return path;
    }
}
#endif
