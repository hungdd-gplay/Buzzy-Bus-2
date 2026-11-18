using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Hàng đợi popup gợi ý khi vào Home (sale pack, rate, quà challenge, chest...).
/// Luồng:
///   1. Các controller gọi RegisterSuggetPopupShow() (báo trước sẽ có 1 popup) rồi AddActionShow(...) khi dữ liệu sẵn sàng.
///   2. HomeController gọi CheckStartShowSugget(): đợi đủ số đã đăng ký (tối đa _TIMEOUT_WAIT_REGISTER giây),
///      sắp theo độ ưu tiên rồi mở lần lượt.
///   3. Popup tiếp theo chỉ mở khi BoxController không còn box nào (kể cả popup phần thưởng mở lồng bên trong)
///      và không có WaitingBox (isLockEscape). Action không mở popup nào thì tự chuyển sang cái tiếp theo.
/// SuperStarLegendaryShow: hàng đợi riêng do chính tính năng tự điều khiển (SuggetSuperStarLegendaryShow / Next...).
/// </summary>
public class SuggetPopupHomeController : MonoBehaviour
{
    private const float _DELAY_BEFORE_SHOW = 0.5f;
    private const float _DELAY_BETWEEN_POPUP = 0.2f;
    private const float _TIMEOUT_WAIT_REGISTER = 3f;
    private const float _TIMEOUT_WAIT_SUPER_STAR_NOT_STARTED = 5f;
    private const float _TIMEOUT_ONLY_LOCK_ESCAPE = 10f;

    [SerializeField] protected List<SuggetPopupData> actionsSuggetShowPopup;
    [SerializeField] protected List<SuggetPopupData> actionsSuggetSuperStarLegendaryShow;
    [SerializeField] private GameObject panelClearClick;

    public bool IsHasPopupShow => _numRegisterPending > 0 || (actionsSuggetShowPopup != null && actionsSuggetShowPopup.Count > 0);

    public enum PriovitySugget
    {
        Normal = 0,
        Important = 1,      // Popup Sale,...
        HighQuality = 2,    // Tính năng mới unlock,...
        Special = 3,        // Open chest, reward,...
        SupperShow = 4,     // Challenge
        EpicShow = 5,       // Rate Popup

        SuperStarLegendaryShow = 100, // Hàng đợi riêng, tính năng tự gọi SuggetSuperStarLegendaryShow()
    }

    public class SuggetPopupData : SelfDefineNoneMono
    {
        public UnityAction actionShow;
        public PriovitySugget priovity;
    }

    public bool IsHasSuperStarLegendaryShow;

    /// <summary>Số popup đã Register nhưng chưa AddActionShow.</summary>
    private int _numRegisterPending;
    private bool _isStarted;
    private bool _isRunning;
    private bool _isSuperStarStarted;
    private Coroutine _coroutineShow;
    /// <summary>Số box đã có trong stack trước khi hàng đợi chạy (panel luôn mở ở Home...).</summary>
    private int _countBoxBase;

    public void Init()
    {
        actionsSuggetShowPopup = new List<SuggetPopupData>();
        actionsSuggetSuperStarLegendaryShow = new List<SuggetPopupData>();
        _numRegisterPending = 0;
        _isStarted = false;
        _isRunning = false;
        _isSuperStarStarted = false;
        IsHasSuperStarLegendaryShow = false;
        if (_coroutineShow != null)
        {
            StopCoroutine(_coroutineShow);
            _coroutineShow = null;
        }
        _SetBlockClick(false);
    }

    #region Đăng ký

    /// <summary>Báo trước sẽ có 1 popup gợi ý. isHasSuperStarLegendaryShow = true nếu popup thuộc hàng SuperStar.</summary>
    public void RegisterSuggetPopupShow(bool isHasSuperStarLegendaryShow = false)
    {
        if (!isHasSuperStarLegendaryShow)
            _numRegisterPending++;
        _SetBlockClick(true);
    }

    public void AddActionShow(SuggetPopupData act)
    {
        if (act == null)
            return;

        if (act.priovity == PriovitySugget.SuperStarLegendaryShow)
        {
            if (actionsSuggetSuperStarLegendaryShow == null)
                actionsSuggetSuperStarLegendaryShow = new List<SuggetPopupData>();
            actionsSuggetSuperStarLegendaryShow.Add(act);
            IsHasSuperStarLegendaryShow = true;
            _LockScrollPage(true);
            return;
        }

        if (actionsSuggetShowPopup == null)
            actionsSuggetShowPopup = new List<SuggetPopupData>();
        actionsSuggetShowPopup.Add(act);
        if (_numRegisterPending > 0)
            _numRegisterPending--;

        // Thêm muộn sau khi hàng đợi đã chạy xong -> chạy lại
        if (_isStarted && !_isRunning)
            _coroutineShow = StartCoroutine(_ShowQueue(false));
    }

    #endregion

    #region Hàng đợi thường

    public void CheckStartShowSugget()
    {
        if (_isStarted)
            return;
        _isStarted = true;
        _coroutineShow = StartCoroutine(_ShowQueue(true));
    }

    /// <summary>Giữ cho code cũ (LongTH/Tool.cs): bắt đầu hiện ngay, không đợi đăng ký.</summary>
    public IEnumerator StartShowPopup()
    {
        _isStarted = true;
        _numRegisterPending = 0;
        yield return _ShowQueue(false);
    }

