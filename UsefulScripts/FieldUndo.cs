/*****************************************************************************
 * FIELDUNDO (v1.1.2)
 * by Reev the Chameleon
 * 2 Sep 2
******************************************************************************
Allow setting non-serialized fields and properties while also recording to Unity Undo.
Useful for custom editor that shows and allows interaction with non-serialized fields.
Note: to record Undo and retain history across edit/play mode, temporary ScriptableObject
of type FieldUndo will be generated in the script folder, which will disappear on editor exit.
Update v1.0.1: Minor code change due to change in dependency code
Update v1.0.2: Fix bugs caused by migrating codes into package
Update v1.0.3: Fix bugs about deleting temp folder
Update v1.1: Add evOnFieldUndo and code to support this feature
Update v1.1.1: Revamp code to saves string as clipboard string and allow undoing properties
Update v1.1.2: Make class public

Note: Currently there are some hardcoded path values. Will refactor later.
*/

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.IO;

using Object = UnityEngine.Object;

namespace Chameleon{

public class FieldUndo : ScriptableObject{
	private enum eBakableType{FIELD,PROPERTY};
	/* Reason we need xxx and xxx2 is so that we can redo. */
	[SerializeField] int objectID;
	[SerializeField] string fieldName;
	[SerializeField] string sValue;
	[SerializeField] int recorderID;
	[SerializeField] bool bSetValue;
	[SerializeField] eBakableType bakableType;
	[SerializeField] bool bDummyToggle;
	[SerializeField] string fieldName2;
	[SerializeField] string sValue2;
	[SerializeField] int recorderID2;
	[SerializeField] bool bSetValue2;
	[SerializeField] eBakableType bakableType2;

	public delegate void DOnFieldUndo(int recorderID,int objectID,string fieldName,string sValue);
	public static event DOnFieldUndo evOnFieldUndo;

	private bool bDummyComparer = false;
	private int undoID = -1;
	private string assetPath;
	private static FieldUndo instance;

	public static void recordSetField(Object unityObject,string fieldName,
		object oValue,string undoMessage="Field Change",int recorderID=-1,bool bSetValue=true)
	{
		recordSetFieldInfo(
			unityObject,
			unityObject?.GetType().GetField(fieldName,ReflectionHelper.BINDINGFLAGS_ALL),
			oValue,
			undoMessage,
			recorderID,
			bSetValue
		);
	}

