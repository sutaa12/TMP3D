using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using System.Collections.Generic;
using Unity.Collections;

namespace Ikaroon.TMP3D
{
	[ExecuteInEditMode, RequireComponent(typeof(TextMeshPro))]
	public class TMP3D_Handler : MonoBehaviour
	{
		public TextMeshPro TMP { get { return m_tmp; } }
		[SerializeField, HideInInspector]
		TextMeshPro m_tmp;

		[SerializeField]
		float m_defaultDepth = 1;

		[SerializeField]
		ComputeShader m_vertexGeneratorCompute;

		[SerializeField]
		bool m_useComputeShader = false;

		List<TMP3D_CharacterInfo> m_tmp3DCharacters = new List<TMP3D_CharacterInfo>();
		List<Vector4> m_cachedMeshUVs = new List<Vector4>();

		// Compute shader related
		GraphicsBuffer m_vertexBuffer;
		GraphicsBuffer m_indexBuffer;
		GraphicsBuffer m_inputVertexBuffer;
		GraphicsBuffer m_characterDataBuffer;
		
		Material m_computeMaterial;
		int m_computeCharacterCount;
		int m_totalVertexCount;
		int m_totalIndexCount;
		
		struct VertexData
		{
			public Vector3 position;
			public Vector3 normal;
			public Vector4 color;
			public Vector2 texcoord0;
			public Vector2 texcoord1;
			public Vector4 texcoord2;
			public Vector4 boundariesUV;
			public Vector4 boundariesLocal;
			public Vector4 boundariesLocalZ;
		}

		struct CharacterData
		{
			public float depth;
			public Vector2 depthMapping;
			public float padding;
		}

		void OnEnable()
		{
			m_tmp = GetComponent<TextMeshPro>();
			TMPro_EventManager.TEXT_CHANGED_EVENT.Add(ON_TEXT_CHANGED);
		}

		void OnDisable()
		{
			TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(ON_TEXT_CHANGED);
			ReleaseBuffers();
			DestroyComputeMaterial();
		}

		void OnDestroy()
		{
			ReleaseBuffers();
			DestroyComputeMaterial();
		}

		void ReleaseBuffers()
		{
			m_vertexBuffer?.Release();
			m_indexBuffer?.Release();
			m_inputVertexBuffer?.Release();
			m_characterDataBuffer?.Release();
			
			m_vertexBuffer = null;
			m_indexBuffer = null;
			m_inputVertexBuffer = null;
			m_characterDataBuffer = null;
			m_computeCharacterCount = 0;
			m_totalVertexCount = 0;
			m_totalIndexCount = 0;
		}

		void DestroyComputeMaterial()
		{
			if (m_computeMaterial == null)
				return;

			if (Application.isPlaying)
				Destroy(m_computeMaterial);
			else
				DestroyImmediate(m_computeMaterial);

			m_computeMaterial = null;
		}

		void ON_TEXT_CHANGED(Object obj)
		{
			if (obj != m_tmp || m_tmp == null)
				return;

			if (m_tmp.textInfo == null)
				return;

			// Remove old characters from 3D data
			for (int i = m_tmp3DCharacters.Count - 1; i >= m_tmp.textInfo.characterCount; i--)
			{
				m_tmp3DCharacters.RemoveAt(i);
			}

			// Add new characters to 3D data
			for (int i = m_tmp3DCharacters.Count; i < m_tmp.textInfo.characterCount; i++)
			{
				m_tmp3DCharacters.Add(new TMP3D_CharacterInfo(m_defaultDepth, new Vector2(0, 1)));
			}

			if (m_useComputeShader && m_vertexGeneratorCompute != null && SystemInfo.supportsComputeShaders)
			{
				UpdateComputeBuffers();
			}
			else
			{
				UpdateMeshValues();
			}
		}

