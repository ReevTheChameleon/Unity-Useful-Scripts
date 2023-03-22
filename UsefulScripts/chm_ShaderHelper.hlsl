#ifndef CHM_SHADERHELPER_
#define CHM_SHADERHELPER_

/*************************************************************************
 * CHM_SHADERHELPER (v1.1.2)
 * by Reev the Chameleon
 * 13 Mar 3
**************************************************************************
#include for this file:
"Packages/com.chameleonplayground.usefulscripts/UsefulScripts/chm_ShaderHelper.hlsl"

Useful functions for writing shaders
Update v1.0.1: Add minor functions and add support for perlinNoise in WebGL build
Update v1.1: Add functions to calculate rotation, spherical coordinate conversion,
3D Perlin Noise, and Fresnel factor and Fresnel reflection
Update v1.1.1: Add conditional compilation support for built-in render pipeline
Update v1.1.2: Modify Perlin hash function for WebGL to reduce predictable pattern

** NOTE: This include file is written focused on URP. To use this include file with
built-in render pipeline, #define BUILTIN_SHADER in your shader. Will consider
whether and how to make it more compatible with built-in and other pipelines later. **
*/

#ifndef BUILTIN_SHADER
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
//PI, TWO_PI, and FOUR_PI were #defined in this include
#else
#include "UnityCG.cginc"
#endif

//==================================================================================
// USEFUL MATH FUNCTIONS
//==================================================================================
/* I have found some problems with static const, and also due to the fact that it
allocates memory, I have decided to change these to #define */
#define CHM_PI			3.1415926535897932384626433832795
#define CHM_2PI			6.283185307179586476925286766559
#define CHM_4PI			12.566370614359172953850573533118

#define CHM_SQRT2		1.4142135623730950488016887242097
#define CHM_COS22_5		0.92387953251128675612818318939679
#define CHM_COS67_5		0.3826834323650897717284599840304
#define CHM_RAD2DEG		57.295779513082320876798154814105
#define CHM_DEG2RAD		(1/CHM_RAD2DEG)

inline float remap(float value,float2 input,float2 output){ //credit: Balint, SO
	return output.x + (output.y-output.x)*(value-input.x)/(input.y-input.x);
}

inline float remap01(float value,float2 output){
	return output.x + value*(output.y-output.x);
}

inline float remapTo01(float value,float2 input){
	return (value-input.x)/(input.y-input.x);
}

inline half3 remapColor(half3 color,float2 output){
	float outputRange = output.y-output.x;
	return half3(
		output.x + color.r*outputRange,
		output.x + color.g*outputRange,
		output.x + color.b*outputRange
	);
}

/* Translate can be done by vector addition */
/* Scale can be done by vector dot product */
inline float2 rotate(float2 uv,float radian){
	float cosAngle = cos(radian);
	float sinAngle = sin(radian);
	return float2(
		cosAngle*uv.x - sinAngle*uv.y,
		sinAngle*uv.x + cosAngle*uv.y
	);
}

//Credit: elseforty, UF & achille hui, mathstackexchange.com
/* use w as quaternion real part */
inline float4 quaternionProduct(float4 q1,float4 q2){
	return float4(
		q1.w*q2.xyz + q2.w*q1.xyz + cross(q1.xyz,q2.xyz),
		q1.w*q2.w - dot(q1.xyz,q2.xyz)
	);
}

float3 rotate(float3 v,float3 vAxis,float radian){
	/* vAxis has to be unit vector */
	float sinTheta = sin(radian/2);
	float4 q = float4( //credit: Doug, mathstackexchange.com
		cos(radian/2),
		sinTheta*vAxis.x,
		sinTheta*vAxis.y,
		sinTheta*vAxis.z
	);
	float4 qInv = q*float4(-1,-1,-1,1);
	return quaternionProduct(quaternionProduct(q,float4(v,0)),qInv).xyz;
}

inline float2 shear(float2 uv,float x,float y=0){
	return float2(
		uv.x + x*uv.y,
		uv.y + y*uv.x
	);
}

/* for usual smoothstep function, use HLSL smoothstep */
inline float easing(float f){ //6f^5-15f^4+10f^3
	return f*f*f*(f*(6*f-15)+10);
}

