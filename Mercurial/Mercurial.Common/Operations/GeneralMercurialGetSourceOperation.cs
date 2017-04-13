#if BuildMaster
using Inedo.BuildMaster.Extensibility;
#elif Otter
using Inedo.Otter.Extensibility;
#endif
using Inedo.Documentation;
using Inedo.Extensions.Mercurial.Credentials;
using Inedo.Extensions.Shared.Mercurial.Operations;
using System.ComponentModel;

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
    public sealed class GeneralGetSourceOperation : GetSourceOperation<MercurialCredentials>
    {
    }
}
