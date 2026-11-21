import unittest
import tempfile
from pathlib import Path
import shutil
from compile_cards import compile_table
from test_localization import edit_cell

class CompilerTests(unittest.TestCase):
    def test_reproducible(self):
        self.assertEqual(compile_table('GameData/Excel/Cards.xlsx'),Path('Assets/Resources/Tables/cards.bytes').read_bytes())
    def test_bad_cost_and_hit_count_rejected(self):
        for cell in ['D3','G3']:
            with tempfile.TemporaryDirectory() as folder:
                path=Path(folder)/'invalid.xlsx'; shutil.copy('GameData/Excel/Cards.xlsx',path)
                edit_cell(path,cell,'invalid')
                with self.assertRaises(ValueError): compile_table(path)
if __name__=='__main__': unittest.main()
