using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NariEffect.Text3D
{
	/// <summary>
	/// Text3D の 1 文字ごとの押し出し厚みと奥行き色の参照範囲を保持します。
	/// </summary>
	/// <remarks>
	/// TMP3D IKaroon の MIT ライセンス実装をもとに、現在の Text3D 名へ移行しています。
	/// https://github.com/Ikaroon/TMP3D
	/// </remarks>
	[MovedFrom(true, sourceNamespace: "NariEffect.Script", sourceClassName: "TMP3D_CharacterInfo")]
	public struct Text3DGlyphThickness
	{
		/// <summary>
		/// 文字をローカル Z 方向へ押し出す厚みです。
		/// </summary>
		public float Thickness => thickness;

		/// <summary>
		/// 奥行き方向の進行度を深度色テクスチャへ割り当てる範囲です。
		/// </summary>
		public Vector2 DepthRange => depthRange;

		/// <summary>
		/// 旧 API 互換用の厚み取得プロパティです。新規コードでは <see cref="Thickness"/> を使います。
		/// </summary>
		[Obsolete("Use Thickness instead.")]
		public float Depth => Thickness;

		/// <summary>
		/// 旧 API 互換用の奥行き範囲取得プロパティです。新規コードでは <see cref="DepthRange"/> を使います。
		/// </summary>
		[Obsolete("Use DepthRange instead.")]
		public Vector2 DepthMapping => DepthRange;

		/// <summary>
		/// 文字単位で保持する押し出し厚みです。
		/// </summary>
		private float thickness;

		/// <summary>
		/// 文字単位で保持する奥行き色の参照範囲です。
		/// </summary>
		private Vector2 depthRange;

		/// <summary>
		/// 文字の厚みと奥行き色の参照範囲を初期化します。
		/// </summary>
		/// <param name="thickness">ローカル Z 方向へ押し出す厚み。</param>
		/// <param name="depthRange">奥行き進行度を色へ割り当てる最小値と最大値。</param>
		public Text3DGlyphThickness(float thickness, Vector2 depthRange)
		{
			this.thickness = thickness;
			this.depthRange = depthRange;
		}

		/// <summary>
		/// 文字の押し出し厚みを更新します。
		/// </summary>
		/// <param name="thickness">新しい厚み。</param>
		public void SetGlyphThickness(float thickness)
		{
			this.thickness = thickness;
		}

		/// <summary>
		/// 文字の奥行き色参照範囲を更新します。
		/// </summary>
		/// <param name="depthRange">新しい奥行き色参照範囲。</param>
		public void SetGlyphDepthRange(Vector2 depthRange)
		{
			this.depthRange = depthRange;
		}

		/// <summary>
		/// 旧 API 互換用の厚み更新メソッドです。新規コードでは <see cref="SetGlyphThickness"/> を使います。
		/// </summary>
		/// <param name="depth">新しい厚み。</param>
		[Obsolete("Use SetGlyphThickness instead.")]
		public void SetDepth(float depth)
		{
			SetGlyphThickness(depth);
		}

		/// <summary>
		/// 旧 API 互換用の奥行き範囲更新メソッドです。新規コードでは <see cref="SetGlyphDepthRange"/> を使います。
		/// </summary>
		/// <param name="depthMapping">新しい奥行き色参照範囲。</param>
		[Obsolete("Use SetGlyphDepthRange instead.")]
		public void SetDepthMapping(Vector2 depthMapping)
		{
			SetGlyphDepthRange(depthMapping);
		}
	}
}
