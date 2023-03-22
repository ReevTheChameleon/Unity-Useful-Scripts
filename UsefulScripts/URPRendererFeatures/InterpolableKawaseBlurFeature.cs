/*************************************************************************
 * INTERPOLABLEKAWASEBLURFEATURE (v1.1.1)
 * by Reev the Chameleon
 * 18 Dec 2
**************************************************************************
A URP ScriptableRendererFeature that captures screen, adds blur, then
re-renders either back to the screen or onto target texture.
The blur is implemented based on Kawase Blur algorithm, but modified
to also "somewhat" support fractional iteration number.
Usage:
1) Make sure that your project is using URP
2) Under the "ForwardRenderer" asset, "Add Renderer Feature" and select this feature
3) Choose "Pass Event" to specify when in the render pipeline to insert this feature
4) Specify "Downsample" and "Iteration" for the blur
5) Tick BBlitToScreen toggle to render the result back to the screen, or untick it
and specify the GlobalTexture name to render to. In the former case, you might
need to use Screen Space rather than Overlay Canvas because Overlay ignore render queue
and will render on top of your blur.
Reference: This script uses the following as main reference
- Tutorial on ScriptableRendererFeature: https://samdriver.xyz/article/scriptable-render
- Example Code for Kawase Blur Algorithm: https://github.com/sebastianhein/urp_kawase_blur
! Note:
This script uses SEPARATE PASSES for downsampling and upsampling to improve
interpolation result, meaning that it uses 2 more passes than the optimized
Kawase Blur Algorithm. If you do not need interpolation and require more optimization,
please checkout DualKawaseBlurFeature.

Update v1.1: Move class to namespace Chameleon and allow access to RenderPass from RendererFeature
Update v1.1.1: Update usage detail
*/

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Chameleon{

public class InterpolableKawaseBlurFeature : ScriptableRendererFeature{
	[SerializeField] RenderPassEvent passEvent =RenderPassEvent.AfterRenderingSkybox;
	[SerializeField] Material material =null;
	[SerializeField][Min(1.0f)] float downsample =1.0f;
	[SerializeField][Min(0.0f)] float iteration =3;
	[SerializeField] string outputTextureName = "BlurTexture";
	[SerializeField] bool bBlitToScreen =true;

	/* In fact I do not really like to expose this, but due to how Unity works,
	changing variables in ScriptableRendererFeature does NOT pass over to ScriptableRendererPass
	unless it is created anew, which becomes very wasteful if you want something to change
	dynamically at runtime. Hence, you need direct access to the pass at runtime. */
	public InterpolableKawaseBlurPass Pass{get; private set;}

	//This is called every frame
	public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData renderingData){
		Pass.cameraRT = renderer.cameraColorTarget;
		renderer.EnqueuePass(Pass);
	}
	//This is called once some fields change
	public override void Create(){
		Pass = new InterpolableKawaseBlurPass();
		Pass.renderPassEvent = passEvent;
		Pass.material = material;
		Pass.downsample = Mathf.Max(1,downsample);
		Pass.iteration = Mathf.Max(0,iteration);
		Pass.textureName = outputTextureName;
		Pass.bBlitToScreen = bBlitToScreen;
	}

	#if UNITY_EDITOR
	[CustomEditor(typeof(InterpolableKawaseBlurFeature))]
	public class InterpolableKawaseBlurFeatureEditor : Editor{
		public override void OnInspectorGUI(){
			bool bBlitToScreen = serializedObject.FindProperty("bBlitToScreen").boolValue;
			if(bBlitToScreen)
				DrawPropertiesExcluding(serializedObject,"m_Script","outputTextureName");
			else
				DrawPropertiesExcluding(serializedObject,"m_Script");
		}
	}
	#endif

	public class InterpolableKawaseBlurPass : ScriptableRenderPass{
		internal RenderTargetIdentifier cameraRT;
		public Material material;
		public string textureName;
		public bool bBlitToScreen;
		public float downsample;
		public float iteration;
		private int tmpRT1 = Shader.PropertyToID("tmpRTKawase1");
		private int tmpRT2 = Shader.PropertyToID("tmpRTKawase2");

		private static readonly int globalIDOffsetPixel =
			Shader.PropertyToID("KawaseBlur_offsetPixel");
		private static readonly int globalIDWeight =
			Shader.PropertyToID("KawaseBlur_weight");

		public override void Execute(ScriptableRenderContext context,ref RenderingData renderingData){
			CommandBuffer commandBuffer = CommandBufferPool.Get();
			commandBuffer.Blit(cameraRT,tmpRT1); //downsample

			if(material && iteration>0.0f){
				commandBuffer.SetGlobalInt(globalIDOffsetPixel,1);
				commandBuffer.SetGlobalFloat(globalIDWeight,1.0f);
				float iterationLeft = iteration;
				int i=0;
				while(iterationLeft > 1.0f){
					commandBuffer.SetGlobalFloat(globalIDOffsetPixel,++i);
					commandBuffer.Blit(tmpRT1,tmpRT2,material,0);
					swapTmpRT();
					--iterationLeft;
				}
				if(iterationLeft > 0.0f){
					commandBuffer.SetGlobalInt(globalIDOffsetPixel,++i);
					commandBuffer.SetGlobalFloat(globalIDWeight,iterationLeft);
					commandBuffer.Blit(tmpRT1,tmpRT2,material,0);
					swapTmpRT();
				}
			}

			if(bBlitToScreen)
				commandBuffer.Blit(tmpRT1,cameraRT);
			else{
				commandBuffer.Blit(tmpRT1,tmpRT2);
				commandBuffer.SetGlobalTexture(textureName,tmpRT2);
			}
			context.ExecuteCommandBuffer(commandBuffer);
			CommandBufferPool.Release(commandBuffer);
		}
		private void swapTmpRT(){
			int tmp = tmpRT1;
			tmpRT1 = tmpRT2;
			tmpRT2 = tmp;
		}
		public override void Configure(CommandBuffer cmd,RenderTextureDescriptor cameraTextureDescriptor){
			int downsampledWidth = (int)(cameraTextureDescriptor.width/downsample);
			int downsampledHeight = (int)(cameraTextureDescriptor.height/downsample);
			cmd.GetTemporaryRT(tmpRT1,downsampledWidth,downsampledHeight);
			cmd.GetTemporaryRT(tmpRT2,downsampledWidth,downsampledHeight);
			ConfigureTarget(tmpRT1);
			ConfigureTarget(tmpRT2); //credit: Sebastian Hein, github
		}
		public override void FrameCleanup(CommandBuffer cmd){
			cmd.ReleaseTemporaryRT(tmpRT1);
			cmd.ReleaseTemporaryRT(tmpRT2);
		}
	}
}

} //end namespace Chameleon
