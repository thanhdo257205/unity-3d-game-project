# PHÀM KIẾM LỤC — Tài liệu thiết kế & Prompt cho Claude Code

> Game 3D kiếm tiên hiệp, lấy cảm hứng *phong cách* từ thể loại "phàm nhân tu tiên": nhân vật tư chất kém, cẩn trọng, điều khiển **bầy phi kiếm**.
> Toàn bộ tên người, tông môn, chiêu thức, bảo vật trong file này là **tự thiết kế**, không dùng tên/hình nhân vật của bất kỳ phim/truyện nào.
>
> File này nằm ở thư mục gốc dự án Unity và được Claude Code tự đọc. Mỗi giai đoạn ở **Phần 12** có prompt mẫu để copy-paste.

---

## TRẠNG THÁI DỰ ÁN (cập nhật khi xong mỗi giai đoạn)

- Thư mục dự án Unity: `PRJ_Game_3D\PRJ_Game_3D_PNTT` (thư mục cha `PRJ_Game_3D` là nơi mở phiên Claude Code, chứa `.mcp.json`).
- Unity `6000.6.3f1`, URP 17.6, Input System 1.20. MCP: **MCP for Unity (CoplayDev)** chạy HTTP ở `http://127.0.0.1:8080/mcp`; Blender MCP qua `uvx blender-mcp`.
- Khi bắt đầu phiên mới: gọi `set_active_instance` với `PRJ_Game_3D_PNTT@<hash>` (xem resource `mcpforunity://instances`).
- Giai đoạn đã xong: **0** (chuẩn bị dự án), **1** (sân tập `SanTap`: nhân vật viên nang, camera Cinemachine sau vai phải, Bloom), **3** (bầy kiếm + 3 đội hình, tâm ngắm, hình nộm). Giai đoạn 2 (kiếm Blender) đã làm trước dưới dạng phi kiếm Thanh Trúc.
- Giai đoạn 3: `SwordSwarm` (object `BayKiem`) sinh `FlyingSword` từ prefab `ThanhTrucKiem` (có TrailRenderer), xếp theo `IFormation` (`FanFormation`, `RingFormation`, `ArrowFormation`). Phím 1/2/3 đổi đội hình, chuột trái bắn lẻ, phím 3 lao cả đàn (bật `volleyOnKey3`: bấm 3 là xếp hàng rồi lao luôn), R gọi kiếm về. `Damageable` + `TrainingDummy` (prefab `HinhNop`, 5 con trong SanTap). `CrosshairUI` là tâm ngắm. Đổi "Số kiếm" trên `BayKiem` lúc đang Play để thử 3/9/24.
- Đã chỉnh sau lần chơi thử đầu (lệch so với bảng thiết kế gốc, theo ý người chơi): kiếm bắn lẻ 80 m/s, loạt Mũi tên 55 m/s (gốc 30); Quạt mở 180°, bán kính 1.8m, cách lưng 0.85m (gốc 160°/1.5m); khi lao kiếm vuốt dài 1.4x và vệt sáng rộng ra; `HitFeedback` (trên `BayKiem`) dừng hình 0.05s (hạ gục 0.1s) + rung camera qua `CameraOrbit.AddShake`.
- Giai đoạn 6 (nhân vật thật): prefab `Assets/_Game/Prefabs/NhanVat_Model.prefab` (con của `NhanVat` trong SanTap, local y -0.05), FBX Mixamo ở `Assets/_Game/Models/Characters/NhanVat/`. `NhanVat@Idle.fbx` là nguồn Avatar Humanoid; 11 file còn lại Copy From Other Avatar. Animator `NhanVat_Animator.controller`: lớp Base (Blend Tree `Speed`: Idle 0 / Walk 1.98 / Run 6.55 m/s đúng tốc độ gốc của clip, Jump, Fall, Landing) + lớp `UpperBody` (Avatar Mask không có Root và chân: PhongKiem khi `SwordSwarm.StrikeFired`, NiemQuyet khi `FormationChanged`). `PlayerAnimator` nối PlayerController/SwordSwarm với Animator. Dam1, Dam2, Da, BiDanh, Chet đã nhập và cấu hình, CHƯA nối vào controller (Giai đoạn 4).
- Bài học Animation: KHÔNG bật Bake Into Pose XZ cho Walk/Run (bake giữ chuyển động tiến trong tư thế khiến thân trôi khỏi GameObject); clip Mixamo "In Place" thực tế vẫn có root motion, chỉ cần tắt Apply Root Motion. Walk/Run có cùng pha bước chân nên không cần Cycle Offset.
- Điểm neo bầy kiếm: `SwordSwarm.anchor` = `NeoBayKiem` (con của xương Spine2); fan/ring/arrow `heightOffset` đã hạ -0.35/-0.40/-0.20 vì neo cao hơn cũ.
- Tà áo bị kéo giãn nhiều khi chạy/nhảy (auto-rig Mixamo gán vải vào xương chân); đã đo: khi chạy ~10% cạnh dài >1.5x. Nếu cần sửa: gán lại skin weight trong Blender hoặc dùng cloth.
- Quy ước kiếm: pivot giữa chuôi, mũi +Z, mặt phẳng lưỡi vuông góc trục Y. Kiếm do SwordSwarm.LateUpdate điều khiển (FlyingSword.Tick), không tự Update.
- Scene chính để thử: `Assets/_Game/Scenes/SanTap.unity`. Scene `TestKiem` chỉ để xem model phi kiếm.
- Script Giai đoạn 1: `PlayerController` (CharacterController: đi/chạy/nhảy) và `CameraOrbit` (điểm ngắm xoay bằng chuột, Cinemachine `CM_SauVaiPhai` bám theo nó).
- Phi kiếm: prefab `Assets/_Game/Prefabs/ThanhTrucKiem.prefab` (2.105 tam giác, dài 1m, pivot giữa chuôi, mũi +Z). Giai đoạn 3 dùng prefab này thay Thanh Ngọc Kiếm.
- Mẹo test qua MCP: Unity chạy nền thì Play mode gần như đứng hình; cần `Application.runInBackground = true` + `EditorApplication.QueuePlayerLoopUpdate()` mỗi tick. Giả lập phím qua `QueueStateEvent` không ổn định, nên kiểm tra logic bằng cách gọi `Update()` qua reflection.
- Ảnh chụp thử do MCP tạo nằm ở `Assets/Screenshots/` (đã bị git bỏ qua).

