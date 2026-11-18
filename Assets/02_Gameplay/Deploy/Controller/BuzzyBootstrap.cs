using UnityEngine;

namespace Buzzy.Game
{
    /// <summary>Khởi động màn chơi trong Play Mode: dựng camera + BuzzyGameView từ database / config / assets.</summary>
    public class BuzzyBootstrap : MonoBehaviour
    {
        public BuzzyLevelDatabase database;
        public BuzzyLayoutConfig config;
        public BuzzyAssets assets;
        public int level = 2;
        [Range(0.25f, 4f)] public float timeScale = 1f;

        void Start()
        {
            var lv = database.Get(level);
            var camGo = new GameObject("Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = config.backgroundTop;
            cam.transform.position = config.cameraPosition; cam.transform.eulerAngles = config.cameraEuler;
            cam.fieldOfView = config.fieldOfView; cam.nearClipPlane = 0.3f; cam.farClipPlane = 200f;
            float aspect = (float)Screen.width / Screen.height;
            cam.aspect = aspect;
            var size = new Vector2Int(Screen.width, Screen.height);
            var view = gameObject.AddComponent<BuzzyGameView>();
            view.TimeScale = timeScale;
            view.Vibrate = () =>
            {
#if UNITY_ANDROID || UNITY_IOS
                Handheld.Vibrate();
#endif
            };
            view.Init(lv, config, assets, cam, size);
        }
    }
}
