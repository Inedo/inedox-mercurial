#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.VariableFunctions;
#endif
using Inedo.Documentation;
using System.ComponentModel;

namespace Inedo.Extensions.Shared.Mercurial.VariableFunctions
{
    [ScriptAlias("DefaultMercurialExePath")]
    [Description("The path to the hg executable to use for Mercurial operations.")]
    [Tag("mercurial")]
    [ExtensionConfigurationVariable(Required = false)]
    public sealed class DefaultMercurialExePathVariableFunction : ScalarVariableFunction
    {
#if BuildMaster
        protected override object EvaluateScalar(IGenericBuildMasterContext context)
        {
            return string.Empty;
        }
#elif Otter
        protected override object EvaluateScalar(IOtterContext context)
        {
            return string.Empty;
        }
#endif
    }
}
