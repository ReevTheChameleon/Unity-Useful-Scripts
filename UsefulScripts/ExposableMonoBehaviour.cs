/************************************************************************
 * EXPOSABLEMONOBEHAVIOUR (v1.2.3)
 * by Reev the Chameleon
 * 9 Feb 2
 ************************************************************************
Having this script in the project, you can make your scripts extend
ExposableMonoBehaviour so that you can:
- Use [Expose] on any fields, properties, methods, and C# events not shown in
normal inspector to view them, including static ones. In case of methods,
this will allow you to call the method via inspector, and in case of C# events,
it will allow you to view all objects subscribed to it.
- Use [ExposeSet] on fields or properties to set them and make them retain
the value you set. For case of GameObject, you have an option to check the
bridge checkbox so it retains connection to GameObject in the scene even
when made prefab. (You are still responsible for creating and registering
the scene GameObject to the BridgeManager.) Note that you are forced to use
bridge when [ExposeSet] static GameObject because they needs to exist
regardless of whether instances of your class exist or not.
- Use [ExposeSubscribe] on methods to allow them to subscribe to C# event
on specified object via inspector.
- Use [ExposeFire] on C# events to allow firing them via inspector.
NOTE: If you want to use your own CustomEditor, you should inherit from
ExposableMonoBehaviourEditor to keep the feature.
Update v1.1: Rearrange some codes and fix issue exposeStatic not created
when adding static and hot-reload.
Update v1.2: Prevent Editor-only functions from linking into real build.
Update v1.2.1: Change EditorPath code to use function from System.IO.
Update v1.2.2: Refactor and move some code to its own file
Update v1.2.3: Minor code change due to dependency code change,
and fix version number naming mistake
*/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

using Object = UnityEngine.Object;

namespace Chameleon{

#if UNITY_EDITOR
using ExposableMonoBehaviourEditor =
	ExposableMonoBehaviour.ExposableMonoBehaviourEditor;
#endif

public enum eTypeType{ //enum does not need [Serializable] if public
	value,
	stringType,
	unity,
	unityNull,
	reference,
}

/* Unity does NOT support serialization of inheritance chain
and won't serialize generic<T>. In version 2019.3 there is
[SerializeReference] which allows one to serialize System.Object
that does NOT point to Unity.Object OR value type. Hence, in the end,
we don't have much choice, but to store both value type and Unity Object
in the same class. */
[Serializable] public sealed class SerializableType{ //TO DO: array & reference type
	public string name;
	public string sType;
	public eTypeType typeType;
	[SerializeField] Object unityObject;
	[SerializeField] string sValue;
	
	public SerializableType(string name,Type type,object value){
		if(type == null)
			type = typeof(object);
		this.name = name;
		this.sType = type.Name;

		if(typeof(Object).IsAssignableFrom(type)){
			typeType = value==null ? eTypeType.unityNull : eTypeType.unity;
			unityObject = value as Object;
			return;
		}
		if(type.IsValueType){
			typeType = eTypeType.value;
			switch(type.Name){
				case nameof(Int32):
					sValue = value!=null ? ((int)value).ToString() : "0";
					return;
				case nameof(Single):
					sValue = value!=null ? ((float)value).ToString() : "0.0";
					return;
				case nameof(Boolean):
					sValue = value!=null ? ((bool)value).ToString() : "False";
					return;
				case nameof(Vector3):
					sValue = value!=null ? ((Vector3)value).toPreciseString() : "(0,0,0)";
					return;
			}
		}
		if(type == typeof(string)){ //string is special reference type.
			typeType = eTypeType.stringType;
			sValue = value!=null ? (string)value : "";
			return;
		}

		typeType = eTypeType.reference; //implement later
		sValue = "";
	}
	public object Value{
		get{
			switch(typeType){
				case eTypeType.unityNull:
					return null;
					/* Even when we supply null, if Unity detects it is Unity type it will
					create an instance of that type and serialize it (because Unity can't
					serialize null). When that happens here, Unity will return some instance of an Object, 
					and when you try assigning that to its derived type (like GameObject) it will throw.
					Also, you can't check for Unity.Object==null or if(Unity.Object) here, as
					they (accessing Unity's bool overload) throws InvalidOperationException when called
					from another thread. It seems OnAfterDeserialize() runs in another thread,
					so if there is null comparison with Unity.Object the code can't be used
					there, so avoid that. Hence you need this enum. */
				case eTypeType.unity:
					return unityObject;
				
				case eTypeType.stringType:
					return sValue;

				case eTypeType.value:
					switch(sType) { //Catch value type first to prevent returning null value type
						case nameof(Int32):
							int intValue;
							return int.TryParse(sValue,out intValue) ? intValue : 0;
						case nameof(Single):
							float floatValue;
							return float.TryParse(sValue,out floatValue) ? floatValue : 0.0f;
						case nameof(Boolean):
							bool boolValue;
							return bool.TryParse(sValue,out boolValue) ? boolValue : false;
						case nameof(Vector3):
							Vector3 vector3Value;
							return tryParse(sValue,out vector3Value) ?
								vector3Value :
								new Vector3(0.0f,0.0f,0.0f);
					}
					break;
			}
			return unityObject; //implement later
		}
	}
	public static bool tryParse(string s,out Vector3 v){
		if(s.Length>0 && s[0]=='(' && s[s.Length-1]==')'){
			s = s.Substring(1,s.Length-2);
			string[] aComponent = s.Split(',');
			if(aComponent.Length == 3){
				if(float.TryParse(aComponent[0],out v.x) &&
					float.TryParse(aComponent[1],out v.y) &&
					float.TryParse(aComponent[2],out v.z)
				)
					return true;
			}
		}
		v = new Vector3(0.0f,0.0f,0.0f);
		return false;
	}
}

//================================================================================================================
#region ATTRIBUTES

[AttributeUsage(
	AttributeTargets.Field |
	AttributeTargets.Property |
	AttributeTargets.Method |
	AttributeTargets.Event
)]
public class ExposeAttribute : Attribute{}

[AttributeUsage(
	AttributeTargets.Field |
	AttributeTargets.Property
)]
public class ExposeSetAttribute : Attribute{}

[AttributeUsage(AttributeTargets.Event)]
public class ExposeFireAttribute : Attribute{}

[AttributeUsage(AttributeTargets.Method)]
public class ExposeSubscribeAttribute : Attribute{}
#endregion
//================================================================================================================

//================================================================================================================
#region EXPOSABLEMONOBEHAVIOUR CLASS

public abstract partial class ExposableMonoBehaviour : MonoBehaviour, ISerializationCallbackReceiver{
	public class FieldData{
		public bool bBridge;
		public int bridgeID;
		public FieldInfo fieldInfo;
	}
	public class PropertyData{
		public bool bBridge;
		public int bridgeID;
		public PropertyInfo propertyInfo;
	}
	private class ParameterSignature{ //data structure called "parallel arrays" or "structure of arrays"
		public int parameterCount;
		public ParameterInfo[] aParameterInfo;
		public object[] aValue;
		public ParameterSignature(ParameterInfo[] aParameterInfo){
			if(aParameterInfo != null){
				parameterCount = aParameterInfo.Length;
				this.aParameterInfo = aParameterInfo;
				aValue = new object[parameterCount];
			}
		}
	}
	private class MethodSignature{
		public MethodInfo methodInfo;
		public object returnValue;
		public ParameterSignature parameterSignature;
		public int indexSubscriber; //index in lExposedSubscriptionInfo, -1 if not subscribing
		public MethodSignature(MethodInfo methodInfo){
			this.methodInfo = methodInfo;
			ParameterInfo[] aParameterInfo = methodInfo.GetParameters();
			if(aParameterInfo?.Length > 0)
				this.parameterSignature = new ParameterSignature(aParameterInfo);
			indexSubscriber = -1;
		}
		public bool isEventCompatible(object testObject,string eventName){
			MethodInfo eventMethodInfo = 
				testObject?.GetType().GetEvent(eventName,ReflectionHelper.BINDINGFLAGS_ALL) //OK even if eventName==null
				?.EventHandlerType.GetMethod("Invoke",ReflectionHelper.BINDINGFLAGS_ALL)
			;
			//Check return value
			if(eventMethodInfo?.ReturnType != methodInfo?.ReturnType)
				return false;
			
			//Check length
			ParameterInfo[] aEventParameterInfo = eventMethodInfo.GetParameters();
			if((parameterSignature==null || parameterSignature.parameterCount==0) && 
				aEventParameterInfo.Length == 0)
				return true;
			if(aEventParameterInfo.Length != parameterSignature?.parameterCount)
				return false;
			
			//Check signature compatibility
			for(int i=0; i<parameterSignature.parameterCount; ++i){
				if(aEventParameterInfo[i].ParameterType !=
					parameterSignature.aParameterInfo[i].ParameterType
				){
					return false;
				}
			}
			return true;
		}
	}
	private class SubscribedEvent{
		public bool bBridge;
		public int bridgeID; //-1 for null bridge
		public object target;
		public string[] aCompatibleEventName;
		public int indexSelected; //-1 if not selected
		public string eventName;
	}
	private class SubscriptionInfo{
		public MethodInfo methodInfo;
		public List<SubscribedEvent> lSubscribedEvent;
	}
	private class EventSignature{
		public EventInfo eventInfo;
		public FieldInfo backingFieldInfo;
		public ParameterSignature parameterSignature;
		public EventSignature(EventInfo eventInfo,bool bNeedParameter=true){
			this.eventInfo = eventInfo;
			/* We are making serious assumption here that backing field of an event
			will have the same name as event itself, which is normally the case
			If this is not the case, this approach will break, the event will behave
			strangely, (like user cannot call Invoke() or call it directly) and no amount
			of reflection will be able to solve it. */
			this.backingFieldInfo =
				eventInfo.DeclaringType.GetField(eventInfo.Name,ReflectionHelper.BINDINGFLAGS_ALL);
			if(bNeedParameter){
				parameterSignature = new ParameterSignature(backingFieldInfo?.FieldType.GetMethod(
					"Invoke",ReflectionHelper.BINDINGFLAGS_ALL)?.GetParameters());
				/* These steps should always succeed. If not, parameterSignature has count of 0.
				and void arrays. */
			}
		}
	}
	
