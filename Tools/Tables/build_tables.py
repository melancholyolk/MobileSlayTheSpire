"""Run from any directory. --watch rebuilds after stable Excel changes, without Unity."""
import argparse
import hashlib
import os
from pathlib import Path
import tempfile
import time
from compile_cards import compile_table
from compile_localization import compile_directory

ROOT = Path(__file__).resolve().parents[2]

def snapshot(root):
    files = sorted(p for p in (root/'GameData/Excel').rglob('*.xlsx') if not p.name.startswith('~$'))
    return tuple((str(p.relative_to(root)), hashlib.sha256(p.read_bytes()).hexdigest()) for p in files)

def build(root=ROOT):
    # Validate everything before replacing any live output. Failed edits preserve the last good assets.
    outputs = {
        root/'Assets/Resources/Tables/cards.bytes': compile_table(root/'GameData/Excel/Cards.xlsx'),
        root/'Assets/Resources/Localization/catalog.bytes': compile_directory(root/'GameData/Excel/Localization'),
    }
    changed = []
    for path, data in outputs.items():
        if path.exists() and path.read_bytes() == data:
            continue
        path.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=path.parent, suffix='.tmp', delete=False) as file:
            file.write(data)
            temp = Path(file.name)
        try:
            os.replace(temp, path)
        finally:
            temp.unlink(missing_ok=True)
        changed.append(path)
    print('Tables OK: ' + (', '.join(str(p.relative_to(root)) for p in changed) or 'unchanged'), flush=True)
    return changed

def watch(root=ROOT, interval=0.5, stop=None):
    previous = None
    candidate = None
    print('Watching GameData/Excel (Ctrl+C to stop)', flush=True)
    while stop is None or not stop.is_set():
        try:
            current = snapshot(root)
            if current == candidate and current != previous:
                build(root)
                previous = current
            candidate = current
        except Exception as error:
            print(f'Build failed; retaining last good assets: {error}', flush=True)
        if stop is not None:
            stop.wait(interval)
        else:
            time.sleep(interval)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--watch', action='store_true')
    args = parser.parse_args()
    try:
        watch() if args.watch else build()
    except KeyboardInterrupt:
        pass
