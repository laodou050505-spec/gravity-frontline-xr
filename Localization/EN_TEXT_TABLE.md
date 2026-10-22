# GRAVITY FRONTLINE — 英文版翻译表 EN_TEXT_TABLE.md
生成日期：2026-10-22

## 术语表（全游戏统一用词）

| 中文术语 | 英文术语 | 备注 |
|---|---|---|
| 重力战线 | GRAVITY FRONTLINE | 产品名，全大写 |
| 斥力 | Repulse / REPULSE | 技能名大写，描述用首字母大写 |
| 奇点湮灭 / 奇点 | Singularity / SINGULARITY | 终极技能 |
| 双极对撞 | Bipolar Collision | 两手抓物合拢的攻击方式 |
| 精准防御 | Precision Defense | 用手持物挡住投射物 |
| 环境连锁 | Chain Reaction | 环境爆炸链 |
| 引力抓取 | Gravity Grab | 主要抓取机制 |
| 护盾 | Shield | 玩家血量 |
| 奇点能量 | Singularity Energy | 终极技能能量槽 |
| 波次 | Wave | 敌人出现轮次 |
| 巡逻机 | Scout | 机器人敌人1型 |
| 工程机 | Engineer | 机器人敌人2型 |
| 盾卫核心 | Shield Core | 机器人敌人3型（boss） |
| 危险导弹 / 危险能量弹 | Danger Projectile / Danger Missile | 射弹 |
| 防卫补给 | Shield Supply | 补血道具 |
| 可吸取物 | Pullable Object | 通用道具基础名 |

---

## HUD 常驻文字（GravityHud.cs / Resources 图片）

| key | 原中文 | 英文 | 来源文件:行 | 使用场景 | 状态 |
|---|---|---|---|---|---|
| hud.wave | 威胁波次 {wave:00} | WAVE {wave:00} | GravityHud.cs:Refresh() | 左上HUD波次 | 完成 |
| hud.score | 得分 {score:00000} | SCORE {score:00000} | GravityHud.cs:Refresh() | 右上HUD得分 | 完成 |
| hud.singularity | 奇点能量 {charge}/{max} | SINGULARITY {charge}/{max} | GravityHud.cs:Refresh() | 右上HUD能量 | 完成 |
| hud.repulse_cooldown | 斥力冷却 {t:0.0}s | REPULSE COOLDOWN {t:0.0}s | GravityHud.cs:Refresh() | 左下HUD | 完成 |
| hud.repulse_ready | 斥力就绪  左手 X | REPULSE READY  LEFT X | GravityHud.cs:Refresh() | 左下HUD | 完成 |

---

## 手部状态栏（GravityHud.cs）

| key | 原中文 | 英文 | 来源文件:行 | 状态 |
|---|---|---|---|---|
| hand.standby | {label}  待机 | {label}  STANDBY | GravityHud.cs:HandReadout() | 完成 |
| hand.holding | {label}  抓取 · {name} | {label}  HOLDING · {name} | GravityHud.cs:HandReadout() | 完成 |
| hand.locked | {label}  锁定 · {name} | {label}  LOCKED · {name} | GravityHud.cs:HandReadout() | 完成 |
| hand.aim | {label}  待机 · 对准目标 | {label}  STANDBY · AIM AT TARGET | GravityHud.cs:HandReadout() | 完成 |

---

## 状态提示（GravityHud.cs / GravityGameManager.cs / GravityHand.cs）

