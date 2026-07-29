#!/usr/bin/env python3
import os, re, csv, json, subprocess, hashlib, math, plistlib
from pathlib import Path
from collections import defaultdict, Counter

ROOT = Path('<PROJECT_ROOT>')
REPORT = ROOT / 'BCAT_AUDIT_REPORT'
ASSETS = ROOT / 'Assets'
WEBGL = ROOT / 'webgl'
BUILDREPORT = Path('/mnt/data/LastBuild.buildreport')
REPORT.mkdir(exist_ok=True)
COMMANDS=[]

def run(cmd, cwd=ROOT, timeout=120):
    COMMANDS.append(' '.join(map(str, cmd)) if isinstance(cmd, (list,tuple)) else cmd)
    try:
        r=subprocess.run(cmd, cwd=str(cwd), text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=timeout)
        return r.returncode, r.stdout, r.stderr
    except Exception as e:
        return 999, '', str(e)

def bytes_fmt(n):
    try: n=float(n)
    except: return ''
    for unit in ['B','KB','MB','GB','TB']:
        if n < 1024 or unit=='TB': return f'{n:.1f} {unit}' if unit!='B' else f'{int(n)} B'
        n/=1024

def file_size(p):
    try: return p.stat().st_size
    except: return 0

def rel(p):
    try: return str(p.relative_to(ROOT))
    except: return str(p)

def write_csv(name, rows, fields):
    with open(REPORT/name, 'w', newline='', encoding='utf-8') as f:
        w=csv.DictWriter(f, fieldnames=fields, extrasaction='ignore')
        w.writeheader(); w.writerows(rows)

def write_md(name, text):
    (REPORT/name).write_text(text, encoding='utf-8')

def is_ignored_dir(p):
    parts=set(p.parts)
    return any(x in parts for x in ['.git','Library','Temp','obj','BCAT_AUDIT_REPORT'])

# Inventory files
all_files=[]
asset_files=[]
for p in ROOT.rglob('*'):
    if p.is_dir(): continue
    if is_ignored_dir(p): continue
    s=file_size(p)
    all_files.append({'path':rel(p),'abs':p,'size':s,'ext':p.suffix.lower()})
    try:
        p.relative_to(ASSETS)
        asset_files.append({'path':rel(p),'abs':p,'size':s,'ext':p.suffix.lower()})
    except: pass

# GUID map
guid_to_asset={}
asset_to_guid={}
for meta in ASSETS.rglob('*.meta'):
    if is_ignored_dir(meta): continue
    try:
        txt=meta.read_text(errors='ignore')[:4096]
    except: continue
    m=re.search(r'^guid:\s*([0-9a-fA-F]+)', txt, re.M)
    if m:
        ap=str(meta.with_suffix('').relative_to(ROOT))
        guid=m.group(1)
        guid_to_asset[guid]=ap
        asset_to_guid[ap]=guid

# YAML/script text refs
text_exts={'.unity','.prefab','.asset','.mat','.controller','.playable','.cs','.uss','.uxml','.inputactions','.asmdef','.json'}
text_files=[x for x in asset_files if x['ext'] in text_exts]
guid_refs=defaultdict(list)
filename_refs=defaultdict(list)
videoaudio_names=[]
for x in asset_files:
    if x['ext'] in {'.mp4','.mov','.m4v','.webm','.ogv','.avi','.wav','.mp3','.aac','.m4a','.ogg','.aiff','.aif'}:
        videoaudio_names.append(Path(x['path']).name)
for tf in text_files:
    p=tf['abs']
    try: txt=p.read_text(errors='ignore')
    except: continue
    for g in re.findall(r'guid:\s*([0-9a-fA-F]{32})', txt):
        if g in guid_to_asset:
            guid_refs[guid_to_asset[g]].append(tf['path'])
    for name in videoaudio_names:
        if name and name in txt:
            filename_refs[name].append(tf['path'])

# Build output
build_rows=[]
if WEBGL.exists():
    for p in WEBGL.rglob('*'):
        if p.is_file():
            build_rows.append({'Asset path':rel(p),'Asset type':'WebGL output','Source file size':file_size(p),'Source file size human':bytes_fmt(file_size(p)),'Estimated packed/build size':file_size(p),'Referencing scene/prefab/component/script/Resources':'Built output','Also present in StreamingAssets':'Yes' if 'StreamingAssets' in p.parts else 'No','Recommended action':'Inspect source asset if large','Risk level':'Audit only'})

# Helpers: ffprobe/identify
FFPROBE='/opt/homebrew/bin/ffprobe' if Path('/opt/homebrew/bin/ffprobe').exists() else 'ffprobe'
IDENTIFY='/opt/homebrew/bin/identify' if Path('/opt/homebrew/bin/identify').exists() else 'identify'

def ffprobe_json(p):
    code,out,err=run([FFPROBE,'-v','error','-show_streams','-show_format','-of','json',str(p)], timeout=40)
    if code!=0: return None, err.strip()
    try: return json.loads(out), ''
    except Exception as e: return None, str(e)

def identify_info(p):
    # width height colorspace alpha format
    code,out,err=run([IDENTIFY,'-format','%w,%h,%m,%[channels]',str(p)], timeout=30)
    if code!=0: return None, err.strip()
    parts=out.strip().split(',')
    if len(parts)>=4: return parts, ''
    return None, out+err

