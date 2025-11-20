using UnityEngine;

public class FleshPulseNerve : MonoBehaviour
{
    [SerializeField] private LayerMask _obstacleLayer;
    [SerializeField] private LayerMask _playerLayer;

    private FleshPulseController _controller;

    public void Initialize(FleshPulseController controller, LayerMask obstacleMask, LayerMask playerMask)
    {
        _controller = controller;
        _obstacleLayer = obstacleMask;
        _playerLayer = playerMask;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & _playerLayer) == 0) return;

        if (_controller == null) return;

        GameObject target = other.gameObject;

        if (IsBlockedByWall(target.transform.position))
        {
            Debug.Log("[FleshPulseNerve] Golpe bloqueado por pared.");
            return;
        }

        _controller.TryHitPlayer(target);
    }

    private bool IsBlockedByWall(Vector3 targetPosition)
    {
        Vector3 origin = _controller.transform.position;
        origin.y += 0.5f;
        targetPosition.y += 0.5f;

        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, _obstacleLayer))
        {
            Debug.Log("[FleshPulseNerve] Está bloqueado por: " + hit.collider.gameObject.name);
            return true;
        }

        Debug.Log("[FleshPulseNerve] No está bloqueado.");
        return false;
    }
}