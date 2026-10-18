using FMODUnity;
using System.Threading;
using Unity.Netcode;
using UnityEngine;

public class PickableItem : NetworkBehaviour
{
    [Header("FMOD")]
    [SerializeField] private EventReference impactEvent;
    [SerializeField] private LayerMask groundLayers = 0;

    [SerializeField, Min(0f)] private float minimumImpactSpeed = 1.5f;

    private float timer=0;


    private bool isHighlighted = false;

    public void SetHighlight(bool highlight)
    {
        Debug.Log("SetHighlight");
        if(!isHighlighted && highlight)
        {
            transform.localScale += new Vector3(0.02f,0.02f,0.02f);
        }else if(isHighlighted && !highlight)
        {
            transform.localScale -= new Vector3(0.02f, 0.02f, 0.02f);
        }

        isHighlighted = highlight;

    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Time.time - timer < 1) return;
        timer = Time.time;

        if (collision.relativeVelocity.magnitude < minimumImpactSpeed)
            return;

        if ((groundLayers.value & (1 << collision.gameObject.layer)) == 0)
            return;

        if (impactEvent.IsNull)
            return;

        RuntimeManager.PlayOneShot(impactEvent, transform.position);
    }


}
