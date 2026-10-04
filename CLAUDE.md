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
- Giai đoạn đã xong: **0** (chuẩn bị dự án), **1** (sân tập `SanTap`: nhân vật viên nang, camera Cinemachine sau vai phải, Bloom), **3** (bầy kiếm + 3 đội hình, tâm ngắm, hình nộm), **4** (tài nguyên, HUD, quyền cước, hình nộm bắn trả; xem mục "Giai đoạn 4" bên dưới), **6** (nhân vật thật). Giai đoạn 2 (kiếm Blender) đã làm trước dưới dạng phi kiếm Thanh Trúc.
- Giai đoạn 4: `PlayerStats` (Máu 100, Pháp lực hồi 5/s rồi 12/s sau 2s không dùng — đang bị trừ liên tục như Vòng thì không hồi, Kiếm ý 0-100 tụt 1/s sau 8s) + thang cảnh giới `RealmData` (10 file trong `Assets/_Game/Data/Realms/`, `realmIndex`=2 là Luyện Khí tầng 7; F4/F5 hạ/nâng cảnh giới trong Editor). `SwordSwarm` lấy trần số kiếm từ `PlayerStats.MaxSwords` (ô "Số kiếm" bị kẹp lại), tiêu pháp lực: bắn lẻ 4, loạt 3/kiếm, đổi đội hình 5, Vòng 6/s (cạn thì tự về Quạt). `PlayerMelee` (Dam1→Dam2→Da, chuột phải/F; `Step.clipStart/clipHit/clipEnd` là giây trong clip, đo từ chuyển động tay chân thật), `PlayerLife` (chết → 2s → màn "Đạo tiêu thân vẫn" + nút Thử lại), `HUD` (tự dựng uGUI lúc Play, phông hệ thống có dấu: Be Vietnam Pro/Noto Sans/Segoe UI/Arial), `EnemyOrb` + `ShooterDummy` (HinhNop_2 và HinhNop_3, đỏ sẫm, bắn cầu 8 m/s mỗi 2.5s trong 25m). Quạt chặn cầu từ cung 160° sau lưng bằng cách cử kiếm ra đón (`FlyingSword.SetGuard`), Vòng chặn khi cầu chạm thanh kiếm đang xoay; kiếm đang bay đi/thu về thì không chặn. Rung camera = Cinemachine Impulse (`CinemachineImpulseSource` trên `BayKiem`, `CinemachineImpulseListener` trên `CM_SauVaiPhai`, kênh 1); dừng hình = `Time.timeScale=0` (0.04s/0.08s khi hạ gục). Phím thử (chỉ Editor): F1 hồi đầy, F2 tự gây 20 sát thương, F3 đầy Kiếm ý, F4/F5 đổi cảnh giới. Walk Speed = 3 m/s.
- Animator đã thêm: tham số `AtkSpeed`, trigger `Hit`/`Die`; lớp nền có Dam1/Dam2/Da (tốc độ theo `AtkSpeed`) và Chet (AnyState khi `Die`); lớp `UpperBody` có BiDanh (khi `Hit`). Khi đánh quyền cước hoặc chết, `PlayerAnimator` tắt trọng số lớp thân trên để toàn thân phát anim. Clip Dam1/Dam2/Da/BiDanh/Chet đặt Root Y theo Feet.
- Tư thế phóng kiếm (đã đổi sang clip `NhanVat@PhongKiem2.fbx`, 3.6s; clip `PhongKiem` cũ vẫn trong dự án nhưng đã gỡ khỏi Animator): tay giơ lên cạnh đầu (0.2-0.8s), giữ (0.8-1.6s), hạ về (1.6-3.0s) — không có động tác duỗi ra trước. Khung "tay tới điểm phóng" = giây 0.9 (`PlayerAnimator.shootKeyTime`). Lớp `UpperBody` có `TayGiu` (Motion Time theo tham số `ShootHoldTime`, đứng yên ở khung đó) và `TayThu` (cùng clip, tốc độ `ShootRetractSpeed`). Chuột trái → `SwordSwarm.RequestSingle` gọi `ShootPoseGate` (PlayerAnimator) → tay chuyển 0.08s rồi kiếm mới rời đội hình; phát sau tay đã ở đó nên bắn ngay theo nhịp cũ; ngừng bắn 0.35s thì thu tay. `SwordSwarm.SwordLaunched` (mỗi kiếm rời đội hình) → cú giật cẳng tay/bàn tay phải 8°/0.1s bằng code trong `PlayerAnimator.LateUpdate`, cùng chỗ nghiêng thân trên theo pitch camera (tối đa 25°). Lưu ý: `CrossFadeInFixedTime(..., fixedTimeOffset)` nhân offset với tốc độ trạng thái; state tốc độ 0 thì offset luôn về 0 nên dùng Motion Time thay vì offset.
- Chỉnh hình ảnh sau Giai đoạn 4: kiếm trong đội hình thu còn 0.7 lần (`SwordSwarm.formationSwordScale`), to dần về cỡ thật khi phóng (0.2s), nhỏ lại khi về (0.35s). Quạt: bán kính 0.7, cách điểm neo 0.58 (≈0.35m sau lưng), tâm ngang vai (điểm neo = vai, heightOffset 0), mũi xa nhất cách tâm ≈1.3m; Vòng bán kính 1.0; Mũi tên gọn hơn. Ánh sáng nhân vật: 2 đèn directional con của `MainCamera` (`AnhSang_Bu` bù từ sau camera, `AnhSang_Vien` viền sáng) chỉ chiếu lên Rendering Layer 1 (renderer của `NhanVat_Model` có mask 2, mặt trời mask 3); đã bật `m_SupportsLightLayers` trong `PC_RPAsset`. Layer của đèn đặt qua `UniversalAdditionalLightData.renderingLayers`, KHÔNG phải `Light.renderingLayerMask`. Viền đỏ khi bị đánh: chỉ 4 mép, đậm tối đa 35%, tắt trong 0.4s; máu <30% thì có viền đỏ nhạt đập theo nhịp (HUD). Tia lửa chặn cầu thu 1/3, 0.12s, bắn ngược hướng cầu bay tới. Rung camera khi trúng đòn giảm một nửa (0.02/0.05).
- Giai đoạn 5 (kẻ địch): `EnemyBase` (Enemies/) = Damageable + NavMeshAgent + máy trạng thái Patrol→Detect→Attack→(Flee)→Dead (+Staggered khi bị quyền cước, `DamageInfo.melee`), mất dấu 6s thì về tuần tra, rơi `LootPouch` (Items/, `TuiTruVat.prefab`) → `PlayerStats.AddSpiritStones`; HUD vẽ thanh máu+tên+cảnh giới kẻ địch và bộ đếm "Linh thạch". `ThanhLang` + `ThanhLangPack` (đàn 4 con: tản các hướng quanh người chơi, mỗi lần chỉ 1 con lao cắn 8 dmg, con chết thì cả đàn hung hăng 3s). `TaTu` (prefab dùng NhanVat_Model, material `TaTu_Mat` với `TaTu_BaseColor.png` đã dịch hue sang đỏ sẫm, controller `TaTu_Animator` là bản sao controller người chơi; 2 `EnemyBlade` = `PhiKiemDo.prefab`, Hỏa Cầu = `HoaCau.prefab` dùng lại `EnemyOrb`). Vật bay của địch đều implement `IEnemyProjectile` (đăng ký ở `EnemyProjectiles.All`), nên SwordSwarm chặn theo đúng luật Quạt/Vòng cũ. Chênh lệch cảnh giới: `RealmData.majorRealm` + `RealmRules.DamageMultiplier` (x2.5 / x0.4, chỉnh ở PlayerStats); `Damageable.DamageTakenMultiplier` do EnemyBase cập nhật. NavMesh: layer `MoiTruong` (MatDat + ChuongNgaiVat), object `NavMesh_SanTap` (NavMeshSurface), dữ liệu `Assets/_Game/Scenes/SanTap_NavMesh.asset`; nếu thêm vật cản mới nhớ đặt layer `MoiTruong` rồi bấm Bake lại. Sân tập: đàn sói ở (-30,0,26), 2 tà tu ở (28,0,24) và (33,0,29); `KeDich_PhimThu` có F6 (hồi sinh mọi kẻ địch) và F7 (bật/tắt AI). Đã bỏ 2 hình nộm bắn (script `ShooterDummy` vẫn còn trong dự án), còn 3 hình nộm đứng yên.
- Tà tu dùng mô hình Mixamo riêng: `Models/Characters/TaTu/TaTu_ChoMixamo@Idle.fbx` (25 xương No Fingers, Humanoid, Avatar riêng `TaTu_ChoMixamo@IdleAvatar`), material `TaTuMixamo_Mat` (3 texture `TaTuMixamo_*`, Metallic-Smoothness đã đổi từ Metallic-Roughness: R=metal, A=1-rough). Prefab `TaTu.prefab` = đối tượng gốc (NavMeshAgent, Damageable, `TaTu`) + con `Model` (instance của FBX). Muốn đổi mô hình khác: thay con `Model`, gán lại Animator (controller `TaTu_Animator`, Avatar mới) và `TaTu.animator`, đặt material, `renderingLayerMask = 2`. Phi kiếm tím `PhiKiemTim.prefab` (emission tím sẫm). Trong chiến đấu (Detect/Attack) tà tu giữ dáng ngự kiếm (clip PhongKiem2, `TaTu.holdCastPoseInCombat`) để che lỗi tay áo cứng/tà áo kéo giãn của mô hình. Texture/asset cũ của phiên bản hue-shift (`TaTu_BaseColor.png`, `TaTu_Mat`, `PhiKiemDo.prefab`...) còn trong dự án nhưng không dùng nữa.
- **Thanh ô đồ, kiếm cầm tay, Ngự Kiếm Thuật** (sau Giai đoạn 5; thay các phím cũ trong ghi chú Giai đoạn 4 phía trên: chuột trái/phím 1-3 phóng kiếm và chuột phải/F quyền cước KHÔNG còn dùng):
  - `ItemData` (ScriptableObject, `Data/Items/`: `ThanhTrucKiem.asset`, `HoiKhiDan.asset`) + `Hotbar` (Items/, trên `NhanVat`, 6 ô, phím 1-6/lăn chuột, chuột trái dùng đan, không dùng đan khi pháp lực đầy) + HUD vẽ thanh ô ở giữa cạnh dưới (kích thước/màu chỉnh trong Inspector của HUD). Biểu tượng chụp từ prefab bằng menu Phàm Kiếm Lục → Chụp biểu tượng vật phẩm (`Scripts/Editor/ItemIconBaker.cs`), ảnh ở `UI/Icons/`.
  - `HeldWeapon` (Player/): chọn ô vũ khí thì kiếm (`Prefabs/KiemCamTay.prefab`, có `SlashTrail` + TrailRenderer, điểm `DayLuoi`/`MuiKiem`) xuất hiện ở túi trữ vật `NeoTuiTruVat` (con của xương Hips, bên trái), bay theo đường cong trong 0.35s tới xương RightHand; cất thì bay ngược 0.25s. Hai tư thế cầm (`carryPose` lúc đứng/chạy: mũi chếch lên-trước; `slashPose` lúc chém, hai tay) tính từ dữ liệu hai bàn tay trong clip; nhát Chem3 (đâm, hai tay xa nhau) có tư thế riêng `Step.useGripOverride`.
  - `PlayerMelee` có hai chuỗi: `unarmedSteps` (Dam1/Dam2/Da, 10/12/20) và `swordSteps` (Chem1/2/3: 18/20/32, tầm 2.2m, quạt 120°, nhát 3 đẩy lùi 2m; thời gian thật mỗi nhát 0.5/0.6/0.9s; mốc gây sát thương 0.25/0.38/0.54s sau khi bấm). Clip `NhanVat@Chem1/2/3.fbx` (từ Mixamo Great Sword Slash x2 và Thrust Slash, rig tà tu, Humanoid Avatar riêng, dùng cho nhân vật chính qua retarget); trạng thái `Chem1-3` trong lớp Base (tốc độ theo `AtkSpeed`).
  - `SwordSwarm` mặc định TẮT: Q → `Toggle()` (trừ 20 pháp lực, kiếm bay ra từ túi mỗi 0.05s xòe thành Quạt; `PlayerAnimator` phát NiemQuyet), `upkeepPerSecond` = 2 (pháp lực không tự hồi vì dùng `DrainMana`), cạn thì tự tắt, chết thì tắt; tắt = kiếm bay về túi 0.25s rồi bị xóa (`FlyingSword.BeginDespawn/TickDespawn`). Khi tắt không chặn đạn, `CrosshairUI` ẩn.
  - **Chỉnh sau lần xem ảnh đầu** (commit "Chỉnh kiếm cầm tay, quạt kiếm, HUD"):
    - Kiếm cầm tay luôn đúng 1m (đã đo: lossyScale = 1, đốc→mũi 0.74m suốt 3 nhát); trước đó trông phình là do vệt chém đặc. `SlashTrail` giờ mảnh/mờ/tắt nhanh: tồn tại 0.12s, màu alpha 0.4, chỉ phủ 55% lưỡi từ phía mũi (`bladeFraction`), mới nhất trong suốt rồi đậm dần (`fadeInFraction`) nên không phủ đặc lên lưỡi. Cửa sổ vệt theo thời gian thật quanh khoảnh khắc trúng (`Step.trailBeforeHit` 0.12s, `trailAfterHit` 0.08s).
    - Quạt kiếm: chỗ đứng tính theo số kiếm CUỐI (`SwordSwarm.swordCount`), mỗi kiếm bay thẳng tới chỗ của mình; thứ tự bay ra từ giữa rồi luân phiên phải - trái (`BuildDeployOrder`, `FlyingSword.Index` là chỉ số chỗ đứng chứ không phải thứ tự sinh). Đo: lệch đối xứng ≤ 0.012m (9 và 6 kiếm), kiếm gần đầu nhất cách tâm đầu 0.76m.
    - Camera: `CM_SauVaiPhai` → `CinemachineThirdPersonFollow.ShoulderOffset` = (0.55, -0.55, 0) (trước là y -0.10) để chân nhân vật nằm trên thanh ô đồ ở màn 1920x1080 (gót ~y 925, thanh ô từ y 972).
    - HUD dùng **TextMeshPro** (Assets/TextMesh Pro đã nhập "TMP Essential Resources" từ gói ugui) với phông **Inter** (SIL OFL, chép từ gói App UI của Unity vào `Assets/_Game/UI/Fonts/Inter-Regular.ttf`; font asset `Inter_Regular_SDF.asset` nạp sẵn mọi chữ tiếng Việt, dựng lại bằng menu Phàm Kiếm Lục → Tạo font HUD (TextMeshPro) / `HudFontBuilder`). Canvas Scaler: Scale With Screen Size 1920x1080, match 0.5. Bảng tài nguyên tự co theo dòng đang hiện (124 điểm ảnh + 34 khi Ngự Kiếm Thuật bật), các ô Q / Z X C nằm cách bảng 12 điểm ảnh, dòng "Pháp lực không đủ" nổi phía trên ô. Chụp HUD đúng 16:9: đặt Game view 1920x1080 (xem ghi chú test).
    - **Thay clip chém**: `PlayerMelee.Step` giờ tách "Mốc trong clip" (`clipStart/clipHit/clipEnd`, phụ thuộc file anim) khỏi "Thời gian thật" (`duration`, `hitTime`, giữ nguyên khi đổi clip). Anim tự tăng/giảm tốc (`AtkSpeed`: trước khi trúng = (clipHit-clipStart)/hitTime, sau khi trúng = (clipEnd-clipHit)/(duration-hitTime)) nên khoảnh khắc trúng luôn đúng `hitTime` (0.25 / 0.38 / 0.54s), tổng 0.5 / 0.6 / 0.9s, sát thương 18 / 20 / 32 không đổi (quyền cước vẫn dùng kiểu cũ `duration = 0` + `speed`). Quy trình đổi clip: (1) chép 3 file mới đè lên `NhanVat@Chem1/2/3.fbx` (giữ nguyên tên); (2) chạy menu Phàm Kiếm Lục → Thay clip chém (`SlashClipTools.Apply`: Humanoid, Avatar riêng, không bake, gán vào Animator, in GỢI Ý mốc; gợi ý chỉ là điểm xuất phát, cần kiểm lại bằng `DebugRecorder`); (3) nhập 3 mốc vào `swordSteps[i].clipStart/clipHit/clipEnd`; (4) chỉnh lại `slashPose`, `carryPose` và `useGripOverride` của `HeldWeapon`/`Step` cho kiểu cầm một tay (đều là số trong Inspector).
  - **Kiếm Luân 20 kiếm** (thay đội hình Quạt; `FormationType.Wheel`, `Swords/WheelFormation.cs`; `FanFormation.cs` còn trong dự án nhưng không dùng nữa):
    - Số kiếm theo cảnh giới (`RealmData.maxSwords`, 10 file trong `Data/Realms`): Luyện Khí tầng 1 / 4 / 7 = 6 / 12 / 20; Trúc Cơ sơ / trung / hậu = 28 / 36 / 44; Kết Đan sơ / trung / hậu = 56 / 68 / 80; Nguyên Anh = 100. Ô "Số kiếm" của `BayKiem` trong scene = 20.
    - Hình dạng (đã chỉnh "Kiếm Luân dày và chụm tâm"): mặt phẳng đứng sau lưng vuông góc hướng nhìn, tâm giữa hai bả vai cách điểm neo 0.58m (cách lưng ~0.35m); kiếm trong đội hình thu 0.8 lần (`formationSwordScale`); hai lớp so le (chỗ chẵn = lớp trong, chỗ lẻ = lớp ngoài; mỗi lớp một nửa số kiếm), lớp ngoài lệch nửa bước góc, lùi thêm 0.03m, xa tâm thêm 0.17m; pivot (giữa chuôi) lớp trong cách tâm 0.28m (lỗ rỗng rất nhỏ), mũi lớp trong ~0.98m, mũi lớp ngoài ~1.15m → cả vầng cao ~2.3m. Hai lớp xoay CÙNG chiều cùng tốc độ 12 độ/giây (`WheelFormation.outerSpinRatio` = 1; trước đây lớp ngoài xoay ngược nên thỉnh thoảng kiếm đứng thành cặp), nên 20 kiếm luôn cách đều 18 độ, mũi so le dài - ngắn; mỗi kiếm dao động ra vào 0.02m (0.7 Hz). Số kiếm khác 20 thì tự chia hai lớp và giãn góc. Chỗ đứng tính theo số kiếm mục tiêu nên kiếm phóng đi để trống chỗ tới khi về.
    - Hình ảnh Kiếm Luân: (1) **sáng hơn** — `SwordSwarm.formationEmissionBoost` = 2 nhân emission của vật liệu kiếm (qua MaterialPropertyBlock trong `FlyingSword.UpdateLook`), Bloom toàn cục ngưỡng 1; (2) **quầng sáng** `SwordHalo` (trên `BayKiem`): quad cộng sáng (additive) tự dựng bằng code, vật liệu `VFX_QuangKiemLuan` + texture `VFX/Textures/QuangTron.png`, đặt ở tâm vầng kiếm lùi 0.05m, bán kính = bán kính vầng (`SwordSwarm.WheelOuterRadius`), độ đậm tối đa 0.3, "thở" ±4%, hiện dần theo tỉ lệ kiếm đã vào chỗ và chỉ hiện ở đội hình Kiếm Luân; (3) **mờ khi nhìn từ sau lưng** — `SwordSwarm.UpdateFormationOpacity`: góc giữa hướng nhìn ngang của camera và hướng trước nhân vật < `behindViewAngle` 50° (đệm 3° chống chớp) thì `FormationOpacity` về `behindOpacity` 0.35 trong `opacityFadeTime` 0.25s, ngược lại 1; kiếm trong đội hình và quầng sáng nhân với độ đậm này. Kiếm đang phóng/bay về/đón cầu/hẹn lao/bay về túi luôn 100% (`FlyingSword.UpdateLook`). Mờ bằng cách đổi sang vật liệu trong suốt `ThanhTrucKiem_Mo` (bản sao vật liệu gốc, alpha blend, ô `fadeMaterial` của prefab), đậm 100% thì dùng lại vật liệu đặc. Dựng các vật liệu này bằng menu Phàm Kiếm Lục → Dựng vật liệu Kiếm Luân (`WheelVisualBuilder`, không ghi đè asset có sẵn). Khi test: xoay camera thật bằng cách gán trường private `yaw` của `CameraOrbit` (`orbitYaw` = yaw nhân vật + 180 là nhìn chính diện, +135 là nghiêng); ảnh chụp `Assets/Screenshots/so_sanh_kiem_luan.png` so cũ - mới.
    - Bay ra: mỗi 0.03s một kiếm theo `WheelFormation.DeployOrder` (từ đỉnh, luân phiên phải - trái), kiếm mới dùng `deploySettleTime` 0.14s nên bánh xe mở ra trong ~0.7s. Tắt: `ClearAllSwords` đặt `FlyingSword.RetireAt`, kiếm đứng trong đội hình về túi theo thứ tự ngược (mỗi 0.03s), kiếm đang ở ngoài về ngay.
    - Pháp lực: bật 30 (không đủ thì không bật, `ManaInsufficient` làm thanh nháy đỏ); duy trì 2/s; bắn lẻ 6; loạt Mũi tên 3/kiếm; kiếm đang ở ngoài (`Mode != Formation`, `OutSwordCount`) 1/s mỗi thanh cộng dồn; Vòng 6/s; đổi đội hình 5. Tổng rút mỗi giây = `SwordSwarm.CurrentDrainPerSecond` (một lần `DrainMana` mỗi khung); cạn thì `SetActive(false)` và mọi kiếm về túi. HUD: tổng > 8/s thì thanh pháp lực chớp nhẹ và hiện "-N/s" màu cam (`HUD.drainTextColor`) NGOÀI thanh, bên phải số "200/200" (`HUD.drainWarnThreshold`). Ô Z ghi "Kiếm Luân", ô Q ghi "Q Ngự Kiếm Thuật (30 pháp lực)".
    - Chặn đạn như Quạt cũ (cung 160° phía sau, cử kiếm ra đón) chỉ khi Ngự Kiếm Thuật bật; đã thử: đạn từ sau bị chặn, đạn từ trước trúng (15 sát thương). Vòng: `layerCapacity` 10, Mũi tên: `columnCapacity` 10 (cho 20 kiếm vẫn gọn).
    - Hiệu năng: `SwordSwarm.LateUpdate` 0.029ms (9 kiếm) → 0.065ms (20 kiếm); fps trong Editor chạy nền không khác (38.6 vs 39.3 ms/khung, bị Editor giới hạn). Với vầng kiếm cao ~2.3m, camera mặc định (FOV 60, cách 3.2m, không đổi) thấy trọn vầng kiếm ở 1920x1080 (đỉnh ~y 90/900, đáy ~y 770/900), không thanh nào bị cắt.
  - **Phong Lôi Sí (lượt 1: vật phẩm, kích hoạt, bay, pháp lực)** — pháp bảo đôi cánh, ô 4:
    - Mô hình: 风雷翅 của 芒果小子 (模之屋; dùng cá nhân, phi thương mại; ghi công ở `Models/PhapBao/PhongLoiSi/GHI_CONG.txt`), đã xử lý trong Blender thành bản game: 16.062 tam giác (2 xương cánh ~3.000 + lông 6.704 trái / 3.360 phải), sải 3.4m, gốc toạ độ giữa hai gốc cánh, bộ xương mới 7 xương (Goc + 3 xương mỗi cánh `Canh_T1-3` = trái, `Canh_P1-3` = phải), 4 clip `Open` 0.5s / `Flap` lặp 1.2s / `Glide` lặp 2s / `Close` 0.4s trong `PhongLoiSi.fbx`. Rig gốc (Auto-Rig Pro 278 xương, action `rig动作` rỗng) KHÔNG dùng. Màu lông gradient bake vào vertex color (alpha của vertex color = độ gần mép lông cho emission mép). Prefab `Prefabs/PhongLoiSi.prefab`; shader lông `Shaders/LongVuLit.shader` (PBR kiểu Lit nhưng đọc vertex color, emission mép mảnh mượt), xương cánh dùng URP Lit. Dựng lại bằng menu Phàm Kiếm Lục → Dựng Phong Lôi Sí (`PhongLoiSiBuilder`); script Blender dựng/xuất nằm ngoài dự án (file .blend KHÔNG đưa vào git).
    - `Items/PhongLoiSi.cs` (trên NhanVat): chuột trái ở ô 4 → `Hotbar.PhapBaoActivated` → bật/tắt (`activationCost` 25). Cánh = `PhongLoiSi_Canh` (con của NhanVat, `WingVisual`): gắn theo Spine2 lệch (0, +0.04, -0.32) (đã nâng 2 lần theo ý người chơi: -0.25 → -0.10 → +0.04; gốc cánh ngang bả vai trên/gần gáy, ra sau để không đâm đầu/vai áo; đo ở offset +0.04: gốc cánh cách xương đầu 0.33m, cách trục thân 0.37m; ảnh từ sau/bên không thấy cánh đâm đầu), cùng hướng thân người. Cánh mở: `SwordSwarm.WheelPushTarget` = 0.5 đẩy Kiếm Luân ra sau.
    - Bay: `PlayerController.FlightActive/FlightMove`; WASD theo hướng camera 3D, Space/Ctrl lên/xuống 8 m/s, Shift 24 m/s (thường 12), quán tính (tăng 20, giảm 14 m/s²), trần 60m so với mặt đất (đo bằng raycast lớp MoiTruong), hạ cánh khi chạm đất lúc đang hạ hoặc hai lần Space nhanh (hạ 8 m/s). Dáng: clip `NhanVat@Bay.fbx` (đo: nằm nghiêng sẵn 66° so với phương đứng, tay xuôi, chân co ra sau; Humanoid, Avatar riêng, bake Root Y + Rotation, KHÔNG bake XZ) làm nền ở lớp nền (trạng thái `Bay`), thân xoay quanh hông (1m) bằng code: lơ lửng đứng thẳng, bay thường nghiêng 50°, tăng tốc 75° (góc code xoay = góc muốn - 66°), nghiêng người theo hướng rẽ (tối đa 25°), chuyển mượt 0.3s. Chém khi bay: trạng thái `Dam1_Bay`...`Chem3_Bay` ở lớp thân trên (`FlightSetupTools`), `PlayerMelee.AttackInFlight`; phóng kiếm vẫn chạy bình thường.
    - Pháp lực (`DrainMana`, không tự hồi khi cánh mở): đứng 1/s, bay 4/s, tăng tốc 9/s; cộng dồn với Ngự Kiếm Thuật, HUD hiện chung con số cam "-N/s" (khi > 8/s). Cạn pháp lực giữa trời: cánh chập chờn 1s rồi tắt, nhân vật rơi; rơi từ trên 12m thì mất 4 máu mỗi mét vượt (rơi 25m mất 52 máu, đã đo). Chết/hồi sinh thì cánh tự đóng.
    - Camera khi bay: lùi thêm 1.5m, FOV +8 (+6 và lùi thêm 0.8m khi tăng tốc), mượt 0.45s.
    - Kiểm thử qua code (không cần bàn phím): `PhongLoiSi.TestSetInput(move, vert, shift)`, `TestTapSpace()`, `TestClearInput()`; mở cánh bằng `Hotbar.Select(3)` + `TryUseSelected()`. Trong Play qua MCP, game chỉ chạy khi Unity có tiêu điểm (AppActivate); theo dõi theo thời gian bằng `EditorApplication.update` + `Debug.Log` rồi đọc Console, đừng đo bằng các lệnh rời rạc.
    - Sấm sét (`WingLightning`), Phá Không (`PhaKhong`, shader `CongKhongGian`) đã viết một phần nhưng CHƯA nối vào cảnh: làm ở lượt 2 và 3.
    - Visual Effect Graph 17.6.0 đã cài, Samples ở `Assets/Samples`; hiệu ứng mẫu dùng được: `Lightning.vfx` (sét theo 8 điểm Transform), `Sparks.vfx`, `Smoke.vfx`, texture `TX_Lighting.tga` (các hình tia sét), `TX_Flipbook_Smoke`, `WispySmoke03b_8x8`, nhiễu `RidgedPerlin`/`CellularNoise01`. Cảnh thử `Scenes/TestVFX.unity`. Không có mẫu sóng xung kích sẵn.
  - Công cụ kiểm thử chỉ có trong Editor: `Core/DebugShots.cs` (chụp cận cảnh bằng camera tạm, ghép lưới ảnh) và `Core/DebugRecorder.cs` (quay khung hình cách đều theo `Time.captureDeltaTime`) — gọi qua MCP `execute_code`.