---

## 0. QUY TẮC CHO CLAUDE CODE (đọc trước khi làm bất cứ việc gì)

Người dùng **không biết code và không biết dựng 3D**. Vì vậy:

1. **Làm từng bước nhỏ.** Mỗi lần chỉ làm một tính năng, xong thì dừng, mô tả ngắn gọn bằng tiếng Việt cách người dùng kiểm tra (bấm Play, bấm phím gì, phải thấy gì).
2. **Git commit sau mỗi bước chạy được**, message tiếng Việt ngắn, ví dụ `Thêm đội hình Vòng Kiếm`. Trước thay đổi lớn, nhắc người dùng rằng có thể quay lại commit trước.
3. **Không xóa hay ghi đè file/asset có sẵn** khi chưa hỏi.
4. **Mọi con số cân bằng game** (sát thương, tốc độ, số kiếm, pháp lực...) đặt trong **ScriptableObject** hoặc biến `[SerializeField]` có tooltip tiếng Việt, để người dùng tự chỉnh trong Inspector mà không cần sửa code.
5. **Code có chú thích tiếng Việt** ở đầu mỗi class và mỗi hàm quan trọng.
6. **Không dùng asset trả phí** khi chưa hỏi. Ưu tiên: primitive của Unity → mô hình Blender đơn giản qua MCP → asset miễn phí.
7. **Khi có lỗi Console**, tự đọc Console qua Unity MCP và sửa trước khi báo xong.
8. **Không tối ưu sớm.** Ưu tiên chạy được và chơi vui; tối ưu khi người dùng báo giật lag.
9. Khi không chắc người dùng muốn gì về *cảm giác chơi* (nhanh/chậm, khó/dễ), **làm bản mặc định hợp lý và nói rõ thông số nào chỉnh được**, không dừng lại hỏi.

### Thông số kỹ thuật
- **Engine:** Unity 6, render pipeline **URP**.
- **Input:** Input System mới (không dùng Input Manager cũ).
- **Camera:** Cinemachine (góc nhìn thứ ba).
- **Hiệu ứng:** Particle System (VFX Graph chỉ khi thật cần), Post-processing **Bloom** bật từ đầu.
- **Ngôn ngữ UI trong game:** tiếng Việt (font hỗ trợ dấu, ví dụ Be Vietnam Pro / Noto Sans).

### Cấu trúc thư mục
```
Assets/
  _Game/
    Scripts/
      Player/        (di chuyển, pháp lực, thần thức, pháp thể)
      Swords/        (phi kiếm, bầy kiếm, đội hình)
      Combat/        (sát thương, thuộc tính, máu, đối kháng)
      Enemies/       (AI quái, tà tu, boss)
      Cultivation/   (cảnh giới, đột phá, đan dược)
      Items/         (pháp bảo, phù lục, trận bàn)
      UI/
      Core/          (GameManager, lưu game, sự kiện)
    Data/            (ScriptableObject: kiếm, cảnh giới, quái, vật phẩm)
    Prefabs/
    Models/          (FBX từ Blender / nguồn khác)
    Materials/
    VFX/
    Audio/
    Scenes/
```

---

## 1. TỔNG QUAN GAME

| Mục | Nội dung |
|---|---|
| **Tên tạm** | Phàm Kiếm Lục |
| **Thể loại** | Nhập vai hành động 3D góc nhìn thứ ba, chơi đơn |
| **Nền tảng** | PC (Windows) |
| **Cảm giác cốt lõi** | Từ một kẻ chỉ điều khiển nổi 3 thanh kiếm gỉ, dần trở thành người mở ra cả trăm thanh kiếm che kín bầu trời |
| **Tinh thần** | Cẩn trọng, tính toán, biết giấu thực lực, biết chạy. Thắng bằng chuẩn bị chứ không bằng may mắn |

### Ba trụ cột thiết kế
1. **Bầy kiếm** — vũ khí chính là cả đàn phi kiếm có đội hình, không phải một thanh kiếm.
2. **Thần thức = sức mạnh nhìn thấy được** — mỗi lần đột phá, số kiếm sau lưng tăng lên rõ ràng.
3. **Phàm nhân cẩn trọng** — chênh lệch cảnh giới rất lớn; người chơi dùng bẫy, trận pháp, phù lục, ẩn tu vi và chạy trốn.

