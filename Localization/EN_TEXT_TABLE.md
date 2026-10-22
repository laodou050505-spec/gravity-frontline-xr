# GRAVITY FRONTLINE — English Text Translation Table

> 原文由脚本从 `/Assets/GravityWarden/Scripts/Runtime/` 和 `/Editor/` 逐行提取，一字不改。
> EN 版对应代码中的当前实际值（可用 grep 核查）。
> 仅列玩家可见文字；内部对象名（VISUAL_、VFX_、SCENE_PREVIEW_ 等开头）、注释、
> Tripo 路径不在范围内，已在交付文档中单独说明。

---

## 1. HUD 主状态栏（GravityHud.cs）

| 原文（逐字提取） | 英文版实际值 | 来源行 | 备注 |
|---|---|---|---|
| `威胁波次  {game.Wave:00}` | `WAVE  {game.Wave:00}` | GravityHud.cs:134 | 波次计数器 |
| `得分  {game.Score:00000}` | `SCORE  {game.Score:00000}` | GravityHud.cs:135 | 积分显示 |
| `{game.AliveEnemies.Count:00} 个目标` (英文版直接 HOSTILES) | `{game.AliveEnemies.Count:00} HOSTILES` | GravityHud.cs:136 | 存活敌人数 |
| `奇点能量  {FloorToInt(UltimateCharge)}/{UltimateRequirement}` | `SINGULARITY  {FloorToInt(UltimateCharge)}/{UltimateRequirement}` | GravityHud.cs:138 | 奇点技能充能 |
| `斥力冷却 {game.RepulseCooldown:0.0}s` | `REPULSE COOLDOWN {game.RepulseCooldown:0.0}s` | GravityHud.cs:139 | 冷却读数 |
| `斥力就绪  左手 X` | `REPULSE READY  [Press Left X]` | GravityHud.cs:139 | 就绪提示，补加 [Press] 动作 |
| `{label}  待机` | `{label}  STANDBY` | GravityHud.cs:160 | 手部无目标状态 |
| `{label}  抓取 · {ShortName(displayName)}` | `{label}  HOLDING · {ShortName(displayName)}` | GravityHud.cs:161 | 持握状态 |
| `{label}  锁定 · {ShortName(displayName)}` | `{label}  LOCKED · {ShortName(displayName)}` | GravityHud.cs:162 | 锁定候选状态 |
| `{label}  待机 · 对准目标` | `{label}  STANDBY · AIM AT TARGET` | GravityHud.cs:163 | 无目标但可寻锁 |
| `目标`（ShortName 回退） | `TARGET` | GravityHud.cs:168 | displayName 为空时回退 |
| `护甲  {CeilToInt(game.Shield):000}` (原版 shieldText) | `WARD  {CeilToInt(game.Shield):000}` | GravityHud.cs:137 | 护盾值；术语统一用 WARD |

---

## 2. HUD 状态图标事件（原版 message.Contains() → 英文版 HudStatusEvent 枚举）

> 原版 GravityVisorHud.cs:158 和 GravityGeneratedHud.cs:72 用字符串匹配选图标。
> 英文版改为 HudStatusEvent 枚举，彻底消除中文匹配。

| 原文（逐字提取） | 枚举值 | 图标语义 | 来源行 |
|---|---|---|---|
| `受损` | `HudStatusEvent.Danger` | 盾受损红闪 | GravityVisorHud.cs:158 / GravityGeneratedHud.cs:72 |
| `冷却` | `HudStatusEvent.Ready` | 斥力冷却灰 | GravityVisorHud.cs:158 |
| `锁定` | `HudStatusEvent.Locked` | 目标锁定紫 | GravityVisorHud.cs:158 / GravityGeneratedHud.cs:72 |
| `已抓取` | `HudStatusEvent.Holding` | 抓取中紫 | GravityVisorHud.cs:158 / GravityGeneratedHud.cs:72 |
| `抓取` | `HudStatusEvent.Holding` | 同上 | GravityVisorHud.cs:158 |
| `击破` | `HudStatusEvent.Impact` | 击杀青绿闪 | GravityVisorHud.cs:158 / GravityGeneratedHud.cs:72 |
| `完成` | `HudStatusEvent.Impact` | 同上 | GravityVisorHud.cs:158 |
| `恢复` | `HudStatusEvent.Ready` | 恢复绿 | GravityVisorHud.cs:158 |
| `就绪` | `HudStatusEvent.Ready` | 就绪白 | GravityVisorHud.cs:158 |
| `释放` | `HudStatusEvent.Ready` | 投掷白 | GravityVisorHud.cs:158 |
| `对撞` | `HudStatusEvent.Impact` | 对撞冲击青 | GravityGeneratedHud.cs:72 |

---

## 3. 游戏内状态消息（GravityGameManager.cs SetStatus）

