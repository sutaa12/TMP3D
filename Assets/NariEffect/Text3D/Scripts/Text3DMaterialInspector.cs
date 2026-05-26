#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Scripting.APIUpdating;
using TMPro.EditorUtilities;

namespace NariEffect.Text3D.Editor
{
    /// <summary>
    /// Text3D 用 SolidUnlit シェーダーのマテリアル項目を、用途別パネルに整理して表示します。
    /// </summary>
    /// <remarks>
    /// TextMeshPro 標準の <see cref="TMP_BaseShaderGUI"/> を継承し、TMP の内部プロパティを保ちながら Text3D 固有の設定を扱います。
    /// </remarks>
    [MovedFrom(true, sourceNamespace: "NariEffect.Script", sourceClassName: "TMP3D_CustomUnlitShaderGUI")]
    public class Text3DMaterialInspector : TMP_BaseShaderGUI
    {
        /// <summary>
        /// デバッグ表示用シェーダーキーワードをポップアップとして扱うための機能定義です。
        /// </summary>
        static ShaderFeature debugModeFeature;

        /// <summary>
        /// General パネルの開閉状態です。
        /// </summary>
        static bool showGeneralPanel = true;

        /// <summary>
        /// Outline パネルの開閉状態です。
        /// </summary>
        static bool showOutlinePanel = true;

        /// <summary>
        /// 3D パネルの開閉状態です。
        /// </summary>
        static bool showText3DPanel = true;

        /// <summary>
        /// Bevel パネルの開閉状態です。
        /// </summary>
        static bool showBevelPanel = true;

        /// <summary>
        /// Lighting パネルの開閉状態です。
        /// </summary>
        static bool showLightingPanel = true;

        /// <summary>
        /// IMGUI で一時的に使い回すラベルです。毎描画での GC 発生を抑えます。
        /// </summary>
        protected static GUIContent sharedLabel = new GUIContent();

        /// <summary>
        /// インスペクターで使うシェーダーキーワード UI を初期化します。
        /// </summary>
        static Text3DMaterialInspector()
        {
            
            
            debugModeFeature = new ShaderFeature()
            {
                undoLabel = "Debug",
                label = new GUIContent("Debug Mode"),
                keywords = new[] { "DEBUG_STEPS", "DEBUG_MASK" },
                keywordLabels = new[] { new GUIContent("None"), new GUIContent("Steps"), new GUIContent("Mask") }
            };
        }

        /// <summary>
        /// マテリアルプロパティを取得し、Undo と複数選択時の混在値表示を開始します。
        /// </summary>
        /// <param name="name">取得するシェーダープロパティ名。</param>
        /// <returns>見つかったマテリアルプロパティ。</returns>
        protected MaterialProperty BeginMaterialProperty(string name)
        {
            MaterialProperty property = FindProperty(name, m_Properties);
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = property.hasMixedValue;
            m_Editor.BeginAnimatedCheck(Rect.zero, property);
            return property;
        }

        /// <summary>
        /// プロパティ編集の監視を終了し、値が変更されたかを返します。
        /// </summary>
        /// <returns>GUI 操作で値が変更された場合は true。</returns>
        protected bool EndMaterialProperty()
        {
            m_Editor.EndAnimatedCheck();
            EditorGUI.showMixedValue = false;
            return EditorGUI.EndChangeCheck();
        }

