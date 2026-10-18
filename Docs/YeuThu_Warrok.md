# Yêu thú Warrok

Nguồn: `character.fbx` và 19 file animation trong `Creature Pack.zip` do người dùng cung cấp.

## Tối ưu bằng Blender 5.2

| Chỉ số | Model gốc | Bản game |
| --- | ---: | ---: |
| Tam giác | 12.626 | 6.059 |
| Đỉnh mesh trong Blender | 6.557 | 3.235 |
| Xương | 81 | 81 |
| Đỉnh không có trọng số da | 0 | 0 |

Dùng Decimate Collapse tỷ lệ 0,48, áp dụng trước Armature; giữ UV và trọng số da. Giảm 52% số tam giác. Bản chỉnh sửa nằm ở `ArtSource/Blender/YeuThu_Optimized.blend`; model chạy trong game là `Assets/_Game/Models/Enemies/YeuThu/YeuThu.fbx`. File gốc trong Downloads không thay đổi.

## Trong game

- Một yêu thú được đặt tại sân tập, gần tọa độ `(-8, 0.1, 12)`.
- Mở bảng Admin bằng phím dấu huyền cạnh số 1, chọn **Sinh Yêu thú** để tạo thêm.
- 180 máu, cảnh giới Trúc Cơ sơ kỳ; đi tuần, phát hiện và đuổi theo người chơi, đánh cận chiến, nhận sát thương/choáng, chết và rơi túi trữ vật.
- Bốn đòn luân phiên: quét tay, đấm, chồm đánh và đập mạnh. Mỗi đòn có thời gian chuẩn bị, một lần kiểm tra sát thương và thời gian hồi; có thể né hoặc ngắt bằng choáng. Không đánh xuyên tường.
- Animator dùng Humanoid để chuyển hoạt ảnh sang model đã giảm lưới. NavMeshAgent điều khiển di chuyển, tắt root motion để hình ảnh không kéo lệch collider.
- Đã nhập đủ 19 animation; controller dùng 9 clip cho đứng/đi/chạy, gầm, bốn đòn đánh và chết. Các clip quay, nhảy và phô cơ được giữ trong thư mục Animations để dùng thêm.

Prefab: `Assets/_Game/Prefabs/YeuThu.prefab`. Có thể dựng lại bằng menu **Phàm Kiếm Lục > Yêu thú > Dựng prefab và thêm vào sân tập**. Script builder chỉ thêm yêu thú và tham chiếu Admin, không dựng lại sân tập.

## Kiểm tra trong Unity

Đã kiểm tra Avatar, pose của các state chiến đấu, model 6.059 tam giác, vị trí trên NavMesh, sát thương một lần mỗi đòn, ngắt đòn bằng choáng, vật cản chặn đòn đánh, chết/hồi sinh và sinh thêm bằng Admin. Unity biên dịch và chạy các bước này không báo lỗi.
