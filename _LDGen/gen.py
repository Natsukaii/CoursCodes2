import re, math, random, os, sys, uuid
from collections import Counter
HERE=os.path.dirname(os.path.abspath(__file__))
exec(open(os.path.join(HERE,'layout.py')).read())
PROJ=sys.argv[1]
SCENE=PROJ+'/Assets/_Content/Scenes/Arena_Ulric.unity'
MATDIR=PROJ+'/Assets/_Content/Models/Walls/Materials/Rooms'
random.seed(42)
used=set()
def nid():
    while True:
        v=random.randint(10**15, 9*10**18)
        if v not in used: used.add(v); return v
M64=(1<<64)-1
def xor(a,b): return ((a&M64)^(b&M64))&0x7fffffffffffffff
def W(c,r): return (2.0*(c-15), 2.0*(r-23))
def f(v):
    if abs(v)<1e-9: return '0'
    return ('%.5f'%v).rstrip('0').rstrip('.')

PREF={ # name: (guid, PI id inside the variant prefab)
 'Wall_straight':('3b7524f919b3d384eb59e639eb9d1f55',8991855884592983654),
 'Wall_corner':('a017078f32a2c314382f7ece1ecaa822',1863608388112035260),
 'Wall_cross':('6f7c3d28ed0734e4f828e50895cf8a9b',1846018020065760295),
 'Wall_deadEnd':('0f8b0ef3f971ebe4d846f50632e0e370',4988589046398001015),
 'Wall_door':('a6fb413fb2bce224e89bd24011beb66a',5581249679103433985),
 'Wall_pillar':('8e48e6e5f72dbdc4d9fb61ea79bc0541',2233176522868210285),
 'Wall_T':('7b3a50bf75c56cc4f80fea7455a3d31a',5786636455336370576),
 'Wall_window':('ede3e472567c9be489a16812e596d23c',7293993816985104322),
}
FBX_TR=-8679921383154817045; FBX_GO=919132149155446097; FBX_MR=-7511558181221131132
FLOOR_GUID='b3d5e59592bfef844943b9f2e18aabf9'; FLOOR_TR=817991563840346717; FLOOR_GO=3810555960743657273; FLOOR_MR=4683136009322080518

