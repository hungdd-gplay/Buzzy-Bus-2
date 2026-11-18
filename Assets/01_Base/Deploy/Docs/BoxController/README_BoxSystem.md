# Hệ thống Box (Popup / Panel)

## Thành phần
| File | Vai trò |
|---|---|
| `BaseBox.cs` | Lớp cha mọi popup: mở / đóng, hiệu ứng, stack, âm thanh, nền tối |
| `BoxController.cs` | Stack popup, sắp xếp sortingOrder, phím Back, Box Save |
| `BoxFactory.cs` | Tạo / tái sử dụng popup từ Resources (thay Setup() lặp lại) |
| `BaseScene.cs` | `OnEscapeWhenStackBoxEmpty()` khi bấm Back mà không còn popup |

## Tạo popup mới bằng code
```csharp
public class DailyRewardBox : BaseBox
{
    public const string PATH_RESOURCE = "UI/Popups/DailyRewardBox";
    public static DailyRewardBox Setup() => BoxFactory.Get<DailyRewardBox>(PATH_RESOURCE);

    protected override void OnAwake() { /* gán listener 1 lần */ }
    protected override void OnStart() { /* đổ dữ liệu mỗi lần mở */ }
}
// Mở:
DailyRewardBox.Setup().Show();
```

## Tạo popup từ ảnh demo
Tools > Base Project > Image Layout Scanner
1. Kéo ảnh demo + thư mục ảnh thành phần, bấm **Scan**.
2. Mở mục **Tạo Popup**, đặt tên (ví dụ `DailyRewardBox`), chọn thư mục script / prefab (prefab phải nằm trong `Resources`).
3. Bấm **Build Popup**: tạo prefab (Canvas + Dim + MainPanel), Button cho ảnh có tên chứa `btn / button / bt`,
   nút có `close / exit / x` được gắn sẵn `Close()`, sinh `DailyRewardBox.cs`, chờ biên dịch rồi tự gắn script + field.

Đặt tên ảnh thành phần theo quy ước để nhận diện nút: `btn_play.png`, `btn_close.png`, `BtnShop.png`.

## Cấu hình trên BaseBox
| Field | Ý nghĩa |
|---|---|
| `isPopup` | Có Canvas riêng, sorting layer `Popup` |
| `isNotStack` | Không đưa vào stack (không tính layer, không đóng bằng Back) |
| `isAnim` + `animType` | None / Scale / Fade / SlideUp, thời gian `durationAppear` / `durationClose` |
| `isCloseByBackKey` | Cho phép phím Back đóng popup (cần bật `isUseBackKey` trên BoxController) |
| `buttonDim` + `isCloseOnClickDim` | Bấm ra ngoài để đóng |
| `isPlaySoundOpenClose` | Phát `SoundId.UIPopupOpen` / `UIPopupClose` |