    private IEnumerator _ShowQueue(bool isWaitRegister)
    {
        _isRunning = true;
        _SetBlockClick(true);

        if (isWaitRegister)
            yield return _WaitRegisterDone();
        yield return new WaitForSeconds(_DELAY_BEFORE_SHOW);
        _countBoxBase = BoxController.Instance != null ? BoxController.Instance.CountBox : 0;

        while (actionsSuggetShowPopup != null && actionsSuggetShowPopup.Count > 0)
        {
            yield return _WaitNoBoxShowing();

            // Ưu tiên cao hiện trước (sắp lại vì có thể có action thêm muộn)
            actionsSuggetShowPopup.Sort((a, b) => ((int)b.priovity).CompareTo((int)a.priovity));
            var _data = actionsSuggetShowPopup[0];
            actionsSuggetShowPopup.RemoveAt(0);
            try
            {
                _data.actionShow?.Invoke();
            }
            catch (System.Exception _exception)
            {
                Debug.LogException(_exception);
            }

            // Đợi popup kịp OnEnable / load async rồi mới kiểm tra
            yield return null;
            yield return null;
            yield return _WaitNoBoxShowing();
            yield return new WaitForSeconds(_DELAY_BETWEEN_POPUP);
        }

        _isRunning = false;
        _coroutineShow = null;
        if (_numRegisterPending <= 0 && !IsHasSuperStarLegendaryShow)
            _SetBlockClick(false);
    }

    private IEnumerator _WaitRegisterDone()
    {
        float _timeStart = Time.unscaledTime;
        while (_numRegisterPending > 0 && Time.unscaledTime - _timeStart < _TIMEOUT_WAIT_REGISTER)
            yield return null;
        if (_numRegisterPending > 0)
        {
            Debug.LogWarning($"[SuggetPopup] Còn {_numRegisterPending} popup đã Register nhưng chưa AddActionShow sau {_TIMEOUT_WAIT_REGISTER}s -> bỏ qua để không khoá Home.");
            _numRegisterPending = 0;
        }

        // Hàng SuperStar: đợi nó chạy xong. Nếu không ai bắt đầu nó thì không đợi mãi.
        _timeStart = Time.unscaledTime;
        while (IsHasSuperStarLegendaryShow)
        {
            if (!_isSuperStarStarted && Time.unscaledTime - _timeStart > _TIMEOUT_WAIT_SUPER_STAR_NOT_STARTED)
            {
                Debug.LogWarning("[SuggetPopup] Hàng SuperStar có dữ liệu nhưng không được gọi SuggetSuperStarLegendaryShow() -> bỏ qua.");
                _EndSuperStar();
                break;
            }
            yield return null;
        }
    }

    /// <summary>Đợi đến khi các box mở thêm (kể cả popup lồng bên trong) đều đóng và không có WaitingBox.</summary>
    private IEnumerator _WaitNoBoxShowing()
    {
        float _timeOnlyLock = 0f;
        while (_IsBoxShowing())
        {
            // Chỉ còn isLockEscape mà không có box nào: tránh kẹt vĩnh viễn nếu ai đó quên mở khoá
            bool _isOnlyLock = BoxController.Instance.CountBox <= _countBoxBase;
            _timeOnlyLock = _isOnlyLock ? _timeOnlyLock + Time.unscaledDeltaTime : 0f;
            if (_timeOnlyLock > _TIMEOUT_ONLY_LOCK_ESCAPE)
                yield break;
            yield return null;
        }
    }

    private bool _IsBoxShowing()
    {
        var _boxController = BoxController.Instance;
        return _boxController != null && (_boxController.CountBox > _countBoxBase || _boxController.isLockEscape);
    }

    #endregion

    #region Hàng SuperStar

    public void SuggetSuperStarLegendaryShow()
    {
        _isSuperStarStarted = true;
        _ShowNextSuperStar();
    }

    public void NextSuggetSuperStarLegendaryShow()
    {
        _ShowNextSuperStar();
    }

    private void _ShowNextSuperStar()
    {
        if (actionsSuggetSuperStarLegendaryShow != null && actionsSuggetSuperStarLegendaryShow.Count > 0)
        {
            var _data = actionsSuggetSuperStarLegendaryShow[0];
            actionsSuggetSuperStarLegendaryShow.RemoveAt(0);
            _data.actionShow?.Invoke();
            return;
        }
        _EndSuperStar();
    }

    private void _EndSuperStar()
    {
        actionsSuggetSuperStarLegendaryShow?.Clear();
        IsHasSuperStarLegendaryShow = false;
        _isSuperStarStarted = false;
        _LockScrollPage(false);
        if (!_isRunning && _numRegisterPending <= 0 && (actionsSuggetShowPopup == null || actionsSuggetShowPopup.Count == 0))
            _SetBlockClick(false);
    }

    #endregion

    #region Helper

    private void _SetBlockClick(bool isBlock)
    {
        if (panelClearClick != null)
            panelClearClick.SetActive(isBlock);
    }

    private static void _LockScrollPage(bool isLock)
    {
        if (HomeController.Instance != null && HomeController.Instance.pageController != null)
            HomeController.Instance.pageController.LockScrollPage(isLock);
    }

    #endregion
}
