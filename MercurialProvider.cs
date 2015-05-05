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
    public sealed class MercurialProvider : DistributedSourceControlProviderBase
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

        public override DirectoryEntryInfo GetDirectoryEntryInfo(string sourcePath)
        {
            var context = (MercurialContext)this.CreateSourceControlContext(sourcePath);
            return this.GetDirectoryEntryInfo(context);
        }

        private DirectoryEntryInfo GetDirectoryEntryInfo(MercurialContext context)
        {
            if (context.Repository == null)
            {
                return new DirectoryEntryInfo(
                    string.Empty,
                    string.Empty,
                    this.Repositories.Select(repo => new DirectoryEntryInfo(repo.Name, repo.Name, null, null)).ToArray(),
                    null
                );
            }
            else if (context.PathSpecifiedBranch == null)
            {
                this.EnsureLocalWorkspace(context);

                return new DirectoryEntryInfo(
                    context.Repository.Name,
                    context.Repository.Name,
                    this.EnumerateBranches(context)
                        .Select(branch => new DirectoryEntryInfo(branch, MercurialContext.BuildSourcePath(context.Repository.Name, branch, null), null, null))
                        .ToArray(),
                    null
                );
            }
            else
            {
                this.EnsureLocalWorkspace(context);
                this.UpdateLocalWorkspace(context);

                var de = this.Agent.GetDirectoryEntry(new GetDirectoryEntryCommand()
                {
                    Path = context.WorkspaceDiskPath,
                    Recurse = false,
                    IncludeRootPath = false
                }).Entry;

                var subDirs = de.SubDirectories
                    .Where(entry => !entry.Name.StartsWith(".hg"))
                    .Select(subdir => new DirectoryEntryInfo(subdir.Name, MercurialContext.BuildSourcePath(context.Repository.Name, context.PathSpecifiedBranch, subdir.Path.Replace('\\', '/')), null, null))
                    .ToArray();

                var files = de.Files
                    .Select(file => new FileEntryInfo(file.Name, MercurialContext.BuildSourcePath(context.Repository.Name, context.PathSpecifiedBranch, file.Path.Replace('\\', '/'))))
                    .ToArray();

                return new DirectoryEntryInfo(
                    de.Name,
                    context.ToLegacyPathString(),
                    subDirs,
                    files
                );
            }
        }

        public override byte[] GetFileContents(string filePath)
        {
            var context = this.CreateSourceControlContext(filePath);
            return this.Agent.ReadFileBytes(context.WorkspaceDiskPath);
        }

        public override bool IsAvailable()
        {
            return true;
        }

        public override void ValidateConnection()
        {
            foreach (var repo in this.Repositories)
            {
                if (!this.Agent.DirectoryExists(repo.GetDiskPath(this.Agent)))
                {
                    // create repo directory and clone repo without checking out the files
                    this.Agent.CreateDirectory(repo.GetDiskPath(this.Agent));
                    this.ExecuteHgCommand(repo, "init");
                    this.ExecuteHgCommand(repo, "pull", repo.RemoteUrl);
                }

                this.ExecuteHgCommand(repo, "manifest");
            }
        }

        public override string ToString()
        {
            if (Repositories.Length == 1)
                return "Mercurial at " + Util.CoalesceStr(Repositories[0].RemoteUrl, Repositories[0].CustomDiskPath);
            else
                return "Mercurial";
        }

        public override void ApplyLabel(string label, SourceControlContext context)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentNullException("label");

            if (context.Repository == null) 
                throw new ArgumentException(context.ToLegacyPathString() + " does not represent a valid Mercurial path.", "context");

            this.UpdateLocalWorkspace(context);

            this.ExecuteHgCommand(
                context.Repository,
                "tag",
                "-u \"" + Util.CoalesceStr(this.CommittingUser, "SYSTEM") + "\"",
                label);

            if (!string.IsNullOrEmpty(context.Repository.RemoteUrl))
                this.ExecuteHgCommand(context.Repository, "push", context.Repository.RemoteUrl);
        }

        public override SourceControlContext CreateSourceControlContext(object contextData)
        {
            return new MercurialContext(this, (string)contextData);
        }

        public override void EnsureLocalWorkspace(SourceControlContext context)
        {
            var repoPath = context.Repository.GetDiskPath(this.Agent);
            if (!this.Agent.DirectoryExists(repoPath) || !this.Agent.DirectoryExists(this.Agent.CombinePath(repoPath, ".hg")))
            {
                this.Agent.CreateDirectory(repoPath);
                this.Clone(context);
            }
        }

        public override IEnumerable<string> EnumerateBranches(SourceControlContext context)
        {
            this.EnsureLocalWorkspace(context);
            this.UpdateLocalWorkspace(context);            

            var result = this.ExecuteHgCommand(context.Repository, "heads", "--template \"{branch}\\r\\n\"");
            if (result.ExitCode != 0)
                throw new InvalidOperationException(Util.CoalesceStr(string.Join(Environment.NewLine, result.Error), "Exit code was nonzero: " + result.ExitCode));

            return result.Output;
        }

        public override void ExportFiles(SourceControlContext context, string targetDirectory)
        {
            this.ExecuteHgCommand(context.Repository, "archive", "\"" + targetDirectory + "\" -X \".hg*\"");
        }

        public override object GetCurrentRevision(SourceControlContext context)
        {
            if (context.Repository == null)
                throw new ArgumentException("Path must specify a Mercurial repository.");

            this.UpdateLocalWorkspace(context);
            var res = this.ExecuteHgCommand(context.Repository, "log -r \"branch('default') and reverse(not(desc('Added tag ') and file(.hgtags)))\" -l1 --template \"{node}\"");

            if (!res.Output.Any())
                return string.Empty;

            return res.Output[0];
        }

        public override void GetLatest(SourceControlContext context, string targetPath)
        {
            this.EnsureLocalWorkspace(context);
            this.UpdateLocalWorkspace(context);
            this.ExportFiles(context, targetPath);
        }

        public override void GetLabeled(string label, SourceControlContext context, string targetPath)
        {
            if (string.IsNullOrEmpty(label)) 
                throw new ArgumentNullException("label");
            if (string.IsNullOrEmpty(targetPath)) 
                throw new ArgumentNullException("targetPath");

            if (context.Repository == null) 
                throw new ArgumentException(context.ToLegacyPathString() + " does not represent a valid Mercurial path.", "context");

            this.UpdateLocalWorkspace(context);

            this.ExecuteHgCommand(context.Repository, "update", "-r \"" + label + "\"");
            this.ExportFiles(context, targetPath);
        }

        public override void UpdateLocalWorkspace(SourceControlContext context)
        {
            if (string.IsNullOrEmpty(context.Repository.RemoteUrl))
                return;

            // pull changes if remote repository is used
            if (!string.IsNullOrEmpty(context.Repository.RemoteUrl))
                this.ExecuteHgCommand(context.Repository, "pull", context.Repository.RemoteUrl);

            // update the working repository, and do not check out the files
            this.ExecuteHgCommand(context.Repository, "update", "-C", context.Branch);
        }

        public override void DeleteWorkspace(SourceControlContext context)
        {
            this.Agent.ClearFolder(context.WorkspaceDiskPath);
        }

        public override IEnumerable<string> EnumerateLabels(SourceControlContext context)
        {
            throw new NotImplementedException();
        }

        private void Clone(SourceControlContext context)
        {
            var result = this.ExecuteHgCommand(context.Repository, "clone", "\"" + context.Repository.RemoteUrl + "\"", ".");
            if (result.ExitCode != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Error.ToArray()));
        }

        private ProcessResults ExecuteHgCommand(SourceRepository repo, string hgCommand, params string[] options)
        {
            if (repo == null)
                throw new ArgumentNullException("repo");

            string repositoryPath = repo.GetDiskPath(this.Agent);

            if (string.IsNullOrEmpty(this.HgExecutablePath) || !File.Exists(this.HgExecutablePath))
                throw new NotAvailableException("Cannot execute Mercurial command; hg executable not found at '" + this.HgExecutablePath + "' - please specify the path to this executable in the provider's configuration.");
            if (!repo.IsBuildMasterManaged && !this.Agent.DirectoryExists(this.Agent.CombinePath(repositoryPath, ".hg")))
                throw new NotAvailableException("A local repository was not found at: " + repositoryPath);

            var args = new StringBuilder();
            args.AppendFormat("{0} -R \"{1}\" -y -v ", hgCommand, repositoryPath);
            args.Append(string.Join(" ", (options ?? new string[0])));

            var results = this.ExecuteCommandLine(this.HgExecutablePath, args.ToString(), repositoryPath);
            if (results.ExitCode != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, results.Error));
            else
                return results;
        }
    }
}
