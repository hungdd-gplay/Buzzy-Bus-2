using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Lớp cha của mọi item gameplay (Controller của 1 item).
/// Phần dùng chung đã có sẵn: đọc cấu hình segment, kiểm tra điều kiện, kích hoạt, thời gian tác dụng,
/// hồi chiêu, giới hạn số lần / level, spawn object / hiệu ứng bay, tạm dừng, kết thúc level.
/// Item mới chỉ cần viết phần tác dụng trong các hàm *Handle (để trống sẵn).
/// Tạo item mới: Tools > Base Project > Item > Item Manager.
/// </summary>
public abstract class ItemBase : MonoBehaviour
{
    public enum GetPlace
    {
        SellectLevel = 0,   // Chọn trước khi vào level (PlayCampainBox), tự kích hoạt khi level sẵn sàng
        GamePlayUI = 1,     // Nút bấm trong gameplay
        Gadern = 2          // Không hiện nút, gameplay tự gọi
    }

    [Header("========= ITEM ===========")]
    public GiftType typeItem;
    public GetPlace getPlace = GetPlace.GamePlayUI;
    [Tooltip("Prefab tạo ra khi gọi Spawn() (bom, khiên...). Để trống nếu item không có object riêng.")]
    [SerializeField] protected GameObject prefabSpawn;
    [Tooltip("Được dùng tiếp khi tác dụng lần trước chưa hết.")]
    [SerializeField] protected bool isCanUseWhileActive;

    protected ItemController controller;
    private bool _isPause;

    public bool IsActiveNow { get; private set; }
    public int CountUsedInLevel { get; private set; }
    public float CooldownLeft { get; private set; }
    public float DurationLeft { get; private set; }

    #region Cấu hình (segment)

    /// <summary>Cấu hình item trong segment hiện tại (CoreGameData > bootsConfig). Có thể null.</summary>
    public CoreGameData.BootsData Config
    {
        get
        {
            if (GameController.Instance == null || GameController.Instance.segmentController == null)
                return null;
            return GameController.Instance.segmentController.GetCurrentSegment().coreGameData.config?.bootsConfig?.GetBoot(typeItem);
        }
    }

    /// <summary>Đọc thông số tác dụng workValue[index] trong segment.</summary>
    public float GetWorkValue(int index, float defaultValue = 0f)
    {
        var _config = Config;
        if (_config == null || _config.workValue == null || index < 0 || index >= _config.workValue.Length)
            return defaultValue;
        return _config.workValue[index];
    }

    public bool IsEnable => Config == null || Config.isEnable;
    public bool IsUnlocked => Config == null || UseProfile.CurrentLevel >= Config.unlockLevel;
    public bool IsConsumeOnActive => Config == null || Config.isConsumeOnActive;
    public int Amount => UseProfile.GetNumItem(typeItem);
    public bool IsUnlimited => UseProfile.IsUnlimitedItem(typeItem);
    public bool IsHasItem => IsUnlimited || Amount > 0;

    #endregion

    #region Vòng đời (ItemController gọi)

    public void Setup(ItemController itemController)
    {
        controller = itemController;
        _ResetState();
        Init();
    }

    /// <summary>Khởi tạo 1 lần khi vào scene gameplay.</summary>
    public virtual void Init()
    {
    }

    public void OnLevelStart()
    {
        _ResetState();
        OnLevelStartHandle();
    }

    public void OnLevelEnd()
    {
        if (IsActiveNow)
            EndItem();
        OnLevelEndHandle();
    }

    public void Pause(bool isPause)
    {
        _isPause = isPause;
        OnPauseHandle(isPause);
    }

    /// <summary>Cập nhật hồi chiêu / thời gian tác dụng. ItemController gọi mỗi frame.</summary>
    public void Tick(float deltaTime)
    {
        if (_isPause)
            return;
        if (CooldownLeft > 0f)
            CooldownLeft = Mathf.Max(0f, CooldownLeft - deltaTime);
        if (!IsActiveNow)
            return;

        OnActiveUpdate(deltaTime);
        if (DurationLeft > 0f)
        {
            DurationLeft -= deltaTime;
            if (DurationLeft <= 0f)
                EndItem();
        }
    }

    private void _ResetState()
    {
        IsActiveNow = false;
        CountUsedInLevel = 0;
        CooldownLeft = 0f;
        DurationLeft = 0f;
        _isPause = false;
    }

    #endregion

    #region Dùng item

