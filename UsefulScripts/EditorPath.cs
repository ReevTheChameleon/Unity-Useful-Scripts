/************************************************************************
 * EDITORPATH (v1.1.1)
 * by Reev the Chameleon
 * 10 Mar 3
*************************************************************************
Collections of functions related to assets path that makes life easier.
Update v1.0.1: Revise code for getting current folder
Update v1.0.2: Fix code for getting current folder when folder is not in Assets
Update v1.0.3: Make getSelectionFolder returns null if nothing is selected
Update v1.1: Add findAssetsOfType function
Update v1.1.1: Add separator argument to nextUniqueFilename, and Add nextUniquePath
*/

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

using Object = UnityEngine.Object;

namespace Chameleon{

public static class EditorPath{ //Can only be called in Editor
	public static string getSelectionFolder(){
		/* If selection is folder return it, otherwise return parent folder */
		if(Selection.activeObject == null)
			return null;
		string currentPath = AssetDatabase.GetAssetPath(Selection.activeObject);
		return //Credit: OperationDogBird, UA
			Directory.Exists(currentPath) ?
			currentPath : //selection is a folder
			Path.GetDirectoryName(currentPath)
		;	
	}
	public static string nextUniqueFilename(string folder,string filename,char separator=' '){
		if(folder==null || filename==null)
			return null;
		if(!File.Exists(folder+"/"+filename))
			return filename; //So we don't need to bother processing if filename doesn't exist
		
		string filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
		string extension = Path.GetExtension(filename); //"." INCLUDED!
		int i = 1;
		string modifiedFilename = filename;
		do{
			modifiedFilename = filenameWithoutExtension +separator+ i++ + extension;
			/* extension is "" if no extension */
		} while(File.Exists(folder+"/"+modifiedFilename));
		
		return modifiedFilename;
	}
	public static string nextUniquePath(string path,char separator=' '){
		string folder = Path.GetDirectoryName(path);
		string filename = nextUniqueFilename(folder,Path.GetFileName(path),separator);
		return folder+"/"+filename;
	}
	public static string getCurrentFolder([CallerFilePath] string path = ""){
		/* Since functions in AssetDatabase mostly requires RELATIVE path while C#
		[CallerFilePath] gives absolute, there is the need to convert. This conversion
		code below is brillant! (Credit: frogsbo, UA) */
		if(path.StartsWith(Application.dataPath)){
			return Path.GetDirectoryName(
				"Assets" + path.Substring(Application.dataPath.Length)
			);
		}
		return Path.GetDirectoryName(path); //If not in Assets, just return absolute path
	}
	/* Credit: glitchers, UA */
	public static List<T> findAssetsOfType<T>() where T:Object{
		List<T> lResult = new List<T>();
		string[] aSGuid = AssetDatabase.FindAssets(
			"t:"+typeof(T).Name);
		for(int i=0; i<aSGuid.Length; ++i){
			T asset = AssetDatabase.LoadAssetAtPath<T>(
				AssetDatabase.GUIDToAssetPath(aSGuid[i])
			);
			if(asset)
				lResult.Add(asset);
		}
		return lResult;
	}
	public static string convertToProjectRelativePath(string path){
		return path.Substring(Application.dataPath.Length-"Assets".Length);
	}
}

} //end namespace Chameleon

#endif
