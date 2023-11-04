using System;
using System.Drawing;
using System.Threading.Tasks;
using System.IO;
using System.Timers;
using System.Windows.Forms;
using Tesseract;
using System.Text.RegularExpressions;
using System.Text;
using System.Windows.Threading;

namespace GT
{
    public class GravTime
    {
        GetBitMap getmap = new GetBitMap();
        ProcessScreenshot process = new ProcessScreenshot();
        DataToChart chart = new DataToChart();
        public event Action<Bitmap> ScreenshotCaptured;
        private string result1 = "";
        private System.Windows.Controls.TextBox textbox1;
        private System.Windows.Controls.TextBox textbox2;
        Dispatcher mdispatcher;
        public event Action<string> ResultProcessed;
       
        public void OnStart(System.Windows.Controls.TextBox text1, System.Windows.Controls.TextBox text2, Dispatcher maindispatcher)
        {
            mdispatcher = maindispatcher;
            textbox1 = text1;
            textbox2 = text2;
            getmap.Write("Service is started at " + DateTime.Now, textbox1, 0.1);
            getmap.ScreenshotCaptured += OnScreenshotCaptured;
            ResultProcessed += OnResultProcessed;
            getmap.TakeScreenShot(textbox1);
        }
        public void OnStop()
        {
            getmap.Stoptimer();
            getmap.Write("Service is stopped at " + DateTime.Now, textbox1, 0.1);
        }
        private async void OnScreenshotCaptured(Bitmap screenshot)
        {
            Task<string> processingTask = process.ProcesScreenshot(screenshot);
            string result = await processingTask;
            if (result.ToLower() == result1)
            {
                return;
            }
            result1 = result.ToLower();
           // if (result1.Contains("you finished all maps") || result1.Contains("the winning maps are"))
           // {
                RaiseResultProcessed(result);
                chart.Data(result1, textbox2);
            //};
        }
        private void OnResultProcessed(string result)
        {
            mdispatcher.Invoke(() => getmap.Write(result, textbox2, 0.1));
        }
        private void RaiseResultProcessed(string result)
        {
            ResultProcessed?.Invoke(result);
        }
    }
    public class GetBitMap
    {
        System.Timers.Timer timer = new System.Timers.Timer();
        private Bitmap screenshot;
        public event Action<Bitmap> ScreenshotCaptured;
        public void TakeScreenShot(System.Windows.Controls.TextBox textbox1)
        {
            timer.Elapsed += (sender, e) =>
            {
                OnElapsedTime(textbox1);
            };
            timer.Interval = 10000;
            timer.Start();
        }
        public void Stoptimer()
        {
            timer.Stop();
        }
        private void OnElapsedTime(System.Windows.Controls.TextBox textbox1)
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            screenshot = new Bitmap(bounds.Width, bounds.Height);
            using (Graphics graphics = Graphics.FromImage(screenshot))
            {
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
            }
            ScreenshotCaptured?.Invoke(screenshot);
        }
        public void Write(string message, System.Windows.Controls.TextBox textbox, double writespeed)
        {
            ControlWriter cw = new ControlWriter(textbox, writespeed);
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(filepath))
            {
                using (StreamWriter sw = File.CreateText(filepath))
                {
                    sw.WriteLine(message);
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(filepath))
                {
                    sw.WriteLine(message);
                }
            }
            cw.Write(message);

        }
    }
    public class ProcessScreenshot
    {
        public async Task<string> ProcesScreenshot(Bitmap screnshot)
        {
            using (var engine = new TesseractEngine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata"), "eng", EngineMode.Default))
            {
                using (var image = PixConverter.ToPix(screnshot))
                {
                    using (var page = engine.Process(image))
                    {
                        var text = page.GetText();
                        return text;
                    }
                }
            }
        }
    }
    public class DataToChart
    {
        GetBitMap map = new GetBitMap();
        public async Task Data(string screentext, System.Windows.Controls.TextBox textbox2)
        {
            string[] words = screentext.Split(" ");
            foreach (string word in words)
            {

            }
        }
    }
    public class ControlWriter : TextWriter
    {
        private System.Windows.Controls.TextBox textBox;
        private int currentIndex;
        private string textToDisplay;
        private DispatcherTimer timer2;

        public ControlWriter(System.Windows.Controls.TextBox textBox, double writespeed)
        {
            this.textBox = textBox;
            currentIndex = 0;
            textToDisplay = "";
            timer2 = new DispatcherTimer();
            timer2.Interval = TimeSpan.FromMilliseconds(writespeed);
            timer2.Tick += Timer_Tick;
        }
        public override void Write(char value)
        {
            textToDisplay += value;
            StartTimer2();
        }
        public override void Write(string value)
        {
            textToDisplay += value;
            StartTimer2();
        }
        public override Encoding Encoding => Encoding.ASCII;

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (currentIndex < textToDisplay.Length)
            {
                textBox.AppendText(textToDisplay[currentIndex].ToString());
                currentIndex++;
            }
            else
            {
                StopTimer();
            }
        }

        private void StartTimer2()
        {
            if (!timer2.IsEnabled)
            {
                timer2.Start();
            }
        }

        private void StopTimer()
        {
            if (timer2.IsEnabled)
            {
                timer2.Stop();
            }
        }
    }
}

