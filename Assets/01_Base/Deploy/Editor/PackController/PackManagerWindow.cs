using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static SaleSegemntData;

namespace BaseProject.EditorTools.Pack
{
    /// <summary>
    /// View: quản lý gói IAP. Mỗi gói hiển thị 2 phần:
    ///   - Thông số (SalePackDatabase): hiển thị, giảm giá, giới hạn mua, điều kiện xuất hiện.
    ///   - Lịch (segment): active, level, thời gian xuất hiện, interval, ngày.
    /// Menu: Tools > Base Project > IAP > Pack Manager
    /// </summary>
    public class PackManagerWindow : EditorWindow
    {
        private const string _KEY_PREFS_CREATE = "BaseProject.PackManager.Create";

        private SalePackDatabase _database;
        private SegmentController _segmentController;
        private SerializedObject _serializedDatabase;
        private SerializedObject _serializedSegment;
        private int _indexSegment;
        private int _indexPackSelected = -1;
        private string _filter = string.Empty;
        private Vector2 _scrollList;
        private Vector2 _scrollDetail;
        private PackCreateSetting _settingCreate = new PackCreateSetting();
        private string _errorCreate = string.Empty;
        private bool _isFoldoutCreate;

        [MenuItem("Tools/Base Project/IAP/Pack Manager")]
        public static void OpenWindow()
        {
            var _window = GetWindow<PackManagerWindow>("Pack Manager");
            _window.minSize = new Vector2(820, 520);
        }

        private void OnEnable()
        {
            string _json = EditorPrefs.GetString(_KEY_PREFS_CREATE, string.Empty);
            if (!string.IsNullOrEmpty(_json))
                JsonUtility.FromJsonOverwrite(_json, _settingCreate);
            _Reload();
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(_KEY_PREFS_CREATE, JsonUtility.ToJson(_settingCreate));
        }

        private void OnFocus()
        {
            _Reload();
        }

        private void _Reload()
        {
            _database = PackManagerController.GetSalePackDatabase();
            _segmentController = PackManagerController.GetSegmentController();
            _serializedDatabase = _database != null ? new SerializedObject(_database) : null;
            _serializedSegment = _segmentController != null ? new SerializedObject(_segmentController) : null;
            _errorCreate = PackManagerController.Validate(_settingCreate);
            Repaint();
        }

        private void OnGUI()
        {
            if (_database == null || _segmentController == null)
            {
                EditorGUILayout.HelpBox("Không tìm thấy SalePackDatabase hoặc GameController prefab có SegmentController.", MessageType.Error);
                if (GUILayout.Button("Tải lại"))
                    _Reload();
                return;
            }

            _DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            _DrawList();
            _DrawDetail();
            EditorGUILayout.EndHorizontal();
        }

        #region Toolbar

        private void _DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var _names = _segmentController.segments.Select((s, i) => $"{i}: {s.nameSegment}").ToArray();
            if (_names.Length > 0)
            {
                _indexSegment = Mathf.Clamp(_indexSegment, 0, _names.Length - 1);
                _indexSegment = EditorGUILayout.Popup(_indexSegment, _names, EditorStyles.toolbarPopup, GUILayout.Width(180));
            }
            _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(160));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Preset gói thiếu lịch", "Điền thông số + lịch theo logic controller cũ cho gói chưa có lịch"), EditorStyles.toolbarButton))
            {
                int _count = PackManagerController.ApplyPresets(false);
                _Reload();
                ShowNotification(new GUIContent($"Đã áp preset {_count} gói"));
            }
            if (GUILayout.Button("Preset (ghi đè)", EditorStyles.toolbarButton)
                && EditorUtility.DisplayDialog("Pack Manager", "Ghi đè thông số + lịch của các gói có sẵn bằng preset?", "Ghi đè", "Huỷ"))
            {
                PackManagerController.ApplyPresets(true);
                _Reload();
            }
            if (GUILayout.Button(new GUIContent("Dọn controller cũ", "Xoá component mất script (controller gói cũ) trong GameController prefab"), EditorStyles.toolbarButton))
            {
                int _count = PackManagerController.CleanOldControllers();
                ShowNotification(new GUIContent($"Đã xoá {_count} component"));
            }
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Danh sách

