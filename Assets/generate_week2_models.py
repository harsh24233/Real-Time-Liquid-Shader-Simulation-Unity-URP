import math
import os

def write_single_obj(filepath, mesh, obj_name):
    os.makedirs(os.path.dirname(filepath), exist_ok=True)
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
        v_coord = r_idx / max(num_rings - 1, 1)
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

def make_cross_section_mesh(rings_func, y_levels, num_pts, close_top=True, close_bottom=True):
    vertices = []
    normals = []
    uvs = []
    faces = []
    num_levels = len(y_levels)
    
    for l_idx, y in enumerate(y_levels):
        pts = rings_func(y, l_idx)
        v_coord = l_idx / max(num_levels - 1, 1)
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

out_dir = "Assets/Models"
os.makedirs(out_dir, exist_ok=True)

# ==============================================================================
# 1. Hourglass Vessel (Narrow pinched waist, flared dual cones)
# ==============================================================================
def get_hourglass_profile(is_fluid=False):
    profile = []
    scale = 0.92 if is_fluid else 1.0
    y_min = -0.115 if is_fluid else -0.12
    y_max = 0.06 if is_fluid else 0.12
    steps = 25
    for i in range(steps):
        t = i / (steps - 1)
        y = y_min + t * (y_max - y_min)
        # hyperbolic waist curve
        r = (0.020 + 2.8 * (y * y)) * scale
        if not is_fluid and y > 0.10:
            r += (y - 0.10) * 0.15 # flared lip
        profile.append((y, max(r, 0.015 * scale)))
    return profile

write_single_obj(f"{out_dir}/Vessel_Hourglass_Glass.obj", make_lathe_mesh(get_hourglass_profile(False), 36), "Glass")
write_single_obj(f"{out_dir}/Vessel_Hourglass_Fluid.obj", make_lathe_mesh(get_hourglass_profile(True), 36), "Liquid")

# ==============================================================================
# 2. Toroid Ring Flask (Vertical doughnut / torus loop container on base with neck)
# ==============================================================================
def make_toroid_flask(is_fluid=False):
    # Parameterize a vertical torus with R_major = 0.052, r_minor = 0.018 (scaled for fluid)
    scale = 0.91 if is_fluid else 1.0
    R_maj = 0.050
    r_min = 0.018 * scale
    
    num_u = 32 # around major loop
    num_v = 18 # around tube cross section
    vertices = []
    normals = []
    uvs = []
    faces = []
    
    for i in range(num_u):
        phi = 2.0 * math.pi * i / num_u # angle around XY loop
        cos_p = math.cos(phi)
        sin_p = math.sin(phi)
        # Center of tube circle at angle phi:
        cx = R_maj * cos_p
        cy = R_maj * sin_p
        
        for j in range(num_v):
            theta = 2.0 * math.pi * j / num_v # angle around tube cross section
            cos_t = math.cos(theta)
            sin_t = math.sin(theta)
            
            # normal direction
            nx = cos_t * cos_p
            ny = cos_t * sin_p
            nz = sin_t
            
            x = cx + r_min * nx
            y = cy + r_min * ny
            z = r_min * nz
            
            vertices.append((x, y, z))
            normals.append((nx, ny, nz))
            uvs.append((i / num_u, j / num_v))
            
    for i in range(num_u):
        i_next = (i + 1) % num_u
        for j in range(num_v):
            j_next = (j + 1) % num_v
            v1 = i * num_v + j + 1
            v2 = i * num_v + j_next + 1
            v3 = i_next * num_v + j_next + 1
            v4 = i_next * num_v + j + 1
            faces.append([(v1, v1, v1), (v2, v2, v2), (v3, v3, v3)])
            faces.append([(v1, v1, v1), (v3, v3, v3), (v4, v4, v4)])
            
    # Add neck at top (for glass only, or smaller for fluid)
    if not is_fluid:
        neck_start_idx = len(vertices) + 1
        neck_h = 0.04
        neck_r = 0.016
        for n_layer in range(4):
            ny = R_maj + r_min + n_layer * (neck_h / 3.0)
            nr = neck_r if n_layer < 3 else neck_r * 1.2
            for s in range(16):
                th = 2.0 * math.pi * s / 16
                vertices.append((nr * math.cos(th), ny, nr * math.sin(th)))
                normals.append((math.cos(th), 0.0, math.sin(th)))
                uvs.append((s / 16, n_layer / 3))
        for n_layer in range(3):
            for s in range(16):
                s_next = (s + 1) % 16
                v1 = neck_start_idx + n_layer * 16 + s
                v2 = neck_start_idx + n_layer * 16 + s_next
                v3 = neck_start_idx + (n_layer + 1) * 16 + s_next
                v4 = neck_start_idx + (n_layer + 1) * 16 + s
                faces.append([(v1, v1, v1), (v2, v2, v2), (v3, v3, v3)])
                faces.append([(v1, v1, v1), (v3, v3, v3), (v4, v4, v4)])
                
    return {'vertices': vertices, 'normals': normals, 'uvs': uvs, 'faces': faces}

