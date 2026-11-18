# Game Flow (Home / Page)

Tools > Base Project > Game Flow (Home - Page). Cấu hình lưu ở `Resources/Config/GameFlowConfig.asset` (mỗi game 1 file).

## Có / không có màn Home
| Field | Ý nghĩa |
|---|---|
| `isUseHome` | Tắt: Loading vào thẳng GamePlay; mọi `LoadScene(HOME_SCENE)` tự đổi thành GamePlay (`GameFlow.ResolveScene`) |
| `levelPlayDirectFromLoading` | Level <= giá trị này thì Loading vào thẳng GamePlay (remote `start_level_back_to_home` ghi đè) |

Thứ tự quyết định scene sau Loading (`GameFlow.GetSceneAfterLoading`):
1. Không có Home → GamePlay.
2. Segment `loadGameConfig.typeSenceFirstOnOpenGame = 1` → GamePlay.
3. Level hiện tại <= ngưỡng → GamePlay, còn lại → Home.

Code mới nên dùng `GameFlow.GoHome()` / `GameFlow.SceneHome` thay vì gọi thẳng `SceneName.HOME_SCENE`.

## Page ở Home
- Bật / tắt từng page, chọn page mặc định. Page tắt bị gỡ khỏi scroll (không để lại khoảng trống) và ẩn nút tab.
- Còn 1 page: khoá vuốt, ẩn thanh tab (gán `tabBar` trên PageController).
- `PageController.TryChangePage(PageName)` trả về false nếu page tắt (HomeScene.OpenShop sẽ mở ShopBox thay thế).

## Tạo page mới
Nhập tên (ví dụ `Event`) → bấm **Tạo page**: thêm `PageName.Event`, sinh `EventPage.cs : PageBase`
(`Init`, `InitState`, `OnChoicePage`, `OnHidePage` để trống), sau khi biên dịch tạo object page trong scroll
và nhân bản 1 nút tab có sẵn (nhớ đổi icon nút, Save scene).
