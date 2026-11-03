import math
import os

out_dir = "Assets/Models"
os.makedirs(out_dir, exist_ok=True)

def write_single_obj(filepath, mesh, obj_name):
    with open(filepath, 'w') as f:
        f.write(f"# Week 9 Procedural Mesh: {obj_name}\n")
        f.write(f"o {obj_name}\n")
        for v in mesh['vertices']:
            f.write(f"v {v[0]:.5f} {v[1]:.5f} {v[2]:.5f}\n")
        for vt in mesh['uvs']:
            f.write(f"vt {vt[0]:.5f} {vt[1]:.5f}\n")
        for vn in mesh['normals']:
            f.write(f"vn {vn[0]:.5f} {vn[1]:.5f} {vn[2]:.5f}\n")
        for face in mesh['faces']:
            f.write("f " + " ".join(f"{idx[0]}/{idx[1]}/{idx[2]}" for idx in face) + "\n")

def combine_meshes(mesh_list):
    combined = {'vertices': [], 'normals': [], 'uvs': [], 'faces': []}
    v_offset = 0
    for m in mesh_list:
        combined['vertices'].extend(m['vertices'])
        combined['normals'].extend(m['normals'])
        combined['uvs'].extend(m['uvs'])
        for f in m['faces']:
            new_f = [(idx[0] + v_offset, idx[1] + v_offset, idx[2] + v_offset) for idx in f]
            combined['faces'].append(new_f)
        v_offset += len(m['vertices'])
    return combined

def v_sub(a, b):
    return (a[0]-b[0], a[1]-b[1], a[2]-b[2])

def v_add(a, b):
    return (a[0]+b[0], a[1]+b[1], a[2]+b[2])

def v_scale(a, s):
    return (a[0]*s, a[1]*s, a[2]*s)

def v_dot(a, b):
    return a[0]*b[0] + a[1]*b[1] + a[2]*b[2]

def v_cross(a, b):
    return (a[1]*b[2] - a[2]*b[1], a[2]*b[0] - a[0]*b[2], a[0]*b[1] - a[1]*b[0])

def v_len(a):
    return math.sqrt(a[0]*a[0] + a[1]*a[1] + a[2]*a[2])

def v_norm(a):
    l = v_len(a)
    if l > 1e-7:
        return (a[0]/l, a[1]/l, a[2]/l)
    return (0.0, 1.0, 0.0)

def make_tube_segment(p1, p2, r1, r2, num_slices=20, close_start=False, close_end=False):
    axis = v_sub(p2, p1)
    axis_len = v_len(axis)
    axis_dir = v_norm(axis)
    
    if abs(axis_dir[1]) < 0.9:
        ref = (0.0, 1.0, 0.0)
    else:
        ref = (1.0, 0.0, 0.0)
    u_dir = v_norm(v_cross(axis_dir, ref))
    v_dir = v_norm(v_cross(axis_dir, u_dir))
    
    vertices = []
    normals = []
    uvs = []
    faces = []
    
    for i in range(num_slices):
        angle = 2.0 * math.pi * i / num_slices
        cu = math.cos(angle)
        sv = math.sin(angle)
        radial_norm = v_add(v_scale(u_dir, cu), v_scale(v_dir, sv))
        pos = v_add(p1, v_scale(radial_norm, r1))
        vertices.append(pos)
        normals.append(radial_norm)
        uvs.append((i / num_slices, 0.0))
        
    for i in range(num_slices):
        angle = 2.0 * math.pi * i / num_slices
        cu = math.cos(angle)
        sv = math.sin(angle)
        radial_norm = v_add(v_scale(u_dir, cu), v_scale(v_dir, sv))
        pos = v_add(p2, v_scale(radial_norm, r2))
        vertices.append(pos)
        normals.append(radial_norm)
        uvs.append((i / num_slices, 1.0))
        
    for i in range(num_slices):
        next_i = (i + 1) % num_slices
        v1 = i + 1
        v2 = next_i + 1
        v3 = num_slices + next_i + 1
        v4 = num_slices + i + 1
        faces.append([(v1, v1, v1), (v2, v2, v2), (v3, v3, v3)])
        faces.append([(v1, v1, v1), (v3, v3, v3), (v4, v4, v4)])
        
    if close_start:
        c_idx = len(vertices) + 1
        vertices.append(p1)
        normals.append(v_scale(axis_dir, -1.0))
        uvs.append((0.5, 0.0))
        for i in range(num_slices):
            next_i = (i + 1) % num_slices
            faces.append([(c_idx, c_idx, c_idx), (next_i + 1, next_i + 1, next_i + 1), (i + 1, i + 1, i + 1)])
            
    if close_end:
        c_idx = len(vertices) + 1
        vertices.append(p2)
        normals.append(axis_dir)
        uvs.append((0.5, 1.0))
        for i in range(num_slices):
            next_i = (i + 1) % num_slices
            faces.append([(c_idx, c_idx, c_idx), (num_slices + i + 1, num_slices + i + 1, num_slices + i + 1), (num_slices + next_i + 1, num_slices + next_i + 1, num_slices + next_i + 1)])
            
    return {'vertices': vertices, 'normals': normals, 'uvs': uvs, 'faces': faces}

