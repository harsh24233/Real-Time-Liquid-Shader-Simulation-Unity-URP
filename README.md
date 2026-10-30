# Real-Time Viscous Liquid Wobble Shader & Physics Simulation (Unity URP)

A real-time liquid shader and viscous fluid wobble simulation system developed in Unity using the **Universal Render Pipeline (URP)** and custom Shader Graph. The system simulates realistic fluid wobble dynamics, surface meniscus, sloshing, and viscosity damping across various container geometries and liquid types.

---

## 1. Overview

This project provides an interactive and physically inspired liquid simulation framework within Unity URP. It combines a custom shader graph for rendering dynamic fluid surfaces inside transparent containers with C# physics controllers that calculate inertia, sloshing frequency, damping, and viscosity effects under translational and rotational forces.

### Key Highlights
- **Interactive Fluid Sloshing**: Dynamic wobble responsive to container movement, rotation, and external impulses.
- **Viscosity & Density Profiles**: Pre-calibrated physical profiles for multiple fluids (Water, Milk, Vegetable Oil, Honey, Ketchup, and Custom).
- **Shader Graph Fluid Rendering**: Real-time fluid surface level, plane intersection, wobble vectors, and foam/meniscus styling.
- **Procedural Container Models**: Custom double-walled glass and fluid meshes generated procedurally.
- **Spectator Camera & In-Game Authority**: Full WASD camera flight and real-time 3D bottle manipulation (drag & drop, 3D Gizmo axis handles, tilt, and slosh testing).

---

## 2. Verified Project Specifications

| Component | Specification |
|---|---|
| **Unity Editor** | `6000.6.1f1` (Unity 6 LTS) |
| **Render Pipeline** | Universal Render Pipeline (URP) `17.6.0` |
| **Input System** | Unity Input System `1.20.0` |
| **Target Platform** | PC / Mobile |
| **Color Space** | Linear |

---

## 3. Supported Fluid Profiles

The simulation models different fluid dynamics based on physical density and dynamic viscosity:

| Fluid | Density ($\text{kg/m}^3$) | Viscosity ($\text{Pa}\cdot\text{s}$) | Wobble Frequency | Decay Duration | Characteristics |
|---|---|---|---|---|---|
| **Water** | 1000 | 0.001 | 2.50 Hz | 1.80 s | High oscillation, low damping, fast wave decay |
| **Milk** | 1030 | 0.003 | 2.30 Hz | 1.60 s | Balanced slosh, opaque fluid scattering |
| **Vegetable Oil** | 920 | 0.050 | 1.80 Hz | 1.20 s | Moderate damping, smooth wave dissipation |
| **Honey** | 1420 | 10.000 | 0.90 Hz | 0.45 s | Highly overdamped, slow sluggish displacement |
| **Ketchup** | 1250 | 50.000 | 0.50 Hz | 0.25 s | Heavy thixotropic damping, quick settling |
| **Custom** | User-defined | User-defined | Configurable | Configurable | Fully customizable via Inspector |

---

## 4. Week 7 Baseline: Container Geometries & Prefabs

The initial Week 7 foundation established 5 primary container models, separating the outer glass vessel from the inner fluid volume:

| Container | Shape | Default Liquid | Prefab Path |
|---|---|---|---|
| **Bottle Hexagonal** | 6-sided faceted bottle | Water | `Assets/Prefabs/Bottle_Hexagonal_Water.prefab` |
| **Bottle Square** | Rounded square bottle | Vegetable Oil | `Assets/Prefabs/Bottle_Square_VegOil.prefab` |
| **Flask Conical** | Erlenmeyer laboratory flask | Milk | `Assets/Prefabs/Flask_Conical_Milk.prefab` |
| **Flask Spherical** | Round-bottom chemical flask | Honey | `Assets/Prefabs/Flask_Spherical_Honey.prefab` |
| **Jar Cylinder** | Cylindrical storage jar | Ketchup | `Assets/Prefabs/Jar_Cylinder_Ketchup.prefab` |

