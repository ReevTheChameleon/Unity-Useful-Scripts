/*************************************************************************
 * MONOBEHAVIOURBAKER (v3.3)
 * by Reev the Chameleon
 * 2 Dec 2
**************************************************************************
Usage:
Attach [Bakable] attribute to static or nonpublic non[SerializeField] fields
or any kinds of properties, then make custom editor for your MonoBehaviour that
extends MonoBehaviourBakerEditor to show them in inspector.
You can then set their values and click "Bake" button to bake their values directly into
its source code script.
(Note that doing so will naturally cause hot-reload. Also, baking NONstatic fields
or properties will naturally affect all instances. If you want to set different values
for each instance, normal serialization approach is unavoidable.)
Using this approach, you can save your settings to non-[SerializeField] fields, static fields,
and properties at virtually no memory overhead and without need to perform serialization.
Useful for optimization.
[1 Jul 2] Now you can attach [JsonBakable] attribute to Unity class type varaibles
to bake them as JSON directly into your code. However, reference types (such as GameObject)
are still not supported.
** Limitation 1: Currently support types: int, float, string, bool, and Vector3 and their array,
but undo functionality not yet supported for array. (Lists are not supported yet, but baking them
should be rare anyway).
** Limitation 2 (technical): In case of multiple objects in the scene, if you change and bake
value in one of them, values on the remaining object AND PREFABS will NOT change UNTIL you either
enter play mode or relaunch the editor. This is because they are already created.
Once you enter play mode or relaunch the editor, values (which are not serializable)
will be reloaded directly from the script, and so all objects of that type will share
the same value at that time. This may cause confusion, but what is shown is the true value
for each of them (which coincide with debug inspector). Anyway, I may think of some way
to mitigate this, probably in the next versions. 
** Limitation 3: Declaring multiple variables on same line is not supported yet:
[attribute] int a,b; //This is not supported yet.
//****Note about Color****
Newly created color is with alpha=0 (transparent). This is true even when you assign opaque color
to the field using eyedropper. This weird behavior is also true for usual PropertyField too,
so it is by Unity's design and will not be fixed.

Update v1.1: Use SourceCodeFiddler for souce code manipulation instead for generalization
Update v1.2: Make this class usable with MonoBehaviourBakerEditorWithScene class
Update v2.0: Add support for array of supported types
Update v2.1: Add support for Vector2, Vector3Int, and Vector2Int type, and refactor code
Update v2.2: Add support for Color type
Update v3.0: Add support for baking JSON serializable Unity class type
Update v3.1: Add copy/paste functionality for [Bakable], and move some code to JsonObject class
and fix bug setting const fields
Update v3.2: Add support for Bounds type
Update v3.2.1: Slight modify inspector interface for const fields
Update v3.2.2: Minor code change to match revised code in EditorHelper
Update v3.2.3: Minor code change to match revised code in EditorHelper (again)
Update v3.2.4: Minor code change to reflect rivision in context menu code in EditorHelper
Update v3.2.5: Fix NullReferenceException bug when drawing fields with null value
Update v3.3: Modify code to allow base class fields and properties show up in inspector
*/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Collections;

using Object = UnityEngine.Object;

namespace Chameleon{

//====================================================================================
#region NORMAL BAKER

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class BakableAttribute : Attribute{
	public int lineNumber;
	public string filePath;
	public BakableAttribute(
		[CallerLineNumber] int lineNumber=0,
		[CallerFilePath] string filePath=""
	){
		/* [CallerLineNumber] requires that you assign default value
		to field it attaches to. Because of it, compiler will overwrite
		lineNumber with actual line where attribute is used.
		(Credit: illegal-immigrant, SO) */
		this.lineNumber = lineNumber;
		this.filePath = filePath;
	}
}

#if UNITY_EDITOR
/* Because Unity Undo system will NOT record any states for non-serializable properties,
need to resort to FieldUndo custom class. Currently observing bugs. */
public abstract partial class MonoBehaviourBakerEditor : Editor{
	public const string EDITORPREFS_AUTOBAKE = "Chm_AutoBake";

	private const int RECORDERID_CONST = 42;
	private static readonly GUIContent guiContentConstTooltip =
		new GUIContent("","Due to their nature, consts are baked immediately upon changed. " +
			"Change will be reflected once scripts recompile.")
		;
	
