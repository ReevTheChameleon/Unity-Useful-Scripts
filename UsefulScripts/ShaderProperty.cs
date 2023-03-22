/******************************************************************************
 * SHADERPROPERTY (v1.0)
 * by Reev the Chameleon
 * 5 Feb 2
*******************************************************************************
Usage:
Add menu to auto-generate source code of static variables representing list of
shader properties for selected shader. This class maintains property IDs as
obtained from Shader.propertyToID() and uses them to get/set material property
for efficiency.
Example Usage:
myMaterial.setFloat(ShaderProperty.myProperty,3.0f);
Variable name is the same as the string you would use in normal GetX/SetX.
However, using the syntax above (extension function from ShaderProperty class),
this class will also help perform type checking at compile time.
*/

using UnityEngine;
using System.IO;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Chameleon{

public static partial class ShaderProperty{
	/* Because accessing Shader Property by ID is more efficient than by string,
	normal practice is to call Shader.PropertyToID() and save the ID for subsequent use.
	Note that this value changes session by session and thus cannot be saved. However,
	one can save list of ID in static class and let other script access it to avoid
	code duplication (Credit Idea: bgolus, UF). */
	/* Currently, there are 2 main options:
	1) Store only int id; in the struct, and initialize it via static field initializer
	(and with help of implicit operator taking int):
	static Property<T> property = Shader.PropertyToID();
	Advantage is simplicity and the fact that every property is ready for use out of the bat.
	Problem is that the FIRST TIME this static class (ShaderProperty) is accessed,
	EVERY property in it will be initialized at the same time. Because PropertyToID() is
	not that fast, calling it hundreds time at once will definitely cause lag. In fact, from
	profiling, it is found that adding new property Shader.PropertyToID() takes about
	1/300 ms per property, which is quite significant, especially when you have to pay even
	when you are not using that property. (As comparison, the "slow" GetComponent<> takes about
	1/30000 ms per call, granted both are designed to be called only once on startup.)
	2) Current approach, that is to also store string name; in the struct, and when accessed
	check whether the property has initialized. If not, initialize it using the saved string.
	Problem is obviously memory needed to store extra string because it is impossible to
	selectively obtain variable name at runtime (nameof() is compile time operator, and using it
	on parameter will just give the PARAMETER name not the underlying passed in argument),
	and also the problem of extra initialization check for every call (but this overhead is
	very small because it is just one extra if statement).
	If there is other alternative, may reconsider approach. */
	public struct Property<T>{
		public int Id {get; private set;}
		private string name;
		public static implicit operator Property<T>(string in_name){
			return new Property<T>{name=in_name,Id=-1};
		}
		public void initialize(){
			if(Id == -1)
				Id = Shader.PropertyToID(name);
		}
	}
	public static float getFloat(this Material material,Property<float> property){
		if(property.Id == -1)
			property.initialize();
		return material.GetFloat(property.Id);
	}
	public static Color getColor(this Material material,Property<Color> property){
		if(property.Id == -1)
			property.initialize();
		return material.GetColor(property.Id);
	}
	public static Vector4 getVector(this Material material,Property<Vector4> property){
		if(property.Id == -1)
			property.initialize();
		return material.GetVector(property.Id);
	}
	public static Texture getTexture(this Material material,Property<Texture> property){
		if(property.Id == -1)
			property.initialize();
		return material.GetTexture(property.Id);
	}

	public static void setFloat(this Material material,Property<float> property,float f){
		if(property.Id == -1)
			property.initialize();
		material.SetFloat(property.Id,f);
	}
	public static void setColor(this Material material,Property<Color> property,Color c){
		if(property.Id == -1)
			property.initialize();
		material.SetColor(property.Id,c);
	}
	public static void setVector(this Material material,Property<Vector4> property,Vector4 v4){
		if(property.Id == -1)
			property.initialize();
		material.SetVector(property.Id,v4);
	}
	public static void setTexture(this Material material,Property<Texture> property,Texture t){
		if(property.Id == -1)
			property.initialize();
		material.SetTexture(property.Id,t);
	}
//----------------------------------------------------------------------------------
	#region AUTOGENERATE MENU
	#if UNITY_EDITOR
	
	[MenuItem("Assets/&Auto Generate/&Shader Property ID List")]
	static void AssetsGenerateShaderPropertyIDList(){
		Shader shader = Selection.activeObject as Shader;
		if(!shader) //Should not happen unless this function is called directly from script.
			return;
		string filePath =
			EditorPath.getSelectionFolder() + "/" +
			shader.name.Replace("/","_") + "_PropertyID.cs" //There may be slash in shader name
		;
		
		List<string> lPrevDefined = new List<string>();
		if(File.Exists(filePath)){
			/* If file exists, actually safest option is to delete it and let all scripts
			recompile so we can actually check whether, with old file excluded, each property already
			exists in the class from other files or not. HOWEVER, I was not able to tell
			the code to wait and run after recompilation (AssetDatabase.DeleteAsset() followed
			by Refresh() is called AFTER the function ends, and CompilationPipeline events
			as well as AssemblyReloadEvents do not perform as expected).
			Hence current approach is to open and search the file for defined variables.
			Current search algorithm is still in experimental stage. */
			SourceCodeFiddler sourceCodeFiddler = new SourceCodeFiddler();
			if(sourceCodeFiddler.readFile(filePath)){
				sourceCodeFiddler.moveToToken("ShaderProperty");
				while(sourceCodeFiddler.moveToToken("Property"))
					lPrevDefined.Add(sourceCodeFiddler.nextToken());
			}
		}

		using(StreamWriter streamWriter = new StreamWriter(File.Create(filePath))){
			/* Should avoid using "\n" in WriteLine() because it will cause difference in
			line ending style and will generate warnings */
			streamWriter.WriteLine("/********** [Auto Generated Code] ************************************");
			streamWriter.WriteLine(" Generated by: ShaderProperty v.1.0 by Reev the Chameleon");
			streamWriter.WriteLine(" For Shader: " + shader.name);
			streamWriter.WriteLine("!!! Comments written here are removed if code is regenerated !!!");
			streamWriter.WriteLine("**********************************************************************/");
			streamWriter.WriteLine();
			streamWriter.WriteLine("using UnityEngine;");
			streamWriter.WriteLine();
			streamWriter.WriteLine("namespace Chameleon{");
			streamWriter.WriteLine();
			streamWriter.WriteLine("public static partial class ShaderProperty{");
			
			for(int i=0; i<ShaderUtil.GetPropertyCount(shader); ++i){ //Credit draft: robertbu, UA
				string propertyName = ShaderUtil.GetPropertyName(shader,i);
				string propertyTypeT;
				switch(ShaderUtil.GetPropertyType(shader,i)){
					case ShaderUtil.ShaderPropertyType.Float:
					case ShaderUtil.ShaderPropertyType.Range:
						propertyTypeT = "float";
						break;
					case ShaderUtil.ShaderPropertyType.Color:
						propertyTypeT = "Color";
						break;
					case ShaderUtil.ShaderPropertyType.Vector:
						propertyTypeT = "Vector4";
						break;
					case ShaderUtil.ShaderPropertyType.TexEnv:
						propertyTypeT = "Texture";
						break;
					default:
						propertyTypeT = "";
						break;
				}
				/* If field has already existed from other auto-generated code, comment out,
				But if it exists in THIS file which will be overwritten, don't comment out. */
				bool bDefined =
					typeof(ShaderProperty).GetField(propertyName) != null &&
					!lPrevDefined.Contains(propertyName)
				;
				streamWriter.WriteLine(
					"\t" + (bDefined ? "//" : "") +
					"public static Property<"+propertyTypeT+"> "+ propertyName +
					" = \"" + propertyName + "\";"
				);
			}
			streamWriter.WriteLine("}");
			streamWriter.WriteLine();
			streamWriter.WriteLine("} //end namespace Chameleon");
		}
		AssetDatabase.Refresh();
	}
	[MenuItem("Assets/&Auto Generate/&Shader Property ID List",true)]
	static bool AssetsGenerateShaderPropertyIDListValidate(){
		return Selection.activeObject.GetType()==typeof(Shader);
	}

	#endif
	#endregion
//----------------------------------------------------------------------------------
}

} //end namespace Chameleon
