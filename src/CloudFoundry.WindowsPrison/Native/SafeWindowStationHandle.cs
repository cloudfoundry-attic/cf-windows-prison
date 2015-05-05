namespace CloudFoundry.WindowsPrison.Native
{
    using Microsoft.Win32.SafeHandles;

    public sealed class SafeWindowStationHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeWindowStationHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseWindowStation(this.handle);
        }
    }
}