def make_sphere(center, radius, num_slices=20, num_stacks=14):
    vertices = []
    normals = []
    uvs = []
    faces = []
    
    for i in range(num_stacks + 1):
        v = i / num_stacks
        phi = math.pi * i / num_stacks
        y = radius * math.cos(phi)
        r_slice = radius * math.sin(phi)
        
        for j in range(num_slices):
            u = j / num_slices
            theta = 2.0 * math.pi * j / num_slices
            x = r_slice * math.sin(theta)
            z = r_slice * math.cos(theta)
            
            p = (center[0] + x, center[1] + y, center[2] + z)
            norm = v_norm((x, y, z))
            vertices.append(p)
            normals.append(norm)
            uvs.append((u, v))
            
    for i in range(num_stacks):
        for j in range(num_slices):
            next_j = (j + 1) % num_slices
            v1 = i * num_slices + j + 1
            v2 = i * num_slices + next_j + 1
            v3 = (i + 1) * num_slices + next_j + 1
            v4 = (i + 1) * num_slices + j + 1
            faces.append([(v1, v1, v1), (v2, v2, v2), (v3, v3, v3)])
            faces.append([(v1, v1, v1), (v3, v3, v3), (v4, v4, v4)])
            
    return {'vertices': vertices, 'normals': normals, 'uvs': uvs, 'faces': faces}

def make_torus_mesh(major_r, minor_r, num_major=36, num_minor=18, normal_axis='Z'):
    vertices = []
    normals = []
    uvs = []
    faces = []
    
    for i in range(num_major):
        phi = 2.0 * math.pi * i / num_major
        u = i / num_major
        c_phi = math.cos(phi)
        s_phi = math.sin(phi)
        
        for j in range(num_minor):
            psi = 2.0 * math.pi * j / num_minor
            v = j / num_minor
            c_psi = math.cos(psi)
            s_psi = math.sin(psi)
            
            if normal_axis == 'Z':
                # Torus lying in XY plane, normal along Z
                cx = major_r * c_phi
                cy = major_r * s_phi
                cz = 0.0
                
                x = (major_r + minor_r * c_psi) * c_phi
                y = (major_r + minor_r * c_psi) * s_phi
                z = minor_r * s_psi
                
                nx = c_psi * c_phi
                ny = c_psi * s_phi
                nz = s_psi
            elif normal_axis == 'Y':
                # Torus lying in XZ plane, upright
                cx = major_r * c_phi
                cy = 0.0
                cz = major_r * s_phi
                
                x = (major_r + minor_r * c_psi) * c_phi
                y = minor_r * s_psi
                z = (major_r + minor_r * c_psi) * s_phi
                
                nx = c_psi * c_phi
                ny = s_psi
                nz = c_psi * s_phi
                
            vertices.append((x, y, z))
            normals.append(v_norm((nx, ny, nz)))
            uvs.append((u, v))
            
    for i in range(num_major):
        next_i = (i + 1) % num_major
        for j in range(num_minor):
            next_j = (j + 1) % num_minor
            v1 = i * num_minor + j + 1
            v2 = next_i * num_minor + j + 1
            v3 = next_i * num_minor + next_j + 1
            v4 = i * num_minor + next_j + 1
            faces.append([(v1, v1, v1), (v2, v2, v2), (v3, v3, v3)])
            faces.append([(v1, v1, v1), (v3, v3, v3), (v4, v4, v4)])
            
    return {'vertices': vertices, 'normals': normals, 'uvs': uvs, 'faces': faces}

def make_capsule_mesh(radius, cylinder_h, num_slices=28, num_cap_stacks=10):
    vertices = []
    normals = []
    uvs = []
    faces = []
    
    half_h = cylinder_h * 0.5
    # Bottom hemisphere (phi from -pi/2 to 0)
    for i in range(num_cap_stacks):
        v = i / (num_cap_stacks * 2 + 10)
        phi = -0.5 * math.pi + (0.5 * math.pi * i / num_cap_stacks)
        y = -half_h + radius * math.sin(phi)
        r_slice = radius * math.cos(phi)
        
        for j in range(num_slices):
            u = j / num_slices
            theta = 2.0 * math.pi * j / num_slices
            x = r_slice * math.sin(theta)
            z = r_slice * math.cos(theta)
            vertices.append((x, y, z))
            normals.append(v_norm((x, radius * math.sin(phi), z)))
            uvs.append((u, v))
            
    # Middle cylinder (2 rings at -half_h and +half_h)
    for k, y in enumerate([-half_h, half_h]):
        v = 0.4 + 0.2 * k
        for j in range(num_slices):
            u = j / num_slices
            theta = 2.0 * math.pi * j / num_slices
            x = radius * math.sin(theta)
            z = radius * math.cos(theta)
            vertices.append((x, y, z))
            normals.append((math.sin(theta), 0.0, math.cos(theta)))
            uvs.append((u, v))
            
    # Top hemisphere (phi from 0 to pi/2)
    for i in range(1, num_cap_stacks + 1):
        v = 0.6 + 0.4 * (i / num_cap_stacks)
        phi = (0.5 * math.pi * i / num_cap_stacks)
        y = half_h + radius * math.sin(phi)
        r_slice = radius * math.cos(phi)
        
        for j in range(num_slices):
            u = j / num_slices
            theta = 2.0 * math.pi * j / num_slices
            x = r_slice * math.sin(theta)
            z = r_slice * math.cos(theta)
            vertices.append((x, y, z))
            normals.append(v_norm((x, radius * math.sin(phi), z)))
            uvs.append((u, v))
            
    total_rings = num_cap_stacks + 2 + num_cap_stacks
    for r in range(total_rings - 1):
        for j in range(num_slices):
            next_j = (j + 1) % num_slices
            v1 = r * num_slices + j + 1
            v2 = r * num_slices + next_j + 1
            v3 = (r + 1) * num_slices + next_j + 1
            v4 = (r + 1) * num_slices + j + 1
            faces.append([(v1, v1, v1), (v2, v2, v2), (v3, v3, v3)])
            faces.append([(v1, v1, v1), (v3, v3, v3), (v4, v4, v4)])
            
    return {'vertices': vertices, 'normals': normals, 'uvs': uvs, 'faces': faces}

