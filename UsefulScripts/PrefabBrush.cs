/************************************************************************
 * PREFABBRUSH (v1.0.1)
 * by Reev the Chameleon
 * 17 Jul 2
*************************************************************************
Brush used to paint GameObjects or Prefabs onto Grid or Tilemap
for convenient 2D alignment
NOTE: Currently not yet support picking function; will add feature later
Update v1.0.1: Change menu name
*/

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using UnityEditor;

namespace Chameleon{

[CreateAssetMenu(menuName="Chameleon/PrefabBrush")]
[CustomGridBrush(false,false,false,"chm_PrefabBrush")] //Not hiding default brush
public class PrefabBrush : GridBrushBase{
	public GameObject gToPaint;
	public bool bOverwrite;
	public override void Paint(GridLayout gridLayout,GameObject brushTarget,Vector3Int position){
		/* The reason we need to call this on every paint and not remembering it is because
		we cannot be sure whether user has moved the tiles between each paint or not.
		It is decided that we will only delete GameObjects whose lie exactly at the specified
		position. */
		if(!gToPaint){
			/* When GameObject is not selected, paint empty (=erase). This is useful
			for box erasing because Unity doesn't provide functionality intuitively
			(you have to make do with tile palette, which is tedious) (Credit: ChuanXin, UF) */
			Erase(gridLayout,brushTarget,position);
			return;
		}
		List<GameObject> lGameObjectAtCell = getGameObjectAtCell(gridLayout,brushTarget,position);
		if(lGameObjectAtCell.Count > 0 && !bOverwrite)
			return;
		foreach(GameObject gExisting in getGameObjectAtCell(gridLayout,brushTarget,position))
			Undo.DestroyObjectImmediate(gExisting);
		Tilemap tilemap = brushTarget.GetComponent<Tilemap>();
		GameObject g;
		if(PrefabUtility.GetPrefabAssetType(gToPaint) == PrefabAssetType.NotAPrefab)
			g = Instantiate(gToPaint);
		else
			g = PrefabUtility.InstantiatePrefab(gToPaint) as GameObject;
		Undo.RegisterCreatedObjectUndo(g,"Paint "+gToPaint.name);
		g.transform.position =
			tilemap ? 
			tilemap.pivottedLocalPos(position) :
			gridLayout.CellToLocal(position)
		;
		g.transform.SetParent(brushTarget.transform,false);
	}
	public override void Erase(GridLayout gridLayout,GameObject brushTarget,Vector3Int position){
		foreach(GameObject gExisting in getGameObjectAtCell(gridLayout,brushTarget,position))
			Undo.DestroyObjectImmediate(gExisting);
	}

	private List<GameObject> getGameObjectAtCell(
		GridLayout gridLayout,GameObject brushTarget,Vector3Int cell)
	{
		Tilemap tilemap = brushTarget.GetComponent<Tilemap>();
		Vector3 vLocalPos = 
			tilemap ?
			tilemap.pivottedLocalPos(cell) :
			gridLayout.CellToLocal(cell)
		;
		List<GameObject> lGameObject = new List<GameObject>();
		foreach(Transform tChild in brushTarget.transform){
			if(tChild.localPosition == vLocalPos)
				lGameObject.Add(tChild.gameObject);
		}
		return lGameObject;
	}
}

} //end namespace Chameleon

#endif
