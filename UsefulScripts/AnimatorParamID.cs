/**************************************************************
 * ANIMATORPARAMID (v1.0.1)
 * by Reev the Chameleon
 * 31 Aug 2
***************************************************************
This class is designed to avoid hardcoding Animator's parameters
in the script. User can assign parameter via the inspector
and access it via Id property of this class or exploit implicit int
operator and just pass the object where parameter ID is needed.
Usage:
1. Declare variable of type AnimatorParamID_Bool, AnimatorParamID_Float,
AnimatorParamID_Int, or AnimatorParamID_Trigger in the script.
*** DO NOT declare variable as type AnimatorParamID directly. ***
2. Assign AnimatorParamID via the inspector. The inspector should
show a slot for AnimatorController asset and a dropdown for
available parameters found in it.
3. Set or get corresponding parameters in Animator using extension methods:
animator.SetParameter or animator.GetParameter respectively.
Compiler should help prevent you from setting parameter with wrong types.

Note: It has been considered whether to verify id before build/get or not,
(because AnimatorController asset might be modified without OnGUI()
being called) but since verifying id seems more expensive than other classes
(require looping through all parameters), and missing parameters will only
show up as warning that cannot be self-corrected anyway, it is decided 
that this is unnecessary for now.

Update v1.0.1: Fix bug where the id does not update sometimes
*/

using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace Chameleon{

/* User should not declare variable of this type directly.
(Unfortunately, C# does not have notion of private inheritance like C++,
so compiler will not prevent declaring variable of this type.
I even consider adding [Obsolete] to it, but, well, it isn't.) */
/* Ideally I would not put [Serializable] here, but that will produce error in
Development Build about "different serialization when loading" */
[Serializable]
public abstract partial class AnimatorParamID{
	[SerializeField] int id;
	#if UNITY_EDITOR
	[SerializeField] AnimatorController animatorController;
	#endif
	public int Id{ get{return id;} }
	public static implicit operator int(AnimatorParamID animatorParamID){
		return animatorParamID.id;
	}
}

/* C# does not support template with integral template parameter
(otherwise I would go AnimatorParamID<AnimatorControllerParameterType T>),
so because there are not that many varients, just directly make class
for each type. */
[Serializable] public class AnimatorParamID_Bool : AnimatorParamID{}
[Serializable] public class AnimatorParamID_Int : AnimatorParamID{}
[Serializable] public class AnimatorParamID_Float : AnimatorParamID{}
[Serializable] public class AnimatorParamID_Trigger : AnimatorParamID{}

//=============================================================================
	#region PROPERTY DRAWER
#if UNITY_EDITOR
public partial class AnimatorParamID{

[CustomPropertyDrawer(typeof(AnimatorParamID),true)]
class AnimatorParamIDDrawer : PropertyDrawer{
	private int index = -1;
	private readonly int nullHash = Animator.StringToHash(""); //Actually this seems to be 0
	private bool bInitialized = false;

	public override void OnGUI(
		Rect position,SerializedProperty property,GUIContent label)
	{
		if(fieldInfo.FieldType == typeof(AnimatorParamID))
			return; //Not showing raw AnimatorParamID type in inspector
		
		Rect originalPos = position;
		EditorGUI.LabelField(position,label);
		
		SerializedProperty spId = property.FindPropertyRelative(nameof(AnimatorParamID.id));
		SerializedProperty spAnimator =
			property.FindPropertyRelative(nameof(AnimatorParamID.animatorController));
		AnimatorController animatorController =
			(AnimatorController)spAnimator.objectReferenceValue;

		position.x += EditorGUIUtility.labelWidth;
		position.width = (position.width-EditorGUIUtility.labelWidth)/2.0f-3.0f;
		EditorGUI.BeginChangeCheck();
		EditorGUI.PropertyField(position,spAnimator,GUIContent.none);
		if(EditorGUI.EndChangeCheck())
			index = -1;
		
		position.x += position.width+2.0f;

		AnimatorControllerParameter[] aParam = null;
		if(animatorController){
			aParam = animatorController.parameters;
			if(!(index==-1 && bInitialized) &&
				(index<0 || index>=aParam.Length ||
				aParam[index].nameHash!=spId.intValue)
			){
				index = findIndex(aParam,spId.intValue);
				if(index != -1)
					spId.intValue = aParam[index].nameHash;
				bInitialized = true;
			}
		}
		string sParam = index<0 ? "<none>" : aParam[index].name;

		if(GUI.Button(position,sParam,EditorStyles.popup)){
			GenericMenu menuSelect = new GenericMenu();
			if(aParam != null){
				foreach(AnimatorControllerParameter param in aParam){
					if(isMatchingParamType(param)){
						menuSelect.AddItem(
							new GUIContent(param.name),
							false,
							() => {
								spId.intValue = param.nameHash;
								spId.serializedObject.ApplyModifiedProperties();
								index = findIndex(aParam,spId.intValue);
							}
						);
					}
				}
				menuSelect.AddSeparator("");
			}
			menuSelect.AddItem(
				new GUIContent("Clear"),
				false,
				() => {
					spId.intValue = nullHash;
					spId.serializedObject.ApplyModifiedProperties();
					index = -1;
				}
			);
			menuSelect.DropDown(position);
		}
	}
	private int findIndex(AnimatorControllerParameter[] aParam,int hashId){
		if(aParam != null){
			for(int i=0; i<aParam.Length; ++i){
				if(hashId == aParam[i].nameHash)
					return i;
			}
		}
		return -1;
	}
	private bool isMatchingParamType(AnimatorControllerParameter aParam){
		switch(fieldInfo.FieldType.Name){
			case nameof(AnimatorParamID_Bool):
				return aParam.type==AnimatorControllerParameterType.Bool;
			case nameof(AnimatorParamID_Float):
				return aParam.type==AnimatorControllerParameterType.Float;
			case nameof(AnimatorParamID_Int):
				return aParam.type==AnimatorControllerParameterType.Int;
			case nameof(AnimatorParamID_Trigger):
				return aParam.type==AnimatorControllerParameterType.Trigger;
		}
		return false;
	}
}

} //end partial class AnimatorParamID
#endif
#endregion
//=============================================================================

//=============================================================================
	#region RELATED ANIMATOR EXTENSIONS
public static class AnimatorExtension{
	public static void getParameter(
		this Animator animator,AnimatorParamID_Bool paramId)
	{
		animator.GetBool(paramId);
	}
	public static void getParameter(
		this Animator animator,AnimatorParamID_Float paramId)
	{
		animator.GetFloat(paramId);
	}
	public static void getParameter(
		this Animator animator,AnimatorParamID_Int paramId)
	{
		animator.GetInteger(paramId);
	}
	public static void setParameter(
		this Animator animator,AnimatorParamID_Bool paramId,bool b)
	{
		animator.SetBool(paramId,b);
	}
	public static void setParameter(
		this Animator animator,AnimatorParamID_Float paramId,float f)
	{
		animator.SetFloat(paramId,f);
	}
	public static void setParameter(
		this Animator animator,AnimatorParamID_Int paramId,int i)
	{
		animator.SetInteger(paramId,i);
	}
	public static void setParameter(
		this Animator animator,AnimatorParamID_Trigger paramId)
	{
		animator.SetTrigger(paramId);
	}
}
	#endregion
//=============================================================================

} //end namespace Chameleon
