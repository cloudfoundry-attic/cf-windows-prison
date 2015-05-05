namespace HP.WindowsPrison
{
    using HP.WindowsPrison.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Pipes;
    using System.Management;
    using System.Security.Principal;

    public class Prison : PrisonModel, IDisposable
    {
        private bool disposed = false;

        static Type[] ruleTypes = new Type[] 
        {
            typeof(Restrictions.CPU),
            typeof(Restrictions.Disk),
            typeof(Restrictions.Filesystem),
            typeof(Restrictions.Memory),
            typeof(Restrictions.Network),
            typeof(Restrictions.WindowStation),
            typeof(Allowances.IISGroup),
            typeof(Allowances.Httpsys),
        };

        internal List<Rule> prisonCells = null;
        internal HP.WindowsPrison.Utilities.WindowsJobObjects.JobObject jobObject = null;
        private static volatile bool wasInitialized = false;

        public Prison()
            : this(Guid.NewGuid())
        {
        }

        public Prison(Guid id)
        {
            this.Id = id;
            this.prisonCells = new List<Rule>();
            this.PrisonExecutor = new PrisonExecutor(this);
            this.PrisonGuard = new PrisonGuard(this);
        }

        internal PrisonExecutor PrisonExecutor
        {
            get;
            private set;
        }

        internal PrisonGuard PrisonGuard
        {
            get;
            private set;
        }

        public HP.WindowsPrison.Utilities.WindowsJobObjects.JobObject JobObject
        {
            get
            {
                return this.jobObject;
            }
        }

        public string PrisonHomePath
        {
            get
            {
                return Path.Combine(this.Configuration.PrisonHomeRootPath, this.Id.ToString("N"));
            }
        }

        internal bool RuleEnabled(RuleTypes cellTypeQuery)
        {
            return ((this.Configuration.Rules & cellTypeQuery) == cellTypeQuery) || ((this.Configuration.Rules & RuleTypes.All) == RuleTypes.All);
        }

        public void Reattach()
        {
            this.prisonCells = new List<Rule>();

            if (this.User != null && !string.IsNullOrWhiteSpace(this.User.UserName))
            {
                Logger.Debug("Prison {0} is attaching to Job Object {1}", this.Id, this.User.UserName);

                this.InitializeJobObject();
            }
            else
            {
                Logger.Debug("Prison {0} has no Job Object to attach to", this.Id);
            }

            if (Configuration.Rules != RuleTypes.None)
            {
                foreach (Type cellType in ruleTypes)
                {
                    Rule cell = (Rule)cellType.GetConstructor(Type.EmptyTypes).Invoke(null);
                    if (this.RuleEnabled(cell.RuleType))
                    {
                        this.prisonCells.Add(cell);
                    }
                }
            }

            foreach (Rule cell in this.prisonCells)
            {
                cell.Recover(this);
            }
        }

        public void Lockdown(PrisonConfiguration rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException("rules");
            }

            if (!Prison.wasInitialized)
            {
                throw new InvalidOperationException("Prison environment has not been initialized. Call Prison.Init to initialize.");
            }

            if (this.IsLocked)
            {
                throw new InvalidOperationException("This prison is already locked.");
            }

            Logger.Debug("Locking down prison {0}", this.Id);

            this.Configuration = rules;

            Directory.CreateDirectory(this.PrisonHomePath);

            if (rules.Rules != RuleTypes.None)
            {
                foreach (Type cellType in ruleTypes)
                {
                    Rule cell = (Rule)cellType.GetConstructor(Type.EmptyTypes).Invoke(null);
                    if (this.RuleEnabled(cell.RuleType))
                    {
                        this.prisonCells.Add(cell);
                    }
                }
            }

            // Create the Windows User
            this.User = new PrisonUser(this.Tag);
            this.User.Create();

            // InitializeSystemVirtualAddressSpaceQuotas();

            this.InitializeJobObject();

            // Lock all cells
            foreach (Rule cell in this.prisonCells)
            {
                if (cell.RuleType != RuleTypes.WindowStation)
                {
                    cell.Apply(this);
                }
            }

            this.User.Profile.CreateUserProfile();
            string customProfilePath = Path.Combine(this.PrisonHomePath, "profile");
            this.User.Profile.ChangeProfilePath(customProfilePath);

            // TODO: is guard supposed to run?
            // RunGuard();

            this.IsLocked = true;

            this.Save();
        }

        public Process Execute(string fileName)
        {
            return this.Execute(fileName, string.Empty);
        }

        public Process Execute(string fileName, string arguments)
        {
            return this.Execute(fileName, arguments, false);
        }

        public Process Execute(string fileName, string arguments, bool interactive)
        {
            return this.Execute(fileName, arguments, interactive, null);
        }

        public Process Execute(string fileName, string arguments, bool interactive, Dictionary<string, string> extraEnvironmentVariables)
        {
            return this.Execute(fileName, arguments, null, interactive, extraEnvironmentVariables, null, null, null);
        }

        public Process Execute(string fileName, string arguments, string currentDirectory, bool interactive, Dictionary<string, string> extraEnvironmentVariables, PipeStream stdinPipeName, PipeStream stdoutPipeName, PipeStream stderrPipeName)
        {
            return PrisonExecutor.Execute(fileName, arguments, currentDirectory, interactive, extraEnvironmentVariables, stdinPipeName, stdoutPipeName, stderrPipeName);
        }

        public void Destroy()
        {
            if (this.IsLocked)
            {
                Logger.Debug("Destroying prison {0}", this.Id);

                foreach (var cell in this.prisonCells)
                {
                    cell.Destroy(this);
                }

                this.jobObject.TerminateProcesses(-1);

                this.jobObject.Dispose();
                this.jobObject = null;

                // TODO: Should destroy delete the home directory???
                // Directory.CreateDirectory(prisonRules.PrisonHomePath);

                this.PrisonGuard.TryStopGuard();
                this.User.Profile.UnloadUserProfileUntilReleased();
                this.User.Profile.DeleteUserProfile();
                this.User.Delete();

                this.SystemRemoveQuota();
            }

            this.DeletePersistedPrison();

            this.IsLocked = false;
        }

        public static void Init()
        {
            if (!Prison.wasInitialized)
            {
                Prison.wasInitialized = true;

                foreach (Type cellType in ruleTypes)
                {
                    Rule cell = (Rule)cellType.GetConstructor(Type.EmptyTypes).Invoke(null);
                    cell.Init();
                }
            }
        }

        public static Dictionary<RuleTypes, RuleInstanceInfo[]> ListCellInstances()
        {
            Dictionary<RuleTypes, RuleInstanceInfo[]> result = new Dictionary<RuleTypes, RuleInstanceInfo[]>();

            foreach (Type cellType in ruleTypes)
            {
                Rule cell = (Rule)cellType.GetConstructor(Type.EmptyTypes).Invoke(null);
                result[cell.RuleType] = cell.List();
            }

            return result;
        }

        private void Save()
        {
            PrisonManager.Save(this);
        }

        private void DeletePersistedPrison()
        {
            PrisonManager.DeletePersistedPrison(this);
        }

        private void InitializeSystemVirtualAddressSpaceQuotas()
        {
            if (this.Configuration.TotalPrivateMemoryLimitBytes > 0)
            {
                SystemVirtualAddressSpaceQuotas.SetPagedPoolQuota(
                    this.Configuration.TotalPrivateMemoryLimitBytes, new SecurityIdentifier(this.User.UserSID));

                SystemVirtualAddressSpaceQuotas.SetNonPagedPoolQuota(
                    this.Configuration.TotalPrivateMemoryLimitBytes, new SecurityIdentifier(this.User.UserSID));

                SystemVirtualAddressSpaceQuotas.SetPagingFileQuota(
                    this.Configuration.TotalPrivateMemoryLimitBytes, new SecurityIdentifier(this.User.UserSID));

                SystemVirtualAddressSpaceQuotas.SetWorkingSetPagesQuota(
                    this.Configuration.TotalPrivateMemoryLimitBytes, new SecurityIdentifier(this.User.UserSID));
            }
        }

        private void InitializeJobObject()
        {
            if (this.jobObject != null)
            {
                return;
            }

            // Create the JobObject
            this.jobObject = new HP.WindowsPrison.Utilities.WindowsJobObjects.JobObject("Global\\" + this.User.UserName);

            if (this.Configuration.CPUPercentageLimit > 0)
            {
                this.JobObject.CPUPercentageLimit = this.Configuration.CPUPercentageLimit;
            }

            if (this.Configuration.TotalPrivateMemoryLimitBytes > 0)
            {
                this.JobObject.JobMemoryLimitBytes = this.Configuration.TotalPrivateMemoryLimitBytes;
            }

            if (this.Configuration.ActiveProcessesLimit > 0)
            {
                this.JobObject.ActiveProcessesLimit = this.Configuration.ActiveProcessesLimit;
            }

            if (this.Configuration.PriorityClass.HasValue)
            {
                this.JobObject.PriorityClass = this.Configuration.PriorityClass.Value;
            }

            this.jobObject.KillProcessesOnJobClose = true;
        }

        private static Process[] GetChildProcesses(int parentId)
        {
            var result = new List<Process>();

            var query = "Select * From Win32_Process Where ParentProcessId = " + parentId;

            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            {
                using (ManagementObjectCollection processList = searcher.Get())
                {
                    foreach (var i in processList)
                    {
                        var pid = Convert.ToInt32(i.GetPropertyValue("ProcessId"), CultureInfo.InvariantCulture);
                        result.Add(Process.GetProcessById(pid));
                    }
                }
            }

            return result.ToArray();
        }

        private void SystemRemoveQuota()
        {
            SystemVirtualAddressSpaceQuotas.RemoveQuotas(new SecurityIdentifier(this.User.UserSID));
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="Prison"/> class.
        /// </summary>
        ~Prison()
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

            if (this.jobObject != null)
            {
                this.jobObject.Dispose();
            }

            this.disposed = true;
        }
    }
}
