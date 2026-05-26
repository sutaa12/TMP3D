using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Ikaroon.TMP3D
{
    /// <summary>
    /// Metal 対応のため GeometryShader を使わずに各文字を「箱メッシュ（6面）」化するビルダー。
    /// TextMeshPro の各文字クワッドをもとに、前面/背面/4つのサイド面を生成し、
    /// シェーダー側へ必要な境界情報（boundariesUV / boundariesLocal / boundariesLocalZ）を UV にエンコードして渡します。
    /// 既存の TMP3D_Unlit_MetalSafe.shader と組み合わせて使用します。
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(TextMeshPro))]
    public sealed class TMP3D_MetalMeshBuilder : MonoBehaviour
    {
        /// <summary>ターゲットの TextMeshPro。Editor/Runtime 両方で動作。</summary>
        public TextMeshPro TMP => _tmp ?? (_tmp = GetComponent<TextMeshPro>());

        [SerializeField] private TextMeshPro _tmp;

        /// <summary>各文字の厚み（奥行）。TMP3D_Handler を併用している場合は uv2.x を優先します。</summary>
        [SerializeField] private float _defaultDepth = 1.0f;

        /// <summary>テキスト変更/Transform 変更時に自動で再ビルドするか。</summary>
        [SerializeField] private bool _autoRebuild = true;

        // 作業用キャッシュ
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector3> _norms = new List<Vector3>();
        private readonly List<Color32> _cols = new List<Color32>();
        private readonly List<Vector2> _uv0 = new List<Vector2>();
        private readonly List<Vector2> _uv1 = new List<Vector2>(); // TMP 内部用 (そのままパススルー)
        private readonly List<Vector4> _uv2 = new List<Vector4>(); // tmp3d(depth, map.x, map.y, _)
        private readonly List<Vector4> _uv3 = new List<Vector4>(); // boundariesUV
        private readonly List<Vector4> _uv4 = new List<Vector4>(); // boundariesLocal
        private readonly List<Vector4> _uv5 = new List<Vector4>(); // boundariesLocalZ
        private readonly List<int> _tris = new List<int>();
        private readonly List<Vector3> _outV = new List<Vector3>();
        private readonly List<Vector3> _outN = new List<Vector3>();
        private readonly List<Color32> _outC = new List<Color32>();
        private readonly List<Vector2> _outUv0 = new List<Vector2>();
        private readonly List<Vector2> _outUv1 = new List<Vector2>();
        private readonly List<Vector4> _outUv2 = new List<Vector4>();
        private readonly List<Vector4> _outUv3 = new List<Vector4>();
        private readonly List<Vector4> _outUv4 = new List<Vector4>();
        private readonly List<Vector4> _outUv5 = new List<Vector4>();

        private Mesh _mesh;

        private void OnEnable()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "TMP3D_MetalSafeMesh" };
                _mesh.MarkDynamic();
            }
            Rebuild();
        }

        private void OnDisable()
        {
            if (_mesh != null)
            {
                _mesh.Clear();
            }
        }

        private void OnDestroy()
        {
            ClearAssign();
            if (_mesh == null) return;

            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
            _mesh = null;
        }

        private void Update()
        {
            if (_autoRebuild && TMP != null && TMP.havePropertiesChanged)
            {
                Rebuild();
            }
        }

        /// <summary>
        /// 手動で再ビルドしたいときに呼び出してください。
        /// </summary>
        public void Rebuild()
        {
            if (TMP == null) return;
            TMP.ForceMeshUpdate();
            var ti = TMP.textInfo;
            if (ti == null || ti.characterCount == 0 || ti.meshInfo == null || ti.meshInfo.Length == 0)
            {
                ClearAssign();
                return;
            }

            // この実装は単一サブメッシュ構成を想定（一般的な構成）。
            var mi = ti.meshInfo[0];
            var srcMesh = mi.mesh;
            if (srcMesh == null) { ClearAssign(); return; }

            srcMesh.GetVertices(_verts);
            srcMesh.GetNormals(_norms);
            srcMesh.GetColors(_cols);
            srcMesh.GetUVs(0, _uv0);
            srcMesh.GetUVs(1, _uv1);
            srcMesh.GetUVs(2, _uv2); // TMP3D_Handler が書いた (depth, mapMin, mapMax, _)

            // 出力先を初期化
            _outV.Clear();
            _outN.Clear();
            _outC.Clear();
            _outUv0.Clear();
            _outUv1.Clear();
            _outUv2.Clear();
            _outUv3.Clear();
            _outUv4.Clear();
            _outUv5.Clear();
            _tris.Clear();

            for (int ci = 0; ci < ti.characterCount; ci++)
            {
                var ch = ti.characterInfo[ci];
                if (!ch.isVisible) continue;
                if (ch.materialReferenceIndex != 0) continue; // 単一サブメッシュのみ対応（必要なら拡張）

                int vi = ch.vertexIndex;
                if (vi < 0 || vi + 3 >= _verts.Count) continue;

                // 元の 4 頂点（ローカル空間）
                Vector3 p0 = _verts[vi + 0];
                Vector3 p1 = _verts[vi + 1];
                Vector3 p2 = _verts[vi + 2];
                Vector3 p3 = _verts[vi + 3];

                // 法線は 0 番のものを使用（TMP の各クワッドで共通）
                Vector3 n = (vi < _norms.Count) ? _norms[vi] : Vector3.forward;

                // カラー/UV は各頂点から取得
                Color32 c0 = GetValue(_cols, vi + 0, (Color32)Color.white);
                Color32 c1 = GetValue(_cols, vi + 1, (Color32)Color.white);
                Color32 c2 = GetValue(_cols, vi + 2, (Color32)Color.white);
                Color32 c3 = GetValue(_cols, vi + 3, (Color32)Color.white);

                Vector2 t0 = GetValue(_uv0, vi + 0, Vector2.zero);
                Vector2 t1 = GetValue(_uv0, vi + 1, Vector2.zero);
                Vector2 t2 = GetValue(_uv0, vi + 2, Vector2.zero);
                Vector2 t3 = GetValue(_uv0, vi + 3, Vector2.zero);

                Vector2 u10 = GetValue(_uv1, vi + 0, Vector2.zero);
                Vector2 u11 = GetValue(_uv1, vi + 1, Vector2.zero);
                Vector2 u12 = GetValue(_uv1, vi + 2, Vector2.zero);
                Vector2 u13 = GetValue(_uv1, vi + 3, Vector2.zero);

                // TMP3D の深さ情報（無ければデフォルト）
                float depth = _defaultDepth;
                if (vi < _uv2.Count) depth = _uv2[vi].x;

                Vector2 depthMap = new Vector2(0f, 1f);
                if (vi < _uv2.Count) depthMap = new Vector2(_uv2[vi].y, _uv2[vi].z);

                // UV / ローカル境界（元の GS 実装と同様の算出）
                float skewUV = Mathf.Abs(t1.x - t0.x);
                float widthUV = Mathf.Abs(t2.x - t1.x);
                float heightUV = Mathf.Abs(t1.y - t0.y);
                float xUV = Mathf.Min(Mathf.Min(t0.x, t2.x), Mathf.Min(t1.x, t3.x));
                float yUV = Mathf.Min(Mathf.Min(t0.y, t1.y), Mathf.Min(t2.y, t3.y));
                Vector4 boundariesUV = new Vector4(xUV, yUV, widthUV, heightUV);

                float skewLocal = Mathf.Abs(p1.x - p0.x);
                float widthLocal = Mathf.Abs(p2.x - p1.x);
                float heightLocal = Mathf.Abs(p1.y - p0.y);
                float xLocal = Mathf.Min(Mathf.Min(p0.x, p2.x), Mathf.Min(p1.x, p3.x));
                float yLocal = Mathf.Min(Mathf.Min(p0.y, p1.y), Mathf.Min(p2.y, p3.y));
                Vector4 boundariesLocal = new Vector4(xLocal, yLocal, widthLocal, heightLocal);
                Vector4 boundariesLocalZ = new Vector4(-depth, 0f, skewLocal, skewUV);

                // 便利関数：四角形を追加（v0-v1-v2 / v2-v1-v3 の2枚三角形）
                void AddQuad(Vector3 q0, Vector3 q1, Vector3 q2, Vector3 q3,
                             Color32 qc0, Color32 qc1, Color32 qc2, Color32 qc3,
                             Vector2 qt0, Vector2 qt1, Vector2 qt2, Vector2 qt3,
                             Vector2 qu10, Vector2 qu11, Vector2 qu12, Vector2 qu13)
                {
                    int start = _outV.Count;

                    // 頂点
                    _outV.Add(q0); _outV.Add(q1); _outV.Add(q2); _outV.Add(q3);

                    // 法線（おおまかでOK。シェーダは主に境界情報を使う）
                    Vector3 faceN = Vector3.Cross(q2 - q0, q1 - q0).normalized;
                    _outN.Add(faceN); _outN.Add(faceN); _outN.Add(faceN); _outN.Add(faceN);

                    // カラー
                    _outC.Add(qc0); _outC.Add(qc1); _outC.Add(qc2); _outC.Add(qc3);

                    // UV0 / UV1
                    _outUv0.Add(qt0); _outUv0.Add(qt1); _outUv0.Add(qt2); _outUv0.Add(qt3);
                    _outUv1.Add(qu10); _outUv1.Add(qu11); _outUv1.Add(qu12); _outUv1.Add(qu13);

                    // UV2 (tmp3d) は全頂点に同一値（char 単位）
                    var uv2v = new Vector4(depth, depthMap.x, depthMap.y, 0);
                    _outUv2.Add(uv2v); _outUv2.Add(uv2v); _outUv2.Add(uv2v); _outUv2.Add(uv2v);

                    // UV3/4/5: 境界情報（全頂点に同一値）
                    _outUv3.Add(boundariesUV); _outUv3.Add(boundariesUV);
                    _outUv3.Add(boundariesUV); _outUv3.Add(boundariesUV);

                    _outUv4.Add(boundariesLocal); _outUv4.Add(boundariesLocal);
                    _outUv4.Add(boundariesLocal); _outUv4.Add(boundariesLocal);

                    _outUv5.Add(boundariesLocalZ); _outUv5.Add(boundariesLocalZ);
                    _outUv5.Add(boundariesLocalZ); _outUv5.Add(boundariesLocalZ);

                    // インデックス
                    _tris.Add(start + 0); _tris.Add(start + 1); _tris.Add(start + 2);
                    _tris.Add(start + 2); _tris.Add(start + 1); _tris.Add(start + 3);
                }

                // 背面（法線方向に奥へ）
                Vector3 b0 = p0 + n * depth;
                Vector3 b1 = p1 + n * depth;
                Vector3 b2 = p2 + n * depth;
                Vector3 b3 = p3 + n * depth;

                // 前面
                AddQuad(p0, p1, p2, p3, c0, c1, c2, c3, t0, t1, t2, t3, u10, u11, u12, u13);
                // 背面（頂点並びで面の向きを調整）
                AddQuad(b2, b1, b0, b3, c2, c1, c0, c3, t2, t1, t0, t3, u12, u11, u10, u13);

                // 左側面（p0-p1 と b0-b1 を結ぶ）
                AddQuad(p0, b0, b1, p1, c0, c0, c1, c1, t0, t0, t1, t1, u10, u10, u11, u11);
                // 右側面（p3-p2 と b3-b2）
                AddQuad(p3, b3, b2, p2, c3, c3, c2, c2, t3, t3, t2, t2, u13, u13, u12, u12);
                // 下側面（p0-p3 と b0-b3）
                AddQuad(p0, p3, b3, b0, c0, c3, c3, c0, t0, t3, t3, t0, u10, u13, u13, u10);
                // 上側面（p1-p2 と b1-b2）
                AddQuad(p1, p2, b2, b1, c1, c2, c2, c1, t1, t2, t2, t1, u11, u12, u12, u11);
            }

            // 出力メッシュを構築
            _mesh.Clear();
            _mesh.SetVertices(_outV);
            _mesh.SetNormals(_outN);
            _mesh.SetColors(_outC);
            _mesh.SetUVs(0, _outUv0);
            _mesh.SetUVs(1, _outUv1);
            _mesh.SetUVs(2, _outUv2);
            _mesh.SetUVs(3, _outUv3);
            _mesh.SetUVs(4, _outUv4);
            _mesh.SetUVs(5, _outUv5);
            _mesh.SetTriangles(_tris, 0, true);

            // TextMeshPro のメッシュに反映（単一サブメッシュ想定）
            var mf = TMP.GetComponent<MeshFilter>() ?? TMP.gameObject.AddComponent<MeshFilter>();
            var mr = TMP.GetComponent<MeshRenderer>() ?? TMP.gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = _mesh;
            mr.sharedMaterial = mi.material;
        }

        /// <summary>メッシュをクリアして適用解除。</summary>
        private void ClearAssign()
        {
            if (_mesh == null) return;
            _mesh.Clear();
            var mf = TMP.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = null;
        }

        private static T GetValue<T>(List<T> values, int index, T fallback)
        {
            return index >= 0 && index < values.Count ? values[index] : fallback;
        }
    }
}
