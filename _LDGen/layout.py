# Level layout on a 2 m node grid. x = 2*(col-15), z = 2*(row-23)
grid={}          # (c,r) -> '#','N' (window),'D' (door) or floor label (lowercase letter)
wallroom={}      # explicit material room for wall nodes
def is_wall(v): return v in ('#','N','D')
def rect(lbl,c0,r0,c1,r1):
    for c in range(c0,c1+1):
        for r in range(r0,r1+1):
            if c in (c0,c1) or r in (r0,r1):
                if not is_wall(grid.get((c,r),'')): grid[(c,r)]='#'
            else: grid[(c,r)]=lbl
def floor(lbl,*nodes):
    for n in nodes: grid[n]=lbl
def wall(*nodes,room=None,kind='#'):
    for n in nodes:
        grid[n]=kind
        if room: wallroom[n]=room

# 01 Hangar (spawn)
rect('a',3,0,13,6)
wall((6,4),(11,4))                       # pillars
# 02 Couloir vitre
for r in range(6,20): wall((4,r)); wall((6,r))
wall((5,19))
for r in range(7,19): floor('b',(5,r))
floor('b',(5,6))                         # hangar exit
# 03 Salle des machines
rect('c',6,8,20,22)
wall((6,10),(6,12),(6,14),kind='N')      # windows corridor -> machines
floor('c',(6,18))                        # entrance
wall((10,12),(16,12),(10,18),(16,18))    # pillars
wall((12,15),(13,15),(14,15))            # central cover
# 04 Armurerie
rect('d',20,8,26,15)
floor('d',(20,11))                       # gap from machines
wall((20,14),kind='N')                   # window armory -> machines
wall((23,11),(23,12))                    # small cover
# 04b Placard secret behind a door
rect('s',26,10,28,12)
wall((26,11),kind='D')
# 05 Gauntlet
for r in range(22,33): wall((11,r)); wall((15,r))
for r in range(23,32):
    for c in (12,13,14): floor('e',(c,r))
floor('e',(13,22))                       # entrance from machines
wall((12,25),(13,25))                    # stub from west
wall((13,28),(14,28))                    # stub from east
# 06 Reacteur
rect('f',5,32,25,46)
for c in (12,13,14): floor('f',(c,32))   # wide entrance
for c in range(13,18): wall((c,39),room='g')
for r in range(37,42): wall((15,r),room='g')
wall((9,35),(21,35),(9,43),(21,43))      # pillars
for r in range(38,41): wall((8,r)); wall((22,r))

ROOMS={ # label: (group name, main rgb, accent rgb, accent emission mult, floor rgb)
 'a':('01_Hangar',          (0.13,0.16,0.21),(0.20,0.85,1.00),2.0,(0.09,0.11,0.14)),
 'b':('02_Couloir_Vitre',   (0.36,0.39,0.44),(0.85,0.92,1.00),1.6,(0.17,0.18,0.20)),
 'c':('03_Salle_Machines',  (0.30,0.19,0.12),(1.00,0.42,0.05),2.5,(0.15,0.11,0.08)),
 'd':('04_Armurerie',       (0.15,0.20,0.13),(0.30,1.00,0.30),2.0,(0.09,0.12,0.09)),
 's':('04b_Placard_Secret', (0.22,0.18,0.08),(1.00,0.80,0.20),3.0,(0.16,0.13,0.06)),
 'e':('05_Gauntlet',        (0.17,0.11,0.23),(0.90,0.20,1.00),2.5,(0.10,0.07,0.13)),
 'f':('06_Reacteur',        (0.19,0.07,0.07),(1.00,0.10,0.06),3.0,(0.11,0.06,0.06)),
 'g':('06_Reacteur',        (0.05,0.05,0.06),(1.00,0.85,0.30),4.0,(0.11,0.06,0.06)),
}
ORDER='abcdsefg'
LIGHTS={ # room: list of (col,row,range,intensity)
 'a':[(8,3,12,4)],
 'b':[(5,9,7,2.5),(5,16,7,2.5)],
 'c':[(9,11,10,4),(17,11,10,4),(13,19,10,4)],
 'd':[(23,10,8,3.5)],
 's':[(27,11,4,3)],
 'e':[(13,24,7,3),(13,30,7,3)],
 'f':[(9,39,11,4),(21,39,11,4),(15,35,10,4),(15,44,10,4)],
 'g':[(15,39,6,6)],
}
CRATES={'c':[(8,10),(18,20),(13,11),(13,19),(18,15),(8,21)],
        'd':[(25,9)], 'e':[(12,30),(14,23)],
        'f':[(12,35),(18,35),(12,43),(18,43),(7,45),(23,33)]}
ENEMIES=[('mele',9,16),('mele',17,16),('range',13,20),('range',18,10),
         ('range',24,13),('mele',13,30),
         ('mele',10,40),('mele',20,40),('range',11,44),('range',19,44),('range',15,34)]
PLAYER=(8,2)
