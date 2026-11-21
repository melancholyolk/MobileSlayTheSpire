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
    def test_arbitrary_character_and_card_removal_need_no_code_registration(self):
        from business_tables import read_workbook
        from compile_cards import encode
        from compile_localization import Registry
        tables={t.name:t for t in read_workbook(Path('GameData/Excel/Cards.xlsx'))}
        cards,characters=tables['Cards'],tables['Characters']
        cards.rows=[list(r) for r in cards.rows if r[2]==0]
        for row in cards.rows: row[2]=42
        characters.rows=[list(r) for r in characters.rows if r[0]==0]
        characters.rows[0][0]=42
        registry=Registry(Path('GameData/Excel/Localization'))
        data=encode(cards,registry,characters)
        removed=next(r for r in cards.rows if r[8]==0)
        cards.rows.remove(removed)
        reduced=encode(cards,registry,characters)
        self.assertLess(len(reduced),len(data))

if __name__=='__main__': unittest.main()
