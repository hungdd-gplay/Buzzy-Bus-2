using System;
using DG.Tweening;
#if FEATURE_ANALYTICS_FALCON
using Falcon.FalconAnalytics.Scripts.Models.Messages.PreDefines;
#endif
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Kiểu hiệu ứng mở / đóng popup.</summary>
public enum BoxAnimType
{
    None = 0,
    Scale = 1,      // Phóng to từ 0 -> 1 (OutBack), mặc định cũ
    Fade = 2,       // Mờ dần (cần CanvasGroup)
    SlideUp = 3,    // Trượt từ dưới lên
}

/// <summary>
/// Lớp cha của mọi Popup / Panel (Box = Popup + Panel).
/// - Popup (isPopup = true): có Canvas riêng, sắp xếp sortingOrder theo stack trong BoxController.
/// - Panel (isPopup = false): nằm trong Canvas khác, chỉ đưa lên trên cùng (SetAsLastSibling).
/// Tạo popup mới: kế thừa BaseBox + Setup() => BoxFactory.Get&lt;T&gt;(path).
/// Tạo nhanh từ ảnh demo: Tools > Base Project > Image Layout Scanner > Build Popup.
/// </summary>
public abstract class BaseBox : MonoBehaviour
{
    private const float _OFFSET_SLIDE_UP = 1200f;

    [SerializeField] protected RectTransform mainPanel;

    [Header("========= CONFIG BOX ===========")]
    [Tooltip("Không đưa vào stack của BoxController (không tính layer, không đóng bằng phím Back).")]
    public bool isNotStack;
    [Tooltip("Popup có Canvas riêng (Screen Space - Camera, sorting layer 'Popup').")]
    public bool isPopup;
    [Tooltip("Tắt = không có hiệu ứng mở / đóng.")]
    [SerializeField] protected bool isAnim = true;
    [ShowIf("isAnim")] [SerializeField] protected BoxAnimType animType = BoxAnimType.Scale;
    [ShowIf("isAnim")] [SerializeField] protected float durationAppear = 0.5f;
    [ShowIf("isAnim")] [SerializeField] protected float durationClose = 0.35f;

    [Header("========= OPTION ===========")]
    [Tooltip("Cho phép đóng bằng phím Back (Android) khi popup ở trên cùng. Cần bật isUseBackKey trên BoxController.")]
    public bool isCloseByBackKey = true;
    [Tooltip("Nền tối phía sau popup. Gán Button để bấm ra ngoài thì đóng.")]
    [SerializeField] protected Button buttonDim;
    [SerializeField] protected bool isCloseOnClickDim;
    [Tooltip("Phát SoundId.UIPopupOpen / UIPopupClose khi mở / đóng.")]
    [SerializeField] protected bool isPlaySoundOpenClose;

    [SerializeField] protected CanvasGroup canvasGruop;

    protected Canvas popupCanvas;

    /// <summary>Giữ lại để các popup cũ biên dịch được (chưa từng được gán => luôn null). Dùng canvasGruop.</summary>
    protected CanvasGroup canvasGroupPanel;

    [HideInInspector] public bool isBoxSave;
    [HideInInspector] public bool isPostEventPopupClose;

    protected string iDPopup;
    protected UnityAction actionOpenSaveBox;

    // Call Back
    public UnityAction OnCloseBox;
    public UnityAction OnAppearDone;
    public UnityAction OnCloseDone;

    private Vector2 _anchoredPositionMainPanel;
    private bool _isClosing;

    public bool IsClosing => _isClosing;

    #region Awake

    private void Awake()
    {
        popupCanvas = GetComponent<Canvas>();
        if (popupCanvas != null && isPopup)
        {
            popupCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            popupCanvas.sortingLayerID = SortingLayer.NameToID("Popup");
        }

        if (mainPanel != null)
        {
            _anchoredPositionMainPanel = mainPanel.anchoredPosition;
            var _tweenAnimation = mainPanel.GetComponent<DOTweenAnimation>();
            if (_tweenAnimation != null)
            {
                _tweenAnimation.isIndependentUpdate = true; // Không phụ thuộc time scale
                isAnim = false;
            }
        }

        if (canvasGruop == null)
            canvasGruop = GetComponent<CanvasGroup>();

        if (buttonDim != null && isCloseOnClickDim)
            buttonDim.onClick.AddListener(_OnClickDim);

        OnAwake();
    }

