using System;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.IO;

namespace Inedo.Extensions.Shared.Mercurial.Clients
{
    public abstract class MercurialClient
    {
        protected ILogSink log;
        protected MercurialRepositoryInfo repository;

        protected MercurialClient(MercurialRepositoryInfo repository, ILogSink log)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public abstract Task<bool> IsRepositoryValidAsync();
        public abstract Task CloneAsync(MercurialCloneOptions options);
        public abstract Task UpdateAsync(MercurialUpdateOptions options);
        public abstract Task ArchiveAsync(string targetDirectory);
        public abstract Task TagAsync(string tag);

        protected static async Task CopyNonMercurialFilesAsync(IFileOperationsExecuter fileOps, string sourceDirectory, string targetDirectory)
        {
            if (!await fileOps.DirectoryExistsAsync(sourceDirectory).ConfigureAwait(false))
                return;

            char separator = fileOps.DirectorySeparator;

            var infos = await fileOps.GetFileSystemInfosAsync(sourceDirectory, new MaskingContext(new[] { "**" }, new[] { "**" + separator + ".hg**" })).ConfigureAwait(false);

            var directoriesToCreate = infos.OfType<SlimDirectoryInfo>().Select(d => CombinePaths(targetDirectory, d.FullName.Substring(sourceDirectory.Length), separator)).ToArray();
            var relativeFileNames = infos.OfType<SlimFileInfo>().Select(f => f.FullName.Substring(sourceDirectory.Length).TrimStart(separator)).ToArray();

            await fileOps.CreateDirectoryAsync(targetDirectory).ConfigureAwait(false);

            foreach (string folder in directoriesToCreate)
                await fileOps.CreateDirectoryAsync(folder).ConfigureAwait(false);

            await fileOps.FileCopyBatchAsync(
                sourceDirectory,
                relativeFileNames,
                targetDirectory,
                relativeFileNames,
                true,
                true
            ).ConfigureAwait(false);
        }

        private static string CombinePaths(string p1, string p2, char separator)
        {
            return p1.TrimEnd(separator) + separator + p2.TrimStart(separator);
        }
    }
}
