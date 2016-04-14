using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Mercurial
{
    internal sealed class MercurialProviderEditor : ProviderEditorBase
    {
        private SourceControlFileFolderPicker txtExePath;
        private ValidatingTextBox txtTagUser;

        public override void BindToForm(ProviderBase extension)
        {
            var provider = (MercurialProvider)extension;
            this.txtExePath.Text = provider.HgExecutablePath;
            this.txtTagUser.Text = provider.CommittingUser;
        }
        public override ProviderBase CreateFromForm()
        {
            var provider = new MercurialProvider
            {
                HgExecutablePath = this.txtExePath.Text,
                CommittingUser = this.txtTagUser.Text
            };

            return provider;
        }

        protected override void CreateChildControls()
        {
            this.txtExePath = new SourceControlFileFolderPicker
            {
                DisplayMode = SourceControlBrowser.DisplayModes.FoldersAndFiles,
                ServerId = this.EditorContext.ServerId,
                Required = true
            };

            this.txtTagUser = new ValidatingTextBox { DefaultText = "Local repository default" };

            this.Controls.Add(
                new SlimFormField("Username for tags:", this.txtTagUser),
                new SlimFormField("Hg command path:", this.txtExePath)
                {
                    HelpText = "The executable path for hg (hg.exe on Windows)."
                }
            );
        }
    }
}
