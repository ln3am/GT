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
        //leads to GT\bin\ebug\net7.0-windows\
        public GetBitMap getmap = new GetBitMap();
        ProcessScreenshot process = new ProcessScreenshot();
        private string result1 = "";
        private System.Windows.Controls.TextBox textbox1;
        private System.Windows.Controls.TextBox textbox2;
        Dispatcher mdispatcher;
        private double writespeed = 0.2;
        public event Action<string, bool> ResultProcessed;
        public event Action<string, double, double, double> FinishedMapTimes;
        private Dictionary<string, double> gravmapstimes = new Dictionary<string, double>();
        private Dictionary<string, double> optimalgravmapstimes = new Dictionary<string, double>();
        private TaskQueueProcessor _processor = new TaskQueueProcessor();
        private Dictionary<string, double> copylastinstance = new Dictionary<string, double>();
        public void OnStart(System.Windows.Controls.TextBox text1, System.Windows.Controls.TextBox text2, Dispatcher maindispatcher)
        {
            mdispatcher = maindispatcher;
            textbox1 = text1;
            textbox2 = text2;
            getmap.Write("Service was started at " + DateTime.Now, textbox1, writespeed);
            gravmapstimes = GetTimes("gravitytimemaps.txt");
            optimalgravmapstimes = GetTimes("optimaltimes.txt");
            if (getmap.firststart)
            {
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
            }
            getmap.TakeScreenShot();
        }
        public void OnStop()
        {
            getmap.Stoptimer();
            getmap.Write("\nService was stopped at " + DateTime.Now, textbox1, writespeed);
        }
        private void OnResultProcessed(string result, bool istextbox1)
        {
            System.Windows.Controls.TextBox textboxx = istextbox1 ? textbox1 : textbox2;
            mdispatcher.Invoke(() => getmap.Write(result, textboxx, writespeed));
        }
        private readonly object qLock = new object();
        public void RaiseResultProcessed(string result, bool istextbox1)
        {
            lock (qLock)
            {
                ResultProcessed?.Invoke(result, istextbox1);
            }
        }
        private async Task OnScreenshotCaptured(Bitmap screenshot)
        {
              string result = process.ProcesScreenshot(screenshot);
              if (!(result.ToLower() == result1))
              {
                    result1 = result.ToLower();
                if (result1.Contains("you finished") && result1.Contains("winning maps"))
                {
                    Dictionary<string, double> mapstimeback = await _processor.EnqueueTaskAsync(result1, gravmapstimes);
                    lock (_updateLock)
                    {
                        if (mapstimeback.Count == 5 && !mapstimeback.ContainsValue(0) && !DictionaryEquals(mapstimeback, copylastinstance))
                        {
                            copylastinstance = new Dictionary<string, double>(mapstimeback);
                            string maps = "";
                            double optimaltime = 0;
                            double maptimereached = 0;
                            int loop = 1;
                            foreach (var key in mapstimeback.Keys)
                            {
                                optimaltime += loop == 1 ? optimalgravmapstimes[key] : gravmapstimes[key];
                                maps += key;
                                if (loop < 5) maps += " ";
                                
                                maptimereached = mapstimeback[key];
                                loop++;
                            }
                            string maptimereachedstring = maptimereached.ToString();
                            string optimaltimestring = optimaltime.ToString();
                            RaiseResultProcessed($"\n{maps}; {maptimereached}; {optimaltimestring}", false);
                            double timereached = Double.Parse(maptimereachedstring);
                            double optimaltimereached = Double.Parse(optimaltimestring);
                            double timedifference = timereached - optimaltimereached;
                            FinishedMapTimes?.Invoke(maps, timereached, optimaltimereached, timedifference);
                            RaiseResultProcessed($"\nNew time has been documented at {DateTime.Now}", true);
                            mapstimeback.Clear();
                        }
                    }
                }
             }
        }
        bool DictionaryEquals(Dictionary<string, double> dict1, Dictionary<string, double> dict2)
        {
            if (dict1.Count != dict2.Count)
                return false;
            foreach (var pair in dict1)
            {
                double value;
                if (!dict2.TryGetValue(pair.Key, out value) || value != pair.Value)
                    return false;
            }
            return true;
        }
        private readonly object _updateLock = new object();
        private Dictionary<string, double> GetTimes(string filename)
        {
            Dictionary<string, double> Gravtimes = new Dictionary<string, double>();
            string[] lines = File.ReadAllLines(Path.Combine(path, filename));
            foreach (var line in lines)
            {
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
        private Task<Dictionary<string, double>> _processingTask = null;
        private volatile bool _isProcessing = false;
        public Task<Dictionary<string, double>> EnqueueTaskAsync(string result1, Dictionary<string, double> bestmaptime)
        {
            lock (_queueLock)
            {
                if (_processingTask == null || _processingTask.IsCompleted || _processingTask.IsFaulted || _processingTask.IsCanceled)
                {
                    _processingTask = Task.Run(() => ProcessDataAsync(result1, bestmaptime));
                }
            }
            return _processingTask;
        }
        private async Task<Dictionary<string, double>> ProcessDataAsync(string result1, Dictionary<string, double> bestmaptime)
        {
            Dictionary<string, double> maptime = chart.Data(result1, new Dictionary<string, double>(), bestmaptime);
            return maptime;
        }
    }
    public class GetBitMap
    {
        System.Timers.Timer timer = new System.Timers.Timer();
        private bool IsEnabled = false;
        public double timetick = 2;
        public event Action<Bitmap> ScreenshotCaptured;
        public bool firststart = true;
        public void TakeManualScreenShot()
        {
            OnElapsedTime();
        }   
        public void TakeScreenShot()
        {
            if (firststart)
            {
                timer.Elapsed += (sender, e) =>
                {
                    OnElapsedTime();
                };
                timer.Interval = timetick * 2000;
            }
            Starttimer();
        }
        public void Starttimer()
        {              
            if (!IsEnabled)
            {
                IsEnabled = true;
                timer.Start();
            }   
        }
        public void Stoptimer()
        {
            firststart = false;
            if (IsEnabled)
            {
                IsEnabled = false;
                timer.Stop();
            }
        }
        private void OnElapsedTime()
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
                    using (var upscaledScreenshot = UpscaleAndGrayscaleImage(screenshot, 3.0f))
                    {
                       ScreenshotCaptured?.Invoke(upscaledScreenshot);
                    }
                }
            });
        }
        private Bitmap UpscaleAndGrayscaleImage(Bitmap original, float scaleFactor)
        {
            var upscaledAndGrayscale = new Bitmap((int)(original.Width * scaleFactor), (int)(original.Height * scaleFactor));

            using (var graphics = Graphics.FromImage(upscaledAndGrayscale))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

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
                    graphics.DrawImage(original, new Rectangle(0, 0, upscaledAndGrayscale.Width, upscaledAndGrayscale.Height),
                        0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
                }
            }

            // Dispose the original bitmap if it's no longer needed
            original.Dispose();

            return upscaledAndGrayscale;
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
                engine.DefaultPageSegMode = PageSegMode.Auto;
                using (var image = PixConverter.ToPix(screnshot))
                {
                    using (var page = engine.Process(image))
                    {
                        var text = page.GetText();
                        screnshot.Dispose();
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
            bool containsmaps = false;
            maptimes.Clear();
            StringBuilder sb = new StringBuilder();
            string[] words = screentext.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            foreach (string word in words)
            {
                sb.Append(word);
                if (sb.ToString().Contains("youfinished") && !containsyou)
                {
                    containsyou = true;
                }
                if (sb.ToString().Contains("winningmaps") && !containsmaps)
                {
                    containsmaps = true;
                }
                if (Regex.IsMatch(word, timepattern))
                {
                    string timestring = word.Substring(4, 2) + '.' + word.Substring(7, 3);
                    if (maptimes.Count == 5 && maptimes.ContainsValue(0) && containsyou && containsmaps)
                    {
                        double minuteover = double.Parse(word.Substring(2, 1));
                        double reachedseconds = Double.Parse(timestring);
                        if (minuteover > 0) reachedseconds += 60;
                        foreach (var key in maptimes.Keys.ToList())
                        {
                            maptimes[key] = reachedseconds;
                        }
                        return maptimes;
                    }
                }
                if (gravmapstimes.ContainsKey(word) && !maptimes.ContainsKey(word) && maptimes.Count < 5 && containsmaps)
                {
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
                StopTimer2();
            }
        }
        private void StartTimer2()
        {
            if (!timer2.IsEnabled)
            {
                timer2.Start();
            }
        }
        private void StopTimer2()
        {
            if (timer2.IsEnabled)
            {
                timer2.Stop();
            }
        }
    }
}