def meta_text(asset_path):
    m=ROOT/(asset_path+'.meta')
    if m.exists():
        try: return m.read_text(errors='ignore')
        except: return ''
    return ''

def meta_field(txt, key):
    m=re.search(r'\b'+re.escape(key)+r':\s*([^\n]+)', txt)
    return m.group(1).strip() if m else ''

def referenced_by(path):
    refs=[]
    if path in guid_refs: refs += guid_refs[path]
    name=Path(path).name
    if name in filename_refs: refs += filename_refs[name]
    # raw Resources / StreamingAssets evidence
    if '/Resources/' in ('/'+path): refs.append('Resources folder inclusion')
    if '/StreamingAssets/' in ('/'+path): refs.append('StreamingAssets raw copy')
    return '; '.join(sorted(set(refs))[:20])

def in_streaming(path): return '/StreamingAssets/' in ('/'+path)

# Video audit
video_ext={'.mp4','.mov','.m4v','.webm','.ogv','.avi'}
video_rows=[]
for x in sorted([a for a in asset_files if a['ext'] in video_ext], key=lambda r:r['size'], reverse=True):
    data,err=ffprobe_json(x['abs'])
    vstream={}; astream={}; fmt={}
    if data:
        fmt=data.get('format',{}) or {}
        for st in data.get('streams',[]):
            if st.get('codec_type')=='video' and not vstream: vstream=st
            if st.get('codec_type')=='audio' and not astream: astream=st
    refs=referenced_by(x['path'])
    similar=[]
    stem=re.sub(r'[_\- ]?(720p|xr|audio|compressed|final|copy|\(\d+\))$','',Path(x['path']).stem, flags=re.I).lower().replace('_',' ').replace('-',' ')
    for y in asset_files:
        if y is x or y['ext'] not in video_ext: continue
        st2=re.sub(r'[_\- ]?(720p|xr|audio|compressed|final|copy|\(\d+\))$','',Path(y['path']).stem, flags=re.I).lower().replace('_',' ').replace('-',' ')
        if stem and (stem==st2 or stem in st2 or st2 in stem): similar.append(y['path'])
    audio_dups=[]
    for y in asset_files:
        if y['ext'] in {'.wav','.mp3','.aac','.m4a','.ogg','.aiff','.aif'} and stem and stem in Path(y['path']).stem.lower().replace('_',' ').replace('-',' '): audio_dups.append(y['path'])
    name=Path(x['path']).name
    build_copy=[]
    if WEBGL.exists():
        for bp in WEBGL.rglob(name): build_copy.append(rel(bp))
    likely_in_data = (not in_streaming(x['path'])) and bool(refs)
    video_rows.append({
        'Full asset path':x['path'],'File size':x['size'],'File size human':bytes_fmt(x['size']),'Codec':vstream.get('codec_name',''),
        'Resolution':f"{vstream.get('width','')}x{vstream.get('height','')}" if vstream else '', 'Frame rate':vstream.get('avg_frame_rate',''),
        'Duration':fmt.get('duration') or vstream.get('duration',''),'Video bitrate':vstream.get('bit_rate') or fmt.get('bit_rate',''),
        'Audio codec':astream.get('codec_name',''),'Audio bitrate':astream.get('bit_rate',''),'Inside StreamingAssets':'Yes' if in_streaming(x['path']) else 'No',
        'Imported as Unity VideoClip':'No/raw copy likely' if in_streaming(x['path']) else 'Yes likely (Unity importable video asset)',
        'VideoPlayer references VideoClip':'; '.join(guid_refs.get(x['path'],[])[:20]),
        'VideoPlayer references URL/custom filename refs':'; '.join(filename_refs.get(name,[])[:20]),
        'Custom script contains filename/path':'; '.join([r for r in filename_refs.get(name,[]) if r.endswith('.cs')][:20]),
        'Similar/duplicate video paths':'; '.join(similar[:20]),'Appears in WebGL StreamingAssets':'Yes' if build_copy else 'No',
        'Likely in webgl.data.br':'Yes' if likely_in_data else 'Unknown/No direct evidence','Video has separate duplicate audio':'; '.join(audio_dups[:20]),
        'Recommended action':'P0: avoid duplicate VideoClip + StreamingAssets inclusion' if likely_in_data and build_copy else ('P1: stream externally or keep only StreamingAssets URL path' if x['size']>50*1024*1024 else 'Review usage'),
        'Risk level':'High' if likely_in_data else 'Moderate'
    })

