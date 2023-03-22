/*******************************************************************************
 * BLENDERSCENE MIMICKER (v2.1)
 * by Reev the Chameleon
 * 15 Dec 2
 *******************************************************************************
Place this script into the project and set shortcuts to the following menus to allow
Blender-like functionality when manipulating GameObject in the scene:
- BlenderMimicker/Translate: Allow "Grab" like functionality. Press axis key (X,Y,Z)
once to lock movement along corresponding global axis, and twice to lock movement along 
local axis respectively. Press shift+axis key to lock movement along global and local
plane in similar manner. When in axis mode, user can also type number to move specified
distance in current locked axis direction.
- BlenderMimicker/Rotate: Allow "Rotate" like functionality. Press axis key (X,Y,Z)
once to lock rotation around global axis, and twice to lock rotation around local axis.
Press R to toggle on/off free rotation mode. When in axis mode, user can type number
to rotate by specified degrees around locked axis.
- BlenderMimicker/Scale: Allow "Scale" like functionality. By default, it scales
uniformly. Press axis key (X,Y,Z) once to lock scaling axis to corresponding global axis,
and press twice to lock it in corresponding local axis. Press shift+axis to lock
scaling along global plane and local plane in similar manner. In any mode, user can
type number to specified exact scaling factor.

Update v1.1: Change code to directly use shortkey and remove unused menu in menubar.
Add support for moving selection with multiple objects.
TODO: Consider how to add support for rotating and scaling selection with multiple objects.
Update v2.0: Add shortcuts for rotating SceneView to top, front, and side view.
Update v2.1: Add shortcut for focusing overlay Canvas
*/

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Runtime.InteropServices;
using UnityEditorInternal;
using UnityEditor.ShortcutManagement;

namespace Chameleon{

public static class BlenderSceneMimicker{
	private static bool bTracked = false;
	/* Want to save Transform, but cannot due to private constructor, so use
	TransformData helper class. */
	private static Transform activeTransform;
	private static Transform[] aTransform;
	private static TransformData originalActiveTransformData;
	private static TransformData[] aOriginalTransformData;
	private static Vector2 v2MouseStartPos; //already scaled by dpi scaling factor
	private static Vector3 v3MouseStartScenePos; //in sceneView coordinate (y-up)
	private static Vector3 v3ObjectStartScenePos; //in sceneView coordinate (y-up, relative sceneView)
	private static Vector2Int v2WrapCount;
	private static Rect rectWrapBound;
	private const int CONTROLID_HINT = 666;
	private static eAxis axis;
	private static bool bLocal;
	private static bool bPlane;
	private static bool bFreeRotation;
	private static Rect rectSceneViewExcludeToolbar = new Rect();
	private static Tool savedTool;
	private static PivotRotation savedPivotRotation;
	private static bool bMimicking;
	private const float FREEROTATION_DEGPERPIXEL = 0.5f; //px, for free rotation
	private static FloatBuffer floatBuffer = new FloatBuffer();
	private const float LABEL_WIDTH = 60.0f;
	private static readonly Rect rectFloatField =
		new Rect(5.0f,5.0f,150.0f,EditorGUIUtility.singleLineHeight);
	private static GUIStyle labelStyle = GUIStyleCollection.blackBoldLabel;
		
	/* [ShortcutAttribute] allows one to call function using purely shortcuts
	and without need to have it on the menu bar. However, it may cause
	shortcut conflict, in which user is required to intervene and resolve. */
	[Shortcut("BlenderSceneMimicker/Translate",KeyCode.G)]
	static void activateMimicTranslate(){
		if(!activateMimicVallidate())
			return;
		deactivateAllMimic(); //prevent 2 modes being active at the same time
		originalActiveTransformData = Selection.activeTransform.save();
		aOriginalTransformData = Selection.transforms.save();
		activeTransform = Selection.activeTransform;
		aTransform = Selection.transforms;
		SceneView.duringSceneGui += mimicBlenderTranslate;
		bMimicking = true;
	}
	[Shortcut("BlenderSceneMimicker/Rotate",KeyCode.R)]
	static void activateMimicRotate(){
		if(!activateMimicVallidate())
			return;
		deactivateAllMimic();
		originalActiveTransformData = Selection.activeTransform.save();
		SceneView.duringSceneGui += mimicBlenderRotate;
		bMimicking = true;
	}
	[Shortcut("BlenderSceneMimicker/Scale",KeyCode.S)]
	static void activateMimicScale(){
		if(!activateMimicVallidate())
			return;
		deactivateAllMimic();
		originalActiveTransformData = Selection.activeTransform.save();
		SceneView.duringSceneGui += mimicBlenderScale;
		bMimicking = true;
	}
	static bool activateMimicVallidate(){
		return 
			Selection.activeTransform != null &&
			EditorWindow.mouseOverWindow?.ToString() == UnityWindowName.WINDOWNAME_SCENEVIEW
		;
	}

