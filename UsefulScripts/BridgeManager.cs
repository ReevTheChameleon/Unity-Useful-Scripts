/************************************************************************
 * BRIDGEMANAGER (v1.2.2)
 * by Reev the Chameleon
 * 24 Mar 2
 ************************************************************************
This script allows one to setup Prefab reference to object in the scene
via inspector, because Prefab by default cannot contain reference to
object in the scene.
To use this feature, you have to attach this script (BridgeManager) to
one of the GameObject in the scene that will act as a medium. This
GameObject should always be in the scene, and because BridgeManager is
LoneMonoBehaviour, you can attach it to GameObject once.
Next, register any Object you want to link to to this BridgeManager.
You can either do this via drag-drop or right-click and choose from Bridge menu.
In prefab script, declare variable of type Bridge, and drag the registered
Object to its slot in inspector. You can reference the variable via
bridge.get<>() or bridge.get().
You can specify type of registered object via Bridge constructor. For example:
Bridge bTransform = new Bridge(typeof(Transform));
As for ScriptableObject, you can make it derived from BridgeAwakenListener
and override void onBridgeAwaken(). BridgeManager will call this function
once itself is ready in the scene. This is useful because ScriptableObject
does not receive normal Unity message.
Update v1.1: Prevent Editor-only functions from linking in to build.
Update v1.11: Add underline menu shortcut
Update v1.2: Add code to detect and remove unused bridge terminals from list on validation,
and automatically add GameObject/Component to BridgeManager when assigned to Bridge.
Update v1.2.1: Allow user to specify type of Bridge via constructor.
Update v1.2.2: Fix compile error in real-build and fix undo bug
*/

using UnityEngine;
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

using Object = UnityEngine.Object;

namespace Chameleon{

[Serializable]
public class IDUnityObject : IComparable<IDUnityObject>{
	public int ID;
	public Object unityObject;

	public IDUnityObject(int ID,Object uo){
		this.ID = ID;
		unityObject = uo;
	}
	public int CompareTo(IDUnityObject other){
		return this.ID-other.ID;
	}
}

/* Because Unity can't serialize interface (annoying), have to make base class.
Will test later whether it is OK to put empty ScriptableObject in the same file
as MonoBehaviour or not (since MonoBehaviour class name match file name and we
are not storing data in this ScriptableObject). It doesn't seem to break as far
as test goes. */
public abstract class BridgeAwakenListener : ScriptableObject{
	public virtual void onBridgeAwaken(){}
}

public partial class BridgeManager : LoneMonoBehaviour<BridgeManager>{
	[SerializeField] private List<IDUnityObject> slBridgeEnd
		= new List<IDUnityObject>();
	[SerializeField] private List<BridgeAwakenListener> lAwakenListener
		= new List<BridgeAwakenListener>();