write_single_obj(f"{out_dir}/Vessel_ToroidRing_Glass.obj", make_toroid_flask(False), "Glass")
write_single_obj(f"{out_dir}/Vessel_ToroidRing_Fluid.obj", make_toroid_flask(True), "Liquid")

# ==============================================================================
# 3. Triangular Prism Flask (Sharp 3-sided prism body transitioning to round neck)
# ==============================================================================
def get_triangular_corners(y, is_fluid=False):
    scale = 0.92 if is_fluid else 1.0
    if y > 0.05:
        # circular neck
        r = (0.020 if y < 0.10 else 0.024) * scale
        return [(r * math.cos(2*math.pi*s/24), r * math.sin(2*math.pi*s/24)) for s in range(24)]
    elif y > 0.03:
        # transition
        alpha = (y - 0.03) / 0.02
        pts = []
        r_circ = 0.020 * scale
        r_tri = 0.065 * scale
        for s in range(24):
            ang = 2 * math.pi * s / 24
            # 3-sided polar radius: r(ang) ~ r_tri / (cos(ang mod 2pi/3 - pi/3))
            mod_ang = (ang % (2 * math.pi / 3)) - (math.pi / 3)
            tri_rad = r_tri * (0.8 + 0.2 * math.cos(3 * ang))
            cur_rad = tri_rad * (1.0 - alpha) + r_circ * alpha
            pts.append((cur_rad * math.cos(ang), cur_rad * math.sin(ang)))
        return pts
    else:
        # 3-lobed / rounded triangular prism
        r_tri = 0.065 * scale
        pts = []
        for s in range(24):
            ang = 2 * math.pi * s / 24
            cur_rad = r_tri * (0.82 + 0.24 * math.cos(3 * ang))
            pts.append((cur_rad * math.cos(ang), cur_rad * math.sin(ang)))
        return pts

y_levels_tri_glass = [-0.12, -0.115, -0.06, 0.0, 0.03, 0.04, 0.05, 0.08, 0.11, 0.12]
y_levels_tri_fluid = [-0.115, -0.06, 0.0, 0.03, 0.04, 0.05, 0.08, 0.095]

write_single_obj(f"{out_dir}/Vessel_TriangularPrism_Glass.obj", 
                 make_cross_section_mesh(lambda y, idx: get_triangular_corners(y, False), y_levels_tri_glass, 24), "Glass")
write_single_obj(f"{out_dir}/Vessel_TriangularPrism_Fluid.obj", 
                 make_cross_section_mesh(lambda y, idx: get_triangular_corners(y, True), y_levels_tri_fluid, 24), "Liquid")

# ==============================================================================
# 4. Teardrop Pear Decanter (Sweeping aerodynamic drop curve with wide bulbous base)
# ==============================================================================
def get_teardrop_profile(is_fluid=False):
    scale = 0.92 if is_fluid else 1.0
    profile = []
    y_min = -0.115 if is_fluid else -0.12
    y_max = 0.07 if is_fluid else 0.12
    steps = 24
    for i in range(steps):
        t = i / (steps - 1)
        y = y_min + t * (y_max - y_min)
        # Teardrop curve: bulbous low center, tapering high
        norm_y = (y - (-0.12)) / 0.24 # 0 to 1
        if norm_y < 0.35:
            # lower bulb expanding
            r = (0.045 + 0.025 * math.sin(norm_y / 0.35 * (math.pi * 0.5))) * scale
        elif norm_y < 0.75:
            # tapering neck
            p = (norm_y - 0.35) / 0.40
            r = (0.070 * (1.0 - p) + 0.020 * p) * scale
        else:
            # slender neck and flared lip
            p = (norm_y - 0.75) / 0.25
            r = (0.020 + (0.006 * p if not is_fluid else 0.0)) * scale
        profile.append((y, r))
    return profile

