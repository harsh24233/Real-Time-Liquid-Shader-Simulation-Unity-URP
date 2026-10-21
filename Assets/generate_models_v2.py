import math, os

def write_single_obj(filepath, mesh, obj_name):
    with open(filepath, 'w') as f:
        f.write(f"# Procedural Mesh: {obj_name}\n")
        f.write(f"o {obj_name}\n")
        for v in mesh['vertices']:
            f.write(f"v {v[0]:.5f} {v[1]:.5f} {v[2]:.5f}\n")
        for vt in mesh['uvs']:
            f.write(f"vt {vt[0]:.5f} {vt[1]:.5f}\n")
        for vn in mesh['normals']:
            f.write(f"vn {vn[0]:.5f} {vn[1]:.5f} {vn[2]:.5f}\n")
        for face in mesh['faces']:
            f.write("f " + " ".join(f"{idx[0]}/{idx[1]}/{idx[2]}" for idx in face) + "\n")

def make_lathe_mesh(profile, num_slices=36, close_top=True, close_bottom=True):
    vertices = []
    normals = []
    uvs = []
    faces = []
    num_rings = len(profile)
    
    for r_idx, (y, r) in enumerate(profile):
        v_coord = r_idx / (num_rings - 1)
        for s_idx in range(num_slices):
            u_coord = s_idx / num_slices
            theta = 2.0 * math.pi * s_idx / num_slices
            cos_t = math.cos(theta)
            sin_t = math.sin(theta)
            
            x = r * cos_t
            z = r * sin_t
            vertices.append((x, y, z))
            uvs.append((u_coord, v_coord))
            
            if r_idx == 0:
                dy = profile[1][0] - profile[0][0]
                dr = profile[1][1] - profile[0][1]
            elif r_idx == num_rings - 1:
                dy = profile[-1][0] - profile[-2][0]
                dr = profile[-1][1] - profile[-2][1]
            else:
                dy = profile[r_idx + 1][0] - profile[r_idx - 1][0]
                dr = profile[r_idx + 1][1] - profile[r_idx - 1][1]
                
            length = math.sqrt(dy * dy + dr * dr)
            if length > 0.00001:
                ny = -dr / length
                nr = dy / length
            else:
                ny = 0.0
                nr = 1.0
                
            normals.append((nr * cos_t, ny, nr * sin_t))
            
    for r_idx in range(num_rings - 1):
        for s_idx in range(num_slices):
            s_next = (s_idx + 1) % num_slices
            v1 = r_idx * num_slices + s_idx + 1
            v2 = r_idx * num_slices + s_next + 1
            v3 = (r_idx + 1) * num_slices + s_next + 1
            v4 = (r_idx + 1) * num_slices + s_idx + 1
            faces.append([(v1, v1, v1), (v2, v2, v2), (v3, v3, v3)])
            faces.append([(v1, v1, v1), (v3, v3, v3), (v4, v4, v4)])
            
    if close_bottom:
        bottom_y, _ = profile[0]
        bot_center_idx = len(vertices) + 1
        vertices.append((0.0, bottom_y, 0.0))
        uvs.append((0.5, 0.0))
        normals.append((0.0, -1.0, 0.0))
        for s_idx in range(num_slices):
            s_next = (s_idx + 1) % num_slices
            v1 = s_idx + 1
            v2 = s_next + 1
            faces.append([(bot_center_idx, bot_center_idx, bot_center_idx), (v2, v2, v2), (v1, v1, v1)])
            
    if close_top:
        top_y, _ = profile[-1]
        top_center_idx = len(vertices) + 1
        vertices.append((0.0, top_y, 0.0))
        uvs.append((0.5, 1.0))
        normals.append((0.0, 1.0, 0.0))
        top_ring_start = (num_rings - 1) * num_slices
        for s_idx in range(num_slices):
            s_next = (s_idx + 1) % num_slices
            v1 = top_ring_start + s_idx + 1
            v2 = top_ring_start + s_next + 1
            faces.append([(top_center_idx, top_center_idx, top_center_idx), (v1, v1, v1), (v2, v2, v2)])
            
    return {'vertices': vertices, 'normals': normals, 'uvs': uvs, 'faces': faces}

