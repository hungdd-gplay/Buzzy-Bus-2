# Buzzy.Core - giả định đã chọn

Lõi C# thuần (noEngineReferences). Test: `dotnet test D:/Buzzy_Bus/CoreTests` (xunit; level mẫu đọc từ thư mục Downloads, đường dẫn trong CoreTests.cs).

1. Lưới thực = (Width+1) x (Height+1); Width/Height là chỉ số lớn nhất (theo ref-level.md).
2. (ĐÃ CHỐT 2026-11-18, người dùng đáp A) Pixel sâu bên trong VẪN lấy được khi chạm màu; chỉ THỨ TỰ lấy là lớp ngoài trước rồi gần ô bến. Bế tắc chỉ còn với mechanic chặn ở M3. "Lớp ngoài cùng" = độ sâu BFS 4 hướng tính MỘT LẦN lúc dựng Board từ ô rỗng/ngoài lưới (viền = 1). Pixel mới lộ về sau có độ sâu lớn hơn nên bị lấy sau pixel lớp ngoài ban đầu. Cùng độ sâu: gần ô bến hơn (khoảng cách Euclid^2), rồi theo (y, x).
3. Vị trí ô bến mặc định: 3 ô chia đều theo chiều ngang, y = -3 (dưới lưới, trục y của dữ liệu). Hướng trục y so với màn hình CHƯA kiểm; UI truyền `slotPos` thật vào `BusGame`.
4. Tap lấy pixel gỡ khỏi Board ngay (đang bay) và giữ chỗ (Reserved). Remaining/Boarded giảm/tăng khi `Arrive`. "Chọn xe" dùng Free = Seats - Reserved (không phải Remaining) để tap lặp không giữ quá chỗ; Free = 0 là trạng thái "Full".
5. Nhiều xe cùng màu: chọn xe có Free lớn nhất, hoà thì ô có chỉ số nhỏ hơn.
6. Tap(x, y) chỉ dùng MÀU của pixel (cả cụm màu), không đòi pixel đó đang lộ (đã chốt); ô rỗng/ngoài lưới = NoMatch.
7. Xe rời khi Boarded == Seats (mọi pixel đã lên). Ô trống được xe kế của đúng cột lấp ngay; cột hết xe thì ô bị rào vĩnh viễn (cột rỗng ngay từ đầu cũng rào ngay).
8. Thắng = Board hết pixel (không chờ pixel bay xong). Thua = bế tắc `[thiết kế]` (design-decisions mục 2): InFlight == 0, không xe nào trong ô còn Free > 0 mà có pixel cùng màu đang lộ. Không có đồng hồ/giới hạn lượt.
9. Solver.Greedy: mỗi vòng chọn màu của xe có Free lớn nhất mà có pixel lộ, Tap, cho pixel đáp ngay. Greedy giải được level_001 và level_002 (chưa chạy 300 level).
10. (M3) Xe 2 màu, Elevator, cổng đổi cột vẫn ngoài phạm vi (67 level; importer ghi cảnh báo). Các mục 12-19 là mechanic + booster M3.
11. Core không dùng System.Text.Json; JSON do test (hoặc loader Unity) tự parse rồi gọi `LevelData.Create`. Cú pháp giữ ở mức C# 9 cho Unity.

## M3 - mechanic + booster (mọi mục `[thiết kế]`: video không có; số chỉnh ở `MechanicRules`, đổi không cần sửa logic)

12. **Mystery (SurprisePixels)**: pixel ẩn không chạm được; hết ẩn (vĩnh viễn) khi có ô kề 4 hướng rỗng thật (ngoài lưới cũng tính; tường không tính). Chạm pixel ẩn = rung.
13. **Walls**: ô cứng không phải pixel, không lấy được. Tường không làm pixel kề "lộ" (ảnh hưởng độ sâu lớp ngoài và việc lộ ô Mystery). Tường có thể nằm y = -1 (quanh khay).
14. **Frozen**: pixel trong vùng không chạm được tới khi vùng vỡ. Số hit còn lại = ceil(RequiredHits / `FrozenHitDivisor` 12); mỗi pixel bị lấy nằm trong `FrozenReach` 4 ô quanh vùng (ngoài vùng) trừ 1. Mẫu Lv.77 hiện 106 / 114 (dữ liệu 210 / 150): hệ số này CHƯA khớp, chờ video.
15. **Spawner**: vùng `SpawnArea` rỗng thì đổ đầy đợt kế tiếp màu `WaveColors[k]` (đợt đầu đổ ngay lúc dựng); mỗi đợt = lấp kín cả vùng (khớp số ghế dư của 8 level lệch). Thắng = bảng hết pixel VÀ hết đợt.
16. **Key + xe xích**: chìa 3x3 nằm trên ô trống; nhặt khi >= `KeyClearFraction` 50 % pixel quanh chìa (vòng 5x5 bỏ 3x3) đã bị lấy; mở mọi xe khoá có `KeyColorCode` = màu chìa. Xe xích đứng đầu cột thì ô chờ (`LockedEntersSlot=false`, theo design-decisions mục 12); đặt `true` để xe xích vẫn vào ô bến nhưng không nhận pixel.
17. **VIP Bus**: xe đỗ (không còn pixel đang bay tới) rời ngay; pixel còn thiếu của nó biến mất (kể cả băng / ẩn). **Sky Grab**: tối đa `SkyGrabCount` 10 pixel của màu (kể cả băng / ẩn) bay vào xe cùng màu còn chỗ. **Bus Claw**: kẹp xe ở hàng chờ vào ô chờ; không có ô chờ thì đổi chỗ với xe đỗ chưa nhận pixel nào (xe đó về chỗ xe kẹp). **Magic Wand**: đổi cả màu + số chỗ giữa xe đỗ chưa nhận pixel và xe chờ sớm nhất mang màu chạm (tổng chỗ mỗi màu giữ nguyên nên không tạo bế tắc mới).
18. Kiểm 300 level (`MechanicTests.AllLevels_Report`, `InScopeLevels_SolvableByRandomRestarts_ExceptKnownHard`): 233 level trong phạm vi; solver ngẫu nhiên 100 lần giải được 229, **4 level cần booster: 38, 145 (Mystery), 77, 78 (Frozen)**; greedy thuần bế tắc 11 level. `Solver.FindOrder` cho bộ tự chơi.
19. Số hiển thị trong game gốc có thể khác (Frozen 106 / 114): xem mục 14.
