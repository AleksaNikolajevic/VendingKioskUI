using System.Configuration;
using System.Data;
using System.IO;
using System.IO.Pipes;
using System.Windows;

namespace VendingKioskUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Task.Run(() => StartPipeServer());
        }

        private async Task StartPipeServer()
        {
            var pipe = new NamedPipeServerStream("MyNavigationPipe", PipeDirection.In);

            while (true)
            {
                await pipe.WaitForConnectionAsync();
                using var reader = new StreamReader(pipe);

                string command = reader.ReadLine();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (command == "GoToPage1")
                    {
                        var frame = ((MainWindow)Application.Current.MainWindow).MainFrame;
                        frame.Navigate(new Page1());
                    }

                    if (command == "GoToPage2")
                    {
                        var frame = ((MainWindow)Application.Current.MainWindow).MainFrame;
                        frame.Navigate(new Page2());
                    }
                });

                pipe.Disconnect();
            }

        }

    }
}