	private static void deactivateAllMimic(){
		if(bMimicking)
			Selection.activeTransform.load(originalActiveTransformData);
		SceneView.duringSceneGui -= mimicBlenderTranslate;
		SceneView.duringSceneGui -= mimicBlenderRotate;
		SceneView.duringSceneGui -= mimicBlenderScale;
		bMimicking = false;
	}
	private static void initializeBlenderMimicMode(SceneView sceneView){
		labelStyle.normal.textColor = Color.black;
		Vector2 v2CurrentMousePos = Event.current.mousePosition;
		savedTool = Tools.current;
		savedPivotRotation = Tools.pivotRotation;
		/* sceneView.position returns (x,y) position of top-left corner of sceneView window,
		BUT it returns height of total window MINUS top tab pane, WHICH also includes
		the top toolbar. We want to avoid including this toolbar to wrapping rect, so
		change the y to the position of real window, which is GUI (0,0) AND deduct
		toolbarHeight/2 from height (there are 2 toolbars: tab pane and SceneView toolbar). */
		rectSceneViewExcludeToolbar = sceneView.position;
		rectSceneViewExcludeToolbar.y = GUIUtility.GUIToScreenPoint(Vector2.zero).y;
		rectSceneViewExcludeToolbar.height -= sceneView.toolbarHeight()/2;
		/* This makes sure we get SCREEN position not the position relative
		to some window (Credit: NiklasBorglund, UA). */
		v2MouseStartPos = GUIUtility.GUIToScreenPoint(v2CurrentMousePos);
		MouseWrapper.setBound(rectSceneViewExcludeToolbar);
		axis = eAxis.any;
		bLocal = false;
		bFreeRotation = false;
		Tools.pivotRotation = PivotRotation.Global;
		floatBuffer.clear();
		/* It seems Camera functions such as WorldToScreenPoint will return position
		as dpi SCALED (for example, if your window is 640px wide, object positioned
		in the middle will give screen point x=413.5 when Windows Display Settings
		scale is set to 125%). SetWindowPos (Win32) also uses SCALED value, while
		Event.mousePosition returns unscaled. Anyway we decided to save BOTH, namely
		v2MouseStartPos as SCALED SCREEN and v2MouseStartPosCamera as UNSCALED CAMERA
		SCREEN coordinate (with y-up) because they are both used in certain cases. */
		v2MouseStartPos *= MouseWrapper.dpiFactor; //dpi SCALED
		v3ObjectStartScenePos =
			Camera.current.WorldToScreenPoint(originalActiveTransformData.position); //sceneView coordinate
		v3MouseStartScenePos = new Vector3(
			MouseWrapper.dpiFactor * v2CurrentMousePos.x,
			MouseWrapper.dpiFactor * (rectSceneViewExcludeToolbar.height-v2CurrentMousePos.y),
			v3ObjectStartScenePos.z
		);
	}
	private static void exitBlenderMimicMode(){
		Tools.current = savedTool;
		Tools.pivotRotation = savedPivotRotation;
		bTracked = false;
		bMimicking = false;
	}
	static void mimicBlenderTranslate(SceneView sceneView){
		/* Not sure whether I do controlID correctly or not. Usually it MUST ALWAYS
		be called because Unity does NOT save the control (as always anything in IMGUI), 
		but rather assigns controlID in the same sequence so that if same controls call
		GetControlID in the same GUI loop will be unique (for example, EventType.Layout
		and EventType.Repaint the control must have same controlID so that each control
		can paint itself correctly) (Credit: SisusCo, UF). But here I have a handler function
		that keeps subscribing/unsubscribing so the code may not be proper. Anyway it is
		indicated in documentation that Keyboard/Mouse Event is processed last in the loop,
		and the code seems to work correctly as far as test goes, so keep it like this for now. */
		int thisControlID = GUIUtility.GetControlID(CONTROLID_HINT,FocusType.Passive);
		GUIUtility.keyboardControl = thisControlID; //Prevent sending strange KeyDown to sceneView
		Event currentEvent = Event.current;
		EventType eventType = currentEvent.GetTypeForControl(thisControlID);
		Vector3 vDelta;
		if(!activeTransform){
			/* User may somehow delete GameObject while in mimic mode, so catch that
			(though unlikely) */
			exitBlenderMimicMode();
			SceneView.duringSceneGui -= mimicBlenderTranslate;
			return;
		}
		/* Unity Editor loses focus. Can happen due to alt+tab (Credit: llMarty, UF) */
		if(!InternalEditorUtility.isApplicationActive)
			return;

		if(!bTracked){
			initializeBlenderMimicMode(sceneView);
			sceneView.Focus(); //So we can receive keyboard input (Credit: Bunny83, UA)
			bTracked = true;
		}
		else{ //bTracked
			Tools.current = Tool.Move;
			Tools.pivotRotation = bLocal ? PivotRotation.Local : PivotRotation.Global;
			if(eventType == EventType.MouseDown){
				/* This prevents sceneView from receiving MouseDown and deselect object */
				GUIUtility.hotControl = thisControlID;
				if(currentEvent.button==0){ //LMB
					/* I wanted to record it at the beginning of operation, but it seems
					you cannot mess with Undo in OnGUI() and OnSceneGUI() seemingly because
					they themselves USE Undo to record their operations. (Credit: Arkade, UF) */
					for(int i=0; i<aTransform.Length; ++i)
						aTransform[i].position = aOriginalTransformData[i].position;
					Undo.RecordObjects(aTransform,"Move");
					vDelta = calculatePosition() - originalActiveTransformData.position;
					for(int i=0; i<aTransform.Length; ++i){
						aTransform[i].position =
							aOriginalTransformData[i].position + vDelta;
					}
				}
				else{
					for(int i=0; i<aTransform.Length; ++i)
						aTransform[i].position = aOriginalTransformData[i].position;
				}
				exitBlenderMimicMode();
				SceneView.duringSceneGui -= mimicBlenderTranslate;
				return;
			}
			else if(eventType == EventType.MouseUp){
				GUIUtility.hotControl = 0; //release hot control
				return;
			}
			else if(eventType == EventType.KeyDown){
				switch(currentEvent.keyCode){
					case KeyCode.Return:
					case KeyCode.Space:
					case KeyCode.KeypadEnter:
						for(int i=0; i<aTransform.Length; ++i)
							aTransform[i].position = aOriginalTransformData[i].position;
						Undo.RecordObjects(aTransform,"Move");
						vDelta = calculatePosition() - originalActiveTransformData.position;
						for(int i=0; i<aTransform.Length; ++i){
							aTransform[i].position =
								aOriginalTransformData[i].position + vDelta;
						}
						exitBlenderMimicMode();
						SceneView.duringSceneGui -= mimicBlenderTranslate;
						return;
					case KeyCode.Escape:
						activeTransform.position = originalActiveTransformData.position;
						exitBlenderMimicMode();
						SceneView.duringSceneGui -= mimicBlenderTranslate;
						return;
					case KeyCode.X:
						bPlane = currentEvent.shift;
						cycleAxisOption(eAxis.x);
						break;
					case KeyCode.Y:
						bPlane = currentEvent.shift;
						cycleAxisOption(eAxis.y);
						break;
					case KeyCode.Z:
						bPlane = currentEvent.shift;
						cycleAxisOption(eAxis.z);
						break;
					case KeyCode.Backspace: //does not produce Event.current.character
						floatBuffer.backspace();
						break;
				}
				/* Other characters. Add to numberBuffer and try parse to float */
				if(axis != eAxis.any && !bPlane){
					char c = Event.current.character;
					floatBuffer.append(Event.current.character);
				}
				Event.current.Use(); //Prevent sending strange KeyDown to sceneView
			}
			/* I wanted to check and process only when EventType is EventType.MouseMove,
			but it turns out we don't receive that type if mouse is within menubar or in
			the taskbar down below. Hence need to process everything to ensure catching that.
			Also, choosing to process only some EventType will cause transform to jitter. */
			vDelta = calculatePosition() - originalActiveTransformData.position;
			for(int i=0; i<aTransform.Length; ++i)
				aTransform[i].position = aOriginalTransformData[i].position + vDelta;
		}
	}
	static void mimicBlenderRotate(SceneView sceneView){
		int thisControlID = GUIUtility.GetControlID(CONTROLID_HINT,FocusType.Passive);
		GUIUtility.keyboardControl = thisControlID;
		Transform transform = Selection.activeTransform;
		Event currentEvent = Event.current;
		EventType eventType = currentEvent.GetTypeForControl(thisControlID);
		if(!transform){
			exitBlenderMimicMode();
			SceneView.duringSceneGui -= mimicBlenderRotate;
			return;
		}
		if(!InternalEditorUtility.isApplicationActive)
			return;

		if(!bTracked){
			initializeBlenderMimicMode(sceneView);
			sceneView.Focus();
			bTracked = true;
		}
		else{
			Tools.current = Tool.Rotate;
			Tools.pivotRotation = bLocal ? PivotRotation.Local : PivotRotation.Global;
			if(eventType == EventType.MouseDown){
				GUIUtility.hotControl = thisControlID;
				if(currentEvent.button==0){ //LMB
					transform.rotation = originalActiveTransformData.rotation;
					Undo.RecordObject(transform,"Rotate");
					transform.rotation = calculateRotation();
				}
				else
					transform.rotation = originalActiveTransformData.rotation;
				exitBlenderMimicMode();
				SceneView.duringSceneGui -= mimicBlenderRotate;
				currentEvent.Use();
				return;
			}
			else if(eventType == EventType.MouseUp){
				GUIUtility.hotControl = 0;
				return;
			}
			else if(eventType == EventType.KeyDown){
				switch(currentEvent.keyCode){
					case KeyCode.Return:
					case KeyCode.Space:
					case KeyCode.KeypadEnter:
						transform.rotation = originalActiveTransformData.rotation;
						Undo.RecordObject(transform,"Rotate");
						transform.rotation = calculateRotation();
						exitBlenderMimicMode();
						SceneView.duringSceneGui -= mimicBlenderRotate;
						return;
					case KeyCode.Escape:
						transform.rotation = originalActiveTransformData.rotation;
						exitBlenderMimicMode();
						SceneView.duringSceneGui -= mimicBlenderRotate;
						return;
					case KeyCode.X:
						bFreeRotation = false;
						cycleAxisOption(eAxis.x);
						break;
					case KeyCode.Y:
						bFreeRotation = false;
						cycleAxisOption(eAxis.y);
						break;
					case KeyCode.Z:
						bFreeRotation = false;
						cycleAxisOption(eAxis.z);
						break;
					case KeyCode.R:
						bFreeRotation = !bFreeRotation;
						axis = eAxis.any;
						break;
					case KeyCode.Backspace:
						floatBuffer.backspace();
						break;
				}
				if(axis != eAxis.any){
					char c = Event.current.character;
					floatBuffer.append(Event.current.character);
				}
				Event.current.Use();
			}
			transform.rotation = calculateRotation();
		}
	}
	static void mimicBlenderScale(SceneView sceneView){
		int thisControlID = GUIUtility.GetControlID(CONTROLID_HINT,FocusType.Passive);
		GUIUtility.keyboardControl = thisControlID;
		Transform transform = Selection.activeTransform;
		Event currentEvent = Event.current;
		EventType eventType = currentEvent.GetTypeForControl(thisControlID);
		if(!transform){
			exitBlenderMimicMode();
			SceneView.duringSceneGui -= mimicBlenderScale;
			return;
		}
		if(!InternalEditorUtility.isApplicationActive)
			return;

		if(!bTracked){
			initializeBlenderMimicMode(sceneView);
			sceneView.Focus();
			bLocal = true; //scale tool only has local PivotRotation
			bTracked = true;
		}
		else{
			Tools.current = Tool.Scale;
			if(eventType == EventType.MouseDown){
				GUIUtility.hotControl = thisControlID;
				if(currentEvent.button==0){ //LMB
					transform.localScale = originalActiveTransformData.localScale;
					Undo.RecordObject(transform,"Scale");
					transform.localScale = calculateScale();
				}
				else
					transform.localScale = originalActiveTransformData.localScale;
				exitBlenderMimicMode();
				SceneView.duringSceneGui -= mimicBlenderScale;
				currentEvent.Use();
				return;
			}
			else if(eventType == EventType.MouseUp){
				GUIUtility.hotControl = 0;
				return;
			}
			else if(eventType==EventType.KeyDown){
				switch(currentEvent.keyCode){
					case KeyCode.Return:
					case KeyCode.Space:
					case KeyCode.KeypadEnter:
						transform.localScale = originalActiveTransformData.localScale;
						Undo.RecordObject(transform,"Scale");
						transform.localScale = calculateScale();
						exitBlenderMimicMode();
						SceneView.duringSceneGui -= mimicBlenderScale;
						currentEvent.Use();
						return;
					case KeyCode.Escape:
						transform.localScale = originalActiveTransformData.localScale;
						exitBlenderMimicMode();
						SceneView.duringSceneGui -= mimicBlenderScale;
						currentEvent.Use();
						return;
					case KeyCode.X:
						bPlane = currentEvent.shift;
						axis = axis==eAxis.x ? eAxis.any : eAxis.x;
						break;
					case KeyCode.Y:
						bPlane = currentEvent.shift;
						axis = axis==eAxis.y ? eAxis.any : eAxis.y;
						break;
					case KeyCode.Z:
						bPlane = currentEvent.shift;
						axis = axis==eAxis.z ? eAxis.any : eAxis.z;
						break;
					case KeyCode.Backspace:
						floatBuffer.backspace();
						break;
				}
				char c = Event.current.character;
				floatBuffer.append(Event.current.character);
				Event.current.Use();
			}
			transform.localScale = calculateScale();
		}
	}
	private static void cycleAxisOption(eAxis in_axis){
		floatBuffer.clear();
		if(axis != in_axis){
			axis = in_axis;
			bLocal = false;
		}
		else if(!bLocal){
			bLocal = true;
		}
		else{
			axis = eAxis.any;
			bLocal = false;
		}
	}
	private static Vector3 calculatePosition(){
		/* NOTE: Not working in orthographic mode, for now */
		Vector2 v2ScreenMove =
			MouseWrapper.wrapMousePos(true)-v2MouseStartPos; //dpi scaled
		/* dpi scale relative to sceneView */
		v2ScreenMove.y = -v2ScreenMove.y; //because screen point is y-up.
		if(axis != eAxis.any){
			Vector3 v3Direction;
			switch(axis){
				case eAxis.x:
					v3Direction = bLocal ? originalActiveTransformData.right : Vector3.right;
					break;
				case eAxis.y:
					v3Direction = bLocal ? originalActiveTransformData.up : Vector3.up;
					break;
				case eAxis.z:
					v3Direction = bLocal ? originalActiveTransformData.forward : Vector3.forward;
					break;
				default:
					v3Direction = Vector3.zero;
					break;
			}
		//-------------------------------------------------------------------------------
			#region LOCK AXIS
			if(!bPlane){
				if(floatBuffer.hasContent()){				
					drawFloatBuffer();
					return originalActiveTransformData.position + floatBuffer.Value*v3Direction;
				}
				Vector2 v2ScreenAxis =
					(Camera.current.WorldToScreenPoint(originalActiveTransformData.position+v3Direction) -
					v3ObjectStartScenePos)
					.normalized
				; //discard z component because they are equal
				Vector2 v2ScreenProjectMove =
					Vector2.Dot(v2ScreenMove,v2ScreenAxis) * v2ScreenAxis;
				return skewLineClosestPoint1(
					originalActiveTransformData.position,
					v3Direction,
					Camera.current.transform.position,
					Camera.current.ScreenPointToRay(new Vector3(
						v3ObjectStartScenePos.x + v2ScreenProjectMove.x,
						v3ObjectStartScenePos.y + v2ScreenProjectMove.y,
						v3ObjectStartScenePos.z
					)).direction
				);
			}
			#endregion
		//-------------------------------------------------------------------------------
			#region LOCK PLANE
			else { //bPlane
				return linePlaneIntersection(
					Camera.current.transform.position,
					Camera.current.ScreenPointToRay(new Vector3(
						v3ObjectStartScenePos.x + v2ScreenMove.x,
						v3ObjectStartScenePos.y  + v2ScreenMove.y,
						v3ObjectStartScenePos.z
					)).direction,
					originalActiveTransformData.position,
					v3Direction
				);
			}
			#endregion
		//-------------------------------------------------------------------------------
		}
		else{ //eAxis.any
			return Camera.current.ScreenToWorldPoint(new Vector3(
				v3ObjectStartScenePos.x + v2ScreenMove.x,
				v3ObjectStartScenePos.y + v2ScreenMove.y,
				v3ObjectStartScenePos.z
			));
		}
	}
	private static Quaternion calculateRotation(){
	//---------------------------------------------------------------------------------
		#region FREEROTATION
		if(bFreeRotation){
			Vector2 v2ScreenMove = MouseWrapper.wrapMousePos(true)-v2MouseStartPos;
			v2ScreenMove.y = -v2ScreenMove.y;
			Vector3 v3RotationAxis =
				Quaternion.AngleAxis(-90.0f,Camera.current.transform.forward) * //counterclockwise in left-handed
				Camera.current.ScreenToWorldPoint(new Vector3(
					v3ObjectStartScenePos.x + v2ScreenMove.x,
					v3ObjectStartScenePos.y + v2ScreenMove.y,
					v3ObjectStartScenePos.z
				))
			;
			return Quaternion.AngleAxis(
				v2ScreenMove.magnitude * FREEROTATION_DEGPERPIXEL,
				v3RotationAxis
			) * originalActiveTransformData.rotation;
		}
		#endregion
	//---------------------------------------------------------------------------------
		/* Not Free Rotation */
		Vector2 v2ScreenPos = GUIUtility.ScreenToGUIPoint(
			MouseWrapper.wrapMousePos(false)
		);
		Vector3 v3ScenePos = new Vector3(
			MouseWrapper.dpiFactor * v2ScreenPos.x,
			MouseWrapper.dpiFactor * (rectSceneViewExcludeToolbar.height-v2ScreenPos.y),
			v3ObjectStartScenePos.z
		);
		Vector3 v3MouseObjectStartOffset; //in SCENE coordinate
		Vector3 v3MouseObjectOffset;
		if(axis != eAxis.any){
			Vector3 v3Direction;
			switch(axis){
				case eAxis.x:
					v3Direction = bLocal ? originalActiveTransformData.right : Vector3.right;
					break;
				case eAxis.y:
					v3Direction = bLocal ? originalActiveTransformData.up : Vector3.up;
					break;
				case eAxis.z:
					v3Direction = bLocal ? originalActiveTransformData.forward : Vector3.forward;
					break;
				default:
					v3Direction = Vector3.zero;
					break;
			}
			/* We are not doing bPlane here because it is redundant. */
			if(floatBuffer.hasContent()){
				drawFloatBuffer();
				return Quaternion.AngleAxis(floatBuffer.Value,v3Direction) *
					originalActiveTransformData.rotation;
			}
			v3MouseObjectStartOffset = linePlaneIntersection(
				Camera.current.transform.position,
				Camera.current.ScreenPointToRay(v3MouseStartScenePos).direction,
				originalActiveTransformData.position,
				v3Direction
			) - originalActiveTransformData.position;
			v3MouseObjectOffset = linePlaneIntersection(
				Camera.current.transform.position,
				Camera.current.ScreenPointToRay(v3ScenePos).direction,
				originalActiveTransformData.position,
				v3Direction
			) - originalActiveTransformData.position;
		}
		else{ //eAxis.any
			v3MouseObjectStartOffset =
				Camera.current.ScreenToWorldPoint(v3MouseStartScenePos) -
				originalActiveTransformData.position
			;
			v3MouseObjectOffset = 
				Camera.current.ScreenToWorldPoint(v3ScenePos) -
				originalActiveTransformData.position
			;
		}
		return Quaternion.FromToRotation(
			v3MouseObjectStartOffset,
			v3MouseObjectOffset
		) * originalActiveTransformData.rotation;
	}
	private static Vector3 calculateScale(){
		Vector3 resultScale = originalActiveTransformData.localScale;
		float scaleFactor;
		if(!floatBuffer.hasContent()){
			Vector2 v2ScreenPos = GUIUtility.ScreenToGUIPoint(
				MouseWrapper.wrapMousePos(false)
			);
			Vector3 v3ScenePos = new Vector3(
				MouseWrapper.dpiFactor * v2ScreenPos.x,
				MouseWrapper.dpiFactor * (rectSceneViewExcludeToolbar.height-v2ScreenPos.y),
				v3ObjectStartScenePos.z
			);
			Vector2 v2StartSceneOffset = v3MouseStartScenePos-v3ObjectStartScenePos;
			Vector2 v2SceneOffset = v3ScenePos-v3ObjectStartScenePos;
			scaleFactor = v2SceneOffset.magnitude/v2StartSceneOffset.magnitude;
		}
		else{ //floatBuffer hasContent
			drawFloatBuffer();
			scaleFactor = floatBuffer.Value;
		}
		switch(axis){
			case eAxis.x:
				if(!bPlane)
					resultScale.x *= scaleFactor;
				else{
					resultScale.y *= scaleFactor;
					resultScale.z *= scaleFactor;
				}
				break;
			case eAxis.y:
				if(!bPlane)
					resultScale.y *= scaleFactor;
				else{
					resultScale.x *= scaleFactor;
					resultScale.z *= scaleFactor;
				}
				break;
			case eAxis.z:
				if(!bPlane)
					resultScale.z *= scaleFactor;
				else{
					resultScale.x *= scaleFactor;
					resultScale.y *= scaleFactor;
				}
				break;
			default: //eAxis.any
				resultScale *= scaleFactor;
				break;
		}
		return resultScale;
	}
	private static void drawFloatBuffer(){
		/* Must be called during SceneGUI() or similar */
		string label = bLocal ? "Local " : "Global ";
		if(bPlane){
			switch(axis){
				case eAxis.x: label += "YZ"; break;
				case eAxis.y: label += "XZ"; break;
				case eAxis.z: label += "XY"; break;
			}
		}
		else
			label += axis.ToString();

		float savedLabelWidth = EditorGUIUtility.labelWidth;
		EditorGUIUtility.labelWidth = LABEL_WIDTH;
		Handles.BeginGUI();
		/* To specify label style, has to draw separately (Credit: TonyLi, UF) */
		EditorGUI.LabelField(rectFloatField,new GUIContent(label),labelStyle);
		EditorGUI.TextField(
			rectFloatField,
			" ",
			floatBuffer.ToString()
		);
		EditorGUIUtility.labelWidth = savedLabelWidth;
		Handles.EndGUI();
	}
	private static Vector3 linePlaneIntersection(Vector3 pointLine,Vector3 directionLine,
		Vector3 pointPlane,Vector3 normalPlane)
	{
		/* Construct the equation using the fact that any lines on plane is perpendicular
		to normal and solve it, then substitute back into line equation. */
		return pointLine + directionLine*
			Vector3.Dot(pointPlane-pointLine,normalPlane)/Vector3.Dot(directionLine,normalPlane)
		;
	}
	private static Vector3 skewLineClosestPoint1(Vector3 pointLine1,Vector3 directionLine1,
		Vector3 pointLine2,Vector3 directionLine2) //Credit: Wikipedia, skew line
	{
		/* Concept is that shortest line linking two lines is perpendicular to both.
		line2 and this shortest line together form a plane. The intersection point
		of line1 with this plane is the closest point on line1. Then call to function
		linePlaneIntersection is omitted to save performance. */
		Vector3 normalPlane2 = Vector3.Cross(directionLine2,Vector3.Cross(directionLine1,directionLine2));
		return pointLine1 + directionLine1*
			Vector3.Dot(pointLine2-pointLine1,normalPlane2)/Vector3.Dot(directionLine1,normalPlane2)
		;
	}
}

/*** Only usable ON WINDOWS (for now) ***/
public static class MouseWrapper{
	private static Vector2Int v2IntWrapCount;
	private static Vector2 prevMousePos; //dpi scaled
	private static Rect rectWrap; //dpi scaled
	public static float boundLimit = 10.0f; //dpi scaled
	public static float dpiFactor{get; private set;} = 1.0f;

