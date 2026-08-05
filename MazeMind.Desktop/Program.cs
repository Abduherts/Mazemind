using System;
using System.Windows.Forms;

namespace MazeMind.Desktop
{
    // Entry point of the MazeMind desktop application.
    // This class is responsible for starting the Windows Forms program.
    internal static class Program
    {
        // Specifies that the application uses a Single-Threaded Apartment (STA),
        // which is required for many Windows components such as dialogs,
        // clipboard operations, drag-and-drop functionality, and COM objects.
        [STAThread]

        // Main method where the application begins execution.
        private static void Main()
        {
            // Initializes the default Windows Forms application configuration,
            // including high DPI support, visual styles, and text rendering settings.
            ApplicationConfiguration.Initialize();

            // Creates the main game window and starts the application's
            // message loop. The program continues running until this window
            // is closed by the user.
            Application.Run(new MazeWindow());
        }
    }
}
