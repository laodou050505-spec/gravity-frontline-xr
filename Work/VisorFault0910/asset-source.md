# 中央追加玻璃裂纹素材

- Provider: Tripo
- Model: Nano Banana (`banana`)
- Operation: image-to-image
- Task ID: `363387c1-b5c5-465b-adb0-c64ef9ea4c46`
- Final status: `success`
- Credits consumed: `5`
- Source URL: https://tripo-data.rg1.data.tripo3d.com/tcli_c6fae067c9334bd9af3d07029c0e7b00/20260910/363387c1-b5c5-465b-adb0-c64ef9ea4c46/generated_image.png
- Saved generation: `generated/01_generated_image_url.png` (1344 × 768 RGB)
- Task record: `generated/tripo_task_363387c1-b5c5-465b-adb0-c64ef9ea4c46.json`
- Reference: the original Nano Banana glass image `../VisorDamage0909/generated/01_generated_image_url.png`; task `32469301-8804-4465-819b-93cdd5524d60`.
- Game asset: `Assets/GravityWarden/Resources/GravityUI/VisorCentralCracks.png` (2048 × 1280 RGBA, separate overlay)
- Preview: `CentralCracks-preview.png`

先调用 ange-embed `list_v2_collections`，对 `h5_respack_texture_v1` 调用 `facet_v2_tags`，再搜索 sparse cracked glass / broken visor transparent screen cracks texture。该库标签只有地面材质，搜索返回雪、石头、混凝土贴图，无适用玻璃裂纹；`asset_image`、`dev_asset_image` 为零记录。未使用返回的地面素材。随后按用户明确指定使用 Nano Banana。

新生成图保持旧纹理细玻璃裂纹质感，但把裂纹放在四角；后处理裁取左下、右上两枚生成裂纹，分别移到画布 (0.40, 0.59)、(0.61, 0.36)，每枚宽约画布 22%。仅做裁切、位置缩放、去暗底、去噪、冰白着色、边缘羽化；没有程序绘制裂纹线条。中央直径 7% 的瞄准区完全透明。alpha > 0.1 的亮线覆盖画布 0.478%，适合叠加在原有边缘碎屏上。原 `VisorGlassCracks.png` 完全保留，不覆盖。

可重复执行 `process-central-cracks.py` 获得同一处理结果；不会调用 API 或产生费用。`processing-stats.json` 记录坐标与覆盖率。

API 参数备注：banana 当前不接受技能示例的 `2K` 尺寸或 `16:10` 比例。前两次校验返回 HTTP 400，未创建任务；最终使用 `1344x768` 与 `16:9` 成功，仅创建上面的一个任务。记录不含密钥或带查询参数的临时签名 URL。
