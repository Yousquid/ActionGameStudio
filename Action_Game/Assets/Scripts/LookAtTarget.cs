using UnityEngine;

public class LookAtTarget : MonoBehaviour
{
    public Transform player;

    private void Update()
    {
        this.transform.position = player.position;
    }
}