write_single_obj(f"{out_dir}/Vessel_Teardrop_Glass.obj", make_lathe_mesh(get_teardrop_profile(False), 36), "Glass")
write_single_obj(f"{out_dir}/Vessel_Teardrop_Fluid.obj", make_lathe_mesh(get_teardrop_profile(True), 36), "Liquid")

# ==============================================================================
# 5. Octagonal Faceted Decanter (8-sided faceted prism with beveled faceted neck)
# ==============================================================================
def get_octagonal_corners(y, is_fluid=False):
    scale = 0.92 if is_fluid else 1.0
    if y > 0.06:
        # 8-sided neck
        r = (0.022 if y < 0.10 else 0.026) * scale
        return [(r * math.cos(i * math.pi / 4.0), r * math.sin(i * math.pi / 4.0)) for i in range(8)]
    elif y > 0.03:
        # chamfered shoulder
        alpha = (y - 0.03) / 0.03
        r_base = 0.062 * scale
        r_neck = 0.022 * scale
        r = r_base * (1.0 - alpha) + r_neck * alpha
        return [(r * math.cos(i * math.pi / 4.0), r * math.sin(i * math.pi / 4.0)) for i in range(8)]
    else:
        # main octagonal prism body
        r = 0.062 * scale
        return [(r * math.cos(i * math.pi / 4.0), r * math.sin(i * math.pi / 4.0)) for i in range(8)]

y_levels_oct_glass = [-0.12, -0.115, -0.05, 0.0, 0.03, 0.045, 0.06, 0.09, 0.11, 0.12]
y_levels_oct_fluid = [-0.115, -0.05, 0.0, 0.03, 0.045, 0.06, 0.08, 0.095]

write_single_obj(f"{out_dir}/Vessel_OctagonalDecanter_Glass.obj",
                 make_cross_section_mesh(lambda y, idx: get_octagonal_corners(y, False), y_levels_oct_glass, 8), "Glass")
write_single_obj(f"{out_dir}/Vessel_OctagonalDecanter_Fluid.obj",
                 make_cross_section_mesh(lambda y, idx: get_octagonal_corners(y, True), y_levels_oct_fluid, 8), "Liquid")

# ==============================================================================
# 6. Helical Twisted Column Flask (Cross-section rotates along vertical axis)
# ==============================================================================
def get_twisted_corners(y, idx, is_fluid=False):
    scale = 0.92 if is_fluid else 1.0
    twist_ang = (y - (-0.12)) * 8.5 # 180+ deg twist
    if y > 0.05:
        # round neck at top
        r = (0.020 if y < 0.10 else 0.024) * scale
        return [(r * math.cos(2*math.pi*s/16), r * math.sin(2*math.pi*s/16)) for s in range(16)]
    elif y > 0.03:
        # transition
        alpha = (y - 0.03) / 0.02
        r_neck = 0.020 * scale
        pts = []
        for s in range(16):
            base_ang = 2 * math.pi * s / 16 + twist_ang
            # 4-lobed / rounded square profile
            r_col = 0.055 * scale * (0.85 + 0.18 * math.cos(4 * (base_ang - twist_ang)))
            r = r_col * (1.0 - alpha) + r_neck * alpha
            pts.append((r * math.cos(base_ang), r * math.sin(base_ang)))
        return pts
    else:
        pts = []
        for s in range(16):
            base_ang = 2 * math.pi * s / 16 + twist_ang
            r = 0.055 * scale * (0.85 + 0.18 * math.cos(4 * (base_ang - twist_ang)))
            pts.append((r * math.cos(base_ang), r * math.sin(base_ang)))
        return pts

y_levels_twist_glass = [-0.12, -0.09, -0.06, -0.03, 0.0, 0.03, 0.04, 0.05, 0.08, 0.11, 0.12]
y_levels_twist_fluid = [-0.115, -0.09, -0.06, -0.03, 0.0, 0.03, 0.04, 0.05, 0.08, 0.095]

write_single_obj(f"{out_dir}/Vessel_TwistedColumn_Glass.obj",
                 make_cross_section_mesh(lambda y, idx: get_twisted_corners(y, idx, False), y_levels_twist_glass, 16), "Glass")
write_single_obj(f"{out_dir}/Vessel_TwistedColumn_Fluid.obj",
                 make_cross_section_mesh(lambda y, idx: get_twisted_corners(y, idx, True), y_levels_twist_fluid, 16), "Liquid")