### Vòng chơi
- **Vòng ngắn (vài phút):** khám phá → gặp địch → chọn đội hình kiếm → thắng (hoặc chạy) → nhặt chiến lợi phẩm.
- **Vòng trung (một buổi):** về động phủ → luyện kiếm / luyện đan / chế phù → bế quan tăng tu vi → nhận nhiệm vụ mới.
- **Vòng dài:** đột phá đại cảnh giới → mở thêm số kiếm, đội hình mới, vùng đất mới.

---

## 2. PHONG CÁCH NGHỆ THUẬT

- **Stylized, không chân thực.** Tỉ lệ người hơi cách điệu, màu sắc bão hòa vừa phải, bóng đổ mềm. Lý do: dễ ghép asset từ nhiều nguồn mà vẫn đồng bộ.
- **Bảng màu chủ đạo:** xanh ngọc (kiếm của nhân vật chính), vàng kim (kiếm khí, ánh sáng), tím-trắng (lôi điện), đỏ sậm (tà tu, ma đạo).
- **Ánh sáng là linh hồn:** kiếm luôn có viền phát quang (emission) + vệt sáng (Trail Renderer). Bloom mạnh vừa phải.
- **Môi trường:** núi đá dựng đứng, mây lưng chừng núi, thác nước, rừng trúc, tông môn mái cong ngói xanh, động phủ trong vách núi.
- **Camera:** khi xuất tuyệt chiêu, camera kéo ra xa để thấy toàn bộ kiếm trận (giống cảnh người nhỏ đứng giữa bánh xe kiếm khổng lồ).

---

## 3. THẾ GIỚI

- **Vùng Vân Lĩnh:** dãy núi biên giới, linh khí loãng, nơi các tông môn nhỏ tranh giành mạch khoáng. Đây là vùng của toàn bộ bản demo.
- **Thanh Hà Tông:** tông môn hạng ba, nhân vật chính là đệ tử ngoại môn. Đệ tử bị bóc lột, tài nguyên ít, cạnh tranh khốc liệt.
- **Phường thị Lạc Thạch:** chợ tu sĩ dưới chân núi. Mua bán, nhận nhiệm vụ, nhưng cũng là nơi dễ bị để ý nếu khoe của.
- **Rừng Hắc Trúc:** khu săn yêu thú, có trúc mang lôi khí (nguyên liệu kiếm lôi).
- **Cổ Kiếm Trủng:** di tích chôn hàng nghìn thanh kiếm gãy của một kiếm tu thời thượng cổ. Dungeon cuối của demo.

---

## 4. NHÂN VẬT

### 4.1 Nhân vật chính — **Tô Mặc**
- **Xuất thân:** con trai thợ rèn ở làng dưới chân núi. Tư chất "tạp linh căn" (linh căn lẫn tạp, tu luyện chậm), suýt bị loại khỏi kỳ tuyển đệ tử.
- **Tính cách:** ít nói, kiên nhẫn, cực kỳ cẩn thận. Không bao giờ đánh trận không chắc thắng; luôn có đường lui.
- **Bí mật:** nhặt được **Tàn Kiếm Ngọc** — một miếng ngọc nứt có thể *hấp thu linh tính của kiếm gãy*, biến chúng thành phi kiếm mới cho bầy kiếm. Đây là lý do một kẻ tư chất kém lại điều khiển được nhiều kiếm hơn người cùng cảnh giới. **Nếu lộ, sẽ bị cường giả truy sát** (cơ chế: xem 6.6).
- **Ngoại hình:** thanh niên ~20 tuổi, tóc dài buộc nửa đầu, áo choàng xanh đen đơn giản của đệ tử ngoại môn, tay áo bó gọn (dân thợ rèn), một túi trữ vật bên hông. Không đeo kiếm bên người — kiếm ẩn trong túi, chỉ hiện ra khi xuất chiêu.
- **Animation cần có:** đứng, đi, chạy, nhảy, rơi, bay trên kiếm (đứng thẳng, hơi nghiêng người), niệm quyết (hai ngón tay chỉ lên), vung tay phóng kiếm, đỡ đòn, bị đánh, chết, ngồi thiền, quyền cước 3 đòn liên tiếp.

### 4.2 NPC
| Tên | Vai trò | Ghi chú |
|---|---|---|
| **Cố lão** | Lão già giữ Tàng Kinh Các, dạy Tô Mặc pháp quyết ngự kiếm | Bí ẩn, có vẻ biết về Tàn Kiếm Ngọc |
| **Liễu Thanh Vân** | Sư tỷ nội môn, kiếm tu băng | Đồng minh, thỉnh thoảng giao nhiệm vụ |
| **Bàn Ngũ** | Thương nhân béo ở phường thị | Mua bán, nhưng sẽ bán thông tin nếu được trả giá |
| **Triệu Liệt** | Đệ tử cùng khóa, đố kỵ | Đối thủ, boss nhỏ giữa game |
| **Huyết Bào Khách** | Tà tu Kết Đan, săn kiếm cổ | Boss cuối demo, mạnh hơn người chơi một đại cảnh giới |

