using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HP.WindowsPrison.Utilities;

namespace HP.WindowsPrison.Allowances
{
    // Add the prison user to the IIS_IUSRS group to allow it to have access to the complication mutex
    // Issues also described here: http://blogs.msdn.com/b/jorman/archive/2006/07/24/system-invalidoperationexception-mutex-could-not-be-created.aspx
    class IISGroup : Rule
    {
        const string IISGroupName = "IIS_IUSRS";
        
        public override void Apply(Prison prison)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            if (WindowsUsersAndGroups.ExistsGroup(IISGroupName))
            {
                WindowsUsersAndGroups.AddUserToGroup(prison.User.UserName, IISGroupName);
            }
            else
            {
                Logger.Warning("Prison {0} not added to IIS Users group {1}. The group was not found.", prison.Id, IISGroupName);
            }
        }

        public override void Destroy(Prison prison)
        {
        }

        public override RuleInstanceInfo[] List()
        {
            return new RuleInstanceInfo[0];
        }

        public override void Init()
        {
        }

        public override RuleTypes RuleType
        {
            get
            {
                return RuleTypes.IISGroup;
            }
        }

        public override void Recover(Prison prison)
        {
        }
    }
}
