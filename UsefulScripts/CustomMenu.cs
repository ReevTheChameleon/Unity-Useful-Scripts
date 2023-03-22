/*******************************************************************************
 * CUSTOMMENU (v1.19)
 * by Reev the Chameleon
 * 10 Mar 3
 *******************************************************************************
Add following menus to the editor:
- Transform Inspector: Menu to reset transform while keeping children transform intact.
- Hierarchy: Quick grouping, unparenting, and dissolving GameObject.
- Assets: Quick grouping and dissolve folder.
- Add Menu Popup: Popup Add Menu as context menu when hotkey is pressed over Scene View,
Project Window, and Hierarchy Window. Displayed Add Menu differs based on window
the mouse is over (like in Blender).
Update v1.1: Move Unity window names to UnityWindowName class and rename some Menu names and code
Update v1.2: Fix PopupAdd code to allow creating GameObject as child
Update v1.3: Add some Animator and MonoBehaviour Inspector menus
Update v1.4: Add [BindSelf] attribute which allows binding fields at edit time via menu,
and add menu for creating blank shader.
Update v1.4.1: Add menu for creating C# Script with CustomEditor
Update v1.5: Add menu for creating shaders and BlendTrees.
Update v1.5.1: Change popup menu for adding GameObject so it does not engage rename mode
Update v1.6: Add right-click menu for marking AnimationClip as legacy.
Update v1.7: Add popup menu for adding UI elements, and menu for anchor snap.
Update v1.8: Add popup menu for adding TextMeshPro elements and menu to convert Text to TextMeshPro
Update v1.9: Add menu to check whether selected sprite is packed into SpriteAtlas or not
Update v1.10: Add some RectTransform and TMP_Text context menu and fix UI instantiate position bug
Update v1.10.1: Fix bug when creating empty UI GameObject and add menu to open persistent data path
Update v1.11: Add context menu for copying Sprite size and replacing GameObject
Update v1.11.1: Fix bug empty GameObject created under Canvas being created outside of Canvas plane
Update v1.12: Add Menu to lock SceneView focus when switching Play Mode and UI/Canvas create menu
Update v1.13: Add CinemachineVirtualCamera context menu
Update v1.14: Add RootMotionInfo Window
Update v1.15: Add context menu for matching 9-slice pixels per unit with current size.
Update v1.16: Add context menu for AnimationWindowEvent to set/clear whether event requires receiver
Update v1.16.1: Add context menu to set/clear event require receiver in AnimationClip
Update v1.17: Add context menu for Rigidbody, and add code for pasting serialized values
Update v1.18: Add context menu for MeshFilter
Update v1.19: Add context menu for AudioImporter and add alt key for copying/pasting Component

Note: Code for copying BlendTree asset will show errors but work, will recheck later.
*/

using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.ShortcutManagement;
using UnityEngine.SceneManagement;
using UnityEditorInternal;
#endif
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
#if CHM_SPRITE_PRESENT
using UnityEngine.U2D;
#endif
#if CHM_CINEMACHINE_PRESENT
using Cinemachine;
#endif

