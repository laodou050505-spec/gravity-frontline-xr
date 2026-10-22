# 地面稳定与头盔碎屏 — 2026-09-09

## 用户要求
修复可吸取场景模型抓起再放地上持续乱蹦/穿地，新增头盔玻璃受损效果；参考提供的裂纹图片，随失血程度加重。需要图片用Tripo Nano Banana。

## 地面修复
根因：原导入模型带旋转，部分BoxCollider把世界AABB尺寸直接作为局部尺寸，物理底面与模型错位；旧FixedUpdate又每步强推回平面，反复造成弹跳。44个场景道具中25个碰撞盒实际修正，原7个初始trigger保留原场景布局，交互后恢复实体。模型几何本地包围盒校准，不用粒子/射线作为物理边界。

碰撞材质0弹性，自然摩擦/角阻尼/休眠；持握和释放同步刚体位置旋转。选择ContinuousSpeculative覆盖线性及角运动。取消普通静置接触时逐帧抬升；仅抓握/释放初次处理地面和实际台阶重叠，完全跌出厚地基才救援。动态物体仍可移动、再次抓取和投掷，无冻结约束。永久200×200×2地基顶部与可见Plane同为y=.1，无重复Plane碰撞。

## 碎屏
Tripo Nano Banana (`banana`) 成功任务32469301-8804-4465-819b-93cdd5524d60，消耗5 credits。来源/原图/参考和处理过程见 Work/VisorDamage0909/asset-source.md。使用用户参考裂纹裁切，去黑底后5.2%像素覆盖率；中央瞄准区域保持透明。

GravityVisorDamage在相机HUD层显示：失血越多，边缘裂纹越清楚且向内延伸；严重缺血增加边缘细色线和浅红边缘；真实受击有短暂红边脉冲；回血减轻，满血无裂纹，结算隐藏。没有新增相机摇晃或快速闪烁，读数/准星绘于裂纹之上。由真实DamagePlayer/ConsumeHealthPack调用HUD刷新，不依赖伪造数值。

## 验证
- 官方Unity6000.5.5f1桌面/Android构建通过，loader/ARM64/IL2CPP/Activity/Multiview/InputSystemOnly验证通过。
- 地面编辑器审计44个模型与碰撞盒逐一匹配；物理回归7阶段、264次释放（日志regrabs=220不包含末44次压地抓取）、原街道/高台/台阶下落/倒置侧躺/40m/s高速落地。每阶段44个全部自然睡眠，末2秒线速度/角速度0，无静置托举、无地基救援，固定建筑未动。首轮ContinuousDynamic倾倒时4cm穿地失败，改角运动CCD后原阈值全部通过。
- 碎屏真实100→75→45→15→药包恢复85→致死结算：桌面及PICO布局各15项通过。
- 原索敌回归31项通过。
- emulator-5554安装Success，base.apk SHA256与本地一致。PID17213正常进入战斗，5秒稳定性通过；19:22:25–49真实敌弹伤害把shield从92递减到0，日志记录完整GRAVITY_VISOR_DAMAGE_HIT，结算画面可见。CUA窗口本轮有延迟，未保存实际碎屏瞬间图片；不要把桌面截图说成模拟器截图。
- 未在真实头显上验证；没有宣称全场景所有组合绝对无穿模。

## 产物
- APK：Builds/PICO/重力战线-PICO.apk；SHA256 `4773592392ab1e89befc9e54c88565756b54730518f8cfa5ab5b9c782fee203f`。
- 源工程和ASCII构建镜像的脚本/scene/shader/纹理逐一hash一致；只覆盖原5554，未创建或使用5556。
- 桌面：Builds/macOS/GravityModularReview.app。
- 桌面证据：Evidence/VisorDamage0909（01满血、02血量75、03血量45、04血量15、05回血85、06结算）及Evidence/VisorDamage0909Pico（桌面PICO布局代理，不是头显画面）。
- Logs/GroundGlass0909Mac2.log、GroundGlass0909Android.log、GroundGlass0909Physics2.log、GroundGlass0909Damage.log、GroundGlass0909PicoDamage.log、GroundGlass0909AimRegression.log、GroundGlass0909Emulator.log。
- 测试命令参数：-gravityGroundSmoke；-gravityDamageEvidence -gravityDamageOutput <目录>；-gravityAimEvidence -gravityAimOutput <目录>。

封面.74距离与.20羽化不变。Android只在英文镜像构建；不要reset现有大量未提交改动。