- Mẹo test: để chụp HUD đúng 16:9, đặt Game view 1920x1080 (qua reflection `GameView.SizeSelectionCallback` với `GameViewSizes.AddCustomSize`), tắt `CameraOrbit.InputEnabled` (chuột thật làm camera trôi khi con trỏ khóa), bật `Application.runInBackground`. Khi Unity "đứng" không trả lời MCP sau lệnh nhập gói, đưa cửa sổ Unity lên trước (AppActivate) là nó chạy tiếp. Nhớ trả `PlayerSettings.runInBackground` về false trước khi commit.
- Mẹo test: Editor chỉ chạy frame khi có tương tác (MCP gọi); nếu phiên Play bị "đơ" sau khi chạy lâu, Stop rồi Play lại. Để test chặn kiếm: đặt `PlayerStats.invulnerableUntil = 1e9` để người chơi không chết giữa chừng.
- Mẹo test: teleport người chơi xa (TeleportTo) làm đàn kiếm bay vụt qua sân đuổi theo, có thể chém trúng kẻ địch (vòng Ring) và gây sát thương giả; khi test kẻ địch nên teleport rồi đợi kiếm về.
- Mẹo test thêm: sửa script KHI ĐANG Play sẽ làm Unity biên dịch lại giữa chừng, xóa trường private và đổ lỗi NullReference/"missing script" giả. Luôn Stop trước khi sửa code. Khi test qua MCP đặt `Time.maximumDeltaTime=0.033` rồi trả về 0.3333333; `Time.timeScale=0` để chụp ảnh đóng băng.
- Giai đoạn 3: `SwordSwarm` (object `BayKiem`) sinh `FlyingSword` từ prefab `ThanhTrucKiem` (có TrailRenderer), xếp theo `IFormation` (`FanFormation`, `RingFormation`, `ArrowFormation`). Phím 1/2/3 đổi đội hình, chuột trái bắn lẻ, phím 3 lao cả đàn (bật `volleyOnKey3`: bấm 3 là xếp hàng rồi lao luôn), R gọi kiếm về. `Damageable` + `TrainingDummy` (prefab `HinhNop`, 5 con trong SanTap). `CrosshairUI` là tâm ngắm. Đổi "Số kiếm" trên `BayKiem` lúc đang Play để thử 3/9/24.
- Đã chỉnh sau lần chơi thử đầu (lệch so với bảng thiết kế gốc, theo ý người chơi): kiếm bắn lẻ 80 m/s, loạt Mũi tên 55 m/s (gốc 30); Quạt mở 180°, bán kính 1.8m, cách lưng 0.85m (gốc 160°/1.5m); khi lao kiếm vuốt dài 1.4x và vệt sáng rộng ra; `HitFeedback` (trên `BayKiem`) dừng hình 0.05s (hạ gục 0.1s) + rung camera (từ Giai đoạn 4 dùng Cinemachine Impulse).
- Giai đoạn 6 (nhân vật thật): prefab `Assets/_Game/Prefabs/NhanVat_Model.prefab` (con của `NhanVat` trong SanTap, local y -0.05), FBX Mixamo ở `Assets/_Game/Models/Characters/NhanVat/`. `NhanVat@Idle.fbx` là nguồn Avatar Humanoid; 11 file còn lại Copy From Other Avatar. Animator `NhanVat_Animator.controller`: lớp Base (Blend Tree `Speed`: Idle 0 / Walk 1.98 / Run 6.55 m/s đúng tốc độ gốc của clip, Jump, Fall, Landing) + lớp `UpperBody` (Avatar Mask không có Root và chân: PhongKiem khi `SwordSwarm.StrikeFired`, NiemQuyet khi `FormationChanged`). `PlayerAnimator` nối PlayerController/SwordSwarm với Animator. Dam1, Dam2, Da, BiDanh, Chet đã nối vào controller ở Giai đoạn 4.
- Bài học Animation: KHÔNG bật Bake Into Pose XZ cho Walk/Run (bake giữ chuyển động tiến trong tư thế khiến thân trôi khỏi GameObject); clip Mixamo "In Place" thực tế vẫn có root motion, chỉ cần tắt Apply Root Motion. Walk/Run có cùng pha bước chân nên không cần Cycle Offset.
- Root Transform Position (Y) cho clip trên không: Jump/Fall phải đặt "Based Upon = Feet" (không bake). Để Center of Mass thì cả người lún tới 1.1m xuống đất (đã gặp và sửa). Idle/Walk/Run: bake Y theo Original.
- Mẹo test: khi chạy lệnh qua MCP, Play bị đứng hình vài trăm ms làm bước thời gian rất lớn, méo cú nhảy; đặt `Time.maximumDeltaTime = 0.04` lúc đo (nhớ trả về 0.3333333).
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
| **Pháp bảo bay** | **Phong Lôi Sí** (đôi cánh sấm sét, ô 4 thanh ô đồ) | Chuột trái bật/tắt; mở cánh rồi bay (WASD theo camera, Space lên, Ctrl xuống, Shift tăng tốc 24 m/s, thường 12 m/s, trần 60m); tốn pháp lực mỗi giây theo trạng thái; Phá Không (phím V) làm ở lượt sau |
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
| Space | Nhảy. Khi **cánh Phong Lôi Sí đang mở**: bấm Space lúc đang ở trên không (hoặc giữ Space 0.3s) = cất cánh. Đang bay: Space = lên, hai lần Space nhanh = hạ cánh |
| Shift | Chạy nhanh / tăng tốc khi bay (Phong Lôi Sí) |
| Ctrl | Đang bay: hạ xuống |
| 1 – 6 / lăn chuột | Chọn ô trên thanh ô đồ (ô 1 trống = tay không, ô 2 Thanh Trúc Kiếm, ô 3 Hồi Khí Đan, ô 4 **Phong Lôi Sí**) |
| Chuột trái (ô 4) | Bật/tắt cánh Phong Lôi Sí (bật tốn 25 pháp lực). Cánh ở lại khi đổi ô; ô 4 có viền xanh lục khi cánh đang mở |
| Chuột trái | Tay không: quyền cước Dam1→Dam2→Da. Cầm kiếm: chém Chem1→Chem2→Chem3 (bấm tiếp ở nửa sau mỗi nhát để nối). Ô đan dược: dùng đan (hồi 80 pháp lực) |
| Q | Bật/tắt **Ngự Kiếm Thuật** (bầy phi kiếm; bật tốn 30 pháp lực, duy trì 2 pháp lực/giây, mỗi kiếm đang ở ngoài thêm 1 pháp lực/giây) |
| Chuột phải (khi Ngự Kiếm Thuật bật) | Phóng một phi kiếm về tâm ngắm (giữ = liên tiếp) |
| Z / X / C (khi Ngự Kiếm Thuật bật) | Đội hình **Kiếm Luân** (bánh xe kiếm, thay Quạt) / Vòng / Mũi tên (C = đổi sang Mũi tên rồi lao cả đàn). Vạn Kiếm Luân (tuyệt chiêu) để giai đoạn sau |
| R (khi Ngự Kiếm Thuật bật) | Gọi toàn bộ kiếm về |
| Hộ thuẫn | (chưa gán phím, để giai đoạn sau; phím Q đã dùng cho Ngự Kiếm Thuật) |
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