namespace Chameleon{

using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using BlendTree = UnityEditor.Animations.BlendTree;
#endif

//====================================================================================
#region RELATED ATTRIBUTES
public class BindSelfAttribute : PropertyAttribute{}
#endregion
//====================================================================================

#if UNITY_EDITOR
public static class CustomMenu{
	/* Overwrite existing one because that one does not deselect assets */
	[MenuItem("Edit/Deselect Everything")]
	static void deselectEverything(){
		Selection.objects = null;
	}
//====================================================================================
	#region COMPONENT INSPECTOR
	/* This is simply for underline shortcut convenience */
	[MenuItem("CONTEXT/Component/&Copy Component")]
	public static void copyComponent(MenuCommand menuCommand){
		ComponentUtility.CopyComponent((Component)menuCommand.context);
	}
	[MenuItem("CONTEXT/Component/Paste Component (&V)")]
	public static bool pasteComponent(MenuCommand menuCommand){
		Component component = (Component)menuCommand.context;
		if(!ComponentUtility.PasteComponentValues(component)){
			if(!ComponentUtility.PasteComponentAsNew(component.gameObject)){
				Debug.LogWarning("Paste Component failed!");
				return false;
			}
		}
		return true;
	}
	#endregion
//====================================================================================
	#region TRANSFORM INSPECTOR
	/* Reset with underline shortcut. Does not overwrite default Reset
	because that one doesn't have ampersand. */
	[MenuItem("CONTEXT/Transform/&Reset")]
	static void transformReset(MenuCommand menuCommand){
		/* It seems menuCommand has already taken into account if many
		Transforms is selected, and perform on each Transform in turn.
		Surprisingly Undo automatically collapses correctly. */
		Transform transform = menuCommand.context as Transform;
		if(transform){
			Undo.RecordObject(transform,"Reset Transform");
			transform.reset();
		}
	}
	[MenuItem("CONTEXT/Transform/Reset\xA0&Keep Children")]
	/* Because Unity capture & after space and treat it as alt hot key,
	you need to use & after other character. 0xA0 is nbsp in Latin-1
	supplement, and is also displayed as space while not triggering
	Unity interpretation (Credit: roojerry, UA). */
	static void transformResetKeepChildren(MenuCommand menuCommand){
		Transform transformParent = menuCommand.context as Transform;
		if(!transformParent)
			return;
		/* This code intent is less clear in light of reducing execution time
		by 80%. Rather than detach and reattach children, reset children along with
		parent and countermove them back to the transform they have before reduces
		about 25% execution time. By calling Undo.RecordObjects() only ONCE and not
		calling Undo.CollapseUndoOperations() (VERY SLOW) at all, we reduce another
		half of execution time. */
		int undoIndex = Undo.GetCurrentGroup();
		Transform[] aUndo = new Transform[transformParent.childCount+1]; //last for parent
		aUndo[transformParent.childCount] = transformParent;
		for(int j=0; j<aUndo.Length-1; ++j)
			aUndo[j] = transformParent.GetChild(j);
		Undo.RecordObjects(aUndo,"Reset Transform Keep Children");
		Vector3 vPosition = transformParent.localPosition;
		Quaternion qRotation = transformParent.localRotation;
		Vector3 vScale = transformParent.localScale;
		transformParent.reset();
		for(int j=0; j<aUndo.Length-1; ++j){
			aUndo[j].localPosition += vPosition;
			aUndo[j].localRotation = qRotation*aUndo[j].localRotation;
			Vector3 vChildScale = aUndo[j].localScale;
			aUndo[j].localScale = new Vector3(
				vScale.x*vChildScale.x,
				vScale.y*vChildScale.y,
				vScale.z*vChildScale.z
			);
		}
	}
	#endregion
//====================================================================================

//====================================================================================
	#region ANIMATOR INSPECTOR
	[MenuItem("CONTEXT/Animator/List Unimplemented Event")]
	public static void AnimatorListUnimplementedEvent(MenuCommand menuCommand){
		Animator animator = menuCommand.context as Animator;
		if(!animator)
			return;
		/* Because we will do search frequently, use SortedSet (C# equivalent of std::set)
		(Credit: tvanfosson, SO) */
		SortedSet<string> setFunction = new SortedSet<string>();
		foreach(MonoBehaviour monoBehaviour in animator.GetComponents<MonoBehaviour>()){
			foreach(MethodInfo methodInfo in monoBehaviour.GetType().GetMethods(
				ReflectionHelper.BINDINGFLAGS_ALL
			)){
				setFunction.Add(methodInfo.Name);
			}
		}
		bool bAllImplemented = true;
		//(Credit: AllFatherGray, UA)
		foreach(AnimationClip animationClip in animator.runtimeAnimatorController.animationClips){
			foreach(AnimationEvent animationEvent in animationClip.events){
				if(setFunction.Contains(animationEvent.functionName))
					continue;
				Debug.Log(
					animationEvent.functionName + 
					" ("+ animationClip.name + ")"
				);
				bAllImplemented = false;
			}
		}
		if(bAllImplemented)
			Debug.Log("All AnimationEvents are implemented!");
	}
	#endregion
//====================================================================================

//====================================================================================
	#region MONOBEHAVIOUR INSPECTOR
	[MenuItem("CONTEXT/MonoBehaviour/Bind [BindSelf] Components")]
	public static void MonoBehaviourBindSelf(MenuCommand menuCommand){
		MonoBehaviour monoBehaviour = menuCommand.context as MonoBehaviour;
		if(!monoBehaviour)
			return;
		bool bBound = true;
		foreach(FieldInfo fieldInfo in monoBehaviour.GetType().GetFields(
			ReflectionHelper.BINDINGFLAGS_ALL
		)){
			if(fieldInfo.IsDefined(typeof(BindSelfAttribute))){
				Type type = fieldInfo.FieldType;
				if(typeof(Component).IsAssignableFrom(type)){
					Component component = (Component)fieldInfo.GetValue(monoBehaviour);
					if(!component){
						bBound = false;
						component = monoBehaviour.GetComponent(type);
						if(!component)
							component = monoBehaviour.gameObject.AddComponent(type);
						if(component){
							fieldInfo.SetValue(monoBehaviour,component);
							Debug.Log("Bound: "+fieldInfo.Name);
						}
					}
				}
			}
		}
		if(bBound)
			Debug.Log("All [BindSelf] bound!");
	}
	/* This does NOT work for completely different class for some reason
	(i.e. UI events like OnPointerEnter are not called). Will try to resolve later. */
	[MenuItem("CONTEXT/MonoBehaviour/Paste Serialized Values")]
	public static void pasteSerializedValues(MenuCommand menuCommand){
		MonoBehaviour monoBehaviour = (MonoBehaviour)menuCommand.context;
		if(ComponentUtility.PasteComponentValues(monoBehaviour)){
			return;} //if normal paste is OK, just do it and return

		Component[] aComponent = monoBehaviour.GetComponents(typeof(Component));
		MonoScript script = MonoScript.FromMonoBehaviour(monoBehaviour);
		int indexComponent=-1;
		while(indexComponent<aComponent.Length){
			if(aComponent[++indexComponent] == monoBehaviour){
				break;}
		}
		if(!ComponentUtility.PasteComponentAsNew(monoBehaviour.gameObject)){
			/* sometimes paste component fails (like [[RequireComponent(typeof(Collider))]
			where Unity does not know which type of Collider to attach) */
			return;}
		//GetComponents again because aComponent is just a copy and does not update
		aComponent = monoBehaviour.GetComponents(typeof(Component));
		int indexNew = aComponent.Length-1;
		MonoBehaviour newMonoBehaviour = (MonoBehaviour)aComponent[indexNew];
		while(indexNew-- > indexComponent){
			ComponentUtility.MoveComponentUp(newMonoBehaviour);}
		using(SerializedObject so = new SerializedObject(newMonoBehaviour)){
			so.FindProperty("m_Script").objectReferenceValue = script;
			so.ApplyModifiedProperties();
		}
		Object.DestroyImmediate(monoBehaviour);
		//For whatever reason, Undo seems to work OK even without explicitly code for.
	}
	#endregion
//====================================================================================

//====================================================================================
	#region RECTTRANSFORM INSPECTOR
	/* Thanks to giano574 & Senshi, UF, for the idea that it can be done. */
	[MenuItem("CONTEXT/RectTransform/Snap anchors to UI corners")]
	public static void rectTransformSnapAnchorsToUICorners(MenuCommand menuCommand){
		RectTransform rt = (RectTransform)menuCommand.context;
		RectTransform rtParent = (RectTransform)rt.parent; //validated by validation funciton
		Undo.RecordObject(rt,"Move UI Anchors");	
		rt.anchorMin = new Vector2(
			rt.anchorMin.x + rt.offsetMin.x/rtParent.rect.width,
			rt.anchorMin.y + rt.offsetMin.y/rtParent.rect.height
		);
		rt.anchorMax = new Vector2(
			rt.anchorMax.x + rt.offsetMax.x/rtParent.rect.width,
			rt.anchorMax.y + rt.offsetMax.y/rtParent.rect.height
		);
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}
	[MenuItem("CONTEXT/RectTransform/Snap anchors to UI corners",true)]
	public static bool rectTransformSnapAnchorsToUICornersValidate(MenuCommand menuCommand){
		return ((RectTransform)menuCommand.context).parent as RectTransform;
	}
	[MenuItem("CONTEXT/RectTransform/&Fit Parent")]
	public static void rectTransformFitParent(MenuCommand menuCommand){
		RectTransform rt = (RectTransform)menuCommand.context;
		Undo.RecordObject(rt,"Fit Parent");
		rt.fitParent();
	}
	#endregion
//====================================================================================

//====================================================================================
	#region TEXT INSPECTOR
	[MenuItem("CONTEXT/Text/Convert To TextMeshProUGUI")]
	/* User still need to reassign font, because TextMeshProUGUI requires different type
	of font asset, which name may differs. */
	static void test(MenuCommand menuCommand){
		Text txt = (Text)menuCommand.context;
		string text = txt.text;
		float fontSize = txt.fontSize;
		FontStyles tmproFontStyle = textToTMProFontStyle(txt.fontStyle);
		Color color = txt.color;
		TextAlignmentOptions tmproAlignment = textToTMProAlignment(txt.alignment);
		bool bEnableWordWrapping = !(txt.horizontalOverflow==HorizontalWrapMode.Overflow);
		TextOverflowModes overflowMode =
			txt.verticalOverflow==VerticalWrapMode.Truncate ?
			TextOverflowModes.Truncate :
			TextOverflowModes.Overflow
		;
		GameObject g = txt.gameObject;
		Undo.DestroyObjectImmediate(txt);
		/* Use TMP_Text just in case we want to AddComponent<TextMeshPro>(), because
		TMP_Text is more general class. */
		/* Not sure why but 2 undo operations seems to be collapsed together correctly */
		TMP_Text tmproText = Undo.AddComponent<TextMeshProUGUI>(g);
		tmproText.text = text;
		tmproText.fontSize = fontSize;
		tmproText.fontStyle = tmproFontStyle;
		tmproText.color = color;
		tmproText.alignment = tmproAlignment;
		tmproText.enableWordWrapping = bEnableWordWrapping;
		tmproText.overflowMode = overflowMode; //This still needs some adjustment
	}
	private static FontStyles textToTMProFontStyle(FontStyle textFontStyle){
		switch(textFontStyle){
			case FontStyle.Normal:
				return FontStyles.Normal;
			case FontStyle.Bold:
				return FontStyles.Bold;
			case FontStyle.Italic:
				return FontStyles.Italic;
			case FontStyle.BoldAndItalic:
				return (FontStyles)((int)FontStyles.Bold+(int)FontStyle.Italic);
		}
		return FontStyles.Normal;
	}
	private static TextAlignmentOptions textToTMProAlignment(TextAnchor textAlignment){
		switch(textAlignment){
			case TextAnchor.UpperLeft:
				return TextAlignmentOptions.TopLeft;
			case TextAnchor.UpperCenter:
				return TextAlignmentOptions.Top;			
			case TextAnchor.UpperRight:
				return TextAlignmentOptions.TopRight;
			case TextAnchor.MiddleLeft:
				return TextAlignmentOptions.Left;	
			case TextAnchor.MiddleCenter:
				return TextAlignmentOptions.Center;	
			case TextAnchor.MiddleRight:
				return TextAlignmentOptions.Right;
			case TextAnchor.LowerLeft:
				return TextAlignmentOptions.BottomLeft;	
			case TextAnchor.LowerCenter:
				return TextAlignmentOptions.Bottom;	
			case TextAnchor.LowerRight:
				return TextAlignmentOptions.BottomRight;	
		}
		return TextAlignmentOptions.TopLeft;
	}
	#endregion
//====================================================================================

//====================================================================================
	#region TMP_TEXT INSPECTOR
	/* Applicable to both TextMeshPro and TextMeshProUGUI, as they both inherit
	from TMP_Text class */
	[MenuItem("CONTEXT/TMP_Text/&Zero Margin")]
	static void tmpTextZeroMargin(MenuCommand menuCommand){
		((TMP_Text)menuCommand.context).margin = Vector4.zero;
	}
	#endregion
//====================================================================================

//====================================================================================
	#region SPRITE INSPECTOR
	[MenuItem("CONTEXT/Sprite/Copy Sprite Size")]
	static void copySpriteSize(MenuCommand menuCommand){
		EditorGUIUtility.systemCopyBuffer =
			EditorHelper.toClipboardString(((Sprite)menuCommand.context).bounds.size);
	}
	#endregion
//====================================================================================

//====================================================================================
	#region CINEMACHINEVIRTUALCAMERA INSPECTOR
#if CHM_CINEMACHINE_PRESENT
	[MenuItem("CONTEXT/CinemachineVirtualCamera/Initialize\xA0&3rd Person Camera")]
	static void vcamInit3rdPersonCamera(MenuCommand menuCommand){
		CinemachineVirtualCamera vcam = (CinemachineVirtualCamera)menuCommand.context;
		// No need to remove old component; this will just replace it
		Cinemachine3rdPersonFollow thirdPersonFollow =
			vcam.AddCinemachineComponent<Cinemachine3rdPersonFollow>();
		thirdPersonFollow.Damping = Vector3.zero;
		thirdPersonFollow.ShoulderOffset = Vector3.zero;
		thirdPersonFollow.VerticalArmLength = 0.0f;
		thirdPersonFollow.CameraDistance = 0.0f;
	}
#endif
	#endregion
//====================================================================================

//====================================================================================
	#region ANIMATIONCLIP INSPECTOR
	/* Note: Multi-selection has been already taken cared of when using CONTEXT */
	/* Alternative: change inspector to Debug Mode and toggle "Legacy" (Credit: Key_Less, UA) */
	[MenuItem("CONTEXT/AnimationClip/Legacy/SetLegacy")]
	public static void animationSetLegacy(MenuCommand menuCommand){
		animationMarkLegacy((AnimationClip)menuCommand.context,true);
	}
	[MenuItem("CONTEXT/AnimationClip/Legacy/ClearLegacy")]
	public static void animationClearLegacy(MenuCommand menuCommand){
		animationMarkLegacy((AnimationClip)menuCommand.context,false);
	}
	/* Cannot set legacy for embedded AnimationClips (for now, may find a way later) */
	[MenuItem("CONTEXT/AnimationClip/Legacy/SetLegacy",true)]
	[MenuItem("CONTEXT/AnimationClip/Legacy/ClearLegacy",true)]
	public static bool animationMarkLegacyValidate(MenuCommand menuCommand){
		return menuCommand.context.hideFlags == HideFlags.None;
	}
	public static void animationMarkLegacy(AnimationClip animationClip,bool bLegacy){
		/* Original idea was to open .anim file in text editor,
		then change m_Legacy to 1 (Credit: LymanCao, UA) */
		SerializedObject serializedObject = new SerializedObject(animationClip);
		SerializedProperty legacySerializedProperty = serializedObject.FindProperty("m_Legacy");
		legacySerializedProperty.boolValue = bLegacy;
		serializedObject.ApplyModifiedProperties();
	}

