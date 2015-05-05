namespace CloudFoundry.WindowsPrison
{
    using CloudFoundry.WindowsPrison.Native;
    using CloudFoundry.WindowsPrison.Utilities;
    using Microsoft.Win32;
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;

    internal class PrisonUserProfile
    {
        internal const int UnloadUserProfileCheckSleepMilliseconds = 100;
        private PrisonUser prisonUser;

        internal PrisonUserProfile(PrisonUser user)
        {
            this.prisonUser = user;
        }

        // Check if the profile is loaded.
        // This is useful to load the profile only once.
        public bool IsProfileLoaded()
        {
            var userSid = WindowsUsersAndGroups.GetLocalUserSid(this.prisonUser.UserName);

            // If a profile is loaded the Registry hive will be loaded in HKEY_USERS\{User-SID}
            var res = Registry.Users.GetSubKeyNames().Contains(userSid);

            return res;
        }

        // Loads the user's profile
        // Note: Beware of Paged Pool memory leaks!!! Unload the user profile when destroying the prison.
        // For each load called there must be a corresponding unload called (independent to process space).
        public void LoadUserProfile()
        {
            Native.NativeMethods.PROFILEINFO profileInfo = new Native.NativeMethods.PROFILEINFO();
            profileInfo.dwSize = Marshal.SizeOf(profileInfo);
            profileInfo.lpUserName = this.prisonUser.UserName;

            // PI_NOUI 0x00000001 // Prevents displaying of messages
            profileInfo.dwFlags = 0x1;

            profileInfo.lpProfilePath = null;
            profileInfo.lpDefaultPath = null;
            profileInfo.lpPolicyPath = null;
            profileInfo.lpServerName = null;

            bool loadSuccess = NativeMethods.LoadUserProfile(this.prisonUser.LogonToken.DangerousGetHandle(), ref profileInfo);
            int lastError = Marshal.GetLastWin32Error();

            if (!loadSuccess)
            {
                Logger.Error("Load user profile failed with error code: {0} for prison user {1}", lastError, this.prisonUser.UserName);
                throw new Win32Exception(lastError);
            }

            if (profileInfo.hProfile == IntPtr.Zero)
            {
                Logger.Error("Load user profile failed. HKCU handle was not loaded. Error code: {0} for prison user {1}", lastError, this.prisonUser.UserName);
                throw new Win32Exception(lastError);
            }
        }

        public void LoadUserProfileIfNotLoaded()
        {
            if (!this.IsProfileLoaded())
            {
                this.LoadUserProfile();
            }
        }

        /// <summary>
        /// This should be a replacement for UnloadUserProfile (http://msdn.microsoft.com/en-us/library/windows/desktop/bb762282%28v=vs.85%29.aspx)
        /// UnloadUserProfile cannot be invoked because the hProfile handle may not be available.
        /// </summary>
        public void UnloadUserProfile()
        {
            var userSid = WindowsUsersAndGroups.GetLocalUserSid(this.prisonUser.UserName);

            var userHive = Registry.Users.OpenSubKey(userSid);
            userHive.Handle.SetHandleAsInvalid();

            if (!NativeMethods.UnloadUserProfile(this.prisonUser.LogonToken.DangerousGetHandle(), userHive.Handle.DangerousGetHandle()))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void CreateUserProfile()
        {
            this.LoadUserProfile();
            this.UnloadUserProfile();
        }

        public void UnloadUserProfileUntilReleased()
        {
            while (this.IsProfileLoaded())
            {
                this.UnloadUserProfile();
                Thread.Sleep(UnloadUserProfileCheckSleepMilliseconds);
            }
        }

        public void ChangeProfilePath(string destination)
        {
            // Strategies for changes profile path:
            // http://programmaticallyspeaking.com/changing-the-user-profile-path-in-windows-7.html
            // 1. Create the user's profile in the default directory. Then change the value of ProfileImagePath from the following reg key:
            // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{User-SID}
            // 2. Change the default profile directory before creating the reg key and the revert it back. Mange the ProfiliesDirectory value from
            // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList
            // 3. Use symbolic links from the default profile path to the desired destination. 
            // 4. Use roaming profile: http://stackoverflow.com/questions/2015103/creating-roaming-user-profiles-programmatically-on-windows-2008

            this.UnloadUserProfileUntilReleased();

            StringBuilder pathBuf = new StringBuilder(1024);
            this.GetNativeUserProfileDirectory(pathBuf);

            string currentProfileDir = pathBuf.ToString();

            this.ChangeRegistryUserProfile(destination);

            Directory.Move(currentProfileDir, destination);
        }

        public void DeleteUserProfile()
        {
            string userSid = this.prisonUser.UserSID;

            int retries = 30;
            int errorCode = 0;

            StringBuilder pathBuf = new StringBuilder(1024);
            this.GetNativeUserProfileDirectory(pathBuf);
            string profilePath = pathBuf.ToString();

            // TODO: merge this with: windows-prison\src\CloudFoundry.WindowsPrison\Utilities\UserImpersonator.cs
            while (retries > 0)
            {
                if (!NativeMethods.DeleteProfile(userSid, profilePath, null))
                {
                    errorCode = Marshal.GetLastWin32Error();

                    if (errorCode == 2)
                    {
                        // Error Code 2: The user profile was not created or was already deleted
                        return;
                    }
                    else
                    {
                        // Error Code 87: The user profile is still loaded.
                        retries--;
                        Thread.Sleep(200);
                    }
                }
                else
                {
                    return;
                }
            }

            throw new Win32Exception(errorCode);
        }

        public void GetNativeUserProfileDirectory(StringBuilder pathBuf)
        {
            uint pathLen = (uint)pathBuf.Capacity;
            NativeMethods.GetUserProfileDirectory(this.prisonUser.LogonToken.DangerousGetHandle(), pathBuf, ref pathLen);
        }

        public void ChangeRegistryUserProfile(string destination)
        {
            using (var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (var userProfKey = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\" + this.prisonUser.UserSID, true))
                {
                    userProfKey.SetValue("ProfileImagePath", destination, RegistryValueKind.ExpandString);
                }
            }
        }
    }
}
