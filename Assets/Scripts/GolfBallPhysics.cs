using UnityEngine;

// Golf-ball physics settings that Unity can't store in the scene.
// - Rigidbody.maxAngularVelocity is not serialized and the project default is 7 rad/s. A 4.3 cm
//   ball rolling at 4 m/s spins at ~190 rad/s, so without raising it the ball slides instead of rolling.
// - Pins the inertia tensor to a solid sphere of the real ball radius, so the large click-helper
//   trigger collider on the ball doesn't change how it spins.
// - Lowers Physics.bounceThreshold while the ball exists. The project value (2 m/s) means no hit slower
//   than 2 m/s bounces at all, which is almost every rail hit in mini golf. It's a global setting, so the
//   original value is put back when the ball is destroyed (e.g. leaving Play mode) - other scenes keep 2.
[RequireComponent(typeof(Rigidbody))]
public class GolfBallPhysics : MonoBehaviour
{
    [SerializeField] float maxAngularVelocity = 1000f;   // rad/s
    [SerializeField] float ballRadius = 0.02135f;        // world radius in metres (regulation golf ball)
    [SerializeField] float bounceThreshold = 0.3f;       // m/s: slower hits don't bounce. Lower = bouncier at low speed

    float previousBounceThreshold;

    void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = maxAngularVelocity;

        float inertia = 0.4f * rb.mass * ballRadius * ballRadius;   // solid sphere: 2/5 m r^2
        rb.inertiaTensor = new Vector3(inertia, inertia, inertia);
        rb.inertiaTensorRotation = Quaternion.identity;

        previousBounceThreshold = Physics.bounceThreshold;
        Physics.bounceThreshold = bounceThreshold;
    }

    void OnDestroy()
    {
        Physics.bounceThreshold = previousBounceThreshold;
    }
}
