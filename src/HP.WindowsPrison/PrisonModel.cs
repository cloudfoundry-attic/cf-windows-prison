namespace HP.WindowsPrison
{
    using System;
    using System.Runtime.Serialization;

    [DataContract]
    public abstract class PrisonModel
    {
        [DataMember]
        internal string desktopName
        {
            get;
            set;
        }

        [DataMember]
        public bool IsLocked
        {
            get;
            internal set;
        }

        [DataMember]
        public Guid Id
        {
            get;
            internal set;
        }

        [DataMember]
        public string Tag
        {
            get;
            set;
        }

        [DataMember]
        public PrisonConfiguration Configuration
        {
            get;
            internal set;
        }

        [DataMember]
        public PrisonUser User
        {
            get;
            internal set;
        }
    }
}