inline float3 sphericalToCartesian(float3 rhy){
	float sinTheta = sin(rhy.y);
	return rhy.x * float3(
		sinTheta * cos(rhy.z),
		cos(rhy.y),
		sinTheta * sin(rhy.z)
	);
}

inline float3 cartesianToSpherical(float3 xyz){ //phi range [-pi,pi)
	float r = length(xyz);
	return float3(
		r,
		acos(xyz.y/r),
		atan2(xyz.z,xyz.x)
	);
}

//==================================================================================
// COLOR BLEND
//==================================================================================
/* TODO: Research how to correctly deal with alpha */
inline half4 blendLighten(half4 colorA,half4 colorB){
	return colorA + colorB - colorA*colorB;
	//return 1-(1-colorA)*(1-colorB); //Credit: Robert Thomas, photoblogstop.com
}

//==================================================================================
// SHORTHAND FUNCTIONS
//==================================================================================
inline float getZBufferDepth(sampler2D _CameraDepthTexture,float4 computedScreenPos){
	/* Usage:
	- Declare _CameraDepthTexture as sampler2D and pass it in as first argument.
	Unity will have automatically filled it with z buffer.
	- Use ComputeScreenPos on clipPos to convert it to [0,1] range and pass it in
	as second argument.
	- If you want to use this function in fragment shader, in vertex shader,
	use ComputeScreenPos on clipPos to convert it to [0,1] range
	then save it as float4:TEXCOORDx and use that. Directly reading from clipPos in
	fragment shader will produce incorrect result. */
	#ifndef BUILTIN_SHADER
	return LinearEyeDepth(
		tex2D(_CameraDepthTexture,computedScreenPos.xy/computedScreenPos.w).r,
		_ZBufferParams
	);
	#else
	return LinearEyeDepth(
		tex2D(_CameraDepthTexture,computedScreenPos.xy/computedScreenPos.w).r
	);
	#endif
	/* _ProjectionParams is (Credit: ifurkend, UF & cyanilux.com)
	x = 1 or -1 (-1 if projection is flipped)
	y = near plane
	z = far plane
	w = 1/far plane
	*/
}

inline float getViewDepth(float4 computedScreenPos){
	/* Again, if used in fragment shader, do not use clipPos directly */
	return computedScreenPos.w;
	/* Below is also correct and show how z and depth is related */
	//return LinearEyeDepth(
	//	computedScreenPos.z/computedScreenPos.w,
	//	_ZBufferParams
	//);
}

inline float3 adjustWorldNormalByNormal(float3 normal,
	float3 worldTangent,float3 worldBitangent,float3 worldNormal)
{
	return normalize(float3(
		worldTangent.x * normal.x +
		worldBitangent.x * normal.y +
		worldNormal.x * normal.z
		,
		worldTangent.y * normal.x +
		worldBitangent.y * normal.y +
		worldNormal.y * normal.z
		,
		worldTangent.z * normal.x +
		worldBitangent.z * normal.y +
		worldNormal.z * normal.z
	));
}

float3 adjustWorldNormalByNormalMap(sampler2D normalMap,float2 uv,
	float3 worldTangent,float3 worldBitangent,float3 worldNormal,float normalMapStrength=1)
{
	/* More accurate version */
	//float3 normalMapNormal = lerp(
	//	float3(0,0,1),
	//	UnpackNormal(tex2D(matNormalMap,v2f.uv)),
	//	matNormalMapStrength
	//);
	/* Cheap version */
	float3 normalMapNormal = UnpackNormal(tex2D(normalMap,uv));
	normalMapNormal.xy *= normalMapStrength;

	return adjustWorldNormalByNormal(
		normalMapNormal,
		worldTangent,
		worldBitangent,
		worldNormal
	);
}

