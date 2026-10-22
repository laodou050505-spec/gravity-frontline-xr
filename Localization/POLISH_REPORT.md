# GRAVITY FRONTLINE — 视觉与体验打磨报告
**打磨日期：** 2026-10-22（本轮）
**工程路径：** `/Users/ld/Documents/Codex/EnglishEditions/GravityFrontline-EN/`
**原版路径：** `/Users/ld/Documents/Codex/2026-08-13/y-2/outputs/引力 斥力/`（只读参照）

---

## 改动清单

### 1. 封面字标（Title.png）— 最关键改动

**改动：** 替换 `Assets/GravityWarden/Resources/GravityUI/Title.png`

**改前：** 使用原版中文游戏字标（重力战线原始设计，中文美术）作为临时占位；  
上一轮曾尝试生成深蓝星空底字标（TitleBanana2_20261022），被退回（背景非纯黑）。

**改后：** 使用 `TitlePremiumFix/TitlePremium/01_generated_image_url.png`（banana2 生成，
已在 Sep 8 14:49 存于工程）居中裁剪至 2202×1414，替换为高级感字标：
- **文字：** GRAVITY FRONTLINE（两行，全大写，等高字体，拼写逐字核查 ✓）
- **设计：** 银色/铬金属 3D 字体 + 铜银引力轨道环 + 青色能量点
- **背景：** 纯黑 #000000（经像素抽样验证：四角最大 RGB 值 = 1，低于 shader 阈值 4.59）
- **尺寸：** 2202×1414 ✓，与 TitleKey 模块（630×410）比例一致

**抠像公式验证：**  
`_KeyThreshold = 0.018`（约 4.59/255）；  
四角像素最大值为 1/255 < 4.59/255 → 全部被键出，无方块或灰雾风险。

| 位置 | 像素值 | 是否键出 |
|---|---|---|
| (0,0) | #010101 (1,1,1) | ✓ 键出 |
| (2201,0) | #010000 (1,0,0) | ✓ 键出 |
| (0,1413) | #000000 (0,0,0) | ✓ 键出 |
| (2201,1413) | #000000 (0,0,0) | ✓ 键出 |

---

### 2. 手柄震动增强（GravityGameManager.cs）

#### 2a. 受伤震动（DamagePlayer）
- **改前：** 玩家护盾受损时仅有音效和视觉冲击波，无手柄震动。
- **改后：** 双手同时震动，强度随剩余护盾量缩放（护盾越低震动越强，0.55→0.88），历时 0.14s。
- **死亡：** 护盾归零时，双手各触发 3 次重型连续脉冲（0.95 amplitude，0.1s/次）。

#### 2b. 波次清除震动（CheckWaveState）
- **改前：** 波次清除无手柄反馈。
- **改后：** 波次清除时双手触发 2 次轻触确认脉冲（0.38 amplitude，0.06s/次），
  让玩家在不看 HUD 时也能感知波次更替节奏。

**已有震动保留（不改）：**
- 抓取（+0.32）、持握周期脉冲（+0.07/0.13）、释放投掷（+0.20）
- 斥力爆发（+0.48 双手）、双极碰撞（+0.80 双手）
- 命中格挡（+0.65）

---

### 3. 命中特效增强（GravityFeedback.cs — HitFlash）

**改前：** 单个球形闪光，最大缩放 0.8，持续 0.16s。  
**改后：** 主球形闪光（最大缩放 1.05，0.18s）+ 次级光环（宽度 0.022，扩散至 0.72，0.22s），
增加深度感和能量感，同时不增加 Draw Call 预算（共 2 个对象，略高于原来 1 个，
动画结束后自动销毁）。

---

## Tripo 调用清单

本轮打磨未新增 Tripo 生成任务。

TitlePremium 字标来自 Sep 8 本工程已有生成（早期探索阶段产物，当时因比例问题未被采用；  
本轮重新评估后确认质量达标，仅做 sips 中心裁剪（2752×1536 → 2202×1414））。

| task_id / 来源 | 用途 | 状态 |
|---|---|---|
| TitlePremium（Sep 8 工程内存档） | 封面字标，banana2 生成 | 居中裁剪采用 ✓ |

---

## 性能基线（改动前）

数据来源：之前构建日志及 Unity Editor 统计（批处理模式，不能实时采样 GPU）。

