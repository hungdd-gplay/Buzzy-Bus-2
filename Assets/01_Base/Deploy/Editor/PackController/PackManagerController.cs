using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Purchasing;
using static SaleSegemntData;

namespace BaseProject.EditorTools.Pack
{
    [Serializable]
    public class PackCreateSetting
    {
        public bool isNewEnum = true;
        public string nameEnum = "NewPack";
        public TypePackIAP typeExisting = TypePackIAP.None;
        public string shortId = "newpack";
        public string popupResourcePath = PathPrefabs.STARTER_PACK_BOX;
        public int prioritySale = 1;
        public PackDurationType durationType = PackDurationType.Duration;
        public long durationSeconds = 600;
        public long intervalReappearSeconds = 3600;
        public int levelUnlock = 5;
    }

    /// <summary>
    /// Controller editor của Pack Manager: đọc / ghi SalePackDatabase, IAPDatabase và lịch gói trong các segment
    /// (GameController prefab), tạo gói mới, áp preset cho gói có sẵn, dọn controller gói cũ.
    /// </summary>
    [InitializeOnLoad]
    public static class PackManagerController
    {
        public const string MARKER_ENUM = "[PACK_GENERATOR]";
        private const string _KEY_PENDING = "BaseProject.PackManager.Pending";
        private const string _PATH_GAME_CONTROLLER = ProjectPaths.GAME_CONTROLLER_PREFAB;

        static PackManagerController()
        {
            EditorApplication.delayCall += ProcessPending;
        }

        #region Asset

        public static SalePackDatabase GetSalePackDatabase() => _LoadFirst<SalePackDatabase>("t:SalePackDatabase");
        public static IAPDatabase GetIapDatabase() => _LoadFirst<IAPDatabase>("t:IAPDatabase");

        public static GameObject GetGameControllerPrefab(out string path)
        {
            foreach (var _path in new[] { _PATH_GAME_CONTROLLER }.Concat(AssetDatabase.FindAssets("GameController t:Prefab").Select(AssetDatabase.GUIDToAssetPath)))
            {
                var _go = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
                if (_go != null && _go.GetComponentInChildren<SegmentController>(true) != null)
                {
                    path = _path;
                    return _go;
                }
            }
            path = null;
            return null;
        }

        public static SegmentController GetSegmentController()
        {
            var _prefab = GetGameControllerPrefab(out _);
            return _prefab != null ? _prefab.GetComponentInChildren<SegmentController>(true) : null;
        }

        private static T _LoadFirst<T>(string filter) where T : UnityEngine.Object
        {
            var _guid = AssetDatabase.FindAssets(filter).FirstOrDefault();
            return string.IsNullOrEmpty(_guid) ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(_guid));
        }

        #endregion

        #region Lịch trong segment

        /// <summary>Thêm lịch mặc định cho gói vào mọi segment chưa có. Trả về số segment được thêm.</summary>
        public static int EnsureSchedule(TypePackIAP typePack, Func<PackScheduleConfig> createDefault)
        {
            var _segmentController = GetSegmentController();
            if (_segmentController == null || _segmentController.segments == null)
                return 0;
            int _count = 0;
            foreach (var _segment in _segmentController.segments)
            {
                if (_segment?.saleData == null)
                    continue;
                if (_segment.saleData.packSchedules == null)
                    _segment.saleData.packSchedules = new List<PackScheduleConfig>();
                if (_segment.saleData.packSchedules.Any(s => s != null && s.typePack == typePack))
                    continue;
                var _schedule = createDefault();
                _schedule.typePack = typePack;
                _segment.saleData.packSchedules.Add(_schedule);
                _count++;
            }
            if (_count > 0)
                _SaveSegmentController(_segmentController);
            return _count;
        }

        public static void RemoveSchedule(TypePackIAP typePack)
        {
            var _segmentController = GetSegmentController();
            if (_segmentController == null)
                return;
            foreach (var _segment in _segmentController.segments)
                _segment?.saleData?.packSchedules?.RemoveAll(s => s == null || s.typePack == typePack);
            _SaveSegmentController(_segmentController);
        }

        private static void _SaveSegmentController(SegmentController segmentController)
        {
            EditorUtility.SetDirty(segmentController);
            PrefabUtility.SavePrefabAsset(segmentController.transform.root.gameObject);
        }

        #endregion

        #region Preset cho gói có sẵn (chuyển từ controller cũ)