# Audio audit
audio_ext={'.wav','.mp3','.aac','.m4a','.ogg','.aiff','.aif'}
audio_rows=[]
for x in sorted([a for a in asset_files if a['ext'] in audio_ext], key=lambda r:r['size'], reverse=True):
    data,err=ffprobe_json(x['abs'])
    astream={}; fmt={}
    if data:
        fmt=data.get('format',{}) or {}
        for st in data.get('streams',[]):
            if st.get('codec_type')=='audio': astream=st; break
    mt=meta_text(x['path'])
    load_type=meta_field(mt,'loadType')
    comp=meta_field(mt,'compressionFormat')
    quality=meta_field(mt,'quality')
    stem=Path(x['path']).stem.lower().replace('_',' ').replace('-',' ')
    video_dups=[]
    for y in asset_files:
        if y['ext'] in video_ext:
            vst=Path(y['path']).stem.lower().replace('_',' ').replace('-',' ')
            if stem and (stem in vst or vst in stem): video_dups.append(y['path'])
    audio_rows.append({'Path':x['path'],'File size':x['size'],'File size human':bytes_fmt(x['size']),'Duration':fmt.get('duration') or astream.get('duration',''),
        'Sample rate':astream.get('sample_rate',''),'Channels':astream.get('channels',''),'Codec':astream.get('codec_name',''),'Bitrate':astream.get('bit_rate') or fmt.get('bit_rate',''),
        'Unity load type':load_type,'Compression format':comp,'Quality setting':quality,'Preload audio data':meta_field(mt,'preloadAudioData'),
        'Referencing objects':referenced_by(x['path']),'Duplicated versions/same recording':'','Audio extracted from videos':'; '.join(video_dups[:20]),
        'Long uncompressed WAV flag':'Yes' if x['ext']=='.wav' and x['size']>10*1024*1024 else 'No','Stereo-to-mono candidate':'Yes' if str(astream.get('channels',''))=='2' else 'No',
        'Under Resources/StreamingAssets':'Resources' if '/Resources/' in ('/'+x['path']) else ('StreamingAssets' if in_streaming(x['path']) else ''),
        'Recommended action':'P1: stream/compress long WAV' if x['ext']=='.wav' and x['size']>10*1024*1024 else 'Review import settings','Risk level':'Moderate'})

# Texture audit
tex_ext={'.png','.jpg','.jpeg','.tga','.tif','.tiff','.psd','.exr','.hdr','.bmp','.gif','.webp'}
texture_rows=[]
for x in sorted([a for a in asset_files if a['ext'] in tex_ext], key=lambda r:r['size'], reverse=True):
    info,err=identify_info(x['abs'])
    w=h=0; fmt=channels=''
    if info:
        try: w=int(info[0]); h=int(info[1])
        except: pass
        fmt=info[2]; channels=info[3]
    mt=meta_text(x['path'])
    has_alpha='Yes' if channels and ('a' in channels.lower() or 'srgba' in channels.lower()) else 'Unknown'
    est=w*h*4 if w and h else ''
    refs=referenced_by(x['path'])
    usage='Unknown'
    lp=x['path'].lower()
    if 'lightmap' in lp or 'lightingdata' in lp: usage='Lightmap/baked lighting'
    elif 'normal' in lp or '_n' in lp: usage='Normal map'
    elif 'page_' in lp or 'blackrefractions' in lp or 'gettinghome' in lp or 'tesseract' in lp: usage='Article/document page'
    elif 'panorama' in lp or '360' in lp: usage='Panorama'
    elif 'plaque' in lp or 'canvas' in lp: usage='UI/plaque/canvas'
    elif 'blackkitchen' in lp or '.glb' in lp: usage='Scanned model/environment texture'
    texture_rows.append({'Path':x['path'],'Source file size':x['size'],'Source file size human':bytes_fmt(x['size']),'Width':w,'Height':h,
        'Texture type':meta_field(mt,'textureType'),'Format/source':fmt,'Alpha usage':has_alpha,'Mipmap setting':meta_field(mt,'enableMipMap'),
        'Read/Write setting':meta_field(mt,'isReadable') or meta_field(mt,'readable'),'Compression setting':meta_field(mt,'textureCompression'),
        'Web platform override': 'WebGL' if 'buildTarget: WebGL' in mt else 'None found','Max texture size':meta_field(mt,'maxTextureSize'),
        'Estimated runtime memory':est,'Estimated runtime memory human':bytes_fmt(est) if est else '', 'Referencing assets':refs,'Usage classification':usage,
        'Flags':'; '.join([f for f in [('Large 4096+' if max(w,h)>=4096 else ''),('Huge 8192+' if max(w,h)>=8192 else ''),('Mipmapped document/UI?' if usage in ['Article/document page','UI/plaque/canvas'] and meta_field(mt,'enableMipMap') not in ['0','False','false'] else ''),('ReadWrite enabled' if meta_field(mt,'isReadable')=='1' else '')] if f]),
        'Recommended action':'P1/P2: downscale/compress or externalize' if max(w,h)>=4096 or x['size']>5*1024*1024 else 'Review if referenced','Risk level':'Visual QA required' if refs else 'Unknown/runtime or unused candidate'})

