using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HP.WindowsPrison.ComWrapper
{
    [ComVisible(true)]
    public interface IProcessTracker
    {
        [ComVisible(true)]
        int Pid
        {
            get;
        }

        [ComVisible(true)]
        int ExitCode
        {
            get;
        }

        [ComVisible(true)]
        bool HasExited();

        [ComVisible(true)]
        void Wait();
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class ProcessTracker : IProcessTracker
    {
        private System.Diagnostics.Process sysProc;

        public ProcessTracker()
        {
        }

        public int Pid
        {
            get
            {
                return this.sysProc.Id;
            }
        }
        public int ExitCode
        {
            get
            {
                return this.sysProc.ExitCode;
            }
        }

        public bool HasExited()
        {
            return this.sysProc.HasExited;
        }

        public void Wait()
        {
            this.sysProc.WaitForExit();
        }

        public ProcessTracker(System.Diagnostics.Process process)
        {
            this.sysProc = process;
        }

    }
}
