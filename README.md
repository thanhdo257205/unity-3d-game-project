
## Mở dự án

1. Cài Unity Editor **6000.6.3f1** bằng Unity Hub.
2. Chọn **Add project from disk** và mở thư mục này.
3. Chờ Unity khôi phục các package và import asset.
4. Mở `Assets/_Game/Scenes/SanTap.unity`, sau đó nhấn Play.

Dự án sử dụng URP, Input System, Cinemachine và Visual Effect Graph. Unity Hub có thể cần Git để tải package MCP for Unity trong `Packages/manifest.json`.

## Cấu trúc

- `Assets/_Game/`: scene, script, prefab, shader, model và dữ liệu game.
- `Packages/`, `ProjectSettings/`: cấu hình cần thiết để mở lại dự án.
- `ArtSource/`: nguồn mỹ thuật, gồm file Blender.
- `Docs/`: tài liệu triển khai và kiểm thử.

`Library/`, `Temp/`, `Logs/`, `UserSettings/`, bản build và ảnh/clip thử trong `Assets/Screenshots/` được bỏ qua theo `.gitignore`. Unity sẽ tạo lại cache khi mở dự án.

## Công cụ biên tập

Dự án có tích hợp MCP for Unity. MCP for Blender được kết nối riêng với Blender; việc clone repo không tự cấu hình kết nối trên máy khác.
