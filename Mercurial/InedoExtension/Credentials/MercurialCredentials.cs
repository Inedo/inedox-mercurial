using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Mercurial.Credentials
{
    [ScriptAlias("Mercurial")]
    [DisplayName("Mercurial")]
    [Description("Generic credentials for Mercurial.")]
    public sealed class MercurialCredentials : ResourceCredentials
    {
        [Persistent]
        [DisplayName("Repository URL")]
        public string RepositoryUrl { get; set; }
        [Persistent]
        [DisplayName("User name")]
        public string UserName { get; set; }
        [Persistent(Encrypted = true)]
        [DisplayName("Password")]
        [FieldEditMode(FieldEditMode.Password)]
        public SecureString Password { get; set; }

        public override RichDescription GetDescription() => new RichDescription(this.UserName, "@", this.RepositoryUrl);
    }
}
