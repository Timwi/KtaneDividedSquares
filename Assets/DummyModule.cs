using UnityEngine;

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
}
