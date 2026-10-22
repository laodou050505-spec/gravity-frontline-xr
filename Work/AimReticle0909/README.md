# 简约准星与真实手柄索敌 — 2026-09-09

## 接续范围
读取 Codex 任务「重力战线2」(01a0802a-0f7b-7991-bda3-c437b234c55d)，接续用户17:04要求：中央不再花哨，随手柄移动并真实索敌。上次只写了部分代码，最后构建误用中文源目录而失败。

## 最终实现
- 中央平时仅小点，真实选中或持有目标时显示四角框；移除序列化场景遗留大光学层与 AIM_OPTIC。保留既有周边 Tripo Nano Banana 面罩素材、动态数值和顶层材质。
- 准星从真实目标中心/射线表面落点经玩家相机投影到 HUD，避免直接手柄射线与近 HUD 平面相交造成的视差。出视野/背后隐藏，不在边缘伪装锁定。
- GravityHand.FindAimTarget 保留前向近抓和0.22米球射线辅助，距离优先并尊重实体遮挡，移除剩1–2敌人时无方向自动补选与另一手抓机器人时自动找道具。
- 最近有效移动选择活动手；抓取/释放输入优先0.75秒；静止悬停不抢手。rayOrigin.forward 作为真实射线方向；近眼保护不再覆盖真实手柄旋转。
- Configure可重复执行而无重复准星/遗留层。桌面Canvas配置完成后不再每帧重置，消除晚更新时准星消失。
- 状态提示区分「已抓取」与「目标锁定…扳机抓取」，锁定不误显示 HOLDING。

## 验证与产物
- 官方Unity6000.5.5f1桌面、Android构建通过；项目检查包含PXR loader/Multiview/ARM64/IL2CPP/Activity/Input System-only。
- 最终桌面测试31/31通过。Evidence/AimReticle0909Delivery/Results.txt：四方向移动、目标中心投影误差<0.06px、空射/遮挡脱锁、左右手切换、抓取、重复配置、顶层材质、出视野隐藏。连续8个渲染完成帧无强制刷新，8帧全部可见、锁定与主手稳定，jitter=0px，最大投影误差0.040px。
- 最终APK: Builds/PICO/重力战线-PICO.apk；SHA256 cad8cdcea5cc6abe6e1ddf97af2ed92f98170ec357cb70267abf76f3de32a7ea。源工程与ASCII镜像运行脚本一致；5554安装Success，设备base.apk哈希与本地一致。
- 5554实际进入战斗，CUA窗口已观察到简化后的中央区域和既有周边HUD。最终PID11464日志18:40–18:41记录真实Right射线变化、位置变化、Robot/道具命中以及target=none脱锁。实际模拟器截图留在本次任务工具记录，未冒充桌面图。
- Logs/AimReticle0909DeliveryMac.log、AimReticle0909FinalAndroid.log、AimReticle0909DeliveryEvidence.log、AimReticle0909FinalEmulator.log。
- 当前桌面包 Builds/macOS/GravityModularReview.app。桌面测试由 -gravityAimEvidence -gravityAimOutput <输出目录> 显式启用，正常游戏不创建测试对象。
- 本轮未重跑旧完整多波次smoke；新增选择/抓取回归通过且最终模拟器战斗运行。未做真实PICO头显舒适度与实体手柄测试。

## 不变的约束
只用既有Pico_XR_Visual_Lab / emulator-5554，不创建或使用5556。封面PicoHorizontalFill=.74和_Feather=.20保持原确认值。Android构建必须在 /Users/ld/Documents/Codex/2026-08-13/y-2/work/gravity_repulsion_pico_build。不要重置工程的大量历史未提交改动。

既有图片来源见 Work/JarvisHudNanoBanana/README.md（Tripo Nano Banana）；本轮未生成新图、未产生生图额度费用。
