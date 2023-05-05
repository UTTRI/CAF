using Ookii.Dialogs.Wpf;
using System.Windows;

namespace CellphoneProcessor.Utilities;

internal static class FileSelection
{
    /// <summary>
    /// Launch a GUI to select a file.
    /// </summary>
    /// <param name="assignment">The process for what to do after the file is selected.</param>
    /// <param name="fileExists">Does the file need to already exist?</param>
    public static void SelectFile<T>(this FrameworkElement us, Action<T, string> assignment, bool fileExists)
    {
        VistaOpenFileDialog dialog = new()
        {
            Multiselect = false,
            CheckFileExists = fileExists,
        };
        if (dialog.ShowDialog() == true
            && us.DataContext is T vm)
        {
            assignment(vm, dialog.FileName);
        }
    }

    public static void SelectDirectory<T>(this FrameworkElement us, Action<T, string> assignment)
    {
        VistaFolderBrowserDialog dialog = new()
        {
            Multiselect = false,
        };
        if (dialog.ShowDialog() == true
            && us.DataContext is T vm)
        {
            assignment(vm, dialog.SelectedPath);
        }
    }
}
