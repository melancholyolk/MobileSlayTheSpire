"""Compile the Cards sheet to SPCR v1, little endian. Requires openpyxl for reading only."""
import argparse
import io
import os
from pathlib import Path
import struct
import openpyxl

HEADERS = ['Id', 'Name', 'Class', 'Cost', 'Effect', 'Amount', 'Hits', 'Exhaust', 'StarterCopies']
CLASSES = ['Warrior', 'Hunter']
EFFECTS = ['Attack', 'Block', 'Strength', 'Poison', 'Draw', 'Heal', 'Energy']

def compile_table(source):
    book = openpyxl.load_workbook(source, data_only=False, read_only=True)
    try:
        sheet = book['Cards']
        rows = list(sheet.iter_rows(values_only=True))
        if list(rows[0][:9]) != HEADERS:
            raise ValueError('Cards header must be ' + ', '.join(HEADERS))
        cards, ids, starters = [], set(), [0, 0]
        for line, row in enumerate(rows[1:], 2):
            if all(v is None for v in row):
                continue
            values = list(row[:9])
            if len(values) != 9 or any(v is None for v in values):
                raise ValueError(f'Row {line}: missing field')
            key, name, cls, cost, effect, amount, hits, exhaust, copies = values
            if not isinstance(key, str) or not key.isascii() or not key.replace('_', '').isalnum() or key in ids:
                raise ValueError(f'Row {line}: invalid or duplicate ID {key}')
            if not isinstance(name, str) or not name.strip() or len(name.encode('utf-8')) > 1024:
                raise ValueError(f'Row {line}: invalid name')
            if cls not in CLASSES or effect not in EFFECTS or type(exhaust) is not bool:
                raise ValueError(f'Row {line}: invalid enum or Exhaust boolean')
            for field, value, low, high in [('Cost',cost,0,10),('Amount',amount,0,999),('Hits',hits,1,10),('StarterCopies',copies,0,20)]:
                if type(value) is not int or not low <= value <= high:
                    raise ValueError(f'Row {line}: {field} must be integer {low}..{high}')
            ids.add(key); starters[CLASSES.index(cls)] += copies
            cards.append((key, name, CLASSES.index(cls), cost, EFFECTS.index(effect), amount, hits, int(exhaust), copies))
        if not cards or len(cards) > 10000 or min(starters) < 5:
            raise ValueError('Need 1..10000 cards and >=5 starter copies per class')
        out = io.BytesIO(); out.write(b'SPCR'); out.write(struct.pack('<II',1,len(cards)))
        for card in sorted(cards):
            for value in card[:2]:
                raw=value.encode('utf-8'); out.write(struct.pack('<I',len(raw))); out.write(raw)
            out.write(struct.pack('<7i',*card[2:]))
        return out.getvalue()
    finally:
        book.close()

if __name__ == '__main__':
    p=argparse.ArgumentParser(); p.add_argument('source',nargs='?',default='GameData/Excel/Cards.xlsx'); p.add_argument('--output',default='Assets/Resources/Tables/cards.bytes')
    args=p.parse_args(); data=compile_table(args.source)
    target=Path(args.output); target.parent.mkdir(parents=True,exist_ok=True)
    temp=target.with_suffix('.tmp'); temp.write_bytes(data); os.replace(temp,target)
    print(f'Compiled {len(data)} bytes -> {target}')