        /// <summary>
        /// Điền thông số + lịch mặc định theo logic của các controller cũ cho những gói có trong SalePackDatabase.
        /// isOverwrite = false: chỉ điền vào gói chưa có lịch.
        /// </summary>
        public static int ApplyPresets(bool isOverwrite)
        {
            var _database = GetSalePackDatabase();
            if (_database == null)
                return 0;
            Undo.RecordObject(_database, "Apply Pack Presets");
            int _count = 0;
            foreach (var _pack in _database.packs)
            {
                if (_pack == null)
                    continue;
                bool _isHasSchedule = GetSegmentController()?.segments?.FirstOrDefault()?.saleData?.GetPackScheduleInList(_pack.typePackIAP) != null;
                if (_isHasSchedule && !isOverwrite)
                    continue;
                if (isOverwrite)
                    RemoveSchedule(_pack.typePackIAP);
                var _schedule = _ApplyPreset(_pack);
                EnsureSchedule(_pack.typePackIAP, () => _schedule);
                _count++;
            }
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssetIfDirty(_database);
            return _count;
        }

        private static PackScheduleConfig _ApplyPreset(SalePackDataModel pack)
        {
            var _schedule = new PackScheduleConfig { typePack = pack.typePackIAP, isActive = true, intervalSuggestSeconds = 1800 };
            var _hideIfPaidSale = new List<TypePackIAP> { TypePackIAP.SaleItem, TypePackIAP.SaleCoin, TypePackIAP.WeeklySale };
            var _requireStarter = new List<PackRequireBought> { new PackRequireBought { typePack = TypePackIAP.StarterPack, minBought = 1 } };

            switch (pack.typePackIAP)
            {
                case TypePackIAP.BeginnerPack:
                    pack.hideIfBought = _hideIfPaidSale; pack.maxBuyTotal = 1;
                    break;
                case TypePackIAP.StarterPack:
                    pack.popupResourcePath = PathPrefabs.STARTER_PACK_BOX;
                    pack.hideIfBought = _hideIfPaidSale; pack.maxBuyTotal = 1;
                    break;
                case TypePackIAP.RemoveAds:
                    pack.clickAction = PackClickAction.BuyDirect; pack.isHideWhenRemoveAds = true; pack.maxBuyTotal = 1;
                    break;
                case TypePackIAP.BigRemoveAdsPack:
                    pack.popupResourcePath = PathPrefabs.BIG_REMOVE_ADS_BOX; pack.isHideWhenRemoveAds = true; pack.maxBuyTotal = 1;
                    pack.suggestMinAdsInPlay = 5; pack.suggestMinAdsInDay = 10;
                    break;
                case TypePackIAP.DailyOffer:
                    pack.popupResourcePath = PathPrefabs.DAILY_OFFER_BOX; pack.hideIfBought = _hideIfPaidSale;
                    pack.minLevelPlayInDay = 3; pack.maxBuyPerAppear = 1;
                    _schedule.durationType = PackDurationType.UntilEndOfDay;
                    break;
                case TypePackIAP.SaleItem:
                    pack.popupResourcePath = PathPrefabs.SALE_ITEM_BOX; pack.requireBought = _requireStarter; pack.maxBuyPerAppear = 1;
                    _schedule.durationType = PackDurationType.UntilEndOfDay;
                    break;
                case TypePackIAP.SaleCoin:
                    pack.popupResourcePath = PathPrefabs.SALE_COIN_BOX; pack.requireBought = _requireStarter; pack.maxBuyPerAppear = 1;
                    _schedule.durationType = PackDurationType.UntilEndOfDay;
                    break;
                case TypePackIAP.WeeklySale:
                    pack.popupResourcePath = PathPrefabs.WEEKLY_SALE_BOX; pack.requireBought = _requireStarter; pack.maxBuyPerAppear = 1;
                    _SetWeekend(_schedule);
                    break;
                case TypePackIAP.MasterOffer:
                    pack.popupResourcePath = PathPrefabs.MASTER_OFFER_BOX; pack.maxBuyPerAppear = 1;
                    pack.requireBought = new List<PackRequireBought> { new PackRequireBought { typePack = TypePackIAP.SaleItem, minBought = 1 } };
                    _schedule.durationType = PackDurationType.UntilEndOfDay;
                    break;
                case TypePackIAP.MegaOffer:
                    pack.popupResourcePath = PathPrefabs.MEGA_OFFER_BOX; pack.maxBuyPerAppear = 1;
                    pack.requireBought = new List<PackRequireBought> { new PackRequireBought { typePack = TypePackIAP.SaleCoin, minBought = 1 } };
                    _schedule.durationType = PackDurationType.UntilEndOfDay;
                    break;
                case TypePackIAP.GiantOffer:
                    pack.popupResourcePath = PathPrefabs.GIANT_OFFER_BOX; pack.maxBuyPerAppear = 1;
                    pack.requireBought = new List<PackRequireBought> { new PackRequireBought { typePack = TypePackIAP.WeeklySale, minBought = 1 } };
                    _SetWeekend(_schedule);
                    break;
                case TypePackIAP.ProBundle:
                case TypePackIAP.UtralBundle:
                    pack.maxBuyPerAppear = 0;
                    break;
                case TypePackIAP.BlackFriday_1:
                    pack.popupResourcePath = PathPrefabs.SMALL_BLACK_FRIDAY_PACK_BOX; pack.maxBuyTotal = 1;
                    pack.hideIfBought = new List<TypePackIAP>(_hideIfPaidSale) { TypePackIAP.StarterPack };
                    _schedule.isActive = false; // Gói mùa vụ: bật + đặt timeStart / timeEnd khi chạy
                    break;
                case TypePackIAP.BlackFriday_2:
                    pack.popupResourcePath = PathPrefabs.BIG_BLACK_FRIDAY_PACK_BOX; pack.requireBought = _requireStarter; pack.maxBuyPerAppear = 1;
                    _schedule.durationType = PackDurationType.UntilEndOfDay;
                    _schedule.isActive = false;
                    break;
                case TypePackIAP.NoelSale_1:
                    pack.popupResourcePath = PathPrefabs.SMALL_NOEL_PACK_BOX; pack.maxBuyTotal = 1;
                    pack.eventSale = EventSaleType.Noel; pack.userType = PackUserType.NonPayer; pack.groupExclusive = "NoelSale";
                    break;
                case TypePackIAP.NoelSale_2:
                    pack.popupResourcePath = PathPrefabs.BIG_NOEL_PACK_BOX; pack.maxBuyTotal = 1;
                    pack.eventSale = EventSaleType.Noel; pack.userType = PackUserType.Payer; pack.groupExclusive = "NoelSale";
                    break;
            }
            return _schedule;
        }

