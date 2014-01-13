using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;

namespace Inedo.BuildMasterExtensions.Mercurial
{
    /// <summary>
    /// A provider that uses Mercurial 1.5.1 or earlier.
    /// </summary>
    [ProviderProperties("Mercurial", "Supports Mercurial 1.4 and later; requires Mercurial to be installed.")]
    [CustomEditor(typeof(MercurialProviderEditor))]
    public sealed class MercurialProvider : SourceControlProviderBase, IMultipleRepositoryProvider<MercurialRepository>, ILabelingProvider, IRevisionProvider
    {
        #region HgCommands Static Class
        /// <summary>
        /// List of commands
        /// </summary>
        static class HgCommands
        {
            /// <summary>
            /// add the specified files on the next commit
            /// </summary>
            public const string add = "add";

            /// <summary>
            /// add all new files, delete all missing files
            /// </summary>
            public const string addremove = "addremove";

            /// <summary>
            /// show changeset information by line for each file
            /// </summary>
            public const string annotate = "annotate";

            /// <summary>
            /// create an unversioned archive of a repository revision
            /// </summary>
            public const string archive = "archive";

            /// <summary>
            /// reverse effect of earlier changeset
            /// </summary>
            public const string backout = "backout";

            /// <summary>
            /// subdivision search of changesets
            /// </summary>
            public const string bisect = "bisect";

            /// <summary>
            /// set or show the current branch name
            /// </summary>
            public const string branch = "branch";

            /// <summary>
            /// list repository named branches
            /// </summary>
            public const string branches = "branches";

            /// <summary>
            /// create a changegroup file
            /// </summary>
            public const string bundle = "bundle";

            /// <summary>
            /// output the current or given revision of files
            /// </summary>
            public const string cat = "cat";

            /// <summary>
            /// make a copy of an existing repository
            /// </summary>
            public const string clone = "clone";

            /// <summary>
            /// commit the specified files or all outstanding changes
            /// </summary>
            public const string commit = "commit";

            /// <summary>
            /// mark files as copied for the next commit
            /// </summary>
            public const string copy = "copy";

            /// <summary>
            /// diff repository (or selected files)
            /// </summary>
            public const string diff = "diff";

            /// <summary>
            /// dump the header and diffs for one or more changesets
            /// </summary>
            public const string export = "export";

            /// <summary>
            /// forget the specified files on the next commit
            /// </summary>
            public const string forget = "forget";

            /// <summary>
            /// search for a pattern in specified files and revisions
            /// </summary>
            public const string grep = "grep";

            /// <summary>
            /// show current repository heads or show branch heads
            /// </summary>
            public const string heads = "heads";

            /// <summary>
            /// show help for a given topic or a help overview
            /// </summary>
            public const string help = "help";

            /// <summary>
            /// identify the working copy or specified revision
            /// </summary>
            public const string identify = "identify";

            /// <summary>
            /// import an ordered set of patches
            /// </summary>
            public const string import = "import";

            /// <summary>
            /// show new changesets found in source
            /// </summary>
            public const string incoming = "incoming";

            /// <summary>
            /// create a new repository in the given directory
            /// </summary>
            public const string init = "init";

            /// <summary>
            /// locate files matching specific patterns
            /// </summary>
            public const string locate = "locate";

            /// <summary>
            /// show revision history of entire repository or files
            /// </summary>
            public const string log = "log";

            /// <summary>
            /// output the current or given revision of the project manifest
            /// </summary>
            public const string manifest = "manifest";

            /// <summary>
            /// merge working directory with another revision
            /// </summary>
            public const string merge = "merge";

            /// <summary>
            /// show changesets not found in the destination
            /// </summary>
            public const string outgoing = "outgoing";

            /// <summary>
            /// show the parents of the working directory or revision
            /// </summary>
            public const string parents = "parents";

            /// <summary>
            /// show aliases for remote repositories
            /// </summary>
            public const string paths = "paths";

