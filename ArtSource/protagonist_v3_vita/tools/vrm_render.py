# 사용: python3 vrm_render.py <model.glb> <outdir> <name=mixamo.fbx,...> <RES> <STEP>
import bpy, sys, math, os, json
from mathutils import Vector
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
glb, outdir = sys.argv[1], sys.argv[2]
clips = [c.split('=') for c in sys.argv[3].split(',')]
RES = int(sys.argv[4]); STEP = int(sys.argv[5])
os.makedirs(outdir, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
import vita_look, vita_coat, mx_retarget
vita_look.apply(); vita_coat.apply()
sys.path.insert(0, os.path.join(HERE, '..', '..', '..', 'tools', 'sprite_bake'))  # 범용 셰이딩
import shading
shading.apply(flat_prefixes=('Coat_', 'Shoe_'))
sc = bpy.context.scene; sc.cycles.samples = int(os.environ.get('SAMPLES', '10'))
sc.render.resolution_x = sc.render.resolution_y = RES; sc.render.filter_size = 0.01
sc.render.image_settings.color_mode = 'RGBA'; sc.render.fps = 30
RIM = float(os.environ.get('RIM', '3'))
rim = bpy.data.objects.new('rim', bpy.data.lights.new('rim', 'SUN')); sc.collection.objects.link(rim)
rim.data.energy = RIM; rim.data.color = (0.55, 0.95, 1.0); rim.rotation_euler = (math.radians(-60), 0, math.radians(20))
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
pivot = bpy.data.objects.new('pivot', None); sc.collection.objects.link(pivot)
for o in bpy.data.objects:
    if o.parent is None and o not in (pivot,) and o.type in ('ARMATURE', 'MESH', 'EMPTY'): o.parent = pivot
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = float(os.environ.get('ORTHO', '3.2'))
el = math.radians(30); target = Vector((0, 0, float(os.environ.get('TZ', '0.75'))))
cam.location = target + Vector((0, -20 * math.cos(el), 20 * math.sin(el))); cam.rotation_euler = (math.radians(90) - el, 0, 0)
DIRS = ['S', 'SW', 'W', 'NW', 'N', 'NE', 'E', 'SE']
meta = {}
for name, fbx in clips:
    act, (f0, f1) = mx_retarget.retarget(arm, fbx, name, inplace=True, lockz=name in os.environ.get('LOCKZ', '').split(','))
    frames = list(range(f0, f1 + 1, STEP)); meta[name] = len(frames)
    for di, d in enumerate(DIRS):
        pivot.rotation_euler[2] = math.radians(180 - 45 * di)   # VRM은 +Y를 봄 → S가 카메라 쪽
        for fi, f in enumerate(frames):
            sc.frame_set(f); sc.render.filepath = os.path.join(outdir, f'{name}_{d}_{fi:02d}.png')
            bpy.ops.render.render(write_still=True)
json.dump(meta, open(os.path.join(outdir, 'meta.json'), 'w')); print('DONE', meta)
