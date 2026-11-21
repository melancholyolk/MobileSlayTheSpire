import argparse,pathlib,json,hashlib,tarfile,gzip,io,shutil
parser=argparse.ArgumentParser(description='Package verified official Tailscale 1.104.7 ARM64 files for offline Docker import')
parser.add_argument('--layers-dir',type=pathlib.Path,required=True)
parser.add_argument('--static-tgz',type=pathlib.Path,required=True)
args=parser.parse_args()
r=args.layers_dir
pkg=args.static_tgz
assert hashlib.sha256(pkg.read_bytes()).hexdigest()=='d6bed2720310b328b861f943d7b4016c5358a9bad60575eb51e8143b79068484'
layers=['fea5b91588ca539ff8a89d7d5df6c6d65d6ab7bd89145ba57f32c302cf02ffbe','f579224f18ef2647c0271f4955565ee7abb910e50c130ee7eaec41d317b7abce']
diffs=['sha256:d0af45c05d06b9fef7731c3ddeeb7141f22477266bb3895a68d3492af49d191b','sha256:e9d72e98a5d026f9fecb2d0532cdd6026a1fe8e603243c15aa94446b1fb3a357']
for name,diff in zip(layers,diffs):
 p=r/name;assert hashlib.sha256(p.read_bytes()).hexdigest()==name
 with gzip.open(p,'rb') as inp,(r/(name+'.tar')).open('wb') as out:shutil.copyfileobj(inp,out)
 assert 'sha256:'+hashlib.sha256((r/(name+'.tar')).read_bytes()).hexdigest()==diff
entry=(pathlib.Path(__file__).with_name('offline-entrypoint.sh')).read_text(encoding='utf-8')
custom=r/'offline-static-layer.tar'
with tarfile.open(custom,'w') as out,tarfile.open(pkg,'r:gz') as src:
 for name in ['tailscale','tailscaled']:
  member=next(m for m in src.getmembers() if m.isfile() and m.name.endswith('/'+name))
  data=src.extractfile(member).read();assert data[:4]==b'\x7fELF' and int.from_bytes(data[18:20],'little')==183
  info=tarfile.TarInfo('usr/local/bin/'+name);info.size=len(data);info.mode=0o755;out.addfile(info,io.BytesIO(data))
 data=entry.encode();info=tarfile.TarInfo('usr/local/bin/offline-entrypoint');info.size=len(data);info.mode=0o755;out.addfile(info,io.BytesIO(data))
diff='sha256:'+hashlib.sha256(custom.read_bytes()).hexdigest();diffs.append(diff)
config={'architecture':'arm64','os':'linux','config':{'Env':['PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin'],'Entrypoint':['/usr/local/bin/offline-entrypoint'],'WorkingDir':'/','Labels':{'org.opencontainers.image.version':'1.104.7','org.opencontainers.image.description':'Locally packaged official Tailscale static ARM64 binaries; userspace Gitea TCP forwarding'}},'rootfs':{'type':'layers','diff_ids':diffs}}
raw=json.dumps(config,separators=(',',':')).encode();cid=hashlib.sha256(raw).hexdigest()+'.json'
archive=r/'tailscale-lfs-offline-1.104.7-arm64.tar.gz'
with tarfile.open(archive,'w:gz',compresslevel=1) as out:
 info=tarfile.TarInfo(cid);info.size=len(raw);out.addfile(info,io.BytesIO(raw))
 names=[]
 for i,p in enumerate([r/(n+'.tar') for n in layers]+[custom]):
  name=str(i)+'/layer.tar';out.add(p,arcname=name);names.append(name)
 data=json.dumps([{'Config':cid,'RepoTags':['local/tailscale-lfs:1.104.7-arm64'],'Layers':names}]).encode()
 info=tarfile.TarInfo('manifest.json');info.size=len(data);out.addfile(info,io.BytesIO(data))
print('Image config',cid,'Archive',archive,'Bytes',archive.stat().st_size,'SHA256',hashlib.sha256(archive.read_bytes()).hexdigest(),flush=True)
