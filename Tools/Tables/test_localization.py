import io
from pathlib import Path
import shutil
import struct
import tempfile
import threading
import time
import unittest
import zipfile
from xml.etree import ElementTree as ET
from build_tables import ROOT, build, watch
from compile_localization import compile_directory

# Modify fixture XML directly so tests exercise real XLSX files without a spreadsheet writer dependency.
def edit_cell(path, cell, text):
    ns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'
    with zipfile.ZipFile(path) as archive:
        content={name:archive.read(name) for name in archive.namelist()}
    sheet=ET.fromstring(content['xl/worksheets/sheet1.xml'])
    node=sheet.find(f'.//{{{ns}}}c[@r="{cell}"]')
    node.clear(); node.set('r',cell); node.set('t','inlineStr')
    ET.SubElement(ET.SubElement(node,f'{{{ns}}}is'),f'{{{ns}}}t').text=text
    content['xl/worksheets/sheet1.xml']=ET.tostring(sheet)
    with zipfile.ZipFile(path,'w') as archive:
        for name,data in content.items(): archive.writestr(name,data)

class LocalizationBuildTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.root=Path(self.temp.name)
        shutil.copytree(ROOT/'GameData/Excel',self.root/'GameData/Excel')
        self.source=self.root/'GameData/Excel/Localization'
        self.book=self.source/'Game.xlsx'
        self.output=self.root/'Assets/Resources/Localization/catalog.bytes'
    def tearDown(self): self.temp.cleanup()
    def test_deterministic_binary_and_noop(self):
        self.assertEqual(compile_directory(self.source),compile_directory(self.source))
        build(self.root); stamp=self.output.stat().st_mtime_ns
        self.assertEqual(build(self.root),[])
        self.assertEqual(stamp,self.output.stat().st_mtime_ns)
        self.assertEqual(struct.unpack('<III',self.output.read_bytes()[4:16]),(1,2,32))
    def test_duplicate_keys_across_files_rejected(self):
        shutil.copy(self.book,self.source/'Duplicate.xlsx')
        with self.assertRaisesRegex(ValueError,'duplicate'): compile_directory(self.source)
    def test_invalid_locale_rejected(self):
        edit_cell(self.book,'D1','not_a_locale')
        with self.assertRaisesRegex(ValueError,'locales'): compile_directory(self.source)
    def test_bad_edit_preserves_good_assets(self):
        build(self.root); before=self.output.read_bytes()
        edit_cell(self.book,'C2','=1+1')
        with self.assertRaisesRegex(ValueError,'formulas'): build(self.root)
        self.assertEqual(before,self.output.read_bytes())
    def test_blank_translation_falls_back(self):
        edit_cell(self.book,'D2','')
        data=compile_directory(self.source)
        self.assertIn(b'ASHEN ASCENT',data)
        self.assertNotIn('灰烬之巅'.encode(),data)
    def test_excel_lock_file_ignored(self):
        (self.source/'~$Game.xlsx').write_bytes(b'not a workbook')
        compile_directory(self.source)
    def test_watch_save_rebuild_and_recover(self):
        build(self.root); before=self.output.read_bytes(); stop=threading.Event()
        thread=threading.Thread(target=watch,args=(self.root,.05,stop)); thread.start()
        try:
            edit_cell(self.book,'D2','测试自动重建')
            deadline=time.monotonic()+5
            while time.monotonic()<deadline and self.output.read_bytes()==before: time.sleep(.05)
            self.assertIn('测试自动重建'.encode(),self.output.read_bytes())
            good=self.output.read_bytes()
            edit_cell(self.book,'C2','=bad'); time.sleep(.25)
            self.assertEqual(good,self.output.read_bytes())
            edit_cell(self.book,'C2','Recovered')
            deadline=time.monotonic()+5
            while time.monotonic()<deadline and b'Recovered' not in self.output.read_bytes(): time.sleep(.05)
            self.assertIn(b'Recovered',self.output.read_bytes())
        finally: stop.set(); thread.join(5)

if __name__=='__main__': unittest.main()
