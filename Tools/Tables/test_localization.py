import csv
import io
from pathlib import Path
import shutil
import tempfile
import threading
import time
import unittest
from unittest.mock import patch
import zipfile
from xml.etree import ElementTree as ET
from build_tables import ROOT, build, watch, build_lock
from business_tables import discover, read_workbook
from compile_localization import Registry

def edit_cell(path,cell,text,sheet_name='sheet1'):
    ns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'
    with zipfile.ZipFile(path) as archive: content={name:archive.read(name) for name in archive.namelist()}
    filename=f'xl/worksheets/{sheet_name}.xml'; sheet=ET.fromstring(content[filename])
    node=sheet.find(f'.//{{{ns}}}c[@r="{cell}"]'); node.clear(); node.set('r',cell); node.set('t','inlineStr')
    ET.SubElement(ET.SubElement(node,f'{{{ns}}}is'),f'{{{ns}}}t').text=text
    content[filename]=ET.tostring(sheet)
    with zipfile.ZipFile(path,'w') as archive:
        for name,data in content.items(): archive.writestr(name,data)

class LocalizationBuildTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.root=Path(self.temp.name)
        shutil.copytree(ROOT/'GameData/Excel',self.root/'GameData/Excel')
        self.directory=self.root/'GameData/Excel/Localization'; self.book=self.root/'GameData/Excel/StoryExample.xlsx'
        self.output=self.root/'Assets/Resources/Localization/catalog.bytes'
    def tearDown(self): self.temp.cleanup()
    def registry(self): return Registry(self.directory)
    def edit_csv(self,name,change):
        path=self.directory/(name+'.csv')
        with path.open(encoding='utf-8-sig',newline='') as file:
            reader=csv.DictReader(file); headers=reader.fieldnames; rows=list(reader)
        change(rows)
        with path.open('w',encoding='utf-8-sig',newline='') as file:
            writer=csv.DictWriter(file,fieldnames=headers,lineterminator='\n'); writer.writeheader(); writer.writerows(rows)
    def test_deterministic_and_noop(self):
        build(self.root); stamp=self.output.stat().st_mtime_ns
        self.assertEqual(build(self.root),[]); self.assertEqual(stamp,self.output.stat().st_mtime_ns)
    def test_new_table_created_and_persisted_before_encoding(self):
        edit_cell(self.book,'B2','string(NewStory)'); original=Registry.binary
        def inspect(registry):
            self.assertTrue((self.directory/'NewStory.csv').exists())
            committed=self.registry(); self.assertEqual(registry.lookup,committed.lookup)
            return original(registry)
        with patch.object(Registry,'binary',inspect): build(self.root)
        table=read_workbook(self.book)[0]; reg=self.registry()
        self.assertEqual(table.binary(reg),(self.root/'Assets/Resources/BusinessTables/StoryExample.bytes').read_bytes())
        self.assertNotIn('灰烬落下'.encode(),table.binary(reg))
        self.assertEqual(len(reg.tables['NewStory']),2)
    def test_reuse_is_scoped_and_ids_are_global(self):
        reg=self.registry(); reg.synchronize([('Story','继续'),('Common','继续'),('Story','继续')])
        self.assertNotEqual(reg.resolve('Story','继续'),reg.resolve('Common','继续'))
        ids=[r['Id'] for rows in reg.tables.values() for r in rows]; self.assertEqual(len(ids),len(set(ids)))
    def test_edit_allocates_new_id_and_retains_old_translation(self):
        old=self.registry().resolve('Story','灰烬落下，新的旅程开始。')
        self.edit_csv('Story',lambda rows: rows[0].update(en='Translated'))
        edit_cell(self.book,'B3','New narrative'); build(self.root)
        reg=self.registry(); self.assertNotEqual(old,reg.resolve('Story','New narrative'))
        self.assertEqual(old,reg.resolve('Story','灰烬落下，新的旅程开始。'))
        self.assertEqual('Translated',reg.tables['Story'][0]['en'])
    def test_invalid_business_edit_preserves_csv_and_binary(self):
        build(self.root); before=self.output.read_bytes(); csv_before=(self.directory/'Common.csv').read_bytes()
        edit_cell(self.book,'B3','New text'); edit_cell(self.root/'GameData/Excel/Cards.xlsx','D3','invalid')
        with self.assertRaises(ValueError): build(self.root)
        self.assertEqual(before,self.output.read_bytes()); self.assertEqual(csv_before,(self.directory/'Common.csv').read_bytes())
        self.assertNotIn('New text',self.registry().lookup['Story'])
    def test_duplicate_id_and_source_rejected(self):
        self.edit_csv('Story',lambda rows: rows[0].update(Id='1'))
        with self.assertRaisesRegex(ValueError,'ID'): build(self.root)
    def test_duplicate_text_rejected(self):
        self.edit_csv('Story',lambda rows: rows[1].update(Source=rows[0]['Source']))
        with self.assertRaisesRegex(ValueError,'duplicate source'): build(self.root)
    def test_malformed_annotation_rejected_without_side_effect(self):
        edit_cell(self.book,'B2','string(../Escape)')
        with self.assertRaisesRegex(ValueError,'types'): build(self.root)
        self.assertFalse(self.output.exists())
    def test_high_water_mark_survives_deletion(self):
        reg=self.registry(); previous=reg.next_id
        self.edit_csv('Story',lambda rows: rows.clear())
        build(self.root); reg=self.registry()
        self.assertGreaterEqual(reg.resolve('Story','灰烬落下，新的旅程开始。'),previous)
    def test_formula_rejected(self):
        edit_cell(self.book,'B3','=1+1')
        with self.assertRaisesRegex(ValueError,'formulas'): build(self.root)
    def test_lock_prevents_concurrent_allocation(self):
        with build_lock(self.root):
            with self.assertRaisesRegex(RuntimeError,'Another'): build(self.root)
    def test_csv_quoting_and_blank_translation_fallback(self):
        value='A comma, a "quote"\nand a newline'
        edit_cell(self.book,'B3',value); build(self.root)
        reg=self.registry(); self.assertIn(value,reg.lookup['Story']); self.assertIn(value.encode(),reg.binary())
    def test_watch_reacts_to_csv_translation_edit(self):
        build(self.root); stop=threading.Event(); thread=threading.Thread(target=watch,args=(self.root,.05,stop)); thread.start()
        try:
            self.edit_csv('Story',lambda rows: rows[0].update(en='Watcher translation'))
            deadline=time.monotonic()+5
            while time.monotonic()<deadline and b'Watcher translation' not in self.output.read_bytes(): time.sleep(.05)
            self.assertIn(b'Watcher translation',self.output.read_bytes())
        finally: stop.set(); thread.join(5)
    def test_removed_business_table_removes_only_owned_output(self):
        build(self.root); self.book.unlink(); build(self.root)
        self.assertFalse((self.root/'Assets/Resources/BusinessTables/StoryExample.bytes').exists())
        self.assertTrue((self.directory/'Story.csv').exists())

if __name__=='__main__': unittest.main()
