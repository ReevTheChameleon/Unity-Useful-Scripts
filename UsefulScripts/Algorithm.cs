/************************************************************************
 * ALGORITHM (v1.3)
 * by Reev the Chameleon
 * 25 Dec 2
 ************************************************************************
Collection of useful algorithm functions

Update v1.1: Add shuffle algorithm
Update v1.2: Add binary key search algorithm
Update v1.2.1: Make addSorted<T> returns an inserted index
Update v1.3: Add hasOneBitSet algorithm
*/

using System;
using System.Collections.Generic;

using Random = UnityEngine.Random;

namespace Chameleon{

public static class Algorithm{
	public static IList<T> insertionSort<T>(IList<T> l) where T:IComparable<T>{
		//Credit: Pajdziu, SO
		int count = l.Count;
		for(int i=1; i<count; ++i){
			T value = l[i];
			int j = i-1;
			while(j>=0 && value.CompareTo(l[j])<0){
				l[j+1] = l[j];
				--j;
			}
			l[j+1] = value;
		}
		return l;
	}
	public static IList<T> shuffle<T>(IList<T> l){
		//based on Fisher-Yates shuffle (Credit: Uwe Keim, grenade, Jon Skeet, SO)
		for(int i=l.Count-1; i>0; --i){
			int j = Random.Range(0,i+1);
			//swap
			T temp = l[j];
			l[j] = l[i];
			l[i] = temp;
		}
		return l;
	}
	/* List<T>.BinarySearch requires type T as argument, so usual usage is to
	"create dummy T" and set some fields before searching, which can be wasteful
	(Credit: Gerard & Anders Abel, SO) (Note: C++ does not have this restriction
	because comparer can take argument of two types (one must be T, of course) (Credit: GManNickG, SO))
	This function allows you to search with key instead (idea Credit: digEmAll, SO).
	predicate should return <0 if first argument<second argument. */
	public static int binaryKeySearch<T,TKey>(
		IList<T> list,TKey key,Func<T,TKey,int> predicate)
	{
		/* Use one comparison per round implementation because half of the keys needs
		full-depth search anyway, so having extra equality case in the while loop will add
		1 comparison per round at the benefit of about 1 less round on average if key exists,
		or no benefit otherwise. (Credit: Yves Daoust, SO) */
		int count = list.Count;
		int l = 0;
		int r = count;
		while(l < r){
			int m = (l+r)/2;
			if(predicate(list[m],key) < 0) //list[m]<key
				l = m+1;
			else
				r = m;
		}
		if(l >= count)
			return ~count; 
		return predicate(list[l],key)==0 ? l : ~l;
	}
	/* Assume that l is already sorted, this function add new element in
	correct position without needing to re-sort (Credit: noseratio, SO)
	return inserted index */
	public static int addSorted<T>(List<T> l,T obj) where T:IComparable<T>{
		int index = l.BinarySearch(obj);
		if(index < 0)
			index = ~index;
		l.Insert(index,obj);
		return index;
	}
	public static bool hasOneBitSet(int i){
		return i!=0 && (i&(i-1))==0; //Credit: Abhishek Keshri, SO
	}
}
	
} //end namespace Chameleon
