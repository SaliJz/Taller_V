using UnityEngine;

[System.Serializable]
public class ConnectionPoint : MonoBehaviour
{
    public bool isConnected = false;
    public Transform connectedTo;

    public ConnectionType connectionType;

    void OnDrawGizmos()
    {
        Gizmos.color = isConnected ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
}