| key | 原中文 | 英文 | 来源文件:行 | 状态 |
|---|---|---|---|---|
| status.holding | 已抓取  {name} · 松开投掷 | HOLDING  {name} · RELEASE TO THROW | GravityHand.cs:120 | 完成 |
| status.released | 已释放  {name} | RELEASED  {name} | GravityHand.cs:133 | 完成 |
| status.locked | 目标锁定  {name} · 扳机抓取 | LOCKED  {name} · TRIGGER TO GRAB | GravityHand.cs:318 | 完成 |
| status.too_heavy | {name} 质量过大：引力锁定 {a}/{b} | {name} TOO HEAVY: GRAVITY LOCK {a}/{b} | GravityTarget.cs:340 | 完成 |
| status.impact | 目标摧毁 | TARGET DOWN | GravityGameManager.cs:OnTargetDestroyed | 完成 |
| status.precision | 精准防御  +60 | PRECISION DEFENSE  +60 | GravityGameManager.cs:239 | 完成 |
| status.shield_hit | 护盾受损  -{amount} | SHIELD DAMAGE  -{amount} | GravityGameManager.cs:251 | 完成 |
| status.repulse_cooldown_rt | 斥力冷却 {t:0.0}s | REPULSE COOLDOWN {t:0.0}s | GravityGameManager.cs:286 | 完成 |
| status.repulse_hit | 斥力爆发  击退 {n} 个目标 | REPULSE BURST  PUSHED {n} TARGETS | GravityGameManager.cs:315 | 完成 |
| status.repulse_clear | 斥力爆发  空域净空 | REPULSE BURST  CLEAR | GravityGameManager.cs:315 | 完成 |
| status.bipolar_need_lock | 需要双手各锁定一个目标 | LOCK TWO TARGETS  ONE PER HAND | GravityGameManager.cs:325 | 完成 |
| status.bipolar_done | 双极对撞  完美分解 | BIPOLAR COLLISION  ANNIHILATED | GravityGameManager.cs:341 | 完成 |
| status.singularity_low | 奇点能量不足 {a}/{b} | SINGULARITY INSUFFICIENT  {a}/{b} | GravityGameManager.cs:349 | 完成 |
| status.singularity_no_enemy | 当前没有可收束的敌人 | NO TARGETS IN SINGULARITY RANGE | GravityGameManager.cs:354 | 完成 |
| status.recenter | 战场起点与追踪原点已重新校准 | COMBAT ORIGIN RECALIBRATED | GravityGameManager.cs:371 | 完成 |
| status.ult_phase1 | 地爆天星：引力锚定 | SINGULARITY: GRAVITY LOCK | GravityGameManager.cs:383 | 完成 |
| status.ult_phase2 | 地爆天星：群体环绕 | SINGULARITY: ORBITAL PULL | GravityGameManager.cs:393 | 完成 |
| status.ult_phase3 | 地爆天星：奇点压缩 | SINGULARITY: COMPRESSION | GravityGameManager.cs:398 | 完成 |
| status.ult_phase4 | 地爆天星：湮灭爆发 | SINGULARITY: ANNIHILATION BURST | GravityGameManager.cs:403 | 完成 |
| status.ult_done | 奇点坍缩  大道净空 | SINGULARITY COLLAPSE  AREA CLEAR | GravityGameManager.cs:437 | 完成 |
| status.wave1_hint | 对准敌人 · 扳机抓取 · 松开投掷 | AIM · TRIGGER TO GRAB · RELEASE TO THROW | GravityGameManager.cs:471 | 完成 |
| status.wave_n_arrive | 第 {wave:00} 波侵入者抵达 · 锁定目标 | WAVE {wave:00} INCOMING · LOCK TARGETS | GravityGameManager.cs:472 | 完成 |
| status.supply_drop | 防卫补给已投放  吸取即可恢复 | SUPPLY DROP  GRAB TO RESTORE SHIELD | GravityGameManager.cs:492 | 完成 |
| status.wave_clear | 第 {wave} 波守护完成  总分 {score} | WAVE {wave} CLEAR  SCORE {score} | GravityGameManager.cs:494 | 完成 |

---

## 结算界面（GravityHud.cs ShowRunSummary）

| key | 原中文 | 英文 | 状态 |
|---|---|---|---|
| result.title | 本轮结算 | RUN COMPLETE | 完成 |
| result.total | 总分  {score:00000} | TOTAL SCORE  {score:00000} | 完成 |
| result.waves | 通过关数  {waves:00} | WAVES CLEARED  {waves:00} | 完成 |
| result.enemies | 击破目标  {count:00} | TARGETS DOWN  {count:00} | 完成 |
| result.base_score | 基础战果  +{n} | COMBAT SCORE  +{n} | 完成 |
| result.wave_score | 波次守护  +{n} | WAVE BONUS  +{n} | 完成 |
| result.bonus_title | 额外奖励 | BONUS RECORD | 完成 |
| result.precision | 精准防御 ×{n}   +{pts} | PRECISION ×{n}   +{pts} | 完成 |
| result.reflect | 反弹进攻 ×{n}   +{pts} | REFLECTION ×{n}   +{pts} | 完成 |
| result.repulse | 斥力击破 ×{n}   +{pts} | REPULSION ×{n}   +{pts} | 完成 |
| result.collision | 双极对撞 ×{n}   +{pts} | COLLISION ×{n}   +{pts} | 完成 |
| result.chain | 环境连锁 ×{n}   +{pts} | CHAIN REACTION ×{n}   +{pts} | 完成 |
| result.singularity | 奇点湮灭 ×{n}   +{pts} | SINGULARITY ×{n}   +{pts} | 完成 |
| result.redeploy | 右手 A 重新部署 | RIGHT A: REDEPLOY | 完成 |