def make_star_prism(r_inner, r_outer, height, num_points=5, y_center=0.0):
    total_pts = num_points * 2
    ring = []
    for i in range(total_pts):
        angle = math.pi * i / num_points - math.pi * 0.5
        r = r_outer if (i % 2 == 0) else r_inner
        ring.append((r * math.cos(angle), r * math.sin(angle)))
        
    half_h = height * 0.5
    y_bot = y_center - half_h
    y_top = y_center + half_h
    
    vertices = []
    normals = []
    uvs = []
    faces = []
    
    # Bottom ring
    for i, (x, z) in enumerate(ring):
        vertices.append((x, y_bot, z))
        # Normal facing outward
        angle = math.pi * i / num_points - math.pi * 0.5
        normals.append((math.cos(angle), 0.0, math.sin(angle)))
        uvs.append((i / total_pts, 0.0))
        
    # Top ring
    for i, (x, z) in enumerate(ring):
        vertices.append((x, y_top, z))
        angle = math.pi * i / num_points - math.pi * 0.5
        normals.append((math.cos(angle), 0.0, math.sin(angle)))
        uvs.append((i / total_pts, 1.0))
        
    # Side quads
    for i in range(total_pts):
        next_i = (i + 1) % total_pts
        v1 = i + 1
        v2 = next_i + 1
        v3 = total_pts + next_i + 1
        v4 = total_pts + i + 1
        faces.append([(v1, v1, v1), (v2, v2, v2), (v3, v3, v3)])
        faces.append([(v1, v1, v1), (v3, v3, v3), (v4, v4, v4)])
        
    # Bottom cap
    bot_c = len(vertices) + 1
    vertices.append((0.0, y_bot, 0.0))
    normals.append((0.0, -1.0, 0.0))
    uvs.append((0.5, 0.5))
    for i in range(total_pts):
        next_i = (i + 1) % total_pts
        faces.append([(bot_c, bot_c, bot_c), (next_i + 1, next_i + 1, next_i + 1), (i + 1, i + 1, i + 1)])
        
    # Top cap
    top_c = len(vertices) + 1
    vertices.append((0.0, y_top, 0.0))
    normals.append((0.0, 1.0, 0.0))
    uvs.append((0.5, 0.5))
    for i in range(total_pts):
        next_i = (i + 1) % total_pts
        faces.append([(top_c, top_c, top_c), (total_pts + i + 1, total_pts + i + 1, total_pts + i + 1), (total_pts + next_i + 1, total_pts + next_i + 1, total_pts + next_i + 1)])
        
    return {'vertices': vertices, 'normals': normals, 'uvs': uvs, 'faces': faces}

