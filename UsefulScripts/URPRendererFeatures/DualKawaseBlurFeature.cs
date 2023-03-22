/*************************************************************************
 * DUALKAWASEBLURFEATURE (v1.1)
 * by Reev the Chameleon
 * 13 Nov 2
**************************************************************************
A URP ScriptableRendererFeature that captures screen, adds blur, then
re-renders either back to the screen or onto target texture.
The blur is implemented based on Dual Kawase Blur algorithm:
https://community.arm.com/cfs-file/__key/communityserver-blogs-components-weblogfiles/00-00-00-20-66/siggraph2015_2D00_mmg_2D00_marius_2D00_notes.pdf
Usage:
1) Make sure that your project is using URP
2) Under the "ForwardRenderer" asset, "Add Renderer Feature" and select this feature
3) Choose "Pass Event" to specify when in the render pipeline to insert this feature
4) Specify "Downsample" and "Iteration" for the blur
5) Tick BBlitToScreen toggle to render the result back to the screen, or untick it
and specify the GlobalTexture name to render to.

Update v1.1: Move class to namespace Chameleon and allow access to RenderPass from RendererFeature
*/

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Chameleon{

public class DualKawaseBlurFeature : ScriptableRendererFeature{
	[SerializeField] RenderPassEvent passEvent =RenderPassEvent.AfterRenderingSkybox;
	[SerializeField] Material material =null;
	[SerializeField][Min(0)] int iteration =3;
	[SerializeField] string outputTextureName = "BlurTexture";
	[SerializeField] bool bBlitToScreen =true;
	public DualKawaseBlurPass Pass{get; private set;}

	public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData renderingData){
		Pass.cameraRT = renderer.cameraColorTarget;
		renderer.EnqueuePass(Pass);
	}
	public override void Create(){
		Pass = new DualKawaseBlurPass();
		Pass.renderPassEvent = passEvent;
		Pass.material = material;
		Pass.iteration = Mathf.Max(0,iteration);
		Pass.textureName = outputTextureName;
		Pass.bBlitToScreen = bBlitToScreen;
		Pass.initTmpRTArray();
	}

	#if UNITY_EDITOR
	[CustomEditor(typeof(DualKawaseBlurFeature))]
	public class DualKawaseBlurFeatureEditor : Editor{
		public override void OnInspectorGUI(){
			bool bBlitToScreen = serializedObject.FindProperty("bBlitToScreen").boolValue;
			if(bBlitToScreen)
				DrawPropertiesExcluding(serializedObject,"m_Script","outputTextureName");
			else
				DrawPropertiesExcluding(serializedObject,"m_Script");
		}
	}
	#endif

	public class DualKawaseBlurPass : ScriptableRenderPass{
		internal RenderTargetIdentifier cameraRT;
		public Material material;
		public string textureName;
		public bool bBlitToScreen;
		public int iteration;
		private int[] aTmpRT;
		private int targetRT;

		public void initTmpRTArray(){
			aTmpRT = new int[(iteration+1)/2]; //credit: Ian Nelson, SO
			for(int i=0; i<aTmpRT.Length; ++i)
				aTmpRT[i] = Shader.PropertyToID("tmpRTDualKawase"+i);
		}

		private static readonly int globalIDOffsetPixel =
			Shader.PropertyToID("KawaseBlur_offsetPixel");
		private static readonly int globalIDWeight =
			Shader.PropertyToID("KawaseBlur_weight");

		public override void Execute(ScriptableRenderContext context,ref RenderingData renderingData){
			if(!material || iteration<=0)
				return;
			CommandBuffer commandBuffer = CommandBufferPool.Get();
			int offsetPixel = 1;
			commandBuffer.SetGlobalInt(globalIDOffsetPixel,offsetPixel);
			commandBuffer.SetGlobalFloat(globalIDWeight,0.25f);
			
			if(iteration%2 == 0){ //iteration is even
				commandBuffer.Blit(cameraRT,aTmpRT[0],material,0);
				for(int i=0; i<iteration/2-1; ++i)
					commandBuffer.Blit(aTmpRT[i],aTmpRT[i+1],material,0);
				for(int i=iteration/2-1; i>0; --i) //save last iteration to blit to target (Credit: Alexander Ameye, github)
					commandBuffer.Blit(aTmpRT[i],aTmpRT[i-1],material,1);
			}
			else{ //iteration is odd, there is no middle
				if(iteration == 1)
					commandBuffer.Blit(cameraRT,aTmpRT[0]);
				else if(iteration == 3){
					commandBuffer.Blit(cameraRT,aTmpRT[0],material,0);
					commandBuffer.Blit(aTmpRT[0],aTmpRT[1],material,1);
				}
				else{
					commandBuffer.Blit(cameraRT,aTmpRT[0],material,0);
					for(int i=0; i<iteration/2; ++i)
						commandBuffer.Blit(aTmpRT[i],aTmpRT[i+1],material,0);
					commandBuffer.Blit(aTmpRT[iteration/2],aTmpRT[iteration/2-2],material,1);
					for(int i=iteration/2-2; i>0; --i)
						commandBuffer.Blit(aTmpRT[i],aTmpRT[i-1],material,1);
				}
			}
			
			if(bBlitToScreen)
				commandBuffer.Blit(aTmpRT[0],cameraRT,material,1);
			else{
				commandBuffer.Blit(aTmpRT[0],targetRT,material,1);
				commandBuffer.SetGlobalTexture(textureName,targetRT);
			}
			context.ExecuteCommandBuffer(commandBuffer);
			CommandBufferPool.Release(commandBuffer);
		}
		public override void Configure(CommandBuffer cmd,RenderTextureDescriptor cameraTextureDescriptor){
			int downsampledWidth = cameraTextureDescriptor.width;
			int downsampledHeight = cameraTextureDescriptor.height;
			for(int i=0; i<=iteration/2-1; ++i){
				downsampledWidth /= 2;
				downsampledHeight /= 2;
				cmd.GetTemporaryRT(aTmpRT[i],downsampledWidth,downsampledHeight,0,
					FilterMode.Bilinear,RenderTextureFormat.ARGB32);
				ConfigureTarget(aTmpRT[i]);
			}
			if(iteration%2 != 0) //iteration is odd, there is no middle
				cmd.GetTemporaryRT(aTmpRT[iteration/2],downsampledWidth,downsampledHeight,0,
					FilterMode.Bilinear,RenderTextureFormat.ARGB32);
		}
		public override void FrameCleanup(CommandBuffer cmd){
			for(int i=0; i<aTmpRT.Length; ++i)
				cmd.ReleaseTemporaryRT(aTmpRT[i]);
		}
	}
}

} //end namespace Chameleon
