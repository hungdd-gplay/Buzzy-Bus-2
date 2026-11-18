using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BaseProject.EditorTools.Item
{
    /// <summary>
    /// Controller tạo item mới đầy đủ:
    ///   1. Thêm giá trị GiftType (1000 - 1999) vào GiftDatabase.cs (trước dòng [ITEM_GENERATOR]).
    ///   2. Sinh script Item{Name} : ItemBase với các hàm tác dụng để trống.
    ///   3. Sau khi biên dịch: thêm cấu hình vào mọi segment (GameController prefab), icon vào GiftDatabase,
    ///      và object item vào ItemController trong scene đang mở.
    /// Các bước 3 cũng gọi riêng được từ Item Manager cho item đã có.
    /// </summary>
    [InitializeOnLoad]
    public static class ItemCreatorController
    {
        public const string MARKER_ENUM = "[ITEM_GENERATOR]";
        private const string _KEY_PENDING = "BaseProject.ItemCreator.Pending";
        private const string _PATH_GAME_CONTROLLER_PREFAB = ProjectPaths.GAME_CONTROLLER_PREFAB;

        static ItemCreatorController()
        {
            EditorApplication.delayCall += ProcessPending;
        }

        #region Tra cứu

        /// <summary>Các GiftType thuộc khoảng item gameplay.</summary>
        public static List<GiftType> GetGameplayItemTypes()
        {
            return Enum.GetValues(typeof(GiftType)).Cast<GiftType>().Where(GiftDatabase.IsGameplayItem).Distinct().OrderBy(t => (int)t).ToList();
        }

        /// <summary>Giá trị enum trống tiếp theo (đọc cả file để tính những giá trị chưa biên dịch).</summary>
        public static int GetNextFreeValue()
        {
            var _used = new HashSet<int>(GetGameplayItemTypes().Select(t => (int)t));
            string _pathEnum = GetPathScript(typeof(GiftDatabase));
            if (!string.IsNullOrEmpty(_pathEnum))
            {
                foreach (Match _match in Regex.Matches(File.ReadAllText(_pathEnum), @"=\s*(\d+)\s*,"))
                    _used.Add(int.Parse(_match.Groups[1].Value, CultureInfo.InvariantCulture));
            }
            for (int v = GiftDatabase.ITEM_GAMEPLAY_MIN; v <= GiftDatabase.ITEM_GAMEPLAY_MAX; v++)
                if (!_used.Contains(v))
                    return v;
            return -1;
        }

        public static string Validate(ItemCreateSetting setting)
        {
            if (string.IsNullOrEmpty(setting.nameItem) || !Regex.IsMatch(setting.nameItem, @"^[A-Z][A-Za-z0-9]*$"))
                return "Tên item phải dạng PascalCase (bắt đầu bằng chữ hoa, chỉ chữ và số). Ví dụ: Bomb, AddTime.";
            if (Enum.GetNames(typeof(GiftType)).Contains(setting.NameEnum))
                return $"GiftType.{setting.NameEnum} đã tồn tại.";
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(setting.NameClass, false) != null))
                return $"Class {setting.NameClass} đã tồn tại.";
            if (!setting.folderScript.StartsWith("Assets"))
                return "Thư mục script phải nằm trong Assets.";
            if (File.Exists($"{setting.folderScript.TrimEnd('/')}/{setting.NameClass}.cs"))
                return $"File {setting.NameClass}.cs đã tồn tại.";
            if (string.IsNullOrEmpty(GetPathScript(typeof(GiftDatabase))))
                return "Không tìm thấy GiftDatabase.cs.";
            if (GetNextFreeValue() < 0)
                return "Hết giá trị GiftType trong khoảng 1000 - 1999.";
            return string.Empty;
        }

        /// <summary>Class ItemBase đang dùng GiftType này (dựa vào code có "GiftType.{name}").</summary>
        public static Type FindItemClass(GiftType typeItem)
        {
            string _token = "GiftType." + typeItem;
            foreach (var _type in TypeCache.GetTypesDerivedFrom<ItemBase>())
            {
                if (_type.IsAbstract)
                    continue;
                string _path = GetPathScript(_type);
                if (!string.IsNullOrEmpty(_path) && File.ReadAllText(_path).Contains(_token))
                    return _type;
            }
            return null;
        }

        public static string GetPathScript(Type type)
        {
            foreach (var _guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                var _script = AssetDatabase.LoadAssetAtPath<MonoScript>(_path);
                if (_script != null && (_script.GetClass() == type || Path.GetFileNameWithoutExtension(_path) == type.Name && _script.text.Contains("class " + type.Name)))
                    return _path;
            }
            return null;
        }

        #endregion

        #region Tạo item

        public static bool Create(ItemCreateSetting setting, Sprite icon)
        {
            string _error = Validate(setting);
            if (!string.IsNullOrEmpty(_error))
            {
                EditorUtility.DisplayDialog("Tạo item", _error, "OK");
                return false;
            }

            int _value = GetNextFreeValue();
            string _pathEnum = GetPathScript(typeof(GiftDatabase));
            string _pathScript = $"{setting.folderScript.TrimEnd('/')}/{setting.NameClass}.cs";

            if (!_InsertEnum(_pathEnum, setting.NameEnum, _value, setting.description))
                return false;
            _CreateFolder(setting.folderScript.TrimEnd('/'));
            File.WriteAllText(_pathScript, GenerateScript(setting), new UTF8Encoding(true));

            var _pending = new ItemPendingCreate
            {
                setting = setting,
                valueEnum = _value,
                pathIcon = icon != null ? AssetDatabase.GetAssetPath(icon) : string.Empty
            };
            SessionState.SetString(_KEY_PENDING, JsonUtility.ToJson(_pending));

            AssetDatabase.ImportAsset(_pathEnum, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(_pathScript, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[ItemCreator] Đã thêm GiftType.{setting.NameEnum} = {_value} và {_pathScript}. Chờ biên dịch để gắn vào segment / scene...");
            return true;
        }

        private static bool _InsertEnum(string pathEnum, string nameEnum, int value, string description)
        {
            string _text = File.ReadAllText(pathEnum);
            int _indexMarker = _text.IndexOf(MARKER_ENUM, StringComparison.Ordinal);
            if (_indexMarker < 0)
            {
                EditorUtility.DisplayDialog("Tạo item", $"Không thấy dòng '// {MARKER_ENUM}' trong enum GiftType ({pathEnum}).", "OK");
                return false;
            }
            int _indexLineStart = _text.LastIndexOf('\n', _indexMarker) + 1;
            string _newLine = _text.Contains("\r\n") ? "\r\n" : "\n";
            string _comment = string.IsNullOrEmpty(description) ? string.Empty : " // " + description.Replace("\n", " ");
            string _line = $"    {nameEnum} = {value},{_comment}{_newLine}";
            _text = _text.Insert(_indexLineStart, _line);
            File.WriteAllText(pathEnum, _text, new UTF8Encoding(true));
            return true;
        }

        public static string GenerateScript(ItemCreateSetting setting)
        {
            string _description = string.IsNullOrEmpty(setting.description) ? setting.nameItem : setting.description;
            var _code = new StringBuilder();
            _code.AppendLine("using UnityEngine;");
            _code.AppendLine();
            _code.AppendLine("/// <summary>");
            _code.AppendLine($"/// Item {setting.nameItem}: {_description}");
            _code.AppendLine($"/// Cấu hình: Segment > coreGameData > bootsConfig > items (GiftType.{setting.NameEnum}).");
            _code.AppendLine("/// Phần dùng chung (kiểm tra, trừ số lượng, hồi chiêu, thời gian tác dụng, spawn...) đã có ở ItemBase.");
            _code.AppendLine("/// </summary>");
            _code.AppendLine($"public class {setting.NameClass} : ItemBase");
            _code.AppendLine("{");
            _code.AppendLine("    private void Reset()");
            _code.AppendLine("    {");
            _code.AppendLine($"        typeItem = GiftType.{setting.NameEnum};");
            _code.AppendLine($"        getPlace = GetPlace.{setting.getPlace};");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    public override void Init()");
            _code.AppendLine("    {");
            _code.AppendLine("        base.Init();");
            _code.AppendLine("        // Khởi tạo 1 lần khi vào gameplay");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    /// <summary>Điều kiện riêng của gameplay để dùng item.</summary>");
            _code.AppendLine("    protected override bool IsCanUseItemHandle()");
            _code.AppendLine("    {");
            _code.AppendLine("        return true;");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    /// <summary>Tác dụng của item. Thông số từ segment: GetWorkValue(0), GetWorkValue(1)...</summary>");
            _code.AppendLine("    protected override bool ActiveItemHandle()");
            _code.AppendLine("    {");
            _code.AppendLine("        // TODO: tác dụng lên gameplay");
            _code.AppendLine("        return true;");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    /// <summary>Gỡ tác dụng (chỉ gọi khi durationActive > 0).</summary>");
            _code.AppendLine("    protected override void EndItemHandle()");
            _code.AppendLine("    {");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    protected override void OnActiveUpdate(float deltaTime)");
            _code.AppendLine("    {");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    protected override void OnSpawnHandle(GameObject spawned)");
            _code.AppendLine("    {");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    protected override void OnLevelStartHandle()");
            _code.AppendLine("    {");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    protected override void OnLevelEndHandle()");
            _code.AppendLine("    {");
            _code.AppendLine("    }");
            _code.AppendLine("}");
            return _code.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
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

            var _pending = JsonUtility.FromJson<ItemPendingCreate>(_json);
            var _setting = _pending.setting;
            var _typeClass = _FindType(_setting.NameClass);
            if (!Enum.TryParse(_setting.NameEnum, out GiftType _typeItem) || _typeClass == null)
            {
                Debug.LogWarning($"[ItemCreator] Chưa thấy GiftType.{_setting.NameEnum} / {_setting.NameClass} (đang lỗi biên dịch?). Sửa lỗi xong sẽ tự chạy tiếp.");
                return;
            }
            SessionState.EraseString(_KEY_PENDING);

            var _log = new StringBuilder($"[ItemCreator] Đã tạo item {_setting.NameClass} (GiftType.{_typeItem} = {(int)_typeItem}).");
            if (_setting.isAddToSegment)
                _log.Append($"\n - Segment: thêm cấu hình vào {AddConfigToSegments(_typeItem, _setting)} segment.");
            if (_setting.isAddToGiftDatabase)
            {
                var _icon = string.IsNullOrEmpty(_pending.pathIcon) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(_pending.pathIcon);
                _log.Append(AddToGiftDatabase(_typeItem, _icon, _setting.nameItem, _setting.description)
                    ? "\n - GiftDatabase: đã thêm (icon dùng cho nút / hiệu ứng)."
                    : "\n - GiftDatabase: không tìm thấy asset.");
            }
            if (_setting.isAddToScene)
            {
                var _go = AddItemToScene(_typeClass, _typeItem, _setting.getPlace);
                _log.Append(_go != null
                    ? $"\n - Scene: đã thêm '{_go.name}' vào ItemController (nhớ Save scene)."
                    : "\n - Scene: không có ItemController trong scene đang mở. Mở scene gameplay rồi bấm 'Thêm vào scene' trong Item Manager.");
            }
            Debug.Log(_log.ToString());

            var _script = AssetDatabase.LoadAssetAtPath<MonoScript>(GetPathScript(_typeClass));
            if (_script != null)
                EditorGUIUtility.PingObject(_script);
        }

        #endregion

        #region Segment / GiftDatabase / Scene

        public static CoreGameData.BootsData CreateConfig(GiftType typeItem, ItemCreateSetting setting)
        {
            return new CoreGameData.BootsData
            {
                typeItem = typeItem,
                isEnable = true,
                unlockLevel = setting.unlockLevel,
                price = setting.price,
                packAmount = setting.packAmount,
                amountDefault = setting.amountDefault,
                durationActive = setting.durationActive,
                cooldown = setting.cooldown,
                maxUsePerLevel = setting.maxUsePerLevel,
                isConsumeOnActive = setting.isConsumeOnActive,
                workValue = _ParseWorkValue(setting.workValueText)
            };
        }

        /// <summary>Thêm cấu hình item vào mọi segment chưa có. Trả về số segment được thêm.</summary>
        public static int AddConfigToSegments(GiftType typeItem, ItemCreateSetting setting)
        {
            string _pathPrefab = _FindGameControllerPrefab();
            if (string.IsNullOrEmpty(_pathPrefab))
            {
                Debug.LogWarning("[ItemCreator] Không tìm thấy prefab có SegmentController.");
                return 0;
            }

            var _root = PrefabUtility.LoadPrefabContents(_pathPrefab);
            int _count = 0;
            try
            {
                var _segmentController = _root.GetComponentInChildren<SegmentController>(true);
                if (_segmentController == null || _segmentController.segments == null)
                    return 0;
                foreach (var _segment in _segmentController.segments)
                {
                    var _boots = _segment?.coreGameData?.config?.bootsConfig;
                    if (_boots == null)
                        continue;
                    if (_boots.items == null)
                        _boots.items = new List<CoreGameData.BootsData>();
                    if (_boots.items.Any(b => b != null && b.typeItem == typeItem))
                        continue;
                    _boots.items.Add(CreateConfig(typeItem, setting));
                    _count++;
                }
                if (_count > 0)
                {
                    EditorUtility.SetDirty(_segmentController);
                    PrefabUtility.SaveAsPrefabAsset(_root, _pathPrefab);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(_root);
            }
            return _count;
        }

        /// <summary>Item đã có cấu hình trong segment đầu tiên chưa.</summary>
        public static bool HasConfigInSegment(GiftType typeItem)
        {
            string _pathPrefab = _FindGameControllerPrefab();
            if (string.IsNullOrEmpty(_pathPrefab))
                return false;
            var _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_pathPrefab);
            var _segmentController = _prefab != null ? _prefab.GetComponentInChildren<SegmentController>(true) : null;
            if (_segmentController == null || _segmentController.segments == null || _segmentController.segments.Count == 0)
                return false;
            var _boots = _segmentController.segments[0]?.coreGameData?.config?.bootsConfig;
            return _boots != null && _boots.GetBoot(typeItem) != null;
        }

        public static bool AddToGiftDatabase(GiftType typeItem, Sprite icon, string nameItem, string description)
        {
            var _guid = AssetDatabase.FindAssets("t:GiftDatabase").FirstOrDefault();
            if (string.IsNullOrEmpty(_guid))
                return false;
            var _database = AssetDatabase.LoadAssetAtPath<GiftDatabase>(AssetDatabase.GUIDToAssetPath(_guid));
            if (_database.giftList == null)
                _database.giftList = new Dictionary<GiftType, Gift>();

            if (!_database.giftList.TryGetValue(typeItem, out var _gift) || _gift == null)
            {
                _gift = new Gift();
                _database.giftList[typeItem] = _gift;
            }
            _gift.nameGift = nameItem;
            _gift.detailGift = description;
            if (icon != null)
                typeof(Gift).GetField("giftSprite", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(_gift, icon);

            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssetIfDirty(_database);
            return true;
        }

        public static bool HasInGiftDatabase(GiftType typeItem, out Sprite icon)
        {
            icon = null;
            var _guid = AssetDatabase.FindAssets("t:GiftDatabase").FirstOrDefault();
            if (string.IsNullOrEmpty(_guid))
                return false;
            var _database = AssetDatabase.LoadAssetAtPath<GiftDatabase>(AssetDatabase.GUIDToAssetPath(_guid));
            if (_database == null || _database.giftList == null || !_database.giftList.ContainsKey(typeItem))
                return false;
            icon = _database.GetIconItem(typeItem);
            return true;
        }

        public static ItemController FindItemControllerInScene()
        {
            return Object.FindObjectsByType<ItemController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        }

        public static ItemBase FindItemInScene(GiftType typeItem)
        {
            return Object.FindObjectsByType<ItemBase>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(i => i.typeItem == typeItem);
        }

        /// <summary>Tạo object con của ItemController trong scene đang mở, gắn class item.</summary>
        public static GameObject AddItemToScene(Type typeClass, GiftType typeItem, ItemBase.GetPlace getPlace)
        {
            var _itemController = FindItemControllerInScene();
            if (_itemController == null || typeClass == null)
                return null;
            var _existing = FindItemInScene(typeItem);
            if (_existing != null)
                return _existing.gameObject;

            var _go = new GameObject(typeClass.Name);
            Undo.RegisterCreatedObjectUndo(_go, "Add Item");
            _go.transform.SetParent(_itemController.transform, false);
            var _item = (ItemBase)_go.AddComponent(typeClass);
            _item.typeItem = typeItem;
            _item.getPlace = getPlace;
            EditorUtility.SetDirty(_item);
            EditorSceneManager.MarkSceneDirty(_go.scene);
            return _go;
        }

        #endregion

        #region Helper

        private static string _FindGameControllerPrefab()
        {
            var _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_PATH_GAME_CONTROLLER_PREFAB);
            if (_prefab != null && _prefab.GetComponentInChildren<SegmentController>(true) != null)
                return _PATH_GAME_CONTROLLER_PREFAB;
            foreach (var _guid in AssetDatabase.FindAssets("GameController t:Prefab"))
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                var _go = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
                if (_go != null && _go.GetComponentInChildren<SegmentController>(true) != null)
                    return _path;
            }
            return null;
        }

        private static float[] _ParseWorkValue(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new float[0];
            return text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f)
                .ToArray();
        }

        private static Type _FindType(string nameClass)
        {
            foreach (var _assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var _type = _assembly.GetType(nameClass, false);
                if (_type != null && typeof(ItemBase).IsAssignableFrom(_type))
                    return _type;
            }
            return null;
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