def make_box(min_x, max_x, min_y, max_y, min_z, max_z):
    vertices = [
        (min_x, min_y, max_z), (max_x, min_y, max_z), (max_x, max_y, max_z), (min_x, max_y, max_z),
        (max_x, min_y, min_z), (min_x, min_y, min_z), (min_x, max_y, min_z), (max_x, max_y, min_z),
        (min_x, max_y, max_z), (max_x, max_y, max_z), (max_x, max_y, min_z), (min_x, max_y, min_z),
        (min_x, min_y, min_z), (max_x, min_y, min_z), (max_x, min_y, max_z), (min_x, min_y, max_z),
        (max_x, min_y, max_z), (max_x, min_y, min_z), (max_x, max_y, min_z), (max_x, max_y, max_z),
        (min_x, min_y, min_z), (min_x, min_y, max_z), (min_x, max_y, max_z), (min_x, max_y, min_z)
    ]
    normals = [
        (0, 0, 1), (0, 0, 1), (0, 0, 1), (0, 0, 1),
        (0, 0, -1), (0, 0, -1), (0, 0, -1), (0, 0, -1),
        (0, 1, 0), (0, 1, 0), (0, 1, 0), (0, 1, 0),
        (0, -1, 0), (0, -1, 0), (0, -1, 0), (0, -1, 0),
        (1, 0, 0), (1, 0, 0), (1, 0, 0), (1, 0, 0),
        (-1, 0, 0), (-1, 0, 0), (-1, 0, 0), (-1, 0, 0)
    ]
    uvs = [
        (0, 0), (1, 0), (1, 1), (0, 1),
        (0, 0), (1, 0), (1, 1), (0, 1),
        (0, 0), (1, 0), (1, 1), (0, 1),
        (0, 0), (1, 0), (1, 1), (0, 1),
        (0, 0), (1, 0), (1, 1), (0, 1),
        (0, 0), (1, 0), (1, 1), (0, 1)
    ]
    faces = []
    for face_idx in range(6):
        base = face_idx * 4 + 1
        faces.append([(base, base, base), (base+1, base+1, base+1), (base+2, base+2, base+2)])
        faces.append([(base, base, base), (base+2, base+2, base+2), (base+3, base+3, base+3)])
    return {'vertices': vertices, 'normals': normals, 'uvs': uvs, 'faces': faces}

def make_rounded_rect_prism(half_w, half_d, height, corner_r, num_corner_pts=6, y_center=0.0):
    half_h = height * 0.5
    y_bot = y_center - half_h
    y_top = y_center + half_h
    
    # Generate 2D rounded rectangle contour
    pts = []
    cx_pos = half_w - corner_r
    cz_pos = half_d - corner_r
    
    corners = [
        (cx_pos, cz_pos, 0.0),
        (-cx_pos, cz_pos, 0.5 * math.pi),
        (-cx_pos, -cz_pos, 1.0 * math.pi),
        (cx_pos, -cz_pos, 1.5 * math.pi)
    ]
    
    for cx, cz, base_angle in corners:
        for i in range(num_corner_pts):
            angle = base_angle + (0.5 * math.pi * i / num_corner_pts)
            pts.append((cx + corner_r * math.cos(angle), cz + corner_r * math.sin(angle)))
            
    total_pts = len(pts)
    vertices = []
    normals = []
    uvs = []
    faces = []
    
    # Bottom ring
    for i, (x, z) in enumerate(pts):
        vertices.append((x, y_bot, z))
        normals.append(v_norm((x, 0.0, z)))
        uvs.append((i / total_pts, 0.0))
        
    # Top ring
    for i, (x, z) in enumerate(pts):
        vertices.append((x, y_top, z))
        normals.append(v_norm((x, 0.0, z)))
        uvs.append((i / total_pts, 1.0))
        
    # Sides
    for i in range(total_pts):
        next_i = (i + 1) % total_pts
        v1 = i + 1
        v2 = next_i + 1
        v3 = total_pts + next_i + 1
        v4 = total_pts + i + 1
        faces.append([(v1, v1, v1), (v2, v2, v2), (v3, v3, v3)])
        faces.append([(v1, v1, v1), (v3, v3, v3), (v4, v4, v4)])
        
    # Bottom cap
    bot_c = len(vertices) + 1
    vertices.append((0.0, y_bot, 0.0))
    normals.append((0.0, -1.0, 0.0))
    uvs.append((0.5, 0.5))
    for i in range(total_pts):
        next_i = (i + 1) % total_pts
        faces.append([(bot_c, bot_c, bot_c), (next_i + 1, next_i + 1, next_i + 1), (i + 1, i + 1, i + 1)])
        
    # Top cap
    top_c = len(vertices) + 1
    vertices.append((0.0, y_top, 0.0))
    normals.append((0.0, 1.0, 0.0))
    uvs.append((0.5, 0.5))
    for i in range(total_pts):
        next_i = (i + 1) % total_pts
        faces.append([(top_c, top_c, top_c), (total_pts + i + 1, total_pts + i + 1, total_pts + i + 1), (total_pts + next_i + 1, total_pts + next_i + 1, total_pts + next_i + 1)])
        
    return {'vertices': vertices, 'normals': normals, 'uvs': uvs, 'faces': faces}


# -------------------------------------------------------------
# 1. PETROL CONTAINER (JERRYCAN)
# -------------------------------------------------------------
print("Generating 1: Petrol Container (Jerrycan)...")
# Body
jerry_glass_body = make_rounded_rect_prism(half_w=0.17, half_d=0.09, height=0.42, corner_r=0.035, y_center=0.0)
# Top handle
handle_bar = make_tube_segment(p1=(-0.08, 0.25, 0.0), p2=(0.08, 0.25, 0.0), r1=0.015, r2=0.015, num_slices=12)
handle_leg1 = make_tube_segment(p1=(-0.08, 0.21, 0.0), p2=(-0.08, 0.25, 0.0), r1=0.015, r2=0.015, num_slices=12)
handle_leg2 = make_tube_segment(p1=(0.08, 0.21, 0.0), p2=(0.08, 0.25, 0.0), r1=0.015, r2=0.015, num_slices=12)
# Spout neck & cap
spout_neck = make_tube_segment(p1=(-0.12, 0.21, 0.0), p2=(-0.13, 0.27, 0.0), r1=0.022, r2=0.020, num_slices=16)
spout_cap = make_tube_segment(p1=(-0.13, 0.27, 0.0), p2=(-0.135, 0.29, 0.0), r1=0.024, r2=0.024, num_slices=16, close_end=True)
petrol_glass = combine_meshes([jerry_glass_body, handle_bar, handle_leg1, handle_leg2, spout_neck, spout_cap])

