using UnityEngine;

namespace Buzzy.Game
{
    /// <summary>
    /// Mọi số bố cục của màn chơi (đơn vị thế giới, trục: x phải, y lên, z xa camera). Không rải hằng số trong code.
    /// Mã trong Tooltip = dòng .game/research/visual-check.md mà số này phục vụ.
    /// </summary>
    [CreateAssetMenu(menuName = "Buzzy Bus/Layout Config", fileName = "BuzzyLayoutConfig")]
    public class BuzzyLayoutConfig : ScriptableObject
    {
        [Header("Khung ảnh chụp (V1, V2)")]
        [Tooltip("V2: tỉ lệ khung game 9:20")] public Vector2Int captureSize = new Vector2Int(1080, 2400);

        [Header("Camera (V1)")]
        [Tooltip("V1: nhìn gần thẳng từ trên (góp ý 01); vị trí chỉ cần cao hơn cảnh, ortho không phụ thuộc khoảng cách")] public Vector3 cameraPosition = new Vector3(0f, 30f, -9.64f);
        [Tooltip("V1: pitch 60° (góp ý 02: nghiêng về trước hơn 67°; mép dọc bảng vẫn song song nhờ ortho)")] public Vector3 cameraEuler = new Vector3(60f, 0f, 0f);
        [Tooltip("V1: orthographic = không phối cảnh (mẫu); false → dùng fieldOfView")] public bool cameraOrthographic = true;
        [Tooltip("V1: nửa chiều cao khung nhìn (thế giới) khi orthographic; 24 cột x 0.466 = 90 % bề ngang khung 9:20")] public float orthographicSize = 13.8f;
        [Tooltip("V1: chỉ dùng khi không orthographic")] public float fieldOfView = 64.4f;

        /// <summary>Đặt camera theo config (3 nơi dựng cảnh dùng chung: Bootstrap, StaticBuilder, MotionCapture).</summary>
        public void ApplyCamera(Camera cam, float aspect)
        {
            cam.transform.position = cameraPosition; cam.transform.eulerAngles = cameraEuler;
            cam.orthographic = cameraOrthographic; cam.orthographicSize = orthographicSize; cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 200f; cam.aspect = aspect;
        }

        /// <summary>Cỡ (rộng, cao) quad nền phủ hết khung nhìn ở khoảng cách dist trước camera.</summary>
        public Vector2 BackdropSize(Camera cam, float dist)
        {
            float h = cam.orthographic ? 2f * cam.orthographicSize : 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return new Vector2(h * cam.aspect * 1.2f, h * 1.2f);
        }

        [Header("Ảnh pixel (V19-V23)")]
        [Tooltip("V20: bước ô theo z (hàng)")] public float cellPitch = 0.495f;
        [Tooltip("V20: bước ô theo x (cột); 24 cột = 90 % bề ngang khung")] public float cellPitchX = 0.466f;
        [Tooltip("V21: tỉ lệ thu mesh Block_Mesh so với pitch (mesh thân ~1.05)")] public float blockScale = 0.39f;
        [Tooltip("V19: tâm ảnh theo trục x thế giới")] public float boardCenterX = 0f;
        [Tooltip("V19, V33: z tâm bảng pixel (mẫu: bảng cao hay thấp đều có tâm ở y ~58.5 %; Lv.2 20 hàng y 43–74 %, Lv.77 31 hàng y 36–82 %)")] public float boardCenterZ = 4.358f;
        /// <summary>z của hàng y=0 (thấp nhất, gần camera) cho bảng cao h hàng: tâm bảng cố định ở boardCenterZ.</summary>
        public float BoardZ0(int height) { return boardCenterZ - (height - 1) * 0.5f * cellPitch; }