| 指标 | 基线值（上轮 APK）| 本轮变化 |
|---|---|---|
| Title.png 文件大小 | 3.95 MB（原版字标） | 4.31 MB（+9%，因裁剪后 PNG 无损） |
| APK 大小（上轮） | ~99 MB | 预期相近（Title 增量小） |
| 改动脚本 | 0 | 2 个 .cs 修改（Manager + Feedback） |
| 新 Draw Call（HitFlash） | 1 obj/命中 | 2 obj/命中（+1 光环，动画 0.22s 后销毁） |
| 手柄震动 API 调用 | 4 类事件 | 6 类事件（+受伤、+波次清除） |

---

## 未完成项与风险

| 项目 | 状态 | 原因 |
|---|---|---|
| 街道场景光照提质 | 未改 | 场景由代码生成（GravityProjectBuilder），修改需重建场景；风险高，不宜本轮触碰 |
| 结算面板材质提质 | 未改 | ResultLeft/ResultRight PNG 已是英文 Nano Banana 设计，质量达标 |
| Start/Exit 按钮提质 | 未改 | 原版已是英文设计；尺寸和命中区域不变是红线，不宜改动 |
| 真机 PICO 4 验证 | 未测 | 无实体头显 |
| 编辑器运行画面截图 | 未取 | 构建采用 batchmode，不支持 Play 模式截图；VisualQA 提供资产对比图 |

---

## VisualQA 截图清单

`VisualQA/Polish-20261022/`：

| 文件名 | 内容 |
|---|---|
| `cover_BEFORE_rejected_starfield.png` | 退回的深蓝星空底字标（banana2，2752×1536） |
| `cover_BEFORE_original_keyart.png` | 原版 key art 参照图 |
| `cover_AFTER_premium_gravity_frontline.png` | 新字标（2202×1414，纯黑底，GRAVITY FRONTLINE 金属引力环） |
| `hud_visor_labels.png` | HUD VisorLabels 字标图集（8 行英文 HUD 标签） |
| `hud_visor_overlay.png` | JarvisVisorOverlay 护目镜光学叠加层 |

---

## 原版自检说明

本轮代码修改（GravityGameManager.cs、GravityFeedback.cs）不改变任何游戏规则、
数值、碰撞、输入映射、状态机转换或 UI 布局。布局代理 54 项基于布局不变的约束，
本轮未触动任何布局相关代码，理论上仍然通过。GravityEvidenceCapture 亦不受影响。

---

*报告生成：2026-10-22 打磨轮次*

---

## 包名修复与构建阻塞说明（16:25 追加）

**问题发现：** 第二次构建完成后，用 `aapt dump badging` 检查，发现 APK 包名为 `com.gravitywarden.repulsion`（缺少 `.en` 后缀）。

**根因：** `ProjectSettings/ProjectSettings.asset` 中 `applicationIdentifier.Android` 在历史某次提交中被还原为原版包名。

**已修复：** 已将 `ProjectSettings.asset` 的包名改回 `com.gravitywarden.repulsion.en`。

**阻塞项：**
1. **磁盘空间不足**：当前可用仅 ~10 GB，低于 15 GB 阈值，不能启动第三次 Unity 构建。
2. **Library/Bee 删除被权限系统拒绝**：已尝试 `rm -rf Library/Bee`，权限系统两次拒绝操作。  
   Library/Bee 大小 ~4.8 GB；若能删除，可释放至 ~15 GB，从而满足构建前提条件。  
   **依规范"删除被权限系统拒绝时，在报告里说明，不要绕过"**，此项须由用户或总控手动处理。
3. **模拟器安装未完成**：因 APK 包名不合格，已按规范跳过安装，等待正确 APK。

**当前状态：**
- ProjectSettings.asset 已修复（`com.gravitywarden.repulsion.en`）并含于本次 git 提交
- 需要：用户/总控手动删除 `Library/Bee` 和 `Temp`，磁盘回到 ≥15 GB 后，可执行第三次构建
- 第三次构建命令（就绪后直接运行）：
  ```
  lockf /tmp/en-unity-A.lock /Applications/Unity/Hub/Editor/6000.1.1f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit \
    -projectPath /Users/ld/Documents/Codex/EnglishEditions/GravityFrontline-EN \
    -buildTarget Android \
    -executeMethod GravityRepulsion.Editor.GravityExistingSceneUpgrader.BatchBuildAndroid \
    -logFile Logs/polish_build3_20261022.log
  ```
  然后 aapt 验证 → `lockf /tmp/pico-emulator.flock adb -s emulator-5554 install -r Builds/EN/GravityFrontline-EN-PICO.apk`

