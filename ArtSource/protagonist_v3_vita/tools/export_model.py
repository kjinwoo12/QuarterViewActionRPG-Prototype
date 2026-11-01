import bpy, sys, os
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
OUT = os.path.join(HERE, 'export4')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, 'vrm/vita_src.glb'))
import vita_look, vita_coat
vita_look.apply(); vita_coat.apply()
for o in list(bpy.data.objects):
    if o.name.startswith('Icosphere'): bpy.data.objects.remove(o)
# VRoid 옷은 안 쓰는 부분을 텍스처 알파 0으로 숨김 → 그런 면은 실제로 삭제
import numpy as np
def drop_hidden(o, key):
    me = o.data; uv = me.uv_layers.active.data
    mi = next(k for k, m in enumerate(me.materials) if m and key in m.name)
    img = next(n.image for n in me.materials[mi].node_tree.nodes if n.type == 'TEX_IMAGE' and n.image)
    w, h = img.size; A = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[..., 3]
    def al(u, v): return A[int(np.clip(v, 0, .9999) * h), int(np.clip(u, 0, .9999) * w)]
    dead = set()
    for p in me.polygons:
        if p.material_index != mi: continue
        us = [uv[l].uv.copy() for l in p.loop_indices]; c = sum(us, us[0] * 0) / len(us)
        pts = us + [c] + [(x + c) / 2 for x in us]
        if max(al(x[0], x[1]) for x in pts) < 0.05: dead.add(p.index)
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in dead], context='FACES')
    bm.to_mesh(me); bm.free(); print('HIDDEN', o.name, key, len(dead))
import bmesh
drop_hidden(bpy.data.objects['Body'], 'Tops')
# 한 겹 면 → 두께 부여 (게임엔진/믹사모는 뒷면 컬링)
import bmesh
def nonmani(o):
    bm = bmesh.new(); bm.from_mesh(o.data); n = sum(1 for e in bm.edges if not e.is_manifold); bm.free(); return n
for o in [o for o in bpy.data.objects if o.name.startswith(('Coat_', 'Sneaker_'))]:
    before = nonmani(o)
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()
    if before:
        sd = o.modifiers.new('thick', 'SOLIDIFY'); sd.thickness = 0.008 if o.name.startswith('Coat_') else 0.004
        sd.offset = -1; sd.use_rim = True; sd.use_even_offset = True
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_move_to_index(modifier='thick', index=0)
        bpy.ops.object.modifier_apply(modifier='thick')
    print('THICK', o.name, before, '->', nonmani(o), len(o.data.polygons))
# VRM 언릿(Emission) 머티리얼 → Principled+텍스처 (FBX/믹사모가 텍스처를 읽도록)
TEX = os.path.join(OUT, 'textures'); os.makedirs(TEX, exist_ok=True)
for im in bpy.data.images:
    if im.size[0] == 0: continue
    p = os.path.join(TEX, im.name + '.png'); im.filepath_raw = p; im.file_format = 'PNG'; im.save()
    if im.packed_file: im.unpack(method='REMOVE')
    im.filepath = p
for m in bpy.data.materials:
    if not m.use_nodes: continue
    nt = m.node_tree; tex = next((n for n in nt.nodes if n.type == 'TEX_IMAGE'), None)
    if tex is None or any(n.type == 'BSDF_PRINCIPLED' for n in nt.nodes): continue
    img = tex.image; nt.nodes.clear()
    t = nt.nodes.new('ShaderNodeTexImage'); t.image = img
    b = nt.nodes.new('ShaderNodeBsdfPrincipled'); o = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(t.outputs['Color'], b.inputs['Base Color']); nt.links.new(t.outputs['Alpha'], b.inputs['Alpha'])
    nt.links.new(b.outputs[0], o.inputs[0])
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'gil_protagonist.blend'), compress=True)
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, 'gil_protagonist.glb'), export_format='GLB')
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'gil_protagonist_rigged.fbx'), path_mode='COPY', embed_textures=True, add_leaf_bones=False)
# 믹사모 오토리거용: 뼈대 없이 메시만 (모디파이어 적용)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
for o in meshes:
    bpy.context.view_layer.objects.active = o
    if o.data.shape_keys: o.shape_key_clear()
    for m in list(o.modifiers):
        if m.type == 'ARMATURE': bpy.ops.object.modifier_apply(modifier=m.name)
    mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    o.vertex_groups.clear()
    if o.data.shape_keys: o.shape_key_clear()
bpy.data.objects.remove(arm)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'gil_protagonist_for_mixamo.fbx'), path_mode='COPY', embed_textures=True, object_types={'MESH'})
print('EXPORTED', [o.name for o in meshes])
