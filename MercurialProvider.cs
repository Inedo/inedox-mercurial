using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;

namespace Inedo.BuildMasterExtensions.Mercurial
{
    /// <summary>
    /// A provider that uses Mercurial 1.4 or later.
    /// </summary>
    [ProviderProperties("Mercurial", "Supports Mercurial 1.4 and later; requires Mercurial to be installed.")]
    [CustomEditor(typeof(MercurialProviderEditor))]
    public sealed class MercurialProvider : SourceControlProviderBase, IMultipleRepositoryProvider<MercurialRepository>, ILabelingProvider, IRevisionProvider
    {
        /// <summary>
        /// Gets or sets the path on disk to the hg executable (hg.exe on Windows)
        /// </summary>
        [Persistent]
        public string HgExecutablePath { get; set; }
        /// <summary>
        /// Gets or sets the user name that will be used for committing tags
        /// </summary>
        [Persistent]
        public string CommittingUser { get; set; }

        private new IFileOperationsExecuter Agent 
        { 
            get { return (IFileOperationsExecuter)base.Agent.GetService<IFileOperationsExecuter>(); } 
        }

        public override char DirectorySeparator
        {
            get { return '/'; }
        }

        public override void GetLatest(string sourcePath, string targetPath)
        {
            if (targetPath == null) throw new ArgumentNullException("targetPath");
            var hgSourcePath = new MercurialPath(this, sourcePath);
            if (hgSourcePath.Repository == null) throw new ArgumentException(sourcePath + " does not represent a valid Mercurial path.", "sourcePath");

            UpdateLocalRepo(hgSourcePath.Repository, hgSourcePath.Branch);
            CopyNonHgFiles(hgSourcePath.PathOnDisk, targetPath);
        }

        public override DirectoryEntryInfo GetDirectoryEntryInfo(string sourcePath)
        {
            return GetDirectoryEntryInfo(new MercurialPath(this, sourcePath));
        }

        private DirectoryEntryInfo GetDirectoryEntryInfo(MercurialPath path)
        {
            if (path.Repository == null)
            {
                return new DirectoryEntryInfo(
                    string.Empty,
                    string.Empty,
                    this.Repositories.Select(repo => new DirectoryEntryInfo(repo.RepositoryName, repo.RepositoryName, null, null)).ToArray(),
                    null
                );
            }
            else if (path.PathSpecifiedBranch == null)
            {
                this.EnsureRepoIsPresent(path.Repository);

                return new DirectoryEntryInfo(
                    path.Repository.RepositoryName,
                    path.Repository.RepositoryName,
                    this.EnumBranches(path.Repository)
                        .Select(branch => new DirectoryEntryInfo(branch, MercurialPath.BuildSourcePath(path.Repository.RepositoryName, branch, null), null, null))
                        .ToArray(),
                    null
                );
            }
            else
            {
                this.EnsureRepoIsPresent(path.Repository);
                this.UpdateLocalRepo(path.Repository, path.Branch);

                var de = this.Agent.GetDirectoryEntry(new GetDirectoryEntryCommand()
                {
                    Path = path.PathOnDisk,
                    Recurse = false,
                    IncludeRootPath = false
                }).Entry;

                var subDirs = de.SubDirectories
                    .Where(entry => !entry.Name.StartsWith(".hg"))
                    .Select(subdir => new DirectoryEntryInfo(subdir.Name, MercurialPath.BuildSourcePath(path.Repository.RepositoryName, path.PathSpecifiedBranch, subdir.Path.Replace('\\', '/')), null, null))
                    .ToArray();

                var files = de.Files
                    .Select(file => new FileEntryInfo(file.Name, MercurialPath.BuildSourcePath(path.Repository.RepositoryName, path.PathSpecifiedBranch, file.Path.Replace('\\', '/'))))
                    .ToArray();

                return new DirectoryEntryInfo(
                    de.Name,
                    path.ToString(),
                    subDirs,
                    files
                );
            }
        }

