/*************************************************************************
 * SPICESHADER (v1.0)
 * by Reev the Chameleon
 * 11 May 2
**************************************************************************
Unlit shader for rendering rectangular portion of the entire texture.
*/

Shader "chm_Shader/SpliceShader"{
	Properties{
		[NoScaleOffset][MainTexture] matTexture("Texture",2D) = "white"{}
		[MainColor] matTint("Tint",Color) = (1,1,1,1)
		[MatVector2Int] matTotalRowCol("Total Row/Column",Vector) = (1,1,0,0)
		[MatVector2Int] matIndex("Image Index",Vector) = (1,1,0,0)
	}
	SubShader{
		Tags{"Queue" = "Transparent"}
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha
		LOD 100
		Pass{
			Name "Main"

			HLSLPROGRAM
			#pragma vertex vertexShader
			#pragma fragment fragmentShader
			#include "UnityCG.cginc"
			//for Universal Render Pipelines
			//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct VertToFrag{
				float4 clipPos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			sampler2D matTexture;
			half4 matTint;
			float2 matTotalRowCol;
			float2 matIndex;

			VertToFrag vertexShader(
				float4 objectPos : POSITION,
				float2 uv : TEXCOORD0
			){
				VertToFrag v2f;
				v2f.clipPos = UnityObjectToClipPos(objectPos);
				//For Universal Render Pipelines
				//float3 worldPos = TransformObjectToWorld(objectPos.xyz);
				//v2f.clipPos = TransformWorldToHClip(worldPos);
				v2f.uv = float2(
					(matIndex.y + uv.x) / matTotalRowCol.y,
					(matTotalRowCol.x-1 - matIndex.x + uv.y) / matTotalRowCol.x
					//So row runs top to bottom
				);
				return v2f;
			}
			half4 fragmentShader(
				VertToFrag v2f
			) : SV_TARGET
			{
				return tex2D(matTexture,v2f.uv) * matTint;
			}
			ENDHLSL
		}
	}

	Fallback "Hidden/Universal Render Pipeline/FallbackError"
	Fallback "Hidden/InternalErrorShader"
}