	/* Must also return ReorderableList somehow to support array. May think of
	more elegant way to do this later. */
	public static List<FieldInfo> getBakableFieldInfoList(Object target,
		out List<ReorderableList> lReorderableList)
	{
		List<FieldInfo> lBakableFieldInfo = new List<FieldInfo>();
		lReorderableList = new List<ReorderableList>();
		foreach(FieldInfo fieldInfo
			in target.GetType().GetFields(ReflectionHelper.BINDINGFLAGS_ALL))
		{
			if(!fieldInfo.IsDefined(typeof(BakableAttribute)) ||
				fieldInfo.IsDefined(typeof(SerializeField)) ||
				(fieldInfo.IsPublic && !fieldInfo.IsStatic) ||
				(!isBakableType(fieldInfo.FieldType) &&
					!isBakableType(fieldInfo.FieldType.GetElementType()))) //non array will be null
				continue;
			lBakableFieldInfo.Add(fieldInfo);
			
			Type fieldType = fieldInfo.FieldType;
			if(typeof(IList).IsAssignableFrom(fieldType)){
			//if(fieldType.IsArray){
				lReorderableList.Add(
					EditorHelper.createReorderableList((IList)fieldInfo.GetValue(target),fieldType)
				);
			}
		}
		return lBakableFieldInfo;
	}
	public static List<PropertyInfo> getBakablePropertyInfoList(Object target){
		List<PropertyInfo> lBakablePropertyInfo = new List<PropertyInfo>();
		foreach(PropertyInfo propertyInfo
			in target.GetType().GetProperties(ReflectionHelper.BINDINGFLAGS_ALL))
		{
			if(!propertyInfo.IsDefined(typeof(BakableAttribute)) ||
				!isBakableType(propertyInfo.PropertyType))
				continue;
			lBakablePropertyInfo.Add(propertyInfo);
		}
		return lBakablePropertyInfo;
	}
	private static void drawNormalBakableInspector(Object target,List<FieldInfo> lBakableFieldInfo,
		List<ReorderableList> lBakableReorderableList,List<PropertyInfo> lBakablePropertyInfo,
		ref bool bFieldFoldout,ref bool bPropertyFoldout,ref bool bAutoBake)
	{
	//----------------------------------------------------------------------------------
		#region FIELDS
		if(lBakableFieldInfo?.Count>0){
			bFieldFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(bFieldFoldout,"Fields");
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(bFieldFoldout){
				++EditorGUI.indentLevel;
				/* Only consider [Bakable] that is not serialized, because if it is serialized
				then baking is meaningless. */
				int indexReorderableList = 0;
				foreach(FieldInfo fieldInfo in lBakableFieldInfo){
					GUI.enabled = true;
					if(fieldInfo.FieldType.IsArray){
						object value = fieldInfo.GetValue(target);
						EditorGUILayout.BeginHorizontal();
						string name = "";
						if(fieldInfo.DeclaringType != target.GetType()) //Credit: cuongle, SO
							name += "[base] ";
						if(fieldInfo.IsLiteral) //shouldn't be possible for array element though
							name += "[const] ";
						else if(fieldInfo.IsStatic)
							name += "[static] ";
						name += fieldInfo.Name;
						EditorGUILayout.LabelField(name);
						EditorGUILayout.Space(10);
						GUI.enabled = !Application.isPlaying;
						if(GUILayout.Button("Bake",GUILayout.Width(50f))){
							BakableAttribute attribute = fieldInfo.GetCustomAttribute<BakableAttribute>();
							bake(
								attribute.filePath,
								attribute.lineNumber,
								fieldInfo.Name,
								fieldInfo.FieldType,
								value,
								false
							);
						}
						GUI.enabled = true;
						EditorGUILayout.EndHorizontal();
						EditorGUILayout.Space(5.0f);
						//For whatever reason, EditorGUI.indentLevel has no effects on ReorderableList
						//currently not yet support ReorderableList undo
						ReorderableList r = lBakableReorderableList[indexReorderableList++];
						EditorGUI.BeginChangeCheck();
						r.DoLayoutList();
						if(EditorGUI.EndChangeCheck())
							fieldInfo.SetValue(target,r.list);
					}
					else{
						EditorGUILayout.BeginHorizontal();
						string name = "";
						object value = fieldInfo.GetValue(target);
						object userValue;
						BakableAttribute attribute = fieldInfo.GetCustomAttribute<BakableAttribute>();
						if(fieldInfo.DeclaringType != target.GetType())
							name += "[base] ";
						if(fieldInfo.IsLiteral){
							/* Because consts can't be changed (even via reflection), but it would still be
							very useful to bake them due to memory save (they become literal), I decide to
							allow user to change them from inspector on the condition that they are baked
							immediately upon changed. NOTE: Undo feature not supported yet! */
							name += "[const] ";
							name += fieldInfo.Name;
							if(EditorApplication.isCompiling){
								bool bSavedEnabled = GUI.enabled;
								GUI.enabled = false;
								EditorGUILayout.TextField(name,"Please wait for recompile");
								GUI.enabled = bSavedEnabled;
							}
							else if(!EditorHelper.drawField(name,value,out userValue,true,
								fieldInfo.FieldType,EditorHelper.getDrawnAttribute(fieldInfo)))
							{
								FieldUndo.recordSetFieldInfo(target,fieldInfo,userValue,
									"Const Field Change",RECORDERID_CONST,false);
								bake(
									attribute.filePath,
									attribute.lineNumber,
									fieldInfo.Name,
									fieldInfo.FieldType,
									userValue,
									false
								);
							}
							GUILayout.Space(10);
							GUILayout.Button(
								"",
								EditorStyles.label,
								GUILayout.Width(50)
							);
							Rect rect = GUILayoutUtility.GetLastRect();
							EditorGUI.LabelField(
								rect,
								"Auto"
							);
							EditorGUI.LabelField(rect,guiContentConstTooltip);
						}

						else{ //not const
							if(fieldInfo.IsStatic)
								name += "[static] ";
							if(fieldInfo.IsInitOnly)
								name += "[readonly] ";
							name += fieldInfo.Name;
							
							if(!EditorHelper.drawField(name,value,out userValue,false,
								fieldInfo.FieldType,EditorHelper.getDrawnAttribute(fieldInfo)))
								FieldUndo.recordSetFieldInfo(target,fieldInfo,userValue);
							if(EditorHelper.contextClicked(GUILayoutUtility.GetLastRect()))
								EditorHelper.createCopyPasteContextMenu(
									target as MonoBehaviour,
									fieldInfo.Name,
									true
								).ShowAsContext();
				
							GUILayout.Space(10);
							GUI.enabled = !Application.isPlaying; //recompile during playmode will cause trouble
							if(GUILayout.Button("Bake",GUILayout.Width(50))){
								bake(
									attribute.filePath,
									attribute.lineNumber,
									fieldInfo.Name,
									fieldInfo.FieldType,
									value,
									false
								);
							}
						}
						EditorGUILayout.EndHorizontal();
					}
				}
				--EditorGUI.indentLevel;
			} //end bFieldFoldout
		}
		#endregion
	//----------------------------------------------------------------------------------
		#region PROPERTIES
		if(lBakablePropertyInfo?.Count>0){
			bPropertyFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(bPropertyFoldout,"Properties");
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(bPropertyFoldout){
				++EditorGUI.indentLevel;
				foreach(PropertyInfo propertyInfo in lBakablePropertyInfo){
					GUI.enabled = propertyInfo.SetMethod!=null;
					EditorGUILayout.BeginHorizontal();
					string name = "";
					if(propertyInfo.DeclaringType != target.GetType())
						name += "[base] ";
					if(propertyInfo.GetMethod.IsStatic)
						name += "[static] ";
					name += propertyInfo.Name;

					object value = propertyInfo.GetValue(target);
					object userValue;
					if(!EditorHelper.drawField(name,value,out userValue,false,
						propertyInfo.PropertyType,EditorHelper.getDrawnAttribute(propertyInfo)))
						FieldUndo.recordSetPropertyInfo(target,propertyInfo,userValue);
					if(EditorHelper.contextClicked(GUILayoutUtility.GetLastRect()))
						EditorHelper.createCopyPasteContextMenu(
							target as MonoBehaviour,
							propertyInfo.Name,
							true
						).ShowAsContext();
				
					GUILayout.Space(10);
					GUI.enabled &= !Application.isPlaying; //recompile during playmode will cause trouble
					if(GUILayout.Button("Bake",GUILayout.Width(50))){
						BakableAttribute attribute = propertyInfo.GetCustomAttribute<BakableAttribute>();
						bake(
							attribute.filePath,
							attribute.lineNumber,
							propertyInfo.Name,
							propertyInfo.PropertyType,
							value,
							true
						);
					}
					EditorGUILayout.EndHorizontal();
				}
				--EditorGUI.indentLevel;
			} //end bPropertyFoldout
		}
		#endregion
	//----------------------------------------------------------------------------------
	}
	public static void bake(string scriptPath,int lineNumber,string fieldName,
		Type type,object value,bool bBlockStatement,bool bDelayedCall=false)
	{
		if(value == null)
			bakeFieldText(scriptPath,lineNumber,fieldName,null,bBlockStatement);
		else if(typeof(IList).IsAssignableFrom(type)){
			int length = ((IList)value).Count;
			string bakeText = "{";
			for(int i=0; i<length; ++i)
				bakeText += buildText(type.GetElementType(),((IList)value)[i]) + ",";
			bakeText += "}";
			bakeFieldText(scriptPath,lineNumber,fieldName,bakeText,bBlockStatement);
		}
		else
			bakeFieldText(scriptPath,lineNumber,fieldName,buildText(type,value),bBlockStatement);
		
		if(bDelayedCall)
			/* In case of warning "SendMessage cannot be called during
			Awake, CheckConsistency, or OnValidate" */
			EditorApplication.delayCall += AssetDatabase.Refresh;
		else
			AssetDatabase.Refresh();
	}
	private static string buildText(Type type,object value){
		switch(type.Name){
			case nameof(Int32):
				return value.ToString();
			case nameof(String):
				return "\""+value.ToString()+"\"";
			case nameof(Boolean):
				return value.ToString().ToLower();
			case nameof(Single):
				return value.ToString()+"f";
			case nameof(Vector3):
				Vector3 vector3Value = value as Vector3? ?? Vector3.zero;
				return "new Vector3("+vector3Value.x+"f,"+vector3Value.y+"f,"+vector3Value.z+"f)";
			case nameof(Vector2):
				Vector2 vector2Value = value as Vector2? ?? Vector2.zero;
				return "new Vector2("+vector2Value.x+"f,"+vector2Value.y+"f)";
			case nameof(Vector3Int):
				Vector3Int vector3IntValue = value as Vector3Int? ?? Vector3Int.zero;
				return "new Vector3Int("+vector3IntValue.x+","+vector3IntValue.y+","+vector3IntValue.z+")";
			case nameof(Vector2Int):
				Vector2Int vector2IntValue = value as Vector2Int? ?? Vector2Int.zero;
				return "new Vector2Int("+vector2IntValue.x+","+vector2IntValue.y+")";
			case nameof(Color):
				Color colorValue = value as Color? ?? Color.black;
				return "new Color("+colorValue.r+"f,"+colorValue.g+"f,"+colorValue.b+"f,"+colorValue.a+"f)";
			case nameof(Vector4):
				Vector4 vector4Value = value as Vector4? ?? Vector4.zero;
				return "new Vector4("+vector4Value.x+"f,"+vector4Value.y+"f,"+vector4Value.z+"f,"+vector4Value.w+"f)";
			case nameof(Bounds):
				Bounds boundValue = value as Bounds? ?? new Bounds();
				return "new Bounds("+buildText(boundValue.center)+","+buildText(boundValue.extents*2)+")";
		}
		return null;
	}
	private static string buildText<T>(T value){
		return buildText(typeof(T),value);
	}
	private static void bakeFieldText(string scriptPath,int lineNumber,
		string fieldName,string content,bool bBlockStatement){
		/* Current algorithm is not very beautiful. May review and revamp algorithm,
		possibly apply state machine concept.  */
		/* Specify content=null to unbake */
		//string scriptPath =
		//	AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(monoBehaviour));
		if(scriptPath==null || content==null)
			return;
		/* Because you can't just insert into the middle of the file, the best
		approach is to read everything, modify your content, then rewriting it back.
		(Credit: Jonathon Reinhart, SO). This should still be fairly efficient if
		file is not larger than, say, 500 lines (Credit: Peter, SO) */
		SourceCodeFiddler sourceCodeFiddler = new SourceCodeFiddler();
		if(!sourceCodeFiddler.readFile(scriptPath))
			return;
		sourceCodeFiddler.moveToLine(lineNumber);
		sourceCodeFiddler.moveToToken(fieldName);
		if(!bBlockStatement){
			sourceCodeFiddler.moveToEndToken();
			sourceCodeFiddler.removeUntil(';');
			if(content != null)
				sourceCodeFiddler.insert(" = "+content);
		}
		else{ //blockStatement
			sourceCodeFiddler.moveToCloseBrace();
			sourceCodeFiddler.stashPosition();
			sourceCodeFiddler.moveToNextCodeChar();
			bool bAssigned = sourceCodeFiddler.ThisChar=='=';
			sourceCodeFiddler.destashPosition();
			if(bAssigned){
				sourceCodeFiddler.nextChar();
				sourceCodeFiddler.removeUntil(';');
				if(content != null)
					sourceCodeFiddler.insert(" = "+content,false);
			}
			else if(content != null)
				sourceCodeFiddler.insert(" = "+content+";",true);
		}
		sourceCodeFiddler.writeFile(scriptPath);
	}
	public static void bakeAllFields(Object target,List<FieldInfo> lFieldInfo){
		foreach(FieldInfo fieldInfo in lFieldInfo){
			BakableAttribute attribute = fieldInfo.GetCustomAttribute<BakableAttribute>();
			bake(
				attribute.filePath,
				attribute.lineNumber,
				fieldInfo.Name,
				fieldInfo.FieldType,
				fieldInfo.GetValue(target),
				false
			);
		}	
	}
	public static void bakeAllProperties(Object target,List<PropertyInfo> lPropertyInfo){
		foreach(PropertyInfo propertyInfo in lPropertyInfo){
			BakableAttribute attribute = propertyInfo.GetCustomAttribute<BakableAttribute>();
			bake(
				attribute.filePath,
				attribute.lineNumber,
				propertyInfo.Name,
				propertyInfo.PropertyType,
				propertyInfo.GetValue(target),
				true
			);
		}
	}
	private static bool isBakableType(Type type){
		switch(type?.Name){
			case nameof(Int32):
			case nameof(Single):
			case nameof(String):
			case nameof(Boolean):
		
			case nameof(Vector3):
			case nameof(Vector2):
			case nameof(Vector3Int):
			case nameof(Vector2Int):
			case nameof(Color):
			case nameof(Vector4):
			case nameof(Bounds):
				return true;
		}
		return false;
	}
	public static void onConstFieldUndo(
		int recorderID,int objectID,string fieldName,string sValue)
	{
		if(recorderID == RECORDERID_CONST){
			Object target =  EditorUtility.InstanceIDToObject(objectID);
			if(!target)
				return;
			FieldInfo fieldInfo = target.GetType().GetField(
				fieldName,ReflectionHelper.BINDINGFLAGS_ALL
			);
			if(fieldInfo == null)
				return;
			BakableAttribute attribute = fieldInfo.GetCustomAttribute<BakableAttribute>();
			object oValue;
			if(!UnityTypeParser.tryParse(sValue,fieldInfo.FieldType,out oValue) ||
				oValue==fieldInfo.GetValue(target))
				return;
			bake(
				attribute.filePath,
				attribute.lineNumber,
				fieldName,
				fieldInfo.FieldType,
				oValue,
				false,
				true
			);
		} 
	}
}
#endif

#endregion
//====================================================================================

//====================================================================================
#region JSON BAKER

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class JsonBakableAttribute : Attribute{
	public int lineNumber;
	public string filePath;
	public JsonBakableAttribute(
		[CallerLineNumber] int lineNumber=0,
		[CallerFilePath] string filePath=""
	){
		/* [CallerLineNumber] requires that you assign default value
		to field it attaches to. Because of it, compiler will overwrite
		lineNumber with actual line where attribute is used.
		(Credit: illegal-immigrant, SO) */
		this.lineNumber = lineNumber;
		this.filePath = filePath;
	}
}

#if UNITY_EDITOR
/* Because Unity EditorGUI and EditorGUILayout function for class types (verify later)
MODIFIES original value BEFORE returning true for EndChangeCheck next call, it is impossible
to undo such fields unless you allocate memory for every field to store their values
as soon as they are changed. This is not affordable, so I decide that Json Bakable fields
(class types) do NOT have undo feature. However, I will add revert feature instead. */
public abstract partial class MonoBehaviourBakerEditor : Editor{
	/* Must also return ReorderableList somehow to support array. May think of
	more elegant way to do this later. */
	public static List<FieldInfo> getJsonBakableFieldInfoList(Object target,
		out List<ReorderableList> lReorderableList)
	{
		List<FieldInfo> lBakableFieldInfo = new List<FieldInfo>();
		lReorderableList = new List<ReorderableList>();
		foreach(FieldInfo fieldInfo
			in target.GetType().GetFields(ReflectionHelper.BINDINGFLAGS_ALL))
		{
			if(!fieldInfo.IsDefined(typeof(JsonBakableAttribute)) ||
				fieldInfo.IsDefined(typeof(SerializeField)) ||
				(fieldInfo.IsPublic && !fieldInfo.IsStatic) ||
				fieldInfo.IsLiteral || //Can't json bake const
				(!JsonObject.isSupportedType(fieldInfo.FieldType) &&
					!JsonObject.isSupportedType(fieldInfo.FieldType.GetElementType()))) //non array will be null
				continue;
			lBakableFieldInfo.Add(fieldInfo);
			
			Type fieldType = fieldInfo.FieldType;
			if(typeof(IList).IsAssignableFrom(fieldType)){
			//if(fieldType.IsArray){
				lReorderableList.Add(
					EditorHelper.createReorderableList((IList)fieldInfo.GetValue(target),fieldType)
				);
			}
		}
		return lBakableFieldInfo;
	}
	public static List<PropertyInfo> getJsonBakablePropertyInfoList(Object target){
		List<PropertyInfo> lBakablePropertyInfo = new List<PropertyInfo>();
		foreach(PropertyInfo propertyInfo
			in target.GetType().GetProperties(ReflectionHelper.BINDINGFLAGS_ALL))
		{
			if(!propertyInfo.IsDefined(typeof(JsonBakableAttribute)) ||
				!JsonObject.isSupportedType(propertyInfo.PropertyType))
				continue;
			lBakablePropertyInfo.Add(propertyInfo);
		}
		return lBakablePropertyInfo;
	}
	private static void drawJsonBakableInspector(Object target,List<FieldInfo> lBakableFieldInfo,
		List<ReorderableList> lBakableReorderableList,List<PropertyInfo> lBakablePropertyInfo,
		ref bool bFieldFoldout,ref bool bPropertyFoldout,ref bool bAutoBake)
	{
	//----------------------------------------------------------------------------------
		#region FIELDS
		if(lBakableFieldInfo?.Count>0){
			bFieldFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(bFieldFoldout,"JSON Fields");
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(bFieldFoldout){
				++EditorGUI.indentLevel;
				/* Only consider [Bakable] that is not serialized, because if it is serialized
				then baking is meaningless. */
				int indexReorderableList = 0;
				foreach(FieldInfo fieldInfo in lBakableFieldInfo){
					GUI.enabled = true;
					if(fieldInfo.FieldType.IsArray){
						object value = fieldInfo.GetValue(target);
						EditorGUILayout.BeginHorizontal();
						string name = "";
						if(fieldInfo.DeclaringType != target.GetType())
							name += "[base] ";
						if(fieldInfo.IsStatic)
							name += "[static] ";
						if(fieldInfo.IsInitOnly)
							name += "[readonly] ";
						name += fieldInfo.Name;
						EditorGUILayout.LabelField(name);
						EditorGUILayout.Space(10);
						GUI.enabled = !Application.isPlaying;
						JsonBakableAttribute attribute =
							fieldInfo.GetCustomAttribute<JsonBakableAttribute>();
						if(GUILayout.Button("B",GUILayout.Width(23f))){
							bakeJson(
								attribute.filePath,
								attribute.lineNumber,
								fieldInfo.Name,
								fieldInfo.FieldType,
								value,
								false
							);
						}
						if(GUILayout.Button("R",GUILayout.Width(23))){
							string json = readAssignedJson(
								target as MonoBehaviour,
								attribute.lineNumber,
								fieldInfo.Name,
								false
							);
							if(json != null){
								fieldInfo.SetValue(
									target,
									JsonObject.objectFromJson(fieldInfo.FieldType,json,true)
								);
								//Make sure that ReorderableList points to our new array
								lBakableReorderableList[indexReorderableList] = 
									EditorHelper.createReorderableList(
										(IList)fieldInfo.GetValue(target),
										fieldInfo.FieldType
									)
								;
							}
						}
						GUI.enabled = true;
						EditorGUILayout.EndHorizontal();
						EditorGUILayout.Space(5.0f);
						ReorderableList r = lBakableReorderableList[indexReorderableList++];
						//For whatever reason, EditorGUI.indentLevel has no effects on ReorderableList
						//currently not yet support ReorderableList undo
						EditorGUI.BeginChangeCheck();
						r.DoLayoutList();
						if(EditorGUI.EndChangeCheck())
							fieldInfo.SetValue(target,r.list);
					}
					else{
						EditorGUILayout.BeginHorizontal();
						string name = "";
						object value = fieldInfo.GetValue(target);
						object userValue;
						JsonBakableAttribute attribute =
							fieldInfo.GetCustomAttribute<JsonBakableAttribute>();

						if(fieldInfo.IsStatic)
							name += "[static] ";
						if(fieldInfo.IsInitOnly)
							name += "[readonly] ";
						name += fieldInfo.Name;
							
						if(!EditorHelper.drawField(name,value,out userValue,false,fieldInfo.FieldType))
							fieldInfo.SetValue(target,userValue);
						if(EditorHelper.contextClicked(GUILayoutUtility.GetLastRect()))
							EditorHelper.createCopyPasteContextMenu(
								target as MonoBehaviour,
								fieldInfo.Name
							).ShowAsContext();

						GUILayout.Space(10);
						GUI.enabled = !Application.isPlaying; //recompile during playmode will cause trouble
						if(GUILayout.Button("B",GUILayout.Width(23))){
							bakeJson(
								attribute.filePath,
								attribute.lineNumber,
								fieldInfo.Name,
								fieldInfo.FieldType,
								value,
								false
							);
						}
						if(GUILayout.Button("R",GUILayout.Width(23))){
							string json = readAssignedJson(
								target as MonoBehaviour,
								attribute.lineNumber,
								fieldInfo.Name,
								false
							);
							if(json != null){
								fieldInfo.SetValue(target,JsonObject.objectFromJson(
									fieldInfo.FieldType,json,false)
								);
							} 
						}
						EditorGUILayout.EndHorizontal();
					}
				}
				--EditorGUI.indentLevel;
			} //end bFieldFoldout
		}
		#endregion
	//----------------------------------------------------------------------------------
		#region PROPERTIES
		if(lBakablePropertyInfo?.Count>0){
			bPropertyFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(bPropertyFoldout,"JSON Properties");
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(bPropertyFoldout){
				++EditorGUI.indentLevel;
				foreach(PropertyInfo propertyInfo in lBakablePropertyInfo){
					GUI.enabled = propertyInfo.SetMethod!=null;
					EditorGUILayout.BeginHorizontal();
					string name =
						propertyInfo.GetMethod.IsStatic ?
						"[static] "+propertyInfo.Name :
						propertyInfo.Name
					;
					object value = propertyInfo.GetValue(target);
					object userValue;
						
					if(!EditorHelper.drawField(name,value,out userValue,false,propertyInfo.PropertyType))
						propertyInfo.SetValue(target,userValue);
					if(EditorHelper.contextClicked(GUILayoutUtility.GetLastRect()))
						EditorHelper.createCopyPasteContextMenu(
							target as MonoBehaviour,
							propertyInfo.Name
						).ShowAsContext();
					GUILayout.Space(10);
					GUI.enabled &= !Application.isPlaying; //recompile during playmode will cause trouble
					JsonBakableAttribute attribute =
						propertyInfo.GetCustomAttribute<JsonBakableAttribute>();
					if(GUILayout.Button("B",GUILayout.Width(23))){
						bakeJson(
							attribute.filePath,
							attribute.lineNumber,
							propertyInfo.Name,
							propertyInfo.PropertyType,
							value,
							true
						);
					}
					if(GUILayout.Button("R",GUILayout.Width(23))){
						string json = readAssignedJson(
							target as MonoBehaviour,
							attribute.lineNumber,
							propertyInfo.Name,
							true
						);
						if(json != null){
							propertyInfo.SetValue(target,JsonObject.objectFromJson(
								propertyInfo.PropertyType,json,false)
							);
						} 
					}
					EditorGUILayout.EndHorizontal();
				}
				--EditorGUI.indentLevel;
			} //end bPropertyFoldout
		}
		#endregion
	//----------------------------------------------------------------------------------
	}
	public static void bakeJson(string scriptPath,int lineNumber,string fieldName,
		Type type,object value,bool bBlockStatement,bool bDelayedCall=false)
	{
		bakeFieldText(scriptPath,lineNumber,fieldName,buildJsonBakeText(type,value),bBlockStatement);
		if(bDelayedCall)
			/* In case of warning "SendMessage cannot be called during
			Awake, CheckConsistency, or OnValidate" */
			EditorApplication.delayCall += AssetDatabase.Refresh;
		else
			AssetDatabase.Refresh();
	}
	private static string buildJsonBakeText(Type type,object value){
		bool bArray = type.IsArray;
		Type elementType = bArray ? type.GetElementType() : type;
		if(!JsonObject.isSupportedType(elementType))
			return null;
		
		switch(elementType.Name){
			case nameof(Gradient): return buildJsonText<Gradient>(value,bArray);
			case nameof(AnimationCurve): return buildJsonText<AnimationCurve>(value,bArray);
		}
		return null;
	}
	private static string buildJsonText<T>(object value,bool bArray){
		return "JsonUtility.FromJson<"
			+ typeof(JustWrapper<T[]>).Namespace+"."
			+ nameof(JustWrapper<T[]>) + "<"+typeof(T).FullName+(bArray?"[]":"")+"> >(\""
			+ JsonObject.jsonFromObject<T>(value,bArray).Replace("\"","\\\"")
			+ "\").obj"
		;
	}
	public static void bakeAllJsonFields(Object target,List<FieldInfo> lFieldInfo){
		foreach(FieldInfo fieldInfo in lFieldInfo){
			JsonBakableAttribute attribute = fieldInfo.GetCustomAttribute<JsonBakableAttribute>();
			bakeJson(
				attribute.filePath,
				attribute.lineNumber,
				fieldInfo.Name,
				fieldInfo.FieldType,
				fieldInfo.GetValue(target),
				false
			);
		}
	}
	public static void bakeAllJsonProperties(Object target,List<PropertyInfo> lPropertyInfo){
		foreach(PropertyInfo propertyInfo in lPropertyInfo){
			JsonBakableAttribute attribute = propertyInfo.GetCustomAttribute<JsonBakableAttribute>();
			bakeJson(
				attribute.filePath,
				attribute.lineNumber,
				propertyInfo.Name,
				propertyInfo.PropertyType,
				propertyInfo.GetValue(target),
				true
			);
		}
	}
	private static string readAssignedJson(MonoBehaviour monoBehaviour,int lineNumber,
		string fieldName,bool bBlockStatement)
	{
		string scriptPath = 
			AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(monoBehaviour));
		if(scriptPath==null)
			return null;
		SourceCodeFiddler sourceCodeFiddler = new SourceCodeFiddler();
		if(!sourceCodeFiddler.readFile(scriptPath))
			return null;
		sourceCodeFiddler.moveToLine(lineNumber);
		sourceCodeFiddler.moveToToken(fieldName);
		if(!bBlockStatement)
			sourceCodeFiddler.moveToEndToken();
		else //bBlockStatement
			sourceCodeFiddler.moveToCloseBrace();
		sourceCodeFiddler.moveToNextCodeChar();
		if(sourceCodeFiddler.ThisChar != '=')
			return null;
		sourceCodeFiddler.moveToChar('(');
		sourceCodeFiddler.moveToChar('{');
		string json = sourceCodeFiddler.readUntil(')');
		return json.Remove(json.Length-1).Replace("\\\"","\""); //If length<1, let throw
	}
}
#endif

