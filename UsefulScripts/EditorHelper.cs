/**************************************************************
 * EDITORHELPER (v3.6)
 * by Reev the Chameleon
 * 23 Feb 3
***************************************************************
Collection of functions and consts that are useful for editor in general
Update v1.1: Add functions for converting Vector3 from/to clipboard
Update v1.2: Add function to get toolbar height and Unity window names
Update v2.0: Add FloatBuffer class for dynamically receiving float as user types,
add GUIStyleCollection class, and some miscellaneous functions
Update v2.1: Add drawField method
Update v2.2: Add function to help create ReorderableList
Update v2.3: Add support for Vector2, Vector3Int, and Vector2Int types
Update v2.4: Add support for drawing Color type in drawField method
Update v2.5: Add support for drawing delayed fields, Gradient and AnimationCurve types
Update v3.0: Add function to create copy/paste context menu at rect and revamp clipboard codes
Update v3.1: Add support for copying many common types to/from clipboard
Update v3.2: Add support for drawing Texture2D fields and function to create drop zone
Update v3.2.1: Make created ReorderableList works with List and fix copy-paste context menu
Update v3.2.2: Fix NullReferenceException bug with ReorderableList dBeforeListChange delegate
Update v3.3: Revamp context menu related code, add copy/paste support for Quaternion and AnimationCurve
Update v3.4: Add code in preparation to support drawing fields with attributes
Update v3.4.1: Fix DropZone code so it also takes GameObjects with corresponding Component
Update v3.5: Add support for drawing [Layer], and add ForwardDrawer class
Update v3.6: Add multitypeDropZone and multitypeObjectField, and add Editor<T> class
*/

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using UnityEditorInternal;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;

using Object = UnityEngine.Object;

