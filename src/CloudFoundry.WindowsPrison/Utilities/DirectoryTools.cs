namespace CloudFoundry.WindowsPrison.Utilities
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Security.AccessControl;
    using System.Security.Principal;

    public sealed class DirectoryTools
    {
        private DirectoryTools()
        {
        }

        static public void GetOwnershipForDirectory(string path, IdentityReference owner)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            DirectorySecurity dirSecurity = dirInfo.GetAccessControl();
            
            dirSecurity.SetOwner(owner);
            dirSecurity.SetAccessRule(
                new FileSystemAccessRule(
                    owner,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.InheritOnly,
                    AccessControlType.Allow));

            using (new ProcessPrivileges.PrivilegeEnabler(Process.GetCurrentProcess(), ProcessPrivileges.Privilege.Restore))
            {
                dirInfo.SetAccessControl(dirSecurity);
            }
        }

        static public void ForceDeleteDirectory(string path)
        {
            var currentId = new NTAccount(Environment.UserDomainName, Environment.UserName);
            GetOwnershipForDirectory(path, currentId);
        }
    }
}
