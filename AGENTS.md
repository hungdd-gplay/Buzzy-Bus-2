# AGENTS.md — Buzzy Bus (clone)

Luật cho mọi AI agent (Claude, Codex, Gemini). Chi tiết: `.claude/game-profile.md`, `.game/research/*`, `.game/plan.md`.

## Dự án

- Unity 6000.3.10f1 (đã cài, `C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe`), 3D mobile dọc 9:20. Luật đầy đủ: `.game/research/teardown.md`.
  Tóm tắt: chạm cụm pixel một màu → pixel (lớp ngoài, gần xe nhất) bay lên xe cùng màu đang đỗ ở 1 trong 3 ô bến, tới hết số chỗ; xe đủ người rời bến, xe kế của cột xuống; hết pixel thắng; bế tắc thua `[thiết kế]`.
- Hình theo `.game/research/visual-spec.md`, bảng nghiệm thu `.game/research/visual-check.md`, level chuẩn `.game/research/ref-level.md` (Lv.2).
- Code game: `Assets/02_Gameplay/Deploy/{Model,View,Controller,Editor}`; luật chơi thuần: `Assets/02_Gameplay/Deploy/Model/Core` (namespace `Buzzy.Core`, không UnityEngine). Framework: `Assets/01_Base`.
- Level: JSON gốc → importer Editor → `LevelDatabase` (ScriptableObject). Lưới = (Width+1)x(Height+1).

## Luật bắt buộc

1. Namespace `Buzzy.Core` (luật), `Buzzy.Game` (Unity), `Buzzy.Game.EditorTools` (editor).
2. Không sửa tay YAML scene / prefab / asset; không xoá / sinh lại `.meta`. Đổi qua editor script (`-executeMethod`).
3. Không đọc / in / copy: `.keystore`, `.jks`, `google-services.json`, `GoogleService-Info.plist`, `ApiKey.txt`. Không đụng cấu hình SDK / keystore.
4. Chỉ commit khi người dùng yêu cầu; commit sau mỗi mốc trên nhánh `game/buzzy-bus`.
5. File nặng (video, ảnh test, cache) để ngoài repo (scratchpad / ổ tạm).
6. Số bố cục (vị trí, cỡ, camera, tốc độ) ở `BuzzyLayoutConfig` + `GameConfiguration`, không hằng số trong code; mỗi số ghi mã dòng `visual-check.md`.
7. Luật `[thiết kế]` (`.game/research/design-decisions.md`) làm bằng dữ liệu để dễ đổi; không viết như đã quan sát.
8. Comment + chuỗi UI tool: tiếng Việt ngắn gọn, giống code xung quanh.

## Kiểm tra

- Test luật: `C:/Users/PC/.dotnet/dotnet.exe test CoreTests` (csproj liên kết `Model/Core/**/*.cs`).
- Compile Unity: `UNITY_EXE=<Unity.exe> python <plugin>/scripts/unity_run.py compile` (Unity Editor phải đóng).
- Tự chơi + ảnh: `python <plugin>/scripts/unity_run.py playtest --levels 2 --out <tmp>/playtest_M1 --size 1080x1920`.
- So hình: `python <plugin>/scripts/side_by_side.py <ảnh chuẩn> <ảnh game> <ra.jpg> --grid 10` + `--crop x0,y0,x1,y1`; rồi `orch.py check --ms <mốc>`.
  Báo kết quả kèm đường dẫn đầy đủ từng ảnh + dòng chưa đạt; không viết "khớp mẫu" khi còn dòng chưa đạt.
- Ảnh chuẩn (khung 324x720): `C:\Users\PC\AppData\Local\Temp\claude\e376962bfa314a45\4283704d-f624-4de9-9b77-0aed82675794\scratchpad\ref\golden\`.

## Bài học

- Lệnh bị từ chối quyền: KHÔNG gọi vòng (script tạm, wrapper). Ghi lệnh vào báo cáo để người điều phối chạy.
- Hỏi người dùng bằng chữ ngắn, đánh số, giải thích thuật ngữ (người dùng không thích hộp AskUserQuestion).
- Mở project 6000.3 bằng Unity 6000.0.76f1 có thể sửa `ProjectVersion.txt` / `Packages`; bản sao lưu ở scratchpad `backup_unity/`.
- `BuzzyLayoutConfig.asset` là giá trị CHẠY THẬT (ảnh nghiệm thu đo trên nó). Trường đã có trong asset: đổi giá trị = đổi asset qua tool `Tools/Buzzy Bus/Sync Config Defaults` (batch: `unity_run.py exec Buzzy.Game.EditorTools.BuzzyMotionCapture.SyncConfigDefaults`, `BUZZY_FIELDS=a,b`) + ghi dòng visual-check; đổi mặc định code KHÔNG có tác dụng; mặc định code phải bằng asset. Trường mới thêm thì mặc định code ăn ngay. OBJ gốc có sub-mesh quad `X_1`: tool nạp mesh lấy sub-mesh nhiều đỉnh nhất.
