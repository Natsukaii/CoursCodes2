# "Night City" pass: dark wet materials, night lighting, fog, post-process profile, reflection probes.
# Re-runnable (every value is set from scratch).
import re, os, sys, math, random, uuid
HERE=os.path.dirname(os.path.abspath(__file__))
exec(open(os.path.join(HERE,'layout.py')).read())
PROJ=sys.argv[1]; A=PROJ+'/Assets/'
SCENE=A+'_Content/Scenes/Arena_Ulric.unity'
PBR='2e86e5b44f6b9284e8a60217cdc2b214'
random.seed(2077)
def W(c,r): return (2.0*(c-15), 2.0*(r-23))
def f(v):
    if abs(v)<1e-9: return '0'
    return ('%.5f'%v).rstrip('0').rstrip('.')

# ------------------------------------------------------------ 1. materials
PAL={'a':(0.00,0.55,0.62),'b':(0.70,0.04,0.42),'c':(0.90,0.42,0.04),'d':(0.22,0.70,0.18),
     's':(0.85,0.55,0.08),'e':(0.40,0.08,0.80),'f':(0.75,0.03,0.05),'g':(0.15,0.75,0.85)}
WALL={'a':(0.065,0.085,0.095),'b':(0.085,0.060,0.085),'c':(0.105,0.085,0.070),'d':(0.065,0.085,0.065),
      's':(0.120,0.095,0.055),'e':(0.065,0.060,0.100),'f':(0.095,0.050,0.050),'g':(0.030,0.030,0.035)}
NAMES={'a':'Hangar','b':'Couloir_Vitre','c':'Salle_Machines','d':'Armurerie','s':'Placard_Secret','e':'Gauntlet','f':'Reacteur','g':'Reacteur_Coeur'}
R=A+'_Content/Models/Walls/Materials/Rooms/'; B=A+'_Content/Models/Characters/Bot/Materials/'; M=A+'_Content/Materials/'; WM=A+'_Content/Models/Walls/Materials/'
S={}
for k,n in NAMES.items():
    S[R+f'M_{n}_Main.mat']=(WALL[k],0.85,0.72,None)                      # dark painted metal, a bit wet
    S[R+f'M_{n}_Floor.mat']=((0.022,0.024,0.028),0.55,0.90,None)        # wet floor: mirror-like puddles
    S[R+f'M_{n}_Neon.mat']=(tuple(c*0.35 for c in PAL[k]),0.0,0.9,tuple(c*1.8 for c in PAL[k]))
S.update({
 WM+'M_wall_A.mat':((0.07,0.075,0.085),0.85,0.72,None),
 WM+'M_wall_B.mat':((0.00,0.30,0.35),0.6,0.8,None),
 M+'M_Crate_PBR.mat':((0.09,0.075,0.055),0.7,0.55,None),
 M+'M_SpawnPad_PBR.mat':((0.05,0.055,0.06),0.9,0.85,(0.0,0.20,0.24)),
 M+'M_Projectile_PBR.mat':((0.9,0.3,0.6),0.0,0.9,(3.0,0.25,1.6)),       # hot pink tracer
 M+'M_world.mat':((0.025,0.025,0.03),0.2,0.85,None),                    # wet street
 B+'mat_bodywork_A.mat':((0.80,0.62,0.04),0.6,0.75,None),               # Night City yellow
 B+'mat_bodywork_A1.mat':((0.55,0.03,0.04),0.6,0.65,None),              # Arasaka red
 B+'enemy_bodywork_A 1.mat':((0.55,0.03,0.04),0.6,0.65,None),
 B+'mat_bodywork_B.mat':((0.10,0.11,0.13),0.9,0.6,None),
 B+'mat_head.mat':((0.03,0.035,0.045),0.3,0.95,None),
 B+'mat_eye.mat':(None,0.0,0.92,(0.0,1.6,2.0)),
})
for p,(base,m,sm,em) in S.items():
    t=open(p,encoding='utf-8').read(); assert PBR in t,p
    t=re.sub(r'- _MetallicStrength: [-\d.e]+',f'- _MetallicStrength: {m}',t)
    t=re.sub(r'- _SmoothnessStrength: [-\d.e]+',f'- _SmoothnessStrength: {sm}',t)
    if base: t=re.sub(r'_BaseColor: \{[^}]*\}','_BaseColor: {r: %.4f, g: %.4f, b: %.4f, a: 1}'%base,t)
    if em:
        t=re.sub(r'_EmissionColor: \{[^}]*\}','_EmissionColor: {r: %.4f, g: %.4f, b: %.4f, a: 1}'%em,t)
        t=re.sub(r'- _EmissionOn: \d','- _EmissionOn: 1',t)
        if 'USE_EMISSION_ON' not in t: t=t.replace('m_ValidKeywords: []','m_ValidKeywords:\n  - USE_EMISSION_ON')
    open(p,'w',encoding='utf-8',newline='\n').write(t)
