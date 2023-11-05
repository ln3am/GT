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
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace GT
{
    public class GravTime
    {
        string path = AppDomain.CurrentDomain.BaseDirectory;
        GetBitMap getmap = new GetBitMap();
        ProcessScreenshot process = new ProcessScreenshot();
        private string result1 = "";
        private System.Windows.Controls.TextBox textbox1;
        private System.Windows.Controls.TextBox textbox2;
        Dispatcher mdispatcher;
        public event Action<string, bool> ResultProcessed;
        private Dictionary<string, double> mapstime = new Dictionary<string, double>();
        private Dictionary<string, double> gravmapstimes = new Dictionary<string, double>();
        private TaskQueueProcessor _processor = new TaskQueueProcessor();
        private Dictionary<string, double> copylastinstance = new Dictionary<string, double>();

        public void OnStart(System.Windows.Controls.TextBox text1, System.Windows.Controls.TextBox text2, Dispatcher maindispatcher)
        {
            mdispatcher = maindispatcher;
            textbox1 = text1;
            textbox2 = text2;
            getmap.Write("Service was started at " + DateTime.Now, textbox1, 0.1);
            gravmapstimes = GetTimes();
            getmap.ScreenshotCaptured += async (screenshot) =>
            {
                try
                {
                    await OnScreenshotCaptured(screenshot);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Exception in async event handler: {ex}");
                }
            };
            ResultProcessed += OnResultProcessed;
            getmap.TakeScreenShot(textbox1);
        }
        public void OnStop()
        {
            getmap.Stoptimer();
            getmap.Write("\nService was stopped at " + DateTime.Now, textbox1, 0.1);
        }
        private void OnResultProcessed(string result, bool istextbox1)
        {
            System.Windows.Controls.TextBox textboxx = istextbox1 ? textbox1 : textbox2;
            mdispatcher.Invoke(() => getmap.Write(result, textboxx, 0.1));
        }
        public void RaiseResultProcessed(string result, bool istextbox1)
        {
            ResultProcessed?.Invoke(result, istextbox1);
        }

        private async Task OnScreenshotCaptured(Bitmap screenshot)
        {
              string result = process.ProcesScreenshot(screenshot);
              if (!(result.ToLower() == result1))
              {
                    result1 = result.ToLower();
                    if (result1.Contains("you finished") && result1.Contains("winning maps"))
                    {
                        Dictionary<string, double> mapstimeback = await _processor.EnqueueTaskAsync(result1, mapstime, gravmapstimes);
                        if (mapstimeback.Count == 5 && !mapstimeback.ContainsValue(0) && mapstimeback != copylastinstance)
                            {
                            copylastinstance = mapstimeback;
                            string maps = "";
                            double optimaltime = 0;
                            double maptimereached = 0;
                            foreach (var key in mapstimeback.Keys)
                            {
                                optimaltime += gravmapstimes[key];
                                maps += key + " ";
                                maptimereached = mapstimeback[key];
                            }
                            string maptimereachedstring = maptimereached.ToString();
                            string optimaltimestring = optimaltime.ToString();
                            RaiseResultProcessed($"\nfinished {maps} in {maptimereachedstring.Substring(0, 2) + "." + maptimereachedstring.Substring(2, 3)} seconds.\nOptimal Time to reach is around {optimaltimestring.Substring(0, 2) + "." + optimaltimestring.Substring(2, 3)} seconds", false);
                            mapstime.Clear();
                        }
                    }
              }
        }
        private Dictionary<string, double> GetTimes()
        {
            Dictionary<string, double> Gravtimes = new Dictionary<string, double>();
            string[] lines = File.ReadAllLines(Path.Combine(path, "gravitytimemaps.txt"));
            foreach (var line in lines)
            {
                // Trace.WriteLine(line);
                string[] lineparts = line.Split(" ");
                Gravtimes.Add(lineparts[0], double.Parse(lineparts[1]));
            }
            return Gravtimes;
        }
    }
    public class TaskQueueProcessor
    {
        DataToValue chart = new DataToValue();
        private readonly object _queueLock = new object();
        private volatile bool _isProcessing = false;
        public async Task<Dictionary<string, double>> EnqueueTaskAsync(string result1, Dictionary<string, double> maptime, Dictionary<string, double> bestmaptime)
        {
            lock (_queueLock)
            {
                if (_isProcessing)
                {
                    maptime.Clear();
                    return maptime;
                }
                _isProcessing = true;
            }
            try
            {
                maptime = chart.Data(result1, maptime, bestmaptime);
                return maptime;
            }
            finally
            {
                _isProcessing = false;
            }
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
            timer.Interval = 2000;
            timer.Start();
        }
        public void Stoptimer()
        {
            timer.Stop();
        }
        private void OnElapsedTime(System.Windows.Controls.TextBox textbox1)
        {
            Task.Run(() =>
            {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            int captureWidth = bounds.Width;
            int captureHeight = bounds.Height;

                using (Bitmap screenshot = new Bitmap(captureWidth, captureHeight))
                {
                    using (Graphics graphics = Graphics.FromImage(screenshot))
                    {
                        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new Size(captureWidth, captureHeight));
                    }
                    using (var upscaledScreenshot = UpscaleAndGrayscaleImage(screenshot, 2.0f))
                    {
                       ScreenshotCaptured?.Invoke(upscaledScreenshot);
                    }
                }
            });
        }
        private Bitmap UpscaleAndGrayscaleImage(Bitmap original, float scaleFactor)
        {
            var upscaled = new Bitmap((int)(original.Width * scaleFactor), (int)(original.Height * scaleFactor));
            using (var graphics = Graphics.FromImage(upscaled))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(original, 0, 0, upscaled.Width, upscaled.Height);
            }
            var grayscale = new Bitmap(upscaled.Width, upscaled.Height);
            using (var graphics = Graphics.FromImage(grayscale))
            {
                ColorMatrix colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                new float[] {.3f, .3f, .3f, 0, 0},
                new float[] {.59f, .59f, .59f, 0, 0},
                new float[] {.11f, .11f, .11f, 0, 0},
                new float[] {0, 0, 0, 1, 0},
                new float[] {0, 0, 0, 0, 1}
                    });
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(colorMatrix);
                    graphics.DrawImage(upscaled, new Rectangle(0, 0, upscaled.Width, upscaled.Height),
                        0, 0, upscaled.Width, upscaled.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            upscaled.Dispose();
            return grayscale;
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
        public string ProcesScreenshot(Bitmap screnshot)
        {
            using (var engine = new TesseractEngine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata"), "eng", EngineMode.Default))
            {
                using (var image = PixConverter.ToPix(screnshot))
                {
                    using (var page = engine.Process(image))
                    {
                        var text = page.GetText();
                        screnshot.Dispose();
                        image.Dispose();
                        page.Dispose();
                        return text;
                    }
                }
            }
        }
    }
    public class DataToValue
    {
        public Dictionary<string, double> Data(string screentext, Dictionary<string, double> maptimes, Dictionary<string, double> gravmapstimes)
        {
            string timepattern = @"[{\[\(]\d{2}[:;]\d{2}[.,]\d{3}[}\]\)]";
            char[] delimiters = new char[] { ' ', '\n', '\r', '!', ',' };
            bool containsyou = false;
            maptimes.Clear();
            string[] words = screentext.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            string wordsequenz = "";
            foreach (string word in words)
            {
                wordsequenz += word;
                if (wordsequenz.Contains("youfinished"))
                {
                    containsyou = true;
                }
                if (Regex.IsMatch(word, timepattern))
                {
                    Trace.WriteLine(word + " match");
                    string timestring = word.Substring(4, 2) + '.' + word.Substring(7, 3);
                    if (maptimes.Count == 5 && maptimes.ContainsValue(0) && containsyou)
                    {
                        foreach (var key in maptimes.Keys.ToList())
                        {
                            maptimes[key] = Double.Parse(timestring);
                        }
                    }
                }
                if (gravmapstimes.ContainsKey(word) && !maptimes.ContainsKey(word))
                {
                    Trace.WriteLine(word + " containskey");
                    maptimes.Add(word, 0);
                }
            }
            return maptimes;
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