float3 normalFromHeight(float3 worldPos,float3 worldNormal,float height){
	/* An implementation of equation (4) in "Surface Gradient-Based Bump Mapping
	Framework" by Morten S. Mikkelsen in "Journal of Computer Graphics Techniques
	Vol.9 No.3, 2020". The published paper can be found at:
	https://jcgt.org/published/0009/03/04/paper.pdf */
	
	/* Note: ddx and ddy is as cheap as subtraction (Credit: HappyCoder, gamedev.net)
	As this function uses ddx and ddy, it can ONLY be used in fragment shader. */
	float3 dWorldPosdx = ddx(worldPos);
	float3 dWorldPosdy = ddy(worldPos);
	float3 nCrossdWorldPosdy = cross(worldNormal,dWorldPosdy);
	float3 nCrossdWorldPosdx = cross(worldNormal,dWorldPosdx);
				
	return normalize(worldNormal +
		(nCrossdWorldPosdy*ddx(height) - nCrossdWorldPosdx*ddy(height))/
		dot(nCrossdWorldPosdx,dWorldPosdy)
	);
	/* Original form below has extra cross product, which can be elided by using
	vector triple product property */
	//return normalize(v2f.vertexWorldNormal+
	//	(cross(v2f.vertexWorldNormal,dWorldPosdy)*ddx(noise) +
	//		cross(dWorldPosdx,v2f.vertexWorldNormal)*ddy(noise))/
	//	dot(v2f.vertexWorldNormal,cross(dWorldPosdx,dWorldPosdy)));
}

//==================================================================================
// NOISES
//==================================================================================
/* TODO: Implement Simplex Noise (but I really think it is not that faster though) */

//--------------------------------------------------------------------------------
// PERLIN NOISE
//--------------------------------------------------------------------------------
/* Note: Return value from PerlinNoise functions is in range [-1,1], but there is
very small chance it will touch those extreme value, so instead of using
(noise+1)/2, one might consider using something like (noise+0.5) so that
there exists some regionw of pure black and pure white. This also seems to be what
Unity does (ShaderGraph Gradient Noise Node documenatation).
*/

/* Ken Perlin's hash array (taken from https://adrianb.io/2014/08/09/perlinnoise.html) */
static const int permutation[] = {151,160,137,91,90,15,
	131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
	190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,
	88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
	77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,
	102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
	135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,
	5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
	223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,
	129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
	251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,
	49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
	138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180,
	151,160,137,91,90,15,
	131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
	190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,
	88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
	77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,
	102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
	135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,
	5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
	223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,
	129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
	251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,
	49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
	138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180,
};

inline uint perlinHash256(uint x,uint y){
	/* It seems that const value CAN'T be used when compiling shader for WebGL, so
	Perlin's hash array above will be ignored. Hence this has to be done other way
	(presumably more expensive) */
	/* Note: Current algorithm has symmetrical problem. Will study and revise later. */
	/* Below seems to be canonical 2D random function for some time,
	although some claims its distribution is not very good (Credit: appas, SO) */
	#ifdef WEBGL
	return frac(sin(12.9898*x+78.233*y)*43758.5453) * 256;
	#endif
	return permutation[permutation[x&0xFF] + y&0xFF];
}
inline uint perlinHash256(uint x,uint y,uint z){
	#ifdef WEBGL
	return frac(sin(12.9898*x+78.233*y+50*z)*43758.5453123) * 256;
	#endif
	return permutation[permutation[permutation[x&0xFF] + y&0xFF] + z&0xFF];
}

/* return [0,1] inclusive */
inline float perlinHash01(uint x,uint y){
	return (float)perlinHash256(x,y) / 255;
}

#if PERLIN_FAST
inline float perlinInfluence2D(uint gridX,uint gridY,float fracX,float fracY){
	switch(perlinHash256(gridX,gridY) % 4){
		case 0: return fracX;
		case 1: return fracY;
		case 2: return -fracX;
		case 3: return -fracY;
		default: return 0;
	}
}
#else
inline float perlinInfluence2D(uint gridX,uint gridY,float fracX,float fracY){
	switch(perlinHash256(gridX,gridY) % 8){
		case 0: return fracX;
		case 1: return fracY;
		case 2: return -fracX;
		case 3: return -fracY;
		case 4: return (fracX+fracY)/CHM_SQRT2;
		case 5: return (-fracX+fracY)/CHM_SQRT2;
		case 6: return (-fracX-fracY)/CHM_SQRT2;
		case 7: return (fracX-fracY)/CHM_SQRT2;
		//case 8: return CHM_COS22_5*fracX+COS67_5*fracY;
		//case 9: return CHM_COS67_5*fracX+COS22_5*fracY;
		//case 10: return -CHM_COS67_5*fracX+COS22_5*fracY;
		//case 11: return -CHM_COS22_5*fracX+COS67_5*fracY;
		//case 12: return -CHM_COS22_5*fracX-COS67_5*fracY;
		//case 13: return -CHM_COS67_5*fracX-COS22_5*fracY;
		//case 14: return CHM_COS67_5*fracX-COS22_5*fracY;
		//case 15: return CHM_COS22_5*fracX-COS67_5*fracY;
		default: return 0;
	}
}
#endif
/* This tries to make it really random, but I don't think its appearance is much better... */
//float perlinInfluence2D(uint gridX,uint gridY,float fracX,float fracY){
//	float hash = perlinHash256(gridX&0xFF,gridY&0xFF);
//	return fracX*cos(hash) + fracY*sin(hash);
//}

