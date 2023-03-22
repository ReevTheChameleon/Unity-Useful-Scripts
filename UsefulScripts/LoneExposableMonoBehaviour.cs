/************************************************************
 * LONEEXPOSABLEMONOBEHAVIOUR (v1.1)
 * by Reev the Chameleon
 * 9 Jan 2
*************************************************************
Usage:
public class YourClass : LoneExposableMonoBehaviour<YourClass>
There can only be one of this script in the scene,
but with features of ExposableMonoBehaviour.
Update v1.1: Prevent linking Editor-only functions in build (OnValidate()) AND
ensure that "instance" is not null when requested by OnEnable() from other scripts.
*/

using UnityEngine;

namespace Chameleon{

[DefaultExecutionOrder(-1)]
public abstract class LoneExposableMonoBehaviour<T> : ExposableMonoBehaviour
	where T : LoneExposableMonoBehaviour<T>
{
	protected static T instance;

	private void ensureLoneInstance(){
		if(instance == null)
			instance = this as T;
		else if(instance != this){
			Debug.LogError("Error!: Instance already exists at "+instance.gameObject);
			DestroyImmediate(this);
		}
	}
	protected override void Awake(){
		ensureLoneInstance();
	}

	#if UNITY_EDITOR
	protected override void Reset(){
		ensureLoneInstance();
	}
	protected virtual void OnValidate(){
		if(instance && instance != this){
			Debug.LogWarning("Warning!: Instance already exists at "+instance.gameObject+
				" and only one will survive when entering play mode");
		}
		instance = this as T;
	}
	#endif
}

}
