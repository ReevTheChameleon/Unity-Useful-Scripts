/************************************************************************
 * GRADIENTCREATOR (v1.1)
 * by Reev the Chameleon
 * 10 Aug 2
*************************************************************************
EditorWindow for creating simple horizontal or vertical gradient
as .png file asset (may add diagonal/2-directional later).
While one can easily create gradients via Photoshop, creating them
via in-editor window is more convenient, and the resulting file is
about 10 times smaller than that from Photoshop even at smallest
file size settings.

Update v1.1: Add feature for creating gradients in arbitrary angles
*/

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Chameleon{

public static class GradientCreator{
	public static Texture2D createGradientTexture(
		int width,int height,Gradient gradient,float gradientAngleDeg)
	{
		Texture2D tex2d = new Texture2D(width,height);
		if(gradientAngleDeg == 90.0f){
			Color[] aColor = new Color[height];
			for(int x=0; x<height; ++x)
				aColor[x] = gradient.Evaluate((float)x/height);
			for(int i=0; i<width; ++i)
				tex2d.SetPixels(i,0,1,height,aColor);
		}
		else if(gradientAngleDeg==0.0f || gradientAngleDeg==180.0f){
			Color[] aColor = new Color[width];
			for(int x=0; x<width; ++x)
				aColor[x] = gradient.Evaluate((float)x/width);
			for(int i=0; i<height; ++i)
				tex2d.SetPixels(0,i,width,1,aColor);
		}
		else{
			float gradientLength =
				Vector2.Dot(
					new Vector2( //diagonal
						width-1.0f,
						gradientAngleDeg>0.0f ? height : -height
					),
					Vector2Extension.fromPolar(1.0f,gradientAngleDeg)
				);
			for(int x=0; x<width; ++x){
				for(int y=0; y<height; ++y){
					Vector2 vRelative =
						gradientAngleDeg>0.0f ?
						new Vector2(x,y).newRotate(-gradientAngleDeg) :
						new Vector2(x,y-height).newRotate(-gradientAngleDeg)
					;
					tex2d.SetPixel(x,y,gradient.Evaluate(vRelative.x/gradientLength));
					/* (0,0) is at BOTTOM-left corner */
				}
			}
		}
		tex2d.Apply(); //Credit: CiberX15, UA
		return tex2d;
	}
	public static Texture2D createSolidTexture(int width,int height,Color color){
		Texture2D tex2d = new Texture2D(width,height);
		Color[] aColor = new Color[width*height];
		for(int i=0; i<aColor.Length; ++i)
			aColor[i] = color;
		tex2d.SetPixels(aColor);
		tex2d.Apply();
		return tex2d;
	}
}

class GradientCreatorWindow : EditorWindow{
	private enum eGradientDirection{HORZ,VERT}
	private Texture2D tex2d;
	private int width=64,height=64;
	private float gradientAngle = 0.0f; //degree
	private Gradient gradient = new Gradient();
	private static Material matPreview;
	private bool bPreviewed;
	private static Texture2D texDefaultCheckerGray;
	private static string lastPath;

	[MenuItem("Assets/Create/Chameleon/GradientCreator...",priority=-101)]
	static void showWindow(){
		GradientCreatorWindow window = GetWindow<GradientCreatorWindow>();
		window.position = new Rect(window.position.x,window.position.y,300.0f,300.0f); //Credit: useless.unity.user, UA
		/* Because GetWindowWithRect will create NON-RESIZABLE window */
	}
	void OnGUI(){
		if(!tex2d)
			tex2d = new Texture2D(64,64);
		EditorGUILayout.Space(5f);
		width = EditorGUILayout.IntField("Width",width);
		height = EditorGUILayout.IntField("Height",height);
		gradientAngle =
			EditorGUILayout.Slider("Gradient Direction",gradientAngle,-90.0f,90.0f);
		gradient = EditorGUILayout.GradientField("Gradient",gradient);
		if(GUI.changed)
			bPreviewed = false;
		if(GUILayout.Button("Preview")){
			tex2d = GradientCreator.createGradientTexture(width,height,gradient,gradientAngle);
			bPreviewed = true;
		}
		if(GUILayout.Button("Create")){
			if(!bPreviewed){
				tex2d = GradientCreator.createGradientTexture(width,height,gradient,gradientAngle);
				bPreviewed = true;
			}
			string path = EditorUtility.SaveFilePanel(
				"Save Gradient",
				Path.GetDirectoryName(lastPath) ?? Application.dataPath,
				Path.GetFileName(lastPath) ?? "Gradient.png",
				"png"
			);
			if(path.Length != 0){
				byte[] texPNG = tex2d.EncodeToPNG();
				if(texPNG != null){
					File.WriteAllBytes(path,texPNG);
					string relativePath = path.Substring(Application.dataPath.Length-"Assets".Length);
					AssetDatabase.ImportAsset(relativePath);
					/* Below will change import settings to sprite (Credit: FlyingOstriche, UA) */
					TextureImporter textureImporter = AssetImporter.GetAtPath(relativePath) as TextureImporter;
					textureImporter.textureType = TextureImporterType.Sprite;
					textureImporter.wrapMode = TextureWrapMode.Repeat;
					AssetDatabase.WriteImportSettingsIfDirty(relativePath);
				}
				lastPath = path;
			}
		}
		Rect rectPreview =
			new Rect(5.0f,7*EditorGUIUtility.singleLineHeight+5f,tex2d.width,tex2d.height);
		if(!texDefaultCheckerGray){
			texDefaultCheckerGray =
				AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Checker-Gray.png");
		}
		/* Credit: Tim Hunter, SO */
		//Note: Used to use EditorGUI.DrawPreviewTexture, but that produces wrong result
		GUI.DrawTexture(
			rectPreview,
			texDefaultCheckerGray
		);
		GUI.DrawTexture(
			rectPreview,
			tex2d
		);
	}
}

} //end namespace Chameleon

#endif
