/************************************************************************
 * EXPOSABLESTATIC (v1.0)
 * by Reev the Chameleon
 * 3 Jan 2
 ************************************************************************
This is used in conjuction with ExposableMonoBehaviour. It creates
ScriptableObject that saves static data of corresponding ExposableMonoBehaviour.
This allow static data to persist even when all instances of that class is gone.
*/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Chameleon{

using SerializableFieldData = ExposableMonoBehaviour.SerializableFieldData;
using SerializablePropertyData = ExposableMonoBehaviour.SerializablePropertyData;

//ScriptableObject has to be on its own file
public class ExposableStatic : BridgeAwakenListener, ISerializationCallbackReceiver{
	public string sType; //Because Unity won't serialize System.Type
	public List<SerializableFieldData> lSavedStaticFieldData
		= new List<SerializableFieldData>();
	public List<SerializablePropertyData> lSavedStaticPropertyData
		= new List<SerializablePropertyData>();	
	private const BindingFlags BINDINGFLAGS_STATIC =
		BindingFlags.Public | BindingFlags.NonPublic |
		BindingFlags.Static | BindingFlags.FlattenHierarchy
	;

	public void OnBeforeSerialize(){}
	public void OnAfterDeserialize(){
		overwriteExposedStatic();
	}
	private void overwriteExposedStatic(){
		Type type = Type.GetType(sType);
		foreach(SerializableFieldData savedStaticFieldData in lSavedStaticFieldData){
			FieldInfo fieldInfo = 
				type?.GetField(savedStaticFieldData.field.name,BINDINGFLAGS_STATIC);
			if(fieldInfo == null)
				continue;
			object oSavedStaticField = 
				savedStaticFieldData.bridgeID == -1 ?
				savedStaticFieldData.field.Value :
				BridgeManager.get(savedStaticFieldData.bridgeID)
			;
			if(oSavedStaticField?.GetType()!=fieldInfo.FieldType)
				oSavedStaticField = null;
			fieldInfo.SetValue(null,oSavedStaticField);
		}
		foreach(SerializablePropertyData savedStaticPropertyData in lSavedStaticPropertyData){
			PropertyInfo propertyInfo = 
				type?.GetProperty(savedStaticPropertyData.property.name,BINDINGFLAGS_STATIC);
			if(propertyInfo == null)
				continue;
			object oSavedStaticProperty =
				savedStaticPropertyData.bridgeID == -1 ?
				savedStaticPropertyData.property.Value :
				BridgeManager.get(savedStaticPropertyData.bridgeID)
			;
			if(oSavedStaticProperty?.GetType()!=propertyInfo.PropertyType)
				oSavedStaticProperty = null;
			propertyInfo.SetValue(null,oSavedStaticProperty);
		}
	}
	public override void onBridgeAwaken(){
		/* Link bridges */
		Type type = Type.GetType(sType);
		foreach(SerializableFieldData savedStaticFieldData in lSavedStaticFieldData){
			if(savedStaticFieldData.bridgeID == -1)
				continue;
			object oSavedStaticField = BridgeManager.get(savedStaticFieldData.bridgeID);
			FieldInfo fieldInfo = 
				type?.GetField(savedStaticFieldData.field.name,BINDINGFLAGS_STATIC);
			if(fieldInfo == null)
				continue;
			if(oSavedStaticField?.GetType()!=fieldInfo.FieldType)
				oSavedStaticField = null;
			fieldInfo.SetValue(null,oSavedStaticField);
		}
		foreach(SerializablePropertyData savedStaticPropertyData in lSavedStaticPropertyData){
			if(savedStaticPropertyData.bridgeID == -1)
				continue;
			object oSavedStaticProperty = BridgeManager.get(savedStaticPropertyData.bridgeID);
			PropertyInfo propertyInfo = 
				type?.GetProperty(savedStaticPropertyData.property.name,BINDINGFLAGS_STATIC);
			if(propertyInfo == null)
				continue;
			if(oSavedStaticProperty?.GetType()!=propertyInfo.PropertyType)
				oSavedStaticProperty = null;
			propertyInfo.SetValue(null,oSavedStaticProperty);
		}
	}
}	

} //end namespace Chameleon