	/* In normal AnimationClip, you can access CustomMenu of AnimationEventWindow
	to set/clear require receiver for each AnimationEvent. However, below are useful
	when dealing with IMPORTED AnimationClip, where such option is not present.
	Note that it will apply set/clear to ALL events (for simplicity). */	
	[MenuItem("CONTEXT/AnimationClip/AnimationEvents/Set Require Receiver")]
	public static void animationClipSetEventsRequireReceiver(MenuCommand menuCommand){
		AnimationClip clip = (AnimationClip)menuCommand.context;
		setEventMessageOptionsInFile(clip,SendMessageOptions.RequireReceiver);
	}
	[MenuItem("CONTEXT/AnimationClip/AnimationEvents/Clear Require Receiver")]
	public static void animationClipClearEventsRequireReceiver(MenuCommand menuCommand){
		AnimationClip clip = (AnimationClip)menuCommand.context;
		setEventMessageOptionsInFile(clip,SendMessageOptions.DontRequireReceiver);
	}
	public static void setEventMessageOptionsInFile(
		AnimationClip clip,SendMessageOptions sendMessageOptions)
	{
		string sClipPath = AssetDatabase.GetAssetPath(clip);
		string sMetaPath = sClipPath+".meta";
		string sContent = File.ReadAllText(sMetaPath);
		/* Because this string may be large and we may need to modify in many places,
		we use StringBuilder (Credit: James Ko & Albin Sunnanbo, SO).
		However, as StringBuilder lacks searching method (IndexOf), and we only modify
		ONE character per place, we search using original string. */
		System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder(sContent);
		int indexReplace = 0;
		//first +17 is OK because we know it will never skip over anything we need due to file content
		while((indexReplace=sContent.IndexOf("messageOptions: ",indexReplace+17)) != -1){
			stringBuilder[indexReplace+16] = (char)((int)sendMessageOptions+'0');} //Credit: MarcinJuraszek, SO
		
		File.WriteAllText(sMetaPath,stringBuilder.ToString());
		AssetDatabase.ImportAsset(sClipPath);
	}

	class RootMotionInfoWindow : EditorWindow{
		private AnimationClip clip;
		private AnimationCurve positionCurveX;
		private AnimationCurve positionCurveY;
		private AnimationCurve positionCurveZ;
		private AnimationCurve rotationCurveX;
		private AnimationCurve rotationCurveY;
		private AnimationCurve rotationCurveZ;
		private AnimationCurve rotationCurveW;
		private bool bExtracted;
		private float samplePoint; //[0,1]
		private float time;
		private Vector3 vSample;
		private Quaternion qSample;