print('materials',len(S))

# ------------------------------------------------------------ 2. post-process profile, own copy for this scene
src=open(A+'_Content/Settings/SampleSceneProfile.asset',encoding='utf-8').read()
VP=A+'_Content/Settings/VP_NightCity.asset'
def setp(t,comp,param,val,state=1):
    i=t.index(f'  m_Name: {comp}\n'); j=t.find('\n--- ',i); j=len(t) if j<0 else j
    blk=t[i:j]; assert f'  {param}:\n' in blk,(comp,param)
    blk=re.sub(rf'(  {param}:\n    m_OverrideState: )\d(\n    m_Value: )[^\n]*',lambda m:m.group(1)+str(state)+m.group(2)+str(val),blk,count=1)
    return t[:i]+blk+t[j:]
t=src.replace('m_Name: SampleSceneProfile','m_Name: VP_NightCity')
t=setp(t,'Bloom','threshold',0.75); t=setp(t,'Bloom','intensity',1.1); t=setp(t,'Bloom','scatter',0.72)
t=setp(t,'Bloom','tint','{r: 1, g: 0.86, b: 0.96, a: 1}')
t=setp(t,'Vignette','intensity',0.4); t=setp(t,'Vignette','smoothness',0.45); t=setp(t,'Vignette','color','{r: 0.02, g: 0, b: 0.04, a: 1}')
t=setp(t,'Tonemapping','mode',2)                                           # ACES
def comp(fid,name,guid,params):
    body=''.join(f"  {k}:\n    m_OverrideState: 1\n    m_Value: {v}\n" for k,v in params)
    return (f"--- !u!114 &{fid}\nMonoBehaviour:\n  m_ObjectHideFlags: 3\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n"
            f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: 0}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n  m_Name: {name}\n  m_EditorClassIdentifier: \n  active: 1\n{body}")