---

## 5. Project Structure

```text
LiquidShaderFirst/
├── Assets/
│   ├── Materials/
│   │   ├── Week2/                                        # Dedicated Week 8 fluid materials (12 variants)
│   │   │   ├── Vessel_AlchemicalRetort_VegOil_Fluid.mat
│   │   │   ├── Vessel_Hourglass_Water_Fluid.mat
│   │   │   └── ...
│   │   ├── Honey_Fluid.mat
│   │   ├── Milk_Fluid.mat
│   │   ├── Ketchup_Fluid.mat
│   │   ├── VegOil_Fluid.mat
│   │   ├── Water_Fluid.mat
│   │   └── glass.mat
│   ├── Models/
│   │   ├── Vessel_*_Glass.obj                            # Week 8 arbitrary geometry glass enclosures (12)
│   │   ├── Vessel_*_Fluid.obj                            # Week 8 arbitrary geometry fluid volumes (12)
│   │   ├── Bottle_Hexagonal_Glass.obj                    # Week 7 baseline models
│   │   ├── Bottle_Square_Glass.obj
│   │   ├── Flask_Conical_Glass.obj
│   │   ├── Flask_Spherical_Glass.obj
│   │   └── Jar_Cylinder_Glass.obj
│   ├── Prefabs/
│   │   ├── Week2/                                        # Week 8 arbitrary geometry vessel prefabs (12)
│   │   │   ├── Vessel_Hourglass_Water.prefab
│   │   │   ├── Vessel_ToroidRing_VegOil.prefab
│   │   │   └── ...
│   │   ├── Bottle_Hexagonal_Water.prefab                 # Week 7 baseline prefabs (5)
│   │   ├── Bottle_Square_VegOil.prefab
│   │   ├── Flask_Conical_Milk.prefab
│   │   ├── Flask_Spherical_Honey.prefab
│   │   └── Jar_Cylinder_Ketchup.prefab
│   ├── Scenes/
│   │   └── SampleScene.unity                             # Dual-tier showcase scene (Week 8 front, Week 7 back)
│   ├── Settings/
│   │   ├── PC_RPAsset.asset
│   │   ├── Mobile_RPAsset.asset
│   │   └── UniversalRenderPipelineGlobalSettings.asset
│   ├── TutorialInfo/
│   │   └── Editor/
│   │       ├── SetupArbitraryGeometryShowcase.cs         # Automated Week 8 showcase assembler
│   │       └── SetupFluidContainers.cs                   # Automated Week 7 showcase assembler
│   ├── ArbitraryViscousFluidFriction.cs                  # Week 8 physics: Navier-Stokes boundary friction
│   ├── LiquidShaderGrapjh.shadergraph                    # Core URP fluid shader graph
│   ├── LiquidWobble.cs                                   # Baseline wobble shader controller
│   ├── ShowcaseCameraController.cs                       # Camera spectator & in-game bottle authority
│   ├── ViscousLiquidWobble.cs                            # Week 7 viscous fluid dynamics controller
│   ├── generate_models_v2.py                             # Procedural generator for Week 7 models
│   └── generate_week2_models.py                          # Procedural generator for Week 8 arbitrary models
├── Packages/
│   ├── manifest.json
│   └── packages-lock.json
├── ProjectSettings/
│   └── ProjectVersion.txt
├── .gitignore
├── .vsconfig
└── README.md
```

---

## 6. Core Architecture

### 6.1 `LiquidShaderGrapjh.shadergraph`
A custom URP Shader Graph that handles:
- **Surface Level Plane**: World-space or object-space liquid fill height.
- **Wobble Angle & Matrix**: Rotates the fluid plane according to sine wave displacement vectors passed from C#.
- **Subsurface & Translucency**: Distinct opacity and refraction properties for realistic fluid visuals.