# Fluid volume
petrol_fluid = make_rounded_rect_prism(half_w=0.155, half_d=0.078, height=0.39, corner_r=0.028, y_center=-0.005)

write_single_obj("Assets/Models/Week9_PetrolContainer_Glass.obj", petrol_glass, "Week9_PetrolContainer_Glass")
write_single_obj("Assets/Models/Week9_PetrolContainer_Fluid.obj", petrol_fluid, "Week9_PetrolContainer_Fluid")


# -------------------------------------------------------------
# 2. STAR-SHAPED BOX
# -------------------------------------------------------------
print("Generating 2: Star-shaped Box...")
star_glass_body = make_star_prism(r_inner=0.125, r_outer=0.26, height=0.44, num_points=5, y_center=0.0)
star_lid_rim = make_star_prism(r_inner=0.135, r_outer=0.275, height=0.03, num_points=5, y_center=0.235)
star_base = make_star_prism(r_inner=0.135, r_outer=0.275, height=0.03, num_points=5, y_center=-0.235)
star_box_glass = combine_meshes([star_glass_body, star_lid_rim, star_base])

star_box_fluid = make_star_prism(r_inner=0.112, r_outer=0.238, height=0.40, num_points=5, y_center=0.0)

write_single_obj("Assets/Models/Week9_StarBox_Glass.obj", star_box_glass, "Week9_StarBox_Glass")
write_single_obj("Assets/Models/Week9_StarBox_Fluid.obj", star_box_fluid, "Week9_StarBox_Fluid")


# -------------------------------------------------------------
# 3. TOROIDAL TANK
# -------------------------------------------------------------
print("Generating 3: Toroidal Tank...")
# Torus standing vertically (normal along Z, circle in XY plane)
torus_glass_ring = make_torus_mesh(major_r=0.19, minor_r=0.072, num_major=36, num_minor=20, normal_axis='Z')
# Top filler port
torus_top_port = make_tube_segment(p1=(0.0, 0.25, 0.0), p2=(0.0, 0.30, 0.0), r1=0.026, r2=0.026, num_slices=16)
torus_top_cap = make_tube_segment(p1=(0.0, 0.30, 0.0), p2=(0.0, 0.32, 0.0), r1=0.030, r2=0.030, num_slices=16, close_end=True)
# Bottom support cradle legs
leg_left = make_tube_segment(p1=(-0.14, -0.22, -0.05), p2=(-0.14, -0.29, -0.05), r1=0.016, r2=0.016, num_slices=12, close_end=True)
leg_right = make_tube_segment(p1=(0.14, -0.22, -0.05), p2=(0.14, -0.29, -0.05), r1=0.016, r2=0.016, num_slices=12, close_end=True)
leg_left_f = make_tube_segment(p1=(-0.14, -0.22, 0.05), p2=(-0.14, -0.29, 0.05), r1=0.016, r2=0.016, num_slices=12, close_end=True)
leg_right_f = make_tube_segment(p1=(0.14, -0.22, 0.05), p2=(0.14, -0.29, 0.05), r1=0.016, r2=0.016, num_slices=12, close_end=True)
toroidal_glass = combine_meshes([torus_glass_ring, torus_top_port, torus_top_cap, leg_left, leg_right, leg_left_f, leg_right_f])

# Fluid volume
toroidal_fluid = make_torus_mesh(major_r=0.19, minor_r=0.063, num_major=36, num_minor=18, normal_axis='Z')

write_single_obj("Assets/Models/Week9_ToroidalTank_Glass.obj", toroidal_glass, "Week9_ToroidalTank_Glass")
write_single_obj("Assets/Models/Week9_ToroidalTank_Fluid.obj", toroidal_fluid, "Week9_ToroidalTank_Fluid")