    protected virtual void OnAwake()
    {
    }

    #endregion

    #region Save Box

    public void InitBoxSave(bool isBoxSave, UnityAction actionOpenSaveBox)
    {
        this.isBoxSave = isBoxSave;
        this.actionOpenSaveBox = actionOpenSaveBox;
    }

    protected string GetIDPopup()
    {
        return iDPopup;
    }

    protected virtual void SetIDPopup()
    {
        iDPopup = GetType().ToString();
    }

    /// <summary>Đưa popup vào danh sách Box Save (tự mở lại khi load scene / khi stack rỗng mà bấm Back).</summary>
    public virtual void SaveBox()
    {
        if (isBoxSave && BoxController.Instance != null)
            BoxController.Instance.AddBoxSave(GetIDPopup(), actionOpenSaveBox);
    }

    #endregion

    #region Open

    protected virtual void OnEnable()
    {
        _isClosing = false;
        _RefreshCamera();
        if (!isNotStack && BoxController.Instance != null)
            BoxController.Instance.AddNewBackObj(this);

        SetIDPopup();
        if (isPlaySoundOpenClose)
            SoundManager.PlayUI(SoundId.UIPopupOpen);
        DoAppear();
        OnStart();
    }

    protected virtual void OnStart()
    {
    }

    public virtual void Show()
    {
        gameObject.SetActive(true);
#if FEATURE_ANALYTICS_FALCON
        try
        {
            new FEventLog(FalconLog.OPEN_POPUP_ + gameObject.name).Send();
        }
        catch (Exception _exception)
        {
            Debug.LogWarning("[BaseBox] Falcon log lỗi: " + _exception.Message);
        }
#endif
    }

    protected virtual void DoAppear()
    {
        _SetInteractable(false);
        if (!isAnim || animType == BoxAnimType.None)
        {
            _OnAppearComplete();
            return;
        }

        switch (animType)
        {
            case BoxAnimType.Scale:
                if (mainPanel == null) { _OnAppearComplete(); return; }
                mainPanel.DOKill();
                mainPanel.localScale = Vector3.zero;
                mainPanel.DOScale(1f, durationAppear).SetUpdate(true).SetEase(Ease.OutBack).OnComplete(_OnAppearComplete);
                _FadeCanvasGroup(0f, 1f, durationAppear * 0.5f);
                break;

            case BoxAnimType.Fade:
                if (canvasGruop == null) { _OnAppearComplete(); return; }
                _FadeCanvasGroup(0f, 1f, durationAppear).OnComplete(_OnAppearComplete);
                break;

            case BoxAnimType.SlideUp:
                if (mainPanel == null) { _OnAppearComplete(); return; }
                mainPanel.DOKill();
                mainPanel.localScale = Vector3.one;
                mainPanel.anchoredPosition = _anchoredPositionMainPanel - new Vector2(0f, _OFFSET_SLIDE_UP);
                mainPanel.DOAnchorPos(_anchoredPositionMainPanel, durationAppear).SetUpdate(true).SetEase(Ease.OutCubic).OnComplete(_OnAppearComplete);
                _FadeCanvasGroup(0f, 1f, durationAppear * 0.5f);
                break;
        }
    }

    private void _OnAppearComplete()
    {
        _SetInteractable(true);
        OnAppearDone?.Invoke();
    }

    #endregion

    #region Close

    public virtual void Close()
    {
        if (_isClosing || !gameObject.activeSelf)
            return;
        _isClosing = true;

        if (!isNotStack && BoxController.Instance != null)
            BoxController.Instance.Remove(this);
        if (isPlaySoundOpenClose)
            SoundManager.PlayUI(SoundId.UIPopupClose);
        DoClose();
        if (isPostEventPopupClose)
            EventDispatcher.EventDispatcher.Instance.PostEvent(EventID.CLOSE_POPUP);
    }

