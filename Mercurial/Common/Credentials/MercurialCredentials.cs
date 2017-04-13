#if BuildMaster
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web;
#elif Otter
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensions;
#endif
using Inedo.Documentation;
using Inedo.Serialization;
using System.ComponentModel;
using System.Security;

namespace Inedo.Extensions.Shared.Mercurial.Credentials
{
    public abstract class MercurialCredentials : ResourceCredentials
    {
        [Persistent]
        [DisplayName("Repository URL")]
        public virtual string RepositoryUrl { get; set; }
        [Persistent]
        [DisplayName("User name")]
        public string UserName { get; set; }
        [Persistent(Encrypted = true)]
        [DisplayName("Password")]
        [FieldEditMode(FieldEditMode.Password)]
        public SecureString Password { get; set; }

        public override RichDescription GetDescription()
        {
            return new RichDescription(this.UserName, "@", this.RepositoryUrl);
        }
    }
}