	/* I tried as hard as I can to set position using Unity native function but I
	could not. In the end I have to use Win32. If it comes to this I might as well
	learn how to marshal so I can also use my own C++ code here. */
	[DllImport("user32.dll")]
	private static extern bool SetCursorPos(int x,int y); // Credit: zachwuzhere, UA
	//[StructLayout(LayoutKind.Sequential)]
	//private struct POINT{ //Credit: Mo0gles, SO
	//	public int X;
	//	public int Y;
	//}
	//[DllImport("user32.dll")]
	//private static extern bool GetCursorPos(out POINT lpPoint);

	public static void setBound(Rect rectWrapUnscaled){
		/* This is hackish, but I can't find other way to retrieved dpi factor
		(Scaling in Windows Display Settings) at the moment, so I will use hardcoded
		number 96, which is Windows default pixel per 1 logical unit, for now. */
		dpiFactor = Screen.dpi/96.0f;
		v2IntWrapCount = new Vector2Int(0,0);
		prevMousePos = new Vector3(-1.0f,-1.0f,-1.0f); //invalidate prevMousePos
		rectWrap = new Rect(
			dpiFactor*rectWrapUnscaled.x + boundLimit,
			dpiFactor*rectWrapUnscaled.y + boundLimit,
			dpiFactor*rectWrapUnscaled.width -2*boundLimit,
			dpiFactor*rectWrapUnscaled.height -2*boundLimit
		);
	}
	/* returns accumulate SCREEN position, optionally dpi scaled. */
	public static Vector2 wrapMousePos(bool bDpiScale){
		/* To wrap the mouse around, Unity has EditorGUIUtility.SetWantsMouseJumping(1);
		BUT that only jumps mouse around when dragging, and it also defaults to using
		whole editor window for rectWrap. In this case, we will check the mouse position,
		and if it is within 10px to the edge, we jump it around. This seems to be
		also how Unity (and Blender?) do it, as seen when you drag the Handle. */
		Vector2 v2MousePos = 
			dpiFactor*GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
		if(v2MousePos.x%rectWrap.width != prevMousePos.x%rectWrap.width ||
			v2MousePos.y%rectWrap.height != prevMousePos.y%rectWrap.height)
		{
			prevMousePos = v2MousePos;
			if(v2MousePos.x < rectWrap.x){
				--v2IntWrapCount.x;
				v2MousePos.x += rectWrap.width;
				SetCursorPos((int)v2MousePos.x,(int)v2MousePos.y);
			}
			else if(v2MousePos.x > rectWrap.xMax){
				++v2IntWrapCount.x;
				v2MousePos.x -= rectWrap.width;
				SetCursorPos((int)v2MousePos.x,(int)v2MousePos.y);
			}
			if(v2MousePos.y < rectWrap.y){
				--v2IntWrapCount.y;
				v2MousePos.y += rectWrap.height;
				SetCursorPos((int)v2MousePos.x,(int)v2MousePos.y);  
			}
			else if(v2MousePos.y > rectWrap.yMax){
				++v2IntWrapCount.y;
				v2MousePos.y -= rectWrap.height;
				SetCursorPos((int)v2MousePos.x,(int)v2MousePos.y);
			}
		}
		Vector2 v2ScaledAcccumMousePos = new Vector2(
			v2IntWrapCount.x*rectWrap.width+v2MousePos.x,
			v2IntWrapCount.y*rectWrap.height+v2MousePos.y
		); //dpi scaled
		return bDpiScale ? v2ScaledAcccumMousePos : v2ScaledAcccumMousePos/dpiFactor;
	}
}

public static class SceneViewRotator{
	private static double startTime;
	private static Quaternion qStart;
	private static Quaternion qEnd;
	private static bool bInProgress = false;
	
