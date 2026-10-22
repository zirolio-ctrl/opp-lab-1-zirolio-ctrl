using CommunityToolkit.Mvvm.ComponentModel;

namespace opp_lab_1_zirolio_ctrl.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Greeting { get; set; } = "Welcome to Avalonia!";
}
