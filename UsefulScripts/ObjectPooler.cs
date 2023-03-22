/************************************************************************
 * OBJECTPOOLER (v4.0.1)
 * by Reev the Chameleon
 * 4 Mar 3
*************************************************************************
This is simply a refactor of a code I wrote long ago, which to be honest
is surprisingly good despite being so old. I will keep improving it
as needed. I have changed its name from "ChmObjectPool" to simply
"ObjectPooler", but moved it into namespace Chameleon so to be in line
with other scripts in the package.

Update v2.0: Make the class implement MonoBehaviour to support shared pool case
Update v2.0.1: Fix bug where getting GameObject on Start() may return it inactive
Update v2.1: Add function for getting object while keeping prefab's rotation
Update v2.2: Add parameter in getObject specifying whether position/rotation is in world or local space
Update v3.0: Remove unnecessary code to make the class lighter
Update v4.0: Add drawer class to help distinguish ObjectPooler on same GameObject based on pfObject
Update v4.0.1: Comment out [DefaultExecutionOrder] as it causes NullReferenceException in some cases
*/


/************** ChmObjectPool v.1.0 *****************************************
By Reev The Chameleon
 23 Jun 2 [3 Feb 1]
Disregard paragraph below. To use this script, attach it to your GameObject.
This GameObject will become parent to all instantiated objects in the pool by default.
	[To use this script, just add it to your asset. Type ChmObjectPool will be acknowledged by Unity,
	so you can declare a variable of this type.]

In the script you want to pool, create variable of type ChmObjectPool, and assigns its field:
- pfObject: the object you want to create a pool of.
- countInitial: initial amount or amount of object to create in the pool after "reset"
- bGrow: specify if you want to allow the pool to grow when more objects than existing in the
  pool is requested. When that happens, new instance of pfObject is Instantiated and add
  to the pool.

Paragraph below is no longer true; I have decided to finally make this class inherits MonoBehaviour
so single pool can be shared across multiple scripts easily. Otherwise, it cannot be done without trickery
	[Then once you are ready, you HAVE TO call the "reset" function yourself. Since this ChmObjectPool
	is designed to be efficient, it does NOT inherits MonoBehaviour, so it does not have functions like
	Awake() or Start() for Unity to call; you must do it yourself. (And due to the fact that this
	class is Serializable, trying to initialize things in constructor is meaningless due to the way
	Unity handles things.)]

You can get the number of objects currently in the pool via property Count.
In debug mode inspector, you can look at lObjectInPool field directly.

When you want to retrieve the object, just call "getObject" with position and rotation, like you
would in Instantiate. In case the object may be affected by physics, and you want the pool
to return the reset version, make sure to call "setDelegateResetObject" once, passing in the reset function.
"getObject" will then return the reset version of GameObject.

DO NOT Destroy(gameObject) in the pool. Instead, use gameObject.SetActive(false).
The object can then be reused. IF you Destroy, well, you just break the contract.
The code is not going to break, but you just defeat the purpose of pooling.
Also, the object destroyed will NOT be replenished, unless bGrow is ticked and you request more 
object than existing in the pool.

You can get accumulated number of object Destroyed AND detected by the pool 
by property CountDestroyed (shown in inspector in debug mode).
Note that CountDestroyed updates ONLY when the pool DETECTS that the objects in it has been Destroyed, 
so it does not update immediately in response to object being Destroyed 
(it is not the pool's responsibility to track how the object in it has been used anyway).
This number is reset when "reset" is called.

You can resize the pool at anytime by calling "resize". However, the pool will not resize
to smaller than the number of currently active GameObject. Alternatively, you may want to
"shinkToFit", which will resize the pool to the amount of currently active GameObject
multiplied by specified safety factor.

"reset" discards the whole pool and recreates new one. All GameObjects in the pool will
also be destroyed even if it is currently active in the scene. Use with care.
The pool will fill up with countInitial number of pfObject.
"reset" does not reset dDelegateResetObject
************************************************************************************/

