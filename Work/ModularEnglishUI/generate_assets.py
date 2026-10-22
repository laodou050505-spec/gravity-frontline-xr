import json, subprocess, time
from pathlib import Path
base=Path(__file__).resolve().parent
out=base.parents[1]/'Assets/GravityWarden/Art/Tripo/ModularEnglish'
for job in json.loads((base/'generation-plan.json').read_text()):
    folder=out/job['name']
    if list(folder.glob('tripo_task_*.json')):
        print('Already saved',job['name'],flush=True);continue
    cmd=['python3','/Users/ld/.codex/skills/tripo-image/scripts/tripo_image.py','--output-dir',str(folder),'text-to-image','--model',job['model'],'--size',job['size'],'--aspect-ratio',job['aspect_ratio'],'--prompt',job['prompt']]
    for attempt in range(12):
        result=subprocess.run(cmd,capture_output=True,text=True)
        if result.returncode==0:
            print(job['name'],'SUCCESS',flush=True);break
        error=result.stderr.strip()
        print(job['name'], 'attempt',attempt+1,error,flush=True)
        if '429' not in error: raise SystemExit(result.returncode)
        time.sleep(min(60,15*(attempt+1)))
    else: raise SystemExit('Generation still rate limited; source remains unchanged.')