	public static float lerpTime = 0.3f;
	public static void lerpTo(Quaternion in_qEnd){
		if(!bInProgress && !SceneView.lastActiveSceneView.in2DMode){
			bInProgress = true;
			startTime = EditorApplication.timeSinceStartup;
			qStart = SceneView.lastActiveSceneView.rotation;
			qEnd = in_qEnd;
			EditorApplication.update += update; //Credit: MogulTech, UF
		}
	}
	public static void update(){
		float t = (float)(EditorApplication.timeSinceStartup-startTime)/lerpTime;
		if(t > 1.0f){
			SceneView.lastActiveSceneView.rotation = qEnd;
			EditorApplication.update -= update;
			bInProgress = false;
			return;
		}
		SceneView.lastActiveSceneView.rotation = Quaternion.Lerp(qStart,qEnd,t);
	}
	[Shortcut("SceneViewRotator/TopView",KeyCode.Keypad7)]
	public static void topView(){
		lerpTo(QuaternionExtension.down);
	}
	[Shortcut("SceneViewRotator/BottomView",KeyCode.Keypad7,ShortcutModifiers.Action)] //means ctrl
	public static void BottomView(){
		lerpTo(QuaternionExtension.up);
	}
	[Shortcut("SceneViewRotator/FrontView",KeyCode.Keypad1)]
	public static void frontView(){
		/* For most 3D apps, front view means looking forward, which is
		inconsistent with top and side view, but that is how it is (Credit: Regnas, blender.community) */
		lerpTo(Quaternion.identity);
	}
	[Shortcut("SceneViewRotator/BackView",KeyCode.Keypad1,ShortcutModifiers.Action)]
	public static void backView(){
		/* For most 3D apps, front view means looking forward, which is
		inconsistent with top and side view, but that is how it is (Credit: Regnas, blender.community) */
		lerpTo(QuaternionExtension.back);
	}
	[Shortcut("SceneViewRotator/RightView",KeyCode.Keypad3)]
	public static void rightView(){
		lerpTo(QuaternionExtension.left);
	}
	[Shortcut("SceneViewRotator/LeftView",KeyCode.Keypad3,ShortcutModifiers.Action)]
	public static void leftView(){
		lerpTo(QuaternionExtension.right);
	}
	[Shortcut("SceneViewRotator/Toggle Orthographic Camera",KeyCode.Keypad5)]
	public static void toggleOrthographicCamera(){
		SceneView.lastActiveSceneView.orthographic = !SceneView.lastActiveSceneView.orthographic;
	}
	[Shortcut("SceneViewRotator/Toggle 2D View",KeyCode.Keypad2)]
	public static void toggle2DView(){
		SceneView.lastActiveSceneView.in2DMode = !SceneView.lastActiveSceneView.in2DMode;
	}
	[Shortcut("SceneViewRotator/Frame Overlay Canvas",KeyCode.Keypad6)]
	public static void frameOverlayCanvas(){
		/* I Could have calculated where to place the camera, but that needs size of GameView,
		which how to find is still controversial. I could have use FindObjectsOfType to find
		a Canvas Component that is an overlay canvas, but that searches the whole scene.
		Finally, I ended up with this method. */
		GameObject g = new GameObject();
		g.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
		Object prevActiveObject = Selection.activeObject;
		Selection.activeObject = g;
		EditorApplication.delayCall += ()=>{
			SceneView.lastActiveSceneView.FrameSelected();
			Object.DestroyImmediate(g);
			Selection.activeObject = prevActiveObject;
		};
	}
}

} //end namespace Chameleon

#endif