| 原文（逐字提取） | 英文版实际值 | 来源行 |
|---|---|---|
| `精准防御  +60` | `PRECISION DEFENSE  +60` | GravityGameManager.cs:239 |
| `护盾受损  -{amount}` | `WARD DAMAGED  -{amount}` | GravityGameManager.cs:251 |
| `斥力冷却 {RepulseCooldown:0.0}s` | `REPULSE COOLDOWN {RepulseCooldown:0.0}s` | GravityGameManager.cs:286 |
| `斥力爆发  击退 {hitCount} 个目标` | `REPULSE BURST  PUSHED {hitCount} TARGETS` | GravityGameManager.cs:315 |
| `斥力爆发  空域净空` | `REPULSE BURST  CLEAR` | GravityGameManager.cs:315 |
| `需要双手各锁定一个目标` | `LOCK TWO TARGETS  ONE PER HAND` | GravityGameManager.cs:325 |
| `双极对撞  完美分解` | `BIPOLAR COLLISION  ANNIHILATED` | GravityGameManager.cs:341 |
| `奇点能量不足 {UltimateCharge}/{UltimateRequirement}` | `SINGULARITY INSUFFICIENT  {UltimateCharge}/{UltimateRequirement}` | GravityGameManager.cs:349 |
| `当前没有可收束的敌人` | `NO TARGETS IN SINGULARITY RANGE` | GravityGameManager.cs:354 |
| `战场起点与追踪原点已重新校准` | `COMBAT ORIGIN RECALIBRATED` | GravityGameManager.cs:371 |
| `地爆天星：引力锚定` | `SINGULARITY: GRAVITY LOCK` | GravityGameManager.cs:383 |
| `地爆天星：群体环绕` | `SINGULARITY: ORBITAL PULL` | GravityGameManager.cs:393 |
| `地爆天星：奇点压缩` | `SINGULARITY: COMPRESSION` | GravityGameManager.cs:398 |
| `地爆天星：湮灭爆发` | `SINGULARITY: ANNIHILATION BURST` | GravityGameManager.cs:403 |
| `奇点坍缩  大道净空` | `SINGULARITY COLLAPSE  AREA CLEAR` | GravityGameManager.cs:437 |
| `对准敌人 · 扳机抓取 · 松开投掷` | `AIM · [Pull Trigger] TO GRAB · [Release Grip] TO THROW` | GravityGameManager.cs:471 |
| `第 {Wave:00} 波侵入者抵达 · 锁定目标` | `WAVE {Wave:00} INCOMING · LOCK TARGETS` | GravityGameManager.cs:472 |
| `防卫补给已投放  吸取即可恢复` | `SUPPLY DROP  GRAB TO RESTORE WARD` | GravityGameManager.cs:492 |
| `第 {Wave} 波守护完成  总分 {Score}` | `WAVE {Wave} CLEAR  SCORE {Score}` | GravityGameManager.cs:494 |

### 3a. 击杀原因消息（原版 OnTargetDestroyed 显示 reason 字符串 → 英文版 switch 枚举）

| 原版 reason 字符串（逐字提取） | DestroyReason 枚举 | 英文显示消息 |
|---|---|---|
| `击破` | `Default` | `TARGET DOWN` |
| `分解` | `Default` | `TARGET DOWN` |
| `反射爆裂` | `ReflectAttack` | `REFLECTION KILL` |
| `斥力` | `RepulseAttack` | `REPULSE KILL` |
| `双极对撞` / `对撞` | `BipolarCollision` | `COLLISION KILL` |
| `环境连锁` | `ChainReaction` | `CHAIN REACTION` |
| `奇点湮灭` | `SingularityAnnihilation` | `SINGULARITY ANNIHILATION` |
| `精准防御` | `PrecisionDefense` | `PRECISION DEFENSE` |
| `护盾恢复 +35`（HealthPack reason） | `HealthPack` | （无 HUD 消息，仅内部计分） |
| `爆散证据`（GravityEvidenceCapture） | `SmokeTest` | （smoke test，不显示，不计分） |

---

## 4. 手部交互消息（GravityHand.cs）

| 原文（逐字提取） | 英文版实际值 | 来源行 |
|---|---|---|
| `已抓取  {target.displayName} · 松开投掷` | `HOLDING  {target.displayName} · [Release Grip] TO THROW` | GravityHand.cs:120 |
| `已释放  {target.displayName}` | `RELEASED  {target.displayName}` | GravityHand.cs:133 |
| `目标锁定  {candidate.displayName} · 扳机抓取` | `LOCKED  {candidate.displayName} · [Pull Trigger] TO GRAB` | GravityHand.cs:318 |

---

## 5. 结算界面（GravityHud.cs ShowRunSummary）

