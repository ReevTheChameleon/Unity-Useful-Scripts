/**************************************************************
 * INPUTACTIONID (v1.0.3)
 * by Reev the Chameleon
 * 3 Oct 2
***************************************************************
This class is designed to avoid hardcoding InputAction's name
in the script. User can assign InputAction via the inspector
and access it via Id property of this class
Usage:
1. Declare variable of type InputActionID in the script
2. Attach the script to a GameObject with PlayerInput Component
3. Assign InputActionID via the inspector. The field should show a
list of available InputAction found in PlayerInput Component
4. You can access the assigned InputAction by code, for example:
playerInput.actions[actionIDRed.Id].performed += func;
func must be in the form of void func(InputAction.CallbackContext context).
You can query input value from context.ReadValue<T>();
5. You should NOT forget to unsubscribe in OnDisable().

Update v1.0.1: Add #define for .asmdef Version Define check
Update v1.0.2: Revise usage instruction and remove some unnecessary code
Update v1.0.3: Add implicit operator casting the class to sGuid
*/

#if CHM_INPUTSYSTEM_PRESENT

#if ENABLE_INPUT_SYSTEM

using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
using UnityEditor;
#endif

namespace Chameleon{

[Serializable]
public class InputActionID{
	[SerializeField] private string sGuid;
	public string Id{ get{return sGuid;} }
	public static implicit operator string(InputActionID inputActionID){
		return inputActionID.sGuid;
	}
	public static implicit operator bool(InputActionID inputActionID){
		return inputActionID.sGuid!=null;
	}
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(InputActionID))]
class InputActionIDDrawer : PropertyDrawer{
	private PlayerInput playerInput;
	public override void OnGUI(
		Rect position,SerializedProperty property,GUIContent label)
	{
		Rect originalPos = position;
		//position.width -= 50.0f;
		if(!playerInput &&
			!(playerInput=((Component)(property.serializedObject.targetObject)).
				GetComponent<PlayerInput>()))
		{
			drawError(position,label.text,"<No PlayerInput>");
			//EditorGUI.LabelField(position,"<No PlayerInput>",EditorStyles.textField);
			return;
		}
		if(!playerInput.actions){
			drawError(position,label.text,"<No InputActionAsset>");
			return;
		}
		
		SerializedProperty spGuid = property.FindPropertyRelative("sGuid");
		string sGuid = spGuid.stringValue;
		InputAction inputAction = 
			sGuid==null || sGuid.Length==0 ?
			null :
			playerInput.actions.FindAction(new Guid(sGuid))
		;
		string sActionName =
			inputAction==null ?
			"<none>" :
			inputAction.actionMap.name + "/" + inputAction.name
		;
		EditorGUI.LabelField(position,label);
		position.x += EditorGUIUtility.labelWidth;
		position.width = originalPos.width-EditorGUIUtility.labelWidth-50.0f;
		EditorGUI.LabelField(position,sActionName,EditorStyles.textField);
		
		position.x = originalPos.xMax-50.0f;
		position.width = 50.0f;
		if(GUI.Button(position,"Select")){
			GenericMenu menuSelect = new GenericMenu();
			foreach(InputAction action in playerInput.actions){
				menuSelect.AddItem(
					new GUIContent(action.actionMap.name+"/"+action.name),
					false,
					() => {
						spGuid.stringValue = action.id.ToString();
						spGuid.serializedObject.ApplyModifiedProperties();
					}
				);
			}
			/* Consider allowing user to clear by pressing delete key rather than/in addition to
			having to choose from select menu. */
			menuSelect.AddSeparator("");
			menuSelect.AddItem(
				new GUIContent("Clear"),
				false,
				() => {
					spGuid.stringValue = "";
					spGuid.serializedObject.ApplyModifiedProperties();
				}
			);
			menuSelect.DropDown(position);
		}
	}
	private void drawError(Rect position,string label,string message){
		Rect originalPos = position;
		EditorGUI.LabelField(position,label);
		position.x += EditorGUIUtility.labelWidth;
		position.width = originalPos.width-EditorGUIUtility.labelWidth-24.0f;
		bool bSavedEnabled = GUI.enabled;
		GUI.enabled = false;
		EditorGUI.LabelField(position,message,EditorStyles.textField);
		GUI.enabled = bSavedEnabled;
		position.x = originalPos.xMax-20.0f;
		EditorGUI.LabelField(position,EditorGUIUtility.IconContent(UnityIconPath.ICONPATH_WARNING));
	}
}
#endif

} //end namespace Chameleon

#endif //CHM_ENABLE_INPUT_SYSTEM

#endif //INPUTSYSTEM_PRESENT
