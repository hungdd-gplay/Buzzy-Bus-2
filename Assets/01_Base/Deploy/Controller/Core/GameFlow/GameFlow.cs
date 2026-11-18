using UnityEngine;

/// <summary>
/// Controller luồng màn hình: quyết định scene sau Loading, scene "Home" thực tế của game
/// (game không có Home thì về thẳng GamePlay). GameController.LoadScene() đã tự đi qua ResolveScene().
/// </summary>
public static class GameFlow
{
    public static GameFlowConfig Config => GameFlowConfig.Instance;

    public static bool IsUseHome => Config.isUseHome;

    /// <summary>Scene dùng làm "Home" của game hiện tại.</summary>
    public static string SceneHome => IsUseHome ? SceneName.HOME_SCENE : SceneName.GAME_PLAY;

    /// <summary>
    /// Scene sau màn Loading:
    /// không có Home -> GamePlay; segment loadGameConfig.typeSenceFirstOnOpenGame = 1 -> GamePlay;
    /// level hiện tại <= levelPlayDirectFromLoading (remote 'start_level_back_to_home') -> GamePlay; còn lại -> Home.
    /// </summary>
    public static string GetSceneAfterLoading()
    {
        if (!IsUseHome)
            return SceneName.GAME_PLAY;

        var _loadGameConfig = _GetLoadGameConfig();
        if (_loadGameConfig != null && _loadGameConfig.typeSenceFirstOnOpenGame == 1)
            return SceneName.GAME_PLAY;

        int _levelPlayDirect = RemoteConfigController.GetIntConfig(FirebaseConfig.START_LEVEL_BACK_TO_HOME, Config.levelPlayDirectFromLoading);
        return UseProfile.CurrentLevel <= _levelPlayDirect ? SceneName.GAME_PLAY : SceneName.HOME_SCENE;
    }

    /// <summary>Đổi scene đích theo cấu hình (không có Home: HomeScene -> GamePlay).</summary>
    public static string ResolveScene(string sceneName)
    {
        if (!IsUseHome && sceneName == SceneName.HOME_SCENE)
            return SceneName.GAME_PLAY;
        return sceneName;
    }

    /// <summary>Về Home (hoặc GamePlay nếu game không có Home).</summary>
    public static void GoHome()
    {
        GameController.Instance.LoadScene(SceneHome);
    }

    public static bool IsPageEnable(PageName page)
    {
        return IsUseHome && Config.IsPageEnable(page);
    }

    private static CoreGameData.LoadGameConfig _GetLoadGameConfig()
    {
        if (GameController.Instance == null || GameController.Instance.segmentController == null)
            return null;
        try
        {
            return GameController.Instance.segmentController.GetCurrentSegment().coreGameData.config.loadGameConfig;
        }
        catch (System.Exception _exception)
        {
            Debug.LogWarning("[GameFlow] Không đọc được loadGameConfig: " + _exception.Message);
            return null;
        }
    }
}