### 6.2 C# Simulation Controllers
- **`ArbitraryViscousFluidFriction.cs` (Week 8)**: Simulates coupled fluid density, dynamic viscosity, wall friction coefficients, and geometric tortuosity boundary shear across 12 arbitrary vessel geometries.
- **`ViscousLiquidWobble.cs` (Week 7)**: Simulates physical viscous decay, oscillation frequency scaling, and velocity dampening across 5 baseline containers.
- **`ShowcaseCameraController.cs`**: Manages spectator camera navigation (WASD + free look) and provides complete in-game bottle authority (direct mouse drag, 3D Gizmo axis handles, tilt, and fluid switching).

---

## 7. Getting Started & Controls Quickstart

### Prerequisites
- **Unity 6000.6.1f1** (Unity 6 LTS)
- **Universal Render Pipeline (URP)** package installed (restored automatically by Unity)

### Setup Instructions
1. Clone the repository:
   ```bash
   git clone https://github.com/harsh24233/Real-Time-Liquid-Shader-Simulation-Unity-URP.git
   ```
2. Open **Unity Hub**.
3. Click **Add** -> **Add project from disk**.
4. Select the `LiquidShaderFirst` directory.
5. Open with **Unity Editor 6000.6.1f1**.
6. Open the main showcase scene:
   ```text
   Assets/Scenes/SampleScene.unity
   ```
7. Press **Play** to explore the showcase and interact with the containers.

---

## 8. Week 8 Extension: Arbitrary Geometry Liquid Simulation & Surface Friction

In the Week 8 update, the liquid shader simulation framework has been expanded with **12 newly engineered arbitrary geometry enclosed vessels**, placed in a **dedicated front showcase line**, featuring coupled fluid **density ($\rho$)**, **dynamic viscosity ($\mu$)**, and **container interior surface friction ($f_{\text{surface}}$ & $\lambda_{\text{geom}}$)** without altering or modifying the original Week 7 setup.

### 8.1 The 12 Newly Designed Arbitrary Geometry Enclosed Vessels

