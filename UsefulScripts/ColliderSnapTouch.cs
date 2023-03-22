/****************************************************************************
 * COLLIDERSNAPTOUCH (v1.01)
 * by Reev the Chameleon
 * 16 Jan 2
*****************************************************************************
Allow user to snap touch and align a collider to the nearest collider
in specified direction, both via code and via Collider Component
right-click menu.
Note: Because we cast ray out from CENTER of object, it is better to snap
small object to larger one to avoid cast miss.
Update v1.01: Add underline menu shortcut

TODO: Consider what to do if collider is on child object, and also
how to manage multiple objects selection.
*/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Chameleon{

public static class ColliderSnapTouch{
	public static bool snapTouch(this Collider collider,Vector3 v3Direction){ //v3Direction is global
		Vector3 v3ColliderPosition = collider.transform.position;
		RaycastHit raycastHitOther;
		RaycastHit raycastHitThis;

		/* Rough algorithm credit: LitchiSzu, Reddit */
		if(!Physics.Raycast(v3ColliderPosition,v3Direction,out raycastHitOther))
			return false;
		if(!Physics.Raycast(raycastHitOther.point,-v3Direction,out raycastHitThis))
			return false;
		Vector3 v3ThisSurfaceOffset = raycastHitThis.point - v3ColliderPosition;
		Quaternion deltaRotation =
			Quaternion.FromToRotation(raycastHitThis.normal,-raycastHitOther.normal);
		collider.transform.rotate(deltaRotation);
		v3ThisSurfaceOffset = deltaRotation*v3ThisSurfaceOffset;
		collider.transform.Translate(
			raycastHitOther.point-(v3ColliderPosition+v3ThisSurfaceOffset),
			Space.World
		);
		return true;
	}
//--------------------------------------------------------------------------------------------	
	#region EDITOR MENU
	#if UNITY_EDITOR
	private const string undoMessage = "Snap Touch Collider";
	private const string failMessage =
		"Warning: Collider snap touch failed, nothing to snap to.";

	[MenuItem("CONTEXT/Collider/&Snap Touch/-y axis (&Down)")]
	public static void snapDown(MenuCommand menuCommand){
		Collider collider = (Collider)menuCommand.context;
		Undo.RecordObject(collider.transform,undoMessage);
		if(!collider.snapTouch(Vector3.down))
			Debug.LogWarning(failMessage);
	}
	[MenuItem("CONTEXT/Collider/&Snap Touch/+y axis (&Up)")]
	public static void snapUp(MenuCommand menuCommand){
		Collider collider = (Collider)menuCommand.context;
		Undo.RecordObject(collider.transform,undoMessage);
		if(!collider.snapTouch(Vector3.up))
			Debug.LogWarning(failMessage);
	}
	[MenuItem("CONTEXT/Collider/&Snap Touch/-x axis (&Left)")]
	public static void snapLeft(MenuCommand menuCommand){
		Collider collider = (Collider)menuCommand.context;
		Undo.RecordObject(collider.transform,undoMessage);
		if(!collider.snapTouch(Vector3.left))
			Debug.LogWarning(failMessage);
	}
	[MenuItem("CONTEXT/Collider/&Snap Touch/+x axis (&Right)")]
	public static void snapRight(MenuCommand menuCommand){
		Collider collider = (Collider)menuCommand.context;
		Undo.RecordObject(collider.transform,undoMessage);
		if(!collider.snapTouch(Vector3.right))
			Debug.LogWarning(failMessage);
	}
	[MenuItem("CONTEXT/Collider/&Snap Touch/-z axis (&Back)")]
	public static void snapBack(MenuCommand menuCommand){
		Collider collider = (Collider)menuCommand.context;
		Undo.RecordObject(collider.transform,undoMessage);
		if(!collider.snapTouch(Vector3.back))
			Debug.LogWarning(failMessage);
	}
	[MenuItem("CONTEXT/Collider/&Snap Touch/+z axis (&Forward)")]
	public static void snapForward(MenuCommand menuCommand){
		Collider collider = (Collider)menuCommand.context;
		Undo.RecordObject(collider.transform,undoMessage);
		if(!collider.snapTouch(Vector3.forward))
			Debug.LogWarning(failMessage);
	}
	#endif
	#endregion
//--------------------------------------------------------------------------------------------	
}

//============================================================================================
#region COLLIDERSNAPTOUCH WINDOW
#if UNITY_EDITOR
public class ColliderSnapTouchWindow : EditorWindow{
	private Collider collider;
	private Quaternion qRotation =
		Quaternion.FromToRotation(Vector3.forward,Vector3.down);
	//Initialize as pointing downward
	private bool bHideTool = true;
	private bool bSavedHideTool;
	private bool bHit = true;
	
	[MenuItem("CONTEXT/Collider/&Snap Touch/&Custom Direction...",priority = 1100)]
	static void getSnapColliderWindow(MenuCommand menuCommand){
		//ColliderSnapTouchWindow snapColliderWindow = GetWindow<ColliderSnapTouchWindow>();
		ColliderSnapTouchWindow snapColliderWindow = GetWindowWithRect<ColliderSnapTouchWindow>(
			new Rect(0.0f,0.0f,300.0f,130.0f),
			true, //floating utility window
			"Custom Collider Snap Touch",
			true //focus
		);
		snapColliderWindow.collider = (Collider)menuCommand.context;
	}
	void OnEnable(){
		/* Used to be "onSceneGUIDelegate" (Credit: rhys_vdw, UA)
		This gets called whenever SceneView calls OnGUI(). */
		SceneView.duringSceneGui += onSceneGUI;
		bSavedHideTool = Tools.hidden;
	}
	void OnGUI(){
		if(!collider)
			Close();
		Vector3 v3Direction = EditorGUILayout.Vector3Field(
			"Snap Direction",
			(qRotation*Vector3.forward).roundFifthDecimal()
		);
		if(GUI.changed)
			qRotation.eulerAngles = v3Direction;
		
		bHideTool = EditorGUILayout.Toggle("Hide Transform Tool",bHideTool);
		Tools.hidden = bHideTool;
		
		bool bSavedGUIEnable = GUI.enabled;
		GUI.enabled = bHit;
		if(GUILayout.Button("Snap")){
			Undo.RecordObject(collider.transform,"Snap Touch Collider");
			ColliderSnapTouch.snapTouch(
				collider,
				qRotation*Vector3.forward
			);
			Close();
		}
		GUI.enabled = bSavedGUIEnable;
		if(!bHit){
			EditorGUILayout.HelpBox(
				"No snappable object found in that direction!",
				MessageType.Warning
			);
		}
	}
	void OnSelectionChange(){
		Close();
	}
	void OnDestroy(){
		SceneView.duringSceneGui -= onSceneGUI;
		Tools.hidden = bSavedHideTool;
	}
	void onSceneGUI(SceneView sceneView){
		Vector3 v3ColliderPosition = collider.transform.position;
		Vector3 v3Direction = qRotation*Vector3.forward;
		RaycastHit raycastHit;
		bHit = Physics.Raycast(v3ColliderPosition,v3Direction,out raycastHit);
		if(bHit)
			Handles.DrawDottedLine(v3ColliderPosition,raycastHit.point,2.0f);
		else //Just draw some segment (for now)
			Handles.DrawDottedLine(v3ColliderPosition,v3ColliderPosition+10.0f*v3Direction,2.0f);
		qRotation = Handles.RotationHandle(qRotation,v3ColliderPosition);
		if(GUI.changed)
			Repaint();
		/* Not doing undo here because handle and qRotation will be lost anyway
		if window closes. */
	}
}
#endif
#endregion
//============================================================================================

} //end namespace Chameleon

