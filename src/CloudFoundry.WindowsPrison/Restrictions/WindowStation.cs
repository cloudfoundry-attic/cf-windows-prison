namespace CloudFoundry.WindowsPrison.Restrictions
{
    using CloudFoundry.WindowsPrison.Native;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Runtime.InteropServices;

    class WindowStation : Rule
    {
        private static readonly object windowStationContextLock = new object();
        IntPtr windowStation = IntPtr.Zero;

        public override RuleTypes RuleType
        {
            get
            {
                return RuleTypes.WindowStation;
            }
        }

        public override void Apply(Prison prison)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            if (this.windowStation != IntPtr.Zero)
            {
                return;
            }

            NativeMethods.SECURITY_ATTRIBUTES secAttributes = new NativeMethods.SECURITY_ATTRIBUTES();
            secAttributes.nLength = Marshal.SizeOf(secAttributes);

            this.windowStation = OpenWindowStation(prison.User.UserName);

            if (this.windowStation == IntPtr.Zero)
            {
                // TODO SECURITY: change security attributes. the default will give everyone access to the object including other prisons
                this.windowStation = CreateWindowStation(prison.User.UserName);
            }

            lock (windowStationContextLock)
            {
                IntPtr currentWindowStation = GetProcessWindowStation();

                try
                {
                    SetProcessWindowStation(this.windowStation);

                    // TODO SECURITY: change security attributes. the default will give everyone access to the object including other prisons
                    CreateDesktop();

                    prison.desktopName = string.Format(CultureInfo.InvariantCulture, @"{0}\Default", prison.User.UserName);
                }
                finally
                {
                    SetProcessWindowStation(currentWindowStation);
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
                    return false;
                }

                list.Add(windowStation);

                return true;
            });

            IList<string> workstationList = EnumWindowsStations(childProc);

            foreach (string workstation in workstationList)
            {
                result.Add(new RuleInstanceInfo() { Name = workstation });
            }

            return result.ToArray();
        }

        public override void Init()
        {
        }

        public override void Recover(Prison prison)
        {
        }

        private static IList<string> EnumWindowsStations(NativeMethods.EnumWindowStationsDelegate childProc)
        {
            IList<string> workstationList = new List<string>();
            GCHandle gcHandle = GCHandle.Alloc(workstationList);
            if (!NativeMethods.EnumWindowStations(childProc, GCHandle.ToIntPtr(gcHandle)))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return workstationList;
        }

        private static IntPtr OpenWindowStation(string username)
        {
            IntPtr windowStation = NativeMethods.OpenWindowStation(username, false, NativeMethods.WINDOWS_STATION_ACCESS_MASK.WINSTA_CREATEDESKTOP);

            int openWinStaStatus = Marshal.GetLastWin32Error();

            // Error 0x2 is ERROR_FILE_NOT_FOUND
            // http://msdn.microsoft.com/en-us/library/windows/desktop/ms681382%28v=vs.85%29.aspx
            if (windowStation == IntPtr.Zero && openWinStaStatus != 0x2)
            {
                throw new Win32Exception(openWinStaStatus);
            }

            return windowStation;
        }

        private static IntPtr CreateWindowStation(string username)
        {
            IntPtr windowStation = NativeMethods.CreateWindowStation(username, 0, NativeMethods.WINDOWS_STATION_ACCESS_MASK.WINSTA_CREATEDESKTOP, null);

            int createWinStaStatus = Marshal.GetLastWin32Error();

            if (windowStation == IntPtr.Zero)
            {
                throw new Win32Exception(createWinStaStatus);
            }

            return windowStation;
        }

        private static void SetProcessWindowStation(IntPtr windowStation)
        {
            if (!NativeMethods.SetProcessWindowStation(windowStation))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private static IntPtr GetProcessWindowStation()
        {
            return NativeMethods.GetProcessWindowStation();
        }

        private static IntPtr CreateDesktop()
        {
            IntPtr desktop = NativeMethods.CreateDesktop("Default", null, null, 0, NativeMethods.ACCESS_MASK.DESKTOP_CREATEWINDOW, null);
            int createDesktopStatus = Marshal.GetLastWin32Error();

            if (desktop == IntPtr.Zero)
            {
                throw new Win32Exception(createDesktopStatus);
            }

            return desktop;
        }
    }
}
