/************************************************************************
 * EDITORWITHSCENE (v1.4)
 * by Reev the Chameleon
 * 10 Nov 2
*************************************************************************
Inherit CustomEditor from this to make Vector3 marked with [ShowPosition],
float marked with [ShowAxis], and Vector3[2] marked with [ShowWireCube] display
gizmo in the scene.
Update v1.01: Write description header.
Update v1.02: Add eAxis.any
Update v1.1: Add code to display wire cube for [ShowWireCube]
Update v1.2: Make this class undo works with BakableMonoBehaviourEditorWithScene
Note: There is coupling with [Bakable] attribute. Will consider how to fix this later.
Update v1.3: EditorWithScene: Change [ShowWireCube] attribute to use Bounds
Update v1.3.1: Minor code rearrangement
Update v1.4: Make [ShowPosition] support local position

Note: Attributes in this scripts are accessed via reflection, and because
reflection cannot find private fields from base class, if you decide to use them
in derived class, you need to make the field at least as protected so it can be found.
*/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Reflection;
using System.Collections.Generic;

namespace Chameleon{

//===================================================================================
#region ATTRIBUTES
//---------------------------------------------------------------------------------
#region SHOWPOSITIONATTRIBUTE
public class ShowPositionAttribute : PropertyAttribute{
	public bool bLocal;
	public string label;
	public bool bBlackLabel;
	public ShowPositionAttribute(bool bLocal=false,string label="",bool bBlackLabel=false){
		/* Because types of parameters allowed in attribute constructor is very limited
		(Credit: Ohad Schneider & Kobi, SO), we cannot pass entire GUIStyle here.
		(considering alternative). */
		this.bLocal = bLocal;
		this.label = label;
		this.bBlackLabel = bBlackLabel;
	}
}
#endregion
//---------------------------------------------------------------------------------
#region SHOWAXISATTRIBUTE
public enum eAxis{any,x,y,z} //ignored for Vector3

public class ShowAxisAttribute : PropertyAttribute{
	public eAxis axis;
	public string label;
	public bool bBlackLabel;
	public ShowAxisAttribute(
		eAxis axis=eAxis.x, //default to x
		string label="",
		bool bBlackLabel=false
	){
		this.axis = axis;
		this.label = label;
		this.bBlackLabel = bBlackLabel;
	}
}
#endregion
//---------------------------------------------------------------------------------
#region SHOWWIRECUBE
public class ShowWireCubeAttribute : PropertyAttribute{
	public string label;
	public bool bBlackLabel;
	public ShowWireCubeAttribute(string label="",bool bBlackLabel=false){
		/* Because types of parameters allowed in attribute constructor is very limited
		(Credit: Ohad Schneider & Kobi, SO), we cannot pass entire GUIStyle here.
		(considering alternative). */
		this.label = label;
		this.bBlackLabel = bBlackLabel;
	}
}
#endregion
//---------------------------------------------------------------------------------
#endregion
//===================================================================================

//===================================================================================
#region EDITORWITHSCENE
#if UNITY_EDITOR
public abstract class EditorWithScene : Editor{
	protected List<FieldInfo> lFieldInfoShowPosition = new List<FieldInfo>();
	protected List<FieldInfo> lFieldInfoShowAxis = new List<FieldInfo>();
	protected List<FieldInfo> lFieldInfoShowWireCube = new List<FieldInfo>();
	protected bool bMonoBehaviour;
	protected GUIStyle labelStyle;

