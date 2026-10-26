using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Win10WebmCut;

/// <summary>Windows local-file revision; identity also catches hard links and DOS path aliases.</summary>
public sealed record FileRevision(uint Volume, ulong FileId, long Length, long LastWriteFileTime)
{
    public bool SameFile(FileRevision other) => Volume == other.Volume && FileId == other.FileId;
    public static FileRevision Read(string path)
    {
        using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (!GetFileInformationByHandle(handle, out HandleInformation info))
            throw new IOException("Nie można odczytać tożsamości lokalnego pliku.", new Win32Exception(Marshal.GetLastWin32Error()));
        ulong id = ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
        ulong size = ((ulong)info.FileSizeHigh << 32) | info.FileSizeLow;
        long writeTime = ((long)(uint)info.LastWriteTime.dwHighDateTime << 32) | (uint)info.LastWriteTime.dwLowDateTime;
        return new(info.VolumeSerialNumber, id, checked((long)size), writeTime);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out HandleInformation information);
    [StructLayout(LayoutKind.Sequential)]
    private struct HandleInformation
    {
        public uint FileAttributes;
        public FILETIME CreationTime;
        public FILETIME LastAccessTime;
        public FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