		[MenuItem("CONTEXT/AnimationClip/RootMotionInfo...")]
		static void showWindow(MenuCommand menuCommand){
			//RootMotionInfoWindow rootMotionInfoWindow = GetWindowWithRect<RootMotionInfoWindow>(
			//	new Rect(0.0f,0.0f,300.0f,300.0f),
			//	true,
			//	"RootMotion Info",
			//	true
			//);
			RootMotionInfoWindow rootMotionInfoWindow = GetWindow<RootMotionInfoWindow>();
			rootMotionInfoWindow.clip = (AnimationClip)menuCommand.context;
		}
		void OnGUI(){
			if(!bExtracted){
				extractRootMotion();
				bExtracted = true;
			}
			GUI.enabled = false;
			EditorGUILayout.ObjectField(
				"Clip",
				clip,
				typeof(AnimationClip),
				false
			);
			EditorGUILayout.FloatField("Clip Length",clip.length);

			GUI.enabled = true;
			EditorGUILayout.LabelField("Position",EditorStyles.boldLabel);
			++EditorGUI.indentLevel;
			drawReadonlyCopyableCurve("x",positionCurveX,nameof(positionCurveX));
			drawReadonlyCopyableCurve("y",positionCurveY,nameof(positionCurveY));
			drawReadonlyCopyableCurve("z",positionCurveZ,nameof(positionCurveZ));
			--EditorGUI.indentLevel;
			EditorGUILayout.LabelField("Rotation",EditorStyles.boldLabel);
			++EditorGUI.indentLevel;
			drawReadonlyCopyableCurve("x",rotationCurveX,nameof(rotationCurveX));
			drawReadonlyCopyableCurve("y",rotationCurveY,nameof(rotationCurveY));
			drawReadonlyCopyableCurve("z",rotationCurveZ,nameof(rotationCurveZ));
			drawReadonlyCopyableCurve("w",rotationCurveW,nameof(rotationCurveW));
			--EditorGUI.indentLevel;

			EditorGUILayout.LabelField("Sample Point",EditorStyles.boldLabel);
			Rect rectSlider = GUILayoutUtility.GetLastRect();
			rectSlider.x += EditorGUIUtility.labelWidth;
			rectSlider.width -= EditorGUIUtility.labelWidth;
			EditorGUI.BeginChangeCheck();
			samplePoint = GUI.HorizontalSlider(rectSlider,samplePoint,0.0f,1.0f);
			if(EditorGUI.EndChangeCheck()){
				time = samplePoint*clip.length;
				sampleCurve(time);
			}
			EditorGUI.BeginChangeCheck();
			time = EditorGUILayout.FloatField("Time",time);
			if(EditorGUI.EndChangeCheck()){
				time = Mathf.Clamp(time,0.0f,clip.length);
				samplePoint = time/clip.length;
				sampleCurve(time);
			}
			GUI.enabled = false;
			EditorGUILayout.Vector3Field("Position",vSample);
			GUI.enabled = true;
			if(EditorHelper.contextClicked(GUILayoutUtility.GetLastRect()))
				EditorHelper.createCopyPasteContextMenu(this,nameof(vSample)).ShowAsContext();
			GUI.enabled = false;
			EditorGUILayout.Vector3Field("Rotation",qSample.eulerAngles);
			GUI.enabled = true;
			if(EditorHelper.contextClicked(GUILayoutUtility.GetLastRect()))
				EditorHelper.createCopyPasteContextMenu(this,nameof(qSample)).ShowAsContext();
		}
		private void drawReadonlyCopyableCurve(string label,AnimationCurve curve,string curveName){
			GUI.enabled = false;
			EditorGUILayout.CurveField(label,curve);
			GUI.enabled = true;
			if(EditorHelper.contextClicked(GUILayoutUtility.GetLastRect()))
				EditorHelper.createCopyPasteContextMenu(this,curveName).ShowAsContext();
		}
		private void sampleCurve(float time){
			vSample = new Vector3(
				positionCurveX.Evaluate(time),
				positionCurveY.Evaluate(time),
				positionCurveZ.Evaluate(time)
			);
			qSample = new Quaternion(
				rotationCurveX.Evaluate(time),
				rotationCurveY.Evaluate(time),
				rotationCurveZ.Evaluate(time),
				rotationCurveW.Evaluate(time)
			);
		}
		private void extractRootMotion(){
			EditorCurveBinding curveBinding = new EditorCurveBinding();
			curveBinding.path = null;
			curveBinding.type = typeof(Animator);
			curveBinding.propertyName = "RootT.x";
			positionCurveX = AnimationUtility.GetEditorCurve(clip,curveBinding);
			curveBinding.propertyName = "RootT.y";
			positionCurveY = AnimationUtility.GetEditorCurve(clip,curveBinding);
			curveBinding.propertyName = "RootT.z";
			positionCurveZ = AnimationUtility.GetEditorCurve(clip,curveBinding);
			curveBinding.propertyName = "RootQ.x";
			rotationCurveX = AnimationUtility.GetEditorCurve(clip,curveBinding);
			curveBinding.propertyName = "RootQ.y";
			rotationCurveY = AnimationUtility.GetEditorCurve(clip,curveBinding);
			curveBinding.propertyName = "RootQ.z";
			rotationCurveZ = AnimationUtility.GetEditorCurve(clip,curveBinding);
			curveBinding.propertyName = "RootQ.w";
			rotationCurveW = AnimationUtility.GetEditorCurve(clip,curveBinding);
		}
	}
	#endregion
//====================================================================================

//====================================================================================
	#region IMAGE INSPECTOR
	[MenuItem("CONTEXT/Image/Sync 9-Slice PixelsPerUnit Multiplier/Width")]
	public static void imageSync9SlicePixelPerUnitMultiplierWidth(MenuCommand menuCommand){
		Image image = (Image)menuCommand.context;
		image.pixelsPerUnitMultiplier =
			image.mainTexture.width/image.rectTransform.rect.width;
		/* I cannot find any other ways that can reliably refresh the UI. */
		image.enabled = !image.enabled;
		image.enabled = !image.enabled;
	}
	[MenuItem("CONTEXT/Image/Sync 9-Slice PixelsPerUnit Multiplier/Height")]
	public static void imageSync9SlicePixelPerUnitMultiplierHeight(MenuCommand menuCommand){
		Image image = (Image)menuCommand.context;
		image.pixelsPerUnitMultiplier =
			image.mainTexture.height/((RectTransform)image.transform).rect.height;
		image.enabled = !image.enabled;
		image.enabled = !image.enabled;
	}
	[MenuItem("CONTEXT/Image/Sync 9-Slice PixelsPerUnit Multiplier/Width",true)]
	[MenuItem("CONTEXT/Image/Sync 9-Slice PixelsPerUnit Multiplier/Height",true)]
	/* I would like to disable entire submenu, but it seems impossible with MenuItem. */
	public static bool imageSync9SlicePixelPerUnitMultiplierValidate(MenuCommand menuCommand){
		return ((Image)menuCommand.context).type == Image.Type.Sliced;
	}
	#endregion
//====================================================================================

//====================================================================================
	#region ANIMATIONWINDOWEVENT INSPECTOR
	/* Usually when AnimationEvent is triggered but there is no function with specified name,
	console will log error message "has no receiver!". The game can still run, and
	there is no problem in build and error does not show even in development build, 
	but it can be annoying in Play Mode. These context menu allows you to turn it off. */
	static void setAnimationWindowEventSendMessageOption(SendMessageOptions sendMessageOption){
		Type typeAnimationWindowEvent = Selection.activeObject.GetType();
		AnimationClip clip =
			(AnimationClip)typeAnimationWindowEvent.GetField("clip") //this field is public
			.GetValue(Selection.activeObject)
		;
		AnimationEvent[] aAnimationEvent = AnimationUtility.GetAnimationEvents(clip);
		for(int i=0; i<Selection.objects.Length; ++i){
			int eventIndex = 
				(int)typeAnimationWindowEvent.GetField("eventIndex").GetValue(Selection.objects[i]);
			aAnimationEvent[eventIndex].messageOptions = sendMessageOption;
		}
		AnimationUtility.SetAnimationEvents(clip,aAnimationEvent);
	}
	static SendMessageOptions getAnimationWindowEventSendMessageOption(){
		Type typeAnimationWindowEvent = Selection.activeObject.GetType();
		AnimationClip clip =
			(AnimationClip)typeAnimationWindowEvent.GetField("clip") //this field is public
			.GetValue(Selection.activeObject)
		;
		int eventIndexActive = 
			(int)typeAnimationWindowEvent.GetField("eventIndex").GetValue(Selection.activeObject);
		AnimationEvent[] aAnimationEvent = AnimationUtility.GetAnimationEvents(clip);
		return aAnimationEvent[eventIndexActive].messageOptions;
	}
	[MenuItem("CONTEXT/AnimationWindowEvent/Set Require Receiver")]
	static void animationWindowEventSetRequireReceiver(){
		setAnimationWindowEventSendMessageOption(SendMessageOptions.RequireReceiver);
	}
	[MenuItem("CONTEXT/AnimationWindowEvent/Set Require Receiver",true)]
	static bool animationWindowEventSetRequireReceiverValidate(){
		return getAnimationWindowEventSendMessageOption()!=SendMessageOptions.RequireReceiver;
	}
	[MenuItem("CONTEXT/AnimationWindowEvent/Clear Require Receiver")]
	static void animationWindowEventClearRequireReceiver(){
		setAnimationWindowEventSendMessageOption(SendMessageOptions.DontRequireReceiver);
	}
	[MenuItem("CONTEXT/AnimationWindowEvent/Clear Require Receiver",true)]
	static bool animationWindowEventClearRequireReceiverValidate(){
		return getAnimationWindowEventSendMessageOption()!=SendMessageOptions.DontRequireReceiver;
	}
	#endregion
//====================================================================================

//====================================================================================
	#region RIGIDBODY INSPECTOR
	[MenuItem("CONTEXT/Rigidbody/Reset Velocity")]
	static void rigidbodyResetVelocity(MenuCommand menuCommand){
		Rigidbody rb = (Rigidbody)menuCommand.context;
		rb.velocity = Vector3.zero;
	}
	[MenuItem("CONTEXT/Rigidbody/Reset Angular Velocity")]
	static void rigidbodyResetAngularVelocity(MenuCommand menuCommand){
		Rigidbody rb = (Rigidbody)menuCommand.context;
		rb.angularVelocity = Vector3.zero;
	}
	#endregion
//====================================================================================

//====================================================================================
	#region MESHFILTER INSPECTOR
	[MenuItem("CONTEXT/MeshFilter/Duplicate Mesh Asset...")]
	static void meshFilterDuplicateMeshAsset(MenuCommand menuCommand){
		MeshFilter meshFilter = (MeshFilter)menuCommand.context;
		Mesh mesh = Mesh.Instantiate(meshFilter.sharedMesh);
		string path = EditorUtility.SaveFilePanel(
			"Save Mesh",
			Application.dataPath,
			"Mesh.asset",
			"asset"
		);
		if(path.Length != 0){
			path = path.Substring(Application.dataPath.Length-"Assets".Length);
			AssetDatabase.CreateAsset(mesh,path);
			AssetDatabase.SaveAssetIfDirty(mesh);
			EditorGUIUtility.PingObject(mesh);
		}
	}
	#endregion
//====================================================================================

//====================================================================================
	#region AUDIOIMPORTER INSPECTOR
	[MenuItem("CONTEXT/AudioImporter/Sample Count")]
	static void audioImporterSampleCount(MenuCommand menuCommand){
		AudioImporter audioImporter = (AudioImporter)menuCommand.context;
		AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
			AssetDatabase.GetAssetPath(audioImporter));
		Debug.Log(clip.samples*clip.channels);
	}
	#endregion
//====================================================================================

//====================================================================================
	#region HIERARCHY
	/* Because when multiple objects are selected, menu is called for EACH of them
	in turn, if the function needs to be called only once across entire selection,
	we need something to record and judge whether to perform the function or not. */
	private static class OncePerSelectionMenu{
		private static int selectionLoopCount = 0;
		public static bool BShouldExecute{
			get{
				if(selectionLoopCount >= Selection.transforms.Length-1){
					selectionLoopCount = 0;
					return true;
				}
				selectionLoopCount++;
				return false;
			}
		}
	}
	/* CONTEXT only shows menu when right-click in inspector so not useful here.
	Priority must be lower than 49 to show in hierarchy. (Credit: neginfinity, UF) */
	[MenuItem("GameObject/&Unparent",false,43)]
	static void gameObjectUnparent(){
		/* This is better than default Clear Parent in 2020 because unparented children
		will be moved together to under former parent in hierarchy rather than
		randomly spread everywhere. */
		if(!OncePerSelectionMenu.BShouldExecute)
			return;
		Transform[] aSelectedTransform = Selection.transforms;
		/* Because Selection.transforms does NOT have well define order,
		to make sure Transforms are organized, we need some sorting. */
		Array.Sort(aSelectedTransform,new TransformIndexComparer());
		int indexUndo = Undo.GetCurrentGroup();
		/* Because Selection.transforms only returns top level, it is OK even
		when both children and parent are selected at the same time. */
		for(int i=aSelectedTransform.Length-1; i>=0; --i){
			Transform transformChild = aSelectedTransform[i];
			Transform transformParent = transformChild.parent;
			if(transformParent != null){
				Undo.SetTransformParent(
					transformChild,
					transformParent.parent,
					"Unparent"
				);
				int indexParent = transformParent.GetSiblingIndex();
				/* There is a trick here that if child comes before parent, when
				you set its sibling index it will be pulled out and the index of
				parent will shift down by 1. Hence you need only indexParent not
				indexParent+1 for that case. */
				transformChild.SetSiblingIndex(
					transformChild.GetSiblingIndex()<indexParent ?
					indexParent :
					indexParent+1
				);
			}
		}
		Undo.CollapseUndoOperations(indexUndo);
	}
	/* Create Empty at current location is ctrl+shift+N already */
	/* This creates GameObject at TOP level, and at world origin */
	[MenuItem("GameObject/Empty GameObject At\xA0&Origin",priority=48)]
	static void gameObjectCreateEmptyAtOrigin(){
		GameObject g = new GameObject("Empty");
		Undo.RegisterCreatedObjectUndo(g,"Create GameObject");
		Selection.activeGameObject = g; //Credit: jamesflowerdew, UA
	}
	[MenuItem("GameObject/&Group",priority=43)]
	static void gameObjectGroup(){
		if(!OncePerSelectionMenu.BShouldExecute)
			return;
		Transform[] aSelectedTransform = Selection.transforms;
		if(aSelectedTransform.Length == 0)
			return;
		int indexUndo = Undo.GetCurrentGroup();
		GameObject gGroup = new GameObject("Group");
		Undo.RegisterCreatedObjectUndo(gGroup,"Group Selected");
		gGroup.transform.parent = Selection.activeTransform.parent;
		Transform transformGroup = gGroup.transform;
		Array.Sort(aSelectedTransform,new TransformIndexComparer());
		for(int i=0; i<aSelectedTransform.Length; ++i){
			Undo.SetTransformParent(
				aSelectedTransform[i],
				transformGroup,
				"Group Selected"
			);
			aSelectedTransform[i].SetSiblingIndex(i);
		}
		Undo.CollapseUndoOperations(indexUndo);
		Selection.activeTransform = transformGroup;
	}
	[MenuItem("GameObject/Disso&lve",priority=43)]
	static void gameObjectDissolve(MenuCommand menuCommand){
		GameObject gParent = menuCommand.context as GameObject;
		if(!gParent)
			return;
		Transform transformParent = gParent.transform;
		int indexUndo = Undo.GetCurrentGroup();
		GameObject[] aChild = new GameObject[transformParent.childCount];
		int indexChild = 0;
		foreach(Transform transformChild in transformParent)
			aChild[indexChild++] = transformChild.gameObject;
		for(int i=aChild.Length-1; i>=0; --i){
			Transform transformChild = aChild[i].transform;
			Undo.SetTransformParent(
				transformChild,
				transformParent.parent,
				"Dissolve GameObject"
			); //want to use DetachChildren(), but cannot record Undo effectively.
			int indexParent = transformParent.GetSiblingIndex();
			transformChild.SetSiblingIndex(
				transformChild.GetSiblingIndex()<indexParent ?
				indexParent :
				indexParent+1
			);
		}
		Undo.DestroyObjectImmediate(gParent);
		Undo.CollapseUndoOperations(indexUndo);
		Selection.objects = aChild; //need GameObject[] or will not do selection
	}
//-----------------------------------------------------------------------------------
	#region REPLACE GAMEOBJECT WINDOW
	class SwapGameObjectWindow : EditorWindow{
		private GameObject gReplace;

