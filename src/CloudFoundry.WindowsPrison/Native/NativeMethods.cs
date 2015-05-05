namespace CloudFoundry.WindowsPrison.Native
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Win32.SafeHandles;

    [ExcludeFromCodeCoverageAttribute]
    internal static class NativeMethods
    {
        public const int STD_OUTPUT_HANDLE = -11;
        public const int STD_INPUT_HANDLE = -10;
        public const int STD_ERROR_HANDLE = -12;

        public const int UOI_FLAGS = 1;
        public const int UOI_NAME = 2;
        public const int UOI_TYPE = 3;
        public const int UOI_USER_SID = 4;
        public const int UOI_HEAPSIZE = 5;
        public const int UOI_IO = 6;

        internal const int SE_PRIVILEGE_ENABLED = 0x00000002;
        internal const int SE_PRIVILEGE_DISABLED = 0x00000000;
        internal const int TOKEN_QUERY = 0x00000008;
        internal const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;

        public delegate bool EnumWindowStationsDelegate(string windowsStation, IntPtr lParam);

        #region Enums
        [Flags]
        public enum ThreadAccess : uint
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200)
        }

        [Flags]
        public enum ACCESS_MASK : uint
        {
            DELETE = 0x00010000,
            READ_CONTROL = 0x00020000,
            WRITE_DAC = 0x00040000,
            WRITE_OWNER = 0x00080000,
            SYNCHRONIZE = 0x00100000,

            STANDARD_RIGHTS_REQUIRED = 0x000f0000,

            STANDARD_RIGHTS_READ = 0x00020000,
            STANDARD_RIGHTS_WRITE = 0x00020000,
            STANDARD_RIGHTS_EXECUTE = 0x00020000,

            STANDARD_RIGHTS_ALL = 0x001f0000,

            SPECIFIC_RIGHTS_ALL = 0x0000ffff,

            ACCESS_SYSTEM_SECURITY = 0x01000000,

            MAXIMUM_ALLOWED = 0x02000000,

            GENERIC_READ = 0x80000000,
            GENERIC_WRITE = 0x40000000,
            GENERIC_EXECUTE = 0x20000000,
            GENERIC_ALL = 0x10000000,

            DESKTOP_READOBJECTS = 0x00000001,
            DESKTOP_CREATEWINDOW = 0x00000002,
            DESKTOP_CREATEMENU = 0x00000004,
            DESKTOP_HOOKCONTROL = 0x00000008,
            DESKTOP_JOURNALRECORD = 0x00000010,
            DESKTOP_JOURNALPLAYBACK = 0x00000020,
            DESKTOP_ENUMERATE = 0x00000040,
            DESKTOP_WRITEOBJECTS = 0x00000080,
            DESKTOP_SWITCHDESKTOP = 0x00000100,

            WINSTA_ENUMDESKTOPS = 0x00000001,
            WINSTA_READATTRIBUTES = 0x00000002,
            WINSTA_ACCESSCLIPBOARD = 0x00000004,
            WINSTA_CREATEDESKTOP = 0x00000008,
            WINSTA_WRITEATTRIBUTES = 0x00000010,
            WINSTA_ACCESSGLOBALATOMS = 0x00000020,
            WINSTA_EXITWINDOWS = 0x00000040,
            WINSTA_ENUMERATE = 0x00000100,
            WINSTA_READSCREEN = 0x00000200,

            WINSTA_ALL_ACCESS = 0x0000037f
        }

        [Flags]
        public enum WINDOWS_STATION_ACCESS_MASK : uint
        {
            WINSTA_NONE = 0,

            WINSTA_ENUMDESKTOPS = 0x0001,
            WINSTA_READATTRIBUTES = 0x0002,
            WINSTA_ACCESSCLIPBOARD = 0x0004,
            WINSTA_CREATEDESKTOP = 0x0008,
            WINSTA_WRITEATTRIBUTES = 0x0010,
            WINSTA_ACCESSGLOBALATOMS = 0x0020,
            WINSTA_EXITWINDOWS = 0x0040,
            WINSTA_ENUMERATE = 0x0100,
            WINSTA_READSCREEN = 0x0200,
        }

        [Flags]
        public enum ProcessCreationFlags : uint
        {
            ZERO_FLAG = 0x00000000,
            CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
            CREATE_DEFAULT_ERROR_MODE = 0x04000000,
            CREATE_NEW_CONSOLE = 0x00000010,
            CREATE_NEW_PROCESS_GROUP = 0x00000200,
            CREATE_NO_WINDOW = 0x08000000,
            CREATE_PROTECTED_PROCESS = 0x00040000,
            CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
            CREATE_SEPARATE_WOW_VDM = 0x00001000,
            CREATE_SHARED_WOW_VDM = 0x00001000,
            CREATE_SUSPENDED = 0x00000004,
            CREATE_UNICODE_ENVIRONMENT = 0x00000400,
            DEBUG_ONLY_THIS_PROCESS = 0x00000002,
            DEBUG_PROCESS = 0x00000001,
            DETACHED_PROCESS = 0x00000008,
            EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
            INHERIT_PARENT_AFFINITY = 0x00010000
        }

        [Flags]
        public enum LogonFlags
        {
            LOGON_WITH_PROFILE = 0x00000001,
            LOGON_NETCREDENTIALS_ONLY = 0x00000002
        }

        public enum LogonType
        {
            LOGON32_LOGON_INTERACTIVE = 2,
            LOGON32_LOGON_NETWORK = 3,
            LOGON32_LOGON_BATCH = 4,
            LOGON32_LOGON_SERVICE = 5,
            LOGON32_LOGON_UNLOCK = 7,
            LOGON32_LOGON_NETWORK_CLEARTEXT = 8,
            LOGON32_LOGON_NEW_CREDENTIALS = 9,
        }

        public enum LogonProvider
        {
            LOGON32_PROVIDER_DEFAULT = 0,
            LOGON32_PROVIDER_WINNT35 = 1,
            LOGON32_PROVIDER_WINNT40 = 2,
            LOGON32_PROVIDER_WINNT50 = 3
        }
        #endregion

        #region Functions
        [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LoadUserProfile(
            [In] IntPtr hToken,
            ref ProfileInfo lpProfileInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(
            int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcess(
            [In, Optional] string lpApplicationName,
            [In, Out, Optional] string lpCommandLine,
            [In, Optional] IntPtr lpProcessAttributes,
            [In, Optional] IntPtr lpThreadAttributes,
            [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            [In] ProcessCreationFlags dwCreationFlags,
            [In, Optional] string lpEnvironment,
            [In, Optional] string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            [Out] out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcessWithLogonW(
            [In] string lpUsername,
            [In, Optional] string lpDomain,
            [In] string lpPassword,
            [In] LogonFlags dwLogonFlags,
            [In] string lpApplicationName,
            [In, Out, Optional] string lpCommandLine,
            [In] ProcessCreationFlags dwCreationFlags,
            [In, Optional] string lpEnvironment,
            [In, Optional] string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            [Out] out PROCESS_INFORMATION lpProcessInfo);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcessAsUser(
            [In, Optional] IntPtr hToken,
            [In, Optional] string lpApplicationName,
            [In, Out, Optional] string lpCommandLine,
            [In, Optional] ref SECURITY_ATTRIBUTES lpProcessAttributes,
            [In, Optional] ref SECURITY_ATTRIBUTES lpThreadAttributes,
            [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            [In] ProcessCreationFlags dwCreationFlags,
            [In, Optional] string lpEnvironment,
            [In, Optional] string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            [Out] out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern uint ResumeThread(
            [In] IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern uint SuspendThread(
            [In] IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(
            [In] IntPtr handle);

        // ms-help://MS.VSCC.v80/MS.MSDN.v80/MS.WIN32COM.v10.en/dllproc/base/createdesktop.htm
        [DllImport("user32.dll", EntryPoint = "CreateDesktop", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateDesktop(
            [MarshalAs(UnmanagedType.LPWStr)] string desktopName,
            [MarshalAs(UnmanagedType.LPWStr)] string device, // must be null.
            [MarshalAs(UnmanagedType.LPWStr)] string deviceMode, // must be null,
            [MarshalAs(UnmanagedType.U4)] int flags,  // use 0
            [MarshalAs(UnmanagedType.U4)] ACCESS_MASK accessMask,
            [MarshalAs(UnmanagedType.LPStruct)] SecurityAttributes attributes);

        // ms-help://MS.VSCC.v80/MS.MSDN.v80/MS.WIN32COM.v10.en/dllproc/base/closedesktop.htm
        [DllImport("user32.dll", EntryPoint = "CloseDesktop", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseDesktop(
            IntPtr handle);

        [DllImport("user32", CharSet = CharSet.Unicode, SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseWindowStation(
            IntPtr hWinsta);

        [DllImport("user32.dll", EntryPoint = "CreateWindowStation", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateWindowStation(
                        [MarshalAs(UnmanagedType.LPWStr)] string name,
                        [MarshalAs(UnmanagedType.U4)] int reserved,      // must be zero.
                        [MarshalAs(UnmanagedType.U4)] WINDOWS_STATION_ACCESS_MASK desiredAccess,
                        [MarshalAs(UnmanagedType.LPStruct)] SecurityAttributes attributes);

        [DllImport("user32", SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        public static extern IntPtr OpenWindowStation(
            [MarshalAs(UnmanagedType.LPTStr)] string lpszWinSta,
            [MarshalAs(UnmanagedType.Bool)] bool fInherit,
            [MarshalAs(UnmanagedType.U4)] WINDOWS_STATION_ACCESS_MASK dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessWindowStation(
            IntPtr hWinSta);

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        [DllImport("user32", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetProcessWindowStation();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetUserObjectInformation(
            IntPtr hObj,
            int nIndex,
            [Out] byte[] pvInfo,
            uint nLength,
            out uint lpnLengthNeeded);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindowStations(
            EnumWindowStationsDelegate lpEnumFunc,
            IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr OpenThread(
            ThreadAccess dwDesiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            int dwThreadId);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LogonUser(
            [In] string userName,
            [In, Optional] string domain,
            [In, Optional] string password,
            [In] LogonType logonType,
            [In] LogonProvider logonProvider,
            [Out] out SafeTokenHandle token);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int DuplicateToken(
            IntPtr token,
            int impersonationLevel,
            ref IntPtr newToken);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RevertToSelf();

        [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern bool LoadUserProfile(
            IntPtr hToken,
            ref PROFILEINFO lpProfileInfo);

        [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern bool UnloadUserProfile(
            [In] System.IntPtr token,
            [In] System.IntPtr profile);

        [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern bool DeleteProfile(
            [In] string sidString,
            [In, Optional] string profilePath,
            [In, Optional] string computerName);

        // Required SE_RESTORE_NAME and SE_BACKUP_NAME 
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int RegUnLoadKey(
            [In] IntPtr hKey,
            [In, Optional] string lpSubKey);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern bool CreateEnvironmentBlock(
            [Out] out IntPtr lpEnvironment,
            [In, Optional] IntPtr hToken,
            [In, MarshalAs(UnmanagedType.Bool)] bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyEnvironmentBlock(
            IntPtr lpEnvironment);

        [DllImport("userenv.dll", CharSet = CharSet.Auto)]
        public static extern int CreateProfile(
            [MarshalAs(UnmanagedType.LPWStr)] string pszUserSid,
            [MarshalAs(UnmanagedType.LPWStr)] string pszUserName,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszProfilePath,
            uint cchProfilePath);

        [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern bool GetUserProfileDirectory(
            [In] IntPtr hToken,
            [Out, Optional] [MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpProfileDir,
            [In, Out] ref uint lpcchSize);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AdjustTokenPrivileges(
            IntPtr htok,
            [MarshalAs(UnmanagedType.Bool)] bool disall,
            ref TokPriv1Luid newst,
            int len,
            IntPtr prev,
            IntPtr relen);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool OpenProcessToken(
            IntPtr h,
            int acc,
            ref IntPtr phtok);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LookupPrivilegeValue(
            string host,
            string name,
            ref long pluid);
        #endregion

        #region Structures
        [StructLayout(LayoutKind.Sequential)]
        public struct PROFILEINFO
        {
            public int dwSize;

            public int dwFlags;

            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpUserName;

            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpProfilePath;

            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpDefaultPath;

            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpServerName;

            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpPolicyPath;

            public IntPtr hProfile;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ProfileInfo
        {
            /// 
            /// Specifies the size of the structure, in bytes.
            /// 
            public int dwSize;

            /// 
            /// This member can be one of the following flags: PI_NOUI or PI_APPLYPOLICY
            /// 
            public int dwFlags;

            /// 
            /// Pointer to the name of the user. 
            /// This member is used as the base name of the directory in which to store a new profile. 
            /// 
            [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpUserName;

            /// 
            /// Pointer to the roaming user profile path. 
            /// If the user does not have a roaming profile, this member can be NULL.
            /// 
            [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpProfilePath;

            /// 
            /// Pointer to the default user profile path. This member can be NULL. 
            /// 
            [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpDefaultPath;

            /// 
            /// Pointer to the name of the validating domain controller, in NetBIOS format. 
            /// If this member is NULL, the Windows NT 4.0-style policy will not be applied. 
            /// 
            [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpServerName;

            /// 
            /// Pointer to the path of the Windows NT 4.0-style policy file. This member can be NULL. 
            /// 
            [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpPolicyPath;

            /// 
            /// Handle to the HKEY_CURRENT_USER registry key. 
            /// 
            public IntPtr hProfile;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct TokPriv1Luid
        {
            public int Count;
            public long Luid;
            public int Attr;
        }
        #endregion

        #region Classes
        [StructLayout(LayoutKind.Sequential)]
        public class SecurityAttributes
        {
            #region Struct members
            [MarshalAs(UnmanagedType.U4)]
            private int mStructLength;

            private IntPtr mSecurityDescriptor;

            [MarshalAs(UnmanagedType.U4)]
            private bool mInheritHandle;
            #endregion

            public SecurityAttributes()
            {
                this.mStructLength = Marshal.SizeOf(typeof(SecurityAttributes));
                this.mSecurityDescriptor = IntPtr.Zero;
            }

            public IntPtr SecurityDescriptor
            {
                get { return this.mSecurityDescriptor; }
                set { this.mSecurityDescriptor = value; }
            }

            public bool Inherit
            {
                get { return this.mInheritHandle; }
                set { this.mInheritHandle = value; }
            }
        }
        #endregion
    }
}
