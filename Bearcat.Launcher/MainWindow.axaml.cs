using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace Bearcat.Launcher;

public partial class MainWindow : Window
{
    public MainWindow(bool isInitialStart)
    {
        InitializeComponent();

        if (isInitialStart)
        {
            CloseWindowAfterDelay();
        } 
    }
    
    private async Task CloseWindowAfterDelay()
    {
        await Task.Delay(TimeSpan.FromSeconds(3));
        Close();
    }
}
