using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemEffectPool : MonoBehaviour
{
    #region Singleton

    public static ItemEffectPool Instance { get; private set; }

    #endregion

    #region Inspector Fields

    [Header("Dash Fire Pool")]
    [SerializeField] private GameObject dashCirclePrefab;
    [SerializeField] private int dashPoolSize = 8;

    [Header("Petra Spike Pool (Small - Shield)")]
    [SerializeField] private GameObject smallSpikePrefab;
    [SerializeField] private int smallSpikePoolSize = 15;

    [Header("Petra Spike Pool (Large - Melee)")]
    [SerializeField] private GameObject largeSpikePrefab;
    [SerializeField] private int largeSpikePoolSize = 10;

    #endregion

    #region Private Fields

    private Queue<DashFire> dashAvailable = new Queue<DashFire>();
    private List<DashFire> dashAll = new List<DashFire>();

    private Queue<PetraSpike> smallSpikeAvailable = new Queue<PetraSpike>();
    private List<PetraSpike> smallSpikeAll = new List<PetraSpike>();

    private Queue<PetraSpike> largeSpikeAvailable = new Queue<PetraSpike>();
    private List<PetraSpike> largeSpikeAll = new List<PetraSpike>();

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        InitializeDashPool();
        InitializeSmallSpikePool();
        InitializeLargeSpikePool();
    }

    #endregion

    #region Initialization

    private void InitializeDashPool()
    {
        if (dashCirclePrefab == null) return;
        for (int i = 0; i < dashPoolSize; i++)
        {
            GameObject go = Instantiate(dashCirclePrefab, Vector3.zero, Quaternion.identity, transform);
            DashFire circle = go.GetComponent<DashFire>();
            go.SetActive(false);
            dashAvailable.Enqueue(circle);
            dashAll.Add(circle);
        }
    }

    private void InitializeSmallSpikePool()
    {
        if (smallSpikePrefab == null) return;
        for (int i = 0; i < smallSpikePoolSize; i++)
        {
            GameObject go = Instantiate(smallSpikePrefab, Vector3.zero, Quaternion.identity, transform);
            PetraSpike spike = go.GetComponent<PetraSpike>();
            go.SetActive(false);
            smallSpikeAvailable.Enqueue(spike);
            smallSpikeAll.Add(spike);
        }
    }

    private void InitializeLargeSpikePool()
    {
        if (largeSpikePrefab == null) return;
        for (int i = 0; i < largeSpikePoolSize; i++)
        {
            GameObject go = Instantiate(largeSpikePrefab, Vector3.zero, Quaternion.identity, transform);
            PetraSpike spike = go.GetComponent<PetraSpike>();
            go.SetActive(false);
            largeSpikeAvailable.Enqueue(spike);
            largeSpikeAll.Add(spike);
        }
    }

    #endregion

    #region Dash Fire Methods

    public void SpawnDashFire(Vector3 position, float damage, float expandDuration, float maxRadius,
                      float stayDuration, float tickInterval, LayerMask enemyLayer)
    {
        DashFire circle;

        if (dashAvailable.Count > 0)
        {
            circle = dashAvailable.Dequeue();
        }
        else
        {
            circle = dashAll[0];
            circle.ForceReturn();
        }

        circle.Activate(position, damage, expandDuration, maxRadius,
                        stayDuration, tickInterval, enemyLayer,
                        () => ReturnDashFire(circle));
    }

    public void ReturnDashFire(DashFire circle)
    {
        circle.gameObject.SetActive(false);
        if (!dashAvailable.Contains(circle))
            dashAvailable.Enqueue(circle);
    }

    #endregion

    #region Petra Spike Methods

    public void SpawnSpike(Vector3 position, Quaternion rotation, float damage, float lifetime, LayerMask enemyLayer, bool isLargeSpike)
    {
        Queue<PetraSpike> targetQueue = isLargeSpike ? largeSpikeAvailable : smallSpikeAvailable;
        List<PetraSpike> targetList = isLargeSpike ? largeSpikeAll : smallSpikeAll;

        PetraSpike spike;

        if (targetQueue.Count > 0)
        {
            spike = targetQueue.Dequeue();
        }
        else
        {
            spike = targetList[0];
            spike.StopAllCoroutines();
            ReturnSpike(spike, isLargeSpike);
            spike = targetQueue.Dequeue();
        }

        Vector3 finalPos = position;
        if (Physics.Raycast(position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 2f))
        {
            if (hit.collider.CompareTag("Ground"))
            {
                float sinkAmount = isLargeSpike ? 0.3f : 0.15f;
                finalPos = hit.point - (Vector3.up * sinkAmount);
            }
        }

        spike.transform.SetPositionAndRotation(finalPos, rotation);
        spike.gameObject.SetActive(true);

        spike.Initialize(damage, lifetime, enemyLayer, isLargeSpike, () => ReturnSpike(spike, isLargeSpike));
    }

    public void ReturnSpike(PetraSpike spike, bool isLargeSpike)
    {
        spike.gameObject.SetActive(false);
        Queue<PetraSpike> targetQueue = isLargeSpike ? largeSpikeAvailable : smallSpikeAvailable;

        if (!targetQueue.Contains(spike))
        {
            targetQueue.Enqueue(spike);
        }
    }

    #endregion
}