/************************************************************************
 * EXTENSION (v13.1.5)
 * by Reev the Chameleon
 * 17 Mar 3
*************************************************************************
Extension methods that makes life easier.
Update v1.1: Rewrite code to improve performance by 30%
Update v2.0: Add rotate(Quaternion) and lookFromTo function
Update v2.1: Add reset function and TransformIndexComparer class
Update v3.0: Add TransformData struct and fix reset function to also reset EulerAnglesHint
Update v4.0: Add Vector3 extension and change file name to reflect this
Update v4.0.1: Replace implicit constructor of TransformData with explicit function
Update v5.0: Add RandomExtension
Update v6.0: Add MathfExtension, RectTransformExtension, and ColorExtension
Update v6.1: Add commonly used functionality to various Extension classes
Update v7.0: Add Tilemap Extension, Transform setLocal functionality, and lerpUnclamped functions
Update v8.0: Add Vector2, Vector4 extension, and functions to RectTransformExtension and MathfExtension
Update v9.0: Add MonoBehaviour, Rect, and Bounds extension, and fix bug in RectTransformData class
Update v10.0: Add Rigidbody & Rigidbody2D extension and add functions to Vector2D, Mathf, and Random extension
Update v11.0: Add Vector2Int extension, add facility functions for setting Transform position and
improve performance of existing codes, and add helper functions in several extensions
Update v11.1: Add ObjectExtension and TimeExtension to wrap functions that behave differently on Unity version
Update v11.2: Add functions related to eulerAngles to TransformExtension and Vector3Extension
Update v12.0: Add TmpTextExtension, Color32Extension, and fix RectTransformData class
Update v12.0.1: Fix bug in AccessVertexColor of TmpTextExtension when text starts with punctuation
Update v13.0: Add ScriptableRenderPipelineExtension and QuaternionExtension
Update v13.1: Add facility functions related to Quaternion, Vector2, and angle
Update v13.1.1: Add range(Vector2) to RandomExtension and add mod to MathfExtension
Update v13.1.2: Add function to find quaternion reflection
Update v13.1.3: Add helper functions to Vector3 and Vector2 extension
Update v13.1.4: Add aspect ratio resize function to RectTransformExtension
Update v13.1.5: Add forAllObjectsOfType taking Type parameter
*/

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if CHM_TILEMAP_PRESENT
using UnityEngine.Tilemaps;
#endif

#if CHM_INPUTSYSTEM_PRESENT
using UnityEngine.InputSystem;
#endif

