using HP.WindowsPrison.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HP.WindowsPrison
{
    public static class PrisonHostRequirements
    {

        public static void Verify()
        {
            PrisonHostRequirements.Verify(WindowsUsersAndGroups.CurrentUser);
        }

        public static void Verify(string user)
        {
            if (!UserRightsAssignmentPolicies.UserHasPrivilege(
                UserRightsAssignmentPolicies.ReplaceTokenPrivilege, 
                user))
            {
                throw new PrisonException(
                    "User {0} does not have the {1} privilege", 
                    user,
                    UserRightsAssignmentPolicies.ReplaceTokenPrivilege);
            }
        }

        //public static void Setup(string user)
        //{

        //}

        //public static void Remove(string user)
        //{

        //}

        //public static void VerifyUserisAdmin(string user)
        //{

        //}

        //private static void VerifyBaseJobObjectRights()
        //{

        //}
    }
}
