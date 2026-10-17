# Thần thông — bản hiệu ứng thứ hai

Mục tiêu: thay phần trình bày đơn giản của bản đầu bằng hiệu ứng có lớp, có quá trình tụ/phóng/tan và phản ứng tại điểm va chạm. Giữ cơ chế G/T, sát thương, pháp lực, hồi chiêu và quy tắc thu kiếm khi địch chết.

## Thay đổi hình ảnh

- **Hộ thể:** màn ngọc dùng shader mật độ nhiễu chuyển động, viền nhìn nghiêng và các dải khí xoay khác chiều. Hạt khí trôi quanh thân. Đòn chạm tạo gợn sóng lan trên mặt màn; thả G thì khí tan trong 0,3 giây thay vì biến mất ngay.
- **Kim Liên:** làm lại model Blender thành 24 cánh cong có lòng, UV riêng theo cánh và lõi hạt nổi. Vật liệu dùng nền vàng đồng, gân sáng, viền và ánh ở tâm, thay cho phủ toàn bộ mô hình bằng vàng sáng phẳng. Nguồn `ArtSource/Blender/KimLien.blend`, FBX giữ đường dẫn cũ.
- **Tụ lực:** các hạt hội tụ từ ngoài vào lõi, ba dải khí cuộn quanh hoa, lõi tăng nhịp theo tiến trình tụ. Khi phóng, các dải tụ nhường chỗ cho vệt bay và khí kéo phía sau.
- **Va chạm:** lõi xuất hiện ngắn, các mặt sóng có độ trễ, mảnh sáng bắn ra và khí dư trôi chậm hơn. Tia và khói có giới hạn số hạt và tuổi thọ; không dùng chớp trắng phủ màn hình.
- **Phi kiếm:** vệt lõi mảnh đi cùng vệt khí rộng có mật độ biến thiên và hạt khí phía đuôi. Giảm emission thân kiếm để giữ được hình lưỡi thay vì cháy thành vệt trắng. Dùng cùng ngôn ngữ xanh ngọc/vàng cho hai bên.
- **Mức sáng:** giữ sét cánh 2,4/3,2 đã chỉnh theo yêu cầu trước. Không tăng Bloom toàn cảnh trong lần này; phần vật liệu khí thần thông giảm radiance từ 3,2 xuống 1,8 và vệt kiếm dùng 2,3.

## Xem và dựng lại

- Bấm Play trong `SanTap`, dùng **G** và **T** như trước.
- `ThanThongBuilder.Build()` dựng lại model/vật liệu/prefab thần thông; `SwordReferenceBuilder.Build()` dựng lại kiếm. Chạy trong Edit Mode.
- Menu **Phàm Kiếm Lục → Thần thông → Quay trình diễn VFX trong Play** ghi 180 khung hình ở 24 fps, gồm chặn đòn, tụ lực/phóng Kim Liên, va chạm và đấu phi kiếm. Dùng camera trình diễn riêng trong chính scene SanTap; không thay camera gameplay, không lưu nhân vật thử vào scene. Khung hình nằm trong `Library/TienHiepPreview/`.
- Clip xem trước: `Assets/Screenshots/vfx_v2_showcase.mp4`.
- Ảnh các pha: `vfx_v2_ward.png`, `vfx_v2_charge.png`, `vfx_v2_impact.png`, `vfx_v2_swords.png` trong cùng thư mục Screenshots.
- Ảnh bản đầu được giữ trong `ArtSource/References/VFX_Before/` để đối chiếu.

## Giới hạn

Kiểm tra trong Play Mode: 17 kiểm tra thần thông và 24 kiểm tra đấu phi kiếm đạt. Báo cáo `Library/ThanThongValidation_VFX2.txt` và `Library/SwordReferenceValidation_VFX2.txt`. Clip được quay từ 180 khung hình thực trong Unity, 960×600, 24 fps; có camera trình diễn riêng để quan sát rõ các pha hiệu ứng.

Đây là hiệu ứng thời gian thực trong sân tập hiện có. Địa hình, ánh sáng môi trường và bộ animation kết ấn vẫn dùng tài nguyên game hiện tại. Chưa có môi trường điện ảnh, pháp tướng khổng lồ hay bộ diễn xuất nhân vật giống từng khung hình trong phim.
