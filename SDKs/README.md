# SDKs

Thư mục chứa gói SDK, nằm NGOÀI `Assets/` nên Unity không tự import.

- Bỏ file `.unitypackage` hoặc `.tgz` vào đây (có thể chia thư mục con).
- Import: **Tools > Base Project > SDK > Import tất cả SDK** (hoặc **SDK Importer** để chọn từng gói).
- Thứ tự import theo tên file → đặt tiền tố `01_`, `02_`... nếu SDK cần import trước (vd External Dependency Manager).
- Điền ID: **Tools > Base Project > SDK ID Manager**.