#if CHM_URP_PRESENT
using System.Reflection;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Chameleon{

//=====================================================================================
#region TRANSFORM EXTENSION
public static class TransformExtension{
	public static void setX(this Transform transform,float x){
		Vector3 v3Temp = transform.position;
		v3Temp.x = x;
		transform.position = v3Temp;
	}
	public static void setY(this Transform transform,float y){
		Vector3 v3Temp = transform.position;
		v3Temp.y = y;
		transform.position = v3Temp;
	}
	public static void setZ(this Transform transform,float z){
		Vector3 v3Temp = transform.position;
		v3Temp.z = z;
		transform.position = v3Temp;
	}
	public static void setLocalX(this Transform transform,float x){
		Vector3 v3Temp = transform.localPosition;
		v3Temp.x = x;
		transform.localPosition = v3Temp;
	}
	public static void setLocalY(this Transform transform,float y){
		Vector3 v3Temp = transform.localPosition;
		v3Temp.y = y;
		transform.localPosition = v3Temp;
	}
	public static void setLocalZ(this Transform transform,float z){
		Vector3 v3Temp = transform.localPosition;
		v3Temp.z = z;
		transform.localPosition = v3Temp;
	}
	public static void rotate(this Transform transform,Quaternion q,Space relativeTo=Space.World){
		if(relativeTo == Space.World)
			transform.rotation = q*transform.rotation;
		else
			transform.localRotation = q*transform.localRotation;
	}
	public static void lookFromTo(this Transform transform,Vector3 v3From,Vector3 v3To){
		transform.rotation = Quaternion.FromToRotation(v3From,v3To)*transform.rotation;
	}
	public static void lookDirection(this Transform transform,Vector3 vDirection,Vector3? vUp=null){
		transform.rotation = Quaternion.LookRotation(vDirection,vUp??Vector3.zero);
	}
	public static void setEulerX(this Transform transform,float eulerX){
		Vector3 vTemp = transform.eulerAngles;
		vTemp.x = eulerX;
		transform.eulerAngles = vTemp;
	}
	public static void setEulerY(this Transform transform,float eulerY){
		Vector3 vTemp = transform.eulerAngles;
		vTemp.y = eulerY;
		transform.eulerAngles = vTemp;
	}
	public static void setEulerZ(this Transform transform,float eulerZ){
		Vector3 vTemp = transform.eulerAngles;
		vTemp.z = eulerZ;
		transform.eulerAngles = vTemp;
	}
	public static void setLocalEulerX(this Transform transform,float eulerX){
		Vector3 vTemp = transform.localEulerAngles;
		vTemp.x = eulerX;
		transform.localEulerAngles = vTemp;
	}
	public static void setLocalEulerY(this Transform transform,float eulerY){
		Vector3 vTemp = transform.localEulerAngles;
		vTemp.y = eulerY;
		transform.localEulerAngles = vTemp;
	}
	public static void setLocalEulerZ(this Transform transform,float eulerZ){
		Vector3 vTemp = transform.localEulerAngles;
		vTemp.z = eulerZ;
		transform.localEulerAngles = vTemp;
	}
	public static float getPitchAngle(this Transform transform){
		/* eulerAngles.x is always in the range [0,90]U[270,360) (and can be different
		from what shown in inspector), and sometimes it is annoying to deal with the break
		in the range. This function returns equivalent angle in the range [-90,90],
		similar to the pitch angle in airplane. */
		return
			transform.eulerAngles.x > 180.0f ?
			transform.eulerAngles.x-360.0f :
			transform.eulerAngles.x
		;
	}
	/* Convention only needs to be consistent because quaternion multiplication
	is not commutative. It is decide here that:
	qLocal = qWorld with object's rotation undone.
	qWorld = qLocal with object's rotation added. */
	public static Quaternion inverseTransformRotation(this Transform transform,Quaternion qWorld){
		//world to local
		return Quaternion.Inverse(transform.rotation) * qWorld; //Credit: Steven-1, UF
	}
	public static Quaternion transformRotation(this Transform transform,Quaternion qLocal){
		//local to world
		return transform.rotation * qLocal;
	}
	public static void setLocalScaleX(this Transform transform,float scaleX){
		Vector3 vTemp = transform.localScale;
		vTemp.x = scaleX;
		transform.localScale = vTemp;
	}
	public static void setLocalScaleY(this Transform transform,float scaleY){
		Vector3 vTemp = transform.localScale;
		vTemp.y = scaleY;
		transform.localScale = vTemp;
	}
	public static void setLocalScaleZ(this Transform transform,float scaleZ){
		Vector3 vTemp = transform.localScale;
		vTemp.z = scaleZ;
		transform.localScale = vTemp;
	}
	/* Virtue of this is to use it like transform.setPosition(x:3.0f,z:5.0f);
	(Credit: Rabid Penguin, SO). I have confirmed that this function works
	with negligible extra cost (<1%, sometimes even faster) comparing to
	the conventional way of setting transform.position to new Vector3. */
	public static void setPosition(this Transform transform,
		float? x=null,float? y=null,float? z=null)
	{
		Vector3 vPos = transform.position;
		vPos.x = x ?? vPos.x;
		vPos.y = y ?? vPos.y;
		vPos.z = z ?? vPos.z;
		transform.position = vPos;
	}
	public static void setLocalPosition(this Transform transform,
		float? x=null,float? y=null,float? z=null)
	{
		Vector3 vPos = transform.localPosition;
		vPos.x = x ?? vPos.x;
		vPos.y = y ?? vPos.y;
		vPos.z = z ?? vPos.z;
		transform.localPosition = vPos;
	}
	public static void reset(this Transform transform){
		transform.localPosition = Vector3.zero;
		transform.localRotation = Quaternion.identity;
		transform.localScale = Vector3.one;
		#if UNITY_EDITOR
		/* If not doing below, Euler angle as shown in inspector will not reset. */
		SerializedObject serializedTransform = new SerializedObject(transform);
		serializedTransform.FindProperty("m_LocalEulerAnglesHint").vector3Value = Vector3.zero;
		serializedTransform.ApplyModifiedPropertiesWithoutUndo();
		#endif
	}
	public static TransformData save(this Transform transform){
		return new TransformData(transform);
	}
	public static TransformData[] save(this Transform[] aTransform){
		TransformData[] aTransformData = new TransformData[aTransform.Length];
		for(int i=0; i<aTransform.Length; ++i)
			aTransformData[i] = aTransform[i].save();
		return aTransformData;
	}
	public static void load(this Transform transform,TransformData transformData){
		/* Cannot overload operator= in C#, so need to explicitly define load function. */
		transform.localPosition = transformData.localPosition;
		transform.localRotation = transformData.localRotation;
		transform.localScale = transformData.localScale;
	}
	public static void lerp(this Transform transform,
		TransformData tdStart,TransformData tdEnd,float t)
	{
		transform.localPosition = Vector3.Lerp(tdStart.localPosition,tdEnd.localPosition,t);
		transform.localRotation = Quaternion.Lerp(tdStart.localRotation,tdEnd.localRotation,t);
		transform.localScale = Vector3.Lerp(tdStart.localScale,tdEnd.localScale,t);
	}
	public static void lerpUnclamped(this Transform transform,
		TransformData tdStart,TransformData tdEnd,float t)
	{
		transform.localPosition = Vector3.LerpUnclamped(tdStart.localPosition,tdEnd.localPosition,t);
		transform.localRotation = Quaternion.LerpUnclamped(tdStart.localRotation,tdEnd.localRotation,t);
		transform.localScale = Vector3.LerpUnclamped(tdStart.localScale,tdEnd.localScale,t);
	}
}

//Code Idea Credit: Abaobao, UA
public class TransformIndexComparer : IComparer<Transform>{
	public int Compare(Transform lhs,Transform rhs){
		/* Assume lhs and rhs are on the same hierarchy depth */
		return lhs.GetSiblingIndex()-rhs.GetSiblingIndex();
	}
}

/* Unity's Transform inherits Component, which means you cannot create standalone
Transform without GameObject. This leads to problem when you want to just quickly 
save Transform data (not including parent and such). This class is written to
facilitate that. */
[System.Serializable]
public struct TransformData{
	public Vector3 localPosition;
	public Quaternion localRotation;
	public Vector3 localScale;
	public Vector3 position;
	public Quaternion rotation;
	public TransformData(Transform transform){
		localPosition = transform.localPosition;
		localRotation = transform.localRotation;
		localScale = transform.localScale;
		position = transform.position;
		rotation = transform.rotation;
	}
	public Vector3 right{get{return rotation*Vector3.right;} }
	public Vector3 left{get{return rotation*Vector3.left;} }
	public Vector3 up{get{return rotation*Vector3.up;} }
	public Vector3 down{get{return rotation*Vector3.down;} }
	public Vector3 forward{ get{return rotation*Vector3.forward;} }
	public Vector3 back{ get{return rotation*Vector3.back;} }
}
#endregion
//=====================================================================================

//=====================================================================================
#region VECTOR EXTENSIONS
//-----------------------------------------------------------------------------------
	#region VECTOR3 EXTENSION
public static class Vector3Extension{
	public static string toPreciseString(this Vector3 v){
		return
			"(" + v.x.ToString()+ "," +
			v.y.ToString() + "," +
			v.z.ToString() + ")"
		;
	}
	public static Vector3 newX(this Vector3 v,float x){
		v.x = x;
		return v;
	}
	public static Vector3 newY(this Vector3 v,float y){
		v.y = y;
		return v;
	}
	public static Vector3 newZ(this Vector3 v,float z){
		v.z = z;
		return v;
	}
	public static Vector3 lerpYParabolic(
		Vector3 vFrom,Vector3 vTo,float yAccel,float t)
	{
		return new Vector3(
			Mathf.Lerp(vFrom.x,vTo.x,t),
			vFrom.y+t*(vTo.y-vFrom.y+yAccel*(t-1)/2),
			Mathf.Lerp(vFrom.z,vTo.z,t)
		);
		//Alternative formula: Vector3.Lerp(vFrom,vTo)+yAccel*t*(t-1)/2;
	}
	/* t is clamped to [0,1] */
	public static Vector3 lerpEulerAngles(Vector3 from,Vector3 to,float t){
		//LerpAngle will handle angle wrapping (Credit: BakonGuy, UA)
		return new Vector3(
			Mathf.LerpAngle(from.x,to.x,t),
			Mathf.LerpAngle(from.y,to.y,t),
			Mathf.LerpAngle(from.z,to.z,t)
		);
	}
	public static Vector2 xy(this Vector3 v){
		return new Vector2(v.x,v.y);
	}
	public static Vector2 xz(this Vector3 v){
		return new Vector2(v.x,v.z);
	}
	public static Vector2 yz(this Vector3 v){
		return new Vector2(v.y,v.z);
	}
	//Useful for finding eulerAngles.y by taking polarAngle() later
	public static Vector2 zx(this Vector3 v){
		return new Vector2(v.z,v.x);
	}
	public static Quaternion rotation(this Vector3 v){
		return Quaternion.FromToRotation(Vector3.forward,v);
	}
	public static Vector3 eulerAngles(this Vector3 v){
		return v.rotation().eulerAngles;
		//return Quaternion.LookRotation(v,Vector3.up).eulerAngles;
	}
	public static Vector3 reciprocal(this Vector3 v){
		return new Vector3(1/v.x,1/v.y,1/v.z);
	}
}
#endregion
//-----------------------------------------------------------------------------------
	#region VECTOR2 EXTENSION
public static class Vector2Extension{
	public static string toPreciseString(this Vector2 v){
		return
			"(" + v.x.ToString()+ "," +
			v.y.ToString() + ")"
		;
	}
	public static Vector2 newX(this Vector2 v,float x){
		v.x = x;
		return v;
	}
	public static Vector2 newY(this Vector2 v,float y){
		v.y = y;
		return v;
	}
	public static Vector2 fromPolar(float magnitude,float thetaDeg){
		float thetaRad = thetaDeg*Mathf.Deg2Rad; //Credit: lordofduct, UF
		return magnitude * new Vector2(Mathf.Cos(thetaRad),Mathf.Sin(thetaRad));
	}
	public static Vector2 newRotate(this Vector2 v,float angleDeg){
		float angleRad = angleDeg*Mathf.Deg2Rad;
		float sin = Mathf.Sin(angleRad);
		float cos = Mathf.Cos(angleRad);
		return new Vector2(
			v.x*cos-v.y*sin,
			v.x*sin+v.y*cos
		);
	}
	public static float polarAngle(this Vector2 v,bool bRadian=false){
		/* Atan2 returns radians in [-pi,pi] and does quadrant verification */
		float angle = Mathf.Atan2(v.y,v.x);
		return bRadian ? angle : angle*Mathf.Rad2Deg;
	}
	public static float forwardAngle(this Vector2 v,bool bRadian=false){
		return 90.0f-v.polarAngle();
	}
	public static Vector2 reciprocal(this Vector2 v){
		return new Vector2(1/v.x,1/v.y);
	}
	public static Vector3 toVector3xz(this Vector2 v,float y=0.0f){
		return new Vector3(v.x,y,v.y);
	}
	public static Vector3 toVector3xy(this Vector2 v,float z=0.0f){
		return new Vector3(v.x,v.y,z);
	}
}
	#endregion
//-----------------------------------------------------------------------------------
	#region VECTOR4 EXTENSION
public static class Vector4Extension{
	public static string toPreciseString(this Vector4 v){
		return
			"(" + v.x.ToString()+ "," +
			v.y.ToString() + "," +
			v.z.ToString() + "," +
			v.w.ToString() + ")"
		;
	}
	public static Vector4 newX(this Vector4 v,float x){
		v.x = x;
		return v;
	}
	public static Vector4 newY(this Vector4 v,float y){
		v.y = y;
		return v;
	}
	public static Vector4 newZ(this Vector4 v,float z){
		v.z = z;
		return v;
	}
	public static Vector4 newW(this Vector4 v,float w){
		v.w = w;
		return v;
	}
}
	#endregion
//-----------------------------------------------------------------------------------
	#region VECTOR3INT EXTENSION
public static class Vector3IntExtension{
	public static Vector3Int newX(this Vector3Int v,int x){
		v.x = x;
		return v;
	}
	public static Vector3Int newY(this Vector3Int v,int y){
		v.y = y;
		return v;
	}
	public static Vector3Int newZ(this Vector3Int v,int z){
		v.z = z;
		return v;
	}
}
#endregion
//-----------------------------------------------------------------------------------
	#region VECTOR2INT EXTENSION
public static class Vector2IntExtension{
	public static Vector2Int newX(this Vector2Int v,int x){
		v.x = x;
		return v;
	}
	public static Vector2Int newY(this Vector2Int v,int y){
		v.y = y;
		return v;
	}
}
	#endregion

#endregion 
//=====================================================================================

//=====================================================================================
#region RANDOM EXTENSION
public static class RandomExtension{
	///<summary>[inclusive,inclusive]</summary>
	public static float range(Vector2 range){
		return Random.Range(range.x,range.y);
	}
	///<summary>[inclusive,exclusive)</summary>
	public static int range(Vector2Int range){
		return Random.Range(range.x,range.y);
	}
	public static Vector3 insideCube(Vector3 center,Vector3 extent){
		return new Vector3(
			center.x + Random.Range(-extent.x,extent.x)/2,
			center.y + Random.Range(-extent.y,extent.y)/2,
			center.z + Random.Range(-extent.z,extent.z)/2
		);
	}
	public static Vector3 insideCube(Bounds bound){
		return insideCube(bound.center,bound.extents);
	}
	public static int rangeExcept(int minInclusive,int maxInclusive,int exception){
		/* If we can trust user input, this check would be unnecessary */
		if(exception<minInclusive || exception>maxInclusive)
			return Random.Range(minInclusive,maxInclusive);
		int result = Random.Range(minInclusive,maxInclusive-1);
		if(result >= exception)
			++result;
		return result;
	}
	public static Vector2 onUnitCircle(){
		float angleRad = Random.Range(0.0f,MathfExtension.TWO_PI);
		return new Vector2(Mathf.Cos(angleRad),Mathf.Sin(angleRad));
	}
	public static Vector2 onUnitCircle(Vector2 rangeAngleDeg){
		float angleRad = Mathf.Deg2Rad*Random.Range(rangeAngleDeg.x,rangeAngleDeg.y);
		return new Vector2(Mathf.Cos(angleRad),Mathf.Sin(angleRad));
	}
}
#endregion
//=====================================================================================

//=====================================================================================
#region MATHF EXTENSION
public static class MathfExtension{
	public const float TWO_PI = 2*Mathf.PI;
	public const float HALF_PI = Mathf.PI/2;
	public static float sin(float t,float tOffset=0.0f){
		return Mathf.Sin(TWO_PI*(t+tOffset));
	}
	public static float sinBump(float t,float tOffset=0.0f){
		return (Mathf.Sin(TWO_PI*t-HALF_PI+tOffset)+1)/2;
	}
	public static float clamp(float value,Vector2 range){
		return Mathf.Clamp(value,range.x,range.y);
	}
	public static int clamp(int value,Vector2Int rangeInt){
		return Mathf.Clamp(value,rangeInt.x,rangeInt.y);
	}
	/* Make sure that value stays out of specified range
	It will push value to the nearest edge. */
	public static float inverseClamp(float value,float a,float b){
		if(value>a && value<b)
			return value-a<b-value ? a : b;
		return value;
	}
	public static float inverseClamp(int value,int a,int b){
		if(value>a && value<b)
			return value-a<b-value ? a : b;
		return value;
	}
	public static float exponentialDecay(float a,float b,float t,float alpha){
		return b+(a-b)*Mathf.Exp(-alpha*t);
	}
	public static float clampAngleDeg(float t,float a,float b){
		float midAngle = (a+b)/2;
		float HalfRange = Mathf.DeltaAngle(midAngle,b);
		float deltaAngle = Mathf.DeltaAngle(midAngle,t);
		if(deltaAngle > HalfRange)
			return b;
		if(deltaAngle < -HalfRange)
			return a;
		return t;
	}
	/* Operator % returns answer in range of (-m,m) according to Fortran rule.
	This function returns modulus as should be according to number theory (always positive)
	REQUIREMENT: m is positive
	(Credit: ShreevatsaR, SO) */
	public static int mod(int x,int m){
		return (x%m + m) % m;
	}
}
#endregion
//=====================================================================================

//=====================================================================================
#region RECTTRANSFORM EXTENSION
public static class RectTransformExtension{
	public static void setWidth(this RectTransform rectTransform,float width){
		RectTransform rtParent = rectTransform.parent as RectTransform;
		if(!rtParent)
			rectTransform.sizeDelta = rectTransform.sizeDelta.newX(width);
		else{
			float deltaAnchor = rectTransform.anchorMax.x-rectTransform.anchorMin.x;
			float baseWidth = deltaAnchor*rtParent.rect.width;
			rectTransform.sizeDelta = rectTransform.sizeDelta.newX(width-baseWidth);
		}
	}
	public static void setHeight(this RectTransform rectTransform,float height){
		RectTransform rtParent = rectTransform.parent as RectTransform;
		if(!rtParent)
			rectTransform.sizeDelta = rectTransform.sizeDelta.newY(height);
		else{
			float deltaAnchorParent = rectTransform.anchorMax.y-rectTransform.anchorMin.y;
			float baseHeight = deltaAnchorParent*rtParent.rect.height;
			rectTransform.sizeDelta = rectTransform.sizeDelta.newY(height-baseHeight);
		}
	}
	public static void expand(this RectTransform rectTransform,
		float left,float top,float right,float bottom)
	/* This seems to have taken into account the effect of CanvasScaler, so it still
	reacts correctly even when aspect ratio changes. */
	{
		rectTransform.offsetMin = new Vector2(
			rectTransform.offsetMin.x - left,
			rectTransform.offsetMin.y - top
		);
		rectTransform.offsetMax = new Vector2(
			rectTransform.offsetMax.x + right,
			rectTransform.offsetMax.y + bottom
		);
	}
	public static void expand(this RectTransform rectTransform,Vector4 v4LeftTopRightBottom){
		expand(
			rectTransform,
			v4LeftTopRightBottom.x,
			v4LeftTopRightBottom.y,
			v4LeftTopRightBottom.z,
			v4LeftTopRightBottom.w
		);
	}
	public static RectTransformData save(this RectTransform rectTransform){
		CanvasScaler canvasScaler = rectTransform.GetComponent<CanvasScaler>();
		return new RectTransformData(rectTransform,canvasScaler);
	}
	public static void load(this RectTransform rectTransform,RectTransformData rectTransformData)
	{
		CanvasScaler canvasScaler = rectTransform.GetComponent<CanvasScaler>();
		float scaleFactor = canvasScaler ? canvasScaler.scaleFactor : 1.0f;
		rectTransform.anchorMin = rectTransformData.anchorMin;
		rectTransform.anchorMax = rectTransformData.anchorMax;
		rectTransform.anchoredPosition = rectTransformData.anchoredPosition;
		rectTransform.sizeDelta = rectTransformData.sizeDeltaRaw * scaleFactor;
		rectTransform.pivot = rectTransformData.pivot;
		rectTransform.localRotation = rectTransformData.qLocalRotation;
	}
	public static void lerp(
		this RectTransform rt,RectTransformData rt1,RectTransformData rt2,float value)
	{
		rt.anchorMin = Vector2.Lerp(rt1.anchorMin,rt2.anchorMin,value);
		rt.anchorMax = Vector2.Lerp(rt1.anchorMax,rt2.anchorMax,value);
		rt.anchoredPosition = Vector2.Lerp(rt1.anchoredPosition,rt2.anchoredPosition,value);
		rt.sizeDelta = Vector2.Lerp(rt1.sizeDeltaRaw,rt2.sizeDeltaRaw,value);
		rt.pivot = Vector2.Lerp(rt1.pivot,rt2.pivot,value);
		rt.rotation = Quaternion.Lerp(rt1.qLocalRotation,rt2.qLocalRotation,value);
	}
	public static void lerpUnclamped(
		this RectTransform rt,RectTransformData rt1,RectTransformData rt2,float value)
	{
		rt.anchorMin = Vector2.LerpUnclamped(rt1.anchorMin,rt2.anchorMin,value);
		rt.anchorMax = Vector2.LerpUnclamped(rt1.anchorMax,rt2.anchorMax,value);
		rt.anchoredPosition = Vector2.LerpUnclamped(rt1.anchoredPosition,rt2.anchoredPosition,value);
		rt.sizeDelta = Vector2.LerpUnclamped(rt1.sizeDeltaRaw,rt2.sizeDeltaRaw,value);
		rt.pivot = Vector2.LerpUnclamped(rt1.pivot,rt2.pivot,value);
		rt.rotation = Quaternion.LerpUnclamped(rt1.qLocalRotation,rt2.qLocalRotation,value);
	}
	public static void fitParent(this RectTransform rt){
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}
	public static void fitParentWidth(this RectTransform rt){
		rt.anchorMin = rt.anchorMin.newX(0.0f);
		rt.anchorMax = rt.anchorMax.newX(1.0f);
		rt.offsetMin = rt.offsetMin.newX(0.0f);
		rt.offsetMax = rt.offsetMax.newX(0.0f);
	}
	public static void fitParentHeight(this RectTransform rt){
		rt.anchorMin = rt.anchorMin.newY(0.0f);
		rt.anchorMax = rt.anchorMax.newY(1.0f);
		rt.offsetMin = rt.offsetMin.newY(0.0f);
		rt.offsetMax = rt.offsetMax.newY(0.0f);
	}
	public static Vector2 resizeToMatchAspectRatio(this RectTransform rt,
		float aspectRatio,bool bEnlarge=false)
	{
		float aspectRt = rt.rect.height/rt.rect.width;
		if(aspectRt > aspectRatio){ //rt too high
			if(bEnlarge){
				rt.setWidth(rt.rect.height/aspectRatio);}
			else{
				rt.setHeight(rt.rect.width*aspectRatio);}
		}
		else if(aspectRt < aspectRatio){ //rt too long
			if(bEnlarge){
				rt.setHeight(rt.rect.width*aspectRatio);}
			else{
				rt.setWidth(rt.rect.height/aspectRatio);}
		}
		return new Vector2(rt.rect.width,rt.rect.height);
	}
}

[System.Serializable]
public struct RectTransformData{
	public Vector2 anchorMin;
	public Vector2 anchorMax;
	public Vector2 anchoredPosition;
	public Vector2 sizeDeltaRaw;
	public Vector2 pivot;
	public Quaternion qLocalRotation; //for whatever reason, localRotation doesn't work
	public RectTransformData(
		RectTransform rectTransform,CanvasScaler canvasScaler=null)
	{
		float scaleFactor = canvasScaler ? canvasScaler.scaleFactor : 1.0f;
		anchorMin = rectTransform.anchorMin;
		anchorMax = rectTransform.anchorMax;
		anchoredPosition = rectTransform.anchoredPosition;
		sizeDeltaRaw = rectTransform.sizeDelta/scaleFactor;
		pivot = rectTransform.pivot;
		qLocalRotation = rectTransform.localRotation;
	}
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(RectTransformData))]
class RectTransformDataDrawer : PropertyDrawer{
	private GUIStyle styleButton = new GUIStyle(GUI.skin.button); //Credit: Hyago Oliveira, SO
	private GUIContent contentAnchorMin = new GUIContent("AnchorMin");
	private GUIContent contentAnchorMax = new GUIContent("AnchorMax");
	private GUIContent contentAnchoredPosition = new GUIContent("AnchoredPosition");
	private GUIContent contentSizeDeltaRaw = new GUIContent("SizeDelta");
	private GUIContent contentRotation = new GUIContent("Rotation");
	private bool bFoldout = false;

