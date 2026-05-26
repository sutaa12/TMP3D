/// <summary>
/// compute shader が生成した厚み付き TextMeshPro ジオメトリを、SDF レイマーチで塗り分ける Text3D 用シェーダーです。
/// </summary>
/// <remarks>
/// メイン SubShader は StructuredBuffer を読むため shader target 5.0 を使います。下部の SubShader は低機能環境向けの透明フォールバックです。
/// </remarks>
Shader "NariEffect/Text3D/SolidUnlit"
{
    Properties
    {
        /// <summary>表面に乗算する任意のテクスチャです。</summary>
        _FaceTex("Face Texture", 2D) = "white" {}
        /// <summary>文字表面のベース色です。</summary>
        _Color("Color", Color) = (1,1,1,1)
        /// <summary>太字文字で使う SDF のしきい値です。</summary>
        _WeightBold("Weight Bold", Range(0,1)) = 0.6
        /// <summary>通常文字で使う SDF のしきい値です。</summary>
        _WeightNormal("Weight Normal", Range(0,1)) = 0.5
        /// <summary>表面テクスチャを Y 方向へスクロールする速度です。</summary>
        _FaceTextureScrollSpeed("FaceTextureScrollSpeed", float) = 0
        /// <summary>アウトラインテクスチャを Y 方向へスクロールする速度です。</summary>
        _OutlineTextureScrollSpeed("OutlineTextureScrollSpeed", float) = 0

        /// <summary>レイマーチ 1 回あたりの最小前進距離です。</summary>
        _RaymarchMinStep("Raymarch min step", Range(0.001, 0.1)) = 0.01
        /// <summary>レイマーチ 1 回あたりの最大前進距離です。</summary>
        _RaymarchStepLength("Raymarch step length", Range(0.001, 1)) = 0.1
        /// <summary>レイマーチのステップ長をランダム化するためのブルーノイズ配列です。</summary>
        _RaymarchBlueNoise("Raymarch Blue Noise", 2DArray) = "black" {}
        /// <summary>ブルーノイズ配列のスライス数です。</summary>
        _RaymarchBlueNoise_Slices("Raymarch Blue Noise Slices", float) = 1
        /// <summary>ブルーノイズの時間方向アニメーション速度です。</summary>
        _RaymarchBlueNoise_Speed("Raymarch Blue Noise Speed", float) = 10
        /// <summary>ブルーノイズから補間するステップ長の最小値と最大値です。</summary>
        _RaymarchTemporalStepLength("Raymarch Temporal Step Length", Vector) = (0.01, 0.05, 0, 0)
        /// <summary>奥行き方向の色グラデーションに使うテクスチャです。</summary>
        _DepthAlbedo("Depth Albedo", 2D) = "white" {}
        /// <summary>奥行き面へ乗算する色です。</summary>
        _DepthColor("Depth Color", Color) = (1,1,1,1)

        /// <summary>1 段目アウトラインに乗算するテクスチャです。</summary>
        _OutlineTex("Outline Texture", 2D) = "white" {}
        /// <summary>1 段目アウトラインの色です。</summary>
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        /// <summary>1 段目アウトラインの太さです。</summary>
        _OutlineWidth("Outline Thickness", Range(0,1)) = 0
        /// <summary>別パスで描く 2 段目アウトラインの色です。</summary>
        _Outline2Color("Outline2 Color", Color) = (0,0,0,1)
        /// <summary>別パスで描く 2 段目アウトラインの太さです。</summary>
        _Outline2Width("Outline2 Thickness", Range(0,1)) = 0

        /// <summary>TextMeshPro の SDF フォントアトラスです。</summary>
        _MainTex("Font Atlas", 2D) = "white" {}
        /// <summary>フォントアトラスの幅です。</summary>
        _TextureWidth("Texture Width", float) = 512
        /// <summary>フォントアトラスの高さです。</summary>
        _TextureHeight("Texture Height", float) = 512
        /// <summary>SDF の勾配スケールです。エッジの太さとアンチエイリアスに使います。</summary>
        _GradientScale("Gradient Scale", float) = 5.0
        /// <summary>TMP 互換の X スケールです。</summary>
        _ScaleX("Scale X", float) = 1.0
        /// <summary>TMP 互換の Y スケールです。</summary>
        _ScaleY("Scale Y", float) = 1.0
        /// <summary>TMP 互換の遠近補正係数です。</summary>
        _PerspectiveFilter("Perspective Correction", Range(0, 1)) = 0.875
        /// <summary>TMP 互換のシャープネス調整値です。</summary>
        _Sharpness("Sharpness", Range(-1,1)) = 0

        /// <summary>TMP 内部用のスケール比 A です。</summary>
        _ScaleRatioA("Scale Ratio A", float) = 1.0
        /// <summary>TMP 内部用のスケール比 B です。</summary>
        _ScaleRatioB("Scale Ratio B", float) = 1.0
        /// <summary>TMP 内部用のスケール比 C です。</summary>
        _ScaleRatioC("Scale Ratio C", float) = 1.0

        /// <summary>SDF から疑似法線を作るベベル幅です。</summary>
        _BevelWidth("Bevel Width", Range(0,1)) = 0.1
        /// <summary>ベベルを内側または外側へずらす量です。</summary>
        _BevelOffset("Bevel Offset", Range(-1,1)) = 0.0
        /// <summary>ベベル法線の強度です。</summary>
        _Bevel("Bevel Strength", Range(0,1)) = 1.0
        /// <summary>ベベル形状の丸みです。</summary>
        _BevelRoundness("Bevel Roundness", Range(0,1)) = 0.5
        /// <summary>ベベルの山を削る量です。</summary>
        _BevelClamp("Bevel Clamp", Range(0,1)) = 0.1

        /// <summary>疑似スペキュラーの色です。</summary>
        _SpecularColor("SpecularColor", Color) = (1,1,1,1)
        /// <summary>スペキュラーの鋭さです。</summary>
        _Reflectivity("Reflectivity", Range(5.0,15.0)) = 10
        /// <summary>スペキュラーの強度です。</summary>
        _SpecularPower("Specular Power", Range(0,4)) = 0
        /// <summary>XY 平面上の疑似ライト角度です。</summary>
        _LightAngle("Light Angle", Range(0,6.28)) = 1.0
        /// <summary>疑似拡散光の影響量です。</summary>
        _Diffuse("Diffuse Term", Range(0,1)) = 0.5
        /// <summary>最低限残す環境光の量です。</summary>
        _Ambient("Ambient Term", Range(0,1)) = 0.2
    }

    // ===== SubShader 1: メイン（Compute 生成バッファ参照 / Raymarch 実行）=====
    SubShader
    {
        Tags { "Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="Geometry" }
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha
        // ---------- Main Pass ----------
        Pass
        {
            Stencil { Ref 2 Comp Always Pass Replace }

            CGPROGRAM
            #pragma target 5.0
            #pragma vertex   Text3DSolidVertex
            #pragma fragment Text3DUnlitFragment
            #pragma multi_compile __ DEBUG_STEPS DEBUG_MASK
            #include "UnityCG.cginc"

            /// <summary>C# から設定される Text3D オブジェクトのローカルからワールドへの変換行列です。</summary>
            float4x4 _Text3DLocalToWorld;
            /// <summary>C# から設定される Text3D オブジェクトのワールドからローカルへの変換行列です。</summary>
            float4x4 _Text3DWorldToLocal;

            /// <summary>
            /// compute shader が出力した厚み付き文字頂点です。
            /// </summary>
            /// <remarks>
            /// BuildText3DGeometry.compute と Text3DSolidRenderer.SolidGlyphVertex と同じ順序・型を保ちます。
            /// </remarks>
            struct SolidGlyphVertex
            {
                /// <summary>ローカル空間の頂点位置です。</summary>
                float3 position;
                /// <summary>ローカル空間の面法線です。</summary>
                float3 normal;
                /// <summary>頂点色です。</summary>
                float4 color;        // 0..1
                /// <summary>フォント SDF アトラス UV です。</summary>
                float2 texcoord0;    // atlas UV
                /// <summary>TMP 互換用の補助値です。y はボールド判定に使います。</summary>
                float2 texcoord1;    // tmp (x:unused, y:boldFlag) ※既定 y=1=non-bold
                /// <summary>x が厚み、y/z が奥行き色範囲です。</summary>
                float4 texcoord2;    // tmp3d (x:depth, yz:depthMapped, w:unused)
                /// <summary>アトラス UV 内の文字境界です。</summary>
                float4 boundariesUV;     // (xMin, yMin, width, height) in atlas
                /// <summary>ローカル XY 平面上の文字境界です。</summary>
                float4 boundariesLocal;  // (xMin, yMin, width, height) in local
                /// <summary>Z 範囲と斜体補正値です。</summary>
                float4 boundariesLocalZ; // (zMin, zMax, skewLocal, skewUV)
            };

            /// <summary>
            /// 頂点シェーダーからフラグメントシェーダーへ渡す Text3D 描画用データです。
            /// </summary>
            struct VertexToFragment
            {
                /// <summary>クリップ空間位置です。</summary>
                float4 position        : SV_POSITION;
                /// <summary>頂点色です。</summary>
                fixed4 color           : COLOR;
                /// <summary>フォント SDF アトラス UV です。</summary>
                float2 atlas           : TEXCOORD0;
                /// <summary>ワールド空間位置です。</summary>
                float4 worldPos        : TEXCOORD1;
                /// <summary>アトラス UV 内の文字境界です。</summary>
                float4 boundariesUV    : TEXCOORD2;
                /// <summary>ローカル XY 平面上の文字境界です。</summary>
                float4 boundariesLocal : TEXCOORD3;
                /// <summary>Z 範囲と斜体補正値です。</summary>
                float4 boundariesLocalZ: TEXCOORD4;
                /// <summary>厚みと奥行き色範囲です。</summary>
                float4 tmp3d           : TEXCOORD5;
                /// <summary>TMP 互換用の補助値です。</summary>
                float2 tmp             : TEXCOORD6;
            };

            /// <summary>
            /// 色と深度を同時に返すフラグメント出力です。
            /// </summary>
            struct FragmentOutput { fixed4 color:SV_Target; float depth:SV_Depth; };

            /// <summary>compute shader が生成したソリッド頂点バッファです。</summary>
            StructuredBuffer<SolidGlyphVertex> _SolidGlyphVertexBuffer;
            /// <summary>compute shader が生成したインデックスバッファです。</summary>
            StructuredBuffer<uint>       _SolidGlyphIndexBuffer;

            /// <summary>TMP SDF アトラスとその Tiling/Offset です。</summary>
            sampler2D _MainTex; float4 _MainTex_ST;
            /// <summary>表面テクスチャとその Tiling/Offset です。</summary>
            sampler2D _FaceTex; float4 _FaceTex_ST;
            /// <summary>アウトラインテクスチャとその Tiling/Offset です。</summary>
            sampler2D _OutlineTex; float4 _OutlineTex_ST;
            /// <summary>奥行き方向のグラデーションテクスチャです。</summary>
            sampler2D _DepthAlbedo;
            /// <summary>表面色、アウトライン色、奥行き色です。</summary>
            fixed4 _Color, _OutlineColor, _DepthColor;
            /// <summary>フォントアトラス寸法と SDF 勾配スケールです。</summary>
            float  _TextureWidth, _TextureHeight, _GradientScale;
            /// <summary>太字・通常文字の SDF しきい値です。</summary>
            float  _WeightBold, _WeightNormal;
            /// <summary>表面/アウトラインテクスチャのスクロール速度です。</summary>
            half   _FaceTextureScrollSpeed, _OutlineTextureScrollSpeed;

            /// <summary>アウトライン幅と TMP 互換の未使用ソフトネスです。</summary>
            half _OutlineWidth, _OutlineSoftness; // _OutlineSoftnessは計算には未使用だが互換のため
            /// <summary>疑似ベベルの形状調整値です。</summary>
            half _BevelWidth, _BevelOffset, _BevelRoundness, _BevelClamp;
            /// <summary>疑似ベベルの強度です。</summary>
            int  _Bevel;
            /// <summary>疑似ライティングの拡散光、環境光、ライト角です。</summary>
            half _Diffuse, _Ambient, _LightAngle;
            /// <summary>疑似スペキュラーの鋭さと強度です。</summary>
            half _Reflectivity, _SpecularPower;
            /// <summary>疑似スペキュラーの色です。</summary>
            fixed4 _SpecularColor;

            /// <summary>レイマーチの最小ステップと最大ステップです。</summary>
            float _RaymarchMinStep, _RaymarchStepLength;
            /// <summary>ブルーノイズから補間する一時ステップ長の範囲です。</summary>
            float2 _RaymarchTemporalStepLength;
            /// <summary>ステップ長のばらつきに使うブルーノイズ配列です。</summary>
            UNITY_DECLARE_TEX2DARRAY(_RaymarchBlueNoise);
            /// <summary>ブルーノイズテクスチャのテクセル情報です。</summary>
            float4 _RaymarchBlueNoise_TexelSize;
            /// <summary>ブルーノイズのスライス数と時間方向速度です。</summary>
            float  _RaymarchBlueNoise_Slices, _RaymarchBlueNoise_Speed;
            /// <summary>TMP 互換の SDF スケール比です。</summary>
            float _ScaleRatioA;

            // ====== 定数 ======
            #define MAX_STEPS 32

            /// <summary>値 x が a..b の範囲でどの位置にあるかを 0..1 基準で返します。</summary>
            float InverseLerp(float a, float b, float x){ return (x-a) / max(1e-6,(b-a)); }

            /// <summary>3D マスク座標の x/y を文字の UV 範囲へ戻し、SDF アトラスをサンプリングします。</summary>
            float SampleSDF3D(float3 mask3D, VertexToFragment i){
                float2 uv;
                uv.x = saturate(lerp(i.boundariesUV.x, i.boundariesUV.x + i.boundariesUV.z, mask3D.x));
                uv.y = saturate(lerp(i.boundariesUV.y, i.boundariesUV.y + i.boundariesUV.w, mask3D.y));
                return tex2D(_MainTex, uv).a;
            }

            /// <summary>3D マスク座標が 0..1 のボックス内にあるかを、clip しやすい符号付き値で返します。</summary>
            float IsInBounds(float3 m){
                float cx = -(abs(m.x - 0.5) - 0.5) + 0.01;
                float cy = -(abs(m.y - 0.5) - 0.5) + 0.01;
                float cz = -(abs(m.z - 0.5) - 0.5) + 0.01;
                return min(0, min(cx, min(cy, cz)));
            }

            /// <summary>ローカル空間位置を、文字ごとの 0..1 マスク空間へ変換します。</summary>
            float3 PositionToMask(float3 lp, VertexToFragment i){
                float3 m;
                m.y = InverseLerp(i.boundariesLocal.y, i.boundariesLocal.y + i.boundariesLocal.w, lp.y);
                float xOffset = saturate(m.y) * i.boundariesLocalZ.z;
                m.x = InverseLerp(i.boundariesLocal.x, i.boundariesLocal.x + i.boundariesLocal.z, lp.x - xOffset);
                m.z = InverseLerp(i.boundariesLocalZ.x, i.boundariesLocalZ.y, lp.z);
                return m;
            }

            /// <summary>SDF の値を、現在文字のローカル空間上で進めるべき距離へ変換します。</summary>
            float GradientToLocalLength(VertexToFragment i, float value, float edge){
                float gradientUV = _GradientScale / max(1.0, _TextureHeight);
                float gradientRelative = gradientUV / max(1e-6, i.boundariesUV.w);
                float localM = i.boundariesLocal.w * gradientRelative;
                float mn = -(localM * edge);
                float mx =  (localM * (1 - edge));
                return lerp(mn, mx, value);
            }

            /// <summary>プラットフォーム差を吸収しながら SV_Depth 用の深度を計算します。</summary>
            float ComputeDepth(float4 clippos){
                #if defined(SHADER_TARGET_GLSL) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
                    return ((clippos.z / clippos.w) + 1.0) * 0.5;
                #else
                    return clippos.z / clippos.w;
                #endif
            }

            /// <summary>
            /// SDF アトラス周辺 4 点からベベル用の疑似法線を作ります。
            /// </summary>
            /// <param name="uv">サンプリング中心のアトラス UV。</param>
            /// <param name="bias">SDF のしきい値補正。</param>
            /// <param name="dxy">アトラス上で隣接点を取るための UV オフセット。</param>
            /// <returns>疑似ベベルに使うローカル法線。</returns>
            fixed3 GetSurfaceNormal(float2 uv, half bias, float3 dxy)
            {
                fixed4 h = fixed4(
                    tex2D(_MainTex, uv - dxy.xz).a,
                    tex2D(_MainTex, uv + dxy.xz).a,
                    tex2D(_MainTex, uv - dxy.zy).a,
                    tex2D(_MainTex, uv + dxy.zy).a
                );

                h += bias + _BevelOffset;
                half bevelW = max(0.01, _OutlineWidth + _BevelWidth);
                h -= 0.5; h /= bevelW; h = saturate(h + 0.5);
                h = 1 - abs(h * 2 - 1);
                h = lerp(h, sin(h * UNITY_PI * 0.5), _BevelRoundness);
                h = min(h, 1 - _BevelClamp);
                h *= _Bevel * bevelW * _GradientScale * -2.0;

                fixed3 va = normalize(fixed3( 1, 0, h.y - h.x));
                fixed3 vb = normalize(fixed3( 0,-1, h.w - h.z));
                return normalize(cross(va, vb));
            }

            /// <summary>
            /// 法線とライト方向から Blinn-Phong 風のスペキュラー色を返します。
            /// </summary>
            /// <param name="n">疑似ベベル法線。</param>
            /// <param name="l">ライト方向。</param>
            /// <returns>スペキュラー反射色。</returns>
            fixed3 GetSpecular(fixed3 n, fixed3 l){
                half spec = pow(max(0, dot(n,l)), _Reflectivity);
                return _SpecularColor.rgb * spec * _SpecularPower;
            }

            /// <summary>
            /// SDF 距離に応じて表面色とアウトライン色をアルファ合成します。
            /// </summary>
            /// <param name="d">SDF から求めたエッジ距離。</param>
            /// <param name="faceColor">文字表面色。</param>
            /// <param name="outlineColor">アウトライン色。</param>
            /// <param name="outline">アウトライン幅。</param>
            /// <returns>表面とアウトラインを混ぜた色。</returns>
            fixed4 BlendFaceAndOutline(half d, fixed4 faceColor, fixed4 outlineColor, half outline)
            {
                half outlineAlpha = saturate((d + outline)) * sqrt(min(1.0, outline));
                faceColor.rgb   *= faceColor.a;
                outlineColor.rgb*= outlineColor.a;
                return lerp(faceColor, outlineColor, outlineAlpha);
            }

            /// <summary>現在フラグメントのローカル空間視線方向です。</summary>
            float3 Temp_ViewDir, Temp_LocalStartPos, Temp_LocalPos, Temp_Mask3D;
            /// <summary>レイマーチ中の境界判定、SDF 値、進行距離です。</summary>
            float  Temp_Bound, Temp_Value, Temp_Progress;
            /// <summary>レイマーチ対象の補間入力です。</summary>
            VertexToFragment    Temp_Input;
            /// <summary>ブルーノイズで決めた今回の最大ステップ長です。</summary>
            float  Temp_ActualStepLength;
            /// <summary>AABB 交差で得たレイの進入距離と退出距離です。</summary>
            float  Temp_Enter, Temp_Exit;

            /// <summary>
            /// レイが平面へ到達する距離を計算します。
            /// </summary>
            /// <param name="rayOrigin">レイの開始位置。</param>
            /// <param name="rayDirection">レイ方向。</param>
            /// <param name="planeNormal">平面法線。</param>
            /// <param name="planeOrigin">平面上の任意の点。</param>
            /// <returns>正方向の交差距離。交差しない場合は負の番兵値。</returns>
            float ProjectRayOntoPlane(float3 rayOrigin, float3 rayDirection, float3 planeNormal, float3 planeOrigin)
            {
                float denom = dot(planeNormal, rayDirection);
                if (abs(denom) <= 1e-3) return -1000;
                float t = dot(planeOrigin - rayOrigin, planeNormal) / denom;
                if (t <= 1e-3) return -1000;
                return t;
            }

            /// <summary>
            /// スラブ法でレイと AABB の交差区間を求めます。
            /// </summary>
            /// <param name="ro">レイの開始位置。</param>
            /// <param name="rd">レイ方向。</param>
            /// <param name="bmin">AABB の最小座標。</param>
            /// <param name="bmax">AABB の最大座標。</param>
            /// <param name="t0">交差区間の開始距離。</param>
            /// <param name="t1">交差区間の終了距離。</param>
            /// <returns>レイが AABB と交差する場合は true。</returns>
            bool IntersectAABB(float3 ro, float3 rd, float3 bmin, float3 bmax, out float t0, out float t1)
            {
                // 0 除算を避けながら invDir ~= 1/rd を符号付きで安定に計算します。
                float3 invAbs = 1.0 / max(abs(rd), 1e-6);
                float3 invDir = invAbs * sign(rd);
                float3 tA = (bmin - ro) * invDir;
                float3 tB = (bmax - ro) * invDir;
                float3 tmin3 = min(tA, tB);
                float3 tmax3 = max(tA, tB);
                float tmin = max(tmin3.x, max(tmin3.y, tmin3.z));
                float tmax = min(tmax3.x, min(tmax3.y, tmax3.z));
                t0 = tmin; t1 = tmax;
                return tmax >= max(tmin, 0.0);
            }

            /// <summary>
            /// 現在フラグメントの視線レイをローカル空間に変換し、文字ボリュームとの交差範囲を初期化します。
            /// </summary>
            /// <param name="i">頂点シェーダーから渡された文字境界情報。</param>
            void InitializeRaymarching(VertexToFragment i)
            {
                // view dir をワールド空間から Text3D ローカル空間へ変換します。正投影時はカメラ前方を使います。
                float3 viewDirWS = normalize(i.worldPos.xyz - _WorldSpaceCameraPos.xyz);
                viewDirWS = lerp(viewDirWS, normalize(mul((float3x3)unity_CameraToWorld, float3(0,0,1))), unity_OrthoParams.w);

                Temp_LocalStartPos = mul(_Text3DWorldToLocal, float4(i.worldPos.xyz,1)).xyz;
                Temp_ViewDir       = mul((float3x3)_Text3DWorldToLocal, viewDirWS);

                // FULL モード相当として、視線逆方向へ文字ボリューム手前まで開始点を戻します。
                float3 negViewDir = normalize(-Temp_ViewDir);

                float3 up   = normalize(float3(i.boundariesLocalZ.z, i.boundariesLocal.w, 0));
                float3 side = normalize(cross(up, float3(0,0,1)));

                float back  = i.boundariesLocalZ.x;
                float front = i.boundariesLocalZ.y;
                float bottom= i.boundariesLocal.y;
                float top   = bottom + i.boundariesLocal.w;
                float left  = i.boundariesLocal.x;
                float right = left + i.boundariesLocal.z;

                float xL = ProjectRayOntoPlane(Temp_LocalStartPos, negViewDir, side, float3(left,  bottom, 0));
                float xR = ProjectRayOntoPlane(Temp_LocalStartPos, negViewDir, side, float3(right, bottom, 0));
                float x  = abs(max(xL, xR));

                float yB = ProjectRayOntoPlane(Temp_LocalStartPos, negViewDir, float3(0,1,0), float3(0, bottom, 0));
                float yT = ProjectRayOntoPlane(Temp_LocalStartPos, negViewDir, float3(0,1,0), float3(0,top,0));
                float y  = abs(max(yB, yT));

                float zB = ProjectRayOntoPlane(Temp_LocalStartPos, negViewDir, float3(0,0,1), float3(0,0, back));
                float zF = ProjectRayOntoPlane(Temp_LocalStartPos, negViewDir, float3(0,0,1), float3(0,0, front));
                float z  = abs(max(zB, zF));

                float camDist = length(mul((float3x3)_Text3DWorldToLocal, _WorldSpaceCameraPos.xyz - i.worldPos.xyz));
                float dist    = min(camDist, min(x, min(y, z)));
                Temp_LocalStartPos += negViewDir * dist;

                Temp_Progress = 0.0;
                Temp_Input    = i;

                // ステップ長をブルーノイズでわずかに変え、縞状のアーティファクトを抑えます。
                float4 sp = ComputeScreenPos(i.position);
                float2 suv = sp.xy * _RaymarchBlueNoise_TexelSize.xy;
                float slice = fmod(_Time.w * _RaymarchBlueNoise_Speed, _RaymarchBlueNoise_Slices);
                float offset = UNITY_SAMPLE_TEX2DARRAY(_RaymarchBlueNoise, float3(suv, slice)).r;
                Temp_ActualStepLength = lerp(_RaymarchTemporalStepLength.x, _RaymarchTemporalStepLength.y, offset);

                // スキューを含む外接 AABB とレイの交差範囲だけを走査し、不要なステップを減らします。
                float skew = i.boundariesLocalZ.z;
                float3 bmin = float3(min(left,  left + skew), bottom, back);
                float3 bmax = float3(max(right, right + skew), top,    front);
                float t0, t1;
                bool hit = IntersectAABB(Temp_LocalStartPos, /*非正規化のまま*/ Temp_ViewDir, bmin, bmax, t0, t1);
                if (hit)
                {
                    // 開始位置を境界内にクランプ
                    Temp_Enter   = max(0.0, t0);
                    Temp_Exit    = t1;
                    Temp_Progress= Temp_Enter;
                }
                else
                {
                    Temp_Enter = 1e20; Temp_Exit = -1e20; // 不ヒット：ループで即終了
                }
            }

            /// <summary>
            /// SDF 値から次に進む距離を決め、レイマーチを 1 ステップ進めます。
            /// </summary>
            /// <param name="edge">現在の太さ設定から求めた SDF のヒットしきい値。</param>
            void NextRaymarch(float edge)
            {
                Temp_LocalPos = Temp_LocalStartPos + Temp_ViewDir * Temp_Progress;
                Temp_Mask3D   = PositionToMask(Temp_LocalPos, Temp_Input);
                Temp_Bound    = IsInBounds(Temp_Mask3D);
                Temp_Value    = 1.0 - SampleSDF3D(saturate(Temp_Mask3D), Temp_Input);

                float sdfDist = GradientToLocalLength(Temp_Input, Temp_Value, edge);
                float2 viewXY = Temp_ViewDir.xy;
                float  lenXY  = max(1e-6, length(viewXY));
                float  ratio  = sdfDist / lenXY;
                float  stepLen = max(0.0, length(Temp_ViewDir) * ratio);
                float  maxStep = min(_RaymarchStepLength, Temp_ActualStepLength);
                Temp_Progress += max(_RaymarchMinStep, min(maxStep, stepLen));
            }

            /// <summary>
            /// SV_VertexID でインデックスバッファを引き、ソリッド頂点をクリップ空間へ変換します。
            /// </summary>
            /// <param name="vertexID">DrawProceduralNow が渡す頂点番号。</param>
            /// <param name="instanceID">未使用のインスタンス番号。</param>
            /// <returns>フラグメントシェーダーへ渡す補間データ。</returns>
            VertexToFragment Text3DSolidVertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                VertexToFragment o;
                uint idx = _SolidGlyphIndexBuffer[vertexID];
                SolidGlyphVertex v = _SolidGlyphVertexBuffer[idx];
                

                float4 wp = mul(_Text3DLocalToWorld, float4(v.position,1));
                o.worldPos        = wp;
                o.position        = mul(UNITY_MATRIX_VP, wp);
                o.color           = v.color;
                o.atlas           = v.texcoord0;
                o.boundariesUV    = v.boundariesUV;
                o.boundariesLocal = v.boundariesLocal;
                o.boundariesLocalZ= v.boundariesLocalZ;
                o.tmp3d           = v.texcoord2;
                o.tmp             = v.texcoord1;
                return o;
            }

            /// <summary>
            /// デバッグキーワードが有効な場合に出力色をステップ数またはマスク座標へ差し替えます。
            /// </summary>
            /// <param name="outp">通常描画で作った出力。</param>
            /// <param name="step">ヒットしたレイマーチステップ。</param>
            /// <returns>デバッグ設定を反映した出力。</returns>
            FragmentOutput ValidateOutput(FragmentOutput outp, int step)
            {
                #if DEBUG_STEPS
                    float s = (float)step / (float)MAX_STEPS;
                    outp.color = float4(s,0,0,1);
                #elif DEBUG_MASK
                    outp.color = float4(Temp_Mask3D.xyz,1);
                #endif
                return outp;
            }

            /// <summary>
            /// 文字ボリューム内をレイマーチして SDF ヒット位置を探し、表面/奥行き/アウトライン/ベベルを合成します。
            /// </summary>
            /// <param name="i">頂点シェーダーから渡された文字境界と UV 情報。</param>
            /// <returns>色と深度を含むフラグメント出力。</returns>
            FragmentOutput Text3DUnlitFragment(VertexToFragment i)
            {
                FragmentOutput o; o.depth=0; o.color=0;

                half bold = step(i.tmp.y, 0);
                half edge = lerp(_WeightNormal, _WeightBold, bold);

                // 事前サンプル
                half s = tex2D(_MainTex, i.atlas).a;

                // 画素勾配→スケール
                float2 dx = ddx(i.atlas), dy = ddy(i.atlas);
                float  grad2 = max(dot(dx,dx), dot(dy,dy));
                float  scale = rsqrt(max(1e-8, grad2)) * _GradientScale;

                float weight = lerp(_WeightNormal, _WeightBold, bold) / 4.0;
                weight = (weight) * _ScaleRatioA * 0.5;
                float bias = (0.5 - weight) + (0.5 / scale);
                float sd   = (bias - s) * scale;

                float outlineWidth = _OutlineWidth;

                // レイマーチ開始前に、文字の外接ボックスと視線の交差区間を求めます。
                InitializeRaymarching(i);

                // ライト
                fixed3 light = normalize(fixed3(sin(_LightAngle), cos(_LightAngle), -1));
                float  colorPower = clamp(_SpecularPower, 1.0, 2.0);

                [loop]
                for (int step=0; step<=MAX_STEPS; step++)
                {
                    // バウンディング外に出たら終了
                    if (Temp_Progress > Temp_Exit) break;

                    NextRaymarch(edge);

                    // 境界外から境界内へ侵入するレイを早期破棄しないため、境界内かつ SDF ヒット時だけ描画します。
                    if (Temp_Bound >= 0 && Temp_Value <= edge)
                    {
                        float3 lp = Temp_LocalPos;
                        float  charDepth = i.tmp3d.x;
                        float2 depthMapped = i.tmp3d.yz;

                        float depth    = -lp.z;
                        float progress = saturate(InverseLerp(0, max(1e-6,charDepth), depth));
                        progress = saturate(lerp(depthMapped.x, depthMapped.y, progress));

                        float3 depthRGB = tex2D(_DepthAlbedo, float2(progress,0.5)).rgb * _DepthColor.rgb;

                        // Face/Outline テクスチャは localPos.xy ベースで参照し、奥行き内部は DepthAlbedo へ切り替えます。
                        float2 scrollPos = lp.xy; scrollPos.y += _FaceTextureScrollSpeed * _Time.y;
                        float4 faceCol  = tex2D(_FaceTex, scrollPos * _FaceTex_ST.xy - _FaceTex_ST.zw) * (_Color * colorPower);
                        float2 outUVPos = lp.xy; outUVPos.y += _OutlineTextureScrollSpeed * _Time.y;
                        float4 outlineC = _OutlineColor * colorPower * tex2D(_OutlineTex, outUVPos * _OutlineTex_ST.xy - _OutlineTex_ST.zw);

                        if (progress < 0.95) { outlineWidth = 0; faceCol.rgb = depthRGB; }

                        float4 totalCol = BlendFaceAndOutline(sd, faceCol, outlineC, outlineWidth * scale);

                        // SDF から疑似ベベル法線を作り、Unlit に軽いライティング感を加えます。
                        float3 dxy = float3(0.5/_TextureWidth, 0.5/_TextureHeight, 0);
                        float3 n   = GetSurfaceNormal(i.atlas, edge, dxy);
                        float3 spec= GetSpecular(n, light);
                        float3 finalRGB = totalCol.rgb;
                        finalRGB += spec * totalCol.a;
                        finalRGB *= 1 - (dot(n, light) * _Diffuse);
                        finalRGB *= lerp(_Ambient, 1, n.z*n.z);

                        float4 clipPos = mul(UNITY_MATRIX_VP, mul(_Text3DLocalToWorld, float4(lp,1)));
                        o.depth = ComputeDepth(clipPos);
                        o.color = float4(finalRGB, 1);
                        return ValidateOutput(o, step);
                    }
                }

                clip(-1);
                return ValidateOutput(o, MAX_STEPS);
            }
            ENDCG
        }

        // ---------- Outline2 Pass（拡大再描画でアウトライン） ----------
        Pass
        {
            Name "Outline2"
            Stencil { Ref 2 Comp NotEqual }  // 元と同等のステンシル条件

            CGPROGRAM
            #pragma target 5.0
            #pragma vertex   Text3DSolidVertex
            #pragma fragment Text3DOutlineFragment
            #include "UnityCG.cginc"

            /// <summary>Text3D オブジェクトのローカルからワールドへの変換行列です。</summary>
            float4x4 _Text3DLocalToWorld;
            /// <summary>Text3D オブジェクトのワールドからローカルへの変換行列です。</summary>
            float4x4 _Text3DWorldToLocal;

            /// <summary>Outline2 パスで再利用する compute 出力頂点です。</summary>
            struct SolidGlyphVertex {
                /// <summary>ローカル空間の頂点位置です。</summary>
                float3 position; float3 normal; float4 color; float2 texcoord0; float2 texcoord1; float4 texcoord2;
                /// <summary>文字の UV、ローカル XY、ローカル Z の境界情報です。</summary>
                float4 boundariesUV; float4 boundariesLocal; float4 boundariesLocalZ;
            };

            /// <summary>Outline2 パスの頂点からフラグメントへ渡す情報です。</summary>
            struct VertexToFragment {
                /// <summary>クリップ空間位置、色、アトラス UV、ワールド位置です。</summary>
                float4 position:SV_POSITION; fixed4 color:COLOR; float2 atlas:TEXCOORD0; float4 worldPos:TEXCOORD1;
                /// <summary>文字境界と TMP 互換補助値です。</summary>
                float4 boundariesUV:TEXCOORD2; float4 boundariesLocal:TEXCOORD3; float4 boundariesLocalZ:TEXCOORD4;
                float4 tmp3d:TEXCOORD5; float2 tmp:TEXCOORD6;
            };
            /// <summary>compute shader が生成したソリッド頂点バッファです。</summary>
            StructuredBuffer<SolidGlyphVertex> _SolidGlyphVertexBuffer; StructuredBuffer<uint> _SolidGlyphIndexBuffer;

            /// <summary>TMP SDF アトラスとその Tiling/Offset です。</summary>
            sampler2D _MainTex; float4 _MainTex_ST;
            /// <summary>太字/通常文字の SDF しきい値と勾配スケールです。</summary>
            float _WeightBold, _WeightNormal, _GradientScale;
            /// <summary>フォントアトラス寸法です。</summary>
            float _TextureWidth, _TextureHeight;
            /// <summary>2 段目アウトライン色です。</summary>
            fixed4 _Outline2Color;
            /// <summary>2 段目アウトライン幅です。</summary>
            half _Outline2Width;

            /// <summary>値 x が a..b の範囲でどの位置にあるかを返します。</summary>
            float InverseLerp(float a,float b,float x){ return (x-a)/max(1e-6,(b-a)); }

            /// <summary>3D マスク座標から SDF アトラスをサンプリングします。</summary>
            float SampleSDF3D(float3 m, VertexToFragment i){ float2 uv; uv.x=saturate(lerp(i.boundariesUV.x,i.boundariesUV.x+i.boundariesUV.z,m.x));
                                                uv.y=saturate(lerp(i.boundariesUV.y,i.boundariesUV.y+i.boundariesUV.w,m.y));
                                                return tex2D(_MainTex, uv).a; }
            /// <summary>マスク座標が文字ボリューム内かを符号付き値で返します。</summary>
            float IsInBounds(float3 m){ float cx=-(abs(m.x-0.5)-0.5)+0.01; float cy=-(abs(m.y-0.5)-0.5)+0.01; float cz=-(abs(m.z-0.5)-0.5)+0.01; return min(0,min(cx,min(cy,cz))); }

            /// <summary>ローカル空間位置を文字内の 0..1 マスク空間へ変換します。</summary>
            float3 PositionToMask(float3 lp, VertexToFragment i){
                float3 m; m.y=InverseLerp(i.boundariesLocal.y, i.boundariesLocal.y+i.boundariesLocal.w, lp.y);
                float xOff=saturate(m.y)*i.boundariesLocalZ.z;
                m.x=InverseLerp(i.boundariesLocal.x, i.boundariesLocal.x+i.boundariesLocal.z, lp.x-xOff);
                m.z=InverseLerp(i.boundariesLocalZ.x, i.boundariesLocalZ.y, lp.z);
                return m;
            }
            /// <summary>SV_Depth 用の深度値をプラットフォーム差を吸収して返します。</summary>
            float ComputeDepth(float4 cp){
                #if defined(SHADER_TARGET_GLSL) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
                    return ((cp.z/cp.w)+1.0)*0.5;
                #else
                    return cp.z/cp.w;
                #endif
            }

            /// <summary>Outline2 レイマーチ中の視線、位置、境界判定、SDF 値、進行距離、入力情報です。</summary>
            float3 V, L0, LP; float B, Val, Prog; VertexToFragment Inp;

            /// <summary>Outline2 用のレイ開始位置と方向をローカル空間で初期化します。</summary>
            void InitRM(VertexToFragment i){
                float3 vws = normalize(i.worldPos.xyz - _WorldSpaceCameraPos.xyz);
                vws = lerp(vws, normalize(mul((float3x3)unity_CameraToWorld, float3(0,0,1))), unity_OrthoParams.w);
                L0 = mul(_Text3DWorldToLocal, float4(i.worldPos.xyz,1)).xyz;
                V  = mul((float3x3)_Text3DWorldToLocal, vws);
                Prog = 0; Inp = i;
            }
            /// <summary>Outline2 用に SDF 値から距離を見積もり、レイを 1 ステップ進めます。</summary>
            void StepRM(float edge){
                LP = L0 + V * Prog;
                float3 m = PositionToMask(LP, Inp);
                B   = IsInBounds(m);
                Val = 1.0 - SampleSDF3D(saturate(m), Inp);
                float2 vxy = V.xy; float lenXY=max(1e-6,length(vxy));
                Prog += max(length(V) * ((Val-edge)/max(1e-6,lenXY)), 0.01);
            }

            /// <summary>Outline2 パス用の頂点シェーダーです。</summary>
            VertexToFragment Text3DSolidVertex(uint vid:SV_VertexID, uint iid:SV_InstanceID){
                VertexToFragment o; uint idx = _SolidGlyphIndexBuffer[vid]; SolidGlyphVertex v = _SolidGlyphVertexBuffer[idx];
                float4 wp = mul(_Text3DLocalToWorld, float4(v.position,1));
                o.worldPos = wp; o.position = mul(UNITY_MATRIX_VP, wp);
                o.color=v.color; o.atlas=v.texcoord0; o.boundariesUV=v.boundariesUV; o.boundariesLocal=v.boundariesLocal;
                o.boundariesLocalZ=v.boundariesLocalZ; o.tmp3d=v.texcoord2; o.tmp=v.texcoord1; return o;
            }

            /// <summary>Outline2 パスの色と深度を返す出力構造体です。</summary>
            struct OUT{ fixed4 color:SV_Target; float depth:SV_Depth; };

            /// <summary>
            /// 2 段目アウトライン用に、太めの SDF しきい値で短いレイマーチを行います。
            /// </summary>
            OUT Text3DOutlineFragment(VertexToFragment i)
            {
                OUT o; o.color=0; o.depth=0;
                half bold = step(i.tmp.y, 0);
                float edge = lerp(_WeightNormal, _WeightBold, bold) + _Outline2Width;

                InitRM(i);
                [loop] for(int s=0;s<=8;s++){
                    StepRM(edge);
                    if (B >= 0 && Val <= edge){
                        float4 cp = mul(UNITY_MATRIX_VP, mul(_Text3DLocalToWorld, float4(LP,1)));
                        o.depth = ComputeDepth(cp);
                        o.color = _Outline2Color;
                        return o;
                    }
                }
                clip(-1); return o;
            }
            ENDCG
        }
    }

    // ===== SubShader 2: 透明系フォールバック（元を維持 / 変更なしでもOK）=====
    // ※必要なければ削除可。ここでは元の構造をだいたい踏襲しています。
	SubShader
	{
	    Tags
	    {
	        "Queue" = "Transparent"
	        "IgnoreProjector" = "True"
	        "RenderType" = "Transparent"
	    }

		Lighting Off
		Fog { Mode Off }

		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			Stencil
			{
				Ref 2
				Comp Always
				Pass Replace
			}

			CGPROGRAM
			#pragma target 2.0
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			#pragma multi_compile __ OUTLINE_ON

			#include "Assets/TMP3D/Runtime/Shaders/Lib/TMP3D_Common.cginc"

			#define MAX_STEPS 8

            /// <summary>フォールバックパスが返す色と深度です。</summary>
			struct FragmentOutput
			{
				/// <summary>最終的なピクセル色です。</summary>
				fixed4 color : SV_Target;
				/// <summary>深度バッファへ書く値です。</summary>
				half depth : SV_Depth;
			};

            /// <summary>フォールバックパス用に、プラットフォーム差を吸収した深度値を返します。</summary>
			half compute_depth(fixed4 clippos)
			{
				#if defined(SHADER_TARGET_GLSL) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
				return ((clippos.z / clippos.w) + 1.0) * 0.5;
				#else
				return clippos.z / clippos.w;
				#endif
			}

            /// <summary>TMP3D の共通ジオメトリ処理を呼び出すフォールバック用ジオメトリシェーダーです。</summary>
			[maxvertexcount(8)]
			void TMP3D_GEOM_VARIANT(triangle tmp3d_v2g input[3], inout TriangleStream<tmp3d_g2f> triStream)
			{
				TMP3D_GEOM(input, triStream);
			}

            /// <summary>TMP 互換のアウトラインソフトネスです。</summary>
			half _OutlineSoftness;
            /// <summary>疑似ベベルの幅です。</summary>
			half _BevelWidth;
            /// <summary>疑似ベベルの中心オフセットです。</summary>
			half _BevelOffset;
            /// <summary>疑似ベベルの強度です。</summary>
			int _Bevel;
            /// <summary>疑似ベベルの丸みです。</summary>
			half _BevelRoundness;
            /// <summary>疑似ベベルの山を削る量です。</summary>
			half _BevelClamp;
            /// <summary>疑似拡散光の強さです。</summary>
			half _Diffuse;
            /// <summary>環境光の下限量です。</summary>
			half _Ambient;
            /// <summary>疑似ライトの角度です。</summary>
			half _LightAngle;
            /// <summary>スペキュラーの鋭さです。</summary>
			half _Reflectivity;
            /// <summary>スペキュラーの強度です。</summary>
			half _SpecularPower;
            /// <summary>スペキュラーの色です。</summary>
			fixed4 _SpecularColor;
            /// <summary>アウトライン用テクスチャです。</summary>
			sampler2D _OutlineTex;
            /// <summary>メインテクスチャの Tiling/Offset です。</summary>
			fixed4 _MainTex_ST;
            /// <summary>旧実装互換のベベル色です。</summary>
			fixed4 _BevelColor;
            /// <summary>奥行き・アウトライン用の色です。</summary>
			fixed4 _DepthColor;
            /// <summary>表面テクスチャのスクロール速度です。</summary>
			half _FaceTextureScrollSpeed;
            /// <summary>アウトラインテクスチャのスクロール速度です。</summary>
			half _OutlineTextureScrollSpeed;

            /// <summary>フォールバック頂点シェーダーの入力です。</summary>
			struct appdata
			{
			    float4 vertex : POSITION;     /// <summary>頂点座標</summary>
			    float2 uv     : TEXCOORD0;    /// <summary>UV（SDF 用なので高精度必須）</summary>
			};

            /// <summary>フォールバック頂点シェーダーからフラグメントシェーダーへ渡す値です。</summary>
			struct VertexToFragment
			{
			    float4 pos   : SV_POSITION;
			    float2 uv    : TEXCOORD0;     /// <summary>アウトライン用 UV</summary>
			    float2 atlas : TEXCOORD1;     /// <summary>フォント SDF 用 UV</summary>
			};

            /// <summary>フォールバック用に通常メッシュ頂点をクリップ空間へ変換します。</summary>
			VertexToFragment vert(appdata v)
			{
				VertexToFragment o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _OutlineTex);
				o.atlas = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			/// <summary>atlas から元の UV に戻す</summary>
            fixed2 UnpackUV(half uv)
            {
                fixed2 o;
                o.x = floor(uv / 4096);
                o.y = uv - 4096 * o.x;
                return o * 0.001953125; // 1/512
            }

            /// <summary>
            /// SDF の高さフィールドから 2D ベベル法線を計算する。
            /// h.xyzw: 四隅のサンプル値
            /// </summary>
            fixed3 GetSurfaceNormal(fixed4 h, half bias)
            {
                // ベベルオフセット＋バイアス分を加える
                h += bias + _BevelOffset;

                // 線幅：アウトライン幅＋ベベル幅 を最低値 .01 以上に
                half bevelWidth = max(0.01, _OutlineWidth + _BevelWidth);

                // -0.5..0.5 に正規化して輪郭追跡
                h -= 0.5;
                h /= bevelWidth;
                h = saturate(h + 0.5);

                // 0..1→山型になるように
                h = 1 - abs(h * 2 - 1);

                // 丸み付け
                h = lerp(h, sin(h * UNITY_PI/2), _BevelRoundness);
                // clamp で上下を削る
                h = min(h, 1 - _BevelClamp);

                // 高さをスケール
                h *= _Bevel * bevelWidth * _GradientScale * -2.0;

                // 2 辺で微法線を作成しクロス
                fixed3 va = normalize(fixed3(1, 0, h.y - h.x));
                fixed3 vb = normalize(fixed3(0,-1, h.w - h.z));
                return cross(va, vb);
            }

            /// <summary>
            /// UV から SDF をサンプリングし、GetSurfaceNormal(fixed4, bias) に渡す
            /// </summary>
            fixed3 GetSurfaceNormal(fixed2 uv, half bias, fixed3 delta)
            {
                // 周辺 4 点をサンプリング（αチャンネル距離場）
                fixed4 h = fixed4(
                    tex2D(_MainTex, uv - delta.xz).a,
                    tex2D(_MainTex, uv + delta.xz).a,
                    tex2D(_MainTex, uv - delta.zy).a,
                    tex2D(_MainTex, uv + delta.zy).a
                );
                return GetSurfaceNormal(h, bias);
            }

            /// <summary>Blinn-Phong 風スペキュラー</summary>
            fixed3 GetSpecular(fixed3 n, fixed3 l)
            {
                half spec = pow(max(0, dot(n, l)), _Reflectivity);
                return _SpecularColor.rgb * spec * _SpecularPower;
            }

            /// <summary>SDF 距離に応じて表面色とアウトライン色を合成します。</summary>
			fixed4 BlendFaceAndOutline(half d, fixed4 faceColor, fixed4 outlineColor, half outline)
			{
				half faceAlpha = 1-saturate((d - outline * 0.5));
				half outlineAlpha = saturate((d + outline * 0.5)) * sqrt(min(1.0, outline));

				faceColor.rgb *= faceColor.a;
				outlineColor.rgb *= outlineColor.a;

				faceColor = lerp(faceColor, outlineColor, outlineAlpha);

				faceColor *= faceAlpha;

				return faceColor;
			}


            /// <summary>フォールバック表示用に SDF の表面、アウトライン、ベベル風ライティングを計算します。</summary>
			fixed4 frag(VertexToFragment input) : SV_Target
			{
			    UNITY_SETUP_INSTANCE_ID(input);

			    // SDF サンプルは half/float
			    float bold = 0.0;
			    float edge = lerp(_WeightNormal, _WeightBold, bold);

			    // SDF 値
			    half s = tex2D(_MainTex, input.atlas).a;

			    // 3) 派生値の計算：UV 全体の勾配からスケールを出す（より頑健）
			    float2 dx = ddx(input.atlas);
			    float2 dy = ddy(input.atlas);
			    float grad2 = max(dot(dx, dx), dot(dy, dy));
			    float scale = rsqrt(max(1e-8, grad2)) * _GradientScale;

			    float weight = lerp(_WeightNormal, _WeightBold, bold) / 4.0;
			    weight = (weight) * _ScaleRatioA * 0.5;
			    float bias = (.5 - weight) + (.5 / scale);
			    float sd   = (bias - s) * scale;

			    float outlineWidth = _OutlineWidth * 0.5;

			    float2 scrollPos     = input.atlas; scrollPos.y     += _FaceTextureScrollSpeed * _Time.y;
			    float2 outLineScroll = input.atlas; outLineScroll.y += _OutlineTextureScrollSpeed * _Time.y;

			    float  colorPower = clamp(_SpecularPower, 1.0, 2.0);
			    float4 face       = tex2D(_FaceTex, scrollPos * _FaceTex_ST.xy - _FaceTex_ST.zw) * (_Color * colorPower);
			    float4 outlineCol = _OutlineColor * colorPower * tex2D(_OutlineTex, outLineScroll * _OutlineTex_ST.xy - _OutlineTex_ST.zw);

			    float4 totalCol = BlendFaceAndOutline(sd, face, outlineCol, outlineWidth * scale);

			    // 一部端末で clip が最適化される問題の回避
			    if (totalCol.a < 0.01) discard;

			    // （以下は元コードの計算を half/float 化）
			    float3 dxy = float3(0.5/_TextureWidth, 0.5/_TextureHeight, 0);
			    float3 n   = normalize(GetSurfaceNormal(input.atlas, edge, dxy));
			    float3 light = normalize(float3(sin(_LightAngle), cos(_LightAngle), -1));
			    float3 spec  = GetSpecular(n, light);
			    float3 finalColor = totalCol.rgb;

			    finalColor += spec * totalCol.a;
			    finalColor *= 1 - (dot(n, light) * _Diffuse);
			    finalColor *= lerp(_Ambient, 1, n.z * n.z);

			    return float4(finalColor, totalCol.a);
			}

			ENDCG
		}
	    // ----------- ① アウトラインパス（奥行き風）-----------
	    Pass
	    {
	        Name "Outline"
	            Stencil
	            {
	                Ref 2
	                Comp NotEqual
	            }

	        Cull Back     // 背面カリング → アウトラインを後ろに押し出すイメージ
	        ZWrite Off

	        CGPROGRAM
	        #pragma vertex vert
	        #pragma fragment fragOutline
	        #include "UnityCG.cginc"

	        sampler2D _MainTex;
	        fixed4 _MainTex_ST;
	        fixed4 _DepthColor;
	        half _Outline2Width;

            /// <summary>フォールバックのアウトラインパスに渡す頂点入力です。</summary>
	        struct appdata
	        {
                /// <summary>頂点座標です。</summary>
	            fixed4 vertex : POSITION;
                /// <summary>フォントアトラス UV です。</summary>
	            fixed2 uv : TEXCOORD0;
	        };

            /// <summary>フォールバックのアウトラインパスで補間する値です。</summary>
	        struct VertexToFragment
	        {
                /// <summary>クリップ空間位置です。</summary>
	            fixed4 pos : SV_POSITION;
                /// <summary>元 UV です。</summary>
	            fixed2 uv  : TEXCOORD0;
                /// <summary>フォントアトラス UV です。</summary>
				fixed2 atlas : TEXCOORD1;
	        };

            /// <summary>アウトライン用に頂点をカメラ奥と XY 方向へ押し広げます。</summary>
			VertexToFragment vert(appdata v)
			{
			    VertexToFragment o;

			    fixed3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

			    // カメラ方向へ少し奥に押す
			    fixed3 cameraDir = normalize(UnityWorldSpaceViewDir(worldPos));
			    worldPos -= cameraDir * 500;

			    // XY方向へスケーリング
			    fixed2 direction = normalize(v.vertex.xy);
			    fixed2 offset = direction * _Outline2Width;

			    fixed4 displaced = mul(unity_WorldToObject, fixed4(worldPos, 1.1));
			    displaced.xy += offset;
				o.atlas = TRANSFORM_TEX(v.uv, _MainTex);

			    o.pos = UnityObjectToClipPos(displaced);
			    o.uv = v.uv;
			    return o;
			}
            /// <summary>SDF アトラスのアルファでアウトライン形状を切り抜き、奥行き色を返します。</summary>
	        fixed4 fragOutline(VertexToFragment input) : SV_Target
	        {
        		clip(tex2D(_MainTex, input.atlas).a - 0.33);
        		fixed4 col = _DepthColor * 0.1;
        		col.a = 1;
	            return col;
	        }
	        ENDCG
	    }
	}
    CustomEditor "NariEffect.Text3D.Editor.Text3DMaterialInspector"
}