	//Consider changing to struct later.
	[Serializable] public class SerializableFieldData{ //has to be public for use in ExposableStatic
		public int bridgeID;
		public SerializableType field;
	}
	[Serializable] public class SerializablePropertyData{
		public int bridgeID;
		public SerializableType property;
	}
	[Serializable] private class SerializableMethodArgument{
		public string methodName;
		public List<SerializableType> lArgument;
	}
	[Serializable] private class SerializableEventArgument{
		public string eventName;
		public List<SerializableType> lArgument;
	}
	[Serializable] private class SerializableEvent{
		public int bridgeID;
		public SerializableType target;
		public string eventName;
	}
	[Serializable] private class SerializableSubscriptionInfo{
		public string methodName;
		public List<SerializableEvent> lSubscribedEvent;
	}

//------------------------------------------------------------------------------------------------------------
	#region EDITOR FOLDOUT STATE
	#if UNITY_EDITOR
	[SerializeField][HideInInspector] private bool bMainFoldout = true;
	[SerializeField][HideInInspector] private bool bFieldFoldout = true;
	[SerializeField][HideInInspector] private bool bPropertyFoldout = true;
	[SerializeField][HideInInspector] private bool bMethodFoldout = true;
	[SerializeField][HideInInspector] private bool bEventFoldout = true;
	#endif
	#endregion
//------------------------------------------------------------------------------------------------------------
	
	private List<FieldData> lExposedFieldData = new List<FieldData>();
	private List<PropertyData> lExposedPropertyData = new List<PropertyData>();
	private List<MethodSignature> lExposedMethodSignature = new List<MethodSignature>();
	private List<EventSignature> lExposedEventSignature = new List<EventSignature>();
	private List<SubscriptionInfo> lExposedSubscriptionInfo = new List<SubscriptionInfo>();

	[SerializeField][HideInInspector] List<SerializableFieldData> lSavedFieldData
		= new List<SerializableFieldData>();
	[SerializeField][HideInInspector] List<SerializablePropertyData> lSavedPropertyData
		= new List<SerializablePropertyData>(); //This is the saved state, not including change between session
	[SerializeField][HideInInspector] List<SerializableMethodArgument> lSavedMethodArgument
		= new List<SerializableMethodArgument>();
	[SerializeField][HideInInspector] List<SerializableEventArgument> lSavedEventArgument
		= new List<SerializableEventArgument>();
	[SerializeField][HideInInspector] List<SerializableSubscriptionInfo> lSavedSubscriptionInfo
		= new List<SerializableSubscriptionInfo>();

	[SerializeField][HideInInspector] ExposableStatic exposableStatic = null;
	#pragma warning disable 0414
	/* In real build, this variable is not used and will generate warning. However,
	due to its presence in if-else statement, it is difficult to remove without
	altering large portion of code so decide to disable warning for now. May look for
	better alternatives later. */
	private bool bHasStatic = false;
	#pragma warning restore 0414
	[SerializeField][HideInInspector] private bool bFake = true;
	private bool bFakeStaticOperation = false;
	/* This is because when Reset() is clicked, OnBeforeSerialize() is called on FAKE
	object where everything is default value, then OnAfterDeserialize() and Reset()
	is called. This breaks the code because it serializes default value and deserializes
	it back, causing it to reset value. This cause the problem for statics which
	reset effect is undesirable. We thus create this and set it to false in
	Reset() because it is guaranteed to be called before OnBeforeSerialize()
	when default script is attached (by drag-drop), whereas when right-click reset 
	Reset() is called AFTER OnBeforeSerialized, and so we can check this variable
	to determine which kinds of default script we are getting, and chose to
	not process static if it is a fake. */

