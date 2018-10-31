using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    private int _animationRunning;
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
        _arrangeRunning = false;
        _animationRunning = 0;

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
            _numOtherModules = 0;
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
            if (_isSolved || _animationRunning > 0)
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
        _animationRunning++;
        Rotator.gameObject.SetActive(true);
        var sz = 0.1625f / _sideLength;
        Rotator.localPosition = new Vector3(sz * (x - _sideLength * .5f) - .0005f, 0, -sz * (y - (_sideLength - 1) * .5f));
        _squares[i].transform.parent = Rotator;

        StatusLight.localPosition = new Vector3(sz * (x - (_sideLength - 1) * .5f), -sz * 1.1f, -sz * (y - (_sideLength - 1) * .5f));
        Capsule.sharedMaterial.color = Color.grey;
        StatusLight.gameObject.SetActive(true);

        Audio.PlaySoundAtTransform("DoorOpen", _squares[i].transform);
        StartCoroutine(bounceLight(i, sz, solve));

        var duration = .6f;
        var elapsed = 0f;
        var maxAngle = 111f;
        var halfWay = false;
        while (elapsed < duration)
        {
            var t = elapsed / duration;
            Rotator.localEulerAngles = new Vector3(0, 0, t * (1 - t) * 4 * maxAngle);
            elapsed += Time.deltaTime;
            if (!halfWay && elapsed > duration * .5f)
            {
                halfWay = true;
                yield return new WaitForSeconds(solve ? .1f : .42f);
                elapsed = duration * .5f;
            }
            yield return null;
        }
        Rotator.localEulerAngles = new Vector3(0, 0, 0);
        Audio.PlaySoundAtTransform("DoorClose", _squares[i].transform);
        if (!solve)
        {
            duration = .8f;
            elapsed = 0f;
            while (elapsed < duration)
            {
                StatusLight.localScale = new Vector3(sz, sz, sz) * (30 - 20 * elapsed / duration);
                yield return null;
                elapsed += Time.deltaTime;
            }
            StatusLight.gameObject.SetActive(false);
        }
        _squares[i].transform.parent = Field;
        _animationRunning--;
    }

    private IEnumerator bounceLight(int i, float sz, bool solve)
    {
        _animationRunning++;
        StatusLight.gameObject.SetActive(true);
        var duration = .21f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            StatusLight.localScale = new Vector3(sz, sz, sz) * (10 + 20 * elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }

        Audio.PlaySoundAtTransform("Boing", StatusLight);
        var x = StatusLight.localPosition.x;
        var z = StatusLight.localPosition.z;
        duration = .8f;
        elapsed = 0f;
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
        _animationRunning--;
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

            Debug.LogFormat(@"<Divided Squares #{0}> Adj: {1}{2}, {3}{4}", _moduleId, (char) ('A' + adj1 % sideLength), adj1 / sideLength + 1, (char) ('A' + adj2 % sideLength), adj2 / sideLength + 1);

            // Use a recursive brute-force solver to find an arrangement of colors that satisfies all of the conditions.
            _numFails = 0;
            if (!fill(colors, 0, sideLength, adj1, adj2))
            {
                Debug.LogFormat(@"<Divided Squares #{0}> Trying again", _moduleId);
                yield return null;
                goto tryAgain;
            }

            for (int y = 0; y < sideLength; y++)
                Debug.LogFormat(@"<Divided Squares #{0}> numPairs {1} = {2}", _moduleId, y, string.Join(", ", Enumerable.Range(0, sideLength).Select(x => numPairs(colors, y * sideLength + x, y * sideLength + x, colors[y * sideLength + x].Value, sideLength).ToString()).ToArray()));

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
            var aboveFine = ix < w || (cs != colors[ix - w] && (ix - w == _correctSquare || numPairs(colors, ix - w, ix, cs, w) < 2));
            var leftFine = ix % w == 0 || (cs != colors[ix - 1] && (ix - 1 == _correctSquare || numPairs(colors, ix - 1, ix, cs, w) < 2));
            var belowFine = ix + w >= w * w || (colors[ix + w] != cs && (colors[ix + w] == null || ix + w == _correctSquare || numPairs(colors, ix + w, ix, cs, w) < 2));
            var rightFine = ix % w == w - 1 || (colors[ix + 1] != cs && (colors[ix + 1] == null || ix + 1 == _correctSquare || numPairs(colors, ix + 1, ix, cs, w) < 2));
            if (!aboveFine || !leftFine || !belowFine || !rightFine)
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

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!examine a1 [Briefly examine square A1] | !press a1 [Press square A1 across a timer tick]";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        var m = Regex.Match(command, @"\A\s*(?:(?<examine>examine)|(?<press>press))\s+(?<sq>[A-Z]\d+)\s*\z", RegexOptions.IgnoreCase);
        if (!m.Success)
            yield break;

        var square = m.Groups["sq"].Value;
        var x = char.ToUpperInvariant(square[0]) - 'A';
        int y;
        if (!int.TryParse(square.Substring(1), out y))
            yield break;
        y--;

        if (x >= _sideLength || y < 0 || y >= _sideLength)
        {
            yield return "sendtochat @{0} Dude, it’s not THAT big yet.";
            yield break;
        }

        yield return null;

        if (m.Groups["examine"].Success)
        {
            tryAgain:
            var time = (int) Bomb.GetTime();
            var solved = Bomb.GetSolvedModuleNames().Count();
            yield return new WaitUntil(() => (int) Bomb.GetTime() != time);
            if (Bomb.GetSolvedModuleNames().Count() != solved)
                goto tryAgain;
            yield return AllSquares[x + 13 * y];
            yield return new WaitForSeconds(.4f);
            yield return AllSquares[x + 13 * y];
        }
        else if (m.Groups["press"].Success)
        {
            yield return AllSquares[x + 13 * y];
            var time = (int) Bomb.GetTime();
            var solved = Bomb.GetSolvedModuleNames().Count();
            yield return new WaitUntil(() => (int) Bomb.GetTime() != time);
            yield return AllSquares[x + 13 * y];
            if (Bomb.GetSolvedModuleNames().Count() != solved)
                yield return "sendtochat @{0} Shucks mate — some module got solved at the same time, so... try again?";
        }

        // To ensure the strikes and solves are correctly attributed, just keep the coroutine active while the animation is still running
        while (_animationRunning > 0)
            yield return null;
    }
}
