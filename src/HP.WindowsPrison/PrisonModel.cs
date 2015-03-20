using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace HP.WindowsPrison
{
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

        public PrisonUser User
        {
            get;
            internal set;
        }
    }
}