| Vessel Name | Arbitrary Geometry Description | Default Fluid | Surface Friction Type | Geometry Tortuosity ($\lambda_{\text{geom}}$) | Prefab Path |
|---|---|---|---|---|---|
| **Vessel_Hourglass_Water** | Pinched hyperbolic waist with flared dual cones | Water | Polished Glass ($f = 0.15$) | $1.85$ (Constriction shear) | `Assets/Prefabs/Week2/Vessel_Hourglass_Water.prefab` |
| **Vessel_ToroidRing_VegOil** | Hollow enclosed toroidal / ring loop body | Vegetable Oil | Standard Glass ($f = 0.35$) | $1.70$ (Dual-wall boundary loop) | `Assets/Prefabs/Week2/Vessel_ToroidRing_VegOil.prefab` |
| **Vessel_TriangularPrism_Honey** | Sharp 3-sided prism with rounded transition neck | Honey | Frosted Glass ($f = 0.65$) | $1.60$ (Tri-corner boundary drag) | `Assets/Prefabs/Week2/Vessel_TriangularPrism_Honey.prefab` |
| **Vessel_Teardrop_Ketchup** | Asymmetric sweeping streamlined teardrop bulb | Ketchup | Textured Resin ($f = 0.88$) | $1.15$ (Streamlined boundary flow) | `Assets/Prefabs/Week2/Vessel_Teardrop_Ketchup.prefab` |
| **Vessel_OctagonalDecanter_Water** | 8-sided faceted prism with beveled faceted neck | Water | Standard Glass ($f = 0.35$) | $1.40$ (Faceted edge vortex drag) | `Assets/Prefabs/Week2/Vessel_OctagonalDecanter_Water.prefab` |
| **Vessel_TwistedColumn_VegOil** | 180° twisted helical column with spiral contours | Vegetable Oil | Frosted Glass ($f = 0.65$) | $1.95$ (Helical spiral wall shear) | `Assets/Prefabs/Week2/Vessel_TwistedColumn_VegOil.prefab` |
| **Vessel_DualBulb_Honey** | Stacked dual spherical lobes connected by constriction | Honey | Standard Glass ($f = 0.35$) | $1.90$ (Inter-chamber orifice drag) | `Assets/Prefabs/Week2/Vessel_DualBulb_Honey.prefab` |
| **Vessel_CardioidHeart_Ketchup** | Cardioid mathematical heart cross-section vessel | Ketchup | Polished Glass ($f = 0.15$) | $1.65$ (Concave crease friction) | `Assets/Prefabs/Week2/Vessel_CardioidHeart_Ketchup.prefab` |
| **Vessel_StarPrism_Water** | 5-pointed star body with 10 acute inner/outer ridges | Water | Textured Resin ($f = 0.88$) | $2.10$ (Extreme acute corner skin friction) | `Assets/Prefabs/Week2/Vessel_StarPrism_Water.prefab` |
| **Vessel_AlchemicalRetort_VegOil** | Bulbous base with angled 35° curved distillation beak | Vegetable Oil | Polished Glass ($f = 0.15$) | $1.80$ (Angled distillation spout shear) | `Assets/Prefabs/Week2/Vessel_AlchemicalRetort_VegOil.prefab` |
| **Vessel_DiamondGem_Honey** | Octahedral bipyramidal faceted gemstone enclosure | Honey | Polished Glass ($f = 0.15$) | $1.75$ (Multi-faceted boundary drag) | `Assets/Prefabs/Week2/Vessel_DiamondGem_Honey.prefab` |
| **Vessel_EllipticalCanteen_Ketchup** | Flattened elliptical canteen with side corrugated ribs | Ketchup | Standard Glass ($f = 0.35$) | $2.05$ (Corrugated horizontal wall drag) | `Assets/Prefabs/Week2/Vessel_EllipticalCanteen_Ketchup.prefab` |

### 8.2 Fluid Rheology & Surface Boundary Friction Mechanics

