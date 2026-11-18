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
10. Bỏ qua cơ chế phụ (Frozen, Wall, Spawner, Key/Locked, xe 2 màu, booster); loader chỉ đọc ID/Seats/ColorCode và Width/Height/Pixels.
11. Core không dùng System.Text.Json; JSON do test (hoặc loader Unity) tự parse rồi gọi `LevelData.Create`. Cú pháp giữ ở mức C# 9 cho Unity.
