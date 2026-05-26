// ============================================================================
// TMP3D_GSCompat.cginc
//  - Geometry シェーダ依存のマクロ/関数を “無効化しつつ互換” させるためのシム。
//  - 既存の *.shader / *.cginc が GS 前提で書かれていても、このファイルを
//    先に include しておけば Metal / D3D11 (vs_4_0) でビルドできるようにします。
//  - 重要: 本シムは「頂点->フラグメント」直通の MetalSafe 版を前提とし、
//    GS が付与していた境界情報等は uv3/uv4/uv5 にエンコード済みであることを仮定します。
//
// 使い方:
//  1) シェーダの先頭の方で以下を追加:
//       #define TMP3D_USE_COMPAT 1
//       #include "Lib/Compat/TMP3D_GSCompat.cginc"
//  2) 既存のコード側で g2f / V2G / G2F / EMIT_* など GS 前提の記述があっても
//     ほとんどそのまま通ります（内部では no-op マクロ化）。
//
// 注意:
//  - ここで定義している型/マクロ名は “よくある” TMP3D 実装を想定しています。
//    自作の別名シンボルがある場合は、このファイルに alias を追記してください。
// ============================================================================

#ifndef TMP3D_GS_COMPAT_INCLUDED
#define TMP3D_GS_COMPAT_INCLUDED

// --------------------------------------------------------------------------
// 1)「GS がある前提」の判定フラグをオフにする
// --------------------------------------------------------------------------
#ifndef TMP3D_HAS_GEOMETRY
    #define TMP3D_HAS_GEOMETRY 0
#endif

// --------------------------------------------------------------------------
/*
 2) 代表的なストリーム/構造体名の互換
    - 既存コードで g2f / v2g / appdata などを使っているケースを想定。
    - MetalSafe 版では頂点シェーダ出力 struct を「tmp3d_g2f_metal」としているので、
      それを g2f / v2f の別名にします。
*/
#ifdef SHADER_STAGE_VERTEX
    // 頂点ステージ側では forward 宣言のみ
#endif

// 既に別の g2f がある場合は尊重
#ifndef g2f
    #define g2f tmp3d_g2f_metal
#endif
#ifndef v2f
    #define v2f tmp3d_g2f_metal
#endif

// --------------------------------------------------------------------------
/*
 3) GS で使うことの多いマクロ/関数の no-op 化
    - EMIT / AppendTriangle / RestartStrip 等を何もしない形に置換。
    - VERTEX_TO_GEOMETRY / GEOMETRY_TO_FRAGMENT は「通すだけ」化。
*/
#ifndef EMIT_VERTEX
    #define EMIT_VERTEX(x)   /* no-op */
#endif
#ifndef EMIT_TRIANGLE
    #define EMIT_TRIANGLE(a,b,c) /* no-op */
#endif
#ifndef RESTART_STRIP
    #define RESTART_STRIP()  /* no-op */
#endif

#ifndef VERTEX_TO_GEOMETRY
    #define VERTEX_TO_GEOMETRY(v) (v)
#endif
#ifndef GEOMETRY_TO_FRAGMENT
    #define GEOMETRY_TO_FRAGMENT(v) (v)
#endif

// --------------------------------------------------------------------------
/*
 4) GS が計算していた「境界情報」を取得するためのヘルパ
    - MetalSafe 版では、uv3/uv4/uv5 にエンコード済み。
    - 既存コードが GetBoundaries... のような関数を呼んでいた場合に合わせます。
*/
inline float4 TMP3D_GetBoundariesUV(g2f IN)      { return IN.boundariesUV; }
inline float4 TMP3D_GetBoundariesLocal(g2f IN)   { return IN.boundariesLocal; }
inline float4 TMP3D_GetBoundariesLocalZ(g2f IN)  { return IN.boundariesLocalZ; }

// 互換 alias（よくある命名）
#ifndef GetBoundariesUV
    #define GetBoundariesUV(IN)      TMP3D_GetBoundariesUV(IN)
#endif
#ifndef GetBoundariesLocal
    #define GetBoundariesLocal(IN)   TMP3D_GetBoundariesLocal(IN)
#endif
#ifndef GetBoundariesLocalZ
    #define GetBoundariesLocalZ(IN)  TMP3D_GetBoundariesLocalZ(IN)
#endif

// --------------------------------------------------------------------------
/*
 5) 代表的なフィールド名の互換用マクロ
    - 既存の g2f が worldPos/atlas/tmp3d 等を持っていた想定で alias。
*/
#ifndef G2F_ATLAS
    #define G2F_ATLAS(IN)        ((IN).atlas)
#endif
#ifndef G2F_COLOR
    #define G2F_COLOR(IN)        ((IN).color)
#endif
#ifndef G2F_WORLD_POS
    #define G2F_WORLD_POS(IN)    ((IN).worldPos)
#endif
#ifndef G2F_TMP3D
    #define G2F_TMP3D(IN)        ((IN).tmp3d)
#endif
#ifndef G2F_TMP
    #define G2F_TMP(IN)          ((IN).tmp)
#endif

// --------------------------------------------------------------------------
/*
 6) 既存 GS 向けユーティリティ関数のダミー実装
    - 例: SetPerCharacterState(...) などがあれば、ここで「何もしない版」を用意して
      呼び出し側のコンパイルを通します（必要に応じて追記）。
*/
inline void TMP3D_SetPerCharacterState(inout g2f s) { /* noop */ }

// --------------------------------------------------------------------------
/*
 7) デバッグ系 define の安全化
*/
#ifndef DEBUG_STEPS
    // #define DEBUG_STEPS 1  // 必要なら有効化
#endif
#ifndef DEBUG_MASK
    // #define DEBUG_MASK 1
#endif

#endif // TMP3D_GS_COMPAT_INCLUDED