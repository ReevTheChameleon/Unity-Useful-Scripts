/************************************************************************
 * IKTWOBONE (v1.1.1)
 * by Reev the Chameleon
 * 14 Feb 2
*************************************************************************
Class for solving and controlling two-bone inverse kinematic (IK).
This class contains a static method to solve two-bone IK, that
can be called from anywhere, and you can attach this script to
the GameObject to let it control two-bone IK on it directly,
albeit Unity also has AnimationRigging Component that would do that
more elegantly.

Update v1.0.1: Add #if UNITY_EDITOR around editor code to prevent build error
Update v1.1: Add weight support and allow previewing via inspector
Update v1.1.1: Specify execution order so it executes before other LateUpdate
*/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Chameleon{

[DefaultExecutionOrder(-1)] //So its LateUpdate executes before other's
public class IKTwoBone : MonoBehaviour{
	[Range(0,1)] public float weight;
	public Transform tStart;
	public Transform tMid;
	public Transform tEnd;
	[Space(5)]
	public Transform tTarget;
	public Transform tHint;

	public static void solveIKTwoBone(Transform tStart,Transform tMid,
		Transform tEnd,Transform tTarget,Transform tHint,float weight)
	{
		Vector3 vT = tTarget.position-tStart.position;
		Vector3 vB = tEnd.position-tMid.position;
		Vector3 vA = tMid.position-tStart.position;
		float a = vA.magnitude;
		float b = vB.magnitude;
		float l = vT.magnitude;

	/* ---------------- tStart rotation --------------------- */
		//This quaternion rotate vA to vT
		Quaternion qT = Quaternion.FromToRotation(vA,vT);
		bool bTriangle = false;
		if(l>a+b){ //Target too far
			qT = Quaternion.FromToRotation(vA,vT);}
		else if(l<a-b && l<b-a){ //Target too near
			qT = a>b ?
				Quaternion.FromToRotation(vA,vT) :
				Quaternion.FromToRotation(vA,-vT) //rotate 180 degree
			;
		}
		else{ //Will form a triangle
			qT = Quaternion.FromToRotation(vA,vT);
			bTriangle = true;
		}

		//assume applying qT to tStart; its children also rotates
		vA = qT * vA;
		vB = qT * vB;
		Vector3 vH = Vector3.zero;
		/* This quaternion rotates the bone plane so it contains hint point. */
		if(tHint){
			vH = tHint.position-tStart.position;
			qT = Quaternion.FromToRotation(
				Vector3.Cross(vB,vA),
				Vector3.Cross(vT,vH)
			) * qT;
		}

		if(bTriangle){
			float cosO = (l*l+a*a-b*b)/(2*a*l); //probably can optimize by using sqrMagnitude
			float cosHalfO = Mathf.Sqrt((1+cosO)/2);
			float sinHalfO = Mathf.Sqrt((1-cosO)/2);
			Vector3 vAxis =
				tHint ?
				Vector3.Cross(vT,vH).normalized :
				Vector3.Cross(vT,vA).normalized
			;
			/* Rotation by angle x around vAxis is represented by
			quaternion cos(x/2)+sin(x/2)v */
			qT = new Quaternion(
				sinHalfO*vAxis.x,
				sinHalfO*vAxis.y,
				sinHalfO*vAxis.z,
				cosHalfO
			) * qT;
		}

		tStart.rotation = Quaternion.Lerp(
			tStart.rotation,
			qT * tStart.rotation,
			weight
		);
	/* ---------------- tMid rotation --------------------- */
		tMid.rotation = Quaternion.Lerp(
			tMid.rotation,
			Quaternion.FromToRotation(
				tEnd.position-tMid.position,
				tTarget.position-tMid.position
			) * tMid.rotation,
			weight
		);
	/* ---------------- tEnd rotation --------------------- */
		tEnd.rotation = Quaternion.Lerp(tEnd.rotation,tTarget.rotation,weight);
	}
	public void solveIKTwoBone(){
		IKTwoBone.solveIKTwoBone(tStart,tMid,tEnd,tTarget,tHint,weight);
	}
	void LateUpdate(){
		solveIKTwoBone();
	}

	#if UNITY_EDITOR
	[System.NonSerialized] public bool bRealtimePreview;
	#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(IKTwoBone))]
class IKTwoBoneEditor : Editor{
	IKTwoBone targetAs;
	void OnEnable(){
		targetAs = (IKTwoBone)target;
	}
	public override void OnInspectorGUI(){
		DrawDefaultInspector();
		//if(GUILayout.Button("Preview IK")){
		//	Undo.RecordObject(targetAs.tStart,"Preview IK");
		//	targetAs.solveIKTwoBone();
		//}
		GUILayout.Space(5);
		if(GUILayout.Button("Preview IK")){
			IKTwoBonePreviewWindow.showWindow(targetAs);}
	}
	private class IKTwoBonePreviewWindow : EditorWindow{
		private IKTwoBone rigTarget;
		private TransformData tdStart = new TransformData();
		private TransformData tdMid = new TransformData();
		private TransformData tdEnd = new TransformData();
		private bool bPreview;
		public static void showWindow(IKTwoBone target){
			IKTwoBonePreviewWindow window = GetWindowWithRect<IKTwoBonePreviewWindow>(
				new Rect(0.0f,0.0f,200.0f,70.0f),
				true,
				"IKTwoBone Previewer",
				true
			);
			window.rigTarget = target;
			window.tare();
			window.bPreview = true;
		}
		void OnEnable(){
			SceneView.duringSceneGui += updateSceneView;
		}
		void OnDisable(){
			SceneView.duringSceneGui -= updateSceneView;
			resetPosition();
		}
		void OnGUI(){
			float savedLabelWidth = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 50.0f;
			rigTarget.weight = EditorGUILayout.Slider("weight",rigTarget.weight,0.0f,1.0f);
			GUILayout.Space(5);
			EditorGUI.BeginChangeCheck();
			bPreview = GUILayout.Toggle(bPreview,"Preview",new GUIStyle(GUI.skin.button)); //Credit: Hyago Oliveira
			if(EditorGUI.EndChangeCheck()){
				if(!bPreview){
					resetPosition();}
			}
			if(GUILayout.Button("Tare")){
				tare();}
			EditorGUIUtility.labelWidth = savedLabelWidth;
		}
		private void updateSceneView(SceneView sceneView){
			if(bPreview){
				resetPosition();
				rigTarget.solveIKTwoBone();
			}
		}
		private void tare(){
			bPreview = false;
			tdStart = rigTarget.tStart.save();
			tdMid = rigTarget.tMid.save();
			tdEnd = rigTarget.tEnd.save();
		}
		private void resetPosition(){
			rigTarget.tStart.load(tdStart);
			rigTarget.tMid.load(tdMid);
			rigTarget.tEnd.load(tdEnd);
		}
	}
}
#endif

} //end namespace Chameleon
