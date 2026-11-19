using System;
using UnityEditor;
using UnityEngine;

namespace Buzzy.Game.EditorTools
{
    /// <summary>
    /// M8 (U18): font LilitaOne chỉ có glyph Latin → đặt danh sách font hệ thống dự phòng (fontNames) qua TrueTypeFontImporter để CJK / Thai / Arabic
    /// lấy glyph từ font máy (Windows: Microsoft YaHei, Malgun Gothic, Meiryo, Leelawadee UI, Segoe UI; Android: font hệ thống). Không sửa .meta tay.
    /// Menu: Tools/Buzzy Bus/Font Fallback (M8). Lần 1 (19/11) đặt rồi SaveAndReimport nhưng .meta không đổi → giờ: Dynamic trước, SetDirty +
    /// WriteImportSettingsIfDirty + ImportAsset(ForceUpdate), rồi ĐỌC LẠI importer và in giá trị thật (lỗi nếu chưa lưu).
    /// Còn tồn: Android không có font CJK hệ thống chung → nhúng font CJK khi phát hành.
    /// </summary>
    public static class BuzzyFontTool
    {
        const string FontPath = "Assets/02_Gameplay/Res/Buzzy/Fonts/LilitaOne-Regular.ttf";
        static readonly string[] Fallback = { "Lilita One", "Arial", "Segoe UI", "Microsoft YaHei", "Malgun Gothic", "Meiryo", "Leelawadee UI", "Tahoma", "Noto Sans", "Noto Sans CJK SC", "Roboto" };

        [MenuItem("Tools/Buzzy Bus/Font Fallback (M8)")]
        public static void Apply()
        {
            var imp = AssetImporter.GetAtPath(FontPath) as TrueTypeFontImporter;
            if (imp == null) { Debug.LogError("[BuzzyFontTool] không thấy importer " + FontPath); return; }
            imp.fontTextureCase = FontTextureCase.Dynamic;   // fontNames chỉ có nghĩa với font động; đặt trước
            imp.includeFontData = true;
            imp.fontNames = Fallback;
            EditorUtility.SetDirty(imp);
            AssetDatabase.WriteImportSettingsIfDirty(FontPath);
            imp.SaveAndReimport();
            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            // đọc lại để chắc đã ghi vào .meta
            var check = AssetImporter.GetAtPath(FontPath) as TrueTypeFontImporter;
            string got = check == null || check.fontNames == null ? "(null)" : string.Join(", ", check.fontNames);
            bool ok = check != null && check.fontNames != null && check.fontNames.Length == Fallback.Length;
            if (ok) Debug.Log("[BuzzyFontTool] OK fontNames (đọc lại) = " + got);
            else Debug.LogError("[BuzzyFontTool] CHƯA LƯU: fontNames đọc lại = " + got + " (mong " + Fallback.Length + " tên); kiểm .meta " + FontPath);
        }
    }
}
