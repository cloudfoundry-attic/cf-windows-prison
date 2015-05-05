namespace CloudFoundry.WindowsPrison
{
    using CloudFoundry.WindowsPrison.Native;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO.Pipes;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    internal class PrisonExecutor
    {
        private Prison prison;

        internal PrisonExecutor(Prison prison)
        {
            this.prison = prison;
        }

        public Process Execute(string fileName, string arguments, string currentDirectory, bool interactive, Dictionary<string, string> extraEnvironmentVariables, PipeStream stdinPipeName, PipeStream stdoutPipeName, PipeStream stderrPipeName)
        {
            // C with Win32 API example to start a process under a different user: http://msdn.microsoft.com/en-us/library/aa379608%28VS.85%29.aspx

            if (!this.prison.IsLocked)
            {
                throw new PrisonException("The prison has to be locked before you can use it.");
            }

            this.prison.User.Profile.LoadUserProfileIfNotLoaded();

            var envs = this.prison.User.RetrieveDefaultEnvironmentVariables();

            // environmentVariables from the method parameters have precedence over the default envs
            if (extraEnvironmentVariables != null)
            {
                foreach (var env in extraEnvironmentVariables)
                {
                    envs[env.Key] = env.Value;
                }
            }

            string envBlock = BuildEnvironmentVariable(envs);

            Logger.Debug("Starting process '{0}' with arguments '{1}' as user '{2}' in working directory '{3}'", fileName, arguments, this.prison.User.UserName, this.prison.PrisonHomePath);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = null;
            }

            if (this.prison.RuleEnabled(RuleTypes.WindowStation))
            {
                var winStationCell = this.prison.prisonCells.First((a) => { return a.RuleType == RuleTypes.WindowStation; });
                winStationCell.Apply(this.prison);
            }

            NativeMethods.PROCESS_INFORMATION processInfo = this.NativeCreateProcessAsUser(interactive, fileName, arguments, currentDirectory, envBlock, stdinPipeName, stdoutPipeName, stderrPipeName);

            NativeMethods.CloseHandle(processInfo.hProcess);
            NativeMethods.CloseHandle(processInfo.hThread);

            var workerProcessPid = processInfo.dwProcessId;
            var workerProcess = Process.GetProcessById(workerProcessPid);

            // Tag the process with the Job Object before resuming the process.
            this.prison.jobObject.AddProcess(workerProcess);

            // Add process in the second job object 
            this.prison.PrisonGuard.AddProcessToGuardJobObject(workerProcess);

            // This would allow the process to query the ExitCode. ref: http://msdn.microsoft.com/en-us/magazine/cc163900.aspx
            workerProcess.EnableRaisingEvents = true;

            ResumeProcess(workerProcess);

            return workerProcess;
        }

        private static void ResumeProcess(Process workerProcess)
        {
            // Now that the process is tagged with the Job Object so we can resume the thread.
            IntPtr threadHandler = Native.NativeMethods.OpenThread(Native.NativeMethods.ThreadAccess.SUSPEND_RESUME, false, workerProcess.Threads[0].Id);
            uint resumeResult = Native.NativeMethods.ResumeThread(threadHandler);
            NativeMethods.CloseHandle(threadHandler);

            if (resumeResult != 1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
        private Native.NativeMethods.PROCESS_INFORMATION NativeCreateProcessAsUser(bool interactive, string filename, string arguments, string curDir, string envBlock, PipeStream stdinPipeName, PipeStream stdoutPipeName, PipeStream stderrPipeName)
        {
            var startupInfo = new Native.NativeMethods.STARTUPINFO();
            var processInfo = new Native.NativeMethods.PROCESS_INFORMATION();

            startupInfo = new Native.NativeMethods.STARTUPINFO();

            if (this.prison.RuleEnabled(RuleTypes.WindowStation))
            {
                startupInfo.lpDesktop = this.prison.desktopName;
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
                startupInfo.lpDesktop = string.Empty;
            }
            else
            {
                // creationFlags |= Native.ProcessCreationFlags.CREATE_NO_WINDOW;

                // startupInfo.dwFlags |= 0x00000100; // STARTF_USESTDHANDLES

                // Dangerous and maybe insecure to give a handle like that an untrusted processes
                // startupInfo.hStdInput = Native.GetStdHandle(Native.STD_INPUT_HANDLE);
                // startupInfo.hStdOutput = Native.GetStdHandle(Native.STD_OUTPUT_HANDLE);
                // startupInfo.hStdError = Native.GetStdHandle(Native.STD_ERROR_HANDLE);

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
                curDir = this.prison.PrisonHomePath;
            }

            NativeMethods.SECURITY_ATTRIBUTES processAttributes = new NativeMethods.SECURITY_ATTRIBUTES();
            NativeMethods.SECURITY_ATTRIBUTES threadAttributes = new NativeMethods.SECURITY_ATTRIBUTES();
            processAttributes.nLength = Marshal.SizeOf(processAttributes);
            threadAttributes.nLength = Marshal.SizeOf(threadAttributes);

            if (!NativeMethods.CreateProcessAsUser(
                hToken: this.prison.User.LogonToken.DangerousGetHandle(),
                lpApplicationName: filename,
                lpCommandLine: arguments,
                lpProcessAttributes: ref processAttributes,
                lpThreadAttributes: ref threadAttributes,
                bInheritHandles: true,
                dwCreationFlags: creationFlags,
                lpEnvironment: envBlock,
                lpCurrentDirectory: curDir,
                lpStartupInfo: ref startupInfo,
                lpProcessInformation: out processInfo))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return processInfo;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
        private static IntPtr GetHandleFromPipe(PipeStream ps)
        {
            return ps.SafePipeHandle.DangerousGetHandle();
        }
    }
}
