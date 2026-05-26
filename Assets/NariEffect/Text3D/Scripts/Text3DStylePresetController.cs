using System;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NariEffect.Text3D
{
    /// <summary>
    /// Text3D 用マテリアルへ色、アウトライン、ベベル、ライティングのプリセット値を適用します。
    /// </summary>
    /// <remarks>
    /// 実行時に個別マテリアルを作る設定では、同じフォント素材を共有する別オブジェクトへプリセット変更が波及しないようにします。
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshPro))]
    public class Text3DStylePresetController : MonoBehaviour
    {
        /// <summary>
        /// シェーダーの表面色プロパティ ID です。
        /// </summary>
        static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>
        /// 奥行き面の色プロパティ ID です。
        /// </summary>
        static readonly int DepthColorId = Shader.PropertyToID("_DepthColor");

        /// <summary>
        /// 1 段目アウトライン色のプロパティ ID です。
        /// </summary>
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        /// <summary>
        /// 1 段目アウトライン幅のプロパティ ID です。
        /// </summary>
        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        /// <summary>
        /// 2 段目アウトライン色のプロパティ ID です。
        /// </summary>
        static readonly int Outline2ColorId = Shader.PropertyToID("_Outline2Color");

        /// <summary>
        /// 2 段目アウトライン幅のプロパティ ID です。
        /// </summary>
        static readonly int Outline2WidthId = Shader.PropertyToID("_Outline2Width");

        /// <summary>
        /// ベベル幅のプロパティ ID です。
        /// </summary>
        static readonly int BevelWidthId = Shader.PropertyToID("_BevelWidth");

        /// <summary>
        /// ベベル位置オフセットのプロパティ ID です。
        /// </summary>
        static readonly int BevelOffsetId = Shader.PropertyToID("_BevelOffset");

        /// <summary>
        /// ベベル強度のプロパティ ID です。
        /// </summary>
        static readonly int BevelId = Shader.PropertyToID("_Bevel");

        /// <summary>
        /// ベベルの丸みのプロパティ ID です。
        /// </summary>
        static readonly int BevelRoundnessId = Shader.PropertyToID("_BevelRoundness");

        /// <summary>
        /// ベベル高さの上限を調整するプロパティ ID です。
        /// </summary>
        static readonly int BevelClampId = Shader.PropertyToID("_BevelClamp");

        /// <summary>
        /// スペキュラー色のプロパティ ID です。
        /// </summary>
        static readonly int SpecularColorId = Shader.PropertyToID("_SpecularColor");

        /// <summary>
        /// 反射の鋭さに使うプロパティ ID です。
        /// </summary>
        static readonly int ReflectivityId = Shader.PropertyToID("_Reflectivity");

        /// <summary>
        /// スペキュラーの強度に使うプロパティ ID です。
        /// </summary>
        static readonly int SpecularPowerId = Shader.PropertyToID("_SpecularPower");

        /// <summary>
        /// 擬似ライトの方向角プロパティ ID です。
        /// </summary>
        static readonly int LightAngleId = Shader.PropertyToID("_LightAngle");

        /// <summary>
        /// 拡散光の影響量プロパティ ID です。
        /// </summary>
        static readonly int DiffuseId = Shader.PropertyToID("_Diffuse");

        /// <summary>
        /// 環境光の下限量プロパティ ID です。
        /// </summary>
        static readonly int AmbientId = Shader.PropertyToID("_Ambient");

        /// <summary>
        /// プリセット Default で使う表面色です。
        /// </summary>
        static readonly Color DefaultFaceColor = new Color(0f, 0.6293888f, 1f, 1f);

        /// <summary>
        /// プリセット Default で使う奥行き面の色です。
        /// </summary>
        static readonly Color DefaultDepthColor = new Color(0.23985404f, 0.353509f, 0.6603774f, 1f);

        /// <summary>
        /// プリセット Default で使う 2 段アウトライン設定です。
        /// </summary>
        static readonly OutlineSettings DefaultOutline =
            new OutlineSettings(
                new Color(1f, 0f, 0.19551277f, 1f),
                0.222f,
                new Color(0f, 0.8566521f, 0.9254902f, 1f),
                0.122f);

        /// <summary>
        /// プリセット Default で使うベベル設定です。
        /// </summary>
        static readonly EmbossSettings DefaultEmboss = new EmbossSettings(0.553f, -0.874f, 1f, 0f, 0.671f);

        /// <summary>
        /// プリセット Default で使うライティング設定です。
        /// </summary>
        static readonly LightingSettings DefaultLighting =
            new LightingSettings(Color.white, 15f, 2.57f, 3.8f, 0.333f, 0.448f);

        /// <summary>
        /// 表面色のプリセット候補です。
        /// </summary>
        public enum FaceColorPreset
        {
            /// <summary>既定の水色を使います。</summary>
            Default,
            /// <summary>白を使います。</summary>
            White,
            /// <summary>金色を使います。</summary>
            Gold,
            /// <summary>エメラルド色を使います。</summary>
            Emerald,
            /// <summary>マゼンタを使います。</summary>
            Magenta,
            /// <summary>カスタム色を使います。</summary>
            Custom
        }

        /// <summary>
        /// 文字の奥行き面に使う色のプリセット候補です。
        /// </summary>
        public enum InsideColorPreset
        {
            /// <summary>既定の青系の奥行き色を使います。</summary>
            Default,
            /// <summary>表面色を暗くした色を使います。</summary>
            [InspectorName("Match Face")]
            MatchFace,
            /// <summary>深い青を使います。</summary>
            [InspectorName("Deep Blue")]
            DeepBlue,
            /// <summary>黒に近いチャコール色を使います。</summary>
            Charcoal,
            /// <summary>暖色寄りの影色を使います。</summary>
            [InspectorName("Warm Shadow")]
            WarmShadow,
            /// <summary>カスタム色を使います。</summary>
            Custom
        }

        /// <summary>
        /// アウトラインのプリセット候補です。
        /// </summary>
        public enum OutlinePreset
        {
            /// <summary>既定の 2 段アウトラインを使います。</summary>
            Default,
            /// <summary>アウトラインを消します。</summary>
            None,
            /// <summary>細い黒アウトラインを使います。</summary>
            [InspectorName("Thin Black")]
            ThinBlack,
            /// <summary>細い白アウトラインを使います。</summary>
            [InspectorName("Thin White")]
            ThinWhite,
            /// <summary>ネオン調のシアンアウトラインを使います。</summary>
            [InspectorName("Neon Cyan")]
            NeonCyan,
            /// <summary>赤からピンク寄りのアウトラインを使います。</summary>
            [InspectorName("Red Pink")]
            RedPink,
            /// <summary>カスタムアウトライン設定を使います。</summary>
            Custom
        }

        /// <summary>
        /// ベベル表現のプリセット候補です。
        /// </summary>
        public enum EmbossPreset
        {
            /// <summary>既定の立体的なベベルを使います。</summary>
            Default,
            /// <summary>ベベルをほぼ無効にします。</summary>
            Flat,
            /// <summary>浅く柔らかいベベルを使います。</summary>
            Soft,
            /// <summary>丸みの強いベベルを使います。</summary>
            Rounded,
            /// <summary>深く強いベベルを使います。</summary>
            Deep,
            /// <summary>カスタムベベル設定を使います。</summary>
            Custom
        }

        /// <summary>
        /// ライティングのプリセット候補です。
        /// </summary>
        public enum LightingPreset
        {
            /// <summary>既定の光沢設定を使います。</summary>
            Default,
            /// <summary>光沢を抑えたマットな設定を使います。</summary>
            Matte,
            /// <summary>ほどよい反射の設定を使います。</summary>
            Balanced,
            /// <summary>強めの光沢設定を使います。</summary>
            Glossy,
            /// <summary>明暗差の大きい演出的な設定を使います。</summary>
            Dramatic,
            /// <summary>カスタムライティング設定を使います。</summary>
            Custom
        }

        /// <summary>
        /// 2 段アウトラインの色と幅をまとめて保持します。
        /// </summary>
        [Serializable]
        public struct OutlineSettings
        {
            /// <summary>
            /// 1 段目アウトラインの色です。
            /// </summary>
            public Color outlineColor;

            /// <summary>
            /// 1 段目アウトラインの幅です。
            /// </summary>
            [Range(0f, 1f)]
            public float outlineWidth;

            /// <summary>
            /// 2 段目アウトラインの色です。
            /// </summary>
            public Color outline2Color;

            /// <summary>
            /// 2 段目アウトラインの幅です。
            /// </summary>
            [Range(0f, 1f)]
            public float outline2Width;

            /// <summary>
            /// アウトライン設定を初期化します。
            /// </summary>
            /// <param name="outlineColor">1 段目アウトラインの色。</param>
            /// <param name="outlineWidth">1 段目アウトラインの幅。</param>
            /// <param name="outline2Color">2 段目アウトラインの色。</param>
            /// <param name="outline2Width">2 段目アウトラインの幅。</param>
            public OutlineSettings(Color outlineColor, float outlineWidth, Color outline2Color, float outline2Width)
            {
                this.outlineColor = outlineColor;
                this.outlineWidth = outlineWidth;
                this.outline2Color = outline2Color;
                this.outline2Width = outline2Width;
            }
        }

        /// <summary>
        /// SDF から作る疑似ベベルの見た目をまとめて保持します。
        /// </summary>
        [Serializable]
        public struct EmbossSettings
        {
            /// <summary>
            /// ベベルとして扱う SDF 範囲の広さです。
            /// </summary>
            [Range(0f, 1f)]
            public float bevelWidth;

            /// <summary>
            /// ベベルの中心を内側または外側へずらす量です。
            /// </summary>
            [Range(-1f, 1f)]
            public float bevelOffset;

            /// <summary>
            /// ベベル法線の強さです。
            /// </summary>
            [Range(0f, 1f)]
            public float bevelStrength;

            /// <summary>
            /// ベベル形状を丸める量です。
            /// </summary>
            [Range(0f, 1f)]
            public float bevelRoundness;

            /// <summary>
            /// ベベルの高さを削って平らにする量です。
            /// </summary>
            [Range(0f, 1f)]
            public float bevelClamp;

            /// <summary>
            /// ベベル設定を初期化します。
            /// </summary>
            /// <param name="bevelWidth">ベベル幅。</param>
            /// <param name="bevelOffset">ベベル中心のオフセット。</param>
            /// <param name="bevelStrength">ベベル強度。</param>
            /// <param name="bevelRoundness">ベベルの丸み。</param>
            /// <param name="bevelClamp">ベベル高さのクランプ量。</param>
            public EmbossSettings(float bevelWidth, float bevelOffset, float bevelStrength, float bevelRoundness, float bevelClamp)
            {
                this.bevelWidth = bevelWidth;
                this.bevelOffset = bevelOffset;
                this.bevelStrength = bevelStrength;
                this.bevelRoundness = bevelRoundness;
                this.bevelClamp = bevelClamp;
            }
        }

        /// <summary>
        /// Unlit シェーダーへ追加する疑似ライティングの値をまとめて保持します。
        /// </summary>
        [Serializable]
        public struct LightingSettings
        {
            /// <summary>
            /// スペキュラー反射に乗せる色です。
            /// </summary>
            public Color specularColor;

            /// <summary>
            /// 反射ハイライトの鋭さです。
            /// </summary>
            [Range(5f, 15f)]
            public float reflectivity;

            /// <summary>
            /// スペキュラー反射の強度です。
            /// </summary>
            [Range(0f, 4f)]
            public float specularPower;

            /// <summary>
            /// XY 平面上での擬似ライト角度です。
            /// </summary>
            [Range(0f, 6.28f)]
            public float lightAngle;

            /// <summary>
            /// 拡散光による陰影の強さです。
            /// </summary>
            [Range(0f, 1f)]
            public float diffuse;

            /// <summary>
            /// 最低限残す環境光の量です。
            /// </summary>
            [Range(0f, 1f)]
            public float ambient;

            /// <summary>
            /// ライティング設定を初期化します。
            /// </summary>
            /// <param name="specularColor">スペキュラー反射色。</param>
            /// <param name="reflectivity">反射ハイライトの鋭さ。</param>
            /// <param name="specularPower">スペキュラー反射の強度。</param>
            /// <param name="lightAngle">擬似ライトの角度。</param>
            /// <param name="diffuse">拡散光の影響量。</param>
            /// <param name="ambient">環境光の下限量。</param>
            public LightingSettings(Color specularColor, float reflectivity, float specularPower, float lightAngle, float diffuse, float ambient)
            {
                this.specularColor = specularColor;
                this.reflectivity = reflectivity;
                this.specularPower = specularPower;
                this.lightAngle = lightAngle;
                this.diffuse = diffuse;
                this.ambient = ambient;
            }
        }

        /// <summary>
        /// プリセットを適用する TextMeshPro コンポーネントです。
        /// </summary>
        [Header("Target")]
        [SerializeField]
        TextMeshPro targetText;

        /// <summary>
        /// 明示的に操作するマテリアルです。未指定なら TextMeshPro の共有フォントマテリアルを使います。
        /// </summary>
        [Tooltip("Optional material to drive. Leave empty to use the TextMeshPro font shared material.")]
        [SerializeField]
        Material targetMaterial;

        /// <summary>
        /// 再生中にオブジェクト専用マテリアルを作り、同一フォントを共有する他の文字へ変更が伝播しないようにします。
        /// </summary>
        [Tooltip("Creates a per-object material while playing so multiple Text3D objects can use different preset combinations.")]
        [SerializeField]
        bool useRuntimeMaterialInstance = true;

        /// <summary>
        /// 有効化時に自動でプリセットを適用するかどうかです。
        /// </summary>
        [SerializeField]
        bool applyOnEnable = true;

        /// <summary>
        /// エディットモードでも値変更時にプリセットを適用するかどうかです。
        /// </summary>
        [SerializeField]
        bool applyInEditMode = true;

        /// <summary>
        /// Animator や Timeline でプリセット値が毎フレーム変わる場合に、毎フレーム再適用します。
        /// </summary>
        [Tooltip("Enable this when Animator or Timeline changes the preset fields every frame.")]
        [SerializeField]
        bool applyEveryFrame;

        /// <summary>
        /// 現在選択している表面色プリセットです。
        /// </summary>
        [Header("Preset Dropdowns")]
        [SerializeField]
        FaceColorPreset faceColorPreset = FaceColorPreset.Default;

        /// <summary>
        /// 現在選択している奥行き色プリセットです。
        /// </summary>
        [SerializeField]
        InsideColorPreset insideColorPreset = InsideColorPreset.Default;

        /// <summary>
        /// 現在選択しているアウトラインプリセットです。
        /// </summary>
        [SerializeField]
        OutlinePreset outlinePreset = OutlinePreset.Default;

        /// <summary>
        /// 現在選択しているベベルプリセットです。
        /// </summary>
        [SerializeField]
        EmbossPreset embossPreset = EmbossPreset.Default;

        /// <summary>
        /// 現在選択しているライティングプリセットです。
        /// </summary>
        [SerializeField]
        LightingPreset lightingPreset = LightingPreset.Default;

        /// <summary>
        /// <see cref="FaceColorPreset.Custom"/> 選択時に使う表面色です。
        /// </summary>
        [Header("Custom Values")]
        [SerializeField]
        Color customFaceColor = DefaultFaceColor;

        /// <summary>
        /// <see cref="InsideColorPreset.Custom"/> 選択時に使う奥行き色です。
        /// </summary>
        [SerializeField]
        Color customInsideColor = DefaultDepthColor;

        /// <summary>
        /// <see cref="OutlinePreset.Custom"/> 選択時に使うアウトライン設定です。
        /// </summary>
        [SerializeField]
        OutlineSettings customOutline = DefaultOutline;

        /// <summary>
        /// <see cref="EmbossPreset.Custom"/> 選択時に使うベベル設定です。
        /// </summary>
        [SerializeField]
        EmbossSettings customEmboss = DefaultEmboss;

        /// <summary>
        /// <see cref="LightingPreset.Custom"/> 選択時に使うライティング設定です。
        /// </summary>
        [SerializeField]
        LightingSettings customLighting = DefaultLighting;

        /// <summary>
        /// 再生中に生成したオブジェクト専用マテリアルです。
        /// </summary>
        Material runtimeMaterial;

        /// <summary>
        /// 実行時マテリアルを解放するときに戻す元の共有マテリアルです。
        /// </summary>
        Material originalSharedMaterial;

        /// <summary>
        /// 現在プリセット適用対象として解決されるマテリアルを取得します。
        /// </summary>
        public Material TargetMaterial => ResolveTargetMaterial();

        /// <summary>
        /// コンポーネント追加時に対象 TextMeshPro を探し、現在のプリセットを即時適用します。
        /// </summary>
        void Reset()
        {
            CacheTargetText();
            ApplyPresets();
        }

        /// <summary>
        /// 有効化時に対象をキャッシュし、設定に応じてプリセットを適用します。
        /// </summary>
        void OnEnable()
        {
            CacheTargetText();

            if (applyOnEnable && (Application.isPlaying || applyInEditMode))
            {
                ApplyPresets();
            }
        }

        /// <summary>
        /// 毎フレーム適用が有効な場合に、アニメーションで変化したプリセット値を反映します。
        /// </summary>
        void Update()
        {
            if (Application.isPlaying && applyEveryFrame)
            {
                ApplyPresets();
            }
        }

        /// <summary>
        /// 無効化時に実行時専用マテリアルを破棄し、元の共有マテリアルへ戻します。
        /// </summary>
        void OnDisable()
        {
            ReleaseRuntimeMaterial();
        }

        /// <summary>
        /// インスペクター変更時に対象を補完し、エディットモード適用設定に応じて見た目を更新します。
        /// </summary>
        void OnValidate()
        {
            CacheTargetText();

            if (Application.isPlaying || applyInEditMode)
            {
                ApplyPresets();
            }
        }

        /// <summary>
        /// 現在のプリセットを解決済みの対象マテリアルへ適用します。
        /// </summary>
        [ContextMenu("Apply Presets")]
        public void ApplyPresets()
        {
            var material = ResolveTargetMaterial();
            if (material == null)
                return;

            ApplyPresets(material);
            SyncTargetText(material);
        }

        /// <summary>
        /// 指定したマテリアルへ現在のプリセット値を書き込みます。
        /// </summary>
        /// <param name="material">プリセットを適用するマテリアル。</param>
        public void ApplyPresets(Material material)
        {
            if (material == null)
                return;

            var faceColor = ResolveFaceColor();
            var outline = ResolveOutlineSettings();
            var emboss = ResolveEmbossSettings();
            var lighting = ResolveLightingSettings();

            SetColor(material, ColorId, faceColor);
            SetColor(material, DepthColorId, ResolveInsideColor(faceColor));
            ApplyOutline(material, outline);
            ApplyEmboss(material, emboss);
            ApplyLighting(material, lighting);
            UpdateOutlineKeyword(material, outline);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(material);
            }
#endif
        }

        /// <summary>
        /// すべてのプリセット種別をまとめて変更し、すぐにマテリアルへ反映します。
        /// </summary>
        /// <param name="face">表面色プリセット。</param>
        /// <param name="inside">奥行き色プリセット。</param>
        /// <param name="outline">アウトラインプリセット。</param>
        /// <param name="emboss">ベベルプリセット。</param>
        /// <param name="lighting">ライティングプリセット。</param>
        public void SetPresets(
            FaceColorPreset face,
            InsideColorPreset inside,
            OutlinePreset outline,
            EmbossPreset emboss,
            LightingPreset lighting)
        {
            faceColorPreset = face;
            insideColorPreset = inside;
            outlinePreset = outline;
            embossPreset = emboss;
            lightingPreset = lighting;
            ApplyPresets();
        }

        /// <summary>
        /// 表面色プリセットだけを変更し、すぐに反映します。
        /// </summary>
        /// <param name="preset">新しい表面色プリセット。</param>
        public void SetFaceColorPreset(FaceColorPreset preset)
        {
            faceColorPreset = preset;
            ApplyPresets();
        }

        /// <summary>
        /// 奥行き色プリセットだけを変更し、すぐに反映します。
        /// </summary>
        /// <param name="preset">新しい奥行き色プリセット。</param>
        public void SetInsideColorPreset(InsideColorPreset preset)
        {
            insideColorPreset = preset;
            ApplyPresets();
        }

        /// <summary>
        /// アウトラインプリセットだけを変更し、すぐに反映します。
        /// </summary>
        /// <param name="preset">新しいアウトラインプリセット。</param>
        public void SetOutlinePreset(OutlinePreset preset)
        {
            outlinePreset = preset;
            ApplyPresets();
        }

        /// <summary>
        /// ベベルプリセットだけを変更し、すぐに反映します。
        /// </summary>
        /// <param name="preset">新しいベベルプリセット。</param>
        public void SetEmbossPreset(EmbossPreset preset)
        {
            embossPreset = preset;
            ApplyPresets();
        }

        /// <summary>
        /// ライティングプリセットだけを変更し、すぐに反映します。
        /// </summary>
        /// <param name="preset">新しいライティングプリセット。</param>
        public void SetLightingPreset(LightingPreset preset)
        {
            lightingPreset = preset;
            ApplyPresets();
        }

        /// <summary>
        /// 明示マテリアル、実行時インスタンス、共有フォントマテリアルの順に適用先を決定します。
        /// </summary>
        /// <returns>プリセットを書き込む対象マテリアル。見つからない場合は null。</returns>
        Material ResolveTargetMaterial()
        {
            CacheTargetText();

            if (targetMaterial != null)
                return targetMaterial;

            if (Application.isPlaying && useRuntimeMaterialInstance)
                return EnsureRuntimeMaterial();

            return targetText != null ? targetText.fontSharedMaterial : null;
        }

        /// <summary>
        /// 参照が未設定の場合に同じ GameObject の TextMeshPro を取得します。
        /// </summary>
        void CacheTargetText()
        {
            if (targetText == null)
            {
                targetText = GetComponent<TextMeshPro>();
            }
        }

        /// <summary>
        /// マテリアルの差し替えと TextMeshPro の再描画フラグ更新を行います。
        /// </summary>
        /// <param name="material">TextMeshPro と MeshRenderer に同期するマテリアル。</param>
        void SyncTargetText(Material material)
        {
            if (targetText == null || material == null)
                return;

            if ((targetMaterial != null || material == runtimeMaterial) && targetText.fontSharedMaterial != material)
            {
                targetText.fontSharedMaterial = material;

                var meshRenderer = targetText.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sharedMaterial = material;
                }
            }

            targetText.havePropertiesChanged = true;
            targetText.UpdateMeshPadding();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(targetText);
            }
