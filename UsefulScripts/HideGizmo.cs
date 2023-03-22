/************************************************************************
 * HIDEGIZMO (v1.1)
 * by Reev the Chameleon
 * 7 Jan 2
 ************************************************************************
Attach this script to a GameObject allows showing/hiding its transform
gizmo as desire.
Update v1.1: Fix bug where all gizmos may start hidden when switch mode.
*/

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

namespace Chameleon{

public sealed class HideGizmo : MonoBehaviour{
	[SerializeField] bool bHide;

	void OnValidate(){
		/* Since OnValidate() is also called at beginning of scene load without any
		GameObject selected, this prevent starting scene with all gizmos hidden. */
		if(Selection.activeGameObject == gameObject)
			Tools.hidden = bHide;
	}

	[CustomEditor(typeof(HideGizmo))]
	class HideGizmoEditor : Editor{
		/* These work even when Inspector tab is closed. */
		void OnEnable(){
			Tools.hidden = ((HideGizmo)target).bHide; //Credit: Michael-Ryan, UF
		}
		void OnDisable(){
			Tools.hidden = false; //May consider using saved gizmo state if relevant
		}
	}
}

} //end namespace Chameleon

#endif
