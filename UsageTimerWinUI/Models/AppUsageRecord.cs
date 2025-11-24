using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace UsageTimerWinUI.Models
{
    public class AppUsageRecord : INotifyPropertyChanged
    {
        private double _minutes;
        private string _formatted = "";

        public string _finalName = "";

        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public double Seconds { get; set; }
        
        public string FinalName
        {
            get => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;
            set { _finalName = value; OnPropertyChanged(); }
        }

        public double Minutes
        {
            get => _minutes;
            set { _minutes = value; OnPropertyChanged(); }
        }
        public string Formatted
        {
            get => _formatted;
            set { _formatted = value; OnPropertyChanged(); }
        }

        public Microsoft.UI.Xaml.Media.ImageSource? Icon { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
