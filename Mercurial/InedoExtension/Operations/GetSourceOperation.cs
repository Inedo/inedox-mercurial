using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Mercurial.Credentials;
using Inedo.Extensions.Shared.Mercurial.Clients;
using Inedo.Web.Plans.ArgumentEditors;

namespace Inedo.Extensions.Mercurial.Operations
{
    [DisplayName("Get Source from Mercurial Repository")]
    [Description("Gets the source code from a general Mercurial repository.")]
    [Tag("source-control")]
    [ScriptAlias("Hg-GetSource")]
    [ScriptNamespace("Mercurial", PreferUnqualified = true)]
    [Example(@"
# pulls source from a remote repository and archives/exports the contents to a target directory
Hg-GetSource(
    Credentials: Hdars-Mercurial,
    RepositoryUrl: https://www.selenic.com/repo/hello,
    DiskPath: ~\Sources
);
")]
    public sealed class GetSourceOperation : MercurialOperation
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [ScriptAlias("RepositoryUrl")]
        [DisplayName("Repository URL")]
        [PlaceholderText("Use repository from credentials")]
        [MappedCredential(nameof(MercurialCredentials.RepositoryUrl))]
        public string RepositoryUrl { get; set; }

        [ScriptAlias("DiskPath")]
        [DisplayName("Export to directory")]
        [FilePathEditor]
        [PlaceholderText("$WorkingDirectory")]
        public string DiskPath { get; set; }

        [ScriptAlias("Branch")]
        [DisplayName("Branch name")]
        [PlaceholderText("default")]
        public string Branch { get; set; }
        [ScriptAlias("Tag")]
        [DisplayName("Tag name")]
        public string Tag { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var repositoryUrl = this.RepositoryUrl;
            if (string.IsNullOrEmpty(repositoryUrl))
            {
                this.LogError("RepositoryUrl is not specified. It must be included in either the referenced credential or in the RepositoryUrl argument of the operation.");
                return;
            }

            string branchDesc = string.IsNullOrEmpty(this.Branch) ? "" : $" on '{this.Branch}' branch";
            string tagDesc = string.IsNullOrEmpty(this.Tag) ? "" : $" tagged '{this.Tag}'";
            this.LogInformation($"Getting source from '{repositoryUrl}'{branchDesc}{tagDesc}...");

            var workspacePath = WorkspacePath.Resolve(context, repositoryUrl, this.WorkspaceDiskPath);

            if (this.CleanWorkspace)
            {
                this.LogDebug($"Clearing workspace path '{workspacePath.FullPath}'...");
                var fileOps = context.Agent.GetService<IFileOperationsExecuter>();
                await fileOps.ClearDirectoryAsync(workspacePath.FullPath).ConfigureAwait(false);
            }

            var client = this.CreateClient(context, repositoryUrl, workspacePath);
            bool valid = await client.IsRepositoryValidAsync().ConfigureAwait(false);
            if (!valid)
            {
                await client.CloneAsync(
                    new MercurialCloneOptions
                    {
                        Branch = this.Branch
                    }
                ).ConfigureAwait(false);
            }

            await client.UpdateAsync(
                new MercurialUpdateOptions
                {
                    Branch = this.Branch,
                    Tag = this.Tag
                }
            ).ConfigureAwait(false);

            await client.ArchiveAsync(context.ResolvePath(this.DiskPath)).ConfigureAwait(false);

            this.LogInformation("Get source complete.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string source = AH.CoalesceString(config[nameof(this.RepositoryUrl)], config[nameof(this.CredentialName)]);

            return new ExtendedRichDescription(
               new RichDescription("Get Mercurial Source"),
               new RichDescription("from ", new Hilite(source), " to ", new DirectoryHilite(config[nameof(this.DiskPath)]))
            );
        }
    }
}
