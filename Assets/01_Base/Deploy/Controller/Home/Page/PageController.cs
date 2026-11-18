using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
#if FEATURE_ANALYTICS_FALCON
using Falcon.FalconAnalytics.Scripts.Models.Messages.PreDefines;
#endif

public enum PageName
{
    Shop = 0,
    Home = 1,
    Ranking = 2,
    Team = 3,
    HomeDecor = 4,
    // [PAGE_GENERATOR] Page tạo bằng Tools > Base Project > Game Flow được thêm phía trên dòng này
}

/// <summary>
/// Quản lý các page ở Home (vuốt ngang bằng HorizontalScrollSnap + nút tab ChoicePageUI).
/// Page bị tắt trong GameFlowConfig sẽ bị gỡ khỏi scroll (không để lại khoảng trống) và ẩn nút tab.
/// Thứ tự page = thứ tự object con trong Content của scroll.
/// </summary>
public class PageController : MonoBehaviour
{
    [SerializeField] private PageBase[] pages;

    [SerializeField] private HorizontalScrollSnap horizontalScrollSnap;
    [SerializeField] private ScrollRect scrollPage;
    [Tooltip("Thanh nút tab, tự ẩn khi chỉ còn 1 page (có thể để trống).")]
    [SerializeField] private GameObject tabBar;

    /// <summary>Các page đang bật, theo đúng thứ tự trong scroll.</summary>
    private readonly List<PageBase> _lsPageActive = new List<PageBase>();
    private Transform _holderPageDisable;
    private bool _isLockByCode;

    public PageName CurrentPage { get; private set; }
    public IReadOnlyList<PageBase> PagesActive => _lsPageActive;

    #region Init

    public void Init()
    {
        _RemoveDisabledPages();

        foreach (var _page in _lsPageActive)
            _page.Init(this);

        bool _isMultiPage = _lsPageActive.Count > 1;
        if (tabBar != null)
            tabBar.SetActive(_isMultiPage);
        _ApplyLockScroll();

        var _config = GameFlowConfig.Instance;
        var _pageStart = IsPageActive(_config.defaultPage) ? _config.defaultPage
            : (IsPageActive(PageName.Home) ? PageName.Home : (_lsPageActive.Count > 0 ? _lsPageActive[0].idPage : PageName.Home));
        int _indexStart = _GetIndexInScroll(_pageStart);
        if (_indexStart >= 0 && horizontalScrollSnap != null)
        {
            horizontalScrollSnap.StartingScreen = _indexStart;
            horizontalScrollSnap.GoToScreen(_indexStart);
        }
        if (_indexStart >= 0)
            OnPageChange(_indexStart);
    }

    /// <summary>Gỡ page bị tắt ra khỏi scroll (đưa vào object ẩn) để scroll không còn khoảng trống.</summary>
    private void _RemoveDisabledPages()
    {
        _lsPageActive.Clear();
        var _config = GameFlowConfig.Instance;
        var _content = horizontalScrollSnap != null && scrollPage != null ? scrollPage.content : null;

        foreach (var _page in pages)
        {
            if (_page == null)
                continue;
            if (_config.IsPageEnable(_page.idPage))
                continue;

            if (_page.choicePageUI != null)
                _page.choicePageUI.gameObject.SetActive(false);
            // Còn >= 2 page thì gỡ hẳn khỏi scroll; còn 1 page thì chỉ ẩn (scroll bị khoá nên không lộ khoảng trống)
            if (_content != null && _page.transform.parent == _content && _content.childCount > 2)
            {
                horizontalScrollSnap.RemoveChild(_page.transform.GetSiblingIndex(), out var _removed);
                _removed.transform.SetParent(_GetHolderPageDisable(), false);
            }
            _page.gameObject.SetActive(false);
        }

        // Page bật theo thứ tự trong scroll
        if (_content != null)
        {
            for (int i = 0; i < _content.childCount; i++)
            {
                var _page = _content.GetChild(i).GetComponent<PageBase>();
                if (_page != null && _page.gameObject.activeSelf && System.Array.IndexOf(pages, _page) >= 0)
                    _lsPageActive.Add(_page);
            }
        }
        else
        {
            foreach (var _page in pages)
                if (_page != null && _page.gameObject.activeSelf)
                    _lsPageActive.Add(_page);
        }
    }

    private Transform _GetHolderPageDisable()
    {
        if (_holderPageDisable == null)
        {
            var _go = new GameObject("[PagesDisabled]");
            _go.SetActive(false);
            _go.transform.SetParent(transform, false);
            _holderPageDisable = _go.transform;
        }
        return _holderPageDisable;
    }

