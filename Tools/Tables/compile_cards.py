"""SPCR v3: data-driven character IDs and integer artwork IDs."""
import io
from pathlib import Path
import struct
from business_tables import read_workbook, LOCALIZED
from compile_localization import Registry, write_text
HEADERS=['Id','Name','Class','Cost','Effect','Amount','Hits','Exhaust','StarterCopies','ArtId']
EFFECTS=['Attack','Block','Strength','Poison','Draw','Heal','Energy']

def validate(table,characters=None):
    if table.fields!=HEADERS or not LOCALIZED.fullmatch(table.types[1]) or table.types[:1]+table.types[2:]!=['string','int','int','string','int','int','bool','int','int']:
        raise ValueError('Cards requires standard fields, integer Class/ArtId and Name type string(Table)')
    starters={}
    for line,row in enumerate(table.rows,3):
        key,name,cls,cost,effect,amount,hits,exhaust,copies,art=row
        if not key.isascii() or not key.replace('_','').isalnum(): raise ValueError(f'Row {line}: invalid card ID')
        if cls<0 or effect not in EFFECTS or art<=0: raise ValueError(f'Row {line}: invalid character/effect/art')
        for field,value,low,high in [('Cost',cost,0,10),('Amount',amount,0,999),('Hits',hits,1,10),('StarterCopies',copies,0,20)]:
            if not low<=value<=high: raise ValueError(f'Row {line}: invalid {field}')
        starters[cls]=starters.get(cls,0)+copies
    if not table.rows or len(table.rows)>10000 or min(starters.values())<5: raise ValueError('Need 1..10000 cards and >=5 starter copies per character')
    if characters is not None:
        if characters.fields!=['Id','Name','MaxHp','PortraitId'] or characters.types!=['int','string(Common)','int','int']:
            raise ValueError('Characters requires Id, Name, MaxHp, PortraitId')
        ids=set()
        for key,name,hp,portrait in characters.rows:
            if key<0 or not 1<=hp<=9999 or portrait<=0: raise ValueError('Invalid character definition')
            ids.add(key)
        if ids!=set(starters): raise ValueError('Every character requires a valid starter deck; cards must reference existing characters')

def encode(table,registry,characters=None):
    if characters is None:
        characters=next(t for t in read_workbook(Path(__file__).resolve().parents[2]/'GameData/Excel/Cards.xlsx') if t.name=='Characters')
    validate(table,characters)
    definitions={r[0]:r for r in characters.rows}
    out=io.BytesIO(); out.write(b'SPCR'+struct.pack('<II',3,len(table.rows)))
    target=LOCALIZED.fullmatch(table.types[1])[1]
    for key,name,cls,cost,effect,amount,hits,exhaust,copies,art in sorted(table.rows):
        _,character_name,hp,portrait=definitions[cls]
        write_text(out,key)
        out.write(struct.pack('<12i',registry.resolve(target,name),cls,cost,EFFECTS.index(effect),amount,hits,int(exhaust),copies,art,registry.resolve('Common',character_name),hp,portrait))
    return out.getvalue()

def compile_table(source,registry=None):
    tables={t.name:t for t in read_workbook(source)}
    registry=registry or Registry(Path(__file__).resolve().parents[2]/'GameData/Excel/Localization')
    return encode(tables['Cards'],registry,tables.get('Characters'))

if __name__=='__main__':
    from build_tables import build
    build()
