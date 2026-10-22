# 低血量暗化、建筑阻挡与箱子落地 — 2026-09-10

用户要求失血画面逐渐变暗、中央少量玻璃裂纹、所有模型不穿两侧房屋；追加截图反馈箱子半截埋地。

## 实现
- GravityVisorDamage + shader：近黑冷色(.002,.003,.006)，75/45/15血量中央alpha约.092/.230/.381，回血平滑恢复；增加两组稀疏中央细纹，准星核心净区。损伤层按左右眼实际视锥扩展6%边余，HUD不扩大。
- 50个导入固定模型原本无碰撞，补33房屋/钟楼和17街边亭柱的独立本地形状。14个原始嵌入建筑的道具挪至最近空位，独立真实collider两两审计通过。
- GravityArchitectureCollision对持握、大招、机器人可视轮廓、炮弹、补给包进行路径扫掠，墙前停止/沿墙移动；贴墙转动每次≤3度。炮弹撞建筑消散。
- 13块两类原有路面改用同源MeshCollider并禁用同对象旧Box，抬升2个原先稍埋路面的道具（非箱子）。12箱子初始底部原本.228/.230，未虚称初始被抬高；新路面精确碰撞+既有本地箱体接触保证重放。箱子原姿态及0/90/180/270四姿态在原街道路面全部完整、自然静止。
- 地面普通休眠不脚本托举，原地基与封面.74/.20维持。

## 关键排错
- 首轮禁用查询Box导致ComputePenetration假阴性，后改为场外启用trigger探针，并用真实道具collider×建筑独立审计；最终14个原始墙内道具已移出。不要依赖早期0移动结论。
- 相邻建筑角缝最短分离会反复推，增加一次联合找附近同高空位。
- 首次建筑测试错误使用旋转房屋AABB最内角，改为真实penetration+跨墙侧检查，未放宽阈值。原始通道测试遇真实灯具，改为6m高建筑间实际开放通道。

## 验证
- 官方Unity6000.5.5f1 macOS与Android成功；PXR loader、Multiview、ARM64、IL2CPP、Activity、InputSystemOnly通过。
- 最终地面264释放/44模型/7阶段全部通过，44自然睡眠，0尾段速度、0静置推移、0地基救援。日志WallVisor0910GroundSmoke2.log。
- 建筑44真实模型抓取跨楼、10次持续推墙、沿墙移动、旋转、40m/s投掷通过；真实楼间空隙通过，固定建筑未动。maxDynamicPenetration=.0170m，在原.035容差内。日志WallVisor0910ArchitectureSmoke3.log。
- ActorSmoke通过3机器人、3屋内出生、24补给转向、40m/s补给投墙、2类炮弹普通80m/s/反射16m/s、真实大招；使用真实地基空白区域的隔离立面。日志WallVisor0910ActorSmoke2.log。
- 箱子12初始近景，右2箱4姿态8次释放全部通过；Evidence/GroundPlacement0910Rotations，日志WallVisor0910CratesRotations.log。
- 真实伤害/回血流程与实际shader像素、视口覆盖29/29；桌面Evidence/WallVisor0910Final，PICO布局代理Evidence/WallVisor0910Pico。中心亮纹覆盖约1.6%。原索敌31/31。
- 原emulator-5554安装Success，base.apk hash与交付一致。PID9393真实进入战斗，11:49–11:50 shield92→0伤害日志；CUA观察正常结算。窗口截图延迟，未保存模拟器碎屏瞬间，视觉证据为桌面/PICO布局代理。未做真实头显验证。

## 交付
APK Builds/PICO/重力战线-PICO.apk，SHA256 fb4f21a1326356548b10ceef0385dc3044a1d371870bc09cd7fa5f9d8bf4e47a。
桌面 Builds/macOS/GravityModularReview.app。生产脚本与shader相同，APK是Mac5对应生产版；Mac6只新增opt-in箱子四姿态测试与重复Prepare序列化微量浮点差异，最终Assets已同步英文镜像，未为纯测试增补重做APK。
构建Logs/WallVisor0910Mac5.log、Mac6.log、Android.log；模拟器WallVisor0910Emulator.log。
只用原5554，勿创建5556。工程已有大量未提交变更，勿reset。