        [Header("Làn đường + bến (V8, V9, V13, V14)")]
        [Tooltip("V8: z gần camera của đường")] public float roadNearZ = 14.03f;
        [Tooltip("V8: z xa camera của đường")] public float roadFarZ = 21.79f;
        [Tooltip("V9: z của vạch đứt")] public float dashZ = 17.963f;
        [Tooltip("V13: z tâm xe ở bến")] public float slotZ = 15.689f;
        [Tooltip("V13: x tâm 3 ô bến")] public float[] slotX = { -2.53f, 0.16f, 2.92f };
        [Tooltip("V13: góc xoay yaw xe ở bến (độ)")] public float slotYaw = 22f;
        [Tooltip("V14: tỉ lệ thu mesh Bus (rộng xe ~19 % khung)")] public float busScale = 0.9f;

        [Header("Hàng chờ (V10-V12)")]
        [Tooltip("V10: z tâm xe hàng chờ")] public float queueZ = 20.408f;
        [Tooltip("V10: x tâm 3 cột")] public float[] queueX = { -2.36f, 0f, 2.24f };
        [Tooltip("V11: tỉ lệ thu Bus ở hàng chờ")] public float queueBusScale = 0.95f;


        [Header("UI theo % khung game (V3-V4, V24-V27, U*)")]
        [Tooltip("V5: tâm badge Lv (x %, y % từ trên)")] public Vector2 badgeCenter = new Vector2(0.12f, 0.067f);
        [Tooltip("V5: bề ngang badge (% bề ngang khung)")] public float badgeWidth = 0.245f;
        [Tooltip("V3: tray trên cùng: cao % phần nhìn thấy")] public float trayVisibleHeight = 0.115f;
        [Tooltip("V4: dịch khay xuống (% chiều cao) để che mép trên xe hàng chờ")] public float trayShift = 0.025f;
        [Tooltip("V24: y thanh đáy (% từ trên)")] public float bottomBarTop = 0.89f;
        [Tooltip("V26: x tâm 4 booster (%)")] public float[] boosterX = { 0.136f, 0.334f, 0.532f, 0.729f };
        [Tooltip("V26: y tâm booster (%)")] public float boosterY = 0.92f;
        [Tooltip("V26: đường kính booster (% bề ngang)")] public float boosterSize = 0.174f;
        [Tooltip("V27: tâm nút cài đặt (x %, y %)")] public Vector2 settingsCenter = new Vector2(0.895f, 0.92f);
        [Tooltip("V27: đường kính nút cài đặt (% bề ngang)")] public float settingsSize = 0.11f;
        [Tooltip("V25: màu thanh đáy")] public Color bottomBarColor = new Color32(0x6B, 0x3A, 0xDA, 255);
        [Tooltip("V16: tâm bong bóng Full (x %, y %)")] public Vector2 fullBubbleCenter = new Vector2(0.20f, 0.268f);
        [Tooltip("V16: bề ngang bong bóng Full (% bề ngang)")] public float fullBubbleWidth = 0.085f;
        [Tooltip("V21: nhân chiều cao khối pixel (mẫu cao hơn rộng)")] public float blockHeightScale = 1.65f;
        [Tooltip("V20: nhân bề ngang khối pixel (mẫu: khối chạm nhau theo hàng ngang)")] public float blockWidthScale = 1.25f;
        [Tooltip("V9: nửa dài mỗi vạch đứt (thế giới)")] public float dashHalfLength = 0.29f;
        [Tooltip("V9: bước lặp vạch đứt theo x (thế giới)")] public float dashSpacing = 0.86f;
        [Tooltip("V9: nửa bề dày vạch đứt theo z (thế giới)")] public float dashHalfWidth = 0.04f;
        [Tooltip("V4: màu rim khay / bến")] public Color lipColor = new Color32(0x7C, 0x82, 0xD5, 255);
        [Tooltip("V12: chữ số chỗ trên nóc xe, cỡ")] public float roofNumberSize = 1.3f;


