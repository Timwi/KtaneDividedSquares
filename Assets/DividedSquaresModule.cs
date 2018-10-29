using System;
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

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    //[MenuItem("Divided Squares/Do")]
    //static void CreateGrid()
    //{
    //    var module = FindObjectOfType<DividedSquaresModule>();

    //    var children = new List<KMSelectable>();
    //    for (int y = 0; y < 13; y++)
    //        for (int x = 0; x < 13; x++)
    //        {
    //            var obj = x == 0 && y == 0 ? module.Square : Instantiate(module.Square);
    //            obj.name = string.Format("Square {0}{1}", (char) ('A' + x), 1 + y);
    //            obj.transform.parent = module.Field.transform;
    //            obj.transform.localPosition = new Vector3(.0125f * (x - 6), 0, -.0125f * (y - 6));
    //            obj.transform.localEulerAngles = new Vector3(90, 0, 0);
    //            obj.transform.localScale = new Vector3(.012f, .012f, .012f);
    //            children.Add(obj.GetComponent<KMSelectable>());
    //        }

    //    module.GetComponent<KMSelectable>().Children = children.ToArray();
    //    module.GetComponent<KMSelectable>().ChildRowLength = 13;
    //      module.AllSsquares = children.ToArray();
    //}

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

    private static readonly int[] _table = @"-1,9,4,2,10,6,20,-1,13,7,19,22,21,25,-1,1,29,5,14,24,16,-1,3,18,12,27,0,23,-1,26,11,15,28,17,8,-1".Split(',').Select(num => int.Parse(num)).ToArray();

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        _squares = AllSquares.Select(obj => obj.GetComponent<MeshRenderer>()).ToArray();

        var snPairs = new List<ColorPair>();
        foreach (var ch in Bomb.GetSerialNumberLetters().Distinct())
        {
            var ix = Array.IndexOf(_table, ch - 'A' + 1);
            snPairs.Add(new ColorPair(ix % Colors.Length, ix / Colors.Length));
        }
        _snColorPairs = snPairs.ToArray();

        if (Application.isEditor)
        {
            Arrange(3, 10, 50);
        }
        else
        {
            // numModules = Bomb.GetSolvableModuleNames().Count(str => !str.Equals(Module.ModuleDisplayName))
            //Arrange(1);
        }
    }

    private void Arrange(int sideLength, int curSolved, int numOtherModules)
    {
        // Arrange the squares
        for (int x = 0; x < 13; x++)
        {
            for (int y = 0; y < 13; y++)
            {
                var obj = AllSquares[y * 13 + x];
                if (x >= sideLength || y >= sideLength)
                {
                    obj.gameObject.SetActive(false);
                    continue;
                }
                obj.gameObject.SetActive(true);
                var sz = 0.1625f / sideLength;
                obj.transform.localScale = new Vector3(sz - .0005f, sz - .0005f, sz - .0005f);
                obj.transform.localPosition = new Vector3(sz * (x - (sideLength - 1) * .5f), 0, -sz * (y - (sideLength - 1) * .5f));
            }
        }

        if (sideLength == 1)
        {
            // Decide on a target number of solved modules
            var target = Rnd.Range(curSolved, Math.Min(31, numOtherModules));
            var ix = Array.IndexOf(_table, target);
            _colorA = ix % Colors.Length;
            _colorB = ix / Colors.Length;
            _squares[0].material.color = Colors[_colorA];
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
                    i >= curSolved && i <= numOtherModules &&
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
                goto tryAgain;

            var adj1 = adjacents[Rnd.Range(0, adjacents.Count)];
            adjacents.Remove(adj1);
            var adj2 = adjacents[Rnd.Range(0, adjacents.Count)];
            adjacents.Remove(adj2);

            // Use a recursive brute-force solver to find an arrangement of colors that satisfies all of the conditions.
            if (!fill(colors, 0, sideLength, adj1, adj2))
                goto tryAgain;

            for (int x = 0; x < sideLength; x++)
                for (int y = 0; y < sideLength; y++)
                    _squares[x + 13 * y].material.color = Colors[colors[x + sideLength * y].Value];
        }

        Debug.LogFormat(@"[Divided Squares #{0}] Side length now {1}, correct square is {2}{3}, Color A = {4}, Color B = {5}", _moduleId, sideLength, (char) ('A' + (_correctSquare % sideLength)), (_correctSquare / sideLength) + 1, ColorNames[_colorA], ColorNames[_colorB]);
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
        }
        colors[ix] = null;
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