		[MenuItem("GameObject/Replace GameObject...")]
		static void showWindow(){
			GetWindowWithRect<SwapGameObjectWindow>(
				new Rect(0.0f,0.0f,300.0f,95.0f),
				true,
				"Replace GameObject",
				true
			);
		}
		void OnGUI(){
			EditorGUILayout.Space(5f);
			gReplace = (GameObject)EditorGUILayout.ObjectField(
				"Replace With",
				gReplace,
				typeof(GameObject),
				true
			);
			bool bCanReplace = false;
			string txtInfo = "";
			if(!gReplace)
				txtInfo = "Please provide GameObject to be used as replacement";
			else if(Selection.transforms.Length == 0)
				txtInfo = "Please select GameObjects to be replaced";
			else
				bCanReplace = true;
			GUI.enabled = bCanReplace;
			if(GUILayout.Button("Replace") && gReplace){
				if(!gReplace)
					return;
				bool bPrefab = (PrefabUtility.GetPrefabAssetType(gReplace) != PrefabAssetType.NotAPrefab);
				foreach(GameObject g in Selection.gameObjects){
					/* Because Selection.gameObjects only returns top level,
					it is OK even when parent and children are selected at the
					same time (operation will only be carried out on parent). */
					Transform t = g.transform;
					TransformData savedTransform = t.save();
					Transform tParent = t.parent;
					int siblingIndex = t.GetSiblingIndex();
					Undo.DestroyObjectImmediate(g);
					GameObject gNew;
					if(bPrefab)
						gNew = PrefabUtility.InstantiatePrefab(gReplace) as GameObject;
					else
						gNew = Instantiate(gReplace);
					Undo.RegisterCreatedObjectUndo(gNew,"Replace GameObject");
					Undo.SetTransformParent(gNew.transform,tParent,"Whatever");
					gNew.transform.SetSiblingIndex(siblingIndex); //keep hierarchy order
					gNew.transform.load(savedTransform);
				}
			}
			GUI.enabled = true;
			if(!bCanReplace)
				EditorGUILayout.HelpBox(txtInfo,MessageType.Info);
			else{
				foreach(Transform t in Selection.transforms){
					if(t.childCount > 0){
						EditorGUILayout.HelpBox(
							"Some selected GameObjects have children; " +
							"they will be lost when you replace their parents.",
							MessageType.Warning
						);
						break;
					}
				}
			}
		}
		void OnSelectionChange(){
			Repaint();
		}
	}
	#endregion
//-----------------------------------------------------------------------------------
	#endregion
//====================================================================================

//====================================================================================
	#region ASSETS
	[MenuItem("Assets/&Group Into Folder")]
	static void assetsGroup(){
		string currentPath = AssetDatabase.GetAssetPath(Selection.activeObject);
		currentPath = currentPath.Substring(0,currentPath.LastIndexOf('/'));
		string folderGUID = AssetDatabase.CreateFolder(currentPath,"Group Folder");
		if(folderGUID == "")
			return;
		foreach(Object unityObject in Selection.objects){
			string assetPath = AssetDatabase.GetAssetPath(unityObject);
			string trailingFileName = assetPath.Substring(assetPath.LastIndexOf('/'));
			AssetDatabase.MoveAsset(
				AssetDatabase.GetAssetPath(unityObject),
				AssetDatabase.GUIDToAssetPath(folderGUID)+"/"+trailingFileName
			);
		}
	}
	[MenuItem("Assets/Disso&lve Folder")]
	static void assetsDissolve(){
		string folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
		int indexSlash = folderPath.LastIndexOf('/');
		if(!AssetDatabase.IsValidFolder(folderPath) || indexSlash==-1) //root (Assets)
			return;
		if(!EditorUtility.DisplayDialog(
			"WARNING!",
			"Dissolving:\n"+folderPath+".\nThis operation cannot be undone.",
			"OK",
			"Cancel"
		))
			return;

		string parentPath = folderPath.Substring(0,indexSlash);
		string[] aFilePath = Directory.GetFiles(folderPath+"/");
		foreach(string filePath in aFilePath){
			if(filePath.EndsWith(".meta"))
				continue;
			AssetDatabase.MoveAsset(
				filePath,
				parentPath+"/"+Path.GetFileName(filePath)
			);
		}
		string[] aSubdirectoryPath = Directory.GetDirectories(folderPath+"/");
		foreach(string subdirectoryPath in aSubdirectoryPath){
			AssetDatabase.MoveAsset(
				subdirectoryPath,
				parentPath+subdirectoryPath.Substring(subdirectoryPath.LastIndexOf("/"))
			);
		}
		/* Alternatively:
		//System.Collections.Generic.IEnumerable<string> fileEnumerable =
		//	Directory.EnumerateFiles(folderPath); //Credit: Douglas, SO
		//if(fileEnumerable!=null && fileEnumerable.GetEnumerator().MoveNext()) //Credit: Darren, SO
		this version claims that it does not have to get all the files, but just
		the first one is enough (no need to MoveNext enumerator to the end). HOWEVER,
		I am still unsure how or whether it is necessary to dispose of the IEnumerator.
		Given that there shouldn't be much file left anyway, this should be fine. */
		if(Directory.GetFiles(folderPath).Length==0) //Credit: ispiro, SO
			AssetDatabase.DeleteAsset(folderPath);
		else
			Debug.LogWarning("Warning: Unable to move all files out. Not deleting directory");
	}
	/* Shortcut for opening file in explorer */
	[MenuItem("Assets/Open in\xA0&Explorer")]
	static void assetsOpenInExplorer(){
		Object selectedObject = Selection.activeObject;
		if(!selectedObject)
			return;
		EditorApplication.ExecuteMenuItem("Assets/Show in Explorer");
	}
	/* Only works in Windows */
	[MenuItem("Assets/Open persistent DataPath")]
	static void assetsOpenPersistentDataPath(){
		//Credit: ArkaneX, UA & jjuam, UF)
		ProcessStartInfo processStartInfo = new ProcessStartInfo("explorer.exe");
		processStartInfo.Arguments = Application.persistentDataPath.Replace('/','\\');
		//Forward slash won't work as Arguments of Process.Start (Credit: David Browne - Microsoft, SO)
		Process.Start(processStartInfo);
	}
//-----------------------------------------------------------------------------------
	#region ASSETS/CREATE
	[MenuItem("Assets/Create/Shader/Blank Shader")]
	static void assetsCreateBlankShader(){
		/* this is the better new version available since Unity 2019. It automatically
		gets into rename mode after creating asset, as well as internally deals with
		duplicated file name. (Credit: johnsoncodehk, github & mstevenson, UF) */
		ProjectWindowUtil.CreateAssetWithContent(
			"BlankShader.shader",
			"",
			EditorGUIUtility.ObjectContent(null,typeof(Shader)).image as Texture2D
		);
		//string folder = EditorPath.getSelectionFolder();
		//File.Create(folder + "/" + EditorPath.nextUniqueFilename(
		//	folder,
		//	"BlankShader.shader"
		//)).Close();
		///* File.Create returns a FileStream that needs to be closed. Alternative way
		//is "using(File.Create(...));" (Credit: jgodfrey, UA). If you do not close the stream,
		//Unity cannot import it and will throw error. */
		//AssetDatabase.Refresh(); //display newly created file in editor
	}
	[MenuItem("Assets/Create/Blend Tree",priority=410)]
	static void assetsCreateBlendTree(){
		/* Normally BlendTree assets are created in Animator and embedded in it.
		However, there may be a time you want to create it as standalone asset
		(Credit: HeyZoos, UF, for pointing out it can be done)
		Note: Newly created BlendTree will have Parameter named "Blend", which
		you may have to rename accordingly when used in Animator. */
		BlendTree blendtree = new BlendTree();
		AssetDatabase.CreateAsset(
			blendtree,
			EditorPath.getSelectionFolder() + "/" + "NewBlendTree.blendtree"
		);
		AssetDatabase.Refresh();
		EditorGUIUtility.PingObject(blendtree);
	}
	#endregion
//-----------------------------------------------------------------------------------
	#region ASSETS/CHECK