### 4.3 Kẻ địch
| Tên | Loại | Hành vi | Yếu với |
|---|---|---|---|
| **Thanh Lang** | Yêu thú bầy đàn | Đi theo đàn 3–5 con, bao vây | Đội hình Vòng |
| **Thạch Viên** | Yêu thú to, chậm | Ném đá, đập đất gây choáng | Đội hình Mũi tên, Hỏa |
| **Thi Khôi** | Xác sống trong Cổ Kiếm Trủng | Chậm, máu trâu, hồi máu trong bóng tối | **Lôi** |
| **Tà tu Luyện Khí** | Tu sĩ | Dùng 1–3 phi kiếm, ném phù hỏa cầu, biết chạy khi yếu máu | Tùy |
| **Kiếm hồn** | Linh thể | Bay, xuyên tường, chỉ trúng đòn khi đang tấn công | Lôi, Phong |

---

## 5. HỆ THỐNG TU LUYỆN

### 5.1 Cảnh giới (demo chỉ cần Luyện Khí → Trúc Cơ; các tầng sau để dữ liệu sẵn)
| Cảnh giới | Tầng | Số kiếm tối đa (Thần thức) | Pháp lực | Mở khóa |
|---|---|---|---|---|
| Luyện Khí | 1–3 | 3 | 100 | Đội hình Mũi tên |
| Luyện Khí | 4–6 | 6 | 150 | Đội hình Quạt |
| Luyện Khí | 7–9 | 9 | 200 | Đội hình Vòng, ngự kiếm phi hành |
| Trúc Cơ | sơ/trung/hậu | 12 / 18 / 24 | 400 | Đại Kiếm Trận (bản nhỏ) |
| Kết Đan | sơ/trung/hậu | 36 / 48 / 60 | 1000 | Kiếm trận nhiều tầng |
| Nguyên Anh | — | 72+ | 2500 | (ngoài demo) |

### 5.2 Tăng tu vi
- **Bế quan tại động phủ:** ngồi thiền, tiêu hao linh thạch/đan dược, thời gian trong game trôi nhanh.
- **Chiến đấu:** hạ địch rơi linh khí, nhưng ít hơn bế quan.
- **Đột phá đại cảnh giới:** cần đan dược đặc biệt + chọn thời điểm. Có tỷ lệ thất bại (giảm bằng chuẩn bị). Thất bại → mất một ít tu vi, không mất máu.

### 5.3 Chênh lệch cảnh giới
- Mỗi **đại cảnh giới** chênh lệch → sát thương nhận vào ×2.5, sát thương gây ra ×0.4.
- Ý đồ: gặp kẻ mạnh hơn hẳn thì **không thể đánh trực diện**, phải dùng bẫy, địa hình, phù lục, hoặc chạy.

---

## 6. CHIẾN ĐẤU

### 6.1 Tài nguyên
- **Máu (Sinh lực).**
- **Pháp lực:** tiêu hao khi phóng kiếm, đổi đội hình, dùng pháp thuật. Hồi chậm theo thời gian, nhanh khi thiền.
- **Thần thức:** giới hạn **số kiếm điều khiển cùng lúc** (theo bảng 5.1). Bị đánh trúng mạnh → thần thức chấn động: tạm mất kiểm soát vài thanh kiếm (rơi xuống đất, phải gọi lại).
- **Kiếm ý:** thanh tích lũy khi kiếm đánh trúng. Đầy → dùng được Đại Kiếm Trận.

### 6.2 Bầy kiếm (hệ thống quan trọng nhất)
- Mỗi phi kiếm là một object riêng, bay mượt tới **vị trí mục tiêu** do đội hình quy định (dùng nội suy/lò xo, không dịch chuyển tức thời).
- Kiếm **luôn quay mũi theo hướng di chuyển**, có Trail Renderer và emission.
- Kiếm có **độ bền**: bị chém trúng nhiều sẽ mờ đi; vỡ thì biến mất, tự hồi lại sau thời gian hoặc khi nghỉ.

### 6.3 Bốn đội hình
| # | Tên | Hình dạng | Công dụng | Thông số mặc định |
|---|---|---|---|---|
| 1 | **Thủ Kiếm – Quạt** | Kiếm xòe hình quạt sau lưng, mũi chĩa ra ngoài | Tự động chặn đòn và đạn từ sau/hai bên; giảm sát thương nhận vào | Cung 160°, bán kính 1.5m, >12 kiếm thì xếp thêm lớp ngoài |
| 2 | **Hồi Kiếm – Vòng** | Vòng tròn quanh người, quay liên tục | Chém mọi thứ áp sát, chống bị bao vây | Bán kính 2.5m, quay 180°/giây, sát thương mỗi lần chạm |
| 3 | **Phá Kiếm – Mũi tên** | Xếp thành hàng dọc rồi lao thẳng mục tiêu | Sát thương dồn một điểm, xuyên giáp | Bay 30m/giây, mỗi kiếm cách nhau 0.08 giây; **sau đó phải thu về (1.5 giây), trong lúc này lưng trống** |
| 4 | **Vạn Kiếm Luân – Tuyệt chiêu** | Bánh xe kiếm khổng lồ dựng đứng trên trời, xoay rồi ép xuống | Diệt diện rộng | Cần đầy Kiếm ý; bán kính 8m; người chơi bất động 2 giây khi niệm (có thể bị cắt) |

- **Đổi đội hình:** phím 1–4, tốn một ít pháp lực, kiếm bay chuyển vị trí mất ~0.4 giây (người chơi nhìn thấy sự chuyển động — rất quan trọng cho cảm giác).
- **Bắn lẻ:** chuột trái phóng **một** thanh kiếm từ đội hình hiện tại về phía tâm ngắm rồi tự quay về. Giữ chuột trái = bắn liên tiếp.
- **Ý đồ chiến thuật:** kiếm đang ở ngoài đánh thì không bảo vệ. Người chơi phải cân nhắc khi nào tấn công dồn, khi nào giữ kiếm thủ.