float perlinInfluence3D(uint gridX,uint gridY,uint gridZ,float fracX,float fracY,float fracZ){
	/* I read somewhere suggestion to use 8 corner and 2x12 faces to make things
	divisible by some 2^n power, but have not implemented them back then. Now I don't
	remember the source, only the method... */
	switch(perlinHash256(gridX,gridY,gridZ) %32){
		//faces
		case 0: case 12: return fracX+fracY; //(1,1,0)
		case 1: case 13: return fracX-fracY; //(1,-1,0)
		case 2: case 14: return fracX+fracZ; //(1,0,1)
		case 3: case 15: return fracX-fracZ; //(1,0,-1)
		case 4: case 16: return fracY+fracZ; //(0,1,1)
		case 5: case 17: return fracY-fracZ; //(0,1,-1)
		case 6: case 18: return -fracY+fracZ; //(0,-1,1)
		case 7: case 19: return -fracY-fracZ; //(0,-1,-1)
		case 8: case 20: return -fracX+fracY; //(-1,1,0)
		case 9: case 21: return -fracX-fracY; //(-1,-1,0)
		case 10: case 22: return -fracX+fracZ; //(-1,0,1)
		case 11: case 23: return -fracX-fracZ; //(-1,0,-1)

		//vertices
		case 24: return fracX+fracY+fracZ; //(1,1,1)
		case 25: return fracX+fracY-fracZ; //(1,1,-1)
		case 26: return fracX-fracY+fracZ; //(1,-1,1)
		case 27: return fracX-fracY-fracZ; //(1,-1,-1)
		case 28: return -fracX+fracY+fracZ; //(-1,1,1)
		case 29: return -fracX+fracY-fracZ; //(-1,1,-1)
		case 30: return -fracX-fracY+fracZ; //(-1,-1,1)
		case 31: return -fracX-fracY-fracZ; //(-1,-1,-1)
	}
}

/* Return noise in range of [-1,1] */
/* Initial seed should be input as offset to the uv */
float perlinNoise(float2 uv){
	/* Equivalent would be HLSL floor function. uint does not work if uv becomes
	negative, which problem is obvious when rotating uv. */
	int gridX = uv.x<0 ? uv.x-1 : uv.x;
	int gridY = uv.y<0 ? uv.y-1 : uv.y;
	float fracX = uv.x-gridX;
	float fracY = uv.y-gridY;
	int gridXNext = gridX+1;
	int gridYNext = gridY+1;

	return 
		lerp(
			lerp(
				perlinInfluence2D(gridX,gridY,fracX,fracY),
				perlinInfluence2D(gridXNext,gridY,fracX-1,fracY),
				easing(fracX)
			),
			lerp(
				perlinInfluence2D(gridX,gridYNext,fracX,fracY-1),
				perlinInfluence2D(gridXNext,gridYNext,fracX-1,fracY-1),
				easing(fracX)
			),
			easing(fracY)
		)
	;
}

float perlinNoise(float2 uv,uint octave){
	//Credit Idea: Adrian Biagioli (Flafla2)
	/* Each octave add noise of twice frequency but with half amplitude
	following the spirit of Fourier series. */
	float noise = perlinNoise(uv);
	float amplitude = 1.0f;
	[unroll(8)] for(uint i=1; i<octave; ++i){
		uv *= 2;
		amplitude /= 2;
		noise += amplitude*perlinNoise(uv);
	}
	return noise;
}

