"""Blender authoring: hand-keyed silhouettes from local reference, not claimed video mocap.
Run with Blender MCP in the NamCung_ReferenceMotion scene containing NhanVat@Idle.
"""
import bpy
import json
import math
import os
from mathutils import Vector, Matrix, Quaternion

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'Assets', '_Game', 'Models', 'ReferenceCombat')
os.makedirs(OUT, exist_ok=True)
scene = bpy.context.scene
rig = next(o for o in scene.objects if o.type == 'ARMATURE')
base_action = bpy.data.actions.get(rig.get('NCK_base_action', 'mixamo.com')) or rig.animation_data.action
rig.animation_data.action = base_action
rig['NCK_base_action'] = base_action.name
scene.frame_set(20)
base_action.use_fake_user = True
base = {p.name: (p.matrix_basis.to_quaternion().copy(), p.location.copy(), p.scale.copy()) for p in rig.pose.bones}
arm_lengths = {}
for side in ['Left', 'Right']:
    arm, fore, hand = [rig.pose.bones['mixamorig:' + side + part] for part in ['Arm', 'ForeArm', 'Hand']]
    arm_lengths[side] = ((fore.head - arm.head).length, (hand.head - fore.head).length)

seal = [(0.07, 1.34, .30), (-.07, 1.40, .32)]
poses = {
    'FireIce': ([(.38, 1.65, .15), (-.38, 1.65, .15)], [(.08, 1.84, .29), (-.08, 1.84, .29)]),
    'Mirrors': (seal, [(.09, 1.28, .27), (-.10, 1.43, .46)]),
    'Reincarnation': (seal, [(.34, 1.47, .14), (-.34, 1.47, .14)]),
    'BloodSword': ([(.10, 1.36, .35), (-.23, 1.55, -.27)], [(.13, 1.29, .26), (-.07, 1.44, .46)]),
    'ThunderSword': ([(.10, 1.28, .28), (-.10, 1.89, .12)], [(.12, 1.27, .25), (-.10, 1.85, .23)]),
    'GoldenNet': (seal, [(.29, 1.54, .33), (-.29, 1.54, .33)]),
    'MindPearl': ([(.08, 1.25, .28), (-.10, 1.47, .37)], [(.09, 1.28, .27), (-.07, 1.46, .47)]),
}

def reset():
    for p in rig.pose.bones:
        q, loc, scale = base[p.name]
        p.rotation_mode = 'QUATERNION'
        p.rotation_quaternion = q
        p.location = loc
        p.scale = scale
    bpy.context.view_layer.update()

def aim(bone, direction):
    current = bone.tail - bone.head
    delta = current.normalized().rotation_difference(direction.normalized())
    matrix = bone.matrix.copy()
    bone.matrix = Matrix.LocRotScale(matrix.translation, delta @ matrix.to_quaternion(), matrix.to_scale())
    bpy.context.view_layer.update()

def arm_to(side, target):
    arm, fore, hand = [rig.pose.bones['mixamorig:' + side + part] for part in ['Arm', 'ForeArm', 'Hand']]
    shoulder = arm.head.copy()
    l1, l2 = arm_lengths[side]
    delta = Vector(target) - shoulder
    distance = min(delta.length, (l1 + l2) * .97)
    direction = delta.normalized()
    pole = Vector((.8 if side == 'Left' else -.8, -.5, -.15))
    bend = (pole - direction * pole.dot(direction)).normalized()
    along = (l1*l1 - l2*l2 + distance*distance) / (2*max(.01, distance))
    height = math.sqrt(max(0, l1*l1 - along*along))
    elbow = shoulder + direction*along + bend*height
    aim(arm, elbow - shoulder)
    aim(fore, shoulder + direction*distance - fore.head)
    aim(hand, Vector((0, .15, 1)))

def smooth(t):
    t = max(0, min(1, t))
    return t*t*(3-2*t)

