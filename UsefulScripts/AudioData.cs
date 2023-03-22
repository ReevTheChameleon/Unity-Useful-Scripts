/************************************************************************
 * AUDIODATA (v1.1.1)
 * by Reev the Chameleon
 * 8 Sep 2
*************************************************************************
Allows you to use variable of type AudioData which has both
AudioClip and volume information that can be set in inspector, 
and allows you to use audioSource.playOneShot() and
audioSource.setClip() with it.
Also, AudioData with [Preview] attribute will show preview button
that can be clicked to listen to the sound (this feature plays
preview sound as 2D).
While volume is shown as slider for convenience, user can use
[NoRange] attribute to show it as simple float field and assign
volume outside of range 0-1.

Update v1.1: Fix bug preview player not destroyed and add [NoRange] atribute
Update v1.1.1: Change code to avoid compilation error in Unity version before 2020.3
*/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Reflection;

using Object = UnityEngine.Object;

namespace Chameleon{

[Serializable]
public class AudioData{
	public AudioClip audioClip;
	public float volume = 1.0f;
}

[AttributeUsage(AttributeTargets.Field)]
public class PreviewAttribute : Attribute{}

[AttributeUsage(AttributeTargets.Field)]
public class NoRangeAttribute : Attribute{}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(AudioData))]
class OneShotAudioClipDrawer : PropertyDrawer{
	const float PLAYBUTTON_WIDTH = 58.0f;
	private bool bPlaying = false;
	private AudioSource audioSource;
	private double previewStartTime;
	private float previewRemainTime = 0.0f;

	public override float GetPropertyHeight(SerializedProperty property,GUIContent label){
		return 2.0f*EditorGUIUtility.singleLineHeight;
	}
	public override void OnGUI(
		Rect position,SerializedProperty property,GUIContent label)
	{
		position.height = EditorGUIUtility.singleLineHeight;
		EditorGUI.PropertyField(position,property.FindPropertyRelative("audioClip"),label);
		
		++EditorGUI.indentLevel;
		position.y += EditorGUIUtility.singleLineHeight;

		if(fieldInfo.IsDefined(typeof(PreviewAttribute))){
			float savedLabelWidth = EditorGUIUtility.labelWidth;
			float fullWidth = position.width;
			EditorGUIUtility.labelWidth -= PLAYBUTTON_WIDTH;
			position.width = PLAYBUTTON_WIDTH;
			AudioClip audioClip =
				property.FindPropertyRelative("audioClip")?.objectReferenceValue as AudioClip;
			if(bPlaying){
				if(GUI.Button(position,"Stop") && audioSource){
					Object.DestroyImmediate(audioSource.gameObject);
					bPlaying = false;
				}
			}
			else if(GUI.Button(position,"Preview")){
				/* static function AudioSource.PlayClipAtPoint(clip,vPos) will create temp
				AudioSource and dispose of it once the clip ends (Credit: Bunny83, UA)
				but it doesn't work in Editor Mode because it relies on Destroy() function  */
				SerializedProperty spVolume = property.FindPropertyRelative("volume");
				if(audioClip){
					if(!audioSource){
						GameObject g = new GameObject("[OneShotAudio Previewer]");
						g.hideFlags = HideFlags.DontSave; //So it disappears when change mode
						/* You can't create lone Component, as Unity will treat it as null.
						You have to attach it to GameObject. */
						audioSource = g.AddComponent<AudioSource>();
						audioSource.spatialBlend = 0.0f;
					}
					audioSource.volume = spVolume.floatValue;
					audioSource.PlayOneShot(audioClip);
					previewRemainTime = Mathf.Max(
						audioClip.length,
						(float)(previewRemainTime-Time.realtimeSinceStartup+previewStartTime)
					);
					previewStartTime = TimeExtension.RealtimeSinceStartup;
					bPlaying = true;
					/* I wanted to use async/await, but you can't call Unity functions in other
					threads, so abandoned */
				}
			}
			if(TimeExtension.RealtimeSinceStartup-previewStartTime > previewRemainTime){
				cleanUpPreviewAudio();
				bPlaying = false;
			}
			position.x += position.width;
			position.width = fullWidth - position.width;
			drawVolume(position,property.FindPropertyRelative("volume"));
			EditorGUIUtility.labelWidth = savedLabelWidth;
			Selection.selectionChanged -= cleanUpPreviewAudio;
			Selection.selectionChanged += cleanUpPreviewAudio; //so it stops if user changes selection
		}
		else
			drawVolume(position,property.FindPropertyRelative("volume"));
		--EditorGUI.indentLevel;
	}
	private void drawVolume(Rect pos,SerializedProperty spVolume){
		if(!fieldInfo.IsDefined(typeof(NoRangeAttribute))){
			/* Wrap property change between EditorGUI.BeginProperty() and EditorGUI.EndProperty()
			so that any changes will be reflected as prefab change (Credit: JeffBert, UA) */
			EditorGUI.BeginProperty(pos,GUIContent.none,spVolume);
			EditorGUI.BeginChangeCheck();
			float userVolume = EditorGUI.Slider(pos,"Volume",spVolume.floatValue,0.0f,1.0f);
			if(EditorGUI.EndChangeCheck()){
				spVolume.floatValue = userVolume;
				if(audioSource)
					audioSource.volume = userVolume;
			}
			EditorGUI.EndProperty();
		}
		else
			EditorGUI.PropertyField(pos,spVolume);
	}
	private void cleanUpPreviewAudio(){
		if(audioSource)
			Object.DestroyImmediate(audioSource.gameObject);
		Selection.selectionChanged -= cleanUpPreviewAudio;
	}
}
#endif

public static class AudioSorceExtension{
	public static void playOneShot(
		this AudioSource audioSource,AudioData audioClipData)
	{
		audioSource.PlayOneShot(
			audioClipData.audioClip,
			audioClipData.volume
		);
	}
	public static void setClip(this AudioSource audioSource,AudioData audioClipData){
		audioSource.clip = audioClipData.audioClip;
		audioSource.volume = audioClipData.volume;
	}
}

} //end namespace Chameleon