	public override void OnGUI(Rect position,SerializedProperty property,GUIContent label){
		Rect rectOriginal = position;
		float savedLabelWidth = EditorGUIUtility.labelWidth;
		position.width = savedLabelWidth;
		bFoldout = EditorGUI.BeginFoldoutHeaderGroup(position,bFoldout,label);
		EditorGUI.EndFoldoutHeaderGroup();
		if(bFoldout){
			bool bWideMode = EditorGUIUtility.wideMode;
			if(!bWideMode){
				EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth - 212.0f;
				EditorGUIUtility.wideMode = true;
			}
			//cache some frequently used variables
			float singleLineHeight = EditorGUIUtility.singleLineHeight;
			float labelWidth = EditorGUIUtility.labelWidth;
			position.height = singleLineHeight;
			position.width = (rectOriginal.width-labelWidth)*3/2 + labelWidth;
			position.y += singleLineHeight;
			SerializedProperty spAnchorMin = property.FindPropertyRelative(nameof(RectTransformData.anchorMin));
			EditorGUI.PropertyField(position,spAnchorMin,contentAnchorMin);
			position.y += singleLineHeight;
			SerializedProperty spAnchorMax = property.FindPropertyRelative(nameof(RectTransformData.anchorMax));
			EditorGUI.PropertyField(position,spAnchorMax,contentAnchorMax);
			position.y += singleLineHeight;
			SerializedProperty spAnchoredPosition = property.FindPropertyRelative(nameof(RectTransformData.anchoredPosition));
			EditorGUI.PropertyField(position,spAnchoredPosition,contentAnchoredPosition);
			position.y += singleLineHeight;
			SerializedProperty spSizeDeltaRaw = property.FindPropertyRelative(nameof(RectTransformData.sizeDeltaRaw));
			EditorGUI.PropertyField(position,spSizeDeltaRaw,contentSizeDeltaRaw);
			position.width = rectOriginal.width;
			position.y += singleLineHeight;
			SerializedProperty spLocalRotation = property.FindPropertyRelative(nameof(RectTransformData.qLocalRotation));
			EditorGUI.BeginChangeCheck();
			float zLocalRotation = EditorGUI.FloatField(position,contentRotation,spLocalRotation.quaternionValue.eulerAngles.z);
			if(EditorGUI.EndChangeCheck()){
				Vector3 localEuler = spLocalRotation.quaternionValue.eulerAngles;
				spLocalRotation.quaternionValue = Quaternion.Euler(
					localEuler.x,localEuler.y,zLocalRotation
				);
			}
		}
		position.x = rectOriginal.x+savedLabelWidth;
		position.y = rectOriginal.y;
		position.width = rectOriginal.width-savedLabelWidth;
		EditorGUI.BeginChangeCheck();
		Object oRtUser = EditorGUI.ObjectField(
			position,
			null,
			typeof(RectTransform),
			true
		);
		if(EditorGUI.EndChangeCheck() && oRtUser){
			RectTransform rtSource = (RectTransform)oRtUser; //should be valid
			property.FindPropertyRelative(nameof(RectTransformData.anchorMin)).vector2Value =
				rtSource.anchorMin;
			property.FindPropertyRelative(nameof(RectTransformData.anchorMax)).vector2Value =
				rtSource.anchorMax;
			property.FindPropertyRelative(nameof(RectTransformData.anchoredPosition)).vector2Value =
				rtSource.anchoredPosition;
			property.FindPropertyRelative(nameof(RectTransformData.sizeDeltaRaw)).vector2Value =
				rtSource.sizeDelta;
			property.FindPropertyRelative(nameof(RectTransformData.qLocalRotation)).quaternionValue =
				rtSource.localRotation;
			//property.serializedObject.ApplyModifiedProperties();
		}
		/* Shouldn't be necessary */
		//EditorGUIUtility.wideMode = bWideMode;
		//EditorGUIUtility.labelWidth = savedLabelWidth;
	}
	public override float GetPropertyHeight(SerializedProperty property,GUIContent label){
		if(bFoldout)
			return EditorGUIUtility.singleLineHeight * 6;
		return EditorGUIUtility.singleLineHeight;
	}
}
#endif

