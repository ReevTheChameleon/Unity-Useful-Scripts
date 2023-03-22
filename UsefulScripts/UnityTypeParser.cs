/************************************************************************
 * UNITYTYPEPARSER (v1.1)
 * by Reev the Chameleon
 * 27 Jun 2
*************************************************************************
Functions for parsing string to types

Update v1.1: Add support for Vector2, Vector3Int, Vector2Int, and Color types
*/

using UnityEngine;
using System;

public static class UnityTypeParser{
	public static bool tryParse(string s,out Vector3 v){
		if(s.Length>0 && s[0]=='(' && s[s.Length-1]==')'){
			s = s.Substring(1,s.Length-2);
			string[] aComponent = s.Split(',');
			if(aComponent.Length == 3){
				if(float.TryParse(aComponent[0],out v.x) &&
					float.TryParse(aComponent[1],out v.y) &&
					float.TryParse(aComponent[2],out v.z)
				)
					return true;
			}
		}
		v = new Vector3(0.0f,0.0f,0.0f);
		return false;
	}
	public static bool tryParse(string s,out Vector2 v){
		if(s.Length>0 && s[0]=='(' && s[s.Length-1]==')'){
			s = s.Substring(1,s.Length-2);
			string[] aComponent = s.Split(',');
			if(aComponent.Length == 2){
				if(float.TryParse(aComponent[0],out v.x) &&
					float.TryParse(aComponent[1],out v.y)
				)
					return true;
			}
		}
		v = new Vector2(0.0f,0.0f);
		return false;
	}
	public static bool tryParse(string s,out Vector3Int v){
		int x,y,z;
		if(s.Length>0 && s[0]=='(' && s[s.Length-1]==')'){
			s = s.Substring(1,s.Length-2);
			string[] aComponent = s.Split(',');
			if(aComponent.Length == 3){
				if(int.TryParse(aComponent[0],out x) &&
					int.TryParse(aComponent[1],out y) &&
					int.TryParse(aComponent[2],out z)
				){
					/* x,y,z of Vector3Int are properties, so they can't be used
					as out parameter */
					v = new Vector3Int(x,y,z);
					return true;
				}
			}
		}
		v = new Vector3Int(0,0,0);
		return false;
	}
	public static bool tryParse(string s,out Vector2Int v){
		int x,y;
		if(s.Length>0 && s[0]=='(' && s[s.Length-1]==')'){
			s = s.Substring(1,s.Length-2);
			string[] aComponent = s.Split(',');
			if(aComponent.Length == 2){
				if(int.TryParse(aComponent[0],out x) &&
					int.TryParse(aComponent[1],out y)
				){
					v = new Vector2Int(x,y);
					return true;
				}
			}
		}
		v = new Vector2Int(0,0);
		return false;
	}
	public static bool tryParse(string s,out Color color){
		if(s.Length>0 && s.Substring(0,5)=="RGBA(" && s[s.Length-1]==')'){
			s = s.Substring(5,s.Length-6);
			string[] aComponent = s.Split(',');
			if(aComponent.Length == 4){
				if(float.TryParse(aComponent[0],out color.r) &&
					float.TryParse(aComponent[1],out color.g) &&
					float.TryParse(aComponent[2],out color.b) &&
					float.TryParse(aComponent[3],out color.a)
				)
					return true;
			}
		}
		color = Color.black;
		return false;
	}
	public static bool tryParse(string sValue,Type type,out object value){
		bool bResult = false;
		switch(type.Name){
			case nameof(Int32):
				int intValue;
				bResult = int.TryParse(sValue,out intValue);
				value = intValue;
				return bResult;
			case nameof(Single):
				float floatValue;
				bResult = float.TryParse(sValue,out floatValue);
				value = floatValue;
				return bResult;
			case nameof(Boolean):
				bool boolValue;
				bResult = bool.TryParse(sValue,out boolValue);
				value = boolValue;
				return bResult;
			case nameof(Vector3):
				Vector3 vector3Value;
				bResult = tryParse(sValue,out vector3Value);
				value = vector3Value;
				return bResult;
			case nameof(Vector2):
				Vector2 vector2Value;
				bResult = tryParse(sValue,out vector2Value);
				value = vector2Value;
				return bResult;
			case nameof(Vector3Int):
				Vector3Int vector3IntValue;
				bResult = tryParse(sValue,out vector3IntValue);
				value = vector3IntValue;
				return bResult;
			case nameof(Vector2Int):
				Vector2Int vector2IntValue;
				bResult = tryParse(sValue,out vector2IntValue);
				value = vector2IntValue;
				return bResult;	
			case nameof(Color):
				Color colorValue;
				bResult = tryParse(sValue,out colorValue);
				value = colorValue;
				return bResult;
		}
		value = null;
		return false;
	}
}