# Mesh/model audit
model_ext={'.fbx','.obj','.glb','.gltf','.blend','.dae','.3ds','.mesh'}
mesh_rows=[]
for x in sorted([a for a in asset_files if a['ext'] in model_ext], key=lambda r:r['size'], reverse=True):
    mt=meta_text(x['path'])
    details=''
    embedded=''
    if x['ext']=='.glb':
        try:
            with open(x['abs'],'rb') as f:
                head=f.read(12)
                # GLB chunks
                chunks=[]
                while True:
                    ch=f.read(8)
                    if len(ch)<8: break
                    ln=int.from_bytes(ch[:4],'little'); typ=ch[4:8].decode('ascii','ignore')
                    chunks.append((typ,ln)); f.seek(ln,1)
                details='; '.join([f'{t}:{l}' for t,l in chunks])
                embedded='Likely embedded buffer/textures' if chunks else ''
        except Exception as e: details=str(e)
    mesh_rows.append({'Path':x['path'],'Source size':x['size'],'Source size human':bytes_fmt(x['size']),'Vertex count':'Requires Unity import audit','Triangle count':'Requires Unity import audit','Submesh count':'Requires Unity import audit','Material count':'Requires Unity import audit','Blend shapes':'Requires Unity import audit','Animation clips':'Requires Unity import audit','Read/Write status':meta_field(mt,'isReadable') or meta_field(mt,'readWriteEnabled') or ('Disabled/unknown' if '_readWriteEnabled: 0' in mt else ('Enabled' if '_readWriteEnabled: 1' in mt else '')),'Mesh compression':meta_field(mt,'meshCompression'),'Imported cameras':meta_field(mt,'importCameras'),'Imported lights':meta_field(mt,'importLights'),'Referencing scenes/prefabs':referenced_by(x['path']),'Collider type':'Search scene for MeshCollider refs','MeshCollider usage':'Requires Unity/scene object dependency audit','Duplicated model hints':'','LOD existence':'Requires Unity audit','Hidden/unused geometry':'Requires Unity audit','Embedded oversized textures':embedded,'GLB chunk details':details,'Recommended action':'P1/P2: inspect high-poly/embedded textures' if x['size']>20*1024*1024 else 'Review','Risk level':'High visual/physics QA' if referenced_by(x['path']) else 'Unknown'})

# Scene audit
scene_rows=[]
class_ids={'20':'Camera','23':'MeshRenderer','137':'SkinnedMeshRenderer','64':'MeshCollider','65':'BoxCollider','135':'SphereCollider','136':'CapsuleCollider','82':'AudioSource','108':'Light','215':'ReflectionProbe','198':'ParticleSystem','218':'Terrain','223':'Canvas','328':'VideoPlayer'}
enabled_scenes=[]
try:
    ebs=(ROOT/'ProjectSettings/EditorBuildSettings.asset').read_text(errors='ignore')
    blocks=re.findall(r'- enabled:\s*1\n\s*path:\s*([^\n]+)', ebs)
    enabled_scenes=blocks
except: pass
for sp in enabled_scenes:
    p=ROOT/sp
    txt=p.read_text(errors='ignore') if p.exists() else ''
    counts=Counter(re.findall(r'^--- !u!(\d+) &', txt, re.M))
    mono_classes=re.findall(r'm_EditorClassIdentifier:\s*([^\n]*)', txt)
    roots=re.search(r'SceneRoots:[\s\S]*?m_Roots:\n((?:\s*- \{fileID: [^\n]+\}\n)+)', txt)
    root_count=len(re.findall(r'- \{fileID:', roots.group(1))) if roots else ''
    disabled=len(re.findall(r'm_IsActive:\s*0', txt))
    missing_scripts=len(re.findall(r'm_Script:\s*\{fileID:\s*0', txt))
    row={'Scene path':sp,'Scene file size':file_size(p),'Scene file size human':bytes_fmt(file_size(p)),'Root object count':root_count,'Total GameObject count':counts.get('1',0),'Disabled GameObject count':disabled,'Renderer count':counts.get('23',0)+counts.get('137',0),'MeshRenderer count':counts.get('23',0),'SkinnedMeshRenderer count':counts.get('137',0),'Collider count':sum(counts.get(k,0) for k in ['64','65','135','136','56']),'MeshCollider count':counts.get('64',0),'AudioSource count':counts.get('82',0),'VideoPlayer count':counts.get('328',0),'Light count':counts.get('108',0),'Reflection probe count':counts.get('215',0),'Realtime light count':'Requires Unity baked mode audit','Particle system count':counts.get('198',0),'Terrain count':counts.get('218',0),'Canvas count':counts.get('223',0),'EventSystem count':sum(1 for c in mono_classes if 'EventSystem' in c),'Camera count':counts.get('20',0),'Duplicate manager objects':'See YAML names manually','Missing scripts':missing_scripts,'Missing references':'Requires Unity validator','Hidden test/backup objects':'Search names in scene','Unused prefabs inactive':'Requires dependency audit','Large assets on inactive objects':'See GHOST candidates','Duplicate persistent systems':'Scene-local audit required'}
    scene_rows.append(row)

# Duplicate files hash, limit to project excluding Library/Temp/.git; hash all <=? include large okay maybe several GB, do it
hash_groups=defaultdict(list)
for x in all_files:
    p=x['abs']
    try:
        h=hashlib.sha256()
        with open(p,'rb') as f:
            for chunk in iter(lambda:f.read(1024*1024), b''):
                h.update(chunk)
        hash_groups[h.hexdigest()].append(x)
    except: pass
dup_rows=[]
for h,items in hash_groups.items():
    if len(items)>1:
        total=sum(i['size'] for i in items)
        for i in items:
            dup_rows.append({'SHA256':h,'Path':i['path'],'Size':i['size'],'Size human':bytes_fmt(i['size']),'Duplicate group count':len(items),'Group total size':total,'Group total human':bytes_fmt(total),'Recommendation':'Likely safe verify in Unity' if i['path'].startswith('Assets/') else 'Filesystem duplicate; verify use','Risk':'High-risk deletion unless unreferenced evidence strong'})

