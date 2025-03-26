using System.Windows;
using ChangeProperties.ViewModels;

namespace ChangeProperties
{
    public partial class LoadingWindow : Window
    {

        public LoadingWindow()
        {
            InitializeComponent();

            var viewModel = new LoadingViewModel();
            viewModel.RequestClose += () => this.Close(); // Podpięcie zamknięcia
            DataContext = viewModel;
        }
        public void UpdateProgress(int value)
        {
            Dispatcher.Invoke(() =>
            {
                pasekPostepu.Value = value;
            });
        }
        public void StopIndeterminate()
        {
            pasekPostepu.IsIndeterminate = false;
        }
    }
}