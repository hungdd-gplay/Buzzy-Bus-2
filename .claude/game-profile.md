# Game profile — Buzzy Bus (clone)

Skill `gplay-game` đọc file này trước.

## Định danh
- Tên / package id: tạm "Buzzy Bus" / `com.DefaultCompany.ProjectBase` (đổi ở M1 T01; tên cuối + package id chưa quyết, cần thay trước phát hành)
- Unity: 6000.3.10f1 (đã cài) · Spine: không dùng
- Clone từ: base GPlay trong repo (giữ: Singleton, popup BaseBox/BoxController, GameFlow, GamePlayController, pool, SoundManager, Localization, GPlayerPrefs; bỏ: Bird Sort — 7102 level, 01_BirdSort, cụm meta cũ)

## Code
- Namespace: `Buzzy.Core` (luật), `Buzzy.Game` (Unity), `Buzzy.Game.EditorTools`
- Thư mục code game: `Assets/02_Gameplay/Deploy/{Model,View,Controller,Editor}` · framework: `Assets/01_Base`
- Scene: `Assets/02_Gameplay/Res/Scenes/` (script build cố định đường dẫn: tạo ở M1)
- Menu tool: `Tools/Buzzy Bus/...`

## Cơ chế
- Luật lõi (tóm tắt từ `.game/research/teardown.md`, M7 luật 1c): chạm vào pixel → pixel cùng màu CÓ ĐƯỜNG RA ngoài lan từ điểm chạm (BFS, đào hầm) đi bộ tới xe cùng màu ở ô bến, tới hết số chỗ; pixel bị màu khác bao kín ở lại; xe đủ lùi vòng rồi phóng, xe kế xuống; cột hết xe → rào; hết pixel thắng; bế tắc thua. Cờ `exposedOnly` / `orderFromTapPoint` trong BuzzyLayoutConfig (tắt = luật cũ).
- Hình + luồng màn: `.game/research/visual-spec.md` · ảnh chuẩn: `...\scratchpad\ref\golden\game_start.png` (Lv.2, 324x720 = 9:20) · chuẩn khung từ M7b: 9:16 (mẫu 9:16 = golden crop 324x576, xem visual-spec)

| Cơ chế | Runtime | Importer | File dữ liệu |
|---|---|---|---|
| Lõi chạm-bay-xe | `Model/Core` | LevelImporter | `Levels/level_*.json` |
| Frozen, Mystery, Walls, Spawner, Key + Locked | M3 | M3 | trường trong level JSON |
| Booster (VIP, Sky Grab, Magic Wand, Bus Claw) | M3 | — | `BoosterConfig.json` |

## Level
- Nguồn: `C:\Users\PC\Contacts\Downloads\Buzzy Bus Resource\Buzzy Bus\Levels\level_001..300.json` · Database: `LevelDatabase` (tạo ở M1) · menu Refresh: `Tools/Buzzy Bus/Import Levels`
- Lưới (Width+1)x(Height+1), toạ độ pixel 0..Width / 0..Height bao gồm.
- Level chuẩn để dựng + so: Lv.2 (`.game/research/ref-level.md`).

## Text / âm thanh
- Âm thanh: `Full_APK_Export/Audio` (23 wav) → `Assets/02_Gameplay/Res/Buzzy/Audio/*.wav` (tên đổi, M7), gán vào `BuzzyAssets` bằng `Tools/Buzzy Bus/Refresh Assets`; phát qua `BuzzyAudio` (trên camera của BuzzyBootstrap); ánh xạ sự kiện: design-decisions 17 · Font: `LilitaOne-Regular` (Curated/Fonts) · localization: base GPlay (Resources/Localization*.txt)

## Bí mật (agent không đọc)
- `.keystore`, `.jks`, `google-services.json`, `GoogleService-Info.plist`, `ApiKey.txt`. Lưu ý: keystore + Firebase của Bird Sort đang trong git (người dùng chọn giữ nguyên).

## Lệnh
- Test logic thuần: `C:/Users/PC/.dotnet/dotnet.exe test CoreTests`
- Compile Unity batchmode: `UNITY_EXE="C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe" python <plugin>/scripts/unity_run.py compile`
- Chơi thử tay (M7): mở `Assets/02_Gameplay/Res/Scenes/BuzzyGame.unity`, Play; bảng `<` `Lv` `>` `R` dưới badge (`BuzzyBootstrap.devPanel`, chỉ Editor); số feel ở `BuzzyLayoutConfig` mục "Cảm giác chơi" (A*, J*, T*)
- Luồng (M5): scene `BuzzyLoading` → `BuzzyGame` (`BuzzyBootstrap` = flow: MetaState + GPlayerPrefs, popup trong `BuzzyGameView.Popups.cs`); chụp màn: `unity_run.py exec Buzzy.Game.EditorTools.BuzzyScreensCapture.Run` (BUZZY_OUT, BUZZY_LEVEL)
- Validator: `python tools/solve_levels.py` (M4: gọi dotnet ValidatorTests; báo cáo `.game/research/level-report.md`, tool Unity `Tools/Buzzy Bus/Validate Levels`) · Tự chơi + ảnh: `unity_run.py playtest --levels N --out <tmp>`, Unity đóng, có GPU
- Art tạm / bên thứ ba phải thay trước phát hành: toàn bộ mesh / sprite / shader / audio / level từ APK gốc (tên, logo cũng thế)

## Quảng cáo / analytics
- SDK: MAX, AppsFlyer, Adjust, Firebase, PlayFab, Facebook, GoogleMobileAds, UMP (có sẵn, dùng stub ở bản đầu)

## Bài học riêng dự án
- Frame video: khung game thật là 324x720 trong video 1280x720 (crop=324:720:478:0); hai bên là dải mờ.
- JSON level: Width/Height là chỉ số lớn nhất, không phải số ô.
