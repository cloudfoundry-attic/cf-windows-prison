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
        public const string prisonRestrictionsGroup = "prison_filesyscell";

        public override void Apply(Prison prison)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            WindowsUsersAndGroups.AddUserToGroup(prison.User.UserName, prisonRestrictionsGroup);

            if (Directory.Exists(prison.PrisonHomePath))
            {
                prison.User.Profile.UnloadUserProfileUntilReleased();
                Directory.Delete(prison.PrisonHomePath, true);
            }

            Directory.CreateDirectory(prison.PrisonHomePath);

            DirectoryInfo deploymentDirInfo = new DirectoryInfo(prison.PrisonHomePath);
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
            if (!WindowsUsersAndGroups.ExistsGroup(prisonRestrictionsGroup))
            {
                WindowsUsersAndGroups.CreateGroup(prisonRestrictionsGroup, "Members of this group are users used to sandbox Windows Prison Containers");
            }

            string windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string windowsRoot = Directory.GetDirectoryRoot(windowsFolder);

            string publicDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
            var publicDocumentsDirectory = new DirectoryInfo(publicDocumentsPath);
            string publicPath = publicDocumentsDirectory.Parent.FullName;

            string[] offLimitsDirectories = new string[]
            {
                windowsRoot,
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            };

            string[] offLimitsDirectoriesRecursive = new string[]
            {
                Path.Combine(windowsRoot, "tracing"),
                publicPath,
                Environment.GetFolderPath(Environment.SpecialFolder.CommonAdminTools),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonMusic),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonOemLinks),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonTemplates),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonVideos),
            };

            foreach (string dir in offLimitsDirectories)
            {
                Filesystem.AddCreateSubdirDenyRule(prisonRestrictionsGroup, dir, false);
                Filesystem.AddCreateFileDenyRule(prisonRestrictionsGroup, dir, false);
            }

            foreach (string dir in offLimitsDirectoriesRecursive)
            {
                Filesystem.AddCreateSubdirDenyRule(prisonRestrictionsGroup, dir, true);
                Filesystem.AddCreateFileDenyRule(prisonRestrictionsGroup, dir, true);
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
            string command = string.Format(CultureInfo.InvariantCulture, @"icacls ""{0}"" /deny {1}:(OI)(CI)(AD) /c{2}", directory.Replace("\\", "/"), user, recursive ? " /t" : string.Empty);

            int ret = Command.ExecuteCommand(command);

            if (ret != 0)
            {
                throw new PrisonException(@"icacls command denying subdirectory creation failed; command was: {0}", command);
            }
        }

        public static void AddCreateFileDenyRule(string user, string directory, bool recursive = false)
        {
            string command = string.Format(CultureInfo.InvariantCulture, @"icacls ""{0}"" /deny {1}:(CI)W /c{2}", directory.Replace("\\", "/"), user, recursive ? " /t" : string.Empty);
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

        public override RuleTypes RuleType
        {
            get
            {
                return RuleTypes.FileSystem;
            }
        }

        public override void Recover(Prison prison)
        {
        }

        private static void SetDirectoryOwner(DirectorySecurity deploymentDirSecurity, Prison prison)
        {
            deploymentDirSecurity.SetOwner(new NTAccount(prison.User.UserName));
            deploymentDirSecurity.SetAccessRule(
                new FileSystemAccessRule(
                    prison.User.UserName, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow));
        }
    }
}
