"""Excel schema: field names on row 1, types on row 2, records from row 3."""
from dataclasses import dataclass
import io
import math
import re
import struct
import openpyxl
from compile_localization import IDENTIFIER, write_text
LOCALIZED=re.compile(r'^string\(([A-Za-z][A-Za-z0-9_]{0,63})\)$')
KINDS={'int':1,'float':2,'bool':3,'string':4}

@dataclass
class Table:
    name: str
    fields: list
    types: list
    rows: list
    def references(self):
        return [(LOCALIZED.fullmatch(kind)[1],row[i]) for i,kind in enumerate(self.types) if LOCALIZED.fullmatch(kind) for row in self.rows]
    def binary(self,registry):
        out=io.BytesIO(); out.write(b'SPBT'+struct.pack('<III',1,len(self.fields),len(self.rows)))
        for name,kind in zip(self.fields,self.types):
            write_text(out,name); out.write(bytes([1 if LOCALIZED.fullmatch(kind) else KINDS[kind]]))
        for row in self.rows:
            for kind,value in zip(self.types,row):
                match=LOCALIZED.fullmatch(kind)
                if match: out.write(struct.pack('<i',registry.resolve(match[1],value)))
                elif kind=='int': out.write(struct.pack('<i',value))
                elif kind=='float': out.write(struct.pack('<d',value))
                elif kind=='bool': out.write(bytes([value]))
                else: write_text(out,value)
        return out.getvalue()

def read_workbook(path):
    book=openpyxl.load_workbook(path,read_only=True,data_only=False); result=[]
    try:
        for sheet in book:
            if sheet.title.startswith('_'): continue
            if not IDENTIFIER.fullmatch(sheet.title): raise ValueError(f'{path}: invalid table name {sheet.title}')
            data=list(sheet.iter_rows(values_only=True))
            if len(data)<2: raise ValueError(f'{path}/{sheet.title}: missing type row')
            fields=list(data[0])
            while fields and fields[-1] is None: fields.pop()
            types=list(data[1][:len(fields)])
            if not fields or len(set(fields))!=len(fields) or any(not isinstance(f,str) or not IDENTIFIER.fullmatch(f) for f in fields): raise ValueError(f'{path}/{sheet.title}: invalid field names')
            if len(types)!=len(fields) or any(not isinstance(t,str) or (t not in KINDS and not LOCALIZED.fullmatch(t)) for t in types): raise ValueError(f'{path}/{sheet.title}: invalid types; use int, float, bool, string or string(Table)')
            rows=[]; ids=set()
            for line,raw in enumerate(data[2:],3):
                if all(v is None for v in raw): continue
                if len(raw)<len(fields) or any(v is not None for v in raw[len(fields):]): raise ValueError(f'{path}:{line}: row width mismatch')
                row=[]
                for name,kind,value in zip(fields,types,raw):
                    prefix=f'{path}/{sheet.title}:{line}/{name}'
                    if kind=='int':
                        if type(value) is not int or not -2147483648<=value<=2147483647: raise ValueError(f'{prefix}: expected int32')
                    elif kind=='float':
                        if type(value) not in (float,int) or not math.isfinite(value): raise ValueError(f'{prefix}: expected finite float')
                    elif kind=='bool':
                        if type(value) is not bool: raise ValueError(f'{prefix}: expected boolean')
                    else:
                        if value is None and kind=='string': value=''
                        if not isinstance(value,str) or value.startswith('=') or len(value.encode('utf-8'))>65536: raise ValueError(f'{prefix}: expected text, formulas unsupported')
                        if LOCALIZED.fullmatch(kind) and not value.strip(): raise ValueError(f'{prefix}: localized text cannot be empty')
                    row.append(value)
                if fields[0]=='Id':
                    if types[0] not in ('int','string') or row[0] in ids or row[0]=='': raise ValueError(f'{path}:{line}: invalid/duplicate Id')
                    ids.add(row[0])
                rows.append(row)
            if len(rows)>100000 or len(fields)>256: raise ValueError('Business table too large')
            result.append(Table(sheet.title,fields,types,rows))
        return result
    finally: book.close()

def discover(directory):
    tables={}; names=set()
    for path in sorted(directory.rglob('*.xlsx')):
        if path.name.startswith('~$') or 'Localization' in path.relative_to(directory).parts: continue
        for table in read_workbook(path):
            if table.name.lower() in names: raise ValueError(f'Duplicate business table {table.name}')
            names.add(table.name.lower()); tables[table.name]=table
    return tables