        [Header("Chuyển động (C*) - M2")]
        [Tooltip("C1: xe xuất hiện phía trên cách ô bến bao nhiêu z (hầm)")] public float enterFromZ = 16f;
        [Tooltip("C1: thời lượng xe lăn vào ô bến (s)")] public float enterDuration = 0.9f;
        [Tooltip("C1: trễ giữa các xe lúc vào màn (s)")] public float enterStagger = 0.12f;
        [Tooltip("C1: xe hàng chờ vào sau (s)")] public float queueEnterDelay = 0.5f;
        [Tooltip("C3: xe ở hàng chờ xuống ô trống (s)")] public float queueToSlotDuration = 0.8f;
        [Tooltip("C7: tốc độ xe rời bến (đơn vị/s)")] public float leaveSpeed = 22f;
        [Tooltip("C7: z của làn trên để xe chạy ngược ra")] public float leaveLaneZ = 17.963f;
        [Tooltip("C7: x mép phải/trái xe thoát ra")] public float leaveEdgeX = 12f;
        [Tooltip("C4: thời gian bay của 1 pixel (s)")] public float flightDuration = 1.4f;
        [Tooltip("C4: tổng thời gian xả cả cụm (s) cho 50 người")] public float flightSpread = 1.4f;
        [Tooltip("C4/C17: độ cao vòng cung (đám đông đi bộ: thấp, có nhảy)")] public float flightArc = 0.9f;
        [Tooltip("C5: pixel xuất hiện đầu level: tổng lan (s) và thời lượng mỗi khối (s)")] public Vector2 appearSpreadDuration = new Vector2(0.9f, 0.7f);
        [Tooltip("C9: trễ rào chắn sau khi xe rời (s)")] public float barrierDelay = 0.36f;
        [Tooltip("C13: bắt đầu cho chạm sau khi vào màn (s)")] public float inputDelay = 1.5f;

        [Header("Chuyển động hoãn từ M2 - làm ở M5 (V17, V18, V23, C3, C4, C13, C17, C18)")]
        [Tooltip("V18: tỉ lệ thu mesh Barrier (mẫu rào ~8 % bề ngang khung)")] public float barrierScale = 0.42f;
        [Tooltip("V17: vạch P ô trống: màu, cỡ khung đứt (dài theo z, rộng theo x, thế giới), nét, cỡ chữ P")] public Color pMarkColor = new Color32(0x9A, 0xA0, 0xC4, 255);
        public Vector2 pMarkSize = new Vector2(1.9f, 2.7f);
        public float pMarkDash = 0.32f;
        public float pMarkLine = 0.07f;
        public float pLetterSize = 2.2f;
        [Tooltip("C3/C4: vào màn: khung phóng to từ giữa (hệ số thu ban đầu, thời lượng s) + màn đỏ tan dần (màu+alpha, thời lượng s)")] public float introZoom = 4f;
        public float introZoomDuration = 0.8f;
        public Color introHazeColor = new Color(0.85f, 0.12f, 0.12f, 0.7f);
        public float introHazeDuration = 0.9f;
        [Tooltip("C17/C18: hành khách đi bộ thành đám: lắc ngang (thế giới), nhảy (cao, số bước), lệch tốc độ ±, xoay mặt theo hướng đi")] public float walkWobble = 0.45f;
        public float walkHop = 0.22f;
        public int walkHops = 3;
        public float walkSpeedJitter = 0.3f;
        [Tooltip("C13: xe rời bến: nhích lùi (thế giới) trong bao lâu (s), rồi dừng ngắn (phanh) trước khi rẽ")] public float leaveReverse = 0.35f;
        public float leaveReverseDuration = 0.22f;
        public float leaveBrakePause = 0.08f;
        [Tooltip("V23: cụm rời (không dính ảnh chính) bay lơ lửng: dịch xa camera (z), dịch vào tâm (số ô), biên độ + tốc độ nhấp nhô [thiết kế]")] public float looseLift = 0f;
        public float looseInward = 0.7f;
        public float looseBob = 0.9f;
        public float looseBobSpeed = 1.6f;

