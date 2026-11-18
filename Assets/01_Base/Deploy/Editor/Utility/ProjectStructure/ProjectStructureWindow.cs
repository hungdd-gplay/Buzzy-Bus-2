using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BaseProject.EditorTools.ProjectStructure
{
    /// <summary>
    /// View: Tools > Base Project > Project Structure.
    /// Quét -> xem trước (lọc, sửa đích, bỏ chọn) -> Xuất/Nhập CSV -> Thực hiện -> Hoàn tác nếu cần.
    /// </summary>
    public class ProjectStructureWindow : EditorWindow
    {
        private const int _COUNT_PER_PAGE = 300;
        private const string _KEY_RULES = "BaseProject.ProjectStructure.Rules";

        private ProjectStructureRules _rules;
        private MovePlan _plan;
        private Vector2 _scrollRules;
        private Vector2 _scrollList;
        private bool _isShowRules;
        private string _search = string.Empty;
        private int _indexFilter; // 0 = tất cả, còn lại = MoveCategory + 1
        private int _page;
        private List<MoveEntry> _lsFiltered = new List<MoveEntry>();

        [MenuItem("Tools/Base Project/Project Structure", false, 300)]
        public static void Open()
        {
            var _window = GetWindow<ProjectStructureWindow>("Project Structure");
            _window.minSize = new Vector2(900, 500);
        }

        private void OnEnable()
        {
            string _json = EditorPrefs.GetString(_KEY_RULES, string.Empty);
            _rules = string.IsNullOrEmpty(_json) ? new ProjectStructureRules() : JsonUtility.FromJson<ProjectStructureRules>(_json);
        }

        private void _SaveRules()
        {
            EditorPrefs.SetString(_KEY_RULES, JsonUtility.ToJson(_rules));
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Cấu trúc đích: 01_Base/{Res, Deploy/Model|View|Controller|Editor}, 02_Gameplay/{Res, Deploy}, 03_ThirdParty, 04_Environment, Resources, StreamingAssets.\n" +
                "SDK nhạy đường dẫn (Firebase, MaxSdk, Plugins, TextMesh Pro...) giữ ở gốc. Asset không dùng chuyển vào _ToDelete.\n" +
                "Di chuyển bằng AssetDatabase.MoveAsset nên giữ GUID (không mất tham chiếu). Hãy commit git trước khi Thực hiện.",
                MessageType.Info);

            _DrawRules();
            _DrawToolbar();
            if (_plan == null)
            {
                EditorGUILayout.HelpBox("Bấm 'Quét' để tạo kế hoạch xem trước. Chưa có gì bị di chuyển.", MessageType.None);
                return;
            }
            _DrawSummary();
            _DrawList();
        }

        private void _DrawRules()
        {
            _isShowRules = EditorGUILayout.Foldout(_isShowRules, "Luật phân loại", true);
            if (!_isShowRules)
                return;
            _scrollRules = EditorGUILayout.BeginScrollView(_scrollRules, GUILayout.MaxHeight(220));
            EditorGUI.BeginChangeCheck();
            _rules.keepAtRoot = _DrawListText("Giữ ở gốc (SDK nhạy đường dẫn)", _rules.keepAtRoot);
            _rules.thirdParty = _DrawListText("Chuyển vào 03_ThirdParty", _rules.thirdParty);
            _rules.environmentPatterns = _DrawListText("File môi trường (tiền tố tên / .đuôi)", _rules.environmentPatterns);
            _rules.junkAtRoot = _DrawListText("Rác ở gốc -> _ToDelete", _rules.junkAtRoot);
            _rules.mainBaseFolder = EditorGUILayout.TextField("Thư mục base chính", _rules.mainBaseFolder);
            _rules.isScanUnused = EditorGUILayout.Toggle("Quét asset không dùng", _rules.isScanUnused);
            if (EditorGUI.EndChangeCheck())
                _SaveRules();
            if (GUILayout.Button("Khôi phục luật mặc định", GUILayout.Width(180)))
            {
                _rules = new ProjectStructureRules();
                _SaveRules();
            }
            EditorGUILayout.EndScrollView();
        }

        private static List<string> _DrawListText(string label, List<string> values)
        {
            EditorGUILayout.LabelField(label + " (phân cách bằng dấu phẩy)");
            string _text = EditorGUILayout.TextArea(string.Join(", ", values), GUILayout.MinHeight(34));
            return _text.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }

        private void _DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Quét (xem trước)", GUILayout.Height(26)))
                _Scan();
            GUI.enabled = _plan != null;
            if (GUILayout.Button("Xuất CSV", GUILayout.Height(26)))
                _ExportCsv();
            if (GUILayout.Button("Nhập CSV", GUILayout.Height(26)))
                _ImportCsv();
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Thực hiện", GUILayout.Height(26)))
                _Apply();
            GUI.backgroundColor = Color.white;
            GUI.enabled = ProjectStructureController.IsHasHistory;
            if (GUILayout.Button("Hoàn tác lần trước", GUILayout.Height(26)))
                _Revert();
            GUI.enabled = true;
            GUI.enabled = ProjectStructureController.IsHasHistory;
            if (GUILayout.Button("Sửa sau tái cấu trúc", GUILayout.Height(26)))
                _FixAfterRestructure();
            GUI.enabled = true;
            if (GUILayout.Button("Chỉ tạo khung thư mục", GUILayout.Height(26)))
            {
                ProjectStructureController.CreateFolderSkeleton();
                AssetDatabase.Refresh();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void _DrawSummary()
        {
            var _counts = _plan.entries.GroupBy(e => e.category).ToDictionary(g => g.Key, g => g.Count());
            var _text = new StringBuilder($"Quét lúc {_plan.timeCreate} — {_plan.entries.Count} mục, chọn {_plan.entries.Count(e => e.isSelected)}:  ");
            foreach (MoveCategory _category in Enum.GetValues(typeof(MoveCategory)))
                if (_counts.TryGetValue(_category, out int _count))
                    _text.Append($"{_category} {_count}   ");
            EditorGUILayout.LabelField(_text.ToString(), EditorStyles.boldLabel);
            foreach (var _warning in _plan.warnings)
                EditorGUILayout.HelpBox(_warning, MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            var _options = new[] { "Tất cả" }.Concat(Enum.GetNames(typeof(MoveCategory))).ToArray();
            EditorGUI.BeginChangeCheck();
            _indexFilter = EditorGUILayout.Popup(_indexFilter, _options, GUILayout.Width(140));
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                _page = 0;
                _Filter();
            }
            if (GUILayout.Button("Chọn hết", GUILayout.Width(80)))
                _lsFiltered.Where(e => e.category != MoveCategory.Conflict).ToList().ForEach(e => e.isSelected = true);
            if (GUILayout.Button("Bỏ hết", GUILayout.Width(80)))
                _lsFiltered.ForEach(e => e.isSelected = false);
            EditorGUILayout.EndHorizontal();
        }

        private void _DrawList()
        {
            int _countPage = Mathf.Max(1, Mathf.CeilToInt(_lsFiltered.Count / (float)_COUNT_PER_PAGE));
            _page = Mathf.Clamp(_page, 0, _countPage - 1);

            _scrollList = EditorGUILayout.BeginScrollView(_scrollList);
            foreach (var _entry in _lsFiltered.Skip(_page * _COUNT_PER_PAGE).Take(_COUNT_PER_PAGE))
            {
                EditorGUILayout.BeginHorizontal();
                _entry.isSelected = EditorGUILayout.Toggle(_entry.isSelected, GUILayout.Width(18));
                GUI.color = _GetColor(_entry.category);
                EditorGUILayout.LabelField(_entry.category.ToString(), GUILayout.Width(90));
                GUI.color = Color.white;
                if (GUILayout.Button(_entry.pathFrom, EditorStyles.label, GUILayout.Width(position.width * 0.38f)))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(_entry.pathFrom));
                _entry.pathTo = EditorGUILayout.TextField(_entry.pathTo);
                EditorGUILayout.LabelField(new GUIContent("?", _entry.reason), GUILayout.Width(14));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _page > 0;
            if (GUILayout.Button("<", GUILayout.Width(30)))
                _page--;
            GUI.enabled = true;
            EditorGUILayout.LabelField($"Trang {_page + 1}/{_countPage} ({_lsFiltered.Count} mục)", GUILayout.Width(200));
            GUI.enabled = _page < _countPage - 1;
            if (GUILayout.Button(">", GUILayout.Width(30)))
                _page++;
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private static Color _GetColor(MoveCategory category)
        {
            switch (category)
            {
                case MoveCategory.Unused: return new Color(1f, 0.6f, 0.4f);
                case MoveCategory.Conflict: return new Color(1f, 0.4f, 0.4f);
                case MoveCategory.ThirdParty: return new Color(0.6f, 0.8f, 1f);
                case MoveCategory.Environment: return new Color(1f, 0.9f, 0.4f);
                case MoveCategory.Resources:
                case MoveCategory.StreamingAssets: return new Color(0.8f, 0.6f, 1f);
                default: return Color.white;
            }
        }

        private void _Filter()
        {
            _lsFiltered = _plan == null ? new List<MoveEntry>() : _plan.entries.Where(e =>
                (_indexFilter == 0 || (int)e.category == _indexFilter - 1)
                && (string.IsNullOrEmpty(_search) || e.pathFrom.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                    || e.pathTo.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        }

        private void _Scan()
        {
            try
            {
                _plan = ProjectStructureController.BuildPlan(_rules, (info, progress) => EditorUtility.DisplayCancelableProgressBar("Project Structure", info, progress));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            _page = 0;
            _Filter();
        }

        private void _ExportCsv()
        {
            string _path = EditorUtility.SaveFilePanel("Xuất kế hoạch", Path.GetFullPath("."), "ProjectStructurePlan", "csv");
            if (string.IsNullOrEmpty(_path))
                return;
            File.WriteAllText(_path, ProjectStructureController.ToCsv(_plan), new UTF8Encoding(true));
            EditorUtility.RevealInFinder(_path);
        }

        private void _ImportCsv()
        {
            string _path = EditorUtility.OpenFilePanel("Nhập kế hoạch", Path.GetFullPath("."), "csv");
            if (string.IsNullOrEmpty(_path))
                return;
            int _count = ProjectStructureController.ApplyCsv(_plan, File.ReadAllText(_path));
            _Filter();
            ShowNotification(new GUIContent($"Đã cập nhật {_count} mục từ CSV"));
        }

        private void _Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Project Structure", "Hãy thoát Play mode trước.", "OK");
                return;
            }
            if (EditorUtility.scriptCompilationFailed)
            {
                EditorUtility.DisplayDialog("Project Structure", "Project đang lỗi biên dịch. Sửa lỗi trước để phân loại script chính xác.", "OK");
                return;
            }
            int _countSelected = _plan.entries.Count(e => e.isSelected && e.category != MoveCategory.Conflict);
            if (!EditorUtility.DisplayDialog("Project Structure",
                    $"Di chuyển {_countSelected} mục?\n\nNên commit git trước. Có thể hoàn tác bằng nút 'Hoàn tác lần trước'.", "Thực hiện", "Huỷ"))
                return;

            (int _countDone, List<string> _errors) _result;
            try
            {
                _result = ProjectStructureController.Apply(_plan, (info, progress) => EditorUtility.DisplayCancelableProgressBar("Đang di chuyển", info, progress));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            _ShowResult("Đã di chuyển", _result._countDone, _result._errors);
            _plan = null;
            _lsFiltered.Clear();
        }

        private void _FixAfterRestructure()
        {
            if (!EditorUtility.DisplayDialog("Project Structure",
                    "Sửa các mục bị xếp sai ở lần tái cấu trúc trước (UnityPurchasing, Asset refs in scenes, Plugins native, FeatureSettings, IAPDatabase, khoá service account)?",
                    "Sửa", "Huỷ"))
                return;
            var _result = ProjectStructureController.FixAfterRestructure();
            foreach (var _log in _result.logs)
                Debug.Log("[ProjectStructure] " + _log);
            EditorUtility.DisplayDialog("Project Structure", $"Đã sửa {_result.countDone} mục (chi tiết ở Console).", "OK");
            _plan = null;
            _lsFiltered.Clear();
        }

        private void _Revert()
        {
            if (!EditorUtility.DisplayDialog("Project Structure", "Chuyển mọi thứ về vị trí cũ (lần thực hiện gần nhất)?", "Hoàn tác", "Huỷ"))
                return;
            var _result = ProjectStructureController.RevertLast();
            _ShowResult("Đã hoàn tác", _result.countDone, _result.errors);
            _plan = null;
            _lsFiltered.Clear();
        }

        private static void _ShowResult(string title, int countDone, List<string> errors)
        {
            foreach (var _error in errors)
                Debug.LogWarning("[ProjectStructure] " + _error);
            string _message = $"{title} {countDone} mục." + (errors.Count > 0 ? $"\n{errors.Count} lỗi (xem Console)." : string.Empty);
            Debug.Log("[ProjectStructure] " + _message);
            EditorUtility.DisplayDialog("Project Structure", _message, "OK");
        }
    }
}
