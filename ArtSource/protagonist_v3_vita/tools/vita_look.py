# VRoid 'Vita'(CC0)를 주인공 외형으로: 분홍 단발, 검은 크롭탑·핫팬츠, 맨다리+흰 양말, 흰 어글리 슈즈, 손가락 장갑
import bpy, bmesh, math, os
import numpy as np
from PIL import Image, ImageDraw
from mathutils import Vector

SKIN = (250, 222, 206); BLACK = (24, 22, 28); WHITE = (238, 236, 232); RED = (196, 32, 48); SOCK = (244, 244, 244)

def img_np(im):
    w, h = im.size; a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    return a
def np_img(im, a): im.pixels[:] = a.ravel(); im.update(); im.pack()

def tex_of(mat):
    return next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image)

def label_mask(obj, mat_index, img, rule):
    """3D 위치 규칙으로 UV 삼각형을 칠해 라벨 맵을 만든다"""
    w, h = img.size; lab = Image.new('L', (w, h), 0); d = ImageDraw.Draw(lab)
    me = obj.data; uv = me.uv_layers.active.data; mw = obj.matrix_world
    for p in me.polygons:
        if p.material_index != mat_index: continue
        L = rule(mw @ p.center)
        if not L: continue
        pts = [(uv[li].uv[0] * w, uv[li].uv[1] * h) for li in p.loop_indices]  # blender 픽셀 배열은 아래→위
        d.polygon(pts, fill=L, outline=L)
    return np.array(lab)  # 행 0 = 아래(블렌더 픽셀 순서와 같음)

def shade(a, m, col, keep=0.35):
    lum = a[..., :3].mean(axis=2, keepdims=True); mean = lum[m].mean() if m.any() else 0.5
    k = np.clip((lum / max(mean, 1e-3)) ** keep, 0.75, 1.2)
    a[m, :3] = (np.array(col) / 255.0 * k)[m]

def body_rule(c):
    ax = abs(c.x)
    if ax > 0.50 and c.z > 1.2: return 2              # 손: 검은 장갑
    if ax > 0.17 and c.z > 1.2: return 1              # 팔: 맨살
    if c.z > 1.17: return 2                           # 크롭탑
    if c.z > 1.0: return 1                            # 배
    if c.z > 0.84: return 2                           # 핫팬츠
    if c.z > 0.21: return 1                           # 다리
    return 3                                          # 양말

def recolor_body(body):
    mats = [m.name for m in body.data.materials]
    i = next(k for k, n in enumerate(mats) if 'Body' in n and 'SKIN' in n)
    img = tex_of(body.data.materials[i]); a = img_np(img)
    lab = label_mask(body, i, img, body_rule)
    a[lab == 1, :3] = np.array(SKIN) / 255.0
    shade(a, lab == 2, BLACK); a[lab == 3, :3] = np.array(SOCK) / 255.0
    np_img(img, a)

def recolor_tops(body):
    i = next(k for k, m in enumerate(body.data.materials) if 'Tops' in m.name)
    img = tex_of(body.data.materials[i]); a = img_np(img)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    line = ((g + b) / 2 - r > 0.35) & (np.maximum(g, b) > 0.75)   # 진한 시안 발광선만 빨강
    lum = a[..., :3].mean(-1, keepdims=True)
    a[..., :3] = np.array(BLACK) / 255.0 * np.clip(0.8 + lum * 0.6, 0.8, 1.4)
    a[line, :3] = np.array(RED) / 255.0
    np_img(img, a)

def recolor_shoes(body):
    i = next(k for k, m in enumerate(body.data.materials) if 'Shoes' in m.name)
    img = tex_of(body.data.materials[i]); a = img_np(img)
    lab = label_mask(body, i, img, lambda c: 3 if c.z < 0.035 else (2 if c.z > 0.17 else 1))
    shade(a, lab == 1, WHITE, 0.2); shade(a, lab == 2, RED, 0.2); shade(a, lab == 3, BLACK, 0.2)
    rr, gg, bb = a[..., 0], a[..., 1], a[..., 2]
    line = (lab == 1) & (np.maximum(gg, bb) > rr + 0.2)
    a[line, :3] = np.array(RED) / 255.0
    np_img(img, a)
    # 발목까지 덮는 두툼한 운동화: 신발 정점을 발 중심에서 바깥으로
    me = body.data; mw = body.matrix_world; inv = mw.inverted()
    vids = {v for p in me.polygons if p.material_index == i for v in p.vertices}
    for side in (-1, 1):
        S = [vi for vi in vids if (mw @ me.vertices[vi].co).x * side > 0]
        if not S: continue
        P = [mw @ me.vertices[vi].co for vi in S]
        cx = sum(p.x for p in P) / len(P); cy = sum(p.y for p in P) / len(P)
        for vi, p in zip(S, P):
            k = 1.28
            me.vertices[vi].co = inv @ Vector((cx + (p.x - cx) * k, cy + (p.y - cy) * k * 1.05, max(p.z * 1.15, p.z)))

def hide_shoes(body):
    # 원본은 굽 높은 부츠라 스프라이트에서 역관절처럼 보임 → 면을 지우고 vita_coat에서 납작한 운동화를 새로 만든다
    import bmesh
    i = next(k for k, m in enumerate(body.data.materials) if 'Shoes' in m.name)
    bm = bmesh.new(); bm.from_mesh(body.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index == i], context='FACES')
    bm.to_mesh(body.data); bm.free()

def recolor_hair():
    for m in bpy.data.materials:
        if 'HAIR' not in m.name: continue
        img = tex_of(m); a = img_np(img)
        lum = a[..., :3].mean(-1, keepdims=True)
        a[..., :3] = np.clip(lum * np.array([1.0, 0.66, 0.72]) * 1.08, 0, 1)
        np_img(img, a)

def apply():
    for o in list(bpy.data.objects):
        if o.name.startswith('Icosphere'): o.hide_render = True
    body = bpy.data.objects['Body']
    recolor_body(body); recolor_tops(body); hide_shoes(body); recolor_hair()
