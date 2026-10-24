using System;
using System.Windows;
using PaintNetClone.Core.Document;

namespace PaintNetClone
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Length > 0)
            {
                string filePath = e.Args[0];
                // Handle file opening on startup
            }
        }
    }
}