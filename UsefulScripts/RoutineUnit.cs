/************************************************************************
 * ROUTINEUNIT (v3.1)
 * by Reev the Chameleon
 * 26 Feb 3
*************************************************************************
Collections of IEnumerator functions used with StartCoroutine to tween
the Object between 2 status. They can be used as substitutes
for usual animation, and in many cases perform better.

Update v1.1: Add CanvasGroup, Graphic, and RectTransform tween, add support
for onTweenComplete callback, and use LerpUnclamped instead of Lerp
Update v1.2: Add more Transform tween functions
Update v1.3: Add SpriteRenderer and Material tween, and rearrange tweenGeneric parameter order
Update v2.0: Add IRoutineUnit as an interface for routines which can be skippable and loopable.
Major overhaul on TweenRoutine by making it into a class, allowing user to set
various properties while it is running. Add ChainEnumerator and ParallelEnumerator for
combining IEnumerators. Add TypewriteRoutineUnit class and Trigger class.
Update v2.0.1: Minor change in constructor of TypewriteRoutineUnit
Update v2.1: Add more code to TMP_Text and RectTransform tween. Add Cooldown class
Update v3.0: Add TweenRoutineUnit<T> class and modify return type of RoutineUnitCollection functions
Update v3.0.1: Add parameter to support tweening TMP_Text vertices in first frame
Update v3.1: Add code for tweening volume of AudioSource and AudioListener
*/

using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro;

namespace Chameleon{

//=====================================================================================
	#region ROUTINEUNIT
public interface IRoutineUnit : IEnumerator{
	void skip();
	//void Reset(); should also have valid implementation (just calling itr.Reset() may throw)
}
public class RoutineUnit : IRoutineUnit{
	protected IEnumerator itr;
	protected Action dOnDone;
	protected Action dOnReset;
	protected Action dOnSkip;
	public RoutineUnit(IEnumerator itr,
		Action dOnDone=null,Action dOnReset=null,Action dOnSkip=null)
	{
		this.itr = itr;
		this.dOnDone = dOnDone;
		this.dOnReset = dOnReset;
		this.dOnSkip = dOnSkip;
	}
	public object Current{ get{return itr.Current;} }
	public bool MoveNext(){
		if(!itr.MoveNext()){
			dOnDone?.Invoke();
			return false;
		}
		return true;
	}
	public void Reset(){
		if(dOnReset != null)
			dOnReset.Invoke();
		else
			itr.Reset();
	}
	public void skip() {
		if(dOnSkip != null)
			dOnSkip.Invoke();
		else
			(itr as IRoutineUnit)?.skip();
	}
}
	#endregion
//=====================================================================================

//=====================================================================================
	#region TWEEN ROUTINEUNIT
public enum eTweenLoopMode{Once,Loop,Pingpong};
public delegate void DTweenFunction(float t);
public delegate void DOnTweenDone(float tAtDone);

public class TweenRoutineUnit : IRoutineUnit{
	//fixed parameters
	protected DTweenFunction dTweenFunction;
	protected float duration;
	public readonly eTweenLoopMode loopMode;
	public readonly DOnTweenDone dOnDone;
	//alterable parameters
	public bool bReverse =false;
	public bool bCull =false; //if true, will keep track of time but not invoke dTweenFunction
	
	//internal use
	private float t =0.0f; //clamped to [0,1]
	private bool bAdvanceTime =false; //first MoveNext() will not advance time
	private YieldInstruction wait;
	private double skipYieldTime = -1.0; //so will not skipped if run in Start()