# Resources/Streaming/Addressables
rsa_rows=[]
for d in ASSETS.rglob('*'):
    if d.is_dir() and d.name=='Resources':
        files=[p for p in d.rglob('*') if p.is_file() and not p.name.endswith('.meta')]
        total=sum(file_size(p) for p in files)
        rsa_rows.append({'Path':rel(d),'Kind':'Resources folder','Total size':total,'Total size human':bytes_fmt(total),'File count':len(files),'Used evidence':'All Resources content is build-includable','Recommendation':'P0/P1: remove large non-runtime Resources or load externally','Risk':'High runtime/dependency risk'})
sa=ASSETS/'StreamingAssets'
if sa.exists():
    for p in sa.rglob('*'):
        if p.is_file() and not p.name.endswith('.meta'):
            refs=referenced_by(rel(p))
            rsa_rows.append({'Path':rel(p),'Kind':'StreamingAssets file','Total size':file_size(p),'Total size human':bytes_fmt(file_size(p)),'File count':1,'Used evidence':refs,'Recommendation':'P1: keep only if URL/StreamingAssets runtime path is used; avoid duplicate VideoClip refs','Risk':'Runtime-loading risk'})
# addressables
for p in ASSETS.rglob('*Addressable*'):
    if p.is_file():
        rsa_rows.append({'Path':rel(p),'Kind':'Addressables-related asset','Total size':file_size(p),'Total size human':bytes_fmt(file_size(p)),'File count':1,'Used evidence':'Path/name match','Recommendation':'Inspect Addressables groups','Risk':'Unknown'})

# Package audit
pkg_rows=[]
manifest=json.loads((ROOT/'Packages/manifest.json').read_text())
for name,ver in manifest.get('dependencies',{}).items():
    loc='manifest'
    pkg_rows.append({'Package':name,'Version/source':ver,'Kind':'Package dependency','Potential build impact':'Runtime package' if not any(x in name for x in ['editor','test','rider','visualstudio','vscode','collab']) else 'Editor/test package may not ship but bloats project','Recommendation':'Review if unused in runtime/build' if name in ['com.unity.render-pipelines.high-definition','com.unity.probuilder','com.unity.visualscripting','com.unity.timeline','com.unity.test-framework','com.unity.test-framework.performance','com.unity.package-validation-suite','com.unity.multiplayer.center'] else 'Keep if used','Risk':'Package removal requires Unity QA'})
# Samples
for p in ASSETS.rglob('Samples'):
    if p.is_dir():
        files=[f for f in p.rglob('*') if f.is_file() and not f.name.endswith('.meta')]
        pkg_rows.append({'Package':rel(p),'Version/source':'Imported samples','Kind':'Sample content under Assets','Potential build impact':bytes_fmt(sum(file_size(f) for f in files)),'Recommendation':'P2/P3: remove from build if unreferenced','Risk':'Verify scenes/prefabs do not depend on samples'})

# Largest assets combined for build composition
largest_assets=[]
for x in sorted(asset_files, key=lambda r:r['size'], reverse=True)[:500]:
    if x['path'].endswith('.meta'): continue
    ext=x['ext']
    typ='Texture' if ext in tex_ext else 'Video' if ext in video_ext else 'Audio' if ext in audio_ext else 'Model/Mesh' if ext in model_ext else 'Unity/Other'
    largest_assets.append({'Asset path':x['path'],'Asset type':typ,'Source file size':x['size'],'Source file size human':bytes_fmt(x['size']),'Estimated packed/build size':'Unavailable: buildreport missing','Referencing scene/prefab/component/script/Resources':referenced_by(x['path']),'Also present in StreamingAssets':'Yes' if in_streaming(x['path']) else 'No','Recommended action':'P0/P1 inspect' if x['size']>20*1024*1024 else 'Review','Risk level':'High' if referenced_by(x['path']) else 'Unknown'})

# Ghost candidates
suffix_re=re.compile(r'(copy|backup|old|final2?|test|temp|duplicate|\(\d+\)|bak|archive|_old|_copy)', re.I)
ghost_rows=[]
for x in sorted(asset_files, key=lambda r:r['size'], reverse=True):
    if x['path'].endswith('.meta'): continue
    refs=referenced_by(x['path'])
    category=''
    label=''
    if '/Resources/' in ('/'+x['path']): category='B: indirectly included'; label='Referenced indirectly'
    elif '/StreamingAssets/' in ('/'+x['path']): category='B: indirectly included'; label='Unknown/runtime-loaded'
    elif refs: category='Referenced'; label='Required/Referenced indirectly'
    elif suffix_re.search(x['path']) or x['ext'] in {'.zip','.apk','.mov','.psd','.tif','.tiff','.exr'}: category='C: duplicate/obsolete candidate'; label='Likely safe, verify in Unity'
    else: category='A: no text/GUID refs found'; label='Unknown/runtime-loaded' if x['ext'] in {'.cs','.asmdef','.shader'} else 'Likely safe, verify in Unity'
    if category.startswith('A') or category.startswith('C') or x['size']>2*1024*1024:
        ghost_rows.append({'Path':x['path'],'Size':x['size'],'Size human':bytes_fmt(x['size']),'Category':category,'Label':label,'Reference evidence':refs,'Reason':'No GUID/text reference found by static scan' if not refs else 'Large referenced/indirect asset','Recommendation':'Do not delete until Unity dependency check confirms','Risk':'High-risk deletion' if refs or x['ext'] in {'.cs','.shader','.asset'} else 'Verify in Unity'})