# ---------------- materials
os.makedirs(MATDIR,exist_ok=True)
if not os.path.exists(MATDIR+'.meta'):
    open(MATDIR+'.meta','w',newline='\n').write(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
base=open(PROJ+'/Assets/_Content/Models/Walls/Materials/M_wall_A.mat',encoding='utf-8').read()
def make_mat(name,rgb,emis=None,smooth=0.35,metal=0.0):
    t=base.replace('m_Name: M_wall_A','m_Name: '+name)
    col='{r: %.4f, g: %.4f, b: %.4f, a: 1}'%rgb
    t=re.sub(r'_BaseColor: \{.*?\}','_BaseColor: '+col,t); t=re.sub(r'- _Color: \{.*?\}','- _Color: '+col,t)
    t=re.sub(r'- _Smoothness: [\d.]+','- _Smoothness: %s'%smooth,t); t=re.sub(r'- _Metallic: [\d.]+','- _Metallic: %s'%metal,t)
    if emis:
        t=re.sub(r'_EmissionColor: \{.*?\}','_EmissionColor: {r: %.4f, g: %.4f, b: %.4f, a: 1}'%emis,t)
        t=t.replace('m_ValidKeywords: []','m_ValidKeywords:\n  - _EMISSION').replace('m_LightmapFlags: 4','m_LightmapFlags: 2')
    p=f'{MATDIR}/{name}.mat'
    open(p,'w',encoding='utf-8',newline='\n').write(t)
    mp=p+'.meta'
    if os.path.exists(mp): return re.search(r'guid: (\w+)',open(mp).read()).group(1)
    g=uuid.uuid4().hex
    open(mp,'w',newline='\n').write(f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    return g
MAT={}
for k,(nm,main,acc,ei,fl) in ROOMS.items():
    short=nm.split('_',1)[1]+('_Coeur' if k=='g' else '')
    MAT[k]=dict(main=make_mat(f'M_{short}_Main',main,smooth=0.3,metal=0.2),
                acc=make_mat(f'M_{short}_Neon',acc,tuple(x*ei for x in acc),smooth=0.8),
                floor=make_mat(f'M_{short}_Floor',fl,smooth=0.55,metal=0.1))

# ---------------- auto tiling
DIRS={'N':(0,1),'E':(1,0),'S':(0,-1),'W':(-1,0)}
BASE={'Wall_straight':{'E','W'},'Wall_window':{'E','W'},'Wall_door':{'E','W'},'Wall_corner':{'W','N'},
      'Wall_T':{'E','W','N'},'Wall_cross':{'N','E','S','W'},'Wall_deadEnd':{'S'},'Wall_pillar':set()}
def rot(d,deg):
    x,z=DIRS[d]; a=math.radians(deg)
    X=round(x*math.cos(a)+z*math.sin(a)); Z=round(-x*math.sin(a)+z*math.cos(a))
    return [k for k,v in DIRS.items() if v==(X,Z)][0]
def solve(piece,need):
    for deg in (0,90,180,270):
        if {rot(d,deg) for d in BASE[piece]}==need: return deg
def wallroom_of(n):
    if n in wallroom: return wallroom[n]
    cnt={}; c,r=n
    for dc in (-1,0,1):
        for dr in (-1,0,1):
            v=grid.get((c+dc,r+dr))
            if v and not is_wall(v): cnt[v]=cnt.get(v,0)+(2 if dc==0 or dr==0 else 1)
    return max(cnt,key=lambda k:(cnt[k],ORDER.index(k))) if cnt else 'a'
pieces=[]
for n,v in sorted(grid.items()):
    if not is_wall(v): continue
    c,r=n; need={d for d,(dc,dr) in DIRS.items() if is_wall(grid.get((c+dc,r+dr),''))}
    k=len(need); straight=need in({'N','S'},{'E','W'})
    if v=='N' and straight: piece='Wall_window'
    elif v=='D' and straight: piece='Wall_door'
    elif k==0: piece='Wall_pillar'
    elif k==1: piece='Wall_deadEnd'
    elif k==2: piece='Wall_straight' if straight else 'Wall_corner'
    elif k==3: piece='Wall_T'
    else: piece='Wall_cross'
    deg=solve(piece,need); assert deg is not None,(n,piece,need)
    pieces.append((n,piece,deg,wallroom_of(n)))

# ---------------- floors: one tile per floor node, extended to the wall centre, then merged
tiles=[]
for (c,r),v in grid.items():
    if is_wall(v): continue
    x,z=W(c,r)
    e=lambda dc,dr: 2.0 if is_wall(grid.get((c+dc,r+dr),'')) else 1.0
    tiles.append([v,x-e(-1,0),x+e(1,0),z-e(0,-1),z+e(0,1)])
def merge(ts,axis):
    key=(lambda t:(t[0],t[3],t[4],t[1])) if axis=='x' else (lambda t:(t[0],t[1],t[2],t[3]))
    out=[]
    for t in sorted(ts,key=key):
        p=out[-1] if out else None
        if axis=='x' and p and p[0]==t[0] and p[3]==t[3] and p[4]==t[4] and abs(p[2]-t[1])<1e-6: p[2]=t[2]
        elif axis=='z' and p and p[0]==t[0] and p[1]==t[1] and p[2]==t[2] and abs(p[4]-t[3])<1e-6: p[4]=t[4]
        else: out.append(list(t))
    return out
tiles=merge(merge(tiles,'x'),'z')

# ---------------- YAML helpers
def mod(tid,guid,path,val,ref='{fileID: 0}'):
    return f"    - target: {{fileID: {tid}, guid: {guid}, type: 3}}\n      propertyPath: {path}\n      value: {val}\n      objectReference: {ref}\n"
def tr_mods(tid,guid,pos,deg=0,scale=None):
    a=math.radians(deg); s=''
    for k,v in zip('xyz',pos): s+=mod(tid,guid,'m_LocalPosition.'+k,f(v))
    s+=mod(tid,guid,'m_LocalRotation.w',f(math.cos(a/2)))+mod(tid,guid,'m_LocalRotation.x','0')
    s+=mod(tid,guid,'m_LocalRotation.y',f(math.sin(a/2)))+mod(tid,guid,'m_LocalRotation.z','0')
    s+=mod(tid,guid,'m_LocalEulerAnglesHint.x','0')+mod(tid,guid,'m_LocalEulerAnglesHint.y',f(deg))+mod(tid,guid,'m_LocalEulerAnglesHint.z','0')
    if scale:
        for k,v in zip('xyz',scale): s+=mod(tid,guid,'m_LocalScale.'+k,f(v))
    return s
def prefab_instance(parent_tr,guid,root_tr_src,mods):
    pi=nid(); st=nid()
    doc=(f"--- !u!1001 &{pi}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    serializedVersion: 3\n"
         f"    m_TransformParent: {{fileID: {parent_tr}}}\n    m_Modifications:\n{mods}    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n"
         f"    m_AddedGameObjects: []\n    m_AddedComponents: []\n  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}\n"
         f"--- !u!4 &{st} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: {root_tr_src}, guid: {guid}, type: 3}}\n"
         f"  m_PrefabInstance: {{fileID: {pi}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    return doc,st
def gameobject(name,parent_tr,pos=(0,0,0),comps=()):
    return dict(go=nid(),t=nid(),name=name,parent=parent_tr,pos=pos,children=[],comps=list(comps),extra='')
def node_docs(n):
    comps=''.join(f"  - component: {{fileID: {c}}}\n" for c in [n['t']]+n['comps'])
    ch=''.join(f"  - {{fileID: {c}}}\n" for c in n['children'])
    p=n['pos']
    return (f"--- !u!1 &{n['go']}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n"
            f"  serializedVersion: 6\n  m_Component:\n{comps}  m_Layer: 0\n  m_Name: {n['name']}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n"
            f"  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
            f"--- !u!4 &{n['t']}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n"
            f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {n['go']}}}\n  serializedVersion: 2\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n"
            f"  m_LocalPosition: {{x: {f(p[0])}, y: {f(p[1])}, z: {f(p[2])}}}\n  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n"
            + (f"  m_Children:\n{ch}" if ch else "  m_Children: []\n")
            + f"  m_Father: {{fileID: {n['parent']}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n" + n['extra'])

# ---------------- read scene
txt=open(SCENE,encoding='utf-8').read()
parts=re.split(r'\n(?=--- !u!)',txt)
header=parts[0]; docs=[d.rstrip('\n') for d in parts[1:]]
def did(d): return re.match(r'--- !u!\d+ &(-?\d+)',d).group(1)
for d in docs: used.add(abs(int(did(d))))
idx={did(d):d for d in docs}
def is_stripped(d): return d.split('\n')[0].endswith('stripped')
def children_of(tid):
    d=idx[tid]; m=re.search(r'  m_Children:((?:\n  - \{fileID: -?\d+\})*)',d)
    return re.findall(r'fileID: (-?\d+)',m.group(1)) if m else []
LD_T='1608528057'
assert 'm_Children' in idx[LD_T]
# remove everything currently under LD (old walls, or a previous run of this script)
remove=set()
def collect(tid):
    for c in children_of(tid):
        remove.add(c); d=idx[c]
        if is_stripped(d):
            pi=re.search(r'm_PrefabInstance: \{fileID: (-?\d+)',d).group(1); remove.add(pi)
        else:
            collect(c)
            go=re.search(r'm_GameObject: \{fileID: (-?\d+)',d).group(1); remove.add(go)
            remove.update(re.findall(r'component: \{fileID: (-?\d+)\}',idx[go]))
collect(LD_T)
for d in docs:
    m=re.search(r'm_PrefabInstance: \{fileID: (-?\d+)\}',d)
    if is_stripped(d) and m and m.group(1) in remove: remove.add(did(d))
# original crates + root enemies get replaced by the LD ones
drop_roots=set()
for d in docs:
    if d.startswith('--- !u!1001') and 'm_TransformParent: {fileID: 0}' in d and re.search(r'propertyPath: m_Name\n\s+value: Ennemie(Mele|Range)\n',d):
        remove.add(did(d)); drop_roots.add(did(d))
crate_src=None
for d in docs:
    if d.startswith('--- !u!1 ') and re.search(r'm_Name: Destructibles_0\d\n',d):
        ids=[did(d)]+re.findall(r'component: \{fileID: (-?\d+)\}',d)
        if 'Destructibles_01\n' in d: crate_src=ids
        tr=ids[1]
        if re.search(r'm_Father: \{fileID: 0\}',idx[tr]): drop_roots.add(tr)
        remove.update(ids)
for d in docs:
    m=re.search(r'm_PrefabInstance: \{fileID: (-?\d+)\}',d)
    if is_stripped(d) and m and m.group(1) in remove: remove.add(did(d))
assert crate_src, 'Destructibles_01 not found'
templates={i:idx[i] for i in crate_src}
EN_SRC={}
for d in docs:
    if d.startswith('--- !u!1001'):
        nm=re.search(r'propertyPath: m_Name\n\s+value: (Ennemie(?:Mele|Range))',d)
        if nm:
            g=re.search(r'm_SourcePrefab: \{fileID: \d+, guid: (\w+)',d).group(1)
            tp=re.search(r'target: \{fileID: (-?\d+), guid: \w+, type: 3\}\n\s+propertyPath: m_LocalPosition.x',d).group(1)
            tn=re.search(r'target: \{fileID: (-?\d+), guid: \w+, type: 3\}\n\s+propertyPath: m_Name',d).group(1)
            EN_SRC['mele' if 'Mele' in nm.group(1) else 'range']=(g,tp,tn)
assert len(EN_SRC)==2, EN_SRC
docs=[d for d in docs if did(d) not in remove]
print('removed docs:',len(remove))

# ---------------- build
nodes=[]; new_docs=[]; ld_children=[]; groups={}
def group(room,sub):
    rn=ROOMS[room][0]
    if (rn,None) not in groups:
        n=gameobject(rn,LD_T); nodes.append(n); ld_children.append(n['t']); groups[(rn,None)]=n
    if (rn,sub) not in groups:
        par=groups[(rn,None)]; n=gameobject(sub,par['t']); nodes.append(n); par['children'].append(n['t']); groups[(rn,sub)]=n
    return groups[(rn,sub)]
for (c,r),piece,deg,room in pieces:
    guid,vpi=PREF[piece]; g=group(room,'Walls'); x,z=W(c,r)
    rt=xor(FBX_TR,vpi); mr=xor(FBX_MR,vpi); go=xor(FBX_GO,vpi)
    m=tr_mods(rt,guid,(x,0,z),deg)+mod(go,guid,'m_Name',f'{piece}_{c}_{r}')
    m+=mod(mr,guid,'m_Materials.Array.data[0]','',f"{{fileID: 2100000, guid: {MAT[room]['acc']}, type: 2}}")
    m+=mod(mr,guid,'m_Materials.Array.data[1]','',f"{{fileID: 2100000, guid: {MAT[room]['main']}, type: 2}}")
    doc,st=prefab_instance(g['t'],guid,rt,m); new_docs.append(doc); g['children'].append(st)
for i,(room,x0,x1,z0,z1) in enumerate(tiles):
    g=group(room,'Floor')
    m=tr_mods(FLOOR_TR,FLOOR_GUID,((x0+x1)/2,0.02,(z0+z1)/2),0,((x1-x0)/2,1,(z1-z0)/2))
    m+=mod(FLOOR_GO,FLOOR_GUID,'m_Name',f'Floor_{i:02d}')
    m+=mod(FLOOR_MR,FLOOR_GUID,'m_Materials.Array.data[0]','',f"{{fileID: 2100000, guid: {MAT[room]['floor']}, type: 2}}")
    doc,st=prefab_instance(g['t'],FLOOR_GUID,FLOOR_TR,m); new_docs.append(doc); g['children'].append(st)
light_tpl=idx['410087040']; urp_tpl=idx['410087042']
for room,ls in LIGHTS.items():
    g=group(room,'Lights'); acc=ROOMS[room][2]
    for j,(c,r,rng,inten) in enumerate(ls):
        x,z=W(c,r); lid=nid(); uid=nid()
        n=gameobject(f'PointLight_{j}',g['t'],(x,3.0,z),[lid,uid])
        L=re.sub(r'^--- !u!108 &\d+',f'--- !u!108 &{lid}',light_tpl)
        L=L.replace('m_GameObject: {fileID: 410087039}',f"m_GameObject: {{fileID: {n['go']}}}")
        L=re.sub(r'\n  m_Type: 1\n','\n  m_Type: 2\n',L,count=1)
        L=re.sub(r'\n  m_Color: \{.*?\}','\n  m_Color: {r: %.3f, g: %.3f, b: %.3f, a: 1}'%acc,L,count=1)
        L=re.sub(r'm_Intensity: [\d.]+',f'm_Intensity: {inten}',L); L=re.sub(r'm_Range: [\d.]+',f'm_Range: {rng}',L)
        L=re.sub(r'(m_Shadows:\n    m_Type:) \d',r'\1 0',L).replace('m_UseColorTemperature: 1','m_UseColorTemperature: 0')
        U=re.sub(r'^--- !u!114 &\d+',f'--- !u!114 &{uid}',urp_tpl).replace('m_GameObject: {fileID: 410087039}',f"m_GameObject: {{fileID: {n['go']}}}")
        n['extra']=L+'\n'+U+'\n'; nodes.append(n); g['children'].append(n['t'])
src_go,src_tr=crate_src[0],crate_src[1]
for room,cl in CRATES.items():
    g=group(room,'Props')
    for j,(c,r) in enumerate(cl):
        x,z=W(c,r); remap={s:str(nid()) for s in crate_src}; ang=math.radians(random.choice([0,10,20,35,45]))
        out=[]
        for s in crate_src:
            d=templates[s]
            d=re.sub(r'^(--- !u!\d+ &)(-?\d+)',lambda m:m.group(1)+remap[m.group(2)],d)
            d=re.sub(r'fileID: (-?\d+)\}',lambda m:'fileID: '+remap.get(m.group(1),m.group(1))+'}',d)
            if s==src_go: d=re.sub(r'm_Name: Destructibles_01',f'm_Name: Crate_{j:02d}',d)
            if s==src_tr:
                d=re.sub(r'm_LocalPosition: \{.*?\}','m_LocalPosition: {x: %s, y: 0.79, z: %s}'%(f(x),f(z)),d)
                d=re.sub(r'm_LocalRotation: \{.*?\}','m_LocalRotation: {x: 0, y: %s, z: 0, w: %s}'%(f(math.sin(ang/2)),f(math.cos(ang/2))),d)
                d=re.sub(r'm_LocalEulerAnglesHint: \{.*?\}','m_LocalEulerAnglesHint: {x: 0, y: %s, z: 0}'%f(math.degrees(ang)),d)
                d=re.sub(r'm_Father: \{fileID: -?\d+\}',f"m_Father: {{fileID: {g['t']}}}",d)
            out.append(d)
        new_docs.append('\n'.join(out)+'\n'); g['children'].append(remap[src_tr])
eg=gameobject('Enemies',LD_T); nodes.append(eg); ld_children.append(eg['t'])
for j,(kind,c,r) in enumerate(ENEMIES):
    g,tp,tn=EN_SRC[kind]; x,z=W(c,r)
    m=tr_mods(tp,g,(x,0,z),180)+mod(tn,g,'m_Name',('EnnemieMele' if kind=='mele' else 'EnnemieRange')+f'_{j:02d}')
    doc,st=prefab_instance(eg['t'],g,tp,m); new_docs.append(doc); eg['children'].append(st)

# ---------------- patch existing objects
def patch(i,fn):
    for k,d in enumerate(docs):
        if did(d)==i: docs[k]=fn(d); return
    raise KeyError(i)
patch(LD_T,lambda d: re.sub(r'  m_Children:(?:(?:\n  - \{fileID: -?\d+\})+| \[\])','  m_Children:'+''.join(f'\n  - {{fileID: {c}}}' for c in ld_children),d))
px,pz=W(*PLAYER)
def player(d):
    d=re.sub(r'(propertyPath: m_LocalPosition.x\n\s+value: )\S+',lambda m:m.group(1)+f(px),d,count=1)
    d=re.sub(r'(propertyPath: m_LocalPosition.z\n\s+value: )\S+',lambda m:m.group(1)+f(pz),d,count=1)
    return d
patch('6146035290072368406',player)
cube_go=[did(d) for d in docs if d.startswith('--- !u!1 ') and '\n  m_Name: Cube\n' in d][0]
cube_tr=[did(d) for d in docs if d.startswith('--- !u!4 ') and f'm_GameObject: {{fileID: {cube_go}}}' in d][0]
patch(cube_tr,lambda d: re.sub(r'm_LocalPosition: \{.*?\}','m_LocalPosition: {x: %s, y: 0.03, z: %s}'%(f(px),f(pz)),d))
patch('410087040',lambda d: re.sub(r'm_Intensity: [\d.]+','m_Intensity: 1.2',d))
sr=[did(d) for d in docs if d.startswith('--- !u!1660057539')][0]
def roots(d):
    for i in drop_roots: d=d.replace(f'\n  - {{fileID: {i}}}','')
    return d
patch(sr,roots)
# ---------------- write
new_text=''.join(node_docs(n) for n in nodes)+''.join(new_docs)
sr_doc=[d for d in docs if did(d)==sr][0]
others=[d for d in docs if did(d)!=sr]
out=header+'\n'+'\n'.join(others)+'\n'+new_text+sr_doc+'\n'
open(SCENE,'w',encoding='utf-8',newline='\n').write(out)
# sanity: dangling references to removed ids
dang=[i for i in remove if re.search(r'fileID: %s\}'%i,out)]
print('dangling refs:',dang)
print('walls',len(pieces),'floor tiles',len(tiles),'lights',sum(map(len,LIGHTS.values())),'crates',sum(map(len,CRATES.values())),'enemies',len(ENEMIES))
print(Counter(p for _,p,_,_ in pieces))
