/*************************************************************************
 * KAWASEBLUR SHADER (v1.0.1)
 * by Reev the Chameleon
 * 11 Nov 2
**************************************************************************
For use with Dual Kawase Blur Algorithm as proposed by Marius Bjorge, ARM
Reference: https://community.arm.com/cfs-file/__key/communityserver-blogs-components-weblogfiles/00-00-00-20-66/siggraph2015_2D00_mmg_2D00_marius_2D00_notes.pdf
Note: Downsample pass can also be used with normal Kawase Blur, where iteration
can be fractional number, in which case the global shader property "KawaseBlue_weight"
is used to perform interpolation. It should be set to 1.0f if iteration is whole number.
When used as downsample pass of Dual Kawase Blur, it should be set to 0.25f for best result.

Update v1.0.1: Fix shader name typo and set parameters to make blur smoother
*/

Shader "chm_Shader/URPRendererFeature/KawaseBlur"{
	Properties{
		[MainTexture] _MainTex("Texture",2D) = "white"{}
	}
	SubShader{
		LOD 100
		
		Pass{
			Name "Downsample"

			HLSLPROGRAM
			#pragma vertex vertexShader
			#pragma fragment fragmentShader
			#include "UnityCG.cginc"

			struct VertToFrag{
				float4 clipPos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;

			//global shader properties
			int KawaseBlur_offsetPixel;
			float KawaseBlur_weight = 1.0f;

			VertToFrag vertexShader(
				float4 objectPos : POSITION,
				float2 uv : TEXCOORD0
			){
				VertToFrag v2f;
				v2f.clipPos = UnityObjectToClipPos(objectPos);
				v2f.uv = uv;
				return v2f;
			}

			half4 fragmentShader(
				VertToFrag v2f
			) : SV_TARGET
			{
				//<textureName>_TexelSize (Credit: AllanSamurai, UA)
				//x contains 1.0/width
				//y contains 1.0/height
				//z contains width
				//w contains height
				float2 offset = _MainTex_TexelSize.xy*(KawaseBlur_offsetPixel-0.5);
				half4 result = half4(0,0,0,0);
				result += tex2D(_MainTex,v2f.uv+float2(-offset.x,-offset.y));
				result += tex2D(_MainTex,v2f.uv+float2(-offset.x,offset.y));
				result += tex2D(_MainTex,v2f.uv+float2(offset.x,-offset.y));
				result += tex2D(_MainTex,v2f.uv+float2(offset.x,offset.y));
				result *= KawaseBlur_weight;
				result += tex2D(_MainTex,v2f.uv);
				return result/(4*KawaseBlur_weight+1);
			}
			ENDHLSL
		}

		Pass{
			Name "Upsample"

			HLSLPROGRAM
			#pragma vertex vertexShader
			#pragma fragment fragmentShader
			#include "UnityCG.cginc"

			struct VertToFrag{
				float4 clipPos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;

			//global shader properties
			int KawaseBlur_offsetPixel;

			VertToFrag vertexShader(
				float4 objectPos : POSITION,
				float2 uv : TEXCOORD0
			){
				VertToFrag v2f;
				v2f.clipPos = UnityObjectToClipPos(objectPos);
				v2f.uv = uv;
				return v2f;
			}

			half4 fragmentShader(
				VertToFrag v2f
			) : SV_TARGET
			{
				float2 offset = _MainTex_TexelSize.xy*(KawaseBlur_offsetPixel-0.5);
				half4 result = half4(0,0,0,0);
				result += tex2D(_MainTex,v2f.uv+float2(-offset.x,-offset.y))*2;
				result += tex2D(_MainTex,v2f.uv+float2(-offset.x,offset.y))*2;
				result += tex2D(_MainTex,v2f.uv+float2(offset.x,-offset.y))*2;
				result += tex2D(_MainTex,v2f.uv+float2(offset.x,offset.y))*2;
				result += tex2D(_MainTex,v2f.uv+float2(0,2*offset.y));
				result += tex2D(_MainTex,v2f.uv+float2(0,-2*offset.y));
				result += tex2D(_MainTex,v2f.uv+float2(2*offset.x,0));
				result += tex2D(_MainTex,v2f.uv+float2(-2*offset.x,0));
				return result/12;
			}
			ENDHLSL
		}
	}
}
