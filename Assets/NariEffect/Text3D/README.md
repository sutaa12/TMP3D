# Text3D

`Text3D` is the project-owned 3D TextMeshPro effect package.

## Main Prefabs

- `Prefabs/3DTextSample.prefab`: sample 3D text prefab using the solid renderer.

## Runtime Components

- `Text3DSolidRenderer`: renders TextMeshPro glyph quads as solid 3D text. It uses the GPU builder path when supported and falls back to TMP3D UV2 mesh data when compute shaders or the custom render shader are unavailable.
- `Text3DGlyphThickness`: stores per-glyph thickness and depth range data.
- `Text3DStylePresetController`: applies combinable face color, inside/depth color, outline, emboss, and lighting presets to `NariEffect/Text3D/SolidUnlit` materials. It uses a runtime material instance by default so multiple objects can switch presets independently in Play Mode.
- `Text3DMaterialInspector`: custom material inspector for the solid unlit shader.
- `Text3DExplainerMaterialInspector`: custom material inspector for the explainer shader modes.

## Requirements

- Unity `6000.3.16f1`.
- TextMesh Pro resources under `Assets/TextMesh Pro`.
- TMP3D vendor package under `Assets/TMP3D`.
- GPU geometry building requires compute shader support, `BuildText3DGeometry.compute`, and `NariEffect/Text3D/SolidUnlit`.

## Explainer Shader

- `Shaders/Text3DSolidUnlitExplainer.shader` is a drop-in copy of `NariEffect/Text3D/SolidUnlit` with extra visualization modes.
- `Materials/Text3DSolidUnlitExplainer.mat` can be assigned to `Text3DSolidRenderer`'s `Solid Material Template` field, or the TextMeshPro font material shader can be changed to `NariEffect/Text3D/SolidUnlitExplainer`.
- Mode summary:
  - `01 Compute Box Only`: draws only the compute-generated extruded boxes in cyan with white edges.
  - `02 Mask Coordinates`: visualizes local box-to-glyph mask coordinates as RGB.
  - `03 Atlas SDF Slice`: shows the font atlas SDF as grayscale.
  - `04 Text Only`: shows only raymarched text hits in white.
  - `05 Box Raycast Hit Test`: green is text hit, red is empty box space.
  - `06 Ray Steps`: shows raymarch step count as grayscale.
  - `07 Depth Progress`: shows hit depth through the solid glyph as grayscale.
  - `08 Final Rendering`: same intent as the production render path.

## Setup Notes

1. Add `Assets/NariEffect/Text3D/Text3DCounterSample.unity` or a scene using the prefab to Build Settings.
2. Keep `.meta` files intact when moving or renaming assets.
3. Keep shader assets referenced by materials or Always Included Shaders for player builds that rely on `Shader.Find`.

## Verification

- C# smoke check: `dotnet build Test.sln --no-incremental`.
- Unity Editor verification is still required for shader import, prefab wiring, scene play mode, and player builds.
