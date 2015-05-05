namespace CloudFoundry.WindowsPrison.Utilities
{
    using CloudFoundry.WindowsPrison.Native;
    using System;

    public static class PrivilegeAdjust
    {
        ////// "SeAssignPrimaryTokenPrivilege", "SeAuditPrivilege", "SeBackupPrivilege",
        ////// "SeChangeNotifyPrivilege", "SeCreateGlobalPrivilege", "SeCreatePagefilePrivilege",
        ////// "SeCreatePermanentPrivilege", "SeCreateSymbolicLinkPrivilege", "SeCreateTokenPrivilege",
        ////// "SeDebugPrivilege", "SeEnableDelegationPrivilege", "SeImpersonatePrivilege", "SeIncreaseBasePriorityPrivilege",
        ////// "SeIncreaseQuotaPrivilege", "SeIncreaseWorkingSetPrivilege", "SeLoadDriverPrivilege",
        ////// "SeLockMemoryPrivilege", "SeMachineAccountPrivilege", "SeManageVolumePrivilege",
        ////// "SeProfileSingleProcessPrivilege", "SeRelabelPrivilege", "SeRemoteShutdownPrivilege",
        ////// "SeRestorePrivilege", "SeSecurityPrivilege", "SeShutdownPrivilege", "SeSyncAgentPrivilege",
        ////// "SeSystemEnvironmentPrivilege", "SeSystemProfilePrivilege", "SeSystemtimePrivilege",
        ////// "SeTakeOwnershipPrivilege", "SeTcbPrivilege", "SeTimeZonePrivilege", "SeTrustedCredManAccessPrivilege",
        ////// "SeUndockPrivilege", "SeUnsolicitedInputPrivilege"

        public static bool EnablePrivilege(IntPtr processHandle, string privilege, bool disable)
        {
            bool retVal;
            NativeMethods.TokPriv1Luid tp;
            IntPtr htok = IntPtr.Zero;
            retVal = NativeMethods.OpenProcessToken(processHandle, NativeMethods.TOKEN_ADJUST_PRIVILEGES | NativeMethods.TOKEN_QUERY, ref htok);
            tp.Count = 1;
            tp.Luid = 0;
            if (disable)
            {
                tp.Attr = NativeMethods.SE_PRIVILEGE_DISABLED;
            }
            else
            {
                tp.Attr = NativeMethods.SE_PRIVILEGE_ENABLED;
            }

            retVal = NativeMethods.LookupPrivilegeValue(null, privilege, ref tp.Luid);
            retVal = NativeMethods.AdjustTokenPrivileges(htok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);

            return retVal;
        }
    }
}