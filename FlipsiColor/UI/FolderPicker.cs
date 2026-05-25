using System;
using System.Runtime.InteropServices;

namespace FlipsiColor.UI;

/// <summary>
/// Native Win32 Folder Browser Dialog — kein WinForms nötig
/// </summary>
public static class FolderPicker
{
    public static string? OpenFolder(string title = "Ordner auswählen")
    {
        var dialog = new FolderBrowserDialog
        {
            Title = title
        };

        if (dialog.Show() == 0) // S_OK
        {
            return dialog.GetFolder();
        }

        return null;
    }

    private class FolderBrowserDialog : IDisposable
    {
        private IFileDialog? _dialog;

        public string Title { get; set; } = "Ordner auswählen";

        public FolderBrowserDialog() { }

        public int Show()
        {
            _dialog = (IFileDialog)new FileOpenDialogRCW();
            _dialog.SetOptions(FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM);
            _dialog.SetTitle(Title);
            return _dialog.Show(IntPtr.Zero);
        }

        public string GetFolder()
        {
            if (_dialog == null) return "";
            _dialog.GetResult(out var item);
            item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
            return path;
        }

        public void Dispose()
        {
            if (_dialog != null)
            {
                Marshal.ReleaseComObject(_dialog);
                _dialog = null;
            }
        }
    }

    // COM Interfaces
    [ComImport]
    [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialogRCW { }

    [ComImport]
    [Guid("d57c7288-d4ad-4768-be02-9d9648e8e8d4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        [PreserveSig] int SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
        [PreserveSig] int SetFileTypeIndex(uint iFileType);
        [PreserveSig] int GetFileTypeIndex(out uint piFileType);
        [PreserveSig] int Advise(IntPtr pfde, out uint pdwCookie);
        [PreserveSig] int Unadvise(uint dwCookie);
        [PreserveSig] int SetOptions(FOS fos);
        [PreserveSig] int GetOptions(out FOS pfos);
        [PreserveSig] int SetDefaultFolder(IShellItem psi);
        [PreserveSig] int SetFolder(IShellItem psi);
        [PreserveSig] int GetFolder(out IShellItem ppsi);
        [PreserveSig] int GetCurrentSelection(out IShellItem ppsi);
        [PreserveSig] int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        [PreserveSig] int GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        [PreserveSig] int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        [PreserveSig] int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        [PreserveSig] int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        [PreserveSig] int GetResult(out IShellItem ppsi);
        [PreserveSig] int AddPlace(IShellItem psi, int fdap);
        [PreserveSig] int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        [PreserveSig] int Close(int hr);
        [PreserveSig] int SetClientGuid(ref Guid guid);
        [PreserveSig] int ClearClientData();
        [PreserveSig] int SetFilter(IntPtr pFilter);
    }

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c9c8c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetParent(out IShellItem ppsi);
        [PreserveSig] int GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        [PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        [PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [Flags]
    private enum FOS : uint
    {
        FOS_PICKFOLDERS = 0x00000020,
        FOS_FORCEFILESYSTEM = 0x00000004,
    }

    private enum SIGDN : uint
    {
        SIGDN_FILESYSPATH = 0x80058000,
    }
}