		void UpdateComputeBuffers()
		{
			if (m_tmp.textInfo == null || m_tmp.textInfo.meshInfo.Length == 0)
			{
				ReleaseBuffers();
				return;
			}

			// For simplicity, we'll handle the first submesh
			var meshInfo = m_tmp.textInfo.meshInfo[0];
			var mesh = meshInfo.mesh;
			
			if (mesh == null)
			{
				ReleaseBuffers();
				return;
			}

			int verticesPerChar = 4; // Quad vertices
			int verticesPerBox = 24; // 6 faces * 4 vertices

			var visibleCharacterIndices = new List<int>();
			for (int i = 0; i < m_tmp.textInfo.characterCount; i++)
			{
				var charInfo = m_tmp.textInfo.characterInfo[i];
				if (!charInfo.isVisible || charInfo.materialReferenceIndex != 0)
					continue;

				if (charInfo.vertexIndex < 0 || charInfo.vertexIndex + verticesPerChar > mesh.vertexCount)
					continue;

				visibleCharacterIndices.Add(i);
			}

			int characterCount = visibleCharacterIndices.Count;
			if (characterCount == 0 || mesh.vertexCount == 0)
			{
				ReleaseBuffers();
				return;
			}
			
			// Release old buffers
			ReleaseBuffers();

			m_computeCharacterCount = characterCount;
			m_totalVertexCount = characterCount * verticesPerBox;
			m_totalIndexCount = characterCount * 36; // 6 faces * 2 triangles * 3 indices
			int inputVertexCount = characterCount * verticesPerChar;

			if (m_totalVertexCount <= 0 || m_totalIndexCount <= 0 || inputVertexCount <= 0)
			{
				ReleaseBuffers();
				return;
			}

			// Create buffers
			m_vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_totalVertexCount, System.Runtime.InteropServices.Marshal.SizeOf<VertexData>());
			m_indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_totalIndexCount, sizeof(uint));
			
			// Prepare input data
			var inputVertices = new NativeArray<VertexData>(inputVertexCount, Allocator.Temp);
			var characterData = new NativeArray<CharacterData>(characterCount, Allocator.Temp);
			
			// Fill input vertex data
			var vertices = mesh.vertices;
			var normals = mesh.normals;
			var colors = mesh.colors32;
			var uv0 = mesh.uv;
			var uv1 = mesh.uv2;
			
			mesh.GetUVs(2, m_cachedMeshUVs);
			
			for (int i = 0; i < characterCount; i++)
			{
				var charInfo = m_tmp.textInfo.characterInfo[visibleCharacterIndices[i]];
				int sourceVertexIndex = charInfo.vertexIndex;

				for (int j = 0; j < verticesPerChar; j++)
				{
					int sourceIndex = sourceVertexIndex + j;
					int targetIndex = i * verticesPerChar + j;

					var vertData = new VertexData();
					vertData.position = vertices[sourceIndex];
					vertData.normal = sourceIndex < normals.Length ? normals[sourceIndex] : Vector3.back;
					// Color32をVector4に変換
					Color32 c = sourceIndex < colors.Length ? colors[sourceIndex] : new Color32(255, 255, 255, 255);
					vertData.color = new Vector4(
						c.r / 255f,
						c.g / 255f,
						c.b / 255f,
						c.a / 255f
					);
					vertData.texcoord0 = sourceIndex < uv0.Length ? uv0[sourceIndex] : Vector2.zero;
					vertData.texcoord1 = sourceIndex < uv1.Length ? uv1[sourceIndex] : Vector2.zero;
					
					if (sourceIndex < m_cachedMeshUVs.Count)
						vertData.texcoord2 = m_cachedMeshUVs[sourceIndex];
					
					inputVertices[targetIndex] = vertData;
				}
			}
			
			// Fill character data
			for (int i = 0; i < characterCount; i++)
			{
				var charData = new CharacterData();
				int characterIndex = visibleCharacterIndices[i];
				if (characterIndex < m_tmp3DCharacters.Count)
				{
					charData.depth = m_tmp3DCharacters[characterIndex].m_depth;
					charData.depthMapping = m_tmp3DCharacters[characterIndex].m_depthMapping;
				}
				else
				{
					charData.depth = m_defaultDepth;
					charData.depthMapping = new Vector2(0, 1);
				}
				characterData[i] = charData;
			}
			
