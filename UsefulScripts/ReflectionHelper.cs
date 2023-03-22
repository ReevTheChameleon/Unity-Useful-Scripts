/************************************************************************
 * REFLECTIONHELPER (v2.2)
 * by Reev the Chameleon
 * 1 Sep 2
*************************************************************************
Collections of functions and constants related to reflection
that makes life easier.

Update v1.0.1: Revise code to reduce redundancy
Update v2.0: Remove unused code
Update v2.1: Add function to check if class derived from generic
Update v2.2: Add function for getting element type from generic type
*/

using System.Reflection;
using System;
using System.Collections.Generic;

namespace Chameleon{

public static class ReflectionHelper{
	public const BindingFlags BINDINGFLAGS_ALL =
		BindingFlags.Public | BindingFlags.NonPublic |
		BindingFlags.Static | BindingFlags.Instance |
		BindingFlags.FlattenHierarchy
	;
	public static bool isDerivedFromGeneric(this Type type,Type genericType){
		/* Credit: JaredPar & xanadont, SO */
		if(type==null || genericType==null ||
			!type.IsClass || !genericType.IsGenericType) //type cannot be interface
			return false;
		while(type!=typeof(object)){
			if(type.IsGenericType && type.GetGenericTypeDefinition()==genericType)
				return true;
			type = type.BaseType;
		}
		return false;
	}
	public static Type getElementType(Type listType){ //only support array and List<T> for now
		/* If listType is generic, simply use GetElementType() on listType will
		return null, and we have to check genericTypeArguments() instead.
		(Credit: Jonesopolis, SO & Marc Gravell & zds, SO) */
		if(listType.IsGenericType && listType.GetGenericTypeDefinition()==typeof(List<>))
			return listType.GetGenericArguments()[0];
		else
			return listType.GetElementType(); //null if fails
	}
}

} //end namespace Chameleon
