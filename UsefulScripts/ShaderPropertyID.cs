/**************************************************************
 * SHADERPROPERTYID (v1.0)
 * by Reev the Chameleon
 * 23 Dec 2
***************************************************************
This class is designed to avoid hardcoding shader property names
in the script. User can assign properties via the inspector,
then use Material extension method to get/set them on designated Material
by Material extension methods provided.
Usage:
1. Declare variable of type ShaderPropertyID_float, ShaderPropertyID_Color,
ShaderPropertyID_Vector4, or ShaderPropertyID_Texture in the script.
*** DO NOT declare variable as type ShaderPropertyID directly. ***
2. Assign Shader Property via the inspector. The inspector should
show a slot for a shader asset and a dropdown for available properties
of corresponding type found in it.
(The editor was modified so that the shader slot also accepts Material
and GameObjects containing a Renderer.)
3. Set or get a corresponding property in Material using extension methods:
material.setX or material.getX respectively.
Compiler should help prevent you from getting/setting property with wrong types.

Note: the id for shader property changes for every run of the game,
so only the name string of it can be serialized. The id is determined
on first use though (except for empty property, which you shouldn't do anyway.)
*/

using UnityEngine;
using System;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Chameleon{

/* This is an old approach. Since 2020.1, Unity CAN serialize generic class, and so
this can be implemented entirely using ShaderProperty<T>. However, since I sometimes
still work with 2019.4, I want to write an approach that can be used uniformly
across versions, and so it ends up like this (which honestly isn't much different). */
[Serializable]
public abstract class ShaderPropertyID{
	private static readonly int nullHash = Shader.PropertyToID("");
	//Shader nameID changes on each run, so can serialized string at most 
	[SerializeField] string name = "";
	public string Name{ get{return name;} }
	private int id = nullHash;
	//public int Id{get; private set;}
	public int Id{
		get{
			if(id==nullHash){
				id = Shader.PropertyToID(name);}
			return id;
		}
	}
	/* Initialize in constructor does NOT work (Unity seems to call it before assigning
	serialized values. */
	//public ShaderPropertyID(){
	//	Debug.Log("Constructor "+name);
	//	id = Shader.PropertyToID(name);
	//}
	/* Would be nice to have implicit conversion to int, but that means user
	will be able to pass this class directly to, say, Material.SetFloat when
	the property is not float, and we don't want that (we want to prevent this
	at compile time), so we do NOT provide that implicit casting. */

	#if UNITY_EDITOR
	[SerializeField] Shader shader;

	[CustomPropertyDrawer(typeof(ShaderPropertyID),true)]
	class ShaderPropertyIDDrawer : PropertyDrawer{
		public override void OnGUI(Rect position,SerializedProperty property,GUIContent label){
			SerializedProperty spName = property.FindPropertyRelative(nameof(ShaderPropertyID.name));
			SerializedProperty spShader = property.FindPropertyRelative(nameof(ShaderPropertyID.shader));
			Rect rectDrawPos = position;
			EditorGUI.LabelField(rectDrawPos,label);
			rectDrawPos.x += EditorGUIUtility.labelWidth;
			rectDrawPos.width = (rectDrawPos.width-EditorGUIUtility.labelWidth)/2.0f-3.0f;
			Material material = EditorHelper.dropZone<Material>(rectDrawPos,false)?[0];
			if(material==null){
				material = EditorHelper.dropZone<Renderer>(rectDrawPos,false)?[0].sharedMaterial;}
			if(material!=null){
				spShader.objectReferenceValue = material.shader;
				updateDisplayPropertyName(spShader,spName);
			}
			else{
				EditorGUI.BeginChangeCheck();
				EditorGUI.ObjectField(rectDrawPos,spShader,GUIContent.none);
				if(EditorGUI.EndChangeCheck()){
					updateDisplayPropertyName(spShader,spName);}

				rectDrawPos.x += rectDrawPos.width+2.0f;
				rectDrawPos.width -= 2.0f;
				if(GUI.Button(
					rectDrawPos,
					spName.stringValue=="" ? "<none>" : spName.stringValue,
					EditorStyles.popup
				)){
					GenericMenu menu = new GenericMenu();
					Shader shader = (Shader)spShader.objectReferenceValue;
					int count = shader ? shader.GetPropertyCount() : 0;
					for(int i=0; i<count; ++i){
						if(isMatchingType(shader.GetPropertyType(i),fieldInfo.FieldType)){
							string s = shader.GetPropertyName(i);
							menu.AddItem(
								new GUIContent(s),
								false,
								() => {
									spName.stringValue = s;
									spName.serializedObject.ApplyModifiedProperties();
								}
							);
						}
					}
					if(count>0){
						menu.AddSeparator("");}
					menu.AddItem(
						new GUIContent("Clear"),
						false,
						() => {
							spName.stringValue = "";
							spName.serializedObject.ApplyModifiedProperties();
						}
					);
					menu.DropDown(rectDrawPos);
				}
			}
		}
		private bool isMatchingType(ShaderPropertyType shaderPropertyType,Type fieldType){
			switch(shaderPropertyType){
				case ShaderPropertyType.Float:
					return fieldType == typeof(ShaderPropertyID_float);
				case ShaderPropertyType.Color:
					return fieldType == typeof(ShaderPropertyID_Color);
				case ShaderPropertyType.Vector:
					return fieldType == typeof(ShaderPropertyID_Vector4);
				case ShaderPropertyType.Texture:
					return fieldType == typeof(ShaderPropertyID_Texture);
			}
			return false;
		}
		private void updateDisplayPropertyName(SerializedProperty spShader,SerializedProperty spName){
			Shader shader = (Shader)spShader.objectReferenceValue;
			int index = shader ? shader.FindPropertyIndex(spName.stringValue) : -1;
			if(index == -1){
				spName.stringValue = "";}
		}
	}
	#endif
}

[Serializable] public class ShaderPropertyID_float : ShaderPropertyID{ }
[Serializable] public class ShaderPropertyID_Color : ShaderPropertyID{ }
[Serializable] public class ShaderPropertyID_Vector4 : ShaderPropertyID{ }
[Serializable] public class ShaderPropertyID_Texture : ShaderPropertyID{ }

public static class ShaderProperty2Extension{
	public static float getFloat(this Material material,ShaderPropertyID_float property){
		return material.GetFloat(property.Id);
	}
	public static Color getColor(this Material material,ShaderPropertyID_Color property){
		return material.GetColor(property.Id);
	}
	public static Vector4 getVector4(this Material material,ShaderPropertyID_Vector4 property){
		return material.GetVector(property.Id);
	}
	public static Texture getTexture(this Material material,ShaderPropertyID_Texture property){
		return material.GetTexture(property.Id);
	}
	public static void setFloat(
		this Material material,ShaderPropertyID_float property,float f)
	{
		material.SetFloat(property.Id,f);
	}
	public static void setColor(
		this Material material,ShaderPropertyID_Color property,Color c)
	{
		material.SetColor(property.Id,c);
	}
	public static void setVector4(
		this Material material,ShaderPropertyID_Vector4 property,Vector4 v)
	{
		material.SetVector(property.Id,v);
	}
	public static void setTexture(
		this Material material,ShaderPropertyID_Texture property,Texture t)
	{
		material.SetTexture(property.Id,t);
	}
}

} //end namespace Chameleon