#endif
        }

        /// <summary>
        /// 再生中に必要な場合だけ共有フォントマテリアルから専用インスタンスを作成します。
        /// </summary>
        /// <returns>生成または再利用した実行時専用マテリアル。</returns>
        Material EnsureRuntimeMaterial()
        {
            if (runtimeMaterial != null)
                return runtimeMaterial;

            if (targetText == null || targetText.fontSharedMaterial == null)
                return null;

            originalSharedMaterial = targetText.fontSharedMaterial;
            runtimeMaterial = new Material(originalSharedMaterial)
            {
                name = $"{originalSharedMaterial.name} ({name} Preset)",
                hideFlags = HideFlags.DontSave
            };

            targetText.fontSharedMaterial = runtimeMaterial;

            var meshRenderer = targetText.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = runtimeMaterial;
            }

            return runtimeMaterial;
        }

        /// <summary>
        /// 実行時専用マテリアルを破棄し、TextMeshPro と MeshRenderer を元の共有マテリアルへ戻します。
        /// </summary>
        void ReleaseRuntimeMaterial()
        {
            if (runtimeMaterial == null)
                return;

            if (targetText != null && targetText.fontSharedMaterial == runtimeMaterial)
            {
                targetText.fontSharedMaterial = originalSharedMaterial;

                var meshRenderer = targetText.GetComponent<MeshRenderer>();
                if (meshRenderer != null && meshRenderer.sharedMaterial == runtimeMaterial)
                {
                    meshRenderer.sharedMaterial = originalSharedMaterial;
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(runtimeMaterial);
            }
            else
#endif
            {
                Destroy(runtimeMaterial);
            }

            runtimeMaterial = null;
            originalSharedMaterial = null;
        }

        /// <summary>
        /// 選択中の表面色プリセットを実際の色へ変換します。
        /// </summary>
        /// <returns>マテリアルへ設定する表面色。</returns>
        Color ResolveFaceColor()
        {
            switch (faceColorPreset)
            {
                case FaceColorPreset.White:
                    return Color.white;
                case FaceColorPreset.Gold:
                    return new Color(1f, 0.66f, 0.12f, 1f);
                case FaceColorPreset.Emerald:
                    return new Color(0.08f, 0.92f, 0.58f, 1f);
                case FaceColorPreset.Magenta:
                    return new Color(1f, 0.08f, 0.48f, 1f);
                case FaceColorPreset.Custom:
                    return customFaceColor;
                case FaceColorPreset.Default:
                default:
                    return DefaultFaceColor;
            }
        }

        /// <summary>
        /// 選択中の奥行き色プリセットを実際の色へ変換します。
        /// </summary>
        /// <param name="faceColor">Match Face 選択時の基準になる表面色。</param>
        /// <returns>マテリアルへ設定する奥行き面の色。</returns>
        Color ResolveInsideColor(Color faceColor)
        {
            switch (insideColorPreset)
            {
                case InsideColorPreset.MatchFace:
                    return Color.Lerp(faceColor, Color.black, 0.45f);
                case InsideColorPreset.DeepBlue:
                    return new Color(0.04f, 0.12f, 0.34f, 1f);
                case InsideColorPreset.Charcoal:
                    return new Color(0.035f, 0.04f, 0.05f, 1f);
                case InsideColorPreset.WarmShadow:
                    return new Color(0.46f, 0.18f, 0.08f, 1f);
                case InsideColorPreset.Custom:
                    return customInsideColor;
                case InsideColorPreset.Default:
                default:
                    return DefaultDepthColor;
            }
        }

        /// <summary>
        /// 選択中のアウトラインプリセットを具体的なアウトライン設定へ変換します。
        /// </summary>
        /// <returns>マテリアルへ設定するアウトライン設定。</returns>
        OutlineSettings ResolveOutlineSettings()
        {
            switch (outlinePreset)
            {
                case OutlinePreset.None:
                    return new OutlineSettings(Color.clear, 0f, Color.clear, 0f);
                case OutlinePreset.ThinBlack:
                    return new OutlineSettings(Color.black, 0.08f, Color.clear, 0f);
                case OutlinePreset.ThinWhite:
                    return new OutlineSettings(Color.white, 0.07f, Color.clear, 0f);
                case OutlinePreset.NeonCyan:
                    return new OutlineSettings(
                        new Color(0f, 0.95f, 1f, 1f),
                        0.16f,
                        new Color(0.03f, 0.35f, 1f, 1f),
                        0.08f);
                case OutlinePreset.RedPink:
                    return new OutlineSettings(
                        new Color(1f, 0.05f, 0.22f, 1f),
                        0.18f,
                        new Color(1f, 0.55f, 0.08f, 1f),
                        0.05f);
                case OutlinePreset.Custom:
                    return customOutline;
                case OutlinePreset.Default:
                default:
                    return DefaultOutline;
            }
        }

        /// <summary>
        /// 選択中のベベルプリセットを具体的なベベル設定へ変換します。
        /// </summary>
        /// <returns>マテリアルへ設定するベベル設定。</returns>
        EmbossSettings ResolveEmbossSettings()
        {
            switch (embossPreset)
            {
                case EmbossPreset.Flat:
                    return new EmbossSettings(0f, 0f, 0f, 0f, 0f);
                case EmbossPreset.Soft:
                    return new EmbossSettings(0.14f, -0.12f, 0.35f, 0.55f, 0.08f);
                case EmbossPreset.Rounded:
                    return new EmbossSettings(0.28f, -0.24f, 0.65f, 0.9f, 0.18f);
                case EmbossPreset.Deep:
                    return new EmbossSettings(0.62f, -0.78f, 1f, 0.2f, 0.58f);
                case EmbossPreset.Custom:
                    return customEmboss;
                case EmbossPreset.Default:
                default:
                    return DefaultEmboss;
            }
        }

        /// <summary>
        /// 選択中のライティングプリセットを具体的なライティング設定へ変換します。
        /// </summary>
        /// <returns>マテリアルへ設定するライティング設定。</returns>
        LightingSettings ResolveLightingSettings()
        {
            switch (lightingPreset)
            {
                case LightingPreset.Matte:
                    return new LightingSettings(Color.white, 8f, 0.25f, 3.8f, 0.2f, 0.75f);
                case LightingPreset.Balanced:
                    return new LightingSettings(Color.white, 10f, 1.25f, 3.3f, 0.35f, 0.45f);
                case LightingPreset.Glossy:
                    return new LightingSettings(Color.white, 15f, 3f, 3.8f, 0.5f, 0.35f);
                case LightingPreset.Dramatic:
                    return new LightingSettings(new Color(1f, 0.95f, 0.82f, 1f), 14f, 3.5f, 5.4f, 0.75f, 0.15f);
                case LightingPreset.Custom:
                    return customLighting;
                case LightingPreset.Default:
                default:
                    return DefaultLighting;
            }
        }

        /// <summary>
        /// アウトライン関連のシェーダープロパティへ値を安全に書き込みます。
        /// </summary>
        /// <param name="material">更新対象のマテリアル。</param>
        /// <param name="settings">適用するアウトライン設定。</param>
        static void ApplyOutline(Material material, OutlineSettings settings)
        {
            SetColor(material, OutlineColorId, settings.outlineColor);
            SetFloat(material, OutlineWidthId, Mathf.Clamp01(settings.outlineWidth));
            SetColor(material, Outline2ColorId, settings.outline2Color);
            SetFloat(material, Outline2WidthId, Mathf.Clamp01(settings.outline2Width));
        }

        /// <summary>
        /// ベベル関連のシェーダープロパティへ値を安全に書き込みます。
        /// </summary>
        /// <param name="material">更新対象のマテリアル。</param>
        /// <param name="settings">適用するベベル設定。</param>
        static void ApplyEmboss(Material material, EmbossSettings settings)
        {
            SetFloat(material, BevelWidthId, Mathf.Clamp01(settings.bevelWidth));
            SetFloat(material, BevelOffsetId, Mathf.Clamp(settings.bevelOffset, -1f, 1f));
            SetFloat(material, BevelId, Mathf.Clamp01(settings.bevelStrength));
            SetFloat(material, BevelRoundnessId, Mathf.Clamp01(settings.bevelRoundness));
            SetFloat(material, BevelClampId, Mathf.Clamp01(settings.bevelClamp));
        }

        /// <summary>
        /// ライティング関連のシェーダープロパティへ値を安全に書き込みます。
        /// </summary>
        /// <param name="material">更新対象のマテリアル。</param>
        /// <param name="settings">適用するライティング設定。</param>
        static void ApplyLighting(Material material, LightingSettings settings)
        {
            SetColor(material, SpecularColorId, settings.specularColor);
            SetFloat(material, ReflectivityId, Mathf.Clamp(settings.reflectivity, 5f, 15f));
            SetFloat(material, SpecularPowerId, Mathf.Clamp(settings.specularPower, 0f, 4f));
            SetFloat(material, LightAngleId, Mathf.Clamp(settings.lightAngle, 0f, 6.28f));
            SetFloat(material, DiffuseId, Mathf.Clamp01(settings.diffuse));
            SetFloat(material, AmbientId, Mathf.Clamp01(settings.ambient));
        }

        /// <summary>
        /// アウトライン幅に応じて OUTLINE_ON キーワードを切り替えます。
        /// </summary>
        /// <param name="material">更新対象のマテリアル。</param>
        /// <param name="settings">アウトラインの幅情報を含む設定。</param>
        static void UpdateOutlineKeyword(Material material, OutlineSettings settings)
        {
            if (settings.outlineWidth > 0.0001f || settings.outline2Width > 0.0001f)
            {
                material.EnableKeyword("OUTLINE_ON");
            }
            else
            {
                material.DisableKeyword("OUTLINE_ON");
            }
        }

        /// <summary>
        /// プロパティが存在する場合だけ色を設定し、別シェーダー使用時のエラーを避けます。
        /// </summary>
        /// <param name="material">更新対象のマテリアル。</param>
        /// <param name="propertyId">色プロパティの ID。</param>
        /// <param name="value">設定する色。</param>
        static void SetColor(Material material, int propertyId, Color value)
        {
            if (material.HasProperty(propertyId))
            {
                material.SetColor(propertyId, value);
            }
        }

        /// <summary>
        /// プロパティが存在する場合だけ数値を設定し、別シェーダー使用時のエラーを避けます。
        /// </summary>
        /// <param name="material">更新対象のマテリアル。</param>
        /// <param name="propertyId">float プロパティの ID。</param>
        /// <param name="value">設定する値。</param>
        static void SetFloat(Material material, int propertyId, float value)
        {
            if (material.HasProperty(propertyId))
            {
                material.SetFloat(propertyId, value);
            }
        }
    }
}
