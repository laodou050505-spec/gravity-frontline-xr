# GRAVITY FRONTLINE — 英文版交付报告
**工程路径：** `/Users/ld/Documents/Codex/EnglishEditions/GravityFrontline-EN/`
**原版路径：** `/Users/ld/Documents/Codex/2026-08-13/y-2/outputs/引力 斥力/`（原版完整不动）
**交付日期：** 2026-10-22

---

## APK 交付

| 项目 | 值 |
|---|---|
| 文件路径 | `Builds/EN/GravityFrontline-EN-PICO.apk` |
| 文件大小 | 94 MB (99,078,780 bytes) |
| SHA-256 | `2a684cfde7ea28fe7d40b8e1157b657910a46d4187625300d99aba460d8072e4` |
| 包名 | `com.gravitywarden.repulsion.en` |
| 产品名 | `GRAVITY FRONTLINE` |
| Unity 版本 | 6000.5.5f1 |
| 目标平台 | Android ARM64 / PICO 4 / IL2CPP |

---

## 关键工程变更摘要

### 1. HudStatusEvent 枚举（新增）
`Assets/GravityWarden/Scripts/Runtime/GravityStatusTypes.cs` — 新建文件  
替代 `GravityVisorHud.cs` 和 `GravityGeneratedHud.cs` 中的 `message.Contains("中文")` 图标选择逻辑。

### 2. DestroyReason 枚举（新增）
同 `GravityStatusTypes.cs` — 替代 `GravityTarget.Damage()` / `DestroyTarget()` 的 reason 字符串参数，
消除 `RecordEnemyScore(string)` 中的中文字符串评分逻辑。

### 3. 所有运行时 HUD 文字 → 英文
涵盖：GravityHud.cs、GravityHand.cs、GravityGameManager.cs、GravityTarget.cs

### 4. 场景文件 displayName 修复
`Assets/GravityWarden/Scenes/GravityBoulevard.unity` — 44 处中文 displayName 替换为英文：
- 蒸汽消防栓 → Steam Fire Hydrant（11 处）
- 金属货箱 → Metal Crate（12 处）
- 手压水泵 → Hand Pump（6 处）
- 街边金属长椅 → Metal Bench（5 处）
- 齿轮装饰街道筒 → Gear Street Column（5 处）
- 街道隔离柱 → Street Barrier（4 处）
- 机械邮筒 → Mechanical Mailbox（1 处）

### 5. 手柄键位格式更新
所有手柄按键提示改为带动作的括号格式：
- `扳机抓取` → `[Pull Trigger] TO GRAB`
- `松开投掷` → `[Release Grip] TO THROW`
- `左手 X` → `[Press Left X]`
- `右手 A 重新部署` → `[Press Right A]: REDEPLOY`
- `左 Y 归位` → `[Left Y] RECENTER`

---

## CJK 扫描结果

- 扫描工具：`python3 -I` + 正则字符串字面量提取  
- 玩家可见字符串中文残留：**零**  
- 允许保留中文（内部名称，不显示给玩家）：
  - 内部对象名前缀（`VISUAL_*`、`VFX_*`、`SCENE_PREVIEW_*`、`ENVIRONMENT_*`）
  - Editor/GravityProjectBuilder.cs 中护腕零件部件名（`主金属护壳`等，仅用于生成 3D 节点层级）
  - 音乐资产名（`BGM_机械欧式街道战斗循环_CC-BY-4.0`）
  - 场景注释行、Tripo 路径

---

## Tripo 调用次数

**0 次** — 无新资产生成需求，所有 HUD 图标均为预有英文素材。

---

## VisualQA

`VisualQA/EN-20261022/` 目录下 15 张 UI 设计参考截图：

| 文件名 | 覆盖内容 |
|---|---|
| `ui-asset-Title.png` | 游戏封面标题屏 |
| `ui-asset-Start.png` | 开始/教程提示界面 |
| `ui-asset-HudTopLeft.png` | HUD 左上角（波次/得分/敌人数） |
| `ui-asset-HudTopRight.png` | HUD 右上角（护盾/奇点充能） |
| `ui-asset-HudBottomLeft.png` | HUD 左下角（斥力状态/左手读数） |
| `ui-asset-HudBottomRight.png` | HUD 右下角（右手读数） |
| `ui-asset-VisorLabels.png` | 护目镜叠加状态标签 |
| `ui-asset-Ready.png` | HUD 状态图标 — Ready |
| `ui-asset-Locked.png` | HUD 状态图标 — Locked（紫色目标锁定） |
| `ui-asset-Holding.png` | HUD 状态图标 — Holding（持握中） |
| `ui-asset-Impact.png` | HUD 状态图标 — Impact（击杀/对撞） |
| `ui-asset-Danger.png` | HUD 状态图标 — Danger（盾受损） |
| `ui-asset-ResultLeft.png` | 结算左卡（RUN COMPLETE / TOTAL SCORE） |
| `ui-asset-ResultRight.png` | 结算右卡（BONUS RECORD 各项） |
| `ui-asset-Exit.png` | 退出/重新部署界面 |

**注：** 上述截图为 UI 设计参考图（源素材）。真机运行截图需部署至 PICO 设备或在
交互式 Unity 编辑器中运行后截图——当前 batchmode 构建环境不支持实时渲染捕获。
如需真机截图，请在 PICO 4 设备上安装 APK 后录屏/截图。

---

## 模拟器安装记录

| 项目 | 值 |
|---|---|
| 目标设备 | `emulator-5554` |
| 安装日期 | 2026-10-22 14:03 |
| 安装命令 | `adb -s emulator-5554 install GravityFrontline-EN-PICO.apk` |
| 安装结果 | Success |
| pm list 验证 | `package:com.gravitywarden.repulsion` + `package:com.gravitywarden.repulsion.en` 同时存在 |

---

## 未验证边界

- **PICO XR 多视图渲染**：APK 通过 IL2CPP+Multiview 构建，但未在设备上运行测试
- **护腕中文部件名**：`主金属护壳` 等仅存在于 Editor 生成代码（`GravityProjectBuilder.cs`），
  若在运行时有 GameObject.Find 调用（目前无发现）需额外检查
- **DesktopFallbackController 桌面按键**：Desktop Q/E/Space/F/R/C 仅在非 XR 模式有效，未测试

---

## 不确定翻译

| 原文 | 英文版选择 | 不确定原因 |
|---|---|---|
| 护盾 → WARD vs SHIELD | SHIELD（统一） | 烘焙 HUD 标签（VisorLabels/HudBottomRight）本身写 SHIELD，动态文字已统一改为 SHIELD |
| 地爆天星（ultimate 技能名） | SINGULARITY（统一） | 直译为"Ground Explosion Star"过于字面；选用与技能充能条一致的 SINGULARITY |
| 波次守护 | WAVE BONUS | "守护" 有"defend/guard"义，但结算语境下 BONUS 更符合奖励分类 |
| 奇点湮灭 | SINGULARITY ANNIHILATION | 直译保留，符合技能名称传统 |

