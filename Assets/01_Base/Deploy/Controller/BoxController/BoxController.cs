using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/***
 * Box = Popup + Panel
 * - Mỗi Box khi bật (OnEnable) được đưa vào stack, khi tắt (Close / SetActive(false)) được gỡ khỏi stack.
 * - Popup sau luôn có sortingOrder cao hơn popup trước (BASE_INDEX_LAYER + STEP_INDEX_LAYER * vị trí).
 * - Panel (isPopup = false) có layer thấp hơn mọi Popup => muốn mở Panel từ Popup thì phải đóng Popup trước.
 * - Phím Back (Android): đóng box trên cùng; stack rỗng thì mở Box Save hoặc gọi BaseScene.OnEscapeWhenStackBoxEmpty().
 ***/
public class BoxController : MonoBehaviour
{
    public static BoxController Instance;

    public const int BASE_INDEX_LAYER = 20;
    public const int STEP_INDEX_LAYER = 40;

    [SerializeField] private BaseScene currentScene;
    [Tooltip("Bật phím Back / Escape để đóng popup trên cùng (trước đây bị comment => mặc định tắt).")]
    [SerializeField] private bool isUseBackKey;

    /// <summary>Phần tử cuối = box trên cùng.</summary>
    private readonly List<BaseBox> _lsBoxStack = new List<BaseBox>();

    /// <summary>
    /// Các Box bắt buộc phải xem (ID popup -> hàm mở lại).
    /// Hiện đi hiện lại đến khi user đóng thì mới RemoveBoxSave.
    /// </summary>
    private readonly Dictionary<string, UnityAction> _dicActionSaveBox = new Dictionary<string, UnityAction>();

    /// <summary>Khoá phím Back (ví dụ khi đang hiện WaitingBox).</summary>
    public bool isLockEscape;
    public UnityAction actionStackEmpty;
    public UnityAction actionOnClosedOneBox;

    public int CountBox => _lsBoxStack.Count;

    #region Unity

    protected void Awake()
    {
        Instance = this;
        gameObject.name = "BoxController";
        SceneManager.sceneLoaded += _OnSceneLoaded;
    }