            /// <summary>
            /// pull changes from the specified source
            /// </summary>
            public const string pull = "pull";

            /// <summary>
            /// push changes to the specified destination
            /// </summary>
            public const string push = "push";

            /// <summary>
            /// roll back an interrupted transaction
            /// </summary>
            public const string recover = "recover";

            /// <summary>
            /// remove the specified files on the next commit
            /// </summary>
            public const string remove = "remove";

            /// <summary>
            /// rename files; equivalent of copy + remove
            /// </summary>
            public const string rename = "rename";

            /// <summary>
            /// various operations to help finish a merge
            /// </summary>
            public const string resolve = "resolve";

            /// <summary>
            /// restore individual files or directories to an earlier state
            /// </summary>
            public const string revert = "revert";

            /// <summary>
            /// roll back the last transaction
            /// </summary>
            public const string rollback = "rollback";

            /// <summary>
            /// print the root (top) of the current working directory
            /// </summary>
            public const string root = "root";

            /// <summary>
            /// export the repository via HTTP
            /// </summary>
            public const string serve = "serve";

            /// <summary>
            /// show combined config settings from all hgrc files
            /// </summary>
            public const string showconfig = "showconfig";

            /// <summary>
            /// show changed files in the working directory
            /// </summary>
            public const string status = "status";

            /// <summary>
            /// summarize working directory state
            /// </summary>
            public const string summary = "summary";

            /// <summary>
            /// add one or more tags for the current or given revision
            /// </summary>
            public const string tag = "tag";

            /// <summary>
            /// list repository tags
            /// </summary>
            public const string tags = "tags";

            /// <summary>
            /// show the tip revision
            /// </summary>
            public const string tip = "tip";

            /// <summary>
            /// apply one or more changegroup files
            /// </summary>
            public const string unbundle = "unbundle";

            /// <summary>
            /// update working directory
            /// </summary>
            public const string update = "update";

            /// <summary>
            /// verify the integrity of the repository
            /// </summary>
            public const string verify = "verify";

            /// <summary>
            /// output version and copyright information
            /// </summary>
            public const string version = "version";
        }
        #endregion

        class MercurialPath
        {
            public static char RepositorySeparatorChar = '|';
            public MercurialPath(MercurialProvider provider, string sourcePath)
            {
                if (provider == null) throw new ArgumentNullException("provider");
                if (string.IsNullOrEmpty(sourcePath)) return;

                // pathParts => [repoName][repoPath]
                var pathParts = (sourcePath ?? "").Split(new[] { MercurialPath.RepositorySeparatorChar }, 2);
                if (pathParts.Length != 2) pathParts = new[] { pathParts[0], "" };

                // this.Repository
                foreach (MercurialRepository repo in provider.Repositories)
                    if (pathParts[0] == repo.RepositoryName)
                        this.Repository = repo;

                this.SourcePath = sourcePath;
                this.RelativePath = pathParts[1];
                this.PathOnDisk = Path.Combine(Repository.RepositoryPath, this.RelativePath);
            }
            public MercurialRepository Repository { get; private set; }
            public string SourcePath { get; private set; }
            public string PathOnDisk { get; private set; }
            public string RelativePath { get; private set; }
        }


        #region Properties
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
        #endregion

        #region SourceControlProviderBase
        public override char DirectorySeparator
        {
            get { return '/'; }
        }

        public override void GetLatest(string sourcePath, string targetPath)
        {
            if (targetPath == null) throw new ArgumentNullException("targetPath");
            var hgSourcePath = new MercurialPath(this, sourcePath);
            if (hgSourcePath.Repository == null) throw new ArgumentException(sourcePath + " does not represent a valid Mercurial path.", "sourcePath");

            UpdateLocalRepo(hgSourcePath.Repository);
            CopyNonHgFiles(hgSourcePath.PathOnDisk, targetPath);
        }