	public static bool isInitialized(){
		return instance; //true if not null
	}
	private static int nextID; //prevent ID reset when removing and adding another bridge
	[SerializeField] private int savedNextID;
	public static int add(Object uo){
		if(!instance)
			return -1; //If ID warps around would be cumbersome. Low chance though
		int index = instance.slBridgeEnd.FindIndex(iduo => iduo.unityObject==uo);
		if(index >= 0)
			return instance.slBridgeEnd[index].ID;
		/* If not doing this, updated list will not be saved since project is
		opened for second time onward for some reason. Note that it is likely that
		this Undo operation is "useless" because Undo internally calls OnValidate(),
		which will destroy Objects unreferred to in BridgeManager rather than just
		undo the addition. Hence, it is actually here only to make Bridge assignment work. */
		#if UNITY_EDITOR
		Undo.RecordObject(instance,"Add Object to BridgeManager");
		#endif
		instance.slBridgeEnd.Add(new IDUnityObject(nextID,uo));
		instance.savedNextID = nextID + 1;
		return nextID++;
	}
	public static bool remove(Object uo){
		if(!instance)
			return false;
		int index = instance.slBridgeEnd.FindIndex(iduo => iduo.unityObject==uo);
		if(index < 0)
			return false;
		#if UNITY_EDITOR
		Undo.RecordObject(instance,"Remove Object from BridgeManager");
		#endif
		instance.slBridgeEnd.RemoveAt(index);
		return true;
	}
	public static bool removeID(int id){
		if(!instance)
			return false;
		int index = instance.slBridgeEnd.BinarySearch(new IDUnityObject(id,null));
		if(index < 0)
			return false;
		instance.slBridgeEnd.RemoveAt(index);
		return true;
	}
	public static Object get(int id){
		if(!instance)
			return null;
		int index = instance.slBridgeEnd.BinarySearch(new IDUnityObject(id,null));
		return index>=0 ? instance.slBridgeEnd[index].unityObject : null;
	}
	public static int findIndex(Object uo){
		if(!instance || !uo)
			return -1;
		return instance.slBridgeEnd.FindIndex(iduo => iduo.unityObject==uo);
	}
	public static int findID(Object uo){
		int index = findIndex(uo);
		if(index < 0)
			return -1;
		return instance.slBridgeEnd[index].ID;
	}
	protected override void Awake() {
		base.Awake();
		for(int i=lAwakenListener.Count-1; i>=0; --i){
			if(lAwakenListener[i] == null)
				lAwakenListener.RemoveAt(i);
			else
				lAwakenListener[i].onBridgeAwaken();
		}
	}
	public static bool registerAwaken(BridgeAwakenListener listener){
		if(!instance || 
			instance.lAwakenListener.FindIndex(bl => bl==listener) >= 0 ||
			listener == null)
			return false;
		#if UNITY_EDITOR
		Undo.RecordObject(instance,"Add BridgeAwakenListener to BridgeManager");
		#endif
		instance.lAwakenListener.Add(listener);
		return true;
	}
	public static bool unregisterAwaken(BridgeAwakenListener listener){
		if(!instance || listener==null)
			return false;
		int index = instance.lAwakenListener.FindIndex(bl => bl==listener);
		if(index < 0)
			return false;
		#if UNITY_EDITOR
		Undo.RecordObject(instance,"Remove BridgeAwakenListener from BridgeManager");
		#endif
		instance.lAwakenListener.RemoveAt(index);
		return true;
	}
#if UNITY_EDITOR
	/* HashSet is better than SortedSet in this case because object does not implement
	IComparable. However, HashSet only requires IEquatable, which object supports.
	Note: In general, if you need sorting/finding element with maximum value etc.,
	Sorted container would better suit. Otherwise, hash table is generally better.
	Dictionary also uses hashtable internally. slBridgeEnd needs to be List because
	it has to be serialized, but we tried make it sorted for best performance. */
	private static HashSet<Bridge> setBridge = new HashSet<Bridge>();
	public static void registerBridge(Bridge bridge){
		setBridge.Add(bridge);
	}
	protected override void OnValidate(){
		base.OnValidate();
		validateList();
		nextID = savedNextID;
	}
	[ContextMenu("&Validate Bridge Terminal List")]
	private void validateList(){
		int[] aReferenceCount = new int[slBridgeEnd.Count];
		/* all elements will initialize to 0 (Credit: Sergey Berezovskiy, SO) */
		foreach(Bridge b in setBridge){
			if(b != null){
				int index = instance.slBridgeEnd.BinarySearch(new IDUnityObject(b.BridgeID,null));
				if(index >= 0)
					++aReferenceCount[index];
			}
		}
		for(int i=slBridgeEnd.Count-1; i>=0 ;--i){
			if(slBridgeEnd[i].unityObject==null || aReferenceCount[i]<=0){
				slBridgeEnd.RemoveAt(i);
			}
		}
		slBridgeEnd.Sort();
		for(int i=lAwakenListener.Count-1; i>=0; --i){
			if(lAwakenListener[i] == null)
				lAwakenListener.RemoveAt(i);
		}
	}
	#endif
}

#if UNITY_EDITOR
public partial class BridgeManager : LoneMonoBehaviour<BridgeManager>{
	[MenuItem("CONTEXT/Component/&Bridge/&Add to BridgeManager",false,601)]
	[MenuItem("GameObject/&Bridge/&Add to BridgeManager",false,41)]
	public static void add(MenuCommand menuCommand) {
		if(BridgeManager.add(menuCommand.context) == -1)
			Debug.LogError("Error!: BridgeManager has not been created");
		else
			Debug.Log("Added "+menuCommand.context+" to BridgeManager");
	}
	[MenuItem("CONTEXT/Component/&Bridge/&Remove from BridgeManager",false,602)]
	[MenuItem("GameObject/&Bridge/&Remove from BridgeManager",false,42)]
	public static void remove(MenuCommand menuCommand) {
		if(!BridgeManager.isInitialized())
			Debug.LogError("Error!: BridgeManager has not been created");
		else {
			BridgeManager.remove(menuCommand.context);
			Debug.Log("Removed "+menuCommand.context+" from BridgeManager");
		}
	}
	//TO DO: Menu Validation

	[CustomEditor(typeof(BridgeManager))]
	private class BridgeManagerEditor : Editor{
		private BridgeManager targetAs;
		private bool bBridgeFoldout = true;
		private bool bListenerFoldout = true;

