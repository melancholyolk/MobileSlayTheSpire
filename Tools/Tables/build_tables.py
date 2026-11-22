"""Validate Excel -> persist incremental localization CSVs -> encode and publish binaries."""
import argparse
from contextlib import contextmanager
import hashlib
import os
from pathlib import Path
import tempfile
import time
from business_tables import discover
from compile_cards import validate, encode
from compile_localization import Registry
from body_tables import validate_body
ROOT=Path(__file__).resolve().parents[2]

@contextmanager
def build_lock(root):
    folder=root/'.uloop'; folder.mkdir(parents=True,exist_ok=True)
    with (folder/'tables.lock').open('a+b') as file:
        file.seek(0); file.write(b'0'); file.flush(); file.seek(0)
        try:
            if os.name=='nt':
                import msvcrt
                msvcrt.locking(file.fileno(),msvcrt.LK_NBLCK,1)
            else:
                import fcntl
                fcntl.flock(file.fileno(),fcntl.LOCK_EX|fcntl.LOCK_NB)
        except OSError as error: raise RuntimeError('Another table build is running; retry shortly') from error
        try: yield
        finally:
            file.seek(0)
            if os.name=='nt': msvcrt.locking(file.fileno(),msvcrt.LK_UNLCK,1)
            else: fcntl.flock(file.fileno(),fcntl.LOCK_UN)

def snapshot(root):
    files=sorted(p for p in (root/'GameData/Excel').rglob('*') if p.is_file() and p.suffix.lower() in ('.xlsx','.csv','.json') and not p.name.startswith('~$'))
    return tuple((str(p.relative_to(root)),hashlib.sha256(p.read_bytes()).hexdigest()) for p in files)

def publish(outputs):
    changed=[]
    for path,data in outputs.items():
        if path.exists() and path.read_bytes()==data: continue
        path.parent.mkdir(parents=True,exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=path.parent,suffix='.tmp',delete=False) as file:
            file.write(data); temp=Path(file.name)
        try: os.replace(temp,path)
        finally: temp.unlink(missing_ok=True)
        changed.append(path)
    return changed

def build(root=ROOT):
    root=Path(root)
    with build_lock(root):
        before=snapshot(root)
        tables=discover(root/'GameData/Excel')
        if 'Cards' not in tables: raise ValueError('Cards table is required')
        validate(tables['Cards'],tables.get('Characters'))
        validate_body(tables)
        directory=root/'GameData/Excel/Localization'
        registry=Registry(directory)
        registry.synchronize(ref for table in tables.values() for ref in table.references())
        sources=registry.sources()
        if sum(map(len,registry.tables.values()))>100000: raise ValueError('Localization catalog too large')
        if snapshot(root)!=before: raise RuntimeError('Sources changed during validation; retry build')
        # Persist first, then re-read the exact committed CSVs. No binary can contain an unregistered ID.
        changed=publish(sources)
        synced=snapshot(root)
        registry=Registry(directory)
        outputs={root/'Assets/Resources/Tables/cards.bytes':encode(tables['Cards'],registry,tables.get('Characters')),
                 root/'Assets/Resources/Localization/catalog.bytes':registry.binary(),
                 root/'Assets/Scripts/Localization/LocIds.cs':registry.constants()}
        outputs[root/'Assets/GameResources/Tables/10.bytes']=outputs[root/'Assets/Resources/Tables/cards.bytes']
        outputs[root/'Assets/GameResources/Tables/11.bytes']=outputs[root/'Assets/Resources/Localization/catalog.bytes']
        for name,table in tables.items():
            if name!='Cards': outputs[root/f'Assets/Resources/BusinessTables/{name}.bytes']=table.binary(registry)
        for resource_id,name in enumerate(('BodyParts','BodyPorts','BodyNodes','BodyMotions'),23):
            if name in tables:
                outputs[root/f'Assets/GameResources/Tables/{resource_id}.bytes']=tables[name].binary(registry)
        if snapshot(root)!=synced: raise RuntimeError('CSV changed during encoding; retry build')
        changed+=publish(outputs)
        # This directory is owned exclusively by this compiler. Removed business tables must not ship stale data.
        for path in (root/'Assets/Resources/BusinessTables').glob('*.bytes'):
            if path not in outputs:
                path.unlink(); path.with_name(path.name+'.meta').unlink(missing_ok=True); changed.append(path)
        print('Tables OK: '+(', '.join(str(p.relative_to(root)) for p in changed) or 'unchanged'),flush=True)
        return changed

def watch(root=ROOT,interval=0.5,stop=None):
    previous=candidate=None
    print('Watching Excel and localization CSVs (Ctrl+C to stop)',flush=True)
    while stop is None or not stop.is_set():
        try:
            current=snapshot(root)
            if current==candidate and current!=previous:
                build(root)
                previous=snapshot(root)  # Account for CSVs written by the build itself.
            candidate=current
        except Exception as error:
            print(f'Build failed; retaining last good binaries: {error}',flush=True)
        if stop is not None: stop.wait(interval)
        else: time.sleep(interval)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument('--watch',action='store_true'); args=parser.parse_args()
    try: watch() if args.watch else build()
    except KeyboardInterrupt: pass