# -------------------------------------------------------------
# 4. Y-JUNCTION MANIFOLD (IMPORTANT)
# -------------------------------------------------------------
print("Generating 4: Y-junction Manifold (Important)...")
# Trunk tube
y_trunk = make_tube_segment(p1=(0.0, -0.25, 0.0), p2=(0.0, -0.02, 0.0), r1=0.062, r2=0.062, num_slices=24, close_start=True)
# Junction center sphere
y_center_sphere = make_sphere(center=(0.0, -0.02, 0.0), radius=0.068, num_slices=20, num_stacks=12)
# Left branch (angles to x = -0.18, y = 0.23)
y_branch_l = make_tube_segment(p1=(0.0, -0.02, 0.0), p2=(-0.18, 0.23, 0.0), r1=0.054, r2=0.050, num_slices=24, close_end=True)
# Right branch (angles to x = +0.18, y = 0.23)
y_branch_r = make_tube_segment(p1=(0.0, -0.02, 0.0), p2=(0.18, 0.23, 0.0), r1=0.054, r2=0.050, num_slices=24, close_end=True)
# Flange collars
flange_bot = make_tube_segment(p1=(0.0, -0.26, 0.0), p2=(0.0, -0.24, 0.0), r1=0.082, r2=0.082, num_slices=20, close_start=True, close_end=True)
# Outlet collars
flange_l = make_sphere(center=(-0.18, 0.23, 0.0), radius=0.065, num_slices=16, num_stacks=10)
flange_r = make_sphere(center=(0.18, 0.23, 0.0), radius=0.065, num_slices=16, num_stacks=10)
# Manifold support cross-bracket
y_cross_bar = make_tube_segment(p1=(-0.10, 0.11, 0.0), p2=(0.10, 0.11, 0.0), r1=0.016, r2=0.016, num_slices=12)

y_manifold_glass = combine_meshes([y_trunk, y_center_sphere, y_branch_l, y_branch_r, flange_bot, flange_l, flange_r, y_cross_bar])

# Fluid volume
y_f_trunk = make_tube_segment(p1=(0.0, -0.24, 0.0), p2=(0.0, -0.02, 0.0), r1=0.053, r2=0.053, num_slices=20, close_start=True)
y_f_center = make_sphere(center=(0.0, -0.02, 0.0), radius=0.058, num_slices=18, num_stacks=10)
y_f_branch_l = make_tube_segment(p1=(0.0, -0.02, 0.0), p2=(-0.175, 0.22, 0.0), r1=0.046, r2=0.042, num_slices=20, close_end=True)
y_f_branch_r = make_tube_segment(p1=(0.0, -0.02, 0.0), p2=(0.175, 0.22, 0.0), r1=0.046, r2=0.042, num_slices=20, close_end=True)
y_manifold_fluid = combine_meshes([y_f_trunk, y_f_center, y_f_branch_l, y_f_branch_r])

write_single_obj("Assets/Models/Week9_YJunctionManifold_Glass.obj", y_manifold_glass, "Week9_YJunctionManifold_Glass")
write_single_obj("Assets/Models/Week9_YJunctionManifold_Fluid.obj", y_manifold_fluid, "Week9_YJunctionManifold_Fluid")


# -------------------------------------------------------------
# 5. CAPSULE-SHAPED TANK
# -------------------------------------------------------------
print("Generating 5: Capsule-shaped Tank...")
capsule_glass_body = make_capsule_mesh(radius=0.145, cylinder_h=0.28, num_slices=32, num_cap_stacks=10)
# Reinforcing girth bands
band1 = make_tube_segment(p1=(0.0, -0.08, 0.0), p2=(0.0, -0.06, 0.0), r1=0.158, r2=0.158, num_slices=24, close_start=True, close_end=True)
band2 = make_tube_segment(p1=(0.0, 0.06, 0.0), p2=(0.0, 0.08, 0.0), r1=0.158, r2=0.158, num_slices=24, close_start=True, close_end=True)
# Top valve nozzle
cap_nozzle = make_tube_segment(p1=(0.0, 0.28, 0.0), p2=(0.0, 0.33, 0.0), r1=0.028, r2=0.028, num_slices=16, close_end=True)
# 4 Support legs
c_leg1 = make_tube_segment(p1=(-0.09, -0.22, -0.09), p2=(-0.11, -0.30, -0.11), r1=0.015, r2=0.015, num_slices=12, close_end=True)
c_leg2 = make_tube_segment(p1=(0.09, -0.22, -0.09), p2=(0.11, -0.30, -0.11), r1=0.015, r2=0.015, num_slices=12, close_end=True)
c_leg3 = make_tube_segment(p1=(-0.09, -0.22, 0.09), p2=(-0.11, -0.30, 0.11), r1=0.015, r2=0.015, num_slices=12, close_end=True)
c_leg4 = make_tube_segment(p1=(0.09, -0.22, 0.09), p2=(0.11, -0.30, 0.11), r1=0.015, r2=0.015, num_slices=12, close_end=True)
capsule_glass = combine_meshes([capsule_glass_body, band1, band2, cap_nozzle, c_leg1, c_leg2, c_leg3, c_leg4])

capsule_fluid = make_capsule_mesh(radius=0.132, cylinder_h=0.26, num_slices=28, num_cap_stacks=8)

write_single_obj("Assets/Models/Week9_CapsuleTank_Glass.obj", capsule_glass, "Week9_CapsuleTank_Glass")
write_single_obj("Assets/Models/Week9_CapsuleTank_Fluid.obj", capsule_fluid, "Week9_CapsuleTank_Fluid")


