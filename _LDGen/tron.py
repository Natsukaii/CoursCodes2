# Tron (1982) layer: wall top neons, floor outlines, light-cycle trail, glowing floor decals.
# Re-runnable: removes the previous "Tron" groups before adding the new ones.
import re, os, sys, math, random, uuid
from collections import deque
HERE=os.path.dirname(os.path.abspath(__file__))
exec(open(os.path.join(HERE,'layout.py')).read())
PROJ=sys.argv[1]
SCENE=PROJ+'/Assets/_Content/Scenes/Arena_Ulric.unity'
TEXDIR=PROJ+'/Assets/_Content/Textures/Tron'
MATDIR=PROJ+'/Assets/_Content/Materials/Tron'
PBR='2e86e5b44f6b9284e8a60217cdc2b214'
random.seed(1982)
def W(c,r): return (2.0*(c-15), 2.0*(r-23))
def f(v):
    if abs(v)<1e-9: return '0'
    return ('%.5f'%v).rstrip('0').rstrip('.')
def meta_guid(path,body):
    mp=path+'.meta'
    if os.path.exists(mp): return re.search(r'guid: (\w+)',open(mp).read()).group(1)
    g=uuid.uuid4().hex
    open(mp,'w',newline='\n').write(f"fileFormatVersion: 2\nguid: {g}\n"+body)
    return g