#endregion
//=====================================================================================

//=====================================================================================
#region COLOR & COLOR32 EXTENSION
/* Color32 takes less space and should be faster because it uses bytes
while Color uses floats */
//-----------------------------------------------------------------------------------
	#region COLOR EXTENSION
public static class ColorExtension{
	public static Color createColor(Color color,float a){
		return new Color(color.r,color.g,color.b,a);
	}
	public static Color newA(this Color color,float a){
		return new Color(color.r,color.g,color.b,a);
	}
}
	#endregion
//-----------------------------------------------------------------------------------
	#region COLOR32 EXTENSION
public static class Color32Extension{
	public static Color32 newA(this Color32 color,byte a){
		return new Color32(color.r,color.g,color.b,a);
	}
}
	#endregion
//-----------------------------------------------------------------------------------
#endregion
//=====================================================================================

//=====================================================================================
#region MONOBEHAVIOUR EXTENSION
public static class MonoBehaviourExtension{
	public static Coroutine delayCall(this MonoBehaviour monoBehaviour,
		Action a,float sec)
	{
		return monoBehaviour.StartCoroutine(rfDelay(a,new WaitForSeconds(sec)));
	}
	public static Coroutine delayCallEndOfFrame(this MonoBehaviour monoBehaviour,
		Action a)
	{
		return monoBehaviour.StartCoroutine(rfDelay(a,new WaitForEndOfFrame()));
	}
	private static IEnumerator rfDelay(Action a,YieldInstruction yieldInstruction)
	{
		yield return yieldInstruction;
		a.Invoke();
	}
}
#endregion
//=====================================================================================

