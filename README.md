# Real-Time Liquid Shader & Viscous Fluid Wobble Simulation in Unity URP

A real-time liquid shader and viscous fluid wobble simulation system developed in Unity using the **Universal Render Pipeline (URP)** and custom Shader Graph. The system simulates realistic fluid wobble dynamics, surface meniscus, sloshing, dynamic fill/drain planes, and viscosity damping across various container geometries and liquid types.

---

## 1. Project Overview

This project provides an interactive and physically inspired liquid simulation framework within Unity URP. It combines a custom shader graph for rendering dynamic fluid surfaces inside transparent containers with C# physics controllers that calculate inertia, sloshing frequency, damping, fill level dynamics, and viscosity effects under translational and rotational forces.

Key simulation features:
- **Shader Graph Fluid Rendering**: Real-time fluid surface level, plane intersection, wobble vectors, and foam/meniscus styling.
- **Dynamic Fill Amount & Cut-Plane Modulation**: Real-time interactive fill level adjustment with physical slosh envelope suppression ($\Psi_{\mathrm{fill}}(F) = 4F(1-F)$).
- **Physical Inertia & Wobble Dynamics**: Spring-damper physics driven by container linear acceleration and angular velocity.
- **Coupled Density & Dynamic Viscosity**: Multi-fluid rheology where physical density ($\rho$) and dynamic viscosity ($\mu$) dictate wave frequency, decay time, and Reynolds-number flow characteristics.
- **Complex Geometric Morphologies & Hydraulic Resistance**: Intricate enclosures (petrol jerrycan, star prism box, toroidal tank, Y-junction manifold, capsule tank, dendritic tree branch, multichambered maze) with distinct boundary shear and tortuosity coefficients.
- **Universal Render Pipeline (URP)**: Modern scriptable render pipeline configuration with support for forward rendering and real-time lighting.
- **Full In-Game Bottle Authority**: Player has full authority to move, shake, tilt, nudge, or fly around bottles in Game view using a free-fly spectator camera or interactive 3D move gizmos.

---

## 2. Rendering Pipeline & Technical Stack

- **Unity Version**: Unity 6 (6000.6.1f1) / 6000.5.9f1
- **Render Pipeline**: Universal Render Pipeline (URP)
- **Shader Authoring**: Unity Shader Graph (`LiquidShaderGrapjh.shadergraph`)
- **Input System**: Both new Unity Input System (`UnityEngine.InputSystem`) and legacy `Input` supported via preprocessor directives
- **Target Platforms**: PC / Mac / Linux Standalone, Mobile (GLES3/Vulkan/Metal)

---

## 3. Physical Principles & Fluid Rheology

The simulation models fluid surface oscillation through a coupled spring-damper differential formulation driven by container kinematics:

$$\frac{d^2 \theta}{dt^2} + 2\zeta\omega_n \frac{d\theta}{dt} + \omega_n^2 \theta = a_{\mathrm{external}}$$

