using Ikaroon.TMP3D;
using UnityEditor;
using UnityEngine;

namespace Ikaroon.TMP3DEditor
{
	[CustomEditor(typeof(TMP3D_Handler))]
	public class TMP3D_HandlerEditor : Editor
	{
		TMP3D_Handler m_handler;
		
		SerializedProperty m_defaultDepthProp;
		SerializedProperty m_useComputeShaderProp;
		SerializedProperty m_vertexGeneratorComputeProp;

		private void OnEnable()
		{
			m_handler = (TMP3D_Handler)target;
			
			m_defaultDepthProp = serializedObject.FindProperty("m_defaultDepth");
			m_useComputeShaderProp = serializedObject.FindProperty("m_useComputeShader");
			m_vertexGeneratorComputeProp = serializedObject.FindProperty("m_vertexGeneratorCompute");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			
			// Default depth setting
			EditorGUILayout.PropertyField(m_defaultDepthProp, new GUIContent("Default Depth"));
			
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Rendering Mode", EditorStyles.boldLabel);
			
			// Toggle between geometry shader and compute shader
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(m_useComputeShaderProp, new GUIContent("Use Compute Shader"));
			
			if (m_useComputeShaderProp.boolValue)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(m_vertexGeneratorComputeProp, new GUIContent("Vertex Generator Compute"));
				
				if (m_vertexGeneratorComputeProp.objectReferenceValue == null)
				{
					EditorGUILayout.HelpBox("Vertex Generator Compute Shader is required when using Compute Shader mode.", MessageType.Warning);
					
					if (GUILayout.Button("Auto-assign Default Compute Shader"))
					{
						// Try to find the default compute shader
						var computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/TMP3D/Runtime/Shaders/TMP3D_VertexGenerator.compute");
						if (computeShader == null)
						{
							// Try alternative paths
							string[] guids = AssetDatabase.FindAssets("TMP3D_VertexGenerator t:ComputeShader");
							if (guids.Length > 0)
							{
								string path = AssetDatabase.GUIDToAssetPath(guids[0]);
								computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
							}
						}
						
						if (computeShader != null)
						{
							m_vertexGeneratorComputeProp.objectReferenceValue = computeShader;
						}
						else
						{
							EditorUtility.DisplayDialog("Compute Shader Not Found", 
								"Could not find TMP3D_VertexGenerator.compute. Please assign it manually.", "OK");
						}
					}
				}
				
				EditorGUI.indentLevel--;
				
				EditorGUILayout.Space();
				EditorGUILayout.HelpBox("Compute Shader mode generates 3D geometry using GPU compute for better performance with complex text.", MessageType.Info);
			}
			else
			{
				EditorGUILayout.HelpBox("Geometry Shader mode uses traditional geometry shaders for 3D text generation.", MessageType.Info);
			}
			
			if (EditorGUI.EndChangeCheck())
			{
				// Force refresh when switching modes
				if (m_handler.TMP != null)
				{
					m_handler.TMP.SetVerticesDirty();
				}
			}
			
			serializedObject.ApplyModifiedProperties();
			
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Font Asset", EditorStyles.boldLabel);
			
			// Original font asset conversion functionality
			var fontAsset = m_handler.TMP.font;
			EditorGUI.BeginDisabledGroup(fontAsset.material.shader.name.Contains(TMP3D_Data.SHADER_NAME_SPACE));
			if (GUILayout.Button(new GUIContent("Create 3D Font Asset Variant")))
			{
				Undo.RecordObject(m_handler.TMP, "Converted Font Asset to 3D");
				var newFont = TMP3D_Data.ConvertFontAssetTo3D(fontAsset);
				m_handler.TMP.font = newFont;
			}
			EditorGUI.EndDisabledGroup();
			
			// Debug information
			if (Application.isPlaying)
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Debug Info", EditorStyles.boldLabel);
				EditorGUILayout.LabelField($"Character Count: {m_handler.TMP.textInfo.characterCount}");
				EditorGUILayout.LabelField($"Rendering Mode: {(m_useComputeShaderProp.boolValue ? "Compute Shader" : "Geometry Shader")}");
			}
		}
	}
}