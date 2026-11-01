# 흰 두루마기(짧은 기장, 넓은 소매, 트인 앞섶, 붉은 단) 절차 생성 — 뼈에 스킨
import bpy, bmesh, math
from mathutils import Vector

def mat(name, rgb):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = (*[c / 255 for c in rgb], 1); return m

def bone(arm, n): return arm.matrix_world @ arm.data.bones[n].head_local

def body_extent(body, z, xlim=0.21):
    mw = body.matrix_world
    vs = [mw @ v.co for v in body.data.vertices if abs((mw @ v.co).z - z) < 0.025 and abs((mw @ v.co).x) < xlim]
    if not vs: return None
    return max(abs(v.x) for v in vs), min(v.y for v in vs), max(v.y for v in vs)

def add_obj(name, bm, arm, groups):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    o.parent = arm
    for p in me.polygons: p.use_smooth = True
    vg = {}
    for vi, ws in groups.items():
        for g, w in ws.items():
            if w <= 0: continue
            if g not in vg: vg[g] = o.vertex_groups.new(name=g)
            vg[g].add([vi], w, 'REPLACE')
    m = o.modifiers.new('arm', 'ARMATURE'); m.object = arm
    return o

def torso(arm, body, white, red):
    zs = [1.36, 1.31, 1.25, 1.19, 1.12, 1.05, 0.98, 0.91, 0.84, 0.77]
    hip = bone(arm, 'J_Bip_C_Hips').z
    N = 32; bm = bmesh.new(); grid = []; groups = {}
    rows = []
    for i, z in enumerate(zs):
        e = body_extent(body, z) or body_extent(body, z + 0.03)
        rx, y0, y1 = e
        t = max(0.0, (hip - z) / (hip - zs[-1]))      # 엉덩이 아래로 퍼짐
        m = 0.035 + 0.05 * t
        if i == 0: rx, y0, y1 = 0.15, -0.08, 0.09     # 어깨선
        rows.append((z, rx + m, y0 - m, y1 + m, t))
    # 앞(+Y) 트임: 위는 좁게, 아래로 갈수록 넓게
    for (z, rx, y0, y1, t) in rows:
        gap = math.radians(14 + 34 * min(1, max(0, (1.30 - z) / 0.5)))
        cy, ry = (y0 + y1) / 2, (y1 - y0) / 2
        row = []
        for k in range(N + 1):
            a = math.pi / 2 + gap + k * (2 * math.pi - 2 * gap) / N   # +Y 방향 = 90°
            v = bm.verts.new((math.cos(a) * rx, cy + math.sin(a) * ry, z)); row.append(v)
        grid.append(row)
    bm.verts.index_update()
    for r in range(len(grid) - 1):
        for k in range(N):
            f = bm.faces.new((grid[r][k], grid[r + 1][k], grid[r + 1][k + 1], grid[r][k + 1]))
            f.material_index = 1 if (r == len(grid) - 2 or k in (0, N - 1) or r == 0) else 0
    for r, row in enumerate(grid):
        z = rows[r][0]
        for v in row:
            g = {}
            if z > 1.25: g['J_Bip_C_UpperChest'] = 1
            elif z > 1.12: g['J_Bip_C_Chest'] = 1
            elif z > 1.0: g['J_Bip_C_Spine'] = 1
            else:
                t = min(1, (1.0 - z) / 0.23); side = 'L' if v.co.x < 0 else 'R'
                wl = 0.6 * t * min(1, abs(v.co.x) / 0.12)
                g['J_Bip_C_Hips'] = 1 - wl; g[f'J_Bip_{side}_UpperLeg'] = wl
            groups[v.index] = g
    o = add_obj('Coat_Body', bm, arm, groups); o.data.materials.append(white); o.data.materials.append(red)
    return o