        private static void _SetWeekend(PackScheduleConfig schedule)
        {
            schedule.isAllWeek = false;
            schedule.saturday = true;
            schedule.sunday = true;
            schedule.durationType = PackDurationType.UntilEndOfActiveDays;
        }

        #endregion

        #region Tạo gói mới

        public static string Validate(PackCreateSetting setting)
        {
            var _database = GetSalePackDatabase();
            if (_database == null)
                return "Không tìm thấy SalePackDatabase.";
            if (setting.isNewEnum)
            {
                if (string.IsNullOrEmpty(setting.nameEnum) || !Regex.IsMatch(setting.nameEnum, @"^[A-Z][A-Za-z0-9_]*$"))
                    return "Tên gói phải dạng PascalCase. Ví dụ: FlashSale10Min.";
                if (Enum.GetNames(typeof(TypePackIAP)).Contains(setting.nameEnum))
                    return $"TypePackIAP.{setting.nameEnum} đã tồn tại, hãy chọn 'Dùng TypePackIAP có sẵn'.";
            }
            else
            {
                if (setting.typeExisting == TypePackIAP.None)
                    return "Chọn TypePackIAP.";
                if (_database.IsHasPack(setting.typeExisting))
                    return $"{setting.typeExisting} đã có trong SalePackDatabase.";
            }
            if (string.IsNullOrEmpty(setting.shortId))
                return "Nhập shortID sản phẩm (product id = package_name + '.' + shortID).";
            return string.Empty;
        }

