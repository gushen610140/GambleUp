using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using MelonLoader;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GambleUp
{
    /// <summary>
    /// Lean runtime log. Only three things matter right now:
    ///   1. when we entered CasinoScene
    ///   2. when the cheat slot was spawned, and at which coordinates
    ///   3. a live broadcast of the player's position / balance / tickets
    /// Everything else (spawn census, item catalogs, pickup traces) was recon and has been removed.
    /// </summary>
    public static class Probe
    {
        public static MelonLogger.Instance Log;

        private static string _dir;
        private static string _file;
        private static string _pauseFlag;
        private static readonly object WriteLock = new object();

        public static bool Paused { get; private set; }
        public static string CurrentScene = "<none>";
        public static int CurrentBuildIndex = -1;

        // ------------------------------------------------------------ paths

        private static void EnsurePaths()
        {
            if (_file != null) return;
            try { _dir = Application.persistentDataPath; }
            catch
            {
                _dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Low", "GambleUp");
            }
            _file = Path.Combine(_dir, "GambleUp_probe.log");
            _pauseFlag = Path.Combine(_dir, "GambleUp_probe.paused");
        }

        public static string FilePath
        {
            get { EnsurePaths(); return _file; }
        }

        public static string PauseFlagPath
        {
            get { EnsurePaths(); return _pauseFlag; }
        }

        public static void RefreshPause()
        {
            EnsurePaths();
            Paused = File.Exists(_pauseFlag);
        }

        // ------------------------------------------------------------ output

        public static void W(string tag, string msg, bool console = true)
        {
            if (Paused) return;
            EnsurePaths();
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] [" + tag + "] " + msg;
            lock (WriteLock)
            {
                try { File.AppendAllText(_file, line + Environment.NewLine); } catch { }
            }
            if (console) Console(line);
        }

        public static void Console(string msg)
        {
            if (Log != null) Log.Msg(msg);
        }

        /// <summary>
        /// Big boxed banner. Always written and always echoed to the console, even when verbose
        /// logging is paused, so milestone events are impossible to miss.
        /// </summary>
        public static void Banner(string title, params string[] lines)
        {
            const string bar = "================================================================";
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(bar);
            sb.AppendLine("  " + title);
            sb.AppendLine(bar);
            if (lines != null)
                foreach (string l in lines) sb.AppendLine("  " + l);
            sb.AppendLine(bar);

            string text = sb.ToString();
            EnsurePaths();
            lock (WriteLock)
            {
                try { File.AppendAllText(_file, text + Environment.NewLine); } catch { }
            }
            foreach (string l in text.TrimEnd().Split('\n'))
                Console(l.TrimEnd());
        }

        // ------------------------------------------------------------ formatting

        public static string Vec(Vector3 v) =>
            "(" + v.x.ToString("0.0") + ", " + v.y.ToString("0.0") + ", " + v.z.ToString("0.0") + ")";

        public static string Pos(GameObject go)
        {
            try { return go == null ? "?" : Vec(go.transform.position); }
            catch { return "?"; }
        }

        public static string Fmt(BigNumber b)
        {
            try
            {
                if (b.IsZero) return "0";
                if (b.Exponent > -6 && b.Exponent < 15) return b.ToDouble().ToString("0.####");
                return b.Mantissa.ToString("0.####") + "e" + b.Exponent;
            }
            catch { return "?"; }
        }

        public static string Mode()
        {
            if (NetworkServer.active) return NetworkClient.active ? "Host" : "Server";
            return NetworkClient.active ? "Client" : "Offline";
        }

        // ------------------------------------------------------------ live broadcast

        /// <summary>One compact line every couple of seconds: where the player is and what they hold.</summary>
        public static void LogPlayerTick()
        {
            RefreshPause();
            if (Paused) return;

            var sb = new StringBuilder();
            sb.Append("scene=").Append(CurrentScene).Append("(#").Append(CurrentBuildIndex).Append(")")
              .Append("  mode=").Append(Mode());

            try
            {
                var lp = NetworkClient.localPlayer;
                if (lp != null)
                {
                    Transform t = lp.transform;
                    var rb = t.GetComponent<Rigidbody>();
                    sb.Append("  player=").Append(Vec(t.position))
                      .Append(" yaw=").Append(t.eulerAngles.y.ToString("0"))
                      .Append("  vel=").Append(rb == null ? "-" : rb.linearVelocity.magnitude.ToString("0.0"));
                }
                else sb.Append("  player=<no localPlayer>");
            }
            catch (Exception e) { sb.Append("  player=[err ").Append(e.Message).Append(']'); }

            try
            {
                var gm = Extensions.NetworkSingleton<GameManager>.Instance;
                sb.Append(gm == null
                    ? "  game=[null]"
                    : "  day=" + gm.daysPassed + "/" + (gm.daysPassed + gm.daysLeft) +
                      " floor=" + gm.currentFloor + " state=" + gm.state +
                      " quota=" + Fmt(gm.currentQuota) + "/" + Fmt(gm.requiredQuotaToNextFloor));
            }
            catch { sb.Append("  game=[?]"); }

            try
            {
                var mm = Extensions.NetworkSingleton<MoneyManager>.Instance;
                sb.Append(mm == null
                    ? "  money=[null]"
                    : "  balance=" + Fmt(mm.balance) + " tickets=" + mm.ticketBalance);
            }
            catch { sb.Append("  money=[?]"); }

            if (Cheat.SpawnedSlots.Count > 0)
            {
                sb.Append("  cheatSlots=[");
                for (int i = 0; i < Cheat.SpawnedSlots.Count; i++)
                {
                    if (i > 0) sb.Append(' ');
                    var g = Cheat.SpawnedSlots[i];
                    sb.Append(g == null ? "<gone>" : Vec(g.transform.position));
                }
                sb.Append(']');
            }

            W("PLAYER", sb.ToString());
        }

        // ------------------------------------------------------------ scene

        public static void AnnounceScene(int buildIndex, string sceneName)
        {
            CurrentBuildIndex = buildIndex;
            CurrentScene = sceneName;

            bool casino = string.Equals(sceneName, "CasinoScene", StringComparison.Ordinal);
            Probe.Banner(casino ? ">>> ENTERED CasinoScene <<<" : "SCENE LOADED: " + sceneName,
                "buildIndex = " + buildIndex,
                "network    = " + Mode() + "  (server active = " + NetworkServer.active + ")",
                "cheat      = enabled " + Cheat.Enabled + ", reward +" + Cheat.Reward +
                    ", machines " + (Cheat.Positions.Count > 0
                        ? Cheat.Positions.Count + " at fixed positions"
                        : "1 in front of player"),
                casino ? "The cheat slot spawns in this scene." : "Not a casino floor, no cheat slot here.");
        }
    }

    /// <summary>Keeps the per-frame log tick alive across scene loads.</summary>
    public class ProbeDriver : MonoBehaviour
    {
        private float _next;

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _next) return;
            _next = now + 2f;
            Probe.LogPlayerTick();
            Cheat.Tick();
        }
    }
}
