# 《引力 斥力》资产替换与 Scene 摆位表

## 替换原则

- 只替换名为 `REPLACE_VISUAL_*` 的子节点内容。
- 保留父节点上的 `Collider`、`Rigidbody`、`GravityTarget`、`RobotEnemy`、`ChainReactionProp` 与出生点引用。
- 导入模型先设为 `1 Unity unit = 1 m`，应用变换后再微调视觉子节点，不要缩放玩法根节点。
- 机器人正面按当前 Prefab 的 `-Z` 方向校准；分件模型全部放在 `REPLACE_VISUAL_Robot_导入分件模型放这里` 下。
- 概念图根目录：`../../../../tripo-concepts/`（项目外部同级交付目录）。

## 建筑与街道

| 资产 | Scene 节点 / 替换节点 | 数量与实际摆位 | 建议成品尺寸 | 碰撞要求 | Tripo 概念图 |
| --- | --- | --- | --- | --- | --- |
| 钟楼 | `REPLACE_CLOCKTOWER_钟楼/REPLACE_VISUAL_Clocktower_导入模型放这里` | 1 个；世界坐标 `(0, 0, -86)` | 约 `11 x 28 x 9 m` | 保留静态根节点；模型可用简化 Box Collider | `tripo-concepts/clocktower/01_generated_image_url.png` |
| 欧式建筑 | `ARCHITECTURE_两侧欧式建筑_逐栋可替换/Building_L_*/REPLACE_VISUAL_Building_导入模型放这里` 与 `Building_R_*` | 左右各 10 栋；Z 为 `6, -4.5, -15, -25.5, -36, -46.5, -57, -67.5, -78, -88.5`；左 X 为 `-13.4/-13.75` 交替，右 X 为 `13.4/13.75` 交替 | 单栋约 `8.6 x 10-12.4 x 6.2 m` | 保留父节点；每栋使用 1-3 个简化静态 Collider | `tripo-concepts/building/01_generated_image_url.png` |
| 街灯 | `LIGHTING_街灯与月光/REPLACE_LAMP_L_*/REPLACE_VISUAL_Lamp_导入模型放这里` 与右侧对应节点 | 左右各 9 盏；X=`-9.1/+9.1`，Z=`5, -6, -17, -28, -39, -50, -61, -72, -83` | 高约 `4.3 m` | 视觉一般不参与抓取；保留暖色 Point Light | `tripo-concepts/street-lamp/01_generated_image_url.png` |
| 道路 | `STREET_道路与人行道/Road_道路_18x120m` | 中心约 `(0, -0.15, -42)` | `18 x 0.3 x 120 m` | 保留静态 Box Collider | 无需独立模型，建议铺装 PBR 材质 |
| 人行道 | `STREET_道路与人行道/Sidewalk_Left`、`Sidewalk_Right` | X=`-11.8/+11.8`，Z=`-42` | 单侧 `5.5 x 0.25 x 120 m` | 保留静态 Box Collider | 可与建筑统一石材材质 |

## 可吸取街道物

所有下列根节点都可吸取、投掷并抵挡一次攻击。模型只放入对应 `REPLACE_VISUAL_*` 子节点。

| 资产 | Scene 节点 | 实际坐标 `(X, Y, Z)` | 建议尺寸 / 碰撞 | Tripo 概念图 |
| --- | --- | --- | --- | --- |
| 箱子 | `Prop_Crate_01/04/07/10/13/16_可替换` | `(-7.4,.6,4)`, `(7.75,.6,-8.9)`, `(-7.4,.6,-21.8)`, `(7.75,.6,-34.7)`, `(-7.4,.6,-47.6)`, `(7.75,.6,-60.5)` | 约 `.9 m` 立方体；保留根 Box Collider | `tripo-concepts/crate/01_generated_image_url.png` |
| 长椅 | `Prop_Bench_02/05/08/11/14/17_可替换` | `(7.75,.6,-.3)`, `(-7.4,.6,-13.2)`, `(7.75,.6,-26.1)`, `(-7.4,.6,-39)`, `(7.75,.6,-51.9)`, `(-7.4,.6,-64.8)` | 约 `1.9 x 1.05 x .7 m`；保留根 Box Collider | `tripo-concepts/bench/01_generated_image_url.png` |
| 垃圾桶 | `Prop_Bin_03/06/09/12/15/18_可替换` | `(-7.4,.6,-4.6)`, `(7.75,.6,-17.5)`, `(-7.4,.6,-30.4)`, `(7.75,.6,-43.3)`, `(-7.4,.6,-56.2)`, `(7.75,.6,-69.1)` | 约 `.75 x 1.05 x .75 m`；保留根 Box Collider | `tripo-concepts/bin/01_generated_image_url.png` |
| 连锁能源罐 | `Prop_EnergyCanister_01-06_连锁爆炸/REPLACE_VISUAL_EnergyCanister_导入模型放这里` | `(-6.5,.75,-12)`, `(6.5,.75,-22.5)`, `(-6.5,.75,-33)`, `(6.5,.75,-43.5)`, `(-6.5,.75,-54)`, `(6.5,.75,-64.5)` | 约 `.8 x 1.35 x .8 m`；保留根 Collider 与 `ChainReactionProp` | `tripo-concepts/energy-canister/01_generated_image_url.png` |

