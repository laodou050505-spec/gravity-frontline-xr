# 2026-09-10 碎屏细化：死亡保留、中央等比、轻伤渐进

## 用户要求与本轮结果

用户要求死亡结算保留死亡时碎屏、变暗和机械故障；修正中央裂纹纵向拉伸；减轻轻伤初期的边缘碎屏，使其随失血渐进增强。本轮直接细化现有材质和显示状态，继续使用此前的 Nano Banana 图，不新生成素材。

## 实现摘要

- 新增独立 HUD 子节点 `VISOR_DAMAGE_CANVAS`，把故障、边缘碎屏、中央碎屏三层移入此节点，位于战斗 HUD 和结算卡片下方。结算隐藏战斗 HUD 时不再连带隐藏损伤层。
- `GravityHud.ShowRunSummary` 调用 `HoldDeathState()`，先把最后一击的损伤提交到显示值，再冻结死亡状态，包括受击边缘和 `_FaultTime`。`SetHealth` 与逐帧渐变在死亡锁定期间停止改变视觉参数；三层保持显示。
- `HideRunSummary` 调用 `ResetForCombat()`。真实重新部署仍走 `GravityGameManager.RestartGame` 重载当前战斗场景，新一局满血、暗化和裂纹清空。
- 中央贴图为 2048×1280，按贴图比例等比适配画布，PICO 从错误的 2400×2400 拉伸改成 2400×1500；桌面仍为 1600×1000。完整 UV 保留。只修改中央层尺寸，原周边碎屏的纹理、图案、位置、UV 与参考尺寸保留。
- 边缘 shader 增加 `_EdgeStrength`，强度为 `0.25 + 0.75 × SmoothStep(0,1,失血比例)`。轻伤柔和，失血后逐步增加，完全损伤恢复原最大裂纹强度。原全屏暗化与局部机械故障继续保留。
- 主要代码：`GravityVisorDamage.cs`、`GravityHud.cs`、`GravityVisorDamage.shader`；回归证据：`GravityVisorDamageEvidence.cs`。本目录原有 `before` 文件保留，未覆盖。

## 独立验证结果

独立核对以下日志与对应 `Results.txt`，均为 **54/54 通过，0 失败**：

- `Logs/VisorRefinement0910Damage.log` → `Evidence/VisorRefinement0910/`
- `Logs/VisorRefinement0910PicoDamage.log` → `Evidence/VisorRefinement0910Pico/`

两个证据目录各有 24 张 PNG，所有日志列出的截图均存在。测试通过真实 `DamagePlayer` 100→92→84→75→45→15，两次真实回血包回到 85，然后真实致死；最后调用生产 `RestartGame`，等待新战斗场景并验证满血清空。测试仅暂停自主敌人攻击，不伪造玩家血量或伤害流程。

### 轻伤实际渲染强度

使用同一边缘层材质/纹理/伤害值渲染黑底，比较新渐进强度与 `_EdgeStrength=1` 的原强度；以下是像素亮度总和比值，不是仅检查参数。

| 血量 | 桌面实际相对强度 | PICO 布局代理实际相对强度 | 设置的边缘强度 |
| --- | --- | --- | --- |
| 92 | 0.246 | 0.249 | 0.264 |
| 84 | 0.300 | 0.300 | 0.301 |
| 75 | 0.367 | 0.366 | 0.367 |

两套实际渲染总亮度与强度均随失血单调增加，轻伤明显弱于旧版。92 血量线条极细，8 位图像量化使像素比值略低于材质参数。对应 `EdgeOnly_health92/84/75.png` 与场景 `01b_Health92.png`、`01c_Health84.png`、`02_Health75.png`。

### 中央纹理等比

按实际 RectTransform 世界四角宽高与纹理像素尺寸计算，桌面和 PICO 布局代理在正常投影、110° 宽 FOV，以及重新部署后的纹理像素纵横比均为 **1.000**（要求误差小于 1%）。PICO 世界尺寸 4.2×2.625，保持贴图 1.6:1 宽高比。独立查看 `Evidence/VisorRefinement0910Pico/04_Health15.png`，中央两个碎裂点恢复比例，没有此前的竖向拉长。

### 死亡保留与重新部署