        private void _DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            _scrollList = EditorGUILayout.BeginScrollView(_scrollList);
            var _segment = _segmentController.segments.Count > _indexSegment ? _segmentController.segments[_indexSegment] : null;
            for (int i = 0; i < _database.packs.Count; i++)
            {
                var _pack = _database.packs[i];
                if (_pack == null)
                    continue;
                if (!string.IsNullOrEmpty(_filter) && _pack.typePackIAP.ToString().IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var _schedule = _segment?.saleData?.GetPackScheduleInList(_pack.typePackIAP);
                string _status = _schedule == null ? "chưa có lịch" : (_schedule.isActive ? _GetSummary(_schedule) : "TẮT");
                var _style = new GUIStyle(EditorStyles.helpBox) { richText = true };
                GUI.backgroundColor = i == _indexPackSelected ? new Color(0.6f, 0.8f, 1f) : Color.white;
                if (GUILayout.Button($"<b>{_pack.typePackIAP}</b>  (ưu tiên {_pack.prioritySale})\n<size=10>{_status}</size>", _style, GUILayout.Height(36)))
                {
                    _indexPackSelected = i;
                    GUI.FocusControl(null);
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();

            _DrawCreate();
            EditorGUILayout.EndVertical();
        }

        private static string _GetSummary(PackScheduleConfig schedule)
        {
            string _duration;
            switch (schedule.durationType)
            {
                case PackDurationType.Duration: _duration = $"hiện {_FormatSeconds(schedule.durationSeconds)}"; break;
                case PackDurationType.UntilEndOfDay: _duration = "hết ngày"; break;
                case PackDurationType.UntilEndOfWeek: _duration = "hết tuần"; break;
                case PackDurationType.UntilEndOfActiveDays: _duration = "hết các ngày chọn"; break;
                default: _duration = "không mất"; break;
            }
            string _interval = schedule.intervalReappearSeconds > 0 ? $", lại sau {_FormatSeconds(schedule.intervalReappearSeconds)}" : "";
            return $"lv {schedule.levelUnlock}, {_duration}{_interval}{(schedule.isAllWeek ? "" : ", theo ngày")}";
        }

        private static string _FormatSeconds(long seconds)
        {
            if (seconds >= 86400 && seconds % 86400 == 0) return $"{seconds / 86400} ngày";
            if (seconds >= 3600 && seconds % 3600 == 0) return $"{seconds / 3600} giờ";
            if (seconds >= 60 && seconds % 60 == 0) return $"{seconds / 60} phút";
            return $"{seconds} giây";
        }

        #endregion

        #region Chi tiết

        private void _DrawDetail()
        {
            EditorGUILayout.BeginVertical();
            _scrollDetail = EditorGUILayout.BeginScrollView(_scrollDetail);
            if (_indexPackSelected < 0 || _indexPackSelected >= _database.packs.Count)
            {
                EditorGUILayout.HelpBox("Chọn 1 gói bên trái để sửa.\n- Thông số gói: SalePackDatabase.\n- Lịch xuất hiện (active, level, thời gian hiện, interval): segment đang chọn trên thanh công cụ.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            var _pack = _database.packs[_indexPackSelected];

            // Lịch (segment)
            EditorGUILayout.LabelField($"Lịch xuất hiện - segment {_indexSegment}", EditorStyles.boldLabel);
            _serializedSegment.Update();
            var _propertySchedules = _serializedSegment.FindProperty($"segments.Array.data[{_indexSegment}].saleData.packSchedules");
            int _indexSchedule = -1;
            if (_propertySchedules != null)
            {
                for (int i = 0; i < _propertySchedules.arraySize; i++)
                {
                    if (_propertySchedules.GetArrayElementAtIndex(i).FindPropertyRelative("typePack").intValue == (int)_pack.typePackIAP)
                    {
                        _indexSchedule = i;
                        break;
                    }
                }
            }
            if (_indexSchedule >= 0)
            {
                EditorGUI.BeginChangeCheck();
                var _propertySchedule = _propertySchedules.GetArrayElementAtIndex(_indexSchedule);
                foreach (var _child in _GetChildren(_propertySchedule))
                    if (_child.name != "typePack")
                        EditorGUILayout.PropertyField(_child, true);
                if (EditorGUI.EndChangeCheck())
                    _ApplySegment();
                if (GUILayout.Button("Xoá lịch ở mọi segment", GUILayout.Width(180))
                    && EditorUtility.DisplayDialog("Pack Manager", $"Xoá lịch của {_pack.typePackIAP} ở mọi segment?", "Xoá", "Huỷ"))
                {
                    PackManagerController.RemoveSchedule(_pack.typePackIAP);
                    _Reload();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Gói chưa có lịch nên sẽ không xuất hiện.", MessageType.Warning);
                if (GUILayout.Button("+ Thêm lịch mặc định vào mọi segment"))
                {
                    PackManagerController.EnsureSchedule(_pack.typePackIAP, () => new PackScheduleConfig());
                    _Reload();
                }
            }

            EditorGUILayout.Space(10);

            // Thông số (database)
            EditorGUILayout.LabelField("Thông số gói - SalePackDatabase", EditorStyles.boldLabel);
            _serializedDatabase.Update();
            var _propertyPack = _serializedDatabase.FindProperty("packs").GetArrayElementAtIndex(_indexPackSelected);
            EditorGUI.BeginChangeCheck();
            foreach (var _child in _GetChildren(_propertyPack))
                EditorGUILayout.PropertyField(_child, true);
            if (EditorGUI.EndChangeCheck())
            {
                _serializedDatabase.ApplyModifiedProperties();
                EditorUtility.SetDirty(_database);
                AssetDatabase.SaveAssetIfDirty(_database);
            }

            EditorGUILayout.Space(6);
            var _iapPack = PackManagerController.GetIapDatabase()?.GetPackAll(_pack.typePackIAP);
            EditorGUILayout.HelpBox(_iapPack != null
                ? $"Sản phẩm (quà, giá, shortID '{_iapPack.shortID}') ở IAPDatabase."
                : "IAPDatabase chưa có sản phẩm cho gói này.", _iapPack != null ? MessageType.None : MessageType.Warning);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void _ApplySegment()
        {
            _serializedSegment.ApplyModifiedProperties();
            EditorUtility.SetDirty(_segmentController);
            PrefabUtility.SavePrefabAsset(_segmentController.transform.root.gameObject);
        }

        private static System.Collections.Generic.IEnumerable<SerializedProperty> _GetChildren(SerializedProperty property)
        {
            var _iterator = property.Copy();
            var _end = property.GetEndProperty();
            if (!_iterator.NextVisible(true))
                yield break;
            while (!SerializedProperty.EqualContents(_iterator, _end))
            {
                yield return _iterator.Copy();
                if (!_iterator.NextVisible(false))
                    yield break;
            }
        }

        #endregion

        #region Tạo gói

        private void _DrawCreate()
        {
            _isFoldoutCreate = EditorGUILayout.Foldout(_isFoldoutCreate, "Tạo gói mới", true, EditorStyles.foldoutHeader);
            if (!_isFoldoutCreate)
                return;

            EditorGUI.BeginChangeCheck();
            _settingCreate.isNewEnum = EditorGUILayout.ToggleLeft("Thêm TypePackIAP mới", _settingCreate.isNewEnum);
            if (_settingCreate.isNewEnum)
                _settingCreate.nameEnum = EditorGUILayout.TextField("Tên", _settingCreate.nameEnum);
            else
                _settingCreate.typeExisting = (TypePackIAP)EditorGUILayout.EnumPopup("TypePackIAP", _settingCreate.typeExisting);
            _settingCreate.shortId = EditorGUILayout.TextField(new GUIContent("shortID", "Product id = package_name + '.' + shortID"), _settingCreate.shortId);
            _settingCreate.popupResourcePath = EditorGUILayout.TextField("Popup (Resources)", _settingCreate.popupResourcePath);
            _settingCreate.prioritySale = EditorGUILayout.IntField("Ưu tiên", _settingCreate.prioritySale);
            _settingCreate.levelUnlock = EditorGUILayout.IntField("Level xuất hiện", _settingCreate.levelUnlock);
            _settingCreate.durationType = (PackDurationType)EditorGUILayout.EnumPopup("Thời gian hiện", _settingCreate.durationType);
            if (_settingCreate.durationType == PackDurationType.Duration)
                _settingCreate.durationSeconds = EditorGUILayout.LongField("Số giây hiện", _settingCreate.durationSeconds);
            _settingCreate.intervalReappearSeconds = EditorGUILayout.LongField(new GUIContent("Xuất hiện lại sau (s)", "0 = ngay khi đủ điều kiện"), _settingCreate.intervalReappearSeconds);
            if (EditorGUI.EndChangeCheck())
                _errorCreate = PackManagerController.Validate(_settingCreate);

            if (!string.IsNullOrEmpty(_errorCreate))
                EditorGUILayout.HelpBox(_errorCreate, MessageType.Warning);
            GUI.enabled = string.IsNullOrEmpty(_errorCreate);
            if (GUILayout.Button("Tạo gói", GUILayout.Height(26)))
            {
                EditorPrefs.SetString(_KEY_PREFS_CREATE, JsonUtility.ToJson(_settingCreate));
                var _copy = JsonUtility.FromJson<PackCreateSetting>(JsonUtility.ToJson(_settingCreate));
                EditorApplication.delayCall += () =>
                {
                    PackManagerController.Create(_copy);
                    _Reload();
                };
            }
            GUI.enabled = true;
        }

        #endregion
    }
}