# -------------------------------------------------------------
# 6. TREE BRANCHED STRUCTURE
# -------------------------------------------------------------
print("Generating 6: Tree Branched Structure...")
# Base trunk
trunk = make_tube_segment(p1=(0.0, -0.25, 0.0), p2=(0.0, -0.08, 0.0), r1=0.060, r2=0.052, num_slices=20, close_start=True)
trunk_pedestal = make_tube_segment(p1=(0.0, -0.27, 0.0), p2=(0.0, -0.25, 0.0), r1=0.095, r2=0.095, num_slices=24, close_start=True, close_end=True)
node0 = make_sphere(center=(0.0, -0.08, 0.0), radius=0.058, num_slices=16, num_stacks=10)

# Tier 1 branches (3 branches)
b1_end = (-0.14, 0.06, 0.04)
b2_end = (0.14, 0.06, 0.04)
b3_end = (0.00, 0.08, -0.12)
branch1 = make_tube_segment(p1=(0.0, -0.08, 0.0), p2=b1_end, r1=0.045, r2=0.038, num_slices=16)
branch2 = make_tube_segment(p1=(0.0, -0.08, 0.0), p2=b2_end, r1=0.045, r2=0.038, num_slices=16)
branch3 = make_tube_segment(p1=(0.0, -0.08, 0.0), p2=b3_end, r1=0.045, r2=0.038, num_slices=16)
node1 = make_sphere(center=b1_end, radius=0.042, num_slices=14, num_stacks=8)
node2 = make_sphere(center=b2_end, radius=0.042, num_slices=14, num_stacks=8)
node3 = make_sphere(center=b3_end, radius=0.042, num_slices=14, num_stacks=8)

# Tier 2 branches (2 per node = 6 sub-branches)
sub_b1a = make_tube_segment(p1=b1_end, p2=(-0.21, 0.22, 0.07), r1=0.032, r2=0.026, num_slices=14)
sub_b1b = make_tube_segment(p1=b1_end, p2=(-0.08, 0.24, 0.02), r1=0.032, r2=0.026, num_slices=14)
sub_b2a = make_tube_segment(p1=b2_end, p2=(0.08, 0.24, 0.02), r1=0.032, r2=0.026, num_slices=14)
sub_b2b = make_tube_segment(p1=b2_end, p2=(0.21, 0.22, 0.07), r1=0.032, r2=0.026, num_slices=14)
sub_b3a = make_tube_segment(p1=b3_end, p2=(-0.07, 0.23, -0.18), r1=0.032, r2=0.026, num_slices=14)
sub_b3b = make_tube_segment(p1=b3_end, p2=(0.07, 0.23, -0.18), r1=0.032, r2=0.026, num_slices=14)

# Terminal reservoir bulbs
tip_bulbs = [
    make_sphere(center=(-0.21, 0.22, 0.07), radius=0.036, num_slices=12, num_stacks=8),
    make_sphere(center=(-0.08, 0.24, 0.02), radius=0.036, num_slices=12, num_stacks=8),
    make_sphere(center=(0.08, 0.24, 0.02), radius=0.036, num_slices=12, num_stacks=8),
    make_sphere(center=(0.21, 0.22, 0.07), radius=0.036, num_slices=12, num_stacks=8),
    make_sphere(center=(-0.07, 0.23, -0.18), radius=0.036, num_slices=12, num_stacks=8),
    make_sphere(center=(0.07, 0.23, -0.18), radius=0.036, num_slices=12, num_stacks=8)
]

tree_glass = combine_meshes([trunk, trunk_pedestal, node0, branch1, branch2, branch3, node1, node2, node3, sub_b1a, sub_b1b, sub_b2a, sub_b2b, sub_b3a, sub_b3b] + tip_bulbs)

# Fluid tree
f_trunk = make_tube_segment(p1=(0.0, -0.24, 0.0), p2=(0.0, -0.08, 0.0), r1=0.050, r2=0.044, num_slices=16, close_start=True)
f_node0 = make_sphere(center=(0.0, -0.08, 0.0), radius=0.048, num_slices=14, num_stacks=8)
f_b1 = make_tube_segment(p1=(0.0, -0.08, 0.0), p2=b1_end, r1=0.038, r2=0.032, num_slices=14)
f_b2 = make_tube_segment(p1=(0.0, -0.08, 0.0), p2=b2_end, r1=0.038, r2=0.032, num_slices=14)
f_b3 = make_tube_segment(p1=(0.0, -0.08, 0.0), p2=b3_end, r1=0.038, r2=0.032, num_slices=14)
f_node1 = make_sphere(center=b1_end, radius=0.035, num_slices=12, num_stacks=8)
f_node2 = make_sphere(center=b2_end, radius=0.035, num_slices=12, num_stacks=8)
f_node3 = make_sphere(center=b3_end, radius=0.035, num_slices=12, num_stacks=8)
f_sub_b1a = make_tube_segment(p1=b1_end, p2=(-0.21, 0.22, 0.07), r1=0.026, r2=0.021, num_slices=12)
f_sub_b1b = make_tube_segment(p1=b1_end, p2=(-0.08, 0.24, 0.02), r1=0.026, r2=0.021, num_slices=12)
f_sub_b2a = make_tube_segment(p1=b2_end, p2=(0.08, 0.24, 0.02), r1=0.026, r2=0.021, num_slices=12)
f_sub_b2b = make_tube_segment(p1=b2_end, p2=(0.21, 0.22, 0.07), r1=0.026, r2=0.021, num_slices=12)
f_sub_b3a = make_tube_segment(p1=b3_end, p2=(-0.07, 0.23, -0.18), r1=0.026, r2=0.021, num_slices=12)
f_sub_b3b = make_tube_segment(p1=b3_end, p2=(0.07, 0.23, -0.18), r1=0.026, r2=0.021, num_slices=12)
f_bulbs = [
    make_sphere(center=(-0.21, 0.22, 0.07), radius=0.029, num_slices=10, num_stacks=6),
    make_sphere(center=(-0.08, 0.24, 0.02), radius=0.029, num_slices=10, num_stacks=6),
    make_sphere(center=(0.08, 0.24, 0.02), radius=0.029, num_slices=10, num_stacks=6),
    make_sphere(center=(0.21, 0.22, 0.07), radius=0.029, num_slices=10, num_stacks=6),
    make_sphere(center=(-0.07, 0.23, -0.18), radius=0.029, num_slices=10, num_stacks=6),
    make_sphere(center=(0.07, 0.23, -0.18), radius=0.029, num_slices=10, num_stacks=6)
]