	public bool IsUsingFixedUpdate{ get{return wait!=null;} }
	public float getT(){return t;}
	/* If !bImmediate, bSkipNextYield is ignored and is implicitly false.
	if bImmediate BUT t is 0.0f or 1.0f, bSkipNextYield is also ignored and implicitly false.
	This is because it will cause the coroutine to end and bSkipNextYield does not carry over
	to the next time it is called.
	If not for cases above, and bSkipNextYield is true, time will not update and function
	will not be invoked the next time MoveNext() is called.
	This should be true if you call setT in Update() or FixedUpdate(),
	so MoveNext() does not redo the work when Unity run it again this frame,
	but should be false in LateUpdate() where Unity will run MoveNext() again next frame. */
	public void setT(float t,bool bImmediate=true,bool bSkipNextYield=true){
		this.t = Mathf.Clamp01(t);
		if(bImmediate){
			skipYieldTime = -1.0; //so MoveNext() can do the work
			bAdvanceTime = false;
			if(MoveNext() && bSkipNextYield)
				skipYieldTime = Time.timeAsDouble;
		}
		else
			bAdvanceTime = false;
	}
	public float getDuration(){return duration;}
	public TweenRoutineUnit(
		DTweenFunction dTweenFunction,float duration,eTweenLoopMode loopMode=eTweenLoopMode.Once,
		DOnTweenDone dOnDone=null,float tStart=0.0f,bool bFixedUpdate=false,
		bool bReverse=false,bool bCull=false)
	{
		this.dTweenFunction = dTweenFunction;
		this.duration = duration;
		this.loopMode = loopMode;
		this.dOnDone = dOnDone;
		this.t = Mathf.Clamp01(tStart);
		wait = bFixedUpdate ? new WaitForFixedUpdate() : null;
		this.bReverse = bReverse;
		this.bCull = bCull;
	}
	public object Current{ get{return wait;} }
	public bool MoveNext(){
		if(skipYieldTime == Time.timeAsDouble){ //bSkipYield is true only if coroutine has not ended
			bAdvanceTime = true; //because this yield has passed
			return true;
		}
		float progress = bReverse ? 1-t : t;
		if(bAdvanceTime){
			progress += IsUsingFixedUpdate ?
				Time.fixedDeltaTime/duration :
				Time.deltaTime/duration
			;
		}
		else
			bAdvanceTime = true;
		if(progress >= 1.0f){
			switch(loopMode){
				case eTweenLoopMode.Once:
					t = bReverse ? 1-progress : progress;
					if(!bCull)
						dTweenFunction.Invoke(Mathf.Clamp01(t));
					dOnDone?.Invoke(t);
					return false;
				case eTweenLoopMode.Loop:
					progress -= 1.0f;
					break;
				case eTweenLoopMode.Pingpong:
					bReverse = !bReverse;
					progress -= 1.0f;
					break;
			}
		}
		t = bReverse ? 1-progress : progress;
		if(!bCull)
			dTweenFunction.Invoke(t);
		return true;
	}
	/* Default Reset() and skip() implementation is such that
	change is reflected immediately and next yield is skipped. */
	public void Reset(){
		setT(bReverse ? 1.0f : 0.0f);
	}
	public void Reset(float duration){
		Reset();
		this.duration = duration;
	}
	public void skip(){
		setT(bReverse ? 0.0f : 1.0f);
	}
}

public delegate void DTweenFunction<T>(T start,T end,float t);
public class TweenRoutineUnit<T> : TweenRoutineUnit{
	public T Start{get; private set;}
	public T End{get; private set;}
	public TweenRoutineUnit(
		DTweenFunction<T> dTweenFunction,T start,T end,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,DOnTweenDone dOnDone=null,
		float tStart=0.0f,bool bFixedUpdate=false,
		bool bReverse=false,bool bCull=false
	)
		: base(null,duration,loopMode,dOnDone,tStart,bFixedUpdate,bReverse,bCull)
	{
		this.Start = start;
		this.End = end;
		this.dTweenFunction = (float t)=>{dTweenFunction.Invoke(this.Start,this.End,t);};
	}
	public void reset(T start,T end){
		this.Start = start;
		this.End = end;
		Reset();
	}
}

public static class RoutineUnitCollection{
	public delegate float DMapping(float t);
//----------------------------------------------------------------------------------
	#region TRANSFORM ROUTINEUNIT
	public static TweenRoutineUnit<Vector3> tweenPosition(this Transform transform,
		Vector3 vStart,Vector3 vEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<Vector3>(
			(Vector3 _vStart,Vector3 _vEnd,float t) => {transform.position =
				Vector3.LerpUnclamped(_vStart,_vEnd,dMapping?.Invoke(t)??t);},
			vStart,vEnd,duration,loopMode,dOnDone,tStart,false
		);
		/* use _vStart and _vEnd since lambda parameter shadowing not supported until C#8 */
	}
	public static TweenRoutineUnit<Vector3> tweenLocalPosition(this Transform transform,
		Vector3 vStart,Vector3 vEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<Vector3>(
			(Vector3 _vStart,Vector3 _vEnd,float t) => {transform.localPosition =
				Vector3.LerpUnclamped(_vStart,_vEnd,dMapping?.Invoke(t)??t);},
			vStart,vEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit<Vector3> tweenPositionYParabolic(this Transform transform,
		Vector3 vStart,Vector3 vEnd,float yAccel,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<Vector3>(
			(Vector3 _vStart,Vector3 _vEnd,float t) => {transform.position =
				Vector3Extension.lerpYParabolic(_vStart,_vEnd,yAccel,t);},
			vStart,vEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit sinShakePosition(this Transform transform,
		Vector3 vShake,float duration,float offset=0.0f,
		DOnTweenDone dOnDone=null)
	{
		Vector3 vCenter = transform.position;
		return new TweenRoutineUnit(
			(float t) => {transform.position = vCenter+vShake*MathfExtension.sin(t+offset);},
			duration,eTweenLoopMode.Once,dOnDone,0.0f,false
		);
	}
	public static TweenRoutineUnit<TransformData> tweenTransform(this Transform transform,
		TransformData tdStart,TransformData tdEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<TransformData>(
			(TransformData _tdStart,TransformData _tdEnd,float t) =>
				{transform.lerpUnclamped(_tdStart,_tdEnd,dMapping?.Invoke(t)??t);},
			tdStart,tdEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit<Vector3> tweenLocalScale(this Transform transform,
		Vector3 vStart,Vector3 vEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<Vector3>(
			(Vector3 _vStart,Vector3 _vEnd,float t) => {transform.localScale =
				Vector3.LerpUnclamped(_vStart,_vEnd,dMapping?.Invoke(t)??t);},
			vStart,vEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit<float> tweenLocalScale(this Transform transform,
		float start,float end,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		Vector3 vStartScale = transform.localScale;
		return new TweenRoutineUnit<float>(
			(float _start,float _end,float t) => {transform.localScale =
				Mathf.LerpUnclamped(_start,_end,dMapping?.Invoke(t)??t) * vStartScale;},
			start,end,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit<Quaternion> tweenRotation(this Transform transform,
		Quaternion qStart,Quaternion qEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<Quaternion>(
			(Quaternion _qStart,Quaternion _qEnd,float t) => {transform.rotation =
				Quaternion.LerpUnclamped(_qStart,_qEnd,dMapping?.Invoke(t)??t);},
			qStart,qEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit<Quaternion> tweenLocalRotation(this Transform transform,
		Quaternion qStart,Quaternion qEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<Quaternion>(
			(Quaternion _qStart,Quaternion _qEnd,float t) => {transform.localRotation =
				Quaternion.LerpUnclamped(_qStart,_qEnd,dMapping?.Invoke(t)??t);},
			qStart,qEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	#endregion
//----------------------------------------------------------------------------------
	#region CANVASGROUP ROUTINE
	public static TweenRoutineUnit<float> tweenAlpha(this CanvasGroup canvasGroup,
		float alphaStart,float alphaEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<float>(
			(float _alphaStart,float _alphaEnd,float t) => {canvasGroup.alpha =
				Mathf.Lerp(_alphaStart,_alphaEnd,dMapping?.Invoke(t)??t);},
			alphaStart,alphaEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	#endregion
//----------------------------------------------------------------------------------
	#region GRAPHIC ROUTINEUNIT
	/* There are public functions CrossFadeAlpha and CrossFadeColor (Graphic can
	do this because it inherits from MonoBehaviour), but it doesn't return Coroutine
	so I don't know how to stop it in a simple way. 
	Hence, I just use my "usual" tween method. */
	public static TweenRoutineUnit<float> tweenAlpha(this Graphic graphic,
		float alphaStart,float alphaEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<float>(
			(float _alphaStart,float _alphaEnd,float t) => {graphic.color =
				graphic.color.newA(Mathf.Lerp(_alphaStart,_alphaEnd,dMapping?.Invoke(t)??t));},
			alphaStart,alphaEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit<Color> tweenColor(this Graphic graphic,
		Color colorStart,Color colorEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<Color>(
			(Color _colorStart,Color _colorEnd,float t) => {graphic.color =
				Color.Lerp(_colorStart,_colorEnd,dMapping?.Invoke(t)??t);},
			colorStart,colorEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	#endregion
//----------------------------------------------------------------------------------
	#region RECTTRANSFORM ROUTINEUNIT
	public static TweenRoutineUnit<RectTransformData> tweenRectTransform(this RectTransform rectTransform,
		RectTransformData rtDataStart,RectTransformData rtDataEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<RectTransformData>(
			(RectTransformData _rtDataStart,RectTransformData _rtDataEnd,float t) => {
				rectTransform.lerpUnclamped(_rtDataStart,_rtDataEnd,dMapping?.Invoke(t)??t);},
			rtDataStart,rtDataEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit<Vector2> tweenAnchoredPosition(this RectTransform rectTransform,
		Vector2 v2Start,Vector2 v2End,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<Vector2>(
			(Vector2 _v2Start,Vector2 _v2End,float t) => {rectTransform.anchoredPosition =
				Vector2.LerpUnclamped(_v2Start,_v2End,dMapping?.Invoke(t)??t);},
			v2Start,v2End,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit<float> tweenWidth(this RectTransform rectTransform,
		float widthStart,float widthEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<float>(
			(float _widthStart,float _widthEnd,float t) => {
				rectTransform.setWidth(Mathf.Lerp(_widthStart,_widthEnd,dMapping?.Invoke(t)??t));},
			widthStart,widthEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	#endregion
//----------------------------------------------------------------------------------
	#region TMP_TEXT ROUTINEUNIT
	/* To prevent word wrapping to next line when changing font size, set
	tmpText.overflowMode to TextOverflowModes.Overflow before calling these functions. */
	public static TweenRoutineUnit<float> tweenFontSize(this TMP_Text tmpText,
		float startSize,float endSize,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<float>(
			(float _startSize,float _endSize,float t) => {tmpText.fontSize =
				Mathf.LerpUnclamped(_startSize,_endSize,dMapping?.Invoke(t)??t);},
			startSize,endSize,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit sinBumpFontSize(this TMP_Text tmpText,
		float bumpScale,float duration,eTweenLoopMode loopMode=eTweenLoopMode.Once,
		DOnTweenDone dOnDone=null)
	{
		float fontSizeStart = tmpText.fontSize;
		float deltaBump = fontSizeStart * (bumpScale-1.0f);
		return new TweenRoutineUnit(
			(float t) => {tmpText.fontSize = fontSizeStart+deltaBump*MathfExtension.sinBump(t);},
			duration,loopMode,dOnDone,0.0f,false
		);
	}
	public static TweenRoutineUnit<float> tweenVerticesAlpha(this TMP_Text tmpText,
		float alphaStart,float alphaEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null,bool bFirstFrame=false)
	{
		if(bFirstFrame){ //otherwise mesh hasn't initialized, and first frame value will be wrong
			tmpText.ForceMeshUpdate();}
		return new TweenRoutineUnit<float>(
			(float _alphaStart,float _alphaEnd,float t) => {tmpText.setVerticesAlpha(
				(byte)(255*Mathf.Lerp(_alphaStart,_alphaEnd,dMapping?.Invoke(t)??t)));},
			alphaStart,alphaEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit<Color32> tweenVerticesColor(this TMP_Text tmpText,
		Color32 colorStart,Color32 colorEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null,bool bFirstFrame=false)
	{
		if(bFirstFrame){
			tmpText.ForceMeshUpdate();}
		return new TweenRoutineUnit<Color32>(
			(Color32 _colorStart,Color32 _colorEnd,float t) => {
				tmpText.setVerticesColor(Color32.Lerp(_colorStart,_colorEnd,dMapping?.Invoke(t)??t));},
			colorStart,colorEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	/* One limitation is that tmpText GameObject has to be active when this routine is created.
	Will find workaround later. */
	public static TypewriteRoutineUnit typewrite(this TMP_Text tmpText,
		float speed,byte alphaStart=0,byte alphaEnd=255)
	{
		return new TypewriteRoutineUnit(tmpText,speed,alphaStart,alphaEnd);
	}
	#endregion
//----------------------------------------------------------------------------------
	#region SPRITERENDERER ROUTINEUNIT
	public static TweenRoutineUnit<float> tweenAlpha(this SpriteRenderer spriteRenderer,
		float alphaStart,float alphaEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<float>(
			(float _alphaStart,float _alphaEnd,float t) => {spriteRenderer.color =
				spriteRenderer.color.newA(Mathf.Lerp(_alphaStart,_alphaEnd,dMapping?.Invoke(t)??t));},
			alphaStart,alphaEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	#endregion
//----------------------------------------------------------------------------------
	#region MATERIAL ROUTINEUNIT
	public static TweenRoutineUnit<float> tweenAlpha(this Material material,
		float alphaStart,float alphaEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<float>(
			(float _alphaStart,float _alphaEnd,float t) => {material.color = material.color.newA(
				Mathf.Lerp(_alphaStart,_alphaEnd,dMapping?.Invoke(t)??t));},
			alphaStart,alphaEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit<Color> tweenColor(this Material material,
		Color colorStart,Color colorEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<Color>(
			(Color _colorStart,Color _colorEnd,float t) => {material.color =
				Color.Lerp(_colorStart,_colorEnd,dMapping?.Invoke(t)??t);},
			colorStart,colorEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit<float> tweenMaterialProperty(this Material material,
		int propertyId,float start,float end,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<float>(
			(float _start,float _end,float t) => {material.SetFloat(
				propertyId,Mathf.Lerp(_start,_end,dMapping?.Invoke(t)??t));},
			start,end,duration,loopMode,dOnDone,tStart,false
		);
	}
	#endregion
//----------------------------------------------------------------------------------
	#region AUDIOSOURCE ROUTINEUNIT
	public static TweenRoutineUnit<float> tweenVolume(this AudioSource audioSource,
		float volumeStart,float volumeEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<float>(
			(float _volumeStart,float _volumeEnd,float t) => {audioSource.volume =
				Mathf.LerpUnclamped(_volumeStart,_volumeEnd,dMapping?.Invoke(t)??t);},
			volumeStart,volumeEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit fadeOut(this AudioSource audioSource,
		float duration,DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return audioSource.tweenVolume(audioSource.volume,0.0f,duration,
			dMapping:dMapping,dOnDone:dOnDone);
	}
	#endregion
//----------------------------------------------------------------------------------
	#region AUDIOLISTENER ROUTINEUNIT
	public static TweenRoutineUnit<float> tweenVolumeAudioListener(
		float volumeStart,float volumeEnd,float duration,
		eTweenLoopMode loopMode=eTweenLoopMode.Once,float tStart=0.0f,
		DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return new TweenRoutineUnit<float>(
			(float _volumeStart,float _volumeEnd,float t) => {AudioListener.volume =
				Mathf.LerpUnclamped(_volumeStart,_volumeEnd,dMapping?.Invoke(t)??t);},
			volumeStart,volumeEnd,duration,loopMode,dOnDone,tStart,false
		);
	}
	public static TweenRoutineUnit fadeOutAudioListener(
		float duration,DMapping dMapping=null,DOnTweenDone dOnDone=null)
	{
		return tweenVolumeAudioListener(AudioListener.volume,0.0f,duration,
			dMapping:dMapping,dOnDone:dOnDone);
	}
	#endregion
//----------------------------------------------------------------------------------
} //end static class TweenRoutineCollection

	#endregion
//=====================================================================================

//=====================================================================================
	#region CHAIN ENUMERATOR
/* Simple chaining of IEnumerator. Once one IEnumerator finishes,
the next starts immediately, until entire chain is traversed.
For more sophisticated control, use StepEnumerator class. */
public class ChainEnumerator : IEnumerator{
	protected IEnumerator[] aItr;
	protected int indexCurrent =0;
	public bool bReverse =false;
	public ChainEnumerator(params IEnumerator[] aItr){
		this.aItr = aItr;
	}
	public int Count{ get{return aItr.Length;} }
	public IEnumerator this[int i]{ get{return aItr[i];} }
	public object Current{ get{return aItr[indexCurrent].Current;} }
	public bool MoveNext(){
		while(indexCurrent>=0 && indexCurrent<aItr.Length){
			if(aItr[indexCurrent].MoveNext())
				return true;
			indexCurrent = bReverse ? indexCurrent-1 : indexCurrent+1;
		}
		return false;
	}
	public void Reset(){
		reset(false);
	}
	public void reset(bool bKeepState){
		indexCurrent = bReverse ? aItr.Length-1 : 0;
		if(!bKeepState){
			for(int i=0; i<aItr.Length; ++i)
				aItr[i].Reset();
		}
	}
}
	#endregion
//=====================================================================================

//=====================================================================================
	#region PARALLEL ENUMERATOR
interface IStopHandler{
	void onStop();
}
/* This class requires that you pass in a MonoBehaviour to be used to run the IEnumerators. */
//Usage Example: StartCoroutine(new ParallelEnumerator(this,rf1(),rf2());
public class ParallelEnumerator : IEnumerator,IStopHandler{
	protected MonoBehaviour monoBehaviour;
	protected IEnumerator[] aItr;
	protected Coroutine[] aCoroutine;
	protected int indexCurrent;
	protected bool bFirstRun =true;
	public int Count{ get{return aItr.Length;} }
	public IEnumerator this[int i]{ get{return aItr[i];} }
	public ParallelEnumerator(MonoBehaviour monoBehaviour,params IEnumerator[] aItr){
		this.monoBehaviour = monoBehaviour;
		this.aItr = aItr;
		this.aCoroutine = new Coroutine[aItr.Length];
	}
	public object Current{ get{return aCoroutine[indexCurrent];} }
	public bool MoveNext(){
		if(bFirstRun){
			for(int i=0; i<aItr.Length; ++i)
				aCoroutine[i] = monoBehaviour.StartCoroutine(aItr[i]);
			bFirstRun = false;
			indexCurrent = -1;
		}
		if(++indexCurrent < aItr.Length)
			return true;
		return false;
	}
	public void Reset(){
		reset(false);
	}
	public void reset(bool bKeepState){
		for(int i=0; i<aItr.Length; ++i){
			if(aCoroutine[i] != null)
				monoBehaviour.StopCoroutine(aCoroutine[i]);
			if(!bKeepState)
				aItr[i].Reset();
		}
		bFirstRun = true;
	}
	public void onStop(){
		if(indexCurrent < aItr.Length){ //still running
			for(int i=0; i<aCoroutine.Length; ++i)
				monoBehaviour.StopCoroutine(aCoroutine[i]);
		}
	}
}
	#endregion
//=====================================================================================

//=====================================================================================
	#region MISCELLENOUS ROUTINEUNIT
public class TypewriteRoutineUnit : IRoutineUnit{
	protected TMP_Text tmpText;
	protected string text;
	public float speed;
	public byte alphaStart;
	public byte alphaEnd;

	private float indexTime =0.0f;
	private int nextIndex =0;
	private int length; //length of text AFTER removing rich text tags
	private bool bInit =false;

	public TypewriteRoutineUnit(
		TMP_Text tmpText,float speed,byte alphaStart,byte alphaEnd)
	{
		this.tmpText = tmpText;
		this.speed = speed;
		this.alphaStart = alphaStart;
		this.alphaEnd = alphaEnd;
		this.text = tmpText.text;
		tmpText.ForceMeshUpdate();
		//Reset();
	}
	public string Text{
		get{ return text; }
		set{
			text = value;
			bInit = false;
		}
	}
	/* yield return WaitForSeconds is also interesting, but doing this
	you can fine-control the time better when speed is fast. */ 
	public object Current{ get{return null;} }
	public bool MoveNext(){
		/* Traditional method of adding one character to tmpText.text at a time is terrible
		because since string is immutable, it ALLOCATES memory every time it is called,
		for the size of approx. 2*that string's length not counting overhead.
		So overall, for string length n, the process will generate approx. n*n/2 garbage.
		Moreover, it causes TMPro to regenerate mesh every time, which is extremely slow,
		not to mention the problem of word-wrapping, accent symbols in Thai language, etc.
		Method below generate garbage only ONCE, where the text is immediately in place,
		and changing vertex color is much faster. */
		if(!bInit){
			Reset();
			bInit = true;
		}
		indexTime += speed*Time.deltaTime;
		int endIndex = Mathf.Min((int)indexTime,length); //return past-the-end index (C++ concept)
		if(nextIndex < endIndex){
			for(int i=nextIndex; i<endIndex; ++i)
				tmpText.setCharacterAlpha(i,alphaEnd,false);
			tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
			nextIndex = endIndex;
		}
		return nextIndex<length;
	}
	public void Reset(){
		if(tmpText.text != text){
			tmpText.text = text;
			tmpText.ForceMeshUpdate();
		}
		for(int i=0; i<text?.Length; ++i)
			tmpText.setCharacterAlpha(i,alphaStart,false);
		tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
		indexTime = 0.0f;
		nextIndex = 0;
		length = tmpText.GetParsedText().Length; //Credit: Filip, SO
	}
	public void skip(){
		for(int i=nextIndex; i<text.Length; ++i)
			tmpText.setCharacterAlpha(i,alphaEnd,false);
		tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
		nextIndex = text.Length;
	}
}

	#endregion
//=====================================================================================

//=====================================================================================
	#region ROUTINE BUILDING BLOCKS
/* Trigger is designed so that it only returns true on the FRAME it is set.
This should be useful for checking skip condition in IEnumerator function,
so that skip condition does not stay as true if it is triggered while
IEnumerator is not running.
CAUTION: Be careful of the order of execution! For example, setting it
in LateUpdate() cannot trigger anything in the Coroutine because
that will be run next frame, where Trigger will have become false. */
public class FrameTrigger{
	public double triggerTime;
	public void set(){
		triggerTime = Time.timeAsDouble;
	}
	public void clear(){
		triggerTime = -1.0;
	}
	public static implicit operator bool(FrameTrigger f){
		return f.triggerTime==Time.timeAsDouble;
	}
}
/* Of course, Time.timeAsDouble will lose precision when become large, but
it will still retain less than 1ms precision even after a millennia
(Credit: brucedawson, randomascii.wordpress.com) */
public class Cooldown{
	private double endTime;
	public void set(float duration){
		endTime = Time.timeAsDouble+duration;
	}
	public void clear(){
		endTime = -1.0;
	}
	public static implicit operator bool(Cooldown c){
		return c.endTime>Time.timeAsDouble;
	}
}
	#endregion
//=====================================================================================

} //end namespace Chameleon
