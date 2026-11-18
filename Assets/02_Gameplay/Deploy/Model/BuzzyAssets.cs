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
        /// <summary>M5: màn thắng (ruy băng, loa, tia pháo hoa, tấm logo), popup (khung, tấm tiêu đề, nút, xu, AD, tim), cài đặt, thua, loading.</summary>
        public Sprite ribbonTop, ribbonSide, trumpetLeft, trumpetRight, spark, logoPlate;
        public Sprite framePopup, frameOuter, titlePlate, titleYellow, titlePurple, titleRed, buttonGreen, buttonOrange, closeButton, whiteFrame;
        public Sprite currencyBar, coinIcon, coinPile, coinSmall, adIcon, heartInfinity, rewardSlider, featureTrack;
        public Sprite soundIcon, musicIcon, hapticIcon, toggleOn, toggleOff, loseIllustration, busLoading;
        /// <summary>Icon tính năng kế (song song featureNames: Hidden, Frozen Pixel, Locked Bus).</summary>
        public Sprite[] featureIcons;
        public Texture2D loadingBackground;
    }
}
