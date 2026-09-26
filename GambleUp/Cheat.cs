using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GambleUp
{
    /// <summary>
    /// Spawns one or more casino Slots at fixed world positions and rewires each machine's interact
    /// button to a flat +balance reward instead of the original game logic.
    ///
    /// Class chain (verified from the decompiled Assembly-CSharp):
    ///   Slots : GameBase : NetworkBehaviour                       <- the slot machine
    ///   GameStamp.gamePrefab (private) -> Object.Instantiate      <- the game's own spawn path
    ///   GameBase.TryStartGame(PlayerInteract)                     <- what the button normally runs
    ///   MoneyManager.TryChangeBalance(BigNumber, PlayerProfile, ChangeType)
    ///
    /// Why the prefab and not a scene clone: NetworkIdentity.hasSpawned is a private field, so
    /// Object.Instantiate on a live scene object copies hasSpawned = true and NetworkIdentity.Awake()
    /// immediately destroys the copy. A prefab asset always has hasSpawned = false.
    /// </summary>
    public static class Cheat
    {
        public const string ObjectName = "GambleUp_CheatSlot";
        public const string ConfigName = "GambleUp_cheat.txt";

        public static bool Enabled = true;
        public static double Reward = 5.0;

        /// <summary>Fixed world positions from the config. Empty means "in front of the player".</summary>
        public static readonly List<Vector3> Positions = new List<Vector3>();

        public static readonly List<GameBase> SpawnedSlots = new List<GameBase>();
        public static int GrantCount;

        private static bool _attempted;
        private static int _failCount;
        private static float _nextAttempt;
        private static Slots _lastTemplate;
        private static string _announcedScene = "\u0000";
        private static GameObject _prefab;

        // ------------------------------------------------------------ config

        public static string ConfigPath
        {
            get
            {
                string dir;
                try { dir = Application.persistentDataPath; }
                catch { dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Low", "GambleUp"); }
                return Path.Combine(dir, ConfigName);
            }
        }

        /// <summary>
        /// Optional config file, re-read on every spawn attempt so edits apply without a rebuild:
        ///   enabled = true
        ///   reward  = 5
        ///   pos     = -6, 0, 60       (repeat the line, or separate with ';' - one machine each)
        /// With no pos lines at all, a single machine is dropped 2.5m in front of the player.
        /// </summary>
        public static void LoadConfig()
        {
            Enabled = true;
            Reward = 5.0;
            Positions.Clear();
            try
            {
                string path = ConfigPath;
                if (!File.Exists(path)) return;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();

                    if (key == "enabled") Enabled = ParseBool(val, Enabled);
                    else if (key == "reward") Reward = ParseDouble(val, Reward);
                    else if (key == "pos")
                        foreach (string one in val.Split(';'))
                            if (TryParseVec(one, out Vector3 v)) Positions.Add(v);
                }
            }
            catch (Exception e) { Probe.W("CHEAT", "config failed: " + e.Message, false); }
        }

        private static bool TryParseVec(string s, out Vector3 v)
        {
            v = Vector3.zero;
            string[] p = s.Split(',');
            if (p.Length != 3) return false;
            if (!double.TryParse(p[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double x)) return false;
            if (!double.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double y)) return false;
            if (!double.TryParse(p[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double z)) return false;
            v = new Vector3((float)x, (float)y, (float)z);
            return true;
        }

        private static bool ParseBool(string s, bool fallback) =>
            s.Equals("1", StringComparison.Ordinal) || s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("yes", StringComparison.OrdinalIgnoreCase) ? true :
            s.Equals("0", StringComparison.Ordinal) || s.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("no", StringComparison.OrdinalIgnoreCase) ? false : fallback;

        private static double ParseDouble(string s, double fallback) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : fallback;

        // ------------------------------------------------------------ spawn

        /// <summary>Safe to call every frame; it rate-limits itself and only runs on the host.</summary>
        public static void Tick()
        {
            AnnounceSceneEntry();

            if (!Enabled) return;
            if (SpawnedSlots.Count > 0 || _attempted) return;
            if (Time.realtimeSinceStartup < _nextAttempt) return;
            _nextAttempt = Time.realtimeSinceStartup + 1f;

            if (!NetworkServer.active) return;
            if (!IsCasinoScene) return;

            var templates = UnityEngine.Object.FindObjectsByType<Slots>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (templates == null || templates.Length == 0) return;

            LoadConfig();
            _prefab = FindSlotsPrefab();
            PurgeLeakedClones();

            var targets = BuildTargets();
            Probe.Banner("CHEAT SLOTS: SPAWNING NOW",
                "machine(s) to create = " + targets.Count,
                "reward per press    = +" + Reward,
                "look for objects named '" + ObjectName + "'.");

            var report = new List<string>();
            int ok = 0;
            foreach (var t in targets)
            {
                Vector3 at = t;
                if (SpawnOne(at, out string err)) ok++;
                else report.Add("FAILED at " + Probe.Vec(at) + ": " + err);
            }

            if (ok < targets.Count)
            {
                _failCount++;
                Probe.Banner("!!! CHEAT SLOT SPAWN #" + _failCount + " INCOMPLETE !!!",
                    new[] { "created " + ok + " of " + targets.Count }.Concat(report).ToArray());
                foreach (string r in report) Probe.W("CHEAT", r, false);
                if (++_partialRetries >= 4) { _attempted = true; return; }
                RollbackAll();
                return;
            }

            _attempted = true;
            AnnounceAllSpawned();
        }

        private static int _partialRetries;

        private static bool IsCasinoScene =>
            string.Equals(SceneManager.GetActiveScene().name, "CasinoScene", StringComparison.Ordinal);

        private static void AnnounceSceneEntry()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene == _announcedScene) return;
            _announcedScene = scene;
            _attempted = false;
            _failCount = 0;
            _partialRetries = 0;
            _prefab = null;
            SpawnedSlots.Clear();
            // Probe.AnnounceScene (OnSceneWasLoaded) already prints the scene banner.
        }

        private static List<Vector3> BuildTargets()
        {
            var list = new List<Vector3>();
            if (Positions.Count > 0) { list.AddRange(Positions); return list; }

            var lp = NetworkClient.localPlayer;
            if (lp == null) { list.Add(new Vector3(0f, 1f, 0f)); return list; }
            Vector3 f = PlayerForward();
            list.Add(lp.transform.position + f * 2.5f);
            return list;
        }

        private static Vector3 PlayerForward()
        {
            var lp = NetworkClient.localPlayer;
            if (lp == null) return Vector3.forward;
            Vector3 f = lp.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.0001f) return Vector3.forward;
            return f.normalized;
        }

        // ------------------------------------------------------------ one machine

        private static bool SpawnOne(Vector3 position, out string error)
        {
            error = null;
            GameObject go = null;
            try
            {
                Vector3 scale = Vector3.one;
                if (_prefab != null)
                {
                    go = UnityEngine.Object.Instantiate(_prefab, position, RotationFor());
                }
                else
                {
                    // Fallback: clone a live machine while it is deactivated, so Awake() does not run
                    // until we have cleared hasSpawned on the copy.
                    var templates = UnityEngine.Object.FindObjectsByType<Slots>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                    Slots template = templates[0];
                    foreach (var t in templates)
                        if (t != null && t.GetComponentInChildren<InteractableEventTrigger>(true) != null)
                        { template = t; break; }
                    _lastTemplate = template;
                    scale = template.transform.lossyScale;

                    bool wasActive = template.gameObject.activeSelf;
                    template.gameObject.SetActive(false);
                    go = UnityEngine.Object.Instantiate(template.gameObject, position, RotationFor());
                    template.gameObject.SetActive(wasActive);

                    foreach (var ni in go.GetComponentsInChildren<NetworkIdentity>(true))
                        ClearHasSpawned(ni);
                    go.SetActive(true);
                }

                go.name = ObjectName + "_" + SpawnedSlots.Count;
                go.transform.localScale = scale;

                var niRoot = go.GetComponent<NetworkIdentity>();
                if (niRoot == null) { error = "no NetworkIdentity on the root"; return false; }

                NetworkServer.Spawn(go);
                if (niRoot.netId == 0)
                {
                    error = "NetworkServer.Spawn left netId at 0 (rejected or destroyed by Mirror)";
                    UnityEngine.Object.Destroy(go);
                    return false;
                }

                var game = go.GetComponent<GameBase>() ?? go.GetComponentInChildren<GameBase>(true);
                if (game == null) { error = "no GameBase (Slots) in the hierarchy"; return false; }
                try
                {
                    int floor = FindFloorIndex();
                    if (floor >= 0) game.NetworkcasinoLevel = floor;
                }
                catch { }

                var trig = go.GetComponentInChildren<InteractableEventTrigger>(true);
                if (trig == null) { error = "no InteractableEventTrigger in the hierarchy"; return false; }

                // The original TryStartGame binding lives in the serialized (persistent) list, and
                // RemoveAllPersistentListeners is unavailable on UnityEvent<T> at runtime, so replace
                // the whole field with a fresh event instead.
                int persistent = trig.serverOnInteractEvent == null
                    ? 0
                    : trig.serverOnInteractEvent.GetPersistentEventCount();
                var fresh = new UnityEngine.Events.UnityEvent<PlayerInteract>();
                GameBase captured = game;
                fresh.AddListener(pi => OnCheatInteract(captured, pi));
                trig.serverOnInteractEvent = fresh;

                SpawnedSlots.Add(game);
                Probe.W("CHEAT", "spawned '" + go.name + "' netId=" + niRoot.netId +
                    " pos=" + Probe.Vec(position) + " scale=" + scale +
                    " trigger='" + trig.gameObject.name + "' replaced " + persistent + " listener(s)", false);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                if (go != null) { try { UnityEngine.Object.Destroy(go); } catch { } }
                return false;
            }
        }

        /// <summary>Yaw follows the player's heading, flipped 180 degrees.</summary>
        private static Quaternion RotationFor() =>
            Quaternion.LookRotation(-PlayerForward(), Vector3.up);

        /// <summary>Walks every GameStamp in the scene and returns the first prefab asset holding a Slots.</summary>
        private static GameObject FindSlotsPrefab()
        {
            try
            {
                var stamps = UnityEngine.Object.FindObjectsByType<GameStamp>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                var fld = typeof(GameStamp).GetField("gamePrefab",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (fld == null) return null;

                int n = 0;
                foreach (var s in stamps)
                {
                    if (s == null) continue;
                    var p = fld.GetValue(s) as GameObject;
                    n++;
                    if (p != null && p.GetComponentInChildren<Slots>(true) != null) return p;
                }
                Probe.W("CHEAT", "no Slots prefab among " + n + " GameStamp(s); using inactive-clone fallback", false);
            }
            catch (Exception e) { Probe.W("CHEAT", "FindSlotsPrefab failed: " + e.Message, false); }
            return null;
        }

        private static int FindFloorIndex()
        {
            var floors = UnityEngine.Object.FindObjectsByType<CasinoFloor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return floors == null || floors.Length == 0 ? -1 : floors[0].floorIndex;
        }

        /// <summary>
        /// NetworkIdentity.ResetState() is internal and clears hasSpawned / netId / isServer, which is
        /// exactly what a pre-Awake copy of a scene object needs. Only ever called on inactive objects.
        /// </summary>
        private static void ClearHasSpawned(NetworkIdentity ni)
        {
            if (ni == null) return;
            try
            {
                var m = typeof(NetworkIdentity).GetMethod("ResetState",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (m != null) { m.Invoke(ni, null); return; }

                typeof(NetworkIdentity).GetField("hasSpawned", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(ni, false);
                typeof(NetworkIdentity).GetProperty("netId")?.GetSetMethod(true)
                    ?.Invoke(ni, new object[] { 0u });
            }
            catch (Exception e)
            {
                Probe.W("CHEAT", "ClearHasSpawned failed on " + ni.name + ": " + e.Message, false);
            }
        }

        private static void RollbackAll()
        {
            foreach (var g in SpawnedSlots)
            {
                if (g == null) continue;
                try { NetworkServer.Destroy(g.gameObject); } catch { }
            }
            SpawnedSlots.Clear();
        }

        /// <summary>Removes any leftover clones from earlier failed attempts.</summary>
        private static void PurgeLeakedClones()
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int n = 0;
            foreach (var t in all)
            {
                if (t == null || !t.name.StartsWith(ObjectName, StringComparison.Ordinal)) continue;
                n++;
                try { NetworkServer.Destroy(t.gameObject); }
                catch { try { UnityEngine.Object.Destroy(t.gameObject); } catch { } }
            }
            if (n > 0) Probe.W("CHEAT", "purged " + n + " leaked clone(s)", false);
        }

        // ------------------------------------------------------------ announce

        private static void AnnounceAllSpawned()
        {
            var lp = NetworkClient.localPlayer;
            Vector3 ppos = lp != null ? lp.transform.position : Vector3.zero;
            bool havePlayer = lp != null;
            Vector3 fwd = PlayerForward();

            var lines = new List<string>
            {
                "machines created = " + SpawnedSlots.Count,
                "reward per press = +" + Reward + " balance (ChangeType.GameResult), no bet, no spin",
                ""
            };

            for (int i = 0; i < SpawnedSlots.Count; i++)
            {
                var g = SpawnedSlots[i];
                if (g == null) { lines.Add("  #" + i + " <destroyed>"); continue; }
                Vector3 p = g.transform.position;
                var ni = g.GetComponent<NetworkIdentity>();
                string dist = "-", bearing = "-";
                if (havePlayer)
                {
                    Vector3 d = p - ppos;
                    dist = d.magnitude.ToString("0.0") + "m";
                    Vector3 flat = new Vector3(d.x, 0f, d.z);
                    if (flat.sqrMagnitude > 0.0001f)
                    {
                        float ang = Vector3.SignedAngle(fwd, flat.normalized, Vector3.up);
                        bearing = ang.ToString("0") + "deg " + (Mathf.Abs(ang) < 45 ? "ahead" :
                                 Mathf.Abs(ang) < 135 ? (ang > 0 ? "right" : "left") : "behind");
                    }
                }
                lines.Add("  #" + i + " '" + g.name + "'  netId=" + (ni == null ? "?" : ni.netId.ToString()) +
                          "  pos=(" + p.x.ToString("0.0") + ", " + p.y.ToString("0.0") + ", " + p.z.ToString("0.0") + ")" +
                          "  " + dist + " " + bearing);
            }

            lines.Add("");
            lines.Add("presses so far = " + GrantCount);
            Probe.Banner("*** " + SpawnedSlots.Count + " CHEAT SLOT(S) READY ***", lines.ToArray());
        }

        // ------------------------------------------------------------ reward

        private static void OnCheatInteract(GameBase game, PlayerInteract playerInteract)
        {
            try
            {
                if (!NetworkServer.active) { Probe.W("CHEAT", "interact ignored: not the server", true); return; }
                if (!SpawnedSlots.Contains(game)) { Probe.W("CHEAT", "interact on a foreign game, ignored", true); return; }

                if (playerInteract == null || !playerInteract.TryGetComponent(out PlayerProfile profile))
                {
                    Probe.W("CHEAT", "interact had no PlayerProfile", true);
                    return;
                }

                var money = Extensions.NetworkSingleton<MoneyManager>.Instance;
                if (money == null) { Probe.W("CHEAT", "MoneyManager is null", true); return; }

                BigNumber amount = BigNumber.FromDouble(Reward);
                bool applied = money.TryChangeBalance(amount, profile, ChangeType.GameResult);
                GrantCount++;

                Probe.Banner("*** CHEAT SLOT PRESSED #" + GrantCount + " ***",
                    "machine      = '" + game.name + "' at " + Probe.Vec(game.transform.position),
                    "original Slots logic SKIPPED (no bet, no spin, no payout routine)",
                    "reward       = +" + Probe.Fmt(amount) + "   ChangeType.GameResult",
                    "player       = " + profile.playerName + " (steam " + profile.steamId + ")",
                    "applied      = " + applied,
                    "balance now  = " + Probe.Fmt(money.balance) + "   tickets = " + money.ticketBalance);
            }
            catch (Exception e)
            {
                Probe.W("CHEAT-ERR", "grant failed: " + e, true);
            }
        }

        // ------------------------------------------------------------ cleanup

        public static void Despawn()
        {
            if (SpawnedSlots.Count == 0) return;
            int n = SpawnedSlots.Count;
            foreach (var g in SpawnedSlots)
            {
                if (g == null) continue;
                try { NetworkServer.Destroy(g.gameObject); } catch { }
            }
            SpawnedSlots.Clear();
            GrantCount = 0;
            _attempted = false;
            _partialRetries = 0;
            Probe.Banner("CHEAT SLOTS REMOVED", "destroyed " + n + " machine(s)");
        }
    }
}
