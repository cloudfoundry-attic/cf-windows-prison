using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
//using ProcessPrivileges;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HP.WindowsPrison.ExecutorService;
using HP.WindowsPrison.Restrictions;
using HP.WindowsPrison.Utilities;
using HP.WindowsPrison.Utilities.WindowsJobObjects;
using System.Globalization;

namespace HP.WindowsPrison
{
    public class Prison : PrisonModel, IDisposable
    {
        private bool disposed = false;

        static Type[] ruleTypes = new Type[]{
            typeof(Restrictions.CPU),
            typeof(Restrictions.Disk),
            typeof(Restrictions.Filesystem),
            typeof(Restrictions.Memory),
            typeof(Restrictions.Network),
            typeof(Restrictions.WindowStation),
            typeof(Allowances.IISGroup),
            typeof(Allowances.Httpsys),
        };

        static private string guardSuffix = "-guard";
        private const int checkGuardRetries = 200;
        List<Rule> prisonCells = null;
        JobObject jobObject = null;
        private static volatile bool wasInitialized = false;
        public const string ChangeSessionBaseEndpointAddress = @"net.pipe://localhost/HP.WindowsPrison.ExecutorService/Executor";
        private static string installUtilPath = Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "InstallUtil.exe");

        public JobObject JobObject
        {
            get 
            { 
                return jobObject; 
            }
        }

        public Prison()
            : this(Guid.NewGuid())
        {
        }

        public Prison(Guid id)
        {
            this.Id = id;
            this.prisonCells = new List<Rule>();

            this.Save();
        }

        public string PrisonHomePath
        {
            get
            {
                return Path.Combine(this.Configuration.PrisonHomeRootPath, this.Id.ToString("N"));
            }
        }

        private bool RuleEnabled(RuleTypes cellTypeQuery)
        {
            return ((this.Configuration.Rules & cellTypeQuery) == cellTypeQuery) || ((this.Configuration.Rules & RuleTypes.All) == RuleTypes.All);
        }