extra=[
 (2077000000000000001,'ColorAdjustments','66f335fb1ffd8684294ad653bf1c7564',[('postExposure',0.15),('contrast',24),('colorFilter','{r: 0.93, g: 0.9, b: 1, a: 1}'),('hueShift',0),('saturation',-6)]),
 (2077000000000000002,'WhiteBalance','221518ef91623a7438a71fef23660601',[('temperature',-22),('tint',14)]),
 (2077000000000000003,'SplitToning','70afe9e12c7a7ed47911bb608a23a8ff',[('shadows','{r: 0.25, g: 0.32, b: 0.55, a: 1}'),('highlights','{r: 0.95, g: 0.55, b: 0.72, a: 1}'),('balance',-25)]),
 (2077000000000000004,'ChromaticAberration','81180773991d8724ab7f2d216912b564',[('intensity',0.12)]),
 (2077000000000000005,'FilmGrain','29fa0085f50d5e54f8144f766051a691',[('type',2),('intensity',0.2),('response',0.8)]),
]
t=t.replace('  components:\n','  components:\n'+''.join(f'  - {{fileID: {fid}}}\n' for fid,*_ in extra),1)
t=t.rstrip('\n')+'\n'+''.join(comp(*e) for e in extra)
open(VP,'w',encoding='utf-8',newline='\n').write(t)
if not os.path.exists(VP+'.meta'):
    open(VP+'.meta','w',newline='\n').write(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
VPG=re.search(r'guid: (\w+)',open(VP+'.meta').read()).group(1)

# ------------------------------------------------------------ 3. scene
txt=open(SCENE,encoding='utf-8').read()
parts=re.split(r'\n(?=--- !u!)',txt); header=parts[0]; docs=[d.rstrip('\n') for d in parts[1:]]
def did(d): return re.match(r'--- !u!\d+ &(-?\d+)',d).group(1)
order=[did(d) for d in docs]; idx={did(d):d for d in docs}
used={abs(int(k)) for k in idx}
def nid():
    while True:
        v=random.randint(10**15,9*10**18)
        if v not in used: used.add(v); return v
def sub(d,key,val): return re.sub(rf'(\n  {key}: )[^\n]*',lambda m:m.group(1)+val,d,count=1)
# render settings: night ambient, purple haze, no daylight sky in the reflections
rs=[k for k,d in idx.items() if d.startswith('--- !u!104 ')][0]; d=idx[rs]
for k,v in [('m_Fog','1'),('m_FogColor','{r: 0.045, g: 0.025, b: 0.07, a: 1}'),('m_FogMode','2'),('m_FogDensity','0.03'),
            ('m_AmbientMode','1'),('m_AmbientSkyColor','{r: 0.045, g: 0.05, b: 0.1, a: 1}'),
            ('m_AmbientEquatorColor','{r: 0.035, g: 0.025, b: 0.06, a: 1}'),('m_AmbientGroundColor','{r: 0.012, g: 0.01, b: 0.016, a: 1}'),
            ('m_SkyboxMaterial','{fileID: 0}'),('m_ReflectionIntensity','0.7')]:
    d=sub(d,k,v)
idx[rs]=d
# moonlight
L='410087040'
d=idx[L]; d=sub(d,'m_Color','{r: 0.5, g: 0.6, b: 1, a: 1}'); d=sub(d,'m_Intensity','0.22'); d=d.replace('m_UseColorTemperature: 1','m_UseColorTemperature: 0'); idx[L]=d
# room lights, matched to the layout by position
tr_of_go={re.search(r'm_GameObject: \{fileID: (-?\d+)',d).group(1):k for k,d in idx.items() if d.startswith('--- !u!4 ') and not d.split('\n')[0].endswith('stripped')}
LPOS={}
for room,ls in LIGHTS.items():
    for (c,r,rng,inten) in ls: LPOS[(round(W(c,r)[0],2),round(W(c,r)[1],2))]=(room,rng)
shadowed=set(); nl=0
for k,d in list(idx.items()):
    if not d.startswith('--- !u!108 ') or k==L: continue
    go=re.search(r'm_GameObject: \{fileID: (-?\d+)',d).group(1)
    p=re.search(r'm_LocalPosition: \{x: ([-\d.e]+), y: [-\d.e]+, z: ([-\d.e]+)\}',idx[tr_of_go[go]])
    room,rng=LPOS.get((round(float(p.group(1)),2),round(float(p.group(2)),2)),('a',8))
    d=sub(d,'m_Color','{r: %.3f, g: %.3f, b: %.3f, a: 1}'%PAL[room])
    d=sub(d,'m_Intensity',f(4.5 if rng<=8 else 6.5)); d=sub(d,'m_Range',f(max(rng*1.35,7)))
    st=2 if room not in shadowed else 0; shadowed.add(room)
    d=re.sub(r'(m_Shadows:\n    m_Type:) \d',rf'\g<1> {st}',d)
    idx[k]=d; nl+=1
print('point lights',nl,'with soft shadows',len(shadowed))
for k,d in idx.items():
    if 'sharedProfile: {fileID: 11400000, guid: ' in d:
        idx[k]=re.sub(r'sharedProfile: \{fileID: 11400000, guid: \w+',f'sharedProfile: {{fileID: 11400000, guid: {VPG}',d)
# reflection probes: realtime, refreshed on awake, box projected, one per room
name_of=lambda go: re.search(r'\n  m_Name: (.*)',idx[go]).group(1)
room_tr={}
for k,(nm,*_) in ROOMS.items():
    if k=='g': continue
    go=[x for x,d in idx.items() if d.startswith('--- !u!1 ') and f'\n  m_Name: {nm}\n' in d][0]
    room_tr[k]=tr_of_go[go]
remove=set()
for k,t in room_tr.items():
    ch=re.search(r'  m_Children:((?:\n  - \{fileID: -?\d+\})*)',idx[t]); ch=re.findall(r'fileID: (-?\d+)',ch.group(1)) if ch else []
    for c in ch:
        if c in idx and not idx[c].split('\n')[0].endswith('stripped'):
            go=re.search(r'm_GameObject: \{fileID: (-?\d+)',idx[c]).group(1)
            if name_of(go)=='ReflectionProbe':
                remove.update([c,go]+re.findall(r'component: \{fileID: (-?\d+)\}',idx[go]))
for k,t in room_tr.items():
    idx[t]=re.sub(r'\n  - \{fileID: (-?\d+)\}',lambda m:'' if m.group(1) in remove else m.group(0),idx[t])
new=''
for k,t in room_tr.items():
    nodes=[n for n,v in grid.items() if v==k]
    xs=[W(*n)[0] for n in nodes]; zs=[W(*n)[1] for n in nodes]
    cx,cz=(min(xs)+max(xs))/2,(min(zs)+max(zs))/2; sx,sz=max(xs)-min(xs)+2,max(zs)-min(zs)+2
    go,tr,rp=nid(),nid(),nid()
    new+=(f"--- !u!1 &{go}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n"
          f"  serializedVersion: 6\n  m_Component:\n  - component: {{fileID: {tr}}}\n  - component: {{fileID: {rp}}}\n  m_Layer: 0\n  m_Name: ReflectionProbe\n"
          f"  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
          f"--- !u!4 &{tr}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n"
          f"  m_GameObject: {{fileID: {go}}}\n  serializedVersion: 2\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {{x: {f(cx)}, y: 1.6, z: {f(cz)}}}\n"
          f"  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {{fileID: {t}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"
          f"--- !u!215 &{rp}\nReflectionProbe:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n"
          f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  serializedVersion: 2\n  m_Type: 0\n  m_Mode: 1\n  m_RefreshMode: 0\n  m_TimeSlicingMode: 2\n"
          f"  m_Resolution: 128\n  m_UpdateFrequency: 0\n  m_BoxSize: {{x: {f(sx)}, y: 4, z: {f(sz)}}}\n  m_BoxOffset: {{x: 0, y: 0.4, z: 0}}\n  m_NearClip: 0.1\n"
          f"  m_FarClip: 100\n  m_ShadowDistance: 40\n  m_ClearFlags: 2\n  m_BackGroundColor: {{r: 0.015, g: 0.01, b: 0.025, a: 0}}\n  m_CullingMask:\n"
          f"    serializedVersion: 2\n    m_Bits: 4294967295\n  m_IntensityMultiplier: 1\n  m_BlendDistance: 1\n  m_HDR: 1\n  m_BoxProjection: 1\n"
          f"  m_RenderDynamicObjects: 1\n  m_UseOcclusionCulling: 1\n  m_Importance: 1\n  m_CustomBakedTexture: {{fileID: 0}}\n")
    idx[t]=re.sub(r'(  m_Children:\n(?:  - \{fileID: -?\d+\}\n)*)',lambda m:m.group(1)+f'  - {{fileID: {tr}}}\n',idx[t],count=1)
docs=[idx[k] for k in order if k not in remove]
sr=[d for d in docs if d.startswith('--- !u!1660057539')][0]
out=header+'\n'+'\n'.join(d for d in docs if d is not sr)+'\n'+new+sr+'\n'
open(SCENE,'w',encoding='utf-8',newline='\n').write(out)
print('probes',len(room_tr),'profile',VPG)
