using System.Collections;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// Used for debugging. Simply click to solve.
/// </summary>
public class DummyModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable Selectable;

    private bool _isSolved;

    void Start()
    {
        Selectable.OnInteract = ButtonInteract;
        _isSolved = false;
    }

    private bool ButtonInteract()
    {
        if (!_isSolved)
        {
            Module.HandlePass();
            _isSolved = true;
        }
        return false;
    }

    private IEnumerator SolveAfterRandomTime()
    {
        yield return new WaitForSeconds(Rnd.Range(1f, 3f));
        _isSolved = true;
        Module.HandlePass();
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        StartCoroutine(SolveAfterRandomTime());
        while (!_isSolved)
            yield return true;
    }
}
