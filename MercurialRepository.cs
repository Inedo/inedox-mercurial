using System.IO;
using Inedo.BuildMaster;
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

        public override string RepositoryName
        {
            get
            {
                return Path.GetFileName(RepositoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
        }
    }
}
