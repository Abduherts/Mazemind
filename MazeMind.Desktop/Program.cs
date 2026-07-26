using System;
using System.Windows.Forms;

namespace MazeMind.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MazeWindow());
        }
    }
}