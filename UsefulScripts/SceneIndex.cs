/*************************************************************************
 * SCENEINDEX (v1.2.1)
 * by Reev the Chameleon
 * 21 Dec 2
**************************************************************************
Usage:
Declare serialized variable of type SceneIndex like so:
[SerializeField] SceneIndex sceneIndex;
Then assign Scene to the field in the inspector.
This class is designed to work with SceneManager.LoadScene, so
one does not have to specify scene to load by string.
If the scene has not been added to Build Settings, it will show
warning sign in the inspector. Otherwise it will show scene index
as assigned in the Build Settings.

Update v1.1: Deal with index shift when scene is deleted/inactive in Build Settings
Update v1.2: Add code to update SceneIndex when scenes in Build Settings change
Update v1.2.1: Fix bug where unassigned SceneIndex has index 0 at start
*/

using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
#endif

using Object = UnityEngine.Object;

namespace Chameleon{

[Serializable]
public partial class SceneIndex{
	[SerializeField] int index = -1;
	public int Index{
		get{
		#if UNITY_EDITOR
			validateIndex();
		#endif
			return index;
		}
	}
	public static implicit operator int(SceneIndex sceneIndex){
		return sceneIndex.Index;
	}
	
	#if UNITY_EDITOR
	[SerializeField] SceneAsset sceneAsset;

	private void validateIndex(){
		index = SceneUtility.GetBuildIndexByScenePath(AssetDatabase.GetAssetPath(sceneAsset));
	}
	/* Below makes sure that when the scenes in Build Settings change, it is reflected
	to SceneIndex. This is important because user can modify scenes in Build Settings
	and build right away, which can cause WRONG scenes to loaded (worse than not loading
	and throwing exception).
	I have tried methods such as preprocess build callbacks and [PostProcessScene],
	but the former requires that you inherit from IPreprocessBuildWithReport, which
	only exists in UnityEditor namespace, and the latter only works with static functions.
	So this seems to be the best way for now.
	Constructors are called more than once, so we have to do unsubscribe and subscribe.
	Also, we do not unsubscribe as we want change in Build Settings to always update
	SceneIndex instances. Obviously only do subscription in Edit Mode */
	public SceneIndex(){
		EditorBuildSettings.sceneListChanged -= validateIndex;
		EditorBuildSettings.sceneListChanged += validateIndex;
	}
	#endif
}

#if UNITY_EDITOR
public partial class SceneIndex{

[CustomPropertyDrawer(typeof(SceneIndex))]
class SceneIndexDrawer : PropertyDrawer{
	static readonly GUIContent guiContentIndexTooltip =
		new GUIContent("","Index in Build Settings");
	static readonly GUIContent guiContentIconTooltip =
		new GUIContent("","This scene has not been included in Build Settings");
	public override void OnGUI(
		Rect position,SerializedProperty property,GUIContent label)
	{
		SceneIndex targetAs =
			fieldInfo.GetValue(property.serializedObject.targetObject) as SceneIndex;
		float xMax = position.xMax;
		position.width -= 40.0f;
		Object userValue = EditorGUI.ObjectField(
			position,
			label,
			targetAs.sceneAsset,
			typeof(SceneAsset),
			true
		);
		if(userValue != targetAs.sceneAsset){
			Undo.RecordObject(property.serializedObject.targetObject,"Assign SceneIndex");
			targetAs.sceneAsset = userValue as SceneAsset;
			targetAs.validateIndex();
		}
		if(targetAs.index == -1){
			position.x = xMax-20.0f;
			position.width = 20.0f;
			EditorGUI.LabelField(
				position,
				EditorGUIUtility.IconContent(UnityIconPath.ICONPATH_WARNING)
			);
			EditorGUI.LabelField(
				position,
				guiContentIconTooltip
			);
		}
		else{
			position.x = xMax-35.0f;
			position.width = 35.0f;
			bool bSavedEnable = GUI.enabled;
			GUI.enabled = false;
			EditorGUI.IntField(
				position,
				"",
				targetAs.index
			);
			EditorGUI.LabelField(
				position,
				guiContentIndexTooltip
			);
			GUI.enabled = bSavedEnable;
		}
	}
}

} //end partial class SceneIndex
#endif

} //end namespace Chameleon