	protected virtual void OnEnable(){
		if(!(bMonoBehaviour=typeof(MonoBehaviour).IsAssignableFrom(target.GetType())))
			return; //Only allow MonoBehaviour for now
		labelStyle = new GUIStyle();
		foreach(FieldInfo fieldInfo in target.GetType().GetFields(
			BindingFlags.Public | BindingFlags.NonPublic |
			BindingFlags.Instance | BindingFlags.Static |
			BindingFlags.FlattenHierarchy
		)){
			if(fieldInfo.IsDefined(typeof(ShowPositionAttribute)) &&
				fieldInfo.FieldType==typeof(Vector3))
				lFieldInfoShowPosition.Add(fieldInfo);
				
			if(fieldInfo.IsDefined(typeof(ShowAxisAttribute)) &&
				fieldInfo.FieldType==typeof(float))
				lFieldInfoShowAxis.Add(fieldInfo);

			if(fieldInfo.IsDefined(typeof(ShowWireCubeAttribute)) &&
				fieldInfo.FieldType==typeof(Bounds))
				lFieldInfoShowWireCube.Add(fieldInfo);
		}
	}
	protected virtual void OnSceneGUI(){
		if(!bMonoBehaviour)
			return;	
	//---------------------------------------------------------------------------------
		#region SHOWPOSITIONATTRIBUTE
		MonoBehaviour mTarget = (MonoBehaviour)target;
		foreach(FieldInfo fieldInfo in lFieldInfoShowPosition){
			ShowPositionAttribute attribute =
				fieldInfo.GetCustomAttribute<ShowPositionAttribute>();
			object oPosition = fieldInfo.GetValue(target);
			if(oPosition == null)
				return;
			Vector3 v3Position = oPosition as Vector3? ?? new Vector3();
			if(attribute.bLocal)
				v3Position = mTarget.transform.TransformPoint(v3Position); 
			if(attribute.bBlackLabel)
				labelStyle.normal.textColor = Color.black;
			Handles.Label(v3Position,attribute.label,labelStyle);
			EditorGUI.BeginChangeCheck();
			v3Position = Handles.PositionHandle(v3Position,Quaternion.identity);
			if(EditorGUI.EndChangeCheck()){
				/* This uglily couples this class to BakableAttribute, but in order to get it
				to work with MonoBehaviourBakerWithScene, it is the simplest way I can think of.
				Will review how to refactor this code later */
				if(!fieldInfo.IsDefined(typeof(BakableAttribute))){
					Undo.RecordObject(target,fieldInfo.Name+" Change");
					fieldInfo.SetValue(
						target,
						attribute.bLocal ?
							mTarget.transform.InverseTransformPoint(v3Position) :
							v3Position
					);
				}
				else{
					FieldUndo.recordSetFieldInfo(
						target,fieldInfo,v3Position,fieldInfo.Name+" Change");
					Repaint();
				}
			}
		}
		#endregion
	//---------------------------------------------------------------------------------
		#region SHOWAXISATTRIBUTE
		foreach(FieldInfo fieldInfo in lFieldInfoShowAxis){
			ShowAxisAttribute attribute =
				fieldInfo.GetCustomAttribute<ShowAxisAttribute>();
			object oValue = fieldInfo.GetValue(target);
			if(oValue == null)
				return;
			float value = oValue as float? ?? 0.0f;
			Vector3 v3Position;
			if(attribute.bBlackLabel)
				labelStyle.normal.textColor = Color.black;
			switch(attribute.axis){
				case eAxis.x: v3Position = new Vector3(value,0.0f,0.0f); break;
				case eAxis.y: v3Position = new Vector3(0.0f,value,0.0f); break;
				case eAxis.z: v3Position = new Vector3(0.0f,0.0f,value); break;
				default: v3Position = new Vector3(); break;
			}
			Handles.Label(v3Position,attribute.label,labelStyle);
			EditorGUI.BeginChangeCheck();
			v3Position = Handles.PositionHandle(v3Position,Quaternion.identity);
			if(EditorGUI.EndChangeCheck()){
				if(!fieldInfo.IsDefined(typeof(BakableAttribute))){
					Undo.RecordObject(target,fieldInfo.Name+" Change");
					switch(attribute.axis){
						case eAxis.x: fieldInfo.SetValue(target,v3Position.x); break;
						case eAxis.y: fieldInfo.SetValue(target,v3Position.y); break;
						case eAxis.z: fieldInfo.SetValue(target,v3Position.z); break;
					}
				}
				else{
					switch(attribute.axis){
						case eAxis.x:
							FieldUndo.recordSetFieldInfo(
								target,fieldInfo,v3Position.x,fieldInfo.Name+" Change");
							break;
						case eAxis.y:
							FieldUndo.recordSetFieldInfo(
								target,fieldInfo,v3Position.y,fieldInfo.Name+" Change");
							break;
						case eAxis.z:
							FieldUndo.recordSetFieldInfo(
								target,fieldInfo,v3Position.z,fieldInfo.Name+" Change");
							break;				
					}
					Repaint();
				}
			}
		}
		#endregion
	//---------------------------------------------------------------------------------
		#region SHOWWIRECUBEATTRIBUTE
		foreach(FieldInfo fieldInfo in lFieldInfoShowWireCube){
			Bounds bound = fieldInfo.GetValue(target) as Bounds? ?? new Bounds();
			ShowWireCubeAttribute attribute =
				fieldInfo.GetCustomAttribute<ShowWireCubeAttribute>();
			if(attribute.bBlackLabel)
				labelStyle.normal.textColor = Color.black;
			Handles.Label(bound.center,attribute.label,labelStyle);
			
			Handles.DrawWireCube(bound.center,bound.extents);
			EditorGUI.BeginChangeCheck();
			Vector3 vPos = Handles.PositionHandle(bound.center,Quaternion.identity);
			if(EditorGUI.EndChangeCheck()){
				bound.center = vPos;
				if(!fieldInfo.IsDefined(typeof(BakableAttribute))){
					Undo.RecordObject(target,fieldInfo.Name+" Change");
					fieldInfo.SetValue(target,bound);
				}
				else{ //[Bakable]
					FieldUndo.recordSetFieldInfo(target,fieldInfo,bound,fieldInfo.Name+" Change");
					Repaint();
				}
			}
			EditorGUI.BeginChangeCheck();
			Vector3 vScale = Handles.ScaleHandle(
				bound.extents,
				bound.center,
				Quaternion.identity,
				HandleUtility.GetHandleSize(bound.center) * 1.2f
			);
			if(EditorGUI.EndChangeCheck()){
				bound.extents = vScale;
				if(!fieldInfo.IsDefined(typeof(BakableAttribute))){
					Undo.RecordObject(target,fieldInfo.Name+" Change");
					fieldInfo.SetValue(target,bound);
				}
				else{ //[Bakable]
					FieldUndo.recordSetFieldInfo(target,fieldInfo,bound,fieldInfo.Name+" Change");
					Repaint();
				}
			}
		}
		#endregion
	//---------------------------------------------------------------------------------
	}
}
#endif
#endregion
//===================================================================================

} //end namespace Chameleon