        [Header("Mechanic + booster - M3 (V27-V30, C23-C24, U7, [thiết kế] mục 4-12)")]
        [Tooltip("V28: màu khối băng (alpha = độ trong)")] public Color iceColor = new Color(0.62f, 0.9f, 1f, 0.8f);
        [Tooltip("U8: màu thanh đáy ở level khó / siêu khó (tag 1, 2)")] public Color hardBottomBarColor = new Color32(0xC8, 0x2B, 0x2B, 255);
        [Tooltip("U8: màu chữ Lv.N trên badge đầu lâu")] public Color hardBadgeTextColor = new Color32(0xB8, 0x24, 0x24, 255);
        [Tooltip("U7: cỡ nút tốc độ (rộng, cao % khung)")] public Vector2 speedPillSize = new Vector2(0.19f, 0.048f);
        [Tooltip("U8: đỉnh thanh đáy ở level khó (mẫu Lv.19: y 92.2 %), cỡ + dịch badge đầu lâu")] public float hardBottomBarTop = 0.922f;
        public float hardBadgeScale = 0.9f;
        public float hardBadgeDropY = 0.01f;
        [Tooltip("V28: nhân chiều cao khối băng so với mesh Ice")] public float iceYScale = 0.75f;
        [Tooltip("V28: cỡ chữ số trên khối băng")] public float iceNumberSize = 2.4f;
        [Tooltip("Walls: màu + chiều cao tường (thế giới)")] public Color wallColor = new Color32(0x5C, 0x60, 0xA8, 255);
        public float wallHeight = 1.0f;
        [Tooltip("Spawner: màu thân máy + chiều cao")] public Color spawnerColor = new Color32(0x9A, 0x6B, 0xE0, 255);
        public float spawnerHeight = 1.3f;
        [Tooltip("V29: màu + cỡ chìa khoá trên bảng")] public Color keyColor = new Color32(0xFF, 0xC8, 0x20, 255);
        public float keyScale = 2.2f;
        [Tooltip("V30: cỡ ổ khoá trên nóc xe bị xích")] public float lockScale = 1.6f;
        [Tooltip("Mystery: thời gian bật lộ màu (s)")] public float revealPopDuration = 0.35f;
        [Tooltip("V27: cấp mở khoá 4 booster (VIP Bus, Sky Grab, Magic Wand, Bus Claw)")] public int[] boosterUnlock = { 4, 8, 13, 16 };
        [Tooltip("Số booster mỗi loại lúc bắt đầu (mẫu Lv.19 / Lv.77: chấm đỏ số 1); kho xu ở M5")] public int boosterStartCount = 1;
        [Tooltip("U7: nút tốc độ x1/x2 — mở từ level, tâm (%), cỡ (% rộng)")] public int speedUnlockLevel = 3;
        public Vector2 speedButtonCenter = new Vector2(0.885f, 0.067f);
        public float speedButtonSize = 0.19f;
        [Tooltip("C23: hệ số tốc độ khi bật x2")] public float speedFast = 2f;
        [Tooltip("Bus Claw: độ cao nhấc xe (thế giới) + thời lượng (s)")] public float clawLift = 4f;
        public float clawDuration = 1.0f;
        [Tooltip("Sky Grab: thời gian trực thăng bay qua (s) và độ cao")] public float heliDuration = 1.6f;
        public float heliHeight = 6f;
        public float heliScale = 0.6f;
        [Tooltip("VIP Bus / pixel biến mất: thời lượng thu nhỏ (s)")] public float vanishDuration = 0.35f;

