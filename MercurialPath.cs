using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Inedo.BuildMaster.Extensibility.Agents;

namespace Inedo.BuildMasterExtensions.Mercurial
{
    /// <summary>
    /// Wraps functionality for Mercurial repositories and paths.
    /// </summary>
    internal sealed class MercurialPath
    {
        private static readonly Regex PathSanitizerRegex = new Regex("[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())) + "]", RegexOptions.Compiled);
        private static readonly Regex MercurialPathRegex = new Regex(@"^(?<1>[^|]+)(\|((?<2>[^:]*):)?(?<3>.*))?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public static char RepositorySeparatorChar = '|';

        /// <summary>
        /// Gets the branch specified in the path, or null if no branch is specified.
        /// </summary>
        public string PathSpecifiedBranch { get; private set; }

        /// <summary>
        /// Gets the branch specified in the path or if no path is specified, falls back to the pre-v3.2 provider-level branch.
        /// </summary>
        public string Branch { get; private set; }

        /// <summary>
        /// Gets the Mercurial repository.
        /// </summary>
        public MercurialRepository Repository { get; private set; }

        /// <summary>
        /// Gets or sets the full source path on disk.
        /// </summary>
        public string PathOnDisk { get; private set; }

        /// <summary>
        /// Gets the path relative to the repository path.
        /// </summary>
        public string RelativePath { get; private set; }

        public MercurialPath(MercurialProvider provider, string sourcePath)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");
            if (string.IsNullOrEmpty(sourcePath))
                return;

            var match = MercurialPathRegex.Match(sourcePath);
            if (!match.Success)
                throw new ArgumentException("Invalid source path (missing repository name).");

            var repositoryName = match.Groups[1].Value;
            var branchName = match.Groups[2].Value;
            var repositoryRelativePath = (match.Groups[3].Value ?? string.Empty).TrimStart('/');
            MercurialRepository repository;

            if (string.IsNullOrEmpty(branchName))
                branchName = "default";
            else
                this.PathSpecifiedBranch = branchName;

            if (!string.IsNullOrEmpty(repositoryName))
            {
                repository = provider.Repositories.FirstOrDefault(r => r.RepositoryName == repositoryName);
                if (repository == null)
                    throw new ArgumentException("Invalid repository: " + repositoryName);
            }
            else
            {
                repository = provider.Repositories.FirstOrDefault();
                if (repository == null)
                    throw new InvalidOperationException("No repositories are defined in this provider.");
            }

            this.Branch = branchName;
            this.Repository = repository;
            this.RelativePath = repositoryRelativePath;
            var fileOps = provider.Agent.GetService<IFileOperationsExecuter>();
            this.PathOnDisk = fileOps.CombinePath(repository.GetFullRepositoryPath(fileOps), repositoryRelativePath);
        }

        public override string ToString()
        {
            if (this.Repository == null)
                return string.Empty;
            if (this.PathSpecifiedBranch == null)
                return this.Repository.RepositoryName;

            return string.Format("{0}|{1}:{2}", this.Repository.RepositoryName, this.PathSpecifiedBranch, this.RelativePath);
        }

        public static string BuildSourcePath(string repositoryName, string branch, string relativePath)
        {
            if (string.IsNullOrEmpty(repositoryName))
                return string.Empty;
            if (string.IsNullOrEmpty(branch))
                return repositoryName;
            if (relativePath == null)
                return string.Format("{0}|{1}:", repositoryName, branch);

            // the DirectoryEntryInfo will include the directory of the repository (which is already handled by the repository name),
            // so it must be trimmmed from the front of the relative path in order for the Mercurial actions to refer to the correct path
            var match = Regex.Match(relativePath, @"^/?(?<1>[^/]+)/?(?<2>.*)$", RegexOptions.ExplicitCapture);
            relativePath = match.Groups[2].Value;

            return string.Format("{0}|{1}:{2}", repositoryName, branch, relativePath);
        }

        public static string BuildPathFromUrl(string url)
        {
            var uri = new UriBuilder(url);
            uri.UserName = null;
            uri.Password = null;

            return PathSanitizerRegex.Replace(uri.Uri.Authority + uri.Uri.AbsolutePath, "_");
        }
    }
}
