using System;
using System.Collections.Generic;
using UnityEngine;
using static SaleSegemntData;

/// <summary>
/// Quản lý tất cả gói IAP:
/// - Mỗi gói trong SalePackDatabase có 1 PackBaseController (tự tạo; gói đặc biệt dùng component con kế thừa).
/// - Mỗi giây tính lại gói nào đang xuất hiện (lịch segment + điều kiện database), báo Home / Shop khi thay đổi.
/// - Gợi ý popup ở Home, xử lý mua thành công, reset ngày mới.
/// </summary>
public class PackController : MonoBehaviour
{
    private const float _INTERVAL_REFRESH = 1f;
    private const string _KEY_GROUP_CHOSEN = "PACK_GROUP_CHOSEN_";

    [Tooltip("Tự điền khi Init. Gói có logic riêng: đặt component kế thừa PackBaseController làm con của object này.")]
    public List<PackBaseController> allPacksInGame = new List<PackBaseController>();

    [HideInInspector] public List<PackBaseController> activeSalePacks = new List<PackBaseController>();
    [HideInInspector] public SaleSegemntData saleData;

    public float PackTimer;
    private float _timerRefresh;
    private bool _isInited;
    private readonly HashSet<TypePackIAP> _setActiveLast = new HashSet<TypePackIAP>();

    #region Init

    public void Init()
    {
        saleData = GameController.Instance.segmentController.GetCurrentSegment().saleData;
        var _database = GameController.Instance.dataContain.salePackDatabase;

        // Controller tuỳ chỉnh đặt sẵn trong prefab
        var _dicCustom = new Dictionary<TypePackIAP, PackBaseController>();
        foreach (var _custom in GetComponentsInChildren<PackBaseController>(true))
            if (_custom != null && !_dicCustom.ContainsKey(_custom.typePack))
                _dicCustom.Add(_custom.typePack, _custom);

        allPacksInGame = new List<PackBaseController>();
        foreach (var _pack in _database.packs)
        {
            if (_pack == null)
                continue;
            if (!_dicCustom.TryGetValue(_pack.typePackIAP, out var _controller))
            {
                var _go = new GameObject("Pack_" + _pack.typePackIAP);
                _go.transform.SetParent(transform, false);
                _controller = _go.AddComponent<PackBaseController>();
            }
            _controller.Init(this, _pack);
            allPacksInGame.Add(_controller);
        }

        // Ưu tiên cao xét trước (quan trọng cho nhóm exclusive và thứ tự hiển thị)
        allPacksInGame.Sort((a, b) => b.packModel.prioritySale.CompareTo(a.packModel.prioritySale));
        activeSalePacks = new List<PackBaseController>();
        _isInited = true;
    }

    public PackScheduleConfig GetSchedule(TypePackIAP typePack)
    {
        return saleData != null ? saleData.GetPackSchedule(typePack) : null;
    }

    #endregion

    #region Trạng thái

    public void InitState()
    {
        CheckSalePack();
    }

    /// <summary>Tính lại gói đang xuất hiện và báo Home / Shop.</summary>
    public void CheckSalePack()
    {
        _RefreshActivePacks();
        _ReloadSale();
    }

    /// <summary>Trả về true nếu danh sách gói đang xuất hiện thay đổi.</summary>
    private bool _RefreshActivePacks()
    {
        var _now = UnbiasedTime.Instance.Now();
        activeSalePacks.Clear();
        foreach (var _pack in allPacksInGame)
        {
            _pack.RefreshState(_now);
            if (_pack.IsActiveNow)
                activeSalePacks.Add(_pack);
        }

        bool _isChanged = activeSalePacks.Count != _setActiveLast.Count;
        if (!_isChanged)
        {
            foreach (var _pack in activeSalePacks)
                if (!_setActiveLast.Contains(_pack.typePack))
                {
                    _isChanged = true;
                    break;
                }
        }
        _setActiveLast.Clear();
        foreach (var _pack in activeSalePacks)
            _setActiveLast.Add(_pack.typePack);
        return _isChanged;
    }

