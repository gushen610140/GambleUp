using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(GambleUp.Core), "GambleUp", "1.1.0", "w5603", null)]
[assembly: MelonGame("TENSTACK", "Gamble With Your Friends")]

namespace GambleUp
{
    public class Core : MelonMod
    {
        private bool _driverMade;

        public override void OnInitializeMelon()
        {
            Probe.Log = LoggerInstance;
            Probe.RefreshPause();

            Cheat.LoadConfig();

            LoggerInstance.Msg("Log file : " + Probe.FilePath);
            LoggerInstance.Msg("Pause    : create/delete '" + Probe.PauseFlagPath + "'");
            LoggerInstance.Msg("Cheat cfg: " + Cheat.ConfigPath + "  enabled=" + Cheat.Enabled +
                               " reward=+" + Cheat.Reward + "  machines=" +
                               (Cheat.Positions.Count > 0
                                   ? Cheat.Positions.Count + " at " +
                                     string.Join(" / ", Cheat.Positions.ConvertAll(p => Probe.Vec(p)).ToArray())
                                   : "1 (in front of player)"));

            Probe.Banner("GambleUp probe online",
                "log file = " + Probe.FilePath,
                "verbose logging paused = " + Probe.Paused);
        }

        public override void OnUpdate()
        {
            if (_driverMade) return;
            _driverMade = true;
            try
            {
                var go = new GameObject("GambleUp.ProbeDriver");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<ProbeDriver>();
            }
            catch (System.Exception e)
            {
                LoggerInstance.Error("driver failed: " + e);
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Probe.AnnounceScene(buildIndex, sceneName);
        }
    }
}
