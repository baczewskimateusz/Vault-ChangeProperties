using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;

namespace ChangeProperties.ViewModels
{
    public class LoadingViewModel : INotifyPropertyChanged
    {
        public event Action RequestClose;

        public RelayCommand CancelCommand { get; }

        public LoadingViewModel()
        {
            CancelCommand = new RelayCommand(() =>
            {
                RequestClose?.Invoke(); 
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
