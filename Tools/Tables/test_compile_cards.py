import unittest
import tempfile
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
from compile_cards import compile_table

class CompilerTests(unittest.TestCase):
    def test_reproducible(self):
        self.assertEqual(compile_table('GameData/Excel/Cards.xlsx'), Path('Assets/Resources/Tables/cards.bytes').read_bytes())

    def test_bad_cost_and_hit_count_rejected(self):
        # Mutate XML in a temporary test copy; the source workbook is untouched.
        with ZipFile('GameData/Excel/Cards.xlsx') as source:
            entries={n:source.read(n) for n in source.namelist()}
        sheet=entries['xl/worksheets/sheet1.xml'].decode()
        import re
        for cell, value in [('D2','-1'), ('G2','0')]:
            changed=re.sub(r'(<x:c\b[^>]*r="'+cell+r'"[^>]*>.*?<x:v>)[^<]*(</x:v>)',lambda m:m[1]+value+m[2],sheet)
            self.assertNotEqual(sheet,changed)
            with tempfile.TemporaryDirectory() as folder:
                path=Path(folder)/'invalid.xlsx'
                with ZipFile(path,'w',ZIP_DEFLATED) as target:
                    for name,data in entries.items(): target.writestr(name,changed.encode() if name=='xl/worksheets/sheet1.xml' else data)
                with self.assertRaises(ValueError): compile_table(path)

if __name__=='__main__': unittest.main()