### 6.4 Pháp thể song tu
- **Quyền cước:** chuột phải (hoặc phím F) đánh tay không 3 đòn liên tiếp, không tốn pháp lực. Dùng khi cạn pháp lực hoặc địch áp sát.
- **Luyện thể** (cây kỹ năng riêng): tăng máu, giảm sát thương, và kỹ năng **Đoạt Kiếm** — đỡ đúng thời điểm khi phi kiếm địch lao tới thì bắt lấy, tạm thời thêm vào bầy kiếm của mình.
- Người chơi tự phân điểm giữa **Pháp** (kiếm, pháp lực) và **Thể** (máu, cận chiến).

### 6.5 Thuộc tính kiếm
| Thuộc tính | Màu | Hiệu ứng | Khắc chế |
|---|---|---|---|
| **Lôi** | Tím-trắng, tia sét nhảy quanh kiếm | Choáng ngắn, sét lan sang địch gần | Thi khôi, kiếm hồn, ma vật |
| **Hỏa** | Cam-đỏ | Thiêu đốt theo thời gian | Yêu thú, thực vật |
| **Băng** | Xanh nhạt, hơi lạnh | Làm chậm, đóng băng nếu cộng dồn | Địch nhanh |
| **Phong** | Xanh lục nhạt, vệt gió | Kiếm bay nhanh và xa hơn 40% | Địch bay |

Gắn thuộc tính bằng cách **luyện chế** tại động phủ (cần nguyên liệu), áp lên toàn bộ bầy kiếm hoặc một phần.

### 6.6 Ẩn tu vi & bị truy sát
- **Liễm Khí Thuật:** bật/tắt (phím H). Khi bật, cảnh giới hiển thị với NPC/địch thấp hơn thật 1–2 tầng.
  - Địch đánh giá thấp bạn → lơ là, không gọi đồng bọn, đánh lén có sát thương ×2.
  - Nhược điểm: khi bật, số kiếm tối đa giảm một nửa (phải kìm thần thức).
- **Mức Chú Ý:** dùng quá nhiều kiếm trước mặt người khác, bán bảo vật hiếm ở phường thị, khoe của → tăng mức Chú Ý. Cao quá → tà tu mạnh hơn bắt đầu xuất hiện phục kích.

### 6.7 Đối kháng chiêu lớn
- Khi hai chiêu lớn va chạm trực diện (ví dụ Phá Kiếm của người chơi gặp kiếm khí của boss): thời gian chậm lại, xuất hiện vòng tròn đối kháng. Người chơi **bấm liên tục phím Space** trong 3 giây.
- Kết quả = (số lần bấm × hệ số) + chênh lệch pháp lực/thần thức. Thua đối kháng → bị đẩy lùi và mất kiếm; thắng → địch choáng.

### 6.8 Chạy trốn
- **Độn Quang Phù:** dịch chuyển nhanh 15m về phía trước.
- **Độn Thổ Phù:** chui xuống đất, biến mất 3 giây, trồi lên chỗ khác.
- **Ngự kiếm phi hành** (từ Luyện Khí tầng 7): đứng lên kiếm bay đi.
- Thoát khỏi trận chiến với kẻ mạnh hơn **được thưởng** ("Cẩn Trọng" — kinh nghiệm thần thức), để khuyến khích lối chơi đúng tinh thần.

---

## 7. VŨ KHÍ & PHÁP BẢO

### 7.1 Phi kiếm
| Tên | Độ hiếm | Sát thương | Độ bền | Đặc biệt |
|---|---|---|---|---|
| **Thiết Kiếm** | Thường | 8 | 30 | Kiếm khởi đầu, xám bạc, hơi gỉ |
| **Thanh Ngọc Kiếm** | Tốt | 14 | 50 | Xanh ngọc, mỏng, bay nhanh |
| **Lôi Trúc Kiếm** | Hiếm | 18 | 45 | Làm từ trúc Rừng Hắc Trúc, mang sẵn **Lôi** |
| **Xích Đồng Kiếm** | Hiếm | 22 | 70 | Nặng, chậm, mang sẵn **Hỏa** |
| **Cổ Kiếm Tàn Phiến** | Cực hiếm | 30 | 40 | Kiếm gãy từ Cổ Kiếm Trủng, hồi sinh qua Tàn Kiếm Ngọc; có kiếm ý riêng |

- Bầy kiếm có thể **trộn nhiều loại kiếm**. Kiếm tốt hơn đặt ở vị trí tấn công, kiếm thường làm khiên.

### 7.2 Pháp bảo & vật phẩm
| Loại | Ví dụ | Cách dùng |
|---|---|---|
| **Hộ thuẫn** | Quy Giáp Thuẫn (mai rùa xanh) | Phím Q: màn chắn trước mặt, chặn hoàn toàn 1 chiêu lớn, hồi 20 giây |
| **Phù lục** (dùng 1 lần) | Hỏa Cầu Phù, Độn Quang Phù, Độn Thổ Phù, Ẩn Thân Phù | Phím Z/X/C chọn – phím E dùng |
| **Trận bàn** | Khốn Trận (giữ chân), Lôi Trận (sét đánh khi địch bước vào) | Đặt xuống đất, kích hoạt khi địch đi vào. Dùng để dụ địch |
| **Khôi lỗi** | Mộc Khôi (con rối gỗ) | Thả ra đánh cùng hoặc đứng làm mồi nhử |
| **Đan dược** | Hồi Khí Đan, Ngưng Thần Đan (+thần thức tạm thời), Trúc Cơ Đan | Phím số trên thanh nhanh |
| **Túi trữ vật** | — | Túi đồ. Địch bị hạ rơi túi trữ vật chứa chiến lợi phẩm |

