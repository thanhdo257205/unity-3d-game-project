# Phân tích chiến đấu — Trọng Thần Sư

Tham chiếu: video `Trọng Thần Sư vừa bước tới…` trong `REF/`, dài 41,47 giây, 30 fps. Phân tích ngày 15/10/2026. Ảnh các mốc: `ArtSource/References/TrongThanSu_Combat.jpg`. Khung hình đã xoay để đọc đúng hướng; không sửa video gốc.

## Ngôn ngữ chiến đấu quan sát được

| Mốc | Quan sát | Chuyển thành cách chơi |
|---|---|---|
| 0–4 giây | Nhân vật kết ấn; đối thủ tụ lực, có khoảng báo trước khi đòn phát ra | Đòn mạnh có thời gian tụ lực, không gây sát thương ngay khi bấm |
| 4–8 giây | Màn năng lượng xanh ngọc kéo dài theo phương đứng, tia vàng va bên ngoài | Hộ thể xanh có hình nón, chặn đường bay kiếm/cầu địch |
| 8–11 giây | Vũ khí vàng áp sát, lóe sáng ngắn tại va chạm | Chặn thành công tích lực, thả để phản công có va chạm thực |
| 15–24 giây | Nhân vật giữ khoảng cách, pháp bảo trên tay; có vật thể dạng hoa sen vàng | Kim Liên: mô hình hoa sen, xoay khi tụ lực rồi phóng vào mục tiêu |
| 25–34 giây | Lao và đổi hướng trên không; vệt xanh–vàng kéo dài, cơ thể nghiêng theo hướng bay | Dùng hệ bay có sẵn, kéo dài vệt tăng tốc nhưng giảm bề rộng/độ đậm |
| 35–41 giây | Ánh vàng phủ vùng rộng, hình thể rất lớn phía xa tạo cảm giác chênh lệch sức mạnh | Dùng sóng áp lực khi Kim Liên va chạm; cảnh pháp tướng khổng lồ chưa được dựng thành nhân vật/chiêu riêng |

Đây là chuyển thể nhịp và nguyên lý chiến đấu thành gameplay, không phải tái dựng từng khung hình. Cắt cảnh, thoại, dàn nhân vật và môi trường điện ảnh không nằm trong bản thay đổi này. Các dáng kết ấn dùng lại lớp hoạt ảnh thi triển hiện có; chưa thay bằng bộ chuyển động tay quay mới.

## Đã đưa vào SanTap

- Giữ **G**: Hộ Thể, 8 pháp lực mở và 5/s duy trì, tối đa 3 giây. Chặn tốn thêm 1 pháp lực/đòn. Chặn kiếm và cầu năng lượng trước khi chúng chạm người chơi.
- Thả **G** sau khi chặn: phóng Kim Liên phản kích. Sát thương 14 + 7 × số đòn đã chặn; tối đa 4 đòn tích lực, đạt 4 tự phản công. Bán kính ảnh hưởng 2,5m. Không có đòn chặn thì thả không sinh phản kích miễn phí.
- Bấm **T**: Kim Liên tụ lực 0,8 giây, tốn 35 pháp lực, hồi 8 giây, gây 45 sát thương trong bán kính 5m. Có khóa mục tiêu thì pháp bảo bám địch; không khóa thì phóng theo hướng camera. Va vào vật cản sẽ nổ ở phía trước vật cản; sát thương lan kiểm tra đường nhìn để không xuyên tường.
- HUD nhắc phím, tích lực và hồi chiêu ở cạnh phải. Mở Admin hoặc chết sẽ hủy hộ thể/tụ lực.
- Vệt cánh khi tăng tốc dài 6m thay vì 3m, hẹp hơn và độ đậm thấp hơn. Giữ nguyên mức sét đã giảm: lõi 2,4, bùng 3,2; không nâng Bloom toàn cảnh.
- Kiếm đã phóng vẫn đánh tới R khi mục tiêu còn sống, tự về khi mục tiêu chết/xóa/vô hiệu hóa, theo yêu cầu trước.

Nguồn Blender: `ArtSource/Blender/KimLien.blend`. Model Unity: `Assets/_Game/Models/PhapBao/KimLien/KimLien.fbx`. Prefab: `Assets/_Game/Prefabs/KimLienTien.prefab`. Menu dựng lại: Phàm Kiếm Lục → Thần thông → Dựng Kim Liên và Hộ Thể. Menu kiểm tra: Thần thông → Kiểm thử trong Play Mode.

## Kiểm chứng

Kiểm chứng trong Unity: 17 kiểm tra thần thông đạt (mana, tụ lực, hồi chiêu, chặn kiếm/cầu thật, phản kích, sát thương lan, tường sát người, hủy khi chết), cùng 24 kiểm tra đấu phi kiếm hiện có. Báo cáo chạy gần nhất ở `Library/ThanThongValidation.txt` và `Library/SwordReferenceValidation.txt`. Ảnh xem trước ở `Assets/Screenshots/trong_than_ho_the_final.png` và `Assets/Screenshots/trong_than_kim_lien_final.png`.

## Công cụ bổ sung và kết nối

1. **Unity Visual Effect Graph**: phù hợp để mở rộng bụi linh lực, sợi năng lượng và va chạm thành hiệu ứng GPU. Có thể thao tác qua Unity MCP và script editor, không cần thêm một MCP riêng. [Tài liệu Unity](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/index.html).
2. **Houdini**: đề xuất khi cần khói cuộn, bão năng lượng, đá vỡ và mô phỏng phức tạp; xuất hiệu ứng đã bake sang Unity bằng VAT/flipbook. Có MCP do cộng đồng phát triển qua `hrpyc`, chưa kết nối trong phiên này; cần cài và kiểm tra riêng. [VFX SideFX](https://www.sidefx.com/products/houdini/vfx/), [VAT](https://www.sidefx.com/docs/houdini/nodes/out/labs--vertex_animation_textures-3.0.html), [Houdini MCP cộng đồng](https://github.com/arybashov/houdini-mcp).
3. **Cascadeur**: lựa chọn cho dáng lao, né và chuyển trọng tâm nhân vật; trao đổi FBX với Unity. Chưa xác nhận một MCP phù hợp, vì vậy không coi là công cụ điều khiển trực tiếp đang có. [Quy trình trao đổi FBX](https://cascadeur.com/help/getting_started/import_fbxdae).

Blender MCP và Unity MCP hiện là bộ công cụ đã kết nối. Không cần thay engine hay cài công cụ khác để chơi các cơ chế vừa thêm.
