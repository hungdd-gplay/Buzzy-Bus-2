using Crystal;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using BaseProject.Core.Feature;

public enum ModeGame
{
    Campaign = 0,
    PvP1 = 1,
}

public enum StateGame
{
    Loading = 0,
    Playing = 1,
    Win = 2,
    Lose = 3,
    Pause = 4
}

/// <summary>
/// Khung điều phối màn chơi (Controller trong MVC).
/// Gameplay cũ (Bird Sort) đã được gỡ bỏ. Các hàm "cầu nối" bên dưới đang để trống,
/// gameplay mới chỉ cần override / điền logic vào các hàm này.
/// Các hệ thống core (Challenge, IQScore, Item, Theme, Tutorial, EventSale) vẫn gọi qua đây.
/// </summary>
public class GamePlayController : Singleton<GamePlayController>
{
    /// <summary>Đánh dấu người chơi vừa thắng 1 level (dùng cho Home / VideoGirlPack).</summary>
    public static bool isHasBeenWinLevel = false;

    [Header("Core")]
    public PlayerContain playerContain;
    public GameScene gameScene;
    public CameraFit cameraFit;
    public StateGame state;

    [Header("Feature controllers (có thể để trống)")]
    public ItemController itemController;
    public ScoreIQController scoreController;
    public IQScoreController iqScoreController;
    public ChallengeGameplayController challengeGameplayController;
    public EventSaleInGameController eventSaleController;
    public ItemShowFarmeController itemShowFarmeController;
    public ThemeController themeController;
    public TutorialFunController tutCoreGameController;
    public TutorialFunController tutItemsController;
    public TutorialFunController tutExtralController;
    public TutorialFunController tutWeeklyChallengeController;

    [Header("Safe Area")]
    public SafeArea safeArea;

    protected override void OnAwake()
    {
        GameController.Instance.currentScene = SceneType.GamePlay;
        if (cameraFit != null)
            cameraFit.Init();
    }

    public void Start()
    {
        Init();
        InitState();
    }

    public void Init()
    {
#if UNITY_IOS
        if (safeArea != null)
            safeArea.enabled = true;
#endif
        if (GameController.Instance.IsFeatureReady(FeatureId.SkillUser))
        {
            GameController.Instance.skillUserController.AddValueCondition(ConditionChangeSkill.TypeCondition.TimeWaitingLevel, 1);
            GameController.Instance.skillUserController.CheckCurrentSkill();
        }

        if (playerContain != null) playerContain.Init();
        if (gameScene != null) gameScene.Init();
        InitLevel();
        if (itemController != null) itemController.Init();
        if (scoreController != null) scoreController.Init();
        if (iqScoreController != null) iqScoreController.Init();
        if (itemShowFarmeController != null) itemShowFarmeController.Init();
        if (challengeGameplayController != null) challengeGameplayController.Init();
        if (eventSaleController != null) eventSaleController.Init();
        if (themeController != null) themeController.Init();
        if (tutCoreGameController != null) tutCoreGameController.Init();
        if (tutItemsController != null) tutItemsController.Init();
        if (tutExtralController != null) tutExtralController.Init();
        if (tutWeeklyChallengeController != null) tutWeeklyChallengeController.Init();

        if (GameController.Instance.IsFeatureReady(FeatureId.Ads))
        {
            AdsManager.DestroyBanner();
            AdsManager.ShowBanner();
        }
    }

    public void InitState()
    {
        if (cameraFit != null)
            cameraFit.InitState();
        InitStateLevel();
        SpawnCollectItems();
    }

    /// <summary>
    /// Rải item thu thập của event / challenge lên các vị trí GetTransformsSpawnItem().
    /// Tự gọi sau InitStateLevel; gameplay gọi lại khi tạo level mới mà không load lại scene.
    /// </summary>
    public void SpawnCollectItems()
    {
        if (challengeGameplayController != null) challengeGameplayController.SpawnItems();
        if (eventSaleController != null) eventSaleController.SpawnItems();
    }

