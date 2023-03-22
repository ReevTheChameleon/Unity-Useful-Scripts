/*************************************************************************
 * PRIMITIVEDOORWAY (v1.0.2)
 * by Reev the Chameleon
 * 26 Dec 2
**************************************************************************
Script designed to aid procedural primitive doorway structure generation

Update v1.0.1: Fix incorrect rotation when PrimitiveDoorway is created on rotated GameObject
Update v1.0.2: Remove unnecessary using
*/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Chameleon{

public class PrimitiveDoorway : MonoBehaviour{
	/* User should not edit this. Edittable via Debug Inspector */
	[SerializeField][HideInInspector] Transform tLeftWall;
	[SerializeField][HideInInspector] Transform tRightWall;
	[SerializeField][HideInInspector] Transform tTopWall;

	[SerializeField] protected Vector2 v2WallSize = new Vector2(2.0f,2.0f);
	[SerializeField] protected Vector2 v2DoorSize = new Vector2(1.0f,1.5f);
	[SerializeField] protected float doorPosition = 0.0f;
	[SerializeField] protected float wallThickness = 0.2f;

	/* Layout:
		*********************
		*********************
		++++            -----
		++++            -----
		++++            -----
	*/

	/* We can validate only assigned parameter rather than validate everything,
	but quite a lot of parameters depend on another (for example, when v2Size
	becomes less than v2HoleSize, the latter should adjust), it seems easier and cleaner
	to do it this way. Will reconsider if performance is found to be an issue. */
	public virtual float WallThickness{
		get{ return wallThickness; }
		set{
			wallThickness = value;
			validateParameter();
			realign();
		}
	}
	public virtual Vector2 WallSize{
		get{ return v2WallSize; }
		set{
			v2WallSize = value;
			validateParameter();
			realign();
		}
	}
	public virtual Vector2 DoorSize{
		get{ return v2DoorSize; }
		set{
			v2DoorSize = value;
			validateParameter();
			realign();
		}
	}
	public virtual float DoorPosition{
		get{ return doorPosition; }
		set{
			doorPosition = value;
			validateParameter();
			realign();
		}
	}
	public virtual bool generateDoorway(){
		bool bGenerated = false;
		if(!tTopWall){
			tTopWall = createWall("TopWall").transform;
			tTopWall.localRotation = Quaternion.identity;
			bGenerated = true;
		}
		if(!tLeftWall){
			tLeftWall = createWall("LeftWall").transform;
			tLeftWall.localRotation = Quaternion.identity;
			bGenerated = true;
		}
		if(!tRightWall){
			tRightWall = createWall("RightWall").transform;
			tRightWall.localRotation = Quaternion.identity;
			bGenerated = true;
		}
		if(bGenerated){
			validateParameter();
			realign();
		}
		return bGenerated;
	}
	private GameObject createWall(string wallName){
		GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
		#if UNITY_EDITOR
		Undo.RegisterCreatedObjectUndo(g,"Generate Doorway");
		//GameObjectUtility.SetParentAndAlign(g,gameObject);
		#endif
		g.name = wallName;
		g.transform.parent = transform;
		g.layer = gameObject.layer;
		return g;
	}
	protected virtual void validateParameter(){
		wallThickness = Mathf.Max(0,wallThickness);
		v2WallSize = Vector2.Max(Vector2.zero,v2WallSize);
		/* doorPosition and v2DoorSize depend on each other, but we want doorPosition
		to be restricted by v2DoorSize, so we clamp doorPosition first. */
		doorPosition = 	Mathf.Clamp(
			doorPosition,
			-v2WallSize.x/2+v2DoorSize.x/2,
			v2WallSize.x/2-v2DoorSize.x/2
		);
		v2DoorSize = new Vector2(
			Mathf.Clamp(
				v2DoorSize.x,
				0.0f,
				2*Mathf.Min(v2WallSize.x/2-doorPosition,doorPosition+v2WallSize.x/2)
			),
			Mathf.Clamp(v2DoorSize.y,0.0f,v2WallSize.y)
		);
	}
	protected virtual void realign(){
		if(!tLeftWall || !tRightWall || !tTopWall)
			return;

		tTopWall.localScale = new Vector3(
			v2WallSize.x,
			v2WallSize.y-v2DoorSize.y,
			wallThickness
		);
		tLeftWall.localScale = new Vector3(
			v2WallSize.x/2+doorPosition-v2DoorSize.x/2,
			v2DoorSize.y,
			wallThickness
		);
		tRightWall.localScale = new Vector3(
			v2WallSize.x/2-doorPosition-v2DoorSize.x/2,
			v2DoorSize.y,
			wallThickness
		);

		tTopWall.localPosition = new Vector3(
			0.0f,
			v2DoorSize.y+tTopWall.localScale.y/2,
			0.0f
		);
		tLeftWall.localPosition = new Vector3(
			-v2WallSize.x/2+tLeftWall.localScale.x/2,
			v2DoorSize.y/2,
			0.0f
		);
		tRightWall.localPosition = new Vector3(
			v2WallSize.x/2-tRightWall.localScale.x/2,
			v2DoorSize.y/2,
			0.0f
		);
	}
	
	#if UNITY_EDITOR
	protected virtual void OnValidate(){
		validateParameter();
		realign();
	}

	[CanEditMultipleObjects]
	[CustomEditor(typeof(PrimitiveDoorway))]
	public class PrimitiveDoorwayEditor : Editor{
		PrimitiveDoorway targetAs;
		protected virtual void OnEnable(){
			targetAs = (PrimitiveDoorway)target;
		}
		public override void OnInspectorGUI(){
			if(!targetAs.tTopWall || !targetAs.tLeftWall || !targetAs.tRightWall){
				if(GUILayout.Button("Generate Door"))
					targetAs.generateDoorway();
			}
			else
				DrawDefaultInspector();
		}
	}
	#endif
}

} //end namespace Chameleon
