from pathlib import Path
import json, uuid
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
WORK=Path(__file__).resolve().parent
SRC=WORK/'generated/01_generated_image_url.png'
OUT=ROOT/'Assets/GravityWarden/Resources/GravityUI/VisorCentralCracks.png'
W,H=2048,1280
source=np.asarray(Image.open(SRC).convert('RGB'),dtype=np.float32)
luminance=source.max(axis=2)
# Remove the generated black-field grain before preserving the glass highlights.
alpha=np.power(np.clip((luminance-35.0)/145.0,0,1),0.78)
rgba=np.empty((*alpha.shape,4),dtype=np.uint8)
rgba[:,:,:3]=(216,240,250)
rgba[:,:,3]=np.rint(alpha*255).astype(np.uint8)
source_image=Image.fromarray(rgba)
canvas=Image.new('RGBA',(W,H),(216,240,250,0))
# Recompose two generated fragments as an independent interior overlay.
# Crops, recoloring and alpha cleanup only; no synthetic fracture strokes.
placements=[
 {'crop':(0,448,430,768),'impact':(226,630),'destination':(.40,.59),'width':.22},
 {'crop':(930,0,1344,320),'impact':(1158,165),'destination':(.61,.36),'width':.22}
]
for piece in placements:
 box=piece['crop'];fragment=source_image.crop(box)
 a=np.asarray(fragment).copy();fh,fw=a.shape[:2]
 yy,xx=np.mgrid[:fh,:fw]
 edge=np.minimum.reduce((xx,yy,fw-1-xx,fh-1-yy)).astype(np.float32)
 fade=np.clip(edge/30.0,0,1);fade=fade*fade*(3-2*fade)
 a[:,:,3]=np.rint(a[:,:,3]*fade).astype(np.uint8)
 scale=piece['width']*W/fw
 size=(round(fw*scale),round(fh*scale))
 fragment=Image.fromarray(a).resize(size,Image.Resampling.LANCZOS)
 local=((piece['impact'][0]-box[0])*scale,(piece['impact'][1]-box[1])*scale)
 location=(round(piece['destination'][0]*W-local[0]),round(piece['destination'][1]*H-local[1]))
 canvas.alpha_composite(fragment,location)
a=np.asarray(canvas).copy()
yy,xx=np.mgrid[:H,:W]
rad=np.sqrt((xx-W*.5)**2+(yy-H*.5)**2)/W
# Guaranteed clear 7% diameter reticle zone with a subtle feather outside.
center=np.clip((rad-.035)/.015,0,1);center=center*center*(3-2*center)
a[:,:,3]=np.rint(a[:,:,3]*center).astype(np.uint8)
canvas=Image.fromarray(a)
OUT.parent.mkdir(parents=True,exist_ok=True)
canvas.save(OUT,optimize=True)
meta=OUT.with_suffix('.png.meta')
if not meta.exists():
 baseline=(OUT.parent/'VisorGlassCracks.png.meta').read_text()
 import re
 meta.write_text(re.sub(r'guid: [0-9a-f]{32}',f'guid: {uuid.uuid4().hex}',baseline,count=1))
coverage=float((a[:,:,3]>25).mean()*100)
(WORK/'processing-stats.json').write_text(json.dumps({'source':str(SRC),'output':str(OUT),'size':[W,H],'alpha_over_0_1_coverage_percent':coverage,'center_clear_diameter_percent':7,'placements':placements},ensure_ascii=False,indent=2)+'\n')
preview=Image.new('RGBA',(W,H),(17,24,29,255));preview.alpha_composite(canvas)
preview.convert('RGB').resize((1280,800),Image.Resampling.LANCZOS).save(WORK/'CentralCracks-preview.png')
print(json.dumps({'output':str(OUT),'size':[W,H],'alpha_over_0_1_coverage_percent':coverage,'max_alpha_in_clear_center':int(a[:,:,3][rad<.035].max()),'placements':placements},ensure_ascii=False,indent=2))
