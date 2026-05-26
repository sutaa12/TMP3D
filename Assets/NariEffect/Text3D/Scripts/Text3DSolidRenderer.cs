using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;
using TMPro;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NariEffect.Text3D
{
    /// <summary>
    /// TextMeshPro の各文字クワッドを GPU で厚み付きメッシュへ変換し、プロシージャル描画します。
    /// </summary>
    /// <remarks>
    /// TMP3D IKaroon の MIT ライセンス実装をもとに、compute shader で 1 文字あたり 16 頂点・36 インデックスのソリッド形状を作ります。
    /// https://github.com/Ikaroon/TMP3D
    /// </remarks>
    [MovedFrom(true, sourceNamespace: "NariEffect.Script", sourceClassName: "TMP3D_ComputeRenderer")]
    [ExecuteInEditMode, RequireComponent(typeof(TextMeshPro))]
    public class Text3DSolidRenderer : MonoBehaviour
    {
        /// <summary>
        /// 通常描画に使う Text3D ソリッドシェーダー名です。
        /// </summary>
        const string SolidUnlitShaderName = "NariEffect/Text3D/SolidUnlit";

        /// <summary>
        /// 内部処理の段階表示に使う Explainer シェーダー名です。
        /// </summary>
        const string SolidUnlitExplainerShaderName = "NariEffect/Text3D/SolidUnlitExplainer";

        /// <summary>
        /// Explainer 表示モードを保持するシェーダープロパティ ID です。
        /// </summary>
        static readonly int GuideModeId = Shader.PropertyToID("_GuideMode");

        /// <summary>
        /// 2 段目アウトライン幅のシェーダープロパティ ID です。
        /// </summary>
        static readonly int Outline2WidthId = Shader.PropertyToID("_Outline2Width");

        /// <summary>
        /// 2 段目アウトライン色のシェーダープロパティ ID です。
        /// </summary>
        static readonly int Outline2ColorId = Shader.PropertyToID("_Outline2Color");

        #region Serializable Debug Structures
        /// <summary>
        /// compute shader へ渡す入力頂点をインスペクターで確認するためのコピー構造体です。
        /// </summary>
        [System.Serializable]
        public struct SourceGlyphVertexInspection
        {
            /// <summary>
            /// TMP メッシュ上のローカル座標です。
            /// </summary>
            public Vector3 pos;

            /// <summary>
            /// フォント SDF アトラスを参照する UV です。
            /// </summary>
            public Vector2 uv0;

            /// <summary>
            /// 厚みと奥行き色範囲を詰めたデータです。
            /// </summary>
            public Vector4 uv3;

            /// <summary>
            /// TMP 頂点カラーです。
            /// </summary>
            public Color32 col;
            
            /// <summary>
            /// GPU 入力用頂点からインスペクター表示用データを作成します。
            /// </summary>
            /// <param name="source">コピー元の GPU 入力用頂点。</param>
            public SourceGlyphVertexInspection(SourceGlyphVertex source)
            {
                pos = source.pos;
                uv0 = source.uv0;
                uv3 = source.uv3;
                col = source.col;
            }
        }
        
        /// <summary>
        /// compute shader が生成したソリッド頂点をインスペクターで確認するためのコピー構造体です。
        /// </summary>
        [System.Serializable]
        public struct SolidGlyphVertexInspection
        {
            /// <summary>
            /// 生成後のローカル座標です。
            /// </summary>
            public Vector3 position;

            /// <summary>
            /// 面ごとのローカル法線です。
            /// </summary>
            public Vector3 normal;

            /// <summary>
            /// 0..1 に展開された頂点カラーです。
            /// </summary>
            public Vector4 color;

            /// <summary>
            /// フォント SDF アトラスを参照する UV です。
            /// </summary>
            public Vector2 texcoord0;

            /// <summary>
            /// TMP 互換用の補助値です。y はボールド判定フラグとして使います。
            /// </summary>
            public Vector2 texcoord1;

            /// <summary>
            /// x が厚み、y/z が奥行き色範囲のデータです。
            /// </summary>
            public Vector4 texcoord2;

            /// <summary>
            /// アトラス UV 内の文字境界です。
            /// </summary>
            public Vector4 boundariesUV;

            /// <summary>
            /// ローカル XY 平面上の文字境界です。
            /// </summary>
            public Vector4 boundariesLocal;

            /// <summary>
            /// ローカル Z 範囲と斜体補正量です。
            /// </summary>
            public Vector4 boundariesLocalZ;
            
            /// <summary>
            /// GPU 出力用頂点からインスペクター表示用データを作成します。
            /// </summary>
            /// <param name="source">コピー元の GPU 出力用頂点。</param>
            public SolidGlyphVertexInspection(SolidGlyphVertex source)
            {
                position = source.position;
                normal = source.normal;
                color = source.color;
                texcoord0 = source.texcoord0;
                texcoord1 = source.texcoord1;
                texcoord2 = source.texcoord2;
                boundariesUV = source.boundariesUV;
                boundariesLocal = source.boundariesLocal;
                boundariesLocalZ = source.boundariesLocalZ;
            }
        }
        #endregion

        /// <summary>
        /// このレンダラーが監視している TextMeshPro コンポーネントです。
        /// </summary>
        public TextMeshPro TargetText => targetText;

        /// <summary>
        /// 旧 API 互換用の TextMeshPro 参照です。新規コードでは <see cref="TargetText"/> を使います。
        /// </summary>
        [Obsolete("Use TargetText instead.")]
        public TextMeshPro TMP => TargetText;

        /// <summary>
        /// メッシュ更新を監視し、元の TMP 頂点を取得する対象です。
        /// </summary>
        [FormerlySerializedAs("m_tmp")]
        [SerializeField, HideInInspector]
        TextMeshPro targetText;

        /// <summary>
        /// 新しい文字に割り当てる既定の押し出し厚みです。
        /// </summary>
        [FormerlySerializedAs("m_defaultDepth")]
        [SerializeField]
        float defaultThickness = 1;

        /// <summary>
        /// 文字クワッドからソリッド頂点・インデックスを生成する compute shader です。
        /// </summary>
        [FormerlySerializedAs("m_vertexGeneratorCompute")]
        [SerializeField]
        ComputeShader geometryBuilderCompute;

        /// <summary>
        /// プロシージャル描画用マテリアルのテンプレートです。未指定なら標準 SolidUnlit シェーダーから作ります。
        /// </summary>
        [Tooltip("Optional procedural render material template. Leave empty to use NariEffect/Text3D/SolidUnlit.")]
        [SerializeField]
        Material solidMaterialTemplate;

        /// <summary>
        /// GPU 生成パスを使うかどうかです。無効時は TMP メッシュの UV2 に奥行きデータだけを書き込みます。
        /// </summary>
        [FormerlySerializedAs("m_useComputeShader")]
        [SerializeField]
        bool useGpuBuilder = false;

        /// <summary>
        /// compute shader の 1 グループあたりスレッド数です。実際の値はカーネルから取得できる場合それを優先します。
        /// </summary>
        [FormerlySerializedAs("m_threadGroupSize")]
        [SerializeField]
        int kernelThreadGroupSize = 64;

        /// <summary>
        /// 入力頂点、生成頂点、インデックスをインスペクターへ読み戻すかどうかです。
        /// </summary>
        [Header("Inspection Settings")]
        [FormerlySerializedAs("m_enableDebug")]
        [SerializeField]
        bool enableInspection = false;
        
        /// <summary>
        /// インスペクターで詳しく確認する文字クワッドの番号です。
        /// </summary>
        [FormerlySerializedAs("m_debugQuadIndex")]
        [SerializeField, Range(0, 10)]
        int inspectionGlyphIndex = 0;
        
        /// <summary>
        /// Gizmo で強調表示する生成頂点の番号です。
        /// </summary>
        [FormerlySerializedAs("m_debugVertexIndex")]
        [SerializeField, Range(0, 15)]
        int inspectionVertexIndex = 0;
        
        /// <summary>
        /// 現在処理対象になっている可視文字クワッド数です。
        /// </summary>
        [Header("Inspection Info")]
        [FormerlySerializedAs("m_debugQuadCount")]
        [SerializeField]
        int inspectedGlyphCount = 0;
        
        /// <summary>
        /// compute shader が生成する総頂点数です。
        /// </summary>
        [FormerlySerializedAs("m_debugTotalVertices")]
        [SerializeField]
        int inspectedVertexCount = 0;
        
        /// <summary>
        /// compute shader が生成する総インデックス数です。
        /// </summary>
        [FormerlySerializedAs("m_debugTotalIndices")]
        [SerializeField]
        int inspectedIndexCount = 0;
        
        /// <summary>
        /// 選択中クワッドの入力 4 頂点を表示するリストです。
        /// </summary>
        [Header("Source Vertex Inspection (4 vertices per quad)")]
        [FormerlySerializedAs("m_debugInputVertices")]
        [SerializeField]
        List<SourceGlyphVertexInspection> inspectedSourceVertices = new List<SourceGlyphVertexInspection>();
        
        /// <summary>
        /// 選択中クワッドから生成された最大 16 頂点を表示するリストです。
        /// </summary>
        [Header("Solid Vertex Inspection (16 vertices per quad)")]
        [FormerlySerializedAs("m_debugOutputVertices")]
        [SerializeField]
        List<SolidGlyphVertexInspection> inspectedSolidVertices = new List<SolidGlyphVertexInspection>();
        
        /// <summary>
        /// 選択中クワッドから生成された最大 36 インデックスを表示するリストです。
        /// </summary>
        [Header("Solid Indices Inspection")]
        [FormerlySerializedAs("m_debugOutputIndices")]
        [SerializeField]
        List<uint> inspectedSolidIndices = new List<uint>();

        /// <summary>
        /// 文字インデックスごとの厚みと奥行き範囲です。
        /// </summary>
        readonly List<Text3DGlyphThickness> glyphThicknesses = new List<Text3DGlyphThickness>();

        /// <summary>
        /// フォールバック時に UV2 を読み書きするための再利用リストです。
        /// </summary>
        readonly List<Vector4> cachedUv2 = new List<Vector4>();

        /// <summary>
        /// TMP メッシュから取り出した入力文字クワッドの頂点列です。
        /// </summary>
        readonly List<SourceGlyphVertex> sourceGlyphQuads = new List<SourceGlyphVertex>();

        /// <summary>
        /// compute shader が出力するソリッド頂点バッファです。
        /// </summary>
        GraphicsBuffer solidVertexBuffer;

        /// <summary>
        /// compute shader が出力するインデックスバッファです。
        /// </summary>
        GraphicsBuffer solidIndexBuffer;

        /// <summary>
        /// compute shader へ渡す元文字クワッドの入力バッファです。
        /// </summary>
        GraphicsBuffer sourceQuadBuffer;

        /// <summary>
        /// 現在確保しているソリッド頂点バッファの要素容量です。
        /// </summary>
        int solidVertexBufferCapacity;

        /// <summary>
        /// 現在確保しているインデックスバッファの要素容量です。
        /// </summary>
        int solidIndexBufferCapacity;

        /// <summary>
        /// 現在確保している入力クワッドバッファの要素容量です。
        /// </summary>
        int sourceQuadBufferCapacity;
        
        /// <summary>
        /// プロシージャル描画に使う実行時生成マテリアルです。
        /// </summary>
        Material solidMaterial;

        /// <summary>
        /// 現在の描画に使うソリッド頂点数です。
        /// </summary>
        int solidVertexCount;

        /// <summary>
        /// 現在の描画に使うインデックス数です。
        /// </summary>
        int solidIndexCount;

        /// <summary>
        /// 現在処理する可視文字クワッド数です。
        /// </summary>
        int glyphQuadCount;

        /// <summary>
        /// BuildText3DGeometry カーネルのインデックスです。未解決時は -1 です。
        /// </summary>
        int geometryKernel = -1;

        /// <summary>
        /// TMP メッシュや厚み設定の変更により、GPU バッファの再生成が必要かどうかです。
        /// </summary>
        bool needsGeometryRefresh;

        /// <summary>
        /// 同一フレームで TextMeshPro の変更通知を重複処理しないためのフレーム番号です。
        /// </summary>
        int lastRefreshFrame = -1;

        /// <summary>
        /// compute shader へ渡す TMP 由来の 1 頂点データです。
        /// </summary>
        /// <remarks>
        /// フィールド順と型は BuildText3DGeometry.compute の SourceGlyphVertex と一致させます。
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        public struct SourceGlyphVertex
        {
            /// <summary>
            /// TMP メッシュ上のローカル座標です。
            /// </summary>
            public Vector3 pos;

            /// <summary>
            /// フォント SDF アトラスを参照する UV です。
            /// </summary>
            public Vector2 uv0;

            /// <summary>
            /// x に厚み、y/z に奥行き色範囲を格納します。
            /// </summary>
            public Vector4 uv3;

            /// <summary>
            /// TMP 頂点カラーです。
            /// </summary>
            public Color32 col;
        }

        /// <summary>
        /// compute shader が出力し、SolidUnlit シェーダーが読むソリッド頂点データです。
        /// </summary>
        /// <remarks>
        /// フィールド順と型は BuildText3DGeometry.compute と Text3DSolidUnlit.shader の SolidGlyphVertex と一致させます。
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        public struct SolidGlyphVertex
        {
            /// <summary>
            /// 押し出し後のローカル座標です。
            /// </summary>
            public Vector3 position;

            /// <summary>
            /// 面ごとのローカル法線です。
            /// </summary>
            public Vector3 normal;

            /// <summary>
            /// 0..1 の頂点カラーです。
            /// </summary>
            public Vector4 color;

            /// <summary>
            /// フォント SDF アトラスを参照する UV です。
            /// </summary>
            public Vector2 texcoord0;

            /// <summary>
            /// TMP 互換用補助値です。y はボールド判定フラグです。
            /// </summary>
            public Vector2 texcoord1;

            /// <summary>
            /// x が厚み、y/z が奥行き色範囲のデータです。
            /// </summary>
            public Vector4 texcoord2;

            /// <summary>
            /// アトラス UV 内での文字境界です。
            /// </summary>
            public Vector4 boundariesUV;

            /// <summary>
            /// ローカル XY 平面上での文字境界です。
            /// </summary>
            public Vector4 boundariesLocal;

            /// <summary>
            /// ローカル Z 範囲と斜体補正量です。
            /// </summary>
            public Vector4 boundariesLocalZ;
        }

        /// <summary>
        /// TextMeshPro の変更通知を購読し、初回のメッシュ情報と GPU カーネルを準備します。
        /// </summary>
        void OnEnable()
        {
            targetText = GetComponent<TextMeshPro>();
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(HandleTextChanged);
            FindGeometryKernel();
            if (targetText != null)
            {
                targetText.ForceMeshUpdate();
                HandleTextChanged(targetText);
            }
        }

        /// <summary>
        /// 変更通知の購読解除と GPU リソースの解放を行います。
        /// </summary>
        void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(HandleTextChanged);
            ReleaseGpuBuffers();
            if (solidMaterial != null)
            {
                DestroySolidMaterial();
            }
        }

        /// <summary>
        /// プロシージャル描画用に生成した一時マテリアルを安全に破棄します。
        /// </summary>
        void DestroySolidMaterial()
        {
            if (solidMaterial == null)
                return;

#if UNITY_EDITOR
            DestroyImmediate(solidMaterial);
#else
            Destroy(solidMaterial);
#endif
            solidMaterial = null;
        }

        /// <summary>
        /// geometryBuilderCompute から BuildText3DGeometry カーネルを探します。
        /// </summary>
        void FindGeometryKernel()
        {
            TryFindGeometryKernel();
        }

        /// <summary>
        /// compute shader のカーネル番号を必要なときだけ解決します。
        /// </summary>
        /// <returns>カーネルを使用できる場合は true。</returns>
        bool TryFindGeometryKernel()
        {
            if (geometryKernel >= 0)
                return true;

            if (geometryBuilderCompute == null)
            {
                geometryKernel = -1;
                return false;
            }

            try
            {
                geometryKernel = geometryBuilderCompute.FindKernel("BuildText3DGeometry");
            }
            catch
            {
                geometryKernel = -1;
            }

            return geometryKernel >= 0;
        }

        /// <summary>
        /// 確保済みの GPU バッファをすべて解放し、容量情報を初期化します。
        /// </summary>
        void ReleaseGpuBuffers()
        {
            solidVertexBuffer?.Release();
            solidIndexBuffer?.Release();
            sourceQuadBuffer?.Release();
            
            solidVertexBuffer = null;
            solidIndexBuffer = null;
            sourceQuadBuffer = null;
            solidVertexBufferCapacity = 0;
            solidIndexBufferCapacity = 0;
            sourceQuadBufferCapacity = 0;
        }

        /// <summary>
        /// 描画数、検査情報、必要に応じた GPU バッファを初期状態へ戻します。
        /// </summary>
        /// <param name="releaseBuffers">true の場合は GPU バッファも解放します。</param>
        void ResetGpuState(bool releaseBuffers)
        {
            glyphQuadCount = 0;
            solidVertexCount = 0;
            solidIndexCount = 0;
            inspectedGlyphCount = 0;
            inspectedVertexCount = 0;
            inspectedIndexCount = 0;
            inspectedSourceVertices.Clear();
            inspectedSolidVertices.Clear();
            inspectedSolidIndices.Clear();

            if (releaseBuffers)
            {
                ReleaseGpuBuffers();
            }
        }

        /// <summary>
        /// TextMeshPro のテキスト変更通知を受け、文字数同期後にジオメトリ更新を予約します。
        /// </summary>
        /// <param name="obj">変更された TextMeshPro オブジェクト。</param>
        void HandleTextChanged(UnityEngine.Object obj)
        {
            if (obj != targetText || targetText == null)
                return;

            if (!SyncGlyphThicknesses())
            {
                ResetGpuState(true);
                return;
            }

            RequestGeometryRefresh();
        }

        /// <summary>
        /// TextMeshPro の現在の文字数に合わせて、文字別の厚みデータ数を増減します。
        /// </summary>
        /// <returns>TextMeshPro の textInfo を参照できる場合は true。</returns>
        bool SyncGlyphThicknesses()
        {
            if (targetText == null || targetText.textInfo == null)
                return false;

            for (int i = glyphThicknesses.Count - 1; i >= targetText.textInfo.characterCount; i--)
            {
                glyphThicknesses.RemoveAt(i);
            }

            for (int i = glyphThicknesses.Count; i < targetText.textInfo.characterCount; i++)
            {
                glyphThicknesses.Add(new Text3DGlyphThickness(defaultThickness, new Vector2(0, 1)));
            }

            return true;
        }

        /// <summary>
        /// GPU パスが使える場合は再生成を予約し、使えない場合は UV2 へのフォールバック書き込みを行います。
        /// </summary>
        void RequestGeometryRefresh()
        {
            if (CanUseGpuBuilder())
            {
                needsGeometryRefresh = true;
            }
            else
            {
                needsGeometryRefresh = false;
                ResetGpuState(true);
                WriteFallbackDepthData();
            }
        }

        /// <summary>
        /// TMP の遅延メッシュ更新を拾い、必要なフレームで GPU バッファを再構築します。
        /// </summary>
        void LateUpdate()
        {
            if (targetText == null)
                return;

            if (targetText.havePropertiesChanged && lastRefreshFrame != Time.frameCount)
            {
                lastRefreshFrame = Time.frameCount;
                if (SyncGlyphThicknesses())
                {
                    RequestGeometryRefresh();
                }
            }

            if (!needsGeometryRefresh)
                return;

            if (CanUseGpuBuilder())
            {
                RebuildGpuBuffers();
            }
            else
            {
                needsGeometryRefresh = false;
                ResetGpuState(true);
                WriteFallbackDepthData();
            }
        }

        /// <summary>
        /// compute shader、対応シェーダー、環境サポートがすべて揃っているかを確認します。
        /// </summary>
        /// <returns>GPU 生成パスで描画できる場合は true。</returns>
        bool CanUseGpuBuilder()
        {
            if (!useGpuBuilder || !SystemInfo.supportsComputeShaders || geometryBuilderCompute == null)
                return false;

            if (!TryFindGeometryKernel())
                return false;

            var shader = ResolveSolidShader();
            return shader != null && shader.isSupported;
        }

        /// <summary>
        /// TMP メッシュの可視文字クワッドを収集し、compute shader 用の入力・出力バッファを作り直します。
        /// </summary>
        /// <remarks>
        /// アルゴリズムは「TMP の 4 頂点クワッドを文字ごとに集める」「1 クワッドにつき 16 頂点・36 インデックス分の容量を確保する」
        /// 「compute shader で押し出し形状へ変換する」という 3 段階です。
        /// </remarks>
        void RebuildGpuBuffers()
        {
            if (!CanUseGpuBuilder() || targetText == null || targetText.textInfo == null)
            {
                needsGeometryRefresh = false;
                ResetGpuState(true);
                WriteFallbackDepthData();
                return;
            }

            sourceGlyphQuads.Clear();

            // TMP はマテリアルごとに meshInfo を分けるため、materialReferenceIndex が一致する文字だけをそのメッシュから拾います。
            for (int mi = 0; mi < targetText.textInfo.meshInfo.Length; mi++)
            {
                var meshInfo = targetText.textInfo.meshInfo[mi];
                var mesh = meshInfo.mesh;
                if (mesh == null)
                    continue;

                var positions = mesh.vertices;
                var colors32 = mesh.colors32;
                var uv0 = mesh.uv;

                for (int ci = 0; ci < targetText.textInfo.characterCount; ci++)
                {
                    var ch = targetText.textInfo.characterInfo[ci];
                    if (!ch.isVisible)
                        continue;
                    if (ch.materialReferenceIndex != mi)
                        continue;

                    int vi = ch.vertexIndex;
                    if (vi < 0 || vi + 3 >= positions.Length)
                        continue;

                    var glyphThickness = ci < glyphThicknesses.Count ?
                        glyphThicknesses[ci] :
                        new Text3DGlyphThickness(defaultThickness, new Vector2(0, 1));

                    for (int k = 0; k < 4; k++)
                    {
                        int vertexIndex = vi + k;
                        // Mesh の vertices は既にオブジェクトのローカル座標なので、変換せず compute shader へ渡します。
                        var localPos = positions[vertexIndex];
                        
                        var v = new SourceGlyphVertex
                        {
                            pos = localPos,
                            uv0 = GetUvOrDefault(uv0, vertexIndex),
                            uv3 = new Vector4(
                                glyphThickness.Thickness,
                                glyphThickness.DepthRange.x,
                                glyphThickness.DepthRange.y,
                                0
                            ),
                            col = GetColorOrDefault(colors32, vertexIndex)
                        };
                        sourceGlyphQuads.Add(v);
                    }
                }
            }

            glyphQuadCount = sourceGlyphQuads.Count / 4;
            if (glyphQuadCount <= 0)
            {
                needsGeometryRefresh = false;
                ResetGpuState(true);
                return;
            }

            solidVertexCount = glyphQuadCount * 16;
            solidIndexCount = glyphQuadCount * 36;

            // 検査表示は CPU 側で見たい分だけ保持し、GPU の本処理とは切り離します。
            RefreshInspectionData(sourceGlyphQuads);

            EnsureGpuBuffers(sourceGlyphQuads);
            DispatchGeometryBuild();
            
            // compute 実行後のデータ読み戻しは重いので、検査モード時だけ行います。
            if (enableInspection)
            {
                ReadInspectionBuffers();
            }
            
            SyncSolidMaterial();
            needsGeometryRefresh = false;
        }

        /// <summary>
        /// インスペクターに表示する入力側の検査情報を更新します。
        /// </summary>
        /// <param name="inputQuads">TMP メッシュから集めた入力クワッド頂点列。</param>
        void RefreshInspectionData(List<SourceGlyphVertex> inputQuads)
        {
            inspectedGlyphCount = glyphQuadCount;
            inspectedVertexCount = solidVertexCount;
            inspectedIndexCount = solidIndexCount;
            
            if (!enableInspection)
            {
                inspectedSourceVertices.Clear();
                inspectedSolidVertices.Clear();
                inspectedSolidIndices.Clear();
                return;
            }
            
            // 選択中の 1 クワッドだけを切り出すことで、文字数が多い場合のインスペクター負荷を抑えます。
            inspectedSourceVertices.Clear();
            int startIdx = inspectionGlyphIndex * 4;
            int endIdx = Mathf.Min(startIdx + 4, inputQuads.Count);
            
            for (int i = startIdx; i < endIdx; i++)
            {
                inspectedSourceVertices.Add(new SourceGlyphVertexInspection(inputQuads[i]));
            }
        }

        /// <summary>
        /// compute shader が生成した頂点とインデックスを検査用に CPU へ読み戻します。
        /// </summary>
        /// <remarks>
        /// GPU からの GetData は同期コストが大きいため、<see cref="enableInspection"/> が有効な時だけ呼びます。
        /// </remarks>
        void ReadInspectionBuffers()
        {
            if (solidVertexBuffer == null || solidIndexBuffer == null)
                return;
            
            // 選択中クワッドに対応する 16 頂点だけを読み戻します。
            inspectedSolidVertices.Clear();
            if (glyphQuadCount > 0 && inspectionGlyphIndex < glyphQuadCount)
            {
                int vertexStart = inspectionGlyphIndex * 16;
                int vertexCount = Mathf.Min(16, solidVertexCount - vertexStart);
                
                if (vertexCount > 0)
                {
                    var vertices = new SolidGlyphVertex[vertexCount];
                    solidVertexBuffer.GetData(vertices, 0, vertexStart, vertexCount);
                    
                    for (int i = 0; i < vertexCount; i++)
                    {
                        inspectedSolidVertices.Add(new SolidGlyphVertexInspection(vertices[i]));
                    }
                }
            }
            
            // 同じクワッドに対応する 36 インデックスだけを読み戻します。
            inspectedSolidIndices.Clear();
            if (glyphQuadCount > 0 && inspectionGlyphIndex < glyphQuadCount)
            {
                int indexStart = inspectionGlyphIndex * 36;
                int indexCount = Mathf.Min(36, solidIndexCount - indexStart);
                
                if (indexCount > 0)
                {
                    var indices = new uint[indexCount];
                    solidIndexBuffer.GetData(indices, 0, indexStart, indexCount);
                    inspectedSolidIndices.AddRange(indices);
                }
            }
        }

        /// <summary>
        /// 入力、出力頂点、出力インデックスの GPU バッファ容量を確保し、入力頂点を転送します。
        /// </summary>
        /// <param name="inputQuads">compute shader へ渡す TMP 由来の入力頂点列。</param>
        void EnsureGpuBuffers(List<SourceGlyphVertex> inputQuads)
        {
            int inCount = Mathf.Max(1, inputQuads.Count);
            int inStride = Marshal.SizeOf<SourceGlyphVertex>();
            int outVtxStride = Marshal.SizeOf<SolidGlyphVertex>();

            EnsureBufferCapacity(ref sourceQuadBuffer, ref sourceQuadBufferCapacity, inCount, inStride);
            EnsureBufferCapacity(ref solidVertexBuffer, ref solidVertexBufferCapacity, solidVertexCount, outVtxStride);
            EnsureBufferCapacity(ref solidIndexBuffer, ref solidIndexBufferCapacity, solidIndexCount, sizeof(uint));

            sourceQuadBuffer.SetData(inputQuads);
        }

        /// <summary>
        /// 必要数を満たせない場合だけ GraphicsBuffer を 2 の累乗容量で作り直します。
        /// </summary>
        /// <param name="buffer">確保または再利用するバッファ。</param>
        /// <param name="capacity">現在の要素容量。再確保時に更新されます。</param>
        /// <param name="requiredCount">今回必要な要素数。</param>
        /// <param name="stride">1 要素あたりのバイト数。</param>
        void EnsureBufferCapacity(ref GraphicsBuffer buffer, ref int capacity, int requiredCount, int stride)
        {
            if (buffer != null && capacity >= requiredCount)
                return;

            buffer?.Release();
            int newCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, newCapacity, stride);
            capacity = newCapacity;
        }

        /// <summary>
        /// 入力クワッドバッファを compute shader に渡し、1 スレッド 1 文字クワッドでジオメトリを生成します。
        /// </summary>
        /// <remarks>
        /// 実際の numthreads.x をカーネルから取得し、可視クワッド数を割り上げて Dispatch グループ数を決めます。
        /// </remarks>
        void DispatchGeometryBuild()
        {
            if (!TryFindGeometryKernel() || geometryBuilderCompute == null ||
                sourceQuadBuffer == null || solidVertexBuffer == null || solidIndexBuffer == null)
                return;

            geometryBuilderCompute.SetBuffer(geometryKernel, "_SourceGlyphQuads", sourceQuadBuffer);
            geometryBuilderCompute.SetInt("_GlyphQuadCount", glyphQuadCount);
            geometryBuilderCompute.SetBuffer(geometryKernel, "_SolidGlyphVertices", solidVertexBuffer);
            geometryBuilderCompute.SetBuffer(geometryKernel, "_SolidGlyphIndices", solidIndexBuffer);
            // 押し出し方向は -Z 前提です。front は z≈0、back は z=-depth に配置されます。
            geometryBuilderCompute.SetVector("_DepthDirection", new Vector4(0, 0, -1, 0));

            // カーネル側の numthreads.x と文字クワッド数から、必要なグループ数を割り上げます。
            uint tgx = (uint)Mathf.Max(1, kernelThreadGroupSize), tgy = 1, tgz = 1;
            try { geometryBuilderCompute.GetKernelThreadGroupSizes(geometryKernel, out tgx, out tgy, out tgz); } catch { }
            int groupsX = Mathf.Max(1, Mathf.CeilToInt((float)glyphQuadCount / Mathf.Max(1, (int)tgx)));
            geometryBuilderCompute.Dispatch(geometryKernel, groupsX, 1, 1);
        }

        /// <summary>
        /// フォントマテリアルやテンプレートの見た目設定を描画用マテリアルへ同期し、GPU バッファを割り当てます。
        /// </summary>
        void SyncSolidMaterial()
        {
            if (!EnsureSolidMaterial())
                return;

            float guideMode = 0;
            bool hasGuideMode = false;

            if (solidMaterialTemplate != null)
            {
                solidMaterial.CopyPropertiesFromMaterial(solidMaterialTemplate);
                solidMaterial.renderQueue = solidMaterialTemplate.renderQueue;
                if (solidMaterialTemplate.HasProperty(GuideModeId))
                {
                    guideMode = solidMaterialTemplate.GetFloat(GuideModeId);
                    hasGuideMode = true;
                }
            }

            // フォントマテリアルのプロパティをコピーし、SDF アトラスや色設定を通常の TMP 表示と揃えます。
            var srcMat = targetText.fontSharedMaterial;
            if (srcMat != null)
            {
                solidMaterial.CopyPropertiesFromMaterial(srcMat);
                solidMaterial.renderQueue = srcMat.renderQueue;

                if (srcMat.HasProperty("_MainTex"))
                {
                    var tex = srcMat.GetTexture("_MainTex");
                    if (tex != null) solidMaterial.SetTexture("_MainTex", tex);
                }
                if (srcMat.HasProperty("_FaceTex"))
                {
                    var tex = srcMat.GetTexture("_FaceTex");
                    if (tex != null) solidMaterial.SetTexture("_FaceTex", tex);
                }
                if (srcMat.HasProperty(GuideModeId))
                {
                    guideMode = srcMat.GetFloat(GuideModeId);
                    hasGuideMode = true;
                }
            }

            if (hasGuideMode)
                solidMaterial.SetFloat(GuideModeId, guideMode);

            // compute 生成バッファをバインドして、頂点シェーダーから StructuredBuffer として読めるようにします。
            solidMaterial.SetBuffer("_SolidGlyphVertexBuffer", solidVertexBuffer);
            solidMaterial.SetBuffer("_SolidGlyphIndexBuffer", solidIndexBuffer);
        }

        /// <summary>
        /// テンプレート、現在のフォントマテリアル、既定名の順で Text3D 用シェーダーを解決します。
        /// </summary>
        /// <returns>使用するシェーダー。見つからない場合は null。</returns>
        Shader ResolveSolidShader()
        {
            if (solidMaterialTemplate != null && solidMaterialTemplate.shader != null)
                return solidMaterialTemplate.shader;

            var sourceShader = targetText != null && targetText.fontSharedMaterial != null ?
                targetText.fontSharedMaterial.shader :
                null;

            if (IsSolidText3DShader(sourceShader))
                return sourceShader;

            return Shader.Find(SolidUnlitShaderName);
        }

        /// <summary>
        /// 指定シェーダーが Text3D のプロシージャル描画に対応するものか判定します。
        /// </summary>
        /// <param name="shader">判定対象のシェーダー。</param>
        /// <returns>SolidUnlit または SolidUnlitExplainer の場合は true。</returns>
        static bool IsSolidText3DShader(Shader shader)
        {
            if (shader == null)
                return false;

            return shader.name == SolidUnlitShaderName ||
                shader.name == SolidUnlitExplainerShaderName;
        }

        /// <summary>
        /// 現在必要なシェーダーで描画用マテリアルを確保し、シェーダー変更時は作り直します。
        /// </summary>
        /// <returns>描画用マテリアルを使用できる場合は true。</returns>
        bool EnsureSolidMaterial()
        {
            var shader = ResolveSolidShader();
            if (shader == null || !shader.isSupported)
                return false;

            if (solidMaterial != null)
            {
                if (solidMaterial.shader != shader)
                {
                    DestroySolidMaterial();
                }
                else
                {
                    return true;
                }
            }

            solidMaterial = new Material(shader)
            {
                name = $"Text3DSolid({targetText?.fontSharedMaterial?.name ?? "NoFontMat"})",
                hideFlags = HideFlags.DontSave
            };
            return true;
        }

        /// <summary>
        /// Unity のカメラ描画タイミングで、compute shader が作ったバッファをプロシージャル描画します。
        /// </summary>
        /// <remarks>
        /// Pass 0 で本体を描き、2 段目アウトラインが有効な場合だけ Pass 1 を追加描画します。
        /// </remarks>
        void OnRenderObject()
        {
            if (!useGpuBuilder || solidVertexBuffer == null || solidIndexBuffer == null || 
                solidIndexCount <= 0)
                return;

            if (!EnsureSolidMaterial())
                return;

            Camera cam = Camera.current;
            if (!ShouldRenderForCamera(cam))
                return;

            if (needsGeometryRefresh)
            {
                RebuildGpuBuffers();
                if (solidMaterial == null || solidVertexBuffer == null || solidIndexBuffer == null ||
                    solidIndexCount <= 0)
                    return;
            }
            
            // テンプレートやフォントマテリアルの変更も反映できるよう、描画直前に同期します。
            SyncSolidMaterial();
            solidMaterial.SetBuffer("_SolidGlyphVertexBuffer", solidVertexBuffer);
            solidMaterial.SetBuffer("_SolidGlyphIndexBuffer", solidIndexBuffer);
            solidMaterial.SetMatrix("_Text3DLocalToWorld", transform.localToWorldMatrix);
            solidMaterial.SetMatrix("_Text3DWorldToLocal", transform.worldToLocalMatrix);
            
            // Pass 0 は厚み付き文字の本体を描画します。
            if (!solidMaterial.SetPass(0))
                return;
            Graphics.DrawProceduralNow(MeshTopology.Triangles, solidIndexCount, 1);
            
            // Pass 1 は 2 段目アウトラインが見える設定のときだけ描画します。
            if (HasVisibleOutlinePass() && solidMaterial.SetPass(1))
            {
                // 行列はパス間でも有効ですが、別パスでの読み落としを避けるため明示的に再設定します。
                solidMaterial.SetMatrix("_Text3DLocalToWorld", transform.localToWorldMatrix);
                solidMaterial.SetMatrix("_Text3DWorldToLocal", transform.worldToLocalMatrix);
                Graphics.DrawProceduralNow(MeshTopology.Triangles, solidIndexCount, 1);
            }
        }

        /// <summary>
        /// 現在のカメラがこのオブジェクトを描画すべきカメラか判定します。
        /// </summary>
        /// <param name="cam">Unity が現在描画中のカメラ。</param>
        /// <returns>プレビューや反射カメラではなく、カリングマスクにこのレイヤーが含まれる場合は true。</returns>
        bool ShouldRenderForCamera(Camera cam)
        {
            if (cam == null || cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection)
                return false;

            int layerMask = 1 << gameObject.layer;
            return (cam.cullingMask & layerMask) != 0;
        }

        /// <summary>
        /// 2 段目アウトライン用パスを描画する必要があるか判定します。
        /// </summary>
        /// <returns>パスが存在し、幅とアルファが見える値なら true。</returns>
        bool HasVisibleOutlinePass()
        {
            if (solidMaterial == null || solidMaterial.passCount < 2)
                return false;

            if (solidMaterial.HasProperty(Outline2WidthId) &&
                solidMaterial.GetFloat(Outline2WidthId) <= 0.0001f)
                return false;

            if (solidMaterial.HasProperty(Outline2ColorId) &&
                solidMaterial.GetColor(Outline2ColorId).a <= 0.0001f)
                return false;

            return true;
        }

        /// <summary>
        /// 指定文字の押し出し厚みを変更し、ジオメトリ更新を予約します。
        /// </summary>
        /// <param name="glyphIndex">TextMeshPro の文字インデックス。</param>
        /// <param name="thickness">新しい押し出し厚み。</param>
        public void SetGlyphThickness(int glyphIndex, float thickness)
        {
            if (glyphIndex < 0 || glyphIndex >= glyphThicknesses.Count)
                return;
                
            var data = glyphThicknesses[glyphIndex];
            data.SetGlyphThickness(thickness);
            glyphThicknesses[glyphIndex] = data;
            
            RequestGeometryRefresh();
        }

        /// <summary>
        /// 指定文字の奥行き色参照範囲を変更し、ジオメトリ更新を予約します。
        /// </summary>
        /// <param name="glyphIndex">TextMeshPro の文字インデックス。</param>
        /// <param name="depthRange">奥行き進行度を色へ割り当てる最小値と最大値。</param>
        public void SetGlyphDepthRange(int glyphIndex, Vector2 depthRange)
        {
            if (glyphIndex < 0 || glyphIndex >= glyphThicknesses.Count)
                return;
                
            var data = glyphThicknesses[glyphIndex];
            data.SetGlyphDepthRange(depthRange);
            glyphThicknesses[glyphIndex] = data;
            
            RequestGeometryRefresh();
        }

        /// <summary>
        /// 旧 API 互換用の厚み変更メソッドです。新規コードでは <see cref="SetGlyphThickness"/> を使います。
        /// </summary>
        /// <param name="characterIndex">TextMeshPro の文字インデックス。</param>
        /// <param name="depth">新しい押し出し厚み。</param>
        [Obsolete("Use SetGlyphThickness instead.")]
        public void SetDepth(int characterIndex, float depth)
        {
            SetGlyphThickness(characterIndex, depth);
        }

        /// <summary>
        /// 旧 API 互換用の奥行き範囲変更メソッドです。新規コードでは <see cref="SetGlyphDepthRange"/> を使います。
        /// </summary>
        /// <param name="characterIndex">TextMeshPro の文字インデックス。</param>
        /// <param name="depthMapping">奥行き進行度を色へ割り当てる最小値と最大値。</param>
        [Obsolete("Use SetGlyphDepthRange instead.")]
        public void SetDepthMapping(int characterIndex, Vector2 depthMapping)
        {
            SetGlyphDepthRange(characterIndex, depthMapping);
        }

        /// <summary>
        /// GPU 生成が使えない場合に、TMP メッシュの UV2 へ厚みと奥行き範囲を書き込みます。
        /// </summary>
        /// <remarks>
        /// 各文字の 4 頂点に同じ depthData を入れ、下線・取り消し線がある場合は対応する 12 頂点にも同じ値を入れます。
        /// これにより TMP3D 互換のフォールバックシェーダーが文字単位の厚み情報を読めます。
        /// </remarks>
        public void WriteFallbackDepthData()
        {
            if (targetText == null || targetText.textInfo == null)
                return;

            var count = Mathf.Min(glyphThicknesses.Count, targetText.textInfo.characterCount);

            for (int i = 0; i < targetText.textInfo.meshInfo.Length; i++)
            {
                var meshInfo = targetText.textInfo.meshInfo[i];
                var mesh = meshInfo.mesh;

                if (mesh == null)
                    continue;

                cachedUv2.Clear();
                mesh.GetUVs(2, cachedUv2);

                if (cachedUv2.Count == 0)
                {
                    // UV2 が未作成の場合は UV0 の xy を残し、z/w に厚み情報を書ける Vector4 配列へ広げます。
                    mesh.GetUVs(0, cachedUv2);
                    for (int j = 0; j < cachedUv2.Count; j++)
                    {
                        cachedUv2[j] = new Vector4(cachedUv2[j].x, cachedUv2[j].y, 0, 0);
                    }
                }

                int requiredSize = mesh.vertexCount;
                while (cachedUv2.Count < requiredSize)
                {
                    cachedUv2.Add(Vector4.zero);
                }

                int lastVertexIndex = -1;
                for (int j = 0; j < count; j++)
                {
                    var charInfo = targetText.textInfo.characterInfo[j];
                    int meshIndex = charInfo.materialReferenceIndex;
                    if (meshIndex != i)
                        continue;

                    int vertexIndex = charInfo.vertexIndex;

                    // TMP の装飾文字などで頂点インデックスが戻るケースを避け、同じ範囲への重複書き込みを抑えます。
                    if (lastVertexIndex > vertexIndex)
                        continue;

                    lastVertexIndex = vertexIndex;
                    var underlineIndex = charInfo.underlineVertexIndex;
                    var strikethroughIndex = charInfo.strikethroughVertexIndex;

                    var glyphThickness = glyphThicknesses[j];
                    var depthData = new Vector4(glyphThickness.Thickness, glyphThickness.DepthRange.x,
                        glyphThickness.DepthRange.y, 0);

                    // 通常の文字クワッドは 4 頂点です。
                    for (int k = 0; k < 4; k++)
                    {
                        int idx = vertexIndex + k;
                        if (idx < cachedUv2.Count)
                        {
                            cachedUv2[idx] = depthData;
                        }
                    }

                    if (underlineIndex != vertexIndex && underlineIndex >= 0)
                    {
                        // TMP の下線は複数クワッドで構成されるため、12 頂点へ同じ奥行き情報を渡します。
                        for (int k = 0; k < 12; k++)
                        {
                            int idx = underlineIndex + k;
                            if (idx < cachedUv2.Count)
                            {
                                cachedUv2[idx] = depthData;
                            }
                        }
                    }

                    if (strikethroughIndex != vertexIndex && strikethroughIndex >= 0)
                    {
                        // TMP の取り消し線も下線と同じく 12 頂点分の装飾メッシュです。
                        for (int k = 0; k < 12; k++)
                        {
                            int idx = strikethroughIndex + k;
                            if (idx < cachedUv2.Count)
                            {
                                cachedUv2[idx] = depthData;
                            }
                        }
                    }
                }

                mesh.SetUVs(2, cachedUv2);
            }
        }

        /// <summary>
        /// インスペクター変更時に参照、カーネル、文字厚みデータ、検査インデックスを再検証します。
        /// </summary>
        private void OnValidate()
        {
            targetText = GetComponent<TextMeshPro>();
            geometryKernel = -1;
            kernelThreadGroupSize = Mathf.Max(1, kernelThreadGroupSize);
            glyphThicknesses.Clear();
            if (targetText != null)
            {
                HandleTextChanged(targetText);
            }
            
            // 検査インデックスを現在の生成済み範囲へ収め、Gizmo や GetData の範囲外アクセスを防ぎます。
            inspectionGlyphIndex = Mathf.Clamp(inspectionGlyphIndex, 0, Mathf.Max(0, glyphQuadCount - 1));
            inspectionVertexIndex = Mathf.Clamp(inspectionVertexIndex, 0, 15);
        }

        /// <summary>
        /// UV 配列の範囲外参照を避け、存在しない場合は Vector2.zero を返します。
        /// </summary>
        /// <param name="uvs">参照する UV 配列。</param>
        /// <param name="index">取得したい頂点インデックス。</param>
        /// <returns>指定インデックスの UV。取得できない場合は Vector2.zero。</returns>
        static Vector2 GetUvOrDefault(Vector2[] uvs, int index)
        {
            return uvs != null && index >= 0 && index < uvs.Length ? uvs[index] : Vector2.zero;
        }

        /// <summary>
        /// 頂点カラー配列の範囲外参照を避け、存在しない場合は白を返します。
        /// </summary>
        /// <param name="colors">参照する頂点カラー配列。</param>
        /// <param name="index">取得したい頂点インデックス。</param>
        /// <returns>指定インデックスの頂点カラー。取得できない場合は白。</returns>
        static Color32 GetColorOrDefault(Color32[] colors, int index)
        {
            return colors != null && index >= 0 && index < colors.Length ? colors[index] : (Color32)Color.white;
        }

        /// <summary>
        /// 選択中オブジェクトのシーンビューに、検査対象の生成頂点と法線を Gizmo 表示します。
        /// </summary>
        void OnDrawGizmosSelected()
        {
            if (!enableInspection || inspectedSolidVertices.Count == 0)
                return;
            
            // 選択された頂点を表示
            if (inspectionVertexIndex < inspectedSolidVertices.Count)
            {
                var vertex = inspectedSolidVertices[inspectionVertexIndex];
                Vector3 worldPos = transform.TransformPoint(vertex.position);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(worldPos, 0.01f);
                
                // 選択頂点は位置に加えて法線方向も表示し、compute shader の面向きを確認しやすくします。
                if (vertex.normal != Vector3.zero)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(worldPos, worldPos + transform.TransformDirection(vertex.normal) * 0.1f);
                }
            }
            
            // 同じクワッドの他の頂点は小さなキューブで表示し、16 頂点の配置を俯瞰できるようにします。
            Gizmos.color = Color.cyan;
            for (int i = 0; i < inspectedSolidVertices.Count; i++)
            {
                if (i != inspectionVertexIndex)
                {
                    Vector3 worldPos = transform.TransformPoint(inspectedSolidVertices[i].position);
                    Gizmos.DrawWireCube(worldPos, Vector3.one * 0.005f);
                }
            }
        }
    }
}
