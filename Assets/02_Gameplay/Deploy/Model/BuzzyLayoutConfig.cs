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
        [Tooltip("V14 (góp ý 10): bóng đổ mềm dưới xe (Shadow.png): lệch (x, z thế giới; phải-dưới), cỡ (rộng, dài thế giới, theo cỡ xe ở bến), độ đậm")] public Vector2 carShadowOffset = new Vector2(0.1f, -0.15f);   // 3c đo đợt 3: lệch phải-dưới ít hơn + quad to hơn 10 % để có viền mỏng bên trái / dưới đuôi như mẫu
        public Vector2 carShadowSize = new Vector2(2.15f, 3.4f);   // ≈ đáy xe + 20 % (mesh 1,7 × 2,8)
        public float carShadowAlpha = 0.55f;   // 3c đo: mẫu tối hơn đường ~35 %
        [Tooltip("V14: phần mép mờ của bóng (tỉ lệ nửa cạnh)")] public float carShadowSoft = 0.35f;
        [Tooltip("V14: màu nhân vào Shadow.png (texture gốc sáng hơn đường; mẫu bóng tối hơn đường ~30 %)")] public Color carShadowColor = new Color(0.05f, 0.05f, 0.12f, 1f);

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
        [Tooltip("V16 (góp ý 14, sbs_full_zoom.jpg): biển Full 3D Sign_Full.obj: tỉ lệ (đĩa rộng 1,58 đv × 0,65 ≈ 1,03 đv ≈ 8,3 % khung), lệch (x = sang trái khung, y = về phía mũi) so với tâm ô: góc trước-trái, đuôi chạm đất đè vạch dưới ô; y cục bộ tâm đĩa để đặt chữ")] public float fullSignScale = 0.73f;   // r10 (3c đo, asset 0,73): đĩa trong 8,5 % khung ≈ golden 8,3 → giữ
        public Vector2 fullSignOffset = new Vector2(1.1f, 1.4f);   // r7: 0,55 làm đĩa chui vào thân xe → 1,1 ra ngoài hông trái; r10 (asset 1,0): đáy 26,0 % vs golden 27,9 % → về phía mũi thêm 0,6
        [Tooltip("V16 (góp ý 14): lún đuôi xuống đất theo trục biển (đv) để chân thành đuôi giọt ngắn ≈ 0,3 đường kính và tâm đĩa hạ (r10: 23,3 % → golden 26,5 %)")] public float fullSignSink = 0.32f;   // r11: 1,6 / 0,4 ra tâm 27,0 % đáy 29,4 % (golden 26,5 / 27,9) → nhích lên
        public float fullSignDiscY = 1.1f;
        public Color fullSignColor = new Color32(0xBF, 0xC4, 0xF0, 255);   // mẫu: lòng lavender
        [Tooltip("V16: lớp 2 = bản sao biển to hơn (tỉ lệ) màu sáng hơn đặt sau → viền sáng ~8 % đường kính")] public float fullSignRimScale = 1.08f;
        public Color fullSignRimColor = new Color32(0xE6, 0xE9, 0xFF, 255);
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
        [Tooltip("C7: tốc độ xe rời bến đoạn chạy ra + quay đầu (đơn vị/s; clip ~13)")] public float leaveSpeed = 22f;   // = asset (44 quyết 2026-11-19: giá trị chạy thật, C13–C16 đo đạt trên 22)
        [Tooltip("C7: z của làn trên để xe chạy ngược ra")] public float leaveLaneZ = 17.963f;
        [Tooltip("C7: x mép phải/trái xe thoát ra")] public float leaveEdgeX = 12f;
        [Tooltip("B10 (góp ý 11): làn trên chỉ 1 xe một lúc; xe rời ở trong dải |z - leaveLaneZ| < laneClearZ còn chiếm làn")] public float laneClearZ = 1.5f;
        [Tooltip("C4: thời gian bay của 1 pixel (s)")] public float flightDuration = 1.4f;
        [Tooltip("C4: tổng thời gian xả cả cụm (s) cho 50 người")] public float flightSpread = 1.4f;
        [Tooltip("C4/C17: độ cao vòng cung (đám đông đi bộ: thấp, có nhảy)")] public float flightArc = 0.9f;
        [Tooltip("C5: pixel xuất hiện đầu level: tổng lan (s) và thời lượng mỗi khối (s)")] public Vector2 appearSpreadDuration = new Vector2(0.9f, 0.7f);
        [Tooltip("C9: trễ rào chắn sau khi xe rời (s); góp ý 16 (3c đo clip Lv.1): 0,2 — trường cũ trong asset → đổi qua Sync")] public float barrierDelay = 0.2f;
        [Tooltip("J7 (góp ý 16, 3c đo clip Lv.1 t=26,63 / 29,67): rào xuất hiện: tổng tới ổn định (s), rơi chạm đất sau (s), độ cao rơi (đv ≈ 0,7 cao rào 1,06), cỡ lúc đầu (0,1–0,5), nở quá (1,10–1,15 → 0,15), nẩy 1 lần cao (đv ≈ 25 % cao rào)")] public float barrierAppearDuration = 0.45f;
        public float barrierDropDuration = 0.10f;
        public float barrierDropHeight = 1.6f;   // tâm xuất phát cao hơn chỗ đứng 2,9 % khung (clip 21 px/720): camera pitch 60° chiếu trục Y còn 0,5 → 0,029 × 27,6 / 0,5
        public float barrierStartScale = 0.2f;
        public float barrierOvershoot = 0.12f;   // nở ngang (clip 1,10–1,15); cao chỉ ép 0,3 × số này
        public float barrierBounceHeight = 0.6f;   // nẩy 1 lần 8 px/720 = 1,1 % khung → 0,011 × 27,6 / 0,5
        [Tooltip("C13: bắt đầu cho chạm sau khi vào màn (s)")] public float inputDelay = 1.5f;

        [Header("Chuyển động hoãn từ M2 - làm ở M5 (V17, V18, V23, C3, C4, C13, C17, C18)")]
        [Tooltip("V18 (góp ý 14, sbs_barrier_zoom.jpg): rào đường 3D Barrier.obj (thanh đỏ sọc trắng + 4 chân, rộng 1,78 đv): tỉ lệ → rộng ≈ 1,3 đv ≈ 60 % bề rộng ô 1,9 / ≈ 11 % khung; lùi về mép dưới ô theo hướng mũi (ô dài 2,7 → 1,2); xoay thêm so với góc ô (độ)")] public float barrierScale = 0.9f;   // r9c (asset đã đồng bộ): 1,0 → phần đỏ 84 px/648, golden 76 → 0,9; LƯU Ý đổi số này phải chạy Tools/Buzzy Bus/Sync Config Defaults (asset M5 giữ 0,42)
        public float barrierForward = 1.2f;
        public Color barrierRed = new Color32(0xE8, 0x2B, 0x2B, 255);
        public Color barrierWhite = new Color32(0xF4, 0xF4, 0xF8, 255);
        [Tooltip("V18: chu kỳ sọc chéo (px trên texture 64): nhỏ → nhiều sọc; mẫu 2 sọc trắng trên thanh")] public int barrierStripePeriod = 6;
        public float barrierYawOffset = 0f;
        [Tooltip("V18: nghiêng rào quanh trục x (độ); góp ý 14: mesh Barrier.obj vốn đã đứng (probe14), giữ 0")] public float barrierTilt = 0f;
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
        public float loadingBarAspect = 0.16f;   // = asset (44 quyết: U-row M5 đã duyệt bằng mắt trên 0,16)
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

        [Header("Luật mechanic [thiết kế] M6 (design-decisions mục 15; README_Assumptions 12, 14)")]
        [Tooltip("Băng: RequiredHits chia hệ số này = số pixel cần lấy để vỡ (1 = số hiện trên băng đúng dữ liệu gốc)")] public float frozenHitDivisor = 1f;
        [Tooltip("Băng: chỉ pixel trong tầm này (ô) quanh vùng mới tính; âm = pixel ở đâu cũng tính")] public int frozenReach = -1;
        [Tooltip("Mystery: true = pixel ẩn không chạm được tới khi lộ; false = chạm được, chỉ giấu màu")] public bool mysteryBlocks = false;
        [Tooltip("Xe khoá: true = vào bến đứng chờ chìa; false = đứng đầu cột, ô bến trống")] public bool lockedEntersSlot = false;
        [Tooltip("Chìa: tỉ lệ pixel vòng quanh phải bị lấy để nhặt")] public float keyClearFraction = 0.5f;
        [Tooltip("Sky Grab: số pixel nhấc mỗi lần")] public int skyGrabCount = 10;
        [Tooltip("M7 góp ý 04: true = luật 1a (chỉ pixel có đường ra ngoài mới lấy khi chạm; 76/233 level bế tắc bắt buộc); false = 1b (chọn hết màu, view cho pixel trong chờ tới khi có đường)")] public bool exposedOnly = true;
        [Tooltip("[thiết kế] (góp ý 13, 44 quyết): nhận chạm màu có xe cùng màu ĐANG XUỐNG bến (clip 3/3 ca chạm khi xe đã đỗ, không có ca ngược lại → chưa xác nhận); tắt = chỉ xe đã đỗ xong mới chạm được")] public bool tapWhileArriving = true;
        [Tooltip("M7 luật 1c (chờ người dùng chốt): true = trong cùng lớp, pixel gần điểm chạm đi trước; false = gần ô bến đi trước")] public bool orderFromTapPoint = true;

        public Buzzy.Core.MechanicRules ToMechanicRules()
        {
            return new Buzzy.Core.MechanicRules
            {
                FrozenHitDivisor = frozenHitDivisor, FrozenReach = frozenReach, MysteryBlocks = mysteryBlocks,
                LockedEntersSlot = lockedEntersSlot, KeyClearFraction = keyClearFraction, SkyGrabCount = skyGrabCount, ExposedOnly = exposedOnly, OrderFromTapPoint = orderFromTapPoint, TapWhileArriving = tapWhileArriving
            };
        }

        [Header("Cảm giác chơi (feel) - M7 góp ý 03 [thiết kế] (visual-check mục G: A* âm, J* phản hồi, T* bảng thử)")]
        [Tooltip("A1: âm lượng hiệu ứng chung; A2: nhạc nền (bg_1 lặp)")] public float sfxVolume = 1f;
        public float musicVolume = 0.45f;
        [Tooltip("A3: pop mỗi khách lên xe: âm lượng, cao độ đầu, bước tăng mỗi khách, số bước tối đa, khoảng cách tối thiểu 2 pop (s), nghỉ bao lâu thì cao độ về đầu (s)")] public float popVolume = 0.55f;
        public float popPitchBase = 0.95f;
        public float popPitchStep = 0.025f;
        public int popPitchSteps = 24;
        public float popMinInterval = 0.045f;
        public float popResetGap = 0.6f;
        [Tooltip("A4: âm lượng tiếng chạm cụm (tap_passenger) và chạm sai (choose_wrong)")] public float tapVolume = 0.8f;
        public float wrongVolume = 0.9f;
        [Tooltip("A5: rung máy khi chạm đúng (ngoài rung chạm sai C21); tắt vì Handheld.Vibrate dài")] public bool hapticOnTap = false;
        [Tooltip("J1: cụm được chọn nhấc lên (thế giới) trong bao lâu (s) — chỉ còn cho Sky Grab; góp ý 04: 0 (hành khách đứng yên tới lượt đi)")] public float pickLift = 0f;
        public float pickLiftDuration = 0.12f;
        [Tooltip("J2: xe lắc khi khách lên: ép cỡ (tỉ lệ), chúi mũi (độ, gốc _boardRockPitchAngle 10), thời lượng (s), cách nhau tối thiểu (s) (góp ý 07: chậm, thưa; đợt 2: thấy được ở 20 fps); J3: biên độ khi đỗ xong")] public float carBounce = 0.03f;
        public float boardRockPitch = 8f;
        [Tooltip("J2 đợt 3: nhấc cả xe lên (đơn vị thế giới) mỗi nhịp khách lên; 0,12 ≈ 5 px / 648 ≈ 7 % cao xe (3c: cần ≥ 5 %)")] public float carBounceLift = 0.12f;
        public float carBounceDuration = 0.4f;
        public float carBounceMinInterval = 0.3f;
        public float parkBounce = 0.14f;
        [Tooltip("J4: số trên nóc bật to khi đổi: hệ số, thời lượng (s)")] public float numberPop = 0.5f;
        public float numberPopDuration = 0.18f;
        [Tooltip("J5: xe vừa đủ chỗ lắc yaw: độ, số lần, thời lượng (s)")] public float fullWiggle = 6f;
        public int fullWiggleCycles = 2;
        public float fullWiggleDuration = 0.4f;
        [Tooltip("J6: chạm sai: cụm màu đó rung ngang (thế giới), số lần, thời lượng (s)")] public float wrongShake = 0.12f;
        public int wrongShakeCycles = 3;
        public float wrongShakeDuration = 0.3f;
        [Tooltip("T1: bảng thử nghiệm Editor giữa đáy bảng và thanh booster (tránh xe hàng chờ): y tâm (%), cỡ nút (% bề ngang), x tâm 4 nút (<, Lv, >, R)")] public float devPanelY = 0.85f;
        public float devButtonSize = 0.075f;
        public float[] devPanelX = { 0.05f, 0.16f, 0.27f, 0.365f };

        [Header("Đi bộ + đường xe - M7 góp ý 04 (W*, visual-check mục G; config gốc PassengerEntity / CarEntity)")]
        [Tooltip("W1: tốc độ đi bộ (đơn vị thế giới / s; gốc _moveToCarSpeed 6 theo ô của họ), lệch ± (gốc _speedVariance 0.3, giảm để không vượt nhau)")] public float walkSpeed = 7.5f;   // clip Lv.1 ~6 đv/s theo ô của họ; đợt 3: người đầu phải lên xe ≤ 1,5 s sau chạm (đo 2,2 s) → nhanh hơn 25 %
        public float walkSpeedVariance = 0.2f;   // clip: túm dãn co (gốc 0.3)
        [Tooltip("W2 (walk-compare-fb08): đi thành TÚM 2–4 người sát nhau (trong túm cách walkGroupGap s), giữa các túm cách walkGap s; số làn, bước làn (× walkMinDist)")] public float walkGap = 0.06f;   // C8 đợt 3 (3c): xả hết 50 người ≤ 1,5 s như clip: 17 túm × 0,06 + 50 × 0,01 ≈ 1,5 s (trước 3,33 s)
        public int walkGroupMin = 2;
        public int walkGroupMax = 4;
        public float walkGroupGap = 0.01f;
        public int walkLanes = 3;
        public float walkLaneSpacing = 1.0f;
        public float walkLaneOffset = 0.5f;   // (không dùng từ góp ý 05)
        [Tooltip("W3: bước chân: quãng 1 nhịp (thế giới), nhún cao, lắc mặt (độ)")] public float walkStride = 0.9f;
        public float walkBobHeight = 0.12f;
        public float walkSway = 12f;
        [Tooltip("W4 / C17 (góp ý 15, [người dùng chọn] 19/11, ghi đè clip 6/7 ca hông trái khung của góp ý 13): lên xe ở phía CỬA = hông trái theo chiều xe chạy = hông PHẢI KHUNG HÌNH (-1; +1 = trái khung), cửa GẦN ĐẦU XE (boardFrontOffset dương = về mũi), cách tâm xe, độ cao cửa; mọi ô bến như nhau. Trường đã có trong asset → đổi qua Sync Config Defaults")] public int boardSide = -1;
        public float boardSideOffset = 0.85f;
        public float boardFrontOffset = 0.5f;
        public float boardHeight = 0.6f;
        public float boardPointOffset = 1.6f;
        [Tooltip("W7 (góp ý 13, fb13 l2_wait_12.4-15.6.jpg): xe chưa đỗ → chờ ở PHÍA BẢNG của mép lề (waitLineZ = roadNearZ), hàng đầu cách mép waitLineOffset ô (0–0,3), dàn ngang tối đa waitSpread ô (9–11) tâm = ô bến đích, cột = sĩ số / waitRows (2–3); xe đỗ xong mới băng qua đường tới hông. gatherMode 0 = chờ cạnh xe như góp ý 08")] public int gatherMode = 1;
        public float waitLineZ = 14.03f;
        public float waitLineOffset = 0.2f;
        public float waitSpread = 10f;
        public int waitRows = 3;
        [Tooltip("W7 (góp ý 13, clip Lv.2 16,0–16,8 / 20,0–20,6): đứng ở lề khi còn xe nào đang chuyển động trong bến (vào / lùi / chạy làn), dù xe của mình đã đỗ; tắt = chỉ chờ xe mình")] public bool waitWhileBayBusy = true;
        public float boardJumpHeight = 1.0f;
        public float boardJumpDuration = 0.22f;   // đợt 3: nhảy nhanh hơn để nhịp lên xe ~17 người / s
        [Tooltip("W5: pixel kẹt hẳn (bị màu khác bao kín): chờ (s) rồi nhảy qua, độ cao nhảy (gốc _stragglerMigrationDelay 0.6)")] public float stragglerDelay = 1.0f;
        public float stragglerHopHeight = 2.0f;
        [Tooltip("W1 (góp ý 09): cỡ người khi đi so với khối đứng: 1 = giữ nguyên tới lúc nhảy vào xe; bán kính không lấn vào khối đứng (thế giới)")] public float walkScale = 1f;
        public float walkObstacleRadius = 0.22f;
        [Tooltip("W6: lề lưới tìm đường quanh bảng (ô); W3: phạt ô kề khối khi tìm đường (ô cách 2 phạt một nửa) để đi cách rìa 1–2 ô như clip")] public int walkGridMargin = 3;
        public float walkEdgePenalty = 3f;
        [Tooltip("C15/C1 (góp ý 07, gốc CarEntity): xe chạy tới bến: gia tốc, tốc độ tối đa, quãng phanh cuối, tốc độ lúc tới; đường cong (tỉ lệ tay cầm Bezier); nghiêng khi cua: hệ số (độ nghiêng / độ-mỗi-giây đổi hướng), tối đa (độ), độ mượt; ngửa khi tăng tốc / chúi khi phanh (độ)")] public float travelAcceleration = 30f;
        public float travelMaxSpeed = 20f;
        public float travelArrivalBrakeDistance = 2.5f;
        public float travelMinArrivalSpeed = 0.6f;
        public float travelCurveHandle = 0.4f;
        public float travelBankStrength = 0.06f;
        public float travelMaxBank = 18f;
        public float travelBankSmoothing = 10f;
        public float travelAccelPitch = 4f;
        public float travelBrakePitch = 8f;
        [Tooltip("W7 (góp ý 05): khoảng cách tối thiểu giữa 2 người (thế giới; khối rộng ~0.49), lực đẩy nhau (gốc _separationStrength 1.5), tốc độ tự về làn")] public float walkMinDist = 0.45f;   // W2: túm sát nhau, chạm mép nhưng không chồng tâm (khối rộng 0,49)
        public float walkSeparation = 8f;   // (không dùng từ góp ý 06, thay bằng chiếu)
        public float walkSeparationRelax = 0.6f;
        [Tooltip("W7 (góp ý 06): số vòng giải va chạm mỗi khung (gốc _crowdSolverIterations 3); đội hình chờ trước cửa: số hàng; lên xe cách nhau (s)")] public int walkSolverIterations = 4;
        public int walkQueueRows = 3;
        [Tooltip("W5 (clip 15–25 người / s): số người được nhảy lên xe cùng lúc, cách nhau (s)")] public float boardInterval = 0.02f;   // đợt 3 (3c đo 9,4 / s, clip 17 / s)
        public int boardParallel = 16;
        [Tooltip("W2: hệ số đi chậm khi có người ngay trước mặt (0 = tắt; đợt 2 tắt vì làm cả dòng bò)")] public float walkNoOvertake = 0f;
        [Tooltip("W7: hệ số nhân chiều sâu (z) khi đo khoảng cách (camera nghiêng 60°: khối sau bị che nếu chỉ cách 1 khối)")] public float walkDepthScale = 1.0f;   // clip: cho phép chồng lấn nhẹ theo chiều sâu
        [Tooltip("C13 (góp ý 05): xe rời LÙI vòng cung (đít quét sang phải, bán kính = leaveLaneZ - slotZ) với tốc độ này tới khi thẳng làn trên, rồi phóng sang trái (leaveExitSpeed, gốc 12→24)")] public float leaveReverseSpeed = 8f;   // góp ý 06: lùi vọt rồi hãm gấp (ease 1); ảnh nét: 11 quá nhanh (0,1 s)
        [Tooltip("C13 (góp ý 06): phanh gấp cuối cú lùi: ngửa mũi (độ), thời lượng; đứng bao lâu rồi phóng; phóng chúi mũi (độ), thời lượng (gốc _leavePivotBrakePitchAngle 16 / 0.6 s)")] public float leaveBrakePitch = 14f;
        public float leaveBrakePitchDuration = 0.45f;
        public float leaveLaunchPause = 0.22f;
        public float leaveLaunchPitch = 7f;
        public float leaveLaunchPitchDuration = 0.5f;
        [Tooltip("C13: vệt bánh khi phóng: dài, nửa khoảng cách 2 bánh, rộng, màu (alpha), thời gian mờ (s); bắt đầu từ bánh sau (lùi so với tâm xe), mọc dần trong (s)")] public float skidLength = 2.5f;
        public float skidRearOffset = 0.9f;
        public float skidGrow = 0.35f;
        public float skidHalfTrack = 0.42f;
        public float skidWidth = 0.17f;
        public Color skidColor = new Color(0.08f, 0.08f, 0.12f, 0.7f);
        public float skidFade = 1.6f;
        public float leaveExitSpeed = 17f;   // clip: 14–19 đv/s
        [Tooltip("C15 (góp ý 04): xe kế của cột bắt đầu xuống sau khi xe cũ rời bao lâu (s) (clip: ~1.5 s, lúc xe cũ sắp khuất)")] public float nextCarDelay = 1.3f;

        // Booster B1–B9 (góp ý 11; clip V4 Lv.4 / V5 Lv.8, 13, 16; design-decisions 4–6). Phiên 1f thêm; luật trong BusGameBoosters.cs.
        [Header("Booster B1–B9 (góp ý 11)")]
        [Tooltip("B1/B7 (chốt 4a): thứ tự lấy pixel khi đổ đầy: 0 = cụm lớn nhất trước (không cần lộ), 1 = lớp ngoài như chạm")] public int fillPickRule = 0;
        [Tooltip("B2: pixel bật lên tại chỗ (cao, s) → bay tụ thành đám trên nóc xe (cao, bán kính, s) → rơi vào xe (s); xuất phát rải trong (s); tổng ≈ 2,0 s")] public float fillPopHeight = 1.2f;
        public float fillPopDuration = 0.3f;
        public float fillGatherHeight = 3.2f;
        public float fillGatherRadius = 1.5f;
        public float fillGatherDuration = 0.85f;
        public float fillDropDuration = 0.35f;
        public float fillSpread = 0.5f;
        [Tooltip("B3 (chốt 2a): Sky Grab bán kính vùng hút (ô) (clip ~25 pixel); chốt 1a: pixel không có xe đỗ cùng màu / xe đầy → biến mất; hút cả băng / ẩn")] public float skyRadius = 2.5f;
        public bool skyVanishUnmatched = true;
        public bool skyTakesFrozen = false;
        [Tooltip("B4: trực thăng vào từ (lệch x, lệch z so với điểm chạm) sau (s), bay tới điểm chạm (s), lơ lửng (s), bay lên qua hàng xe rồi ra mép trên (mỗi đoạn s); độ cao, cỡ, tốc độ cánh (độ/s)")] public Vector2 skyEnterFrom = new Vector2(-14f, 7f);
        public float skyEnterDelay = 0.3f;
        public float skyEnterDuration = 1.0f;
        public float skyHover = 1.8f;
        public float skyExitDuration = 0.5f;
        public float skyHeight = 5f;
        public float skyScale = 0.5f;   // 3c đo r5_boost16: 1,1 → thân heli ~30 % bề ngang khung, clip ~12 %; mesh Helicopter rộng 1,82 đv × 0,5 ≈ 0,9 đv ≈ 11,5 % khung (khung ≈ 7,9 đv)
        [Tooltip("B4: bóng mờ dưới trực thăng trên mặt bảng (rộng, dài, đv) để thấy đang lơ lửng; 0 = tắt")] public Vector2 skyShadowSize = new Vector2(1.6f, 2.6f);
        public float skyRotorSpeed = 1500f;
        [Tooltip("B4: pixel bị hút: bắt đầu rải trong (s) từ lúc lơ lửng, mỗi pixel bay lên bụng trực thăng trong (s)")] public float skySuckSpread = 1.0f;
        public float skySuckDuration = 0.5f;
        [Tooltip("B5 (chốt 3a): 0 = 3 xe đỗ mới là màu nhiều pixel LỘ nhất, 1 = ngẫu nhiên")] public int wandPickRule = 0;
        [Tooltip("B6: từng cột trái→phải cách (s); xe đỗ xoay 1 vòng (s); khối xám '?' giữ (s); số hiện sau (s); màu khối xám")] public float wandStagger = 0.17f;
        public float wandSpin = 0.5f;
        public float wandGrey = 0.5f;
        public float wandNumberDelay = 0.17f;
        public Color wandGreyColor = new Color(0.36f, 0.37f, 0.44f, 1f);   // fd r3_boost16 f88–f90: màu cũ sáng, trông như ô trống
        [Tooltip("B8: camera trượt lên thấy cả cột chờ (s), về (s), lề trên / dưới (thế giới)")] public float clawCamUp = 0.3f;
        public float clawCamBack = 0.4f;
        public float clawCamMargin = 4f;
        [Tooltip("B8: kẹp: cáp thả (s), kẹp (s), nhấc (s, cao), mang tới làn (s), nhả + thu cáp (s); độ cao đỉnh cáp; cỡ kẹp; vị trí thả (x, z) trên làn; xoáy tím trên nóc (s)")] public float clawDescend = 1.5f;
        public float clawGrab = 0.7f;
        public float clawLiftDuration = 0.5f;
        public float clawLiftHeight = 2f;   // fd r5_boost16 f132–f138: 4 → xe bị thanh HUD trên che lúc mang
        public float clawCarry = 1.5f;
        public float clawRelease = 0.3f;
        public float clawCableHeight = 3.5f;   // fd r3_boost16 f114–f128: 14 → kẹp + cáp nằm ngoài khung suốt lúc thả
        public float clawMeshScale = 1.1f;   // đầu kẹp = khối 1,4 đv × 1,1 ≈ 1,5 đv ≈ bề ngang xe (clip); móc Claw.obj treo dưới. r5/r6: mesh móc 0,46 đv × 1,5 vẫn chỉ là chấm
        public Vector2 clawLanePos = new Vector2(1.6f, 17.96f);
        public float swirlDuration = 0.5f;   // 3c: xoáy tím 0,3–0,7 s TRƯỚC khi pixel bay
        [Tooltip("B9: tooltip đáy: y tâm (%), rộng (%), cao / rộng, cỡ chữ (px @1080); VIP: màn tối alpha")] public float tooltipY = 0.80f;
        public float tooltipWidth = 0.82f;
        public float tooltipAspect = 0.19f;
        public int tooltipFontSize = 30;
        public float vipDimAlpha = 0.55f;

        public Buzzy.Core.BoosterRules ToBoosterRules()
        {
            return new Buzzy.Core.BoosterRules { SkyGrabRadius = skyRadius, SkyGrabVanishUnmatched = skyVanishUnmatched, SkyGrabTakesFrozen = skyTakesFrozen, WandPick = wandPickRule, FillPick = fillPickRule };
        }

        [Header("Màu nền (V3, V4, V8)")]
        public Color backgroundTop = new Color32(0xA7, 0xAC, 0xE6, 255);
        [Tooltip("V3: màu đáy nền (chuyển sắc dọc)")] public Color backgroundBottom = new Color32(0x87, 0x8C, 0xC6, 255);
        public Color roadColor = new Color32(0x44, 0x4A, 0x6B, 255);
        public Color dashColor = new Color32(0x8A, 0x90, 0xB4, 255);
        public Color trayColor = new Color32(0x7C, 0x82, 0xD5, 255);
    }
}
