using Ryujinx.HLE.FileSystem.Content;
using Ryujinx.HLE.HOS;
using System;
using System.IO;

namespace Ryujinx.HLE.FileSystem
{
    class VirtualFileSystem : IDisposable
    {
        public const string BasePath   = "RyuFs";
        public const string NandPath   = "nand";
        public const string SdCardPath = "sdmc";
        public const string SystemPath = "system";

        public static string SafeNandPath   = Path.Combine(NandPath, "safe");
        public static string SystemNandPath = Path.Combine(NandPath, "system");
        public static string UserNandPath   = Path.Combine(NandPath, "user");

        public Stream RomFs { get; private set; }

        public void LoadRomFs(string FileName)
        {
            RomFs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
        }

        public void SetRomFs(Stream RomfsStream)
        {
            RomFs?.Close();
            RomFs = RomfsStream;
        }

        public string GetFullPath(string BasePath, string FileName)
        {
            if (FileName.StartsWith("//"))
            {
                FileName = FileName.Substring(2);
            }
            else if (FileName.StartsWith('/'))
            {
                FileName = FileName.Substring(1);
            }
            else
            {
                return null;
            }

            string FullPath = Path.GetFullPath(Path.Combine(BasePath, FileName));

            if (!FullPath.StartsWith(GetBasePath()))
            {
                return null;
            }

            return FullPath;
        }

        public string GetSdCardPath() => MakeDirAndGetFullPath(SdCardPath);

        public string GetNandPath() => MakeDirAndGetFullPath(NandPath);

        public string GetSystemPath() => MakeDirAndGetFullPath(SystemPath);

        public string GetGameSavePath(SaveInfo Save, ServiceCtx Context)
        {
            return MakeDirAndGetFullPath(SaveHelper.GetSavePath(Save, Context));
        }

        public string GetFullPartitionPath(string PartitionPath)
        {
            return MakeDirAndGetFullPath(PartitionPath);
        }

        public string SwitchPathToSystemPath(string SwitchPath)
        {
            string[] Parts = SwitchPath.Split(":");

            if (Parts.Length != 2)
            {
                return null;
            }

            return GetFullPath(MakeDirAndGetFullPath(Parts[0]), Parts[1]);
        }

        public string SystemPathToSwitchPath(string SystemPath)
        {
            string BaseSystemPath = GetBasePath() + Path.DirectorySeparatorChar;

            if (SystemPath.StartsWith(BaseSystemPath))
            {
                string RawPath              = SystemPath.Replace(BaseSystemPath, "");
                int    FirstSeparatorOffset = RawPath.IndexOf(Path.DirectorySeparatorChar);

                if (FirstSeparatorOffset == -1)
                {
                    return $"{RawPath}:/";
                }

                string BasePath = RawPath.Substring(0, FirstSeparatorOffset);
                string FileName = RawPath.Substring(FirstSeparatorOffset + 1);

                return $"{BasePath}:/{FileName}";
            }
            return null;
        }

        private string MakeDirAndGetFullPath(string Dir)
        {
            // Handles Common Switch Content Paths
            switch (Dir)
            {
                case ContentPath.SdCard:
                case "@Sdcard":
                    Dir = SdCardPath;
                    break;
                case ContentPath.User:
                    Dir = UserNandPath;
                    break;
                case ContentPath.System:
                    Dir = SystemNandPath;
                    break;
                case ContentPath.SdCardContent:
                    Dir = Path.Combine(SdCardPath, "Nintendo", "Contents");
                    break;
                case ContentPath.UserContent:
                    Dir = Path.Combine(UserNandPath, "Contents");
                    break;
                case ContentPath.SystemContent:
                    Dir = Path.Combine(SystemNandPath, "Contents");
                    break;
            }

            string FullPath = Path.Combine(GetBasePath(), Dir);

            if (!Directory.Exists(FullPath))
            {
                Directory.CreateDirectory(FullPath);
            }

            return FullPath;
        }

        public DriveInfo GetDrive()
        {
            return new DriveInfo(Path.GetPathRoot(GetBasePath()));
        }

        public string GetBasePath()
        {
            string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            return Path.Combine(AppDataPath, BasePath);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                RomFs?.Dispose();
            }
        }
    }
}