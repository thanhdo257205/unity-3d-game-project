# Chơi thử Phàm Kiếm Lục

Hướng dẫn cho scene sân tập hiện có trong repository. Các phím dưới đây là cấu hình mặc định; có thể thay đổi khi chỉnh Inspector hoặc dữ liệu chiêu.

## Bắt đầu

1. Mở project bằng Unity **6000.6.3f1** và chờ import asset hoàn tất.
2. Mở scene [`SanTap.unity`](../Assets/_Game/Scenes/SanTap.unity), rồi nhấn **Play**.
3. Bấm chuột trái vào cửa sổ **Game** để khóa chuột. Nhấn **Esc** khi cần thả chuột.
4. Nhấn **F** để chuyển giữa góc nhìn sau vai và góc nhìn thứ nhất. Đóng bảng Admin trước khi điều khiển camera.

## Di chuyển và phi kiếm

| Phím | Thao tác |
| --- | --- |
| **W/A/S/D** hoặc phím mũi tên | Di chuyển theo hướng camera |
| **Chuột** | Xoay camera khi chuột đang khóa |
| **Shift trái** | Chạy trên mặt đất; tăng tốc khi bay |
| **Space** | Nhảy trên mặt đất; cất cánh hoặc bay lên khi cánh đã mở |
| **1–6 / lăn chuột** | Chọn ô trên thanh vật phẩm |
| **Chuột trái** | Sử dụng vật phẩm đang chọn; đánh cận chiến khi đang dùng vũ khí phù hợp |
| **Q** | Bật/tắt Ngự Kiếm Thuật |
| **Z / X / C** | Chuyển đội hình Kiếm Luân / Kiếm Hoàn / Mũi tên; C có thể phát lệnh lao kiếm |
| **Tab** | Khóa/bỏ khóa mục tiêu đang ngắm khi Ngự Kiếm Thuật đang bật |
| **Giữ chuột phải** | Phóng phi kiếm khi Ngự Kiếm Thuật đang bật |
| **R** | Thu kiếm, hủy lệnh truy kích |

## Bay và Phá Không

1. Chọn ô có **Phong Lôi Sí** trên thanh vật phẩm (mặc định ô **4**), rồi bấm chuột trái để mở cánh. Cần đủ pháp lực.
2. Giữ **Space** khoảng **0,3 giây** để cất cánh. Nếu đang ở trên không, bấm Space sẽ cất cánh ngay.
3. Khi bay: **W/A/S/D** điều hướng, **Space** lên cao, **Ctrl** hạ thấp và **Shift trái** tăng tốc.
4. Chọn lại ô chứa **Phong Lôi Sí** và chờ cánh mở xong. **Giữ E** để ngắm Phá Không, **nhả E** để thi triển; bấm **chuột phải trong lúc giữ E** để hủy ngắm.
5. Bấm **Space hai lần nhanh** để bắt đầu hạ cánh. Tắt cánh giữa không trung có thể khiến nhân vật rơi và mất máu.

Cánh tiếp tục tiêu hao pháp lực khi mở, kể cả lúc đứng đất. Xem thanh pháp lực trước khi bay cao. Góc nhìn thứ nhất cũng dùng được khi đang bay.

Phá Không dùng được cả trên đất lẫn khi bay, nhưng chỉ khi đang chọn vật phẩm Phong Lôi Sí. Cánh vẫn mở sau khi đổi ô không có nghĩa là E dùng được ở ô khác. Mặc định chiêu tốn **50 pháp lực**, hồi **6 giây**; điểm đến quá gần hoặc không đủ khoảng trống sẽ không hợp lệ. Vật cản và trần bay giới hạn điểm đến.

## Thần thông và bộ chiêu theo video

| Phím | Chiêu / cách sử dụng |
| --- | --- |
| **Giữ G** | Hộ Thể; thả sau khi đỡ đòn để phản kích Kim Liên |
| **T** | Kim Liên tụ lực rồi phóng |
| **H** | Âm Dương Băng Hỏa Kiếm |
| **J** | Ngưng Quang Bảo Kính — bảo vệ và công kích |
| **K** | Luân Hồi Thần Quang — bảo vệ và phát xung |
| **L** | Huyết Kiếm — phóng kiếm đỏ |
| **Y** | Kim Lôi Kiếm — lôi kích và choáng |
| **B** | Kim Ti Linh Võng — khống chế khu vực |
| **U** | Khốn Tâm Châu — khống chế khu vực |

Bảy chiêu H/J/K/L/Y/B/U có pháp lực, thời gian tụ lực và hồi chiêu riêng. HUD hiển thị trạng thái ở cạnh phải. Chờ thu thế xong trước khi ra chiêu tiếp theo; bị đánh lúc tụ lực sẽ ngắt chiêu. Có thể thử **J → H** để tấn công dưới lớp bảo vệ, hoặc **U/B → L/Y** để khống chế rồi gây sát thương.

Chi tiết tham chiếu, chỉ số và giới hạn của animation nằm trong [bộ chiêu Nam Cung](NamCung_ReferenceCombat.md). Huyết Kiếm, Kim Lôi Kiếm và Kim Ti Linh Võng là tên mô tả dùng trong game.

## Thử với yêu thú

Sân tập đã có một yêu thú Warrok. Để tạo thêm, nhấn phím **dấu huyền cạnh số 1** để mở Admin, chọn **Sinh Yêu thú**, rồi đóng bảng để tiếp tục điều khiển. Có thể dùng các tùy chọn thử nghiệm trong bảng để quan sát chiêu lâu hơn.

Xem [thông tin yêu thú](YeuThu_Warrok.md) về model đã tối ưu và các đòn đánh.

## Khi thao tác chưa hoạt động

- **Không xoay camera hoặc không nhận phím chiêu:** bấm vào Game để khóa chuột; đóng Admin và kiểm tra nhân vật còn sống.
- **Không cất cánh:** kiểm tra đã mở Phong Lôi Sí, còn pháp lực và giữ Space đủ lâu.
- **E không thi triển:** chọn lại ô Phong Lôi Sí, đợi cánh mở xong, kiểm tra pháp lực/hồi chiêu và ngắm điểm đến hợp lệ. Chuột phải khi đang giữ E sẽ hủy ngắm.
- **Chiêu chưa xuất hiện:** kiểm tra hồi chiêu, pháp lực và chờ động tác đang thi triển kết thúc. Một chiêu có tụ lực sẽ không phát đòn ngay khi bấm.
- **Không tìm thấy video tham chiếu sau khi clone:** `REF/` chỉ được giữ local và nằm trong `.gitignore`; không cần video đó để chơi scene sân tập.

## Một lượt kiểm tra nhanh

- [ ] Vào SanTap, khóa/thả chuột, đi/chạy/nhảy và đổi góc nhìn bằng F.
- [ ] Mở cánh, cất cánh, lên/xuống và Phá Không bằng E.
- [ ] Bật Q, khóa mục tiêu bằng Tab, phóng kiếm rồi thu bằng R.
- [ ] Thử từng chiêu H/J/K/L/Y/B/U, quan sát tụ lực và hồi chiêu trên HUD.
- [ ] Sinh yêu thú qua Admin và thử một chuỗi bảo vệ hoặc khống chế.

Đây là checklist chơi thử thủ công, không phải báo cáo rằng các bước vừa được chạy lại. Các báo cáo kiểm tra kỹ thuật trước đó được mô tả trong tài liệu của từng tính năng.
