using System.Runtime.InteropServices;
using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using LiveCharts;
using LiveCharts.Wpf;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using LiveCharts.Configurations;
using System.Globalization;
using System.Threading;


namespace GT
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        GravTime grav = new GravTime();
        ViewModel vm1 = new ViewModel();
        public MainWindow()
        {
            InitializeComponent();
            CultureInfo culture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            DataContext = vm1;
        }
        private void VisualizeData(string maps, double times, double optimaltimes, double timedifference)
        {
            string[] smaps = maps.Split(' ');
            string mapshow = "";
            foreach (string map in smaps)
            {
                mapshow += map + "\n";   
            }
            vm1.Labels.Add(mapshow);    
          //  vm2.Labels1.Add(map);
            vm1.CollectionDifferenceTime[0].Values.Add(timedifference);
          //  vm2.SeriesCollection1[0].Values.Add(times);
          //  vm2.SeriesCollection1[1].Values.Add(optimaltimes);
        }
        private void MaximizeWindow(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowState = WindowState.Normal;
            }
        }
        private void MinimizeWindow(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void StartOnClick(object sender, RoutedEventArgs e)
        {
            grav.FinishedMapTimes += VisualizeData;
            grav.OnStart(outputTextBox, outputFalseText, Dispatcher);
        }
        private void StopOnClick(object sender, RoutedEventArgs e)
        {
            grav.OnStop();
        }
        private bool switchon = false;
        private void SwitchViewOnClick(object sender, RoutedEventArgs e)
        {
            switchon = switchon ? false : true;
            if (switchon)
            {
                rectext.Visibility = Visibility.Collapsed;
                rectextblock.Visibility = Visibility.Collapsed;
                consoletext.Visibility = Visibility.Collapsed;
                consoletextblock.Visibility = Visibility.Collapsed;
                bordermini.Visibility = Visibility.Collapsed;
            }
            else
            {
                rectext.Visibility = Visibility.Visible;
                rectextblock.Visibility = Visibility.Visible;
                consoletext.Visibility = Visibility.Visible;
                consoletextblock.Visibility = Visibility.Visible;
                bordermini.Visibility = Visibility.Visible;
            }
        }
        private void CaptureValueSpeedOnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int value = (int)capturevaluespeed.Value;
            switch (value)
            {
                case 1:
                    grav.getmap.timetick = 1;
                    break;
                case 2:
                    grav.getmap.timetick = 2;
                    break;
                case 3:
                    grav.getmap.timetick = 3;
                    break;
                case 4:
                    grav.getmap.timetick = 4;
                    break;
                case 5:
                    grav.getmap.timetick = 5;
                    break;  
            }
        }
        private bool manualcapture = false;
        private void TakeManualScreenShot()
        {
            //add new window with Idisposable to create custom key shortcut
        }
        private void CaptureSpeedOnClick(object sender, RoutedEventArgs e)
        {
            manualcapture = manualcapture ? false : true;
            if (manualcapture)
            {
                capturevaluespeed.Visibility = Visibility.Collapsed;
                grav.getmap.Stoptimer();
            }
            else 
            {
                capturevaluespeed.Visibility = Visibility.Visible;
                grav.getmap.Starttimer();
            }
        }
    }
    public class ViewModel : INotifyPropertyChanged
    {
        public ViewModel()
        {
            var mapper = Mappers.Xy<double>()
           .X((value, index) => index)
           .Y(value => value)
           .Fill(value =>
           {
               if (value > 6)
                   return Brushes.Red;
               else if (value > 3)
                   return Brushes.Orange;
               else if (value > 1)
                   return Brushes.Yellow;
               else if (value > 0)
                   return Brushes.LightBlue;
               else
                   return Brushes.Green;
           }
                     );

            CollectionDifferenceTime = new SeriesCollection(mapper)
            {
                 new LineSeries 
            {
                Title = "Time Difference",
                Values = new ChartValues<double> { 0 },
                PointGeometry = DefaultGeometries.Circle,
                Stroke = Brushes.LightBlue,
                PointGeometrySize = 15,
                StrokeThickness = 2 
            }
            };
            Labels = new List<string> { "Maps" };
        }
        public SeriesCollection CollectionDifferenceTime { get; set; }
        public List<string> Labels { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;   
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }   
    }   
}