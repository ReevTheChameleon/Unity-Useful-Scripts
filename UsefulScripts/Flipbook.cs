/************************************************************************
 * FLIPBOOK (v2.2.2)
 * by Reev the Chameleon
 * 14 Feb 3
*************************************************************************
A class for managing array of Sprite that represents a flipbook. It keeps
the information about the current index in the flipbook, as well as
controls the flipping coroutine so user do not need to maintain it themselves.
Using this class, user will flip the sprites through this class rather than
poking directly at SpriteRenderer, despite still having full access to it.
This class behaves somewhat like how Button Component alter Image Component
for UI animation.

Note: By default, Unity uses AnimationClip to represent a flipbook.
While this is much more flexible, I have found through profiling that
doing so takes about 4x CPU time more than just manually swapping sprite
in SpriteRenderer. Therefore, I decided to create this script for simple animation.

Update v2.0: Change class to inherit from MonoBehaviour so it can manage coroutine by itself
and make the class usable with Image. Also make its inspector easier to use.
Update v2.0.1: Fix bug setting not being serialized, and add startIndex field in inspector
Update v2.1: Add support for undoing and prefab overriding FlipRate
Update v2.2: Make Undo works independent of inspector, and switch to LoneCoroutine for improved safety
Update v2.2.1: Revise code to match change in LoneCoroutine
Update v2.2.2: Fix NullReferenceException bug when attaching Flipbook to GameObject the first time
*/

using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

using Object = UnityEngine.Object;

namespace Chameleon{

/* Note: If aSprite is null OR has zero length, most functions in this class will throw.
I decided to make it so to avoid having to do check in every function, but at the cost
that users know what they are doing. */
public partial class Flipbook : MonoBehaviour{
	private enum eFlipOnStartBehaviour{Loop=0,Once,Once_Destroy,Once_Disable}
	[SerializeField][HideInInspector] Component flipTarget;
	[SerializeField] Sprite[] aSprite;
	[SerializeField] float flipRate = 24.0f;
	[SerializeField][GrayOnPlay] bool bFlipOnStart;
	[SerializeField][GrayOnPlay] eFlipOnStartBehaviour flipOnStartBehaviour;
	public int startIndex; //For ObjectPooler, once reenabled will start from this index
	
	private int index = 0;
	private LoneCoroutine routineFlip = new LoneCoroutine();
	private WaitForSeconds wait;
	
	public delegate void DOnSpriteChange(Object target,Sprite sprite);
	public event DOnSpriteChange evOnSpriteChange;

	private void onSpriteRendererSpriteChange(Object target,Sprite sprite){
		((SpriteRenderer)target).sprite = sprite;
	}
	private void onImageSpriteChange(Object target,Sprite sprite){
		((Image)target).sprite = sprite;
	}
	private bool setFlipTargetDelegate(){
		/* Expectedly, this will be called only in editor and in Awake(),
		and will not affect user subscription. */
		if(!flipTarget){
			evOnSpriteChange = null;
			return true;
		}
		switch(flipTarget.GetType().Name){
			case nameof(SpriteRenderer):
				evOnSpriteChange = onSpriteRendererSpriteChange;
				return true;
			case nameof(Image):
				evOnSpriteChange = onImageSpriteChange;
				return true;
			default:
				evOnSpriteChange = null;
				return false;
		}
		/* Subscribing to own event will not cause memory leak because GC
		uses reachability (not reference count) for garbage collection.
		If the object is destroyed, the fact that it is linked with itself
		does not make it reachable (Credit: StackOverthrow, SO) */
	}

