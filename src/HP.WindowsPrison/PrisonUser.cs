using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using HP.WindowsPrison.Utilities;
using System.Globalization;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace HP.WindowsPrison
{
    [DataContract]
    public class PrisonUser
    {
        public const string GlobalPrefix = "prison";
        public const char Separator = '_';

        SafeTokenHandle logonToken = null;
        PrisonUserProfile profile = null;

        [DataMember]
        private string usernamePrefix;

        [DataMember]
        private bool created = false;

        [DataMember]
        private string username = string.Empty;

        [DataMember]
        private string password = string.Empty;

        [DataMember]
        private string userSID = string.Empty;

        public string UserNamePrefix
        {
            get
            {
                return this.usernamePrefix;
            }
        }

        public string UserName
        {
            get
            {
                return this.username;
            }
        }

        public string Password
        {
            get
            {
                return this.password;
            }
        }

        public string UserSID
        {
            get
            {
                return this.userSID;
            }
        }

        internal SafeTokenHandle LogonToken
        {
            get
            {
                this.InitializeLogonToken();
                return this.logonToken;
            }
        }

        internal PrisonUserProfile Profile
        {
            get
            {
                if (this.profile == null)
                {
                    this.profile = new PrisonUserProfile(this);
                }

                return this.profile;
            }
        }

        public static PrisonUser[] ListUsers()
        {
            List<PrisonUser> result = new List<PrisonUser>();

            string[] allUsers = WindowsUsersAndGroups.GetUsers();


            foreach (string user in allUsers)
            {
                if (user.StartsWith(PrisonUser.GlobalPrefix, StringComparison.Ordinal))
                {
                    Prison prison = PrisonManager.LoadPrisonNoAttach(user);

                    // If we can't find the user's password, ignore the account
                    if (prison != null)
                    {
                        string password = PrisonManager.LoadPrisonNoAttach(user).User.Password;
                        result.Add(new PrisonUser(PrisonUser.GetUsernamePrefix(user), user, password, true));
                    }
                }
            }

            return result.ToArray();
        }

        public static PrisonUser[] ListUsers(string prefixFilter)
        {
            return ListUsers().Where(user => user.usernamePrefix == prefixFilter).ToArray();
        }

        private PrisonUser(string prefix, string username, string password, bool existing)
        {
            if (prefix.Length > 5)
            {
                throw new ArgumentException("The prefix length must be 5 characters or less.");
            }

            this.usernamePrefix = prefix;
            this.username = existing ? username : GenerateUsername(username);
            this.password = password;
            this.created = existing;
        }

        public PrisonUser() : this(string.Empty)
        {
        }

        public PrisonUser(string prefix) : 
            this(prefix, Credentials.GenerateCredential(7), string.Format(CultureInfo.InvariantCulture, "Pr!5{0}", Credentials.GenerateCredential(10)), false)
        {
        }

        public void Create()
        {
            if (this.created)
            {
                throw new InvalidOperationException("This user has already been created.");
            }

            if (WindowsUsersAndGroups.ExistsUser(this.username))
            {
                throw new InvalidOperationException("This windows user already exists.");
            }

            WindowsUsersAndGroups.CreateUser(this.username, this.password);

            this.userSID = WindowsUsersAndGroups.GetLocalUserSid(this.username);

            this.created = true;
        }

        public void Delete()
        {
            if (!this.created)
            {
                throw new InvalidOperationException("This user has not been created yet.");
            }

            if (!WindowsUsersAndGroups.ExistsUser(this.username))
            {
                throw new InvalidOperationException("Cannot find this windows user.");
            }

            WindowsUsersAndGroups.DeleteUser(this.username);
        }

        private static string GetUsernamePrefix(string username)
        {
            string[] pieces = username.Split(PrisonUser.Separator);
            if (pieces.Length != 3)
            {
                return string.Empty;
            }
            else
            {
                return pieces[1];
            }
        }

        private string GenerateUsername(string baseUsername)
        {
            List<string> usernamePieces = new List<string>();
            usernamePieces.Add(PrisonUser.GlobalPrefix);
            
            if (!string.IsNullOrWhiteSpace(this.usernamePrefix))
            {
                usernamePieces.Add(this.usernamePrefix);
            }

            usernamePieces.Add(baseUsername);

            return string.Join(PrisonUser.Separator.ToString(), usernamePieces.ToArray());
        }

        private void InitializeLogonToken()
        {
            if (this.logonToken == null)
            {

                var logonResult = NativeMethods.LogonUser(
                    userName: this.username,
                    domain: ".",
                    password: this.password,
                    logonType: NativeMethods.LogonType.LOGON32_LOGON_INTERACTIVE, // TODO: consider using Native.LogonType.LOGON32_LOGON_SERVICE see: http://blogs.msdn.com/b/winsdk/archive/2013/03/22/how-to-launch-a-process-as-a-full-administrator-when-uac-is-enabled.aspx
                    logonProvider: NativeMethods.LogonProvider.LOGON32_PROVIDER_DEFAULT,
                    token: out logonToken
                    );

                if (!logonResult)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        internal Dictionary<string, string> RetrieveDefaultEnvironmentVariables()
        {
            Dictionary<string, string> res = new Dictionary<string, string>();

            var envblock = IntPtr.Zero;

            if (!NativeMethods.CreateEnvironmentBlock(out envblock, logonToken.DangerousGetHandle(), false))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            // source: http://www.pinvoke.net/default.aspx/userenv.createenvironmentblock
            try
            {
                StringBuilder testData = new StringBuilder("");

                unsafe
                {
                    short* start = (short*)envblock.ToPointer();
                    bool done = false;
                    short* current = start;
                    while (!done)
                    {
                        if ((testData.Length > 0) && (*current == 0) && (current != start))
                        {
                            String data = testData.ToString();
                            int index = data.IndexOf('=');
                            if (index == -1)
                            {
                                res.Add(data, "");
                            }
                            else if (index == (data.Length - 1))
                            {
                                res.Add(data.Substring(0, index), "");
                            }
                            else
                            {
                                res.Add(data.Substring(0, index), data.Substring(index + 1));
                            }
                            testData.Length = 0;
                        }
                        if ((*current == 0) && (current != start) && (*(current - 1) == 0))
                        {
                            done = true;
                        }
                        if (*current != 0)
                        {
                            testData.Append((char)*current);
                        }
                        current++;
                    }
                }
            }
            finally
            {
                NativeMethods.DestroyEnvironmentBlock(envblock);
            }

            return res;
        }

        /// <summary>
        /// Sets an environment variable for the user.
        /// </summary>
        /// <param name="envVariables">Hashtable containing environment variables.</param>
        internal void SetUserEnvironmentVariables(Dictionary<string, string> envVariables)
        {
            if (envVariables == null)
            {
                throw new ArgumentNullException("envVariables");
            }

            if (envVariables.Keys.Any(x => x.Contains('=')))
            {
                throw new ArgumentException("A name of an environment variable contains the invalid '=' characther", "envVariables");
            }

            if (envVariables.Keys.Any(x => string.IsNullOrEmpty(x)))
            {
                throw new ArgumentException("A name of an environment variable is null or empty", "envVariables");
            }

            this.profile.LoadUserProfileIfNotLoaded();

            using (var allUsersKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64))
            {
                using (var envRegKey = allUsersKey.OpenSubKey(this.UserSID + "\\Environment", true))
                {
                    foreach (var env in envVariables)
                    {
                        var value = env.Value == null ? string.Empty : env.Value;

                        envRegKey.SetValue(env.Key, value, RegistryValueKind.String);
                    }

                }
            }
        }
    }
}
