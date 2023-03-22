/************************************************************************
 * SCRIPTDEFINESYMBOL (v1.0)
 * by Reev the Chameleon
 * 11 Jan 2
*************************************************************************
Static class for manipulating project define symbols used for
conditional compilation.
*/

#if UNITY_EDITOR
using UnityEditor;

namespace Chameleon{

public static class ScriptDefineSymbol{
	//Most Credits go to Ondrej Paska, fishtrone.wordpress.com
	public static bool add(string symbol){
		BuildTargetGroup buildTargetGroup =
			EditorUserBuildSettings.selectedBuildTargetGroup;
		string sDefinedSymbol = PlayerSettings.GetScriptingDefineSymbolsForGroup(
			buildTargetGroup
		);
		/* string get from this function is ALWAYS symbols separated by ; WITHOUT
		any whitespaces and WITHOUT ending ; */
		string[] aDefinedSymbol = sDefinedSymbol.Split(';');
		foreach(string s in aDefinedSymbol){
			if(s == symbol)
				return false; //Already exist, so do not add
		}
		sDefinedSymbol += ";"+symbol;
		PlayerSettings.SetScriptingDefineSymbolsForGroup(
			buildTargetGroup,
			sDefinedSymbol
		);
		return true;
	}
	public static bool remove(string Symbol){
		bool bDefined = false;
		BuildTargetGroup buildTargetGroup =
			EditorUserBuildSettings.selectedBuildTargetGroup;
		string sDefinedSymbol = PlayerSettings.GetScriptingDefineSymbolsForGroup(
			buildTargetGroup
		);
		string[] aDefinedSymbol = sDefinedSymbol.Split(';');
		for(int i=0; i<aDefinedSymbol.Length; ++i){
			if(aDefinedSymbol[i] == Symbol){
				aDefinedSymbol[i] = "";
				bDefined = true;
				break; //remove only one symbol (by design)
			}
		}
		if(bDefined){
			PlayerSettings.SetScriptingDefineSymbolsForGroup(
				buildTargetGroup,
				string.Join(";",aDefinedSymbol)
			);
			return true;
		}
		return false;
	}
	public static bool isDefined(string symbol){
		string[] aDefinedSymbol = PlayerSettings.GetScriptingDefineSymbolsForGroup(
			EditorUserBuildSettings.selectedBuildTargetGroup)
			.Split(';')
		;
		foreach(string s in aDefinedSymbol){
			if(s == symbol)
				return true;
		}
		return false;
	}
}

} //end namespace Chameleon

#endif
