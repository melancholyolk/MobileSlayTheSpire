"""Build-time CSV registry: global positive int32 IDs, deduplication scoped to a table."""
import csv
import io
import json
import re
import struct
from pathlib import Path
IDENTIFIER = re.compile(r'^[A-Za-z][A-Za-z0-9_]{0,63}$')
LOCALE = re.compile(r'^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$')
MAX_ID = 2147483647

def write_text(out, value):
    data = value.encode('utf-8')
    if len(data)>65536: raise ValueError('Text exceeds 65536 UTF-8 bytes')
    out.write(struct.pack('<I',len(data))); out.write(data)

class Registry:
    def __init__(self, directory):
        self.directory=Path(directory); config=self.directory/'registry.json'
        self.config=json.loads(config.read_text(encoding='utf-8')) if config.exists() else {'version':1,'locales':['en','zh-Hans'],'next_id':1}
        self.locales=self.config['locales']
        if self.config.get('version')!=1 or not self.locales or len(self.locales)>64 or len(set(self.locales))!=len(self.locales) or any(not isinstance(x,str) or not LOCALE.fullmatch(x) for x in self.locales): raise ValueError('Invalid localization registry locales/version')
        self.next_id=self.config['next_id']
        if type(self.next_id) is not int or not 1<=self.next_id<=MAX_ID+1: raise ValueError('Invalid next_id')
        self.tables={}; self.lookup={}; used_ids=set(); symbols=set(); names=set()
        self.headers=['Id','Source','Comment','Symbol']+self.locales
        for path in sorted(self.directory.glob('*.csv')):
            name=path.stem
            if not IDENTIFIER.fullmatch(name) or name.lower() in names: raise ValueError(f'{path}: invalid or case-colliding table name')
            names.add(name.lower()); rows=[]; sources={}
            with path.open(encoding='utf-8-sig',newline='') as file:
                reader=csv.DictReader(file)
                if reader.fieldnames!=self.headers: raise ValueError(f'{path}: expected {self.headers}')
                for line,row in enumerate(reader,2):
                    if None in row or any(v is None for v in row.values()): raise ValueError(f'{path}:{line}: invalid CSV row width')
                    if not row['Id'].isascii() or not row['Id'].isdigit(): raise ValueError(f'{path}:{line}: Id must be int32')
                    key=int(row['Id']); source=row['Source']; symbol=row['Symbol']
                    if not 1<=key<=MAX_ID or key in used_ids: raise ValueError(f'{path}:{line}: duplicate or invalid global ID {key}')
                    if not source.strip() or source in sources: raise ValueError(f'{path}:{line}: empty or duplicate source text')
                    if symbol and (not IDENTIFIER.fullmatch(symbol) or symbol in symbols): raise ValueError(f'{path}:{line}: invalid or duplicate Symbol')
                    if any(len(v.encode('utf-8'))>65536 for v in row.values()): raise ValueError(f'{path}:{line}: text too long')
                    row['Id']=key; rows.append(row); sources[source]=key; used_ids.add(key)
                    if symbol: symbols.add(symbol)
            self.tables[name]=rows; self.lookup[name]=sources
        self.next_id=max(self.next_id,max(used_ids,default=0)+1)

    def synchronize(self, references):
        names={name.lower():name for name in self.tables}
        for table,source in sorted(set(references)):
            if not IDENTIFIER.fullmatch(table): raise ValueError(f'Invalid localization table {table}')
            if table.lower() in names and names[table.lower()]!=table: raise ValueError(f'Case-colliding localization table {table}')
            names[table.lower()]=table
            if not isinstance(source,str) or not source.strip(): raise ValueError('Localized text cannot be empty')
            if source in self.lookup.setdefault(table,{}): continue
            if self.next_id>MAX_ID: raise ValueError('Localization ID space exhausted')
            row=dict.fromkeys(self.headers,''); row.update(Id=self.next_id,Source=source)
            self.tables.setdefault(table,[]).append(row); self.lookup[table][source]=self.next_id; self.next_id+=1

    def resolve(self, table, source): return self.lookup[table][source]

    def sources(self):
        # Persist the high-water mark first, so interrupted writes never reuse allocated IDs.
        config=dict(self.config,next_id=self.next_id)
        result={self.directory/'registry.json':(json.dumps(config,ensure_ascii=False,indent=2)+'\n').encode('utf-8')}
        for name,rows in sorted(self.tables.items()):
            out=io.StringIO(newline=''); writer=csv.DictWriter(out,fieldnames=self.headers,lineterminator='\n')
            writer.writeheader(); writer.writerows(sorted(rows,key=lambda row:row['Id']))
            result[self.directory/(name+'.csv')]=out.getvalue().encode('utf-8-sig')
        return result

    def binary(self):
        count=sum(map(len,self.tables.values()))
        if count<1 or count>100000 or len(self.tables)>10000: raise ValueError('Invalid catalog size')
        out=io.BytesIO(); out.write(b'SPLC'+struct.pack('<IIII',2,len(self.locales),count,len(self.tables)))
        for locale in self.locales: write_text(out,locale)
        for name,rows in sorted(self.tables.items()):
            write_text(out,name); out.write(struct.pack('<I',len(rows)))
            for row in sorted(rows,key=lambda row:row['Id']):
                out.write(struct.pack('<i',row['Id']))
                for locale in self.locales: write_text(out,row[locale] if row[locale].strip() else row['Source'])
        return out.getvalue()

    def constants(self):
        # Symbols become compile-time integers, never runtime string keys.
        lines=['// Generated by Tools/Tables/build_tables.py. Do not edit.','namespace SpireLocalization','{','    public static class LocIds','    {']
        for row in sorted((r for rows in self.tables.values() for r in rows if r['Symbol']),key=lambda r:r['Symbol']):
            lines.append(f"        public const int @{row['Symbol']} = {row['Id']};")
        return ('\n'.join(lines+['    }','}',''])).encode('utf-8')

def compile_directory(source): return Registry(source).binary()