//=====================================================================================
#region RECT & BOUNDS EXTENSIONS
//NOTE: Rect is 2D and Bounds is 3D (Credit: Eric5h5, UF)
//-----------------------------------------------------------------------------------
	#region RECT EXTENSION
public static class RectExtension{
	public static Rect newWidth(this Rect rect,float width){
		rect.width = width;
		return rect;
	}
	public static Rect newHeight(this Rect rect,float height){
		rect.height = height;
		return rect;
	}
	public static bool isOverlappingCircle(this Rect rect,Vector2 v2Center,float radius){
		// Credit: e.James, SO
		Vector2 v2Distance = new Vector2(
			Mathf.Abs(v2Center.x-rect.center.x),
			Mathf.Abs(v2Center.y-rect.center.y)
		);
		Vector2 v2HalfRect = new Vector2(rect.width/2,rect.height/2);
		
		/* rect and circle rect does not intersect, so there is no chance. */
		if(v2Distance.x > v2HalfRect.x+radius || v2Distance.y > v2HalfRect.y+radius)
			return false;
		/* rect and circle rect intersect with one axis intersecting more than half,
		thus will intersect for sure. */
		if(v2Distance.x <= v2HalfRect.x || v2Distance.y <= v2HalfRect.y)
			return true;
		/* rect and circle rect intersect less than half in both axes, so will
		intersect if distance from corner to circle center <= r. */
		return Vector2.Distance(v2Distance,v2HalfRect) <= radius;
	}
}
	#endregion