The controller [`ArbitraryViscousFluidFriction.cs`](file:///d:/IP%20unity/LiquidShaderFirst/Assets/ArbitraryViscousFluidFriction.cs) models physical fluid dynamics using coupled Navier-Stokes boundary equations:

1. **Fluid Density ($\rho$) & Inertial Momentum**:
   - Denser fluids (e.g. Honey: $1420\ \text{kg/m}^3$) carry higher momentum mass ($M = \rho \cdot V$) under acceleration, resisting initial tilt displacement but generating sustained momentum force.
2. **Dynamic Viscosity ($\mu$) & Kinematic Viscosity ($\nu = \mu / \rho$)**:
   - Governs internal fluid shear stress:
     $$\tau = \mu \frac{\partial u}{\partial y}$$
   - Water ($\mu = 0.001\ \text{Pa}\cdot\text{s}$): underdamped sloshing ($\zeta \approx 0.05$), fast wave oscillation.
   - Vegetable Oil ($\mu = 0.050\ \text{Pa}\cdot\text{s}$): smooth laminar flow, moderate damping.
   - Honey ($\mu = 10.0\ \text{Pa}\cdot\text{s}$): near-critical damping, delayed sluggish displacement.
   - Ketchup ($\mu = 50.0\ \text{Pa}\cdot\text{s}$): non-Newtonian thixotropic paste, high boundary resistance, rapid settling.
3. **Interior Surface Roughness Friction ($f_{\text{surface}}$)**:
   - Polished Glass ($f = 0.15$), Standard Glass ($f = 0.35$), Frosted Glass ($f = 0.65$), Textured Resin ($f = 0.88$).
4. **Geometric Tortuosity & Hydraulic Shape Factor ($\lambda_{\text{geom}}$)**:
   - Quantifies boundary layer friction and form drag due to narrow channels (Hourglass waist, DualBulb orifice), acute vertices (Star Prism), and spiral grooves (Twisted Column).
   - Stokes oscillating boundary shear resistance:
     $$F_{\text{wall\_drag}} = f_{\text{surface}} \cdot \lambda_{\text{geom}} \cdot \sqrt{\rho \cdot \mu \cdot \omega_n}$$
5. **Meniscus Wall Adhesion Cling**:
   - Simulates sticky liquid clinging to arbitrary geometry walls before gravity draws it back down.

### 8.3 Camera Flight Navigation & In-Game Bottle Authority

[`ShowcaseCameraController.cs`](file:///d:/IP%20unity/LiquidShaderFirst/Assets/ShowcaseCameraController.cs) provides complete spectator navigation and in-game vessel manipulation authority:

#### Camera Controls (Player Movement)
- **`[W]`, `[A]`, `[S]`, `[D]`**: Fly the camera freely (Forward, Left Strafe, Backward, Right Strafe). Moving with WASD navigates the player view without affecting bottle positions.
- **`[Right Mouse Button (Hold)]`**: Free-look camera (smooth pitch and yaw, cursor auto-locks during look).
- **`[Left Shift]`**: Sprint / Boost camera movement speed.
- **`[Space]` / `[E]`**: Fly upwards vertically.
- **`[Left Ctrl]` / `[C]` / `[Q]`**: Fly downwards vertically.
- **`[F1]`**: Tiered Overview (frames both Week 8 front line and Week 7 back line).
- **`[F2]`**: Front Line focus (12 Week 8 arbitrary vessels).
- **`[F3]`**: Back Line focus (5 Week 7 baseline containers).
- **`[F4]`**: Focus camera tightly on the active selected vessel.
- **`[F5]`**: Switch to Free-Fly Spectator Camera.
- **`[H]`**: Toggle On-Screen Telemetry HUD.

#### In-Game Bottle Authority (Manipulate Bottles in Play Mode)
- **Left-Click & Drag**: Click directly on **any** bottle or vessel in the scene to pick it up and move/shake it in real-time. Dynamic fluid sloshes realistically with wall friction and viscosity!
- **Mouse Scroll Wheel**: Push or pull the dragged bottle closer or further in depth ($Z$-axis distance).
- **3D Move Gizmo**: Selecting any bottle spawns an interactive in-game 3D Move Tool:
  - **Red [X] Handle**: Drag to translate strictly along the world $X$-axis.
  - **Green [Y] Handle**: Drag to translate strictly along the world $Y$-axis.
  - **Blue [Z] Handle**: Drag to translate strictly along the world $Z$-axis.
- **Arrow Keys**: Fine-nudge the selected bottle in 3D ($[\leftarrow]/[\rightarrow]$ for $X$, $[\uparrow]/[\downarrow]$ for $Y$, $[\text{Shift}+\uparrow/\downarrow]$ or $[\text{PageUp}/\text{PageDown}]$ for $Z$).
- **`[Q]` / `[E]`**: Tilt the bottle left or right to test static fluid level and angular slosh dynamics.
- **`[R]`**: Instantly reset the selected bottle back to its original illuminated plinth/shelf position.
- **`[Backspace]`**: Reset ALL bottles in the showcase back to their original positions.
- **`[1-5]`**: Switch fluid profile (Water, Vegetable Oil, Honey, Ketchup, Milk) on the selected bottle.
- **`[Tab]` / `[HUD Buttons]`**: Cycle through bottles and trigger impulse sloshing.

---

## 9. Version Control & Contribution Rules

- Generated Unity folders (`Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`) are excluded via `.gitignore`.
- All Unity `.meta` files are tracked to ensure asset GUID integrity.
- Do not commit local IDE configuration or user caches.

---

## 10. Author & Academic Context

**Harsh**  
Indraprastha Institute of Information Technology Delhi (IIIT-Delhi)  
Developed for research and development in real-time fluid simulation and interactive graphics.
