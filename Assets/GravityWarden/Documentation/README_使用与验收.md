# 《引力 斥力》Unity / PICO XR 使用与验收

## 打开项目

1. 在 Unity Hub 中选择“添加磁盘中的项目”。
2. 选择项目根目录 `引力 斥力`，使用官方 Unity `6000.5.5f1` 打开。
3. 打开 `Assets/GravityWarden/Scenes/GravityBoulevard.unity`。
4. Scene 已摆好欧式钟楼大道、20 栋建筑、18 盏街灯、44 个可吸取环境物、5 个出生点与 3 个机器人预览。

## PICO 操作

- 左右扳机或握把：射线吸取；靠近目标时直接抓取优先。
- 双手各持一个目标并把两个物体合拢：自动触发双极对撞。
- 左手 X：斥力，击退机器人并反射危险导弹，带 3.8 秒冷却。
- 右手 A：释放奇点大招；首次需求 100，之后每次增加 120，最高 600。
- 左手 Y：重新校准 PICO 追踪原点。
- 失败后右手 A：重新开始。

射线会对当前候选目标改色，手部核心和手柄震动会区分待机、锁定、手持、对撞与受击反馈。吸取优先级为机器人、导弹、补给、街道物；最后少量敌人有选取兜底。

## 桌面完整试玩

- `Q / E`：左手 / 右手吸取或释放。
- `Space`：双手目标对撞。
- `F`：斥力。
- `R`：奇点大招。
- 按住鼠标右键：转动视角。
- `WASD`：小范围测试移动。
- `C`：回到初始位置。
- 失败后 `Enter`：重新开始。

## 核心规则

- 无限波次，得分等于已守住的波数。
- 每个普通手持物只能抵挡一次攻击；导弹不能作为盾牌，但可被吸取、投掷或斥力反射。
- 场景中已有的可吸道具被高速投掷或摧毁会造成环境连锁伤害。
- 每 3 波且护盾不足时投放恢复 35 点的血包。
- 血包 27 使用图片十字正面，运行时按模型横向轴校准为 `Y=90°`，始终面向摄像机。
- 血包 27 带持续绿色漂浮粒子，拾取时会额外爆发绿色粒子；护盾实际扣除后播放受伤音效，吸取物体播放明显的吸力音效。
- 可吸环境物只来自场景已有的 12/13/16/18/21/25/33 号模型；无碰撞的 12 号实例只补交互 BoxCollider，初始穿插实例使用安全触发器，保持 44 个真实模型可吸。
- 场景战斗开始后播放低音量机械战斗 BGM，音乐来源与许可记录见 `THIRD_PARTY_AUDIO.md`。
- 敌人被引力抓取后只缩小视觉根节点约 16%，不缩放碰撞体或玩法根节点。
- 大招击杀不返还能量，且每次释放后需求递增，避免后期每波无限刷新。
- 玩家相机不做强制位移；爽感主要由敌人、道具、冲击波、粒子、射线和触觉反馈产生，以降低 XR 晕动风险。

## 模型替换

只编辑 `REPLACE_VISUAL_*` 子节点。不要删除或移动其父节点上的 Collider、Rigidbody 和玩法脚本。所有实际坐标、尺寸与概念图对应关系见 `ASSET_REPLACEMENT_TABLE.md`。

## PICO 配置

- 官方 PICO Unity Integration SDK `6.0.0`。
- `com.bytedance.pico.xr` 与官方 `PXR_Loader`。
- Floor-level `XROrigin`、双 6DoF 控制器、触觉反馈。
- Android ARM64、IL2CPP、最低 API 29、`UnityPlayerActivity`。
- Multiview、Input System-only、OpenGLES3 + Vulkan、Linear Color Space。

## 已执行验收

- Unity 脚本批处理编译：通过。
- Scene/PICO 配置验证：通过，检测到 44 个可吸取环境物和 2 个 `GravityHand`。
- Scene 相机预览：通过，输出 `Preview/GravityBoulevard_ScenePreview.png`。
- macOS 桌面自动玩法：通过。首波双手对撞清场、环境连锁生效、第二波大招清场、需求从 100 增至 220、第三波继续生成，最终记录 `wave=3 score=2`。
- macOS 构建：`Builds/macOS/引力 斥力.app`。
- PICO APK 构建：`Builds/PICO/引力 斥力-PICO.apk`。
- APK 检查：包名 `com.gravitywarden.repulsion`、版本 `0.1.0`、最低 API 29、仅 `arm64-v8a`。

## Android 中文路径说明

Unity 6 的 Android Tools 会拒绝包含非 ASCII 字符的项目路径，而项目名必须保留“引力 斥力”。APK 因此由 `work/gravity_repulsion_pico_build` 中的同源英文构建镜像生成，再回填到主项目 `Builds/PICO`。日常编辑继续使用中文主项目；重新出 Android 包时同步 `Assets`、`Packages`、`ProjectSettings` 到英文镜像后构建。

## 尚需真机验证

当前没有连接真实 PICO 头显，因此尚未声称验证：实际手柄追踪稳定性、震动强度、瞄准舒适度、双手合拢距离、90 Hz 性能与长时间眩晕感。APK 与配置已通过离线构建检查，最终手感应在真机上微调。
