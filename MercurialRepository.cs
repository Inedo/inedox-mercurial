using System.IO;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMaster.Web;

namespace Inedo.BuildMasterExtensions.Mercurial
{
    [CustomEditor(typeof(MercurialRepositoryEditor))]
    public sealed class MercurialRepository : RepositoryBase
    {
        /// <summary>
        /// Gets or sets the url to the optionally-used remote repository
        /// </summary>
        [Persistent]
        public string RemoteRepositoryUrl { get; set; }

        public bool IsManagedByBuildMaster { get { return string.IsNullOrWhiteSpace(this.RepositoryPath); } }

        public override string RepositoryName
        {
            get
            {
                if (!this.IsManagedByBuildMaster)
                    return Path.GetFileName(RepositoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                return MercurialPath.BuildPathFromUrl(this.RemoteRepositoryUrl);
            }
        }

        public string GetFullRepositoryPath(IFileOperationsExecuter agent)
        {
            if (!this.IsManagedByBuildMaster)
                return this.RepositoryPath;

            return agent.CombinePath(agent.GetBaseWorkingDirectory(), "HgRepositories", MercurialPath.BuildPathFromUrl(this.RemoteRepositoryUrl));
        }
    }
}
