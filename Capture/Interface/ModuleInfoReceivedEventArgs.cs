using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Capture.Interface
{

    [Serializable]   
    public class ModuleInfo
    {
        public string name { get; set; }
        public string path { get; set; } 

        //public ModuleInfo(ProcessModule proc ){
        //    name = proc.ModuleName;
        //    path = proc.FileName;
        //}

        public override int GetHashCode()
        {
            return StringComparer.CurrentCulture.GetHashCode(this.path);

        }

        public override bool Equals(object obj)
        {
            return this.path == ((ModuleInfo)obj).path && this.name == ((ModuleInfo)obj).name;
        }

    }


    [Serializable]   
    public class ModuleInfoReceivedEventArgs: MarshalByRefObject
    {
        //public Modules _moduleinfo { get; set; }
        public string processName {get;set;}
        
        public HashSet<ModuleInfo> modules { get; set; }
        public ModuleInfoReceivedEventArgs(string name, HashSet<ModuleInfo> list)
        {
            modules = list;
            processName = name;
        }

        //public override string ToString()
        //{
        //    return String.Format("{0}: {1}", MessageType, Message);
        //}
    }
}