/* Return noise in range of [-1,1] */
/* Initial seed should be input as offset to the uv */
float perlinNoise(float3 uvw){
	int gridX = uvw.x<0 ? uvw.x-1 : uvw.x;
	int gridY = uvw.y<0 ? uvw.y-1 : uvw.y;
	int gridZ = uvw.z<0 ? uvw.z-1 : uvw.z;
	float fracX = uvw.x-gridX;
	float fracY = uvw.y-gridY;
	float fracZ = uvw.z-gridZ;
	int gridXNext = gridX+1;
	int gridYNext = gridY+1;
	int gridZNext = gridZ+1;

	return 
		lerp(
			lerp(
				lerp(
					perlinInfluence3D(gridX,gridY,gridZ,fracX,fracY,fracZ),
					perlinInfluence3D(gridXNext,gridY,gridZ,fracX-1,fracY,fracZ),
					easing(fracX)
				),
				lerp(
					perlinInfluence3D(gridX,gridYNext,gridZ,fracX,fracY-1,fracZ),
					perlinInfluence3D(gridXNext,gridYNext,gridZ,fracX-1,fracY-1,fracZ),
					easing(fracX)
				),
				easing(fracY)
			),
			lerp(
				lerp(
					perlinInfluence3D(gridX,gridY,gridZNext,fracX,fracY,fracZ-1),
					perlinInfluence3D(gridXNext,gridY,gridZNext,fracX-1,fracY,fracZ-1),
					easing(fracX)
				),
				lerp(
					perlinInfluence3D(gridX,gridYNext,gridZNext,fracX,fracY-1,fracZ-1),
					perlinInfluence3D(gridXNext,gridYNext,gridZNext,fracX-1,fracY-1,fracZ-1),
					easing(fracX)
				),
				easing(fracY)
			),
			easing(fracZ)
		)
	;
}

float perlinNoise(float3 uvw,uint octave){
	float noise = perlinNoise(uvw);
	float amplitude = 1.0f;
	[unroll(8)] for(uint i=1; i<octave; ++i){
		uvw *= 2;
		amplitude /= 2;
		noise += amplitude*perlinNoise(uvw);
	}
	return noise;
}

float perlinNoiseRepeat(float2 uv,uint repeat,float2 seed=float2(0,0)){
	int gridX = uv.x<0 ? uv.x-1 : uv.x;
	int gridY = uv.y<0 ? uv.y-1 : uv.y;
	float fracX = uv.x-gridX;
	float fracY = uv.y-gridY;
	gridX %= repeat;
	gridY %= repeat;
	int gridXNext = (gridX+1) % repeat;
	int gridYNext = (gridY+1) % repeat;
	if(seed.x!=0 || seed.y!=0){
		gridX += seed.x;
		gridY += seed.y;
		gridXNext += seed.x;
		gridYNext += seed.y;
	}
	return 
		lerp( //lerp is HLSL function
			lerp(
				perlinInfluence2D(gridX,gridY,fracX,fracY),
				perlinInfluence2D(gridXNext,gridY,fracX-1,fracY),
				easing(fracX)
			),
			lerp(
				perlinInfluence2D(gridX,gridYNext,fracX,fracY-1),
				perlinInfluence2D(gridXNext,gridYNext,fracX-1,fracY-1),
				easing(fracX)
			),
			easing(fracY)
		)
	;
}

float perlinNoiseRepeat(float2 uv,uint repeat,uint octave,float2 seed=float2(0,0)){
	//Credit Idea: Adrian Biagioli (Flafla2)
	/* Each octave add noise of twice frequency but with half amplitude
	following the spirit of Fourier series. */
	float noise = perlinNoiseRepeat(uv,repeat,seed);
	float amplitude = 1.0f;
	for(uint i=1; i<octave; ++i){
		uv *= 2;
		amplitude /= 2;
		repeat *= 2;
		noise += amplitude*perlinNoiseRepeat(uv,repeat,seed);
	}
	return noise;
}

