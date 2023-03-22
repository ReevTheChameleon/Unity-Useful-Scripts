/************************************************************************
 * LONEMONOBEHAVIOUR (v2.1)
 * by Reev the Chameleon
 * 4 Mar 3
*************************************************************************
Usage:
public class YourClass : LoneMonoBehaviour<YourClass>
There can only be one of this script in the scene.

Update v1.1: Prevent hanging in right-click reset
Update v1.2: Prevent static instatic reset when switch mode.
Update v1.3: Display warning if user hackishly create duplicate
Update v1.4: Prevent linking Editor-only functions in build (OnValidate()) AND
ensure that "instance" is not null when requested by OnEnable() from other scripts.
Update v1.5: Add support for DontDestroyOnLoad
Update v2.0: Solve most of the editor issues by using delayCall to destroy duplicates
Update v2.0.1: Minor code change about warning message
Update v2.1: Add code to detect & throw in Editor if Instance is accessed before initialized
*/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Chameleon{

/* This makes sure that Awake() of this class is called BEFORE OnEnable() of any other
normal classes, because Awake() and OnEnable() are called in pair on each object before
moving to the next . This is important because it is likely that derived class of this
will have events that other classes register to at OnEnable(). There are other ways
to resolve this, but this is the best in my opinion by far. */
[DefaultExecutionOrder(-1)]
public abstract class LoneMonoBehaviour<T> : MonoBehaviour
	where T : LoneMonoBehaviour<T>
{
	[SerializeField][GrayOnPlay] bool bDontDestroyOnLoad;
	protected static T instance;
	public static T Instance{
		get{
			#if UNITY_EDITOR
			/* If instance==null and caller uses it, exception is thrown like normal.
			However, in Editor, instance may be non-null even when Awake has not been called,
			so even with null check the non-null code may run. This prevents it. */
			if(instance && !instance.bAwaken){
				throw(new System.NullReferenceException(
					"At "+typeof(T)+": Instance is accessed before initialized"));
			}
			#endif
			return instance;
		}
	}
	
	protected virtual void Awake(){ //Prevent attachment in play mode.
		if(instance && instance!=this){
			if(bDontDestroyOnLoad)
				Destroy(gameObject);
			else
				Destroy(this);
		}
		else{ //instance==null
			instance = (T)this;
			if(bDontDestroyOnLoad)
				DontDestroyOnLoad(gameObject);
		#if UNITY_EDITOR
			bAwaken = true;
		#endif
		}
	}

	#if UNITY_EDITOR
	private bool bAwaken = false;
	protected virtual void OnValidate(){
		if(instance && instance!=this){
			if(!Application.isPlaying || !bDontDestroyOnLoad)
				Debug.LogError("Error!: Instance already exists at "+instance.gameObject);
			EditorApplication.delayCall += ()=>{DestroyImmediate(this);};
			/* DestroyImmediate() CANNOT be called in OnValidate(), so use delayCall.
			Note: delayCall seems to be cleared once it is called (checked by
			GetInvocationList().Length), we don't need to unsubscribe */
		}
		else
			instance = (T)this;
	}
	#endif
}

}
