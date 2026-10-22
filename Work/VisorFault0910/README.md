# 2026-09-10 原碎屏恢复、中央追加裂纹与显示故障

## 用户要求与结果

用户认可 9 月 9 日的原边缘碎屏，要求在原效果上追加少量中央裂纹，并让失血后的变暗明显可见、带显示装置故障感。本轮恢复原边缘碎屏的位置、尺寸、完整 UV 和 shader 算法；另增独立中央裂纹及全屏故障两层，血量与信息文字仍在最上层。未在本轮改动地面、箱子、房屋碰撞或标题封面。

## 实现

- `GravityVisorDamage.cs` 建立三个独立 RawImage：`VISOR_DISPLAY_FAULT`（底层）、`VISOR_GLASS_DAMAGE`（原碎屏）、`VISOR_CENTRAL_CRACKS`（新中央碎屏）。故障层匹配视野并留 6% 余量；两张碎屏保持既有参考画布尺寸，桌面 1600×1000，PICO 布局 2400×2400。
- 原 `GravityVisorDamage.shader` 与 `Work/WallVisor0910/GravityVisorDamage.before.shader` 逐字一致，SHA256 `fb0876986bf737db1bfcd6e904dad5455de49f2baada01f4f56a35ba7ef634dd`。原 `VisorGlassCracks.png` 沿用。
- 新 `GravityVisorSupplement.shader` 的故障层以 `0.82 × 失血比例^0.75` 连续衰减画面亮度；另有局部暗行、细扫描线和微弱青色故障线。2.6 秒周期只改变局部故障区，未加入全屏快速闪烁或相机摇晃。
- 中央裂纹来自用户指定的 Tripo Nano Banana，独立文件 `VisorCentralCracks.png`（2048×1280）。裁取生成图的两枚碎裂点放到画面中部偏左下及右上，alpha > 0.1 的纹理覆盖率约 0.478%，纹理中心直径 7% 透明。生成成功，task `363387c1-b5c5-465b-adb0-c64ef9ea4c46`，消耗 5 credits；详见 `asset-source.md`。
- 显示参数随真实血量平滑变化。回血减弱裂纹、变暗与机械故障；死亡结算隐藏三层。原 HUD 读数位于三层之上。

## 独立验证

核对 `Logs/VisorFault0910Damage.log` 与 `Logs/VisorFault0910PicoDamage.log`：两份均为 **39/39 通过，0 失败**；对应 `Evidence/VisorFault0910/Results.txt` 与 `Evidence/VisorFault0910Pico/Results.txt` 一致。各证据目录有 15 张 PNG，日志列出的场景截图全部存在。

测试走真实 `DamagePlayer` 100→75→45→15、两次 `ConsumeHealthPack` 回到 85、再真实致死结算。自动敌人攻击在测试中暂停，生产伤害/回血接口正常执行。

| 血量 | Shader 白底核心亮度 | 实景同帧亮度比（桌面） | 实景同帧亮度比（PICO 布局代理） |
| --- | --- | --- | --- |
| 100 | 1.000 | 满血无衰减 | 满血无衰减 |
| 75 | 0.710 | 0.841 | 0.841 |
| 45 | 0.473 | 0.678 | 0.678 |
| 15 | 0.272 | 0.496 | 0.495 |
| 回血至 85 | 0.804 | 0.895 | 0.895 |

实景比值通过同一帧、相同场景/原碎屏/中央碎屏/HUD，只开启或关闭故障层进行比较，再取中央场景像素中位数；这是显示 RGB 亮度比，不能当作物理光度测量。15 血量场景约为无故障时一半显示亮度，已明显暗化。

39 项检查还包括：原碎屏在普通与 110° FOV 下的参考尺寸、位置及 UV 保持不变；故障层覆盖视野；新增纹理独立加载；三层顺序正确；新增中央纹稀疏且准星核心无新增裂纹；回血恢复；结算全部隐藏。低血量中央新增亮线占测试中央采样区 2.5%（桌面）/2.6%（PICO 布局代理）；此值与整张纹理 alpha 覆盖率测量区域不同。2.6 秒故障采样只改变 2.7% 像素，全图平均亮度变化约 0.001，核心变化为 0。

独立查看两张 `04_Health15.png`：原周边碎屏仍存在，中央可见两枚额外碎裂点，场景明显变暗，敌人与道路轮廓可辨，中央瞄准区域仍保留。`04c_Health15_FaultDisabledComparison.png` 是同帧关闭故障层的实景对照。PICO 文件夹是桌面运行时 PICO 布局代理，不是头显实拍或模拟器录屏；其 16:10 预览会裁切正方形参考画布上下边缘。

## 构建

- 官方 Unity `6000.5.5f1`；`Logs/VisorFault0910Mac.log` 包含 `Build Finished, Result: Success.` 与 `GRAVITY_MODULAR_REVIEW_BUILD_PASS`。
- Android 在英文镜像 `work/gravity_repulsion_pico_build` 构建，`Logs/VisorFault0910Android.log` 包含 `Build Finished, Result: Success.` 与 `GRAVITY_CURRENT_LAYOUT_BUILD_PASS target=Android ... size=785502592`。
- PICO loader、Multiview、ARM64、IL2CPP、Activity、Input System-only 验证均通过。两份 visor shader 已编译并打包；无 C# 编译错误或 shader 错误。
- 本轮 macOS 构建内既有地面审计通过：44 道具、13 个道路网格、初始无视觉穿地、无重复 Plane collider、自由刚体约束保留；未重复运行上一轮 264 次物理落地测试。
- APK 目标：`Builds/PICO/重力战线-PICO.apk`。
- 最终 APK SHA256：`03476834605913812321cb95df706ddd9e459bf966cf156ac88eb9b3a8eafbc3`；文件 100,524,442 bytes（约 95.9 MiB）。源工程、英文镜像、设备 base.apk 一致。
- 原模拟器覆盖安装成功：仅 `Pico_XR_Visual_Lab / emulator-5554`，未使用 5556。2026-09-10 12:28:02 从实际 START GAME 按钮进入战斗，PID 14746；12:28:08 场景稳定通过（420 固定对象、44 道具、固定移动/闲置速度均 0），敌弹真实伤害 92→0 已记录。CUA 实际窗口在 20 血量时可见原边缘裂纹、中央两处新增碎裂、明显暗化与局部青色故障条带；实际画面在当前任务工具记录中。`Logs/VisorFault0910Emulator.log` 保存启动、稳定与伤害日志。

## 验证边界与现存构建提示

两套 39 项视觉验证都是 macOS 播放器，其中一套启用 PICO 布局代理；不等于真实双眼头显光学、舒适性或 Android GPU 实测。已补充原 5554 的实际战斗与受击可视验证（见上方）；本地 `Evidence/VisorFault0910` 图片仍明确是桌面预览，不冒充模拟器截图。

Android 构建日志存在 PICO SDK 空间音频三项原生库未 16KB 对齐提示，涉及 Android 15+ 设备兼容性；还有 PICO 深度 shader 变量重声明警告、Unity 过时对象查找 API 警告，以及本机 Metal 工具/许可证访问更新提示。构建最终成功，这些不是本轮碎屏 shader 编译失败。两次播放器退出均有 Unity 线程与内存清理诊断；未见本轮运行异常或失败测试。本轮不扩展修改 SDK、构建环境或无关旧警告。
