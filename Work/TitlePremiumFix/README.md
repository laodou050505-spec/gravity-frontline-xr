# 重力战线：自然大背景、金属标题按钮与地面修复

## 当前视觉
- 用户否定了原来左侧普通字标、按钮，以及右侧独立硬边方图。
- 最终由全幅钟楼街道背景、重力环金属英文标题、START GAME、EXIT GAME 独立模块组成。
- 标题和按钮使用黑色键透明材质叠在场景上；背景按视口裁切，不拉伸建筑。
- 静态字形由 Tripo Nano Banana Pro 生图，实时数值继续由 Unity 绘制。
- 素材生成均成功：标题 c788fd7a-1e1b-4ad6-bd09-124815ede61f；按钮 e74fee2d-cbd4-47c4-b173-58416f9eca2c；大背景 4fe48909-1ace-4aa4-b723-21a65bce3aa2。每项 15 credits，本轮合计 45；详见 generation-results.json。

## 地面与碰撞
- 原场景 Plane 约 82 米宽，7 个可吸物的所有碰撞器为安全 Trigger，释放时仍保持 Trigger，这是穿地主要原因。
- 复用原地面视觉/材质，扩展为 200×200 米并保持贴图密度；独立 2 米厚固定实体基础，不可被吸走。
- 安全 Trigger 只适用于原始未互动状态。激活物理/释放后恢复实体碰撞；保留持有阶段的穿透碰撞行为。
- 抓取跟随、释放和动态物理补充地面穿透修正，防止低处锚点及高速释放把物体送入地下。

## 菜单输入
- 保留明确按钮命中后触发，取消了“射线原点接近头部就无效”的假设。
- 支持 Unity 鼠标/触屏、XR 手柄按钮及模拟扳机、PICO 原生手部瞄准和捏合。
- 开启 PICO ControllersAndHands；首启可能出现模拟手势权限请求。
- 模拟器默认 Eye Gesture Mode 与手柄模式是不同输入路径。官方说明：https://developer.picoxr.com/document/spatial-toolkit/pico-emulator-ui/

## 已验证
- Unity 6000.5.5f1 桌面及 Android 编译/构建成功；PICO loader、Multiview、ARM64、IL2CPP、Activity、InputSystemOnly 验证通过。
- Logs/PremiumGroundSmoke.log：7 个原始 Trigger 物体以 30m/s 下落全部在 y=.100 停稳；贴地抓取、40m/s 释放、9 个地面覆盖点及固定性通过。
- Logs/NaturalBackgroundGameplaySmokeRetry.log：完整游戏回归通过第 3 波，已完成 2 波。第一次与构建并发时 .45 秒缩放采样失败（.912），构建结束后原测试原阈值重跑通过（.849），未修改游戏缩放或放宽测试。
- Evidence/NaturalBackgroundFinal/{01_Title,02_Combat,03_Result}.png 为实际 Unity 相机渲染。
- Logs/NaturalBackgroundMacBuild.log、NaturalBackgroundAndroidBuild.log、NaturalBackgroundEvidence.log。

## 交付
- 权威工程：/Users/ld/Documents/Codex/2026-08-13/y-2/outputs/引力 斥力。
- 最终 APK：Builds/PICO/重力战线-PICO.apk。
- SHA-256：87abcf234caf09d74808860d1cbf43d80323c6a48b3b697d49aa364f7a0ec0d2。
- 复核桌面包：Builds/macOS/GravityModularReview.app。
- 共享模拟器 emulator-5554 被其他游戏任务切换，因此通过官方 pico-cli 创建独立 GravityFrontline_UI_Test（emulator-5556）。最终 APK 安装成功。15:05:23 的运行日志记录 GRAVITY_MENU_HIT_ACTIVATE start=True exit=False ready=True，随后成功加载 GravityBoulevard，销毁标题根，15:05:29 场景稳定性通过。交互由模拟器实际输入触发，并非 -gravitySmoke 自动跳转。日志及游戏画面已保存；没有验证所有手势模式或真实头显。实例保持运行。