	public static void recordSetFieldInfo(Object unityObject,FieldInfo fieldInfo,
		object oValue,string undoMessage="Field Change",int recorderID=-1,bool bSetValue=true)
	{
		if(fieldInfo==null) //can't set const fields!
			return;
		if(!instance)
			initializeInstance();
		/* Record Undo */
		if(Undo.GetCurrentGroup() != instance.undoID){
			//int objectID = unityObject.GetInstanceID();
			if(instance.fieldName != fieldInfo?.Name){
				instance.fieldName2 = instance.fieldName;
				instance.sValue2 = instance.sValue;
				instance.bakableType2 = instance.bakableType;
				instance.bSetValue2 = instance.bSetValue;
				instance.recorderID2 = instance.recorderID;
			}
			else{
				instance.fieldName2 = null;
				instance.bSetValue2 = false;
				instance.recorderID2 = -1;
			}
			instance.objectID = unityObject.GetInstanceID(); //if null, will be 0
			instance.fieldName = fieldInfo?.Name;
			instance.recorderID = recorderID;
			instance.bSetValue = bSetValue;
			instance.bakableType = eBakableType.FIELD;
			instance.sValue = EditorHelper.toClipboardString(fieldInfo.GetValue(unityObject));
			Undo.RegisterCompleteObjectUndo(instance,undoMessage);
			instance.bDummyToggle = !instance.bDummyToggle;
			instance.bDummyComparer = instance.bDummyToggle;
			instance.undoID = Undo.GetCurrentGroup();
		}
		/* Set value and propagate change to this ScriptableObject,
		otherwise Redo won't work. */
		if(bSetValue){
			//Debug.Log(JsonUtility.ToJson(new JustWrapper<Gradient>{obj=(Gradient)fieldInfo?.GetValue(unityObject)}));
			if(unityObject)
				fieldInfo.SetValue(unityObject,oValue);
			else
				fieldInfo.SetValue(null,oValue);
		}
		instance.sValue = EditorHelper.toClipboardString(oValue);
		EditorUtility.SetDirty(instance);
	}
	public static void recordSetProperty(Object unityObject,string fieldName,
		object oValue,string undoMessage="Property Change",int recorderID=-1,bool bSetValue=true)
	{
		recordSetPropertyInfo(
			unityObject,
			unityObject?.GetType().GetProperty(fieldName,ReflectionHelper.BINDINGFLAGS_ALL),
			oValue,
			undoMessage
		);
	}
	public static void recordSetPropertyInfo(Object unityObject,PropertyInfo propertyInfo,
		object oValue,string undoMessage="Property Change",int recorderID=-1,bool bSetValue=true)
	{
		if(propertyInfo==null)
			return;
		if(!instance)
			initializeInstance();
		/* Record Undo */
		if(Undo.GetCurrentGroup() != instance.undoID){
			//int objectID = unityObject.GetInstanceID();
			if(instance.fieldName != propertyInfo?.Name){
				instance.fieldName2 = instance.fieldName;
				instance.sValue2 = instance.sValue;
				instance.bakableType2 = instance.bakableType;
				instance.bSetValue2 = instance.bSetValue;
				instance.recorderID2 = instance.recorderID;
			}
			else{
				instance.fieldName2 = null;
				instance.bSetValue2 = false;
				instance.recorderID2 = -1;
			}
			instance.objectID = unityObject.GetInstanceID(); //if null, will be 0
			instance.fieldName = propertyInfo?.Name;
			instance.recorderID = recorderID;
			instance.bSetValue = bSetValue;
			instance.bakableType = eBakableType.PROPERTY;
			instance.sValue = EditorHelper.toClipboardString(propertyInfo.GetValue(unityObject));
			Undo.RegisterCompleteObjectUndo(instance,undoMessage);
			instance.bDummyToggle = !instance.bDummyToggle;
			instance.bDummyComparer = instance.bDummyToggle;
			instance.undoID = Undo.GetCurrentGroup();
		}
		/* Set value and propagate change to this ScriptableObject,
		otherwise Redo won't work. */
		if(bSetValue){
			if(unityObject)
				propertyInfo.SetValue(unityObject,oValue);
			else
				propertyInfo.SetValue(null,oValue);
		}
		instance.sValue = EditorHelper.toClipboardString(oValue);
		EditorUtility.SetDirty(instance);
	}
	private static void initializeInstance(){
		string assetPath = "Assets/chm_Temp/FieldUndoData.asset";
		/* Let throw if path not found, because it shouldn't be possible to have no path for self */
		if(!(instance = AssetDatabase.LoadAssetAtPath<FieldUndo>(assetPath))){
			if(!AssetDatabase.IsValidFolder("Assets/chm_Temp"))
				AssetDatabase.CreateFolder("Assets","chm_Temp");
			instance = ScriptableObject.CreateInstance<FieldUndo>();
			AssetDatabase.CreateAsset(instance,assetPath);
		}
		instance.bDummyComparer = instance.bDummyToggle;
		instance.assetPath = assetPath;
		instance.hideFlags = HideFlags.NotEditable;
	}
	void OnEnable(){
		EditorApplication.quitting -= onQuit;
		EditorApplication.quitting += onQuit;
	}
	void OnValidate(){
		/* If OnValidate follows undo, undo the fields, else do nothing */
		/* Haven't done property undo yet! */
		if(!instance) //MonoBehaviourBaker recompiles script, so reload just in case
			initializeInstance();
		if(bDummyComparer != bDummyToggle){
			Object unityObject = EditorUtility.InstanceIDToObject(objectID);
			if(unityObject){
				if(bSetValue){
					if(bakableType == eBakableType.FIELD){
						FieldInfo fieldInfo = unityObject.GetType().GetField(
							fieldName,ReflectionHelper.BINDINGFLAGS_ALL
						);
						fieldInfo?.SetValue(
							unityObject,
							EditorHelper.fromClipboardString(sValue,fieldInfo.FieldType)
						);
					}
					else{ //eBakableType.PROPERTY
						PropertyInfo propertyInfo = unityObject.GetType().GetProperty(
							fieldName,ReflectionHelper.BINDINGFLAGS_ALL
						);
						propertyInfo?.SetValue(
							unityObject,
							EditorHelper.fromClipboardString(sValue,propertyInfo.PropertyType)
						);
					}
				}
				if(recorderID != -1){
					evOnFieldUndo?.Invoke(
						recorderID,
						objectID,
						fieldName,
						sValue
					);
				}

				if(bSetValue2){
					if(bakableType == eBakableType.FIELD){
						FieldInfo fieldInfo = unityObject.GetType().GetField(
							fieldName2,ReflectionHelper.BINDINGFLAGS_ALL
						);
						fieldInfo?.SetValue(
							unityObject,
							EditorHelper.fromClipboardString(sValue2,fieldInfo.FieldType)
						);
					}
					else{ //eBakableType.PROPERTY
						PropertyInfo propertyInfo = unityObject.GetType().GetProperty(
							fieldName2,ReflectionHelper.BINDINGFLAGS_ALL
						);
						propertyInfo?.SetValue(
							unityObject,
							EditorHelper.fromClipboardString(sValue2,propertyInfo.PropertyType)
						);
					}
				}
				if(recorderID2 != -1){
					evOnFieldUndo?.Invoke(
						recorderID2,
						objectID,
						fieldName2,
						sValue2
					);
				}
			}
		}
		bDummyComparer = bDummyToggle;
	}
	void onQuit(){
		/* AssetDatabase.GetAssetPath(this) will return null because Unity does not
		search .tmp. Hence, we need to use path saved */
		if(assetPath != null)
			AssetDatabase.DeleteAsset(assetPath);
		if(AssetDatabase.IsValidFolder("Assets/chm_Temp"))
			AssetDatabase.DeleteAsset("Assets/chm_Temp");	
	}
}

} //end namespace Chameleon

#endif