float perlinNoiseRepeat(float3 uvw,uint repeat,float3 seed=float3(0,0,0)){
	int gridX = uvw.x<0 ? uvw.x-1 : uvw.x;
	int gridY = uvw.y<0 ? uvw.y-1 : uvw.y;
	int gridZ = uvw.z<0 ? uvw.z-1 : uvw.z;
	float fracX = uvw.x-gridX;
	float fracY = uvw.y-gridY;
	float fracZ = uvw.z-gridZ;
	gridX %= repeat;
	gridY %= repeat;
	gridZ %= repeat;
	int gridXNext = (gridX+1) % repeat;
	int gridYNext = (gridY+1) % repeat;
	int gridZNext = (gridZ+1) % repeat;
	if(seed.x!=0 || seed.y!=0 || seed.z!=0){
		gridX += seed.x;
		gridY += seed.y;
		gridZ += seed.z;
		gridXNext += seed.x;
		gridYNext += seed.y;
		gridZNext += seed.z;
	}
	return 
		lerp(
			lerp(
				lerp(
					perlinInfluence3D(gridX,gridY,gridZ,fracX,fracY,fracZ),
					perlinInfluence3D(gridXNext,gridY,gridZ,fracX-1,fracY,fracZ),
					easing(fracX)
				),
				lerp(
					perlinInfluence3D(gridX,gridYNext,gridZ,fracX,fracY-1,fracZ),
					perlinInfluence3D(gridXNext,gridYNext,gridZ,fracX-1,fracY-1,fracZ),
					easing(fracX)
				),
				easing(fracY)
			),
			lerp(
				lerp(
					perlinInfluence3D(gridX,gridY,gridZNext,fracX,fracY,fracZ-1),
					perlinInfluence3D(gridXNext,gridY,gridZNext,fracX-1,fracY,fracZ-1),
					easing(fracX)
				),
				lerp(
					perlinInfluence3D(gridX,gridYNext,gridZNext,fracX,fracY-1,fracZ-1),
					perlinInfluence3D(gridXNext,gridYNext,gridZNext,fracX-1,fracY-1,fracZ-1),
					easing(fracX)
				),
				easing(fracY)
			),
			easing(fracZ)
		)
	;
}

float perlinNoiseRepeat(float3 uvw,uint repeat,uint octave,float3 seed=float3(0,0,0)){
	//Credit Idea: Adrian Biagioli (Flafla2)
	/* Each octave add noise of twice frequency but with half amplitude
	following the spirit of Fourier series. */
	float noise = perlinNoiseRepeat(uvw,repeat,seed);
	float amplitude = 1.0f;
	for(uint i=1; i<octave; ++i){
		uvw *= 2;
		amplitude /= 2;
		repeat *= 2;
		noise += amplitude*perlinNoiseRepeat(uvw,repeat,seed);
	}
	return noise;
}

//--------------------------------------------------------------------------------
// VORONOI NOISE
//--------------------------------------------------------------------------------
inline float2 voronoiCenter(uint gridX,uint gridY,float centerRange=1){
	uint hash = perlinHash256(gridX,gridY);
	#if VORONOI_NO_CENTERRANGE
	return float2(hash/16,hash%16)/16;
	#else
	return (float2(hash/16,hash%16)/16 - 0.5) * centerRange + 0.5;
	#endif
}

float voronoiNoiseNearestDistance(float2 uv,out uint2 nearestGrid,float centerRange=1){
	int gridX = uv.x<0 ? uv.x-1 : uv.x;
	int gridY = uv.y<0 ? uv.y-1 : uv.y;
	float2 fracXY = float2(uv.x-gridX,uv.y-gridY);

	float minDistance = 2;
	[unroll] for(int j=-1; j<=1; ++j){
		[unroll] for(int i=-1; i<=1; ++i){
			float distanceToCell = distance(
				voronoiCenter(gridX+i,gridY+j,centerRange)+float2(i,j),
				fracXY
			);
			if(distanceToCell < minDistance){
				minDistance = distanceToCell;
				nearestGrid = int2(gridX+i,gridY+j);
			}
		}
	}
	return minDistance;
}

