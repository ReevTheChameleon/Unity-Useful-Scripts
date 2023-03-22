/*************************************************************************
 * ONBUILDATTRIBUTE (v2.1)
 * by Reev the Chameleon
 * 13 Mar 3
**************************************************************************
Usage:
Mark a MonoBehaviour with [RemoveOnBuild] attribute to exclude it from build
Note: If your function relies on Editor namespace, enclose the attribute in #if UNITY_EDITOR

Update v1.0.1: Move code into namespace Chameleon
Update v1.0.2: Move some common code to ObjectExtension
Update v1.1: Add bool property to specify whether to exclude from Editor build
Update v2.0: Add [ExecuteOnBuild] and rewrite logic so classes inherit from ExecuteOnBuildBase
Update v2.1: Fix build error and execution in Edit Mode error
*/

using UnityEngine;
using System;
using System.Reflection;
#if !UNITY_2020_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#endif

using Object = UnityEngine.Object;

namespace Chameleon{

[AttributeUsage(AttributeTargets.Class)] //Inherited=true by default
public abstract class ExecuteOnBuildBaseAttribute : Attribute{
	public bool bApplyInEditorBuild=false;
	public abstract void onBuild(MonoBehaviour m);
}

#if UNITY_EDITOR
static class OnBuildExecuter{
	[PostProcessScene]
	/* Static functions marked with this attribute seems to be called BEFORE 
	saving build file according to build pipeline as described in
	https://uninomicon.com/scenemanager and mentioned by bluescrn, UF & YinXiaozhou, UA.
	It also seems to be called for EACH scene (Credit: Baste, UF)
	Note: They are also called when switched to play mode in editor, although 
	it seems they are called AFTER Awake() in that case (Credit: voldemartz & zach-r-d, UA) */
	static void onBuild(){
		ObjectExtension.forAllObjectsOfType<MonoBehaviour>((m)=>{
			foreach(ExecuteOnBuildBaseAttribute attribute in
				m.GetType().GetCustomAttributes<ExecuteOnBuildBaseAttribute>())
			{
				//Credit: jister, UF
				if(EditorApplication.isPlaying && !attribute.bApplyInEditorBuild){
					return;}
				attribute.onBuild(m);
			}
		});
	}
}
#endif

public class ExecuteOnBuildAttribute : ExecuteOnBuildBaseAttribute{
	public string actionName;
	public Type type=null;
	public ExecuteOnBuildAttribute(string actionName,Type type=null){
		this.actionName = actionName;
		this.type = type;
	}
	public override void onBuild(MonoBehaviour m){
		if(type==null){
			m.GetType().GetMethod(actionName,ReflectionHelper.BINDINGFLAGS_ALL)
				?.Invoke(m,null);}
		else{
			/* Methods on static class must accept one MonoBehaviour argument.
			If not, there is no reason to mark it over MonoBehaviour class,
			and you shouldn't use this in the first place. */
			type.GetMethod(actionName,ReflectionHelper.BINDINGFLAGS_ALL)
				?.Invoke(null,new MonoBehaviour[]{m});}
	}
}

public class RemoveOnBuildAttribute : ExecuteOnBuildBaseAttribute{
	public override void onBuild(MonoBehaviour m){
		Object.DestroyImmediate(m);
	}
}

} //end namespace Chameleon

