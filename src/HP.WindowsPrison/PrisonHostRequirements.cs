namespace HP.WindowsPrison
{
    using HP.WindowsPrison.Utilities;

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
