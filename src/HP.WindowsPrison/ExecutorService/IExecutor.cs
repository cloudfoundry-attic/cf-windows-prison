using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace HP.WindowsPrison.ExecutorService
{
    [ServiceContract]
    public interface IExecutor
    {
        [OperationContract]
        int ExecuteProcess(
            Prison prison,
            string fileName, 
            string arguments,
            string currentDirectory,
            Dictionary<string, string> extraEnvironmentVariables,
            PipeStream stdinPipeName, 
            PipeStream stdoutPipeName, 
            PipeStream stderrPipeName
            );
    }
}