# Build settings md
ps=(ROOT/'ProjectSettings/ProjectSettings.asset').read_text(errors='ignore')
def psval(k):
    m=re.search(r'^\s*'+re.escape(k)+r':\s*(.*)$', ps, re.M); return m.group(1).strip() if m else 'Not found'
settings_keys=['webGLMemorySize','webGLExceptionSupport','webGLDataCaching','webGLDebugSymbols','webGLCompressionFormat','webGLDecompressionFallback','webGLInitialMemorySize','webGLMaximumMemorySize','webGLMemoryGrowthMode','webGLAnalyzeBuildSize','webGLThreadsSupport','webGLWebAssemblyBigInt','webGLTemplate','stripEngineCode','activeInputHandler','usePlayerLog']
settings_md='# Build Settings Audit\n\n| Setting | Value | Size/memory note |\n|---|---:|---|\n'
for k in settings_keys:
    v=psval(k)
    note=''
    if k=='webGLCompressionFormat' and v=='0': note='Likely Brotli; verify in Unity UI.'
    if k=='webGLExceptionSupport' and v!='0': note='Exceptions increase wasm/runtime cost.'
    if k=='webGLInitialMemorySize': note='Large initial heap can affect browser memory.'
    if k=='webGLMaximumMemorySize': note='High maximum heap permits larger memory growth.'
    if k=='webGLAnalyzeBuildSize' and v=='0': note='Enable for future exact build composition reports.'
    settings_md+=f'| `{k}` | `{v}` | {note} |\n'
settings_md+='\nEnabled build scenes:\n\n' + '\n'.join([f'- `{s}`' for s in enabled_scenes]) + '\n\nPackage concern: HDRP is present alongside URP; it may not ship if unused but increases project/package surface.\n'

# Aggregate categories
sum_video=sum(r['size'] for r in [a for a in asset_files if a['ext'] in video_ext])
sum_audio=sum(r['size'] for r in [a for a in asset_files if a['ext'] in audio_ext])
sum_tex=sum(r['size'] for r in [a for a in asset_files if a['ext'] in tex_ext])
sum_model=sum(r['size'] for r in [a for a in asset_files if a['ext'] in model_ext])
streaming_size=sum(file_size(p) for p in (ASSETS/'StreamingAssets').rglob('*') if p.is_file() and not p.name.endswith('.meta')) if (ASSETS/'StreamingAssets').exists() else 0
webgl_size=sum(file_size(p) for p in WEBGL.rglob('*') if p.is_file()) if WEBGL.exists() else 0
webgl_data=file_size(WEBGL/'Build/webgl.data.br')
webgl_stream=sum(file_size(p) for p in (WEBGL/'StreamingAssets').rglob('*') if p.is_file()) if (WEBGL/'StreamingAssets').exists() else 0

# Output CSVs
build_rows += largest_assets[:100]
write_csv('BUILD_SIZE_BREAKDOWN.csv', build_rows, ['Asset path','Asset type','Source file size','Source file size human','Estimated packed/build size','Referencing scene/prefab/component/script/Resources','Also present in StreamingAssets','Recommended action','Risk level'])
write_csv('LARGEST_ASSETS.csv', largest_assets, ['Asset path','Asset type','Source file size','Source file size human','Estimated packed/build size','Referencing scene/prefab/component/script/Resources','Also present in StreamingAssets','Recommended action','Risk level'])
write_csv('VIDEO_AUDIT.csv', video_rows, list(video_rows[0].keys()) if video_rows else ['Full asset path'])
write_csv('AUDIO_AUDIT.csv', audio_rows, list(audio_rows[0].keys()) if audio_rows else ['Path'])
write_csv('TEXTURE_AUDIT.csv', texture_rows, list(texture_rows[0].keys()) if texture_rows else ['Path'])
write_csv('MESH_AUDIT.csv', mesh_rows, list(mesh_rows[0].keys()) if mesh_rows else ['Path'])
write_csv('SCENE_AUDIT.csv', scene_rows, list(scene_rows[0].keys()) if scene_rows else ['Scene path'])
write_csv('DUPLICATE_FILES.csv', dup_rows, list(dup_rows[0].keys()) if dup_rows else ['SHA256','Path'])
write_csv('GHOST_ASSET_CANDIDATES.csv', ghost_rows, list(ghost_rows[0].keys()) if ghost_rows else ['Path'])
write_csv('RESOURCES_STREAMING_ADDRESSABLES.csv', rsa_rows, ['Path','Kind','Total size','Total size human','File count','Used evidence','Recommendation','Risk'])
write_csv('PACKAGE_AUDIT.csv', pkg_rows, ['Package','Version/source','Kind','Potential build impact','Recommendation','Risk'])
write_md('BUILD_SETTINGS_AUDIT.md', settings_md)

# Markdown reports
largest_videos=video_rows[:10]
largest_audio=audio_rows[:10]
largest_tex=texture_rows[:10]
largest_mesh=mesh_rows[:10]

def top_list(rows, pathkey, sizekey='File size', n=10):
    out=[]
    for r in rows[:n]: out.append(f"- `{r.get(pathkey,'')}` — {bytes_fmt(r.get(sizekey,0))}")
    return '\n'.join(out) if out else '- None found'