        /// <summary>
        /// Vector プロパティの指定 2 要素を最小最大スライダーとして描画します。
        /// </summary>
        /// <param name="property">対象の Vector プロパティ名。</param>
        /// <param name="label">インスペクターに表示するラベル。</param>
        /// <param name="minLimit">入力できる最小値。</param>
        /// <param name="maxLimit">入力できる最大値。</param>
        /// <param name="indexA">最小値として使う Vector 要素番号。</param>
        /// <param name="indexB">最大値として使う Vector 要素番号。</param>
        protected void DrawRangeSlider(string property, string label, float minLimit, float maxLimit, int indexA, int indexB)
        {
            MaterialProperty prop = BeginMaterialProperty(property);
            sharedLabel.text = label;

            var vector = prop.vectorValue;
            var min = vector[indexA];
            var max = vector[indexB];

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = originalLabelWidth - 26;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(sharedLabel);
            min = EditorGUILayout.FloatField(min, GUILayout.Width(100f));
            EditorGUILayout.MinMaxSlider(ref min, ref max, minLimit, maxLimit);
            max = EditorGUILayout.FloatField(max, GUILayout.Width(100f));
            EditorGUILayout.EndHorizontal();

            min = Mathf.Clamp(min, minLimit, max);
            max = Mathf.Clamp(max, min, maxLimit);

            EditorGUIUtility.labelWidth = originalLabelWidth;

            if (EndMaterialProperty())
            {
                vector[indexA] = min;
                vector[indexB] = max;
                prop.vectorValue = vector;
                m_Editor.RegisterPropertyChangeUndo(label);
            }
        }

        /// <summary>
        /// Text3D マテリアルインスペクター全体を、折りたたみ可能なカテゴリ順に描画します。
        /// </summary>
        protected override void DoGUI()
        {
            showGeneralPanel = BeginPanel("General", showGeneralPanel);
            if (showGeneralPanel)
            {
                DrawGeneralPanel();
            }
            EndPanel();

            showText3DPanel = BeginPanel("3D", showText3DPanel);
            if (showText3DPanel)
            {
                DrawText3DPanel();
            }
            EndPanel();

            showOutlinePanel = BeginPanel("Outline", showOutlinePanel);
            if (showOutlinePanel)
            {
                DrawOutlinePanel();
            }
            EndPanel();

            showBevelPanel = BeginPanel("Bevel", showBevelPanel);
            if (showBevelPanel)
            {
                DrawBevelPanel();
            }
            EndPanel();

            showLightingPanel = BeginPanel("Lighting", showLightingPanel);
            if (showLightingPanel)
            {
                DrawLightingPanel();
            }
            EndPanel();

            s_DebugExtended = BeginPanel("Debug Settings", s_DebugExtended);
            if (s_DebugExtended)
            {
                DrawDebugPanel();
            }
            EndPanel();
        }

