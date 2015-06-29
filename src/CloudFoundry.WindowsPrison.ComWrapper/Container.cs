using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CloudFoundry.WindowsPrison.ComWrapper
{
    [ComVisible(true)]
    public interface IContainer
    {
        [ComVisible(true)]
        string Id { get; }

        [ComVisible(true)]
        string HomePath { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1406:AvoidInt64ArgumentsForVB6Clients", Justification = "Compatibility with VB6 is not required"),
        ComVisible(true)]
        long MemoryLimitBytes { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1406:AvoidInt64ArgumentsForVB6Clients", Justification = "Compatibility with VB6 is not required"),
        ComVisible(true)]
        long DiskLimitBytes { get; set; }

        [ComVisible(true)]
        int NetworkPort { get; set; }

        [ComVisible(true)]
        bool IsLockedDown();

        [ComVisible(true)]
        void Lockdown();

        [ComVisible(true)]
        IProcessTracker Run(IContainerRunInfo runInfo);

        [ComVisible(true)]
        void Terminate();

        [ComVisible(true)]
        void Destroy();
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class Container : IContainer, IDisposable
    {
        private bool disposed = false;
        private Prison prison;


        public string Id { get; private set; }

        public string HomePath { get; set; }

        public long MemoryLimitBytes { get; set; }

        public long DiskLimitBytes { get; set; }

        public int NetworkPort { get; set; }

        public bool IsLockedDown()
        {
            return prison.IsLocked;
        }

        public Container()
        {
            try
            {
                this.prison = new Prison();
                this.prison.Tag = "uward";
                this.Id = prison.Id.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error("Prison (COM) error on Container.Constructor: {0}", ex);
                throw;
            }
        }

        public Container(Prison prison)
        {
            try
            {
                if (prison == null)
                {
                    throw new ArgumentNullException("prison");
                }

                this.prison = prison;
                this.Id = prison.Id.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error("Prison (COM) error on Container.Constructor(prison): {0}", ex);
                throw;
            }
        }

        public void Lockdown()
        {
            try
            {
                PrisonConfiguration prisonRules = new PrisonConfiguration();
                prisonRules.Rules = RuleTypes.None;
                prisonRules.PrisonHomeRootPath = this.HomePath;
                prisonRules.Rules |= RuleTypes.WindowStation;
                if (this.MemoryLimitBytes > 0)
                {
                    prisonRules.Rules |= RuleTypes.Memory;
                    prisonRules.TotalPrivateMemoryLimitBytes = this.MemoryLimitBytes;
                }

                if (this.DiskLimitBytes > 0)
                {
                    prisonRules.Rules |= RuleTypes.Disk;
                    prisonRules.DiskQuotaBytes = this.DiskLimitBytes;
                }
                if (this.NetworkPort > 0)
                {
                    prisonRules.Rules |= RuleTypes.Httpsys;
                    prisonRules.UrlPortAccess = this.NetworkPort;
                }

                prison.Lockdown(prisonRules);
            }
            catch (Exception ex)
            {
                Logger.Error("Prison (COM) error on Container.Lockdown: {0}", ex);
                throw;
            }
        }

        public IProcessTracker Run(IContainerRunInfo runInfo)
        {
            try
            {
                if (runInfo == null)
                {
                    throw new ArgumentNullException("runInfo");
                }

                var process = this.prison.Execute(runInfo.FileName, runInfo.Arguments, runInfo.CurrentDirectory, false, runInfo.ExtraEnvironmentVariables, runInfo.StdinPipe, runInfo.StdoutPipe, runInfo.StderrPipe);

                if (runInfo.StdinPipe != null) runInfo.StdinPipe.Dispose();
                if (runInfo.StdoutPipe != null) runInfo.StdoutPipe.Dispose();
                if (runInfo.StderrPipe != null) runInfo.StderrPipe.Dispose();

                return new ProcessTracker(process);
            }
            catch (Exception ex)
            {
                Logger.Error("Prison (COM) error on Container.Run: {0}", ex);
                throw;
            }
        }

        public void Terminate()
        {
            try
            {
                this.prison.JobObject.TerminateProcesses(-1);
            }
            catch (Exception ex)
            {
                Logger.Error("Prison (COM) error on Container.Terminate: {0}", ex);
                throw;
            }
        }

        public void Destroy()
        {
            try
            {
                this.prison.Destroy();
            }
            catch (Exception ex)
            {
                Logger.Error("Prison (COM) error on Container.Destroy: {0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="Container"/> class.
        /// </summary>
        ~Container()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (this.prison != null)
            {
                this.prison.Dispose();
            }

            this.disposed = true;
        }
    }
}
