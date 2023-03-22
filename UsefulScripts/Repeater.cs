/***********************************************************
 * REPEATER (v2.0)
 * by Reev the Chameleon
 * 19 Mar 2
 ***********************************************************
** Note: Currently loses to Unity's native "InvokeRepeating"
in practice. **
Update v2.0: Add new version which perform minimal jobs in
FixedUpdate to avoid framerate drop if schedule event is not dued.
*/

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Chameleon{

public class RepeaterLegacy : GhostMonoBehaviour<RepeaterLegacy>{
	private class Scheduled{
		public int id;
		public Action action; //delegate of type void()
		public float countdown;
		public float interval;

		public Scheduled(Action action,float initialDelay,float interval){
			this.id = nextID;
			this.action = action;
			this.countdown = initialDelay;
			this.interval = interval;
		}
	}
	private static List<Scheduled> lScheduled = new List<Scheduled>();
	private static int nextID = 0;

	public static int invokeRepeating(Action action,float initialDelay,float interval){
		if(!instance)
			instance = new GameObject("[RepeaterLegacy]").AddComponent<RepeaterLegacy>();
		lScheduled.Add(new Scheduled(action,initialDelay,interval));
		return nextID++;
	}

	public static bool cancelRepeating(int actionID){
		for(int i=lScheduled.Count-1; i>=0; --i){
			if(lScheduled[i].id == actionID){
				lScheduled.RemoveAt(i);
				if(lScheduled.Count == 0)
					Destroy(instance.gameObject);
				return true;
			}
		}
		return false;
	}

	public static void cancelAllRepeating(){
		lScheduled.Clear();
		Destroy(instance.gameObject);
	}

	void FixedUpdate(){
		float fixedDeltaTime = Time.fixedDeltaTime;
		foreach(Scheduled r in lScheduled){
			r.countdown -= fixedDeltaTime;
			while(r.countdown <= 0.0f){
				r.action.Invoke();
				r.countdown += r.interval;
			}
		}
	}
}

/* There was one major problem with previous version of Repeater, now renamed to "RepeaterLegacy",
namely that the ENTIRE list of Scheduled must be iterated every frame just to subtract countdown
by Time.fixedDeltaTime. In most circumstances this should be acceptable, but it still bugs me
that unnecesary calculation is taking place. The performance becomes very low when number of
entries becomes large (like >10000), and profiler reveals peaks every FixedUpdate().
In this newer version, instead of doing calculation for all entries, we leave the entries as are,
but sort them in order according to due time. We then compare current time with due time. If
the current time does not yet reach the due time, we break out of the loop early. This way we do not
need to do calculation if time is not yet due. Drawback of this method is that once due time is reached
and its due time is added by repeating interval, the list must be re-sorted. Because the list is
almost sorted, insertion sort algorithm will work better than other algorithm, but still it can be
quite slower than brute-force all entries calculation. In other word, this version accepts higher
workload peak at time due in trade off for virtually no calculation in FixedUpdate() while 
it is not due. In fact, it seems that usual "InvokeRepeating" is doing something similar because
it also has unusual high peak at time due. However, it is shown that this method beats usual
"InvokeRepeating" if many entries are dued in the same FixedUpdate(). Still, "InvokeRepeating" wins
if schedules are dued frequently i.e. every FixedUpdate() because that would mean doing sort in
every FixedUpdate().
Ideal solution is perhaps using std::map in native C++ where you can remove and re-add element
while iterating (C# does not allow that). Will study how to interface with C++ in next version. */
public class Repeater : LoneMonoBehaviour<Repeater>{
	/* Time.time is float, which will lose precision when becoming large, such that its precision
	becomes worse than 10ms (1 frame at 100FPS) in about 36 hours (Credit: Bunny83, UA).
	Since version 2020.3 you can get Time.timeAsDouble, which should run for centuries before
	losing that much precision. However, to accommodate for previous versions as well, we do own
	addition. We use start value of 5 billion so the precision will be more consistent
	(Credit: Bruce Dawson, SO) */
	private static double accumTime = 5000000000.0;
	private static bool bSorted;
	private class Scheduled : IComparable<Scheduled>{
		public int id;
		public Action action; //delegate of type void()
		public double dueTime;
		public float interval;

		public Scheduled(Action action,float initialDelay,float interval){
			this.id = nextID;
			this.action = action;
			this.dueTime = accumTime+initialDelay;
			this.interval = interval;
		}
		public int CompareTo(Scheduled other){
			if(dueTime > other.dueTime)
				return 1;
			else if(dueTime < other.dueTime)
				return -1;
			else
				return id-other.id;
		}
	}

	private static List<Scheduled> slScheduled = new List<Scheduled>();
	private static int nextID = 0;
	
	public static int invokeRepeating(Action action,float initialDelay,float interval){
		if(!instance)
			instance = new GameObject("[Repeater]").AddComponent<Repeater>();
		slScheduled.Add(new Scheduled(action,initialDelay,interval));
		bSorted = false;
		return nextID++;
	}
	void FixedUpdate(){
		if(!bSorted){
			//slScheduled.Sort();
			Algorithm.insertionSort(slScheduled);
			bSorted = true;
		}
		int dueCount = 0;
		accumTime += Time.fixedDeltaTime;
		foreach(var r in slScheduled){
			if(r.dueTime > accumTime)
				break;
			++dueCount;
			do{
				r.action.Invoke();
				r.dueTime += r.interval;
			}while(r.dueTime <= accumTime);
		}
		if(dueCount > 0)
			//slScheduled.Sort();
			Algorithm.insertionSort(slScheduled);
	}
	public static bool cancelRepeating(int actionID) {
		for(int i=slScheduled.Count-1; i>=0; --i) {
			if(slScheduled[i].id == actionID) {
				slScheduled.RemoveAt(i);
				if(slScheduled.Count == 0)
					Destroy(instance.gameObject);
				return true;
			}
		}
		return false;
	}
	public static void cancelAllRepeating() {
		slScheduled.Clear();
		Destroy(instance.gameObject);
	}
}

}
