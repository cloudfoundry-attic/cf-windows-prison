using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HP.WindowsPrison.Utilities;
using System.Globalization;

namespace HP.WindowsPrison.Allowances
{
    internal class Httpsys : Rule
    {
        public override void Apply(Prison prison)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            Httpsys.RemovePortAccess(prison.Configuration.UrlPortAccess, true);
            Httpsys.AddPortAccess(prison.Configuration.UrlPortAccess, prison.User.UserName);
        }

        public override void Destroy(Prison prison)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            Httpsys.RemovePortAccess(prison.Configuration.UrlPortAccess, true);
        }

        /// <summary>
        /// Allow access to the URL with the specified port for the specified username.
        /// This will allow IIS HWC and IIS Express to bind and listen to that port.
        /// </summary>
        /// <param name="port">Http port number.</param>
        /// <param name="userName">Windows Local username.</param>
        public static void AddPortAccess(int port, string userName)
        {
            string command = String.Format(
                CultureInfo.InvariantCulture, 
                "netsh http add urlacl url=http://*:{0}/ user={1} listen=yes delegate=no", 
                port.ToString(CultureInfo.InvariantCulture), 
                userName);

            Logger.Debug("Adding url acl with the following command: {0}", command);

            int ret = Command.ExecuteCommand(command);

            if (ret != 0)
            {
                throw new PrisonException("netsh http add urlacl command failed with error code {0}.", ret);
            }
        }

        /// <summary>
        /// Remove access for the specified port.
        /// </summary>
        /// <param name="port">Http port number.</param>
        /// <param name="ignoreFailure">True if you want an exception to be thrown if there was an error, false otherwise.</param>
        public static void RemovePortAccess(int port, bool ignoreFailure)
        {
            string command = String.Format(CultureInfo.InvariantCulture, "netsh http delete urlacl url=http://*:{0}/", port.ToString(CultureInfo.InvariantCulture));

            Logger.Debug("Removing url acl with the following command: {0}", command);

            int ret = Command.ExecuteCommand(command);

            if (ret != 0 && !ignoreFailure)
            {
                throw new PrisonException("netsh http delete urlacl command failed with exit code {0}.", ret);
            }
        }

        public static void RemovePortAccess(int port)
        {
            RemovePortAccess(port, false);
        }

        public static string ListPortAccess()
        {
            Logger.Debug("Listing url acl");

            string output = Command.RunCommandAndGetOutput("netsh", "http show urlacl");

            return output;
        }

        public override RuleInstanceInfo[] List()
        {
            List<RuleInstanceInfo> result = new List<RuleInstanceInfo>();

            string portAccessList = ListPortAccess();

            foreach (Match match in Regex.Matches(portAccessList, "Reserved URL.+?SDDL", RegexOptions.Singleline))
            {
                Match infoMatch = Regex.Match(match.Value, @"Reserved URL.+?:\s+(http://.*)$", RegexOptions.Multiline);
                Match userMatch = Regex.Match(match.Value, @"User:\s+(.+)$", RegexOptions.Multiline);


                string info = infoMatch.Groups.Count > 1 ? infoMatch.Groups[1].Value.Trim() : string.Empty;
                string name = userMatch.Groups.Count > 1 ? userMatch.Groups[1].Value.Trim() : (Regex.IsMatch(match.Value, "Can't lookup sid, Error: 1332") ? "orphaned" : string.Empty);

                if (name.Contains(PrisonUser.GlobalPrefix + PrisonUser.Separator) || name == "orphaned")
                {
                    result.Add(new RuleInstanceInfo()
                    {
                        Name = name,
                        Info = info
                    });
                }
            }

            return result.ToArray();
        }

        public override void Init()
        {
        }

        public override RuleTypes RuleType
        {
            get
            {
                return RuleTypes.Httpsys;
            }
        }
        public override void Recover(Prison prison)
        {
        }
    }
}
