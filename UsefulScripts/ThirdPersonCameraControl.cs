/*************************************************************************
 * THIRDPERSONCAMERACONTROL (v1.0)
 * by Reev the Chameleon
 * 12 Oct 2
**************************************************************************
Usage:
1) Attach this script to the GameObject intended to be a camera (either
the one with Camera Component or CinemachineVirtualCamera Component).
2) Assign tTarget. This is the target that the camera will look at.
Camera will attempt to match its rotation with tTarget.rotation and follow
tTarget, maintaining targetCameraDistance behind it.
3) Adjust damping. Camera will move smoothly from current position to
the position defined by tTarget.
4) Choose Update Method for camera follow and look, depending on
how tTarget position and rotation updates. If experiencing stuttering,
try changing Update Method.

Rationale: I decided to write this class rather than using Unity's Cinemachine
because it does not handle lerping distance (zooming) automatically;
you need to do lerping yourself either in Coroutine or in Update, which
defeats the purpose because you have to do update yourself ON TOP of Cinemachine's
update, and if you do that, you can just as well do the entire thing so you can
fine-tune anyway.
If wanting Cinemachine capability, this script can be attached to VCam
with "Do Nothing", and turn on the VCam on demand when needed.
*/

using UnityEngine;

namespace Chameleon{

public enum eUpdateMethod{FixedUpdate,LateUpdate,}

public class ThirdPersonCameraControl : MonoBehaviour{
	[Header("Camera")]
	Camera targetCamera;
	[Min(0.000001f)] public float epsilonLinear =0.0001f;
	[Min(0.000001f)] public float epsilonRotation =0.000001f;
	[Min(0.0f)] public float cameraRadius =0.2f;

	[Header("Target")]
	public Transform tTarget;
	[Tooltip("Choose FixedUpdate if your character position is modified by Physics (like modified by collision)")]
	public eUpdateMethod followUpdateMethod =eUpdateMethod.FixedUpdate;
	[Min(0.0f)] public float followDamping;
	[Tooltip("Choose FixedUpdate if your character rotation is modified by Physics")]
	public eUpdateMethod lookUpdateMethod =eUpdateMethod.LateUpdate;
	[Min(0.0f)] public float lookDamping;
	
	[Header("Zoom")]
	[Min(0.0f)] public float targetCameraDistance;
	[Min(0.0f)] public float zoomDamping;
	public LayerMask obstacleLayer;
	
	private Vector3 vLook; //world space
	private Quaternion qLook;
	private float cameraDistance;

	void Awake(){
		targetCamera = GetComponent<Camera>();
	}
	void Start(){
		applySettingsImmediate();
	}
	public void applySettingsImmediate(){
		vLook = tTarget.position;
		qLook = tTarget.rotation;
		cameraDistance = targetCameraDistance;
		transform.position = vLook-tTarget.forward*cameraDistance;
		transform.rotation = qLook;
	}
	private void updateFollow(float deltaTime){
		Vector3 vDelta = tTarget.position-vLook;
		float vDeltaMagnitude = vDelta.magnitude;
		if(vDeltaMagnitude < epsilonLinear)
			return;
		vLook =
			followDamping == 0.0f ?
			tTarget.position :
			tTarget.position - (1-interpolate(deltaTime,followDamping))*vDelta
		;
	}
	private void updateLook(float deltaTime){
		if(Quaternion.Dot(qLook,tTarget.rotation) > 1.0f-epsilonRotation) //Credit: falstro, gamedev.stackexchange
			return;
		//if(qLook == tTarget.rotation) //equivalent to code above with epsilon 0.000001f
		//	return;
		Vector3 vDelta = tTarget.position-vLook;
		vDelta = Quaternion.Inverse(qLook)*vDelta;
		qLook =
			lookDamping==0.0f ?
			tTarget.rotation :
			Quaternion.Euler(Vector3Extension.lerpEulerAngles(
				qLook.eulerAngles,
				tTarget.rotation.eulerAngles,
				interpolate(deltaTime,lookDamping)
			))
			//Quaternion.Lerp(qLook,tTarget.rotation,interpolate(deltaTime,lookDamping))
			//or use Slerp for more accurate result when delta is large (Credit: Luke Hutchison, math.stackexchange)
			/* Lerping quaternions directly can produce unexpected result when some of the
			eulerAngles need to be controlled. For example, doing normal quaternion lerp
			from eulerAngles (0,0,0) to (90,0,0) will produce eulerAngles.z!=0 during the lerp,
			which in this case where eulerAngles.z is expected to be 0 at all the time,
			is not desirable. But if delta very small this may be negligible, and
			quaternion lerp can perform (a little bit) better. */
		;
		vDelta = qLook*vDelta;
		vLook = tTarget.position-vDelta;
	}
	private void updateZoom(float deltaTime){
		float deltaCameraDistance = targetCameraDistance-cameraDistance;
		if(Mathf.Abs(deltaCameraDistance) > epsilonLinear){
			cameraDistance =
				zoomDamping == 0.0f ?
				targetCameraDistance :
				cameraDistance + interpolate(deltaTime,zoomDamping)*deltaCameraDistance
			;
		}
		float occlusionDistance = getOcclusionDistance();
		if(occlusionDistance < cameraDistance)
			cameraDistance = occlusionDistance;
	}
	private float getOcclusionDistance(){
		if(obstacleLayer == 0)
			return Mathf.Infinity;
		float raycastDistance = Mathf.Min(
			cameraDistance,
			targetCamera ? targetCamera.farClipPlane : 5000.0f
		);
		RaycastHit hitInfo;
		if(Physics.SphereCast(
			vLook,
			cameraRadius,
			transform.position-vLook,
			out hitInfo,
			raycastDistance,
			obstacleLayer,
			QueryTriggerInteraction.Ignore //ignore trigger
		)){
			return hitInfo.distance;
		}
		return Mathf.Infinity;
	}
	private float interpolate(float deltaTime,float damping){
		/* Exponential decay is completely independent of framerate.
		Linear is approximation of it using instantaneous slope, so can deviate a little. */
		//return MathfExtension.exponentialDecay(0,1,deltaTime,1/damping);
		return Mathf.Clamp01(deltaTime/damping);
	}
	void FixedUpdate(){
		if(lookUpdateMethod == eUpdateMethod.FixedUpdate)
			updateLook(Time.fixedDeltaTime);
		if(followUpdateMethod == eUpdateMethod.FixedUpdate)
			updateFollow(Time.fixedDeltaTime);
	}
	void LateUpdate(){
		if(lookUpdateMethod == eUpdateMethod.LateUpdate)
			updateLook(Time.deltaTime);
		if(followUpdateMethod == eUpdateMethod.LateUpdate)
			updateFollow(Time.deltaTime);
		updateZoom(Time.deltaTime); //zoom is always in LateUpdate() (for now) because it is input-based
		
		//Apply update result in LateUpdate()
		transform.position = vLook-qLook*Vector3.forward*cameraDistance;
		transform.rotation = qLook;
	}
}

} //end namespace Chameleon