	#if CHM_SPRITE_PRESENT
	[MenuItem("Assets/Check/Sprite/Atlas Packed")]
	static void AssetsCheckSpriteAtlasPacked(){
		/* Because sprite.packed can be false in Editor if "Include in Build" is selected,
		this is an alternative way to check if Sprite is packed into SpriteAtlas or not
		(but this can be quite cost intensive) (adapted from Credit: arenart, UF) */
		List<SpriteAtlas> lSpriteAtlas = new List<SpriteAtlas>();
		string[] aSpriteAtlasAssetGUID = AssetDatabase.FindAssets("t:SpriteAtlas");
		foreach(string sGUID in aSpriteAtlasAssetGUID){
			lSpriteAtlas.Add(AssetDatabase.LoadAssetAtPath<SpriteAtlas>(
				AssetDatabase.GUIDToAssetPath(sGUID)
			));
		}
		List<Object> lObj = new List<Object>();
		foreach(Object obj in Selection.objects){
			string path = AssetDatabase.GetAssetPath(obj);
			/* This will load only subasset (Credit: eses, UF). This is because sprite
			is always subasset (confirm later) */
			Object[] aObj = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
			lObj.AddRange(aObj);
		}
		bool bAllPacked = true;
		foreach(Object obj in lObj){
			if(obj is Sprite){
				bool bIsPacked = false;
				foreach(SpriteAtlas spriteAtlas in lSpriteAtlas){
					if(spriteAtlas.CanBindTo((Sprite)obj)){
						bIsPacked = true;
						break;
					}
				}
				if(!bIsPacked){
					bAllPacked = false;
					Debug.LogWarning(obj + " is not packed into SpriteAtlas");
				}
			}
		}
		if(bAllPacked){
			EditorUtility.DisplayDialog(
				"Check Result: OK",
				"All selected sprites (if any) are packed into SpriteAtlas.",
				"OK"
			);
		}
		else{
			EditorUtility.DisplayDialog(
				"Check Result: ISSUE FOUNDS",
				"Some selected sprites are not packed into SpriteAtlas. See console for details.",
				"OK"
			);
		}
	}
	#endif

