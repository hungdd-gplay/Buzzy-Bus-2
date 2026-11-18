using DG.Tweening;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Lớp cha của item thu thập trong level (item của event / challenge: nhặt đủ để nhận quà).
/// Phần dùng chung đã có sẵn: gắn vào vị trí spawn, hiệu ứng xuất hiện (Spine hoặc Sprite),
/// nhặt (tính điểm, x2, khung hiển thị, icon bay về khung), ẩn / trả lại.
/// Item mới chỉ cần viết phần riêng trong các hàm *Handle (để trống sẵn).
/// Tạo item mới: Tools > Base Project > Item > Item Manager > tab Collect Item.
/// </summary>
public abstract class CollectItemBase : SerializedMonoBehaviour
{
    public const string APPEAR = "appear";
    public const string IDLE = "idle";

    [Header("========= HIỂN THỊ ===========")]
    public Sprite icon;
    [Tooltip("Thân item dạng Spine (anim 'appear' -> 'idle'). Có thể để trống nếu dùng spriteBody.")]
    public SkeletonAnimation body;
    [Tooltip("Thân item dạng Sprite (khi không dùng Spine).")]
    [SerializeField] protected SpriteRenderer spriteBody;
    [SerializeField] protected int LayerBase;
    [SerializeField] protected ExtralLayer fxIdleLayer;

    [Header("========= BAY VỀ KHUNG ===========")]
    [SerializeField] protected Vector2 rangeSpeedFly = new Vector2(0.5f, 1.5f);
    [SerializeField] protected float timeJumpFly = 0.7f;

    private Vector3? _scaleSpriteBody;
    protected ICollectItemController controller;
    protected ICollectItemSource source;

    public bool IsCollected { get; private set; }
    public ICollectItemSource Source => source;
    public CollectItemConfig Config => source != null ? source.CollectConfig : null;

    #region Vòng đời

    /// <summary>Gọi bởi CollectItemGameplayController ngay sau khi tạo.</summary>
    public void Setup(ICollectItemController itemController, ICollectItemSource itemSource)
    {
        controller = itemController;
        source = itemSource;
        IsCollected = false;
        if (icon == null && itemSource != null)
            icon = itemSource.IconCollect;
        OnSetupHandle();
    }

    /// <summary>Giữ cho code cũ. Gắn item vào posSpawn rồi gọi actionCome.</summary>
    public void Init(Transform posSpawn, UnityAction actionCome, bool isFlyOut, float delayTimeFlyOut = 0)
    {
        Spawn(posSpawn);
        actionCome?.Invoke();
    }

    /// <summary>Gắn item vào vị trí (object gameplay) và chạy hiệu ứng xuất hiện.</summary>
    public virtual void Spawn(Transform posSpawn)
    {
        transform.DOKill();
        if (posSpawn != null)
        {
            transform.SetParent(posSpawn, true);
            transform.position = posSpawn.position;
        }
        IsCollected = false;
        gameObject.SetActive(true);
        _SetBodyVisible(true);
        PlayAppear();
        OnSpawnHandle(posSpawn);
    }

    public virtual void PlayAppear()
    {
        if (body != null)
            body.SetAnimation(APPEAR, false, delegate { body.SetAnimation(IDLE, true); });
        else if (spriteBody != null)
        {
            spriteBody.transform.DOKill();
            if (_scaleSpriteBody == null)
                _scaleSpriteBody = spriteBody.transform.localScale;
            spriteBody.transform.localScale = Vector3.zero;
            spriteBody.transform.DOScale(_scaleSpriteBody.Value, 0.3f).SetEase(Ease.OutBack);
        }
        OnAppearHandle();
    }

    /// <summary>Giữ cho code cũ.</summary>
    public void AppearItem(UnityAction actionItemReady)
    {
        actionItemReady?.Invoke();
    }

    /// <summary>Ẩn item (không cộng điểm), ví dụ khi object gameplay mang item bị huỷ.</summary>
    public virtual void Despawn()
    {
        transform.DOKill();
        OnDespawnHandle();
        gameObject.SetActive(false);
    }

    #endregion

    #region Nhặt

    public virtual bool IsCanCollect()
    {
        return !IsCollected && gameObject.activeInHierarchy && IsCanCollectHandle();
    }

