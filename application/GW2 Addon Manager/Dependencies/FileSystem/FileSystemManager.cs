using System.IO;

namespace GW2_Addon_Manager.Dependencies.FileSystem
{
    class FileSystemManager : IFileSystemManager
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public void DirectoryDelete(string path, bool recursive) => Directory.Delete(path, recursive);
        public void DirectoryMove(string sourceDirName, string destDirName)
        {
            if (!Directory.Exists(destDirName))
                Directory.CreateDirectory(destDirName);

            var files = Directory.GetFiles(sourceDirName);
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                var dest = Path.Combine(destDirName, name);
                File.Move(file, dest);
            }

            var folders = Directory.GetDirectories(sourceDirName);
            foreach (var folder in folders)
            {
                var name = Path.GetFileName(folder);
                var dest = Path.Combine(destDirName, name);
                DirectoryMove(folder, dest);
            }

            Directory.Delete(sourceDirName, true);
        }

        public void FileDelete(string path) => File.Delete(path);
        public string PathCombine(string path1, string path2) => Path.Combine(path1, path2);
    }
}