	#endregion
//-----------------------------------------------------------------------------------
	#endregion
//====================================================================================

//====================================================================================
	#region WINDOW
	private const string MENUPATH_WINDOW_KEEPFOCUSSCENEVIEWWHENPLAY
		= "Window/Keep Focus SceneView When Play";
	[MenuItem(MENUPATH_WINDOW_KEEPFOCUSSCENEVIEWWHENPLAY,priority=-11)]
	private static void WindowKeepFocusSceneViewWhenPlay(){
		bool bActive = !Menu.GetChecked(MENUPATH_WINDOW_KEEPFOCUSSCENEVIEWWHENPLAY);
		Menu.SetChecked(MENUPATH_WINDOW_KEEPFOCUSSCENEVIEWWHENPLAY,bActive);
	}
	[InitializeOnLoadMethod] //Credit Idea: LightStriker, UF
	public static void focusSceneViewWhenPlay(){
		if(Menu.GetChecked(MENUPATH_WINDOW_KEEPFOCUSSCENEVIEWWHENPLAY) &&
			EditorApplication.isPlayingOrWillChangePlaymode)
			SceneManager.sceneLoaded += focusSceneViewWhenPlayCallback;
	}
	static void focusSceneViewWhenPlayCallback(Scene scene,LoadSceneMode loadSceneMode){
		EditorWindow.FocusWindowIfItsOpen<SceneView>(); //Credit Idea: Tenderz, UF
		SceneManager.sceneLoaded -= focusSceneViewWhenPlayCallback;
	}
	#endregion
//====================================================================================

//====================================================================================
	#region POPUP ADDMENU
	[InitializeOnLoad]
	public static class PopupAddMenu{
		private const string SCRIPTWITHEDITOR_TEMPLATENAME = "Templates/C#ScriptWithEditor.cs.txt";
		private const string CUSTOMUNLITSHADER_TEMPLATENAME = "Templates/CustomUnlitShader.shader.txt";
		private const string CUSTOMLITSHADER_TEMPLATENAME = "Templates/CustomLitShader.shader.txt";
		private const string CUSTOMLITSHADERWITHNORMALMAP_TEMPLATENAME = "Templates/CustomLitShaderWithNormalMap.shader.txt";

		private static GenericMenu menuAddGameObject;
		private static GenericMenu menuAddAsset;
			