## 敌人、危险与补给 Prefab

| 资产 | Prefab / 替换节点 | Scene 预览摆位或运行时来源 | 建议尺寸 / 碰撞 | Tripo 概念图 |
| --- | --- | --- | --- | --- |
| 巡逻机 | `Prefabs/Robot_Scout.prefab/REPLACE_VISUAL_Robot_导入分件模型放这里` | Scene 预览 `(-4.6,0,1.8)`；运行时由 5 个出生点生成 | 高约 `2.4 m`；保留根 Capsule Collider | `tripo-concepts/robot-scout/01_generated_image_url.png` |
| 盾卫核心 | `Prefabs/Robot_Guard.prefab/REPLACE_VISUAL_Robot_导入分件模型放这里` | Scene 预览 `(0,0,-.3)`；运行时按波次混入 | 高约 `2.6 m`；保留根 Capsule Collider | `tripo-concepts/robot-guard/01_generated_image_url.png` |
| 工程机 | `Prefabs/Robot_Engineer.prefab/REPLACE_VISUAL_Robot_导入分件模型放这里` | Scene 预览 `(4.6,0,1.8)`；运行时按波次混入 | 高约 `2.4 m`；保留根 Capsule Collider | `tripo-concepts/robot-engineer/01_generated_image_url.png` |
| 危险导弹 | `Prefabs/EnemyProjectile.prefab/REPLACE_VISUAL_Projectile` | 机器人运行时发射 | 直径约 `.28 m`；保留 Sphere Trigger；不可作为一次性盾牌 | `tripo-concepts/projectile/01_generated_image_url.png` |
| 防卫补给 | `Prefabs/HealthPack.prefab/REPLACE_VISUAL_HealthPack` | 每 3 波且护盾低于 92 时投到 `(0,1.15,5.4)` | 约 `.7 m`；保留根 Box Collider | `tripo-concepts/health-pack/01_generated_image_url.png` |

## XR 控制器与玩法锚点

| 对象 | Scene 节点 | 实际摆位 / 用途 | 替换要求 | Tripo 概念图 |
| --- | --- | --- | --- | --- |
| 左手引力外壳 | `XR_RIG_PICO与桌面回退/.../LeftHand_左手控制器/REPLACE_VISUAL_PICO_Controller_可替换` | 相对 Camera Offset `(-.46,1.38,-.68)`；运行时跟随左手 6DoF | 只换外观，保留 `GravityHand`、射线和手持锚点 | `tripo-concepts/controller-gauntlet/01_generated_image_url.png` |
| 右手引力外壳 | `XR_RIG_PICO与桌面回退/.../RightHand_右手控制器/REPLACE_VISUAL_PICO_Controller_可替换` | 相对 Camera Offset `(.46,1.38,-.68)`；运行时跟随右手 6DoF | 同上 | 同上 |
| 玩家原点 | `XR_RIG_PICO与桌面回退` | 世界坐标 `(0,0,10.5)`，Floor Tracking | 不替换、不缩放 | - |
| 敌人出生点 | `SPAWN_POINTS_敌人出生点/Spawn_01-05_可调整` | `(-5.1,0,-5)`, `(-2.55,0,-4.25)`, `(0,0,-3.5)`, `(2.55,0,-4.25)`, `(5.1,0,-5)` | 可在 Scene 中移动，保留 Transform 引用 | - |

## 素材来源审计

- 按项目规则先搜索了 `ange-embed` 的 `h5_respack_v1` 与 `ange-asset-v1`。
- 候选模型提供了封面、模型 URL、类型与包围盒，但返回记录没有可核验的作者与许可证字段，因此本版没有直接导入。
- 当前视觉资产全部是本地 Unity 占位几何；上表概念图由 Tripo `banana` 模型生成，供后续在 Tripo 中制作自有模型。
