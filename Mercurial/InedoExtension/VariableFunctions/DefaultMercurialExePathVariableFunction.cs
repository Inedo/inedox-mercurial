using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.Mercurial.VariableFunctions
{
    [ScriptAlias("DefaultMercurialExePath")]
    [Description("The path to the hg executable to use for Mercurial operations.")]
    [Tag("mercurial")]
    [ExtensionConfigurationVariable(Required = false)]
    public sealed class DefaultMercurialExePathVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IVariableFunctionContext context) => string.Empty;
    }
}
