using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float damping = 0.1f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private Vector3 lastPos;

    private void Start()
    {
        lastPos = transform.position;
    }

    private void Update()
    {
        Vector3 delta = transform.position - lastPos;
        delta.y = 0f;   // Fallen und Springen zählen nicht als Laufen
        lastPos = transform.position;

        float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        animator.SetFloat(SpeedHash, speed, damping, Time.deltaTime);
    }
}