summary=f'''# Executive Summary\n\nAudit date: generated from local filesystem evidence. This is read-only; no Unity assets were modified by this audit generator.\n\n## Direct Answers\n\n- **Why is `webgl.data.br` 864 MB?** Exact packed attribution requires `LastBuild.buildreport`, but that file was not present at `/mnt/data/LastBuild.buildreport`. Static evidence shows the project contains very large source textures/models/audio plus build scenes that reference broad exhibit content. `webgl.data.br` is the packed Unity data file and is likely dominated by imported textures, meshes/models, audio clips, scenes, and any non-StreamingAssets video imported as VideoClip.\n- **How much of the built WebGL folder is video?** Built `webgl/StreamingAssets` video totals about **{bytes_fmt(webgl_stream)}**. Source video under `Assets` totals **{bytes_fmt(sum_video)}**.\n- **Are videos duplicated between Unity packed data and StreamingAssets?** Critical duplicate inclusion is possible wherever a video exists in `Assets/StreamingAssets` and is also referenced/imported as a VideoClip elsewhere. The static scan flags these in `VIDEO_AUDIT.csv`; exact confirmation inside `webgl.data.br` needs a build report with packed asset entries.\n- **How much space is consumed by textures?** Source image/texture files under `Assets` total **{bytes_fmt(sum_tex)}** before Unity import compression/packing.\n- **How much is consumed by meshes/models?** Source model/mesh files under `Assets` total **{bytes_fmt(sum_model)}** before Unity import processing.\n- **How much is consumed by audio?** Source audio files under `Assets` total **{bytes_fmt(sum_audio)}**.\n- **How much is unused or indirectly included?** `Resources`/`StreamingAssets`/static ghost candidates are listed in `RESOURCES_STREAMING_ADDRESSABLES.csv` and `GHOST_ASSET_CANDIDATES.csv`. Exact safe deletion requires Unity dependency validation.\n\n## Current Build Outputs\n\n- Entire `webgl` folder: **{bytes_fmt(webgl_size)}**\n- `webgl/Build/webgl.data.br`: **{bytes_fmt(webgl_data)}**\n- `webgl/StreamingAssets`: **{bytes_fmt(webgl_stream)}**\n\n## Largest Built StreamingAssets Videos\n\n{top_list([{'Full asset path':str(p.relative_to(ROOT)),'File size':file_size(p)} for p in sorted((WEBGL/'StreamingAssets').glob('*'), key=file_size, reverse=True) if p.is_file()], 'Full asset path')}\n\n## Five Highest-Impact Changes\n\n1. **P0: Eliminate duplicate video inclusion**. Keep exhibit videos either as StreamingAssets/URL runtime files or imported VideoClips, not both. Conservative savings: 50 MB; likely: 150-250 MB; max plausible: 300+ MB.\n2. **P1: Move large long-form videos to remote hosting/CDN for WebGL**. Conservative: 119 MB; likely: 200-248 MB; max: full StreamingAssets video payload. Runtime-loading risk; requires network/offline policy.\n3. **P1/P2: Downscale/compress large document/page textures** such as full-page PNG article images and plaques. Conservative: 50 MB; likely: 150-300 MB; max depends on page count. Requires visual QA.\n4. **P1/P2: Optimize scanned/photogrammetry models and embedded textures**, especially Black Kitchen and package scans. Conservative: 25 MB; likely: 100+ MB; max requires mesh/texture replacement. Requires visual/physics QA.\n5. **P2: Remove unused package demo/sample content from build dependencies and scenes** after dependency proof. Conservative: 10 MB; likely: 50+ MB; max higher if unused prefabs pull dependencies into scenes.\n\n## Estimated Size By Phase\n\n- Phase 0 current: `webgl` about **{bytes_fmt(webgl_size)}**.\n- Phase 1 video duplication/StreamingAssets policy: likely **{bytes_fmt(max(0, webgl_size-200*1024*1024))}**.\n- Phase 2 texture/document compression: likely **{bytes_fmt(max(0, webgl_size-350*1024*1024))}**.\n- Phase 3 model/audio/package cleanup: likely **{bytes_fmt(max(0, webgl_size-450*1024*1024))}**.\n\n## Risk Summary\n\n- Low risk: remove duplicate raw files only after Unity dependency proof; enable future WebGL build-size analysis; externalize videos using existing URL-capable scripts if already supported.\n- Visual QA required: texture downscaling, material/shader simplification, scanned model decimation.\n- Runtime-loading risk: changing StreamingAssets paths, Resources loading, video URL behavior, or scripts that load by string filename.\n- Unity Play readiness after optimization: not ready until a Play Mode route test covers every exhibit/media path affected.\n'''
write_md('EXECUTIVE_SUMMARY.md', summary)

runtime=f'''# Runtime Memory Risks\n\n- Large `webgl.data.br` plus **{bytes_fmt(webgl_stream)}** StreamingAssets video payload increases browser cache/download pressure.\n- Long audio clips with preload/imported compressed settings may allocate at scene startup; see `AUDIO_AUDIT.csv`.\n- Full-page PNG/document textures may decompress to much larger runtime memory than source size; see `TEXTURE_AUDIT.csv` estimated runtime memory.\n- Read/Write-enabled textures/meshes can duplicate CPU/GPU memory. Static flags are in texture/mesh CSVs, but Unity import inspector validation is required.\n- Video scripts should ensure `VideoPlayer.targetTexture`, audio outputs, and clips/URLs are released/cleared when modals close.\n- Disabled scene objects still retain serialized references and can force dependencies into the build. See `SCENE_AUDIT.csv` and ghost candidates.\n'''
write_md('RUNTIME_MEMORY_RISKS.md', runtime)

