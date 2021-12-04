using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KModkit;
using UnityEngine;
using Random = UnityEngine.Random;

public class FifteenScript : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable[] Buttons;
    public GameObject EmptyGO;
    public Material TintedBackground;
    public Material UntintedBackground;
    public MeshRenderer ModuleBackground;

    struct TileInfo
    {
        public KMSelectable Button;
        public bool Empty;
    }

    struct PlacementInfo
    {
        public int Value;
        public string Explanation;
    }

    struct MoveInfo
    {
        public KMSelectable Button;
        public int From;
        public int To;
        public int NewMoveNumber;
        public bool IsReset;
        public bool IsShuffle;
    }

    static int moduleIdCounter = 1;
    int moduleId, movesLeft;
    bool moduleSolved = false;
    bool animationFinished = false;
    readonly TileInfo[] tiles = new TileInfo[16];
    readonly TileInfo[] solution = new TileInfo[16];
    readonly List<MoveInfo> playerMoves = new List<MoveInfo>();
    readonly List<MoveInfo> solutionMoves = new List<MoveInfo>();
    readonly Queue<MoveInfo> animationQueue = new Queue<MoveInfo>();
    List<PlacementInfo> placements = new List<PlacementInfo>();

    float getX(int index) { return -0.059f + .03f * (index % 4); }
    float getZ(int index) { return 0.0295f - .03f * (index / 4); }

    private FifteenSettings modSettings = new FifteenSettings();

    class FifteenSettings
    {
        public int minMoves = 8;
        public int maxMoves = 10;
    }

    static Dictionary<string, object>[] TweaksEditorSettings = new Dictionary<string, object>[]
    {
        new Dictionary<string, object>
        {
            { "Filename", "Fifteen-settings.json" },
            { "Name", "Fifteen Settings" },
            { "Listing", new List<Dictionary<string, object>>{
                new Dictionary<string, object>
                {
                    { "Key", "minMoves" },
                    { "Text", "Set the minimum number of moves (min 8)." }
                },
                new Dictionary<string, object>
                {
                    { "Key", "maxMoves" },
                    { "Text", "Set the maximum number of moves (max 99)." }
                },
            } }
        }
    };

    void Awake()
    {
        ModConfig<FifteenSettings> modConfig = new ModConfig<FifteenSettings>("Fifteen-settings");
        modSettings = modConfig.Settings;
        Debug.LogFormat("{0}, {1}", modSettings.minMoves, modSettings.maxMoves);
        var min = modSettings.minMoves;
        var max = modSettings.maxMoves;
        if (min < 8 || min > 98)
            min = 8;
        if (max > 99)
            max = 99;
        if (min > max)
        {
            max = min;
            if (max == 99)
                min--;
        }
        if (min == max)
        {
            if (max == 99)
                min--;
            else
                max++;
        }

        modSettings.minMoves = min;
        modSettings.maxMoves = max;
        Debug.LogFormat("{0}, {1}", modSettings.minMoves, modSettings.maxMoves);

        modConfig.Settings = modSettings;

        ModuleBackground.sharedMaterial = min != 8 || max != 10 ? TintedBackground : UntintedBackground;
    }


    void Start()
    {
        moduleId = moduleIdCounter++;

        for (var i = 0; i < Buttons.Length; i++)
        {
            Buttons[i].OnInteract += ButtonPressed(i);
        }
        StartCoroutine(AnimationQueue());
        var sN = Bomb.GetSerialNumber();
        placements.Add(new PlacementInfo { Value = sN[0] >= 'A' ? ((sN[0] - 'A' + 10) % 16 == 0 ? 16 : (sN[0] - 'A' + 10) % 16) : int.Parse(sN[0].ToString()) == 0 ? 16 : int.Parse(sN[0].ToString()), Explanation = "First serial number character in base 36 % 16" });
        placements.Add(new PlacementInfo { Value = sN[1] >= 'A' ? ((sN[1] - 'A' + 10) % 15 == 0 ? 15 : (sN[1] - 'A' + 10) % 15) : int.Parse(sN[1].ToString()) == 0 ? 15 : int.Parse(sN[1].ToString()), Explanation = "Second serial number character in base 36 % 15" });
        placements.Add(new PlacementInfo { Value = (sN[3] - 'A' + 1) % 14 == 0 ? 14 : (sN[3] - 'A' + 1) % 14, Explanation = "Fourth serial number character in A1Z26 % 14" });
        placements.Add(new PlacementInfo { Value = (sN[4] - 'A' + 1) % 13 == 0 ? 13 : (sN[4] - 'A' + 1) % 13, Explanation = "Fifth serial number character in A1Z26 % 13" });
        placements.Add(new PlacementInfo { Value = DateTime.Now.Month, Explanation = "Starting month of the bomb" });
        placements.Add(new PlacementInfo { Value = DateTime.Now.Day % 11 == 0 ? 11 : DateTime.Now.Day % 11, Explanation = "Starting day of the bomb (in month) % 11" });
        placements.Add(new PlacementInfo { Value = int.Parse(sN[2].ToString()) == 0 ? 10 : int.Parse(sN[2].ToString()), Explanation = "Third serial number character" });
        placements.Add(new PlacementInfo { Value = DateTime.Now.Hour % 9 == 0 ? 9 : DateTime.Now.Hour == 0 ? 1 : DateTime.Now.Hour % 9, Explanation = "Starting time of the bomb (24-hours) % 9" });
        placements.Add(new PlacementInfo { Value = int.Parse(sN[5].ToString()) % 8 == 0 ? 8 : int.Parse(sN[5].ToString()) % 8, Explanation = "Sixth serial number character % 8" });
        placements.Add(new PlacementInfo { Value = Bomb.GetIndicators().Count() % 7 == 0 ? 7 : Bomb.GetIndicators().Count() % 7, Explanation = "Amount of indicators % 7" });
        placements.Add(new PlacementInfo { Value = Bomb.GetBatteryCount() % 6 == 0 ? 6 : Bomb.GetBatteryCount() % 6, Explanation = "Amount of batteries % 6" });
        placements.Add(new PlacementInfo { Value = Bomb.GetPortCount() % 5 == 0 ? 5 : Bomb.GetPortCount() % 5, Explanation = "Amount of ports % 5" });
        placements.Add(new PlacementInfo { Value = Bomb.GetPortPlateCount() % 4 == 0 ? 4 : Bomb.GetPortPlateCount() % 4, Explanation = "Amount of port plates % 4" });
        placements.Add(new PlacementInfo { Value = ((int) Bomb.GetTime()) / 60 % 3 == 0 ? 3 : ((int) Bomb.GetTime()) / 60 % 3, Explanation = "Starting bomb timer in minutes % 3" });
        placements.Add(new PlacementInfo { Value = Bomb.GetOnIndicators().Count() % 2 == 0 ? 2 : Bomb.GetOnIndicators().Count() % 2, Explanation = "Amount of lit indicators % 2" });
        Debug.LogFormat(@"[Fifteen #{0}] Indexes for placing the tiles: {1}{2}", moduleId, Environment.NewLine, placements.Select(placement => string.Format("{0}: {1}", placement.Explanation, placement.Value)).Join("\r\n"));
        var initialOrder = Enumerable.Range(1, 16).ToList();
        for (var i = 0; i < 16; i++)
        {
            if (i == 15)
            {
                if (initialOrder[0] == 16)
                    tiles[i].Empty = true;
                else
                {
                    tiles[i].Button = Buttons[initialOrder[0] - 1];
                    tiles[i].Empty = false;
                }
                initialOrder.RemoveAt(0);
            }
            else
            {
                if (initialOrder[placements[i].Value - 1] == 16)
                    tiles[i].Empty = true;
                else
                {
                    tiles[i].Button = Buttons[initialOrder[placements[i].Value - 1] - 1];
                    tiles[i].Empty = false;
                }
                initialOrder.RemoveAt(placements[i].Value - 1);
            }
        }
        Debug.LogFormat(@"[Fifteen #{0}] Goal Board: {1}", moduleId, tiles.Select(tile => tile.Empty ? "Empty" : tile.Button.GetComponentInChildren<TextMesh>().text).Join(", "));
        for (var i = 0; i < tiles.Length; i++)
        {
            if (tiles[i].Empty)
                EmptyGO.transform.localPosition = new Vector3(getX(i), EmptyGO.transform.localPosition.y, getZ(i));
            else
                tiles[i].Button.transform.localPosition = new Vector3(getX(i), tiles[i].Button.transform.localPosition.y, getZ(i));
        }

        tiles.CopyTo(solution, 0);

        movesLeft = Random.Range(modSettings.minMoves, modSettings.maxMoves + 1);
        EmptyGO.GetComponentInChildren<TextMesh>().text = movesLeft.ToString();

        for (var i = 0; i < movesLeft; i++)
        {
            var emptyIx = tiles.IndexOf(tile => tile.Empty);
            var buttonIxs = Enumerable.Range(0, tiles.Length).Where(ix => IsNeighbor(ix, emptyIx) && (i == 0 || ix != solutionMoves[i - 1].To)).ToList().Shuffle();
            var moveInfo = new MoveInfo { From = buttonIxs[0], To = emptyIx, Button = tiles[buttonIxs[0]].Button, IsReset = false, IsShuffle = true, NewMoveNumber = movesLeft };
            solutionMoves.Add(moveInfo);
            animationQueue.Enqueue(moveInfo);
            var t = tiles[buttonIxs[0]];
            tiles[buttonIxs[0]] = tiles[emptyIx];
            tiles[emptyIx] = t;
        }
        Debug.LogFormat(@"[Fifteen #{0}] Steps to goal: {1}", moduleId, solutionMoves.AsEnumerable().Reverse().Select(inf => Array.IndexOf(Buttons, inf.Button) + 1).Join(", "));
    }

    KMSelectable.OnInteractHandler ButtonPressed(int btn)
    {
        return delegate
        {
            if (moduleSolved)
                return false;
            var emptyIx = tiles.IndexOf(tile => tile.Empty);
            var buttonIx = tiles.IndexOf(tile => tile.Button == Buttons[btn]);
            if (IsNeighbor(buttonIx, emptyIx))
            {
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Buttons[btn].transform);
                movesLeft--;
                var moveInfo = new MoveInfo { From = buttonIx, To = emptyIx, Button = tiles[buttonIx].Button, IsReset = false, IsShuffle = false, NewMoveNumber = movesLeft };
                playerMoves.Add(moveInfo);
                animationQueue.Enqueue(moveInfo);
                var t = tiles[buttonIx];
                tiles[buttonIx] = tiles[emptyIx];
                tiles[emptyIx] = t;
            }
            else
            {
                Debug.LogFormat(@"[Fifteen #{0}] Strike! Attempted to move tile {1}, which is a currently unmoveable tile!", moduleId, btn + 1);
                Module.HandleStrike();
                return false;
            }

            if (tiles.SequenceEqual(solution))
            {
                Debug.LogFormat(@"[Fifteen #{0}] Solved! You successfully disarmed the module!", moduleId);
                moduleSolved = true;
            }
            else if (movesLeft == 0)
            {
                Debug.LogFormat(@"[Fifteen #{0}] Strike! No more moves left and the goal is not reached yet!", moduleId);
                Module.HandleStrike();
                Reset();
            }
            return false;
        };
    }

    bool IsNeighbor(int ix1, int ix2)
    {
        return
            (Math.Abs(ix1 % 4 - ix2 % 4) == 1 && ix1 / 4 == ix2 / 4) ||
            (Math.Abs(ix1 / 4 - ix2 / 4) == 1 && ix1 % 4 == ix2 % 4);
    }

    void Reset()
    {
        for (var i = playerMoves.Count - 1; i >= 0; i--)
        {
            movesLeft++;
            animationQueue.Enqueue(new MoveInfo { From = playerMoves[i].To, To = playerMoves[i].From, IsReset = true, IsShuffle = false, Button = tiles[playerMoves[i].To].Button, NewMoveNumber = movesLeft });
            var t = tiles[playerMoves[i].To];
            tiles[playerMoves[i].To] = tiles[playerMoves[i].From];
            tiles[playerMoves[i].From] = t;
        }
        playerMoves.Clear();
    }

    IEnumerator AnimationQueue()
    {
        while (!moduleSolved || animationQueue.Count > 0)
        {
            while (animationQueue.Count == 0)
                yield return null;

            var item = animationQueue.Dequeue();
            var duration = item.IsReset ? .1f : item.IsShuffle ? .00005f : .2f;
            var elapsed = 0f;
            var buttonPos = new Vector3(getX(item.From), 0.01211f, getZ(item.From));
            var emptyPos = new Vector3(getX(item.To), 0.01211f, getZ(item.To));

            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                item.Button.transform.localPosition = Vector3.Lerp(buttonPos, emptyPos, elapsed / duration);
                EmptyGO.transform.localPosition = Vector3.Lerp(emptyPos, buttonPos, elapsed / duration);
                if (elapsed > duration / 2)
                    EmptyGO.GetComponentInChildren<TextMesh>().text = item.NewMoveNumber.ToString();
            }
        }
        Module.HandlePass();
        animationFinished = true;
    }

#pragma warning disable 0414
    readonly string TwitchHelpMessage = "!{0} 15 12 8 [press these tiles in order]";
#pragma warning restore 0414

    IEnumerator ProcessTwitchCommand(string command)
    {
        Match m;
        if (moduleSolved)
        {
            yield return "sendtochaterror The module is already solved";
            yield break;
        }
        else if ((m = Regex.Match(command, @"^\s*((?:(?:[1-9]|1[0-5]) )*([1-9]|1[0-5]))\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            yield return null;
            yield return m.Groups[1].Value.Split(' ').Select(v => Buttons[int.Parse(v) - 1]);
            yield break;

        }
        else
        {
            yield return "sendtochaterror Invalid Command";
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        Debug.LogFormat(@"[Fifteen #{0}] Module was force-solved by TP.", moduleId);
        for (var i = solutionMoves.Count - 1; i >= 0; i--)
        {
            Buttons[Buttons.IndexOf(btn => btn.Equals(solutionMoves[i].Button))].OnInteract();
            yield return new WaitForSeconds(.1f);
        }
        while (!animationFinished)
            yield return true;
    }
}