# ==============================================================================
# 7. Dual Bulb Lab Vessel (Stacked double spherical lobes connected by constriction)
# ==============================================================================
def get_dual_bulb_profile(is_fluid=False):
    scale = 0.92 if is_fluid else 1.0
    profile = []
    # Lower bulb: center y = -0.055, radius 0.052
    for step in range(12):
        ang = -math.pi * 0.45 + (math.pi * 0.85) * (step / 11.0)
        y = -0.055 + 0.052 * math.sin(ang)
        r = 0.052 * math.cos(ang) * scale
        profile.append((y, max(r, 0.015 * scale)))
    # Constriction waist
    profile.append((-0.005, 0.022 * scale))
    profile.append((0.005, 0.022 * scale))
    # Upper bulb: center y = 0.050, radius 0.040
    y_limit = 0.065 if is_fluid else 0.080
    for step in range(10):
        ang = -math.pi * 0.35 + (math.pi * 0.70) * (step / 9.0)
        y = 0.050 + 0.040 * math.sin(ang)
        if y > y_limit and is_fluid:
            break
        r = 0.040 * math.cos(ang) * scale
        profile.append((y, max(r, 0.015 * scale)))
    if not is_fluid:
        # Neck and rim
        profile.extend([(0.085, 0.020), (0.11, 0.020), (0.12, 0.025)])
    return profile

write_single_obj(f"{out_dir}/Vessel_DualBulb_Glass.obj", make_lathe_mesh(get_dual_bulb_profile(False), 36), "Glass")
write_single_obj(f"{out_dir}/Vessel_DualBulb_Fluid.obj", make_lathe_mesh(get_dual_bulb_profile(True), 36), "Liquid")

# ==============================================================================
# 8. Cardioid Heart-Shaped Flask (Cardioid mathematical curve cross-section)
# ==============================================================================
def get_cardioid_corners(y, is_fluid=False):
    scale = 0.92 if is_fluid else 1.0
    if y > 0.05:
        r = (0.020 if y < 0.10 else 0.024) * scale
        return [(r * math.cos(2*math.pi*s/32), r * math.sin(2*math.pi*s/32)) for s in range(32)]
    elif y > 0.03:
        alpha = (y - 0.03) / 0.02
        pts = []
        r_circ = 0.020 * scale
        for s in range(32):
            t = 2.0 * math.pi * s / 32
            # Heart formula
            hx = 0.0038 * (16 * (math.sin(t)**3)) * scale
            hz = 0.0038 * (13 * math.cos(t) - 5 * math.cos(2*t) - 2 * math.cos(3*t) - math.cos(4*t)) * scale
            cx = r_circ * math.cos(t)
            cz = r_circ * math.sin(t)
            pts.append((hx * (1.0 - alpha) + cx * alpha, hz * (1.0 - alpha) + cz * alpha))
        return pts
    else:
        pts = []
        for s in range(32):
            t = 2.0 * math.pi * s / 32
            hx = 0.0038 * (16 * (math.sin(t)**3)) * scale
            hz = 0.0038 * (13 * math.cos(t) - 5 * math.cos(2*t) - 2 * math.cos(3*t) - math.cos(4*t)) * scale
            pts.append((hx, hz))
        return pts

y_levels_heart_glass = [-0.12, -0.115, -0.06, 0.0, 0.03, 0.04, 0.05, 0.08, 0.11, 0.12]
y_levels_heart_fluid = [-0.115, -0.06, 0.0, 0.03, 0.04, 0.05, 0.08, 0.095]

write_single_obj(f"{out_dir}/Vessel_CardioidHeart_Glass.obj",
                 make_cross_section_mesh(lambda y, idx: get_cardioid_corners(y, False), y_levels_heart_glass, 32), "Glass")
write_single_obj(f"{out_dir}/Vessel_CardioidHeart_Fluid.obj",
                 make_cross_section_mesh(lambda y, idx: get_cardioid_corners(y, True), y_levels_heart_fluid, 32), "Liquid")

# ==============================================================================
# 9. 5-Pointed Star Prism Flask (Sharp 10-vert star body with extreme wall area)
# ==============================================================================
def get_star_corners(y, is_fluid=False):
    scale = 0.92 if is_fluid else 1.0
    if y > 0.05:
        r = (0.020 if y < 0.10 else 0.024) * scale
        return [(r * math.cos(2*math.pi*s/20), r * math.sin(2*math.pi*s/20)) for s in range(20)]
    elif y > 0.03:
        alpha = (y - 0.03) / 0.02
        pts = []
        r_circ = 0.020 * scale
        for s in range(20):
            ang = 2 * math.pi * s / 20
            r_star = (0.062 if (s % 2 == 0) else 0.032) * scale
            r = r_star * (1.0 - alpha) + r_circ * alpha
            pts.append((r * math.cos(ang), r * math.sin(ang)))
        return pts
    else:
        pts = []
        for s in range(20):
            ang = 2 * math.pi * s / 20
            r = (0.062 if (s % 2 == 0) else 0.032) * scale
            pts.append((r * math.cos(ang), r * math.sin(ang)))
        return pts

