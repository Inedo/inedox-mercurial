#if BuildMaster
using Inedo.BuildMaster.Extensibility;
#elif Otter
using Inedo.Otter.Extensibility;
#endif
using System.ComponentModel;

namespace Inedo.Extensions.Mercurial.Credentials
{
    [ScriptAlias("Mercurial")]
    [DisplayName("Mercurial")]
    [Description("Generic credentials for Mercurial.")]
    public sealed class MercurialCredentials : Shared.Mercurial.Credentials.MercurialCredentials
    {
    }
}
