using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace HP.WindowsPrison.Restrictions
{
    class Network : Rule
    {
        public override void Apply(Prison prison)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            Network.CreateOutboundThrottlePolicy(prison.User.UserName, prison.User.UserName, prison.Configuration.NetworkOutboundRateLimitBitsPerSecond);

            if (prison.Configuration.UrlPortAccess > 0)
            {
                Network.RemoveOutboundThrottlePolicy(PrisonUser.GlobalPrefix + PrisonUser.Separator + prison.Configuration.UrlPortAccess.ToString(CultureInfo.InvariantCulture));
                Network.CreateOutboundThrottlePolicy(PrisonUser.GlobalPrefix + PrisonUser.Separator + prison.Configuration.UrlPortAccess.ToString(CultureInfo.InvariantCulture), prison.Configuration.UrlPortAccess, prison.Configuration.AppPortOutboundRateLimitBitsPerSecond);
            }
        }

        public override void Destroy(Prison prison)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            Network.RemoveOutboundThrottlePolicy(prison.User.UserName);
            Network.RemoveOutboundThrottlePolicy(PrisonUser.GlobalPrefix + PrisonUser.Separator + prison.Configuration.UrlPortAccess.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Sets the limit for the upload network data rate. This limit is applied for the specified user.
        /// This method is not reentrant. Remove the policy first after creating it again.
        /// </summary>
        private static void CreateOutboundThrottlePolicy(string ruleName, string windowsUsername, long bitsPerSecond)
        {
            var StandardCimv2 = new ManagementScope(@"root\StandardCimv2");

            using (ManagementClass netqos = new ManagementClass("MSFT_NetQosPolicySettingData"))
            {
                netqos.Scope = StandardCimv2;

                using (ManagementObject newInstance = netqos.CreateInstance())
                {
                    newInstance["Name"] = ruleName;
                    newInstance["UserMatchCondition"] = windowsUsername;

                    // ThrottleRateAction is in bytesPerSecond according to the WMI docs.
                    // Acctualy the units are bits per second, as documented in the PowerShell cmdlet counterpart.
                    newInstance["ThrottleRateAction"] = bitsPerSecond;

                    newInstance.Put();
                }
            }
        }

        /// <summary>
        /// Sets the limit for the upload network data rate. This limit is applied for a specific server URL passing through HTTP.sys.
        /// This rules are applicable to IIS, IIS WHC and IIS Express. This goes hand in hand with URL Acls.
        /// This method is not reentrant. Remove the policy first after creating it again.
        /// </summary>
        private static void CreateOutboundThrottlePolicy(string ruleName, int urlPort, long bitsPerSecond)
        {
            var StandardCimv2 = new ManagementScope(@"root\StandardCimv2");

            using (ManagementClass netqos = new ManagementClass("MSFT_NetQosPolicySettingData"))
            {
                netqos.Scope = StandardCimv2;

                using (ManagementObject newInstance = netqos.CreateInstance())
                {
                    newInstance["Name"] = ruleName;
                    newInstance["URIMatchCondition"] = String.Format(CultureInfo.InvariantCulture, "http://*:{0}/", urlPort);
                    newInstance["URIRecursiveMatchCondition"] = true;

                    // ThrottleRateAction is in bytesPerSecond according to the WMI docs.
                    // Acctualy the units are bits per second, as documented in the PowerShell cmdlet counterpart.
                    newInstance["ThrottleRateAction"] = bitsPerSecond;

                    newInstance.Put();
                }
            }
        }

        private static void RemoveOutboundThrottlePolicy(string ruleName)
        {
            var wql = string.Format(CultureInfo.InvariantCulture, "SELECT * FROM MSFT_NetQosPolicySettingData WHERE Name = \"{0}\"", ruleName);
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\StandardCimv2", wql))
            {
                // should only iterate once
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    queryObj.Delete();
                    queryObj.Dispose();
                }
            }
        }

        private static List<Dictionary<string, string>> GetThrottlePolicies()
        {
            var wql = "SELECT * FROM MSFT_NetQosPolicySettingData";

            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\StandardCimv2", wql))
            {
                var policies = searcher.Get();

                var ret = new List<Dictionary<string, string>>();

                foreach (ManagementObject policy in policies)
                {
                    var item = new Dictionary<string, string>();
                    item["Name"] = policy["Name"].ToString();
                    item["ThrottleRateAction"] = policy["ThrottleRateAction"].ToString();
                    item["URIMatchCondition"] = policy["URIMatchCondition"] != null ? policy["URIMatchCondition"].ToString() : String.Empty;
                    item["UserMatchCondition"] = policy["UserMatchCondition"] != null ? policy["UserMatchCondition"].ToString() : String.Empty;
                    ret.Add(item);
                }

                return ret;
            }
        }

        public override RuleInstanceInfo[] List()
        {
            List<RuleInstanceInfo> result = new List<RuleInstanceInfo>();

            foreach (Dictionary<string, string> policy in GetThrottlePolicies())
            {
                if (!string.IsNullOrWhiteSpace(policy["Name"] as string) && policy["Name"].ToString().StartsWith(PrisonUser.GlobalPrefix + PrisonUser.Separator, StringComparison.Ordinal))
                {
                    string info = string.Format(CultureInfo.InvariantCulture, "{0} bps; match: {1}",
                        policy["ThrottleRateAction"],
                        policy["URIMatchCondition"] != null ? policy["URIMatchCondition"] : (policy["UserMatchCondition"] != null ? policy["UserMatchCondition"] : string.Empty)
                        );

                    result.Add(new RuleInstanceInfo()
                    {
                        Name = policy["Name"].ToString(CultureInfo.InvariantCulture),
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
                return RuleTypes.Network;
            }
        }

        public override void Recover(Prison prison)
        {
        }
    }
}
