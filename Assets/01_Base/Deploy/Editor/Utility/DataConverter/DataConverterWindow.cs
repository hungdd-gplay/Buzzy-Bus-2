#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BaseProject.EditorTools.DataConverter
{
    /// <summary>
    /// Tools > Base Project > Data Converter (CSV / XLSX)
    /// B1. Chọn nguồn: file .csv/.tsv/.xlsx hoặc URL CSV (Google Sheet).
    /// B2. Chọn asset ScriptableObject đích + field danh sách (List/Array) cần đổ dữ liệu.
    ///     Chưa có class? bấm "Sinh class từ header" để tạo {Tên}Data + {Tên}Database.
    /// B3. Kiểm tra bảng khớp cột rồi bấm "Import".
    /// </summary>
    public class DataConverterWindow : EditorWindow
    {
        private const string KEY_PREFS = "BaseProject.DataConverter.";
        private const string PATH_FOLDER_GENERATE_DEFAULT = ProjectPaths.BASE_DEPLOY + "/Model/Generated";
        private const int COUNT_ROW_PREVIEW = 8;

        private enum SourceType { File = 0, Url = 1 }

        private SourceType _sourceType;
        private string _pathFile = string.Empty;
        private string _url = string.Empty;
        private List<string> _lstSheetName = new List<string>();
        private int _indexSheet;

        private DataTableModel _table;
        private string _messageResult = string.Empty;

        private ScriptableObject _target;
        private List<FieldInfo> _lstFieldList = new List<FieldInfo>();
        private int _indexFieldList;
        private ImportMode _importMode = ImportMode.Replace;
        private int _indexFieldKey;

        private string _nameClassGenerate = "NewTable";
        private string _folderGenerate = PATH_FOLDER_GENERATE_DEFAULT;

        private Vector2 _scrollPosition;

        [MenuItem("Tools/Base Project/Data Converter (CSV - XLSX)", false, 30)]
        public static void Open()
        {
            var _window = GetWindow<DataConverterWindow>("Data Converter");
            _window.minSize = new Vector2(520, 520);
        }

        private void OnEnable()
        {
            _sourceType = (SourceType)EditorPrefs.GetInt(KEY_PREFS + "sourceType", 0);
            _pathFile = EditorPrefs.GetString(KEY_PREFS + "pathFile", string.Empty);
            _url = EditorPrefs.GetString(KEY_PREFS + "url", string.Empty);
            _folderGenerate = EditorPrefs.GetString(KEY_PREFS + "folderGenerate", PATH_FOLDER_GENERATE_DEFAULT);
            _RefreshSheetNames();
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt(KEY_PREFS + "sourceType", (int)_sourceType);
            EditorPrefs.SetString(KEY_PREFS + "pathFile", _pathFile);
            EditorPrefs.SetString(KEY_PREFS + "url", _url);
            EditorPrefs.SetString(KEY_PREFS + "folderGenerate", _folderGenerate);
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            _DrawSource();
            EditorGUILayout.Space(8);
            _DrawPreview();
            EditorGUILayout.Space(8);
            _DrawTarget();
            EditorGUILayout.Space(8);
            _DrawGenerate();
            if (!string.IsNullOrEmpty(_messageResult))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(_messageResult, MessageType.None);
            }
            EditorGUILayout.EndScrollView();
        }

        // ================== B1. Nguồn ==================

        private void _DrawSource()
        {
            EditorGUILayout.LabelField("1. Nguồn dữ liệu", EditorStyles.boldLabel);
            _sourceType = (SourceType)GUILayout.Toolbar((int)_sourceType, new[] { "File (csv / tsv / xlsx)", "URL CSV (Google Sheet)" });

            if (_sourceType == SourceType.File)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                _pathFile = EditorGUILayout.TextField(_pathFile);
                if (EditorGUI.EndChangeCheck())
                    _RefreshSheetNames();
                if (GUILayout.Button("Chọn...", GUILayout.Width(70)))
                {
                    string _folderStart = string.IsNullOrEmpty(_pathFile) ? string.Empty : Path.GetDirectoryName(_pathFile);
                    string _path = EditorUtility.OpenFilePanelWithFilters("Chọn file dữ liệu", _folderStart,
                        new[] { "Bảng dữ liệu", "csv,tsv,xlsx,xlsm,txt", "Tất cả", "*" });
                    if (!string.IsNullOrEmpty(_path))
                    {
                        _pathFile = _path;
                        _RefreshSheetNames();
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (_lstSheetName.Count > 0)
                    _indexSheet = EditorGUILayout.Popup("Sheet", _indexSheet, _lstSheetName.ToArray());
            }
            else
            {
                _url = EditorGUILayout.TextField("URL", _url);
                EditorGUILayout.HelpBox("Google Sheet: File > Share > Publish to web > chọn sheet > CSV, dán link vào đây.", MessageType.None);
            }

            if (GUILayout.Button("Đọc dữ liệu", GUILayout.Height(26)))
                _LoadTable();
        }

        private void _RefreshSheetNames()
        {
            _lstSheetName.Clear();
            _indexSheet = 0;
            try
            {
                if (!string.IsNullOrEmpty(_pathFile) && File.Exists(_pathFile) && DataTableReader.IsExcelFile(_pathFile))
                    _lstSheetName = DataTableReader.GetSheetNames(_pathFile);
            }
            catch (Exception _exception)
            {
                _messageResult = "Không đọc được danh sách sheet: " + _exception.Message;
            }
        }

        private void _LoadTable()
        {
            try
            {
                _table = _sourceType == SourceType.File
                    ? DataTableReader.ReadFile(_pathFile, _indexSheet)
                    : DataTableReader.ReadUrl(_url);
                _messageResult = $"Đọc xong {_table.nameSource}: {_table.CountColumn} cột, {_table.CountRow} dòng.";
                if (string.IsNullOrEmpty(_nameClassGenerate) || _nameClassGenerate == "NewTable")
                    _nameClassGenerate = Path.GetFileNameWithoutExtension(_sourceType == SourceType.File ? _pathFile : "NewTable");
            }
            catch (Exception _exception)
            {
                _table = null;
                _messageResult = "Lỗi đọc dữ liệu: " + _exception.Message;
            }
        }

        // ================== Preview ==================

        private void _DrawPreview()
        {
            if (_table == null || _table.CountColumn == 0)
                return;
            EditorGUILayout.LabelField($"Xem trước ({Mathf.Min(COUNT_ROW_PREVIEW, _table.CountRow)}/{_table.CountRow} dòng)", EditorStyles.boldLabel);
            float _widthCell = Mathf.Max(80f, (position.width - 40f) / Mathf.Min(_table.CountColumn, 6));

            EditorGUILayout.BeginHorizontal();
            foreach (var _header in _table.lstHeader.Take(12))
                EditorGUILayout.LabelField(_header, EditorStyles.miniBoldLabel, GUILayout.Width(_widthCell));
            EditorGUILayout.EndHorizontal();

            for (int r = 0; r < Mathf.Min(COUNT_ROW_PREVIEW, _table.CountRow); r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < Mathf.Min(12, _table.CountColumn); c++)
                    EditorGUILayout.LabelField(_table.GetCell(r, c), EditorStyles.miniLabel, GUILayout.Width(_widthCell));
                EditorGUILayout.EndHorizontal();
            }
        }

        // ================== B2. Đích ==================

        private void _DrawTarget()
        {
            EditorGUILayout.LabelField("2. Asset đích", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _target = (ScriptableObject)EditorGUILayout.ObjectField("ScriptableObject", _target, typeof(ScriptableObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                _lstFieldList = DataConverterController.GetListFields(_target);
                _indexFieldList = 0;
                _indexFieldKey = 0;
            }

            if (_target == null)
                return;
            if (_lstFieldList.Count == 0)
            {
                EditorGUILayout.HelpBox("Asset không có field List/Array của class [Serializable]. Dùng 'Sinh class từ header' bên dưới.", MessageType.Warning);
                return;
            }

            _indexFieldList = EditorGUILayout.Popup("Field danh sách", _indexFieldList, _lstFieldList.Select(f => f.Name).ToArray());
            var _fieldList = _lstFieldList[Mathf.Clamp(_indexFieldList, 0, _lstFieldList.Count - 1)];
            var _typeElement = DataConverterController.GetElementType(_fieldList.FieldType);
            _importMode = (ImportMode)EditorGUILayout.EnumPopup("Chế độ", _importMode);

            if (_table == null)
            {
                EditorGUILayout.HelpBox("Đọc dữ liệu ở bước 1 trước.", MessageType.Info);
                return;
            }

            var _mapping = DataConverterController.BuildMapping(_typeElement, _table);
            if (_importMode == ImportMode.UpdateByKey)
                _indexFieldKey = EditorGUILayout.Popup("Cột khóa", _indexFieldKey, _mapping.Select(m => m.Key.Name).ToArray());

            EditorGUILayout.LabelField($"Khớp cột cho {_typeElement.Name}", EditorStyles.miniBoldLabel);
            foreach (var _pair in _mapping)
            {
                string _status = _pair.Value >= 0 ? "← " + _table.lstHeader[_pair.Value] : "(không có cột, giữ mặc định)";
                EditorGUILayout.LabelField($"  {_pair.Key.Name} : {_pair.Key.FieldType.Name}", _status);
            }
            var _unused = _table.lstHeader.Where(h => _mapping.All(m => m.Value < 0 || _table.lstHeader[m.Value] != h)).ToList();
            if (_unused.Count > 0)
                EditorGUILayout.HelpBox("Cột không dùng: " + string.Join(", ", _unused), MessageType.None);

            if (GUILayout.Button("Import", GUILayout.Height(30)))
            {
                string _nameKey = _mapping.Count > 0 ? _mapping[Mathf.Clamp(_indexFieldKey, 0, _mapping.Count - 1)].Key.Name : string.Empty;
                _messageResult = DataConverterController.Apply(_target, _fieldList, _table, _importMode, _nameKey);
            }
        }

        // ================== Sinh class ==================

        private void _DrawGenerate()
        {
            EditorGUILayout.LabelField("3. Sinh class từ header (khi chưa có kiểu dữ liệu)", EditorStyles.boldLabel);
            _nameClassGenerate = EditorGUILayout.TextField("Tên", _nameClassGenerate);
            _folderGenerate = EditorGUILayout.TextField("Thư mục", _folderGenerate);

            using (new EditorGUI.DisabledScope(_table == null))
            {
                if (GUILayout.Button("Sinh class từ header"))
                    _messageResult = DataConverterController.GenerateClassFiles(_table, _nameClassGenerate, _folderGenerate);
            }

            var _typeDatabase = DataConverterController.FindType(_nameClassGenerate + "Database");
            using (new EditorGUI.DisabledScope(_typeDatabase == null))
            {
                if (GUILayout.Button($"Tạo asset {_nameClassGenerate}Database"))
                {
                    var _asset = CreateInstance(_typeDatabase);
                    string _path = AssetDatabase.GenerateUniqueAssetPath($"{_folderGenerate}/{_nameClassGenerate}Database.asset");
                    AssetDatabase.CreateAsset(_asset, _path);
                    AssetDatabase.SaveAssets();
                    _target = (ScriptableObject)_asset;
                    _lstFieldList = DataConverterController.GetListFields(_target);
                    _indexFieldList = 0;
                    EditorGUIUtility.PingObject(_asset);
                    _messageResult = "Đã tạo " + _path + ". Bấm Import để đổ dữ liệu.";
                }
            }
        }
    }
}
#endif
