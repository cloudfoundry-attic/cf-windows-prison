using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace HP.WindowsPrison
{
    internal abstract class Rule
    {
        public abstract void Apply(Prison prison);

        public abstract void Destroy(Prison prison);

        public abstract void Recover(Prison prison);

        public abstract RuleInstanceInfo[] List();

        public abstract void Init();

        public abstract RuleTypes RuleType
        {
            get;
        }
    }

    public class RuleInstanceInfo
    {
        public string Name
        {
            get;
            set;
        }

        public string Info
        {
            get;
            set;
        }

        public RuleInstanceInfo()
        {
            this.Name = string.Empty;
            this.Info = string.Empty;
        }
    }

    [DataContract, Flags]
    public enum RuleTypes
    {
        [EnumMember]
        None = 0,
        [EnumMember]
        CPU = 1 << 1,
        [EnumMember]
        Disk = 1 << 2,
        [EnumMember]
        FileSystem = 1 << 3,
        [EnumMember]
        Httpsys = 1 << 4,
        [EnumMember]
        Network = 1 << 5,
        [EnumMember]
        WindowStation = 1 << 6,
        [EnumMember]
        Memory = 1 << 7,
        [EnumMember]
        IISGroup = 1 << 8,
        [EnumMember]
        All = None | CPU | Disk | FileSystem | Httpsys | Network | WindowStation | Memory | IISGroup
    }
}
