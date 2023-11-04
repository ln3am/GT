using System.Windows;

namespace GT
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        GravTime grav = new GravTime();

        public MainWindow()
        {
            InitializeComponent();
        }
        private void StartOnClick(object sender, RoutedEventArgs e)
        {
            grav.OnStart(outputTextBox, outputFalseText, Dispatcher);
        }
        private void StopOnClick(object sender, RoutedEventArgs e)
        {
            grav.OnStop();
        }
    }
}
