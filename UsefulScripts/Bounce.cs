/************************************************************************
 * BOUNCE (v1.0)
 * by Reev the Chameleon
 * 11 Mar 2
 ************************************************************************
Usage:
Attach this script GameObject to achieve correct bounce according to
bounciness set in Physics Material asset.
Rationale:
There is a problem in Unity where when you set bounciness in Physics Material
to 1 the object seems to gain energy each time collision occur. This is most
easily seen when dropping ball with such setting on the ground. It is well known
that such ball will continuously gain height regardless of whatever setting
combination used.
People have suggested many solutions, such as setting "Collision Detection" to
"Continuous" (Credit: LiterallyJeff, UF & derianmeyer, Andaho, UA)or setting
bounciness to some archaic values like 0.9803922 (Credit: mbmt, UA) or 0.9805824
(Credit: leonalchemist, UA). Neither of these solutions work satisfactorily
(specifically for Unity version 2020.3 and non-AMD graphic card, if that matters).
However, by trial and error, I have found that by setting position to the one
in previous FixedUpdate() call in OnCollisionEnter, perfect bounce can be achieved.
While this has only been tested on position, it is deduced that rotation should
also be reverted, and hence the current code.
*/

using UnityEngine;

namespace Chameleon{

[RequireComponent(typeof(Rigidbody))]
public class Bounce : MonoBehaviour{
	Rigidbody rb;
	private TransformData prevTransformData;

	void Awake(){
		rb = GetComponent<Rigidbody>();
		prevTransformData = transform.save();
	}
	void FixedUpdate(){
		prevTransformData = transform.save();
	}
	void OnCollisionEnter(Collision c){
		transform.load(prevTransformData);
	}
}

} //end namespace Chameleon
