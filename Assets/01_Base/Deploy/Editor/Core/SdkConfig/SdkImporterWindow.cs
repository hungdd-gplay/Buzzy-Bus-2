#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace BaseProject.EditorTools
{
    /// <summary>
    /// Thư mục SDK nằm NGOÀI Assets: &lt;Project&gt;/SDKs  (không bị Unity import tự động).
    ///  - *.unitypackage : import bằng AssetDatabase.ImportPackage (không hỏi), lần lượt từng gói.
    ///  - *.tgz          : thêm vào Package Manager (file:...) .
    /// Menu: Tools > Base Project > SDK > Import tất cả SDK  |  SDK Importer (chọn từng gói)  |  Mở thư mục SDKs
    /// Thứ tự import theo tên file -> đặt tiền tố số (01_, 02_...) nếu cần thứ tự.
    /// </summary>
    [InitializeOnLoad]
    public class SdkImporterWindow : EditorWindow
    {
        // Hàng đợi lưu trong SessionState để sống qua domain reload (import gói có script sẽ compile lại).
        private const string KEY_SESSION_QUEUE = "BaseProject.SdkImporter.Queue";
        private const string KEY_SESSION_TOTAL = "BaseProject.SdkImporter.Total";

        static SdkImporterWindow()
        {
            EditorApplication.delayCall += () =>
            {
                if (_LoadQueue().Count > 0 && !EditorApplication.isCompiling)
                {
                    Debug.Log("[SDK] Tiếp tục import sau khi Unity compile lại...");
                    _RegisterEvents();
                    _ImportNext();
                }
            };
        }

        public const string NAME_FOLDER_SDK = "SDKs";
        private const string PATH_MENU = "Tools/Base Project/SDK/";

        private static AddRequest _requestAddPackage;

        private readonly Dictionary<string, bool> _dictSelected = new Dictionary<string, bool>();
        private Vector2 _scrollPosition;

        public static string FolderSdk => Path.GetFullPath(Path.Combine(Application.dataPath, "..", NAME_FOLDER_SDK));

        [MenuItem(PATH_MENU + "Import tất cả SDK", false, 1)]
        public static void ImportAll()
        {
            var _lstFile = GetSdkFiles();
            if (_lstFile.Count == 0)
            {
                EditorUtility.DisplayDialog("SDK", "Thư mục " + FolderSdk + " chưa có .unitypackage / .tgz nào.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Import tất cả SDK",
                    $"Import {_lstFile.Count} gói trong {NAME_FOLDER_SDK}/:\n- " + string.Join("\n- ", _lstFile.Select(Path.GetFileName)),
                    "Import", "Hủy"))
                return;
            ImportFiles(_lstFile);
        }

        [MenuItem(PATH_MENU + "SDK Importer (chọn gói)", false, 2)]
        public static void Open()
        {
            GetWindow<SdkImporterWindow>("SDK Importer").minSize = new Vector2(420, 300);
        }

        [MenuItem(PATH_MENU + "Mở thư mục SDKs", false, 20)]
        public static void OpenFolder()
        {
            Directory.CreateDirectory(FolderSdk);
            EditorUtility.RevealInFinder(FolderSdk);
        }

        public static List<string> GetSdkFiles()
        {
            if (!Directory.Exists(FolderSdk))
                return new List<string>();
            return Directory.GetFiles(FolderSdk, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".unitypackage", System.StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".tgz", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f)
                .ToList();
        }

        /// <summary>Import lần lượt (gói sau chạy khi gói trước xong để tránh xung đột).</summary>
        public static void ImportFiles(IEnumerable<string> lstFile)
        {
            var _lstQueue = new List<string>(lstFile);
            _SaveQueue(_lstQueue);
            SessionState.SetInt(KEY_SESSION_TOTAL, _lstQueue.Count);
            _RegisterEvents();
            _ImportNext();
        }

        private static void _RegisterEvents()
        {
            AssetDatabase.importPackageCompleted -= _OnImportFinished;
            AssetDatabase.importPackageFailed -= _OnImportFailed;
            AssetDatabase.importPackageCancelled -= _OnImportFinished;
            AssetDatabase.importPackageCompleted += _OnImportFinished;
            AssetDatabase.importPackageFailed += _OnImportFailed;
            AssetDatabase.importPackageCancelled += _OnImportFinished;
        }

        private static List<string> _LoadQueue()
        {
            string _raw = SessionState.GetString(KEY_SESSION_QUEUE, string.Empty);
            return string.IsNullOrEmpty(_raw) ? new List<string>() : _raw.Split('\n').Where(x => x.Length > 0).ToList();
        }

        private static void _SaveQueue(List<string> lstQueue)
        {
            SessionState.SetString(KEY_SESSION_QUEUE, string.Join("\n", lstQueue));
        }

        private static void _ImportNext()
        {
            var _lstQueue = _LoadQueue();
            int _countTotal = SessionState.GetInt(KEY_SESSION_TOTAL, _lstQueue.Count);
            int _countDone = _countTotal - _lstQueue.Count;
            if (_lstQueue.Count == 0)
            {
                AssetDatabase.importPackageCompleted -= _OnImportFinished;
                AssetDatabase.importPackageFailed -= _OnImportFailed;
                AssetDatabase.importPackageCancelled -= _OnImportFinished;
                EditorUtility.ClearProgressBar();
                Debug.Log($"[SDK] Hoàn tất import {_countTotal} gói.");
                return;
            }

            string _file = _lstQueue[0];
            _lstQueue.RemoveAt(0);
            _SaveQueue(_lstQueue);
            EditorUtility.DisplayProgressBar("Import SDK", Path.GetFileName(_file), (float)_countDone / Mathf.Max(1, _countTotal));
            Debug.Log("[SDK] Import: " + _file);

            if (_file.EndsWith(".tgz", System.StringComparison.OrdinalIgnoreCase))
            {
                _requestAddPackage = Client.Add("file:" + _file.Replace('\\', '/'));
                EditorApplication.update += _WaitAddPackage;
            }
            else
            {
                AssetDatabase.ImportPackage(_file, false);
            }
        }

        private static void _WaitAddPackage()
        {
            if (_requestAddPackage == null || !_requestAddPackage.IsCompleted)
                return;
            EditorApplication.update -= _WaitAddPackage;
            if (_requestAddPackage.Status == StatusCode.Failure)
                Debug.LogError("[SDK] Lỗi thêm package: " + _requestAddPackage.Error?.message);
            _requestAddPackage = null;
            _ImportNext();
        }

        private static void _OnImportFinished(string namePackage)
        {
            EditorApplication.delayCall += _ImportNext;
        }

        private static void _OnImportFailed(string namePackage, string error)
        {
            Debug.LogError($"[SDK] Import lỗi {namePackage}: {error}");
            EditorApplication.delayCall += _ImportNext;
        }

        // ================== Window ==================

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Đặt file .unitypackage / .tgz vào: " + FolderSdk, MessageType.Info);
            var _lstFile = GetSdkFiles();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (var _file in _lstFile)
            {
                if (!_dictSelected.ContainsKey(_file))
                    _dictSelected[_file] = true;
                string _relative = _file.Substring(FolderSdk.Length).TrimStart('\\', '/');
                _dictSelected[_file] = EditorGUILayout.ToggleLeft(_relative, _dictSelected[_file]);
            }
            if (_lstFile.Count == 0)
                EditorGUILayout.LabelField("(trống)");
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Mở thư mục"))
                OpenFolder();
            if (GUILayout.Button("Chọn hết"))
                foreach (var _file in _lstFile) _dictSelected[_file] = true;
            if (GUILayout.Button("Bỏ chọn"))
                foreach (var _file in _lstFile) _dictSelected[_file] = false;
            using (new EditorGUI.DisabledScope(!_lstFile.Any(f => _dictSelected.TryGetValue(f, out bool v) && v)))
            {
                if (GUILayout.Button("Import đã chọn", GUILayout.Height(26)))
                    ImportFiles(_lstFile.Where(f => _dictSelected[f]).ToList());
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