def ensure_dir(d):
    os.makedirs(d,exist_ok=True)
    meta_guid(d,"folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

# ---------------------------------------------------------------- textures
from PIL import Image, ImageDraw, ImageFilter, ImageChops
ensure_dir(PROJ+'/Assets/_Content/Textures'); ensure_dir(TEXDIR)
N=512
def glow(core,radius=7,amount=0.55):
    halo=core.filter(ImageFilter.GaussianBlur(radius)).point(lambda v:int(v*amount))
    return ImageChops.lighter(core,halo)
def tex_circuit():
    im=Image.new('L',(N,N),0); d=ImageDraw.Draw(im); s=32
    d.rectangle([10,10,N-11,N-11],outline=255,width=4)
    for _ in range(16):
        x,y=random.randrange(2,15)*s,random.randrange(2,15)*s
        pts=[(x,y)]; horiz=random.random()<0.5
        for _ in range(random.randint(2,5)):
            L=random.randint(2,6)*s*random.choice([-1,1])
            if horiz: x=min(max(x+L,2*s),N-2*s)
            else: y=min(max(y+L,2*s),N-2*s)
            pts.append((x,y)); horiz=not horiz
        d.line(pts,fill=255,width=5,joint='curve')
        for (px,py) in (pts[0],pts[-1]):
            d.ellipse([px-9,py-9,px+9,py+9],outline=255,width=4)
    return glow(im)
def tex_grid():
    im=Image.new('L',(N,N),0); d=ImageDraw.Draw(im)
    for i in range(1,4):
        p=i*N//4; d.line([(p,16),(p,N-16)],fill=90,width=2); d.line([(16,p),(N-16,p)],fill=90,width=2)
    d.rectangle([14,14,N-15,N-15],outline=200,width=5)
    L=70
    for cx,cy,sx,sy in [(14,14,1,1),(N-15,14,-1,1),(14,N-15,1,-1),(N-15,N-15,-1,-1)]:
        d.line([(cx,cy),(cx+sx*L,cy)],fill=255,width=12); d.line([(cx,cy),(cx,cy+sy*L)],fill=255,width=12)
    d.rectangle([N//2-20,N//2-20,N//2+20,N//2+20],outline=255,width=5)
    return glow(im)
def tex_chevron():
    im=Image.new('L',(N,N),0); d=ImageDraw.Draw(im)
    for i,b in enumerate([255,170,100]):   # top = strongest, points "up" (+V)
        y=110+i*120
        d.line([(110,y+110),(N//2,y),(N-110,y+110)],fill=b,width=26,joint='curve')
    return glow(im,9,0.6)
def tex_ring():
    im=Image.new('L',(N,N),0); d=ImageDraw.Draw(im); c=N//2
    def circ(r,w,fill=255): d.ellipse([c-r,c-r,c+r,c+r],outline=fill,width=w)
    circ(240,6); circ(150,12); circ(95,3,160)
    for a in range(0,360,10):
        ra=math.radians(a); r0,r1=(200,232) if a%30==0 else (214,232)
        d.line([(c+r0*math.cos(ra),c+r0*math.sin(ra)),(c+r1*math.cos(ra),c+r1*math.sin(ra))],fill=255,width=5)
    for a in range(0,360,90):
        ra=math.radians(a+45); d.line([(c+100*math.cos(ra),c+100*math.sin(ra)),(c+145*math.cos(ra),c+145*math.sin(ra))],fill=220,width=6)
    return glow(im)
def tex_hex():
    im=Image.new('L',(N,N),0); d=ImageDraw.Draw(im); r=36; h=r*math.sqrt(3)
    for row in range(-1,int(N/h)+2):
        for col in range(-1,int(N/(1.5*r))+2):
            cx=col*1.5*r; cy=row*h+(h/2 if col%2 else 0)
            pts=[(cx+r*0.9*math.cos(math.radians(60*i)),cy+r*0.9*math.sin(math.radians(60*i))) for i in range(7)]
            b=255 if random.random()<0.12 else 110
            d.line(pts,fill=b,width=3 if b<200 else 6)
    return glow(im)
def tex_hazard():
    im=Image.new('L',(N,N),0); d=ImageDraw.Draw(im)
    for k in range(-N,2*N,96):
        d.polygon([(k,0),(k+48,0),(k+48-N,N),(k-N,N)],fill=210)
    d.rectangle([0,0,N-1,N-1],outline=255,width=10)
    return glow(im,5,0.4)
def tex_glyphs():   # abstract neon signage, columns of made-up glyphs
    im=Image.new('L',(N,N),0); d=ImageDraw.Draw(im); g=48
    for cx in range(40,N-40,112):
        for cy in range(30,N-60,64):
            x0,y0=cx,cy
            for _ in range(random.randint(2,4)):
                a,b=random.randint(0,2),random.randint(0,2); c2,e=random.randint(0,2),random.randint(0,2)
                d.line([(x0+a*g//2,y0+b*g//2),(x0+c2*g//2,y0+e*g//2)],fill=255,width=7)
        d.rectangle([cx-14,20,cx+g+14,N-20],outline=140,width=3)
    return glow(im,8,0.7)
def tex_barcode():
    im=Image.new('L',(N,N),0); d=ImageDraw.Draw(im); x=30
    while x<N-30:
        w=random.choice([4,6,10,16]); d.rectangle([x,90,x+w,N-130],fill=255 if random.random()<0.8 else 120); x+=w+random.choice([6,8,12])
    for i in range(0,N-60,40): d.rectangle([30+i,N-100,30+i+24,N-80],fill=200)
    return glow(im,4,0.35)
def tex_warning():
    im=Image.new('L',(N,N),0); d=ImageDraw.Draw(im); c=N//2
    d.polygon([(c,60),(N-50,N-70),(50,N-70)],outline=255,width=18)
    d.rectangle([c-14,190,c+14,330],fill=255); d.ellipse([c-17,360,c+17,394],fill=255)
    return glow(im)
def tex_number(s):
    from PIL import ImageFont
    im=Image.new('L',(N,N),0); d=ImageDraw.Draw(im)
    try: fnt=ImageFont.load_default(size=300)
    except TypeError: fnt=ImageFont.load_default()
    bb=d.textbbox((0,0),s,font=fnt); d.text(((N-(bb[2]-bb[0]))//2-bb[0],(N-(bb[3]-bb[1]))//2-bb[1]),s,fill=255,font=fnt)
    d.rectangle([20,20,N-21,N-21],outline=180,width=6); d.line([(20,N-60),(N-21,N-60)],fill=120,width=3)
    return glow(im,8,0.6)
TEXMETA="TextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 1\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 8\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  textureType: 0\n  textureShape: 1\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
TEX={}
for name,fn in [('Circuit',tex_circuit),('Grid',tex_grid),('Chevron',tex_chevron),('Ring',tex_ring),('Hex',tex_hex),
                ('Hazard',tex_hazard),('Glyphs',tex_glyphs),('Barcode',tex_barcode),('Warning',tex_warning)]:
    p=f'{TEXDIR}/T_Tron_{name}.png'; fn().save(p)
    TEX[name]=meta_guid(p,TEXMETA)
for i in range(1,7):
    p=f'{TEXDIR}/T_Tron_Num{i:02d}.png'; tex_number(f'{i:02d}').save(p)
    TEX[f'Num{i:02d}']=meta_guid(p,TEXMETA)
DEC_KINDS=[('Circuit',1.3),('Grid',1.1),('Chevron',1.6),('Ring',1.6),('Hex',1.0),('Hazard',1.2),('Glyphs',1.8),('Barcode',1.3),('Warning',1.6)]

# ---------------------------------------------------------------- materials
ensure_dir(PROJ+'/Assets/_Content/Materials'); ensure_dir(MATDIR)
def pbr(name,base,metal,smooth,emis,tex=None):
    tex=tex or {}
    te=''.join(f"    - {t}:\n        m_Texture: {tex.get(t,'{fileID: 0}')}\n        m_Scale: {{x: 1, y: 1}}\n        m_Offset: {{x: 0, y: 0}}\n" for t in ['_AOtex','_BaseTex','_EmissionTex','_MettalicTex','_NormalTex','_SmoothnessTex'])
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {PBR}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords:
  - USE_EMISSION_ON
  m_InvalidKeywords: []
  m_LightmapFlags: 2
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
{te}    m_Ints: []
    m_Floats:
    - _EmissionOn: 1
    - _MetallicStrength: {metal}
    - _NormalStrength: 1
    - _SmoothnessStrength: {smooth}
    m_Colors:
    - _BaseColor: {{r: {base[0]:.4f}, g: {base[1]:.4f}, b: {base[2]:.4f}, a: 1}}
    - _EmissionColor: {{r: {emis[0]:.4f}, g: {emis[1]:.4f}, b: {emis[2]:.4f}, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""
MATMETA="NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
def mat(name,txt):
    p=f'{MATDIR}/{name}.mat'; open(p,'w',encoding='utf-8',newline='\n').write(txt); return meta_guid(p,MATMETA)
COL={'cyan':(0.00,0.55,0.62),'white':(0.45,0.55,0.70),'orange':(0.90,0.42,0.04),'green':(0.22,0.70,0.18),
     'magenta':(0.70,0.04,0.42),'red':(0.75,0.03,0.05),'blue':(0.40,0.08,0.80)}
ROOMCOL={'a':'cyan','b':'magenta','c':'orange','d':'green','s':'orange','e':'blue','f':'red','g':'cyan'}
NEON={};DEC={}
for cn,c in COL.items():
    NEON[cn]=mat(f'M_Tron_Neon_{cn}',pbr(f'M_Tron_Neon_{cn}',tuple(x*0.35 for x in c),0,0.9,tuple(x*2.2 for x in c)))
    for kind,inten in DEC_KINDS:
        t={'_EmissionTex':f'{{fileID: 2800000, guid: {TEX[kind]}, type: 3}}'}
        DEC[(kind,cn)]=mat(f'M_Tron_{kind}_{cn}',pbr(f'M_Tron_{kind}_{cn}',(0.012,0.012,0.016),0.6,0.9,tuple(x*inten for x in c),t))
ROOMNUM={'a':1,'b':2,'c':3,'d':4,'e':5,'f':6}
for k,i in ROOMNUM.items():
    cn=ROOMCOL[k]; c=COL[cn]; t={'_EmissionTex':f'{{fileID: 2800000, guid: {TEX[f"Num{i:02d}"]}, type: 3}}'}
    DEC[(f'Num{i:02d}',cn)]=mat(f'M_Tron_Num{i:02d}',pbr(f'M_Tron_Num{i:02d}',(0.012,0.012,0.016),0.6,0.9,tuple(x*1.8 for x in c),t))

# ---------------------------------------------------------------- scene
txt=open(SCENE,encoding='utf-8').read()
parts=re.split(r'\n(?=--- !u!)',txt); header=parts[0]; docs=[d.rstrip('\n') for d in parts[1:]]
def did(d): return re.match(r'--- !u!\d+ &(-?\d+)',d).group(1)
idx={did(d):d for d in docs}
used={abs(int(k)) for k in idx}
def nid():
    while True:
        v=random.randint(10**15,9*10**18)
        if v not in used: used.add(v); return v
def go_by_name(name):
    for d in docs:
        if d.startswith('--- !u!1 ') and f'\n  m_Name: {name}\n' in d: return did(d)
def tr_of(go): return re.search(r'component: \{fileID: (-?\d+)\}',idx[go]).group(1)
def children(t):
    m=re.search(r'  m_Children:((?:\n  - \{fileID: -?\d+\})*)',idx[t]); return re.findall(r'fileID: (-?\d+)',m.group(1)) if m else []
# remove previous Tron groups
remove=set()
def collect(t):
    remove.add(t); go=re.search(r'm_GameObject: \{fileID: (-?\d+)',idx[t]).group(1); remove.add(go)
    remove.update(re.findall(r'component: \{fileID: (-?\d+)\}',idx[go]))
    for c in children(t): collect(c)
room_tr={}
for k,(nm,*_) in ROOMS.items():
    g=go_by_name(nm); t=tr_of(g); room_tr[k]=t
    for c in children(t):
        cg=re.search(r'm_GameObject: \{fileID: (-?\d+)',idx[c]).group(1)
        if '\n  m_Name: Tron\n' in idx[cg]: collect(c)
docs=[d for d in docs if did(d) not in remove]
idx={did(d):d for d in docs}
for k,t in room_tr.items():
    idx[t]=re.sub(r'\n  - \{fileID: (-?\d+)\}',lambda m:'' if m.group(1) in remove else m.group(0),idx[t])
print('removed',len(remove))
crate_go=go_by_name('Crate_00')
MR_TPL=[idx[c] for c in re.findall(r'component: \{fileID: (-?\d+)\}',idx[crate_go]) if idx[c].startswith('--- !u!23 ')][0]
CUBE='{fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}'
QUAD='{fileID: 10210, guid: 0000000000000000e000000000000000, type: 0}'
new=[]; tron_groups={}
def quat(ex,ey):  # Unity euler (ex,ey,0), order Z X Y  -> q = qy * qx
    hx,hy=math.radians(ex)/2,math.radians(ey)/2
    return (math.sin(hx)*math.cos(hy), math.cos(hx)*math.sin(hy), -math.sin(hx)*math.sin(hy), math.cos(hx)*math.cos(hy))
def obj(name,parent,pos,scale=(1,1,1),euler=(0,0),mesh=None,matg=None,group=False):
    go,t=nid(),nid(); comps=[t]; extra=''
    if mesh:
        mf,mr=nid(),nid(); comps+=[mf,mr]
        extra+=(f"--- !u!33 &{mf}\nMeshFilter:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n"
                f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_Mesh: {mesh}\n")
        r=re.sub(r'^--- !u!23 &-?\d+',f'--- !u!23 &{mr}',MR_TPL)
        r=re.sub(r'm_GameObject: \{fileID: -?\d+\}',f'm_GameObject: {{fileID: {go}}}',r)
        r=re.sub(r'  m_Materials:\n(  - \{.*?\}\n)+',f'  m_Materials:\n  - {{fileID: 2100000, guid: {matg}, type: 2}}\n',r+'\n').rstrip('\n')
        r=re.sub(r'm_CastShadows: \d','m_CastShadows: 0',r)
        extra+=r+'\n'
    q=quat(*euler)
    node=dict(go=go,t=t,children=[])
    comp=''.join(f"  - component: {{fileID: {c}}}\n" for c in comps)
    node['emit']=lambda: (f"--- !u!1 &{go}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n"
        f"  serializedVersion: 6\n  m_Component:\n{comp}  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
        f"--- !u!4 &{t}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n"
        f"  m_GameObject: {{fileID: {go}}}\n  serializedVersion: 2\n  m_LocalRotation: {{x: {f(q[0])}, y: {f(q[1])}, z: {f(q[2])}, w: {f(q[3])}}}\n"
        f"  m_LocalPosition: {{x: {f(pos[0])}, y: {f(pos[1])}, z: {f(pos[2])}}}\n  m_LocalScale: {{x: {f(scale[0])}, y: {f(scale[1])}, z: {f(scale[2])}}}\n  m_ConstrainProportionsScale: 0\n"
        + (f"  m_Children:\n"+''.join(f"  - {{fileID: {c}}}\n" for c in node['children']) if node['children'] else "  m_Children: []\n")
        + f"  m_Father: {{fileID: {parent}}}\n  m_LocalEulerAnglesHint: {{x: {f(euler[0])}, y: {f(euler[1])}, z: 0}}\n" + extra)
    new.append(node); return node
def group(room,sub):
    if room not in tron_groups:
        g=obj('Tron',room_tr[room],(0,0,0)); tron_groups[room]={'_':g}
        idx[room_tr[room]]=idx[room_tr[room]].replace('  m_Children: []\n','  m_Children:\n') if '  m_Children: []' in idx[room_tr[room]] else idx[room_tr[room]]
        idx[room_tr[room]]=re.sub(r'(  m_Children:\n(?:  - \{fileID: -?\d+\}\n)*)',lambda m:m.group(1)+f'  - {{fileID: {g["t"]}}}\n',idx[room_tr[room]],count=1)
    G=tron_groups[room]
    if sub not in G:
        G[sub]=obj(sub,G['_']['t'],(0,0,0)); G['_']['children'].append(G[sub]['t'])
    return G[sub]
def bar(room,name,x0,z0,x1,z1,y,w,h,color):
    g=group(room,name.split('_')[0]); L=math.hypot(x1-x0,z1-z0)
    sx,sz=(L,w) if abs(z1-z0)<1e-6 else (w,L)
    o=obj(name,g['t'],((x0+x1)/2,y,(z0+z1)/2),(sx,h,sz),mesh=CUBE,matg=NEON[color]); g['children'].append(o['t'])
def decal(room,kind,c,r,size,color,yaw=0,y=0.03):
    g=group(room,'Decals'); x,z=W(c,r)
    o=obj(f'Decal_{kind}',g['t'],(x,y,z),(size,size,1),(90,yaw),QUAD,DEC[(kind,color)]); g['children'].append(o['t'])

isw=lambda n: is_wall(grid.get(n,''))
isf=lambda n: n in grid and not is_wall(grid[n])
def wroom(n):
    if n in wallroom: return wallroom[n]
    cnt={}
    for dc in (-1,0,1):
        for dr in (-1,0,1):
            v=grid.get((n[0]+dc,n[1]+dr))
            if v and not is_wall(v): cnt[v]=cnt.get(v,0)+1
    return max(cnt,key=lambda k:(cnt[k],ORDER.index(k))) if cnt else 'a'
# 1) wall tops: merged horizontal / vertical runs, glowing line on top of the walls
TOP=3.52
for horiz in (True,False):
    seen=set()
    for n in sorted(grid):
        if not isw(n) or n in seen: continue
        step=(1,0) if horiz else (0,1)
        prev=(n[0]-step[0],n[1]-step[1])
        if isw(prev): continue
        run=[n]; m=n
        while isw((m[0]+step[0],m[1]+step[1])): m=(m[0]+step[0],m[1]+step[1]); run.append(m)
        seen.update(run)
        if len(run)<2: continue
        rm=max(set(map(wroom,run)),key=lambda k:sum(wroom(x)==k for x in run))
        (x0,z0),(x1,z1)=W(*run[0]),W(*run[-1])
        bar(rm,'WallTop',x0,z0,x1,z1,TOP,0.1,0.03,ROOMCOL[rm])
for n in grid:   # isolated pillars: glowing cap
    if isw(n) and not any(isw((n[0]+a,n[1]+b)) for a,b in ((1,0),(-1,0),(0,1),(0,-1))):
        rm=wroom(n); x,z=W(*n); g=group(rm,'WallTop')
        o=obj('PillarCap',g['t'],(x,TOP,z),(0.5,0.03,0.86),mesh=CUBE,matg=NEON[ROOMCOL[rm]]); g['children'].append(o['t'])
# 2) floor outlines 0.5 m away from the wall faces (rooms, pillars, cover), corners joined
OFF=1.32; segs={}
for n,v in grid.items():
    if is_wall(v): continue
    for d,(dc,dr) in {'N':(0,1),'S':(0,-1),'E':(1,0),'W':(-1,0)}.items():
        if not isw((n[0]+dc,n[1]+dr)): continue
        ax=(1,0) if dr else (0,1)            # direction of the line
        lo,hi=-1.0,1.0
        for sgn in (-1,1):
            nb=(n[0]+sgn*ax[0],n[1]+sgn*ax[1])
            if isw(nb): e=OFF                                # inner corner: meet the perpendicular line
            elif not isw((nb[0]+dc,nb[1]+dr)): e=0.0          # opening (door): stop at the node
            else: e=1.0
            if sgn<0: lo=-e
            else: hi=e
        x,z=W(*n)
        if dr: key=('h',d,round(z+dr*OFF,3),v); a0,a1=x+lo,x+hi
        else:  key=('v',d,round(x+dc*OFF,3),v); a0,a1=z+lo,z+hi
        segs.setdefault(key,[]).append((a0,a1))
for (o,d,line,room),ss in segs.items():
    ss.sort(); merged=[]
    for a0,a1 in ss:
        if merged and a0<=merged[-1][1]+1e-6: merged[-1][1]=max(merged[-1][1],a1)
        else: merged.append([a0,a1])
    for a0,a1 in merged:
        if a1-a0<0.3: continue
        if o=='h': bar(room,'Floor_Line',a0,line,a1,line,0.035,0.06,0.02,ROOMCOL[room])
        else: bar(room,'Floor_Line',line,a0,line,a1,0.035,0.06,0.02,ROOMCOL[room])
# 3) light-cycle trail: shortest path spawn -> reactor core, avoiding crates
blocked={n for v in CRATES.values() for n in v}
start,goal=PLAYER,(15,36)
prev={start:None}; q=deque([start])
while q:
    n=q.popleft()
    if n==goal: break
    for dc,dr in ((0,1),(1,0),(-1,0),(0,-1)):
        m=(n[0]+dc,n[1]+dr)
        if isf(m) and m not in prev and m not in blocked: prev[m]=n; q.append(m)
path=[]; n=goal
while n: path.append(n); n=prev[n]
path.reverse()
corners=[path[0]]
for i in range(1,len(path)-1):
    a,b,c=path[i-1],path[i],path[i+1]
    if (b[0]-a[0],b[1]-a[1])!=(c[0]-b[0],c[1]-b[1]): corners.append(b)
corners.append(path[-1])
for a,b in zip(corners,corners[1:]):
    (x0,z0),(x1,z1)=W(*a),W(*b)
    # extend by half the width so corners close
    if x0==x1: z0,z1=min(z0,z1)-0.09,max(z0,z1)+0.09
    else: x0,x1=min(x0,x1)-0.09,max(x0,x1)+0.09
    bar(grid[a] if grid[a]!='g' else 'f','Trail',x0,z0,x1,z1,0.04,0.18,0.02,'cyan')
# chevrons along the trail in the corridors, pointing where to go
for i in range(2,len(path)-1,3):
    n=path[i]
    if grid[n] not in ('b','e'): continue
    dc,dr=path[i+1][0]-n[0],path[i+1][1]-n[1]
    yaw={(0,1):0,(1,0):90,(0,-1):180,(-1,0):270}[(dc,dr)]
    decal(grid[n],'Chevron',*n,1.6,'cyan',yaw,0.045)
# 4) decals
decal('a','Ring',*PLAYER,2.8,'cyan',0,0.085)        # spawn pad
decal('a','Circuit',11,2,3.4,'cyan'); decal('a','Grid',5,2,2.6,'cyan')
decal('c','Circuit',8,13,3.6,'orange',90); decal('c','Circuit',18,18,3.6,'orange')
decal('c','Grid',13,17,3.2,'orange'); decal('c','Grid',17,9,3.0,'orange')
decal('d','Circuit',22,9,3.0,'green'); decal('s','Ring',27,11,1.8,'green')
decal('f','Ring',15,39,9.5,'green',0,0.025)          # around the reactor core
for c,r in [(7,34),(23,34),(7,44),(23,44)]: decal('f','Grid',c,r,3.2,'red')
for c,r in [(11,37),(19,41)]: decal('f','Circuit',c,r,3.0,'red',90)

# 5) more decals: scattered floor panels in every room
occupied=set(blocked)|{PLAYER,(11,2),(5,2),(8,13),(18,18),(13,17),(17,9),(22,9),(27,11),(7,34),(23,34),(7,44),(23,44),(11,37),(19,41)}
occupied|={(c,r) for c in range(12,19) for r in range(36,43)}      # reactor core ring
for i in range(2,len(path)-1,3): occupied.add(path[i])                # chevrons
ALT=['cyan','magenta','blue','orange']
def pick_col(room): return ROOMCOL[room] if random.random()<0.65 else random.choice([a for a in ALT if a!=ROOMCOL[room]])
FLOORK=['Circuit','Grid','Hex','Hazard','Barcode','Warning','Glyphs','Circuit','Hex']
DENS={'a':0.35,'b':0.35,'c':0.32,'d':0.35,'e':0.4,'f':0.3}
nfloor=0
for room,dens in DENS.items():
    cand=[n for n,v in grid.items() if v==room]; random.shuffle(cand)
    for n in cand:
        if random.random()>dens: continue
        if any((n[0]+a,n[1]+b) in occupied for a in (-1,0,1) for b in (-1,0,1)): continue
        occupied.add(n); kind=random.choice(FLOORK)
        decal(room,kind,*n,random.choice([1.3,1.6,1.9]),pick_col(room),random.choice([0,90,180,270]),0.028+0.001*(nfloor%5))
        nfloor+=1
# 6) wall signage: vertical neon panels on the inner faces of straight walls
def wall_decal(room,kind,wall,d,w,h,y,color):
    g=group(room,'WallSigns'); x,z=W(*wall); dx,dz=d
    yaw={(0,1):180,(0,-1):0,(1,0):270,(-1,0):90}[d]
    o=obj(f'Sign_{kind}',g['t'],(x+dx*0.2,y,z+dz*0.2),(w,h,1),(0,yaw),QUAD,DEC[(kind,color)]); g['children'].append(o['t'])
WALLK=[('Glyphs',1.0,1.8,1.6),('Barcode',1.5,0.7,1.5),('Warning',0.9,0.9,1.6),('Hex',1.7,1.0,1.7),('Hazard',1.9,0.35,0.3),('Glyphs',0.8,1.4,1.8),('Circuit',1.6,1.6,1.6)]
nsign=0; signed=set()
for wnode,v in sorted(grid.items()):
    if v!='#': continue
    for dd in ((0,1),(0,-1),(1,0),(-1,0)):
        fl=(wnode[0]+dd[0],wnode[1]+dd[1])
        if not isf(fl) or grid[fl] not in DENS: continue
        along=(dd[1],dd[0])
        if not (isw((wnode[0]+along[0],wnode[1]+along[1])) and isw((wnode[0]-along[0],wnode[1]-along[1]))): continue  # straight piece only
        if (wnode[0]-along[0],wnode[1]-along[1],dd) in signed or random.random()>0.38: continue
        signed.add((wnode[0],wnode[1],dd)); room=grid[fl]
        kind,w,h,y=random.choice(WALLK)
        wall_decal(room,kind,wnode,dd,w,h,y,pick_col(room)); nsign+=1
# 7) room numbers on the floor, just inside each entrance
for room,n in {'a':(8,4),'b':(5,8),'c':(7,18),'d':(21,11),'e':(13,23),'f':(13,33)}.items():
    i=ROOMNUM[room]; decal(room,f'Num{i:02d}',*n,1.6,ROOMCOL[room],0,0.032)
print('floor decals',nfloor,'wall signs',nsign)

# ---------------------------------------------------------------- write
for k,t in room_tr.items():
    docs=[idx[t] if did(d)==t else d for d in docs]
sr=[d for d in docs if d.startswith('--- !u!1660057539')][0]
others=[d for d in docs if d is not sr]
out=header+'\n'+'\n'.join(others)+'\n'+''.join(n['emit']() for n in new)+sr+'\n'
open(SCENE,'w',encoding='utf-8',newline='\n').write(out)
from collections import Counter
print('objects',len(new),'trail nodes',len(path))