tree_fluid = combine_meshes([f_trunk, f_node0, f_b1, f_b2, f_b3, f_node1, f_node2, f_node3, f_sub_b1a, f_sub_b1b, f_sub_b2a, f_sub_b2b, f_sub_b3a, f_sub_b3b] + f_bulbs)

write_single_obj("Assets/Models/Week9_TreeBranched_Glass.obj", tree_glass, "Week9_TreeBranched_Glass")
write_single_obj("Assets/Models/Week9_TreeBranched_Fluid.obj", tree_fluid, "Week9_TreeBranched_Fluid")


# -------------------------------------------------------------
# 7. MAZE (MULTICHAMBERED)
# -------------------------------------------------------------
print("Generating 7: Maze (Multichambered)...")
# Outer glass box
outer_glass_box = make_box(-0.22, 0.22, -0.22, 0.22, -0.09, 0.09)
# Internal glass baffles
# Baffle 1 at x = -0.11, leaves bottom slot from y = -0.22 to -0.10
baffle1 = make_box(-0.115, -0.105, -0.10, 0.22, -0.085, 0.085)
# Baffle 2 at x = 0.00, leaves top overflow weir from y = 0.09 to 0.22
baffle2 = make_box(-0.005, 0.005, -0.22, 0.09, -0.085, 0.085)
# Baffle 3 at x = +0.11, leaves bottom slot from y = -0.22 to -0.10
baffle3 = make_box(0.105, 0.115, -0.10, 0.22, -0.085, 0.085)
# Top filler necks
top_neck1 = make_tube_segment(p1=(-0.16, 0.22, 0.0), p2=(-0.16, 0.27, 0.0), r1=0.022, r2=0.022, num_slices=14, close_end=True)
top_neck2 = make_tube_segment(p1=(0.16, 0.22, 0.0), p2=(0.16, 0.27, 0.0), r1=0.022, r2=0.022, num_slices=14, close_end=True)
maze_stand = make_box(-0.24, 0.24, -0.25, -0.22, -0.11, 0.11)

maze_glass = combine_meshes([outer_glass_box, baffle1, baffle2, baffle3, top_neck1, top_neck2, maze_stand])

# Multichamber fluid: 4 chambers + 3 connecting passages
ch1_fluid = make_box(-0.205, -0.120, -0.20, 0.20, -0.075, 0.075)
ch2_fluid = make_box(-0.100, -0.010, -0.20, 0.20, -0.075, 0.075)
ch3_fluid = make_box(0.010, 0.100, -0.20, 0.20, -0.075, 0.075)
ch4_fluid = make_box(0.120, 0.205, -0.20, 0.20, -0.075, 0.075)
# Passages:
# Underflow slot 1 between Ch1 & Ch2 (y in [-0.20, -0.11])
slot1_fluid = make_box(-0.125, -0.095, -0.20, -0.11, -0.070, 0.070)
# Overflow weir 2 between Ch2 & Ch3 (y in [0.08, 0.20])
slot2_fluid = make_box(-0.015, 0.015, 0.08, 0.20, -0.070, 0.070)
# Underflow slot 3 between Ch3 & Ch4 (y in [-0.20, -0.11])
slot3_fluid = make_box(0.095, 0.125, -0.20, -0.11, -0.070, 0.070)

maze_fluid = combine_meshes([ch1_fluid, ch2_fluid, ch3_fluid, ch4_fluid, slot1_fluid, slot2_fluid, slot3_fluid])

write_single_obj("Assets/Models/Week9_MazeMultichamber_Glass.obj", maze_glass, "Week9_MazeMultichamber_Glass")
write_single_obj("Assets/Models/Week9_MazeMultichamber_Fluid.obj", maze_fluid, "Week9_MazeMultichamber_Fluid")

print("All 7 Week 9 structures generated successfully!")
