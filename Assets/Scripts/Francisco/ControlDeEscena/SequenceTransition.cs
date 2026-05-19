using System.Collections;
using UnityEngine;

public class SequenceTransition : TransitionInteractive
{
    #region Estado

    private bool isSequenceRunning = false;

    #endregion

    #region API Pública

    public bool IsSequenceRunning => isSequenceRunning;

    public IEnumerator ExecuteSequence(Transform playerTransform)
    {
        isSequenceRunning = true;

        yield return StartCoroutine(RunNodes(playerTransform));

        RestoreControl();

        isSequenceRunning = false;
    }

    #endregion
}