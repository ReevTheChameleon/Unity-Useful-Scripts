/*************************************************************************
 * UIANCHORSNAPPER (v1.0)
 * by Reev the Chameleon
 * 13 Apr 2
**************************************************************************
Usage:
Normally, UI dimension snaps to the anchors but not the other way around.
Attach this script to a UI element to allow anchors to snap to its edges,
in case you have dimension laid out correctly but want to adjust anchors
afterward.
Note: It seems that keeping this script into real build will cause
warning about missing MonoBehaviour. Will investigate and resolve later.
In the meantime, it is safe to just unattach this script from the UI
before build.
*/

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

namespace Chameleon{

public class UIAnchorSnapper : MonoBehaviour{
	public bool bSnapAnchor = true;
	public float snapLimit = 0.01f; //as percentage of parent dimension
	
	void Reset(){
		if(!(transform is RectTransform)){
			Debug.LogError("Error: This GameObject does not use RectTransform!");
			DestroyImmediate(this);
		}
	}
}

[CustomEditor(typeof(UIAnchorSnapper))]
class TestUISnapperEditor : Editor{
	private UIAnchorSnapper targetAs;
	private RectTransform rt = null;
	private RectTransform rtParent = null;
	private Vector2 prevAnchorMin;
	private Vector2 prevAnchorMax;

	void OnEnable(){
		targetAs = target as UIAnchorSnapper;
		rt = targetAs.transform as RectTransform;
		rtParent = targetAs.transform.parent as RectTransform;
		prevAnchorMin = rt.anchorMin;
		prevAnchorMax = rt.anchorMax;
	}
	void OnSceneGUI(){
		if(!targetAs.bSnapAnchor || !rt || !rtParent ||
			(rt.anchorMin==prevAnchorMin && rt.anchorMax==prevAnchorMax))
			return;
		prevAnchorMin = rt.anchorMin;
		prevAnchorMax = rt.anchorMax;
		
		float snapPixelDistance = targetAs.snapLimit * rtParent.rect.width;
		Vector2 v2NewAnchorMin = rt.anchorMin;
		Vector2 v2NewAnchorMax = rt.anchorMax;
		Vector2 v2OffsetMin = rt.offsetMin;
		Vector2 v2OffsetMax = rt.offsetMax;
		if(Mathf.Abs(rt.offsetMin.x) < snapPixelDistance){
			//anchor is stored as FRACTION of parent's dimension
			v2NewAnchorMin.x += rt.offsetMin.x/rtParent.rect.width;
			v2OffsetMin.x = 0.0f;
		}
		if(Mathf.Abs(rt.offsetMin.y) < snapPixelDistance){
			v2NewAnchorMin.y += rt.offsetMin.y/rtParent.rect.height;
			v2OffsetMin.y = 0.0f;
		}
		if(Mathf.Abs(rt.offsetMax.x) < snapPixelDistance){
			v2NewAnchorMax.x += rt.offsetMax.x/rtParent.rect.width;
			v2OffsetMax.x = 0.0f;
		}
		if(Mathf.Abs(rt.offsetMax.y) < snapPixelDistance){
			v2NewAnchorMax.y += rt.offsetMax.y/rtParent.rect.height;
			v2OffsetMax.y = 0.0f;
		}
		rt.anchorMin = v2NewAnchorMin;
		rt.anchorMax = v2NewAnchorMax;
		rt.offsetMin = v2OffsetMin;
		rt.offsetMax = v2OffsetMax;
	}
}

} //end namespace Chameleon

#endif