        private IEnumerable<string> EnumBranches(MercurialRepository repo)
        {
            if (repo.IsManagedByBuildMaster)
                this.EnsureRepoIsPresent(repo);

            if (!string.IsNullOrEmpty(repo.RemoteRepositoryUrl))
                this.ExecuteHgCommand(repo, "pull", repo.RemoteRepositoryUrl);

            var result = this.ExecuteHgCommand(repo, "heads", "--template \"{branch}\\r\\n\"");
            if (result.ExitCode != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Error.ToArray()));

            return result.Output;
        }

        private void EnsureRepoIsPresent(MercurialRepository repo)
        {
            var repoPath = repo.GetFullRepositoryPath(this.Agent);
            if (!this.Agent.DirectoryExists(repoPath) || !this.Agent.DirectoryExists(this.Agent.CombinePath(repoPath, ".hg")))
            {
                this.Agent.CreateDirectory(repoPath);
                this.CloneRepo(repo);
            }
        }

        public override byte[] GetFileContents(string filePath)
        {
            var hgSourcePath = new MercurialPath(this, filePath);
            if (hgSourcePath.Repository == null) throw new ArgumentException(filePath + " does not represent a valid Mercurial path.", "filePath");

            return File.ReadAllBytes(hgSourcePath.PathOnDisk);
        }

        public override bool IsAvailable()
        {
            return true;
        }

        public override void ValidateConnection()
        {
            foreach (MercurialRepository repo in Repositories)
            {
                if (repo.IsManagedByBuildMaster && !this.Agent.DirectoryExists(repo.GetFullRepositoryPath(this.Agent)))
                {
                    // create repo directory and clone repo without checking out the files
                    this.Agent.CreateDirectory(repo.GetFullRepositoryPath(this.Agent));
                    this.ExecuteHgCommand(repo, "init");
                    this.ExecuteHgCommand(repo, "pull", repo.RemoteRepositoryUrl);
                }

                this.ExecuteHgCommand(repo, "manifest");
            }
        }

        public override string ToString()
        {
            if (Repositories.Length == 1)
                return "Mercurial at " + Util.CoalesceStr(Repositories[0].RemoteRepositoryUrl, Repositories[0].RepositoryPath);
            else
                return "Mercurial";
        }

        public void ApplyLabel(string label, string sourcePath)
        {
            if (string.IsNullOrEmpty(label)) throw new ArgumentNullException("label");
            var hgSourcePath = new MercurialPath(this, sourcePath);
            if (hgSourcePath.Repository == null) throw new ArgumentException(sourcePath + " does not represent a valid Mercurial path.", "sourcePath");

            UpdateLocalRepo(hgSourcePath.Repository, hgSourcePath.Branch);

            ExecuteHgCommand(
                hgSourcePath.Repository,
                "tag",
                "-u \"" + Util.CoalesceStr(this.CommittingUser, "SYSTEM") + "\"",
                label);

            if (!string.IsNullOrEmpty(hgSourcePath.Repository.RemoteRepositoryUrl))
                ExecuteHgCommand(hgSourcePath.Repository, "push", hgSourcePath.Repository.RemoteRepositoryUrl);
        }

        public void GetLabeled(string label, string sourcePath, string targetPath)
        {
            if (string.IsNullOrEmpty(label)) throw new ArgumentNullException("label");
            if (string.IsNullOrEmpty(targetPath)) throw new ArgumentNullException("targetPath");
            var hgSourcePath = new MercurialPath(this, sourcePath);
            if (hgSourcePath.Repository == null) throw new ArgumentException(sourcePath + " does not represent a valid Mercurial path.", "sourcePath");

            UpdateLocalRepo(hgSourcePath.Repository, hgSourcePath.Branch);

            ExecuteHgCommand(hgSourcePath.Repository, "update", "-r \"" + label + "\"");
            CopyNonHgFiles(hgSourcePath.PathOnDisk, targetPath);
        }

        public object GetCurrentRevision(string path)
        {
            var mercurialPath = new MercurialPath(this, path);
            if (mercurialPath.Repository == null)
                throw new ArgumentException("Path must specify a Mercurial repository.");

            UpdateLocalRepo(mercurialPath.Repository, mercurialPath.Branch);
            var res = ExecuteHgCommand(mercurialPath.Repository, "log -r \"branch('default') and reverse(not(desc('Added tag ') and file(.hgtags)))\" -l1 --template \"{node}\"");

