/************************************************************************
 * COMMENT (v1.0)
 * by Reev the Chameleon
 * 12 Feb 3
*************************************************************************
Attach this script to a GameObject to write a comment about it.
The comment box is disabled by default, and can be unlocked for edit
via Component context menu "Unlock".
*/

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

namespace Chameleon{

[RemoveOnBuild]
public class Comment : MonoBehaviour{
	[SerializeField] string sComment;

	[MenuItem("CONTEXT/Comment/Unlock")]
	static void commentUnlock(){
		CommentEditor.bLock = false;
	}

	[CustomEditor(typeof(Comment))]	
	private class CommentEditor : Editor{
		public static bool bLock;
		void OnEnable(){
			bLock = true;
		}
		public override void OnInspectorGUI(){
			//Credit: Bunny83, UA
			GUIStyle styleWordWrap = new GUIStyle(EditorStyles.textArea);
			styleWordWrap.wordWrap = true;
			serializedObject.Update();
			SerializedProperty spComment = serializedObject.FindProperty(nameof(Comment.sComment));
			GUI.enabled = !bLock;
			spComment.stringValue = EditorGUILayout.TextArea(spComment.stringValue,styleWordWrap);
			if(GUI.changed){
				serializedObject.ApplyModifiedProperties();}
		}
	}
}

} //end namespace Chameleon

#endif