		static PopupAddMenu(){
		//---------------------------------------------------------------------------
			#region ADD GAMEOBJECT
			menuAddGameObject = new GenericMenu();
			menuAddGameObject.AddItem(
				new GUIContent("Empty\xA0&GameObject"),
				false,
				createGameObjectAsChild,
				null
			);
			menuAddGameObject.AddItem(
				new GUIContent("Empty GameObject At\xA0&Origin"),
				false,
				gameObjectCreateEmptyAtOrigin
			);
			
			menuAddGameObject.AddSeparator("");
			menuAddGameObject.AddItem(
				new GUIContent("&Cube"),
				false,
				createGameObjectAsChild,
				PrimitiveType.Cube
			);
			menuAddGameObject.AddItem(
				new GUIContent("&Sphere"),
				false,
				createGameObjectAsChild,
				PrimitiveType.Sphere
			);
			menuAddGameObject.AddItem(
				new GUIContent("&Plane"),
				false,
				createGameObjectAsChild,
				PrimitiveType.Plane
			);
			menuAddGameObject.AddItem(
				new GUIContent("Capsu&le"),
				false,
				createGameObjectAsChild,
				PrimitiveType.Capsule
			);
			menuAddGameObject.AddItem(
				new GUIContent("C&ylinder"),
				false,
				createGameObjectAsChild,
				PrimitiveType.Cylinder
			);
			menuAddGameObject.AddItem(
				new GUIContent("&Quad"),
				false,
				createGameObjectAsChild,
				PrimitiveType.Quad
			);

			/* TextMeshPro is included in the project by default */
			menuAddGameObject.AddSeparator("");
			menuAddGameObject.AddItem(
				new GUIContent("&UI/\xA0&Text (TMP)"),
				false,
				executeMenuItemAsChild,
				"GameObject/UI/Text - TextMeshPro"
			);
			menuAddGameObject.AddItem(
				new GUIContent("&UI/\xA0&Button (TMP)"),
				false,
				executeMenuItemAsChild,
				"GameObject/UI/Button - TextMeshPro"
			);
			menuAddGameObject.AddItem(
				new GUIContent("&UI/\xA0&Image"),
				false,
				executeMenuItemAsChild,
				"GameObject/UI/Image"
			);
			menuAddGameObject.AddItem(
				new GUIContent("&UI/To&ggle"),
				false,
				()=>{
					executeMenuItemAsChild("GameObject/UI/Toggle");
					Transform activeTransform = Selection.activeTransform;
					Object.DestroyImmediate(activeTransform.GetChild(1).gameObject);
					executeMenuItemAsChild("GameObject/UI/Text - TextMeshPro");
					Selection.activeTransform = activeTransform;
					EditorGUIUtility.PingObject(Selection.activeGameObject);
				}
			);
			menuAddGameObject.AddItem(
				new GUIContent("&UI/\xA0&3D Text"),
				false,
				executeMenuItemAsChild,
				"GameObject/3D Object/Text - TextMeshPro"
			);
			menuAddGameObject.AddItem(
				new GUIContent("&UI/\xA0&Raw Image"),
				false,
				executeMenuItemAsChild,
				"GameObject/UI/Raw Image"
			);
			menuAddGameObject.AddSeparator("&UI/");
			menuAddGameObject.AddItem(
				new GUIContent("&UI/\xA0&Canvas"),
				false,
				()=>{
					GameObject gContext = Selection.activeGameObject;
					EditorApplication.ExecuteMenuItem("GameObject/UI/Canvas");
					GameObject gCreated = Selection.activeGameObject;
					Undo.RegisterCreatedObjectUndo(gCreated, "Create GameObject");
					GameObjectUtility.SetParentAndAlign(gCreated,gContext);
					((RectTransform)gCreated.transform).fitParent();
					EditorGUIUtility.PingObject(Selection.activeGameObject);
					EditorWindow.GetWindow<SceneView>().Focus();
				}
			);
			#endregion
		//---------------------------------------------------------------------------
			#region ADD ASSET
			menuAddAsset = new GenericMenu();
			menuAddAsset.AddItem(
				new GUIContent("&Folder"),
				false,
				()=>{EditorApplication.ExecuteMenuItem("Assets/Create/Folder");}
			);
			menuAddAsset.AddSeparator("");
			menuAddAsset.AddItem(
				new GUIContent("&Scene"),
				false,
				()=>{EditorApplication.ExecuteMenuItem("Assets/Create/Scene");}
			);
			menuAddAsset.AddSeparator("");
			menuAddAsset.AddItem(
				new GUIContent("&C# Script"),
				false,
				()=>{EditorApplication.ExecuteMenuItem("Assets/Create/C# Script");}
			);
			menuAddAsset.AddItem(
				new GUIContent("C# Script With Custom&Editor"),
				false,
				()=>{
					ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
						EditorPath.getCurrentFolder() + "/" + SCRIPTWITHEDITOR_TEMPLATENAME,
						"NewScriptWithEditor.cs"
					);
				}
			);
			menuAddAsset.AddSeparator("");
			menuAddAsset.AddItem(
				new GUIContent("&Material"),
				false,
				()=>{EditorApplication.ExecuteMenuItem("Assets/Create/Material");}
			);
			menuAddAsset.AddItem(
				new GUIContent("S&hader/&Blank Shader"),
				false,
				assetsCreateBlankShader
			);
			menuAddAsset.AddItem(
				new GUIContent("S&hader/Custom\xA0&Unlit"),
				false,
				()=>{
					ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
						EditorPath.getCurrentFolder() + "/" + CUSTOMUNLITSHADER_TEMPLATENAME,
						"NewCustomUnlitShader.shader"
					);
				}
			);
		#if CHM_URP_PRESENT
			menuAddAsset.AddItem(
				new GUIContent("S&hader/Custom\xA0&Lit"),
				false,
				()=>{
					ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
						EditorPath.getCurrentFolder() + "/" + CUSTOMLITSHADER_TEMPLATENAME,
						"NewCustomLitShader.shader"
					);
				}
			);
			menuAddAsset.AddItem(
				new GUIContent("S&hader/Custom Lit with\xA0&Normal Map"),
				false,
				()=>{
					ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
						EditorPath.getCurrentFolder() + "/" + CUSTOMLITSHADERWITHNORMALMAP_TEMPLATENAME,
						"NewCustomLitShaderWithNormalMap.shader"
					);
				}
			);
		#endif
			menuAddAsset.AddSeparator("");
			menuAddAsset.AddItem(
				new GUIContent("&Animation Clip"),
				false,
				()=>{EditorApplication.ExecuteMenuItem("Assets/Create/Animation");}
			);
			menuAddAsset.AddItem(
				new GUIContent("Animato&r"),
				false,
				()=>{EditorApplication.ExecuteMenuItem("Assets/Create/Animator Controller");}
			);
			#endregion
		//---------------------------------------------------------------------------
		}
		private static void createGameObjectAsChild(object oPrimitiveType){
			/* Use this code rather than the old version that calls "EditorApplication.
			ExecuteMenuItem()" because it is found that in practice creating PRIMITIVE
			GameObject in rename mode is NOT desirable and leads to awkward lag. */
			GameObject gContext = Selection.activeGameObject;
			GameObject gCreated;
			if(oPrimitiveType != null)
				gCreated = GameObject.CreatePrimitive((PrimitiveType)oPrimitiveType);
			else
				gCreated = new GameObject("Empty");
			Undo.RegisterCreatedObjectUndo(gCreated,"Create GameObject");
			/* If not register, will show error "Restored Transform child parent pointer
			from NULL" when undo object creation. */
			GameObjectUtility.SetParentAndAlign(gCreated,gContext);
			/* This will reset child to zero relative to its parent transform also set
			child in same layer as parent, and is the "right" way to deal with creating
			new GameObject under context right-click.
			I didn't do this in other menus because I don't want to reset them. */
			gCreated.transform.position = SceneView.lastActiveSceneView.pivot;
			/* This SHOULD position the created GameObject at the center of scene view
			where is should be if created from "Create" menu. */
			//if(gCreated.GetComponent<CanvasRenderer>())
			//	gCreated.transform.setLocalZ(0.0f);
			if(gContext && gContext.transform is RectTransform){
				gCreated.transform.setLocalZ(0.0f);
				gCreated.AddComponent<RectTransform>();
				/* otherwise supposedly grouping GameObject will be created out of canvas plane */
			}
			/* Otherwise, when creating empty UI element in Canvas (for grouping),
			it will not have RectTransform, and all its children will not be able to anchor. */
			Selection.activeGameObject = gCreated;
			/* force unfold the foldout (Credit: bjarkeck, UA). Cannot find better ways.
			Also, while it opens the foldout, the ping effect is NOT reliable (Credit: Xarbrough, UF),
			Seems to be due to the fact that GameObject creation has not been completed
			(Credit: ArkaneX, UA). It does work correctly in version 2019.4 though. */
			EditorGUIUtility.PingObject(Selection.activeGameObject);
		}
		/* Code below creates GameObject while engaging rename mode, and is kept here
		just in case. */
		//private static void createGameObjectAsChild(object oMenuItemPath){
		//	GameObject gContext = Selection.activeGameObject;
		//	EditorApplication.ExecuteMenuItem(oMenuItemPath as string);
		//	GameObject gCreated = Selection.activeGameObject;
		//	Undo.RegisterCreatedObjectUndo(gCreated,"Whatever");
		//	GameObjectUtility.SetParentAndAlign(Selection.activeGameObject,gContext);
		//	EditorGUIUtility.PingObject(Selection.activeTransform);
		//}
		private static void executeMenuItemAsChild(object oMenuItemName){
			string menuItemName = oMenuItemName as string;
			if (menuItemName == null)
				return;
			GameObject gContext = Selection.activeGameObject;
			EditorApplication.ExecuteMenuItem(menuItemName);
			GameObject gCreated = Selection.activeGameObject;
			Undo.RegisterCreatedObjectUndo(gCreated, "Create GameObject");
			GameObjectUtility.SetParentAndAlign(gCreated, gContext);
			gCreated.transform.position = SceneView.lastActiveSceneView.pivot;
			if(gCreated.GetComponent<CanvasRenderer>())
				/* otherwise UIs will be created out of canvas plane */
				gCreated.transform.setLocalZ(0.0f);
			EditorGUIUtility.PingObject(Selection.activeGameObject);
			/* For whatever reason, when a GameObject is created VIA GAMEOBJECT MENU,
			GameObjectUtility.SetParentAndAlign(gCreated, gContext);
			Unity version 2020 automatically engage rename mode and will stubbornly
			remain in this mode despite any code in this function.
			Below is the only fix that I found working. */
			//Code for getting SceneView window, Credit: benzsuankularb, UA
			EditorWindow.GetWindow<SceneView>().Focus();
		}
		[Shortcut("ChameleonMenu/PopupAddMenu",KeyCode.A,ShortcutModifiers.Shift)]
		static void showAddMenu(){
			/* It seems that in version 2019.4 and below, context menu will show up at wrong
			position, which seems to be the position relative to SCREEN rather than focused window.
			HOWEVER, when calculating screen coordinate and showing it via DropDown(), version
			2020.3 shows it at wrong position (it shows relative to FOCUSED WINDOW coordinate
			despite documentation states it will use screen coordinate).
			As of now, I still cannot find elegant solution that works for both, except
			conditional compiilation. */
			#if UNITY_2020_3_OR_NEWER
			string sMouseOverWindow = EditorWindow.mouseOverWindow?.ToString();
			switch(sMouseOverWindow){
				case UnityWindowName.WINDOWNAME_HIERARCHY:
				case UnityWindowName.WINDOWNAME_SCENEVIEW:
					menuAddGameObject?.ShowAsContext();
					break;
				case UnityWindowName.WINDOWNAME_PROJECT:
					menuAddAsset?.ShowAsContext();
					break;
			}
			#else
			string sMouseOverWindow = EditorWindow.mouseOverWindow?.ToString();
			if(sMouseOverWindow == null)
				return;
			EditorWindow focusedWindow = EditorWindow.focusedWindow;
			Vector2 v2MousePosition = Event.current.mousePosition;
			Rect rectLocation = new Rect(
				(focusedWindow?.position.x ?? 0.0f) + v2MousePosition.x,
				(focusedWindow?.position.y ?? 0.0f) + v2MousePosition.y,
				0.0f,
				0.0f
			);
			switch(sMouseOverWindow){
				case UnityWindowName.WINDOWNAME_HIERARCHY:
				case UnityWindowName.WINDOWNAME_SCENEVIEW:
					menuAddGameObject?.DropDown(rectLocation);
					break;
				case UnityWindowName.WINDOWNAME_PROJECT:
					menuAddAsset?.DropDown(rectLocation);
					break;
			}
			#endif
		}
	}
	#endregion
//====================================================================================

//====================================================================================
	#region BLENDTREE
	[MenuItem("CONTEXT/BlendTree/Copy BlendTree Asset to Animator Path")]
	static void blendtreeCopyAsset(MenuCommand menuCommand){
		/* Note: The copied asset is DIFFERENT one from that embedded in the Animator.
		You need to relink the one in the Animator to the copied one if you want otherwise. */
		/* (Credit: Harekelas, UF) Since you can't use CreateAsset on already existing assets,
		you would need to clone it first and create asset from that. */
		/* IMPORTANT! : This method SHOW ERROR, but works for case of non-nested.
		Have to test the nested case more closely later (but it seems working currently). */
		BlendTree blendtree =
			Object.Instantiate<BlendTree>((BlendTree)Selection.activeObject);
        
		/* Clear console (Credit: gerudobomb, UF) */ 
		//var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
		//var clearMethod = logEntries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
		//clearMethod.Invoke(null, null);
		
		string dstPath =
			EditorPath.getSelectionFolder() + "/" + blendtree.name +".blendtree";
		AssetDatabase.CreateAsset(
			blendtree,
			dstPath
		);
		AssetDatabase.Refresh();
		EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<BlendTree>(dstPath));
	}
	#endregion
//====================================================================================

}
#endif

} //end namespace Chameleon
