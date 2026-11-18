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
        [Tooltip("V1: góc nhìn trước-trên")] public Vector3 cameraPosition = new Vector3(0f, 20.16f, -7.35f);
        [Tooltip("V1")] public Vector3 cameraEuler = new Vector3(54f, 0f, 0f);
        [Tooltip("V1")] public float fieldOfView = 64.4f;

        [Header("Ảnh pixel (V19-V23)")]
        [Tooltip("V20: bước ô theo z (hàng)")] public float cellPitch = 0.5f;
        [Tooltip("V20: bước ô theo x (cột); 24 cột = 90 % bề ngang khung")] public float cellPitchX = 0.466f;
        [Tooltip("V21: tỉ lệ thu mesh Block_Mesh so với pitch (mesh thân ~1.05)")] public float blockScale = 0.44f;
        [Tooltip("V19: tâm ảnh theo trục x thế giới")] public float boardCenterX = 0f;
        [Tooltip("V19: z của hàng y=0 (hàng thấp nhất của ảnh, gần camera)")] public float boardZ0 = 0f;

        [Header("Làn đường + bến (V8, V9, V13, V14)")]
        [Tooltip("V8: z gần camera của đường")] public float roadNearZ = 16.8f;
        [Tooltip("V8: z xa camera của đường")] public float roadFarZ = 34.4f;
        [Tooltip("V9: z của vạch đứt")] public float dashZ = 25.1f;
        [Tooltip("V13: z tâm xe ở bến")] public float slotZ = 19.6f;
        [Tooltip("V13: x tâm 3 ô bến")] public float[] slotX = { -3.78f, 0.24f, 4.35f };
        [Tooltip("V13: góc xoay yaw xe ở bến (độ)")] public float slotYaw = 38f;
        [Tooltip("V14: tỉ lệ thu mesh Bus (rộng xe ~19 % khung)")] public float busScale = 1.24f;

        [Header("Hàng chờ (V10-V12)")]
        [Tooltip("V10: z tâm xe hàng chờ")] public float queueZ = 28.9f;
        [Tooltip("V10: x tâm 3 cột")] public float[] queueX = { -4.17f, 0f, 3.95f };
        [Tooltip("V11: tỉ lệ thu Bus ở hàng chờ")] public float queueBusScale = 1.03f;


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
        [Tooltip("V21: nhân chiều cao khối pixel (mẫu cao hơn rộng)")] public float blockHeightScale = 1.5f;
        [Tooltip("V20: nhân bề ngang khối pixel (mẫu: khối chạm nhau theo hàng ngang)")] public float blockWidthScale = 1.14f;
        [Tooltip("V9: nửa dài mỗi vạch đứt (thế giới)")] public float dashHalfLength = 0.78f;
        [Tooltip("V4: màu rim khay / bến")] public Color lipColor = new Color32(0x7C, 0x82, 0xD5, 255);
        [Tooltip("V12: chữ số chỗ trên nóc xe, cỡ")] public float roofNumberSize = 1.3f;


        [Header("Chuyển động (C*) - M2")]
        [Tooltip("C1: xe xuất hiện phía trên cách ô bến bao nhiêu z (hầm)")] public float enterFromZ = 16f;
        [Tooltip("C1: thời lượng xe lăn vào ô bến (s)")] public float enterDuration = 0.9f;
        [Tooltip("C1: trễ giữa các xe lúc vào màn (s)")] public float enterStagger = 0.12f;
        [Tooltip("C1: xe hàng chờ vào sau (s)")] public float queueEnterDelay = 0.5f;
        [Tooltip("C3: xe ở hàng chờ xuống ô trống (s)")] public float queueToSlotDuration = 0.8f;
        [Tooltip("C7: tốc độ xe rời bến (đơn vị/s)")] public float leaveSpeed = 22f;
        [Tooltip("C7: z của làn trên để xe chạy ngược ra")] public float leaveLaneZ = 24.5f;
        [Tooltip("C7: x mép phải/trái xe thoát ra")] public float leaveEdgeX = 12f;
        [Tooltip("C4: thời gian bay của 1 pixel (s)")] public float flightDuration = 0.6f;
        [Tooltip("C4: tổng thời gian xả cả cụm (s) cho 50 người")] public float flightSpread = 2.0f;
        [Tooltip("C4: độ cao vòng cung")] public float flightArc = 5f;
        [Tooltip("C5: pixel xuất hiện đầu level: tổng lan (s) và thời lượng mỗi khối (s)")] public Vector2 appearSpreadDuration = new Vector2(0.9f, 0.7f);
        [Tooltip("C9: trễ rào chắn sau khi xe rời (s)")] public float barrierDelay = 0.36f;
        [Tooltip("C13: bắt đầu cho chạm sau khi vào màn (s)")] public float inputDelay = 1.5f;

        [Header("Màu nền (V3, V4, V8)")]
        public Color backgroundTop = new Color32(0xA7, 0xAC, 0xE6, 255);
        [Tooltip("V3: màu đáy nền (chuyển sắc dọc)")] public Color backgroundBottom = new Color32(0x87, 0x8C, 0xC6, 255);
        public Color roadColor = new Color32(0x44, 0x4A, 0x6B, 255);
        public Color dashColor = new Color32(0x8A, 0x90, 0xB4, 255);
        public Color trayColor = new Color32(0x7C, 0x82, 0xD5, 255);
    }
}