float voronoiNoiseEdgeDistance(float2 uv,out uint2 nearestGrid,float centerRange=1){
	int gridX = uv.x<0 ? uv.x-1 : uv.x;
	int gridY = uv.y<0 ? uv.y-1 : uv.y;
	float2 fracXY = float2(uv.x-gridX,uv.y-gridY);

	float2 nearestCenter;
	float minDistance = 2;
	[unroll] for(int j=-1; j<=1; ++j){
		[unroll] for(int i=-1; i<=1; ++i){
			float2 center = voronoiCenter(gridX+i,gridY+j,centerRange)+float2(i,j);
			float distanceToCell = distance(
				center,
				fracXY
			);
			if(distanceToCell <= minDistance){
				minDistance = distanceToCell;
				nearestCenter = center;
				nearestGrid = uint2(gridX+i,gridY+j);
			}
		}
	}

	/* Another rough approximation which can be done in single pass is to simply note
	the second nearest center and use vCenter=nearestCenter2-nearestCenter1.
	However, there can be issue where nearest edge is nearer than nearest center
	especially at the corner. Hence, to get the most accurate lines, best approach is
	to simply do another pass. (Credit: ronja-tutorials.com & inigo quilez, iquilezles.org) */
	float2 vFromCenter = fracXY - nearestCenter;
	float minEdgeDistance = 2;
	/* Because of unrolling, need another index name (Credit: ronja-tutorials.com) */
	[unroll] for(int j2=-1; j2<=1; ++j2){
		[unroll] for(int i2=-1; i2<=1; ++i2){
			//Possible to reuse voronoiCenter from previous pass?
			float2 vCenter = 
				voronoiCenter(gridX+i2,gridY+j2,centerRange)+float2(i2,j2) -
				nearestCenter
			;
			float lenCenter = length(vCenter);
			float distanceToEdge = 
				lenCenter/2 - dot(vFromCenter,vCenter)/lenCenter;
			if(distanceToEdge < minEdgeDistance)
				minEdgeDistance = distanceToEdge;
		}
	}
	return minEdgeDistance;
}

#ifndef BUILTIN_SHADER
//==================================================================================
// GENERAL LIGHTING FUNCTIONS
//==================================================================================
/* These functions does NOT calculate attenuation. */
inline half3 diffuseColorLambert(
	half3 lightColor,float3 lightDirection,float3 pixelWorldNormal)
{
	return lightColor * max(0,dot(pixelWorldNormal,lightDirection));
}

float3 halfVector(float3 lightDirection,float3 pixelWorldPos){
	return normalize(
		lightDirection +
		normalize(GetWorldSpaceViewDir(pixelWorldPos))
		//normalize(_WorldSpaceCameraPos-pixelWorldPos)
	);
}

/* This have not taken into account dependency on angle of incidence (Fresnel Factor) */
half3 specularColorBlinnPhong(
	half3 lightColor,float3 halfVector,float3 pixelWorldNormal,float specularExponent)
{
	static const float EIGHT_PI = 25.132741228718345907701147066236;
	return
		(2+specularExponent)/EIGHT_PI *
		lightColor *
		pow(max(0,dot(pixelWorldNormal,halfVector)),specularExponent)
	;
}

float fresnelFactorFull(float3 lightDirection,float3 halfVector,
	float refractiveIndex1,float refractiveIndex2)
{
	//Should be able to optimize more
	float cosThetaI = dot(lightDirection,halfVector);
	float cosThetaT = sqrt(
		(refractiveIndex2*refractiveIndex2 -
		refractiveIndex1*refractiveIndex1*(1-cosThetaI*cosThetaI))
	) / refractiveIndex2; //using Snell's Law
	float n1cosThetaI = refractiveIndex1*cosThetaI;
	float n2cosThetaT = refractiveIndex2*cosThetaT;
	float n1cosThetaT = refractiveIndex1*cosThetaT;
	float n2cosThetaI = refractiveIndex2*cosThetaI;
	float rPerpendicular = (n1cosThetaI-n2cosThetaT)/(n1cosThetaI+n2cosThetaT);
	float rParallel = (n1cosThetaT-n2cosThetaI)/(n1cosThetaT+n2cosThetaI);
	return (rPerpendicular*rPerpendicular+rParallel*rParallel)/2;
}

// R=R0+(1-R0)(1-cos(angle))^5
inline float fresnelFactorSchlick(float3 lightDirection,float3 halfVector,
	float RNormalIncidence=0)
{
	float f = 1-saturate(dot(lightDirection,halfVector));
	float f2 = f*f;
	float lerpFactor = f2*f2*f;
	return lerp(RNormalIncidence,1,lerpFactor);
}

inline float fresnelFactorSchlick(float3 lightDirection,float3 halfVector,
	float offset,float exponent)
{
	return lerp(offset,1,pow(1-saturate(dot(lightDirection,halfVector)),exponent));
}