    #endregion

    #region Đổi page

    /// <summary>Chuyển tới page (idPage = (int)PageName). Page đang tắt thì bỏ qua.</summary>
    public void ChangePage(int idPage, bool isIngoneSugget = false)
    {
        TryChangePage((PageName)idPage);
    }

    public void ChangePage(PageName page)
    {
        TryChangePage(page);
    }

    /// <summary>Trả về false nếu page đang tắt / không có trong Home.</summary>
    public bool TryChangePage(PageName page)
    {
        int _index = _GetIndexInScroll(page);
        if (_index < 0)
        {
            Debug.LogWarning($"[PageController] Page {page} đang tắt hoặc không có trong Home.");
            return false;
        }
        horizontalScrollSnap.ChangePage(_index);
        return true;
    }

    /// <summary>HorizontalScrollSnap gọi (OnSelectionPageChangedEvent) với vị trí page trong scroll (thứ tự object con).</summary>
    public void OnPageChange(int indexInScroll)
    {
        var _pageCurrent = _GetPageAtIndexInScroll(indexInScroll);
        if (_pageCurrent == null)
            return;
        CurrentPage = _pageCurrent.idPage;
        foreach (var _page in _lsPageActive)
        {
            if (_page == _pageCurrent)
                _page.OnChoicePage();
            else
                _page.OnHidePage();
        }
        _LogPage(CurrentPage);
    }

    #endregion

    #region Tra cứu

    public bool IsPageActive(PageName page)
    {
        return _GetIndexInScroll(page) >= 0;
    }

    public PageBase GetPage(PageName page)
    {
        foreach (var _page in pages)
            if (_page != null && _page.idPage == page)
                return _page;
        return null;
    }

    /// <summary>Giữ cho code cũ (idPage = (int)PageName).</summary>
    public PageBase GetPage(int idPage)
    {
        return GetPage((PageName)idPage);
    }

    public T GetPage<T>() where T : PageBase
    {
        foreach (var _page in pages)
            if (_page is T _pageT)
                return _pageT;
        return null;
    }

    /// <summary>Vị trí của page trong scroll (= thứ tự object con trong Content). -1 nếu page tắt.</summary>
    private int _GetIndexInScroll(PageName page)
    {
        foreach (var _page in _lsPageActive)
        {
            if (_page.idPage != page)
                continue;
            var _content = scrollPage != null ? scrollPage.content : null;
            return _content != null && _page.transform.parent == _content ? _page.transform.GetSiblingIndex() : _lsPageActive.IndexOf(_page);
        }
        return -1;
    }

    private PageBase _GetPageAtIndexInScroll(int indexInScroll)
    {
        var _content = scrollPage != null ? scrollPage.content : null;
        if (_content != null)
        {
            if (indexInScroll < 0 || indexInScroll >= _content.childCount)
                return null;
            var _page = _content.GetChild(indexInScroll).GetComponent<PageBase>();
            return _page != null && _lsPageActive.Contains(_page) ? _page : null;
        }
        return indexInScroll >= 0 && indexInScroll < _lsPageActive.Count ? _lsPageActive[indexInScroll] : null;
    }

    #endregion

    public void LockScrollPage(bool isLock)
    {
        _isLockByCode = isLock;
        _ApplyLockScroll();
    }

    private void _ApplyLockScroll()
    {
        if (scrollPage != null)
            scrollPage.enabled = !_isLockByCode && _lsPageActive.Count > 1;
    }

    private static void _LogPage(PageName page)
    {
#if FEATURE_ANALYTICS_FALCON
        string _actionName;
        switch (page)
        {
            case PageName.Shop: _actionName = FalconLog.SHOP_PAGE; break;
            case PageName.Home: _actionName = FalconLog.HOME_PAGE; break;
            case PageName.Ranking: _actionName = FalconLog.RANKING_PAGE; break;
            case PageName.Team: _actionName = FalconLog.TEAM_PAGE; break;
            case PageName.HomeDecor: _actionName = FalconLog.HOME_DECOR_PAGE; break;
            default: _actionName = page.ToString().ToLowerInvariant() + "_page"; break;
        }
        try
        {
            new FEventLog(_actionName).Send();
        }
        catch (System.Exception _exception)
        {
            Debug.LogWarning("[PageController] Falcon log lỗi: " + _exception.Message);
        }
#endif
    }
}