        public override DirectoryEntryInfo GetDirectoryEntryInfo(string sourcePath)
        {
            return GetDirectoryEntryInfo(new MercurialPath(this, sourcePath));
        }
        DirectoryEntryInfo GetDirectoryEntryInfo(MercurialPath path)
        {
            if (path.Repository == null)
            {
                var subDirs = new DirectoryEntryInfo[Repositories.Length];
                for (int i = 0; i < Repositories.Length; i++)
                    subDirs[i] = new DirectoryEntryInfo(
                        Repositories[i].RepositoryName,
                        Repositories[i].RepositoryName,
                        null,
                        null);
                return new DirectoryEntryInfo(string.Empty, string.Empty, subDirs, null);
            }
            else
            {
                UpdateLocalRepo(path.Repository);

                Exception[] exceptionsToIgnore;
                var de = Util.Files.GetDirectoryEntry(path.Repository.RepositoryPath, path.PathOnDisk, out exceptionsToIgnore, false);

                var subDirs = new List<DirectoryEntryInfo>(FilterOutHg(de.SubDirectories)).ToArray();
                for (int i = 0; i < subDirs.Length; i++)
                {
                    subDirs[i] = new DirectoryEntryInfo(
                        subDirs[i].Name,
                        path.Repository.RepositoryName + MercurialPath.RepositorySeparatorChar + subDirs[i].Path.Replace('\\', '/'),
                        null,
                        null);
                }

                var files = new List<FileEntryInfo>(FilterOutHg(de.Files)).ToArray();
                for (int i = 0; i < files.Length; i++)
                {
                    files[i] = new FileEntryInfo(
                        files[i].Name,
                        path.Repository.RepositoryName + MercurialPath.RepositorySeparatorChar + files[i].Path.Replace('\\', '/'));
                }

                return new DirectoryEntryInfo(
                    de.Name,
                    path.SourcePath,
                    subDirs,
                    files);
            }
        }
        public override byte[] GetFileContents(string filePath)
        {
            var hgSourcePath = new MercurialPath(this, filePath);
            if (hgSourcePath.Repository == null) throw new ArgumentException(filePath + " does not represent a valid Mercurial path.", "filePath");

            return File.ReadAllBytes(hgSourcePath.PathOnDisk);
        }
        #endregion

        #region ProviderBase
        public override bool IsAvailable()
        {
            return true;
        }

        public override void ValidateConnection()
        {
            foreach (MercurialRepository repo in Repositories)
            {
                ExecuteHgCommand(repo, HgCommands.manifest);
            }
        }

        public override string ToString()
        {
            if (Repositories.Length == 1)
                return "Mercurial at " + Util.CoalesceStr(Repositories[0].RemoteRepositoryUrl, Repositories[0].RepositoryPath);
            else
                return "Mercurial";

        }
        #endregion

        #region IVersioningProvider
        public void ApplyLabel(string label, string sourcePath)
        {
            if (string.IsNullOrEmpty(label)) throw new ArgumentNullException("label");
            var hgSourcePath = new MercurialPath(this, sourcePath);
            if (hgSourcePath.Repository == null) throw new ArgumentException(sourcePath + " does not represent a valid Mercurial path.", "sourcePath");

            UpdateLocalRepo(hgSourcePath.Repository);

            ExecuteHgCommand(
                hgSourcePath.Repository,
                HgCommands.tag,
                "-u \"" + Util.CoalesceStr(CommittingUser, "SYSTEM") + "\"",
                label);

            if (!string.IsNullOrEmpty(hgSourcePath.Repository.RemoteRepositoryUrl))
                ExecuteHgCommand(hgSourcePath.Repository, HgCommands.push, hgSourcePath.Repository.RemoteRepositoryUrl);
        }

