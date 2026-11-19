using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Buzzy.Game.EditorTools
{
    /// <summary>
    /// M7 (góp ý 08): dựng thử một mesh ở nhiều góc xoay / vật liệu rồi chụp bằng camera game để chọn hướng đúng (rào chắn...).
    /// -executeMethod Buzzy.Game.EditorTools.BuzzyProbe.Barrier ; BUZZY_OUT = thư mục ảnh.
    /// </summary>
    public static class BuzzyProbe
    {
        const string Res = "Assets/02_Gameplay/Res/";

        public static void Barrier()
        {
            string outDir = Environment.GetEnvironmentVariable("BUZZY_OUT"); if (string.IsNullOrEmpty(outDir)) throw new Exception("Thiếu BUZZY_OUT");
            Directory.CreateDirectory(outDir);
            var cfg = BuzzyStaticBuilder.LoadOrCreateConfig(); var assets = BuzzyMotionCapture.LoadOrCreateAssets();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var camGo = new GameObject("Camera"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = cfg.backgroundTop; cfg.ApplyCamera(cam, 648f / 1440f);
            var light = new GameObject("Light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            // nền đường để thấy bóng / tỉ lệ
            var road = GameObject.CreatePrimitive(PrimitiveType.Quad); road.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            road.transform.position = new Vector3(0f, 0f, (cfg.roadNearZ + cfg.roadFarZ) * 0.5f); road.transform.localScale = new Vector3(40f, cfg.roadFarZ - cfg.roadNearZ, 1f);
            var rm = new Material(Shader.Find("Custom/BuzzyEnvironment")); rm.color = cfg.roadColor; road.GetComponent<MeshRenderer>().sharedMaterial = rm;
            // vật liệu: sọc thủ công vs texture Color.png (UV gốc)
            var tex = new Texture2D(64, 64, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) tex.SetPixel(x, y, ((x + y) / 8) % 2 == 0 ? new Color32(0xE8, 0x2B, 0x2B, 255) : new Color32(0xF4, 0xF4, 0xF8, 255));
            tex.Apply();
            var m1 = new Material(Shader.Find("Custom/BuzzyEnvironment")); m1.color = Color.white; m1.mainTexture = tex;
            var colorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Res + "Buzzy/Textures/Color.png");
            var m2 = new Material(Shader.Find("Custom/BuzzyEnvironment")); m2.color = Color.white; m2.mainTexture = colorTex;
            Vector3[] eulers = { new Vector3(0, 22, 0), new Vector3(-90, 22, 0), new Vector3(90, 22, 0), new Vector3(0, 112, 0), new Vector3(0, 202, 0), new Vector3(0, 292, 0) };
            // hàng 1 (z = slotZ): sọc thủ công; hàng 2 (z = slotZ + 3): Color.png
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < eulers.Length; i++)
                {
                    var go = new GameObject("B" + row + "_" + i); go.AddComponent<MeshFilter>().sharedMesh = assets.barrierMesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = row == 0 ? m1 : m2;
                    go.transform.position = new Vector3(-5f + i * 2.2f, 0f, cfg.slotZ + row * 3.2f); go.transform.rotation = Quaternion.Euler(eulers[i]);
                    go.transform.localScale = Vector3.one * cfg.barrierScale;
                    // trục tham chiếu: khối nhỏ ở gốc
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere); dot.transform.position = go.transform.position + Vector3.up * 0.05f; dot.transform.localScale = Vector3.one * 0.12f;
                }
            // xe tham chiếu cạnh đó
            var bus = new GameObject("Bus"); bus.AddComponent<MeshFilter>().sharedMesh = assets.busMesh; var bm = new Material(Shader.Find("Custom/BuzzyVehicle")); bm.color = Color.red; bm.SetTexture("_BaseTex", assets.busTexture); bus.AddComponent<MeshRenderer>().sharedMaterial = bm;
            bus.transform.position = new Vector3(8.5f, 0f, cfg.slotZ); bus.transform.rotation = Quaternion.Euler(0f, cfg.slotYaw, 0f); bus.transform.localScale = Vector3.one * cfg.busScale;
            Shot(cam, outDir, "barrier_game_cam", 648, 1440);
            // camera nhìn ngang từ trước (trục -z) để thấy hình dạng thật
            cam.orthographic = true; cam.orthographicSize = 3f; cam.transform.position = new Vector3(0f, 1.2f, cfg.slotZ - 12f); cam.transform.rotation = Quaternion.Euler(5f, 0f, 0f);
            Shot(cam, outDir, "barrier_front", 1600, 500);
            Debug.Log("[BuzzyProbe] xong -> " + outDir);
        }

        /// <summary>Góp ý 14: dựng vài mesh (BUZZY_MESHES="Sign_Full,BarrierMechanic,Barrier") cạnh xe để xem hình dạng thật, chụp từ camera game + nhìn ngang; mỗi mesh 2 góc (0° / dựng đứng -90°).</summary>
        public static void Meshes()
        {
            string outDir = Environment.GetEnvironmentVariable("BUZZY_OUT"); if (string.IsNullOrEmpty(outDir)) throw new Exception("Thiếu BUZZY_OUT");
            Directory.CreateDirectory(outDir);
            var cfg = BuzzyStaticBuilder.LoadOrCreateConfig(); var assets = BuzzyMotionCapture.LoadOrCreateAssets();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var camGo = new GameObject("Camera"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = cfg.backgroundTop; cfg.ApplyCamera(cam, 648f / 1440f);
            var light = new GameObject("Light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var road = GameObject.CreatePrimitive(PrimitiveType.Quad); road.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            road.transform.position = new Vector3(0f, 0f, (cfg.roadNearZ + cfg.roadFarZ) * 0.5f); road.transform.localScale = new Vector3(40f, cfg.roadFarZ - cfg.roadNearZ, 1f);
            var rm = new Material(Shader.Find("Custom/BuzzyEnvironment")); rm.color = cfg.roadColor; road.GetComponent<MeshRenderer>().sharedMaterial = rm;
            var tex = new Texture2D(64, 64, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) tex.SetPixel(x, y, ((x / 8) + (y / 8)) % 2 == 0 ? new Color32(0xE8, 0x2B, 0x2B, 255) : new Color32(0xF4, 0xF4, 0xF8, 255));
            tex.Apply();
            var m1 = new Material(Shader.Find("Custom/BuzzyEnvironment")); m1.color = Color.white; m1.mainTexture = tex;
            var names = (Environment.GetEnvironmentVariable("BUZZY_MESHES") ?? "Sign_Full,BarrierMechanic,Barrier").Split(',');
            int i = 0;
            foreach (var n in names)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Res + "Buzzy/Meshes/" + n.Trim() + ".obj");
                if (mesh == null) { foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Res + "Buzzy/Meshes/" + n.Trim() + ".obj")) if (o is Mesh) { mesh = (Mesh)o; break; } }
                if (mesh == null) { Debug.Log("[BuzzyProbe] không có mesh " + n); continue; }
                var b = mesh.bounds; Debug.Log("[BuzzyProbe] " + n + " bounds tâm " + b.center.ToString("F2") + " cỡ " + b.size.ToString("F2") + " đỉnh " + mesh.vertexCount + " sub " + mesh.subMeshCount);
                // BUZZY_ROTS="x,y,z;x,y,z;..." (độ) hoặc "cam" / "-cam" (LookRotation theo hướng camera) → mỗi mesh một hàng theo z cho từng góc; mặc định 2 góc 0 / -90
                var rotsEnv = Environment.GetEnvironmentVariable("BUZZY_ROTS"); var rots = new List<Quaternion>();
                if (string.IsNullOrEmpty(rotsEnv)) { rots.Add(Quaternion.Euler(0f, cfg.slotYaw, 0f)); rots.Add(Quaternion.Euler(-90f, cfg.slotYaw, 0f)); }
                else foreach (var part in rotsEnv.Split(';'))
                {
                    var t = part.Trim(); if (t.Length == 0) continue;
                    if (t == "cam") rots.Add(Quaternion.LookRotation(cam.transform.forward, Vector3.up));
                    else if (t == "-cam") rots.Add(Quaternion.LookRotation(-cam.transform.forward, Vector3.up));
                    else { var e = t.Split(','); rots.Add(Quaternion.Euler(float.Parse(e[0]), float.Parse(e[1]), float.Parse(e[2]))); }
                }
                for (int r = 0; r < rots.Count; r++)
                {
                    var go = new GameObject(n + "_" + r); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = m1;
                    go.transform.position = new Vector3(-5f + i * 2.4f, 0f, cfg.slotZ + r * 2.6f); go.transform.rotation = rots[r];
                    float sc = 1.6f / Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z))); go.transform.localScale = Vector3.one * sc;   // chuẩn hoá cạnh lớn nhất = 1,6
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere); dot.transform.position = go.transform.position + Vector3.up * 0.05f; dot.transform.localScale = Vector3.one * 0.12f;
                }
                i++;
            }
            var bus = new GameObject("Bus"); bus.AddComponent<MeshFilter>().sharedMesh = assets.busMesh; var bm = new Material(Shader.Find("Custom/BuzzyVehicle")); bm.color = Color.red; bm.SetTexture("_BaseTex", assets.busTexture); bus.AddComponent<MeshRenderer>().sharedMaterial = bm;
            bus.transform.position = new Vector3(-5f + i * 2.4f + 1f, 0f, cfg.slotZ); bus.transform.rotation = Quaternion.Euler(0f, cfg.slotYaw, 0f); bus.transform.localScale = Vector3.one * cfg.busScale;
            Shot(cam, outDir, "meshes_game_cam", 648, 1440);
            cam.orthographic = true; cam.orthographicSize = 3f; cam.transform.position = new Vector3(0f, 1.2f, cfg.slotZ - 12f); cam.transform.rotation = Quaternion.Euler(5f, 0f, 0f);
            Shot(cam, outDir, "meshes_front", 1600, 500);
            Debug.Log("[BuzzyProbe] xong -> " + outDir);
        }

        static void Shot(Camera cam, string dir, string name, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.aspect = (float)w / h;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
        }
    }
}
