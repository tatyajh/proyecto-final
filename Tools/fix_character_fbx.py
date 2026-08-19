import sys
from pathlib import Path

import bmesh
import bpy


def animation_length(action):
    start, end = action.frame_range
    return max(0.0, float(end - start))


args = sys.argv[sys.argv.index("--") + 1 :]
source = Path(args[0]).resolve()
output_fbx = Path(args[1]).resolve()
output_blend = Path(args[2]).resolve()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source), automatic_bone_orientation=False)

mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
empty_meshes = [obj for obj in mesh_objects if len(obj.data.vertices) == 0 or len(obj.data.polygons) == 0]
empty_mesh_names = [obj.name for obj in empty_meshes]
for obj in empty_meshes:
    bpy.data.objects.remove(obj, do_unlink=True)

mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
targets = mesh_objects

for obj in targets:
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    if bm.faces:
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.validate(clean_customdata=False)
    mesh.update(calc_edges=True)

actions = list(bpy.data.actions)
valid_actions = [action for action in actions if animation_length(action) > 0.01]
selected_action = max(valid_actions, key=animation_length) if valid_actions else None

for obj in bpy.context.scene.objects:
    if obj.type == "ARMATURE":
        if obj.animation_data is None:
            obj.animation_data_create()
        obj.animation_data.action = selected_action

for action in actions:
    if animation_length(action) <= 0.01:
        bpy.data.actions.remove(action)

bpy.ops.wm.save_as_mainfile(filepath=str(output_blend))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=str(output_fbx),
    use_selection=True,
    object_types={"ARMATURE", "MESH", "EMPTY"},
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    add_leaf_bones=False,
    bake_anim=selected_action is not None,
    bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0.0,
    path_mode="COPY",
    embed_textures=True,
)

print(
    "CORRECTION_RESULT",
    {
        "meshes": len(mesh_objects),
        "empty_meshes_removed": empty_mesh_names,
        "actions_found": [(action.name, animation_length(action)) for action in valid_actions],
        "selected_action": selected_action.name if selected_action else None,
        "fbx": str(output_fbx),
        "blend": str(output_blend),
    },
)