        public void GetLabeled(string label, string sourcePath, string targetPath)
        {
            if (string.IsNullOrEmpty(label)) throw new ArgumentNullException("label");
            if (string.IsNullOrEmpty(targetPath)) throw new ArgumentNullException("targetPath");
            var hgSourcePath = new MercurialPath(this, sourcePath);
            if (hgSourcePath.Repository == null) throw new ArgumentException(sourcePath + " does not represent a valid Mercurial path.", "sourcePath");

            UpdateLocalRepo(hgSourcePath.Repository);

            ExecuteHgCommand(hgSourcePath.Repository, HgCommands.update, "-r \"" + label + "\"");
            CopyNonHgFiles(hgSourcePath.PathOnDisk, targetPath);
        }
        #endregion

        #region IRevisionProvider
        /// <summary>
        /// Gets some sort of "fingerprint" that represents the current revision on the source control repository
        /// </summary>
        /// <param name="path">The source control path to monitor</param>
        /// <returns>
        /// A representation of the current revision in source control
        /// </returns>
        public object GetCurrentRevision(string path)
        {
            var mercurialPath = new MercurialPath(this, path);
            if (mercurialPath.Repository == null)
                throw new ArgumentException("Path must specify a Mercurial repository.");

            UpdateLocalRepo(mercurialPath.Repository);
            var res = ExecuteHgCommand(mercurialPath.Repository, HgCommands.log, "-l", "1", "\"" + mercurialPath.RelativePath + "\"");

            var resList = res.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var rev = new byte[6];
            if (resList.Length == 0)
                return rev;

            var line = resList[0];

            int startIndex = line.LastIndexOf(':') + 1;
            if (startIndex == 0)
                return rev;

            for (int i = 0; i < rev.Length; i++)
                rev[i] = byte.Parse(line.Substring(startIndex + i * 2, 2), NumberStyles.HexNumber);

            return rev;
        }
        #endregion

        /// <summary>
        /// Copies files and subfolders from sourceFolder to targetFolder.
        /// </summary>
        /// <param name="sourceFolder">A path of the folder to be copied</param>
        /// <param name="targetFolder">A path of a folder to copy files to.  If targetFolder doesn't exist, it is created.</param>
        static void CopyNonHgFiles(string sourceFolder, string targetFolder)
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

        static void DeleteHgRemnants(string path)
        {
            var archivalFile = Path.Combine(path, ".hg_archival.txt");
            if (File.Exists(path)) File.Delete(archivalFile);
        }
        void UpdateLocalRepo(MercurialRepository repo)
        {
            // pull changes if remote repository is used
            if (!string.IsNullOrEmpty(repo.RemoteRepositoryUrl))
                ExecuteHgCommand(repo, HgCommands.pull, repo.RemoteRepositoryUrl);

            // update the working repository
            ExecuteHgCommand(repo, HgCommands.update);
        }

        string ExecuteHgCommand(MercurialRepository repo, string hgCommand, params string[] options)
        {
            if (repo == null) throw new ArgumentNullException("repo");

            // verify paths
            if (string.IsNullOrEmpty(HgExecutablePath) || !File.Exists(HgExecutablePath)) throw new NotAvailableException("hg executable not found at '" + HgExecutablePath + "'");
            if (string.IsNullOrEmpty(repo.RepositoryPath) || !Directory.Exists(Path.Combine(repo.RepositoryPath, ".hg"))) throw new NotAvailableException("A local repository was not found at '" + repo.RepositoryPath + "'");

            // prepare arguments
            var args = new StringBuilder();
            args.AppendFormat("{0} -R \"{1}\" -y -v ", hgCommand, repo.RepositoryPath);
            args.Append(string.Join(" ", (options ?? new string[0])));

            // prepare process
            var hgProcStart = new ProcessStartInfo
            {
                FileName = HgExecutablePath,
                Arguments = args.ToString(),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            this.LogProcessExecution(hgProcStart);

            // run process
            var hgProc = Process.Start(hgProcStart);
            string cmdResult = hgProc.StandardOutput.ReadToEnd();
            string cmdError = hgProc.StandardError.ReadToEnd();
            hgProc.WaitForExit();
            hgProc.Close();

            // validate and return output
            if (!string.IsNullOrEmpty(cmdError)) throw new InvalidOperationException(cmdError);
            return cmdResult;
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
