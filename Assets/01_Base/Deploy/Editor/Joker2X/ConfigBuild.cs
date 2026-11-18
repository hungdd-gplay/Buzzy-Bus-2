// File cũ ConfigBuild (menu "Joker2x") đã được thay bằng:
//   Assets/01_BaseCode/Scripts/Core/Build/Editor/  (BuildConfig, BuildConfigWindow, BuildTool)
//   Menu "Build" trên thanh menu: Build Config..., Build APK/AAB, Build Xcode, Switch Platform, Mở thư mục Builds.
// Lý do bỏ code cũ: ghi đè toàn bộ Scripting Define Symbols (làm mất FEATURE_*), upload tới URL rỗng,
// sửa AndroidManifest OneSignal không còn tồn tại, mật khẩu keystore nằm trong code runtime (Config.cs).
// Có thể xóa file này.
