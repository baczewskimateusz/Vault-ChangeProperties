using System.Threading;

namespace ChangeProperties.Models
{
    public class LoadingWindowHelper
    {
        private LoadingWindow loadingWindow;
        private Thread progressThread;

        public void ShowProgressWindow()
        {
            progressThread = new Thread(() =>
            {
                loadingWindow = new LoadingWindow();
                loadingWindow.Show();
                System.Windows.Threading.Dispatcher.Run();
            });
            progressThread.SetApartmentState(ApartmentState.STA);
            progressThread.IsBackground = true;
            progressThread.Start();
        }

        public void CloseProgressWindow()
        {
            if (loadingWindow != null)
            {
                loadingWindow.Dispatcher.Invoke(() =>
                {
                    loadingWindow.Close();
                });
            }
        }

        public void UpdateProgress(int value)
        {
            if (loadingWindow != null)
            {
                loadingWindow.Dispatcher.Invoke(() =>
                {
                    loadingWindow.UpdateProgress(value);
                });
            }
        }
    }
   
}