    private void _ReloadSale()
    {
        EventDispatcher.EventDispatcher.Instance.PostEvent(EventID.RELOAD_SALE_HOME);
        EventDispatcher.EventDispatcher.Instance.PostEvent(EventID.RELOAD_SALE_SHOP);
    }

    public bool HasPackSale()
    {
        return activeSalePacks != null && activeSalePacks.Count > 0;
    }

    public bool IsPackActive(TypePackIAP typePack)
    {
        return activeSalePacks.Exists(p => p.typePack == typePack);
    }

    public PackBaseController GetPack(TypePackIAP typePack)
    {
        return allPacksInGame.Find(p => p.typePack == typePack);
    }

    private void Update()
    {
        PackTimer += Time.unscaledDeltaTime;
        if (!_isInited)
            return;
        _timerRefresh += Time.unscaledDeltaTime;
        if (_timerRefresh < _INTERVAL_REFRESH)
            return;
        _timerRefresh = 0f;
        // Gói hết giờ / tới giờ xuất hiện -> báo Home / Shop cập nhật
        if (_RefreshActivePacks())
            _ReloadSale();
    }

    #endregion

    #region Nhóm exclusive

    /// <summary>Trong nhóm exclusive, gói này có phải gói được chọn cho người chơi không (gói đầu tiên đủ điều kiện sẽ giữ chỗ).</summary>
    public bool IsChosenInGroup(PackBaseController pack)
    {
        string _group = pack.packModel.groupExclusive;
        var _database = GameController.Instance.dataContain.salePackDatabase;
        foreach (var _other in _database.GetPacksInGroup(_group))
            if (_other.typePackIAP != pack.typePack && _other.boughtTimes > 0)
                return false; // Đã mua 1 gói trong nhóm -> các gói khác ẩn

        string _key = _KEY_GROUP_CHOSEN + _group;
        if (PlayerPrefs.HasKey(_key))
        {
            var _chosen = (TypePackIAP)PlayerPrefs.GetInt(_key);
            if (_database.IsHasPack(_chosen))
                return _chosen == pack.typePack;
        }
        GPlayerPrefs.SetInt(_key, (int)pack.typePack);
        GPlayerPrefs.Save();
        return true;
    }

    #endregion

    #region Mua / popup

    public void LoadPack(TypePackIAP typePackBought)
    {
        foreach (var _pack in allPacksInGame)
            _pack.LoadPack(typePackBought);
    }

    public void OnBuyIAPSuccess(TypePackIAP typePack)
    {
        LoadPack(typePack);
        CheckSalePack();
    }

    /// <summary>Đăng ký popup gợi ý cho các gói đủ điều kiện vào hàng đợi Home.</summary>
    public void CheckShowPopupSaleHome()
    {
        if (!HasPackSale() || HomeController.Instance == null)
            return;

        foreach (var _pack in new List<PackBaseController>(activeSalePacks))
        {
            if (!_pack.IsCanShowSugget())
                continue;
            var _packShow = _pack;
            HomeController.Instance.suggetPopupHomeController.RegisterSuggetPopupShow();
            HomeController.Instance.suggetPopupHomeController.AddActionShow(new SuggetPopupHomeController.SuggetPopupData
            {
                actionShow = () =>
                {
                    _packShow.OnShowPopupSuggetHome();
                    _packShow.ShowPopup(true);
                },
                priovity = SuggetPopupHomeController.PriovitySugget.Important
            });
        }
        PackTimer = 0;
    }

    public void ResetDataNewDay()
    {
        UseProfile.LevelPlayInDay = 0;
        UseProfile.NumberOfAdsInDay = 0;
        foreach (var _pack in allPacksInGame)
            _pack.ResetDataNewDay();
        if (_isInited)
            CheckSalePack();
    }

    #endregion
}
