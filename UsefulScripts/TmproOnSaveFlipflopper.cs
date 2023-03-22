/*************************************************************************
 * TMPROONSAVEFLIPFLOPPER (v1.0.2)
 * by Reev the Chameleon
 * 17 Feb 3
**************************************************************************
Usage:
Attach this script to GameObject with TMP_Text Component so that
the TMP_Text Component gets enabled/disabled when scene is saved.

Rationale:
While TMP_Text supports stencil buffer and masking, there seems to be
a bug that causes it to disappear from scene view when user saves the scene.
(It is later found that prefab update also cause this, so reflect to code.)
This bug seems to keep getting fixed and resurface for years
(Credit: Stephan_B, UF), and the best workaround is to disable and
re-enable the TMP_Text Component (Futureblur, UF).
This script is just devised to automate that process.
Stephan_B, UF points out that it has to do with internal texture management
and Canvas system, indicating that this script may find its use
to other Components in the future as well.

Update v1.0.1: Specify bRemoveInEditorBuild to also remove this class from Editor build
Update v1.0.2: Minor code change due to rivision in [RemoveOnBuild]
*/

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

namespace Chameleon{

[RemoveOnBuild(bApplyInEditorBuild=true)]
[RequireComponent(typeof(TMP_Text))]
public class TmproOnSaveFlipflopper : MonoBehaviour{
	private void flipflopTmpro(){
		/* This check is here just for safety because the problem
		of 2 null subscribers has been dealt with succuessfully below. */
		if(this!=null){ //Unity's null
			TMP_Text tmpText = GetComponent<TMP_Text>();
			if(tmpText){
				tmpText.enabled = !tmpText.enabled;
				tmpText.enabled = !tmpText.enabled;
				return;
			}
		}
		EditorSceneManager.sceneSaved -= flipflopTmproSceneSaved;
		PrefabUtility.prefabInstanceUpdated -= flipflopTmproPrefabUpdated;
	}
	private void flipflopTmproSceneSaved(Scene scene){
		flipflopTmpro();
	}
	private void flipflopTmproPrefabUpdated(GameObject g){
		flipflopTmpro();
	}
	void OnValidate(){
		/* If not doing this check, when exitting Play Mode, there will be 
		3 flipflopTmpro functions subscribed, of which 2 of them are from
		Unity's null TmproOnSaveFlipflopper, supposedly due to
		OnValidate() called in Play Mode. */
		if(!EditorApplication.isPlayingOrWillChangePlaymode){
			EditorSceneManager.sceneSaved -= flipflopTmproSceneSaved;
			EditorSceneManager.sceneSaved += flipflopTmproSceneSaved;
			PrefabUtility.prefabInstanceUpdated -= flipflopTmproPrefabUpdated;
			PrefabUtility.prefabInstanceUpdated += flipflopTmproPrefabUpdated;
		}
	}
}

} //end namespace Chameleon

#endif
