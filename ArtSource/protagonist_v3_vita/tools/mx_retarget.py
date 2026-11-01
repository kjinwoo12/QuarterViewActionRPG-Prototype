# Mixamo(FBX, 스킨 없음) 동작을 VRM(VRoid) 리그로 옮긴다: 월드 회전 차이(rest 기준)를 그대로 전달
import bpy, math, os
from mathutils import Matrix, Quaternion, Vector

MAP = {'Hips': 'C_Hips', 'Spine': 'C_Spine', 'Spine1': 'C_Chest', 'Spine2': 'C_UpperChest', 'Neck': 'C_Neck', 'Head': 'C_Head'}
for s, t in (('Left', 'L'), ('Right', 'R')):
    MAP.update({f'{s}Shoulder': f'{t}_Shoulder', f'{s}Arm': f'{t}_UpperArm', f'{s}ForeArm': f'{t}_LowerArm', f'{s}Hand': f'{t}_Hand',
                f'{s}UpLeg': f'{t}_UpperLeg', f'{s}Leg': f'{t}_LowerLeg', f'{s}Foot': f'{t}_Foot', f'{s}ToeBase': f'{t}_ToeBase'})
    for f, g in (('Thumb', 'Thumb'), ('Index', 'Index'), ('Middle', 'Middle'), ('Ring', 'Ring'), ('Pinky', 'Little')):
        for i in (1, 2, 3): MAP[f'{s}Hand{f}{i}'] = f'{t}_{g}{i}'

C = Matrix.Rotation(math.pi, 4, 'Z')   # Mixamo는 -Y, VRM은 +Y를 바라봄

def rot(m): return m.to_quaternion()

def knee_back(tgt, P, sd):
    """무릎이 골반 정면에서 몇 도 돌아가 있는지(180=완전히 뒤). 다리가 펴져 있으면 None"""
    nU, nL, nF = (f'J_Bip_{sd}_{b}' for b in ('UpperLeg', 'LowerLeg', 'Foot'))
    H = P[nU].to_translation(); K = P[nL].to_translation(); F = P[nF].to_translation()
    ax = (F - H).normalized(); k = (K - H) - ax * (K - H).dot(ax)
    if k.length < 0.04: return None
    fw = P['J_Bip_C_Hips'].to_quaternion() @ tgt.data.bones['J_Bip_C_Hips'].matrix_local.to_quaternion().inverted() @ Vector((0, 1, 0))
    fw = fw - ax * fw.dot(ax)
    if fw.length < 0.3: return None
    return math.degrees(k.normalized().angle(fw.normalized()))

def knee_fix(tgt, P, sd, apply=True):
    """무릎이 발끝 방향을 향하도록 허벅지·정강이를 엉덩이-발목 축으로 돌린다. 보정 전 어긋난 각도(도)를 돌려줌"""
    tb = tgt.data.bones
    nU, nL, nF, nT = (f'J_Bip_{sd}_{b}' for b in ('UpperLeg', 'LowerLeg', 'Foot', 'ToeBase'))
    if not all(n in P for n in (nU, nL, nF)): return None
    H = P[nU].to_translation(); K = P[nL].to_translation(); F = P[nF].to_translation()
    ax = F - H
    if ax.length < 1e-4: return None
    ax.normalize()
    k = (K - H); k = k - ax * k.dot(ax)
    if k.length < 0.035: return None                     # 다리가 거의 펴져 있으면 판단 불가
    # 발끝 방향: 발 뼈 rest 방향(발목→발가락)을 현재 발 회전으로 돌림
    rest_toe = (tb[nT].head_local - tb[nF].head_local) if nT in tb else Vector((0, 1, 0))
    toe = (P[nF].to_quaternion() @ tb[nF].matrix_local.to_quaternion().inverted()) @ rest_toe
    tp = toe - ax * toe.dot(ax)
    if tp.length < 0.3 * toe.length: return None         # 발끝이 다리 축과 나란하면(발등 펴기) 건너뜀
    k.normalize(); tp.normalize()
    ang = math.atan2(ax.dot(k.cross(tp)), k.dot(tp))
    dev = abs(math.degrees(ang))
    if apply and dev > 1:
        R = Quaternion(ax, ang).to_matrix().to_4x4()
        T = Matrix.Translation(H) @ R @ Matrix.Translation(-H)
        P[nU] = T @ P[nU]; P[nL] = T @ P[nL]               # 발목 위치는 축 위라 그대로, 발 회전은 유지
    return dev