	public virtual void OnBeforeSerialize(){
		/* This is called frequently when object is selected on editor
		(approx. once per frame); try to trim down as much as possible.
		IsDirty is true if someone calls SetDirty. For default inspector case,
		it seems the flag is dirty everytime you change its value.
		For our inspector, when something in our control changes,
		we need to write code to set it dirty ourselves. */
		#if UNITY_EDITOR
		if(!EditorUtility.IsDirty(this))
			return;
		#endif

		if(bFake){
			/* To make sure that right-click reset will clear INSTANCE exposed member */
			OnAfterDeserialize();
			bFakeStaticOperation = true;
		}

		#if UNITY_EDITOR
		string currentFolder = EditorPath.getCurrentFolder();
		if(bHasStatic){
			if(!exposableStatic &&
				!(exposableStatic = AssetDatabase.LoadAssetAtPath<ExposableStatic>(
					currentFolder + "/ExposableStatic/" + this.GetType().Name + ".asset")
			)){
				exposableStatic = ScriptableObject.CreateInstance<ExposableStatic>();
				exposableStatic.sType = this.GetType().Name;
				exposableStatic.hideFlags = HideFlags.NotEditable;
				if(!AssetDatabase.IsValidFolder(currentFolder + "/ExposableStatic"))
					AssetDatabase.CreateFolder(currentFolder,"ExposableStatic");
				AssetDatabase.CreateAsset(
					exposableStatic,
					currentFolder + "/ExposableStatic/"+ this.GetType().Name + ".asset"
				);
			}
		}
		else{ //No statics, destroy reference
			AssetDatabase.DeleteAsset(
				currentFolder + "/ExposableStatic/"+ this.GetType().Name + ".asset"
			);
			exposableStatic = null;
		}
		#endif
		
		bool bExposableHasBridge = false;
	//--------------------------------------------------------------------------------------------------------
		#region FIELD
		lSavedFieldData.Clear();
		if(exposableStatic)
			exposableStatic.lSavedStaticFieldData.Clear();
		foreach(FieldData fieldData in lExposedFieldData){
			if(!fieldData.fieldInfo.IsDefined(typeof(ExposeSetAttribute)))
				continue;
			SerializableFieldData savedFieldData = new SerializableFieldData();
			if(fieldData.fieldInfo.IsStatic && exposableStatic){ //consider optimize, as second should always true (cannot?)
				savedFieldData.field = new SerializableType(
					fieldData.fieldInfo.Name,
					fieldData.fieldInfo.FieldType,
					fieldData.fieldInfo.GetValue(null)
				);
				if(typeof(GameObject).IsAssignableFrom(fieldData.fieldInfo.FieldType)){ //can be in the scene
					/* In case of static, fieldData.bridgeID will be meaningless because it is NOT
					initialized. The reason is that this is stored in ExposableStatic asset, and 
					trying to retrieve its value in OnAfterDeserialize() does NOT work because
					you cannot guarantee that ExposableStatic has completely deserialized when you
					ask for value in OnAfterDeserialize(). We thus find bridgeID just at when
					we are about to save it here. */
					savedFieldData.bridgeID =
						BridgeManager.findID(fieldData.fieldInfo.GetValue(null) as GameObject);
					bExposableHasBridge = true;
				}
				else
					savedFieldData.bridgeID = fieldData.bridgeID;
				exposableStatic.lSavedStaticFieldData.Add(savedFieldData);
				#if UNITY_EDITOR
				EditorUtility.SetDirty(exposableStatic);
				#endif
			}
			else{ //instance field
				savedFieldData.field = new SerializableType(
					fieldData.fieldInfo.Name,
					fieldData.fieldInfo.FieldType,
					fieldData.fieldInfo.GetValue(this)
				);
				savedFieldData.bridgeID = fieldData.bridgeID;
				/* Even if bBridge, if no object selected, bridgeID will be -1,
				and will convert to non-bridge with no set value when deserialized. */
				lSavedFieldData.Add(savedFieldData);
			}
		} //end foreach fieldData
		#endregion
	//--------------------------------------------------------------------------------------------------------
		#region PROPERTY
		lSavedPropertyData.Clear();
		if(exposableStatic)
			exposableStatic.lSavedStaticPropertyData.Clear();
		foreach(PropertyData propertyData in lExposedPropertyData){
			if(!propertyData.propertyInfo.IsDefined(typeof(ExposeSetAttribute)))
				continue;
			SerializablePropertyData savedPropertyData = new SerializablePropertyData();
			if(propertyData.propertyInfo.GetMethod.IsStatic && exposableStatic){
				savedPropertyData.property = new SerializableType(
					propertyData.propertyInfo.Name,
					propertyData.propertyInfo.PropertyType,
					propertyData.propertyInfo.GetValue(null)
				);
				if(typeof(GameObject).IsAssignableFrom(propertyData.propertyInfo.PropertyType)){
					savedPropertyData.bridgeID =
						BridgeManager.findID(propertyData.propertyInfo.GetValue(null) as GameObject);
					bExposableHasBridge = true;
				}
				else
					savedPropertyData.bridgeID = propertyData.bridgeID;
				exposableStatic.lSavedStaticPropertyData.Add(savedPropertyData);
				#if UNITY_EDITOR
				EditorUtility.SetDirty(exposableStatic);
				#endif
			}
			else{
				savedPropertyData.property = new SerializableType(
					propertyData.propertyInfo.Name,
					propertyData.propertyInfo.PropertyType,
					propertyData.propertyInfo.GetValue(this)
				);
				savedPropertyData.bridgeID = propertyData.bridgeID;
				lSavedPropertyData.Add(savedPropertyData);
			}
		}
		#endregion
	//--------------------------------------------------------------------------------------------------------
		#region METHOD
		/* Separate these 2 lists about method because only one
		is used in real build. */
		lSavedMethodArgument.Clear();
		foreach(MethodSignature methodSignature in lExposedMethodSignature){
			if(methodSignature.parameterSignature?.parameterCount > 0){
				SerializableMethodArgument savedMethodArgument = new SerializableMethodArgument();
				savedMethodArgument.methodName = methodSignature.methodInfo.Name;
				savedMethodArgument.lArgument = new List<SerializableType>();
				ParameterSignature parameterSignature = methodSignature.parameterSignature;
				for(int i=0; i<parameterSignature.parameterCount; ++i){
					savedMethodArgument.lArgument.Add(new SerializableType(
						parameterSignature.aParameterInfo[i].Name,
						parameterSignature.aParameterInfo[i].ParameterType,
						parameterSignature.aValue[i]
					));
				}
				lSavedMethodArgument.Add(savedMethodArgument);
			}
		}
			
		lSavedSubscriptionInfo.Clear();
		foreach(SubscriptionInfo subscriptionInfo in lExposedSubscriptionInfo){
			if(subscriptionInfo.lSubscribedEvent?.Count > 0){
				SerializableSubscriptionInfo savedSubscriptionInfo = new SerializableSubscriptionInfo();
				savedSubscriptionInfo.methodName = subscriptionInfo.methodInfo.Name;
				savedSubscriptionInfo.lSubscribedEvent = new List<SerializableEvent>();
				foreach(SubscribedEvent subscribedEvent in subscriptionInfo.lSubscribedEvent){
					if(subscribedEvent.bBridge){
						SerializableEvent savedEvent = new SerializableEvent();
						savedEvent.bridgeID = subscribedEvent.bridgeID;
						savedEvent.eventName = subscribedEvent.eventName;
						savedSubscriptionInfo.lSubscribedEvent.Add(savedEvent);
					}
					else{ //not bridge
						if(subscribedEvent.indexSelected == -1 ||
							subscribedEvent.indexSelected >= subscribedEvent.aCompatibleEventName.Length)
							continue;
						SerializableEvent savedEvent = new SerializableEvent();
						savedEvent.bridgeID = -1;
						savedEvent.target = new SerializableType(
							"", //Not applicable
							subscribedEvent.target?.GetType(),
							subscribedEvent.target
						);
						savedEvent.eventName = subscribedEvent.eventName;
						savedSubscriptionInfo.lSubscribedEvent.Add(savedEvent);
					}
				}
				lSavedSubscriptionInfo.Add(savedSubscriptionInfo);
			}
		}
		#endregion
	//--------------------------------------------------------------------------------------------------------
		#region EVENT
		lSavedEventArgument.Clear();
		foreach(EventSignature eventSignature in lExposedEventSignature){
			if(!eventSignature.eventInfo.IsDefined(typeof(ExposeFireAttribute)) ||
				eventSignature.parameterSignature.parameterCount == 0)
				continue;
			SerializableEventArgument savedEventArgument = new SerializableEventArgument();
			savedEventArgument.eventName = eventSignature.eventInfo.Name;
			savedEventArgument.lArgument = new List<SerializableType>();
			ParameterSignature parameterSignature = eventSignature.parameterSignature;
			for(int i=0; i<parameterSignature.parameterCount; ++i){
				Type parameterType = parameterSignature.aParameterInfo[i].ParameterType;
				savedEventArgument.lArgument.Add(new SerializableType(
					parameterType.Name, //event argument will show up as type
					parameterType,
					parameterSignature.aValue[i]
				));
			}
			lSavedEventArgument.Add(savedEventArgument);
		}
		#endregion
	//--------------------------------------------------------------------------------------------------------
		if(bExposableHasBridge) //Even null exposableStatic is OK.
			BridgeManager.registerAwaken(exposableStatic);

		#if UNITY_EDITOR
		EditorUtility.ClearDirty(this);
		#endif
	}
	public virtual void OnAfterDeserialize(){
		/* This may be called while ExposableStatic still deserializing (wrong value),
		so we will deal only with instance data. Static data will be done in OnAfterDeserialize()
		of ExposableStatic itself. */
		bHasStatic = false;
	//--------------------------------------------------------------------------------------------------------
		#region FIELD
		lExposedFieldData.Clear();
		foreach(FieldInfo fieldInfo in this.GetType().GetFields(ReflectionHelper.BINDINGFLAGS_ALL)){
			bool bExposeSet;
			/* For fields, we only show them exposed only if they are not already shown
			in the inspector. While it makes sense, another reason is the technical
			difficulty that if a field is shown is both places, when changing the one
			in normal inspector the exposed one will not be dirty and not serialized in
			OnBeforeSerialize(). Hence when OnAfterDeserialize() is called after value
			has changed (Unity does OnBeforeSerialize() and OnAfterDeserialize() in
			succession when value changed in inspector), it will reset. Thus we avoid this
			to avoid the conflict. */
			if(((bExposeSet=fieldInfo.IsDefined(typeof(ExposeSetAttribute))) ||
					fieldInfo.IsDefined(typeof(ExposeAttribute))) &&
				(fieldInfo.IsStatic ||
					(fieldInfo.IsPublic && fieldInfo.IsDefined(typeof(HideInInspector))) ||
					(fieldInfo.IsPrivate && !fieldInfo.IsDefined(typeof(SerializeField))))
			){
				FieldData fieldData = new FieldData();
				fieldData.fieldInfo = fieldInfo;
				fieldData.bridgeID = -1;
				fieldData.bBridge = false;
				lExposedFieldData.Add(fieldData);
				if(bExposeSet){
					if(fieldInfo.IsStatic && !bFakeStaticOperation)
						bHasStatic = true;
						/* Do not refer to ExposableStatic here because it may not have
						completed deserialization yet. */
					else{
						int index = lSavedFieldData.FindIndex(f =>
							f.field.name == fieldInfo.Name &&
							f.field.sType == fieldInfo.FieldType.Name
						);
						if(index != -1){ //found
							fieldData.bridgeID = lSavedFieldData[index].bridgeID;
							fieldData.bBridge = fieldData.bridgeID!=-1;
							/* This will give wrong value for bridge value the first time, but it is
							necessary for undo in editor mode. May consider omit/better approach later
							to optimize real build. */
							object savedObject = 
								fieldData.bBridge ?
								BridgeManager.get(fieldData.bridgeID) :
								lSavedFieldData[index].field.Value
							;
							if(savedObject?.GetType() != fieldInfo.FieldType)
								savedObject = null;
							fieldInfo.SetValue(
								this,
								savedObject
							);
							
						}
					}
				}
			}
		}
		#endregion
	//--------------------------------------------------------------------------------------------------------
		#region PROPERTY
		lExposedPropertyData.Clear();
		foreach(PropertyInfo propertyInfo in this.GetType().GetProperties(ReflectionHelper.BINDINGFLAGS_ALL)){
			bool bExposeSet;
			if((bExposeSet=propertyInfo.IsDefined(typeof(ExposeSetAttribute))) ||
				propertyInfo.IsDefined(typeof(ExposeAttribute))
			){
				PropertyData propertyData = new PropertyData();
				propertyData.propertyInfo = propertyInfo;
				propertyData.bridgeID = -1;
				propertyData.bBridge = false;
				lExposedPropertyData.Add(propertyData);
				if(bExposeSet){
					if(propertyInfo.GetMethod.IsStatic && !bFakeStaticOperation)
						bHasStatic = true;
					else{
						int index = lSavedPropertyData.FindIndex(p =>
							p.property.name==propertyInfo.Name &&
							p.property.sType==propertyInfo.PropertyType.Name
						);
						if(index!=-1 && propertyInfo.CanWrite){ //found and can write
							propertyData.bridgeID = lSavedPropertyData[index].bridgeID;
							propertyData.bBridge = propertyData.bridgeID!=-1;
							/* If we delete the object and Unity detects it, it points reference
							to Object fake null instead. If the field accept derived type like
							GameObject, it will throw when SetValue because it cannot convert
							fake Object to GameObject (fake Object real type is not GameObject).
							This handles that by ensuring that all Unity fake nulls are set as null.
							Because we CANNOT check null in OnAfterDeserialize(), we compare type instead. */
							object savedObject =
								propertyData.bBridge ?
								BridgeManager.get(propertyData.bridgeID) :
								lSavedPropertyData[index].property.Value
							;
							if(savedObject?.GetType() != propertyInfo.PropertyType)
								savedObject = null;
							propertyInfo.SetValue(
								this,
								savedObject
							);
						}
					}
				} //end bExposeSet
			} //end loop propertyInfo
		}
		#endregion
	//--------------------------------------------------------------------------------------------------------
		#region METHOD
		lExposedMethodSignature.Clear();
		/* Refresh all subscriptions by unsubscribing all the list, refresh the list,
		and do subscription again. But in real build, we will delay all event subscription
		until OnEnable because OnAfterDeserialize() is called many times. In editor mode,
		it is necessary to see result on the inspector, but on real build it is not
		necessary and cost performance. (Note: Application.isPlaying can't be called here,
		so we will check condition with UNITY_EDITOR. */
		#if UNITY_EDITOR
		resetSubscription(true);
		#endif

		lExposedSubscriptionInfo.Clear();
		foreach(MethodInfo methodInfo in this.GetType().GetMethods(ReflectionHelper.BINDINGFLAGS_ALL)){
			bool bSubscribable;
			if((bSubscribable=methodInfo.IsDefined(typeof(ExposeSubscribeAttribute))) ||
				methodInfo.IsDefined(typeof(ExposeAttribute))
			){
				MethodSignature methodSignature = new MethodSignature(methodInfo);
				ParameterSignature parameterSignature = methodSignature.parameterSignature;
				lExposedMethodSignature.Add(methodSignature);
				
				if(parameterSignature?.parameterCount > 0){
					int indexMethod = lSavedMethodArgument.FindIndex(savedMethod =>
						savedMethod.methodName==methodInfo.Name
					);
					if(indexMethod != -1){
						List<SerializableType> lArgument = lSavedMethodArgument[indexMethod].lArgument;
						for(int i=0; i<parameterSignature.parameterCount; ++i){
							int indexArgument = 
								lArgument.FindIndex(argument =>
									argument.name==parameterSignature.aParameterInfo[i].Name &&
									argument.sType==parameterSignature.aParameterInfo[i].ParameterType.Name
								)
							;
							if(indexArgument != -1)
								parameterSignature.aValue[i] = lArgument[indexArgument].Value;
						}
					}
				}
					
				if(bSubscribable){
					SubscriptionInfo subscriptionInfo = new SubscriptionInfo();
					subscriptionInfo.methodInfo = methodInfo;
					subscriptionInfo.lSubscribedEvent = new List<SubscribedEvent>();
					lExposedSubscriptionInfo.Add(subscriptionInfo);
					methodSignature.indexSubscriber = lExposedSubscriptionInfo.Count-1;

					int indexMethod = lSavedSubscriptionInfo.FindIndex(savedSubscription =>
						savedSubscription.methodName == methodInfo.Name
					);
					if(indexMethod != -1){
						List<SerializableEvent> lSavedEvent =
							lSavedSubscriptionInfo[indexMethod].lSubscribedEvent;
						foreach(SerializableEvent savedEvent in lSavedEvent){
							object oEventTarget = 
								savedEvent.bridgeID == -1 ?
								savedEvent.target?.Value :
								null 
								/* BridgeManager.get(savedEvent.bridgeID) will be unreliable here
								because BridgeManager itself may not be ready at this point. */
							;
							EventInfo[] aEventInfo = oEventTarget?.GetType().GetEvents(ReflectionHelper.BINDINGFLAGS_ALL);
							if(aEventInfo==null && savedEvent.bridgeID==-1) //No event and not bridge
								continue;
							SubscribedEvent subscribedEvent = new SubscribedEvent();
							subscriptionInfo.lSubscribedEvent.Add(subscribedEvent);
							subscribedEvent.target = oEventTarget;
							subscribedEvent.bridgeID = savedEvent.bridgeID;
							subscribedEvent.bBridge = savedEvent.bridgeID!=-1;
							subscribedEvent.indexSelected = -1;
							subscribedEvent.eventName = savedEvent.eventName;
							if(savedEvent.bridgeID == -1){
								List<string> lCompatibleEventName = new List<string>();
								for(int i=0; i<aEventInfo.Length; ++i){
									if(methodSignature.isEventCompatible(
										oEventTarget,
										aEventInfo[i].Name)
									){
										lCompatibleEventName.Add(aEventInfo[i].Name);
										if(aEventInfo[i].Name == savedEvent.eventName)
											subscribedEvent.indexSelected = lCompatibleEventName.Count-1;
									}
								}
								subscribedEvent.aCompatibleEventName = lCompatibleEventName.ToArray();
							}
							else
								subscribedEvent.aCompatibleEventName = new string[0];
							/* At this point, we skip bridge events and they will have
							indexSelected of -1. */
						}
					}
				} //end if(bSubscribe)
			}
		} //end MethodData iteration
		#if UNITY_EDITOR
		foreach(SubscriptionInfo subscriptionInfo in lExposedSubscriptionInfo){
			foreach(SubscribedEvent subscribedEvent in subscriptionInfo.lSubscribedEvent){
				if(subscribedEvent.indexSelected != -1){
					EventReflection.safeSubscribe(
						this,
						subscriptionInfo.methodInfo,
						subscribedEvent.bridgeID==-1 ?
							subscribedEvent.target :
							BridgeManager.get(subscribedEvent.bridgeID)
						,
						subscribedEvent.aCompatibleEventName[subscribedEvent.indexSelected]
					);
				}
			}
		}
		#endif
		#endregion
	//--------------------------------------------------------------------------------------------------------
		#region EVENT
		lExposedEventSignature.Clear();
		foreach(EventInfo eventInfo in this.GetType().GetEvents(ReflectionHelper.BINDINGFLAGS_ALL)){
			bool bExposeFire;
			if((bExposeFire=eventInfo.IsDefined(typeof(ExposeFireAttribute))) ||
				eventInfo.IsDefined(typeof(ExposeAttribute))
			){
				EventSignature eventSignature = new EventSignature(eventInfo,bExposeFire);
				lExposedEventSignature.Add(eventSignature);
				if(bExposeFire){
					int indexEvent = lSavedEventArgument.FindIndex(argument =>
						argument.eventName == eventInfo.Name);
					if(indexEvent != -1){
						List<SerializableType> lArgument = lSavedEventArgument[indexEvent].lArgument;
						ParameterSignature parameterSignature = eventSignature.parameterSignature;
						for(int i=0; i<parameterSignature.parameterCount; ++i){
							int indexArgument = lArgument.FindIndex(argument =>
								argument.sType==parameterSignature.aParameterInfo[i].ParameterType.Name
							);
							if(indexArgument != -1)
								parameterSignature.aValue[i] = lArgument[indexArgument].Value;
						}
					}
				} //end bExposeFire
			}
		} //end EventInfo iteration
		#endregion
	//--------------------------------------------------------------------------------------------------------
		bFake = false;
	}	
	private void linkBridge(){
		/* In OnAfterDeserialize(), saved data are deserialized, but the target of 
		bridge cannot yet be retrieved because we cannot be sure whether BridgeManager
		has complete its initialization or not. This function which find the bridge
		target will be called after that is ensured (Awake() for play mode, and
		OnGUIEnable() for editor. */
	//--------------------------------------------------------------------------------------------------------
		#region FIELD
		foreach(FieldData fieldData in lExposedFieldData){
			if(!fieldData.bBridge)
				continue;
			object oField = BridgeManager.get(fieldData.bridgeID);
			if(oField?.GetType()==fieldData.fieldInfo.FieldType)
				fieldData.fieldInfo.SetValue(this,oField);
		}
		#endregion
	//--------------------------------------------------------------------------------------------------------
		#region PROPERTY
		foreach(PropertyData propertyData in lExposedPropertyData){
			if(!propertyData.bBridge)
				continue;
			object oProperty = BridgeManager.get(propertyData.bridgeID);
			if(oProperty?.GetType()==propertyData.propertyInfo.PropertyType)
				propertyData.propertyInfo.SetValue(this,oProperty);
		}
		#endregion
	//--------------------------------------------------------------------------------------------------------
		#region METHOD SUBSCRIPTION    
		foreach(SubscriptionInfo subscriptionInfo in lExposedSubscriptionInfo){
			for(int i=subscriptionInfo.lSubscribedEvent.Count-1; i>=0; --i){
				SubscribedEvent subscribedEvent = subscriptionInfo.lSubscribedEvent[i];
				if(!subscribedEvent.bBridge)
					continue;
				object oEventTarget = BridgeManager.get(subscribedEvent.bridgeID);
				EventInfo[] aEventInfo = oEventTarget?.GetType().GetEvents(ReflectionHelper.BINDINGFLAGS_ALL);
				if(aEventInfo==null){
					subscriptionInfo.lSubscribedEvent.RemoveAt(i);		
					continue;
				}
				subscribedEvent.target = oEventTarget;
				List<string> lCompatibleEventName = new List<string>();
				for(int j=0; j<aEventInfo.Length; ++j){
					if(subscriptionInfo.methodInfo.isEventCompatible(
						oEventTarget,
						aEventInfo[j].Name)
					){
						lCompatibleEventName.Add(aEventInfo[j].Name);
						if(aEventInfo[j].Name == subscribedEvent.eventName)
							subscribedEvent.indexSelected = lCompatibleEventName.Count-1;
					}
				}
				subscribedEvent.aCompatibleEventName = lCompatibleEventName.ToArray();
			}
		}
		#endregion
	//--------------------------------------------------------------------------------------------------------	
	}
	protected virtual void Awake(){
		linkBridge();
	}
	protected virtual void OnEnable(){
		/* Do Subscription */
		foreach(SubscriptionInfo subscriptionInfo in lExposedSubscriptionInfo){
			foreach(SubscribedEvent subscribedEvent in subscriptionInfo.lSubscribedEvent){
				if(subscribedEvent.indexSelected != -1){
					/* Prevent double subscription due to OnAfterDeserialize(). May consider
					adding check condition for more performance if necessary. */
					EventReflection.safeSubscribe( 
						this,
						subscriptionInfo.methodInfo,
						subscribedEvent.target,
						subscribedEvent.aCompatibleEventName[subscribedEvent.indexSelected]
					);
				}
			}
		}
	}
	protected virtual void OnDisable(){
		/* Unsubscribe */
		foreach(SubscriptionInfo subscriptionInfo in lExposedSubscriptionInfo){
			foreach(SubscribedEvent subscribedEvent in subscriptionInfo.lSubscribedEvent){
				if(subscribedEvent.indexSelected != -1){
					EventReflection.unsubscribe(
						this,
						subscriptionInfo.methodInfo,
						subscribedEvent.target,
						subscribedEvent.aCompatibleEventName[subscribedEvent.indexSelected]
					);
				}
			}
		}
	}