            if (!res.Output.Any())
                return string.Empty;

            return res.Output[0];
        }

        /// <summary>
        /// Copies files and subfolders from sourceFolder to targetFolder.
        /// </summary>
        /// <param name="sourceFolder">A path of the folder to be copied</param>
        /// <param name="targetFolder">A path of a folder to copy files to.  If targetFolder doesn't exist, it is created.</param>
        private static void CopyNonHgFiles(string sourceFolder, string targetFolder)
        {
            // If the source path isn't found, there's nothing to copy
            if (!Directory.Exists(sourceFolder))
                return;

            // If the target path doesn't exist, create it
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);

            DirectoryInfo sourceFolderInfo = new DirectoryInfo(sourceFolder);
            // Copy each file
            foreach (FileInfo theFile in sourceFolderInfo.GetFiles())
            {
                if (theFile.Name.StartsWith(".hg")) continue;
                theFile.CopyTo(Path.Combine(targetFolder, theFile.Name), true);
            }

            // Recurse subdirectories
            foreach (DirectoryInfo subfolder in sourceFolderInfo.GetDirectories())
            {
                if (subfolder.Name.Equals(".hg")) continue;
                CopyNonHgFiles(subfolder.FullName, Path.Combine(targetFolder, subfolder.Name));
            }
        }

        private void UpdateLocalRepo(MercurialRepository repo, string branch)
        {
            if (repo.IsManagedByBuildMaster)
                this.EnsureRepoIsPresent(repo);

            // pull changes if remote repository is used
            if (!string.IsNullOrEmpty(repo.RemoteRepositoryUrl))
                ExecuteHgCommand(repo, "pull", repo.RemoteRepositoryUrl);

            // update the working repository, and do not check out the files
            ExecuteHgCommand(repo, "update", "-C", branch);
        }

        private void CloneRepo(MercurialRepository repo)
        {
            var result = this.ExecuteHgCommand(repo, "clone", "\"" + repo.RemoteRepositoryUrl + "\"", ".");
            if (result.ExitCode != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Error.ToArray()));
        }

        private ProcessResults ExecuteHgCommand(MercurialRepository repo, string hgCommand, params string[] options)
        {
            if (repo == null) 
                throw new ArgumentNullException("repo");

            string repositoryPath = repo.GetFullRepositoryPath(this.Agent);

            if (string.IsNullOrEmpty(this.HgExecutablePath) || !File.Exists(this.HgExecutablePath)) 
                throw new NotAvailableException("Cannot execute Mercurial command; hg executable not found at '" + this.HgExecutablePath + "' - please specify the path to this executable in the provider's configuration.");
            if (!repo.IsManagedByBuildMaster && !this.Agent.DirectoryExists(this.Agent.CombinePath(repositoryPath, ".hg")))
                throw new NotAvailableException("A local repository was not found at: " + repositoryPath);

            var args = new StringBuilder();
            args.AppendFormat("{0} -R \"{1}\" -y -v ", hgCommand, repositoryPath);
            args.Append(string.Join(" ", (options ?? new string[0])));

            return this.ExecuteCommandLine(this.HgExecutablePath, args.ToString(), repositoryPath);
        }

        /// <summary>
        /// Returns an enumeration of file system entries without elements that start with .hg
        /// </summary>
        /// <typeparam name="TEntry">Type of file system entry.</typeparam>
        /// <param name="sourceEntries">Source collection of entries to filter.</param>
        /// <returns>Filtered file system entry collection.</returns>
        private static IEnumerable<TEntry> FilterOutHg<TEntry>(IEnumerable<TEntry> sourceEntries)
            where TEntry : SystemEntryInfo
        {
            foreach (var entry in sourceEntries)
            {
                if (!entry.Name.StartsWith(".hg"))
                    yield return entry;
            }
        }

        public MercurialRepository[] Repositories { get; set; }

        RepositoryBase[] IMultipleRepositoryProvider.Repositories
        {
            get
            {
                return this.Repositories;
            }
            set
            {
                this.Repositories = Array.ConvertAll(value ?? new RepositoryBase[0], r => (MercurialRepository)r);
            }
        }
    }
}
