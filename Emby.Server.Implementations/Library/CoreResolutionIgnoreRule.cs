using System;
using System.IO;
using Emby.Naming.Audio;
using Emby.Naming.Common;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.IO;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Provides the core resolver ignore rules.
    /// </summary>
    public class CoreResolutionIgnoreRule : IResolverIgnoreRule
    {
        private readonly NamingOptions _namingOptions;
        private readonly IServerApplicationPaths _serverApplicationPaths;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoreResolutionIgnoreRule"/> class.
        /// </summary>
        /// <param name="namingOptions">The naming options.</param>
        /// <param name="serverApplicationPaths">The server application paths.</param>
        public CoreResolutionIgnoreRule(NamingOptions namingOptions, IServerApplicationPaths serverApplicationPaths)
        {
            _namingOptions = namingOptions;
            _serverApplicationPaths = serverApplicationPaths;
        }

        /// <inheritdoc />
        public bool ShouldIgnore(FileSystemMetadata fileInfo, BaseItem parent)
        {
            // Don't ignore application folders
            if (fileInfo.FullName.Contains(_serverApplicationPaths.RootFolderPath, StringComparison.InvariantCulture))
            {
                return false;
            }

            // Don't ignore top level folders
            if (fileInfo.IsDirectory && parent is AggregateFolder)
            {
                return false;
            }

            if (IgnorePatterns.ShouldIgnore(fileInfo.FullName))
            {
                return true;
            }

            var filename = fileInfo.Name;

            if (fileInfo.IsDirectory)
            {
                if (parent != null)
                {
                    // Ignore trailer folders but allow it at the collection level
                    if (string.Equals(filename, BaseItem.TrailersFolderName, StringComparison.OrdinalIgnoreCase)
                        && !(parent is AggregateFolder)
                        && !(parent is UserRootFolder))
                    {
                        return true;
                    }

                    if (string.Equals(filename, BaseItem.ThemeVideosFolderName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (string.Equals(filename, BaseItem.ThemeSongsFolderName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            else
            {
                if (parent != null)
                {
                    // Don't resolve these into audio files
                    if (Path.GetFileNameWithoutExtension(filename.AsSpan()).Equals(BaseItem.ThemeSongFileName, StringComparison.Ordinal)
                        && AudioFileParser.IsAudioFile(filename, _namingOptions))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
