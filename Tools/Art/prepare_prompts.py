"""Prepare production jobs only; this script does not call a paid image API."""
import json
from pathlib import Path
import openpyxl

SUBJECTS = {
    'w_strike':'a weary armored warrior swinging a battered iron sword',
    'w_guard':'a warrior bracing a scarred iron shield against an incoming blow',
    'w_cleave':'a broad sword cutting a sweeping arc through ash and fog',
    'w_rage':'a warrior gathering fierce resolve, ember light reflected in tired eyes',
    'w_flurry':'two short iron blades crossing in a rapid double strike',
    'w_wall':'a warrior sheltering behind an enormous weathered tower shield',
    'w_breath':'an exhausted warrior catching breath with a hand on a wounded shoulder',
    'w_focus':'a warrior quietly studying a blade before battle',
    'h_shot':'a hooded hunter loosing a single arrow from a weathered bow',
    'h_dodge':'a hunter twisting aside as a blade passes through a trailing cloak',
    'h_venom':'a hunter coating a sharp arrowhead with muted green venom',
    'h_volley':'three arrows leaving a bow in a rapid sequence',
    'h_focus':'a hunter reading footprints in wet ash beneath dead trees',
    'h_toxin':'a cracked glass vial spilling dense green toxin onto an arrowhead',
    'h_shroud':'a hunter disappearing into a veil of charcoal smoke',
    'h_haste':'a hunter sprinting with sudden adrenaline through a ruined passage'
}
STYLE = ('Original dark fantasy card illustration. Semi-realistic anatomy and believable worn materials; '
         'expressive oil paint, visible dry brush, rough canvas grain, psychological melancholy, angular painterly shadows. '
         'Muted charcoal, dirty ivory, burnt sienna, oxidized teal; warrior accents rusty red, hunter accents subdued moss green. '
         'One clearly readable focal action at thumbnail scale. No text, numbers, border, logo, watermark, copied characters or existing card composition. '
         'Landscape 3:2 art crop, leave 8 percent safe margin; consistent painterly realism across the set. ')
ANGLES = ['three-quarter view','low viewpoint','side profile','close framing','slightly elevated view']
LIGHTS = ['cold overcast light','soft amber sidelight','restrained rim light through smoke','diffuse light in a ruined hall']

def main():
    book=openpyxl.load_workbook('GameData/Excel/Cards.xlsx',read_only=True,data_only=True)
    ids=[row[0] for row in list(book['Cards'].values)[1:] if row[0]]; book.close()
    if set(ids)!=set(SUBJECTS): raise ValueError('Every configured card needs an art subject')
    jobs=[]
    for card in ids:
        for variant in range(20):
            jobs.append({'id':f'{card}_{variant+1:02d}','card_id':card,'variant':variant+1,
                         'status':'pending_generation','output':f'ArtProduction/candidates/{card}/{variant+1:02d}.png',
                         'prompt':STYLE+'Subject: '+SUBJECTS[card]+'. Composition: '+ANGLES[variant%5]+'. Lighting: '+LIGHTS[variant//5]+'.'})
    target=Path('ArtProduction'); target.mkdir(exist_ok=True)
    (target/'jobs.jsonl').write_text(''.join(json.dumps(job,ensure_ascii=False)+'\n' for job in jobs),encoding='utf-8')
    print(f'{len(ids)} cards, {len(jobs)} pending image jobs; no images generated.')

if __name__=='__main__': main()