    protected void OnDestroy()
    {
        SceneManager.sceneLoaded -= _OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    protected virtual void Update()
    {
        if (isUseBackKey && Input.GetKeyUp(KeyCode.Escape))
            ProcessBackBtn();

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F9))
            DebugStack();
#endif
    }

    private void _OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;
        currentScene = FindFirstObjectByType<BaseScene>();
        // Box của scene cũ đã bị huỷ -> dọn khỏi stack
        _lsBoxStack.RemoveAll(box => box == null || !box.gameObject.activeInHierarchy);
        _SortLayerPopup();
        _OpenBoxSave();
    }

    #endregion

    #region Load Box Async

    public void GetBoxAsync(BaseBox instance, Action<BaseBox> OnLoadedFirst, Action<BaseBox> OnLoaded, string resourcePath)
    {
        StartCoroutine(_GetBoxAsyncHandle(instance, OnLoadedFirst, OnLoaded, resourcePath));
    }

    private IEnumerator _GetBoxAsyncHandle(BaseBox instance, Action<BaseBox> OnLoadedFirst, Action<BaseBox> OnLoaded, string resourcePath)
    {
        if (instance == null)
        {
            ResourceRequest _request = Resources.LoadAsync<BaseBox>(resourcePath);
            yield return _request;
            if (_request.asset == null)
            {
                Debug.LogError("[BoxController] Không tìm thấy popup: Resources/" + resourcePath);
                yield break;
            }
            instance = Instantiate(_request.asset as BaseBox);
            OnLoadedFirst?.Invoke(instance);
        }

        OnLoaded?.Invoke(instance);
    }

    public void GetBoxAsync<T>(T instance, Action<T> OnLoadedFirst, Action<T> OnLoaded, string resourcePath)
        where T : BaseBox
    {
        if (instance != null)
        {
            OnLoaded?.Invoke(instance);
            return;
        }

        WaitingBox.Setup().ShowWaiting();
        Resources.LoadAsync<T>(resourcePath)
                 .AsAsyncOperationObservable()
                 .Subscribe(resourceRequest =>
                 {
                     WaitingBox.Setup().HideWaiting();
                     if (resourceRequest.asset == null)
                     {
                         Debug.LogError("[BoxController] Không tìm thấy popup: Resources/" + resourcePath);
                         return;
                     }
                     instance = Object.Instantiate(resourceRequest.asset) as T;
                     OnLoadedFirst?.Invoke(instance);
                     OnLoaded?.Invoke(instance);
                 });
    }

    #endregion

    #region Stack

    public void AddNewBackObj(BaseBox box)
    {
        if (box == null)
            return;
        _lsBoxStack.Remove(box); // Bật lại box đang có trong stack -> đưa lên trên cùng
        _lsBoxStack.Add(box);
        _SortLayerPopup();
    }

    /// <summary>Gỡ đúng box được truyền vào (không phải luôn là box trên cùng).</summary>
    public void Remove(BaseBox box)
    {
        if (!_lsBoxStack.Remove(box))
            return;
        if (_lsBoxStack.Count == 0)
            actionStackEmpty?.Invoke();
        _SortLayerPopup();
    }

    /// <summary>Gỡ box trên cùng (giữ cho code cũ).</summary>
    public void Remove()
    {
        if (_lsBoxStack.Count > 0)
            Remove(_lsBoxStack[_lsBoxStack.Count - 1]);
    }

    public BaseBox GetTopBox()
    {
        return _lsBoxStack.Count > 0 ? _lsBoxStack[_lsBoxStack.Count - 1] : null;
    }

    public bool IsTopBox(BaseBox box)
    {
        return box != null && GetTopBox() == box;
    }

    /// <summary>Đang có Popup (isPopup) hiện.</summary>
    public bool IsShowingPopup()
    {
        for (int i = 0; i < _lsBoxStack.Count; i++)
            if (_lsBoxStack[i] != null && _lsBoxStack[i].isPopup)
                return true;
        return false;
    }

    /// <summary>Không có Popup hay Panel nào đang bật.</summary>
    public bool IsEmptyStackBox()
    {
        return _lsBoxStack.Count == 0;
    }

    private void _SortLayerPopup()
    {
        _lsBoxStack.RemoveAll(box => box == null);
        int _index = 0;
        for (int i = 0; i < _lsBoxStack.Count; i++)
            _lsBoxStack[i].ChangeLayerHandle(ref _index);
    }

    public void DebugStack()
    {
        for (int i = _lsBoxStack.Count - 1; i >= 0; i--)
            Debug.Log($"[BoxController] {i}: {(_lsBoxStack[i] != null ? _lsBoxStack[i].gameObject.name : "null")}");
    }

    #endregion

    #region Back Key

    public virtual void ProcessBackBtn()
    {
        if (isLockEscape)
            return;

        var _top = GetTopBox();
        if (_top != null)
        {
            if (_top.isCloseByBackKey)
                _top.Close();
            return;
        }

        if (!_OpenBoxSave())
            OnPressEscapeStackEmpty();
    }

    protected virtual void OnPressEscapeStackEmpty()
    {
        if (currentScene == null)
            currentScene = FindFirstObjectByType<BaseScene>();
        if (currentScene != null)
            currentScene.OnEscapeWhenStackBoxEmpty();
    }

    #endregion

    #region Box Save

    private bool _OpenBoxSave()
    {
        bool _isOpened = false;
        // Copy ra list vì hàm mở có thể Add / Remove vào dictionary
        foreach (var _actionOpen in new List<UnityAction>(_dicActionSaveBox.Values))
        {
            if (_actionOpen == null)
                continue;
            _isOpened = true;
            _actionOpen();
        }
        return _isOpened;
    }

    public void AddBoxSave(string idPopup, UnityAction actionOpen)
    {
        if (string.IsNullOrEmpty(idPopup))
            return;
        _dicActionSaveBox[idPopup] = actionOpen;
    }

    public void RemoveBoxSave(string idPopup)
    {
        if (!string.IsNullOrEmpty(idPopup))
            _dicActionSaveBox.Remove(idPopup);
    }

    #endregion
}
