using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Mercurial.Credentials;
using Inedo.Extensions.Shared.Mercurial.Clients;

namespace Inedo.Extensions.Mercurial.Operations
{
    public sealed class TagOperation : MercurialOperation
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [ScriptAlias("RepositoryUrl")]
        [DisplayName("Repository URL")]
        [PlaceholderText("Use repository from credentials")]
        [MappedCredential(nameof(MercurialCredentials.RepositoryUrl))]
        public string RepositoryUrl { get; set; }

        [Required]
        [ScriptAlias("Tag")]
        [DisplayName("Tag name")]
        public string Tag { get; set; }

        [ScriptAlias("Branch")]
        [DisplayName("Branch name")]
        [PlaceholderText("default")]
        public string Branch { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            string repositoryUrl = this.RepositoryUrl;
            if (string.IsNullOrEmpty(repositoryUrl))
            {
                this.LogError("RepositoryUrl is not specified. It must be included in either the referenced credential or in the RepositoryUrl argument of the operation.");
                return;
            }

            string branchDesc = string.IsNullOrEmpty(this.Branch) ? "" : $" on '{this.Branch}' branch";
            this.LogInformation($"Tag '{repositoryUrl}'{branchDesc} as '{this.Tag}'...");

            var client = this.CreateClient(context, repositoryUrl, WorkspacePath.Resolve(context, repositoryUrl, this.WorkspaceDiskPath));
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
                    Branch = this.Branch
                }
            ).ConfigureAwait(false);

            await client.TagAsync(this.Tag).ConfigureAwait(false);

            this.LogInformation("Tag complete.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Tag Mercurial Source"),
               new RichDescription("in ", new Hilite(config[nameof(this.RepositoryUrl)]), " with ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
