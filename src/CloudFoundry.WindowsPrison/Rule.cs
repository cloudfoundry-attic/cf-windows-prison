namespace CloudFoundry.WindowsPrison
{
    using System;
    using System.Runtime.Serialization;

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

    internal abstract class Rule
    {
        public abstract RuleTypes RuleType
        {
            get;
        }

        public abstract void Apply(Prison prison);

        public abstract void Destroy(Prison prison);

        public abstract void Recover(Prison prison);

        public abstract RuleInstanceInfo[] List();

        public abstract void Init();
    }

    public class RuleInstanceInfo
    {
        public RuleInstanceInfo()
        {
            this.Name = string.Empty;
            this.Info = string.Empty;
        }

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
    }
}
