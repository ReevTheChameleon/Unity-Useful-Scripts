/**************************************
 * ATTRIBUTE (v6.0.1)
 * by Reev the Chameleon
 * 17 Jan 3
***************************************
Properties with [GrayOnPlay] attribute attached will gray out in Play Mode.
Properties with [Gray] attribute attached will always be gray out.
int or float properties with [Max] or [Clamp] attribute will be restricted to specified
range, and will be drawn in inspector without slider.
(Unity already provides [Min] attribute, so it is not included here.)
string with [Tag] attribute will show in inspector as tag selection dropdown.

Update v2.0: Fix missing label for [GrayOnPlay], and add [Gray] attribute
Update v3.0: Add [Max] and [Clamp] attributes
Update v4.0: Add [Tag] attribute
Update v5.0: Add [WideMode], [WideVector2], and [EnumIndex] attributes
Update v5.0.1: Fix IndexOutOfRange bug in [EnumIndex]
Update v5.1: Make [WideVector2] use wideMode, and make [EnumIndex] work with custom classes
Update v6.0: Add [Layer] and [FixedSize], remove [EnumIndex], and make some drawers use ForwardDrawer
Update v6.0.1: Make [WideVector2] work with Vector2[]
*/

using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif

namespace Chameleon{

//======================================================================================
#region GRAYONPLAY ATTRIBUTE
/* Note: This attribute CANNOT disable array "size" box and "+/-" button.
In fact, this cannot be done by CustomPropertyDrawer (Credit: TonyLi, UF) */
public class GrayOnPlayAttribute : PropertyAttribute{ }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(GrayOnPlayAttribute))]
public class GrayOnPlayDrawer : ForwardDrawer{
	public override void OnGUI(
		Rect position,SerializedProperty property,GUIContent label)
	{
		if(Application.isPlaying){
			bool bDisabled = GUI.enabled;
			GUI.enabled = false;
			base.OnGUI(position,property,label);
			GUI.enabled = bDisabled;
		}
		else
			base.OnGUI(position,property,label);
	}
}
#endif
#endregion
//======================================================================================

//======================================================================================
#region GRAY ATTRIBUTE
/* Note: This attribute CANNOT disable array "size" box and "+/-" button. */
public class GrayAttribute : PropertyAttribute{ }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(GrayAttribute))]
public class GrayDrawer : ForwardDrawer{
	public override void OnGUI(
		Rect position,SerializedProperty property,GUIContent label)
	{
		bool bSavedEnable = GUI.enabled;
		GUI.enabled = false;
		base.OnGUI(position,property,label);
		GUI.enabled = bSavedEnable;
	}
}
#endif
#endregion
//======================================================================================

//======================================================================================
#region MAX ATTRIBUTE
public class MaxAttribute : PropertyAttribute{
	public float maxValue;
	public MaxAttribute(float maxValue){
		this.maxValue = maxValue;
	}
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(MaxAttribute))]
class MaxDrawer : PropertyDrawer{
	public override void OnGUI(
		Rect position,SerializedProperty property,GUIContent label)
	{
		EditorGUI.PropertyField(position,property,label);
		if(fieldInfo.FieldType == typeof(int)){
			MaxAttribute attribute = fieldInfo.GetCustomAttribute<MaxAttribute>();
			property.intValue = Mathf.Min(property.intValue,(int)attribute.maxValue);
		}
		else if(fieldInfo.FieldType == typeof(float)){
			MaxAttribute attribute = fieldInfo.GetCustomAttribute<MaxAttribute>();
			property.floatValue = Mathf.Min(property.floatValue,attribute.maxValue);
		}
	}
}
#endif
#endregion
//======================================================================================

//======================================================================================
#region CLAMP ATTRIBUTE
public class ClampAttribute : PropertyAttribute{
	public float minValue,maxValue;
	public ClampAttribute(float minValue,float maxValue){
		this.minValue = minValue;
		this.maxValue = maxValue;
	}
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ClampAttribute))]
class ClampDrawer : PropertyDrawer{
	public override void OnGUI(
		Rect position,SerializedProperty property,GUIContent label)
	{
		EditorGUI.PropertyField(position,property,label);
		if(fieldInfo.FieldType == typeof(int)){
			ClampAttribute attribute = fieldInfo.GetCustomAttribute<ClampAttribute>();
			property.intValue =
				Mathf.Clamp(property.intValue,(int)attribute.minValue,(int)attribute.maxValue);
		}
		else if(fieldInfo.FieldType == typeof(float)){
			ClampAttribute attribute = fieldInfo.GetCustomAttribute<ClampAttribute>();
			property.floatValue =
				Mathf.Clamp(property.floatValue,attribute.minValue,attribute.maxValue);
		}
	}
}
#endif
#endregion
//======================================================================================

//======================================================================================
#region TAG ATTRIBUTE
public class TagAttribute : PropertyAttribute{}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(TagAttribute))]
class TagAttributeDrawer : PropertyDrawer{
	public override void OnGUI(
		Rect position,SerializedProperty property,GUIContent label)
	{
		if(fieldInfo.FieldType == typeof(string))
			property.stringValue = EditorGUI.TagField(position,label,property.stringValue);
		else
			EditorGUI.PropertyField(position,property,label);
	}
}
#endif
#endregion
//======================================================================================