| Fluid | Density ($\mathrm{kg/m}^3$) | Viscosity ($\mathrm{Pa}\cdot\mathrm{s}$) | Wobble Frequency | Decay Duration | Characteristics |
|---|---|---|---|---|---|
| **Cryogenic Gas** | $500$ | $0.0001$ | $4.2\ \mathrm{Hz}$ | Ultra-fast ripples | Near-zero drag, hyperactive surface sloshing |
| **Petrol (Gasoline)** | $740$ | $0.0006$ | $3.4\ \mathrm{Hz}$ | Fast oscillatory | Low density, rapid slosh waves, sharp meniscus |
| **Water** | $1000$ | $0.0010$ | $3.0\ \mathrm{Hz}$ | $1.5 - 2.5\ \mathrm{s}$ | Low viscosity, underdamped periodic oscillations |
| **Hydraulic Coolant** | $1060$ | $0.0035$ | $3.0\ \mathrm{Hz}$ | Moderate decay | Aqueous circulating hydraulic surge dynamics |
| **Synthetic Motor Oil**| $890$ | $0.2500$ | $2.2\ \mathrm{Hz}$ | Smooth laminar | Viscous boundary damping, laminar swirl |
| **Vegetable Oil** | $915$ | $0.0500$ | $2.2\ \mathrm{Hz}$ | $0.8 - 1.2\ \mathrm{s}$ | Moderate viscosity, smooth laminar damping |
| **Tree Sap Resin** | $1380$ | $8.5000$ | $1.4\ \mathrm{Hz}$ | Heavy overdamping| High boundary wall drag, thick creeping flow |
| **Honey** | $1420$ | $10.000$ | $1.2\ \mathrm{Hz}$ | $0.3 - 0.5\ \mathrm{s}$ | High viscosity, overdamped sluggish response |
| **Heavy Maze Gel** | $1420$ | $25.000$ | $1.1\ \mathrm{Hz}$ | Severe overdamping| Non-linear dissipation across internal baffle slots |
| **Ketchup** | $1250$ | $50.000$ | $0.8\ \mathrm{Hz}$ | Rapid arrest | Non-Newtonian thixotropic paste, high resistance |
| **Liquid Mercury** | $13550$ | $0.0015$ | $2.1\ \mathrm{Hz}$ | Heavy momentum | Extreme mass density, massive inertial resistance |

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
│   │   ├── Week9/                                        # Dedicated Week 9 realistic fluid materials (7 variants)
│   │   │   ├── Week9_Petrol_Fluid.mat
│   │   │   ├── Week9_Mercury_Fluid.mat
│   │   │   ├── Week9_MotorOil_Fluid.mat
│   │   │   ├── Week9_Coolant_Fluid.mat
│   │   │   ├── Week9_CryoGas_Fluid.mat
│   │   │   ├── Week9_TreeSap_Fluid.mat
│   │   │   └── Week9_MazeGel_Fluid.mat
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
│   │   ├── Week9_*_Glass.obj                             # Week 9 complex structure glass enclosures (7)
│   │   ├── Week9_*_Fluid.obj                             # Week 9 complex structure fluid volumes (7)
│   │   ├── Vessel_*_Glass.obj                            # Week 8 arbitrary geometry glass enclosures (12)
│   │   ├── Vessel_*_Fluid.obj                            # Week 8 arbitrary geometry fluid volumes (12)
│   │   └── Bottle_*_Glass.obj                            # Week 7 baseline models (5)
│   ├── Prefabs/
│   │   ├── Week9/                                        # Week 9 realistic dynamics prefabs (7)
│   │   │   ├── Week9_PetrolContainer_Jerrycan.prefab
│   │   │   ├── Week9_StarShapedBox.prefab
│   │   │   ├── Week9_ToroidalTank.prefab
│   │   │   ├── Week9_YJunctionManifold.prefab            # Important 3-way piping manifold
│   │   │   ├── Week9_CapsuleTank.prefab
│   │   │   ├── Week9_TreeBranchedStructure.prefab
│   │   │   └── Week9_MazeMultichamber.prefab
│   │   ├── Week2/                                        # Week 8 arbitrary geometry vessel prefabs (12)
│   │   └── ...
│   ├── Scenes/
│   │   └── SampleScene.unity                             # 3-tier showcase amphitheater (Week 9, 8, 7)
│   ├── TutorialInfo/
│   │   └── Editor/
│   │       ├── SetupWeek9Showcase.cs                     # Automated Week 9 showcase assembler
│   │       ├── SetupArbitraryGeometryShowcase.cs         # Automated Week 8 showcase assembler
│   │       └── SetupFluidContainers.cs                   # Automated Week 7 showcase assembler
│   ├── Week9FluidDynamicsController.cs                   # Week 9 physics: Density, Viscosity, & Dynamic Fill
│   ├── ArbitraryViscousFluidFriction.cs                  # Week 8 physics: Navier-Stokes boundary friction
│   ├── LiquidShaderGrapjh.shadergraph                    # Core URP fluid shader graph
│   ├── ShowcaseCameraController.cs                       # Camera spectator & in-game bottle authority
│   ├── ViscousLiquidWobble.cs                            # Week 7 viscous fluid dynamics controller
│   └── generate_week9_models.py                          # Procedural generator for Week 9 structures
├── Packages/
├── ProjectSettings/
└── README.md
```

---

## 6. Core Architecture

### 6.1 Shader Graph Mechanics (`LiquidShaderGrapjh.shadergraph`)
- **Surface Level Plane**: Liquid height driven by property `_Float`.
- **Wobble Rotation Plane**: Angular displacement matrices driven by `_WobbleX` and `_WobbleZ`.
- **Meniscus / Boundary Foam**: Tinted rim contour highlighting liquid contact points.
- **Subsurface & Translucency**: Opacity, smoothness, and metallic response.

### 6.2 Physics Engine Controllers
- **`Week9FluidDynamicsController.cs` (Week 9)**: Implements realistic fluid mass density ($\rho$), dynamic viscosity ($\mu$), kinematic viscosity ($\nu = \mu/\rho$), container geometric tortuosity ($\kappa$), and the **Fill Amount Magnitude Changing Logic** ($\Psi_{\mathrm{fill}}(F) = 4F(1-F)$).
- **`ArbitraryViscousFluidFriction.cs` (Week 8)**: Simulates coupled fluid density, dynamic viscosity, wall friction coefficients, and geometric tortuosity boundary shear across 12 arbitrary vessel geometries.
- **`ViscousLiquidWobble.cs` (Week 7)**: Multi-density viscous fluid controller with damping profiles.
- **`ShowcaseCameraController.cs`**: Free-fly spectator camera navigation and comprehensive in-game bottle authority.

---

## 7. Scene Hierarchy & 3-Tier Showcase Amphitheater

In `SampleScene.unity`, objects are arranged into a 3-tiered amphitheater so all structures are visible simultaneously:

```text
Viewer Eye / Camera Overview: (0.0, 1.6, -9.0)
│
├── [FRONT TIER - Week 9] Z = -1.8m, Y = -0.09m
│   └── 7 Complex Realistic Morphological Structures:
│       Petrol Jerrycan, Star Box, Toroid, Y-Junction, Capsule, Tree Branch, Multichamber Maze
│
├── [MIDDLE TIER - Week 8] Z = -3.8m, Y = +0.31m
│   └── 12 Arbitrary Geometry Vessels:
│       Hourglass, Toroid Ring, Prism, Decanter, Twisted Column, Dual Bulb, Heart, Gem, Canteen...
│
└── [BACK TIER - Week 7] Z = -6.0m, Y = +0.86m
    └── 5 Baseline Containers:
        Hexagonal Bottle, Square Bottle, Conical Flask, Spherical Flask, Cylinder Jar
