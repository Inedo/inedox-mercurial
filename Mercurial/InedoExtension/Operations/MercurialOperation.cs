using System.ComponentModel;
using System.Security;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Mercurial.Credentials;
using Inedo.Extensions.Shared.Mercurial.Clients;
using Inedo.Extensions.Shared.Mercurial.Clients.CommandLine;

namespace Inedo.Extensions.Mercurial.Operations
{
    public abstract class MercurialOperation : ExecuteOperation, IHasCredentials<MercurialCredentials>
    {
        private protected MercurialOperation()
        {
        }

        public abstract string CredentialName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from credentials")]
        [MappedCredential(nameof(MercurialCredentials.UserName))]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from credentials")]
        [MappedCredential(nameof(MercurialCredentials.Password))]
        public SecureString Password { get; set; }

        [Category("Advanced")]
        [ScriptAlias("MercurialExePath")]
        [DisplayName("Mercurial executable path")]
        [DefaultValue("$DefaultMercurialExePath")]
        public string MercurialExePath { get; set; }

        [Category("Advanced")]
        [ScriptAlias("WorkspaceDiskPath")]
        [DisplayName("Workspace disk path")]
        [PlaceholderText("Automatically managed")]
        [Description("If not set, a workspace name will be automatically generated and persisted based on the Repository URL or other host-specific information (e.g. repository's name).")]
        public string WorkspaceDiskPath { get; set; }

        [Category("Advanced")]
        [ScriptAlias("CleanWorkspace")]
        [DisplayName("Clean workspace")]
        [Description("If set to true, the workspace directory will be cleared before any Mercurial-based operations are performed.")]
        public bool CleanWorkspace { get; set; }

        protected MercurialClient CreateClient(IOperationExecutionContext context, string repositoryUrl, WorkspacePath workspacePath)
        {
            if (!string.IsNullOrEmpty(this.MercurialExePath))
            {
                this.LogDebug($"Executable path specified, using Mercurial command line client at '{this.MercurialExePath}'...");
                return new MercurialCommandLineClient(
                    this.MercurialExePath,
                    context.Agent.GetService<IRemoteProcessExecuter>(),
                    context.Agent.GetService<IFileOperationsExecuter>(),
                    new MercurialRepositoryInfo(workspacePath, repositoryUrl, this.UserName, this.Password),
                    this,
                    context.CancellationToken
                );
            }
            else
            {
                this.LogDebug("No executable path specified, trying 'hg'...");
                return new MercurialCommandLineClient(
                    "hg",
                    context.Agent.GetService<IRemoteProcessExecuter>(),
                    context.Agent.GetService<IFileOperationsExecuter>(),
                    new MercurialRepositoryInfo(workspacePath, repositoryUrl, this.UserName, this.Password),
                    this,
                    context.CancellationToken
                );
            }
        }
    }
}