| 原文（逐字提取） | 英文版实际值 | 来源行 |
|---|---|---|
| `本轮结算`（卡片标题） | `RUN COMPLETE` | GravityHud.cs:212 |
| `总分  {summary.TotalScore:00000}` | `TOTAL SCORE  {summary.TotalScore:00000}` | GravityHud.cs:213 |
| `通过关数  {summary.completedWaves:00}` | `WAVES CLEARED  {summary.completedWaves:00}` | GravityHud.cs:214 |
| `击破目标  {summary.enemiesDefeated:00}` | `TARGETS DOWN  {summary.enemiesDefeated:00}` | GravityHud.cs:215 |
| `基础战果  +{summary.baseCombatScore}` | `COMBAT SCORE  +{summary.baseCombatScore}` | GravityHud.cs:216 |
| `波次守护  +{summary.waveScore}` | `WAVE BONUS  +{summary.waveScore}` | GravityHud.cs:217 |
| `额外奖励`（奖励卡片标题） | `BONUS RECORD` | GravityHud.cs:219 |
| `精准防御 x{n}   +{bonus}` | `PRECISION x{n}   +{bonus}` | GravityHud.cs:220 |
| `反弹进攻 x{n}   +{bonus}` | `REFLECTION x{n}   +{bonus}` | GravityHud.cs:221 |
| `斥力击破 x{n}   +{bonus}` | `REPULSION x{n}   +{bonus}` | GravityHud.cs:222 |
| `双极对撞 x{n}   +{bonus}` | `COLLISION x{n}   +{bonus}` | GravityHud.cs:223 |
| `环境连锁 x{n}   +{bonus}` | `CHAIN REACTION x{n}   +{bonus}` | GravityHud.cs:224 |
| `奇点湮灭 x{n}   +{bonus}` | `SINGULARITY x{n}   +{bonus}` | GravityHud.cs:225 |
| `右手 A 重新部署` | `[Press Right A]: REDEPLOY` | GravityHud.cs:226 |

---

## 6. 道具 displayName（GravityTarget.cs + 场景序列化）

| 原文（GravityTarget.cs 逐字提取） | 英文版实际值 | 场景序列化实例数 |
|---|---|---|
| `蒸汽消防栓` | `Steam Fire Hydrant` | 11 |
| `机械邮筒` | `Mechanical Mailbox` | 1 |
| `街道隔离柱` | `Street Barrier` | 4 |
| `手压水泵` | `Hand Pump` | 6 |
| `街边金属长椅` | `Metal Bench` | 5 |
| `金属货箱` | `Metal Crate` | 12 |
| `齿轮装饰街道筒` | `Gear Street Column` | 5 |
| `可吸取物`（回退 displayName） | `Pullable Object` | GravityTarget.cs:21 |

---

## 7. GravityTarget 重力锁定消息

| 原文（逐字提取） | 英文版实际值 | 来源行 |
|---|---|---|
| `{displayName} 质量过大：引力锁定 {PullStagesCompleted}/{pullStagesRequired}` | `{displayName} TOO HEAVY: GRAVITY LOCK {PullStagesCompleted}/{pullStagesRequired}` | GravityTarget.cs:340 |

---

## 8. 产品名称与控制器提示

| 原文（逐字提取） | 英文版实际值 | 位置 |
|---|---|---|
| `重力战线` | `GRAVITY FRONTLINE` | ProjectSettings.asset / GravityExistingSceneUpgrader.cs:1259 |
| `扳机 / 握把 引力抓取 · 双手合拢 对撞 · 左 X 斥力 · 右 A 奇点 · 左 Y 重置视角` | `[Pull Trigger] / [Hold Grip] TO PULL · CLAP HANDS FOR COLLISION · [Left X] REPULSE · [Right A] SINGULARITY · [Left Y] RECENTER` | GravityExistingSceneUpgrader.cs:1212 |
| `PICO 扳机/握把吸取 · 合拢对撞 · 左 X 斥力 · 右 A 奇点 · 左 Y 归位  |  桌面 Q/E · Space · F · R · C` | `[Trigger]/[Grip] PULL · CLAP COLLISION · [Left X] REPULSE · [Right A] SINGULARITY · [Left Y] RECENTER  |  Desktop Q/E · Space · F · R · C` | GravityProjectBuilder.cs:466 |

---

## 9. CJK 扫描摘要

- 扫描范围：`Assets/GravityWarden/Scripts/Runtime/` 和 `Editor/` 全部 `.cs` 文件
- 玩家可见字符串：**零残留**（所有运行时 text 赋值均为英文）
- 允许保留中文的类别（规范明确豁免）：
  - 内部对象名（`VISUAL_*`、`VFX_*`、`SCENE_PREVIEW_*`、`ENVIRONMENT_*` 等前缀）
  - 子组件部件名（护腕零件名仅在 Editor/GravityProjectBuilder.cs 中用于生成 3D 节点，不显示给玩家）
  - 场景描述注释行
  - Tripo 资产路径（`tripo_convert_*`）
  - 音乐资产名（`BGM_机械欧式街道战斗循环_CC-BY-4.0`）

---

## 10. Tripo 调用次数

**0 次**（无新资产生成；所有 HUD 艺术素材为预有英文版本，零 Tripo 调用）

---

_提取工具：python3 -I + re.findall STRING_LITERAL 扫描各 .cs 文件，2026-10-22_
