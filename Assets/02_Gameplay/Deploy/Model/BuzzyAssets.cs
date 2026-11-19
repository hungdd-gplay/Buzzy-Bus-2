using UnityEngine;

namespace Buzzy.Game
{
    /// <summary>Tham chiếu asset dùng khi chạy (mesh, texture, font, sprite UI) để View không phụ thuộc AssetDatabase.</summary>
    [CreateAssetMenu(menuName = "Buzzy Bus/Assets", fileName = "BuzzyAssets")]
    public class BuzzyAssets : ScriptableObject
    {
        public Mesh busMesh, blockMesh, barrierMesh, iceMesh, keyMesh, lockMesh, heliMesh, heliWingMesh, clawMesh;
        public Mesh fullSignMesh;   // góp ý 14: biển Full 3D (Sign_Full.obj) đuôi chạm đất
        public Mesh poseIdle, poseIdleB, poseRunA, poseRunB, poseJump, poseActive;   // M8 (góp ý 12): 7 pose cùng topology của nhân vật (Block_*.obj) → đổi mesh theo pha khi đi bộ
        public Texture2D colorTexture;   // atlas màu gốc (Color.png); lưu ý: UV của Barrier / Sign_Full không trỏ vào bảng màu (đo 2026-11-19), hiện chưa mesh nào dùng
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
        /// <summary>V14 (góp ý 10): bóng đổ mềm dưới xe (Shadow.png gốc, tím nhạt alpha).</summary>
        public Texture2D shadowTexture;
        /// <summary>M7 âm thanh (Full_APK_Export/Audio → Res/Buzzy/Audio; ánh xạ sự kiện [thiết kế] design-decisions 17).</summary>
        public AudioClip sfxTap, sfxPop, sfxWrong, sfxWin, sfxLose, sfxButton, sfxPositive, sfxBubble, sfxSwipe, sfxReward, sfxIntroduce, sfxChangeFalling, sfxBusLeave;
        public AudioClip sfxIceInit, sfxIceBreak, sfxIceBreak2, sfxVip, sfxHeli, sfxClawStart, sfxClawEnd, sfxShuffle;
        /// <summary>Nhạc nền (bg_1, bg_2); phát [0] lặp.</summary>
        public AudioClip[] music;
    }
}
