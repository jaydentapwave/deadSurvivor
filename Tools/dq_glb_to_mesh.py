"""Convert step-12 GLB models into the RoundDQ3 Converted/<id>/mesh.json format the editor importer reads.

Usage: /usr/bin/python3 -B Tools/dq_glb_to_mesh.py <models-dir>   (re-runnable; overwrites only the listed ids)

glTF is right-handed. Foliage follows the existing RoundDQ3 conversion (x flipped, winding reversed).
The flag flips z instead so its cloth keeps flying toward +x like the previous sprite cloth, and its
origin is moved to the pole foot (the placement anchor the field layout uses).
"""
import json
import shutil
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / "Assets/Game/03.Resources/Art/HD2D/RoundDQ3"
# id -> (Converted id, texture file in the model folder)
FOLIAGE = {
    "dq_tree_round_a_r2": "basecolor-game.png",
    "dq_tree_round_c_r2": "basecolor-game.png",
    "dq_tree_light_r2": "basecolor-game.png",
    "dq_bush_b_r2": "basecolor-game.png",
    "flower_white": "basecolor.png",
}
# Step 13 T2: yellow/purple flowers reuse the new white flower mesh with recoloured petals (no new generation).
# Atlas thirds: stem | petals | centre. Petals are rescaled from the white mean to the existing painted flower colour.
FLOWER_VARIANTS = {"flower_yellow": ((222, 194, 94), (196, 112, 38)), "flower_purple": ((143, 71, 173), None)}
FLAG_POLE_FOOT_GLTF = (-0.6627, 0.0, 0.0514)
COMPONENT = {5126: ("f", 4), 5123: ("H", 2), 5125: ("I", 4)}
WIDTH = {"SCALAR": 1, "VEC2": 2, "VEC3": 3}


def read_glb(path):
    data = path.read_bytes()
    json_length = struct.unpack_from("<I", data, 12)[0]
    document = json.loads(data[20:20 + json_length])
    bin_start = 20 + json_length + 8
    return document, data[bin_start:]


def accessor(document, blob, index):
    a = document["accessors"][index]
    view = document["bufferViews"][a["bufferView"]]
    fmt, size = COMPONENT[a["componentType"]]
    width = WIDTH[a["type"]]
    stride = view.get("byteStride", size * width)
    offset = view.get("byteOffset", 0) + a.get("byteOffset", 0)
    return [struct.unpack_from("<" + fmt * width, blob, offset + i * stride) for i in range(a["count"])]


def node_meshes(document, blob):
    for node in document["nodes"]:
        if "mesh" not in node:
            continue
        if any(k in node for k in ("matrix", "translation", "rotation", "scale")):
            raise ValueError("node transforms are not expected in step-12 exports: " + node.get("name", ""))
        primitives = document["meshes"][node["mesh"]]["primitives"]
        if len(primitives) != 1:
            raise ValueError("expected one primitive per node")
        p = primitives[0]
        yield node.get("name", ""), {
            "position": accessor(document, blob, p["attributes"]["POSITION"]),
            "normal": accessor(document, blob, p["attributes"]["NORMAL"]),
            "uv": accessor(document, blob, p["attributes"]["TEXCOORD_0"]),
            "triangles": [i[0] for i in accessor(document, blob, p["indices"])],
        }


def to_unity(mesh, flip_z, origin=(0.0, 0.0, 0.0)):
    sx, sz = (1, -1) if flip_z else (-1, 1)
    ox, oy, oz = origin
    vertices, normals, uv, triangles = [], [], [], []
    for x, y, z in mesh["position"]:
        vertices += [sx * (x - ox), y - oy, sz * (z - oz)]
    for x, y, z in mesh["normal"]:
        normals += [sx * x, y, sz * z]
    for u, v in mesh["uv"]:
        uv += [u, 1 - v]
    t = mesh["triangles"]
    for i in range(0, len(t), 3):
        triangles += [t[i], t[i + 2], t[i + 1]]
    return {"vertices": vertices, "normals": normals, "uv": uv, "triangles": triangles}


def write(target, data):
    target.mkdir(parents=True, exist_ok=True)
    (target / "mesh.json").write_text(json.dumps(data, separators=(",", ":")))
    count = len(data["vertices"]) // 3
    ys = data["vertices"][1::3]
    print(f"{target.name}: {count} vertices, {len(data['triangles']) // 3} triangles, y {min(ys):.3f}..{max(ys):.3f}")


def recolour_flowers():
    from PIL import Image  # system /usr/bin/python3 ships Pillow
    source = ART / "Converted" / "flower_white"
    atlas = Image.open(source / "basecolor.png").convert("RGB")
    third = atlas.width // 3
    petals = [atlas.getpixel((x, y)) for x in range(third, third * 2) for y in range(atlas.height)]
    mean = [sum(p[i] for p in petals) / len(petals) for i in range(3)]
    for variant, (petal, centre) in FLOWER_VARIANTS.items():
        image = atlas.copy()
        for x in range(third, atlas.width):
            for y in range(atlas.height):
                if x >= third * 2 and centre is None:
                    continue
                target, base = (petal, mean) if x < third * 2 else (centre, (232, 171, 55))
                image.putpixel((x, y), tuple(min(255, round(c * t / b)) for c, t, b in zip(atlas.getpixel((x, y)), target, base)))
        target = ART / "Converted" / variant
        target.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source / "mesh.json", target / "mesh.json")
        image.save(target / "basecolor.png")
        print(variant, "recoloured")


def main(models):
    for model_id, texture in FOLIAGE.items():
        folder = models / model_id
        document, blob = read_glb(folder / "model.glb")
        meshes = list(node_meshes(document, blob))
        if len(meshes) != 1:
            raise ValueError(model_id + " expected one mesh")
        (ART / "Models" / model_id).mkdir(parents=True, exist_ok=True)
        shutil.copyfile(folder / "model.glb", ART / "Models" / model_id / "model.glb")
        target = ART / "Converted" / model_id
        write(target, to_unity(meshes[0][1], False))
        shutil.copyfile(folder / texture, target / "basecolor.png")
    document, blob = read_glb(models / "flag" / "model.glb")
    (ART / "Models" / "flag").mkdir(parents=True, exist_ok=True)
    shutil.copyfile(models / "flag" / "model.glb", ART / "Models" / "flag" / "model.glb")
    parts = dict(node_meshes(document, blob))
    for node, part in (("FlagPole", "flag_pole"), ("FlagCloth", "flag_cloth")):
        write(ART / "Converted" / part, to_unity(parts[node], True, FLAG_POLE_FOOT_GLTF))
    recolour_flowers()


if __name__ == "__main__":
    main(Path(sys.argv[1]))