    protected virtual void DoClose()
    {
        _SetInteractable(false);
        if (!isAnim || animType == BoxAnimType.None)
        {
            _OnCloseComplete();
            return;
        }

        switch (animType)
        {
            case BoxAnimType.Scale:
                if (mainPanel == null) { _OnCloseComplete(); return; }
                mainPanel.DOKill();
                mainPanel.DOScale(0f, durationClose).SetUpdate(true).SetEase(Ease.InBack).OnComplete(_OnCloseComplete);
                break;

            case BoxAnimType.Fade:
                if (canvasGruop == null) { _OnCloseComplete(); return; }
                _FadeCanvasGroup(canvasGruop.alpha, 0f, durationClose).OnComplete(_OnCloseComplete);
                break;

            case BoxAnimType.SlideUp:
                if (mainPanel == null) { _OnCloseComplete(); return; }
                mainPanel.DOKill();
                mainPanel.DOAnchorPos(_anchoredPositionMainPanel - new Vector2(0f, _OFFSET_SLIDE_UP), durationClose)
                    .SetUpdate(true).SetEase(Ease.InCubic).OnComplete(_OnCloseComplete);
                _FadeCanvasGroup(canvasGruop != null ? canvasGruop.alpha : 1f, 0f, durationClose);
                break;
        }
    }

    private void _OnCloseComplete()
    {
        OnCloseDone?.Invoke();
        gameObject.SetActive(false);
    }

    protected virtual void OnDisable()
    {
        _isClosing = false;
        if (mainPanel != null)
        {
            mainPanel.DOKill();
            mainPanel.anchoredPosition = _anchoredPositionMainPanel;
        }
        if (canvasGruop != null)
            canvasGruop.DOKill();

        // Bị tắt trực tiếp bằng SetActive(false) (không qua Close) -> vẫn phải gỡ khỏi stack
        if (!isNotStack && BoxController.Instance != null)
            BoxController.Instance.Remove(this);

        var _onCloseBox = OnCloseBox;
        OnCloseBox = null;
        _onCloseBox?.Invoke();

        if (BoxController.Instance != null)
            BoxController.Instance.actionOnClosedOneBox?.Invoke();
    }

    private void _OnClickDim()
    {
        if (BoxController.Instance == null || BoxController.Instance.IsTopBox(this))
            Close();
    }

    #endregion

    #region Layer

    /// <summary>BoxController gọi khi stack thay đổi để sắp xếp thứ tự hiển thị.</summary>
    public void ChangeLayerHandle(ref int indexInStack)
    {
        // Panel (isPopup = false) giữ nguyên thứ tự trong Canvas cha
        if (!isPopup || popupCanvas == null)
            return;
        popupCanvas.sortingOrder = BoxController.BASE_INDEX_LAYER + indexInStack;
        popupCanvas.planeDistance = 5;
        indexInStack += BoxController.STEP_INDEX_LAYER;
    }

    #endregion

    #region Helper

    /// <summary>Camera có thể đổi khi load scene -> lấy lại mỗi lần mở.</summary>
    private void _RefreshCamera()
    {
        if (popupCanvas == null || !isPopup)
            return;
        if (popupCanvas.worldCamera == null || !popupCanvas.worldCamera.isActiveAndEnabled)
            popupCanvas.worldCamera = Camera.main;
    }

    private void _SetInteractable(bool isInteractable)
    {
        if (canvasGruop != null)
            canvasGruop.interactable = isInteractable;
    }

    private Tween _FadeCanvasGroup(float from, float to, float duration)
    {
        if (canvasGruop == null)
            return null;
        canvasGruop.DOKill();
        canvasGruop.alpha = from;
        return canvasGruop.DOFade(to, duration).SetUpdate(true);
    }

    #endregion

    #region Override cho Tool (LongTH/Tool.cs)

    public virtual bool IsActive()
    {
        return true;
    }

    public virtual void CreateInstance()
    {
    }

    #endregion
}