	#if UNITY_EDITOR
	private void resetSubscription(bool bReload=false){
		foreach(SubscriptionInfo subscriptionInfo in lExposedSubscriptionInfo){
			foreach(SubscribedEvent subscribedEvent in subscriptionInfo.lSubscribedEvent){
				if(subscribedEvent.indexSelected != -1){
					EventReflection.unsubscribe(
						this,
						subscriptionInfo.methodInfo,
						subscribedEvent.target,
						subscribedEvent.aCompatibleEventName[subscribedEvent.indexSelected]
					);
				}
			}
			if(!bReload)
				/* If not reload, shouldn't clear lExposedSubscriptionInfo because slot still needed.
				This happens in Reset(). */
				subscriptionInfo.lSubscribedEvent.Clear();
		}
	}
	protected virtual void Reset(){
		resetSubscription();
		OnAfterDeserialize(); //bFake will be set false here
	}
	#endif
}
#endregion
//================================================================================================================

//================================================================================================================
#region EXPOSABLEMONOBEHAVIOUR EDITOR

#if UNITY_EDITOR
public abstract partial class ExposableMonoBehaviour : MonoBehaviour{

[CustomEditor(typeof(ExposableMonoBehaviour),true)]
public class ExposableMonoBehaviourEditor : Editor{
	private readonly GUIContent bridgeToggleTooltipStyle = new GUIContent(
		"","Tick if target is to be accessed via bridge (useful for prefabs)." +
		"Target must be registered to BridgeManager first"
	);
	private ExposableMonoBehaviour targetAsExposable;