		void OnEnable(){
			targetAs = target as BridgeManager;
		}
		public override void OnInspectorGUI(){
			bool bSavedEnable = GUI.enabled;
			Object userObjectValue = EditorGUILayout.ObjectField(
				"Register Slot",
				null,
				typeof(Object),
				true
			);
			if(userObjectValue &&
				(userObjectValue is GameObject||userObjectValue is Component))
				BridgeManager.add(userObjectValue);

			bBridgeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
				bBridgeFoldout,
				"List of Bridge Terminals"
			);
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(bBridgeFoldout){
				List<IDUnityObject> lIduo = targetAs.slBridgeEnd;
				++EditorGUI.indentLevel;
				for(int i=0; i<lIduo.Count; ++i) {
					GUI.enabled = false;
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.ObjectField(
						lIduo[i].unityObject,
						typeof(Object),
						true
					);
					//EditorGUILayout.IntField(lIduo[i].ID);
					GUI.enabled = true;
					if(GUILayout.Button("-")){
						Undo.RecordObject(targetAs,"Remove Object from BridgeManager");
						targetAs.slBridgeEnd.RemoveAt(i--);
					}
					EditorGUILayout.EndHorizontal();
				}
				--EditorGUI.indentLevel;
				GUI.enabled = bSavedEnable;
				EditorGUILayout.Space();
			}
				
			EditorGUILayout.BeginHorizontal();
			bListenerFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
				bListenerFoldout,
				"List of Awaken Listeners"
			);
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(bListenerFoldout && GUILayout.Button("+"))
				targetAs.lAwakenListener.Add(null);
			EditorGUILayout.EndHorizontal();
			if(bListenerFoldout){
				List<BridgeAwakenListener> lAwakenListener = targetAs.lAwakenListener;
				++EditorGUI.indentLevel;
				for(int i=0; i<lAwakenListener.Count; ++i){
					EditorGUILayout.BeginHorizontal();
					lAwakenListener[i] = EditorGUILayout.ObjectField(
						lAwakenListener[i],
						typeof(BridgeAwakenListener),
						true
					) as BridgeAwakenListener;
					GUI.enabled = true;
					if(GUILayout.Button("-")){
						Undo.RecordObject(targetAs,"Remove BridgeAwakenListener from BridgeManager");
						targetAs.slBridgeEnd.RemoveAt(i--);
					}
					EditorGUILayout.EndHorizontal();
				}
				--EditorGUI.indentLevel;
			}
		}
	}
}
#endif

/* Would be nice if I can use Bridge<T>, but Unity won't serialize it, nor can it
serialize System.Type, so we are stuck with this awkward string and constructor. */
[Serializable]
public partial class Bridge{
	[SerializeField] private int bridgeID = -1;
	[SerializeField] string sBridgeType;

	public T get<T>() where T:Object{
		return BridgeManager.get(bridgeID) as T;
	}
	public Object get(){
		return BridgeManager.get(bridgeID);
	}
	public virtual Type type{
		get {return typeof(Object);}
	}
	#if UNITY_EDITOR
	public int BridgeID{ get{return bridgeID;} }
	public Bridge(){
		BridgeManager.registerBridge(this);
		sBridgeType = typeof(Object).ToString();
	}
	public Bridge(Type bridgeType){
		BridgeManager.registerBridge(this);
		sBridgeType = bridgeType.ToString();
	}
	/* This works because constructor is called AT LEAST once for each variable
	whose type is marked [Serializable]. Also the STATIC setBridge in BridgeManager
	is reset every time Unity performs hot-reload so we can ensure all bridges are
	registered to that set correctly. */
	#else
	/* If not in Editor, just use default constructor in all cases */
	public Bridge(Type bridgeType){}
	#endif
}

#if UNITY_EDITOR
public partial class Bridge{

[CustomPropertyDrawer(typeof(Bridge))]
public class BridgeDrawer : PropertyDrawer{
	public override void OnGUI(
		Rect position,SerializedProperty property,GUIContent label)
	{
		SerializedProperty idProperty = 
			property.FindPropertyRelative("bridgeID");
		/* Type.GetType(string) works only if that string type is in current executing
		assembly or mscorlib.dll. If not, you have to add ",assemblyName" to the end
		of the string (Credit: DrPizza & Dan Sinclair, SO) */
		Type bridgeType =
			Type.GetType(property.FindPropertyRelative("sBridgeType").stringValue+",UnityEngine");
		Object objectValue = BridgeManager.get(idProperty.intValue);
		Object userValue = EditorGUI.ObjectField(
			position,
			label,
			objectValue,
			bridgeType,
			true
		);
		if(userValue != objectValue){
			if(userValue is GameObject && typeof(Component).IsAssignableFrom(bridgeType))
				userValue = ((GameObject)userValue).GetComponent(bridgeType);
			if(!userValue)
				idProperty.intValue = -1;
			else{
				int id = BridgeManager.findID(userValue);
				if(id < 0)
					id = BridgeManager.add(userValue);
				idProperty.intValue = id;
			}
		}
		//Rect rectTest = new Rect(position.x,position.y,position.width/2,position.height);
		//EditorGUI.IntField(rectTest,idProperty.intValue);
	}
}

}
#endif

} //end namespace Chameleon
