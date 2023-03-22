/***********************************************************
 * TIMEDROUTINE (v1.0.2)
 * by Reev the Chameleon
 * 21 Oct 2
 ***********************************************************
This version uses List to store routines and returns Routine.
This causes more initial memory allocation than normal Coroutine
(~1.5 times, or about 100 bytes per start of one routine, not
counting allocation in coroutine function itself),
but also with no allocation when routine run.
It is about 4 times faster than normal Coroutine by about.
It is also about 4 times faster to stop than normal Coroutine.

Update v1.0.1: Minor bug fix
Update v1.0.2: Change class name to avoid name collision

*** This class is obsolete; it may be removed later. ***
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Chameleon{

/* This allows one to reuse value use in yield.
Surprisingly, yield return 3 or other value types allocates
memory every time and value cannot be cached. */
public class WaitRoutine{
	public float wait;
	public WaitRoutine(float wait){
		this.wait=wait;
	}
}

public class RoutineOld{ //This avoids mutable struct in foreach loop
	public IEnumerator enumerator;
	public float countdown;
		
	public RoutineOld(IEnumerator enumerator){
		enumerator.MoveNext(); //proceed until first yield
		this.enumerator = enumerator;
		countdown = (enumerator.Current as WaitRoutine)?.wait ?? 0.0f;
	}
}

public class TimedRoutine : GhostMonoBehaviour<TimedRoutine>{
	private static List<RoutineOld> lRoutine = new List<RoutineOld>();
	
	public static RoutineOld startRoutine(IEnumerator enumerator){
		//Derived from Baste, UF
		if(!instance)
			instance = new GameObject("[TimedRoutine]").AddComponent<TimedRoutine>();
		RoutineOld routine = new RoutineOld(enumerator);
		lRoutine.Add(routine);
		return routine;
	}

	public static bool stopRoutine(RoutineOld routine){
		bool bRemoved = lRoutine.Remove(routine);
		if(lRoutine.Count == 0)
			Destroy(instance.gameObject);
		return bRemoved;
	}

	public static void stopAllRoutine(){
		lRoutine.Clear();
		Destroy(instance.gameObject);
	}

	void Update(){
		float deltaTime = Time.deltaTime;
		//Since you cannot modify collection in foreach loop,
		//mark element and remove afterward.
		for(int i=lRoutine.Count-1; i>=0; --i){
			RoutineOld r = lRoutine[i];
			r.countdown -= deltaTime;
			if(r.countdown <= 0.0f){
				if(r.enumerator.MoveNext())
					r.countdown = (r.enumerator.Current as WaitRoutine)?.wait ?? 0.0f;
				else
					lRoutine.RemoveAt(i);
			}
		}
		if(lRoutine.Count == 0)
			Destroy(instance.gameObject);
	}
}

}
