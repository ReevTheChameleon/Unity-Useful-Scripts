/************************************************************************
 * ANIMATIONPLAYER (v2.1)
 * by Reev the Chameleon
 * 16 Mar 3
*************************************************************************
A class designed to be THE solution for animation.
Built on top of the lower level Playable API, It is designed so that one can work with
animation directly without having to deal with Animator and AnimatorController asset.
It has about 0-10% better performance than Animator, and addresses the following disadvantage
of Animator system (aka formerly Mecanim):
- You need to define ALL animations, transitions, and interruptions beforehand,
which prevent you from animate things flexibly and dynamically.
Using this class, you just call play(AnimationClip), transitionTo(AnimationClip),
and so on, passing in ANY AnimationClips or AnimationTree at anytime you like.
- In order to make transitions, you need to set some cryptic paramenters
which is defined by strings and cannot be seen unless you inspect the Animator Window,
not to mention that you need to call SetFloat, SetBool, SetTrigger, and so on,
hoping that the parameter type matches and that the (string) name is correct.
Again, using this class, you just call the function corresponding to what you
want to do. Also, transition functions accepts eTransitionInterruptMode argument,
allowing you to just specify how you want it to interrupt the current transition, if exists.
- For one-time animation, once the animation ends, you have no choice but
have to accept that Unity will keep animating the last frame over and over,
which is performance-wise wasteful, but stopping it (which can ONLY be achieve by
disabling the entire Animator Component and therefore will also stop everything)
will snap the animation back to the beginning
Using this class, you will get notified when the animation finishes, at which time
you will have a choice to either keep repeating the last frame or save the end state
as new default values before just stopping that layer.
- Animator will keep playing the layers even when their weights are 0.0f.
Combined with the fact that you have to define ALL animations beforehand, this usually
means that you will have multiple layers that are rarely used (such as dead, fly, etc.),
but still get processed every frame before being multiplied with 0 to discard it.
This is tremendously wasteful, and most advices just point in the direction of either
having many Animators or have less layers, which misses the point.
Using this class, any layers or nodes in AnimationTree with weight 0.0f will automatically
hibernate and eat zero performance in animation process. It will just spring to life
automatically when you set weight to nonzero once more.
- Well-known issue of Animator lockdown, where ANY parameters defined in ANY clips used
in Animator are locked and cannot be altered by scripts, inspectors, etc. EVEN when
they are not currently playing. This severely limits users from adding much parameters
to their animations in fear that they won't be able to change them by scripts later.
The best advice from Unity itself on this issue is to override the effect of Animator
in LateUpdate(), which, again, is just beating around the bush.
Using this class, you can just call resetBinding() or resetDefaultValue() to both
reset default values AND inform Unity to ONLY lock parameters of currently playing clips.
- You cannot add animated properties to existing clips (the "Add Property" button
will be grayed out in Animation Window) unless they exist SOMEWHERE in Animator or
Animation Component. This is bizarre logic that just complicate Animation workflow.
Using this class, you just right-click the component and select "Unlock Animation Window"
so that ANY clips in the project will appear and can be edited under this GameObject.
- You have to set AnimationEvent in the inspector using magic string method name.
In newer version of Unity, there will be a dropdown showing list of functions
on the GameObject which AnimationClip is bound to. However, you still need to write
the function somewhere in your script, and if you change the function name there,
the one assigned in the inspector will not update (will show as "Function Not Supported" though).
Using this class, you call addAnimationAction on PlayableController to add an Action
to the PlayableController at specific time. While it does not accept arguments, lambda expression
is supported, so you can use lambda capture instead, which is arguably more versatile.
Also, from testing, the performance of this class is at least on par, if not slightly better,
than Unity's AnimationEvent system.

Usage:
- Call play, transitionTo, stopLayer, and so on, to control your animation.
- This class comes equipped with AnimationTree class, which allows you to construct
something similar to Unity's BlendTree in the inspector. Admittedly, it does not have
fancy features like doing 2D or polar blending, but rather allows you to control
the weight of each node directly, which I feel is more appropriate. You can use them
in function calls like how you would use AnimationClip, as well as use them in
TreePlayableController's syncWeight and lerpWeight functions.
- Calling play and transitionTo functions will return you a PlayableController object,
which you can use to control played animation by calling setTime, setRelativeTime,
setSpeed, setSlotWeight, and so on. You can also just call getPlayableController()
directly, passing in AnimationClip or AnimationTree as argument, to obtain corresponding
PlayableController at anytime.
- right-click AnimationPlayer Component and select "Unlock Animation Window" to display
and edit ANY AnimationClips in the project via Animation Window when this GameObject
is selected.
- Add AnimationAction to PlayableController to trigger an Action at specific time.

!!! EXPERIMENTAL: This class is one of the most complex class I have ever written,
and while I have tested it much, it may still have some bugs. It is currently
still under bug observation !!!

Update v1.1: Revise code so it works with root motion
Update v1.1.1: Add function to obtain root motion deltas
Update v1.1.2: Add indexer to AnimationTree class and fix AnimationTree fadeout bug
Update v2.0: Add support for AnimationAction and remove unused classes
Update v2.0.1: Add WaitEndAnimation property so user can wait for animation end in Coroutine
Update v2.1: Fix bug looping AnimationAction and add warning regarding prefabs
*/

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;
using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace Chameleon{

//======================================================================================
	#region ANIMATIONTREE
[Serializable]
public class AnimationTree : ISerializationCallbackReceiver{
	[NonSerialized] public AnimationClip clip;
	[NonSerialized] public List<AnimationTree> lTree;
	[NonSerialized] public float weight =1.0f;

	public AnimationTree clone(){
		AnimationTree resultTree = new AnimationTree();
		resultTree.clip = clip;
		resultTree.weight = weight;
		if(lTree != null){
			resultTree.lTree = new List<AnimationTree>();
			for(int i=0; i<lTree.Count; ++i)
				resultTree.lTree.Add(lTree[i].clone());
		}
		return resultTree;
	}
	public AnimationTree this[int i]{
		get{
			if(i>=0 && i<lTree?.Count)
				return lTree[i];
			return null;
		}
	}
	//.....................................................................
		#region SERIALIZATION
	[Serializable]
	private struct SerializableNode{
		public AnimationClip clip;
		public int subtreeCount;
		public float weight;
		public SerializableNode(AnimationClip in_clip,int in_subtreeCount,float in_weight){
			clip = in_clip;
			subtreeCount = in_subtreeCount;
			weight = in_weight;
		}
	}
	[SerializeField] List<SerializableNode> lNode = new List<SerializableNode>();
	public void OnBeforeSerialize(){
		lNode.Clear();
		storeNode(this);
	}
	private void storeNode(AnimationTree tree){
		int subtreeCount = tree.lTree?.Count ?? -1;
		lNode.Add(new SerializableNode(tree.clip,subtreeCount,tree.weight));
		for(int i=0; i<subtreeCount; ++i)
			storeNode(tree.lTree[i]);
	}
	public void OnAfterDeserialize(){
		int index = 0;
		restoreNode(this,ref index);
	}
	private void restoreNode(AnimationTree tree,ref int index){
		tree.clip = lNode[index].clip;
		tree.weight = lNode[index].weight;
		int subtreeCount = lNode[index].subtreeCount;
		if(subtreeCount >= 0)
			tree.lTree = new List<AnimationTree>(subtreeCount);
		++index;
		for(int i=0; i<subtreeCount; ++i){
			AnimationTree subtree = new AnimationTree();
			tree.lTree.Add(subtree);
			restoreNode(subtree,ref index);
		}
	}
		#endregion
	//.....................................................................
		#region PROPERTYDRAWER & RELATED
	/* NOT yet supporting Undo! Will consider this feature later (because dealing with
	Undo of ReorderableList is quite difficult). */
	#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(AnimationTree))]
	class AnimationTreeDrawer : PropertyDrawer{
		private bool bInit = false;
		private AnimationTree targetAs;
		private class ReorderableListInfo{
			public ReorderableList r;
			public int lineTaken;
		}
		private Dictionary<AnimationTree,ReorderableListInfo> dictReorderableList =
			new Dictionary<AnimationTree,ReorderableListInfo>();

		private void init(SerializedProperty property){
			targetAs =
				(AnimationTree)fieldInfo.GetValue(property.serializedObject.targetObject);
			dictReorderableList.Clear();
			if(targetAs.lTree == null)
				targetAs.lTree = new List<AnimationTree>(); //initialize just for the root
			prepareDictReorderableList(targetAs);
			bInit = true;
		}
		public override void OnGUI(Rect position,SerializedProperty property,GUIContent label){
			//EditorGUI.PropertyField(position,property.FindPropertyRelative("lNode"));
			if(!bInit)
				init(property);
			EditorGUI.BeginProperty(
				position,null,property.FindPropertyRelative(nameof(AnimationTree.lNode)));
			drawRoot(position,targetAs,label);
			EditorGUI.EndProperty();
			if(GUI.changed)
				EditorUtility.SetDirty(property.serializedObject.targetObject);
		}
		private int prepareDictReorderableList(AnimationTree tree){ //return subnodeCount
			ReorderableListInfo rInfo = new ReorderableListInfo();
			dictReorderableList.Add(tree,rInfo);
			rInfo.lineTaken = 3;
			if(tree.lTree != null){ //is tree
				++rInfo.lineTaken;
				rInfo.r = createReorderableList(tree);
				if(tree.lTree.Count == 0)
					++rInfo.lineTaken; //for empty list
				else{
					for(int i=0; i<tree.lTree.Count; ++i)
						rInfo.lineTaken += prepareDictReorderableList(tree.lTree[i]);
				}
			}
			return rInfo.lineTaken;
		}
		private void drawRoot(Rect position,AnimationTree tree,GUIContent label){
			position.height = EditorGUIUtility.singleLineHeight;
			EditorGUI.LabelField(position,label,EditorStyles.boldLabel);
			Rect originalPos = position;
			position.x = originalPos.xMax - 100.0f;
			position.width = 100.0f;
			position = originalPos; 
			position.y += EditorGUIUtility.singleLineHeight;
			ReorderableListInfo rInfo;
			if(dictReorderableList.TryGetValue(tree,out rInfo))
				rInfo.r?.DoList(position);
			position.y += rInfo.r.GetHeight();
		}
		private void drawTree(Rect position,AnimationTree tree,GUIContent label){
			position.height = EditorGUIUtility.singleLineHeight;
			EditorGUI.LabelField(position,label,EditorStyles.boldLabel);
			Rect originalPos = position;
			position.x = originalPos.xMax - 100.0f;
			position.width = 100.0f;
			if(tree.lTree==null){ //clip
				if(GUI.Button(position,"Change to Tree")){
					tree.lTree = new List<AnimationTree>();
					tree.clip = null;
					bInit = false;
				}
				position = originalPos; 
				position.y += EditorGUIUtility.singleLineHeight;
				tree.clip = (AnimationClip)EditorGUI.ObjectField(
					position,
					"Animation Clip",
					tree.clip,
					typeof(AnimationClip),
					false
				);
				position.y += EditorGUIUtility.singleLineHeight;
			}
			else{ //tree
				if(GUI.Button(position,"Change to Clip")){
					tree.lTree = null;
					bInit = false;
				}
				position = originalPos; 
				position.y += EditorGUIUtility.singleLineHeight;
				ReorderableListInfo rInfo;
				if(dictReorderableList.TryGetValue(tree,out rInfo))
					rInfo.r?.DoList(position);
				position.y += rInfo.r.GetHeight();
			}
			tree.weight = EditorGUI.Slider(
				position,
				"Weight",
				tree.weight,
				0.0f,
				1.0f
			);
		}
		public override float GetPropertyHeight(SerializedProperty property,GUIContent label){
			if(!bInit)
				init(property);
			return getTreeHeight(targetAs)-EditorGUIUtility.singleLineHeight;
			//root doesn't have topmost line
		}
		private float getTreeHeight(AnimationTree tree){
			ReorderableListInfo rInfo;
			if(targetAs!=null && dictReorderableList.TryGetValue(tree,out rInfo))
				return EditorGUIUtility.singleLineHeight * rInfo.lineTaken;
			return 0.0f;
		}
		private ReorderableList createReorderableList(AnimationTree tree){
			int indexActive = -1;
			ReorderableList r =
				new ReorderableList(tree.lTree,typeof(AnimationTree),true,false,true,true);
			r.drawElementCallback = (Rect rect,int index,bool bActive,bool bFocus) => {
				if(r.list.Count == 0)
					return;
				if(bActive)
					indexActive = index;
				else if(indexActive==index) //but not active
					indexActive = r.list.Count-1;
				drawTree(
					rect,
					(AnimationTree)r.list[index],
					new GUIContent("Element "+index)
				);
				//EditorHelper.copyPasteContextMenuArrayItem(rect,r.list,index);
			};
			r.onAddCallback = (ReorderableList rlist) => {
				rlist.list.Insert(
					indexActive+1,
					new AnimationTree()
				);
				indexActive = rlist.list.Count-1;
				bInit = false;
			};
			r.onRemoveCallback = (ReorderableList rlist) => {
				rlist.list.RemoveAt(indexActive);
				indexActive = rlist.list.Count-1;
				bInit = false;
			};
			r.elementHeightCallback += (int index)=>getTreeHeight(tree.lTree[index]);

			return r;
		}
	}
	#endif
		#endregion
	//.....................................................................
}
//---------------------------------------------------------------------------------
	#region ANIMATIONTREE STRUCTURAL COMPARER
public class AnimationTree1StructuralComparer : IEqualityComparer<AnimationTree>{
	/* Credit Idea: Matthew Watson, SO */
	/* For some reasons, explicitly specify null cases is faster than, say,
	x?.clilp!=y?.clip. */
	public bool Equals(AnimationTree x,AnimationTree y){
		if(x==y)
			return true; //also check case of both null
		if(x==null || y==null)
			return false;
		if(x.clip != y.clip) //now both are not null
			return false;
		if(x.lTree == y.lTree) //also check case of both null
			return true;
		if(x.lTree==null || y.lTree==null)
			return false;
		if(x.lTree.Count != y.lTree.Count)
			return false;
		for(int i=0; i<x.lTree.Count; ++i){
			if(!Equals(x.lTree[i],y.lTree[i]))
				return false;
		}
		return true;
	}
	public int GetHashCode(AnimationTree obj){ //Credit: Matthew Watson, Jon Skeet, SO
		if(!obj.clip)
			return 0;
		int result = 17*23 + obj.clip.GetHashCode();
		for(int i=0; i<obj.lTree?.Count; ++i)
			/* (Credit: Glen Hughes, SO) If not unchecked, OverFlowException may be thrown */
			unchecked{ result = result*23 + obj.lTree[i].GetHashCode(); }
		return result;
	}
}
	#endregion
//---------------------------------------------------------------------------------
	#endregion
//======================================================================================

//======================================================================================
	#region PLAYABLECONTROLLER CLASSES
//---------------------------------------------------------------------------------
	#region PLAYABLECONTROLLER
public class PlayableController{
	/* I would like to use "protected" for some fields, but C# does NOT allow you to
	access protected member VIA INSTANCE; you can only access it via inheritance (Credit: Yeldar Kurmangaliyev, SO)
	Ironically, this is added security required due to C# not allowing you to change
	accessibility in the inheritance chain, and rendered protected less useful than in C++. */
	protected AnimationPlayer owner;
	internal Playable playable;
	internal int layerIndex;
	internal TreePlayableController parentController;
	internal int parentPort;
	internal bool bHibernate;
	internal double intersectTime;
	/* This is the time where self time equals parent time. This extends the original concept
	of leadtime by taking speed difference account. */
	internal bool bPendingTime;
	/* The reason we need this is because weird behaviour of playble.GetTime() and SetTime()
	that I have learnt the hard way:
	1) If you call GetTime(), it retrieves time of PREVIOUS FRAME.
	2) If you call SetTime(time), it WILL set the time later THIS FRAME.
	3) If you call GetTime() AFTER SetTime(), it gives you the time you have set i.e.
	the one that will be updated THIS FRAME.
	The "incompatibility" of which results in:
	- You CANNOT sync 2 nodes together using SetTime(GetTime()) because the time will differ
	by Time.deltaTime (or something along that line, depending on time update mode).
	- You CANNOT be sure whether node1.GetTime()-node2.GetTime() will give you their true
	time difference, or that one has just been SetTime() and one hasn't, and the true difference
	will be Time.deltaTime less than expected when the graph updates.
	Of course, the cleanest way to solve this issue is to call graph.Evaluate() immediately
	after SetTime() operation to force time update. Unfortunately, this is expensive operation,
	and sometimes this has to be called within recursive function (such as when lerping weights
	where there are more than one weight in the tree that starts from zero), and Unity will
	throw when it detects that Evaluate() is called in that recursive fashion.
	4) (Discovered later) If you call GetTime() IN PrepareFrame(), you will get the time of 
	THIS FRAME, but ONLY for Playable you are on. If you call GetTime() on OTHER Playables,
	you may get either time of previous frame or this frame depending on its relative location
	to your current Playable. This has largely rendered the approach useless if you want to
	be able to set Playable time relative to other (difficult case being the sibling cases).
	5) (Discovered later) If you call GetTime() in LateUpdate(), you will get time THIS FRAME,
	and regardless of how you set it there, the time WILL get updated NEXT FRAME.
	So in the end there is the need to mark this ourselves because Unity does NOT provide
	this functionality. This field is true if the time you get is the one that will be set
	this frame, and is false if it is the time of the previous frame. */

	public bool IsValid{ get{return playable.IsValid();} }
	public virtual PlayableController this[int slot]{ get{return null;} }
	public bool IsPlaying{
		/* This is unexpectedly difficult, because GetPlayState() will get the state
		OF THIS Playable, and not whether it really connects to the root or not.
		So need to spin the function for that ourselves. */
		get{
			PlayableController controller = this;
			while(controller.parentController != null)
				controller = controller.parentController;
			return controller==owner.layerPlayableController;
		}
	}
	/* get the time that IS EXPECTED to be later this frame,
	before LateUpdate() and rendering of this frame.
	Note: if weight==0.0f, may return outdated time. Will review later. */
	public double getAnticipatedTime(){
		if(bPendingTime || !IsPlaying)
			return playable.GetTime();
		return playable.GetTime()+owner.GraphDeltaTime*getAbsoluteSpeed();
	}
	/* I Wanted to have single function with bImmediate, but because it affects many
	portions of the function, and Unity will not allow any functions with
	graph.Evaluate() to be called recursively (even when you carefully craft to avoid
	calling it at recursive level), it is best to just create them as separate functions. */
	public virtual void setTime(double time){
		playable.SetTime(time);
		playable.SetTime(time); //call twice to make sure root motion behaves
		if(!bPendingTime){
			bPendingTime = true;
			owner.scheduleClearPendingTime(this);
		}
		updateIntersectTime();
		updateActionMarker();
	}
	public virtual void setTimeImmediate(double time){
		setTime(time);
		playable.GetGraph().Evaluate();
	}
	public virtual void setRelativeTime(PlayableController playableInfo,double deltaTime){
		setTime(playableInfo.getAnticipatedTime()+deltaTime);
	}
	internal void updateIntersectTime(){
		if(parentController==null){ //should only be null for LayerPlayableInfo and disconnected
			intersectTime = 0.0;
			return;
		}
		double speedRatio = playable.GetSpeed();
		double time = getAnticipatedTime();
		double parentTime = parentController.getSyncAnticipatedTime();
		if(speedRatio == 1.0)
			intersectTime = parentTime-time;
		else
			intersectTime = (speedRatio*parentTime-time)/(speedRatio-1.0);
	}
	private double getSyncAnticipatedTime(){
		/* This gets anticipated time assuming that all hibernations to the root
		has been lifted. */
		if(bHibernate){
			double speed = playable.GetSpeed();
			if(speed == 1.0f)
				return parentController.getSyncAnticipatedTime()-intersectTime;
			else
				return intersectTime +
					(parentController.getSyncAnticipatedTime()-intersectTime)*speed;
		}
		if(bPendingTime)
			return playable.GetTime();
		return playable.GetTime()+owner.GraphDeltaTime*getAbsoluteSpeed();
	}
	protected internal virtual void syncParentTime(){
		if(bPendingTime)
			return;
		double speed = playable.GetSpeed();
		if(speed == 1.0f)
			setTime(parentController.getAnticipatedTime()-intersectTime);
		else{
			setTime(
				intersectTime +
					(parentController.getAnticipatedTime()-intersectTime)*speed
			);
		}
	}
	public double getSpeed(){
		return playable.GetSpeed();
	}
	public void setSpeed(double speed){
		playable.SetSpeed(speed);
		updateIntersectTime();
	}
	public double getAbsoluteSpeed(){
		double accumSpeed = playable.GetSpeed();
		PlayableController controller = this;
		while(controller.parentController != null){
			controller = controller.parentController;
			accumSpeed *= controller.playable.GetSpeed();
		}
		return accumSpeed;
	}
	public virtual void reset(){
		/* If you use root motion and call SetTime only once, your model position
		will snap backward like you have done "time travel" with position.
		It seems that by calling SetTime() twice, you "somehow" trick Unity's
		RootMotion system to think that your model has been there the whole time,
		and so your model will not do root motion snap. (Credit: JosephHK & AubreyH, UF)
		Note: I don't know how this internally works, but apparently, calling SetTime()
		twice does not change playable.GetPreviousTime() nor playable.GetTime(), so it
		must be hidden somewhere else. */
		playable.SetTime(0.0);
		playable.SetTime(0.0);
		bPendingTime = true;
		owner.scheduleClearPendingTime(this);
		playable.SetSpeed(1.0);
		updateIntersectTime();
		//setTime(0.0);
		//setSpeed(1.0);
	}
	public float getSelfWeight(){ //local weight
		return parentController==null ? -1 : parentController.getSlotWeight(parentPort);
	}
	internal PlayableController(
		AnimationPlayer animPlayer,Playable in_playable,int in_layerIndex)
	{
		owner = animPlayer;
		playable = in_playable;
		layerIndex = in_layerIndex;
	}
	internal bool IsConnecting{ get{return IsPlaying && !bHibernate;} }

	//----------------------------------------------------------------------
		#region ANIMATIONACTION RELATED
	internal struct AnimationAction : IComparable<AnimationAction>{
		public float time;
		public Action action;
		public AnimationAction(float time,Action action){
			this.time = time;
			this.action = action;
		}
		public int CompareTo(AnimationAction other){
			return time-other.time>0 ? 1 : -1;
		}
		public static int isAfter(AnimationAction animAction,float time){
			return animAction.time-time>0 ? 1 :-1;
		}
		/* This is because if not cached, every time isAfter function is passed in
		as delegate it will create NEW delegate (Credit: David Ewen, SO)
		(C# delegate is NOT the same as function pointer in C++),
		so we cache it here to avoid extraneous memory allocation (112B) */
		public static Func<AnimationAction,float,int> dIsAfter = isAfter;
	}
	internal List<AnimationAction> lAction;
	protected int nextIndexAction = 0;
	protected float prevActionTime = 0.0f;
	public int AnimationActionCount{ get{return lAction.Count;} }
	public int addAnimationAction(float time,Action action){
		if(action==null){
			return -1;}
		if(lAction == null){
			lAction = new List<AnimationAction>();}
		int index = Algorithm.addSorted(lAction,new AnimationAction(time,action));
		updateActionMarker();
		if(IsPlaying){
			owner.trackAnimationAction(this);}
		return index;
	}
	public void removeAnimationAction(int index){
		lAction.RemoveAt(index);
		if(lAction.Count == 0){
			clearAnimationAction();}
		else{
			updateActionMarker();}
	}
	public void clearAnimationAction(){
		lAction = null;
		nextIndexAction = 0;
		if(IsPlaying){
			owner.untrackAnimationAction(this);}
	}
	protected internal virtual void updateActionMarker(){
		if(lAction == null){
			return;}
		prevActionTime = (float)getAnticipatedTime();
		int index = Algorithm.binaryKeySearch(
			lAction,
			prevActionTime,
			AnimationAction.dIsAfter
		);
		if(index<0){
			index = ~index;}
		nextIndexAction=index;
	}
	/* to be called in LateUpdate by AnimationPlayer.
	Return true if there are still due Actions, false to signal removal from tracking list. */
	/* It is possible to create a ScriptPlayable node and attach it to the front
	of this playable to invoke action when time is reached. However, I have decided
	to discard this approach because it requires extra nodes for all animations with action.
	Also, performance loses to Unity's AnimationEvent system (the one with magic string method).
	However, unlike transitions, these Actions can be called in LateUpdate() which already exists,
	so we can invoke these Actions there and keep performance overhead to minimum. */
	protected internal virtual bool invokeDueAction(){ //default is non-looping
		float time = (float)playable.GetTime();
		/* Formerly check whether speed>=0 or not, but that requires ABSOLUTE speed,
		which takes huge chunk of performance (just normal playable.GetSpeed()
		already takes unreasonable CPU time.)
		Because it is called every frame, we decided to use this workaround (at cost of 4 bytes). */
		if(time-prevActionTime >= 0.0f){ //forward animation
			while(time >= lAction[nextIndexAction].time){
				lAction[nextIndexAction].action?.Invoke();
				if(++nextIndexAction >= lAction.Count){
					return false;}
			}
		}
		else{ //backward animation
			if(nextIndexAction-1 < 0){
				return false;}
			while(time <= lAction[nextIndexAction-1].time){
				lAction[nextIndexAction].action?.Invoke();
				if(--nextIndexAction <= 0){
					return false;}
			}
		}
		prevActionTime = time;
		return true;
	}
		#endregion
	//----------------------------------------------------------------------
}
	#endregion
//---------------------------------------------------------------------------------
	#region CLIP PLAYABLECONTROLLER
public class ClipPlayableController : PlayableController{
	public AnimationClip Clip{get; private set;}
	public int addEndAnimationAction(Action action){
		return addAnimationAction(Clip.length,action);
	}
	public int addAnimationActionAtEvent(int index,Action action){
		return addAnimationAction(Clip.events[index].time,action);
	}
	public class WaitForEndAnimation : CustomYieldInstruction{
		internal bool bDone = false; //can't override set accessor because base doesn't have one
		public override bool keepWaiting{ get{return !bDone;} }
	}
	public WaitForEndAnimation WaitEndAnimation{
		get{
			WaitForEndAnimation wait = new WaitForEndAnimation();
			addEndAnimationAction(()=>{wait.bDone=true;});
			return wait;
		}
	}
	protected internal override void updateActionMarker(){
		if(!Clip.isLooping){
			base.updateActionMarker();}
		else{ //looping
			if(lAction == null){
				return;}
			prevActionTime = (float)getAnticipatedTime();
			float remainderTime = prevActionTime%Clip.length; 
			int round = (int)Math.Floor(prevActionTime/Clip.length); //round down (in case of negative time)
			int remainderIndex = Algorithm.binaryKeySearch(
				lAction,
				remainderTime,
				AnimationAction.dIsAfter
			);
			if(remainderIndex<0){
				remainderIndex = ~remainderIndex;}
			nextIndexAction = round*lAction.Count+remainderIndex;
		}
	}
	protected internal override bool invokeDueAction(){
		if(!Clip.isLooping){
			return base.invokeDueAction();}
		else{ //looping case
			float time = (float)playable.GetTime();
			int remainder = MathfExtension.mod(nextIndexAction,lAction.Count);
			int round = (nextIndexAction-remainder)/lAction.Count;
			if(time-prevActionTime >= 0.0f){ //forward animation
				float indexTime = round*Clip.length + lAction[remainder].time;
				while(time >= indexTime){
					lAction[remainder].action?.Invoke();
					if(++remainder >= lAction.Count){
						remainder = 0;
						++round;
					}
					indexTime = round*Clip.length + lAction[remainder].time;
					//++nextIndexAction;
				}
				nextIndexAction = round*lAction.Count + remainder;
			}
			else{ //backward animation
				if(--remainder < 0){
					remainder = lAction.Count-1;
					--round;
				}
				float indexTime = round*Clip.length + lAction[remainder].time;
				while(time <= indexTime){
					lAction[remainder].action?.Invoke();
					if(--remainder < 0){
						remainder = lAction.Count-1;
						--round;
					}
					indexTime = round*Clip.length + lAction[remainder].time;
					//--nextIndexAction;
				}
				nextIndexAction = round*lAction.Count + remainder+1;
			}
			prevActionTime = time;
			return true;
		}
	}
	internal ClipPlayableController(
		AnimationPlayer animPlayer,AnimationClipPlayable playable,int layerIndex)
		: base(animPlayer,playable,layerIndex)
	{
		Clip = playable.GetAnimationClip();
	}
}
	#endregion
//---------------------------------------------------------------------------------
	#region TREE PLAYABLECONTROLLER
public class TreePlayableController : PlayableController{
	/* Because of weird behaviour that ConnectInput resets Playable's InputWeight to 0
	and DisconnectInput resets it to 1, we need to keep weight information around
	so we can restore the weight correctly when doing dehibernation. */
	/* I am not exactly fund of this "structure of array" approach (sync error-prone),
	but it should be faster than grouping them together ("array of structure") when you
	usually access only one field at a time, and when fields are not that related.
	This seems more natural and results in simpler code for me. */
	internal List<PlayableController> lChildController = new List<PlayableController>();
	internal List<float> lSlotWeight = new List<float>();
	internal int activeSlotCount;
	internal AnimationTree weightTree;
	/* only non-null for root, so we don't need to always make new tree every transition */
	public override PlayableController this[int slot]{ get{return lChildController[slot];} }
	public int SlotCount{ get{return lChildController.Count;} }

	public bool isSlotPlaying(int slot){
		return lChildController[slot]?.IsPlaying==true;
	}
	public override void setTime(double time){
		base.setTime(time);
		for(int i=0; i<lChildController.Count; ++i)
			lChildController[i]?.syncParentTime();
	}
	public override void setTimeImmediate(double time){
		base.setTime(time);
		for(int i=0; i<lChildController.Count; ++i)
			lChildController[i]?.syncParentTime();
		playable.GetGraph().Evaluate();
	}
	public override void setRelativeTime(
		PlayableController playableController,double deltaTime)
	{
		base.setRelativeTime(playableController,deltaTime);
		for(int i=0; i<lChildController.Count; ++i)
			lChildController[i]?.syncParentTime();
	}
	public override void reset(){
		base.reset();
		for(int i=0; i<lChildController.Count; ++i){
			lChildController[i]?.reset();
			//RightPlayable.SetInputWeight(i,1.0f);
			//lSlotWeight[i] = 1.0f;
		}
	}
	internal TreePlayableController(
		AnimationPlayer animPlayer,Playable playable,int layerIndex)
		: base(animPlayer,playable,layerIndex)
	{
		int slotCount = playable.GetInputCount();
		for(int i=0; i<slotCount; ++i){
			lChildController.Add(null);
			lSlotWeight.Add(1.0f);
		}
	}
	protected virtual Playable RightPlayable{ get{return playable;} }
	//You need to SetInputWeight AFTER connecting OR disconnecting, otherwise it resets to 0
	protected internal virtual void connectInput(
		int slot,PlayableController playableController,float weight)
	{
		if(playableController == null)
			return;
		/* I don't really want to do this, but Unity gives me no other choices because
		there is always ambiguity of whether playable.GetTime() is returning time THIS frame
		or LAST frame. */
		if(!playableController.bPendingTime){
			playableController.bPendingTime = true;
			owner.scheduleClearPendingTime(playableController);
			setChildPendingTimeRecursive();
		}
		bool bShouldConnect = 
			weight!=0.0f && 
			(playableController as TreePlayableController)?.activeSlotCount != 0
		;
		if(bShouldConnect){
			playableController.bHibernate = false;
			RightPlayable.ConnectInput(slot,playableController.playable,0);
			RightPlayable.SetInputWeight(slot,weight);
			if(activeSlotCount++==0 && parentController!=null)
				parentController.dehibernateInput(parentPort,1.0f);
		}
		lChildController[slot] = playableController;
		playableController.parentController = this;
		playableController.parentPort = slot;
		playableController.bHibernate = !bShouldConnect;
		playableController.updateIntersectTime();
		lSlotWeight[slot] = weight;
	}
	internal void setChildPendingTimeRecursive(){
		for(int i=0; i<lChildController.Count; ++i){
			if(lChildController[i]?.IsPlaying==true){
				lChildController[i].bPendingTime = true;
				owner.scheduleClearPendingTime(lChildController[i]);
				(lChildController[i] as TreePlayableController)?.setChildPendingTimeRecursive();
			}
		}
	}
	protected internal virtual void disconnectInput(int slot){
		if(lChildController[slot]!=null){
			if(!lChildController[slot].bHibernate){
				RightPlayable.DisconnectInput(slot);
				if(--activeSlotCount==0 && parentController!=null)
					parentController.hibernateInput(parentPort);
			}
			lChildController[slot].parentController = null;
			lChildController[slot].parentPort = -1;
			lChildController[slot].bHibernate = false;
			//owner.untrackAnimationAction(lChildController[slot]);
			lChildController[slot] = null;
			//lChildInfo[slot].leadtime = 0.0;
			//lChildInfo[slot].bTimePending = true;
		}
	}
	protected internal virtual bool dehibernateInput(int slot,float weight){ //accessible from both assembly and subclass
		if(weight!=0.0f && lChildController[slot]?.bHibernate==true){
			if((lChildController[slot] as TreePlayableController)?.activeSlotCount == 0)
				/* This accidentally works because null!=0, so the condition is false
				if lChildController[slot] is not a TreePlayableController. */
				return false;
			RightPlayable.ConnectInput(slot,lChildController[slot].playable,0);
			RightPlayable.SetInputWeight(slot,weight);
			lChildController[slot].bHibernate = false;
			lSlotWeight[slot] = weight;
			++activeSlotCount;
			if(bHibernate){ //let parent dehibernate do the sync
				parentController?.dehibernateInput(
					parentPort,parentController.lSlotWeight[parentPort]);
			}
			else
				lChildController[slot].syncParentTime();
			return true;
		}
		return false;
	}
	protected internal virtual void hibernateInput(int slot){
		if(lChildController[slot]!=null && !lChildController[slot].bHibernate){
			RightPlayable.DisconnectInput(slot);
			lChildController[slot].bHibernate = true;
			if(--activeSlotCount<=0)
				parentController?.hibernateInput(parentPort);
		}
	}
	public float getSlotWeight(int slot){
		return lSlotWeight[slot];
	}
	public void setSlotWeight(int slot,float weight){
		if(weight==0.0f)
			hibernateInput(slot);
		else if(lSlotWeight[slot]==0.0f)
			/* There can be cases where slot weight not zero, but hibernating due to
			nonlooping clip exceeding duration time or when activeSlotCount==0.
			Don't dehibernate in those cases. */
			dehibernateInput(slot,weight);
		else
			RightPlayable.SetInputWeight(slot,weight);
		lSlotWeight[slot] = weight;
	}
	/* This does not check whether tree corresponds to playableController or not,
	but try to sync weight in the tree structure as best as possible. */
	public void syncWeight(AnimationTree tree){
		if(tree?.lTree == null)
			return;
		int minCount = Mathf.Min(lChildController.Count,tree.lTree.Count);
		for(int i=0; i<minCount; ++i){
			setSlotWeight(i,tree.lTree[i].weight);
			(lChildController[i] as TreePlayableController)?.syncWeight(tree.lTree[i]);
		}
	}
	/* This also doesn't check correspondent, but will lerp if weight exists in BOTH
	treeStart and treeEnd. */
	public void lerpWeight(AnimationTree treeStart,AnimationTree treeEnd,float t){
		if(treeStart?.lTree==null || treeEnd?.lTree==null)
			return;
		//avoid allocating memory for params int[]
		int minCount = Mathf.Min(
			lChildController.Count,
			Mathf.Min(treeStart.lTree.Count,treeEnd.lTree.Count)
		);
		for(int i=0; i<minCount; ++i){
			float startWeight = treeStart.lTree[i].weight;
			float endWeight = treeEnd.lTree[i].weight;
			if(startWeight != endWeight)
				setSlotWeight(i,Mathf.Lerp(startWeight,endWeight,t));
			(lChildController[i] as TreePlayableController)?.lerpWeight(
				treeStart.lTree[i],treeEnd.lTree[i],t);
		}
	}
	public void copyCurrentWeightTo(AnimationTree tree){
		if(tree?.lTree == null)
			return;
		int minCount = Mathf.Min(lSlotWeight.Count,tree.lTree.Count);
		for(int i=0; i<minCount; ++i){
			tree.lTree[i].weight = lSlotWeight[i];
			(lChildController[i] as TreePlayableController)
				?.copyCurrentWeightTo(tree.lTree[i]);
		}
	}
}
	#endregion
//---------------------------------------------------------------------------------
	#region TRANSITION PLAYABLECONTROLLER
internal class TransitionWeightInfo{
	public float startTransitionWeight =1.0f;
	/* Below are used only for transitioning weight WITHIN AnimationTree */
	public AnimationTree startAnimationTree;
	public AnimationTree targetAnimationTree;
}
internal class TransitionPlayableController : TreePlayableController{
	public AnimationMixerPlayable transitionMixerPlayable;
	public List<TransitionWeightInfo> lWeightInfo = new List<TransitionWeightInfo>();
	public float duration;
	public DOnAnimationDone dOnDone;
	public bool bFadeOut;
	public int startPort =-1;
	public int targetPort =-1;
	public int lastPort =-1;

	/* Following advice of Eric Lippert, SO, mark internal functions of internal class
	as public.  */
	public TransitionPlayableController(
		AnimationPlayer animPlayer,Playable playable,AnimationMixerPlayable mixerPlayable,
		int layerIndex)
		: base(animPlayer,mixerPlayable,layerIndex)
	{
		this.playable = playable;
		transitionMixerPlayable = mixerPlayable;
		for(int i=0; i<mixerPlayable.GetInputCount(); ++i)
			lWeightInfo.Add(new TransitionWeightInfo());
		activeSlotCount = 1;
		/* This is a hack that I admittedly add later, because I discover that
		TransitionPlayableController should never be hibernated automatically when
		activeSlotCount==0 because it may happen with zero weight tree.
		If we allow it to hibernate, transitioning from zero weight tree cannot happen
		since the transition's ScriptPlayable cannot run in the first place. */
	}
	protected override Playable RightPlayable{ get{return transitionMixerPlayable;} }
	protected internal override void disconnectInput(int slot){
		base.disconnectInput(slot);
		lWeightInfo[slot].startAnimationTree = null;
		lWeightInfo[slot].targetAnimationTree = null;
	}
	public bool hasInTransition(PlayableController playableController){
		return playableController.parentController == this;
	}
	public void snapshotTransitionWeight(){
		for(int i=0; i<=lastPort; ++i)
			lWeightInfo[i].startTransitionWeight = getSlotWeight(i);
	}
	public int addEntry(){ //return last port
		int inputCount = RightPlayable.GetInputCount();
		if(inputCount == lChildController.Count)
			RightPlayable.SetInputCount(inputCount+1);
		lChildController.Add(null);
		lSlotWeight.Add(1.0f);
		lWeightInfo.Add(new TransitionWeightInfo());
		return lChildController.Count-1;
	}
	public void resetSelf(){
		playable.SetTime(0.0);
		playable.SetSpeed(1.0);
		bPendingTime = true;
		owner.scheduleClearPendingTime(this);
		//updateIntersectTime();
		for(int i=0; i<=lastPort; ++i)
			lChildController[i]?.updateIntersectTime();
	}
}
	#endregion
//---------------------------------------------------------------------------------
	#region LAYER PLAYABLECONTROLLER
internal class LayerInfo{
	public int id;
	public TransitionPlayableController transitionPlayableController;
	public Dictionary<AnimationClip,ClipPlayableController> dictPlayableController =
		new Dictionary<AnimationClip,ClipPlayableController>();
	public Dictionary<AnimationTree,TreePlayableController> dictTreePlayableController =
		new Dictionary<AnimationTree,TreePlayableController>(new AnimationTree1StructuralComparer());
	public AvatarMask avatarMask; //can't get this from playable directly
}
internal class LayerPlayableController : TreePlayableController{
	public List<LayerInfo> lLayerInfo = new List<LayerInfo>();

	public LayerPlayableController(
		AnimationPlayer animPlayer,Playable playable,int layerIndex)
		: base(animPlayer,playable,layerIndex)
	{}
	public int getLayerIndex(int layerID){
		return Algorithm.binaryKeySearch(
			lLayerInfo,
			layerID,
			(LayerInfo layerInfo,int id) => layerInfo.id-id
		);
	}
	public int addEntry(
		int layerID,AvatarMask mask,bool bAdditive,AnimationPlayer owner) //weight=1.0f
	{ //return layerIndex
		int layerIndex = getLayerIndex(layerID);
		if(layerIndex >= 0)
			return -1; //layer already exists
		layerIndex = ~layerIndex;
		lChildController.Insert(layerIndex,null);
		lSlotWeight.Insert(layerIndex,1.0f);
		
		//Create new LayerInfo()
		LayerInfo layerInfo = new LayerInfo();
		layerInfo.id = layerID;
		layerInfo.avatarMask = mask;
		PlayableGraph graph = playable.GetGraph();
		ScriptPlayable<TransitionPlayableBehaviour> transitionPlayable =
			ScriptPlayable<TransitionPlayableBehaviour>.Create(graph,1);
		AnimationMixerPlayable mixerPlayable = AnimationMixerPlayable.Create(graph,2); //Starts with 2 slots
		transitionPlayable.ConnectInput(0,mixerPlayable,0);
		transitionPlayable.SetPropagateSetTime(true);
		TransitionPlayableController transitionPlayableController =
			new TransitionPlayableController(owner,transitionPlayable,mixerPlayable,layerIndex);
		layerInfo.transitionPlayableController = transitionPlayableController;
		TransitionPlayableBehaviour behaviour = transitionPlayable.GetBehaviour();
		behaviour.owner = owner;
		behaviour.controller = layerInfo.transitionPlayableController;
		lLayerInfo.Insert(layerIndex,layerInfo);
		
		//Manage AnimationLayerMixerPlayable slots
		int count = lChildController.Count;
		AnimationLayerMixerPlayable layerPlayable = (AnimationLayerMixerPlayable)playable;
		if(layerPlayable.GetInputCount() < count)
			layerPlayable.SetInputCount(count);
		for(int i=count-1; i>layerIndex; --i){
			layerPlayable.SetLayerMaskFromAvatarMask(
				(uint)i,
				lLayerInfo[i].avatarMask ? lLayerInfo[i].avatarMask : new AvatarMask()
			);
			layerPlayable.SetLayerAdditive((uint)i,layerPlayable.IsLayerAdditive((uint)i-1));
			lLayerInfo[i].transitionPlayableController.layerIndex = i;
			foreach(PlayableController controller in lLayerInfo[i].dictPlayableController.Values)
				controller.layerIndex = i;
			foreach(TreePlayableController treeController in lLayerInfo[i].dictTreePlayableController.Values)
				treeController.layerIndex = i;
			if(lChildController[i] != null){ //shift port right
				layerPlayable.DisconnectInput(i-1);
				layerPlayable.ConnectInput(i,lChildController[i].playable,0);
				layerPlayable.SetInputWeight(i,lSlotWeight[i]);
				lChildController[i].parentPort = i;
			}
		}
		layerPlayable.SetLayerMaskFromAvatarMask(
			(uint)layerIndex,
			mask ? mask : new AvatarMask() //Cannot use ?? operator with Unity Object
		);
		layerPlayable.SetLayerAdditive((uint)layerIndex,bAdditive);
		return layerIndex;
	}
	public void removeEntry(int layerID){
		int layerIndex = getLayerIndex(layerID);
		if(layerIndex < 0)
			return;
		PlayableGraph graph = playable.GetGraph();
		foreach(PlayableController controller in lLayerInfo[layerIndex].dictPlayableController.Values)
			graph.DestroySubgraph(controller.playable);
		foreach(TreePlayableController treeController in lLayerInfo[layerIndex].dictTreePlayableController.Values)
			graph.DestroySubgraph(treeController.playable);
		graph.DestroySubgraph(lLayerInfo[layerIndex].transitionPlayableController.playable);
		lLayerInfo.RemoveAt(layerIndex);
		lChildController.RemoveAt(layerIndex);
		lSlotWeight.RemoveAt(layerIndex);
		int count = lChildController.Count;
		AnimationLayerMixerPlayable layerPlayable = (AnimationLayerMixerPlayable)playable;
		for(int i=layerIndex; i<count; ++i){
			layerPlayable.SetLayerMaskFromAvatarMask(
				(uint)i,
				lLayerInfo[i].avatarMask ? lLayerInfo[i].avatarMask : new AvatarMask()
			);
			layerPlayable.SetLayerAdditive((uint)i,layerPlayable.IsLayerAdditive((uint)i+1));
			lLayerInfo[i].transitionPlayableController.layerIndex = i;
			foreach(PlayableController controller in lLayerInfo[i].dictPlayableController.Values)
				controller.layerIndex = i;
			foreach(TreePlayableController treeController in lLayerInfo[i].dictTreePlayableController.Values)
				treeController.layerIndex = i;
			if(lChildController[i] != null){ //shift port left
				layerPlayable.DisconnectInput(i+1);
				layerPlayable.ConnectInput(i,lChildController[i].playable,0);
				layerPlayable.SetInputWeight(i,lSlotWeight[i]);
				lChildController[i].parentPort = i;
			}
		}
	}
	protected internal override void disconnectInput(int slot){
		base.disconnectInput(slot);
		if(activeSlotCount == 0)
			playable.GetGraph().Stop();
	}
	protected internal override void connectInput(
		int slot,PlayableController playableController,float weight)
	{
		base.connectInput(slot,playableController,weight);
		if(activeSlotCount == 1) //first active slot
			playable.GetGraph().Play();
	}
	/* does NOT clear graph, and graph.Destroy() is expected separately */
	public void clearData(){
		lLayerInfo.Clear();
		lChildController.Clear();
		activeSlotCount = 0;
	}
	public TransitionPlayableController getTransitioningController(int layerIndex){
		return lChildController[layerIndex] as TransitionPlayableController;
	}
}
	#endregion
//---------------------------------------------------------------------------------
#endregion
//======================================================================================

//======================================================================================
	#region PLAYABLEBEHAVIOUR
//---------------------------------------------------------------------------------
	#region TRANSITION PLAYABLEBEHAVIOUR
internal class TransitionPlayableBehaviour : PlayableBehaviour{
	public AnimationPlayer owner;
	public TransitionPlayableController controller;
	/* I wanted to have constructor here, but cannot because ScriptPlayable<T>
	requires non-parameter constructor (to be able to do Create). Furthermore,
	it requires class type, so cannot use struct. */
	/* I also considered using IAnimationJob, but it does not seems to fit
	the functionality very well. */
	/* I don't use duration here because I need to pass time to behaviour explicitly
	anyway. It would be great if there is a callback for when Playable is connected
	to the graph so duration can be retrieved there, but there isn't (OnBehaviourPlay
	is called only on FIRST play, so if you want to use that you need to Pause() when
	transition ends, and Play() in the main code, which is more costly and feels like
	extra layer of maintenance.) */
	public override void PrepareFrame(Playable playable,FrameData frameData){
		/* On the frame when time is up, we do not disconnect Playable yet, but
		instead signal owner AnimationPlayer to disconnect in LateUpdate().
		This allows Playable to perform necessary updates before being disconnected.
		without forcing call Evaluate(), in case dOnDone relies on updated data.
		(Note: Graph related functions are called AFTER Update() and before LateUpdate()
		(Credit: seant_unity, UF). */
		/* Admittedly, these 2 lines are added later after I understand the nature of
		GetTime() in PrepareFrame. When I tested before, I thought it is the time of
		previous frame, when it fact it is time of this frame FOR THIS Playable and its parent,
		but not so for its children, and may be either way for siblings.
		We need to mark its parent's bPendingTime as true to inform code that GetTime()
		is indeed the time this frame.
		Still, to keep things consistent, I decide to just set bPendingTime like this. */
		controller.bPendingTime = true;
		owner.layerPlayableController.bPendingTime = true;

		double currentTime = controller.playable.GetTime();
		float fadeIn = Mathf.Clamp01((float)currentTime/controller.duration);
		float fadeOut = 1.0f-fadeIn;
		int targetPort = controller.targetPort;
		for(int i=0; i<=controller.lastPort; ++i){
			if(i != targetPort)
				controller.setSlotWeight(
					i,controller.lWeightInfo[i].startTransitionWeight*fadeOut);
		}
		TransitionWeightInfo targetWeightInfo = controller.lWeightInfo[targetPort];
		float targetWeight = targetWeightInfo.startTransitionWeight*fadeOut;
		if(!controller.bFadeOut)
			targetWeight += fadeIn;
		controller.setSlotWeight(targetPort,targetWeight);
		if(targetWeightInfo.targetAnimationTree != null){
			((TreePlayableController)controller[targetPort]).lerpWeight(
				targetWeightInfo.startAnimationTree,
				targetWeightInfo.targetAnimationTree,
				fadeIn
			);
		}

		/* On the frame when time is up, we do not disconnect Playable yet, but
		instead signal owner AnimationPlayer to disconnect in LateUpdate().
		This allows Playable to perform necessary updates before being disconnected.
		without forcing call Evaluate(), in case dOnDone relies on updated data.
		(Note: Graph related functions are called AFTER Update() and before LateUpdate()
		(Credit: seant_unity, UF). */
		/* For whatever reason, I DO NOT get ProcessFrame call AT ALL, but even
		if I had, it is useless because it will not be called on the last frame of animation
		(Credit: seant_unity, UF). 
		For this reason, I have to use this ugly workaround, namely trying to signal
		AnimationPlayer to register dOnDone AND disconnection in LateUpdate().
		Note: I tried OnAnimatorIK(), but that gets called ONLY if the rig is HUMANOID (Credit: ADAMN721, UA)
		Storing owner AnimationPlayer in a field is the cleanest I can think of.
		While this seems to expose too much functionality to this class, it is OK
		because this class is private anyway. Other way is to use delegate, but I think
		it is unnecessarily going too much around the bush. */
		/* I used to check IsDone, but doing so becomes annoying because it does NOT
		sync with time, in that if you use SetTime this will not update, plus inquiring
		GetTime is only slightly more expensive than this, but it makes life much easier. */
		if(fadeIn >= 1.0f){
			/* It is specified that multicast delegate invokes method in the order
			that appears in invocation list (Credit: Philip Wallace, SO),
			and that Delegate.Combine is guaranteed to preserve subscription order
			(Credit: Jon Skeet, SO). So here we are relying on that fact to make sure
			that disconnection occurs before dOnDone. */
			/* For some reasons, this causes target's time to continue running correctly
			even when there is reconnection and you don't specify false for connectInput.
			Will investigate later. */
			owner.dOnLateUpdate += ()=>{
				owner.layerPlayableController.bPendingTime = true;
				controller.bPendingTime = true;
				owner.endTransitionIndex(
					controller.layerIndex,
					controller.bFadeOut ? 0 : 1
				);
				controller.bPendingTime = false;
				owner.layerPlayableController.bPendingTime = false;
			};
			owner.dOnLateUpdate += createOnDoneAction(currentTime-controller.duration);
			/* For whatever reason, JIT create garbage for delegate (if scope variables
			are captured) even when the if condition IS FALSE (Credit: Xarbrough, SO).
			Below is the workaround so it only generate garbage when inside the if statement.
			(Credit: Servy, SO) */
		}

		controller.bPendingTime = false;
		owner.layerPlayableController.bPendingTime = false;
	}
	public Action createOnDoneAction(double exceedingTime){
		return ()=>{controller.dOnDone?.Invoke(exceedingTime);};
	}
}
	#endregion
//---------------------------------------------------------------------------------
#endregion
//======================================================================================

//======================================================================================
	#region ANIMATIONPLAYER
//---------------------------------------------------------------------------------
	#region DELEGATE & ENUM DEFINITIONS
public delegate void DOnAnimationDone(double exceedingTime);
public enum eTransitionInterruptMode{
	cannotInterrupt, //don't do anything if layer is currently in transition 
	endCurrentTransition, //end current transition now and use prev target as new start, but dOnDone will NOT trigger
	rerouteCurrentTransition, //use current moment from current transition as new start
}
public enum eTransitionResetMode{
	/* While it is possible to transition from self to self at t=0, I decide not
	to allow it due to performance concerns, and probably from good game design perspective.
	Personally, I think it is best to use "resetIfNotPlaying" because if the clip is playing
	as part of transition, transitioning back to it works most smoothly by just shifting
	weight back to it without resetting it, as if we decide to cancel transition midway
	rather than playing it anew. */
	notReset,
	resetIfNotPlaying,
	resetAlways, //will result in small animation snap if self is playing (depending on current weight)
}
	#endregion
//---------------------------------------------------------------------------------
[DefaultExecutionOrder(-2)]
[RequireComponent(typeof(Animator))]
#if UNITY_EDITOR
[ExecuteOnBuild(nameof(reorderAnimatorComponent),bApplyInEditorBuild=true)]
#endif
public class AnimationPlayer : MonoBehaviour,IAnimationClipSource{
	Animator animator;
	private PlayableGraph graph;
	private PlayableOutput output;
	internal LayerPlayableController layerPlayableController;
	private WaitForEndOfFrame waitForEndFrame = new WaitForEndOfFrame();
	internal Action dOnLateUpdate;
	private bool bHasPendingTime;

	public int maxInterrupt =3;
	public bool bDisposeGraphAndDataOnDisable;
	[SerializeField] DirectorUpdateMode timeUpdateMode =DirectorUpdateMode.GameTime;
	public DirectorUpdateMode TimeUpdateMode{
		get{ return graph.GetTimeUpdateMode(); }
		set{ graph.SetTimeUpdateMode(value); }
	}

	//---------------------------------------------------------------------------------
		#region PENDING TIME MANAGEMENT
	internal List<PlayableController> lPendingTime = new List<PlayableController>();
	internal float GraphDeltaTime{
		get{
			switch(TimeUpdateMode){
				case DirectorUpdateMode.GameTime: return Time.deltaTime;
				case DirectorUpdateMode.UnscaledGameTime: return Time.unscaledDeltaTime;
				case DirectorUpdateMode.DSPClock: return (float)AudioSettings.dspTime;
				/* Note: AudioSettings.dspTime has a reputation of staying the same over
				many frames. I am not sure how it should actually works, so I use this
				as a placeholder for now. If something more is needed, I will probably
				have to redesign the graph. */
				//Credit: https://forum.unity.com/threads/do-people-not-realize-how-bad-audiosource-dsptime-is-can-someone-explain-how-it-works.402308/
				default: return 0.0f;
			}
		}
	}
	internal void scheduleClearPendingTime(PlayableController playableController){
		bHasPendingTime = true;
		lPendingTime.Add(playableController);
	}
		#endregion
	//---------------------------------------------------------------------------------
		#region EDITOR RELATED
	/* To be used with GraphVisualizerClient.Show(graph) in user's script.
	This EXPOSES graph to user, which is terrible, but I don't see other ways
	because GraphVisualizerClient is a script that is not a package; user just put
	the script in their folder and use class in there. So the class is not in the same
	assembly, and not even has assembly of its own. Also, C# does not permit returning
	const reference.
	P.S. Because of Unity's design, although PlayableGraph is a struct, it is still 
	linked to the original (because it stores handle). */
	public PlayableGraph getGraph(){
		return graph;
	}
	/* Implementing IAnimationClipSource and overriding GetAnimationClips will force
	Animation Window to show AnimationClips you add to lClip, even when it is not
	in Animation or Animator Component (Credit: Kybernetik & Baste, UF).
	However, there is no callback of when the Asset is refreshed, so I just leave it
	like this for now. User is required to right-click->Unlock Animation Window
	for it to update. */
	public void GetAnimationClips(List<AnimationClip> lClip){
		#if UNITY_EDITOR
		lClip.AddRange(lUnlockedClip);
		#endif
	}
	#if UNITY_EDITOR
	private static List<AnimationClip> lUnlockedClip = new List<AnimationClip>();
	[MenuItem("CONTEXT/"+nameof(AnimationPlayer)+"/Unlock Animation Window1")]
	static void AnimationPlayerUnlockAnimationWindow(){
		lUnlockedClip = EditorPath.findAssetsOfType<AnimationClip>();
	}
	
	/* When GameObject is disabled, it should be Rebind() like other time so
	when it is re-enabled the default values stay. HOWEVER, Animator Component
	may have been disabled before this, so we move this Component ABOVE Animator
	to make sure it is called before.
	! Note: Unity does NOT give guaranteed that Components will be executed
	in the order that appears in the inspector, BUT many people have found and
	rely on this fact (MadWatch, Kybernetik, UF & Jordii, Rudy Pangestu, UA).
	While this solution looks ugly, others suggested are no better, for example,
	relying on user to call stop() or Animator.Rebind() or Animator.WriteDefaultValues()
	BEFORE disabling the GameObject, and wait for 0.5s before disabling (Credit: yyylny, UF) */
	/* Note: Before this, I have relied on OnValidate() to reorder the Animator Component.
	However, that does not seem to work with model prefab because Unity seems to place Animator
	BEFORE this script every time the scene loads even when you have reordered it.
	Consequentially, it will not work in build. I Could not find any workaround of this,
	so I put up a warning that user should unpack it or nest it under other gameObjects. */
	private int getDeltaToAnimatorComponent(){
		Component[] aComponent = GetComponents(typeof(Component)); //Credit: Eran-Yaacobi, UA
		int indexThis = -1;
		int indexAnimator = -1;
		for(int i=0; i<aComponent.Length; ++i){
			if(aComponent[i]==this)
				indexThis = i;
			else if(indexAnimator<0 && aComponent[i].GetType()==typeof(Animator))
				indexAnimator = i;
			if(indexThis>0 && indexAnimator>0)
				break;
		}
		return indexThis-indexAnimator;
	}
	private void reorderAnimatorComponent(){
		int delta = getDeltaToAnimatorComponent();
		if(delta>0){
			if(PrefabUtility.GetPrefabAssetType(gameObject)==PrefabAssetType.Model){
				if(EditorApplication.isPlayingOrWillChangePlaymode ||
					BuildPipeline.isBuildingPlayer)
				{
					Debug.LogWarning("At "+gameObject.name+": Direct use of model prefab may cause Rebind problem when disabling GameObject.\n" +
						"Consider unpacking it or nest it under normal prefab, attach AnimationPlayer to its parent, and assign correct Avatar under the Animator Component.\n" +
						"Otherwise, you must make sure to call animator.Rebind() every time before disabling the GameObject.")
					;
				}
				return;
			}
			UnityEditor.EditorApplication.delayCall += ()=>{
				while(delta-- > 0)
					//Credit: pigzlz, UA
					UnityEditorInternal.ComponentUtility.MoveComponentUp(this);
			};
		};
	}
	void OnValidate(){
		AnimationPlayerUnlockAnimationWindow();
		if(graph.IsValid())
			graph.SetTimeUpdateMode(timeUpdateMode);
		reorderAnimatorComponent();
	}
	#endif
		#endregion
	//---------------------------------------------------------------------------------
		#region MONOBEHAVIOUR & RELATED FUNCTIONS
	void Awake(){
		animator = GetComponent<Animator>();
	}
	void OnEnable(){
		if(!graph.IsValid())
			initializeGraph();
	}
	private void initializeGraph(){
		graph = PlayableGraph.Create();
		graph.SetTimeUpdateMode(timeUpdateMode);
		output = AnimationPlayableOutput.Create(graph,"Animation Output",animator);
		layerPlayableController =
			new LayerPlayableController(this,AnimationLayerMixerPlayable.Create(graph,0),0); //dummy layer 0
		/* Use permanent layerMixer connecting to the output. Reason is that switching output's
		source Playable requires Animator.Rebind(), and so is very costly. Hence, there needs
		to be a permanent node connecting to the output. The choice is now between using
		custom ScriptPlayable or layerMixer directly. Both incur about the same overhead
		to the graph with one direct connection, but the latter has more cost with multiple layers
		where layerMixer needs to also be connected to it, plus inconvenience of needing to switch
		connection type when changing from 1 to 2 layers.
		From measurement, it is observed that having empty slots does not harm much performance.
		In fact, having extra 100 empty slots will cause less than adding one extra nodes,
		so this is the best way to go. */
		layerPlayableController.bPendingTime = true;
		scheduleClearPendingTime(layerPlayableController);
		output.SetSourcePlayable(layerPlayableController.playable);
		addLayer(0);
	}
	void OnDestroy(){
		if(graph.IsValid())
			graph.Destroy();
		Debug.Log("Tracked Playable Count: "+llActionTrackedController.Count);
		//everything else should be cleared automatically afterward
	}
	void OnDisable(){
		/* Theory behind Rebind() (which I learnt the hard way) is that calling it
		will cause EVERY binding to reset to the default value. This value is important
		because it is used as base from which delta is scaled according to weight.
		For example, what should scale be if Playable weight is 0.5 and scale on the graph
		is 2? We don't know unless, say, we know the default scale is 1, then we can
		multiply the delta by weight to get scale=1.5.
		This is particularly important in mixing when Playables being mixed have different
		bindings, because Unity will use default as stand-in for missing bindings.
		Now, it is observed (because it seems documentation is wrong) (Credit: Kybernetik, UF)
		that when output.SetSourcePlayable() is called, whatever values are at that moment
		will be regarded as default value. Hence, we need to Rebind() to write default values
		before SetSourcePlayable to something else so the original default is carried over.
		Note, however, that while calling animator.Rebind() will rewrite values, the
		APPEARANCE of your GameObject will not show UNTIL you call graph.Evaluate().
		That is how it is implemented in stop(). */
		animator.Rebind();
		if(bDisposeGraphAndDataOnDisable)
			disposeGraphAndData();
	}
	void LateUpdate(){
		if(dOnLateUpdate != null){
			dOnLateUpdate.Invoke();
			dOnLateUpdate = null;
		}
		if(bHasPendingTime){
			for(int i=0; i<lPendingTime.Count; ++i)
				lPendingTime[i].bPendingTime = false;
			lPendingTime.Clear();
			bHasPendingTime = false;
		}
		if(!IsPaused){
			//Removing node while traversing, Credit: dtb, SO
			LinkedListNode<PlayableController> node = llActionTrackedController.First;
			while(node!=null){
				//Debug.Log(node.Value.playable.GetTime());
				LinkedListNode<PlayableController> nodeNext = node.Next;
				/* It is possible that the action itself STOPS the animation, and so
				the LinkedList is cleared away, so we check that too. */
				if(!node.Value.invokeDueAction() && node.List==llActionTrackedController){
					llActionTrackedController.Remove(node);}
				node = nodeNext;
			}
		}
	}
	private void disposeGraphAndData(){
		graph.Destroy();
		layerPlayableController.clearData();
		lPendingTime.Clear();
		llActionTrackedController.Clear();
	}
		#endregion
	//---------------------------------------------------------------------------------
		#region LAYER
	public bool isLayerExist(int layerID){
		return layerPlayableController.getLayerIndex(layerID)>=0;
	}
	public bool addLayer(int layerID,AvatarMask mask=null,bool bAdditive=false){
		return layerPlayableController.addEntry(layerID,mask,bAdditive,this)>=0;
	}
	public void removeLayer(int layerID){
		layerPlayableController.removeEntry(layerID);
	}
	public AvatarMask getAvatarMask(int layerID){
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		return layerIndex>=0 ? layerPlayableController.lLayerInfo[layerIndex].avatarMask : null;
		//because null AvatarMask will throw (Credit: zenn.dev)
	}
	public void setAvatarMask(int layerID,AvatarMask mask){
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		if(layerIndex >= 0){
			AnimationLayerMixerPlayable layerPlayable =
				(AnimationLayerMixerPlayable)layerPlayableController.playable;
			layerPlayable.SetLayerMaskFromAvatarMask(
				(uint)layerIndex,
				mask ? mask : new AvatarMask()
			);
			layerPlayableController.lLayerInfo[layerIndex].avatarMask = mask;
		}
	}
	public bool isLayerAdditive(int layerID){
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		return layerIndex>=0 ?
			((AnimationLayerMixerPlayable)layerPlayableController.playable)
				.IsLayerAdditive((uint)layerIndex) :
			false
		;
	}
	public void setLayerAdditive(int layerID,bool bLayerAdditive){
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		if(layerIndex >= 0){
			AnimationLayerMixerPlayable layerPlayable =
				(AnimationLayerMixerPlayable)layerPlayableController.playable;
			layerPlayable.SetLayerAdditive((uint)layerIndex,bLayerAdditive);
		}
	}
	public float getLayerWeight(int layerID){
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		return layerIndex>=0 ? layerPlayableController.getSlotWeight(layerIndex) : 0.0f;
	}
	public void setLayerWeight(int layerID,float weight){
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		if(layerIndex >= 0)
			layerPlayableController.setSlotWeight(layerIndex,weight);
	}
	private void dismantleLayer(int layerIndex,bool bReset){
		if(layerPlayableController.lChildController[layerIndex] == null)
			return;
		breakTransition(layerIndex); //will do nothing if not transitioning
		untrackAnimationAction(layerPlayableController.lChildController[layerIndex]);
		layerPlayableController.disconnectInput(layerIndex);
		if(bReset)
			graph.Evaluate();
	}
		#endregion
	//---------------------------------------------------------------------------------
		#region PLAY FUNCTIONALITY
	public ClipPlayableController play(AnimationClip clip,int layerID=0,bool bReset=true){
		if(!clip){
			stopLayer(layerID,bReset);
			return null;
		}
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		if(layerIndex < 0) //no such layer
			return null;
		return playPreparedPlayableController(
			preparePlayableController(clip,layerIndex,bReset));
	}
	public TreePlayableController play(AnimationTree tree,int layerID=0,bool bReset=true){
		if(tree==null){
			stopLayer(layerID,bReset);
			return null;
		}
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		if(layerIndex < 0)
			return null;
		return playPreparedPlayableController(
			preparePlayableController(tree,layerIndex,bReset,true));
	}
	public T play<T>(T playableController,bool bReset=true)
		where T:PlayableController
	{
		if(playableController == null)
			/* This should never be possible unless user intentionally pass null,
			because PlayableInfo obtained from this class is never null.
			Hence, we just reject the case. */
			return null; 
		if(bReset)
			playableController.reset();
		return playPreparedPlayableController(playableController);
	}
	public bool IsPaused{ get{return !graph.IsPlaying();} }
	public void pause(){
		graph.Stop();
	}
	public void resume(){
		if(layerPlayableController.activeSlotCount > 0) //else will just accidentally waste performance
			graph.Play();
	}
	/* For stop functions, bReset=true will cause object to reset to its initial pose
	using graph.Evaluate(), which can be expensive. HOWEVER, the time DOES NOT reset to 0.
	Hence, you should probably also call play with bReset=true so it starts animation from 0. */
	public void stopLayer(int layerID=0,bool bReset=true){
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		if(layerIndex >= 0)
			dismantleLayer(layerIndex,bReset);
	}
	private T playPreparedPlayableController<T>(T playableController)
		where T:PlayableController //weight=1.0f
	{
		int layerIndex = playableController.layerIndex;
		if(layerPlayableController.isSlotPlaying(layerIndex))
			dismantleLayer(layerIndex,false); //will be reset when process graph anyway
		layerPlayableController.connectInput(layerIndex,playableController,1.0f);
		trackAnimationAction(playableController);
		graph.Play();
		return playableController;
	}
	private ClipPlayableController preparePlayableController(
		AnimationClip clip,int layerIndex,bool bReset)
	{
		LayerInfo layerInfo = layerPlayableController.lLayerInfo[layerIndex];
		ClipPlayableController playableController;
		if(!layerInfo.dictPlayableController.TryGetValue(clip,out playableController)){
			playableController = buildClipPlayableController(clip,layerIndex);
			layerInfo.dictPlayableController.Add(clip,playableController);
		}
		else if(bReset)
			playableController.reset();
		return playableController;
	}
	private ClipPlayableController buildClipPlayableController(AnimationClip clip,int layerIndex){
		if(!clip)
			return null;
		return new ClipPlayableController(
			this,
			AnimationClipPlayable.Create(graph,clip),
			layerIndex
		);
	}
	private TreePlayableController preparePlayableController(
		AnimationTree tree,int layerIndex,bool bReset,bool bSyncWeight)
	{
		LayerInfo layerInfo = layerPlayableController.lLayerInfo[layerIndex];
		TreePlayableController treePlayableController;
		if(!layerInfo.dictTreePlayableController.TryGetValue(tree,out treePlayableController)){
			treePlayableController = buildTreePlayableController(tree,layerIndex);
			treePlayableController.weightTree = tree.clone();
			layerInfo.dictTreePlayableController.Add(tree,treePlayableController);
		}
		else{
			if(bReset)
				treePlayableController.reset();
			if(bSyncWeight)
				treePlayableController.syncWeight(tree);
		}
		return treePlayableController;
	}
	private TreePlayableController buildTreePlayableController(AnimationTree tree,int layerIndex){
		int count = tree?.lTree?.Count ?? 0;
		if(count == 0)
			return null;
		AnimationMixerPlayable mixerPlayable = AnimationMixerPlayable.Create(graph,count);
		TreePlayableController treePlayableController =
			new TreePlayableController(this,mixerPlayable,layerIndex);
		for(int i=0; i<count; ++i){
			if(tree.lTree[i].clip){
				treePlayableController.connectInput(
					i,
					buildClipPlayableController(tree.lTree[i].clip,layerIndex),
					tree.lTree[i].weight
				);
			}
			else{ //child is tree
				treePlayableController.connectInput(
					i,
					buildTreePlayableController(tree.lTree[i],layerIndex),
					tree.lTree[i].weight
				);
			}
		}
		return treePlayableController;
	}
	public void tick(float time=0.0f){
		graph.Evaluate(time);
	}
		#endregion
	//---------------------------------------------------------------------------------
		#region TRANSITION
	public bool isLayerTransitioning(int layerID){
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		return layerIndex>=0 ?
			layerPlayableController.getTransitioningController(layerIndex)!=null :
			false
		;
	}
	public PlayableController endTransition(int layerID,int breakDirection=1){
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		return layerIndex>=0 ? endTransitionIndex(layerIndex,breakDirection) : null;
	}
	internal PlayableController endTransitionIndex(int layerIndex,int breakDirection){
		TransitionPlayableController transitionPlayableController =
			layerPlayableController.getTransitioningController(layerIndex);
		if(transitionPlayableController == null)
			return null;
		PlayableController targetController = null;
		if(breakDirection > 0)
			targetController = transitionPlayableController[transitionPlayableController.targetPort];
		else if(breakDirection < 0)
			targetController = transitionPlayableController[transitionPlayableController.startPort];
		if(targetController?.bHibernate == true)
			/* Need sync time because we are going to switch connection
			and want correct intersectTime */
			targetController.syncParentTime();
		breakTransition(layerIndex);
		layerPlayableController.disconnectInput(layerIndex);
		if(targetController == null)
			return null;
		layerPlayableController.connectInput(
			layerIndex,targetController,layerPlayableController.lSlotWeight[layerIndex]);
		trackAnimationAction(targetController);
		return targetController;
	}
	internal bool breakTransition(int layerIndex){ //but NOT break connection to transitionPlayable
		TransitionPlayableController transitionPlayableController =
			layerPlayableController.getTransitioningController(layerIndex);
		if(transitionPlayableController == null)
			return false; //not transitioning
		for(int i=0; i<=transitionPlayableController.lastPort; ++i){
			untrackAnimationAction(transitionPlayableController[i]);
			transitionPlayableController.disconnectInput(i);
		}
		//transitionPlayableController.startPort = -1;
		//transitionPlayableController.targetPort = -1;
		transitionPlayableController.lastPort = -1;
		return true;
	}
	public void fadeOutLayer(float time,int layerID=0,DOnAnimationDone dOnDone=null){
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		if(layerIndex < 0)
			return;
		#if UNITY_EDITOR
		if(((AnimationLayerMixerPlayable)layerPlayableController.playable).IsLayerAdditive((uint)layerIndex)){
			Debug.LogWarning("With current implementation, additive layers do not work well with fade out!\nConsider alternatives!");}
		#endif
		PlayableController playingController =
			layerPlayableController.lChildController[layerIndex];
		if(playingController == null) //nothing is playing
			return;
		TransitionPlayableController transitionController =
			playingController as TransitionPlayableController;
		if(transitionController == null) //not transitioning
			transitionController = prepareFreshTransition(layerIndex);
		transitionController.resetSelf();
		transitionController.duration = time;
		transitionController.dOnDone = dOnDone;
		transitionController.bFadeOut = true;
		transitionController.snapshotTransitionWeight();
	}
	public ClipPlayableController transitionTo(
		AnimationClip clip,float time,int layerID=0,
		eTransitionInterruptMode interruptMode=eTransitionInterruptMode.rerouteCurrentTransition,
		eTransitionResetMode resetMode=eTransitionResetMode.resetIfNotPlaying,
		DOnAnimationDone dOnDone=null)
	{
		if(!clip){
			fadeOutLayer(time,layerID,dOnDone);
			return null;
		}
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		if(layerIndex < 0)
			return null;
		return transitionToPlayableController(
			preparePlayableController(clip,layerIndex,false),
			time,interruptMode,resetMode,dOnDone,null
		);
	}
	public TreePlayableController transitionTo(
		AnimationTree tree,float time,int layerID=0,
		eTransitionInterruptMode interruptMode=eTransitionInterruptMode.rerouteCurrentTransition,
		eTransitionResetMode resetMode=eTransitionResetMode.resetIfNotPlaying,
		DOnAnimationDone dOnDone=null)
	{
		if(tree==null){
			fadeOutLayer(time,layerID,dOnDone);
			return null;
		}
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		if(layerIndex < 0)
			return null;
		return transitionToPlayableController(
			preparePlayableController(tree,layerIndex,false,false),
			time,interruptMode,resetMode,dOnDone,tree
		);
	}
	public T transitionTo<T>(T targetController,float time,
		eTransitionInterruptMode interruptMode=eTransitionInterruptMode.rerouteCurrentTransition,
		eTransitionResetMode resetMode=eTransitionResetMode.resetIfNotPlaying,
		DOnAnimationDone dOnDone=null)
		where T:PlayableController
	{
		if(targetController == null)
			return null; //if you want to fade out, use fadeOutLayer explicitly
		return transitionToPlayableController(
			targetController,time,interruptMode,resetMode,dOnDone,null);
	}
	private T transitionToPlayableController<T>(T targetController,float time,
		eTransitionInterruptMode interruptMode,eTransitionResetMode resetMode,
		DOnAnimationDone dOnDone,AnimationTree targetTree)
		where T:PlayableController
	{
		//if(targetController==null) //shouldn't happen
		//	return null;
		int layerIndex = targetController.layerIndex;
		#if UNITY_EDITOR
		if(((AnimationLayerMixerPlayable)layerPlayableController.playable).IsLayerAdditive((uint)layerIndex)){
			Debug.LogWarning("Additive layers do not work well with transition!\nConsider alternatives!");}
		#endif
		TransitionPlayableController transitionController =
			layerPlayableController.getTransitioningController(layerIndex);
		if(transitionController == null){ //not transitioning
			/* It is decided that transitioning to self is not allowed (waste performance)
			EXCEPT AnimationTree weight transition. */
			if(targetController.IsPlaying){ //target is the only one playing now
				if(resetMode == eTransitionResetMode.resetAlways)
					targetController.reset();
				if(targetTree == null){
					if(resetMode == eTransitionResetMode.resetAlways)
						targetController.reset();
					return targetController; //just return it
				}
				//else, do weight transition
				transitionController = prepareWeightTransition(targetController as TreePlayableController,targetTree);
				//direct cast doesn't work because type T may be sibling
			}
			else{ //target is not playing
				transitionController = prepareFreshTransition(layerIndex);
				int targetPort = transitionController.startPort+1;
				if(resetMode != eTransitionResetMode.notReset)
					targetController.reset();
				transitionController.connectInput(targetPort,targetController,0.0f);
				(targetController as TreePlayableController)?.syncWeight(targetTree);
				transitionController.targetPort = targetPort;
				transitionController.lastPort = targetPort;
			}
		}
		else{ //transitioning
			switch(interruptMode){
				case eTransitionInterruptMode.cannotInterrupt:
					return null;
				case eTransitionInterruptMode.endCurrentTransition:
					int prevTargetPort = transitionController.targetPort;
					PlayableController prevTargetController =
						transitionController[prevTargetPort];
					prevTargetController?.syncParentTime();
					(prevTargetController as TreePlayableController)?.syncWeight(
						transitionController.lWeightInfo[prevTargetPort].targetAnimationTree);
					breakTransition(layerIndex);
					if(prevTargetController == targetController){ //target transition to self
						if(targetTree == null){
							layerPlayableController.disconnectInput(layerIndex);
							layerPlayableController.connectInput(layerIndex,targetController,1.0f);
							if(resetMode==eTransitionResetMode.resetAlways)
								targetController.reset();
							return targetController;
						}
						else{ //do weight transition
							transitionController.connectInput(0,targetController,1.0f);
							if(resetMode == eTransitionResetMode.resetAlways)
								targetController.reset();
							transitionController.startPort = 0;
							transitionController.targetPort = 0;
							transitionController.lastPort = 0;
							prepareWeightTransition(
								targetController as TreePlayableController,targetTree);
						}
					}
					else{ //transition to other
						transitionController.connectInput(0,prevTargetController,1.0f);
						transitionController.connectInput(1,targetController,0.0f);
						if(resetMode != eTransitionResetMode.notReset)
							targetController.reset();
						(targetController as TreePlayableController)?.syncWeight(targetTree);
						transitionController.startPort = 0;
						transitionController.targetPort = 1;
						transitionController.lastPort = 1;
					}
					break;
				case eTransitionInterruptMode.rerouteCurrentTransition:
					/* Transitioning back to some previously played state will NOT 
					be counted toward maxInterrupt. */
					if(transitionController.hasInTransition(targetController)){
						transitionController.targetPort = targetController.parentPort;
						if(resetMode == eTransitionResetMode.resetAlways)
							targetController.reset();
						if(targetTree != null)
							prepareWeightTransition(
								targetController as TreePlayableController,targetTree);
					}
					else{ //target is not in transition list
						int targetPort = prepareTransitionSlot(layerIndex);
						transitionController.connectInput(targetPort,targetController,0.0f);
						transitionController.targetPort = targetPort;
						transitionController.lastPort = Mathf.Max(targetPort,transitionController.lastPort);
						if(resetMode != eTransitionResetMode.notReset)
							targetController.reset();
					}
					break;
			}
		}
		transitionController.resetSelf();
		transitionController.duration = time;
		transitionController.dOnDone = dOnDone;
		transitionController.bFadeOut = false;
		transitionController.snapshotTransitionWeight();
		trackAnimationAction(targetController);
		return targetController;
	}
	private TransitionPlayableController prepareFreshTransition(int layerIndex){
		PlayableController playingController =
			layerPlayableController.lChildController[layerIndex];
		TransitionPlayableController transitionController =
			layerPlayableController.lLayerInfo[layerIndex].transitionPlayableController;
		if(playingController == null){ //nothing is playing
			layerPlayableController.connectInput(layerIndex,transitionController,1.0f); //weight=1.0f
			transitionController.startPort = -1; //start from nothing
		}
		else{
			playingController.syncParentTime();
			layerPlayableController.disconnectInput(layerIndex);
			layerPlayableController.connectInput(
				layerIndex,transitionController,layerPlayableController.lSlotWeight[layerIndex]);
			transitionController.connectInput(0,playingController,1.0f); //weight=1.0f
			transitionController.startPort = 0;
		}
		transitionController.targetPort = transitionController.startPort;
		transitionController.lastPort = transitionController.startPort;
		return transitionController;
	}
	private int prepareTransitionSlot(int layerIndex){
		TransitionPlayableController transitionController =
			layerPlayableController.getTransitioningController(layerIndex);
		//if it is null, let throw (should not happen)
		int targetPort = transitionController.targetPort;
		if(transitionController.lastPort >= 1+maxInterrupt){
			/* discard last target. It is possible to discard in FIFO style,
			but you need to keep track of all the order, which I don't think worth it. */
			transitionController.disconnectInput(targetPort);
			return targetPort;
		}
		else{ //have not reach maxInterrupt
			if(transitionController.lastPort == transitionController.lChildController.Count-1)
				transitionController.addEntry();
			return transitionController.lastPort+1;
		}
	}
	private TransitionPlayableController prepareWeightTransition(
		TreePlayableController targetController,AnimationTree targetTree)
	{
		//if targetController==null, let throw
		int layerIndex = targetController.layerIndex;
		TransitionPlayableController transitionController =
			layerPlayableController.getTransitioningController(layerIndex);
		int targetPort = targetController.parentPort;
		if(transitionController == null){
			transitionController = prepareFreshTransition(layerIndex);
			targetPort = 0;
		}
		targetController.copyCurrentWeightTo(targetController.weightTree);
		transitionController.lWeightInfo[targetPort].startAnimationTree = targetController.weightTree;
		transitionController.lWeightInfo[targetPort].targetAnimationTree = targetTree;
		return transitionController;
	}
		#endregion
	//---------------------------------------------------------------------------------
		#region PROPERTIES BINDINGS
	/* There is a well-known problem about mecanim called "Animator lockdown".
	It means that once you set a clip as one of its state, you will NEVER be able to
	change the properties defined in that clip because it will be overwritten by Animator.
	You cannot even change it via inspector (except if you change it in the script via
	LateUpdate(), which in my humble opinion is stupid because you are playing animation (costly)
	and just override it later (more costly). There is entire thread for this:
	https://forum.unity.com/threads/animator-locking-animated-value-even-when-current-state-has-no-curves-keys-for-that-value.440363/
	This is true even for the clips that ARE NOT PLAYING. The reason is that Animator
	collects all properties from ALL clips, then just write default values when they are
	not playing (Credit: Mecanim-Dev, UF). It seems that the clips will NEVER leave the
	Animator UNLESS you disable and re-enable GAMEOBJECT (surprisingly, re-enabling Animator
	Component via editor resets it, but doing so via script does not), where the current state
	will also be taken as new default state.
	For Playables, it can only be reset by setting output to new SourcePlayable. */
	/* I tried to optimize things while make it easy to use, but since Unity has this
	weird behavior, it is impossible to do fully. It depends on user now to understand
	and implement it:
	- If your graph is playing and you are not unbinding, you should leave bReset=false because
	it will be reset anyway when the graph process.
	- If you are going to stop multiple layers, you should specify bReset=false,
	then call reset() and resetBinding() only once at the end. */
	public void reset(){
		animator.Rebind();
		graph.Evaluate();
	}
	public void resetBinding(){
		output.SetSourcePlayable(layerPlayableController.playable);
		//layerPlayableController always plays (unless graph is paused)
		layerPlayableController.setTime(layerPlayableController.getAnticipatedTime());
		layerPlayableController.bPendingTime = true;
		scheduleClearPendingTime(layerPlayableController);
	}
	/* This function is expensive but useful if you want to set new default values using
	ONLY the specified playableController rather than the current global state. */
	public void resetDefaultValue(PlayableController playableController){
		animator.Rebind();
		output.SetSourcePlayable(playableController.playable);
		graph.Evaluate();
		output.SetSourcePlayable(layerPlayableController.playable);
		graph.Evaluate(GraphDeltaTime);
	}
		#endregion
	//---------------------------------------------------------------------------------
		#region ANIMATION ACTION
	/* We don't expect user to search this list much in runtime, and total Playable tracked
	at one instance should not be big enough to overcome hash overhead of HashSet<T> */
	private LinkedList<PlayableController> llActionTrackedController =
		new LinkedList<PlayableController>();
	internal bool trackAnimationAction(PlayableController controller){
		TreePlayableController treePlayableController = controller as TreePlayableController;
		if(treePlayableController != null){
			int count = treePlayableController.lChildController.Count;
			for(int i=0; i<count; ++i){ //track tree branch recursively
				trackAnimationAction(treePlayableController.lChildController[i]);}
		}
		if(controller?.lAction == null){
			return false;}
		//Credit: Andrew900460, SO
		for(LinkedListNode<PlayableController> node=llActionTrackedController.First; 
			node!=null; node=node.Next)
		{
			if(node.Value == controller){
				return false;}
		}
		controller.updateActionMarker();
		llActionTrackedController.AddLast(controller);
		return true;
	}
	internal bool untrackAnimationAction(PlayableController controller){
		TreePlayableController treePlayableController = controller as TreePlayableController;
		if(treePlayableController != null){
			int count = treePlayableController.lChildController.Count;
			for(int i=0; i<count; ++i){ //untrack tree branch recursively
				untrackAnimationAction(treePlayableController.lChildController[i]);}
		}
		if(controller?.lAction == null){
			return false;}
		for(LinkedListNode<PlayableController> node=llActionTrackedController.First; 
			node!=null; node=node.Next)
		{
			if(node.Value == controller){
				llActionTrackedController.Remove(node);
				return true;
			}
		}
		return false;
	}
		#endregion
	//---------------------------------------------------------------------------------
		#region MISCELLINIOUS
	public ClipPlayableController getPlayableController(AnimationClip clip,int layerID=0){
		if(!clip)
			return null;
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		return layerIndex>=0 ? preparePlayableController(clip,layerIndex,false) : null;
	}
	public TreePlayableController getPlayableController(
		AnimationTree tree,int layerID=0,bool bSyncWeight=false)
	{
		if(tree == null)
			return null;
		int layerIndex = layerPlayableController.getLayerIndex(layerID);
		return layerIndex>=0 ? preparePlayableController(tree,layerIndex,false,bSyncWeight) : null;
	}
	/* These functions are intended to be called in OnAnimatorMove() or LateUpdate() */
	public Vector3 getRootMotionPosition(){ //returns delta position
		return animator.deltaPosition;
	}
	public Quaternion getRootMotionRotation(){ //returns delta rotation
		return animator.deltaRotation;
	}
		#endregion
	//---------------------------------------------------------------------------------	
}
#endregion
//======================================================================================

} //end namespace Chameleon