	void Awake(){
		setFlipTargetDelegate();
		wait = new WaitForSeconds(1/flipRate);
	}
	void OnEnable(){
		Index = startIndex;
		if(bFlipOnStart){
			switch(flipOnStartBehaviour){
				case eFlipOnStartBehaviour.Loop:
					flipLoop();
					break;
				case eFlipOnStartBehaviour.Once:
					flipOnce();
					break;
				case eFlipOnStartBehaviour.Once_Destroy:
					flipOnce(()=>{Destroy(gameObject);});
					break;
				case eFlipOnStartBehaviour.Once_Disable:
					flipOnce(()=>{gameObject.SetActive(false);});
					break;
			}
		}
	}
	void OnDisable(){
		routineFlip.stop();
	}
	public int Index{
		get{ return index; }
		set{
			if(aSprite==null || aSprite.Length<=0){
				index = 0;
				return;
			}
			index = Mathf.Clamp(value,0,aSprite.Length-1);
			evOnSpriteChange?.Invoke(flipTarget,aSprite[index]);
		}
	}
	public int StartIndex{
		get{ return startIndex; }
		set{
			startIndex =
				(aSprite==null || aSprite.Length<=0) ?
				0 :
				Mathf.Clamp(value,0,aSprite.Length-1)
			;
		}
	}
	public int Length{
		get{ return aSprite.Length; }
	}
	public bool IsFlipping{ get{return routineFlip.IsRunning;} }
	public float FlipRate{
		get{ return flipRate; }
		set{
			flipRate = value<0.0001f ? 0.0001f : value;
			wait = new WaitForSeconds(1/flipRate);
			/* Currently running flipping routine does NOT stop. This means you
			may have to wait for last wait to ends before seeing flipRate change.
			(For example, if last flipRate is 0.5, you may still have to wait for that 2 secs)
			Will evaluate and change if found undesirable. */
		}
	}
	public Sprite CurrentSprite{
		get{ return aSprite[index]; }
	}
	public Sprite getSprite(int index){
		if(index>=0 && index<aSprite.Length)
			return aSprite[index];
		return null;
	}
	public int next(){
		if(index+1 >= aSprite.Length)
			return -1;
		evOnSpriteChange?.Invoke(flipTarget,aSprite[++index]);
		return index;
	}
	public int previous(){
		if(index-1 < 0)
			return -1;
		evOnSpriteChange?.Invoke(flipTarget,aSprite[--index]);
		return index;
	}
	public int nextWrap(){ //wrap around
		index = (index+1) % aSprite.Length;
		evOnSpriteChange?.Invoke(flipTarget,aSprite[index]);
		return index;
	}
	public int previousWrap(){ //wrap around
		int len = aSprite.Length;
		index = (index+len-1) % len;
		evOnSpriteChange?.Invoke(flipTarget,aSprite[index]);
		return index;
	}
	public void setASprite(Sprite[] aSprite){
		this.aSprite = aSprite;
		index = 0;
		evOnSpriteChange?.Invoke(flipTarget,aSprite[0]);
	}
	public void setSprite(Sprite sprite,int index){
		if(index>=0 && index<aSprite.Length)
			aSprite[index] = sprite;
	}
	public void flipOnce(Action dOnFlipOnceFinish=null){
		routineFlip.start(this,flipOnceRoutine(dOnFlipOnceFinish));
	}
	public void flipLoop(){
		routineFlip.start(this,flipLoopRoutine());
	}
	public void stop(){
		routineFlip.stop();
	}
	private IEnumerator flipOnceRoutine(Action dOnFlipOnceFinish){
		yield return wait;
		while(next() != -1)
			yield return wait;
		dOnFlipOnceFinish?.Invoke();
	}
	private IEnumerator flipLoopRoutine(){
		while(true){
			yield return wait;
			nextWrap();
		}
	}
	#if UNITY_EDITOR
	void OnValidate(){
		setFlipTargetDelegate();
		FlipRate = flipRate;
		StartIndex = startIndex;
	}
	#endif
}

#if UNITY_EDITOR
public partial class Flipbook{
	[CustomEditor(typeof(Flipbook))]
	[CanEditMultipleObjects]
	class FlipbookEditor : Editor{
		private Flipbook targetAs;

		void OnEnable(){
			targetAs = (Flipbook)target;
		}
		public override void OnInspectorGUI(){
			EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(targetAs.flipTarget)));
			if(targetAs.evOnSpriteChange == null){
				EditorGUILayout.HelpBox(
					"Flip Target type not supported!",
					MessageType.Warning //Credit: Bunny83, UA
				);
			}
			EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(targetAs.aSprite)));
			EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(targetAs.flipRate)));
			EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(targetAs.bFlipOnStart)));
			if(targetAs.bFlipOnStart)
				EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(targetAs.flipOnStartBehaviour)));

			/* In Edit Mode, Index reflects to startIndex (so Undo is handled naturally) */
			if(!EditorApplication.isPlaying){
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField(
					serializedObject.FindProperty(nameof(targetAs.startIndex)),
					new GUIContent("Index")
				);
				if(EditorGUI.EndChangeCheck())
					targetAs.Index = targetAs.startIndex;
			}
			else{ //In Play Mode, Index only affects Index, and cannot be Undone
				EditorGUI.BeginChangeCheck();
				int indexUser = EditorGUILayout.IntField("Index",targetAs.index);
				if(EditorGUI.EndChangeCheck())
					targetAs.Index = indexUser;
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}
#endif

} //end namespace Chameleon