        public void Reattach()
        {
            prisonCells = new List<Rule>();

            if (this.User != null && !string.IsNullOrWhiteSpace(this.User.UserName))
            {
                Logger.Debug("Prison {0} is attaching to Job Object {1}", this.Id, this.User.UserName);

                InitializeJobObject();
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
                    if (RuleEnabled(cell.RuleType))
                    {
                        prisonCells.Add(cell);
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
                    if (RuleEnabled(cell.RuleType))
                    {
                        prisonCells.Add(cell);
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

        private void CheckGuard()
        {
            using (var guardJob = JobObject.Attach("Global\\" + this.User.UserName + Prison.guardSuffix))
            {
            }
        }

        private void InitializeGuard()
        {
            try
            {
                CheckGuard();
            }
            catch (Win32Exception)
            {
                RunGuard();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", 
            Justification = "Exception is rethrown in an AggregateException")]
        private Process RunGuard()
        {
            var psi = new ProcessStartInfo();
            int retryCount = 0;
            // this is set to true to prevent HANDLE inheritance 
            // http://stackoverflow.com/questions/10638901/create-a-process-and-redirect-its-input-output-and-dont-inherit-socket-handles
            psi.UseShellExecute = true;

            psi.ErrorDialog = false;
            psi.CreateNoWindow = true;

            // TODO: rename TotalPrivateMemoryLimitBytes to a more general term
            psi.FileName = GetGuardPath();
            psi.Arguments = this.User.UserName + " " + this.Configuration.TotalPrivateMemoryLimitBytes;

            var gp = Process.Start(psi);

            List<Exception> startErrors = new List<Exception>();

            // Wait for guard to start
            while (true)
            {
                try
                {
                    retryCount++;
                    CheckGuard();
                    break;
                }
                catch (Exception ex)
                {
                    startErrors.Add(ex);
                }

                if (retryCount == checkGuardRetries)
                {
                    throw new PrisonException("Maximum start prison guard retries exceeded",
                        new AggregateException(startErrors));
                }

                Thread.Sleep(100);
            }

            return gp;
        }

        private void AddProcessToGuardJobObject(Process p)
        {
            this.InitializeGuard();

            // Careful to close the Guard Job Object, 
            // else it is not guaranteed that the Job Object will not terminate if the Guard exists

            using (var guardJob = JobObject.Attach("Global\\" + this.User.UserName + Prison.guardSuffix))
            {
                guardJob.AddProcess(p);
            }

            this.CheckGuard();
        }

        private void TryStopGuard()
        {
            EventWaitHandle dischargeEvent = null;
            EventWaitHandle.TryOpenExisting("Global\\" + "discharge-" + this.User.UserName, out dischargeEvent);

            if (dischargeEvent != null)
            {
                dischargeEvent.Set();
            }
        }



        private static void InstallService(string servicePath, Dictionary<string, string> parameters)
        {
            string installCmd = installUtilPath;

            foreach (var p in parameters)
            {
                installCmd += " /" + p.Key + "=" + p.Value;
            }

            installCmd += " " + '"' + servicePath + '"';

            var res = Utilities.Command.ExecuteCommand(installCmd);

            if (res != 0)
            {
                throw new PrisonException("Error installing service {0}, exit code: {1}", installCmd, res);
            }
        }

        private static void UninstallService(string servicePath, Dictionary<string, string> parameters)
        {
            string installCmd = installUtilPath;
            installCmd += " /u ";
            foreach (var p in parameters)
            {
                installCmd += " /" + p.Key + "=" + p.Value;
            }

            installCmd += " " + '"' + servicePath + '"';

            var res = Utilities.Command.ExecuteCommand(installCmd);

            if (res != 0)
            {
                throw new PrisonException("Error installing service {0}, exit code: {1}", installCmd, res);
            }
        }

        private static void InitChangeSessionService(string id)
        {
            InstallService(GetChangeSessionServicePath(), new Dictionary<string, string>() { { "service-id", id } });
            Utilities.Command.ExecuteCommand("net start ChangeSession-" + id);
        }

        private static void RemoveChangeSessionService(string id)
        {
            Utilities.Command.ExecuteCommand("net stop ChangeSession-" + id);
            UninstallService(GetChangeSessionServicePath(), new Dictionary<string, string>() { { "service-id", id } });
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
            if (GetCurrentSessionId() == 0)
            {
                var workerProcess = InitializeProcess(fileName, arguments, currentDirectory, interactive, extraEnvironmentVariables, stdinPipeName, stdoutPipeName, stderrPipeName);
                ResumeProcess(workerProcess);
                return workerProcess;
            }
            else
            {
                var workerProcess = InitializeProcessWithChangedSession(fileName, arguments, currentDirectory, extraEnvironmentVariables, stdinPipeName, stdoutPipeName, stderrPipeName);
                return workerProcess;
            }
        }

        public Process InitializeProcessWithChangedSession(string fileName, string arguments, string currentDirectory, Dictionary<string, string> extraEnvironmentVariables, PipeStream stdinPipeName, PipeStream stdoutPipeName, PipeStream stderrPipeName)
        {
            string tempSeriviceId = Guid.NewGuid().ToString();
            InitChangeSessionService(tempSeriviceId);

            var bind = new NetNamedPipeBinding();
            bind.Security.Mode = NetNamedPipeSecurityMode.Transport;
            bind.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;

            var endpoint = new EndpointAddress(ChangeSessionBaseEndpointAddress + "/" + tempSeriviceId);

            using (var channelFactory = new ChannelFactory<IExecutor>(bind, endpoint))
            {
                IExecutor remoteSessionExec = channelFactory.CreateChannel();

                var workingProcessId = remoteSessionExec.ExecuteProcess(this, fileName, arguments, currentDirectory, extraEnvironmentVariables, stdinPipeName, stdoutPipeName, stderrPipeName);
                var workingProcess = Process.GetProcessById(workingProcessId);
                workingProcess.EnableRaisingEvents = true;

                CloseRemoteSession(remoteSessionExec);

                ResumeProcess(workingProcess);

                RemoveChangeSessionService(tempSeriviceId);

                return workingProcess;
            }
        }

        public Process InitializeProcess(string fileName, string arguments, string currentDirectory, bool interactive, Dictionary<string, string> extraEnvironmentVariables, PipeStream stdinPipeName, PipeStream stdoutPipeName, PipeStream stderrPipeName)
        {
            // C with Win32 API example to start a process under a different user: http://msdn.microsoft.com/en-us/library/aa379608%28VS.85%29.aspx

            if (!this.IsLocked)
            {
                throw new PrisonException("This prison has to be locked before you can use it.");
            }

            this.User.Profile.LoadUserProfileIfNotLoaded();

            var envs = this.User.RetrieveDefaultEnvironmentVariables();

            // environmentVariables from the method parameters have precedence over the default envs
            if (extraEnvironmentVariables != null)
            {
                foreach (var env in extraEnvironmentVariables)
                {
                    envs[env.Key] = env.Value;
                }
            }

            string envBlock = Prison.BuildEnvironmentVariable(envs);

            Logger.Debug("Starting process '{0}' with arguments '{1}' as user '{2}' in working directory '{3}'", 
                fileName, arguments, this.User.UserName, this.PrisonHomePath);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = null;
            }

            if (RuleEnabled(RuleTypes.WindowStation))
            {
                var winStationCell = this.prisonCells.First((a) => { return a.RuleType == RuleTypes.WindowStation; });
                winStationCell.Apply(this);
            }

            NativeMethods.PROCESS_INFORMATION processInfo = NativeCreateProcessAsUser(interactive, fileName, arguments, currentDirectory, envBlock, stdinPipeName, stdoutPipeName, stderrPipeName);

            NativeMethods.CloseHandle(processInfo.hProcess);
            NativeMethods.CloseHandle(processInfo.hThread);

            var workerProcessPid = processInfo.dwProcessId;
            var workerProcess = Process.GetProcessById(workerProcessPid);

            // AccessTokenHandle
            // workerProcess.RemovePrivilege(ProcessPrivileges.Privilege.ChangeNotify);
            // ProcessExtensions.RemovePrivilege(new AccessTokenHandle() , Privilege.ChangeNotify);

            // Tag the process with the Job Object before resuming the process.
            this.jobObject.AddProcess(workerProcess);

            // Add process in the second job object 
            this.AddProcessToGuardJobObject(workerProcess);

            // This would allow the process to query the ExitCode. ref: http://msdn.microsoft.com/en-us/magazine/cc163900.aspx
            workerProcess.EnableRaisingEvents = true;

            return workerProcess;
        }

        private static void ResumeProcess(Process workerProcess)
        {
            // Now that the process is tagged with the Job Object so we can resume the thread.
            IntPtr threadHandler = NativeMethods.OpenThread(NativeMethods.ThreadAccess.SUSPEND_RESUME, false, workerProcess.Threads[0].Id);
            uint resumeResult = NativeMethods.ResumeThread(threadHandler);
            NativeMethods.CloseHandle(threadHandler);

            if (resumeResult != 1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void Destroy()
        {
            if (this.IsLocked)
            {
                Logger.Debug("Destroying prison {0}", this.Id);

                foreach (var cell in prisonCells)
                {
                    cell.Destroy(this);
                }

                this.jobObject.TerminateProcesses(-1);

                this.jobObject.Dispose();
                this.jobObject = null;

                // TODO: Should destroy delete the home directory???
                // Directory.CreateDirectory(prisonRules.PrisonHomePath);

                this.TryStopGuard();
                this.User.Profile.UnloadUserProfileUntilReleased();
                this.User.Profile.DeleteUserProfile();
                this.User.Delete();

                SystemRemoveQuota();
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
                SystemVirtualAddressSpaceQuotas.SetPagedPoolQuota
                    (this.Configuration.TotalPrivateMemoryLimitBytes, new SecurityIdentifier(this.User.UserSID));

                SystemVirtualAddressSpaceQuotas.SetNonPagedPoolQuota
                    (this.Configuration.TotalPrivateMemoryLimitBytes, new SecurityIdentifier(this.User.UserSID));

                SystemVirtualAddressSpaceQuotas.SetPagingFileQuota
                    (this.Configuration.TotalPrivateMemoryLimitBytes, new SecurityIdentifier(this.User.UserSID));

                SystemVirtualAddressSpaceQuotas.SetWorkingSetPagesQuota
                    (this.Configuration.TotalPrivateMemoryLimitBytes, new SecurityIdentifier(this.User.UserSID));
            }
        }

        private void InitializeJobObject()
        {
            if (this.jobObject != null) return;

            // Create the JobObject
            this.jobObject = new JobObject("Global\\" + this.User.UserName);

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

        private static int GetCurrentSessionId()
        {
            return 0; // Set for windows-warden
            ///// return Process.GetCurrentProcess().SessionId;
        }

        /// <summary>
        /// Formats a string with the env variables for CreateProcess Win API function.
        /// See env format here: http://msdn.microsoft.com/en-us/library/windows/desktop/ms682653(v=vs.85).aspx
        /// </summary>
        /// <param name="environmentVariables"></param>
        /// <returns></returns>
        private static string BuildEnvironmentVariable(Dictionary<string, string> environmentVariables)
        {
            string ret = null;
            if (environmentVariables.Count > 0)
            {
                foreach (var EnvironmentVariable in environmentVariables)
                {
                    var value = EnvironmentVariable.Value;
                    if (value == null)
                    {
                        value = string.Empty;
                    }

                    if (EnvironmentVariable.Key.Contains('=') || EnvironmentVariable.Key.Contains('\0') || value.Contains('\0'))
                    {
                        throw new ArgumentException("Invalid or restricted character", "environmentVariables");
                    }

                    ret += EnvironmentVariable.Key + "=" + value + '\0';
                }


                ret += "\0";
            }

            return ret;
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

        private static string GetCreateProcessDeletegatePath()
        {
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDirPath = Directory.GetParent(assemblyPath).FullName;

            return Path.Combine(assemblyDirPath, "HP.WindowsPrison.CreateProcessDelegate.exe");
        }

        private static string GetGuardPath()
        {
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDirPath = Directory.GetParent(assemblyPath).FullName;

            return Path.Combine(assemblyDirPath, "HP.WindowsPrison.Guard.exe");
        }

        private static string GetChangeSessionServicePath()
        {
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDirPath = Directory.GetParent(assemblyPath).FullName;

            return Path.Combine(assemblyDirPath, "HP.WindowsPrison.ChangeSession.exe");
        }

        private NativeMethods.PROCESS_INFORMATION NativeCreateProcessAsUser(bool interactive, string filename, string arguments, string curDir, string envBlock, PipeStream stdinPipeName, PipeStream stdoutPipeName, PipeStream stderrPipeName)
        {
            var startupInfo = new NativeMethods.STARTUPINFO();
            var processInfo = new NativeMethods.PROCESS_INFORMATION();

            PipeStream stdinPipe = null;
            PipeStream stdoutPipe = null;
            PipeStream stderrPipe = null;

            startupInfo = new NativeMethods.STARTUPINFO();

            if (RuleEnabled(RuleTypes.WindowStation))
            {
                startupInfo.lpDesktop = this.desktopName;
            }

            NativeMethods.ProcessCreationFlags creationFlags = NativeMethods.ProcessCreationFlags.ZERO_FLAG;

            // Exclude flags
            creationFlags &=
                ~NativeMethods.ProcessCreationFlags.CREATE_PRESERVE_CODE_AUTHZ_LEVEL &
                ~NativeMethods.ProcessCreationFlags.CREATE_BREAKAWAY_FROM_JOB;

            // Include flags
            creationFlags |=
                NativeMethods.ProcessCreationFlags.CREATE_DEFAULT_ERROR_MODE |
                NativeMethods.ProcessCreationFlags.CREATE_NEW_PROCESS_GROUP |
                NativeMethods.ProcessCreationFlags.CREATE_SUSPENDED |
                NativeMethods.ProcessCreationFlags.CREATE_UNICODE_ENVIRONMENT |
                NativeMethods.ProcessCreationFlags.CREATE_NEW_CONSOLE;

            // TODO: extra steps for interactive to work:
            // http://blogs.msdn.com/b/winsdk/archive/2013/05/01/how-to-launch-a-process-interactively-from-a-windows-service.aspx
            if (interactive)
            {
                startupInfo.lpDesktop = "";
            }
            else
            {
                // creationFlags |= Native.ProcessCreationFlags.CREATE_NO_WINDOW;

                // startupInfo.dwFlags |= 0x00000100; // STARTF_USESTDHANDLES

                // Dangerous and maybe insecure to give a handle like that an untrusted processes
                //startupInfo.hStdInput = Native.GetStdHandle(Native.STD_INPUT_HANDLE);
                //startupInfo.hStdOutput = Native.GetStdHandle(Native.STD_OUTPUT_HANDLE);
                //startupInfo.hStdError = Native.GetStdHandle(Native.STD_ERROR_HANDLE);

                if (stdinPipeName != null || stdoutPipeName != null || stderrPipeName != null)
                {
                    startupInfo.dwFlags |= 0x00000100; // STARTF_USESTDHANDLES
                }

                if (stdinPipeName != null)
                {
                    startupInfo.hStdInput = GetHandleFromPipe(stdinPipeName);
                }

                if (stdoutPipeName != null)
                {
                    startupInfo.hStdOutput = GetHandleFromPipe(stdoutPipeName);
                }

                if (stderrPipeName != null)
                {
                    startupInfo.hStdError = GetHandleFromPipe(stderrPipeName);
                }
            }

            if (string.IsNullOrWhiteSpace(curDir))
            {
                curDir = this.PrisonHomePath;
            }

            NativeMethods.SECURITY_ATTRIBUTES processAttributes = new NativeMethods.SECURITY_ATTRIBUTES();
            NativeMethods.SECURITY_ATTRIBUTES threadAttributes = new NativeMethods.SECURITY_ATTRIBUTES();
            processAttributes.nLength = Marshal.SizeOf(processAttributes);
            threadAttributes.nLength = Marshal.SizeOf(threadAttributes);

            CreateProcessAsUser(
                hToken: this.User.LogonToken,
                lpApplicationName: filename,
                lpCommandLine: arguments,
                lpProcessAttributes: ref processAttributes,
                lpThreadAttributes: ref threadAttributes,
                bInheritHandles: true,
                dwCreationFlags: creationFlags,
                lpEnvironment: envBlock,
                lpCurrentDirectory: curDir,
                lpStartupInfo: ref startupInfo,
                lpProcessInformation: out processInfo);

            // TODO: use finally
            if (stdinPipe != null) stdinPipe.Dispose(); stdinPipe = null;
            if (stdoutPipe != null) stdoutPipe.Dispose(); stdoutPipe = null;
            if (stderrPipe != null) stderrPipe.Dispose(); stderrPipe = null;

            return processInfo;
        }

        private static IntPtr GetHandleFromPipe(PipeStream ps)
        {
            return ps.SafePipeHandle.DangerousGetHandle();
        }

        private static void CreateProcessAsUser(
            SafeTokenHandle hToken,
            string lpApplicationName,
            string lpCommandLine,
            ref HP.WindowsPrison.NativeMethods.SECURITY_ATTRIBUTES lpProcessAttributes,
            ref HP.WindowsPrison.NativeMethods.SECURITY_ATTRIBUTES lpThreadAttributes,
            bool bInheritHandles,
            HP.WindowsPrison.NativeMethods.ProcessCreationFlags dwCreationFlags,
            string lpEnvironment,
            string lpCurrentDirectory,
            ref HP.WindowsPrison.NativeMethods.STARTUPINFO lpStartupInfo,
            out HP.WindowsPrison.NativeMethods.PROCESS_INFORMATION lpProcessInformation
            )
        {

            if (!NativeMethods.CreateProcessAsUser(
                    hToken.DangerousGetHandle(),
                    lpApplicationName,
                    lpCommandLine,
                    ref lpProcessAttributes,
                    ref lpThreadAttributes,
                    bInheritHandles,
                    dwCreationFlags,
                    lpEnvironment,
                    lpCurrentDirectory,
                    ref lpStartupInfo,
                    out lpProcessInformation
                ))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private void SystemRemoveQuota()
        {
            SystemVirtualAddressSpaceQuotas.RemoveQuotas(new SecurityIdentifier(this.User.UserSID));
        }

        private static void CloseRemoteSession(IExecutor remoteSessionExec)
        {
            ((ICommunicationObject)remoteSessionExec).Close();
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