	protected virtual void OnEnable(){
		targetAsExposable = target as ExposableMonoBehaviour;
		if(!EditorApplication.isPlayingOrWillChangePlaymode){
			/* For switching back from play mode. In play mode and build will use Awake().
			The reason to use this rather than Application.isPlaying is because when you
			click play, Editor's OnEnable() seems to be called twice. The first time is
			called regardless of whether the object is selected or not, and Application.isPlaying
			will always return false for that, while this function will return true. */
			targetAsExposable.linkBridge();
			if(targetAsExposable.exposableStatic)
				targetAsExposable.exposableStatic.onBridgeAwaken();
		}
	}
	public override void OnInspectorGUI(){
		DrawDefaultInspector();

		EditorGUILayout.Space();
		targetAsExposable.bMainFoldout =
			EditorGUILayout.BeginFoldoutHeaderGroup(targetAsExposable.bMainFoldout,"Exposed Members");
		EditorGUILayout.EndFoldoutHeaderGroup();
		if(!targetAsExposable.bMainFoldout)
			return;

		List<FieldData> lFieldData = targetAsExposable.lExposedFieldData;
		if(lFieldData.Count > 0){
			targetAsExposable.bFieldFoldout =
				EditorGUILayout.BeginFoldoutHeaderGroup(targetAsExposable.bFieldFoldout,"Variables");
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(targetAsExposable.bFieldFoldout){
				foreach(FieldData fieldData in lFieldData){
					drawField(
						target,
						fieldData,
						fieldData.fieldInfo.IsDefined(typeof(ExposeSetAttribute))
					);
				}
			}
		}

		List<PropertyData> lPropertyData = targetAsExposable.lExposedPropertyData;
		if(lPropertyData.Count > 0){
			targetAsExposable.bPropertyFoldout =
				EditorGUILayout.BeginFoldoutHeaderGroup(targetAsExposable.bPropertyFoldout,"Properties");
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(targetAsExposable.bPropertyFoldout){
				foreach(PropertyData propertyData in lPropertyData){
					drawProperty(
						target,
						propertyData,
						propertyData.propertyInfo.IsDefined(typeof(ExposeSetAttribute))
					);
				}
			}
		}

		List<MethodSignature> lMethodSignature = targetAsExposable.lExposedMethodSignature;
		if(lMethodSignature.Count > 0){
			targetAsExposable.bMethodFoldout =
				EditorGUILayout.BeginFoldoutHeaderGroup(targetAsExposable.bMethodFoldout,"Methods");
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(targetAsExposable.bMethodFoldout){
				foreach(MethodSignature methodSignature in lMethodSignature)
					drawMethod(target,methodSignature);
			}
		}

		List<EventSignature> lEventSignature = targetAsExposable.lExposedEventSignature;
		if(lEventSignature.Count > 0){
			targetAsExposable.bEventFoldout = 
				EditorGUILayout.BeginFoldoutHeaderGroup(targetAsExposable.bEventFoldout,"Events");
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(targetAsExposable.bEventFoldout){
				foreach(EventSignature eventSignature in lEventSignature){
					drawEvent(
						target,
						eventSignature,
						eventSignature.eventInfo.IsDefined(typeof(ExposeFireAttribute))
					);
				}
			}
		}
	}
	private void drawField(object oTarget,FieldData fieldData,bool bSet){
		EditorGUILayout.BeginHorizontal();
		bool bPrefab = EditorUtility.IsPersistent(this.target);
		bool bStatic = fieldData.fieldInfo.IsStatic;
		bool bAllowBridgeToggle = BridgeManager.isInitialized() && !bPrefab && !bStatic;
		bool bGameObject = typeof(GameObject).IsAssignableFrom(fieldData.fieldInfo.FieldType);
		bool bSavedEnable = GUI.enabled;
		if(bGameObject)
			fieldData.bBridge = fieldData.bBridge || bStatic || bPrefab;
		else
			fieldData.bBridge = false;
		if(bGameObject){
			GUI.enabled = bAllowBridgeToggle;
			bool bBridge = EditorGUILayout.Toggle(
				fieldData.bBridge,
				GUILayout.Width(15.0f)
			);
			GUI.enabled = bSavedEnable;
			EditorGUI.LabelField(GUILayoutUtility.GetLastRect(),bridgeToggleTooltipStyle);
			if(bBridge!=fieldData.bBridge){
				Undo.RecordObject(target,"Toggle field bridge mode");
				object oValue = fieldData.fieldInfo.GetValue(this.target);
				if(bBridge && oValue!=null){
					fieldData.bridgeID = BridgeManager.findID(oValue as Object);
					if(fieldData.bridgeID == -1)
						fieldData.fieldInfo.SetValue(this.target,null);
				}
				else{ //switch from bridge to normal, must set bridgeID otherwise prefab not working as expected
					fieldData.bridgeID = -1;
				}
				fieldData.bBridge = bBridge;
				EditorUtility.SetDirty(this.target);
			}
		}
		else
			GUILayout.Space(18.0f);

		string fieldName = bStatic ? "(static) " : "";
		fieldName += fieldData.fieldInfo.Name;
		object userValue;
		if(bGameObject && bStatic && !BridgeManager.isInitialized())
			bSet = false;
		if(!drawType(
			fieldName + " (" + fieldData.fieldInfo.FieldType.Name + ")",
			fieldData.fieldInfo.FieldType,
			fieldData.fieldInfo.GetValue(oTarget),
			bSet,
			out userValue
		)){
			if(fieldData.bBridge){
				int userBridgeID = BridgeManager.findID(userValue as Object);
				if(userBridgeID==-1 && userValue!=null){
					Debug.LogWarning("Target has not been registered to BridgeManager!");
					return;
				}
				fieldData.bridgeID = userBridgeID;
			}
			else{
				if(userValue?.GetType() != fieldData.fieldInfo.FieldType)
					userValue = null;
			}
			fieldData.fieldInfo.SetValue(oTarget,userValue);
			if(bStatic && targetAsExposable.exposableStatic){
				Undo.RecordObject(
					targetAsExposable.exposableStatic,"Exposed static field change");
			}
			else
				Undo.RecordObject(target,"Exposed field change");		
			EditorUtility.SetDirty(target);
		}
		EditorGUILayout.EndHorizontal();
	}
	private void drawProperty(object oTarget,PropertyData propertyData,bool bSet){
		EditorGUILayout.BeginHorizontal();
		bool bPrefab = EditorUtility.IsPersistent(this.target);
		bool bStatic = propertyData.propertyInfo.GetMethod.IsStatic;
		bool bAllowBridgeToggle = BridgeManager.isInitialized() && !bPrefab && !bStatic;
		bool bGameObject = typeof(GameObject).IsAssignableFrom(propertyData.propertyInfo.PropertyType);
		bool bSavedEnable = GUI.enabled;
		propertyData.bBridge = propertyData.bBridge || bStatic || bPrefab;
		if(bGameObject){
			GUI.enabled = bAllowBridgeToggle;
			bool bBridge = EditorGUILayout.Toggle(
				propertyData.bBridge,
				GUILayout.Width(15.0f)
			);
			GUI.enabled = bSavedEnable;
			EditorGUI.LabelField(GUILayoutUtility.GetLastRect(),bridgeToggleTooltipStyle);
			if(bBridge != propertyData.bBridge){
				Undo.RecordObject(target,"Toggle property bridge mode");
				object oValue = propertyData.propertyInfo.GetValue(this.target);
				if(bBridge && oValue!=null){
					propertyData.bridgeID = BridgeManager.findID(oValue as Object);
					if(propertyData.bridgeID == -1)
						propertyData.propertyInfo.SetValue(this.target,null);
				}
				else //switch from bridge to normal, must set bridgeID otherwise prefab not working as expected
					propertyData.bridgeID = -1;
				propertyData.bBridge = bBridge;
				EditorUtility.SetDirty(this.target);
			}
		}
		else
			GUILayout.Space(18.0f);

		string propertyName = bStatic ? "(static) " : "";	
		propertyName += propertyData.propertyInfo.Name;
		object userValue;
		if(bGameObject && bStatic && !BridgeManager.isInitialized())
			bSet = false;
		if(!drawType(
			propertyName + " (" + propertyData.propertyInfo.PropertyType.Name + ")",
			propertyData.propertyInfo.PropertyType,
			propertyData.propertyInfo.GetValue(oTarget),	
			bSet && propertyData.propertyInfo.CanWrite,
			out userValue
		)){
			if(propertyData.bBridge){
				int userBridgeID = BridgeManager.findID(userValue as Object);
				if(userBridgeID == -1 && userValue!=null){
					Debug.LogWarning("Target has not been registered to BridgeManager!");
					return;
				}
				propertyData.bridgeID = userBridgeID;
			}
			else{
				if(userValue?.GetType() != propertyData.propertyInfo.PropertyType)
					userValue = null;
			}
			propertyData.propertyInfo.SetValue(oTarget,userValue);
			if(bStatic && targetAsExposable.exposableStatic){
				Undo.RecordObject(
					targetAsExposable.exposableStatic,"Exposed static property change"
				);
			}
			else
				Undo.RecordObject(target,"Exposed property change");
			EditorUtility.SetDirty(target);
		}
		EditorGUILayout.EndHorizontal();
	}
	private void drawMethod(object oTarget,MethodSignature methodSignature){
		MethodInfo methodInfo = methodSignature.methodInfo;
		ParameterSignature parameterSignature = methodSignature.parameterSignature;

		EditorGUILayout.BeginHorizontal();
		if(GUILayout.Button("Call",GUILayout.Width(50.0f)))
			/* MSDN says that uninitialized element in argument array will be represented by
			its default value, so even with unknown types that cannot be shown in inspector,
			default values will be used. */
			methodSignature.returnValue = methodInfo.Invoke(oTarget,parameterSignature?.aValue);
		string methodName = methodInfo.IsStatic ? "(static) " : "";
		methodName += methodInfo.Name;
		string sMethodSignature = methodInfo.ReturnType.Name + "(";
		for(int i=0; i<parameterSignature?.parameterCount; ++i)
			sMethodSignature += parameterSignature.aParameterInfo[i].ParameterType.Name + ",";
		int indexLastComma = sMethodSignature.LastIndexOf(',');
		if(indexLastComma != -1)
			sMethodSignature = sMethodSignature.Substring(0,indexLastComma) + ")";
		else
			sMethodSignature += ")";
		GUIStyle guiStyle = new GUIStyle(EditorStyles.label);
		guiStyle.richText = true;
		EditorGUILayout.LabelField(
			"<b>"+methodName+"</b>  -  "+sMethodSignature,
			guiStyle
		);
		EditorGUILayout.EndHorizontal();

		++EditorGUI.indentLevel;
		for(int i=0; i<parameterSignature?.parameterCount; ++i){
			object userValue;
			Type parameterType = parameterSignature.aParameterInfo[i].ParameterType;
			if(!drawType(
				parameterSignature.aParameterInfo[i].Name + " (" + parameterType.Name + ")",
				parameterType,
				parameterSignature.aValue[i],
				true,
				out userValue
			) && userValue != null)
			{
				Undo.RecordObject(target,"Exposed method argument change");
				parameterSignature.aValue[i] = userValue;
				EditorUtility.SetDirty(target);
			}
		}
		if(methodInfo.ReturnType != typeof(void)){
			object dummy;
			drawType(
				"Return (" + methodInfo.ReturnType.Name + ")",
				methodInfo.ReturnType,
				methodSignature.returnValue,
				false,
				out dummy
			);
		}
		--EditorGUI.indentLevel;
	//--------------------------------------------------------------------------------------------------------
		#region EXPOSESUBSCRIBE
		/* NOT allow undoing because cannot track event subscription effectively 
		UPDATE: Because OnAfterDeserialize() is called for every value change, including
		undo, and correct subscription reconstruction logic is implemented there,
		subscription unexpectedly undoes correctly. However, it is then revealed that
		actually changing value or undoing in inspector is quite costly because
		all logics in OnAfterDeserialize() is called.
		For now, only undo for adding/removing events from subscribing list is supported
		because it is still being considered how to handle target/event index change. */
		int indexSubscriber = methodSignature.indexSubscriber;
		if(indexSubscriber != -1){ //If this method can subscribe, heavy work is needed
			SubscriptionInfo subscriptionInfo = 
				targetAsExposable.lExposedSubscriptionInfo[indexSubscriber];
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Subscribed to: ");
			if(GUILayout.Button("+")){
				Undo.RegisterCompleteObjectUndo(target,"Add subscribed event");
				/* Because this list is not serializable, if use Undo.RecordObject,
				it will not distinguish adding each element, and always undo to
				the state of empty list. */
				subscriptionInfo.lSubscribedEvent.Add(new SubscribedEvent{
					aCompatibleEventName = new string[0],
					target = null,
					indexSelected = -1,
					bridgeID = -1
				});
				EditorUtility.SetDirty(this.target);
			}
			EditorGUILayout.EndHorizontal();

			int indexRemove = -1;
			List<SubscribedEvent> lSubscribedEvent = subscriptionInfo.lSubscribedEvent;
			bool bPrefab = EditorUtility.IsPersistent(this.target);
			/* If target is prefab, will not allow adding non-bridge events */
			bool bAllowBridgeToggle = BridgeManager.isInitialized() && !bPrefab;
			bool bSavedEnable = GUI.enabled;
			for(int i=0; i<lSubscribedEvent.Count; ++i){
				SubscribedEvent subscribedEvent = lSubscribedEvent[i];
				GUI.enabled = bAllowBridgeToggle;
				EditorGUILayout.BeginHorizontal();
				bool bBridge = EditorGUILayout.Toggle(
					subscribedEvent.bBridge || bPrefab,
					GUILayout.Width(15.0f)
				);
				GUI.enabled = bSavedEnable;
				/* This is brilliant idea from Moohasha, UA! Because you can only show
				tooltip on label of control, to show it DIRECTLY over the toggle, you
				create a label of the same size that contains the tooltip and place it 
				over the toggle. To find the position of last control, use
				GUILayoutUtility.GetLastRect(). This cannot be used for the first item
				in the group because it needs preceeding control's rect. (Credit: Radivarig, UA) */
				EditorGUI.LabelField(GUILayoutUtility.GetLastRect(),bridgeToggleTooltipStyle);
				if(bBridge != subscribedEvent.bBridge){
					if(bBridge && subscribedEvent.target!=null){
						//User tick bridge checkbox with non-null target
						subscribedEvent.bridgeID = 
							BridgeManager.findID(subscribedEvent.target as MonoBehaviour);
						if(subscribedEvent.bridgeID == -1){
							if(subscribedEvent.indexSelected != -1){
								EventReflection.unsubscribe(
									this.target,
									methodInfo,
									subscribedEvent.target,
									subscribedEvent.aCompatibleEventName[subscribedEvent.indexSelected]
								);
							}
							subscribedEvent.target = null;
							subscribedEvent.aCompatibleEventName = new string[0];
							subscribedEvent.indexSelected = -1;
							subscribedEvent.eventName = "";
						}
					}
					//UNSUBSCRIBE IF TICK OFF!
					subscribedEvent.bBridge = bBridge;
					EditorUtility.SetDirty(this.target);
				}
				
				Object userObjectValue = EditorGUILayout.ObjectField(
					subscribedEvent.bBridge ?
						BridgeManager.get(subscribedEvent.bridgeID) as MonoBehaviour :
						subscribedEvent.target as MonoBehaviour //Force MonoBehaviour field for now
					, 
					typeof(MonoBehaviour),
					true
				);
				if(userObjectValue != subscribedEvent.target as Object){
					if(bBridge && userObjectValue!=null){ //Using bridge and select non-null object
						int userBridgeID = BridgeManager.findID(userObjectValue);
						if(userBridgeID == -1){
							Debug.LogWarning("Target has not been registered to BridgeManager!");
							continue;
						}
						subscribedEvent.bridgeID = userBridgeID;
					}
					if(subscribedEvent.indexSelected != -1){
						EventReflection.unsubscribe(
							this.target,
							methodInfo,
							subscribedEvent.target,
							subscribedEvent.aCompatibleEventName[subscribedEvent.indexSelected]
						);
					}
					subscribedEvent.target = userObjectValue;
					subscribedEvent.indexSelected = -1;
					//subscribedEvent.eventName = "";
					/* If object starts disabled, its Awake() is deferred until its first enabling,
					so if that disabled object is selected in inspector, this will be
					called before bridges are set, and so index will be unevitably -1. However,
					We keep the eventName intact so that it can correctly link to bridge once
					enabled for the first time */
					List<string> lCompatibleEventName = new List<string>(); //consider making class member for performance
					EventInfo[] aEventInfo =
						userObjectValue?.GetType().GetEvents();
						/* Not using BindingFlags.NonPublic here because even if you can
						list out private events, you can't subscribe to it as it will throw
						InvalidOperationException because the event has no public add method. */
					for(int j=0; j<aEventInfo?.Length; ++j){
						if(methodSignature.isEventCompatible(
							userObjectValue,
							aEventInfo[j].Name)
						){
							lCompatibleEventName.Add(aEventInfo[j].Name);
						}
					}
					string[] aCompatibleEventName = 
						subscribedEvent.aCompatibleEventName = lCompatibleEventName.ToArray();
					EditorUtility.SetDirty(this.target);
				}
				int indexSelected = subscribedEvent.indexSelected;
				int userIndexSelected = EditorGUILayout.Popup(
					indexSelected,subscribedEvent.aCompatibleEventName);
				if(userIndexSelected != indexSelected){
					if(indexSelected != -1){
						EventReflection.unsubscribe(
							this.target,
							methodInfo,
							userObjectValue,
							subscribedEvent.aCompatibleEventName[indexSelected]
						);
					}
					EventReflection.subscribe(
						this.target,
						methodInfo,
						userObjectValue,
						subscribedEvent.aCompatibleEventName[userIndexSelected]
					);
					subscribedEvent.indexSelected = userIndexSelected;
					subscribedEvent.eventName =
						subscribedEvent.aCompatibleEventName[userIndexSelected];
					EditorUtility.SetDirty(this.target);
				}
				if(GUILayout.Button("-")){
					Undo.RegisterCompleteObjectUndo(target,"Remove subscribed event");
					indexRemove = i;
				}
				EditorGUILayout.EndHorizontal();
			} //end lSubscribedEvent (event list) iteration
			if(indexRemove != -1){
				if(lSubscribedEvent[indexRemove].indexSelected != -1){
					EventReflection.unsubscribe(	
						this.target,
						methodInfo,
						lSubscribedEvent[indexRemove].target,
						lSubscribedEvent[indexRemove].aCompatibleEventName[lSubscribedEvent[indexRemove].indexSelected]
					);
				}
				lSubscribedEvent.RemoveAt(indexRemove);
				EditorUtility.SetDirty(this.target);
			}
		} //end if subscribed
		#endregion
	//--------------------------------------------------------------------------------------------------------
		EditorGUILayout.Space();
	}
	private void drawEvent(object oTarget,EventSignature eventSignature,bool bFire){
		ParameterSignature parameterSignature = eventSignature.parameterSignature;
		MulticastDelegate backingField =
			eventSignature.backingFieldInfo.GetValue(oTarget) as MulticastDelegate;
		
		bool bSavedGUIEnabled = GUI.enabled;
		EditorGUILayout.BeginHorizontal();
		GUI.enabled = bFire;
		if(GUILayout.Button("Fire",GUILayout.Width(50.0f))
			&& backingField!=null)
		{
			foreach(Delegate subscriber in backingField.GetInvocationList()){
				subscriber.Method.Invoke(
					subscriber.Target,
					eventSignature.parameterSignature?.aValue
				);
			}
		}
		GUI.enabled = bSavedGUIEnabled;
		string eventName = eventSignature.eventInfo.AddMethod.IsStatic ? "(static) " : "";
		eventName += eventSignature.eventInfo.Name;
		MethodInfo eventHandlerMethodInfo =
			eventSignature.eventInfo.EventHandlerType.GetMethod("Invoke",ReflectionHelper.BINDINGFLAGS_ALL);
		string sMethodSignature = eventHandlerMethodInfo?.ReturnType.Name + "(";
		ParameterInfo[] aParameterInfo = eventHandlerMethodInfo?.GetParameters();
		for(int i=0; i<aParameterInfo?.Length; ++i)
			sMethodSignature += aParameterInfo[i].ParameterType.Name + ",";
		int indexLastComma = sMethodSignature.LastIndexOf(',');
		if(indexLastComma != -1)
			sMethodSignature = sMethodSignature.Substring(0,indexLastComma) + ")";
		else
			sMethodSignature += ")";
		GUIStyle guiStyle = new GUIStyle(EditorStyles.label);
		guiStyle.richText = true;
		EditorGUILayout.LabelField(
			"<b>"+eventName+"</b>  -  "+sMethodSignature,
			guiStyle
		);
		EditorGUILayout.EndHorizontal();
		
		if(bFire){
			for(int i=0; i<parameterSignature.parameterCount; ++i){
				object userValue;
				Type parameterType = parameterSignature.aParameterInfo[i].ParameterType;
				if(!drawType(
					parameterSignature.aParameterInfo[i].Name + " (" + parameterType.Name +")",
					parameterType,
					parameterSignature.aValue[i],
					true,
					out userValue
				) && userValue != null){
					Undo.RecordObject(target,"Exposed event argument change");
					parameterSignature.aValue[i] = userValue;
					EditorUtility.SetDirty(target);
				}
			}
		}
			
		EditorGUILayout.LabelField("Subscribers: ");
		if(backingField == null){
			EditorGUILayout.Space();
			return;
		}
		GUI.enabled = false;
		++EditorGUI.indentLevel;
		foreach(Delegate subscriber in backingField.GetInvocationList()){
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.ObjectField(
				subscriber.Target as Object,
				subscriber.Target.GetType(),
				true
			);
			MethodInfo methodInfo = subscriber.Method;
			string sMethodDeclaration = //Consider whether worth using StringBuilder or not
				methodInfo.ReturnType.Name+" "
				+ methodInfo.Name+"("
			;
			foreach(ParameterInfo parameterInfo in methodInfo.GetParameters()){
				sMethodDeclaration += 
					parameterInfo.ParameterType.Name
					+ parameterInfo.Name + ","
				;
			}
			sMethodDeclaration = sMethodDeclaration.TrimEnd(',') + ")";
			EditorGUILayout.TextField(sMethodDeclaration);
			EditorGUILayout.EndHorizontal();
		}
		GUI.enabled = bSavedGUIEnabled;
		EditorGUILayout.Space();
		--EditorGUI.indentLevel;
	}
	public static bool drawType(string name,Type type,object value,bool bSet,out object outValue){
		bool bSavedGUIEnabled = GUI.enabled;
		GUI.enabled = bSet;

		if(typeof(Object).IsAssignableFrom(type)){
			Object unityObjectValue = value as Object;
			Object unityObjectUserValue = EditorGUILayout.ObjectField(name,unityObjectValue,type,true);
			outValue = unityObjectUserValue;
			GUI.enabled = bSavedGUIEnabled;
			return unityObjectValue==unityObjectUserValue;
		}

		//This way allows switching among Types (Credit: Joey Adams, SO)
		bool bReturn;
		switch(type.Name){
			case nameof(Int32):
				int intValue = value as int? ?? 0;
				int intUserValue = EditorGUILayout.IntField(name,intValue);
				outValue = intUserValue;
				bReturn = intUserValue==intValue;
				break;
			case nameof(Single):
				float floatValue = value as float? ?? 0.0f;
				float floatUserValue = EditorGUILayout.FloatField(name,floatValue);
				outValue = floatUserValue;
				bReturn = floatUserValue==floatValue;
				break;
			case nameof(String):
				string stringValue = value as string ?? "";
				string stringUserValue = EditorGUILayout.DelayedTextField(name,stringValue);
				outValue = stringUserValue;
				bReturn = stringValue==stringUserValue;
				break;
			case nameof(Boolean):
				bool boolValue = value as bool? ?? false;
				bool boolUserValue = EditorGUILayout.Toggle(name,boolValue);
				outValue = boolUserValue;
				bReturn = boolValue==boolUserValue;
				break;
			case nameof(Vector3):
				Vector3 vector3Value = value as Vector3? ?? Vector3.zero;
				Vector3 vector3UserValue = EditorGUILayout.Vector3Field(name,vector3Value);
				outValue = vector3UserValue;
				bReturn = vector3Value==vector3UserValue;
				break;
			default:
				outValue = null;
				bReturn = true; //infering that nothing has changed value because draw failed.
				break;
		}
		GUI.enabled = bSavedGUIEnabled;
		return bReturn;
	}
}

}

#endif //UNITY_EDITOR

#endregion
//================================================================================================================

//================================================================================================================
#region EXPOSABLEMONOBEHAVIOUR EDITOR WITH SCENE
#if UNITY_EDITOR
public class ExposableMonoBehaviourEditorWithScene : ExposableMonoBehaviourEditor{
	private List<FieldInfo> lFieldInfoShowPosition = new List<FieldInfo>();
	private List<FieldInfo> lFieldInfoShowAxis = new List<FieldInfo>();
	private List<PropertyInfo> lPropertyInfoShowPosition = new List<PropertyInfo>();
	private List<PropertyInfo> lPropertyInfoShowAxis = new List<PropertyInfo>();
	private bool bMonoBehaviour;
	private GUIStyle labelStyle;

