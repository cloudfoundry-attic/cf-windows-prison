using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using HP.WindowsPrison.Utilities;
using System.Globalization;

namespace HP.WindowsPrison.Restrictions
{
    class Filesystem : Rule
    {
        public const string prisonRestrictionsGroup = "prisons_FilesysCell";

        public override void Apply(Prison prison)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            WindowsUsersAndGroups.AddUserToGroup(prison.User.Username, prisonRestrictionsGroup);

            if (Directory.Exists(prison.Rules.PrisonHomePath))
            {
                Directory.Delete(prison.Rules.PrisonHomePath, true);
            }

            Directory.CreateDirectory(prison.Rules.PrisonHomePath);

            DirectoryInfo deploymentDirInfo = new DirectoryInfo(prison.Rules.PrisonHomePath);
            DirectorySecurity deploymentDirSecurity = deploymentDirInfo.GetAccessControl();

            // Owner is important to account for disk quota 		
            SetDirectoryOwner(deploymentDirSecurity, prison);

            // Taking ownership of a file has to be executed with0-031233332xpw0odooeoooooooooooooooooooooooooooooooooooooooooooooooooooooooooo restore privilege elevated privilages		
            using (new ProcessPrivileges.PrivilegeEnabler(Process.GetCurrentProcess(), ProcessPrivileges.Privilege.Restore))
            {
                deploymentDirInfo.SetAccessControl(deploymentDirSecurity);
            }
        }

        public override void Destroy(Prison prison)
        {
        }

        public override void Init()
        {
            
        }

        private readonly static object openDirLock = new object();
        private static string[] openDirs = new string[0];

        public static string[] OpenDirs
        {
            get
            {
                return openDirs;
            }
        }

        public static void TakeOwnership(string user, string directory)
        {
            string command = string.Format(CultureInfo.InvariantCulture, @"takeown /R /D Y /S localhost /U {0} /F ""{1}""", user, directory);

            int ret = Command.ExecuteCommand(command);

            if (ret != 0)
            {
                throw new PrisonException(@"take ownership failed.");
            }
        }

        public static void AddCreateSubdirDenyRule(string user, string directory, bool recursive = false)
        {
            string command = string.Format(CultureInfo.InvariantCulture, @"icacls ""{0}"" /deny {1}:(AD) /c{2}", directory.Replace("\\", "/"), user, recursive ? " /t" : string.Empty);

            int ret = Command.ExecuteCommand(command);

            if (ret != 0)
            {
                throw new PrisonException(@"icacls command denying subdir creation failed; command was: {0}", command);
            }
        }

        public static void AddCreateFileDenyRule(string user, string directory, bool recursive = false)
        {
            string command = string.Format(CultureInfo.InvariantCulture, @"icacls ""{0}"" /deny {1}:(W) /c{2}", directory.Replace("\\", "/"), user, recursive ? " /t" : string.Empty);
            int ret = Command.ExecuteCommand(command);

            if (ret != 0)
            {
                throw new PrisonException(@"icacls command denying file creation failed; command was: {0}", command);
            }
        }

        public override RuleInstanceInfo[] List()
        {
            return new RuleInstanceInfo[0];
        }

        public override RuleType GetFlag()
        {
            return RuleType.Filesystem;
        }

        public override void Recover(Prison prison)
        {
        }

        private static void SetDirectoryOwner(DirectorySecurity deploymentDirSecurity, Prison prison)
        {
            deploymentDirSecurity.SetOwner(new NTAccount(prison.User.Username));
            deploymentDirSecurity.SetAccessRule(
                new FileSystemAccessRule(
                    prison.User.Username, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow));
        }
    }
}