def sleeve(arm, side, white, red):
    sh = bone(arm, f'J_Bip_{side}_UpperArm'); el = bone(arm, f'J_Bip_{side}_LowerArm'); hd = bone(arm, f'J_Bip_{side}_Hand')
    sgn = 1 if hd.x > 0 else -1
    x0 = sh.x - sgn * 0.02; x1 = hd.x - sgn * 0.015
    R, N = 9, 18; bm = bmesh.new(); grid = []; groups = {}
    for r in range(R + 1):
        t = r / R; x = x0 + (x1 - x0) * t
        rad = 0.07 + 0.07 * t ** 1.5
        row = []
        for k in range(N):
            a = 2 * math.pi * k / N
            dy, dz = math.cos(a) * rad, math.sin(a) * rad
            if dz < 0: dz *= 1 + 1.0 * t ** 1.5        # 아래로 늘어지는 소매 자락
            row.append(bm.verts.new((x, sh.y + dy, sh.z + dz)))
        grid.append(row)
    bm.verts.index_update()
    for r in range(R):
        for k in range(N):
            q = (grid[r][k], grid[r + 1][k], grid[r + 1][(k + 1) % N], grid[r][(k + 1) % N])
            f = bm.faces.new(q)
            f.material_index = 1 if r == R - 1 else 0
    for r, row in enumerate(grid):
        x = row[0].co.x; t = (x - sh.x) / (el.x - sh.x)
        for v in row:
            if t < 0.85: groups[v.index] = {f'J_Bip_{side}_UpperArm': 1}
            elif t < 1.15: groups[v.index] = {f'J_Bip_{side}_UpperArm': 0.5, f'J_Bip_{side}_LowerArm': 0.5}
            else: groups[v.index] = {f'J_Bip_{side}_LowerArm': 1}
    o = add_obj(f'Coat_Sleeve_{side}', bm, arm, groups); o.data.materials.append(white); o.data.materials.append(red)
    return o

def sneaker(arm, side, white, red, black):
    """발목까지 오는 두툼한 운동화(어글리 슈즈): 흰 갑피, 검은 밑창, 붉은 발목 띠, 검은 옆 패널"""
    F = bone(arm, f'J_Bip_{side}_Foot'); T = bone(arm, f'J_Bip_{side}_ToeBase')
    cx = F.x; fwd = 1 if T.y > F.y else -1
    y_heel = F.y - fwd * 0.075; y_toe = T.y + fwd * 0.085
    W = 0.043                                              # 반폭(두 발이 붙지 않게)
    # 옆에서 본 단면(뒤꿈치→발끝): (y, 바닥 z, 윗면 z)
    prof = [(0.0, 0.0, 0.175), (0.18, 0.0, 0.185), (0.45, 0.0, 0.12), (0.7, 0.0, 0.09), (0.9, 0.005, 0.075), (1.0, 0.02, 0.055)]
    bm = bmesh.new(); rings = []
    for t, z0, z1 in prof:
        y = y_heel + (y_toe - y_heel) * t
        w = W * (0.9 if t in (0.0, 1.0) else 1.0)
        # 단면 8점: 밑창 두께 0.035
        pts = [(-w, z0), (-w, z0 + 0.035), (-w * 1.02, (z0 + z1) / 2), (-w * 0.8, z1), (w * 0.8, z1), (w * 1.02, (z0 + z1) / 2), (w, z0 + 0.035), (w, z0)]
        rings.append([bm.verts.new((cx + px, y, pz)) for px, pz in pts])
    bm.verts.index_update(); n = len(rings[0])
    def face(vs, mi):
        f = bm.faces.new(vs if fwd > 0 else vs[::-1]); f.material_index = mi
    for r in range(len(rings) - 1):
        a, b = rings[r], rings[r + 1]
        for k in range(n):
            k2 = (k + 1) % n
            t0 = prof[r][0]
            if k in (0, 6, 7): mi = 2                                  # 밑창
            elif k in (1, 5) and 0.3 < t0 < 0.75: mi = 2                # 옆 검은 패널
            elif k == 3 and t0 < 0.3: mi = 1                            # 발목 윗단 빨강
            elif k in (2, 4) and t0 < 0.1: mi = 1                       # 뒤꿈치 탭 빨강
            else: mi = 0
            face((a[k], b[k], b[k2], a[k2]), mi)
    face(rings[0][::-1], 0); face(rings[-1], 0)
    groups = {}
    for v in bm.verts:
        t = (v.co.y - y_heel) / (y_toe - y_heel)
        wt = min(1.0, max(0.0, (t - 0.55) / 0.3))
        groups[v.index] = {f'J_Bip_{side}_Foot': 1 - wt, f'J_Bip_{side}_ToeBase': wt}
    o = add_obj(f'Sneaker_{side}', bm, arm, groups)
    for m in (white, red, black): o.data.materials.append(m)
    for p in o.data.polygons: p.use_smooth = False
    return o

def apply():
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE'); body = bpy.data.objects['Body']
    white = mat('Coat_White', (240, 238, 234)); red = mat('Coat_Red', (190, 30, 46))
    torso(arm, body, white, red); sleeve(arm, 'L', white, red); sleeve(arm, 'R', white, red)
    black = mat('Shoe_Black', (26, 24, 30)); swhite = mat('Shoe_White', (242, 240, 236)); sred = mat('Shoe_Red', (196, 32, 48))
    sneaker(arm, 'L', swhite, sred, black); sneaker(arm, 'R', swhite, sred, black)
