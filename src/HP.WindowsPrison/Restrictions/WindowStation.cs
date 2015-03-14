using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HP.WindowsPrison.Restrictions
{
    class WindowStation : Rule
    {
        private static readonly object windowStationContextLock = new object();
        IntPtr windowStation = IntPtr.Zero;
        IntPtr desktop = IntPtr.Zero;

        public override void Apply(Prison prison)
        {
            if (windowStation != IntPtr.Zero) return;

            NativeMethods.SECURITY_ATTRIBUTES secAttributes = new NativeMethods.SECURITY_ATTRIBUTES();
            secAttributes.nLength = Marshal.SizeOf(secAttributes);


            windowStation = NativeOpenWindowStation(prison.User.Username);

            int openWinStaStatus = Marshal.GetLastWin32Error();

            // Error 0x2 is ERROR_FILE_NOT_FOUND
            // http://msdn.microsoft.com/en-us/library/windows/desktop/ms681382%28v=vs.85%29.aspx
            if (windowStation == IntPtr.Zero && openWinStaStatus != 0x2)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (windowStation == IntPtr.Zero &&  openWinStaStatus == 0x2)
            {
                // TODO SECURITY: change security attributes. the default will give everyone access to the object including other prisons
                windowStation = NativeCreateWindowStation(prison.User.Username);

                if (windowStation == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }

            lock (windowStationContextLock)
            {
                IntPtr currentWindowStation = NativeGetProcessWindowStation();

                try
                {
                    bool setOk = NativeSetProcessWindowStation(windowStation);

                    if (!setOk)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    // TODO SECURITY: change security attributes. the default will give everyone access to the object including other prisons
                    desktop = NativeCreateDesktop();

                    if (desktop == IntPtr.Zero)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    prison.desktopName = string.Format(@"{0}\Default", prison.User.Username);
                }
                finally
                {
                    NativeSetProcessWindowStation(currentWindowStation);
                }
            }
        }

        public override void Destroy(Prison prison)
        {
            
        }

        public override RuleInstanceInfo[] List()
        {
            List<RuleInstanceInfo> result = new List<RuleInstanceInfo>();

            NativeMethods.EnumWindowStationsDelegate childProc = new NativeMethods.EnumWindowStationsDelegate((windowStation, lParam) =>
            {
                GCHandle gch = GCHandle.FromIntPtr(lParam);
                IList<string> list = gch.Target as List<string>;

                if (null == list)
                {
                    return (false);
                }

                list.Add(windowStation);

                return (true);
            });

            IList<string> workstationList = new List<string>();

            GCHandle gcHandle = GCHandle.Alloc(workstationList);
            NativeEnumWindowsStations(childProc, gcHandle);

            foreach (string workstation in workstationList)
            {
                result.Add(new RuleInstanceInfo() { Name = workstation });
            }

            return result.ToArray();
        }

        public override void Init()
        {
        }

        public override RuleType GetFlag()
        {
            return RuleType.WindowStation;
        }

        public override void Recover(Prison prison)
        {
        }

        private static bool NativeEnumWindowsStations(NativeMethods.EnumWindowStationsDelegate childProc, GCHandle gcHandle)
        {
            return NativeMethods.EnumWindowStations(childProc, GCHandle.ToIntPtr(gcHandle));
        }

        private static IntPtr NativeOpenWindowStation(string username)
        {
            return NativeMethods.OpenWindowStation(username, false, NativeMethods.WINDOWS_STATION_ACCESS_MASK.WINSTA_CREATEDESKTOP);
        }

        private static IntPtr NativeCreateWindowStation(string username)
        {
            return NativeMethods.CreateWindowStation(username, 0, NativeMethods.WINDOWS_STATION_ACCESS_MASK.WINSTA_CREATEDESKTOP, null);
        }

        private static bool NativeSetProcessWindowStation(IntPtr windowStation)
        {
            return NativeMethods.SetProcessWindowStation(windowStation);
        }

        private static IntPtr NativeGetProcessWindowStation()
        {
            return NativeMethods.GetProcessWindowStation();
        }

        private static IntPtr NativeCreateDesktop()
        {
            return NativeMethods.CreateDesktop("Default", null, null, 0, NativeMethods.ACCESS_MASK.DESKTOP_CREATEWINDOW, null);
        }

    }
}

