/*************************************************************************
 * SHADEREDITOR (v1.2)
 * by Reev the Chameleon
 * 5 Jun 2
**************************************************************************
Usage:
Write [ClassName] in front of any property in the property block of Unity shader
(in attribute-like fashion) to instruct the inspector how to display it
in material editor.
! NOTE: Name of these classes are in GLOBAL namespace, because otherwise
Unity will not find it.
Update v1.1: Fix wideMode bug and add drawers for [MatInt],[MatIntRange],[MatVector2Int]
Update v1.2: Add drawer for [Gray]
*/

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class MatRange : MaterialPropertyDrawer{
	const float MATLABEL_WIDEMODE_HALFWIDTH = 120.0f;
	public override void OnGUI(
		Rect position,MaterialProperty prop,string label,MaterialEditor editor)
	{
		if(prop.type == MaterialProperty.PropType.Range){ //Credit: Ahnaf, SO
			/* The last MATLABEL_WIDEMODE_HALFWIDTH-1.0f prevents slider from disappearing
			when inspector is at its narrowest. This seems to be because in MaterialEditor,
			the FloatField size is larger than normal FloatField, and eats into the width
			of the slider. */
			EditorGUIUtility.labelWidth =
				EditorGUIUtility.wideMode ?
				0.0f :
				MATLABEL_WIDEMODE_HALFWIDTH-1.0f
			;
			prop.floatValue = EditorGUI.Slider(
				position,
				label,
				prop.floatValue,
				prop.rangeLimits.x,
				prop.rangeLimits.y
			);
		}
		else
			editor.DefaultShaderProperty(position,prop,label);
	}
}

public class MatVector2 : MaterialPropertyDrawer{
	/* Because there is no Vector2 type for shader, this will show Vector4 as Vector2
	so it can be adjusted accordingly in the inspector (Credit: tsvedas & Ahnaf, SO) */
	public override void OnGUI(
		Rect position,MaterialProperty prop,string label,MaterialEditor editor)
	{
		if(prop.type == MaterialProperty.PropType.Vector){
			bool bSavedWideMode = EditorGUIUtility.wideMode;
			if(!bSavedWideMode){
				EditorGUIUtility.wideMode = true; //otherwise Vector2Field will take 2 lines
				EditorGUIUtility.labelWidth = 119.0f;
			}
			else
				EditorGUIUtility.labelWidth = 0.0f;
			float labelWidth = EditorGUIUtility.labelWidth;
			/* Because shader draws Vector2Field slot like Vector3Field but with space where
			z should be, we expand the width so that x,y cover up that space */
			position.width = labelWidth + (position.width-labelWidth)*1.5f;
			Vector2 v2 = EditorGUI.Vector2Field(position,label,prop.vectorValue); //implicit conver
			if(GUI.changed)
				prop.vectorValue = v2;
			EditorGUIUtility.wideMode = bSavedWideMode;
		}
		else
			editor.DefaultShaderProperty(position,prop,label);
	}
}

public class MatInt : MaterialPropertyDrawer{
	/* You can use int like matInt("My Int",int) = 0, but it will show up as float
	because Unity uses underlying float. This will help display it as IntField */
	public override void OnGUI(
		Rect position,MaterialProperty prop,string label,MaterialEditor editor)
	{
		if(prop.type == MaterialProperty.PropType.Float){
			int userInt = EditorGUI.IntField(position,label,(int)prop.floatValue);
			if(GUI.changed)
				prop.floatValue = userInt;
		}
		else
			editor.DefaultShaderProperty(position,prop,label);
	}
}

public class MatIntRange : MaterialPropertyDrawer{
	const float MATLABEL_WIDEMODE_HALFWIDTH = 120.0f;
	public override void OnGUI(
		Rect position,MaterialProperty prop,string label,MaterialEditor editor)
	{
		if(prop.type == MaterialProperty.PropType.Range){
			EditorGUIUtility.labelWidth =
				EditorGUIUtility.wideMode ?
				0.0f :
				MATLABEL_WIDEMODE_HALFWIDTH-1.0f
			;
			int userInt = EditorGUI.IntSlider(
				position,
				label,
				(int)prop.floatValue,
				(int)prop.rangeLimits.x,
				(int)prop.rangeLimits.y
			);
			if(GUI.changed)
				prop.floatValue = userInt;
		}
		else
			editor.DefaultShaderProperty(position,prop,label);
	}
}

public class MatVector2Int : MaterialPropertyDrawer{
	public override void OnGUI(
		Rect position,MaterialProperty prop,string label,MaterialEditor editor)
	{
		if(prop.type == MaterialProperty.PropType.Vector){
			bool bSavedWideMode = EditorGUIUtility.wideMode;
			if(!bSavedWideMode){
				EditorGUIUtility.wideMode = true; //otherwise Vector2Field will take 2 lines
				EditorGUIUtility.labelWidth = 119.0f;
			}
			else
				EditorGUIUtility.labelWidth = 0.0f;
			float labelWidth = EditorGUIUtility.labelWidth;
			/* Because shader draws Vector2Field slot like Vector3Field but with space where
			z should be, we expand the width so that x,y cover up that space */
			position.width = labelWidth + (position.width-labelWidth)*1.5f;
			Vector2 v2 = EditorGUI.Vector2IntField(
				position,
				label,
				new Vector2Int((int)prop.vectorValue.x,(int)prop.vectorValue.y)
			);
			if(GUI.changed)
				prop.vectorValue = v2;
			EditorGUIUtility.wideMode = bSavedWideMode;
		}
		else
			editor.DefaultShaderProperty(position,prop,label);
	}
}

public class Gray : MaterialPropertyDrawer{
	public override void OnGUI(Rect position,MaterialProperty prop,string label,MaterialEditor editor) {
		bool bSaveEnabled = GUI.enabled;
		GUI.enabled = false;
		editor.DefaultShaderProperty(position,prop,label);
		GUI.enabled = bSaveEnabled;
	}
	public override float GetPropertyHeight(MaterialProperty prop,string label,MaterialEditor editor) {
		return MaterialEditor.GetDefaultPropertyHeight(prop);
	}
}

#endif
