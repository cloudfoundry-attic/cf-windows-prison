namespace HP.WindowsPrison
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Threading;

    internal class PrisonGuard
    {
        private Prison prison;

        static private string guardSuffix = "-guard";
        private const int checkGuardRetries = 200;

        public PrisonGuard(Prison prison)
        {
            this.prison = prison;
        }

        private string JobObjectName
        {
            get
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Global\\{0}{1}",
                    this.prison.User.UserName,
                    PrisonGuard.guardSuffix);
            }
        }

        private void CheckGuard()
        {
            using (var guardJob = HP.WindowsPrison.Utilities.WindowsJobObjects.JobObject.Attach(this.JobObjectName))
            {
            }
        }

        private void InitializeGuard()
        {
            try
            {
                this.CheckGuard();
            }
            catch (Win32Exception)
            {
                this.RunGuard();
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
            psi.Arguments = this.prison.User.UserName + " " + this.prison.Configuration.TotalPrivateMemoryLimitBytes;

            var gp = Process.Start(psi);

            List<Exception> startErrors = new List<Exception>();

            // Wait for guard to start
            while (true)
            {
                try
                {
                    retryCount++;
                    this.CheckGuard();
                    break;
                }
                catch (Exception ex)
                {
                    startErrors.Add(ex);
                }

                if (retryCount == checkGuardRetries)
                {
                    throw new PrisonException(
                        "Maximum start prison guard retries exceeded",
                        new AggregateException(startErrors));
                }

                Thread.Sleep(100);
            }

            return gp;
        }

        public void AddProcessToGuardJobObject(Process p)
        {
            this.InitializeGuard();

            // Careful to close the Guard Job Object, 
            // else it is not guaranteed that the Job Object will not terminate if the Guard exists

            using (var guardJob = HP.WindowsPrison.Utilities.WindowsJobObjects.JobObject.Attach(this.JobObjectName))
            {
                guardJob.AddProcess(p);
            }

            this.CheckGuard();
        }

        public void TryStopGuard()
        {
            EventWaitHandle dischargeEvent = null;
            EventWaitHandle.TryOpenExisting("Global\\" + "discharge-" + this.prison.User.UserName, out dischargeEvent);

            if (dischargeEvent != null)
            {
                dischargeEvent.Set();
            }
        }

        private static string GetGuardPath()
        {
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDirPath = Directory.GetParent(assemblyPath).FullName;

            return Path.Combine(assemblyDirPath, "HP.WindowsPrison.Guard.exe");
        }
    }
}
