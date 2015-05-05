using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;

namespace Inedo.BuildMasterExtensions.Mercurial
{
    /// <summary>
    /// Wraps functionality for Mercurial repositories and paths.
    /// </summary>
    internal sealed class MercurialContext : SourceControlContext
    {
        private static readonly Regex PathSanitizerRegex = new Regex("[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())) + "]", RegexOptions.Compiled);
        private static readonly Regex MercurialPathRegex = new Regex(@"^(?<1>[^|]+)(\|((?<2>[^:]*):)?(?<3>.*))?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public static char RepositorySeparatorChar = '|';

        /// <summary>
        /// Gets the branch specified in the path, or null if no branch is specified.
        /// </summary>
        public string PathSpecifiedBranch { get; private set; }

        public MercurialContext(MercurialProvider provider, string sourcePath)
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
            SourceRepository repository;

            if (string.IsNullOrEmpty(branchName))
                branchName = "default";
            else
                this.PathSpecifiedBranch = branchName;

            if (!string.IsNullOrEmpty(repositoryName))
            {
                repository = provider.Repositories.FirstOrDefault(r => r.Name == repositoryName);
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
            this.RepositoryRelativePath = repositoryRelativePath;
            var fileOps = provider.Agent.GetService<IFileOperationsExecuter>();
            this.WorkspaceDiskPath = fileOps.CombinePath(repository.GetDiskPath(fileOps), repositoryRelativePath);
        }

        public override string ToLegacyPathString()
        {
            if (this.Repository == null)
                return string.Empty;
            if (this.PathSpecifiedBranch == null)
                return this.Repository.Name;

            return string.Format("{0}|{1}:{2}", this.Repository.Name, this.PathSpecifiedBranch, this.RepositoryRelativePath);
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
    }
}
