using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Windows.Forms;
namespace Capture.Interface
{
    [Serializable]
    public class CaptureConfig
    {
        public Direct3DVersion Direct3DVersion { get; set; }
        public bool ShowOverlay { get; set; }
        public int TargetFramesPerSecond { get; set; }
        public string TargetFolder { get; set; }
        public Keys screenshotHotkey {get; set;}
        public Keys reportCheatHotkey {get;set;}

        public CaptureConfig()
        {
            Direct3DVersion = Direct3DVersion.AutoDetect;
            ShowOverlay = false;
            TargetFramesPerSecond = 5;
            TargetFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            screenshotHotkey = Keys.F5;
            reportCheatHotkey = Keys.F6;
        }
    }
}