    /// <summary>Nhặt item: cộng điểm, hiện khung, icon bay về khung. Trả về false nếu không nhặt được.</summary>
    public virtual bool Collect(bool isUseMagnet = false)
    {
        if (!IsCanCollect())
            return false;
        IsCollected = true;
        _SetBodyVisible(false);

        int _amount = GetAmountPerCollect();
        int _firstScore = controller != null ? controller.GetScoreItem(this) : 0;
        var _frame = controller != null ? controller.GetFrame(this) : null;
        if (_frame != null)
            _frame.Show(icon, _firstScore);

        if (controller != null)
            controller.OnItemCollected(this, _amount);
        OnCollectHandle(_amount, isUseMagnet);
        _FlyToFrame(_frame, _amount);
        return true;
    }

    /// <summary>Giữ tên cũ (ItemChallenge / EventSaleItem).</summary>
    public virtual void Claimp(bool isUseManget = false)
    {
        Collect(isUseManget);
    }

    /// <summary>Số điểm mỗi lần nhặt: scorePerItem, x2 nếu đang có item tăng gấp đôi.</summary>
    public virtual int GetAmountPerCollect()
    {
        var _config = Config;
        int _amount = _config != null ? Mathf.Max(1, _config.scorePerItem) : 1;
        var _typeBoost = GetTypeItemBoostX2();
        if (_typeBoost != GiftType.None && UseProfile.IsUnlimitedItem(_typeBoost))
            _amount *= 2;
        return _amount;
    }

    protected virtual GiftType GetTypeItemBoostX2()
    {
        var _config = Config;
        return _config != null ? _config.typeItemBoostX2 : GiftType.None;
    }

    public float GetWorkValue(int index, float defaultValue = 0f)
    {
        var _config = Config;
        if (_config == null || _config.workValue == null || index < 0 || index >= _config.workValue.Length)
            return defaultValue;
        return _config.workValue[index];
    }

    private void _FlyToFrame(ItemShowFarme frame, int amount)
    {
        var _itemController = GamePlayController.Instance != null ? GamePlayController.Instance.itemController : null;
        if (frame == null || _itemController == null || _itemController.view == null)
        {
            _OnFlyDone(frame, amount);
            return;
        }

        var _fx = _itemController.view.GetItemFX();
        _fx.transform.position = transform.position;
        var _target = frame.iconItemImg.transform;
        _fx.Show(icon, amount, _target.position, Random.Range(rangeSpeedFly.x, rangeSpeedFly.y),
            () => _OnFlyDone(frame, amount),
            isTime: false, destinationFollow: _target, timeDelay: 0.05f, isJump: true, timeJump: timeJumpFly);
    }

    private void _OnFlyDone(ItemShowFarme frame, int amount)
    {
        if (frame != null)
            frame.UpdateScore(amount);
        OnFlyDoneHandle();
        gameObject.SetActive(false);
    }

    private void _SetBodyVisible(bool isVisible)
    {
        if (body != null)
            body.gameObject.SetActive(isVisible);
        if (spriteBody != null)
            spriteBody.gameObject.SetActive(isVisible);
    }

    #endregion

    #region Hàm để trống cho item (override khi cần)

    /// <summary>Ngay sau khi tạo và gán nguồn (event / challenge).</summary>
    protected virtual void OnSetupHandle()
    {
    }

    /// <summary>Sau khi gắn vào object gameplay.</summary>
    protected virtual void OnSpawnHandle(Transform posSpawn)
    {
    }

    protected virtual void OnAppearHandle()
    {
    }

    /// <summary>Điều kiện riêng để nhặt (ví dụ: object mang item phải bị phá).</summary>
    protected virtual bool IsCanCollectHandle()
    {
        return true;
    }

    /// <summary>Lúc nhặt (âm thanh, rung, hiệu ứng riêng...).</summary>
    protected virtual void OnCollectHandle(int amount, bool isUseMagnet)
    {
    }

    /// <summary>Icon bay về tới khung.</summary>
    protected virtual void OnFlyDoneHandle()
    {
    }

    protected virtual void OnDespawnHandle()
    {
    }

    #endregion
}