//-----------------------------------------------------------------------------------
	#region BOUNDS EXTENSION
public static class BoundsExtension{
	public static Bounds newSize(this Bounds bound,Vector3 vSize){
		bound.size = vSize;
		return bound;
	}
	public static Bounds newSizeX(this Bounds bound,float x){
		Vector3 vSize = bound.size;
		vSize.x = x;
		bound.size = vSize;
		return bound;
	}
	public static Bounds newSizeY(this Bounds bound,float y){
		Vector3 vSize = bound.size;
		vSize.y = y;
		bound.size = vSize;
		return bound;
	}
	public static Bounds newSizeZ(this Bounds bound,float z){
		Vector3 vSize = bound.size;
		vSize.z = z;
		bound.size = vSize;
		return bound;
	}
	public static Bounds newCenter(this Bounds bound,Vector3 vCenter){
		bound.center = vCenter;
		return bound;
	}
}
	#endregion
//-----------------------------------------------------------------------------------
#endregion
//=====================================================================================

//=====================================================================================
#region RIGIDBODY & RIGIDBODY2D EXTENSIONS
//-----------------------------------------------------------------------------------
	#region RIGIDBODY EXTENSION
public static class RigidbodyExtension{

}
	#endregion
//-----------------------------------------------------------------------------------
	#region RIGIDBODY2D EXTENSION
public static class Rigidbody2DExtension{
	public static Rigidbody2DMovementData saveMovement(this Rigidbody2D rigidbody2D){
		return new Rigidbody2DMovementData(rigidbody2D);
	}
	public static void loadMovement(this Rigidbody2D rigidbody2D,
		Rigidbody2DMovementData movementData)
	{
		rigidbody2D.velocity = movementData.velocity;
		rigidbody2D.angularVelocity = movementData.angularVelocity;
	}
}

[Serializable]
public struct Rigidbody2DMovementData{
	public Vector2 velocity;
	public float angularVelocity; //degree per seconds
	public Rigidbody2DMovementData(Rigidbody2D rigidbody2D){
		velocity = rigidbody2D.velocity;
		angularVelocity = rigidbody2D.angularVelocity;
	}
}
	#endregion
//-----------------------------------------------------------------------------------
#endregion
//=====================================================================================

