"""Offline Excel -> SPLC binary. No Unity or third-party runtime serializer required."""
import io
import re
import struct
from pathlib import Path
import openpyxl

KEY = re.compile(r'^[A-Za-z0-9_.-]{1,160}$')
LOCALE = re.compile(r'^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$')

def encode_catalog(locales, entries):
    stream = io.BytesIO()
    stream.write(b'SPLC' + struct.pack('<III', 1, len(locales), len(entries)))
    def string(value):
        raw = value.encode('utf-8')
        if len(raw) > 65536:
            raise ValueError('Translation exceeds 65536 UTF-8 bytes')
        stream.write(struct.pack('<I', len(raw)))
        stream.write(raw)
    for locale in locales:
        string(locale)
    for key, translations in sorted(entries.items()):
        string(key)
        for value in translations:
            string(value)
    return stream.getvalue()

def compile_directory(source):
    files = sorted(p for p in Path(source).rglob('*.xlsx') if not p.name.startswith('~$'))
    if not files:
        raise ValueError('Localization requires at least one .xlsx workbook')
    locales, entries = None, {}
    for path in files:
        book = openpyxl.load_workbook(path, read_only=True, data_only=False)
        try:
            sheet = book['Strings']
            rows = iter(sheet.iter_rows(values_only=True))
            header = list(next(rows))
            while header and header[-1] is None:
                header.pop()
            if len(header) < 3 or header[:2] != ['Key', 'Comment']:
                raise ValueError(f'{path}: header must be Key, Comment, en, ...')
            languages = header[2:]
            if languages[0] != 'en' or len(set(languages)) != len(languages) or any(not isinstance(x,str) or not LOCALE.fullmatch(x) for x in languages):
                raise ValueError(f'{path}: invalid locales (en must be first)')
            if locales is not None and languages != locales:
                raise ValueError(f'{path}: all workbooks must have identical locale columns')
            locales = languages
            for line, row in enumerate(rows, 2):
                if all(x is None for x in row):
                    continue
                key = row[0]
                if not isinstance(key, str) or not KEY.fullmatch(key) or key in entries:
                    raise ValueError(f'{path}:{line}: invalid or duplicate key {key}')
                translations = list(row[2:len(header)])
                if not translations or not isinstance(translations[0],str) or not translations[0].strip():
                    raise ValueError(f'{path}:{line}: missing English fallback')
                for value in translations:
                    if value is not None and (not isinstance(value,str) or value.startswith('=')):
                        raise ValueError(f'{path}:{line}: text only; formulas are not supported')
                entries[key] = [value if value is not None and value.strip() else translations[0] for value in translations]
        finally:
            book.close()
    if not entries or len(entries)>100000 or len(locales)>64:
        raise ValueError('Catalog must contain 1..100000 entries and 1..64 locales')
    return encode_catalog(locales, entries)
