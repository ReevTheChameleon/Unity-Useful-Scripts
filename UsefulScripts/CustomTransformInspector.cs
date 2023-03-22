/************************************************************************
 * CUSTOMTRANSFORMINSPECTOR (v2.02)
 * by Reev the Chameleon
 * 4 Jul 2
*************************************************************************
Allow users to right-click on Transform tab and choose a Custom
Transform Inspector to display Transform Component in the way that
most suits their need.
Update v1.0: Ability to scale uniformly by ticking checkbox.
Update v1.1: Fix gimbal lock bug and prevent scientific notation display.
Update v2.0: Add right-click reset, copy, and paste on each field component.
Update v2.01: Add underline menu shortcut
Update v2.02: Small code change to reflect change in EditorHelper class

NOTE: I still can't seem to understand all original source code,
so this may not be as complete as default inspector, but I will try
improving it over time.
*/

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Chameleon{

//=================================================================================
#region CUSTOM TRANSFORM INSPECTOR SELECTION

[InitializeOnLoad] //So static constructor is called on load
public static class CustomTransformInspectorSelection{
	private const string DEFAULT_TRANSFORMINSPECTOR_MENUPATH =
		"CONTEXT/Transform/&Inspectors/Use\xA0&Default Inspector";
	private const string CUSTOM_TRANSFORMINSPECTOR_MENUPATH =
		"CONTEXT/Transform/&Inspectors/Use\xA0&Custom Inspector";

	private struct DefineSymbolMenu{
		public string defineSymbol;
		public string menuPath;
		public DefineSymbolMenu(string defineSymbol,string menuPath){
			this.defineSymbol = defineSymbol;
			this.menuPath = menuPath;
		}
	}
	/* Cannot use const with array */
	private static readonly DefineSymbolMenu[] aDefineSymbolMenu = {
		new DefineSymbolMenu("CHAMELEON_TRANSFORM_INSPECTOR",
			CUSTOM_TRANSFORMINSPECTOR_MENUPATH),
	};

	static CustomTransformInspectorSelection(){
		EditorApplication.delayCall += initialMenuCheck;
		/* This delegate is called once inspector update. Attempt
		to check menu before that will result in warning:
		"Menu cannot be checked because it doesn't exist."
		(Credit: Ziugy, UA) */
	}
	private static void initialMenuCheck(){
		EditorApplication.delayCall -= initialMenuCheck;
		foreach(DefineSymbolMenu d in aDefineSymbolMenu){
			if(ScriptDefineSymbol.isDefined(d.defineSymbol)){
				Menu.SetChecked(d.menuPath,true);
				return;
			}
		}
		Menu.SetChecked(DEFAULT_TRANSFORMINSPECTOR_MENUPATH,true);
	}
	
	[MenuItem(DEFAULT_TRANSFORMINSPECTOR_MENUPATH)]
	static void useDefaultInspector(){
		foreach(DefineSymbolMenu d in aDefineSymbolMenu){
			ScriptDefineSymbol.remove(d.defineSymbol);
			Menu.SetChecked(d.menuPath,false);
		}
		Menu.SetChecked(DEFAULT_TRANSFORMINSPECTOR_MENUPATH,true);
		Debug.Log("Using Default Transform Inspector");
	}

	[MenuItem(CUSTOM_TRANSFORMINSPECTOR_MENUPATH)]
	static void useCustomInspector(){
		foreach(DefineSymbolMenu d in aDefineSymbolMenu){
			ScriptDefineSymbol.remove(d.defineSymbol);
			Menu.SetChecked(d.menuPath,false);
		}
		Menu.SetChecked(DEFAULT_TRANSFORMINSPECTOR_MENUPATH,false);
		ScriptDefineSymbol.add(aDefineSymbolMenu[0].defineSymbol);
		Menu.SetChecked(aDefineSymbolMenu[0].menuPath,true);
		Debug.Log("Using Custom Transform Inspector");
	}
}
#endregion
//=================================================================================

//=================================================================================
#region CUSTOM TRANSFORM INSPECTOR

#if CHAMELEON_TRANSFORM_INSPECTOR
[CustomEditor(typeof(Transform),true)]
class CustomTransformInspector : Editor{
	private const string EDITORPREFS_UNIFORMSCALE = "bUniformScale";
	private readonly GUIContent uniformScaleTooltip =
		new GUIContent("","Uniform Scale");
	private readonly GUIContent copyMenuName = new GUIContent("&Copy Vector");
	private readonly GUIContent pasteMenuName = new GUIContent("Paste\xA0&Vector");
	
	private Transform transform;
	private bool bUniformScale;
	private Vector3 v3Proportion;
	private bool bPastePosition = false;
	private bool bPasteRotation = false;
	private bool bPasteScale = false;
	private Vector3 v3Paste = new Vector3(); //Initialize to suppress warning

	void OnEnable(){
		if(!target)
			return; //It seems sometimes target is fake Unity null
		transform = (Transform)target;
		bUniformScale = EditorPrefs.GetBool(EDITORPREFS_UNIFORMSCALE);
		if(bUniformScale)
			v3Proportion = transform.localScale;
	}
	public override void OnInspectorGUI(){
		/* It IS possible to use built-in Transform Inspector via
		reflection, but I don't like it because Unity can change name
		when upgrade and it will break. */
		if(!target)
			return;

		/* According to Hamid Yusifi, SO, the source code for
		Transform Inspector uses EditorGUIUtility.currentViewWidth
		calculation with magic number 212. */
		if(!EditorGUIUtility.wideMode){
			EditorGUIUtility.wideMode = true;
			EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth-212;
		}

		Rect labelRect;
		Event currentEvent = Event.current;

		Vector3 v3Position =
			EditorGUILayout.Vector3Field("Position",transform.localPosition.roundFifthDecimal());
		if(bPastePosition){
			v3Position = v3Paste;
			GUI.changed = true;
			bPastePosition = false;
		}
		if(GUI.changed){
			Undo.RecordObject(transform,"Position Change");
			transform.localPosition = v3Position;
			GUI.changed = false;
		}
		if(currentEvent.type==EventType.ContextClick){
			labelRect = GUILayoutUtility.GetLastRect();
			labelRect.width = EditorGUIUtility.labelWidth;
			if(labelRect.Contains(currentEvent.mousePosition)){
				GenericMenu contextMenu = new GenericMenu();
				contextMenu.AddItem(new GUIContent("&Reset Position"),false,resetPosition);
				contextMenu.AddSeparator("");
				contextMenu.AddItem(copyMenuName,false,copyVector3,transform.localPosition);
				if(EditorHelper.tryParseClipboard(EditorGUIUtility.systemCopyBuffer,out v3Paste))
					contextMenu.AddItem(pasteMenuName,false,()=>{bPastePosition=true;});
				else
					contextMenu.AddDisabledItem(pasteMenuName);
				contextMenu.ShowAsContext();
				currentEvent.Use();
			}
		} // Code idea, Credit: jjcrawley, UA.
		/* Considering possibility to reduce duplicate code because similar code is used
		for all 3 Transform components */

		/* Finally I am forced to use "m_LocalEulerAnglesHint" SerializedProperty because
		one rotation configuration can be represented by MANY euler angles, and to rotate
		smoothly, one has to save previous rotation state. In this case Unity DOES saves it.
		Because we are extending the Transform Component, we can't create variables to save it
		ourselves, so we ultimately have to rely on Unity's save version. Anyway, this
		SerializedProperty is open for all to use. */
		serializedObject.Update();
		SerializedProperty eulerHintSerializedProperty = serializedObject.FindProperty("m_LocalEulerAnglesHint"); //Credit: gglobensky, UF
		Vector3 v3EulerAngle = eulerHintSerializedProperty.vector3Value;
		if(Quaternion.Euler(v3EulerAngle) != transform.rotation){
			/* This is rather hackish, but it adjusts initial euler angle
			of imported model to what should be shown via default inspector. */
			v3EulerAngle = transform.eulerAngles;
			if(v3EulerAngle.x >= 180.0f) v3EulerAngle.x -= 360.0f;
			if(v3EulerAngle.y >= 180.0f) v3EulerAngle.y -= 360.0f;
			if(v3EulerAngle.z >= 180.0f) v3EulerAngle.z -= 360.0f;
			/* If not do this, m_LocalEulerAnglesHint is not initialized and
			rotation may show incorrect value */
			eulerHintSerializedProperty.vector3Value = v3EulerAngle;
			serializedObject.ApplyModifiedPropertiesWithoutUndo();
		}
		v3EulerAngle =
			EditorGUILayout.Vector3Field("Rotation",v3EulerAngle.roundFifthDecimal());
		if(bPasteRotation){
			v3EulerAngle = v3Paste;
			GUI.changed = true;
			bPasteRotation = false;
		}
		if(GUI.changed){
			Undo.RecordObject(transform,"Rotation Change");
			eulerHintSerializedProperty.vector3Value = v3EulerAngle;
			serializedObject.ApplyModifiedPropertiesWithoutUndo();
			/* Because normal ApplyModifiedProperties() will register Undo with "Inspector" text,
			which is not desirable. Hence record Undo ourselves. Undo has to be recorded BEFORE
			applying change. */
			transform.localEulerAngles = eulerHintSerializedProperty.vector3Value;
			/* This HAS TO be called after ApplyModifiedProperties for some reasons otherwise
			the SerializedProperty will give old value (not assigned one).
			(maybe related to weird getter, setter?) */
			GUI.changed = false;
		}
		if(currentEvent.type==EventType.ContextClick){
			labelRect = GUILayoutUtility.GetLastRect();
			labelRect.width = EditorGUIUtility.labelWidth;
			if(labelRect.Contains(currentEvent.mousePosition)){
				GenericMenu contextMenu = new GenericMenu();
				contextMenu.AddItem(new GUIContent("&Reset Rotation"),false,resetRotation);
				contextMenu.AddSeparator("");
				contextMenu.AddItem(copyMenuName,false,copyVector3,transform.localEulerAngles);
				if(EditorHelper.tryParseClipboard(EditorGUIUtility.systemCopyBuffer,out v3Paste))
					contextMenu.AddItem(pasteMenuName,false,()=>{bPasteRotation=true;});
				else
					contextMenu.AddDisabledItem(pasteMenuName);
				contextMenu.ShowAsContext();
				currentEvent.Use();
			}
		}

		Vector3 v3PreviousScale = transform.localScale;
		Vector3 v3Scale =
			EditorGUILayout.Vector3Field("Scale",v3PreviousScale.roundFifthDecimal());
		if(bPasteScale){
			v3Scale = v3Paste;
			GUI.changed = true;
			bPasteScale = false;
		}
		bool bScaleChanged = GUI.changed;
		GUI.changed = false;
		Rect rectUniformScaleToggle = GUILayoutUtility.GetLastRect();
		rectUniformScaleToggle.x += EditorGUIUtility.labelWidth - 20.0f;
		rectUniformScaleToggle.width = 15.0f;
		bUniformScale = EditorGUI.Toggle(rectUniformScaleToggle,bUniformScale);
		EditorGUI.LabelField(rectUniformScaleToggle,uniformScaleTooltip);
		if(currentEvent.type==EventType.ContextClick){
			labelRect = GUILayoutUtility.GetLastRect();
			labelRect.width = EditorGUIUtility.labelWidth;
			if(labelRect.Contains(currentEvent.mousePosition)){
				GenericMenu contextMenu = new GenericMenu();
				contextMenu.AddItem(new GUIContent("&Reset Scale"),false,resetScale);
				contextMenu.AddSeparator("");
				contextMenu.AddItem(copyMenuName,false,copyVector3,transform.localScale);
				if(EditorHelper.tryParseClipboard(EditorGUIUtility.systemCopyBuffer,out v3Paste))
					contextMenu.AddItem(pasteMenuName,false,()=>{bPasteScale=true;});
				else
					contextMenu.AddDisabledItem(pasteMenuName);
				contextMenu.ShowAsContext();
				currentEvent.Use();
			}
		}
		if(GUI.changed){
			EditorPrefs.SetBool(EDITORPREFS_UNIFORMSCALE,bUniformScale);
			if(bUniformScale)
				v3Proportion = v3PreviousScale;
			GUI.changed = false;
		}
		if(bScaleChanged){
			Undo.RecordObject(transform,"Scale Change");
			if(bUniformScale){
				if(v3Scale.x != v3PreviousScale.x){
					/* Reason to use v3Proportion rather than v3PreviousScale
					is that using v3PreviousScale repeatedly causes accumulated
					error and could finally mess up proportion. */
					float uniformScale = v3Scale.x/v3Proportion.x;
					v3Scale.y = uniformScale*v3Proportion.y;
					v3Scale.z = uniformScale*v3Proportion.z;
				}
				else if(v3Scale.y != v3PreviousScale.y){
					float uniformScale = v3Scale.y/v3Proportion.y;
					v3Scale.x = uniformScale*v3Proportion.x;
					v3Scale.z = uniformScale*v3Proportion.z;
				}
				else if(v3Scale.z != v3PreviousScale.z){
					float uniformScale = v3Scale.z/v3Proportion.z;
					v3Scale.x = uniformScale*v3Proportion.x;
					v3Scale.y = uniformScale*v3Proportion.y;
				}
			}
			transform.localScale = v3Scale;
		}
	} //end OnInspectorGUI()
//-----------------------------------------------------------------------------
	#region MENU FUNCTIONS
	private void resetPosition(){
		Undo.RecordObject(transform,"Reset Position");
		transform.localPosition = Vector3.zero;
	}
	private void resetRotation(){
		Undo.RecordObject(transform,"Reset Rotation");
		transform.localRotation = Quaternion.identity;
	}
	private void resetScale(){
		Undo.RecordObject(transform,"Reset Scale");
		transform.localScale = new Vector3(1.0f,1.0f,1.0f);
	}
	private void copyVector3(object oVector3){
		if(oVector3 is Vector3)
			EditorGUIUtility.systemCopyBuffer = EditorHelper.toClipboardString(oVector3);
	}
	#endregion	
//-----------------------------------------------------------------------------
}

#endif //CHAMELEON_TRANSFORM_INSPECTOR

#endregion
//=================================================================================

} //end namespace Chameleon

#endif //UNITY_EDITOR