---

## 目标 displayName（GravityTarget.cs / GravityProjectBuilder.cs / GravityExistingSceneUpgrader.cs）

| key | 原中文 | 英文 | 来源 | 状态 |
|---|---|---|---|---|
| target.default | 可吸取物 | Pullable Object | GravityTarget.cs:21 | 完成 |
| target.hydrant | 蒸汽消防栓 | Steam Fire Hydrant | GravityTarget.cs:87 | 完成 |
| target.mailbox | 机械邮筒 | Mechanical Mailbox | GravityTarget.cs:89 | 完成 |
| target.barrier | 街道隔离柱 | Street Barrier | GravityTarget.cs:91 | 完成 |
| target.pump | 手压水泵 | Hand Pump | GravityTarget.cs:93 | 完成 |
| target.bench | 街边金属长椅 | Metal Bench | GravityTarget.cs:95 | 完成 |
| target.crate | 金属货箱 | Metal Crate | GravityTarget.cs:97 | 完成 |
| target.column | 齿轮装饰街道筒 | Gear Street Column | GravityTarget.cs:99 | 完成 |
| target.scout | 巡逻机 | Scout | GravityProjectBuilder.cs | 完成 |
| target.engineer | 工程机 | Engineer | GravityProjectBuilder.cs | 完成 |
| target.guard | 盾卫核心 | Shield Core | GravityProjectBuilder.cs | 完成 |
| target.projectile | 危险能量弹 | Danger Projectile | GravityProjectBuilder.cs | 完成 |
| target.health_pack | 防卫补给 | Shield Supply | GravityProjectBuilder.cs | 完成 |
| target.missile | 危险导弹 {n} | Danger Missile {n} | GravityExistingSceneUpgrader.cs | 完成 |
| target.health_pack_27 | 防卫补给 27 | Shield Supply 27 | GravityExistingSceneUpgrader.cs | 完成 |

---

## ProductName / 包名

| 字段 | 原值 | 英文值 | 状态 |
|---|---|---|---|
| productName | 重力战线 | GRAVITY FRONTLINE | 完成 |
| Android bundle ID | com.gravitywarden.repulsion | com.gravitywarden.repulsion.en | 完成 |
| metroPackageName | 引力斥力 | GravityFrontline | 完成 |

---

## 编辑器帮助文字（不影响玩家，但规范要求更改）

| 原中文 | 英文 | 来源 | 状态 |
|---|---|---|---|
| PICO 扳机/握把吸取 · 合拢对撞 · 左 X 斥力 · 右 A 奇点 · 左 Y 归位  ｜  桌面 Q/E · Space · F · R · C | PICO TRIGGER/GRIP GRAB · CLOSE HANDS COLLIDE · LEFT X REPULSE · RIGHT A SINGULARITY · LEFT Y RECENTER ｜ DESKTOP Q/E · SPACE · F · R · C | GravityProjectBuilder.cs:466 | 完成 |
| 扳机 / 握把 引力抓取 · 双手合拢 对撞 · 左 X 斥力 · 右 A 奇点 · 左 Y 重置视角 | TRIGGER / GRIP GRAVITY GRAB · CLOSE HANDS COLLIDE · LEFT X REPULSE · RIGHT A SINGULARITY · LEFT Y RECENTER | GravityExistingSceneUpgrader.cs:1212 | 完成 |

---

## CJK 扫描排除说明

以下中文字符存在于代码中但**不属于玩家可见文字**，按规范可保留：

1. **内部对象名**（`new GameObject("VFX_引力束_挣扎环")` 等约 116 处）：用于 Editor/Runtime 中创建临时 GameObject，不渲染到任何画面 UI。
2. **代码注释**（约 1 处）：`GravityStatusTypes.cs` 注释提及"中文"一词，纯文档说明。
3. **Tripo JSON 任务元数据**（10 个 .json 文件）：记录原始素材路径（`/引力 斥力/`），属于存档键/路径，不渲染到画面。
4. **`VISUAL_可吸道具_`、`VISUAL_原创机械重力护腕_`**：Smoke test 及 Editor 脚本中作为 `StartsWith` 前缀用于结构查找，不显示给玩家。
5. **`Prop_EnergyCanister_{n}_连锁爆炸`**：编辑器生成的 Hierarchy 节点名，不出现在 HUD。
