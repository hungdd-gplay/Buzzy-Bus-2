#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu tạo nhanh thành phần UI có sẵn âm thanh.
///  - GameObject > Base Project > UI > Button (Click Sound)
///  - Chuột phải component Button > Add Click Sound
///  - Tools > Base Project > Sound > Add Click Sound To Selected
/// </summary>
public static class SoundMenuItems
{
    private const string PATH_MENU_TOOL = "Tools/Base Project/Sound/";

    [MenuItem("GameObject/Base Project/UI/Button (Click Sound)", false, 10)]
    private static void CreateButtonWithSound(MenuCommand menuCommand)
    {
        var _parent = _GetParentForUI(menuCommand.context as GameObject);

        var _goButton = new GameObject("Btn_New", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIButtonSound));
        GameObjectUtility.SetParentAndAlign(_goButton, _parent);
        var _rectButton = _goButton.GetComponent<RectTransform>();
        _rectButton.sizeDelta = new Vector2(260f, 90f);

        var _goText = new GameObject("Txt_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        GameObjectUtility.SetParentAndAlign(_goText, _goButton);
        var _rectText = _goText.GetComponent<RectTransform>();
        _rectText.anchorMin = Vector2.zero;
        _rectText.anchorMax = Vector2.one;
        _rectText.offsetMin = Vector2.zero;
        _rectText.offsetMax = Vector2.zero;
        var _text = _goText.GetComponent<TextMeshProUGUI>();
        _text.text = "Button";
        _text.alignment = TextAlignmentOptions.Center;
        _text.color = Color.black;

        Undo.RegisterCreatedObjectUndo(_goButton, "Create Button (Click Sound)");
        Selection.activeGameObject = _goButton;
    }

    [MenuItem("CONTEXT/Button/Add Click Sound")]
    private static void AddClickSoundToButton(MenuCommand menuCommand)
    {
        var _button = menuCommand.context as Button;
        if (_button != null && _button.GetComponent<UIButtonSound>() == null)
            Undo.AddComponent<UIButtonSound>(_button.gameObject);
    }

    [MenuItem(PATH_MENU_TOOL + "Add Click Sound To Selected (kể cả con)")]
    private static void AddClickSoundToSelected()
    {
        int _countAdded = 0;
        foreach (var _goSelected in Selection.gameObjects)
        {
            foreach (var _button in _goSelected.GetComponentsInChildren<Button>(true))
            {
                if (_button.GetComponent<UIButtonSound>() != null)
                    continue;
                Undo.AddComponent<UIButtonSound>(_button.gameObject);
                _countAdded++;
            }
        }
        Debug.Log($"[Sound] Đã thêm UIButtonSound cho {_countAdded} Button.");
    }

    [MenuItem(PATH_MENU_TOOL + "Add Click Sound To Selected (kể cả con)", true)]
    private static bool ValidateAddClickSoundToSelected()
    {
        return Selection.gameObjects.Length > 0;
    }

    private static GameObject _GetParentForUI(GameObject goContext)
    {
        if (goContext != null && goContext.GetComponentInParent<Canvas>() != null)
            return goContext;
        var _canvas = Object.FindFirstObjectByType<Canvas>();
        if (_canvas != null)
            return _canvas.gameObject;

        var _goCanvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _goCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        Undo.RegisterCreatedObjectUndo(_goCanvas, "Create Canvas");
        return _goCanvas;
    }
}
#endif
