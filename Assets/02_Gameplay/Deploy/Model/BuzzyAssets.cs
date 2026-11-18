using UnityEngine;

namespace Buzzy.Game
{
    /// <summary>Tham chiếu asset dùng khi chạy (mesh, texture, font, sprite UI) để View không phụ thuộc AssetDatabase.</summary>
    [CreateAssetMenu(menuName = "Buzzy Bus/Assets", fileName = "BuzzyAssets")]
    public class BuzzyAssets : ScriptableObject
    {
        public Mesh busMesh, blockMesh, barrierMesh;
        public Texture2D busTexture;
        public Font font;
        public Sprite tunnel, levelBar, boosterCircle, lockIcon, settings, knob;
    }
}