    public virtual bool IsCanUseItem()
    {
        if (!IsEnable || !IsUnlocked)
            return false;
        if (CooldownLeft > 0f)
            return false;
        if (IsActiveNow && !isCanUseWhileActive)
            return false;
        var _config = Config;
        if (_config != null && _config.maxUsePerLevel > 0 && CountUsedInLevel >= _config.maxUsePerLevel)
            return false;
        return IsCanUseItemHandle();
    }

    /// <summary>Kích hoạt tác dụng. Không trừ số lượng (ItemController.UseItem lo việc trừ).</summary>
    public virtual bool ActiveItem()
    {
        if (!IsCanUseItem())
            return false;
        if (!ActiveItemHandle())
            return false;

        CountUsedInLevel++;
        var _config = Config;
        CooldownLeft = _config != null ? _config.cooldown : 0f;
        float _duration = _config != null ? _config.durationActive : 0f;
        if (_duration > 0f)
        {
            IsActiveNow = true;
            DurationLeft = _duration;
        }
        if (controller != null)
            controller.NotifyItemActivated(this);
        return true;
    }

    /// <summary>Kết thúc tác dụng (tự gọi khi hết durationActive / hết level, hoặc gameplay gọi chủ động).</summary>
    public void EndItem()
    {
        if (!IsActiveNow)
            return;
        IsActiveNow = false;
        DurationLeft = 0f;
        EndItemHandle();
        if (controller != null)
            controller.NotifyItemEnded(this);
    }

    #endregion

    #region Spawn / hiệu ứng

    /// <summary>Tạo prefabSpawn (lấy từ pool) tại vị trí. Trả về null nếu không có prefab.</summary>
    public virtual GameObject Spawn(Vector3 position, Transform parent = null)
    {
        GameObject _spawned = null;
        if (prefabSpawn != null && controller != null)
            _spawned = controller.SpawnFromPool(prefabSpawn, position, parent);
        OnSpawnHandle(_spawned);
        return _spawned;
    }

    /// <summary>Trả object đã Spawn về pool.</summary>
    public virtual void Despawn(GameObject spawned)
    {
        if (spawned == null)
            return;
        OnDespawnHandle(spawned);
        if (controller != null)
            controller.DespawnToPool(spawned);
        else
            spawned.SetActive(false);
    }

    /// <summary>Hiệu ứng icon item bay từ from tới to (dùng ItemInGameView).</summary>
    public ItemFXView SpawnFlyFX(Vector3 from, Vector3 to, int value = 1, UnityAction onCome = null, float speedMove = 1f)
    {
        if (controller == null || controller.view == null)
        {
            onCome?.Invoke();
            return null;
        }
        var _fx = controller.view.GetItemFX();
        _fx.transform.position = from;
        _fx.Show(typeItem, value, to, speedMove, onCome);
        return _fx;
    }

    #endregion

    #region UI

    public ItemUI GetItemUI()
    {
        if (GamePlayController.Instance == null || GamePlayController.Instance.gameScene == null || GamePlayController.Instance.gameScene.itemsUI == null)
            return null;
        GamePlayController.Instance.gameScene.itemsUI.TryGetValue(typeItem, out var _itemUI);
        return _itemUI;
    }

    public virtual void InteractButton(bool isInteract)
    {
        var _itemUI = GetItemUI();
        if (_itemUI != null)
            _itemUI.InteractBtnUse(isInteract);
    }

    #endregion

    #region Hàm để trống cho gameplay (override khi cần)

    /// <summary>Tác dụng của item. Trả về true nếu kích hoạt thành công (sẽ trừ số lượng, tính hồi chiêu).</summary>
    protected abstract bool ActiveItemHandle();

    /// <summary>Điều kiện riêng của gameplay (ví dụ: còn ô trống, đang không có animation...).</summary>
    protected virtual bool IsCanUseItemHandle()
    {
        return true;
    }

    /// <summary>Gỡ tác dụng khi hết thời gian / hết level (chỉ với item có durationActive > 0).</summary>
    protected virtual void EndItemHandle()
    {
    }

    /// <summary>Mỗi frame trong lúc item đang có tác dụng.</summary>
    protected virtual void OnActiveUpdate(float deltaTime)
    {
    }

    protected virtual void OnSpawnHandle(GameObject spawned)
    {
    }

    protected virtual void OnDespawnHandle(GameObject spawned)
    {
    }

    protected virtual void OnLevelStartHandle()
    {
    }

    protected virtual void OnLevelEndHandle()
    {
    }

    protected virtual void OnPauseHandle(bool isPause)
    {
    }

    #endregion
}