---

## 8. ĐIỀU KHIỂN (bàn phím + chuột)

| Phím | Hành động |
|---|---|
| W A S D | Di chuyển |
| Chuột | Xoay camera / ngắm |
| Space | Nhảy (bấm lần 2 trên không = bay lên kiếm nếu đã mở) |
| Shift | Chạy nhanh / tăng tốc khi bay |
| Chuột trái | Phóng một phi kiếm (giữ = liên tiếp) |
| Chuột phải / F | Quyền cước |
| 1 / 2 / 3 / 4 | Đội hình Quạt / Vòng / Mũi tên / Vạn Kiếm Luân |
| R | Gọi toàn bộ kiếm về |
| Q | Hộ thuẫn |
| E | Dùng phù đang chọn; Z/X/C chọn phù |
| G | Đặt trận bàn |
| H | Bật/tắt Liễm Khí Thuật |
| Tab | Túi trữ vật / bảng nhân vật |
| T | Ngồi thiền |

Hỗ trợ tay cầm để sau (không cần cho demo).

---

## 9. CAMERA, HIỆU ỨNG, ÂM THANH

### Camera
- Cinemachine góc thứ ba, sau vai phải. Khóa mục tiêu (phím chuột giữa) để kiếm tự hướng về địch.
- Rung camera nhẹ khi kiếm trúng; rung mạnh + chậm thời gian 0.1 giây khi hạ địch lớn.
- Tuyệt chiêu: camera kéo lùi và lên cao 2 giây để thấy toàn bộ kiếm trận.

### Hiệu ứng (VFX) — ưu tiên theo thứ tự
1. Trail Renderer + emission cho mỗi kiếm (bắt buộc từ prototype đầu).
2. Tia lửa khi kiếm chạm nhau / chạm địch.
3. Vòng sáng tỏa ra khi đổi đội hình.
4. Sét nhảy quanh kiếm thuộc tính Lôi (Line Renderer ngẫu nhiên đổi điểm).
5. Nổ sáng lớn khi đối kháng.
6. Bánh xe kiếm khổng lồ + vệt sáng xoáy cho Vạn Kiếm Luân.

### Âm thanh (giai đoạn sau)
- Tiếng kiếm vút, tiếng kim loại va chạm, tiếng sét, tiếng gió khi bay, nhạc nền cổ phong (sáo, đàn tranh). Dùng nguồn âm thanh miễn phí có giấy phép rõ ràng.

---

## 10. KIẾN TRÚC CODE (gợi ý cho Claude Code)

| Script | Trách nhiệm |
|---|---|
| `PlayerController` | Di chuyển, nhảy, trạng thái (đứng/chạy/bay/thiền) |
| `PlayerStats` | Máu, pháp lực, thần thức, kiếm ý, cảnh giới |
| `FlyingSword` | Một thanh kiếm: bay tới vị trí đích, xoay mũi, va chạm, độ bền, trail |
| `SwordSwarm` | Quản lý danh sách kiếm, giới hạn theo thần thức, phân vị trí cho từng kiếm |
| `IFormation` + `FanFormation`, `RingFormation`, `ArrowFormation`, `WheelUltimate` | Mỗi đội hình là một class trả về vị trí/hướng cho kiếm thứ *i* trong *n* kiếm |
| `Damageable` | Máu, nhận sát thương, chết |
| `ElementEffect` | Lôi/Hỏa/Băng/Phong và hiệu ứng trạng thái |
| `EnemyAI` (+ các biến thể) | Tuần tra, phát hiện, tấn công, bỏ chạy |
| `RealmData` (ScriptableObject) | Dữ liệu cảnh giới (bảng 5.1) |
| `SwordData` (ScriptableObject) | Dữ liệu từng loại kiếm (bảng 7.1) |
| `ConcealmentSystem` | Liễm Khí Thuật, mức Chú Ý |
| `ClashSystem` | Đối kháng chiêu lớn |
| `SaveSystem` | Lưu/tải bằng JSON |

---

## 11. QUY TRÌNH TẠO ASSET 3D

| Loại asset | Cách làm | Công cụ |
|---|---|---|
| **Phi kiếm** (nhiều loại) | Claude dựng bằng Blender MCP: lưỡi kiếm, chuôi, đốc kiếm từ hình khối đơn giản; low-poly; xuất FBX | Blender MCP |
| **Đạo cụ đơn giản** (lò luyện đan, bồ đoàn, trận bàn, bia đá, cột đình, mộ kiếm) | Blender MCP | Blender MCP |
| **Nhân vật & quái** | Tạo bằng công cụ AI 3D từ ảnh concept → gắn xương và lấy animation trên **Mixamo** → nhập vào Unity | Công cụ AI 3D + Mixamo |
| **Môi trường lớn** (núi, cây, nhà) | Unity Terrain + gói asset Á Đông/stylized miễn phí | Unity, Asset Store |
| **Hiệu ứng** | Claude viết bằng Particle System, Shader Graph | Unity MCP |