			m_inputVertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, inputVertexCount, System.Runtime.InteropServices.Marshal.SizeOf<VertexData>());
			m_characterDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, characterCount, System.Runtime.InteropServices.Marshal.SizeOf<CharacterData>());
			
			m_inputVertexBuffer.SetData(inputVertices);
			m_characterDataBuffer.SetData(characterData);
			
			inputVertices.Dispose();
			characterData.Dispose();
			
			// Execute compute shader
			GenerateBoxVertices();
			
			// Update material to use compute buffers
			UpdateComputeMaterial();
		}

		void GenerateBoxVertices()
		{
			if (m_vertexGeneratorCompute == null || m_computeCharacterCount <= 0)
				return;
			
			int kernel;
			try
			{
				kernel = m_vertexGeneratorCompute.FindKernel("GenerateBoxVertices");
			}
			catch
			{
				ReleaseBuffers();
				return;
			}
			
			m_vertexGeneratorCompute.SetBuffer(kernel, "_InputVertices", m_inputVertexBuffer);
			m_vertexGeneratorCompute.SetBuffer(kernel, "_CharacterData", m_characterDataBuffer);
			m_vertexGeneratorCompute.SetBuffer(kernel, "_OutputVertices", m_vertexBuffer);
			m_vertexGeneratorCompute.SetBuffer(kernel, "_OutputIndices", m_indexBuffer);
			
			m_vertexGeneratorCompute.SetInt("_CharacterCount", m_computeCharacterCount);
			m_vertexGeneratorCompute.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);
			m_vertexGeneratorCompute.SetMatrix("_WorldToObject", transform.worldToLocalMatrix);
			
			int threadGroups = Mathf.CeilToInt(m_computeCharacterCount / 32.0f);
			m_vertexGeneratorCompute.Dispatch(kernel, threadGroups, 1, 1);
		}

		void UpdateComputeMaterial()
		{
			if (m_computeMaterial == null)
			{
				var shader = Shader.Find("TextMeshPro/3D/Compute");
				if (shader != null && shader.isSupported)
				{
					m_computeMaterial = new Material(shader);
				}
			}
    
			if (m_computeMaterial != null && m_tmp.fontSharedMaterial != null)
			{
				// Copy properties from TMP material
				m_computeMaterial.CopyPropertiesFromMaterial(m_tmp.fontSharedMaterial);
        
				// 両方のバッファを設定
				m_computeMaterial.SetBuffer("_VertexBuffer", m_vertexBuffer);
				m_computeMaterial.SetBuffer("_IndexBuffer", m_indexBuffer);
			}
		}

		public void SetDepth(int characterIndex, float depth)
		{
			if (characterIndex < 0 || characterIndex >= m_tmp3DCharacters.Count)
				return;

			var data = m_tmp3DCharacters[characterIndex];
			data.m_depth = depth;
			m_tmp3DCharacters[characterIndex] = data;
		}

		public void SetDepthMapping(int characterIndex, Vector2 depthMapping)
		{
			if (characterIndex < 0 || characterIndex >= m_tmp3DCharacters.Count)
				return;

			var data = m_tmp3DCharacters[characterIndex];
			data.m_depthMapping = depthMapping;
			m_tmp3DCharacters[characterIndex] = data;
		}

		public void UpdateMeshValues()
		{
			if (m_tmp == null || m_tmp.textInfo == null)
				return;

			var count = Mathf.Min(m_tmp3DCharacters.Count, m_tmp.textInfo.characterCount);

			for (int i = 0; i < m_tmp.textInfo.meshInfo.Length; i++)
			{
				var meshInfo = m_tmp.textInfo.meshInfo[i];
				var mesh = meshInfo.mesh;

				if (mesh == null)
					continue;

				// UV2のリストを取得して初期化
				m_cachedMeshUVs.Clear();
				mesh.GetUVs(2, m_cachedMeshUVs);

				// メッシュの頂点数に合わせてリストのサイズを確保
				// UV2が空の場合は、UV0から初期化
				if (m_cachedMeshUVs.Count == 0)
				{
					mesh.GetUVs(0, m_cachedMeshUVs);
					// UV0のデータをVector4に変換して初期化
					for (int j = 0; j < m_cachedMeshUVs.Count; j++)
					{
						m_cachedMeshUVs[j] = new Vector4(m_cachedMeshUVs[j].x, m_cachedMeshUVs[j].y, 0, 0);
					}
				}

				// メッシュの頂点数に合わせてリストを拡張（必要な場合）
				int requiredSize = mesh.vertexCount;
				while (m_cachedMeshUVs.Count < requiredSize)
				{
					m_cachedMeshUVs.Add(Vector4.zero);
				}

				int lastVertexIndex = -1;
				for (int j = 0; j < count; j++)
				{
					var charInfo = m_tmp.textInfo.characterInfo[j];
					int meshIndex = charInfo.materialReferenceIndex;
					if (meshIndex != i)
						continue;

					int vertexIndex = charInfo.vertexIndex;

					if (lastVertexIndex > vertexIndex)
						continue;

					lastVertexIndex = vertexIndex;
					var underlineIndex = charInfo.underlineVertexIndex;
					var strikethroughIndex = charInfo.strikethroughVertexIndex;

					var tmp3DChar = m_tmp3DCharacters[j];
					var tmp3DData = new Vector4(tmp3DChar.m_depth, tmp3DChar.m_depthMapping.x,
						tmp3DChar.m_depthMapping.y, 0);

					// 基本の4頂点を設定（境界チェック付き）
					for (int k = 0; k < 4; k++)
					{
						int idx = vertexIndex + k;
						if (idx < m_cachedMeshUVs.Count)
						{
							m_cachedMeshUVs[idx] = tmp3DData;
						}
					}

					// アンダーラインの頂点を設定（境界チェック付き）
					if (underlineIndex != vertexIndex && underlineIndex >= 0)
					{
						for (int k = 0; k < 12; k++)
						{
							int idx = underlineIndex + k;
							if (idx < m_cachedMeshUVs.Count)
							{
								m_cachedMeshUVs[idx] = tmp3DData;
							}
						}
					}

					// ストライクスルーの頂点を設定（境界チェック付き）
					if (strikethroughIndex != vertexIndex && strikethroughIndex >= 0)
					{
						for (int k = 0; k < 12; k++)
						{
							int idx = strikethroughIndex + k;
							if (idx < m_cachedMeshUVs.Count)
							{
								m_cachedMeshUVs[idx] = tmp3DData;
							}
						}
					}
				}

				// 修正したUV2データをメッシュに設定
				mesh.SetUVs(2, m_cachedMeshUVs);
			}
		}

		void OnRenderObject()
		{
			if (m_useComputeShader && m_computeMaterial != null && m_vertexBuffer != null && m_indexBuffer != null && m_totalIndexCount > 0)
			{
				// インデックスバッファを使用した描画
				m_computeMaterial.SetBuffer("_VertexBuffer", m_vertexBuffer);
				m_computeMaterial.SetBuffer("_IndexBuffer", m_indexBuffer);
				m_computeMaterial.SetPass(0);
        
				// DrawProceduralNowでインデックス数を指定
				// （頂点数ではなくインデックス数を渡す）
				Graphics.DrawProceduralNow(MeshTopology.Triangles, m_totalIndexCount, 1);
			}
		}

		private void OnValidate()
		{
			m_tmp = GetComponent<TextMeshPro>();
			m_tmp3DCharacters.Clear();
			ON_TEXT_CHANGED(m_tmp);
		}
	}
}
