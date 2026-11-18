using UnityEngine;

namespace Buzzy.Game
{
    /// <summary>Tham chiếu asset dùng khi chạy (mesh, texture, font, sprite UI) để View không phụ thuộc AssetDatabase.</summary>
    [CreateAssetMenu(menuName = "Buzzy Bus/Assets", fileName = "BuzzyAssets")]
    public class BuzzyAssets : ScriptableObject
    {
        public Mesh busMesh, blockMesh, barrierMesh, iceMesh, keyMesh, lockMesh, heliMesh, heliWingMesh, clawMesh;
        public Texture2D busTexture;
        public Font font;
        public Sprite tunnel, levelBar, boosterCircle, lockIcon, settings, knob;
        /// <summary>Icon 4 booster: VIP Bus, Sky Grab, Magic Wand, Bus Claw.</summary>
        public Sprite[] boosterIcons;
        public Sprite countBadge, speedIcon, speedGreen, speedPurple;
        /// <summary>Badge level khó / siêu khó (đầu lâu) và nửa nút tốc độ x1 / x2 + viền.</summary>
        public Sprite pauseIcon, levelBarHard, levelBarSuper, speedHalfGreen, speedHalfPurple, speedHalfRim;
    }
}
