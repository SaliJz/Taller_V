using UnityEngine;

/// <summary>
/// Mina explosiva estática colocada por el Jefe 2.
/// </summary>
public class BossExplosiveMine : BaseTrapMine
{
    #region Inspector

    [Header("Alineación al suelo")]
    [SerializeField] private float groundOffset = 0.05f;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        AlignToGround();
        Invoke(nameof(ExplodePublic), duration);
    }

    #endregion

    #region Helpers

    private void AlignToGround()
    {
        int groundLayerMask = LayerMask.GetMask("Ground");
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, groundLayerMask))
        {
            transform.position = hit.point + Vector3.up * groundOffset;
        }
    }

    // Wrapper para Invoke
    private void ExplodePublic() => Explode();

    #endregion
}