plan=f'''# Optimization Priority Plan\n\n| Priority | Recommendation | Conservative | Likely | Max plausible | Risk |\n|---|---|---:|---:|---:|---|\n| P0 | Prove and remove duplicate video inclusion between VideoClip imports and StreamingAssets/URL copies | 50 MB | 150-250 MB | 300+ MB | Runtime media path QA |\n| P1 | Host largest WebGL videos remotely or keep only required StreamingAssets | 119 MB | 248 MB | 248+ MB | Network/runtime loading |\n| P1 | Compress/downscale full-page PNG documents, article pages, plaques | 50 MB | 150-300 MB | 400+ MB | Visual QA |\n| P1/P2 | Optimize Black Kitchen/scanned models and embedded textures | 25 MB | 100+ MB | 200+ MB | Visual/physics QA |\n| P2 | Convert long WAVs to streaming compressed audio and remove duplicate extracted audio when video already contains it | 20 MB | 75+ MB | 150+ MB | Narrative/audio QA |\n| P2 | Remove unused package samples/demo content from included scenes/dependencies | 10 MB | 50+ MB | 150+ MB | Dependency QA |\n| P3 | Shader/material cleanup, strip unused variants/features | 5 MB | 20 MB | 50 MB | Rendering QA |\n'''
write_md('OPTIMIZATION_PRIORITY_PLAN.md', plan)

safe=f'''# Safe Cleanup Candidates\n\nNo asset is marked safe to delete solely from this static audit. Use `GHOST_ASSET_CANDIDATES.csv` with these labels:\n\n- `Likely safe, verify in Unity`: no static GUID/text reference found, or filename suggests backup/copy/old/test. Run Unity dependency validation before deleting.\n- `Referenced indirectly`: Resources or StreamingAssets content; not safe to delete without runtime path audit.\n- `Required`: static GUID/text reference found.\n- `Unknown/runtime-loaded`: may be loaded by string, reflection, Resources, StreamingAssets, or package code.\n- `High-risk deletion`: scripts, shaders, referenced assets, imported models/materials.\n\nLargest candidates should be reviewed first in `GHOST_ASSET_CANDIDATES.csv`.\n'''
write_md('SAFE_CLEANUP_CANDIDATES.md', safe)

checklist='''# Manual Unity Checklist\n\n1. Open Build Report Inspector or rebuild WebGL with `webGLAnalyzeBuildSize` enabled and export packed asset data.\n2. For every P0 video row, inspect all `VideoPlayer` components and scripts using that filename.\n3. In Unity Project view, run dependency checks for top `GHOST_ASSET_CANDIDATES.csv` entries before moving/deleting anything.\n4. Inspect each enabled build scene for inactive/backup/test objects that hold large references.\n5. Verify Resources folder contents and remove only after runtime code search confirms no `Resources.Load` dependency.\n6. Inspect texture import settings for top `TEXTURE_AUDIT.csv` rows: max size, mipmaps, alpha, compression, WebGL override, Read/Write.\n7. Inspect audio import settings for top `AUDIO_AUDIT.csv` rows: load type, preload, compression, mono/stereo.\n8. Inspect BlackKitchen GLB/model importer for read/write, mesh compression, embedded textures, cameras/lights, material count.\n9. Run WebGL Play/Build smoke tests for all media exhibits after any optimization.\n10. Compare browser memory and network waterfall before/after each phase.\n'''
write_md('MANUAL_UNITY_CHECKLIST.md', checklist)

limits=f'''# Audit Limitations\n\n- `/mnt/data/LastBuild.buildreport` was not present, so exact packed-size attribution, shader variant sizes, serialized file sizes, and build-report dependency tables could not be extracted.\n- Unity Editor was not launched; exact imported texture formats, mesh vertex counts, triangle counts, material slot counts, and scene missing-reference validation require Unity APIs.\n- Static YAML/GUID scanning can miss runtime string construction, reflection, package internals, and code-generated references.\n- `webgl.data.br` is compressed packed data; without build report or decompression/Unity object table, source-to-packed attribution is estimated.\n- Deletion safety is intentionally conservative; no asset is declared safe to remove without Unity dependency verification.\n'''
write_md('AUDIT_LIMITATIONS.md', limits)

# Commands log
COMMANDS += ['find webgl -maxdepth 3 -type f -exec du -h {} + | sort -hr | head', 'ffprobe per media file', 'identify per texture file', 'sha256 per project file excluding Library/Temp/.git', 'Unity YAML static inspection for scenes/prefabs/assets']
write_md('AUDIT_COMMANDS_RUN.txt', '\n'.join(COMMANDS)+'\n')

print(json.dumps({'report':str(REPORT),'asset_files':len(asset_files),'videos':len(video_rows),'audio':len(audio_rows),'textures':len(texture_rows),'models':len(mesh_rows),'duplicates':len(dup_rows),'ghosts':len(ghost_rows),'webgl_size':webgl_size}, indent=2))