def make_prism_mesh(corners_func, y_levels, close_top=True, close_bottom=True):
    vertices = []
    normals = []
    uvs = []
    faces = []
    num_pts = len(corners_func(y_levels[0]))
    num_levels = len(y_levels)
    
    for l_idx, y in enumerate(y_levels):
        pts = corners_func(y)
        v_coord = l_idx / (num_levels - 1)
        for p_idx, (x, z) in enumerate(pts):
            u_coord = p_idx / num_pts
            vertices.append((x, y, z))
            uvs.append((u_coord, v_coord))
            rad = math.sqrt(x*x + z*z)
            if rad > 0.0001:
                normals.append((x/rad, 0.0, z/rad))
            else:
                normals.append((0.0, 1.0, 0.0))
                
    for l_idx in range(num_levels - 1):
        for p_idx in range(num_pts):
            p_next = (p_idx + 1) % num_pts
            v1 = l_idx * num_pts + p_idx + 1
            v2 = l_idx * num_pts + p_next + 1
            v3 = (l_idx + 1) * num_pts + p_next + 1
            v4 = (l_idx + 1) * num_pts + p_idx + 1
            faces.append([(v1, v1, v1), (v2, v2, v2), (v3, v3, v3)])
            faces.append([(v1, v1, v1), (v3, v3, v3), (v4, v4, v4)])
            
    if close_bottom:
        bot_idx = len(vertices) + 1
        vertices.append((0.0, y_levels[0], 0.0))
        uvs.append((0.5, 0.0))
        normals.append((0.0, -1.0, 0.0))
        for p_idx in range(num_pts):
            p_next = (p_idx + 1) % num_pts
            v1 = p_idx + 1
            v2 = p_next + 1
            faces.append([(bot_idx, bot_idx, bot_idx), (v2, v2, v2), (v1, v1, v1)])
            
    if close_top:
        top_idx = len(vertices) + 1
        vertices.append((0.0, y_levels[-1], 0.0))
        uvs.append((0.5, 1.0))
        normals.append((0.0, 1.0, 0.0))
        top_start = (num_levels - 1) * num_pts
        for p_idx in range(num_pts):
            p_next = (p_idx + 1) % num_pts
            v1 = top_start + p_idx + 1
            v2 = top_start + p_next + 1
            faces.append([(top_idx, top_idx, top_idx), (v1, v1, v1), (v2, v2, v2)])
            
    return {'vertices': vertices, 'normals': normals, 'uvs': uvs, 'faces': faces}

os.makedirs('Assets/Models', exist_ok=True)

# 1. Conical Flask
conical_glass = [
    (-0.12, 0.070), (-0.115, 0.075), (-0.10, 0.073), (-0.05, 0.053),
    (0.00, 0.035), (0.04, 0.022), (0.05, 0.019), (0.09, 0.019),
    (0.11, 0.019), (0.12, 0.023)
]
conical_fluid = [
    (-0.115, 0.066), (-0.10, 0.069), (-0.05, 0.050), (0.00, 0.032),
    (0.04, 0.019), (0.05, 0.017), (0.09, 0.017), (0.105, 0.017)
]
write_single_obj('Assets/Models/Flask_Conical_Glass.obj', make_lathe_mesh(conical_glass, 36), 'Glass')
write_single_obj('Assets/Models/Flask_Conical_Fluid.obj', make_lathe_mesh(conical_fluid, 36), 'Liquid')

# 2. Spherical Flask
spherical_glass = [(-0.12, 0.035)]
for step in range(20):
    phi = -math.pi * 0.5 + 0.3 + (math.pi * 0.75) * (step / 19.0)
    y = -0.04 + 0.075 * math.sin(phi)
    r = 0.075 * math.cos(phi)
    spherical_glass.append((y, r))
spherical_glass.extend([(0.04, 0.020), (0.08, 0.020), (0.11, 0.020), (0.12, 0.026)])

spherical_fluid = [(-0.115, 0.030)]
for step in range(18):
    phi = -math.pi * 0.5 + 0.3 + (math.pi * 0.75) * (step / 17.0)
    y = -0.04 + 0.070 * math.sin(phi)
    r = 0.070 * math.cos(phi)
    spherical_fluid.append((y, r))
spherical_fluid.extend([(0.04, 0.018), (0.08, 0.018), (0.105, 0.018)])

write_single_obj('Assets/Models/Flask_Spherical_Glass.obj', make_lathe_mesh(spherical_glass, 36), 'Glass')
write_single_obj('Assets/Models/Flask_Spherical_Fluid.obj', make_lathe_mesh(spherical_fluid, 36), 'Liquid')

