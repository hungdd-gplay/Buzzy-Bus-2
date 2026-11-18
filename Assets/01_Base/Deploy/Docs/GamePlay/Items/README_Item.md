# Hệ thống Item gameplay

## Thành phần
| File | Vai trò |
|---|---|
| `ItemBase.cs` | Lớp cha 1 item: cấu hình segment, kiểm tra, kích hoạt, thời gian tác dụng, hồi chiêu, spawn |
| `ItemController.cs` | Quản lý mọi item: sinh nút UI, `UseItem` (kiểm tra + trừ số lượng), pool, tick, sự kiện |
| `SuggetUseItemInGame.cs` | Nháy nút item khi người chơi đứng yên quá lâu (danh sách ưu tiên trong Inspector) |
| `Editor/ItemManagerWindow.cs` | Tools > Base Project > Item > Item Manager |
| Segment | `coreGameData > config > bootsConfig > items` (`CoreGameData.BootsData`) |

## Cấu hình segment (BootsData)
| Field | Ý nghĩa |
|---|---|
| `typeItem` | GiftType của item (1000 - 1999) |
| `isEnable` | Tắt = ẩn nút, không dùng được |
| `unlockLevel`, `price`, `packAmount`, `amountDefault` | Mở khoá, giá, số lượng / lần mua, tặng lần đầu |
| `workValue[]` | Thông số tác dụng, đọc bằng `GetWorkValue(index)` |
| `durationActive` | > 0: giữ tác dụng x giây rồi tự `EndItem()` |
| `cooldown`, `maxUsePerLevel` | Hồi chiêu, giới hạn số lần / level |
| `isConsumeOnActive` | Trừ 1 item mỗi lần dùng |
| `isOnVideoBuy`... | Mua bằng video (BuyItemBox) |

## Luồng dùng item
`ItemUI` bấm → `ItemController.UseItem(type)` → `ItemBase.IsCanUseItem()` (bật, mở khoá, hồi chiêu, số lần, `IsCanUseItemHandle`)
→ `ActiveItemHandle()` → trừ số lượng → cập nhật nút → `OnItemActivated`.
Có `durationActive`: mỗi frame `OnActiveUpdate`, hết giờ / hết level → `EndItemHandle()`.

## Hàm để trống cho item mới
`ActiveItemHandle` (bắt buộc), `IsCanUseItemHandle`, `EndItemHandle`, `OnActiveUpdate`, `OnSpawnHandle`,
`OnDespawnHandle`, `OnLevelStartHandle`, `OnLevelEndHandle`, `OnPauseHandle`.

Hàm có sẵn: `Spawn(pos)` / `Despawn(go)` (pool theo `prefabSpawn`), `SpawnFlyFX(from, to)`, `GetWorkValue(i)`, `GetItemUI()`.

## Tạo item mới
Tools > Base Project > Item > Item Manager > **Tạo item mới**: nhập tên (ví dụ `Bomb`) + thông số, bấm **Tạo item**.
Tool sẽ thêm `GiftType.BombItem`, sinh `ItemBomb.cs`, sau khi biên dịch thêm cấu hình vào mọi segment,
GiftDatabase (icon) và object `ItemBomb` dưới ItemController của scene gameplay đang mở. Viết tác dụng trong `ActiveItemHandle()`.

## Gameplay cần gọi
- `itemController.OnLevelStart()` khi bắt đầu / chơi lại level (OnReplayLevel đã gọi sẵn).
- `itemController.OnLevelEnd()` khi thắng / thua / thoát (đã gọi sẵn trong GamePlayController).
- `itemController.ActiveItemSellectLevel()` khi level sẵn sàng (item chọn trước ở PlayCampainBox).
- `state = StateGame.Playing` để bật gợi ý dùng item.

---

# Item thu thập (Event / Challenge)

## Thành phần
| File | Vai trò |
|---|---|
| `GamePlay/CollectItems/CollectItemBase.cs` | Lớp cha item thu thập: gắn lên object, xuất hiện (Spine / Sprite), nhặt, x2, bay về khung |
| `GamePlay/CollectItems/CollectItemGameplayController.cs` | Spawn item mọi nguồn đang chạy, tính điểm, nhặt hết (nam châm), cộng tiến độ khi thắng / thua |
| `GamePlay/CollectItems/ICollectItemSource.cs` | Interface nguồn (ChallengeBase, EventBase đã cài) |
| `ItemChallenge` / `EventSaleItem` | Item của challenge / event (kế thừa CollectItemBase) |
| `ChallengeGameplayController` / `EventSaleInGameController` | Kế thừa CollectItemGameplayController |
| Segment | `challengeData > ... > collectItem`, `saleData > ... > collectItem` (`CollectItemConfig`) |

## Cấu hình (CollectItemConfig)
`isEnable`, `numItemTutorial` (level mở khoá), `maxItemPerLevel`, `scorePerItem`, `typeItemBoostX2`,
`isKeepWhenLose`, `multiplyOnWin`, `workValue[]`. Số item mỗi level vẫn theo `percentSpawnItems`.

## Luồng
`GamePlayController.InitState()` → `SpawnCollectItems()` rải item lên `GetTransformsSpawnItem()` (gameplay override hàm này).
Gameplay gọi `item.Collect()` khi người chơi nhặt (hoặc `CollectAll()` cho nam châm).
`OnWinLevel` → cộng điểm vào tiến độ; `OnLoseLevel` → chỉ cộng nguồn có `isKeepWhenLose`.

## Hàm để trống
`OnSetupHandle`, `OnSpawnHandle`, `OnAppearHandle`, `IsCanCollectHandle`, `OnCollectHandle`, `OnFlyDoneHandle`, `OnDespawnHandle`.

## Tạo item thu thập mới
Tools > Base Project > Item > Item Manager > tab **Item thu thập**: chọn event / challenge, nhập tên (ví dụ `Pumpkin`), icon, bấm **Tạo**.
Tool sinh `PumpkinCollectItem.cs`, tạo prefab (Body = SpriteRenderer icon) và gán làm item rơi của nguồn trong GameController prefab.