//==================================================================================
// LIGHTING.HLSL HELPER
//==================================================================================
inline half additionalLightInverseRangeSquare(uint index){
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
	return _AdditionalLightsBuffer[GetPerObjectLightIndex(index)].attenuation.x;
#else
	return _AdditionalLightsAttenuation[GetPerObjectLightIndex(index)].x;
#endif
}

inline float4 additionalLightPosition(uint index){
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
	return _AdditionalLightsBuffer[GetPerObjectLightIndex(index)].position;
#else
	return _AdditionalLightsPosition[GetPerObjectLightIndex(index)];
#endif
}

inline half3 ambientColorSphericalHarmonic9(float3 pixelWorldNormal){
	return SampleSH(pixelWorldNormal);
}

//==================================================================================
// SIMPLE LIGHT MODEL
//==================================================================================
// Not including Fresnel Factor in specular reflection
half3 simpleLitColor(
	half3 matColor,float3 pixelWorldPos,float3 pixelWorldNormal,
	float diffuseCoeff,float specularCoeff,float specularExponent,
	half3 matSpecularColor=half3(1,1,1))
{
	/* One should ensure to normalize pixelWorldNormal if it was passed directly
	from vertex shader. */
	float3 vHalf = halfVector(_MainLightPosition.xyz,pixelWorldPos);
	half3 diffuseColor =
		diffuseColorLambert(
			_MainLightColor.xyz,
			_MainLightPosition.xyz,
			pixelWorldNormal
		) +
		ambientColorSphericalHarmonic9(pixelWorldNormal);
	;
	half3 specularColor = 
		specularColorBlinnPhong(
			_MainLightColor.xyz,
			vHalf,
			pixelWorldNormal,
			specularExponent
		)
	;
	int lightCount = GetAdditionalLightsCount();
	for(int i=0; i<lightCount; ++i){
		Light light = GetAdditionalLight(i,pixelWorldPos);
		vHalf = halfVector(light.direction,pixelWorldPos);
		diffuseColor += light.distanceAttenuation * (
			diffuseColorLambert(
				light.color,
				light.direction,
				pixelWorldNormal
			)
		);
		specularColor += light.distanceAttenuation * (
			specularColorBlinnPhong(
				light.color,
				vHalf,
				pixelWorldNormal,
				specularExponent
			)
		);
	}
	return
		matColor*diffuseCoeff*diffuseColor +
		specularCoeff*specularColor*matSpecularColor
	;
}
#endif

//==================================================================================
// FUNCTIONS FROM UNITYCG.CGINC
// These functions are taken/adapted from "UnityCG.cginc" because they are useful even
// when working in other render pipelines, but cannot be found elsewhere.
// Taken from: https://github.com/chsxf/unity-built-in-shaders/blob/master/Shaders/CGIncludes/UnityCG.cginc
// Under MIT License: https://github.com/chsxf/unity-built-in-shaders/blob/master/Shaders/license.txt
//==================================================================================
/* Because Unity stores HDR files as RGBM format, it has to be decoded back to RGB
when used, and it is safest to use Unity's own decoding algorithm. However,
this function only exists in "UnityCG.cginc", and #including this in other
render pipelines cause conflict, so it is necessary to reproduce only that part
of the code here so it can be used in any render pipelines.
This is particularly useful when sampling cubemap.
*/
// Decodes HDR textures
// handles dLDR, RGBM formats
inline half3 decodeHDR(half4 data, half4 decodeInstructions){
	// Take into account texture alpha if decodeInstructions.w is true(the alpha value affects the RGB channels)
	half alpha = decodeInstructions.w * (data.a - 1.0) + 1.0;
	// If Linear mode is not supported we can skip exponent part
	#ifdef UNITY_COLORSPACE_GAMMA
		return (decodeInstructions.x * alpha) * data.rgb;
	#else
		#if defined(UNITY_USE_NATIVE_HDR)
		return decodeInstructions.x * data.rgb; // Multiplier for future HDRI relative to absolute conversion.
		#else
		return (decodeInstructions.x * pow(abs(alpha), decodeInstructions.y)) * data.rgb;
		#endif
	#endif
}

#endif
