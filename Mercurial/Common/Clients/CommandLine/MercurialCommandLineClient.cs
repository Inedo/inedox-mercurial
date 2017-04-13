using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine.Executer;

namespace Inedo.Extensions.Shared.Mercurial.Clients.CommandLine
{
    public sealed class MercurialCommandLineClient : MercurialClient
    {
        private static readonly LazyRegex BranchParsingRegex = new LazyRegex(@"refs/heads/(?<branch>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline);

        private string hgExePath;
        private IRemoteProcessExecuter processExecuter;
        private IFileOperationsExecuter fileOps;
        private CancellationToken cancellationToken;

        public MercurialCommandLineClient(string hgExePath, IRemoteProcessExecuter processExecuter, IFileOperationsExecuter fileOps, MercurialRepositoryInfo repository, ILogger log, CancellationToken cancellationToken)
            : base(repository, log)
        {
            if (hgExePath == null)
                throw new ArgumentNullException(nameof(hgExePath));
            if (processExecuter == null)
                throw new ArgumentNullException(nameof(processExecuter));
            if (fileOps == null)
                throw new ArgumentNullException(nameof(fileOps));

            this.hgExePath = hgExePath;
            this.processExecuter = processExecuter;
            this.fileOps = fileOps;
            this.cancellationToken = cancellationToken;
        }

        public override async Task<bool> IsRepositoryValidAsync()
        {
            var result = await this.ExecuteCommandLineAsync(
                new MercurialArgumentsBuilder("log -l 1"),
                this.repository.LocalRepositoryPath,
                false
              ).ConfigureAwait(false);

            return result.ExitCode == 0 && result.Error.Count == 0;
        }

        public override async Task CloneAsync(MercurialCloneOptions options)
        {
            var args = new MercurialArgumentsBuilder("clone");

            if (options.Branch != null)
            {
                args.Append("-b");
                args.AppendQuoted(options.Branch);
            }

            args.AppendSensitive(this.repository.GetRemoteUrlWithCredentials());
            args.AppendQuoted(this.repository.LocalRepositoryPath);

            await this.ExecuteCommandLineAsync(args, this.repository.LocalRepositoryPath).ConfigureAwait(false);
        }

        public override async Task UpdateAsync(MercurialUpdateOptions options)
        {
            await this.ExecuteCommandLineAsync(new MercurialArgumentsBuilder("--config extensions.purge= purge --all"), this.repository.LocalRepositoryPath).ConfigureAwait(false);

            var pullArgs = new MercurialArgumentsBuilder("pull -u -f");
            if (options.Tag != null || options.Branch != null)
            {
                pullArgs.Append("-r");
                pullArgs.AppendQuoted(options.Tag ?? options.Branch);
            }
            pullArgs.AppendSensitive(this.repository.GetRemoteUrlWithCredentials());
            await this.ExecuteCommandLineAsync(pullArgs, this.repository.LocalRepositoryPath).ConfigureAwait(false);
        }

        public override async Task TagAsync(string tag)
        {
            var args = new MercurialArgumentsBuilder("tag -u");
            if (!string.IsNullOrEmpty(this.repository.UserName))
            {
                args.AppendQuoted(this.repository.UserName);
            }
            else
            {
#if BuildMaster
                args.Append("BuildMaster");
#elif Otter
                args.Append("Otter");
#endif
            }
            args.Append("-f");
            args.Append(tag);
            await this.ExecuteCommandLineAsync(args, this.repository.LocalRepositoryPath).ConfigureAwait(false);

            var pushArgs = new MercurialArgumentsBuilder("push");
            pushArgs.AppendSensitive(this.repository.GetRemoteUrlWithCredentials());

            await this.ExecuteCommandLineAsync(pushArgs, this.repository.LocalRepositoryPath).ConfigureAwait(false);
        }

        public override Task ArchiveAsync(string targetDirectory)
        {
            return CopyNonMercurialFilesAsync(this.fileOps, this.repository.LocalRepositoryPath, targetDirectory);
        }

        private async Task<ProcessResults> ExecuteCommandLineAsync(MercurialArgumentsBuilder args, string workingDirectory, bool throwOnFailure = true)
        {
            var startInfo = new RemoteProcessStartInfo
            {
                FileName = this.hgExePath,
                Arguments = args.ToString(),
                WorkingDirectory = workingDirectory,
                EnvironmentVariables =
                {
                    { "HGPLAIN", "1" }
                },
            };

            this.log.LogDebug("Ensuring local repository path exists...");
            await this.fileOps.CreateDirectoryAsync(this.repository.LocalRepositoryPath).ConfigureAwait(false);

            this.log.LogDebug("Working directory: " + startInfo.WorkingDirectory);
            this.log.LogDebug("Executing: " + startInfo.FileName + " " + args.ToSensitiveString());

            using (var process = this.processExecuter.CreateProcess(startInfo))
            {
                var outputLines = new List<string>();
                var errorLines = new List<string>();

                process.OutputDataReceived += (s, e) => { if (e?.Data != null) outputLines.Add(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e?.Data != null) errorLines.Add(e.Data); };

                process.Start();

                await process.WaitAsync(this.cancellationToken).ConfigureAwait(false);

                if (throwOnFailure && process.ExitCode != 0)
                {
                    throw new ExecutionFailureException($"hg returned error code {process.ExitCode}\n{string.Join("\n", errorLines)}");
                }
                return new ProcessResults(process.ExitCode ?? -1, outputLines, errorLines);
            }
        }
    }
}