**Quy tắc tỉ lệ:** 1 đơn vị Unity = 1 mét. Nhân vật cao ~1.8m. Phi kiếm dài ~1.0m (khi bay trong bầy kiếm có thể scale 0.8). Pivot của kiếm đặt ở **giữa chuôi**, trục Z hướng về mũi kiếm.

---

## 12. LỘ TRÌNH & PROMPT MẪU

> Mỗi giai đoạn: dán prompt cho Claude Code → bấm Play kiểm tra → nếu ổn bảo Claude commit → sang giai đoạn sau. Đừng nhảy cóc.

### Giai đoạn 0 — Chuẩn bị dự án
```
Đọc file CLAUDE.md trong thư mục gốc, đặc biệt Phần 0.
Kiểm tra dự án Unity đang mở (qua Unity MCP): phiên bản Unity, đã dùng URP chưa,
đã có Input System, Cinemachine chưa. Cài những gói còn thiếu.
Tạo cấu trúc thư mục như Phần 0. Khởi tạo git kèm .gitignore chuẩn cho Unity, commit đầu tiên.
Báo lại cho tôi bằng tiếng Việt: đã làm gì, có gì cần tôi tự bấm trong Unity không.
```

### Giai đoạn 1 — Sân tập & nhân vật viên nang
```
Theo tài liệu thiết kế (Phần 8, 9, 10): tạo scene "SanTap" gồm mặt đất phẳng 100x100m,
vài khối hộp làm chướng ngại, ánh sáng trời chiều, bật Bloom (URP Volume).
Tạo nhân vật người chơi tạm bằng viên nang (Capsule) cao 1.8m, di chuyển WASD,
chạy Shift, nhảy Space, camera Cinemachine góc thứ ba sau vai, xoay bằng chuột.
Mọi thông số tốc độ để trong Inspector có tooltip tiếng Việt.
Xong thì hướng dẫn tôi cách bấm Play để thử.
```

### Giai đoạn 2 — Mô hình phi kiếm (Blender)
```
Dùng Blender MCP dựng một phi kiếm low-poly theo Phần 7.1 (Thiết Kiếm) và Phần 11:
dài 1m, lưỡi thẳng mỏng có sống kiếm, đốc kiếm nhỏ, chuôi quấn đơn giản.
Pivot ở giữa chuôi, trục mũi kiếm hướng +Z khi vào Unity. Dưới 500 tam giác.
Xuất FBX vào Assets/_Game/Models/Swords/. Tạo luôn biến thể Thanh Ngọc Kiếm (mảnh hơn).
Trong Unity tạo material có emission: Thiết Kiếm màu bạc phát sáng nhẹ, Thanh Ngọc Kiếm xanh ngọc.
Chụp lại góc nhìn trong Blender để tôi xem trước.
```

### Giai đoạn 3 — Bầy kiếm & đội hình (QUAN TRỌNG NHẤT)
```
Theo Phần 6.2, 6.3 và 10: viết FlyingSword, SwordSwarm và 3 đội hình Quạt, Vòng, Mũi tên.
- Người chơi có 9 phi kiếm (dùng prefab Thanh Ngọc Kiếm), mỗi kiếm có Trail Renderer phát sáng.
- Kiếm bay mượt tới vị trí đội hình, luôn quay mũi theo hướng bay.
- Phím 1/2/3 đổi đội hình, kiếm chuyển vị trí trong ~0.4 giây.
- Chuột trái phóng một kiếm về phía tâm ngắm rồi tự quay về; phím 3 lao cả đàn vào mục tiêu gần nhất.
- Đặt 5 hình nộm (khối trụ có thanh máu) để thử. Hình nộm bị đánh thì nháy đỏ và văng tia lửa.
- Mọi số (bán kính, tốc độ, góc quạt, sát thương) chỉnh được trong Inspector.
Thêm một biến "Số kiếm" để tôi đổi 3 → 9 → 24 xem đội hình tự giãn ra.
```

### Giai đoạn 4 — Tài nguyên, HUD, pháp thể
```
Theo Phần 6.1 và 6.4: thêm Máu, Pháp lực, Thần thức (giới hạn số kiếm), Kiếm ý.
HUD tiếng Việt góc dưới trái: thanh máu, thanh pháp lực, số kiếm đang dùng/tối đa, thanh kiếm ý.
Phóng kiếm và đổi đội hình tốn pháp lực; cạn pháp lực không phóng được.
Thêm quyền cước 3 đòn (chuột phải), không tốn pháp lực.
Hình nộm biết đánh trả: bắn quả cầu chậm về phía người chơi, để thử đội hình Quạt tự chặn đạn.
```

### Giai đoạn 5 — Kẻ địch đầu tiên
```
Theo Phần 4.3: tạo Thanh Lang (tạm dùng khối hộp dài màu xám-xanh) với AI:
tuần tra, phát hiện người chơi trong 15m, chạy tới cắn, đi theo đàn 4 con và bao vây.
Tạo Tà tu Luyện Khí (viên nang đỏ) dùng 2 phi kiếm của riêng hắn, bỏ chạy khi còn 20% máu.
Hạ địch rơi túi trữ vật (khối nhỏ phát sáng), đi qua thì nhặt.
```