report = []
for name, (gather, release) in poses.items():
    reset()
    old_action = bpy.data.actions.get('NCK_' + name)
    if old_action:
        rig.animation_data.action = base_action
        bpy.data.actions.remove(old_action)
    action = bpy.data.actions.new('NCK_' + name)
    action.use_fake_user = True
    rig.animation_data.action = action
    initial = [tuple(rig.pose.bones['mixamorig:' + side + 'Hand'].head) for side in ['Left', 'Right']]
    previous = {}
    for frame in range(1, 56):
        scene.frame_set(frame)
        reset()
        u = (frame-1)/54
        for index, side in enumerate(['Left', 'Right']):
            if u < .33:
                target = Vector(initial[index]).lerp(Vector(gather[index]), smooth(u/.33))
            elif u < .56:
                target = Vector(gather[index]).lerp(Vector(release[index]), smooth((u-.33)/.23))
            elif u < .72:
                target = Vector(release[index])
            else:
                target = Vector(release[index]).lerp(Vector(initial[index]), smooth((u-.72)/.28))
            arm_to(side, target)
        for p in rig.pose.bones:
            if p.name in previous:
                p.rotation_quaternion.make_compatible(previous[p.name])
            previous[p.name] = p.rotation_quaternion.copy()
            p.keyframe_insert('rotation_quaternion', frame=frame, group=p.name)
            p.keyframe_insert('location', frame=frame, group=p.name)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:
                        key.interpolation = 'LINEAR'
    scene.frame_start = 1
    scene.frame_end = 55
    scene.render.fps = 30
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    path = os.path.join(OUT, 'NCK_' + name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE'},
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y')
    report.append({'clip': name, 'frames': 55, 'fps': 30, 'release_normalized': .56})

# Low-poly relic silhouettes: sword along Blender Z (= Unity Y), mirror facing Blender -Y.
old_collection = bpy.data.collections.get('NamCung_Relics')
if old_collection:
    for generated in list(old_collection.objects):
        bpy.data.objects.remove(generated, do_unlink=True)
    bpy.data.collections.remove(old_collection)
collection = bpy.data.collections.new('NamCung_Relics')
scene.collection.children.link(collection)
def move_collection(obj):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)
    return obj

def sword(name, width, length, serrated):
    vertices, faces = [], []
    sections = [(0, width*.48), (.14*length, width), (.65*length, width*.65), (.91*length, width*.32), (length, 0)]
    for z, radius in sections:
        vertices.extend([(-radius,0,z),(0,-.045,z),(radius,0,z),(0,.045,z)])
    for i in range(4):
        for j in range(4):
            a=i*4+j; b=i*4+(j+1)%4
            faces.append((a,b,b+4,a+4))
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); collection.objects.link(obj)
    parts=[obj]
    for z, radius, depth in [(-.14,.045,.28),(-.31,.085,.08),(0,.22,.055)]:
        bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=radius,depth=depth,location=(0,0,z))
        parts.append(move_collection(bpy.context.object))
    if serrated:
        for sign in [-1,1]:
            for i in range(5):
                bpy.ops.mesh.primitive_cone_add(vertices=4,radius1=width*.55,depth=.18,location=(sign*width*.7,0,.12+i*.15))
                part=move_collection(bpy.context.object);part.rotation_euler[1]=sign*math.pi/2;parts.append(part)
    bpy.ops.object.select_all(action='DESELECT')
    for part in parts:part.select_set(True)
    bpy.context.view_layer.objects.active=obj;bpy.ops.object.join()
    obj.data.calc_loop_triangles()
    return obj

blood=sword('BloodBlade',.14,1.65,True)
gold=sword('ThunderBlade',.12,1.8,False)
bpy.ops.mesh.primitive_torus_add(major_segments=48,minor_segments=6,major_radius=.5,minor_radius=.045,rotation=(math.pi/2,0,0))
mirror=move_collection(bpy.context.object);mirror.name='MirrorFrame'
parts=[mirror]
for index in range(12):
    angle=index*math.tau/12
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.08,location=(math.sin(angle)*.53,0,math.cos(angle)*.53))
    parts.append(move_collection(bpy.context.object))
bpy.ops.object.select_all(action='DESELECT')
for p in parts:p.select_set(True)
bpy.context.view_layer.objects.active=mirror;bpy.ops.object.join()
bpy.ops.object.select_all(action='DESELECT')
for obj in collection.objects:obj.select_set(True)
bpy.context.view_layer.objects.active=mirror
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'NCK_Relics.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y')
for obj in collection.objects:
    obj.data.calc_loop_triangles()
    report.append({'mesh':obj.name,'triangles':len(obj.data.loop_triangles)})
collection.hide_viewport=True
rig.animation_data.action=bpy.data.actions['NCK_ThunderSword']
scene.frame_set(31)
os.makedirs(os.path.join(ROOT,'ArtSource','Blender'),exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ArtSource','Blender','NamCung_ReferenceCombat.blend'))
with open(os.path.join(ROOT,'ArtSource','ReferenceCombat','blender_build_report.json'),'w',encoding='utf-8') as f:
    json.dump(report,f,ensure_ascii=False,indent=2)
print(json.dumps(report,ensure_ascii=False))