using UnityEngine;
using System.Collections.Generic;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Chameleon{

/* Make sure that reset() is called before any other Awake() or OnEnable(),
even before LoneMonoBehaviour. */
//[DefaultExecutionOrder(-2)]
public class ObjectPooler : MonoBehaviour,IEnumerable<GameObject>{
	[GrayOnPlay] public GameObject pfObject;
	public int countInitial;
	public bool bGrow = true;

	private List<GameObject> lObjectInPool = new List<GameObject>();
	private int indexCurrent = 0;

	public int Count{
		get{ return lObjectInPool.Count; }
	}
	void Awake(){
		reset();
	}
	 //Do not pass in dead parent! WILL throw MissingReferenceException
	public void reset(){
		foreach(GameObject g in lObjectInPool)
			Destroy(g);
		lObjectInPool = new List<GameObject>(); //throw away old list
		for(int i=0; i<countInitial; ++i)
			instantiateObjectInPool();
		indexCurrent = 0;
	}
	private GameObject instantiateObjectInPool(){
		GameObject g = Instantiate(pfObject,transform);
		g.SetActive(false);
		lObjectInPool.Add(g);
		return g;
	}
	public GameObject getObjectRawInactive(){
		//Regardless of whether we remove objects from the list, we will iterate
		//this number of time.
		int objectCount = lObjectInPool.Count;
		//This tries to make it a round-robin. Hope that it is more efficient
		//than conventional.
		for(int i=0; i<objectCount; ++i){
			GameObject g = lObjectInPool[indexCurrent%lObjectInPool.Count];
			if(!g){
				//The code can be more efficient if this check is left out,
				//but this is for safety in case game object is Destroyed
				//i.e. someone breaks the contract of pooling.
				indexCurrent = indexCurrent%lObjectInPool.Count;
				lObjectInPool.Remove(g);
				continue; //the index will automatically points to next element.
			}

			if(!g.activeInHierarchy)
				return lObjectInPool[indexCurrent++%lObjectInPool.Count];

			++indexCurrent;
			//We don't care how large indexCurrent is, as long as modulo works
			//This is for efficiency. Otherwise we have to reassign modulo to indexCurrent
		}
		//No inactive GameObject
		if(bGrow){
			GameObject g = instantiateObjectInPool();
			return g;
		}
		return null;
	}
	public GameObject getObject(
		Vector3 position,Quaternion rotation,Transform tParent,bool bWorldSpace=true)
	{
		GameObject g = getObjectRawInactive();
		if(g){
			g.transform.SetParent(tParent,true); //keep scale
			if(bWorldSpace){
				g.transform.position = position;
				g.transform.rotation = rotation;
			}
			else{
				g.transform.localRotation = rotation;
				g.transform.localPosition = position;
			}
			g.SetActive(true);
		}
		return g;
	}
	public GameObject getObject(Vector3 position){ //keep rotation
		return getObject(position,pfObject.transform.rotation,transform,true);
	}
	public GameObject getObject(Vector3 position,Quaternion rotation){
		return getObject(position,rotation,transform,true);
	}
	public void shrinkToFit(float factorSafety=1.25f,bool bRepairPool=false){
		int countToKeep=0;
		foreach(GameObject g in lObjectInPool){
			if(g.activeInHierarchy)
				++countToKeep;
		}
		countToKeep = (int)Mathf.Ceil(factorSafety*countToKeep);
		resize(countToKeep);
	}

	public void resize(int size){
		//Move active GameObject to new list
		//If size is smaller than active GameObject, we ignore size
		//and move all active GameObject.
		List<GameObject> lTemp = new List<GameObject>();
		foreach(GameObject g in lObjectInPool){
			if(g && g.activeInHierarchy)
				lTemp.Add(g);
		}
		//If the size is still larger, move inactive GameObject to new list also
		for(int i=0; i<lObjectInPool.Count; ++i){
			GameObject g = lObjectInPool[i];
			if(g && !g.activeInHierarchy){
				if(size>lTemp.Count)
					lTemp.Add(g);
				else
					Object.Destroy(g); //destroy extraneous items
			}
		}

		lObjectInPool = lTemp; 
		//now lObjectInPool points to lTemp, and the previous one is garbage collected.

		//At this point, if size is larger, Instantiate until cover
		for(int j=size-lTemp.Count; j>0; --j){
			instantiateObjectInPool();
		}
	}
	public void addToPool(GameObject g){ //Add existing objects to pool
		if(g){
			lObjectInPool.Add(g);
			g.transform.parent = transform;
		}
	}
	public IEnumerator<GameObject> GetEnumerator(){
		return lObjectInPool.GetEnumerator();
	}
	/* IEnumerable must also implement this version (albeit can be private) */
	IEnumerator IEnumerable.GetEnumerator(){
		return GetEnumerator();
	}
}

#if UNITY_EDITOR
/* This drawer adds pfObject name to the text shown in ObjectField slots to help
distinguish them when they come from the same GameObject. */
[CustomPropertyDrawer(typeof(ObjectPooler))]
public class ObjectPoolerPropertyDrawer : PropertyDrawer{
	public override void OnGUI(Rect position,SerializedProperty property,GUIContent label) {
		EditorGUI.BeginProperty(position,label,property);
		EditorGUI.BeginChangeCheck();
		/* This is ingenious trick where one can alter the name shown in ObjectField slot
		"a little" by modifying the .name property immediately before passing it to the
		ObjectField and restore it afterward. (Credit: Immanuel-Scholz, UA, great idea!) */
		ObjectPooler pooler = (ObjectPooler)property.objectReferenceValue;
		string savedName = pooler?.name;
		if(savedName != null){
			pooler.name = pooler.name + ": "
				+ (pooler.pfObject ? pooler.pfObject.name : "(none)");
		}
		ObjectPooler objectPoolerUser = (ObjectPooler)EditorGUI.ObjectField(
			position,
			label,
			pooler,
			typeof(ObjectPooler),
			true
		);
		if(savedName != null){
			pooler.name = savedName; }
		if(EditorGUI.EndChangeCheck()){
			property.objectReferenceValue = objectPoolerUser; }
		EditorGUI.EndProperty();
	}
}
#endif

} //end namespace Chameleon
