using Ini.Net;
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
                userSid,
                string.Format(CultureInfo.InvariantCulture, "*{0}", userSid),
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

            IniFile updatedConfiguration = new IniFile(inFile);

            string grantsValue = string.Join(",", grants.ToArray());

            updatedConfiguration.WriteString(PrivilegeRightsSection, privilege, grantsValue);

            string command = string.Format(
                CultureInfo.InvariantCulture,
                "secedit.exe /configure /db secedit.sdb /cfg  \"{0}\"",
                inFile);

            int exitCode = Utilities.Command.ExecuteCommand(command);

            if (exitCode != 0)
            {
                throw new PrisonException("secedit exited with code {0} while trying to to save privileges", exitCode);
            }
        }

        private static HashSet<string> GetPrivilegeGrants(string privilege)
        {
            string grants = string.Empty;

            var currentPrivileges = LoadPrivileges();

            currentPrivileges.TryGetValue(privilege, out grants);

            HashSet<string> result = new HashSet<string>();

            string[] usersWithPrivilege = grants.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string user in usersWithPrivilege)
            {
                result.Add(user.ToUpperInvariant().Trim());
            }

            return result;
        }

        private static IDictionary<string, string> LoadPrivileges()
        {
            string outFile = Path.GetTempFileName();

            string command = string.Format(
                CultureInfo.InvariantCulture,
                "secedit.exe /export /cfg \"{0}\"",
                outFile);

            int exitCode = Utilities.Command.ExecuteCommand(command);

            if (exitCode != 0)
            {
                throw new PrisonException("secedit exited with code {0} while trying to to get privileges", exitCode);
            }

            IniFile output = new IniFile(outFile);

            if (output.SectionExists(PrivilegeRightsSection))
            {
                return output.ReadSection(PrivilegeRightsSection);
            }
            else
            {
                return new Dictionary<string, string>();
            }
        }
    }
}