        /// <summary>
        /// 表面色、フォントウェイト、表面テクスチャなど基本設定を描画します。
        /// </summary>
        void DrawGeneralPanel()
        {
            EditorGUI.indentLevel++;
            DoColor("_Color", "Color");
            DoSlider("_WeightBold", "Weight Bold");
            DoSlider("_WeightNormal", "Weight Normal");
            DoTexture2D("_FaceTex", "Face Texture", true);
            DoFloat("_FaceTextureScrollSpeed", "Face Texture Scroll Speed");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        /// <summary>
        /// 1 段目と 2 段目のアウトライン色、幅、テクスチャ設定を描画します。
        /// </summary>
        void DrawOutlinePanel()
        {
            EditorGUI.indentLevel++;
            DoTexture2D("_OutlineTex", "Outline Texture", true);
            DoFloat("_OutlineTextureScrollSpeed", "Outline Texture Scroll Speed");
            DoColor("_OutlineColor", "Outline Color");
            DoSlider("_OutlineWidth", "Outline Thickness");
            DoColor("_Outline2Color", "Outline2 Color");
            DoSlider("_Outline2Width", "Outline2 Thickness");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        /// <summary>
        /// 奥行き面のテクスチャと色など、3D レイマーチ部分の設定を描画します。
        /// </summary>
        void DrawText3DPanel()
        {
            EditorGUI.indentLevel++;

            DoTexture2D("_DepthAlbedo", "Depth Albedo");
            DoColor("_DepthColor", "Depth Color");

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        /// <summary>
        /// SDF から作る疑似ベベルの幅、位置、丸み、クランプを描画します。
        /// </summary>
        void DrawBevelPanel()
        {
            EditorGUI.indentLevel++;
            DoSlider("_BevelWidth", "Bevel Width");
            DoSlider("_BevelOffset", "Bevel Offset");
            DoSlider("_Bevel", "Bevel Strength");
            DoSlider("_BevelRoundness", "Bevel Roundness");
            DoSlider("_BevelClamp", "Bevel Clamp");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        /// <summary>
        /// ベベル法線にかける疑似ライティングとスペキュラー設定を描画します。
        /// </summary>
        void DrawLightingPanel()
        {
            EditorGUI.indentLevel++;
            DoColor("_SpecularColor", "Specular Color");
            DoSlider("_Reflectivity", "Reflectivity");
            DoSlider("_SpecularPower", "Specular Power");
            DoSlider("_LightAngle", "Light Angle");
            DoSlider("_Diffuse", "Diffuse Term");
            DoSlider("_Ambient", "Ambient Term");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        /// <summary>
        /// SDF アトラスやデバッグ表示キーワードなど、検証用の設定を描画します。
        /// </summary>
        void DrawDebugPanel()
        {
            EditorGUI.indentLevel++;
            DoTexture2D("_MainTex", "Font Atlas");
            DoFloat("_GradientScale", "Gradient Scale");
            DoFloat("_TextureWidth", "Texture Width");
            DoFloat("_TextureHeight", "Texture Height");
            debugModeFeature.ReadState(m_Material);
            debugModeFeature.DoPopup(m_Editor, m_Material);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }
    }

    /// <summary>
    /// Text3D の内部処理を段階表示する Explainer シェーダー用インスペクターです。
    /// </summary>
    public class Text3DExplainerMaterialInspector : Text3DMaterialInspector
    {
        /// <summary>
        /// Explainer パネルの開閉状態です。
        /// </summary>
        static bool showExplainerPanel = true;

        /// <summary>
        /// Explainer の表示段階をインスペクターに並べるラベル一覧です。
        /// </summary>
        static readonly GUIContent[] guideModeLabels =
        {
            new GUIContent("01 Compute Box Only"),
            new GUIContent("02 Mask Coordinates"),
            new GUIContent("03 Atlas SDF Slice"),
            new GUIContent("04 Text Only"),
            new GUIContent("05 Box Raycast Hit Test"),
            new GUIContent("06 Ray Steps"),
            new GUIContent("07 Depth Progress"),
            new GUIContent("08 Final Rendering")
        };

        /// <summary>
        /// <see cref="guideModeLabels"/> と対応する _GuideMode の数値です。
        /// </summary>
        static readonly int[] guideModeValues = { 1, 2, 3, 4, 5, 6, 7, 0 };

        /// <summary>
        /// Explainer 用の表示モード選択だけを描画します。
        /// </summary>
        protected override void DoGUI()
        {
            showExplainerPanel = BeginPanel("Explainer", showExplainerPanel);
            if (showExplainerPanel)
            {
                DrawExplainerPanel();
            }
            EndPanel();
        }

        /// <summary>
        /// Explainer の専用パネル内容を描画します。
        /// </summary>
        void DrawExplainerPanel()
        {
            EditorGUI.indentLevel++;
            DrawGuideModePopup();
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        /// <summary>
        /// _GuideMode プロパティを段階名付きのポップアップとして描画します。
        /// </summary>
        void DrawGuideModePopup()
        {
            MaterialProperty prop = BeginMaterialProperty("_GuideMode");
            sharedLabel.text = "Explainer Mode";
            int current = Mathf.Clamp(Mathf.RoundToInt(prop.floatValue), 0, guideModeValues.Length - 1);
            int next = EditorGUILayout.IntPopup(sharedLabel, current, guideModeLabels, guideModeValues);

            if (EndMaterialProperty())
            {
                prop.floatValue = next;
                m_Editor.RegisterPropertyChangeUndo("Explainer Mode");
            }
        }
    }

}
#endif