#endregion
//====================================================================================

//====================================================================================
#region EDITORS

#if UNITY_EDITOR
public abstract partial class MonoBehaviourBakerEditor : Editor{
	private bool bMainFoldout = true;
	private bool bFieldFoldout = true;
	private bool bPropertyFoldout = true;
	private List<FieldInfo> lBakableFieldInfo;
	private List<ReorderableList> lBakableReorderableList;
	private List<PropertyInfo> lBakablePropertyInfo;
	private bool bAutoBake;

	bool bJsonFieldFoldout = true;
	bool bJsonPropertyFoldout =true;
	private List<FieldInfo> lJsonBakableFieldInfo;
	private List<ReorderableList> lJsonBakableReorderableList;
	private List<PropertyInfo> lJsonBakablePropertyInfo;

	protected virtual void OnEnable(){
		lBakableFieldInfo = getBakableFieldInfoList(target,out lBakableReorderableList);
		lBakablePropertyInfo = getBakablePropertyInfoList(target);
		bAutoBake = EditorPrefs.GetBool(EDITORPREFS_AUTOBAKE,false);
		FieldUndo.evOnFieldUndo -= onConstFieldUndo;
		FieldUndo.evOnFieldUndo += onConstFieldUndo;

		lJsonBakableFieldInfo = getJsonBakableFieldInfoList(target,out lJsonBakableReorderableList);
		lJsonBakablePropertyInfo = getJsonBakablePropertyInfoList(target);
	}
	public override void OnInspectorGUI(){
		DrawDefaultInspector();
		drawBakableInspector(target,lBakableFieldInfo,lBakableReorderableList,lBakablePropertyInfo,
			lJsonBakableFieldInfo,lJsonBakableReorderableList,lJsonBakablePropertyInfo,
			ref bMainFoldout,ref bFieldFoldout,ref bPropertyFoldout,ref bJsonFieldFoldout,
			ref bJsonPropertyFoldout,ref bAutoBake
		);
	}
	public static void drawBakableInspector(Object target,List<FieldInfo> lBakableFieldInfo,
		List<ReorderableList> lBakableReorderableList,List<PropertyInfo> lBakablePropertyInfo,
		List<FieldInfo> lJsonBakableFieldInfo,List<ReorderableList> lJsonBakableReorderableList,
		List<PropertyInfo> lJsonBakablePropertyInfo,ref bool bMainFoldout,ref bool bFieldFoldout,
		ref bool bPropertyFoldout,ref bool bJsonFieldFoldout,ref bool bJsonPropertyFoldout,
		ref bool bAutoBake)
	{
		bMainFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(bMainFoldout,"Bakable Entries");
		EditorGUILayout.EndFoldoutHeaderGroup();
		if(bMainFoldout){
			bool bSaveGUIEnabled = GUI.enabled;
			EditorGUILayout.BeginHorizontal();
			bool bUserAutoBake = EditorGUILayout.Toggle("Auto Bake",bAutoBake);
			if(bUserAutoBake != bAutoBake){
				bAutoBake = bUserAutoBake;
				EditorPrefs.SetBool(EDITORPREFS_AUTOBAKE,bUserAutoBake);
			}
			GUI.enabled = !Application.isPlaying;
			if(GUILayout.Button("Bake All Now")){
				bakeAllFields(target,lBakableFieldInfo);
				bakeAllProperties(target,lBakablePropertyInfo);
				bakeAllJsonFields(target,lJsonBakableFieldInfo);
				bakeAllJsonProperties(target,lJsonBakablePropertyInfo);
			}
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();
			drawNormalBakableInspector(
				target,lBakableFieldInfo,lBakableReorderableList,lBakablePropertyInfo,
				ref bFieldFoldout,ref bPropertyFoldout,ref bAutoBake
			);
			drawJsonBakableInspector(
				target,lJsonBakableFieldInfo,lJsonBakableReorderableList,lJsonBakablePropertyInfo,
				ref bJsonFieldFoldout,ref bJsonPropertyFoldout, ref bAutoBake
			);
			GUI.enabled = bSaveGUIEnabled;
		}
	}
	protected virtual void OnDisable(){
		if(bAutoBake){
			bakeAllFields(target,lBakableFieldInfo);
			bakeAllProperties(target,lBakablePropertyInfo);
		}
		FieldUndo.evOnFieldUndo -= onConstFieldUndo;
	}
}