y_levels_star_glass = [-0.12, -0.115, -0.06, 0.0, 0.03, 0.04, 0.05, 0.08, 0.11, 0.12]
y_levels_star_fluid = [-0.115, -0.06, 0.0, 0.03, 0.04, 0.05, 0.08, 0.095]

write_single_obj(f"{out_dir}/Vessel_StarPrism_Glass.obj",
                 make_cross_section_mesh(lambda y, idx: get_star_corners(y, False), y_levels_star_glass, 20), "Glass")
write_single_obj(f"{out_dir}/Vessel_StarPrism_Fluid.obj",
                 make_cross_section_mesh(lambda y, idx: get_star_corners(y, True), y_levels_star_fluid, 20), "Liquid")

# ==============================================================================
# 10. Alchemical Curved-Neck Retort (Bulb body with sweeping curved distillation neck)
# ==============================================================================
def make_retort_mesh(is_fluid=False):
    scale = 0.92 if is_fluid else 1.0
    vertices = []
    normals = []
    uvs = []
    faces = []
    
    # Base sphere body: center (0, -0.03, 0), radius 0.060 * scale
    num_lat = 18
    num_lon = 24
    r_bulb = 0.060 * scale
    cy = -0.03
    
    # Spherical body
    for i in range(num_lat):
        phi = -math.pi * 0.5 + math.pi * (i / (num_lat - 1))
        cos_p = math.cos(phi)
        sin_p = math.sin(phi)
        for j in range(num_lon):
            theta = 2.0 * math.pi * j / num_lon
            nx = cos_p * math.cos(theta)
            ny = sin_p
            nz = cos_p * math.sin(theta)
            vertices.append((r_bulb * nx, cy + r_bulb * ny, r_bulb * nz))
            normals.append((nx, ny, nz))
            uvs.append((j / num_lon, i / (num_lat - 1)))
            
    for i in range(num_lat - 1):
        for j in range(num_lon):
            j_next = (j + 1) % num_lon
            v1 = i * num_lon + j + 1
            v2 = i * num_lon + j_next + 1
            v3 = (i + 1) * num_lon + j_next + 1
            v4 = (i + 1) * num_lon + j + 1
            faces.append([(v1, v1, v1), (v2, v2, v2), (v3, v3, v3)])
            faces.append([(v1, v1, v1), (v3, v3, v3), (v4, v4, v4)])
            
    # Angled spout/neck extending to the side and slightly curving
    if not is_fluid:
        neck_start = len(vertices) + 1
        num_neck_steps = 10
        num_neck_rad = 12
        for k in range(num_neck_steps):
            frac = k / (num_neck_steps - 1)
            # Sweeping curve: starting at top of bulb (0, 0.03, 0) curving towards (+x, +y, +z)
            nkx = frac * 0.08
            nky = 0.03 + frac * 0.09 - (frac * frac) * 0.02
            nkz = frac * 0.03
            nkr = (0.018 * (1.0 - frac * 0.4))
            for s in range(num_neck_rad):
                th = 2.0 * math.pi * s / num_neck_rad
                vx = nkx + nkr * math.cos(th)
                vy = nky
                vz = nkz + nkr * math.sin(th)
                vertices.append((vx, vy, vz))
                normals.append((math.cos(th), 0.3, math.sin(th)))
                uvs.append((s / num_neck_rad, frac))
                
        for k in range(num_neck_steps - 1):
            for s in range(num_neck_rad):
                s_next = (s + 1) % num_neck_rad
                v1 = neck_start + k * num_neck_rad + s
                v2 = neck_start + k * num_neck_rad + s_next
                v3 = neck_start + (k + 1) * num_neck_rad + s_next
                v4 = neck_start + (k + 1) * num_neck_rad + s
                faces.append([(v1, v1, v1), (v2, v2, v2), (v3, v3, v3)])
                faces.append([(v1, v1, v1), (v3, v3, v3), (v4, v4, v4)])
                
    return {'vertices': vertices, 'normals': normals, 'uvs': uvs, 'faces': faces}

