using System;
using UnityEngine;

namespace Buzzy.Game
{
    /// <summary>
    /// M8: chuỗi UI của Buzzy qua hệ Localize của base (Localization.Get / Localization.language, PlayerPrefs "Language").
    /// Khoá bz_* nằm ở 15 file RIÊNG Resources/Localization-Buzzy-&lt;Lang&gt;.txt (cùng định dạng base: dòng đầu "KEY_lang,&lt;Lang&gt;", rồi "key,value"),
    /// nạp gộp (merge) sau file base nên không bị ghi đè khi ai đó bấm Tools/Localize/Reload từ sheet. Arabic lưu chữ gốc, định hình lúc hiển thị (ArabicFixer).
    /// Bản dịch đầu do AI dịch, cần người bản địa soát (design-decisions M8).
    /// </summary>
    public static class BuzzyStrings
    {
        /// <summary>Thứ tự kiểm (leader 44): English, Vietnamese, Spanish, Portuguese, German, French, Italian, Russian, Turkish, Indonesian, Thai, Japanese, Korean, Chinese, Arabic. Tên = SystemLanguage.ToString() = hậu tố file base.</summary>
        public static readonly string[] Languages = { "English", "Vietnamese", "Spanish", "Portuguese", "German", "French", "Italian", "Russian", "Turkish", "Indonesian", "Thai", "Japanese", "Korean", "Chinese", "Arabic" };
        /// <summary>Tên bản địa hiện trên nút chọn ngôn ngữ (không dịch).</summary>
        public static readonly string[] NativeNames = { "English", "Tiếng Việt", "Español", "Português", "Deutsch", "Français", "Italiano", "Русский", "Türkçe", "Bahasa Indonesia", "ไทย", "日本語", "한국어", "中文", "العربية" };

        /// <summary>Phát sau khi đổi ngôn ngữ (view làm mới chữ HUD / popup đang mở).</summary>
        public static event Action Changed;
        static bool _inited;

        public static string Current { get { var l = Localization.language; return string.IsNullOrEmpty(l) ? "English" : l; } }
        public static int CurrentIndex { get { int i = Array.IndexOf(Languages, Current); return i < 0 ? 0 : i; } }
        /// <summary>Arabic: chữ đã định hình + đảo (ArabicFixer) để Text vẽ trái→phải vẫn đọc đúng.</summary>
        public static bool IsRtl { get { return Current == "Arabic"; } }

        /// <summary>Gọi 1 lần lúc khởi động (Bootstrap, sau LocalizeService nếu có): nạp ngôn ngữ đã lưu / ngôn ngữ máy rồi gộp file Buzzy. Gọi lại vô hại.</summary>
        public static void Init()
        {
            if (_inited) return; _inited = true;
            string env = Environment.GetEnvironmentVariable("BUZZY_LANG");   // bộ chụp batchmode chọn ngôn ngữ
            string lang = !string.IsNullOrEmpty(env) ? env : PlayerPrefs.GetString("Language", DefaultLanguage());
            Apply(lang, false);
        }

        static string DefaultLanguage()
        {
            string s = Application.systemLanguage.ToString();
            return Array.IndexOf(Languages, s) >= 0 ? s : "English";
        }

        /// <summary>API ổn định cho bộ chụp / flow: đổi ngôn ngữ lúc chạy (tên = SystemLanguage.ToString(), vd "Vietnamese"); gọi trước khi dựng view, hoặc sau đó gọi view.RelocalizeHud().</summary>
        public static void SetLanguage(string lang) { _inited = true; Apply(lang); }

        /// <summary>Đổi ngôn ngữ: gộp file base + file Buzzy của ngôn ngữ đó, chọn, lưu PlayerPrefs, báo Changed.</summary>
        public static void Apply(string lang, bool save = true)
        {
            if (Array.IndexOf(Languages, lang) < 0) lang = "English";
            var baseAsset = Resources.Load<TextAsset>(lang == "English" ? "Localization" : "Localization-" + lang);
            if (baseAsset != null) Localization.LoadCSV(baseAsset.bytes, true);
            var mine = Resources.Load<TextAsset>("Localization-Buzzy-" + lang);
            if (mine != null) Localization.LoadCSV(mine.bytes, true);
            else Debug.LogWarning("[BuzzyStrings] thiếu Resources/Localization-Buzzy-" + lang + ".txt");
            Localization.language = lang;
            if (save) PlayerPrefs.SetString("Language", lang);
            if (Changed != null) Changed();
        }

        /// <summary>Chuỗi theo khoá; thiếu khoá → trả về khoá (dễ thấy khi soát). Arabic được định hình.</summary>
        public static string Get(string key)
        {
            if (!_inited) Init();
            string s = null;
            try { s = Localization.Exists(key) ? Localization.Get(key) : null; } catch (Exception) { s = null; }
            if (string.IsNullOrEmpty(s)) return key;
            return IsRtl ? Rtl(s) : s;
        }

        /// <summary>Chuỗi có tham số {0}…: thay tham số trước rồi mới định hình Arabic (số giữ chữ số Latin).</summary>
        public static string Format(string key, params object[] args)
        {
            if (!_inited) Init();
            string s = null;
            try { s = Localization.Exists(key) ? Localization.Get(key) : null; } catch (Exception) { s = null; }
            if (string.IsNullOrEmpty(s)) return key + " " + string.Join(" ", Array.ConvertAll(args, a => a == null ? "" : a.ToString()));
            try { s = string.Format(s, args); } catch (FormatException) { }
            return IsRtl ? Rtl(s) : s;
        }

        static string Rtl(string s)
        {
            try { return ArabicSupport.ArabicFixer.Fix(s, true, false); } catch (Exception) { return s; }
        }
    }
}
