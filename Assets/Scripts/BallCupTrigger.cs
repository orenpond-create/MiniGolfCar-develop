using UnityEngine;

// Sits at the bottom of a cup and logs when the golf ball drops in.
// Trigger colliders are ignored, so the ball's large click-helper trigger doesn't count as holed.
[RequireComponent(typeof(Collider))]
public class BallCupTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger)
            return;
        if (other.GetComponentInParent<Ball>() != null)
            Debug.Log("Ball holed in " + name + "!");
    }
}
