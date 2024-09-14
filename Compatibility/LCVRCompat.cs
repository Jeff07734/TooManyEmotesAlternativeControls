using LCVR.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TooManyEmotesAlternateControls.Compatibility
{
    internal static class LCVRCompat
    {
        private static bool Enabled { get { return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("io.daxcess.lcvr"); } }
        private static bool IsInVR { get { return VRSession.InVR; } }
        public static bool LoadedAndEnabled { get { return Enabled && IsInVR; } }
    }
}