```

---

## 8. Week 8 Extension: Arbitrary Geometry Simulation & Surface Friction

In Week 8, the framework introduced **12 arbitrary geometry enclosed vessels** placed in a dedicated showcase line, featuring Navier-Stokes boundary layer wall friction:

$$F_{\mathrm{drag}} = f_{\mathrm{surface}} \cdot \lambda_{\mathrm{geom}} \cdot \sqrt{\rho \cdot \mu \cdot \omega_n}$$

---

## 9. Week 9 Extension: Realistic Fluid Rheology, Dynamic Fill Logic & Complex Morphological Structures

The Week 9 update introduces an advanced fluid dynamics architecture developed **independently without modifying or regressing Week 7 or Week 8 systems**. It addresses three scientific and geometric challenges:

### 9.1 Realistic Density ($\rho$) & Dynamic Viscosity ($\mu$) Modeling

Each container is paired with a distinct real-world fluid with physically verified constants:

1. **Petrol / Gasoline** ($\rho = 740\ \mathrm{kg/m}^3, \mu = 0.0006\ \mathrm{Pa}\cdot\mathrm{s}$):
   - Translucent amber fuel. Low density and ultra-low viscosity produce rapid, high-frequency slosh waves with sharp meniscus edges.
2. **Liquid Mercury (Quicksilver)** ($\rho = 13550\ \mathrm{kg/m}^3, \mu = 0.0015\ \mathrm{Pa}\cdot\mathrm{s}$):
   - Mirror-finish chrome liquid metal. Massive density carries $13.5\times$ the inertial mass of water. Resists sudden acceleration but retains immense kinetic momentum, producing powerful, persistent wave oscillations.
3. **Synthetic Motor Oil** ($\rho = 890\ \mathrm{kg/m}^3, \mu = 0.2500\ \mathrm{Pa}\cdot\mathrm{s}$):
   - Golden-brown lubricant. High viscous dissipation suppresses small capillary ripples, maintaining smooth laminar flow.
4. **Hydraulic Coolant** ($\rho = 1060\ \mathrm{kg/m}^3, \mu = 0.0035\ \mathrm{Pa}\cdot\mathrm{s}$):
   - Fluorescent emerald green circulating hydraulic fluid. Optimized for energetic fluid surging across piping networks.
5. **Cryogenic Liquid Gas / Propane** ($\rho = 500\ \mathrm{kg/m}^3, \mu = 0.0001\ \mathrm{Pa}\cdot\mathrm{s}$):
   - Crystal-clear pale ice-blue cryogen. Ultralight density and almost zero dynamic drag generate hyperactive high-frequency fluttering ripples.
6. **Tree Sap / Natural Amber Resin** ($\rho = 1380\ \mathrm{kg/m}^3, \mu = 8.5000\ \mathrm{Pa}\cdot\mathrm{s}$):
   - Rich golden-red viscous sap. Heavy boundary layer wall cling with thick creeping flow along branching channels.
7. **Heavy Maze Gel / Industrial Molasses** ($\rho = 1420\ \mathrm{kg/m}^3, \mu = 25.0000\ \mathrm{Pa}\cdot\mathrm{s}$):
   - Opaque crimson gel. Strongly overdamped flow traversing constricted labyrinth chambers.

---

### 9.2 Fill Amount Magnitude Changing Logic

In fluid mechanics, free-surface slosh wave amplitude is physically constrained by fluid volume and container headspace:

$$\Psi_{\mathrm{fill}}(F) = 4 \cdot F \cdot (1 - F)$$

Where $F \in [0.0, 1.0]$ is the normalized container fill fraction.

#### Physical Principles of the Fill Envelope:
1. **Near-Empty Regime ($F \to 0$)**: Fluid mass approaches zero and the shallow boundary layer quenches surface standing waves ($\lim_{F \to 0} \Psi(F) = 0$).
2. **Intermediate Resonance Peak ($F \approx 0.50$)**: Maximum free-surface area combined with adequate headspace allows unrestricted slosh wave cresting and standing wave resonance ($\Psi(0.50) = 1.0$).
3. **Near-Full Confinement ($F \to 1.0$)**: Fluid is bounded by the container ceiling; headspace confinement physically suppresses wave amplitude ($\lim_{F \to 1} \Psi(F) = 0$).

#### Gravity Wave Resonant Frequency Dispersion:
Based on intermediate-to-deep water dispersion mechanics:

$$\omega_n(F) = \omega_0 \cdot \sqrt{\tanh(\pi \cdot F)}$$

Shallow fill levels exhibit longer wave periods, while deeper fill levels shift toward the asymptotic deep-water gravity slosh frequency.

#### Real-Time Shader Integration:
- The controller calculates the invariant container-relative cut plane position:
  $$Y_{\mathrm{cut}} = \mathrm{lerp}(Y_{\min}, Y_{\max}, F)$$
- Updates the shader property `_Float` in real-time via `MaterialPropertyBlock`, perfectly invariant to world space translation so raising or lowering containers never alters the fluid level.
- Interactive hotkeys (`[` to drain, `]` to fill) and an automated harmonic pump cycle (`enableAutoFillDrainCycle`) allow live visualization of fill-level slosh modulation.

---

### 9.3 The 7 Complex Morphological Structures & Geometric Tortuosity

| Structure Name | Morphological Description | Fluid Material | Tortuosity ($\kappa$) | Prefab Path |
|---|---|---|---|---|
| **Week9_PetrolContainer_Jerrycan** | Rounded rectangular body, top tubular handle, stiffening side ribs, angled filler spout & cap | Petrol (Gasoline) | $1.35$ | `Assets/Prefabs/Week9/Week9_PetrolContainer_Jerrycan.prefab` |
| **Week9_StarShapedBox** | 5-pointed extruded star prism with 10 acute boundary ridges, top lid rim, and base stand | Liquid Mercury | $2.20$ | `Assets/Prefabs/Week9/Week9_StarShapedBox.prefab` |
| **Week9_ToroidalTank** | Upright circular annular ring torus with central void opening, top port, and 4 cradle legs | Synthetic Motor Oil | $1.75$ | `Assets/Prefabs/Week9/Week9_ToroidalTank.prefab` |
| **Week9_YJunctionManifold** | **Important**: 3-way piping manifold with vertical trunk splitting into dual symmetric angled branches, flange rings, and cross-strut | Hydraulic Coolant | $2.45$ | `Assets/Prefabs/Week9/Week9_YJunctionManifold.prefab` |
| **Week9_CapsuleTank** | Cylindrical pressure barrel with dual hemispherical dome caps, reinforcing girth bands, and support legs | Cryogenic Gas | $1.10$ | `Assets/Prefabs/Week9/Week9_CapsuleTank.prefab` |
| **Week9_TreeBranchedStructure** | Dendritic multi-tier branching tree: base trunk, 3 primary limbs, 6 capillary spouts, and terminal reservoir bulbs | Tree Sap Resin | $2.60$ | `Assets/Prefabs/Week9/Week9_TreeBranchedStructure.prefab` |
| **Week9_MazeMultichamber** | 4-chamber baffled labyrinth tank with alternating underflow slots and overflow weir partitions | Heavy Maze Gel | $3.10$ | `Assets/Prefabs/Week9/Week9_MazeMultichamber.prefab` |

---

## 10. Showcase Controls & Interactive In-Game Authority

### Camera Navigation Controls
- **`[WASD]` / `[Arrow Keys]`**: Fly camera freely across the showcase without sticking or key collisions.
- **`[Right-Click + Mouse Move]`**: Look around in 360° (first-person spectator view).
- **`[Shift]`**: Sprint multiplier ($2.5\times$ speed).
- **`[Space] / [E]`**: Fly upward.
- **`[C] / [Ctrl] / [Q]`**: Fly downward.
- **`[F1]`**: **Overview Mode** (frames all 3 showcase tiers).
- **`[F6]`**: **Week 9 Showcase Line** (frames the 7 new realistic structures).
- **`[F2]`**: **Week 8 Showcase Line** (frames the 12 arbitrary geometry vessels).
- **`[F3]`**: **Week 7 Baseline Line** (frames the original 5 containers).
- **`[F4]`**: **Focus Camera** on selected vessel.
- **`[F5]`**: **Free-Fly Spectator Mode**.
- **`[H]`**: Toggle on-screen telemetry HUD.

### In-Game Bottle Authority & Dynamic Fill
- **Left-Click & Drag**: Click directly on **any** bottle or structure in the scene to move/shake it in real-time.
- **Mouse Scroll Wheel**: Push or pull the dragged container closer or further in depth ($Z$-axis).
- **Interactive 3D Move Gizmo**: Displays interactive red ($X$), green ($Y$), and blue ($Z$) move handles on the selected vessel.
- **`[` / `]`**: Dynamically decrease or increase fluid fill level ($F$) in real-time. Watch sloshing peak at $50\%$ and suppress at $10\%$ and $95\%$.
- **`[Alt + Arrow Keys]`**: Nudge selected bottle in 3D.
- **`[Alt + Q/E]`**: Tilt selected bottle left or right.
- **`[1-7]`**: Switch fluid profile (Petrol, Mercury, Motor Oil, Coolant, CryoGas, Tree Sap, Maze Gel) on the selected Week 9 vessel.
- **`[R]`**: Reset selected vessel to its original shelf plinth.
- **`[Backspace]`**: Reset all vessels across all 3 tiers.

---

## 11. Automated Editor Setup Tools

The project includes editor tools under the Unity menu bar:
- **`Tools > Setup Week 9 - Realistic Fluid Dynamics & Dynamic Fill Showcase`**: Automatically creates Week 9 models, prefabs, materials, and assembles the front showcase tier.
- **`Tools > Setup Week 2 - Arbitrary Geometry Showcase`**: Assembles the 12 Week 8 arbitrary geometry vessels.
- **`Tools > Setup Fluid Containers`**: Assembles the 5 Week 7 baseline bottles.

---

## 12. Version Control & Contribution Rules

- Generated Unity folders (`Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`) are excluded via `.gitignore`.
- All Unity `.meta` files are tracked to ensure asset GUID integrity.
- Do not commit local IDE configuration or user caches.

---

## 13. Author & Academic Context

**Harsh**  
Indraprastha Institute of Information Technology Delhi (IIIT-Delhi)  
Developed for research and development in real-time fluid simulation and interactive graphics.