/* Composition is one possible way to simulate multiple inheritance (Credit: KF2 & Justin, SO) */
public class MonoBehaviourBakerEditorWithScene : EditorWithScene{
	private bool bMainFoldout = true;
	private bool bFieldFoldout = true;
	private bool bPropertyFoldout = true;
	private List<FieldInfo> lBakableFieldInfo;
	private List<ReorderableList> lBakableReorderableList;
	private List<PropertyInfo> lBakablePropertyInfo;
	private bool bAutoBake;

	bool bJsonFieldFoldout = true;
	bool bJsonPropertyFoldout =true;
	private List<FieldInfo> lJsonBakableFieldInfo;
	private List<ReorderableList> lJsonBakableReorderableList;
	private List<PropertyInfo> lJsonBakablePropertyInfo;

	protected override void OnEnable(){
		base.OnEnable();
		lBakableFieldInfo = MonoBehaviourBakerEditor.getBakableFieldInfoList(target,out lBakableReorderableList);
		lBakablePropertyInfo = MonoBehaviourBakerEditor.getBakablePropertyInfoList(target);
		bAutoBake = EditorPrefs.GetBool(MonoBehaviourBakerEditor.EDITORPREFS_AUTOBAKE,false);
		FieldUndo.evOnFieldUndo -= MonoBehaviourBakerEditor.onConstFieldUndo;
		FieldUndo.evOnFieldUndo += MonoBehaviourBakerEditor.onConstFieldUndo;

		lJsonBakableFieldInfo = MonoBehaviourBakerEditor.getJsonBakableFieldInfoList(target,out lJsonBakableReorderableList);
		lJsonBakablePropertyInfo = MonoBehaviourBakerEditor.getJsonBakablePropertyInfoList(target);
	}
	public override void OnInspectorGUI(){
		DrawDefaultInspector();
		MonoBehaviourBakerEditor.drawBakableInspector(target,lBakableFieldInfo,lBakableReorderableList,lBakablePropertyInfo,
			lJsonBakableFieldInfo,lJsonBakableReorderableList,lJsonBakablePropertyInfo,
			ref bMainFoldout,ref bFieldFoldout,ref bPropertyFoldout,ref bJsonFieldFoldout,
			ref bJsonPropertyFoldout,ref bAutoBake
		);
	}
	protected virtual void OnDisable(){
		if(bAutoBake){
			MonoBehaviourBakerEditor.bakeAllFields(target,lBakableFieldInfo);
			MonoBehaviourBakerEditor.bakeAllProperties(target,lBakablePropertyInfo);
		}
		FieldUndo.evOnFieldUndo -= MonoBehaviourBakerEditor.onConstFieldUndo;
	}
}
#endif

#endregion
//====================================================================================

} //end namespace Chameleon