### Giai đoạn 6 — Nhân vật thật & animation
```
Tôi đã có file FBX nhân vật Tô Mặc gắn xương từ Mixamo và các animation [liệt kê tên file].
Nhập vào Unity, thay viên nang bằng nhân vật này, làm Animator Controller cho:
đứng, đi, chạy, nhảy, rơi, niệm quyết (khi đổi đội hình), phóng kiếm, quyền cước 3 đòn, bị đánh, chết.
Kiểm tra tỉ lệ (cao ~1.8m) và hướng nhân vật. Báo tôi animation nào còn thiếu.
```
*(Trước giai đoạn này: tạo ảnh concept Tô Mặc theo mô tả 4.1 → dùng công cụ AI 3D tạo mô hình → đưa lên Mixamo gắn xương và tải animation.)*

### Giai đoạn 7 — Thuộc tính & pháp bảo
```
Theo Phần 6.5 và 7.2: thêm thuộc tính Lôi và Hỏa cho kiếm (đổi bằng menu thử nghiệm).
Lôi: tia sét nhảy quanh kiếm (Line Renderer), gây choáng, lan sang địch gần.
Hỏa: kiếm phát lửa, đốt theo thời gian.
Thêm Quy Giáp Thuẫn (phím Q) và 2 phù: Độn Quang Phù, Hỏa Cầu Phù.
Thêm trận bàn Khốn Trận (phím G).
```

### Giai đoạn 8 — Ngự kiếm phi hành
```
Theo Phần 6.8 và 8: nhấn Space lần 2 trên không thì một thanh kiếm lớn xuất hiện dưới chân,
nhân vật đứng lên và bay. WASD bay ngang, Space lên, Ctrl xuống, Shift tăng tốc.
Có vệt sáng phía sau, nhân vật hơi nghiêng theo hướng rẽ, gió (particle) lướt qua khi bay nhanh.
Chạm đất hoặc bấm Space lần nữa thì hạ xuống. Bay tốn pháp lực theo thời gian.
Mở rộng sân tập thành vùng núi (Terrain) để thử bay.
```

### Giai đoạn 9 — Tuyệt chiêu & đối kháng
```
Theo Phần 6.3 (#4) và 6.7: làm Vạn Kiếm Luân khi đầy Kiếm ý (phím 4):
nhân vật bất động niệm quyết 2 giây, camera kéo lùi lên cao, toàn bộ kiếm bay lên trời
xếp thành bánh xe dựng đứng bán kính 8m, xoay nhanh dần rồi ép xuống khu vực phía trước,
nổ sáng lớn, rung camera. Sau đó làm hệ thống Đối kháng: khi Phá Kiếm va chiêu lớn của boss,
chậm thời gian, hiện vòng tròn, bấm Space liên tục 3 giây để phân thắng thua.
```

### Giai đoạn 10 — Tu luyện, động phủ, ẩn tu vi
```
Theo Phần 5 và 6.6: tạo dữ liệu cảnh giới bằng ScriptableObject theo bảng 5.1.
Làm động phủ (một hang đá nhỏ) với bồ đoàn để ngồi thiền tăng tu vi, lò luyện chế để gắn thuộc tính kiếm.
Làm đột phá Luyện Khí → Trúc Cơ (cần Trúc Cơ Đan, có tỷ lệ thất bại).
Làm Liễm Khí Thuật (phím H) và mức Chú Ý. Thêm lưu/tải game bằng JSON.
```

### Giai đoạn 11 — Demo hoàn chỉnh
```
Ghép các phần thành một demo khoảng 20–30 phút:
Thanh Hà Tông (nhận nhiệm vụ) → Rừng Hắc Trúc (đánh Thanh Lang, lấy Lôi Trúc) → động phủ (luyện kiếm lôi, đột phá)
→ đấu Triệu Liệt → Cổ Kiếm Trủng (Thi Khôi, kiếm hồn) → boss Huyết Bào Khách
(mạnh hơn 1 đại cảnh giới: người chơi phải dùng trận bàn, kiếm lôi và chạy đúng lúc để thắng).
Thêm menu chính, màn hình thua, thoại NPC đơn giản bằng tiếng Việt.
```

---

## 13. CHECKLIST KIỂM THỬ (người dùng tự làm sau mỗi giai đoạn)

- [ ] Bấm Play không có lỗi đỏ trong Console.
- [ ] Làm đúng các phím mà Claude hướng dẫn, thấy đúng kết quả.
- [ ] Chơi thử 5 phút: có chỗ nào khó chịu, quá nhanh, quá chậm, nhìn không rõ? → Ghi lại bằng lời thường và nói với Claude, ví dụ: *"kiếm bay về chậm quá, đổi đội hình nhìn bị giật"*.
- [ ] FPS ổn định (bật Stats ở góc phải cửa sổ Game).
- [ ] Đã commit git.

## 14. MẸO NÓI CHUYỆN VỚI CLAUDE CODE

- **Mô tả cảm giác, không cần thuật ngữ:** "Kiếm lúc va vào địch không có lực, tôi muốn cảm giác chém mạnh hơn" là đủ.
- **Một yêu cầu mỗi lần.** Nhiều việc cùng lúc dễ hỏng nhiều thứ cùng lúc.
- **Khi hỏng:** "Từ lúc thêm X thì Y bị lỗi, hãy xem Console và sửa; nếu không sửa được thì quay về commit trước."
- **Gửi ảnh chụp màn hình** khi khó tả bằng lời.
- **Thường xuyên nhắc:** "Đọc lại CLAUDE.md trước khi làm" khi bắt đầu phiên mới.