        [Header("Màn thắng + popup - M5 (V31, V32, U1-U6) [thiết kế: design-decisions 3, 7, 8]")]
        [Tooltip("V31: nền tối phủ màn")] public Color overlayColor = new Color32(0x12, 0x10, 0x1C, 255);
        public float overlayAlpha = 0.92f;
        [Tooltip("V31→U2: thời gian logo + pháo hoa trước popup (s)")] public float winSequenceDuration = 2.4f;
        [Tooltip("V31: logo giữa màn (bề ngang %, tâm y %)")] public float winLogoWidth = 0.66f;
        public float winLogoY = 0.415f;
        public Color logoPlateColor = new Color32(0x3B, 0x7B, 0xE0, 255);
        public float logoPlateAspect = 0.68f;
        public float logoLineGap = 0.05f;
        public int logoFontPx = 140;
        public Color logoTopColor = new Color32(0xFF, 0xC8, 0x20, 255);
        public Color logoBottomColor = new Color32(0xFF, 0xFF, 0xFF, 255);
        public Color textOutlineColor = new Color32(0x3A, 0x22, 0x66, 255);
        [Tooltip("V31: ruy băng tím trên + 2 bên; ribbonTopVisible = phần oval lộ dưới mép trên (tỉ lệ cao sprite)")] public float ribbonTopWidth = 1.1f;
        public float ribbonTopVisible = 0.12f;
        public float ribbonSideWidth = 0.20f;
        public float ribbonSideX = 0.09f;
        public float ribbonSideY = 0.10f;
        [Tooltip("V31: 4 loa tím hai góc dưới: tâm loa 1 bên trái (x, y %), lệch loa 2, bề ngang")] public Vector2 trumpetLeft = new Vector2(0.12f, 0.63f);
        public Vector2 trumpetOffset = new Vector2(-0.04f, 0.1f);
        public float trumpetWidth = 0.30f;
        [Tooltip("V31: pháo hoa: số tia mỗi chùm, thời lượng (s), bán kính (% rộng), cỡ tia")] public int fireworkSparks = 18;
        public float fireworkDuration = 0.9f;
        public float fireworkRadius = 0.28f;
        public float sparkWidth = 0.12f;
        [Tooltip("U2/U5: khung tím ngoài (bề ngang %, tỉ lệ cao/rộng, tâm y %); bảng kem trong (bề ngang, đỉnh + chiều cao theo tỉ lệ khung ngoài); tấm tiêu đề (bề ngang, tỉ lệ, dịch xuống)")] public float popupWidth = 0.86f;
        public float popupAspect = 1.36f;
        public float popupCenterY = 0.505f;
        public float popupInnerWidth = 0.76f;
        public float popupInnerTop = 0.22f;
        public float popupInnerHeightTall = 0.54f;
        public float popupInnerHeight = 0.44f;
        public float popupTitleWidth = 0.60f;
        public float popupTitleAspect = 0.30f;
        public float popupTitleDrop = 0.035f;
        public float popupButtonWideWidth = 0.44f;
        [Tooltip("U3: nút = sprite vuông kéo thành viên thuốc (cao/rộng)")] public float popupButtonAspect = 0.55f;
        public Color popupTextColor = new Color32(0x5A, 0x3E, 0x9E, 255);
        [Tooltip("U2: hàng New Feature / Your Reward / thanh nhân (tỉ lệ chiều cao khung từ đỉnh); nút cách đáy")] public float featureRowY = 0.37f;
        public float rewardRowY = 0.47f;
        public float sliderRowY = 0.66f;
        public float buttonRowUp = 0.13f;
        public float featureBarWidth = 0.50f;
        public Color featureFillColor = new Color32(0x6A, 0xD0, 0x2A, 255);
        public float coinPileWidth = 0.28f;
        public float sliderWidth = 0.60f;
        public Color sliderMarkColor = new Color(1f, 1f, 1f, 0.45f);
        [Tooltip("U3: nút xu / AD: bề ngang %, lệch khỏi tâm %")] public float popupButtonWidth = 0.32f;
        public float popupButtonGap = 0.185f;
        public float closeButtonSize = 0.10f;
        [Tooltip("U4: thanh xu trên cùng + tim Full")] public Vector2 currencyBarCenter = new Vector2(0.09f, 0.10f);
        public float currencyBarWidth = 0.22f;
        public Vector2 heartCenter = new Vector2(0.27f, 0.10f);
        public float heartWidth = 0.09f;
        [Tooltip("Thua: trễ sau bế tắc tới popup (s); cỡ hình minh hoạ")] public float loseDelay = 0.8f;
        public float loseIllusWidth = 0.44f;
        [Tooltip("Cài đặt / shop: hàng đầu (tỉ lệ cao khung), bước hàng, cỡ nút gạt")] public float settingsFirstRowY = 0.3f;
        public float settingsRowStep = 0.13f;
        public float toggleWidth = 0.15f;
        public float shopFirstRowY = 0.30f;
        public float shopRowStep = 0.10f;
        [Tooltip("Shop [thiết kế]: gói xu + giá hiển thị (IAP nối ở M7), xu xem quảng cáo")] public int[] shopPackCoins = { 200, 1000, 3000 };
        public string[] shopPackPrice = { "$0.99", "$3.99", "$9.99" };
        public int shopAdCoins = 50;
        [Tooltip("U1: màn loading: thời lượng (s), thanh (bề ngang %, y %), logo y %")] public float loadingDuration = 2.5f;
        public float loadingBarWidth = 0.70f;
        public float loadingBarY = 0.835f;
        public float loadingBarAspect = 0.11f;
        public float loadingLogoY = 0.25f;
        public float loadingLogoWidth = 0.66f;