def retarget(tgt, fbx_path, name, inplace=True, lockz=False):
    before = set(bpy.data.objects); acts_before = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=fbx_path)
    new = [o for o in bpy.data.objects if o not in before]
    src = next(o for o in new if o.type == 'ARMATURE')
    sact = src.animation_data.action
    f0, f1 = [int(round(x)) for x in sact.frame_range]
    pairs = []
    for b in src.data.bones:
        k = b.name.split(':')[-1]
        if k in MAP and ('J_Bip_' + MAP[k]) in tgt.data.bones: pairs.append((b.name, 'J_Bip_' + MAP[k]))
    tb = tgt.data.bones
    order = [n for n in [b.name for b in tb] if n in {t for _, t in pairs}]   # 부모 먼저
    s2t = {t: s for s, t in pairs}
    srest = {s: src.matrix_world @ src.data.bones[s].matrix_local for s, _ in pairs}
    trest = {t: tgt.matrix_world @ tb[t].matrix_local for t in order}
    hs = (src.matrix_world @ src.data.bones[s2t['J_Bip_C_Hips']].head_local).z
    ht = (tgt.matrix_world @ tb['J_Bip_C_Hips'].head_local).z
    scale = ht / hs
    # 골반 폭 보정: 원본(Mixamo) 골반이 넓어 그대로 옮기면 다리가 안으로 모이고 교차함 → 다리 사슬 전체를 바깥으로 벌림
    def hw(arm_o, b): return abs((arm_o.matrix_world @ arm_o.data.bones[b].head_local).x)
    sL = next(n for n, _ in pairs if n.endswith('LeftUpLeg')); sF = next(n for n, _ in pairs if n.endswith('LeftFoot'))
    ls = ((src.matrix_world @ src.data.bones[sL].head_local) - (src.matrix_world @ src.data.bones[sF].head_local)).length
    lt = ((tgt.matrix_world @ tb['J_Bip_L_UpperLeg'].head_local) - (tgt.matrix_world @ tb['J_Bip_L_Foot'].head_local)).length
    gap = hw(src, sL) * lt / ls - hw(tgt, 'J_Bip_L_UpperLeg')
    ab = math.atan2(max(gap, 0.0), lt) + math.radians(float(__import__('os').environ.get('ABD_DEG', '0')))  # 스프라이트에서 다리가 겹쳐 보이지 않게 약간 벌림
    sideL = 1 if (tgt.matrix_world @ tb['J_Bip_L_UpperLeg'].head_local).x > 0 else -1
    ABD = {}
    for sd, sg in (('L', sideL), ('R', -sideL)):
        q = Quaternion((0, 1, 0), -sg * ab)   # 발이 바깥(+sg*x)으로
        for b in ('UpperLeg', 'LowerLeg', 'Foot', 'ToeBase'): ABD[f'J_Bip_{sd}_{b}'] = q
    print('ABD deg', math.degrees(ab))
    act = bpy.data.actions.new(name); tgt.animation_data_create(); tgt.animation_data.action = act
    for pb in tgt.pose.bones: pb.rotation_mode = 'QUATERNION'
    sc = bpy.context.scene
    first_hip = None
    KNEE = os.environ.get('KNEEFIX', '1') == '1'
    stats = []
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        P = {}
        for t in order:                                   # 1단계: 목표 월드 행렬 계산
            s = s2t[t]
            Ms = src.matrix_world @ src.pose.bones[s].matrix
            Rw = rot(C) @ rot(Ms) @ rot(srest[s]).inverted() @ rot(C).inverted() @ rot(trest[t])
            if t in ABD: Rw = ABD[t] @ Rw
            bt = tb[t]
            if bt.parent and bt.parent.name in P:
                head = P[bt.parent.name] @ (bt.parent.matrix_local.inverted() @ bt.matrix_local).to_translation()
            elif bt.parent:
                head = (tgt.matrix_world @ bt.matrix_local).to_translation()
            else:
                head = bt.matrix_local.to_translation()
            if t == 'J_Bip_C_Hips':
                hw = C @ Ms.to_translation(); r0 = C @ srest[s].to_translation()
                d = (hw - r0) * scale
                if first_hip is None: first_hip = d.copy()
                if inplace: d.x = 0; d.y = 0
                if lockz: d.z = first_hip.z   # 위로 오르는 동작은 높이 이동을 게임 쪽에 맡김
                head = trest[t].to_translation() + d
            P[t] = Matrix.Translation(head) @ Rw.to_matrix().to_4x4()
        for sd in ('L', 'R'):                             # 2단계: 무릎 방향 검사(골반 정면 대비) / 선택적 보정
            dev = knee_back(tgt, P, sd)
            if dev is not None: stats.append((dev, f, sd))
            if KNEE: knee_fix(tgt, P, sd, apply=True)
        for t in order:                                   # 3단계: 로컬 회전으로 바꿔 키 입력
            bt = tb[t]; M = P[t]; pb = tgt.pose.bones[t]
            if bt.parent:
                Pp = P.get(bt.parent.name) or (tgt.matrix_world @ bt.parent.matrix_local)
                basis = (bt.parent.matrix_local.inverted() @ bt.matrix_local).inverted() @ Pp.inverted() @ M
            else:
                basis = bt.matrix_local.inverted() @ M
            loc, q, _ = basis.decompose()
            pb.rotation_quaternion = q
            pb.keyframe_insert('rotation_quaternion', frame=f)
            if t == 'J_Bip_C_Hips':
                pb.location = loc; pb.keyframe_insert('location', frame=f)
    if stats:
        bad = [(f, sd, round(d)) for d, f, sd in stats if d > 100]
        print(f'KNEE {name}: 무릎이 뒤를 향한 프레임 {len(bad)}/{len(stats)} {bad[:12]}')
    for o in new: bpy.data.objects.remove(o, do_unlink=True)
    for a in set(bpy.data.actions) - acts_before - {act}: bpy.data.actions.remove(a)
    return act, (f0, f1)