	protected override void OnEnable(){
		base.OnEnable();
		if(!(bMonoBehaviour=typeof(MonoBehaviour).IsAssignableFrom(target.GetType())))
			return; //Only allow MonoBehaviour for now
		labelStyle = new GUIStyle();
	//---------------------------------------------------------------------------------
		#region FIELD
		foreach(FieldInfo fieldInfo in target.GetType().GetFields(
			BindingFlags.Public | BindingFlags.NonPublic |
			BindingFlags.Instance | BindingFlags.Static |
			BindingFlags.FlattenHierarchy
		)){
			if(fieldInfo.IsDefined(typeof(ShowPositionAttribute)) &&
				fieldInfo.FieldType==typeof(Vector3))
				lFieldInfoShowPosition.Add(fieldInfo);
				
			if(fieldInfo.IsDefined(typeof(ShowAxisAttribute)) &&
				fieldInfo.FieldType==typeof(float))
				lFieldInfoShowAxis.Add(fieldInfo);
		}
		#endregion
	//---------------------------------------------------------------------------------
		#region PROPERTY
		foreach(PropertyInfo propertyInfo in target.GetType().GetProperties(
			BindingFlags.Public | BindingFlags.NonPublic |
			BindingFlags.Instance | BindingFlags.Static |
			BindingFlags.FlattenHierarchy
		)){
			if(propertyInfo.IsDefined(typeof(ShowPositionAttribute)) &&
				propertyInfo.PropertyType==typeof(Vector3))
				lPropertyInfoShowPosition.Add(propertyInfo);
				
			if(propertyInfo.IsDefined(typeof(ShowAxisAttribute)) &&
				propertyInfo.PropertyType==typeof(float))
				lPropertyInfoShowAxis.Add(propertyInfo);
		}
		#endregion
	//---------------------------------------------------------------------------------
	}
	protected virtual void OnSceneGUI(){
		if(!bMonoBehaviour)
			return;	
	//---------------------------------------------------------------------------------
		#region SHOWPOSITIONATTRIBUTE
		//Fields
		foreach(FieldInfo fieldInfo in lFieldInfoShowPosition){
			ShowPositionAttribute attribute =
				fieldInfo.GetCustomAttribute<ShowPositionAttribute>();
			object oPosition = fieldInfo.GetValue(target);
			if(oPosition == null)
				return;
			Vector3 v3Position = oPosition as Vector3? ?? new Vector3();
			if(attribute.bBlackLabel)
				labelStyle.normal.textColor = Color.black;
			Handles.Label(v3Position,attribute.label,labelStyle);
			EditorGUI.BeginChangeCheck();
			v3Position = Handles.PositionHandle(v3Position,Quaternion.identity);
			if(EditorGUI.EndChangeCheck()){
				Undo.RecordObject(target,fieldInfo.Name+" Change");
				fieldInfo.SetValue(target,v3Position);
			}
		}
		//Properties
		foreach(PropertyInfo propertyInfo in lPropertyInfoShowPosition){
			ShowPositionAttribute attribute =
				propertyInfo.GetCustomAttribute<ShowPositionAttribute>();
			object oPosition = propertyInfo.GetValue(target);
			if(oPosition == null)
				return;
			Vector3 v3Position = oPosition as Vector3? ?? new Vector3();
			if(attribute.bBlackLabel)
				labelStyle.normal.textColor = Color.black;
			Handles.Label(v3Position,attribute.label,labelStyle);
			EditorGUI.BeginChangeCheck();
			v3Position = Handles.PositionHandle(v3Position,Quaternion.identity);
			if(EditorGUI.EndChangeCheck()){
				Undo.RecordObject(target,propertyInfo.Name+" Change");
				propertyInfo.SetValue(target,v3Position);
			}
		}
		#endregion
	//---------------------------------------------------------------------------------
		#region SHOWAXISATTRIBUTE
		//Fields
		foreach(FieldInfo fieldInfo in lFieldInfoShowAxis){
			ShowAxisAttribute attribute =
				fieldInfo.GetCustomAttribute<ShowAxisAttribute>();
			object oValue = fieldInfo.GetValue(target);
			if(oValue == null)
				return;
			float value = oValue as float? ?? 0.0f;
			Vector3 v3Position;
			if(attribute.bBlackLabel)
				labelStyle.normal.textColor = Color.black;
			switch(attribute.axis){
				case eAxis.x: v3Position = new Vector3(value,0.0f,0.0f); break;
				case eAxis.y: v3Position = new Vector3(0.0f,value,0.0f); break;
				case eAxis.z: v3Position = new Vector3(0.0f,0.0f,value); break;
				default: v3Position = new Vector3(); break;
			}
			Handles.Label(v3Position,attribute.label,labelStyle);
			EditorGUI.BeginChangeCheck();
			v3Position = Handles.PositionHandle(v3Position,Quaternion.identity);
			if(EditorGUI.EndChangeCheck()){
				Undo.RecordObject(target,fieldInfo.Name+" Change");
				switch(attribute.axis){
					case eAxis.x: fieldInfo.SetValue(target,v3Position.x); break;
					case eAxis.y: fieldInfo.SetValue(target,v3Position.y); break;
					case eAxis.z: fieldInfo.SetValue(target,v3Position.z); break;
				}
			}
		}
		//Properties
		foreach(PropertyInfo propertyInfo in lPropertyInfoShowAxis){
			ShowAxisAttribute attribute =
				propertyInfo.GetCustomAttribute<ShowAxisAttribute>();
			object oValue = propertyInfo.GetValue(target);
			if(oValue == null)
				return;
			float value = oValue as float? ?? 0.0f;
			Vector3 v3Position;
			if(attribute.bBlackLabel)
				labelStyle.normal.textColor = Color.black;
			switch(attribute.axis){
				case eAxis.x: v3Position = new Vector3(value,0.0f,0.0f); break;
				case eAxis.y: v3Position = new Vector3(0.0f,value,0.0f); break;
				case eAxis.z: v3Position = new Vector3(0.0f,0.0f,value); break;
				default: v3Position = new Vector3(); break;
			}
			Handles.Label(v3Position,attribute.label,labelStyle);
			EditorGUI.BeginChangeCheck();
			v3Position = Handles.PositionHandle(v3Position,Quaternion.identity);
			if(EditorGUI.EndChangeCheck()){
				Undo.RecordObject(target,propertyInfo.Name+" Change");
				switch(attribute.axis){
					case eAxis.x: propertyInfo.SetValue(target,v3Position.x); break;
					case eAxis.y: propertyInfo.SetValue(target,v3Position.y); break;
					case eAxis.z: propertyInfo.SetValue(target,v3Position.z); break;
				}
			}
		}
		#endregion
	//---------------------------------------------------------------------------------
	}
}
#endif
#endregion
//================================================================================================================

public static class EventReflection{
	/* These 2 methods DO NOT check compatibility! */
	public static bool subscribe(object oSubscriber,MethodInfo methodInfo,object oTarget,string eventName){
		if(methodInfo == null)
			return false;
		EventInfo eventInfo = oTarget?.GetType().GetEvent(eventName);
		if(eventInfo == null)
			return false;
		eventInfo.AddEventHandler(
			oTarget,
			Delegate.CreateDelegate(eventInfo.EventHandlerType,oSubscriber,methodInfo) //Credit: Daniel Bruckner, SO
		);
		/* Consider whether to catch & process Exceptions or not:
			- Throw ArgumentNullException if methodInfo is null
			- Can throw ArgumentException (but unlikely in this function)
			- Throw MissingMethodException if "Invoke" not found
			- Can throw MethodAccessException */
		return true;
	}
	public static bool unsubscribe(object oSubscriber,MethodInfo methodInfo,object oTarget,string eventName){
		if(methodInfo == null)
			return false;
		EventInfo eventInfo = oTarget?.GetType().GetEvent(eventName);
		if(eventInfo == null)
			return false;
		eventInfo.RemoveEventHandler
			(oTarget,
			Delegate.CreateDelegate(eventInfo.EventHandlerType,oSubscriber,methodInfo)
		);
		return true;
	}
	public static bool safeSubscribe(object oSubscriber,MethodInfo methodInfo,object oTarget,string eventName){
		//Credit: alf, SO, but still NOT safe enough for multithreading.
		if(!unsubscribe(oSubscriber,methodInfo,oTarget,eventName))
			return false;
		return subscribe(oSubscriber,methodInfo,oTarget,eventName);
	}
	public static bool isEventCompatible(this MethodInfo methodInfo,object testObject,string eventName){
		MethodInfo eventMethodInfo = 
			testObject?.GetType().GetEvent(eventName) //OK even if eventName==null
			?.EventHandlerType.GetMethod("Invoke",ReflectionHelper.BINDINGFLAGS_ALL)
		;
		//Check return value
		if(eventMethodInfo?.ReturnType != methodInfo?.ReturnType)
			return false;
			
		//Check length
		ParameterInfo[] aEventParameterInfo = eventMethodInfo.GetParameters();
		ParameterInfo[] aMethodParameterInfo = methodInfo.GetParameters();
		if(aEventParameterInfo.Length != aMethodParameterInfo.Length)
			return false;
			
		//Check signature compatibility
		for(int i=0; i<aMethodParameterInfo.Length; ++i){
			if(aEventParameterInfo[i].ParameterType !=
				aMethodParameterInfo[i].ParameterType
			){
				return false;
			}
		}
		return true;
	}
}

} //end namespace Chameleon