        [Header("Meta - M5 [thiết kế] (design-decisions 7, 14; video V4: 640 → 720 xu)")]
        public int startCoins = 600;
        public int winCoins = 40;
        public int[] adMultipliers = { 2, 3, 4, 3, 2 };
        public float sliderStepSeconds = 0.35f;
        [Tooltip("BoosterConfig: CoinPrice, CoinRewardQuantity 3, AdsRewardQuantity 1; quà mở khoá 1")] public int[] boosterPrice = { 500, 700, 300, 600 };
        public int boosterBuyQty = 3;
        public int boosterAdQty = 1;
        public int boosterUnlockGift = 1;
        [Tooltip("FeatureUnlockConfig (_showVictoryProgress, trong phạm vi): mốc + tên hiện ở New Feature")] public int[] featureLevels = { 6, 10, 40 };
        public string[] featureNames = { "Hidden", "Frozen Pixel", "Locked Bus" };
        public int maxLevel = 300;

        public Buzzy.Core.MetaConfig ToMetaConfig()
        {
            return new Buzzy.Core.MetaConfig
            {
                StartCoins = startCoins, WinCoins = winCoins, AdMultipliers = adMultipliers, SliderStepSeconds = sliderStepSeconds,
                BoosterUnlock = boosterUnlock, BoosterPrice = boosterPrice, BoosterBuyQty = boosterBuyQty, BoosterAdQty = boosterAdQty, BoosterUnlockGift = boosterUnlockGift,
                FeatureLevels = featureLevels, FeatureNames = featureNames, MaxLevel = maxLevel
            };
        }

        [Header("Màu nền (V3, V4, V8)")]
        public Color backgroundTop = new Color32(0xA7, 0xAC, 0xE6, 255);
        [Tooltip("V3: màu đáy nền (chuyển sắc dọc)")] public Color backgroundBottom = new Color32(0x87, 0x8C, 0xC6, 255);
        public Color roadColor = new Color32(0x44, 0x4A, 0x6B, 255);
        public Color dashColor = new Color32(0x8A, 0x90, 0xB4, 255);
        public Color trayColor = new Color32(0x7C, 0x82, 0xD5, 255);
    }
}
