﻿using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Jering.JavascriptUtils.Node
{
    public class NodeJSProcessFactory : INodeJSProcessFactory
    {
        private readonly NodeJSProcessOptions _nodeProcessOptions;

        public NodeJSProcessFactory(IOptions<NodeJSProcessOptions> optionsAccessor)
        {
            _nodeProcessOptions = optionsAccessor.Value;
        }

        public Process Create(string nodeServerScript)
        {
            ProcessStartInfo startInfo = CreateNodeProcessStartInfo(nodeServerScript);

            return CreateAndStartNodeProcess(startInfo);
        }

        internal ProcessStartInfo CreateNodeProcessStartInfo(string nodeServerScript)
        {
            nodeServerScript = EscapeCommandLineArg(nodeServerScript);

            // This method is virtual, as it provides a way to override the NODE_PATH or the path to node.exe
            int currentProcessPid = Process.GetCurrentProcess().Id;
            var startInfo = new ProcessStartInfo("node")
            {
                Arguments = $"{_nodeProcessOptions.NodeAndV8Options} -e \"{nodeServerScript}\" -- --parentPid {currentProcessPid} --port {_nodeProcessOptions.Port}",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = _nodeProcessOptions.ProjectPath
            };

            // Append environment Variables
            if (_nodeProcessOptions.EnvironmentVariables != null)
            {
                foreach (var envVarKey in _nodeProcessOptions.EnvironmentVariables.Keys)
                {
                    string envVarValue = _nodeProcessOptions.EnvironmentVariables[envVarKey];
                    if (envVarValue != null)
                    {
                        startInfo.Environment[envVarKey] = envVarValue;
                    }
                }
            }

            // Append projectPath to NODE_PATH so it can locate node_modules. ProjectPath may be null if only bundles/self-contained-scripts
            // will be executed.
            if (_nodeProcessOptions.ProjectPath != null)
            {
                string existingNodePath = Environment.GetEnvironmentVariable("NODE_PATH") ?? string.Empty;
                if (existingNodePath != string.Empty)
                {
                    existingNodePath += Path.PathSeparator;
                }

                startInfo.Environment["NODE_PATH"] = existingNodePath + Path.Combine(_nodeProcessOptions.ProjectPath, "node_modules");
            }

            return startInfo;
        }

        internal Process CreateAndStartNodeProcess(ProcessStartInfo startInfo)
        {
            try
            {
                Process process = Process.Start(startInfo);

                // On Mac at least, a killed child process is left open as a zombie until the parent
                // captures its exit code. We don't need the exit code for this process, and don't want
                // to use process.WaitForExit() explicitly (we'd have to block the thread until it really
                // has exited), but we don't want to leave zombies lying around either. It's sufficient
                // to use process.EnableRaisingEvents so that .NET will grab the exit code and let the
                // zombie be cleaned away without having to block our thread.
                process.EnableRaisingEvents = true;

                return process;
            }
            catch (Exception ex)
            {
                string message = "Failed to start Node process. To resolve this:.\n\n"
                            + "[1] Ensure that Node.js is installed and can be found in one of the PATH directories.\n"
                            + $"    Current PATH enviroment variable is: { Environment.GetEnvironmentVariable("PATH") }\n"
                            + "    Make sure the Node executable is in one of those directories, or update your PATH.\n\n"
                            + "[2] See the InnerException for further details of the cause.";
                throw new InvalidOperationException(message, ex);
            }
        }

        // TODO verify that this escaping works for non-windows platforms
        internal string EscapeCommandLineArg(string arg)
        {
            var stringBuilder = new StringBuilder();
            int slashSequenceLength = 0;
            for(int i = 0; i < arg.Length; i++)
            {
                char currentChar = arg[i];

                if(currentChar == '\\')
                {
                    slashSequenceLength++;

                    // If the last character in the argument is \, it must be escaped, together with any \ that immediately preceed it.
                    // This prevents situations like: SomeExecutable.exe "SomeArg\", where the quote meant to demarcate the end of the
                    // argument gets escaped.
                    if(i == arg.Length - 1)
                    {
                        for (int j = 0; j < slashSequenceLength; j++)
                        {
                            stringBuilder.
                                Append('\\').
                                Append('\\');
                        }
                    }
                }
                else if(currentChar == '"')
                {
                    // Every \ or sequence of \ that preceed a " must be escaped.
                    for(int j = 0; j < slashSequenceLength; j++)
                    {
                        stringBuilder.
                            Append('\\').
                            Append('\\');
                    }
                    slashSequenceLength = 0;

                    stringBuilder.
                        Append('\\').
                        Append('"');

                }
                else
                {
                    for (int j = 0; j < slashSequenceLength; j++)
                    {
                        stringBuilder.Append('\\');
                    }
                    slashSequenceLength = 0;

                    stringBuilder.Append(currentChar);
                }
            }

            return stringBuilder.ToString();
        }
    }
}