/*************************************************************************
 * LONECOROUTINE (v3.0.1)
 * by Reev the Chameleon
 * 26 Feb 3
**************************************************************************
Usage:
This class acts mainly as a Coroutine wrapper (it does NOT derive from one).
In MonoBehaviour derived class,
- Declare variable like so: LoneCoroutine routine = new LoneCoroutine();
- Wrap IEnumerator in it and Start Coroutine by: routine.start(this,yourFunc());
If previous routine is running, it will be stopped first.
The calling syntax with MonoBehaviour argument is a bit weird,
but should be simplest and fastest implementation. Might look into it later.
- Stop current running Coroutine by: routine.stop();
- You can query whether the routine is running via IsRunning property.
- Get wrapped IEnumerator as specified type T by: routine.getItr<T>();
- You can resume stopped IEnumerator by calling routine.start(this,itr);
By C# nature, itr will continue from where you left it. However, if you want
to run it new and fresh, you must pass in new IEnumerator by calling yourFunc() again.
- For IEnumerator that implements valid Reset(), you can just call routine.restart().
This avoids recreating IEnumerator again.
- You can wait for LoneCoroutine to finish by yielding on WaitLoneCoroutine instance
returned by start(), resume(), or restart(). Once it yields, you can check
WasStopped property to see whether LoneCoroutine ends by itself or was stopped.

*** CAUTION: This class does NOT play well with StopAllCoroutines due to it
bypassing the IsRunning manipulation. It will still start and stop Coroutine
correctly thank to how StopCoroutine can be called on stopped Coroutine (albeit
inefficient), but querying IsRunning will return wrong result.
Might think of some solutions later. ***

Rationale:
It seems that StopCoroutine with null Coroutine will throw. However, StopCoroutine
will NOT make the coroutine becomes null, and even when that coroutine has stopped,
you can still call StopCoroutine on it once again. However, StopCoroutine is quite slow,
and it allocates some 328KB, so it is better to check bIsRunning and not have to call
StopCoroutine when unnecessary.
Instead of managing that bIsRunning variable yourself, this class manage it for you by
wrapping the Coroutine along with the bool. You then don't have to worry about declaring and
setting that bool in your routine function, nor worry that stopping the Coroutine will throw,
and can be assured that "start" will always clear out any existing running coroutines.
Sometimes you need to wait for LoneCoroutine to finish. You can poll IsRunning, but
if somebody else starts it with something else or restarts, IsRunning will still be true
despite previous routine has been stopped and discarded. Another option is to yield Coroutine,
but that will hang forever if Coroutine is stopped. Yet another method is giving away IEnumerator,
but that defeats the purpose of LoneCoroutine.
Hence, we provide WaitLoneCoroutine class for waiter to yield. It is managed in a way that
each start of LoneCoroutine marks previous instance as done and creates new corresponding instance.
As it inherits CustomYieldInstruction, there is a cost that waiter will check its keepWaiting
every frame, but this is the cleanest way to do it as far as I know.

Update v1.1: Add evOnStop and supporting code
Update v1.2: Add optional parameter to allow stopping LoneCoroutine without triggering evOnStop
Update v2.0: Revamp code to avoid starting unnecessary Coroutine and unnecessary memory allocation,
add resume, restart, and getItr functionality, remove evOnStop because it is considered redundant
Update v3.0: Add WaitLoneCoroutine class and add code to handle stopping IStopHandler
Update v3.0.1: Add warnings when resume() is called on uninitialized LoneCoroutine
*/

using UnityEngine;
using System;
using System.Collections;

namespace Chameleon{

public class LoneCoroutine{
	private MonoBehaviour monoBehaviour;
	private Coroutine coroutine;
	private WaitLoneCoroutine lastWait = new WaitLoneCoroutine(); //There is only one instance for each LoneCoroutine.

	public LoneCoroutine(MonoBehaviour monoBehaviour=null,IEnumerator itr=null){
		this.monoBehaviour = monoBehaviour;
		this.Itr = itr;
	}
	public bool IsRunning{get; private set;} = false;
	public IEnumerator Itr{get; private set;}
	public T getItr<T>() where T:class,IEnumerator{
		return Itr as T;
	}
	public void stop(){
		if(IsRunning){
			monoBehaviour.StopCoroutine(coroutine);
			(Itr as IStopHandler)?.onStop();
			IsRunning = false;
			lastWait.bDone = true;
		}
		lastWait.WasStopped = true;
	}
	public WaitLoneCoroutine start(MonoBehaviour monoBehaviour,IEnumerator itr){
		stop(); //will do nothing if !IsRunning
		this.monoBehaviour = monoBehaviour;
		this.Itr = itr;
		IsRunning = true;
		lastWait = new WaitLoneCoroutine();
		this.coroutine = monoBehaviour.StartCoroutine(rfRun());
		return lastWait;
	}
	public WaitLoneCoroutine resume(){
		if(!monoBehaviour){
			Debug.LogWarning("resume() called on uninitialized LoneCoroutine");
			return null;
		}
		if(!IsRunning){
			IsRunning = true;
			lastWait = new WaitLoneCoroutine(); //must come before StartCoroutine in case Itr.MoveNext() false immediately
			coroutine = monoBehaviour.StartCoroutine(rfRun());
		}
		return lastWait;
	}
	/* IMPORTANT! While restart() saves memory by avoiding creating new IEnumerator,
	it REQUIRES that Reset() is implemented. Note that IEnumerator auto-generated by
	compiler from yield function will THROW NonSupportedException if Reset() is called,
	so restart() cannot be used with such IEnumerator. */
	public WaitLoneCoroutine restart(){
		stop();
		Itr.Reset();
		return resume();
	}
	private IEnumerator rfRun(){
		while(Itr.MoveNext())
			yield return Itr.Current;
		//yield return monoBehaviour.StartCoroutine(itr);
		/* This is equivalent to above, but causes Unity to register one more Coroutine,
		and hence allocates unnecessary memory (Credit Idea: Ruben Kazumov & Kyle G, SO). */
		IsRunning = false;
		lastWait.bDone = true;
	}
}
public class WaitLoneCoroutine : CustomYieldInstruction{
	public bool WasStopped{get; internal set;} = false;
	internal bool bDone = false;
	public override bool keepWaiting{ get{return !bDone;} }
}

} //end namespace Chameleon