write_single_obj(f"{out_dir}/Vessel_AlchemicalRetort_Glass.obj", make_retort_mesh(False), "Glass")
write_single_obj(f"{out_dir}/Vessel_AlchemicalRetort_Fluid.obj", make_retort_mesh(True), "Liquid")

# ==============================================================================
# 11. Diamond / Octahedral Bipyramidal Gem Vessel (Faceted gemstone geometry)
# ==============================================================================
def get_diamond_corners(y, is_fluid=False):
    scale = 0.92 if is_fluid else 1.0
    if y > 0.06:
        # collar
        r = (0.022 if y < 0.10 else 0.026) * scale
        return [(r * math.cos(2*math.pi*s/8), r * math.sin(2*math.pi*s/8)) for s in range(8)]
    elif y > 0.0:
        # upper facet tapering inwards from wide girdle to collar
        frac = y / 0.06
        r = (0.070 * (1.0 - frac) + 0.022 * frac) * scale
        return [(r * math.cos(2*math.pi*s/8), r * math.sin(2*math.pi*s/8)) for s in range(8)]
    else:
        # lower pavilion expanding from base to wide girdle
        frac = (y - (-0.12)) / 0.12
        r = (0.020 * (1.0 - frac) + 0.070 * frac) * scale
        return [(r * math.cos(2*math.pi*s/8), r * math.sin(2*math.pi*s/8)) for s in range(8)]

y_levels_dia_glass = [-0.12, -0.06, 0.0, 0.03, 0.06, 0.09, 0.11, 0.12]
y_levels_dia_fluid = [-0.115, -0.06, 0.0, 0.03, 0.06, 0.08, 0.095]

write_single_obj(f"{out_dir}/Vessel_DiamondGem_Glass.obj",
                 make_cross_section_mesh(lambda y, idx: get_diamond_corners(y, False), y_levels_dia_glass, 8), "Glass")
write_single_obj(f"{out_dir}/Vessel_DiamondGem_Fluid.obj",
                 make_cross_section_mesh(lambda y, idx: get_diamond_corners(y, True), y_levels_dia_fluid, 8), "Liquid")

# ==============================================================================
# 12. Flattened Elliptical Ribbed Canteen (Wide elliptical cross section with side ribs)
# ==============================================================================
def get_canteen_corners(y, is_fluid=False):
    scale = 0.92 if is_fluid else 1.0
    if y > 0.06:
        # round neck
        r = (0.020 if y < 0.10 else 0.024) * scale
        return [(r * math.cos(2*math.pi*s/24), r * math.sin(2*math.pi*s/24)) for s in range(24)]
    elif y > 0.04:
        # shoulder transition
        alpha = (y - 0.04) / 0.02
        r_neck = 0.020 * scale
        pts = []
        for s in range(24):
            th = 2 * math.pi * s / 24
            # Ribbed ripple effect
            rib = 1.0 + 0.05 * math.sin(y * 90.0)
            ex = 0.072 * math.cos(th) * rib * scale
            ez = 0.036 * math.sin(th) * rib * scale
            nx = r_neck * math.cos(th)
            nz = r_neck * math.sin(th)
            pts.append((ex * (1.0 - alpha) + nx * alpha, ez * (1.0 - alpha) + nz * alpha))
        return pts
    else:
        # ribbed elliptical body
        pts = []
        for s in range(24):
            th = 2 * math.pi * s / 24
            rib = 1.0 + 0.05 * math.sin(y * 90.0)
            ex = 0.072 * math.cos(th) * rib * scale
            ez = 0.036 * math.sin(th) * rib * scale
            pts.append((ex, ez))
        return pts

y_levels_can_glass = [-0.12, -0.09, -0.06, -0.03, 0.0, 0.02, 0.04, 0.06, 0.09, 0.11, 0.12]
y_levels_can_fluid = [-0.115, -0.09, -0.06, -0.03, 0.0, 0.02, 0.04, 0.06, 0.08, 0.095]

write_single_obj(f"{out_dir}/Vessel_EllipticalCanteen_Glass.obj",
                 make_cross_section_mesh(lambda y, idx: get_canteen_corners(y, False), y_levels_can_glass, 24), "Glass")
write_single_obj(f"{out_dir}/Vessel_EllipticalCanteen_Fluid.obj",
                 make_cross_section_mesh(lambda y, idx: get_canteen_corners(y, True), y_levels_can_fluid, 24), "Liquid")

print("SUCCESS: Generated 12 new arbitrary geometry Glass and Fluid models (24 OBJ files)!")
