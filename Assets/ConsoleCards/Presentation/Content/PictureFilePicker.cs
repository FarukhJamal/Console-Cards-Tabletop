using System;
using System.Runtime.InteropServices;

namespace ConsoleCards.Presentation.Content
{
    /// <summary>
    /// Lets the player choose a picture file (doc 24, A4): Unity's file panel in the Editor, the Windows file dialog
    /// in a Windows build. Elsewhere it is unavailable and the panel offers the uploads folder instead.
    /// </summary>
    public static class PictureFilePicker
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        public static bool IsAvailable => true;
#else
        public static bool IsAvailable => false;
#endif

        /// <summary>Shows the file dialog (it blocks until closed). True with a path when a picture was chosen.</summary>
        public static bool TryPick(string title, out string path)
        {
            path = string.Empty;
#if UNITY_EDITOR
            path = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                title,
                string.Empty,
                new[] { "Pictures", "png,jpg,jpeg" });
            return !string.IsNullOrEmpty(path);
#elif UNITY_STANDALONE_WIN
            return TryPickWindows(title, out path);
#else
            return false;
#endif
        }

        /// <summary>
        /// Card studio IMPORT A FOLDER OF PNGs: a folder in the Editor; in a Windows build the player picks one picture
        /// and its folder is used.
        /// </summary>
        public static bool TryPickFolder(string title, out string folder)
        {
            folder = string.Empty;
#if UNITY_EDITOR
            folder = UnityEditor.EditorUtility.OpenFolderPanel(title, string.Empty, string.Empty);
            return !string.IsNullOrEmpty(folder);
#elif UNITY_STANDALONE_WIN
            if (!TryPickWindows(title + " (pick any picture in it)", out string picture)) return false;
            folder = System.IO.Path.GetDirectoryName(picture) ?? string.Empty;
            return folder.Length > 0;
#else
            return false;
#endif
        }

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        private const int MaximumPathCharacters = 1024;
        private const int OfnPathMustExist = 0x00000800;
        private const int OfnFileMustExist = 0x00001000;
        private const int OfnNoChangeDir = 0x00000008;
        private const int OfnExplorer = 0x00080000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileNameW(ref OpenFileName dialog);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        private static bool TryPickWindows(string title, out string path)
        {
            path = string.Empty;
            int bufferBytes = MaximumPathCharacters * 2;
            IntPtr buffer = Marshal.AllocHGlobal(bufferBytes);
            try
            {
                Marshal.Copy(new byte[bufferBytes], 0, buffer, bufferBytes);
                OpenFileName dialog = new OpenFileName
                {
                    lStructSize = Marshal.SizeOf(typeof(OpenFileName)),
                    hwndOwner = GetActiveWindow(),
                    lpstrFilter = "Pictures (PNG, JPG)\0*.png;*.jpg;*.jpeg\0\0",
                    nFilterIndex = 1,
                    lpstrFile = buffer,
                    nMaxFile = MaximumPathCharacters,
                    lpstrTitle = title,
                    Flags = OfnExplorer | OfnPathMustExist | OfnFileMustExist | OfnNoChangeDir,
                };
                if (!GetOpenFileNameW(ref dialog))
                {
                    return false;
                }

                path = Marshal.PtrToStringUni(buffer) ?? string.Empty;
                return path.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
#endif
    }
}
