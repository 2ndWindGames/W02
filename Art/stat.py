import bpy, sys, os
sys.path.insert(0, "/home/claude/blender")
exec(open("/home/claude/blender/fishgen.py").read().split('if __name__ ==')[0])
bpy.ops.wm.read_factory_settings(use_empty=True)
mat = toon_material()
tot = 0
for k, sp in SPECIES.items():
    o = build_fish(k, sp); o.data.materials.append(mat)
    o.data.calc_loop_triangles()
    n = len(o.data.loop_triangles); tot += n
    print(f"{k:9s} verts {len(o.data.vertices):5d}  tris {n:5d}")
print("TOTAL tris (4종)", tot)
os.makedirs("/home/claude/blender/export", exist_ok=True)
bpy.ops.export_scene.gltf(filepath="/home/claude/blender/export/fish.glb",
                          export_format='GLB', export_apply=True)
print("GLB EXPORT OK")