namespace Chameleon{

public static class EditorHelper{
	public static Vector3 roundFifthDecimal(this Vector3 v){
		/* Because by default, inspector will show scientific notation if
		value is less than 0.0001. The "e" part may overflow the field and
		resulting in value very close to zero value showing up as large float */
		return new Vector3(
			Mathf.Round(v.x*10000)/10000.0f, //Credit: Mike3, UA
			Mathf.Round(v.y*10000)/10000.0f,
			Mathf.Round(v.z*10000)/10000.0f
		);
	}
	/* GUI functions can ONLY be called from OnGUI(). Call this in OnGUI() of
	relavant window. */
	public static float toolbarHeight(this EditorWindow window){
		return GUIUtility.GUIToScreenPoint(Vector2.zero).y-window.position.y;
	} //Note: Extension property not supported, otherwise would be attractive.
	public static Transform ActiveTransformTopSelected(){
		/* return top level Transform in selection corresponding to currently
		active Transform */
		Transform topSelected = Selection.activeTransform;
		while(topSelected != null){
			foreach(Transform t in Selection.transforms){
				if(t == topSelected)
					return topSelected;
			}
			topSelected = topSelected.parent;
		}
		return null;
	}
	public static List<Attribute> getDrawnAttribute(MemberInfo memberInfo){
		List<Attribute> lAttribute = new List<Attribute>();
		Attribute a;
		if((a=memberInfo.GetCustomAttribute<TagAttribute>()) != null)
			lAttribute.Add(a);
		if((a=memberInfo.GetCustomAttribute<LayerAttribute>()) != null)
			lAttribute.Add(a);
		return lAttribute;
	}
	public static bool drawField(
		string fieldName,object value,out object outValue,bool bDelayed=false,Type type=null,
		List<Attribute> lDrawnAttribute=null)
	{
		/* return false if output value is different than input */
		/* bDelayed is only applicable with int, float, and string fields */
		/* While it would be a very good approach to pass the call to EditorGUI version
		(see how EditorGUILayout does this in Unity reference code), it is not possible because
		"style" argument is different between each type.
		Hence, unfortunately, we have to deal with this code duplication. */
		/* In previous versions, this function only support struct type, so value?.GetType()
		is never null. However, once we support class type, we need type argument so it is
		implemented as optional parameter. */
		/*** IMPORTANT: Due to how Unity MODIFIES original value for EditorGUILayout functions
		of class types, outValue is ORIGINAL value for those type. I would be happy to revise
		codes and make this clearer, but I discover this fact too late in the development
		that it will affect all the codes in the algorithm, so I leave it like this for now ***/
		if(type==null)
			type = value?.GetType();
		switch(type.Name){
			case nameof(Int32):
				int intValue = value as int? ?? 0;
				bool bLayer = false;
				for(int i=0; i<lDrawnAttribute.Count; ++i){
					if(lDrawnAttribute[i] is LayerAttribute){
						bLayer = true;
						break;
					}
				}
				int intUserValue =
					bLayer ?
					EditorGUILayout.LayerField(fieldName,intValue) :
					bDelayed ?
						EditorGUILayout.DelayedIntField(fieldName,intValue) :
						EditorGUILayout.IntField(fieldName,intValue)
				;
				outValue = intUserValue;
				return intUserValue==intValue;
			case nameof(Single):
				float floatValue = value as float? ?? 0.0f;
				float floatUserValue =
					bDelayed ?
					EditorGUILayout.DelayedFloatField(fieldName,floatValue) :
					EditorGUILayout.FloatField(fieldName,floatValue)
				;
				outValue = floatUserValue;
				return floatUserValue==floatValue;
			case nameof(String):
				string stringValue = value as string ?? "";
				bool bTag = false;
				for(int i=0; i<lDrawnAttribute.Count; ++i){
					if(lDrawnAttribute[i] is TagAttribute){
						bTag = true;
						break;
					}
				}
				string stringUserValue = 
					bTag ?
					EditorGUILayout.TagField(fieldName,stringValue) :
					bDelayed ?
						EditorGUILayout.DelayedTextField(fieldName,stringValue) :
						EditorGUILayout.TextField(fieldName,stringValue)
				;
				outValue = stringUserValue;
				return stringUserValue==stringValue;
			case nameof(Boolean):
				bool boolValue = value as bool? ?? false;
				bool boolUserValue = EditorGUILayout.Toggle(fieldName,boolValue);
				outValue = boolUserValue;
				return boolUserValue==boolValue;
			case nameof(Vector3):
				Vector3 vector3Value = value as Vector3? ?? Vector3.zero;
				Vector3 vector3UserValue = EditorGUILayout.Vector3Field(fieldName,vector3Value);
				outValue = vector3UserValue;
				return vector3UserValue==vector3Value;
			case nameof(Vector2): //Will fix width later (need to dig into documentation)
				Vector2 vector2Value = value as Vector2? ?? Vector2.zero;
				Vector2 vector2UserValue = EditorGUILayout.Vector2Field(fieldName,vector2Value);
				outValue = vector2UserValue;
				return vector2UserValue==vector2Value;
			case nameof(Vector3Int):
				Vector3Int vector3IntValue = value as Vector3Int? ?? Vector3Int.zero;
				Vector3Int vector3IntUserValue = EditorGUILayout.Vector3IntField(fieldName,vector3IntValue);
				outValue = vector3IntUserValue;
				return vector3IntUserValue==vector3IntValue;
			case nameof(Vector2Int):
				Vector2Int vector2IntValue = value as Vector2Int? ?? Vector2Int.zero;
				Vector2Int vector2IntUserValue = EditorGUILayout.Vector2IntField(fieldName,vector2IntValue);
				outValue = vector2IntUserValue;
				return vector2IntUserValue==vector2IntValue;
			case nameof(Color):
				Color colorValue = value as Color? ?? Color.black;
				Color colorUserValue = EditorGUILayout.ColorField(fieldName,colorValue);
				outValue = colorUserValue;
				return colorUserValue==colorValue;
			case nameof(Vector4):
				Vector4 vector4Value = value as Vector4? ?? Vector4.zero;
				Vector4 vector4UserValue = EditorGUILayout.Vector4Field(fieldName,vector4Value);
				outValue = vector4UserValue;
				return vector4UserValue==vector4Value;
			case nameof(Bounds):
				Bounds boundValue = value as Bounds? ?? new Bounds();
				Bounds boundUserValue = EditorGUILayout.BoundsField(fieldName,boundValue);
				outValue = boundUserValue;
				return boundUserValue==boundValue;

			case nameof(Gradient):
				Gradient gradientValue = value as Gradient ?? new Gradient();
				EditorGUI.BeginChangeCheck();
				outValue = EditorGUILayout.GradientField(fieldName,gradientValue);
				return !EditorGUI.EndChangeCheck();
			case nameof(AnimationCurve):
				AnimationCurve curveValue = value as AnimationCurve ?? new AnimationCurve();
				EditorGUI.BeginChangeCheck();
				outValue = EditorGUILayout.CurveField(fieldName,curveValue);
				return !EditorGUI.EndChangeCheck();
			case nameof(Texture2D):
				Object tex2dValue = value as Object ?? null;
				/* Specifying this height or less prevents Unity from drawing ObjectField
				as texture preview field (Credit: PowerhoofDave, UA)*/
				Rect rect = GUILayoutUtility.GetLastRect();
				EditorGUI.BeginChangeCheck();
				outValue = EditorGUI.ObjectField(rect,fieldName,tex2dValue,type,true);
				return !EditorGUI.EndChangeCheck();
			default:
				outValue = null;
				return true;
		}
	}
	/* This version is useful for ReorderableList */
	/* bDelayed is only applicable with int, float, and string fields */
	public static bool drawField(Rect rect,string fieldName,object value,out object outValue,
		bool bDelayed=false,Type type=null)
	{
		/* return false if output value is different than input */
		if(type==null)
			type = value?.GetType();
		switch(type.Name){
			case nameof(Int32):
				int intValue = value as int? ?? 0;
				int intUserValue =
					bDelayed ?
					EditorGUI.DelayedIntField(rect,fieldName,intValue) :
					EditorGUI.IntField(rect,fieldName,intValue)
				;
				outValue = intUserValue;
				return intUserValue==intValue;
			case nameof(Single):
				float floatValue = value as float? ?? 0.0f;
				float floatUserValue =
					bDelayed ?
					EditorGUI.DelayedFloatField(rect,fieldName,floatValue) :
					EditorGUI.FloatField(rect,fieldName,floatValue)
				;
				outValue = floatUserValue;
				return floatUserValue==floatValue;
			case nameof(String):
				string stringValue = value as string ?? "";
				string stringUserValue =
					bDelayed ?
					EditorGUI.DelayedTextField(rect,fieldName,stringValue) :
					EditorGUI.TextField(rect,fieldName,stringValue)
				;
				outValue = stringUserValue;
				return stringUserValue==stringValue;
			case nameof(Boolean):
				bool boolValue = value as bool? ?? false;
				bool boolUserValue = EditorGUI.Toggle(rect,fieldName,boolValue);
				outValue = boolUserValue;
				return boolUserValue==boolValue;
			case nameof(Vector3):
				Vector3 vector3Value = value as Vector3? ?? Vector3.zero;
				Vector3 vector3UserValue = EditorGUI.Vector3Field(rect,fieldName,vector3Value);
				outValue = vector3UserValue;
				return vector3UserValue==vector3Value;
			case nameof(Vector2):
				Vector2 vector2Value = value as Vector2? ?? Vector2.zero;
				Vector2 vector2UserValue = EditorGUI.Vector2Field(rect,fieldName,vector2Value);
				outValue = vector2UserValue;
				return vector2UserValue==vector2Value;
			case nameof(Vector3Int):
				Vector3Int vector3IntValue = value as Vector3Int? ?? Vector3Int.zero;
				Vector3Int vector3IntUserValue = EditorGUI.Vector3IntField(rect,fieldName,vector3IntValue);
				outValue = vector3IntUserValue;
				return vector3IntUserValue==vector3IntValue;
			case nameof(Vector2Int):
				Vector2Int vector2IntValue = value as Vector2Int? ?? Vector2Int.zero;
				Rect newRect = rect;
				newRect.width = (newRect.width-EditorGUIUtility.labelWidth)*1.5f + EditorGUIUtility.labelWidth;
				Vector2Int vector2IntUserValue = EditorGUI.Vector2IntField(newRect,fieldName,vector2IntValue);
				outValue = vector2IntUserValue;
				return vector2IntUserValue==vector2IntValue;
			case nameof(Color):
				Color colorValue = value as Color? ?? Color.black;
				Color colorUserValue = EditorGUILayout.ColorField(fieldName,colorValue);
				outValue = colorUserValue;
				return colorUserValue==colorValue;
			case nameof(Vector4):
				Vector4 vector4Value = value as Vector4? ?? Vector4.zero;
				Vector4 vector4UserValue = EditorGUI.Vector4Field(rect,fieldName,vector4Value);
				outValue = vector4UserValue;
				return vector4UserValue==vector4Value;
			case nameof(Bounds):
				Bounds boundValue = value as Bounds? ?? new Bounds();
				Bounds boundUserValue = EditorGUI.BoundsField(rect,fieldName,boundValue);
				outValue = boundUserValue;
				return boundUserValue==boundValue;
			
			case nameof(Gradient):
				Gradient gradientValue = value as Gradient ?? new Gradient();
				EditorGUI.BeginChangeCheck();
				outValue = EditorGUI.GradientField(rect,fieldName,gradientValue);
				return !EditorGUI.EndChangeCheck();
			case nameof(AnimationCurve):
				AnimationCurve curveValue = value as AnimationCurve ?? new AnimationCurve();
				EditorGUI.BeginChangeCheck();
				outValue = EditorGUI.CurveField(rect,fieldName,curveValue);
				return !EditorGUI.EndChangeCheck();
			case nameof(Texture2D):
				Object tex2dValue = value as Object ?? null;
				/* Specifying this height or less prevents Unity from drawing ObjectField
				as texture preview field (Credit: PowerhoofDave, UA)*/
				rect.height = 16.0f;
				EditorGUI.BeginChangeCheck();
				outValue = EditorGUI.ObjectField(rect,fieldName,tex2dValue,type,true);
				return !EditorGUI.EndChangeCheck();
			default:
				outValue = null;
				return true;
		}
	}
	/* ReorderableList is an undocumented class in UnityEditorInternal. Knowledge Credit:
	https://xinyustudio.wordpress.com/2015/07/21/unity3d-using-reorderablelist-in-custom-editor/,
	https://blog.terresquall.com/2020/03/creating-reorderable-lists-in-the-unity-inspector/,
	https://stackoverflow.com/questions/56180821/how-can-i-use-reorderablelist-with-a-list-in-the-inspector-and-adding-new-empty */
	/* Note: Due to technical complication (cannot pass ref array as IList and need reallocation),
	you should ALWAYS confirm synchronization if your list is array.
	!!! There is still problem in Undoing ReorderableList. Will try to fix that later. !!!
	I did not implement Undo because the list may not be serialized, which Unity's Undo will
	not work. Also, it is quite difficult to do without working with real class.
	You need to record object prior to change (via dBeforeListchange) AND make sure
	r.list and your list is in sync when Undo (probably via Undo.undoRedoPerformed
	or OnValidate) */
	/* listType parameter is REQUIRED because if ilist is null there is no way to know
	what it is. */
	public static ReorderableList createReorderableList(
		IList ilist,Type listType,Action dBeforeListChange=null)
	{
		/* Uses the concept of closure with lambda to avoid having to create new class
		(due to unfamiliarity, looking out for bugs) */
		Type elementType = ReflectionHelper.getElementType(listType);
		if(ilist == null)
			ilist = Activator.CreateInstance(listType,0) as IList; //Credit: Kumar, SO
		int indexActive = ilist.Count-1;
		ReorderableList r = new ReorderableList(ilist,elementType,true,true,true,true);
		r.elementHeight = EditorGUIUtility.singleLineHeight; //Credit: Utamaru, SO
		r.drawElementCallback = (Rect rect,int index,bool bActive,bool bFocus) => {
			if(r.list.Count == 0)
				return;
			if(bActive)
				indexActive = index;
			else if(indexActive==index) //but not active
				indexActive = r.list.Count-1;
			object outValue;
			if(!drawField(rect,"Element "+index,r.list[index],out outValue,false,elementType)){
				dBeforeListChange?.Invoke();
				r.list[index] = outValue;
			}
			copyPasteContextMenuArrayItem(rect,r.list,index);
		};
		r.drawHeaderCallback = (Rect rect) => {
			float prevLabelWidth = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 50.0f;
			rect.x = rect.width - 80f;
			rect.width = 100f;
			EditorGUI.BeginChangeCheck();
			int userSize = EditorGUI.DelayedIntField(rect,"Size",r.list.Count);
			if(EditorGUI.EndChangeCheck()){
				dBeforeListChange?.Invoke();
				/* You can pass size in Activator.CreateInstance too
				(Credit: nielsvanvliet, social.msdn.microsoft.com) */
				IList ilistNew;
				if(r.list.IsFixedSize){
					ilistNew =
						Activator.CreateInstance(listType,Mathf.Max(userSize,0)) as IList;
					for(int i=0; i<Mathf.Min(r.list.Count,userSize); ++i)
						ilistNew[i] = r.list[i];
				}
				else{
					ilistNew = Activator.CreateInstance(listType) as IList;
					int i=0;
					for(; i<Mathf.Min(r.list.Count,userSize); ++i)
						ilistNew.Add(r.list[i]);
					object defaultObject =
						elementType.IsValueType ? Activator.CreateInstance(elementType) : null;
					for(; i<userSize; ++i)
						ilistNew.Add(defaultObject);
				}
				r.list = ilistNew;
				indexActive = ilistNew.Count-1;
			}
			EditorGUIUtility.labelWidth = prevLabelWidth;
		};
		r.onAddCallback = (ReorderableList rlist) => {
			dBeforeListChange?.Invoke();
			if(rlist.list.IsFixedSize){
				int size = rlist.count;
				IList ilistNew =
					Activator.CreateInstance(listType,size+1) as IList;
				for(int i=0; i<size+1; ++i){
					if(i<=indexActive)
						ilistNew[i] = rlist.list[i];
					else if(i==indexActive+1)
						ilistNew[i] = default;
					else
						ilistNew[i] = rlist.list[i-1];
				}
				rlist.list = ilistNew;
			}
			else //not fixed size
				rlist.list.Insert(
					indexActive+1,
					elementType.IsValueType ? Activator.CreateInstance(elementType) : null
					//Credit: Dror Helper & Neville Nazerane, SO
				);
			indexActive = rlist.list.Count-1;
		};
		r.onRemoveCallback = (ReorderableList rlist) => {
			dBeforeListChange?.Invoke();
			if(rlist.list.IsFixedSize){
				int size = rlist.count;
				IList ilistNew =
					Activator.CreateInstance(listType,Mathf.Max(0,size-1)) as IList;
				for(int i=0; i<size-1; ++i){
					if(i<indexActive)
						ilistNew[i] = rlist.list[i];
					else
						ilistNew[i] = rlist.list[i+1];
				}
				rlist.list = ilistNew;
			}
			else
				rlist.list.RemoveAt(indexActive);
			indexActive = rlist.list.Count-1;
		};
		return r;
	}
	/* When rect is right-click, display functional copy/paste context menu
	if type is supported. */
	//Example usage:
	//if(EditorHelper.contextClicked(rect))
	//	EditorHelper.copyPasteContextMenu(rect,targetAs,nameof(targetAs.g)).ShowAsContext();
	public static GenericMenu createCopyPasteContextMenu(
		Object target,string fieldName,bool bPrivateUndo=false)
	{
		/* The reasons that I have to resort to reflection are pasting problem. 
		Attempt to assign new object to passed in object won't work unless ref is used,
		but due to limitation in genericMenu.AddItem function you cannot use ref there.
		Also the fact that it returns void means you can't get anything out of it. */
		FieldInfo fieldInfo =
			target?.GetType().GetField(fieldName,ReflectionHelper.BINDINGFLAGS_ALL);
		if(fieldInfo != null){
			Type type = fieldInfo.FieldType;
			if(!isClipboardSupportedType(type))
				return null;
			GenericMenu contextMenu = new GenericMenu();
			contextMenu.AddItem(
				new GUIContent("Copy (&C)"),
				false,
				()=>{EditorGUIUtility.systemCopyBuffer =
					toClipboardString(fieldInfo.GetValue(target));}
			);
			string sBuffer = EditorGUIUtility.systemCopyBuffer;
			if(sBuffer?.Length>0){ //Will revise gray-out condition later
				contextMenu.AddItem(
					new GUIContent("Paste (&V)"),
					false,
					bPrivateUndo ?
						/* Ternary operator will fail for delegate if 2 types do not match.
						Because method may be overridden, you have to specify the specific
						delegate type for it to work. */
						(GenericMenu.MenuFunction)(()=>{FieldUndo.recordSetFieldInfo(
							target,fieldInfo,fromClipboardString(sBuffer,type));
						}) :
						()=>{
							Undo.RecordObject(target,target.name);
							fieldInfo.SetValue(target,fromClipboardString(sBuffer,type));
						}
				);
			}
			else
				contextMenu.AddDisabledItem(new GUIContent("Paste (&V)"));
			return contextMenu;
		}

		PropertyInfo propertyInfo =
			target?.GetType().GetProperty(fieldName,ReflectionHelper.BINDINGFLAGS_ALL);
		if(propertyInfo != null){
			Type type = propertyInfo.PropertyType;
			if(!isClipboardSupportedType(type))
				return null;
			GenericMenu contextMenu = new GenericMenu();
			contextMenu.AddItem(
				new GUIContent("Copy (&C)"),
				false,
				()=>{EditorGUIUtility.systemCopyBuffer =
					toClipboardString(propertyInfo.GetValue(target));}
			);
			string sBuffer = EditorGUIUtility.systemCopyBuffer;
			if(sBuffer?.Length>0){ //Will revise gray-out condition later
				contextMenu.AddItem(
					new GUIContent("Paste (&V)"),
					false,
					bPrivateUndo ?
						(GenericMenu.MenuFunction)(()=>{FieldUndo.recordSetPropertyInfo(
							target,propertyInfo,fromClipboardString(sBuffer,type));
						}) :
						()=>{
							Undo.RecordObject(target,target.name);
							propertyInfo.SetValue(target,fromClipboardString(sBuffer,type));
						}
				);
			}
			else
				contextMenu.AddDisabledItem(new GUIContent("Paste (&V)"));
			return contextMenu;
		}
		return null;
	}
	public static void copyPasteContextMenuArrayItem(Rect rect,IList iList,int index){
		/* Because array is reference type, you can set its value directly
		without need to use SetValue (Credit: Marc Gravell, SO) */
		Event currentEvent = Event.current;
		if(currentEvent.type==EventType.ContextClick && 
			rect.Contains(currentEvent.mousePosition))
		{
			if(iList==null)
				return;
			Type elementType = ReflectionHelper.getElementType(iList.GetType());
			if(!isClipboardSupportedType(elementType))
				return;
			GenericMenu contextMenu = new GenericMenu();
			contextMenu.AddItem(
				new GUIContent("Copy (&C)"),
				false,
				()=>{EditorGUIUtility.systemCopyBuffer =
					toClipboardString(iList[index]);}
			);
			string sBuffer = EditorGUIUtility.systemCopyBuffer;
			if(sBuffer?.Length>0){ //Will revise gray-out condition later
				contextMenu.AddItem(
					new GUIContent("Paste (&V)"),
					false,
					()=>{iList[index] = fromClipboardString(sBuffer,elementType);}
				);
			}
			else
				contextMenu.AddDisabledItem(new GUIContent("Paste (&V)"));
			contextMenu.ShowAsContext();
			currentEvent.Use();
		}
	}
	public static bool isClipboardSupportedType(Type type){
		switch(type?.Name){
			case nameof(Int32):
			case nameof(Single):
			case nameof(Boolean):
			case nameof(String):
			case nameof(Vector3):
			case nameof(Vector2):
			case nameof(Vector3Int):
			case nameof(Vector2Int):
			case nameof(Color):
			case nameof(Bounds):
			case nameof(Quaternion):
			case nameof(Gradient):
			case nameof(AnimationCurve):
				return true;
		}
		return false;
	}
	public static string toClipboardString(object oValue,Type type=null){
		if(type == null)
			type = oValue?.GetType();
		if(!isClipboardSupportedType(type))
			return null;
		switch(type.Name){
		//------------------------------------------------------------------------------
			#region STRUCT TYPES
			case nameof(Vector3):
				Vector3 v = (Vector3)oValue;
				return "Vector3("+v.x+","+v.y+","+v.z+")";
			case nameof(Vector2):
				Vector2 v2 = (Vector2)oValue;
				return "Vector2("+v2.x+","+v2.y+")";
			case nameof(Vector3Int):
				Vector3Int v3Int = (Vector3Int)oValue;
				return "Vector3("+v3Int+")";
			case nameof(Vector2Int):
				Vector2Int v2Int = (Vector2Int)oValue;
				return "Vector2("+v2Int+")";
			case nameof(Color):
				Color color = (Color)oValue;
				return "#" +
					((int)(255*color.r)).ToString("X2") + //Credit: Matt & Gavin Miller, SO
					((int)(255*color.g)).ToString("X2") +
					((int)(255*color.b)).ToString("X2") +
					((int)(255*color.a)).ToString("X2")
				;
			case nameof(Bounds):
				Bounds bound = (Bounds)oValue;
				return "Bounds("+bound.center.x+","+bound.center.y+","+bound.center.z+"," +
					bound.extents.x+","+bound.extents.y+","+bound.extents.z+")";
			case nameof(Quaternion):
				Quaternion q = (Quaternion)oValue;
				return "Quaternion("+q.x+","+q.y+","+q.z+","+q.w+")";
			#endregion
		//------------------------------------------------------------------------------
			#region JSON TYPES
			case nameof(Gradient):
				return "UnityEditor.GradientWrapperJSON:{\"gradient\""
					+ JsonObject.jsonFromObject<Gradient>(oValue,false).Substring("{\"obj\"".Length);
			case nameof(AnimationCurve):
				return "UnityEditor.AnimationCurveWrapperJSON:{\"curve\""
					+ JsonObject.jsonFromObject<AnimationCurve>(oValue,false).Substring("{\"obj\"".Length);
			#endregion
		//------------------------------------------------------------------------------			
			default:
				return oValue.ToString(); //buit-in types
		}
	}
	public static object fromClipboardString(string sValue,Type type){
		if(sValue==null || !isClipboardSupportedType(type))
			return null;
		switch(type.Name){
		//------------------------------------------------------------------------------
			#region VALUE TYPES
			case nameof(Int32):
				int intValue;
				if(int.TryParse(sValue,out intValue))
					return intValue;
				return null;

			case nameof(Single):
				float floatValue;
				if(float.TryParse(sValue,out floatValue))
					return floatValue;
				return null;

			case nameof(Boolean):
				bool boolValue;
				if(bool.TryParse(sValue,out boolValue))
					return boolValue;
				return null;

			case nameof(String):
				return sValue;

			case nameof(Vector3):
				if(sValue.Length>11 && sValue.Substring(0,8)=="Vector3(" &&
					sValue[sValue.Length-1]==')')
				{
					Vector3 v = new Vector3();
					sValue = sValue.Substring(8,sValue.Length-9);
					string[] aComponent = sValue.Split(',');
					if(aComponent.Length == 3){
						if(float.TryParse(aComponent[0],out v.x) &&
							float.TryParse(aComponent[1],out v.y) &&
							float.TryParse(aComponent[2],out v.z)
						)
							return v;
					}
				}
				return null;

			case nameof(Vector2):
				if(sValue.Length>11 && sValue.Substring(0,8)=="Vector2(" &&
					sValue[sValue.Length-1]==')')
				{
					Vector2 v2 = new Vector2();
					sValue = sValue.Substring(8,sValue.Length-9);
					string[] aComponent = sValue.Split(',');
					if(aComponent.Length == 2){
						if(float.TryParse(aComponent[0],out v2.x) &&
							float.TryParse(aComponent[1],out v2.y)
						)
							return v2;
					}
				}
				return null;

			case nameof(Vector3Int):
				if(sValue.Length>11 && sValue.Substring(0,8)=="Vector3(" &&
					sValue[sValue.Length-1]==')')
				{
					int x,y,z;
					sValue = sValue.Substring(8,sValue.Length-9);
					string[] aComponent = sValue.Split(',');
					if(aComponent.Length == 3){
						if(int.TryParse(aComponent[0],out x) &&
							int.TryParse(aComponent[1],out y) &&
							int.TryParse(aComponent[2],out z)
						)
							return new Vector3Int(x,y,z);
					}
				}
				return null;

			case nameof(Vector2Int):
				if(sValue.Length>11 && sValue.Substring(0,8)=="Vector2(" &&
					sValue[sValue.Length-1]==')')
				{
					int x,y;
					sValue = sValue.Substring(8,sValue.Length-9);
					string[] aComponent = sValue.Split(',');
					if(aComponent.Length == 2){
						if(int.TryParse(aComponent[0],out x) &&
							int.TryParse(aComponent[1],out y)
						)
							return new Vector2Int(x,y);
					}
				}
				return null;

			case nameof(Color):
				if(sValue.Length==9 && sValue[0]=='#'){
					uint r,g,b,a;
					//Credit: MSDN docs
					if(uint.TryParse(sValue.Substring(1,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out r) &&
						uint.TryParse(sValue.Substring(3,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out g) &&
						uint.TryParse(sValue.Substring(5,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out b) &&
						uint.TryParse(sValue.Substring(7,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out a)
					){
						return new Color((float)r/255,(float)g/255,(float)b/255,(float)a/255);
					}
				}
				return null;

			case nameof(Bounds):
				if(sValue.Length>13 && sValue.StartsWith("Bounds(") &&
					sValue[sValue.Length-1]==')')
				{
					sValue = sValue.Substring(7,sValue.Length-8);
					string[] aComponent = sValue.Split(',');
					if(aComponent.Length == 6){
						Vector3 vCenter,vExtents;
						if(float.TryParse(aComponent[0],out vCenter.x) &&
							float.TryParse(aComponent[1],out vCenter.y) &&
							float.TryParse(aComponent[2],out vCenter.z) &&
							float.TryParse(aComponent[3],out vExtents.x) &&
							float.TryParse(aComponent[4],out vExtents.y) &&
							float.TryParse(aComponent[5],out vExtents.z)
						)
							return new Bounds(vCenter,vExtents*2); //Extents is half of size
					}
				}
				return null;
			
			case nameof(Quaternion):
				if(sValue.Length>14 && sValue.Substring(0,11)=="Quaternion(" &&
					sValue[sValue.Length-1]==')')
				{
					Quaternion q = new Quaternion();
					sValue = sValue.Substring(11,sValue.Length-12);
					string[] aComponent = sValue.Split(',');
					if(aComponent.Length == 4){
						if(float.TryParse(aComponent[0],out q.x) &&
							float.TryParse(aComponent[1],out q.y) &&
							float.TryParse(aComponent[2],out q.z) &&
							float.TryParse(aComponent[3],out q.w)
						)
							return q;
					}
				}
				return null;

			#endregion
		//------------------------------------------------------------------------------
			#region JSON TYPES
			case nameof(Gradient):
				return JsonObject.objectFromJson<Gradient>(
					"{\"obj\"" + sValue.Substring("UnityEditor.GradientWrapperJSON:{\"gradient\"".Length),
					false
				);
			case nameof(AnimationCurve):
				return JsonObject.objectFromJson<AnimationCurve>(
					"{\"obj\"" + sValue.Substring("UnityEditor.AnimationCurveWrapperJSON:{\"curve\"".Length),
					false
				);
			#endregion
		//------------------------------------------------------------------------------
			default:
				return null;
		}
	}
	public static bool tryParseClipboard<T>(string sValue,out T value){
		object o = fromClipboardString(sValue,typeof(T));
		if(o==null){
			value = default(T);
			return false;
		}
		value = (T)o;
		return true;
	}
	/* This function has to be called within OnGUI() or OnInspectorGUI()! */
	/* pass bUseTypeUnmatchEvent=false to let event fall through if type is unmatched.
	Useful for chaining dropZones or stacking it on top of an ObjectField. */
	public static T[] dropZone<T>(Rect rect,bool bUseTypeUnmatchEvent=true) where T:Object{
		Event evt = Event.current;
		EventType eventType = evt.type;
		if(eventType==EventType.DragPerform || eventType==EventType.DragUpdated){
			if(rect.Contains(evt.mousePosition)){
				for(int i=0; i<DragAndDrop.objectReferences.Length; ++i){
					if(!typeof(T).IsAssignableFrom(DragAndDrop.objectReferences[i].GetType())){
						//might be GameObject from scene
						/* For whatever reason, it seems you CAN'T assign to the objectReferences array,
						so we will copy that out if user release drag in the code down below. */
						if(!(DragAndDrop.objectReferences[i] as GameObject)?.GetComponent<T>()){
							DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
							if(bUseTypeUnmatchEvent){
								Event.current.Use();}
							return null;
						}
					}
				}
				//Below, the type is correct
				Event.current.Use();
				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
				if(eventType==EventType.DragPerform){ //This happens rarely, so copy array is OK
					DragAndDrop.AcceptDrag();
					T[] aT = new T[DragAndDrop.objectReferences.Length];
					for(int i=0; i<DragAndDrop.objectReferences.Length; ++i){
						aT[i] = 
							(DragAndDrop.objectReferences[i] as GameObject)?.GetComponent<T>() ??
							(T)DragAndDrop.objectReferences[i]
						;
					}
					return aT;
					/* can't cast Parent[] to Derived[] (Credit: Dirk, SO),
					so best return Object[] and let users manipulate it themselves */
				}
			}
		}
		return null;
	}
	public static Object[] multitypeDropZone(
		Rect rect,Type[] aType,bool bUseTypeUnmatchEvent=true)
	{
		Event evt = Event.current;
		EventType eventType = evt.type;
		if(eventType==EventType.DragPerform || eventType==EventType.DragUpdated){
			if(rect.Contains(evt.mousePosition)){
				bool bValid = false;
				for(int i=0; i<DragAndDrop.objectReferences.Length; ++i){
					for(int j=0; j<aType.Length; ++j){
						if(!aType[j].IsAssignableFrom(DragAndDrop.objectReferences[i].GetType())){
							//might be GameObject from scene
							/* For whatever reason, it seems you CAN'T assign to the objectReferences array,
							so we will copy that out if user release drag in the code down below. */
							if(!(DragAndDrop.objectReferences[i] as GameObject)?.GetComponent(aType[j])){
								continue;}
						}
						bValid = true;
					}
				}
				if(!bValid){
					DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
					if(bUseTypeUnmatchEvent){
						Event.current.Use();}
					return null;
				}
				//Below, the type is correct
				Event.current.Use();
				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
				if(eventType==EventType.DragPerform){ //This happens rarely, so copy array is OK
					DragAndDrop.AcceptDrag();
					Object[] aObject = new Object[DragAndDrop.objectReferences.Length];
					for(int i=0; i<DragAndDrop.objectReferences.Length; ++i){
						GameObject g = DragAndDrop.objectReferences[i] as GameObject;
						if(g != null){
							for(int j=0; j<aType.Length; ++j){
								Object uo = g.GetComponent(aType[j]);
								if(uo != null){
									aObject[i] = uo;
									break;
								}
							}
						}
						else{
							aObject[i] = DragAndDrop.objectReferences[i];}
					}
					return aObject;
					/* can't cast Parent[] to Derived[] (Credit: Dirk, SO),
					so best return Object[] and let users manipulate it themselves */
				}
			}
		}
		return null;
	}
	/* If array, fieldInfo of PropertyDrawer is that of an array, not the type to draw.
	This functions return real type that PropertyDrawer is about to draw. */
	public static Type getTypeToDraw(FieldInfo fieldInfo){
		return typeof(IList).IsAssignableFrom(fieldInfo.FieldType) ?
			fieldInfo.FieldType.GetElementType() : //property is array
			fieldInfo.FieldType
		;
	}
	/* Use this function in custom PropertyDrawer to get target
	Returns object because property being drawn may be non-class type. */
	public static object getSerializedPropertyTarget(
		FieldInfo fieldInfo,SerializedProperty property)
	{
		if(typeof(IList).IsAssignableFrom(fieldInfo.FieldType)){ //property is array
			int index = getSerializedPropertyArrayIndex(property);
			return ((IList)fieldInfo.GetValue(property.serializedObject.targetObject))[index];
		}
		else
			return fieldInfo.GetValue(property.serializedObject.targetObject);
	}
	public static int getSerializedPropertyArrayIndex(SerializedProperty property){
		/* When drawing array, property.propertyPath will be in the form of
		"fieldName.Array.Data[i]" where i is currently drawing index
		(Credit: sketchyventures.com)
		Update: For nested array, it would be something like
		"fieldName1.Array.Data[i].fieldName2.Array.Data[j]", so we need LastIndexOf. */
		string propertyPath = property.propertyPath;
		int indexSubstringStart = propertyPath.LastIndexOf('[')+1;
		if(indexSubstringStart <= 0){
			return -1;}
		int indexSubstringEnd = propertyPath.LastIndexOf(']');
		return Int32.Parse(
			propertyPath.Substring(indexSubstringStart,indexSubstringEnd-indexSubstringStart));
	}
	public static SerializedProperty getArraySerializedProperty(SerializedProperty spElement){
		string propertyPath = spElement.propertyPath;
		int indexOpenBracket = propertyPath.LastIndexOf('[');
		if(indexOpenBracket == -1){
			return null;}
		int indexDot = propertyPath.IndexOf('.');
		string[] aSplit = propertyPath.Split('.');
		SerializedProperty sp = spElement.serializedObject.FindProperty(aSplit[0]);
		for(int i=1; i<aSplit.Length; ++i){
			if(aSplit[i]=="Array"){
				continue;}
			if(aSplit[i].StartsWith("data[")){
				if(i==aSplit.Length-1){
					return sp;}
				int index = Int32.Parse(
					aSplit[i].Substring(5,aSplit[i].IndexOf(']')-5));
				sp = sp.GetArrayElementAtIndex(index);
			}
			else{
				sp = sp.FindPropertyRelative(aSplit[i]);}	
		}
		return sp;
	}
	public static bool contextClicked(Rect rect){
		Event currentEvent = Event.current;
		if(currentEvent.type==EventType.ContextClick && 
			rect.Contains(currentEvent.mousePosition))
		{
			currentEvent.Use();
			return true;
		}
		return false;
	}
	public static Type getPropertyDrawerType(Type propertyType){
		/* Credit: Dr-Nick, johnseghersmsft, UF */
		/* It seems Unity have internal class UnityEditor.ScriptAttributeUtility that
		has internal static method GetDrawerTypeForType(Type) which can grab corresponding
		PropertyDrawer for given types. We will aim to access that. */
		Assembly[] aAssembly = AppDomain.CurrentDomain.GetAssemblies();
		Assembly assemblyUnityEditor = null;
		for(int i=0; i<aAssembly.Length; ++i){
			if(aAssembly[i].GetName().Name == "UnityEditor"){
				assemblyUnityEditor = aAssembly[i];
				break;
			}
		}
		return (Type)assemblyUnityEditor.GetType("UnityEditor.ScriptAttributeUtility")
			.GetMethod("GetDrawerTypeForType",ReflectionHelper.BINDINGFLAGS_ALL)
			.Invoke(null,new object[]{propertyType})
		;
	}
	/* Although it is just simple 2 calls in Unity API, you still need to
	look it up every time, and if done wrong, it can DELETE your folder.
	Hence, for safety, we create this function and just call it.
	"name" should also include extension (e.g. ".asset") */
	public static void createAssetAtCurrentFolder(Object o,string name,bool bPingObject=true){
		AssetDatabase.CreateAsset(
			o,
			AssetDatabase.GenerateUniqueAssetPath(EditorPath.getSelectionFolder()+"/"+name)
		);
		AssetDatabase.SaveAssets(); //may use newer AssetDatabase.SaveAssetIfDirty() if supported
		if(bPingObject){
			EditorGUIUtility.PingObject(o);}
	}
	public static Object multitypeObjectField(string label,Object uo,Type[] aType){
		EditorGUILayout.LabelField(" ");
		return multitypeObjectField(GUILayoutUtility.GetLastRect(),label,uo,aType);
	}
	public static Object multitypeObjectField(Rect rect,string label,Object uo,Type[] aType)
	{
		float xMax = rect.xMax;
		EditorGUI.LabelField(rect,label);
		rect.x += EditorGUIUtility.labelWidth;
		rect.width -= EditorGUIUtility.labelWidth;
		Rect rectEvent = rect;
		rectEvent.width -= EditorGUIUtility.singleLineHeight+2.0f;
		Object uoUser = multitypeDropZone(rectEvent,aType)?[0];
		rectEvent.x = rect.xMax-EditorGUIUtility.singleLineHeight;
		rectEvent.width = EditorGUIUtility.singleLineHeight;
		Object uoPicked = objectPicker(rectEvent,getFilterString(aType));
		if(uoPicked){
			uoUser = uoPicked; }
		EditorGUI.ObjectField(rect,"",uo,typeof(Object),true);
		if(uoUser){
			for(int i=0; i<aType.Length; ++i){
				if(uoUser.GetType()==aType[i]){
					return uoUser;}
			}
		}
		return uo;
	}
	public static Object objectPicker(
		Rect rect,string filterString,GUIContent guiContent=null,bool bAllowScene=true)
	{
		if(GUI.Button(rect,guiContent??new GUIContent(""))){
			Object obj = null;
			EditorGUIUtility.ShowObjectPicker<Object>(obj,bAllowScene,filterString,0); //Credit: YinXiaoZhou, UA
		}
		Event evt = Event.current;
		EventType eventType = evt.type;
		if(eventType==EventType.ExecuteCommand && evt.commandName=="ObjectSelectorUpdated"){
			evt.Use();
			return EditorGUIUtility.GetObjectPickerObject();
		}
		return null;
	}
	public static Object objectPicker<T>(
		Rect rect,GUIContent guiContent=null,bool bAllowScene=true)
		where T:Object
	{
		return objectPicker(rect,typeof(T).Name,guiContent,bAllowScene);
	}
	public static string getFilterString(Type[] aType){
		string sFilter = "";
		for(int i=0; i<aType.Length; ++i){
			sFilter += "t:"+aType[i].Name+",";}
		return sFilter;
	}
	/* This function relies on reflection and method names so it may break in future version.
	For example, in older versions, you need "Window/Hierarchy" as menu name. */
	public static void setExpandInHierarchy(Object uo,bool bExpand){
		EditorApplication.ExecuteMenuItem("Window/General/Hierarchy"); //Credit: vexe & usernameHed, UA
		EditorWindow hierarchyWindow = EditorWindow.focusedWindow;
		//Credit: Jlpeebles, UA
		hierarchyWindow.GetType()
			.GetMethod("SetExpandedRecursive",ReflectionHelper.BINDINGFLAGS_ALL)
			.Invoke(
				hierarchyWindow,
				new object[]{uo.GetInstanceID(),bExpand}
			)
		;
	}
}

/* Facilitate Editor writing a bit. */
public class Editor<T> : Editor where T:Object{
	protected T targetAs;
	protected virtual void OnEnable(){
		targetAs = (T)target;
	}
}

public static class UnityIconPath{
	public const string ICONPATH_WARNING = "icons/console.warnicon.png";
	public const string ICONPATH_WARNING_SMALL = "icons/console.warnicon.sml.png";
	public const string ICONPATH_ERROR = "icons/console.erroricon.png";
	public const string ICONPATH_INFO = "icons/console.infoicon.png";
}

public static class UnityWindowName{
	public const string WINDOWNAME_SCENEVIEW = " (UnityEditor.SceneView)";
	public const string WINDOWNAME_HIERARCHY = " (UnityEditor.SceneHierarchyWindow)";
	public const string WINDOWNAME_PROJECT = " (UnityEditor.ProjectBrowser)";
	public const string WINDOWNAME_INSPECTOR = " (UnityEditor.InspectorWindow)";
}

public class FloatBuffer{
	/* A Float buffer that accepts and validate each character added
	without need of tangible GUI FloatField. Problem with FloatField is that
	it eats up all keystroke when it has focus, and there is no easy way
	to send keystroke directly to it. This FloatBuffer Cannot do arithmatic
	and does not accept character that will render sBuffer unparsable,
	meaning that if buffer is not empty there is always valid Value. */
	private string sBuffer;
	private bool bHasDecimal;
	private float value;
	public float Value{ get{return value;} }
	public override string ToString(){
		return sBuffer;
	}
	public bool append(char c){
		if(c=='.'){
			if(bHasDecimal)  //ignore duplicate decimal point
				return false;
			bHasDecimal = true;
		}
		else if(!(char.IsDigit(c) || (c=='-'&&sBuffer.Length==0))) //prefix negative
			return false;
		sBuffer += c;
		return float.TryParse(sBuffer,out value);
	}
	public FloatBuffer backspace(){
		if(sBuffer.Length > 0){
			if(sBuffer[sBuffer.Length-1] == '.')
				bHasDecimal = false;
			sBuffer = sBuffer.Remove(sBuffer.Length-1);
			float.TryParse(sBuffer,out value);
		}
		return this;
	}
	public void clear(){
		sBuffer = "";
		value = 0.0f;
		bHasDecimal = false;
	}
	public bool hasContent(){
		return sBuffer != "";
	}
	public static FloatBuffer operator+(FloatBuffer floatBuffer,char c){
		floatBuffer.append(c);
		return floatBuffer;
	}
	public static FloatBuffer operator--(FloatBuffer floatBuffer){
		floatBuffer.backspace();
		return floatBuffer;
	}
}

public static class GUIStyleCollection{
	public static readonly GUIStyle blackBoldLabel;
	static GUIStyleCollection(){
		blackBoldLabel = new GUIStyle(EditorStyles.boldLabel);
		blackBoldLabel.normal.textColor = Color.black;
	}
}

//=====================================================================================
#region FORWARDDRAWER
/* A PropertyDrawer base class that will forward property drawing to
property's PropertyDrawer if exists. */
public abstract class ForwardDrawer : PropertyDrawer{
	private bool bInit = false;
	private PropertyDrawer propertyDrawer;
	private void init(){
		if(fieldInfo==null){
			return;}
		if(attribute != null){
		/* Use Attribute drawer first, and type drawer later. */
			//Type typeDrawer = EditorHelper.getPropertyDrawerType(attribute.GetType());
			//if(typeof(ForwardDrawer).IsAssignableFrom(typeDrawer)){
			Attribute[] aAttribute = Attribute.GetCustomAttributes(fieldInfo);
			Type[] aType = new Type[aAttribute.Length];
			int indexType=0;
			for(int i=0; i<aAttribute.Length; ++i){
				if(aAttribute[i].GetType()==attribute.GetType()){
					continue;}
				aType[indexType++] = aAttribute[i].GetType();
			}
			Type typeToDraw = fieldInfo.FieldType;
			if(typeof(IList).IsAssignableFrom(typeToDraw)){
				typeToDraw = typeToDraw.GetElementType();}
			aType[indexType] = typeToDraw;

			initNextDrawer(aType,0);
		}
		/* If not attribute drawer, we don't need to forward anymore, so let propertyDrawer=null */
		bInit = true;
	}
	private void initNextDrawer(Type[] aType,int indexType){
		if(aType!=null && indexType>=0){
			for(int i=indexType; i<aType.Length; ++i){
				Type typeDrawer = EditorHelper.getPropertyDrawerType(aType[i]);
				if(typeDrawer != null){
					propertyDrawer = createPropertyDrawer(
						typeDrawer,
						fieldInfo,
						typeof(PropertyAttribute).IsAssignableFrom(aType[i]) ? 
							(PropertyAttribute)fieldInfo.GetCustomAttribute(aType[i]) :
							null
					);
					(propertyDrawer as ForwardDrawer)?.initNextDrawer(aType,i+1);
					break;
				}
			}
		}
		bInit = true;
	}
	private PropertyDrawer createPropertyDrawer(
		Type typeDrawer,FieldInfo fieldInfo,PropertyAttribute attribute)
	{
		//Credit: Deleted User, UF
		PropertyDrawer propertyDrawer = (PropertyDrawer)Activator.CreateInstance(typeDrawer);
		typeof(PropertyDrawer).GetField("m_FieldInfo",ReflectionHelper.BINDINGFLAGS_ALL)
			.SetValue(propertyDrawer,fieldInfo);
		typeof(PropertyDrawer).GetField("m_Attribute",ReflectionHelper.BINDINGFLAGS_ALL)
			.SetValue(propertyDrawer,attribute);
		return propertyDrawer;
	}
	public override void OnGUI(Rect position,SerializedProperty property,GUIContent label){
		if(!bInit){
			init();}
		if(propertyDrawer != null){
			propertyDrawer.OnGUI(position,property,label);}
		else{
			EditorGUI.PropertyField(
				position,
				property,
				label,
				property.isExpanded
			);
		}
	}
}
#endregion
//=====================================================================================

} //end namespace Chameleon

#endif
