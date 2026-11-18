using System.Collections.Generic;
using UnityEngine;

public enum TypeMiniGame
{
    None = 0,
    // Thêm loại mini game mới ở đây, giá trị khớp với MiniGameRegister.ID
}

/// <summary>
/// Khung quản lý mini game (Controller trong MVC).
/// Các mini game cũ (ConnectBird, TripleBird) đã bị gỡ.
/// Mini game mới: kế thừa MiniGameRegister, kéo vào danh sách lsMiniGames, đặt ID tương ứng TypeMiniGame.
/// Remote config StringHelper.CURRENT_ID_MINI_GAME quyết định mini game nào đang chạy.
/// </summary>
public class MiniGameController : MonoBehaviour
{
    public static int Version_Except = 0;

    [SerializeField] private bool _isEnableMiniGame = false;
    public List<MiniGameRegister> lsMiniGames = new List<MiniGameRegister>();
    public int defaultIdMiniGame = 0;

    public bool IsEnableMiniGame => _isEnableMiniGame;

    public void Init()
    {
        if (!_isEnableMiniGame)
            return;
        var _miniGame = GetControllerMG();
        if (_miniGame != null)
            _miniGame.Init();
    }

    public void ResetDay()
    {
        if (!_isEnableMiniGame)
            return;
        var _miniGame = GetControllerMG();
        if (_miniGame != null)
            _miniGame.ResetDay();
    }

    public MiniGameRegister GetControllerMG()
    {
        int _currentId = RemoteConfigController.GetIntConfig(StringHelper.CURRENT_ID_MINI_GAME, defaultIdMiniGame);
        return GetControllerMG(_currentId);
    }

    public MiniGameRegister GetControllerMG(int idMiniGame)
    {
        foreach (var _miniGame in lsMiniGames)
        {
            if (_miniGame != null && _miniGame.ID == idMiniGame)
                return _miniGame;
        }
        return null;
    }

    public void OpenMiniGameBox()
    {
        if (!_isEnableMiniGame)
            return;
        var _miniGame = GetControllerMG();
        if (_miniGame != null)
            _miniGame.OpenBox();
    }

    public void OpenMiniGameBox(TypeMiniGame typeMiniGame)
    {
        if (!_isEnableMiniGame)
            return;
        var _miniGame = GetControllerMG((int)typeMiniGame);
        if (_miniGame != null)
            _miniGame.OpenBox();
    }
}
