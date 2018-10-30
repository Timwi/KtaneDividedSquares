using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DividedSquares;
using KModkit;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Divided Squares
/// Created by Timwi
/// </summary>
public class DividedSquaresModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable[] AllSquares;
    public Transform Field;
    public Transform Rotator;
    public Transform StatusLight;
    public Transform CapsuleContainer;
    public MeshRenderer Capsule;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    public Color[] Colors;
    public string[] ColorNames;

    public int TestingSize;
    public int TestingNumModules;

    private int _correctSquare;
    private int? _correctNumSolved; // null if any is allowed
    private int _colorA;
    private int _colorB;
    struct ColorPair
    {
        public int A { get; private set; }
        public int B { get; private set; }
        public ColorPair(int x, int y) { A = x; B = y; }
    }
    private ColorPair[] _snColorPairs;
    private MeshRenderer[] _squares;
    private int _sideLength;
    private int _numFails;
    private int _numOtherModules;
    private int _curSolved;
    private bool _arrangeRunning;
    private int? _squareDownAtSolved;
    private int? _squareDownAtTimer;
    private bool _isSolved;

    private static readonly int[] _table = @"-1,9,4,2,10,6,20,-1,13,7,19,22,21,25,-1,1,29,5,14,24,16,-1,3,18,12,27,0,23,-1,26,11,15,28,17,8,-1".Split(',').Select(num => int.Parse(num)).ToArray();
    private static readonly string[] _excludedModules = @"Divided Squares,Forget Me Not,Forget Everything,Turn The Key,The Time Keeper,Souvenir,The Swan".Split(',');

    private void Start()
    {
        CapsuleContainer.gameObject.SetActive(Application.isEditor);
        StatusLight.gameObject.SetActive(false);

        _moduleId = _moduleIdCounter++;
        _squares = AllSquares.Select(obj => obj.GetComponent<MeshRenderer>()).ToArray();
        _squareDownAtSolved = null;
        _isSolved = false;

        var snPairs = new List<ColorPair>();
        foreach (var ch in Bomb.GetSerialNumberLetters().Distinct())
        {
            var ix = Array.IndexOf(_table, ch - 'A' + 1);
            snPairs.Add(new ColorPair(ix % Colors.Length, ix / Colors.Length));
        }
        _snColorPairs = snPairs.ToArray();
        _curSolved = 0;

        for (int i = 0; i < AllSquares.Length; i++)
        {
            AllSquares[i].OnInteract = mouseDown(i % 13, i / 13, i);
            AllSquares[i].OnInteractEnded = mouseUp(i % 13, i / 13, i);
        }

        if (Application.isEditor)
        {
            _numOtherModules = 8;
            StartCoroutine(Arrange(2, _curSolved));
        }
        else
        {
            _numOtherModules = Bomb.GetSolvableModuleNames().Count(str => !_excludedModules.Contains(str));
            Debug.LogFormat(@"<Divided Squares #{0}> _numOtherModules = {1}", _moduleId, _numOtherModules);
            StartCoroutine(Arrange(1, _curSolved));
        }
    }

    private KMSelectable.OnInteractHandler mouseDown(int x, int y, int i)
    {
        return delegate
        {
            if (_isSolved)
                return false;
            if (x + _sideLength * y != _correctSquare)
            {
                Debug.LogFormat(@"[Divided Squares #{0}] You pressed {1}{2}. Wrong square.", _moduleId, (char) ('A' + x), y + 1);
                StartCoroutine(solveOrStrikeAnimation(x, y, i, solve: false));
            }
            else
            {
                _squares[i].material.color = Colors[_colorB];
                _squareDownAtSolved = Bomb.GetSolvedModuleNames().Count();
                _squareDownAtTimer = (int) Bomb.GetTime();
            }
            return false;
        };
    }

    private Action mouseUp(int x, int y, int i)
    {
        return delegate
        {
            _squares[(_correctSquare % _sideLength) + 13 * (_correctSquare / _sideLength)].material.color = Colors[_colorA];
            if (_squareDownAtSolved == null)
                return;

            var c = Bomb.GetSolvedModuleNames().Count();
            if (c != _squareDownAtSolved.Value)
            {
                // The number of solved modules has changed while the button was held. 
                // This can happen frequently on TP, so tolerate it.
                return;
            }

            if ((int) Bomb.GetTime() != _squareDownAtTimer.Value)
            {
                bool all = false;
                if (_correctNumSolved == null || c == _correctNumSolved.Value || (all = allSolved()))
                {
                    Debug.LogFormat(all ? @"[Divided Squares #{0}] Pressed when only special modules are unsolved. Correct." : @"[Divided Squares #{0}] Pressed at {1} solved. Correct.", _moduleId, c);
                    _isSolved = true;
                    StartCoroutine(solveOrStrikeAnimation(x, y, i, solve: true));
                }
                else
                {
                    Debug.LogFormat(@"[Divided Squares #{0}] Pressed at {1} solved. Incorrect number.", _moduleId, c);
                    StartCoroutine(solveOrStrikeAnimation(x, y, i, solve: false));
                }
            }

            _squareDownAtSolved = null;
            _squareDownAtTimer = null;
        };
    }

    private IEnumerator solveOrStrikeAnimation(int x, int y, int i, bool solve)
    {
        Rotator.gameObject.SetActive(true);
        var sz = 0.1625f / _sideLength;
        Rotator.localPosition = new Vector3(sz * (x - _sideLength * .5f) - .0005f, 0, -sz * (y - (_sideLength - 1) * .5f));
        _squares[i].transform.parent = Rotator;

        StatusLight.localPosition = new Vector3(sz * (x - (_sideLength - 1) * .5f), -sz * 1.1f, -sz * (y - (_sideLength - 1) * .5f));
        StatusLight.localScale = new Vector3(sz, sz, sz) * 30;
        Capsule.sharedMaterial.color = Color.grey;
        StatusLight.gameObject.SetActive(true);

        var duration = .6f;
        var elapsed = 0f;
        var maxAngle = 111f;
        var lightStarted = false;
        var halfWay = false;
        while (elapsed < duration)
        {
            var t = elapsed / duration;
            Rotator.localEulerAngles = new Vector3(0, 0, t * (1 - t) * 4 * maxAngle);
            elapsed += Time.deltaTime;
            if (!lightStarted && elapsed > duration * .35f)
            {
                lightStarted = true;
                StartCoroutine(bounceLight(i, sz, solve));
            }
            if (!halfWay && elapsed > duration * .5f)
            {
                halfWay = true;
                yield return new WaitForSeconds(solve ? .1f : .42f);
                elapsed = duration * .5f;
            }
            yield return null;
        }
        Rotator.localEulerAngles = new Vector3(0, 0, 0);
        if (!solve)
        {
            yield return new WaitForSeconds(.8f);
            StatusLight.gameObject.SetActive(false);
        }
        _squares[i].transform.parent = Field;
    }

    private IEnumerator bounceLight(int i, float sz, bool solve)
    {
        var x = StatusLight.localPosition.x;
        var z = StatusLight.localPosition.z;
        var duration = .8f;
        var elapsed = 0f;
        var maxHeight = 2 * sz;
        var processed = false;
        while (elapsed < duration)
        {
            var t = elapsed / duration;
            StatusLight.localPosition = new Vector3(x, t * (1 - t) * 4 * maxHeight - (solve ? (1 - t) * sz : sz) * 1.1f, z);
            elapsed += Time.deltaTime;
            if (!processed && elapsed > duration * (solve ? .6f : .3f))
            {
                processed = true;
                if (solve)
                {
                    Module.HandlePass();
                    Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, StatusLight);
                }
                else
                    Module.HandleStrike();
                if (Application.isEditor)
                    Capsule.sharedMaterial.color = solve ? Color.green : Color.red;
            }
            yield return null;
        }
        StatusLight.localPosition = new Vector3(x, solve ? 0 : -sz * 1.1f, z);
    }

    private bool allSolved()
    {
        var modules = Bomb.GetSolvableModuleNames().ToList();
        var solved = Bomb.GetSolvedModuleNames();
        for (int i = 0; i < solved.Count; i++)
            modules.Remove(solved[i]);
        return modules.All(m => _excludedModules.Contains(m));
    }

    private void Update()
    {
        if (_isSolved)
            return;
        var c = Bomb.GetSolvedModuleNames().Count();
        if (c > _curSolved)
        {
            _curSolved = c;
            if (_correctNumSolved != null && c > _correctNumSolved.Value)
            {
                Debug.LogFormat(@"[Divided Squares #{0}] Number of solved modules is now {1}.", _moduleId, c);
                if (_sideLength == 13)
                {
                    Debug.LogFormat(@"[Divided Squares #{0}] Can now solve at any number of solved modules.", _moduleId);
                    _correctNumSolved = null;
                }
                else
                    StartCoroutine(UpdateSolved(_sideLength + 1, c));
            }
        }
    }

    private IEnumerator UpdateSolved(int sideLength, int curSolved)
    {
        yield return new WaitUntil(() => !_arrangeRunning);
        if (_sideLength < sideLength)
        {
            Debug.LogFormat(@"[Divided Squares #{0}] Number of solved modules surpassed.", _moduleId);
            StartCoroutine(Arrange(sideLength, curSolved));
        }
    }

    private IEnumerator Arrange(int sideLength, int curSolved)
    {
        _arrangeRunning = true;
        _sideLength = sideLength;

        // Arrange the squares
        for (int x = 0; x < 13; x++)
        {
            for (int y = 0; y < 13; y++)
            {
                var obj = AllSquares[y * 13 + x];
                obj.gameObject.SetActive(false);
                if (x >= sideLength || y >= sideLength)
                    continue;
                var sz = 0.1625f / sideLength;
                obj.transform.localScale = new Vector3(sz - .0005f, sz - .0005f, sz - .0005f);
                obj.transform.localPosition = new Vector3(sz * (x - (sideLength - 1) * .5f), 0, -sz * (y - (sideLength - 1) * .5f));
            }
        }

        if (sideLength == 1)
        {
            // Decide on a target number of solved modules
            _correctNumSolved = Rnd.Range(curSolved, Math.Min(30, _numOtherModules));
            var ix = Array.IndexOf(_table, _correctNumSolved.Value);
            _colorA = ix % Colors.Length;
            _colorB = ix / Colors.Length;
            _squares[0].material.color = Colors[_colorA];
            _squares[0].gameObject.SetActive(true);
        }
        else
        {
            var availableColors = _snColorPairs.SelectMany(xy => new[] { xy.A, xy.B }).Distinct().ToArray();

            tryAgain:
            var colors = new int?[sideLength * sideLength];

            // Decide which square is the “correct square”
            _correctSquare = Rnd.Range(0, sideLength * sideLength);
            var sqX = _correctSquare % sideLength;
            var sqY = _correctSquare / sideLength;
            Debug.LogFormat(@"<Divided Squares #{0}> Side length now {1}", _moduleId, sideLength);
            Debug.LogFormat(@"<Divided Squares #{0}> Correct square is {1}{2}", _moduleId, (char) ('A' + _correctSquare % sideLength), (_correctSquare / sideLength) + 1);

            // Decide on a target number of solved modules, and which color the “correct square” is going to be
            var targetNumbers = Enumerable.Range(sideLength * sideLength - 1, 30)
                .Where(i =>
                    // Must be in range
                    i >= curSolved && i <= _numOtherModules &&
                    // Must be possible to form a SN pair with this number’s Color A
                    availableColors.Contains(Array.IndexOf(_table, i - sideLength * sideLength + 1) % Colors.Length))
                .ToList();

            if (targetNumbers.Count == 0)
            {
                // No valid number left; use a number out of range, which allows the user to solve at any point
                _correctNumSolved = null;
                _colorA = availableColors.PickRandom();
                _colorB = Enumerable.Range(0, Colors.Length).Where(i => i != _colorA).PickRandom();
                Debug.LogFormat(@"<Divided Squares #{0}> No valid number of solved modules. Color A = {1}, Color B = {2}", _moduleId, ColorNames[_colorA], ColorNames[_colorB]);
            }
            else
            {
                // Pick a valid target number of solved modules and determine the correct colors for it
                Debug.LogFormat(@"<Divided Squares #{0}> Targets to choose from: {1}", _moduleId, string.Join(", ", targetNumbers.Select(num => num.ToString()).ToArray()));
                _correctNumSolved = targetNumbers.PickRandom();
                var ix = Array.IndexOf(_table, _correctNumSolved.Value - sideLength * sideLength + 1);
                _colorA = ix % Colors.Length;
                _colorB = ix / Colors.Length;
                Debug.LogFormat(@"<Divided Squares #{0}> Chose target of {1} solved modules. Color A = {2}, Color B = {3}", _moduleId, _correctNumSolved, ColorNames[_colorA], ColorNames[_colorB]);
            }
            colors[_correctSquare] = _colorA;

            // Which adjacent squares can we use?
            var adjacents = new List<int>();
            if (sqX > 0 && _snColorPairs.Any(pair => pair.B == colors[_correctSquare]))
            {
                var adj = sqX - 1 + sideLength * sqY;
                adjacents.Add(adj);
                colors[adj] = _snColorPairs.Where(pair => pair.B == colors[_correctSquare]).PickRandom().A;
            }
            if (sqX < sideLength - 1 && _snColorPairs.Any(pair => pair.A == colors[_correctSquare]))
            {
                var adj = sqX + 1 + sideLength * sqY;
                adjacents.Add(adj);
                colors[adj] = _snColorPairs.Where(pair => pair.A == colors[_correctSquare]).PickRandom().B;
            }
            if (sqY > 0 && _snColorPairs.Any(pair => pair.B == colors[_correctSquare]))
            {
                var adj = sqX + sideLength * (sqY - 1);
                adjacents.Add(adj);
                colors[adj] = _snColorPairs.Where(pair => pair.B == colors[_correctSquare]).PickRandom().A;
            }
            if (sqY < sideLength - 1 && _snColorPairs.Any(pair => pair.A == colors[_correctSquare]))
            {
                var adj = sqX + sideLength * (sqY + 1);
                adjacents.Add(adj);
                colors[adj] = _snColorPairs.Where(pair => pair.A == colors[_correctSquare]).PickRandom().B;
            }

            // We need at least two. This might be false if the chosen square is in a corner and we can’t use the chosen color as both A or B
            if (adjacents.Count < 2)
            {
                yield return null;
                goto tryAgain;
            }

            var adj1 = adjacents[Rnd.Range(0, adjacents.Count)];
            adjacents.Remove(adj1);
            var adj2 = adjacents[Rnd.Range(0, adjacents.Count)];
            adjacents.Remove(adj2);

            // Use a recursive brute-force solver to find an arrangement of colors that satisfies all of the conditions.
            _numFails = 0;
            if (!fill(colors, 0, sideLength, adj1, adj2))
            {
                Debug.LogFormat(@"<Divided Squares #{0}> Trying again", _moduleId);
                yield return null;
                goto tryAgain;
            }

            var numbers = Enumerable.Range(0, sideLength * sideLength).ToList();
            var step = Rnd.Range(sideLength * sideLength / 4, 3 * sideLength * sideLength / 4);
            var nIx = Rnd.Range(0, numbers.Count);
            while (numbers.Count > 0)
            {
                nIx %= numbers.Count;
                var n = numbers[nIx];
                numbers.RemoveAt(nIx);
                var obj = _squares[(n % sideLength) + 13 * (n / sideLength)];
                obj.material.color = Colors[colors[n].Value];
                obj.gameObject.SetActive(true);
                nIx += step;
                yield return new WaitForSeconds(.1f / sideLength);
            }
        }

        Debug.LogFormat(@"[Divided Squares #{0}] There are {1} squares.", _moduleId, sideLength * sideLength);
        Debug.LogFormat(@"[Divided Squares #{0}] Correct square is {1}{2}.", _moduleId, (char) ('A' + (_correctSquare % sideLength)), (_correctSquare / sideLength) + 1);
        Debug.LogFormat(@"[Divided Squares #{0}] Color A is {1}, Color B is {2}.", _moduleId, ColorNames[_colorA], ColorNames[_colorB]);
        Debug.LogFormat(@"[Divided Squares #{0}] Target number of solved modules: {1}.", _moduleId, _correctNumSolved == null ? "any" : _correctNumSolved.Value.ToString());
        _arrangeRunning = false;
    }

    private bool fill(int?[] colors, int ix, int w, int adj1, int adj2)
    {
        if (ix == w * w)
            return true;
        if (ix == _correctSquare || ix == adj1 || ix == adj2)
            return fill(colors, ix + 1, w, adj1, adj2);

        var s = Rnd.Range(0, Colors.Length);
        for (int i = 0; i < Colors.Length; i++)
        {
            var cs = (s + i) % Colors.Length;
            if (/* Above */ !(ix < w || (cs != colors[ix - w] && (ix - w == _correctSquare || numPairs(colors, ix - w, ix, cs, w) < 2))) ||
                /* Left */ !(ix % w == 0 || (cs != colors[ix - 1] && (ix - 1 == _correctSquare || numPairs(colors, ix - 1, ix, cs, w) < 2))) ||
                /* Below */ !(ix + w >= w * w || ((ix + w != adj1 || cs != colors[adj1]) && (ix + w != adj2 || cs != colors[adj2]) && (ix + w != _correctSquare || cs != colors[_correctSquare]))) ||
                /* Right */ !(ix % w == w - 1 || ((ix + 1 != adj1 || cs != colors[adj1]) && (ix + 1 != adj2 || cs != colors[adj2]) && (ix + 1 != _correctSquare || cs != colors[_correctSquare]))))
                continue;
            colors[ix] = cs;
            if (fill(colors, ix + 1, w, adj1, adj2))
                return true;
            if (_numFails > 131072)
                return false;
        }
        colors[ix] = null;
        _numFails++;
        return false;
    }

    private int numPairs(int?[] colors, int ix, int placeWhere, int colorToPlace, int sideLength)
    {
        colors[placeWhere] = colorToPlace;
        var r = 0;
        if (ix % sideLength > 0 && _snColorPairs.Any(pair => pair.A == colors[ix - 1] && pair.B == colors[ix]))
            r++;
        if (ix % sideLength < sideLength - 1 && _snColorPairs.Any(pair => pair.A == colors[ix] && pair.B == colors[ix + 1]))
            r++;
        if (ix >= sideLength && _snColorPairs.Any(pair => pair.A == colors[ix - sideLength] && pair.B == colors[ix]))
            r++;
        if (ix + sideLength < sideLength * sideLength && _snColorPairs.Any(pair => pair.A == colors[ix] && pair.B == colors[ix + sideLength]))
            r++;
        return r;
    }
}
