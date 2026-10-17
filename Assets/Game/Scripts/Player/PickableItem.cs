using Unity.Netcode;
using UnityEngine;

public class PickableItem : NetworkBehaviour
{
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


}
