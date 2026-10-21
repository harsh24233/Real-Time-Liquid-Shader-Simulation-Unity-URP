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
- **Interactive Controls**: Built-in mouse dragging and keyboard translation for live play-mode testing.

---

## 2. Verified Project Specifications

| Component | Specification |
|---|---|
| **Unity Editor** | `6000.6.1f1` (Unity 6) |
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

## 4. Container Geometries & Prefabs

The project includes custom-engineered container models separating the outer glass vessel from the inner fluid volume:

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
│   ├── Models/
│   │   ├── Bottle_Hexagonal.obj
│   │   ├── Bottle_Hexagonal_Fluid.obj
│   │   ├── Bottle_Hexagonal_Glass.obj
│   │   ├── Bottle_Square.obj
│   │   ├── Flask_Conical.obj
│   │   ├── Flask_Spherical.obj
│   │   └── Jar_Cylinder.obj
│   ├── Prefabs/
│   │   ├── Bottle_Hexagonal_Water.prefab
│   │   ├── Bottle_Square_VegOil.prefab
│   │   ├── Flask_Conical_Milk.prefab
│   │   ├── Flask_Spherical_Honey.prefab
│   │   └── Jar_Cylinder_Ketchup.prefab
│   ├── Scenes/
│   │   └── SampleScene.unity
│   ├── Settings/
│   │   ├── PC_RPAsset.asset
│   │   ├── Mobile_RPAsset.asset
│   │   └── UniversalRenderPipelineGlobalSettings.asset
│   ├── LiquidShaderGrapjh.shadergraph
│   ├── LiquidWobble.cs
│   ├── ViscousLiquidWobble.cs
│   ├── Honey_Fluid.mat
│   ├── Milk_Fluid.mat
│   ├── Ketchup_Fluid.mat
│   ├── VegOil_Fluid.mat
│   ├── Water_Fluid.mat
│   ├── glass.mat
│   └── generate_models_v2.py
├── Packages/
│   ├── manifest.json
│   └── packages-lock.json
├── ProjectSettings/
│   ├── ProjectVersion.txt
│   ├── TagManager.asset
│   ├── QualitySettings.asset
│   └── ...
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

### 6.2 `ViscousLiquidWobble.cs` & `LiquidWobble.cs`
C# MonoBehaviour scripts executing both in Editor (`[ExecuteAlways]`) and Play Mode:
- **Velocity & Angular Velocity Tracking**: Derives acceleration from frame-to-frame transform deltas.
- **Viscous Damping Sine Wave**: Calculates wobble displacement with frequency and decay scaled by viscosity.
- **Material Property Block / Instance Updates**: Feeds `_WobbleX` and `_WobbleZ` into the shader.
- **Interactive Controls**: Supports mouse dragging and WASD/arrow key interaction during runtime testing.

---

## 7. Getting Started

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
7. Press **Play** to interact with the containers using mouse click-and-drag or keyboard controls.

---

## 8. Version Control & Contribution Rules

- Generated Unity folders (`Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`) are excluded via `.gitignore`.
- All Unity `.meta` files are tracked to ensure asset GUID integrity.
- Do not commit local IDE configuration or user caches.

---

## 9. Author & Academic Context

**Harsh**  
Indraprastha Institute of Information Technology Delhi (IIIT-Delhi)  
Developed for research and development in real-time fluid simulation and interactive graphics.