    public bool IsShowRate()
    {
        if (!UseProfile.CanShowRate)
            return false;
        int _levelPassed = GameController.Instance.useProfile.CurrentLevelPlay - 1;
        float _levelStartShowRate = RemoteConfigController.GetFloatConfig(FirebaseConfig.LEVEL_START_SHOW_RATE, 5);
        return _levelPassed >= _levelStartShowRate;
    }

    #region Cầu nối gameplay (để trống - gameplay mới điền vào)

    /// <summary>Khởi tạo level hiện tại (load data, spawn object...).</summary>
    public virtual void InitLevel()
    {
        state = StateGame.Loading;
    }

    /// <summary>Gọi sau InitLevel, chuyển level sang trạng thái sẵn sàng chơi.</summary>
    public virtual void InitStateLevel()
    {
    }

    /// <summary>Tạm dừng / tiếp tục logic level (timer, input...).</summary>
    public virtual void PauseLevel(bool isPause)
    {
        if (itemController != null) itemController.PauseItems(isPause);
    }

    /// <summary>Hồi sinh sau khi thua.</summary>
    public virtual void ReviveLevel(bool isAddTime, bool isResetSlot, bool isLoseExtral, bool isUseElectric = false, int numElectricUse = 0)
    {
    }

    /// <summary>Cộng thêm thời gian chơi cho level (giây).</summary>
    public virtual void AddTimePlayLevel(float timeAdd)
    {
    }

    /// <summary>Số sao đạt được khi thắng level.</summary>
    public virtual int GetStarWinLevel()
    {
        return 0;
    }

    /// <summary>Điểm hiện tại của level (dùng cho IQ score).</summary>
    public virtual int GetCurrentScore()
    {
        return 0;
    }

    /// <summary>Danh sách phần thưởng người chơi sẽ mất nếu thoát level.</summary>
    public virtual List<RewardRandom> GetItemYouMiss()
    {
        return new List<RewardRandom>();
    }

    /// <summary>Tỉ lệ hoàn thành yêu cầu level (0..1).</summary>
    public virtual float GetPercentRequirementFound()
    {
        return 0;
    }

    /// <summary>Các vị trí để gắn item challenge / event lên object gameplay.</summary>
    public virtual List<Transform> GetTransformsSpawnItem()
    {
        return new List<Transform>();
    }

    /// <summary>Loại item có được hiển thị trong level hiện tại hay không.</summary>
    public virtual bool IsItemAvailableInLevel(ItemBase item)
    {
        return true;
    }

    /// <summary>Các chướng ngại (nếu có) đã sẵn sàng chưa.</summary>
    public virtual bool IsObstaclesReady()
    {
        return true;
    }

    /// <summary>Cho phép / khoá chức năng hoàn tác (undo).</summary>
    public virtual void SetLockReturn(bool isLock)
    {
    }

    /// <summary>Tutorial trong level có đang chạy không.</summary>
    public virtual bool IsHasTutorialInLevel()
    {
        return false;
    }

    /// <summary>Gợi ý mua item khi người chơi dùng item.</summary>
    public virtual void OnUseItemSuggetBuy(GiftType typeItem)
    {
    }

    /// <summary>Gọi khi kết thúc level để hiện gợi ý (sale...) trước khi chạy action.</summary>
    public virtual void OnSuggetEndLevel(UnityAction actionEnd)
    {
        actionEnd?.Invoke();
    }

    public virtual void OnWinLevel()
    {
        state = StateGame.Win;
        isHasBeenWinLevel = true;
        if (itemController != null) itemController.OnLevelEnd();
        if (challengeGameplayController != null) challengeGameplayController.WinLevel();
        if (eventSaleController != null) eventSaleController.WinLevel();
    }

    public virtual void OnLoseLevel()
    {
        state = StateGame.Lose;
        if (itemController != null) itemController.OnLevelEnd();
        if (challengeGameplayController != null) challengeGameplayController.LoseLevel();
        if (eventSaleController != null) eventSaleController.LoseLevel();
    }

    public virtual void OnReplayLevel()
    {
        if (itemController != null) itemController.OnLevelStart();
    }

    public virtual void OnQuitLevel()
    {
        if (itemController != null) itemController.OnLevelEnd();
    }

    #endregion
}