        public static void Create(PackCreateSetting setting)
        {
            string _error = Validate(setting);
            if (!string.IsNullOrEmpty(_error))
            {
                EditorUtility.DisplayDialog("Tạo gói", _error, "OK");
                return;
            }

            SessionState.SetString(_KEY_PENDING, JsonUtility.ToJson(setting));
            if (!setting.isNewEnum)
            {
                ProcessPending();
                return;
            }

            string _pathEnum = _GetPathScript(typeof(IAPDatabase));
            string _text = File.ReadAllText(_pathEnum);
            int _indexMarker = _text.IndexOf(MARKER_ENUM, StringComparison.Ordinal);
            if (_indexMarker < 0)
            {
                SessionState.EraseString(_KEY_PENDING);
                EditorUtility.DisplayDialog("Tạo gói", $"Không thấy dòng '// {MARKER_ENUM}' trong enum TypePackIAP.", "OK");
                return;
            }
            int _value = Enum.GetValues(typeof(TypePackIAP)).Cast<int>().Max() + 1;
            foreach (Match _match in Regex.Matches(_text.Substring(0, _indexMarker), @"=\s*(\d+)\s*,?"))
                _value = Math.Max(_value, int.Parse(_match.Groups[1].Value) + 1);
            string _newLine = _text.Contains("\r\n") ? "\r\n" : "\n";
            int _indexLine = _text.LastIndexOf('\n', _indexMarker) + 1;
            _text = _text.Insert(_indexLine, $"    {setting.nameEnum} = {_value},{_newLine}");
            File.WriteAllText(_pathEnum, _text, new UTF8Encoding(true));
            AssetDatabase.ImportAsset(_pathEnum, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[PackManager] Đã thêm TypePackIAP.{setting.nameEnum} = {_value}. Chờ biên dịch...");
        }

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
            var _setting = JsonUtility.FromJson<PackCreateSetting>(_json);
            TypePackIAP _type = _setting.typeExisting;
            if (_setting.isNewEnum && !Enum.TryParse(_setting.nameEnum, out _type))
            {
                Debug.LogWarning($"[PackManager] Chưa thấy TypePackIAP.{_setting.nameEnum} (đang lỗi biên dịch?). Sửa lỗi xong sẽ tự chạy tiếp.");
                return;
            }
            SessionState.EraseString(_KEY_PENDING);

            // 1) SalePackDatabase
            var _database = GetSalePackDatabase();
            Undo.RecordObject(_database, "Create Pack");
            _database.packs.Add(new SalePackDataModel
            {
                typePackIAP = _type,
                typeSale = TypeSale.FlashSale,
                prioritySale = _setting.prioritySale,
                isCanShowPopup = true,
                isShowShop = true,
                popupResourcePath = _setting.popupResourcePath,
                scaleIcon = Vector3.one,
                maxBuyPerAppear = 1
            });
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssetIfDirty(_database);

            // 2) IAPDatabase (sản phẩm: id, quà)
            var _iapDatabase = GetIapDatabase();
            if (_iapDatabase != null && _iapDatabase.GetPackAll(_type) == null)
            {
                if (_iapDatabase.lstPacksInapp == null)
                    _iapDatabase.lstPacksInapp = new List<IAPPack>();
                _iapDatabase.lstPacksInapp.Add(new IAPPack
                {
                    namePack = _type.ToString(),
                    type = _type,
                    productType = ProductType.Consumable,
                    typeBuy = TypeBuy.Inapp,
                    shortID = _setting.shortId,
                    itemsResult = new Dictionary<GiftType, RewardRandom>()
                });
                EditorUtility.SetDirty(_iapDatabase);
                AssetDatabase.SaveAssetIfDirty(_iapDatabase);
            }

            // 3) Lịch trong mọi segment
            int _countSegment = EnsureSchedule(_type, () => new PackScheduleConfig
            {
                isActive = true,
                levelUnlock = _setting.levelUnlock,
                durationType = _setting.durationType,
                durationSeconds = _setting.durationSeconds,
                intervalReappearSeconds = _setting.intervalReappearSeconds
            });
            Debug.Log($"[PackManager] Đã tạo gói {_type}: SalePackDatabase + IAPDatabase (shortID '{_setting.shortId}', nhớ điền quà) + lịch ở {_countSegment} segment.");
        }

        #endregion

        #region Dọn controller cũ

        /// <summary>Xoá component bị mất script (controller gói cũ) dưới PackController trong GameController prefab.</summary>
        public static int CleanOldControllers()
        {
            var _prefab = GetGameControllerPrefab(out string _path);
            if (_prefab == null)
                return 0;
            var _root = PrefabUtility.LoadPrefabContents(_path);
            int _count = 0;
            try
            {
                var _packController = _root.GetComponentInChildren<PackController>(true);
                if (_packController == null)
                    return 0;
                foreach (var _transform in _packController.GetComponentsInChildren<Transform>(true).Reverse().ToList())
                {
                    _count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(_transform.gameObject);
                    if (_transform != _packController.transform && _transform.childCount == 0 && _transform.GetComponents<Component>().Length == 1)
                        UnityEngine.Object.DestroyImmediate(_transform.gameObject);
                }
                _packController.allPacksInGame?.RemoveAll(p => p == null);
                PrefabUtility.SaveAsPrefabAsset(_root, _path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(_root);
            }
            return _count;
        }

        #endregion

        private static string _GetPathScript(Type type)
        {
            foreach (var _guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                var _script = AssetDatabase.LoadAssetAtPath<MonoScript>(_path);
                if (_script != null && _script.GetClass() == type)
                    return _path;
            }
            return null;
        }
    }

    internal static class SaleSegemntDataEditorExtension
    {
        /// <summary>Tìm lịch trong danh sách (không cần InitSegemnt).</summary>
        public static PackScheduleConfig GetPackScheduleInList(this SaleSegemntData saleData, TypePackIAP typePack)
        {
            return saleData?.packSchedules?.FirstOrDefault(s => s != null && s.typePack == typePack);
        }
    }
}
