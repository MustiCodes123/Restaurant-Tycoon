import argparse
import os
import sys

import bpy


CHARACTERS = {
    "CH_1_11_C_MC": "CH_1/CH_1_11_C_MC.fbx",
    "CH_2_1_A_MC": "CH_2/CH_2_1_A_MC.fbx",
    "CH_3_7_B_MC": "CH_3/CH_3_7_B_MC.fbx",
    "CH_8_2_B_MC": "CH_8/CH_8_2_B_MC.fbx",
    "CH_8_3_A_MC": "CH_8/CH_8_3_A_MC.fbx",
    "CH_8_3_B_MC": "CH_8/CH_8_3_B_MC.fbx",
    "CH_8_3_C_MC": "CH_8/CH_8_3_C_MC.fbx",
    "CH_8_3_E_MC": "CH_8/CH_8_3_E_MC.fbx",
    "CH_9_3_C_MC": "CH_9/CH_9_3_C_MC.fbx",
    "CH_9_4_C_MC": "CH_9/CH_9_4_C_MC.fbx",
    "CH_11_8_B_MC": "CH_11/CH_11_8_B_MC.fbx",
    "CH_12_3_C_MC": "CH_12/CH_12_3_C_MC.fbx",
    "CH_12_5_C_MC": "CH_12/CH_12_5_C_MC.fbx",
    "CH_14_1_C_MC": "CH_14/CH_14_1_C_MC.fbx",
}

REQUIRED_BONES = (
    "Hips",
    "Torso",
    "Head",
    "Shoulder.L",
    "Arm.L",
    "ForeArm.L",
    "Hand.L",
    "Shoulder.R",
    "Arm.R",
    "ForeArm.R",
    "Hand.R",
    "UpperLeg.L",
    "Leg.L",
    "Foot.L",
    "UpperLeg.R",
    "Leg.R",
    "Foot.R",
)


def parse_args():
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", required=True)
    parser.add_argument("--characters", nargs="*", default=list(CHARACTERS))
    parser.add_argument("--validate-only", action="store_true")
    return parser.parse_args(args)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def find_single_armature(character_name):
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(
            f"{character_name}: expected one armature, found {len(armatures)}"
        )
    return armatures[0]


def validate_bones(character_name, armature):
    missing = [name for name in REQUIRED_BONES if name not in armature.data.bones]
    if missing:
        raise RuntimeError(f"{character_name}: missing required bones: {missing}")


def repair_hierarchy(character_name, armature):
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    bones = armature.data.edit_bones
    hips = bones["Hips"]
    for bone_name in ("Torso", "UpperLeg.L", "UpperLeg.R"):
        bone = bones[bone_name]
        bone.parent = hips
        bone.use_connect = False

    bpy.ops.object.mode_set(mode="OBJECT")

    for bone_name in ("Torso", "UpperLeg.L", "UpperLeg.R"):
        parent = armature.data.bones[bone_name].parent
        if parent is None or parent.name != "Hips":
            raise RuntimeError(f"{character_name}: failed to parent {bone_name} to Hips")


def select_character_objects(armature):
    bpy.ops.object.select_all(action="DESELECT")
    selected = []
    for obj in bpy.context.scene.objects:
        if obj.type in {"ARMATURE", "MESH", "EMPTY"}:
            obj.select_set(True)
            selected.append(obj)

    if armature not in selected:
        armature.select_set(True)
        selected.append(armature)

    bpy.context.view_layer.objects.active = armature
    return selected


def export_character(output_path):
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=True,
        object_types={"ARMATURE", "MESH", "EMPTY"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="OFF",
        use_subsurf=False,
        use_armature_deform_only=False,
        add_leaf_bones=False,
        armature_nodetype="NULL",
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )


def rebuild_character(project_path, character_name):
    relative_path = CHARACTERS[character_name]
    source_path = os.path.join(
        project_path,
        "Assets",
        "New Assets",
        "Characters",
        "FBX",
        "Characters",
        relative_path,
    )
    output_path = os.path.join(
        project_path,
        "Assets",
        "Ready Characters",
        "Models",
        character_name,
        f"{character_name}.fbx",
    )

    if not os.path.isfile(source_path):
        raise FileNotFoundError(source_path)

    reset_scene()
    bpy.ops.import_scene.fbx(filepath=source_path, use_anim=False)
    armature = find_single_armature(character_name)
    validate_bones(character_name, armature)
    repair_hierarchy(character_name, armature)
    selected = select_character_objects(armature)
    export_character(output_path)

    mesh_count = sum(1 for obj in selected if obj.type == "MESH")
    print(
        f"RIG_REBUILT|{character_name}|meshes={mesh_count}|output={output_path}",
        flush=True,
    )


def validate_export(project_path, character_name):
    output_path = os.path.join(
        project_path,
        "Assets",
        "Ready Characters",
        "Models",
        character_name,
        f"{character_name}.fbx",
    )
    if not os.path.isfile(output_path):
        raise FileNotFoundError(output_path)

    reset_scene()
    bpy.ops.import_scene.fbx(filepath=output_path, use_anim=False)
    armature = find_single_armature(character_name)
    validate_bones(character_name, armature)

    for bone_name in ("Torso", "UpperLeg.L", "UpperLeg.R"):
        parent = armature.data.bones[bone_name].parent
        if parent is None or parent.name != "Hips":
            raise RuntimeError(
                f"{character_name}: exported {bone_name} is not parented to Hips"
            )

    character_meshes = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and len(obj.vertex_groups) > 0
    ]
    if len(character_meshes) != 1:
        raise RuntimeError(
            f"{character_name}: expected one skinned mesh, found {len(character_meshes)}"
        )

    mesh = character_meshes[0]
    armature_modifiers = [
        modifier for modifier in mesh.modifiers if modifier.type == "ARMATURE"
    ]
    if len(armature_modifiers) != 1 or armature_modifiers[0].object != armature:
        raise RuntimeError(f"{character_name}: mesh is not bound to its armature")

    if any(dimension <= 0.01 for dimension in mesh.dimensions):
        raise RuntimeError(
            f"{character_name}: invalid mesh dimensions {tuple(mesh.dimensions)}"
        )

    dimensions = tuple(round(value, 3) for value in mesh.dimensions)
    print(
        f"RIG_VALID|{character_name}|dimensions={dimensions}|materials={len(mesh.data.materials)}",
        flush=True,
    )


def main():
    args = parse_args()
    project_path = os.path.abspath(args.project)
    unknown = sorted(set(args.characters) - set(CHARACTERS))
    if unknown:
        raise RuntimeError(f"Unknown characters: {unknown}")

    failures = []
    for character_name in args.characters:
        try:
            if args.validate_only:
                validate_export(project_path, character_name)
            else:
                rebuild_character(project_path, character_name)
        except Exception as exc:
            failures.append(f"{character_name}: {exc}")
            print(f"RIG_FAILED|{character_name}|{exc}", flush=True)

    if failures:
        raise RuntimeError("Rig rebuild failures:\n" + "\n".join(failures))

    action = "validated" if args.validate_only else "rebuilt"
    print(
        f"RIG_PIPELINE_COMPLETE|action={action}|count={len(args.characters)}",
        flush=True,
    )


if __name__ == "__main__":
    main()
