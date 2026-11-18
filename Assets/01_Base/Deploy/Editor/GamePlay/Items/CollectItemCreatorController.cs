using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BaseProject.EditorTools.Item
{
    /// <summary>Thông tin 1 nguồn item thu thập (challenge / event) đọc từ GameController prefab.</summary>
    public class CollectSourceInfo
    {
        public bool isChallenge;
        public ChallengeType typeChallenge;
        public EventSaleType typeEvent;
        public string nameObject;
        public CollectItemBase prefabCollect;
        public Sprite icon;
        public CollectItemConfig config;

        public string Id => isChallenge ? "Challenge." + typeChallenge : "Event." + typeEvent;
        public string NameBaseClass => isChallenge ? nameof(ItemChallenge) : nameof(EventSaleItem);
    }

    [Serializable]
    public class CollectItemCreateSetting
    {
        public string nameItem = "NewCollect";
        public string description = "";
        public string idSource = "";
        public string folderScript = ProjectPaths.GAMEPLAY_DEPLOY + "/Controller/CollectItems";
        public string folderPrefab = ProjectPaths.GAMEPLAY_RES + "/Prefabs/CollectItems";
        public bool isAssignToSource = true;

        public string NameClass => nameItem + "CollectItem";
    }

    [Serializable]
    public class CollectItemPendingCreate
    {
        public CollectItemCreateSetting setting;
        public string pathIcon;
    }

    /// <summary>
    /// Controller tạo item thu thập mới cho 1 event / challenge:
    /// sinh script {Name}CollectItem (kế thừa ItemChallenge / EventSaleItem, các hàm *Handle để trống),
    /// sau khi biên dịch tạo prefab (Body = SpriteRenderer icon) và gán làm item rơi của nguồn trong GameController prefab.
    /// </summary>
    [InitializeOnLoad]
    public static class CollectItemCreatorController
    {
        private const string _KEY_PENDING = "BaseProject.CollectItemCreator.Pending";

        static CollectItemCreatorController()
        {
            EditorApplication.delayCall += ProcessPending;
        }

        #region Tra cứu

        public static List<CollectSourceInfo> GetSources()
        {
            var _lsSource = new List<CollectSourceInfo>();
            var _prefab = _LoadGameControllerPrefab(out _);
            if (_prefab == null)
                return _lsSource;

            var _segment = _GetFirstSegment(_prefab);
            foreach (var _challenge in _prefab.GetComponentsInChildren<ChallengeBase>(true))
            {
                _lsSource.Add(new CollectSourceInfo
                {
                    isChallenge = true,
                    typeChallenge = _challenge.typeChallenge,
                    nameObject = _challenge.name,
                    prefabCollect = _challenge.itemChallengePrefab,
                    icon = _challenge.iconItem,
                    config = _SafeGet(() => _segment?.challengeData?.GetChallengeConfig(_challenge.typeChallenge)?.collectItem)
                });
            }
            foreach (var _event in _prefab.GetComponentsInChildren<EventBase>(true))
            {
                _lsSource.Add(new CollectSourceInfo
                {
                    isChallenge = false,
                    typeEvent = _event.typeEvent,
                    nameObject = _event.name,
                    prefabCollect = _event.itemEventPrefab,
                    icon = _event.iconItem,
                    config = _SafeGet(() => _segment?.saleData?.GetEndlessConfig(_event.typeEvent)?.collectItem)
                });
            }
            return _lsSource;
        }

        public static string Validate(CollectItemCreateSetting setting, List<CollectSourceInfo> lsSource)
        {
            if (string.IsNullOrEmpty(setting.nameItem) || !Regex.IsMatch(setting.nameItem, @"^[A-Z][A-Za-z0-9]*$"))
                return "Tên phải dạng PascalCase (bắt đầu bằng chữ hoa, chỉ chữ và số). Ví dụ: Pumpkin, Star.";
            if (lsSource.All(s => s.Id != setting.idSource))
                return "Hãy chọn event / challenge sẽ rơi item này.";
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(setting.NameClass, false) != null))
                return $"Class {setting.NameClass} đã tồn tại.";
            if (!setting.folderScript.StartsWith("Assets") || !setting.folderPrefab.StartsWith("Assets"))
                return "Thư mục script / prefab phải nằm trong Assets.";
            if (File.Exists($"{setting.folderScript.TrimEnd('/')}/{setting.NameClass}.cs"))
                return $"File {setting.NameClass}.cs đã tồn tại.";
            return string.Empty;
        }

        #endregion

        #region Tạo

        public static bool Create(CollectItemCreateSetting setting, Sprite icon)
        {
            var _lsSource = GetSources();
            string _error = Validate(setting, _lsSource);
            if (!string.IsNullOrEmpty(_error))
            {
                EditorUtility.DisplayDialog("Tạo item thu thập", _error, "OK");
                return false;
            }

            var _source = _lsSource.First(s => s.Id == setting.idSource);
            string _pathScript = $"{setting.folderScript.TrimEnd('/')}/{setting.NameClass}.cs";
            _CreateFolder(setting.folderScript.TrimEnd('/'));
            File.WriteAllText(_pathScript, GenerateScript(setting, _source), new UTF8Encoding(true));

            SessionState.SetString(_KEY_PENDING, JsonUtility.ToJson(new CollectItemPendingCreate
            {
                setting = setting,
                pathIcon = icon != null ? AssetDatabase.GetAssetPath(icon) : string.Empty
            }));
            AssetDatabase.ImportAsset(_pathScript, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[CollectItemCreator] Đã tạo {_pathScript}. Chờ biên dịch để tạo prefab...");
            return true;
        }

        public static string GenerateScript(CollectItemCreateSetting setting, CollectSourceInfo source)
        {
            string _description = string.IsNullOrEmpty(setting.description) ? setting.nameItem : setting.description;
            var _code = new StringBuilder();
            _code.AppendLine("using UnityEngine;");
            _code.AppendLine();
            _code.AppendLine("/// <summary>");
            _code.AppendLine($"/// Item thu thập {setting.nameItem} ({source.Id}): {_description}");
            _code.AppendLine("/// Cấu hình: segment > collectItem của event / challenge. Phần chung (spawn, nhặt, x2, bay về khung) ở CollectItemBase.");
            _code.AppendLine("/// </summary>");
            _code.AppendLine($"public class {setting.NameClass} : {source.NameBaseClass}");
            _code.AppendLine("{");
            _AppendMethod(_code, "Ngay sau khi tạo và gán event / challenge.", "protected override void OnSetupHandle()");
            _AppendMethod(_code, "Sau khi gắn vào object gameplay.", "protected override void OnSpawnHandle(Transform posSpawn)");
            _AppendMethod(_code, null, "protected override void OnAppearHandle()");
            _AppendMethod(_code, "Điều kiện riêng để nhặt (ví dụ: object mang item phải bị phá).", "protected override bool IsCanCollectHandle()", "return true;");
            _AppendMethod(_code, "Lúc nhặt: âm thanh, rung, hiệu ứng riêng... Thông số: GetWorkValue(0).", "protected override void OnCollectHandle(int amount, bool isUseMagnet)", "// TODO");
            _AppendMethod(_code, "Icon bay về tới khung.", "protected override void OnFlyDoneHandle()");
            _AppendMethod(_code, null, "protected override void OnDespawnHandle()", null, true);
            _code.AppendLine("}");
            return _code.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        private static void _AppendMethod(StringBuilder code, string summary, string signature, string body = null, bool isLast = false)
        {
            if (!string.IsNullOrEmpty(summary))
                code.AppendLine($"    /// <summary>{summary}</summary>");
            code.AppendLine("    " + signature);
            code.AppendLine("    {");
            if (!string.IsNullOrEmpty(body))
                code.AppendLine("        " + body);
            code.AppendLine("    }");
            if (!isLast)
                code.AppendLine();
        }

        #endregion

        #region Sau khi biên dịch

        public static void ProcessPending()
        {
            string _json = SessionState.GetString(_KEY_PENDING, string.Empty);
            if (string.IsNullOrEmpty(_json))
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ProcessPending;
                return;
            }

            var _pending = JsonUtility.FromJson<CollectItemPendingCreate>(_json);
            var _setting = _pending.setting;
            var _type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(_setting.NameClass, false)).FirstOrDefault(t => t != null);
            if (_type == null)
            {
                Debug.LogWarning($"[CollectItemCreator] Chưa thấy class {_setting.NameClass} (đang lỗi biên dịch?). Sửa lỗi xong sẽ tự chạy tiếp.");
                return;
            }
            SessionState.EraseString(_KEY_PENDING);

            var _source = GetSources().FirstOrDefault(s => s.Id == _setting.idSource);
            if (_source == null)
            {
                Debug.LogError($"[CollectItemCreator] Không thấy nguồn {_setting.idSource} trong GameController prefab.");
                return;
            }
            var _icon = string.IsNullOrEmpty(_pending.pathIcon) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(_pending.pathIcon);
            if (_icon == null)
                _icon = _source.icon;

            string _pathPrefab = CreatePrefab(_type, _setting, _source, _icon);
            var _log = new StringBuilder($"[CollectItemCreator] Đã tạo {_setting.NameClass}: {_pathPrefab}");
            if (_setting.isAssignToSource && AssignPrefabToSource(_source, _pathPrefab, _icon))
                _log.Append($"\n - Đã gán làm item rơi của {_source.Id} ({_source.nameObject}) trong GameController prefab.");
            Debug.Log(_log.ToString());
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(_pathPrefab));
        }

        public static string CreatePrefab(Type typeClass, CollectItemCreateSetting setting, CollectSourceInfo source, Sprite icon)
        {
            _CreateFolder(setting.folderPrefab.TrimEnd('/'));
            string _pathPrefab = AssetDatabase.GenerateUniqueAssetPath($"{setting.folderPrefab.TrimEnd('/')}/{setting.NameClass}.prefab");

            var _root = new GameObject(setting.NameClass);
            try
            {
                var _body = new GameObject("Body");
                _body.transform.SetParent(_root.transform, false);
                var _spriteRenderer = _body.AddComponent<SpriteRenderer>();
                _spriteRenderer.sprite = icon;

                var _component = (CollectItemBase)_root.AddComponent(typeClass);
                var _serialized = new SerializedObject(_component);
                _serialized.FindProperty("icon").objectReferenceValue = icon;
                _serialized.FindProperty("spriteBody").objectReferenceValue = _spriteRenderer;
                var _propertyType = _serialized.FindProperty(source.isChallenge ? "typeChallenge" : "typeEvent");
                if (_propertyType != null)
                    _propertyType.intValue = source.isChallenge ? (int)source.typeChallenge : (int)source.typeEvent;
                _serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(_root, _pathPrefab);
            }
            finally
            {
                Object.DestroyImmediate(_root);
            }
            return _pathPrefab;
        }

        /// <summary>Gán prefab làm item rơi của challenge / event trong GameController prefab.</summary>
        public static bool AssignPrefabToSource(CollectSourceInfo source, string pathPrefab, Sprite icon)
        {
            _LoadGameControllerPrefab(out string _pathGameController);
            if (string.IsNullOrEmpty(_pathGameController))
                return false;
            var _prefabItem = AssetDatabase.LoadAssetAtPath<GameObject>(pathPrefab);
            if (_prefabItem == null)
                return false;

            var _root = PrefabUtility.LoadPrefabContents(_pathGameController);
            bool _isAssigned = false;
            try
            {
                if (source.isChallenge)
                {
                    var _challenge = _root.GetComponentsInChildren<ChallengeBase>(true).FirstOrDefault(c => c.typeChallenge == source.typeChallenge);
                    var _item = _prefabItem.GetComponent<ItemChallenge>();
                    if (_challenge != null && _item != null)
                    {
                        _challenge.itemChallengePrefab = _item;
                        if (_challenge.iconItem == null)
                            _challenge.iconItem = icon;
                        EditorUtility.SetDirty(_challenge);
                        _isAssigned = true;
                    }
                }
                else
                {
                    var _event = _root.GetComponentsInChildren<EventBase>(true).FirstOrDefault(e => e.typeEvent == source.typeEvent);
                    var _item = _prefabItem.GetComponent<EventSaleItem>();
                    if (_event != null && _item != null)
                    {
                        _event.itemEventPrefab = _item;
                        if (_event.iconItem == null)
                            _event.iconItem = icon;
                        EditorUtility.SetDirty(_event);
                        _isAssigned = true;
                    }
                }
                if (_isAssigned)
                    PrefabUtility.SaveAsPrefabAsset(_root, _pathGameController);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(_root);
            }
            return _isAssigned;
        }

        #endregion

        #region Helper

        private static GameObject _LoadGameControllerPrefab(out string pathPrefab)
        {
            foreach (var _path in new[] { ProjectPaths.GAME_CONTROLLER_PREFAB }
                         .Concat(AssetDatabase.FindAssets("GameController t:Prefab").Select(AssetDatabase.GUIDToAssetPath)))
            {
                var _go = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
                if (_go != null && _go.GetComponentInChildren<SegmentController>(true) != null)
                {
                    pathPrefab = _path;
                    return _go;
                }
            }
            pathPrefab = null;
            return null;
        }

        private static SegmentData _GetFirstSegment(GameObject prefab)
        {
            var _segmentController = prefab.GetComponentInChildren<SegmentController>(true);
            return _segmentController != null && _segmentController.segments != null && _segmentController.segments.Count > 0
                ? _segmentController.segments[0]
                : null;
        }

        private static T _SafeGet<T>(Func<T> getter) where T : class
        {
            try
            {
                return getter();
            }
            catch
            {
                return null;
            }
        }

        private static void _CreateFolder(string pathFolder)
        {
            if (string.IsNullOrEmpty(pathFolder) || AssetDatabase.IsValidFolder(pathFolder))
                return;
            string _parent = Path.GetDirectoryName(pathFolder)?.Replace('\\', '/');
            _CreateFolder(_parent);
            AssetDatabase.CreateFolder(_parent, Path.GetFileName(pathFolder));
        }

        #endregion
    }
}
