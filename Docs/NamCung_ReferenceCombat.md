# Chiêu thức người chơi theo video Nam Cung Khuyết – Thiên Nam Song Thánh

Đã phân tích video local dài **12:28,4**, 1280×720, 30 fps. Phạm vi theo lựa chọn người dùng: **chỉ nhân vật người chơi**. Không thêm boss hay sửa AI yêu thú cho bộ chiêu này.

Đối chiếu toàn timeline bằng 125 ảnh cách nhau 6 giây, sau đó xem 120 ảnh chi tiết ở các đoạn giao chiến. Bảng dưới phân biệt tên xuất hiện trong video với tên mô tả dùng trong game.

| Phím | Chiêu | Đoạn tham chiếu | Hình ảnh và động tác chính | Gameplay |
| --- | --- | --- | --- | --- |
| **U** | Khốn Tâm Châu | 00:46–00:58, 01:18–01:24 | Châu tím ở lòng bàn tay, một tay đưa ra trước, vòng trường lực | 20 pháp lực; giam/choáng 1,8 s, hồi 7 s |
| **H** | Âm Dương Băng Hỏa Kiếm | 02:10–02:18, 03:20–03:34 | Hai kiếm lửa cam/băng xanh, dang tay rồi hợp tay trên cao, luồng khí xoắn | 28 pháp lực; sát thương theo nhịp, hồi 6 s |
| **J** | Ngưng Quang Bảo Kính | 04:29–04:47 | Niệm quyết trước ngực, năm mặt kính ngọc, tia sáng hội tụ và màn chắn | 30 pháp lực; hộ thể 3,6 s và tia công kích, hồi 10 s |
| **K** | Luân Hồi Thần Quang | 05:06–05:18, 09:42–10:12 | Tâm tối, vành lửa cam, quầng tím, mở tay và phát xung | 38 pháp lực; chắn đạn và xung phía trước, hồi 12 s |
| **L** | Huyết Kiếm* | 07:38–08:08 | Rút tay cạnh vai rồi đẩy kiếm đỏ lớn, các sợi huyết khí cuộn theo | 30 pháp lực; vật bay có va chạm và nổ nhỏ, hồi 7 s |
| **Y** | Kim Lôi Kiếm* | 09:54–10:06 | Một tay giơ cao, kiếm vàng dựng đứng, lôi quang phân nhánh | 38 pháp lực; đòn lôi và choáng 1,2 s, hồi 10 s |
| **B** | Kim Ti Linh Võng* | 10:45–11:12 | Niệm quyết rồi mở hai tay, lưới vàng đan dày xen khí tím | 24 pháp lực; khống chế khu vực 2,8 s, hồi 8 s |

\* Tên mô tả để phân biệt trong game; chưa xác nhận là tên chính thức của chiêu trong phim. Các chiêu có thể dùng khi đứng đất hoặc khi đang bay bằng Phong Lôi Sí.

## Phong cách chiến đấu đã đưa vào gameplay

- Điều khiển pháp bảo ở tầm xa, thân người ổn định và có động tác chuẩn bị dễ nhận ra.
- Ba pha rõ ràng: **tụ lực → xuất chiêu → thu thế**. Mốc 0,56 trong animation Blender điều khiển đúng thời điểm sinh hiệu ứng; có thể chỉnh thời gian tụ lực trên từng asset.
- Bảo kính/thần quang còn tồn tại sau khi thu thế, cho phép nối một chiêu tấn công dưới lớp bảo vệ. Chặn đạn dùng cùng hệ thống projectile hiện có.
- Có thể phối **U hoặc B → L/Y**, hoặc **J → H**. Dùng di chuyển và Phá Không E để đổi vị trí giữa những lần thi triển.
- Bị đánh trong lúc tụ lực sẽ ngắt chiêu và trả quyền điều khiển. Chiêu đã tiêu hao pháp lực và bắt đầu hồi khi khởi niệm, không hoàn lại khi bị ngắt.
- Các đòn tôn trọng vật cản; kiếm đỏ quét va chạm dọc đường bay để tránh xuyên mục tiêu khi khung hình dài.

**F** vẫn đổi góc nhìn, **E** vẫn Phá Không. Các phím Q/Z/X/C/R, G/T và hệ thống cận chiến hiện có tiếp tục sử dụng như trước.

## Blender và animation

Nguồn chỉnh sửa: `ArtSource/Blender/NamCung_ReferenceCombat.blend`.

Script: `ArtSource/ReferenceCombat/build_blender_reference.py`. Bảy action dài 55 frame ở 30 fps, được dựng keyframe và giải cánh tay hai khớp từ tư thế quan sát trong video. Đây là **animation dựng tay theo tham chiếu**, không phải kết quả motion capture tự động hay bản sao từng frame của phim. Rig hiện tại không có xương ngón tay riêng nên thủ quyết bằng ngón tay chưa thể giống đầy đủ.

Đã giữ chuyển động chân/root hiện có bằng AvatarMask thân trên và thêm lớp `NCK_Reference` phía trên controller hiện hữu. Không thay thế các clip locomotion hoặc các clip Rokoko đang dùng. Các pháp bảo mới dựng trong Blender có 176 / 116 / 816 tam giác tương ứng huyết kiếm / lôi kiếm / khung bảo kính.

## Chỉnh tiếp và kiểm tra

- Dữ liệu từng chiêu: `Assets/_Game/Data/ReferenceCombat/` (phím, tên, màu, pháp lực, thời gian, tầm và sát thương).
- FBX, vật liệu và AvatarMask: `Assets/_Game/Models/ReferenceCombat/`.
- Menu **Phàm Kiếm Lục > Chiêu theo video**: nhập animation, dựng/gắn bộ chiêu, kiểm tra gameplay và chụp ảnh từng chiêu.
- `ReferenceCombatValidation.Run()` thực hiện 82 kiểm tra: bảy chiêu, phím riêng, Avatar Humanoid, chi phí/hồi chiêu, ngắt/thu thế, thời điểm xuất VFX, sát thương không lặp trong cùng nhịp, chặn bởi tường, giữ chân qua AvatarMask, nối hệ thống chắn đạn và ngắt khi bị đánh.
- Báo cáo lần chạy: `Library/ReferenceCombat/validation.txt`. Ảnh sample từng chiêu: `Library/ReferenceCombat/unity_*.png`. Các file kiểm tra này không đưa lên Git.
- Chi tiết mốc quan sát và mức chắc chắn: `ArtSource/ReferenceCombat/analysis.json`. Trích ảnh lại bằng `analyze_reference.py`; video gốc vẫn chỉ ở `REF/` local.

Độ sáng được giới hạn để giữ nét nhân vật và tránh lặp lại tình trạng vòng kiếm xanh quá chói. Bản dựng giữ các đặc điểm nhìn rõ trong video; hình thể nhân vật, trang phục, biểu cảm và các cảnh cắt phim không được thay thế bằng mô hình của phim.
