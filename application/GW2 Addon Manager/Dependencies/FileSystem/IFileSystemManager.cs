using System.IO;

namespace GW2_Addon_Manager.Dependencies.FileSystem
{
    public interface IFileSystemManager
    {
        bool DirectoryExists(string path);
        void DirectoryDelete(string path, bool recursive);

        /// <summary>
        ///     Moves folder with all of its contents from source path to destination. If destination directory does not exists, it
        ///     will be created.
        /// </summary>
        /// <remarks>
        ///     Because <see cref="Directory.Move" /> does not work across different drives, directory contents is recursively
        ///     copied with <see cref="File.Move" />.
        ///     <para>
        ///         <seealso href="https://stackoverflow.com/questions/23927702/move-a-folder-from-one-drive-to-another-in-c-sharp" />
        ///     </para>
        /// </remarks>
        /// <param name="sourceDirName">Path of directory to copy.</param>
        /// <param name="destDirName">Path of destination directory.</param>
        void DirectoryMove(string sourceDirName, string destDirName);

        void FileDelete(string path);

        string PathCombine(string path1, string path2);
    }
}