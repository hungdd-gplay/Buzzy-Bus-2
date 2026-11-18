# Gói IAP (Sale Pack)

Quản lý: **Tools > Base Project > IAP > Pack Manager**.

## Dữ liệu nằm ở đâu
| Nơi | Nội dung |
|---|---|
| `IAPDatabase` | Sản phẩm: shortID / product id, quà nhận được, giá mặc định |
| `SalePackDatabase` (SalePackDataModel) | Thông số gói: hiển thị (icon Home, banner Shop, popup), `popupResourcePath`, `salePercent`, giới hạn mua, điều kiện xuất hiện, điều kiện gợi ý popup |
| Segment `saleData.packSchedules` (PackScheduleConfig) | Lịch: `isActive`, `levelUnlock`, thời gian xuất hiện, interval, ngày trong tuần, khoảng thời gian. Remote config key: `pack_schedule_config` (`{"packs":[...]}`) |

## Lịch xuất hiện (segment)
| Field | Ý nghĩa |
|---|---|
| `durationType` | `Forever` (không mất), `Duration` (hiện `durationSeconds` giây, ví dụ 5 - 10 phút), `UntilEndOfDay`, `UntilEndOfWeek`, `UntilEndOfActiveDays` (ví dụ T7 + CN -> hết CN) |
| `intervalReappearSeconds` | Hết 1 lần xuất hiện thì chờ bao lâu mới xuất hiện lại (0 = ngay khi đủ điều kiện) |
| `intervalSuggestSeconds` | Khoảng cách giữa 2 lần tự bật popup gợi ý ở Home |
| `isAllWeek` + các ngày | Chỉ xuất hiện vào ngày được chọn |
| `timeStart` / `timeEnd` | Khoảng thời gian chạy (gói mùa vụ), `yyyy-MM-dd HH:mm` |

Mỗi lần xuất hiện: bắt đầu khi đủ điều kiện, kết thúc theo `durationType` (không vượt `timeEnd` / hết event), rồi chờ `intervalReappearSeconds`.
Số lần mua trong lần xuất hiện được reset ở lần xuất hiện mới.

## Điều kiện (SalePackDatabase)
`maxBuyPerAppear`, `maxBuyTotal`, `userType` (All / NonPayer / Payer), `requireBought` (đã mua gói X đủ N lần),
`hideIfBought`, `minLevelPlayInDay`, `isHideWhenRemoveAds`, `eventSale` (chỉ chạy trong event sale), `groupExclusive`
(nhiều gói cùng nhóm: người chơi chỉ thấy 1, mua 1 thì cả nhóm ẩn), `suggestMinAdsInPlay` / `suggestMinAdsInDay`.

## Tạo gói mới
Pack Manager > **Tạo gói mới**: thêm `TypePackIAP` (hoặc dùng loại có sẵn), shortID, popup, level, thời gian hiện, interval.
Tool thêm gói vào SalePackDatabase + IAPDatabase (nhớ điền quà) + lịch ở mọi segment. Nhiều gói có thể dùng chung 1 popup:
popup hiển thị theo `typePackIAP` được truyền vào (`BasePackBox.Open`).

## Gói có logic đặc biệt
Kế thừa `PackBaseController`, override `IsEligibleHandle` / `IsActiveHandle` / `OnPurchasedHandle`... và đặt component
làm con của PackController với `typePack` tương ứng. Gói còn lại dùng controller mặc định do PackController tự tạo.

## Chuyển từ hệ thống cũ
19 controller riêng (StarterPackController, DailyPackController...) đã gộp vào `PackBaseController`.
Pack Manager > **Preset gói thiếu lịch** điền thông số + lịch tương đương logic cũ; **Dọn controller cũ** xoá component mất script trong GameController prefab.
