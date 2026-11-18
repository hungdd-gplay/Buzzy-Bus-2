using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Buzzy.Game.EditorTools
{
    /// <summary>
    /// Ghi số bố cục vào BuzzyLayoutConfig.asset từ file JSON (không sửa tay YAML asset).
    /// -executeMethod Buzzy.Game.EditorTools.BuzzyConfigTool.ApplyJson ; môi trường: BUZZY_CFG_JSON = đường dẫn file.
    /// JSON phẳng: { "cameraEuler": [67,0,0], "slotX": [-2.5,0.2,2.9], "busScale": 0.95, "cameraOrthographic": true }.
    /// </summary>
    public static class BuzzyConfigTool
    {
        public static void ApplyJson()
        {
            string path = Environment.GetEnvironmentVariable("BUZZY_CFG_JSON");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new Exception("Thiếu BUZZY_CFG_JSON: " + path);
            var cfg = BuzzyStaticBuilder.LoadOrCreateConfig();
            var so = new SerializedObject(cfg);
            int n = 0;
            foreach (var kv in ParseFlat(File.ReadAllText(path)))
            {
                var p = so.FindProperty(kv.Key);
                if (p == null) { Debug.LogWarning("[BuzzyConfigTool] Không có trường " + kv.Key); continue; }
                Set(p, kv.Value); n++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cfg); AssetDatabase.SaveAssets();
            Debug.Log("[BuzzyConfigTool] Đã ghi " + n + " trường vào BuzzyLayoutConfig.asset");
        }

        /// <summary>Ghi JSON rồi dựng + chụp khung tĩnh ngay (BUZZY_LEVEL, BUZZY_OUT) trong 1 lần mở Unity.</summary>
        public static void ApplyJsonAndCapture() { ApplyJson(); BuzzyStaticBuilder.BuildAndCapture(); }

        static void Set(SerializedProperty p, List<string> v)
        {
            float F(int i) { return float.Parse(v[Mathf.Min(i, v.Count - 1)], CultureInfo.InvariantCulture); }
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: p.floatValue = F(0); break;
                case SerializedPropertyType.Integer: p.intValue = (int)F(0); break;
                case SerializedPropertyType.Boolean: p.boolValue = v[0] == "true" || v[0] == "1"; break;
                case SerializedPropertyType.Vector2: p.vector2Value = new Vector2(F(0), F(1)); break;
                case SerializedPropertyType.Vector3: p.vector3Value = new Vector3(F(0), F(1), F(2)); break;
                case SerializedPropertyType.Vector2Int: p.vector2IntValue = new Vector2Int((int)F(0), (int)F(1)); break;
                case SerializedPropertyType.Color: p.colorValue = new Color(F(0), F(1), F(2), v.Count > 3 ? F(3) : 1f); break;
                case SerializedPropertyType.Generic:
                    if (!p.isArray) throw new Exception("Không hỗ trợ trường " + p.name);
                    p.arraySize = v.Count;
                    for (int i = 0; i < v.Count; i++)
                    {
                        var e = p.GetArrayElementAtIndex(i);
                        if (e.propertyType == SerializedPropertyType.Integer) e.intValue = (int)F(i); else e.floatValue = F(i);
                    }
                    break;
                default: throw new Exception("Không hỗ trợ kiểu " + p.propertyType + " của " + p.name);
            }
        }

        // JSON phẳng: khoá → số | bool | mảng số. Tự đọc để không phụ thuộc thư viện.
        static Dictionary<string, List<string>> ParseFlat(string json)
        {
            var r = new Dictionary<string, List<string>>();
            int i = 0;
            string Tok()
            {
                while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ',' || json[i] == ':')) i++;
                if (i >= json.Length) return null;
                char c = json[i];
                if (c == '"') { int j = json.IndexOf('"', i + 1); string s = json.Substring(i + 1, j - i - 1); i = j + 1; return s; }
                if (c == '{' || c == '}' || c == '[' || c == ']') { i++; return c.ToString(); }
                int k = i; while (k < json.Length && !char.IsWhiteSpace(json[k]) && ",:[]{}".IndexOf(json[k]) < 0) k++;
                string t = json.Substring(i, k - i); i = k; return t;
            }
            string tok = Tok();
            if (tok != "{") throw new Exception("JSON phải bắt đầu bằng {");
            while ((tok = Tok()) != null && tok != "}")
            {
                string key = tok; var vals = new List<string>();
                string v = Tok();
                if (v == "[") { while ((v = Tok()) != "]") vals.Add(v); } else vals.Add(v);
                r[key] = vals;
            }
            return r;
        }
    }
}