//=====================================================================================
#region OBJECT EXTENSIONS
public static class ObjectExtension{
	public static void forAllObjectsOfType(Type type,Action<Object> action){
		#if !UNITY_2020_3_OR_NEWER
		foreach(GameObject g in
			UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
		{
			foreach(Object obj in g.GetComponentsInChildren(type,true))
				action.Invoke(obj);
		}
		#else //UNITY_2020_3_OR_NEWER
		foreach(Object obj in Object.FindObjectsOfType(type,true))
			action.Invoke(obj);
		#endif
	}
	public static void forAllObjectsOfType<T>(Action<T> action) where T:Object{
		#if !UNITY_2020_3_OR_NEWER
		/* For older version, there is no overload of FindObjectsOfType that can find
		Components in INACTIVE GameObjects, so we need to iterate that ourselves
		(Credit: Baste, UF & rempelj, UA) */
		foreach(GameObject g in
			UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
		{
			foreach(T obj in g.GetComponentsInChildren<T>(true))
				action.Invoke(obj);
		}

		#else //UNITY_2020_3_OR_NEWER
		foreach(T obj in Object.FindObjectsOfType<T>(true))
			action.Invoke(obj);
		#endif
	}
}
#endregion
//=====================================================================================

//=====================================================================================
#region TIME EXTENSIONS
public static class TimeExtension{
	public static double RealtimeSinceStartup{
		get{
			#if !UNITY_2020_3_OR_NEWER
			return Time.realtimeSinceStartup;
			#else
			return Time.realtimeSinceStartupAsDouble;
			#endif
		}
	}
}
#endregion
//=====================================================================================

//=====================================================================================
#region TILEMAP & GRIDLAYOUT EXTENSION
#if CHM_TILEMAP_PRESENT
//NOTE: Tilemap extends GridLayout base class
public static class TilemapExtension{
	public static Vector3 pivottedLocalPos(this Tilemap tilemap,Vector3Int cell){
		return tilemap.CellToLocal(cell) +
			Vector3.Scale(tilemap.cellSize,tilemap.tileAnchor); //component-wise multiply
	}
	public static Vector3 pivottedWorldPos(this Tilemap tilemap,Vector3Int cell){
		return tilemap.transform.TransformPoint(tilemap.pivottedLocalPos(cell));
	}
	public static int getTileCount(this Tilemap tilemap,BoundsInt bound){
		/* I don't know why Unity doesn't provide this function, and unsure
		whether this is optimal or not. Anyway... */
		int count = 0;
		foreach(Vector3Int cell in tilemap.cellBounds.allPositionsWithin){
			if(tilemap.HasTile(cell))
				++count;
		}
		return count;
	}
	public static int getTotalTileCount(this Tilemap tilemap){
		return tilemap.getTileCount(tilemap.cellBounds);
	}
	public static bool isOverlappingTileCollider(this Tilemap tilemap,
		Vector3Int cell,Collider other)
	{
		Tile tile = tilemap.GetTile(cell) as Tile;
		if(!tile)
			return false;
		GameObject g = new GameObject();
		g.transform.position = tilemap.pivottedLocalPos(cell);
		g.transform.SetParent(tilemap.transform,false);
		g.AddComponent<SpriteRenderer>().sprite = tile.sprite;
		PolygonCollider2D p = g.AddComponent<PolygonCollider2D>();
		bool bOverlap = false;
		if(p.OverlapCollider(new ContactFilter2D().NoFilter(),new Collider2D[1]) > 0)
			bOverlap = true;
		Object.DestroyImmediate(g);
		return bOverlap;
	}
}
public static class GridLayoutExtension{
	/* Most credits go to: Paul Masri-Stone, SO */
	public static BoundsInt getOverlappingCellBound(this GridLayout gridLayout,
		Bounds boundLocal) //boundLocal is local to grid
	{
		Vector3Int cellMin = gridLayout.LocalToCell(boundLocal.min);
		Vector3Int cellMax = gridLayout.LocalToCell(boundLocal.max);
		//Unlike Bounds constructor, BoundsInt constructor takes MIN and size
		return new BoundsInt(cellMin,cellMax-cellMin+Vector3Int.one);
	}
}
#endif
#endregion
//=====================================================================================

//=====================================================================================
#region TMP_TEXT EXTENSION
public static class TmpTextExtension{
	/* These should NOT be called in FIRST FRAME because textInfo won't contain data until
	text is rendered. If needed in Start() or Awake(), call tmpText.ForceMeshUpdate()
	before these functions (Stephan_B, UF). */
	/* Specify bUpdateImmediate=false if you want to do many other vertex operations
	and decide to group calls to UpdateVertexData by yourself. */
	public static void setCharacterColor(this TMP_Text tmpText,int index,Color32 color32,
		bool bUpdateImmediate=true)
	{
		int indexVertex;
		Color32[] aVertexColor = tmpText.accessVertexColor(index,out indexVertex);
		if(aVertexColor == null)
			return;
		aVertexColor[indexVertex] = color32;
		aVertexColor[indexVertex+1] = color32;
		aVertexColor[indexVertex+2] = color32;
		aVertexColor[indexVertex+3] = color32;
		if(bUpdateImmediate)
			tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
	}
	public static void setCharacterColor(this TMP_Text tmpText,int index,Color32[] aColor32,
		bool bUpdateImmediate=true)
	{
		int indexVertex;
		Color32[] aVertexColor = tmpText.accessVertexColor(index,out indexVertex);
		if(aVertexColor == null)
			return;
		aVertexColor[indexVertex] = aColor32[0];
		aVertexColor[indexVertex+1] = aColor32[1];
		aVertexColor[indexVertex+2] = aColor32[2];
		aVertexColor[indexVertex+3] = aColor32[3];
		if(bUpdateImmediate)
			tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
	}
	public static void setCharacterAlpha(this TMP_Text tmpText,int index,byte alpha,
		bool bUpdateImmediate=true)
	{
		int indexVertex;
		Color32[] aVertexColor = tmpText.accessVertexColor(index,out indexVertex);
		if(aVertexColor == null)
			return;
		aVertexColor[indexVertex] = aVertexColor[indexVertex].newA(alpha);
		aVertexColor[indexVertex+1] = aVertexColor[indexVertex+1].newA(alpha);
		aVertexColor[indexVertex+2] = aVertexColor[indexVertex+2].newA(alpha);
		aVertexColor[indexVertex+3] = aVertexColor[indexVertex+3].newA(alpha);
		if(bUpdateImmediate)
			tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
	}
	public static Color32[] accessVertexColor(this TMP_Text tmpText,
		int indexChar,out int indexVertex)
	{
		TMP_TextInfo textInfo = tmpText.textInfo;
		if(textInfo?.wordCount<=0 || indexChar>=textInfo.characterCount){
			indexVertex = -1;
			return null;
		}
		/* Example 12 uses wordInfo, but that will fail if word starts with
		a punctuation, where wordInfo[0].firstCharacterIndex points to the first
		character AFTER the punctutation. Since there is no example, currently
		observing bugs. */
		int indexCharInfo = textInfo.pageInfo[0].firstCharacterIndex + indexChar;
		if(!textInfo.characterInfo[indexCharInfo].isVisible){
			/* Because weird behavior that characterInfo.vertexIndex returns 0 for
			invisible char such as space, and will cause the FIRST letter to be colored. */
			indexVertex = -1;
			return null;
		}
		/* Credit: Example 12 of TextMeshPro package examples */
		indexVertex = textInfo.characterInfo[indexCharInfo].vertexIndex; //index of first vertex
		/* Because same string may use many materials for different font styles */
		int meshIndex = textInfo.characterInfo[indexCharInfo].materialReferenceIndex;
		return textInfo.meshInfo[meshIndex].colors32;
	}
	/* These are more efficient than setting alpha or colors because it does not
	regenerate entire TMP_Text. HOWEVER, the properties as queried or displayed in the
	inspector will NOT match the current alpha.
	You can set overrideColorTags to true, call textMeshPro.Rebuild(canvasUpdateStage) (Credit: Peter777, UF),
	then reset overrideColorTags to false to reset the text to its original color.
	If the text changes. The color will reset when it is rebuilt. This can be resolved by
	calling textMeshPro.ForceMeshUpdate() before using functions below, the combination of which
	still gives better performance than setting txtMeshPro.color directly. */
	public static void setVerticesAlpha(this TMP_Text tmpText,byte alpha256,
		bool bUpdateImmediate=true)
	{
		int length = tmpText.text?.Length ?? 0;
		if(length == 0)
			return;
		for(int i=0; i<length; ++i)
			tmpText.setCharacterAlpha(i,alpha256,false);
		if(bUpdateImmediate)
			tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
	}
	public static void setVerticesColor(this TMP_Text tmpText,Color32[] aColor32,
		bool bUpdateImmediate=true)
	{
		int length = tmpText.text?.Length ?? 0;
		if(length == 0)
			return;
		for(int i=0; i<length; ++i)
			tmpText.setCharacterColor(i,aColor32,false);
		if(bUpdateImmediate)
			tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
	}
	public static void setVerticesColor(this TMP_Text tmpText,Color32 color32,
		bool bUpdateImmediate=true)
	{
		int length = tmpText.text?.Length ?? 0;
		if(length == 0)
			return;
		for(int i=0; i<length; ++i)
			tmpText.setCharacterColor(i,color32,false);
		if(bUpdateImmediate)
			tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
	}
}
#endregion
//=====================================================================================

//=====================================================================================
#region SCRIPTABLERENDERPIPELINE EXTENSION
//-----------------------------------------------------------------------------------
	#region UNIVERSALRENDERPIPELINE EXTENSION
#if CHM_URP_PRESENT
public static class ScriptableRendererDataExtension{
	public static T getRendererFeature<T>(this ScriptableRendererData data)
		where T:ScriptableRendererFeature
	{
		for(int i=0; i<data.rendererFeatures.Count; ++i)
			if(data.rendererFeatures[i] is T)
				return (T)data.rendererFeatures[i];
		return null;
	}
}
public static class UniversalRenderPipelineAssetExtension{
	public static ScriptableRendererData getRendererData(
		this UniversalRenderPipelineAsset urpAsset,int index)
	{
		return ((ScriptableRendererData[])
			typeof(UniversalRenderPipelineAsset)
			.GetField("m_RendererDataList",ReflectionHelper.BINDINGFLAGS_ALL)
			.GetValue(urpAsset))[index]
		;
	}
}
#endif
	#endregion
//-----------------------------------------------------------------------------------
#endregion
//=====================================================================================

//=====================================================================================
#region QUATERNION EXTENSION
public static class QuaternionExtension{
	public static readonly float halfSqrt2 = Mathf.Sqrt(2)/2;
	public static readonly Quaternion right = new Quaternion(0.0f,halfSqrt2,0.0f,halfSqrt2);
	public static readonly Quaternion left = new Quaternion(0.0f,-halfSqrt2,0.0f,halfSqrt2);
	public static readonly Quaternion forward = Quaternion.identity;
	public static readonly Quaternion back = new Quaternion(0.0f,1.0f,0.0f,0.0f);
	public static readonly Quaternion up = new Quaternion(-halfSqrt2,0.0f,0.0f,halfSqrt2);
	public static readonly Quaternion down = new Quaternion(halfSqrt2,0.0f,0.0f,halfSqrt2);

	public static string toPreciseString(this Quaternion q){
		return
			"(" + q.x.ToString()+ "," +
			q.y.ToString() + "," +
			q.z.ToString() + "," +
			q.w.ToString() + ")"
		;
	}
	public static Quaternion inverse(this Quaternion q){ //just for convenience and shorten code
		return Quaternion.Inverse(q);
	}
	public static Quaternion clampAngle(this Quaternion q,float angleMin,float angleMax){
		float angle;
		Vector3 vAxis;
		q.ToAngleAxis(out angle,out vAxis);
		angle = Mathf.DeltaAngle(0.0f,angle);
		if(Mathf.Approximately(angle,0.0f))
			return Quaternion.identity;
		return Quaternion.AngleAxis(Mathf.Clamp(angle,angleMin,angleMax),vAxis);
	}
	public static Quaternion mirror(this Quaternion q,Vector3 vAxis){
		return new Quaternion(vAxis.x,vAxis.y,vAxis.z,0.0f) * q;
	}
	public static Quaternion qReflection(Vector3 v){
		float absX = Mathf.Abs(v.x);
		float absY = Mathf.Abs(v.y);
		float absZ = Mathf.Abs(v.z);
		float minAbs = Mathf.Min(absX,absY,absZ);
		if(minAbs==absX){
			if(absX==0.0f){
				return new Quaternion(1,0,0,0);}
			//else, none of them is zero because min>0
			return new Quaternion(0,1/v.y,-1/v.z,0).normalized;
		}
		if(minAbs==absY){
			if(absY==0.0f){
				return new Quaternion(0,1,0,0);}
			return new Quaternion(1/v.x,0,-1/v.z,0).normalized;
		}
		if(minAbs==absZ){
			if(absZ==0.0f){
				return new Quaternion(0,0,1,0);}
			return new Quaternion(1/v.x,-1/v.y,0,0).normalized;
		}
		return Quaternion.identity;
	}
}
#endregion
//=====================================================================================

} //end namespace Chameleon