//======================================================================================
#region LAYER ATTRIBUTE
public class LayerAttribute : PropertyAttribute{}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(LayerAttribute))]
class LayerAttributeDrawer : PropertyDrawer{
	public override void OnGUI(
		Rect position,SerializedProperty property,GUIContent label)
	{
		if(fieldInfo.FieldType == typeof(int)){
			property.intValue = EditorGUI.LayerField(position,label,property.intValue);}
		else{
			EditorGUI.PropertyField(position,property,label);}
	}
}
#endif
#endregion
//======================================================================================

//======================================================================================
#region WIDEMODE ATTRIBUTE
public class WideModeAttribute : PropertyAttribute{ }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(WideModeAttribute))]
class WideModeAttributeDrawer : ForwardDrawer{
	public override void OnGUI(Rect position,SerializedProperty property,GUIContent label){
		bool bSaveWideMode = EditorGUIUtility.wideMode;
		EditorGUIUtility.wideMode = true;
		base.OnGUI(position,property,label);
		EditorGUIUtility.wideMode = bSaveWideMode;
	}
}
#endif
#endregion
//======================================================================================

//======================================================================================
#region WIDEVECTOR2 ATTRIBUTE
public class WideVector2Attribute : PropertyAttribute{ }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(WideVector2Attribute))]
class WideVector2AttributeDrawer : ForwardDrawer{
	public override void OnGUI(Rect position,SerializedProperty property,GUIContent label){
		bool bPrevWideMode = EditorGUIUtility.wideMode;
		EditorGUIUtility.wideMode = true;
		if(EditorHelper.getTypeToDraw(fieldInfo)==typeof(Vector2)){
			position.width = 1.5f*(position.width-EditorGUIUtility.labelWidth)+EditorGUIUtility.labelWidth;
		}
		base.OnGUI(position,property,label);
		EditorGUIUtility.wideMode = bPrevWideMode;
	}
	public override float GetPropertyHeight(SerializedProperty property,GUIContent label){
		//return EditorGUI.GetPropertyHeight(property,label,true);
		return EditorGUIUtility.singleLineHeight;
	}
}
#endif
#endregion 
//======================================================================================

//======================================================================================
#region FIXEDSIZE ATTRIBUTE
/* Attach this to ARRAY to make it fixed size. Implementation is quite hacky, so
there might still be some bugs.
CAUTION: This attribute by itself does NOT resize the array. It is designed to work
with inspector, so only when the inspector is opened would it do the resize. */
/* It might be possible to force resize, but that needs recursive iteration of
serialized property, which means duplicating Unity's serialization logic,
which feels like overkill, so I don't do that. */
public class FixedSizeAttribute : PropertyAttribute{
	public int size;
	public string[] aName;
	public FixedSizeAttribute(int size){
		this.size = size;
	}
	public FixedSizeAttribute(params string[] aName){
		this.aName = aName;
		this.size = aName.Length;
	}
	public FixedSizeAttribute(Type typeEnum){
		this.aName = Enum.GetNames(typeEnum); //will throw if type is not Enum
		this.size = aName.Length;
	}
	public FixedSizeAttribute(Type typeEnum,int start,int end=-1){
		string[] aNameEnum = Enum.GetNames(typeEnum);
		size = end==-1 ? aNameEnum.Length-start : end-start+1;
		aName = new string[size];
		Array.Copy(aNameEnum,start,aName,0,size);
	}
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(FixedSizeAttribute))]
class FixedSizeAttributeDrawer : ForwardDrawer{
	public override void OnGUI(Rect position,SerializedProperty property,GUIContent label){
		if(fieldInfo!=null && typeof(Array).IsAssignableFrom(fieldInfo.FieldType)){ //property is Array
			int size = ((FixedSizeAttribute)attribute).size;
			SerializedProperty spArray = EditorHelper.getArraySerializedProperty(property);
			if(spArray.arraySize != size){
				spArray.arraySize = size;}
			/* This is a hack that steals Event from +/- button, because if they are clicked,
			Log will show warning, which is annoying. Since elements are drawn before +/- button,
			we create invisible button at that position, and so it receives input BEFORE +/- button
			and steals the Event. */
			int index = EditorHelper.getSerializedPropertyArrayIndex(property);
			if(index >= size){
				return;}
			if(index == size-1){
				Rect buttonPos = position;
				buttonPos.y += 23.0f;
				buttonPos.x = buttonPos.xMax-58.0f;
				buttonPos.width = 55.0f;
				GUI.Button(buttonPos," ",GUIStyle.none);
			}
			string[] aName = ((FixedSizeAttribute)attribute).aName;
			GUIContent labelNew = (aName!=null ? new GUIContent(aName[index]) : label);
			base.OnGUI(position,property,labelNew);
		}
		else{
			base.OnGUI(position,property,label);}
	}
	public override float GetPropertyHeight(SerializedProperty property,GUIContent label) {
		return EditorGUI.GetPropertyHeight(property,property.isExpanded);
	}
}
#endif
#endregion
//======================================================================================

} //end namespace Chameleon
