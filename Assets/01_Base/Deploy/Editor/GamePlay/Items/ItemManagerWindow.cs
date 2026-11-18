using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BaseProject.EditorTools.Item
{
    /// <summary>
    /// View: xem tình trạng các item gameplay (script / segment / GiftDatabase / scene) và tạo item mới.
    /// Menu: Tools > Base Project > Item > Item Manager
    /// </summary>
    public class ItemManagerWindow : EditorWindow
    {
        private const string _KEY_PREFS_SETTING = "BaseProject.ItemManager.Setting";
        private const string _KEY_PREFS_SETTING_COLLECT = "BaseProject.ItemManager.SettingCollect";
        private static readonly string[] _NAME_TABS = { "Item gameplay", "Item thu thập (Event / Challenge)" };

        private class ItemRow
        {
            public GiftType typeItem;
            public Type typeClass;
            public bool isHasConfig;
            public bool isHasGift;
            public Sprite icon;
            public ItemBase itemInScene;
        }

        private ItemCreateSetting _setting = new ItemCreateSetting();
        private Sprite _icon;
        private List<ItemRow> _lsRow = new List<ItemRow>();
        private Vector2 _scroll;
        private bool _isFoldoutCreate = true;
        private int _valueNextFree;
        private string _errorCreate = string.Empty;
        private int _indexTab;

        private CollectItemCreateSetting _settingCollect = new CollectItemCreateSetting();
        private Sprite _iconCollect;
        private List<CollectSourceInfo> _lsSource = new List<CollectSourceInfo>();
        private string _errorCreateCollect = string.Empty;

        [MenuItem("Tools/Base Project/Item/Item Manager")]
        public static void OpenWindow()
        {
            var _window = GetWindow<ItemManagerWindow>("Item Manager");
            _window.minSize = new Vector2(560, 480);
        }

        private void OnEnable()
        {
            string _json = EditorPrefs.GetString(_KEY_PREFS_SETTING, string.Empty);
            if (!string.IsNullOrEmpty(_json))
                JsonUtility.FromJsonOverwrite(_json, _setting);
            string _jsonCollect = EditorPrefs.GetString(_KEY_PREFS_SETTING_COLLECT, string.Empty);
            if (!string.IsNullOrEmpty(_jsonCollect))
                JsonUtility.FromJsonOverwrite(_jsonCollect, _settingCollect);
            _Refresh();
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(_KEY_PREFS_SETTING, JsonUtility.ToJson(_setting));
            EditorPrefs.SetString(_KEY_PREFS_SETTING_COLLECT, JsonUtility.ToJson(_settingCollect));
        }

        private void OnFocus()
        {
            _Refresh();
        }

        private void _Refresh()
        {
            _lsRow.Clear();
            foreach (var _typeItem in ItemCreatorController.GetGameplayItemTypes())
            {
                ItemCreatorController.HasInGiftDatabase(_typeItem, out var _icon);
                _lsRow.Add(new ItemRow
                {
                    typeItem = _typeItem,
                    typeClass = ItemCreatorController.FindItemClass(_typeItem),
                    isHasConfig = ItemCreatorController.HasConfigInSegment(_typeItem),
                    isHasGift = _icon != null || ItemCreatorController.HasInGiftDatabase(_typeItem, out _),
                    icon = _icon,
                    itemInScene = ItemCreatorController.FindItemInScene(_typeItem)
                });
            }
            _valueNextFree = ItemCreatorController.GetNextFreeValue();
            _errorCreate = ItemCreatorController.Validate(_setting);
            _lsSource = CollectItemCreatorController.GetSources();
            _errorCreateCollect = CollectItemCreatorController.Validate(_settingCollect, _lsSource);
            Repaint();
        }

        private void OnGUI()
        {
            _indexTab = GUILayout.Toolbar(_indexTab, _NAME_TABS, GUILayout.Height(24));
            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_indexTab == 0)
            {
                _DrawList();
                EditorGUILayout.Space(10);
                _DrawCreate();
            }
            else
            {
                _DrawCollectList();
                EditorGUILayout.Space(10);
                _DrawCollectCreate();
            }
            EditorGUILayout.EndScrollView();
        }

        #region Danh sách

        private void _DrawList()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Item gameplay (GiftType {GiftDatabase.ITEM_GAMEPLAY_MIN} - {GiftDatabase.ITEM_GAMEPLAY_MAX})", EditorStyles.boldLabel);
            if (GUILayout.Button("Làm mới", GUILayout.Width(80)))
                _Refresh();
            EditorGUILayout.EndHorizontal();

            bool _isHasControllerInScene = ItemCreatorController.FindItemControllerInScene() != null;
            if (!_isHasControllerInScene)
                EditorGUILayout.HelpBox("Scene đang mở không có ItemController (mở scene gameplay để thêm item vào scene).", MessageType.Info);

            foreach (var _row in _lsRow)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(_row.icon != null ? AssetPreview.GetAssetPreview(_row.icon) : null, GUILayout.Width(32), GUILayout.Height(32));
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"{_row.typeItem} ({(int)_row.typeItem})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"Script: {(_row.typeClass != null ? _row.typeClass.Name : "—")}   " +
                    $"Segment: {(_row.isHasConfig ? "✔" : "✘")}   " +
                    $"GiftDatabase: {(_row.isHasGift ? "✔" : "✘")}   " +
                    $"Scene: {(_row.itemInScene != null ? "✔" : "✘")}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                if (_row.typeClass != null && GUILayout.Button("Mở", GUILayout.Width(44)))
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(ItemCreatorController.GetPathScript(_row.typeClass)));
                if (!_row.isHasConfig && GUILayout.Button("+ Segment", GUILayout.Width(80)))
                {
                    ItemCreatorController.AddConfigToSegments(_row.typeItem, _setting);
                    _Refresh();
                }
                if (!_row.isHasGift && GUILayout.Button("+ Gift", GUILayout.Width(56)))
                {
                    ItemCreatorController.AddToGiftDatabase(_row.typeItem, null, _row.typeItem.ToString(), string.Empty);
                    _Refresh();
                }
                GUI.enabled = _isHasControllerInScene && _row.typeClass != null;
                if (_row.itemInScene == null && GUILayout.Button("+ Scene", GUILayout.Width(64)))
                {
                    ItemCreatorController.AddItemToScene(_row.typeClass, _row.typeItem, ItemBase.GetPlace.GamePlayUI);
                    _Refresh();
                }
                GUI.enabled = true;
                if (_row.itemInScene != null && GUILayout.Button("Chọn", GUILayout.Width(50)))
                    Selection.activeGameObject = _row.itemInScene.gameObject;
                EditorGUILayout.EndHorizontal();
            }
        }

        #endregion

        #region Item thu thập

        private void _DrawCollectList()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Event / Challenge trong GameController prefab", EditorStyles.boldLabel);
            if (GUILayout.Button("Làm mới", GUILayout.Width(80)))
                _Refresh();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Số item rơi / level: percentSpawnItems của event / challenge. Điểm, x2, giữ khi thua, hệ số: segment > collectItem.", MessageType.None);

            if (_lsSource.Count == 0)
                EditorGUILayout.HelpBox("Không tìm thấy ChallengeBase / EventBase trong GameController prefab.", MessageType.Info);

            foreach (var _source in _lsSource)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(_source.icon != null ? AssetPreview.GetAssetPreview(_source.icon) : null, GUILayout.Width(32), GUILayout.Height(32));
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"{_source.Id}  ({_source.nameObject})", EditorStyles.boldLabel);
                string _config = _source.config == null
                    ? "chưa có cấu hình segment"
                    : $"{(_source.config.isEnable ? "bật" : "TẮT")}, +{_source.config.scorePerItem}/item, x2: {_source.config.typeItemBoostX2}, giữ khi thua: {(_source.config.isKeepWhenLose ? "có" : "không")}";
                EditorGUILayout.LabelField($"Prefab: {(_source.prefabCollect != null ? _source.prefabCollect.name : "—")}   |   {_config}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                if (_source.prefabCollect != null && GUILayout.Button("Chọn", GUILayout.Width(50)))
                {
                    Selection.activeObject = _source.prefabCollect.gameObject;
                    EditorGUIUtility.PingObject(_source.prefabCollect.gameObject);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void _DrawCollectCreate()
        {
            EditorGUILayout.LabelField("Tạo item thu thập mới", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUI.indentLevel++;
            var _ids = _lsSource.ConvertAll(s => s.Id).ToArray();
            int _indexSource = Mathf.Max(0, System.Array.IndexOf(_ids, _settingCollect.idSource));
            if (_ids.Length > 0)
                _settingCollect.idSource = _ids[EditorGUILayout.Popup("Event / Challenge", _indexSource, _ids)];
            _settingCollect.nameItem = EditorGUILayout.TextField(new GUIContent("Tên (PascalCase)", "Ví dụ: Pumpkin -> class PumpkinCollectItem"), _settingCollect.nameItem);
            EditorGUILayout.LabelField(" ", $"class {_settingCollect.NameClass}", EditorStyles.miniLabel);
            _settingCollect.description = EditorGUILayout.TextField("Mô tả", _settingCollect.description);
            _iconCollect = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Icon", "Để trống dùng icon của event / challenge"), _iconCollect, typeof(Sprite), false);
            _settingCollect.folderScript = EditorGUILayout.TextField("Thư mục script", _settingCollect.folderScript);
            _settingCollect.folderPrefab = EditorGUILayout.TextField("Thư mục prefab", _settingCollect.folderPrefab);
            _settingCollect.isAssignToSource = EditorGUILayout.Toggle(new GUIContent("Gán làm item rơi", "Thay itemChallengePrefab / itemEventPrefab của nguồn trong GameController prefab"), _settingCollect.isAssignToSource);
            EditorGUI.indentLevel--;
            if (EditorGUI.EndChangeCheck())
                _errorCreateCollect = CollectItemCreatorController.Validate(_settingCollect, _lsSource);

            if (!string.IsNullOrEmpty(_errorCreateCollect))
                EditorGUILayout.HelpBox(_errorCreateCollect, MessageType.Warning);
            GUI.enabled = string.IsNullOrEmpty(_errorCreateCollect);
            if (GUILayout.Button("Tạo item thu thập", GUILayout.Height(30)))
            {
                EditorPrefs.SetString(_KEY_PREFS_SETTING_COLLECT, JsonUtility.ToJson(_settingCollect));
                var _settingCopy = JsonUtility.FromJson<CollectItemCreateSetting>(JsonUtility.ToJson(_settingCollect));
                var _iconCopy = _iconCollect;
                EditorApplication.delayCall += () => CollectItemCreatorController.Create(_settingCopy, _iconCopy);
            }
            GUI.enabled = true;
        }

        #endregion

        #region Tạo mới

        private void _DrawCreate()
        {
            _isFoldoutCreate = EditorGUILayout.Foldout(_isFoldoutCreate, "Tạo item mới", true, EditorStyles.foldoutHeader);
            if (!_isFoldoutCreate)
                return;

            EditorGUI.BeginChangeCheck();
            EditorGUI.indentLevel++;
            _setting.nameItem = EditorGUILayout.TextField(new GUIContent("Tên (PascalCase)", "Ví dụ: Bomb -> GiftType.BombItem, class ItemBomb"), _setting.nameItem);
            EditorGUILayout.LabelField(" ", $"GiftType.{_setting.NameEnum} = {_valueNextFree}   |   class {_setting.NameClass}", EditorStyles.miniLabel);
            _setting.description = EditorGUILayout.TextField("Mô tả", _setting.description);
            _setting.getPlace = (ItemBase.GetPlace)EditorGUILayout.EnumPopup(new GUIContent("Vị trí dùng", "GamePlayUI: nút trong level. SellectLevel: chọn trước khi vào level. Gadern: gameplay tự gọi"), _setting.getPlace);
            _icon = (Sprite)EditorGUILayout.ObjectField("Icon", _icon, typeof(Sprite), false);
            _setting.folderScript = EditorGUILayout.TextField("Thư mục script", _setting.folderScript);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Cấu hình segment mặc định", EditorStyles.boldLabel);
            _setting.unlockLevel = EditorGUILayout.IntField("Mở khoá ở level", _setting.unlockLevel);
            _setting.price = EditorGUILayout.IntField("Giá (coin)", _setting.price);
            _setting.packAmount = EditorGUILayout.IntField("Số lượng / lần mua", _setting.packAmount);
            _setting.amountDefault = EditorGUILayout.IntField("Tặng lần đầu", _setting.amountDefault);
            _setting.durationActive = EditorGUILayout.FloatField(new GUIContent("Thời gian tác dụng (s)", "0 = dùng tức thì"), _setting.durationActive);
            _setting.cooldown = EditorGUILayout.FloatField("Hồi chiêu (s)", _setting.cooldown);
            _setting.maxUsePerLevel = EditorGUILayout.IntField(new GUIContent("Tối đa / level", "0 = không giới hạn"), _setting.maxUsePerLevel);
            _setting.isConsumeOnActive = EditorGUILayout.Toggle("Trừ item khi dùng", _setting.isConsumeOnActive);
            _setting.workValueText = EditorGUILayout.TextField(new GUIContent("Work value", "Các số cách nhau dấu phẩy, đọc bằng GetWorkValue(index)"), _setting.workValueText);

            EditorGUILayout.Space(4);
            _setting.isAddToSegment = EditorGUILayout.Toggle("Thêm vào mọi segment", _setting.isAddToSegment);
            _setting.isAddToGiftDatabase = EditorGUILayout.Toggle("Thêm vào GiftDatabase", _setting.isAddToGiftDatabase);
            _setting.isAddToScene = EditorGUILayout.Toggle("Thêm vào ItemController (scene đang mở)", _setting.isAddToScene);
            EditorGUI.indentLevel--;
            if (EditorGUI.EndChangeCheck())
                _errorCreate = ItemCreatorController.Validate(_setting);

            if (!string.IsNullOrEmpty(_errorCreate))
                EditorGUILayout.HelpBox(_errorCreate, MessageType.Warning);
            GUI.enabled = string.IsNullOrEmpty(_errorCreate);
            if (GUILayout.Button("Tạo item", GUILayout.Height(30)))
            {
                EditorPrefs.SetString(_KEY_PREFS_SETTING, JsonUtility.ToJson(_setting));
                var _settingCopy = JsonUtility.FromJson<ItemCreateSetting>(JsonUtility.ToJson(_setting));
                var _iconCopy = _icon;
                EditorApplication.delayCall += () => ItemCreatorController.Create(_settingCopy, _iconCopy);
            }
            GUI.enabled = true;
        }

        #endregion
    }
}
