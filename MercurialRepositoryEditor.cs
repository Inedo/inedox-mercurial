using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Mercurial
{
    internal sealed class MercurialRepositoryEditor : RepositoryEditorBase
    {
        SourceControlFileFolderPicker txtRepositoryPath;
        ValidatingTextBox txtRemoteRepoPath;

        protected override void CreateChildControls()
        {
            this.txtRepositoryPath = new SourceControlFileFolderPicker()
            {
                DisplayMode = SourceControlBrowser.DisplayModes.Folders,
                Required = true
            };
            this.txtRepositoryPath.PreRender += (s,e) =>
            { 
                var ctx = GetProviderEditorContext();
                if (ctx != null) this.txtRepositoryPath.ServerId = ctx.ServerId;
            };

            this.txtRemoteRepoPath = new ValidatingTextBox() { Width = 300 };


            CUtil.Add(this,
                new StandardFormField(
                    "Local Repository:",
                    this.txtRepositoryPath),
                new StandardFormField(
                    "Remote Repository URL:", 
                    this.txtRemoteRepoPath)
            );
        }

        public override void BindToForm(RepositoryBase _extension)
        {
            var extension = (MercurialRepository)_extension;

            EnsureChildControls();

            this.txtRepositoryPath.Text = extension.RepositoryPath;
            this.txtRemoteRepoPath.Text = extension.RemoteRepositoryUrl;
        }

        public override RepositoryBase CreateFromForm()
        {
            EnsureChildControls();
            return new MercurialRepository
            {
                RepositoryPath = this.txtRepositoryPath.Text,
                RemoteRepositoryUrl = this.txtRemoteRepoPath.Text
            };
        }
    }
}