真实致死后两套测试均得到：Shield=0、DamageRatio=VisibleDamage=1、HitPulse=1、暗化=0.82、FaultRatio=1、中央不透明度=0.82、边缘强度=1，`DeathStateHeld=true`，三层仍显示。等待 2.6 秒后全部视觉参数（包括 `_FaultTime`）最大变化 **0.000000**。

结算层位于损伤根之上：结算 panel sibling=20，`VISOR_DAMAGE_CANVAS` sibling=0，14 个活动文字节点采用顶层材质。独立查看 `06b_RunSummary_Held.png`，死亡裂纹、红边和暗化保留，结算信息清晰显示在上方。随后调用真实重新部署入口，新 manager 和 visor 创建，血量=100，DamageRatio/VisibleDamage/HitPulse/暗化/FaultRatio/中央裂纹均为 0，死亡锁定解除；白底实际 shader 核心亮度=1.000、新增裂纹=0。边缘强度下限参数仍为 0.25，但满血的裂纹 reveal 为 0，所以实际画面完全清晰。

上一轮重点也回归通过：75/45/15 血量的同帧实景亮度比分别 0.841/0.678/0.496，回血到 85 后为 0.895；机械故障仅改变约 2.7% 像素，全图平均变化约 0.001，核心变化=0。

## 构建与交付文件

- 官方 Unity：`6000.5.5f1`。
- 最终 macOS 构建日志 `Logs/VisorRefinement0910Mac2.log`：`Build Finished, Result: Success.`、`GRAVITY_MODULAR_REVIEW_BUILD_PASS`，正常退出。
- Android 构建在英文镜像 `work/gravity_repulsion_pico_build`，日志 `Logs/VisorRefinement0910Android.log`：`Build Finished, Result: Success.`、`GRAVITY_CURRENT_LAYOUT_BUILD_PASS`，正常退出。
- PICO loader、Multiview、ARM64、IL2CPP、Activity、Input System-only 校验通过。无本轮 C# 编译错误或 visor shader 错误。
- 原地面审计随构建通过：44 道具、13 个精确道路网格、初始无视觉穿地、无重复 Plane collider、自由刚体约束保留。没有在本轮重复上一轮 264 次物理落地测试。
- 独立逐字核对源工程与英文镜像的 runtime、evidence、HUD、两份 visor shader、中央纹理及主场景，全部一致。
- 最终 APK：`Builds/PICO/重力战线-PICO.apk`
- 实际文件大小：**100537762 bytes**（独立读取）。Android 日志中的 `size=785788037` 是 BuildReport 报告值，不能用作磁盘压缩 APK 大小。
- SHA256：`19aeeee279bc2cc7744df4bdf98e4d367b5e13fce934adb1875223eab1bf4893`（独立重新计算一致）。
- 原模拟器覆盖安装成功：仅 `Pico_XR_Visual_Lab / emulator-5554`，设备 base.apk 与上方 hash 相同。实际 PID17959，12:55:23 从封面进入战斗，12:55:29 场景稳定通过；敌人真实攻击使血量92→0，12:55:55输出 `GRAVITY_VISOR_DEATH_HELD damage=1.000 darkening=0.820`。CUA实际窗口确认结算面板显示时，边缘/中央裂纹、暗化与故障条带持续保留，结果文字在上层。实际窗口图保留在本任务工具记录；本地PNG均为桌面或布局代理。完整日志 `Logs/VisorRefinement0910Emulator.log`。

## 验证边界与既有提示

两套 54 项验证均为 macOS 播放器，其中 PICO 目录启用了 PICO 画布布局代理；不能称为真实头显实拍、实际双眼舒适性或 Android GPU 验证。16:10 桌面代理视口会裁切 PICO 正方形画布上下边缘，中央贴图比例已通过世界几何测量验证。原5554实际结算显示验证已补充于上方；仍未做真实头显舒适性测试。

构建日志仍有项目已有的 SDK/第三方 shader 警告、过时对象查找 API 警告、许可证访问更新提示、本机 Metal 工具提示，以及三个 PICO 空间音频原生库未 16KB 对齐的 Android 15+ 兼容性提示。没有本轮 visor shader 编译失败；构建和全部测试成功。未扩展修改 SDK、无关 shader、地面或已确认封面。
