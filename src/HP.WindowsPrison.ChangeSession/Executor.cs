using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.ServiceModel;
using System.Text;
using HP.WindowsPrison.ExecutorService;

namespace HP.WindowsPrison.ChangeSession
{
    internal class Executor : IExecutor
    {
        [PrincipalPermission(SecurityAction.Demand, Role = "BUILTIN\\Administrators")]
        public int ExecuteProcess(Prison prison, string fileName, string arguments, string currentDirectory, Dictionary<string, string> extraEnvironmentVariables, PipeStream stdinPipeName, PipeStream stdoutPipeName, PipeStream stderrPipeName)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            // To debug the service uncomment the following line:
            //// Debugger.Launch();

            prison.Reattach();
            var p = prison.InitializeProcess(fileName, arguments, currentDirectory, false, extraEnvironmentVariables, stdinPipeName, stdoutPipeName, stderrPipeName);

            return p.Id;
        }
    }
}