# 3. Square Bottle
def get_rect_corners(y, width, depth, corner_r, neck_r):
    if y > 0.05:
        r = neck_r if y < 0.11 else neck_r * 1.15
        pts = [(r * math.cos(2*math.pi*s/24), r * math.sin(2*math.pi*s/24)) for s in range(24)]
        return pts
    elif y > 0.03:
        alpha = (y - 0.03) / 0.02
        w_half = width * 0.5 * (1.0 - alpha) + neck_r * alpha
        d_half = depth * 0.5 * (1.0 - alpha) + neck_r * alpha
        return [(w_half * math.cos(2*math.pi*s/24), d_half * math.sin(2*math.pi*s/24)) for s in range(24)]
    else:
        pts = []
        w_h = width * 0.5 - corner_r
        d_h = depth * 0.5 - corner_r
        centers = [(w_h, d_h), (-w_h, d_h), (-w_h, -d_h), (w_h, -d_h)]
        for c_idx, (cx, cz) in enumerate(centers):
            base_angle = c_idx * (math.pi * 0.5)
            for seg in range(6):
                ang = base_angle + (math.pi * 0.5) * (seg / 6.0)
                pts.append((cx + corner_r * math.cos(ang), cz + corner_r * math.sin(ang)))
        return pts

y_levels_sq_glass = [-0.12, -0.115, -0.06, 0.0, 0.03, 0.04, 0.05, 0.08, 0.11, 0.12]
y_levels_sq_fluid = [-0.115, -0.06, 0.0, 0.03, 0.04, 0.05, 0.08, 0.105]

def glass_rect(y): return get_rect_corners(y, 0.10, 0.06, 0.015, 0.020)
def fluid_rect(y): return get_rect_corners(y, 0.092, 0.054, 0.013, 0.018)

write_single_obj('Assets/Models/Bottle_Square_Glass.obj', make_prism_mesh(glass_rect, y_levels_sq_glass), 'Glass')
write_single_obj('Assets/Models/Bottle_Square_Fluid.obj', make_prism_mesh(fluid_rect, y_levels_sq_fluid), 'Liquid')

# 4. Cylindrical Jar
jar_glass = [
    (-0.10, 0.052), (-0.095, 0.055), (-0.05, 0.055), (0.00, 0.055),
    (0.05, 0.055), (0.07, 0.051), (0.09, 0.051), (0.10, 0.056)
]
jar_fluid = [
    (-0.095, 0.050), (-0.05, 0.050), (0.00, 0.050), (0.05, 0.050),
    (0.07, 0.047), (0.09, 0.047)
]
write_single_obj('Assets/Models/Jar_Cylinder_Glass.obj', make_lathe_mesh(jar_glass, 36), 'Glass')
write_single_obj('Assets/Models/Jar_Cylinder_Fluid.obj', make_lathe_mesh(jar_fluid, 36), 'Liquid')

# 5. Hexagonal Bottle
def get_hex_corners(y, radius, neck_r):
    if y > 0.05:
        r = neck_r if y < 0.11 else neck_r * 1.15
        return [(r * math.cos(2*math.pi*s/24), r * math.sin(2*math.pi*s/24)) for s in range(24)]
    elif y > 0.03:
        alpha = (y - 0.03) / 0.02
        pts = []
        for i in range(6):
            ang = i * (math.pi / 3.0)
            hx = radius * math.cos(ang) * (1.0 - alpha) + neck_r * math.cos(ang) * alpha
            hz = radius * math.sin(ang) * (1.0 - alpha) + neck_r * math.sin(ang) * alpha
            pts.append((hx, hz))
        return pts
    else:
        return [(radius * math.cos(i*math.pi/3.0), radius * math.sin(i*math.pi/3.0)) for i in range(6)]

def glass_hex(y): return get_hex_corners(y, 0.065, 0.022)
def fluid_hex(y): return get_hex_corners(y, 0.059, 0.019)

y_levels_hex_glass = [-0.12, -0.115, -0.05, 0.0, 0.03, 0.04, 0.05, 0.08, 0.11, 0.12]
y_levels_hex_fluid = [-0.115, -0.05, 0.0, 0.03, 0.04, 0.05, 0.08, 0.105]

write_single_obj('Assets/Models/Bottle_Hexagonal_Glass.obj', make_prism_mesh(glass_hex, y_levels_hex_glass), 'Glass')
write_single_obj('Assets/Models/Bottle_Hexagonal_Fluid.obj', make_prism_mesh(fluid_hex, y_levels_hex_fluid), 'Liquid')

print("All 10 discrete OBJ models written successfully!")
