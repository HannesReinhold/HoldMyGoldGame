using Unity.Netcode;
using UnityEngine;

public abstract class HighlightableZone : NetworkBehaviour
{
    [Tooltip("Objekt, das beim Anvisieren sichtbar wird (z. B. Outline-Mesh, Glow, 'E drücken'-Label). Im Inspector deaktiviert lassen.")]
    [SerializeField] private GameObject highlightVisual;

    public void SetHighlight(bool on)
    {
        if (highlightVisual != null && highlightVisual.activeSelf != on)
            highlightVisual.SetActive(on);
    }
}