/***********************************************************
 * GHOSTMONOBEHVAIOUR (v1.1)
 * by Reev the Chameleon
 * 9 Jan 2
 ***********************************************************
Usage:
public class YourClass : LoneMonoBehaviour<YourClass>
Cannot be attached to GameObject via editor,
and will Destroy GameObject it is attached to if removed.
Update v1.1: Prevent Editor-only functions from linking into real build.
*/

using UnityEngine;

namespace Chameleon{

public abstract class GhostMonoBehaviour<T> : MonoBehaviour
	where T : GhostMonoBehaviour<T>
{
	protected static T instance;

	protected virtual void Awake(){
		if(instance)
			DestroyImmediate(this);
	}
	protected virtual void OnDestroy(){
		//If it is destroyed, no reason for its GameObject to be around.
		Destroy(gameObject);
	}
	#if UNITY_EDITOR
	protected virtual void Reset(){
		DestroyImmediate(this);
	}
	#endif
}

}