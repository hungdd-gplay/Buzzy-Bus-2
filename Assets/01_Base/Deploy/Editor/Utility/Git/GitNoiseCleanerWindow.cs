#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BaseProject.EditorTools
{
    /// <summary>
    /// Tools > Base Project > Git > Dọn thay đổi rác
    /// .gitignore KHÔNG thể ẩn thay đổi của file đã được git theo dõi. Tool này quét các file đang "Modified",
    /// file nào mà MỌI dòng thay đổi chỉ là dòng "rác" (Unity tự ghi khi bấm/mở: m_EditorClassIdentifier, timeCreated,
    /// serializedVersion, xuống dòng CRLF/LF, khoảng trắng...) thì đưa vào danh sách để khôi phục về bản đã commit.
    /// Mẫu dòng rác cấu hình trong file &lt;Project&gt;/.gitnoise (mỗi dòng 1 regex, dòng # là ghi chú).
    /// </summary>
    public class GitNoiseCleanerWindow : EditorWindow
    {
        private const string NAME_FILE_NOISE = ".gitnoise";

        private static readonly string[] _ARRAY_PATTERN_DEFAULT =
        {
            @"^\s*m_EditorClassIdentifier:",
            @"^\s*timeCreated:",
            @"^\s*licenseType:",
            @"^\s*serializedVersion:",
            @"^\s*fileFormatVersion:",
            @"^\s*m_LastModified",
            @"^\s*m_IndirectSpecularColor:",
            @"^\s*m_LightingDataAsset:",
        };

        private class FileNoise
        {
            public string path;
            public bool isSelected = true;
            public int countLineChanged;
        }

        private readonly List<FileNoise> _lstFileNoise = new List<FileNoise>();
        private int _countModified;
        private string _messageResult = string.Empty;
        private Vector2 _scrollPosition;

        private static string FolderProject => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [MenuItem("Tools/Base Project/Git/Dọn thay đổi rác (ID, time...)", false, 50)]
        public static void Open()
        {
            var _window = GetWindow<GitNoiseCleanerWindow>("Git - thay đổi rác");
            _window.minSize = new Vector2(520, 360);
            _window._Scan();
        }

        [MenuItem("Tools/Base Project/Git/Mở file cấu hình .gitnoise", false, 51)]
        public static void OpenNoiseFile()
        {
            string _path = Path.Combine(FolderProject, NAME_FILE_NOISE);
            if (!File.Exists(_path))
                File.WriteAllLines(_path, new[] { "# Mỗi dòng 1 regex: dòng thay đổi khớp mẫu được coi là rác" }.Concat(_ARRAY_PATTERN_DEFAULT));
            EditorUtility.OpenWithDefaultApp(_path);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Danh sách file chỉ thay đổi các dòng rác (Unity tự ghi). Khôi phục = git checkout về bản đã commit. " +
                                    "File có thay đổi thật sẽ KHÔNG nằm trong danh sách.", MessageType.Info);
            EditorGUILayout.LabelField($"File đang Modified: {_countModified}   |   Chỉ có thay đổi rác: {_lstFileNoise.Count}");

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (var _file in _lstFileNoise)
                _file.isSelected = EditorGUILayout.ToggleLeft($"{_file.path}   ({_file.countLineChanged} dòng)", _file.isSelected);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Quét lại"))
                _Scan();
            if (GUILayout.Button("Sửa mẫu (.gitnoise)"))
                OpenNoiseFile();
            using (new EditorGUI.DisabledScope(!_lstFileNoise.Any(f => f.isSelected)))
            {
                if (GUILayout.Button("Khôi phục file đã chọn", GUILayout.Height(26)))
                    _RevertSelected();
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_messageResult))
                EditorGUILayout.HelpBox(_messageResult, MessageType.None);
        }

        private void _Scan()
        {
            _lstFileNoise.Clear();
            _messageResult = string.Empty;
            if (!_RunGit("rev-parse --is-inside-work-tree", out _, out string _error))
            {
                _messageResult = "Project chưa phải git repo hoặc chưa cài git: " + _error;
                return;
            }

            _RunGit("-c core.quotepath=off diff --name-only", out string _output, out _);
            var _lstModified = _output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
            _countModified = _lstModified.Count;
            var _lstRegex = _LoadPatterns();

            foreach (var _path in _lstModified)
            {
                _RunGit($"-c core.quotepath=off diff --no-color -U0 --ignore-cr-at-eol --ignore-space-change -- \"{_path}\"", out string _diff, out _);
                if (_IsOnlyNoise(_diff, _lstRegex, out int _countLine))
                    _lstFileNoise.Add(new FileNoise { path = _path, countLineChanged = _countLine });
            }
            _messageResult = $"Quét xong: {_lstFileNoise.Count}/{_countModified} file chỉ có thay đổi rác.";
            Repaint();
        }

        /// <summary>Mọi dòng +/- trong diff đều khớp mẫu rác (hoặc diff rỗng do chỉ khác CRLF/khoảng trắng).</summary>
        private static bool _IsOnlyNoise(string diff, List<Regex> lstRegex, out int countLine)
        {
            countLine = 0;
            foreach (var _rawLine in diff.Split('\n'))
            {
                string _line = _rawLine.TrimEnd('\r');
                if (_line.StartsWith("+++") || _line.StartsWith("---"))
                    continue;
                if (!_line.StartsWith("+") && !_line.StartsWith("-"))
                    continue;
                if (_line.StartsWith("Binary"))
                    return false;
                countLine++;
                string _content = _line.Substring(1);
                if (string.IsNullOrWhiteSpace(_content))
                    continue;
                if (!lstRegex.Any(r => r.IsMatch(_content)))
                    return false;
            }
            return !diff.Contains("Binary files");
        }

        private void _RevertSelected()
        {
            var _lstSelected = _lstFileNoise.Where(f => f.isSelected).Select(f => f.path).ToList();
            if (!EditorUtility.DisplayDialog("Khôi phục", $"Khôi phục {_lstSelected.Count} file về bản đã commit?", "Khôi phục", "Hủy"))
                return;

            int _countDone = 0;
            foreach (var _batch in _Chunk(_lstSelected, 40))
            {
                string _args = "checkout -- " + string.Join(" ", _batch.Select(p => $"\"{p}\""));
                if (_RunGit(_args, out _, out string _error))
                    _countDone += _batch.Count;
                else
                    Debug.LogError("[GitNoise] " + _error);
            }
            AssetDatabase.Refresh();
            _messageResult = $"Đã khôi phục {_countDone} file.";
            _Scan();
        }

        private static IEnumerable<List<string>> _Chunk(List<string> lst, int size)
        {
            for (int i = 0; i < lst.Count; i += size)
                yield return lst.GetRange(i, Math.Min(size, lst.Count - i));
        }

        private static List<Regex> _LoadPatterns()
        {
            string _path = Path.Combine(FolderProject, NAME_FILE_NOISE);
            IEnumerable<string> _lines = File.Exists(_path) ? File.ReadAllLines(_path) : _ARRAY_PATTERN_DEFAULT;
            var _lst = new List<Regex>();
            foreach (var _line in _lines)
            {
                string _pattern = _line.Trim();
                if (_pattern.Length == 0 || _pattern.StartsWith("#"))
                    continue;
                try { _lst.Add(new Regex(_pattern, RegexOptions.Compiled)); }
                catch (Exception _exception) { Debug.LogWarning($"[GitNoise] Regex lỗi '{_pattern}': {_exception.Message}"); }
            }
            return _lst;
        }

        private static bool _RunGit(string arguments, out string output, out string error)
        {
            output = string.Empty;
            error = string.Empty;
            try
            {
                var _info = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = FolderProject,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                using (var _process = Process.Start(_info))
                {
                    var _taskOutput = _process.StandardOutput.ReadToEndAsync();
                    error = _process.StandardError.ReadToEnd();
                    _process.WaitForExit();
                    output = _taskOutput.Result;
                    return _process.ExitCode == 0;
                }
            }
            catch (Exception _exception)
            {
                error = _exception.Message;
                return false;
            }
        }
    }
}
#endif
