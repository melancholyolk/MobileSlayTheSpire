"""SPCR v2: localized card names are int32 IDs, never inline strings."""
import io
from pathlib import Path
import struct
from business_tables import read_workbook, LOCALIZED
from compile_localization import Registry, write_text
HEADERS=['Id','Name','Class','Cost','Effect','Amount','Hits','Exhaust','StarterCopies']
CLASSES=['Warrior','Hunter']
EFFECTS=['Attack','Block','Strength','Poison','Draw','Heal','Energy']

def validate(table):
    if table.fields!=HEADERS or not LOCALIZED.fullmatch(table.types[1]) or table.types[:1]+table.types[2:]!=['string','string','int','string','int','int','bool','int']:
        raise ValueError('Cards requires standard fields and Name type string(Table)')
    starters=[0,0]
    for line,row in enumerate(table.rows,3):
        key,name,cls,cost,effect,amount,hits,exhaust,copies=row
        if not key.isascii() or not key.replace('_','').isalnum(): raise ValueError(f'Row {line}: invalid card ID')
        if cls not in CLASSES or effect not in EFFECTS: raise ValueError(f'Row {line}: invalid card enum')
        for field,value,low,high in [('Cost',cost,0,10),('Amount',amount,0,999),('Hits',hits,1,10),('StarterCopies',copies,0,20)]:
            if not low<=value<=high: raise ValueError(f'Row {line}: invalid {field}')
        starters[CLASSES.index(cls)]+=copies
    if not table.rows or len(table.rows)>10000 or min(starters)<5: raise ValueError('Need 1..10000 cards and >=5 starter copies per class')

def encode(table,registry):
    validate(table); out=io.BytesIO(); out.write(b'SPCR'+struct.pack('<II',2,len(table.rows)))
    target=LOCALIZED.fullmatch(table.types[1])[1]
    for key,name,cls,cost,effect,amount,hits,exhaust,copies in sorted(table.rows):
        write_text(out,key)
        out.write(struct.pack('<8i',registry.resolve(target,name),CLASSES.index(cls),cost,EFFECTS.index(effect),amount,hits,int(exhaust),copies))
    return out.getvalue()

def compile_table(source, registry=None):
    table=next(t for t in read_workbook(source) if t.name=='Cards')
    validate(table)
    registry=registry or Registry(Path(__file__).resolve().parents[2]/'GameData/Excel/Localization')
    return encode(table,registry)

if __name__=='__main__':
    # Use the transactional full pipeline so direct invocation cannot bypass localization synchronization.
    from build_tables import build
    build()
