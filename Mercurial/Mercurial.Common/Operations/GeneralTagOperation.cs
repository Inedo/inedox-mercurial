#if BuildMaster
using Inedo.BuildMaster.Extensibility;
#elif Otter
using Inedo.Otter.Extensibility;
#endif
using Inedo.Documentation;
using Inedo.Extensions.Mercurial.Credentials;
using Inedo.Extensions.Shared.Mercurial.Operations;
using System.ComponentModel;

namespace Inedo.Extensions.Operations
{
    [DisplayName("Tag Mercurial Source")]
    [Description("Tags the source code in a general Mercurial repository.")]
    [Tag("source-control")]
    [ScriptAlias("Hg-Tag")]
    [ScriptNamespace("Mercurial", PreferUnqualified = true)]
    [Example(@"
# tags the current source tree with the current release name and package number
Hg-Tag(
    Credentials: Hdars-Mercurial,
    RepositoryUrl: https://www.selenic.com/repo/hello,
    Tag: $ReleaseName.$PackageNumber
);
")]
    public sealed class GeneralTagOperation : TagOperation<MercurialCredentials>
    {
    }
}
