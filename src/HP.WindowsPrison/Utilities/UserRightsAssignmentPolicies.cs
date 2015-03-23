using IniParser;
using IniParser.Model;
using IniParser.Parser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HP.WindowsPrison.Utilities
{
    internal static class UserRightsAssignmentPolicies
    {
        private const string PrivilegeRightsSection = "Privilege Rights";
        public const string ReplaceTokenPrivilege = "SeAssignPrimaryTokenPrivilege";

        public static bool UserHasPrivilege(string privilege, string user)
        {
            if (string.IsNullOrWhiteSpace(privilege))
            {
                throw new ArgumentException("Privilege cannot be empty.", "privilege");
            }

            if (string.IsNullOrWhiteSpace(privilege))
            {
                throw new ArgumentException("user cannot be empty.", "user");
            }

            string userSid = WindowsUsersAndGroups.GetLocalUserSid(user);

            HashSet<string> grants = GetPrivilegeGrants(privilege);

            string[] valuesToLookup = new string[] {
                userSid.ToUpperInvariant(),
                string.Format(CultureInfo.InvariantCulture, "*{0}", userSid).ToUpperInvariant(),
                user.ToUpperInvariant()
            };

            return grants.Any(u => valuesToLookup.Contains(u.ToUpperInvariant().Trim()));
        }

        public static void GrantPrivilegeToUser(string privilege, string user)
        {
            if (string.IsNullOrWhiteSpace(privilege))
            {
                throw new ArgumentException("Privilege cannot be empty.", "privilege");
            }

            if (string.IsNullOrWhiteSpace(privilege))
            {
                throw new ArgumentException("user cannot be empty.", "user");
            }

            string userSid = WindowsUsersAndGroups.GetLocalUserSid(user);

            HashSet<string> grants = GetPrivilegeGrants(privilege);

            grants.Add(
                string.Format(
                CultureInfo.InvariantCulture,
                "*{0}",
                userSid));

            Save(privilege, grants);
        }

        public static void RevokePrivilegeToUser(string privilege, string user)
        {
            if (string.IsNullOrWhiteSpace(privilege))
            {
                throw new ArgumentException("Privilege cannot be empty.", "privilege");
            }

            if (string.IsNullOrWhiteSpace(privilege))
            {
                throw new ArgumentException("user cannot be empty.", "user");
            }

            string userSid = WindowsUsersAndGroups.GetLocalUserSid(user);

            HashSet<string> grants = GetPrivilegeGrants(privilege);

            string[] valuesToLookup = new string[] {
                userSid,
                string.Format(CultureInfo.InvariantCulture, "*{0}", userSid),
                user.ToUpperInvariant()
            };

            foreach (string value in valuesToLookup)
            {
                if (grants.Contains(value))
                {
                    grants.Remove(value);
                }
            }

            Save(privilege, grants);
        }

        private static void Save(string privilege, HashSet<string> grants)
        {
            string inFile = Path.GetTempFileName();

            try
            {
                var updatedConfiguration = new IniData();
                updatedConfiguration.Sections.AddSection("Unicode");
                updatedConfiguration.Sections.AddSection("Version");
                updatedConfiguration.Sections.AddSection(PrivilegeRightsSection);

                updatedConfiguration["Unicode"]["Unicode"] = "yes";
                updatedConfiguration["Version"]["signature"] = "\"$CHICAGO$\"";
                updatedConfiguration["Version"]["revision"] = "1";

                string grantsValue = string.Join(",", grants.ToArray());

                updatedConfiguration[PrivilegeRightsSection][privilege] = grantsValue;

                FileIniDataParser iniFile = new FileIniDataParser();
                iniFile.WriteFile(inFile, updatedConfiguration);

                string command = string.Format(
                    CultureInfo.InvariantCulture,
                    "secedit.exe /configure /db secedit.sdb /cfg \"{0}\"",
                    inFile);

                int exitCode = Utilities.Command.ExecuteCommand(command);

                if (exitCode != 0)
                {
                    throw new PrisonException("secedit exited with code {0} while trying to to save privileges", exitCode);
                }
            }
            finally
            {
                File.Delete(inFile);
            }
        }

        private static HashSet<string> GetPrivilegeGrants(string privilege)
        {
            string grants = null;

            var currentPrivileges = LoadPrivileges();

            if (currentPrivileges.ContainsKey(privilege))
            {
                grants = currentPrivileges[privilege];
            }
            else
            {
                grants = string.Empty;
            }

            HashSet<string> result = new HashSet<string>();

            string[] usersWithPrivilege = grants.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string user in usersWithPrivilege)
            {
                result.Add(user.ToUpperInvariant().Trim());
            }

            return result;
        }

        private static KeyDataCollection LoadPrivileges()
        {
            string outFile = Path.GetTempFileName();

            try
            {
                string command = string.Format(
                CultureInfo.InvariantCulture,
                "secedit.exe /export /cfg \"{0}\"",
                outFile);

                int exitCode = Utilities.Command.ExecuteCommand(command);

                if (exitCode != 0)
                {
                    throw new PrisonException("secedit exited with code {0} while trying to to get privileges", exitCode);
                }

                var parser = new FileIniDataParser();
                IniData output = parser.ReadFile(outFile);

                if (output.Sections.ContainsSection(PrivilegeRightsSection))
                {
                    return output.Sections[PrivilegeRightsSection];
                }
                else
                {
                    return new KeyDataCollection();
                }
            }
            finally
            {
                File.Delete(outFile);
            }
        }
    }
}
