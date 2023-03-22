/**************************************************************
 * JSONOBJECT (v1.0)
 * by Reev the Chameleon
 * 2 Jul 2
***************************************************************
Functions used to convert back and forth between fields and their
JSON strings representation.
Background:
Unity's JsonUtility can only be used with classes, so using it with
single variable would require wrapping it in a class. Also you need
to specify the type at compile time, which is severe restriction for usage
in Editor coding.
JsonObject allows more flexibility by internally managing wrapper class
as well as having functions that allows you to convert object of dynamic-types
from/to JSON at runtime if the types are supported.
*/

using UnityEngine;
using System;

namespace Chameleon{

public class JustWrapper<T>{
	public T obj;
}

public static class JsonObject{
	public static string jsonFromObject(Type type,object oValue){
		bool bArray = type.IsArray;
		Type elementType = bArray ? type.GetElementType() : type;
		if(!isSupportedType(elementType))
			return null;
		switch(elementType.Name){
			case nameof(Gradient): return jsonFromObject<Gradient>(oValue,bArray);
			case nameof(AnimationCurve): return jsonFromObject<AnimationCurve>(oValue,bArray);
		}
		return null;
	}
	public static string jsonFromObject<TElement>(object oValue,bool bArray){
		if(bArray)
			return JsonUtility.ToJson(new JustWrapper<TElement[]>{obj=(TElement[])oValue});
		return JsonUtility.ToJson(new JustWrapper<TElement>{obj=(TElement)oValue});
	}
	public static object objectFromJson(Type type,string json,bool bArray){
		Type elementType = bArray ? type.GetElementType() : type;
		if(!isSupportedType(elementType))
			return null;
		switch(elementType.Name){
			case nameof(Gradient): return objectFromJson<Gradient>(json,bArray);
			case nameof(AnimationCurve): return objectFromJson<AnimationCurve>(json,bArray);
			default: return null;
		}
	}
	public static object objectFromJson<TElement>(string json,bool bArray){
		if(bArray)
			return JsonUtility.FromJson<JustWrapper<TElement[]> >(json).obj;
		return JsonUtility.FromJson<JustWrapper<TElement> >(json).obj;
	}
	public static bool isSupportedType(Type type){
		if(type == typeof(Gradient) ||
			type == typeof(AnimationCurve)
		){
			return true;
		}
		return false;
	}
}

} //end namespace Chameleon
