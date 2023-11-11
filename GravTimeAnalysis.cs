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
            getmap.Write("\nService was started at " + DateTime.Now, textbox1, writespeed, true);
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
            getmap.Write("\nService was stopped at " + DateTime.Now, textbox1, writespeed, true);
            
        }
        private void OnResultProcessed(string result, bool istextbox1)
        {
            System.Windows.Controls.TextBox textboxx = istextbox1 ? textbox1 : textbox2;
            mdispatcher.Invoke(() => getmap.Write(result, textboxx, writespeed, istextbox1));
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
                if (result1.Contains("you finished"))
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
                            maptimereached = ErrorCheck(maptimereached, optimaltime, 1);
                            maptimereached = ErrorCheck(maptimereached, optimaltime, 3);
                            double timedifference = maptimereached - optimaltime;
                            RaiseResultProcessed($"\n{maps}; {maptimereached}; {optimaltime}; {timedifference}", false);
                            FinishedMapTimes?.Invoke(maps, maptimereached, optimaltime, timedifference);
                            RaiseResultProcessed($"\nNew time has been documented at {DateTime.Now}", true);
                            mapstimeback.Clear();
                            Double ErrorCheck(Double maptimereached, Double optimaltime, int index)
                            {
                                string checkforerror = maptimereached.ToString();
                                if (checkforerror.Substring(index, 1) == "7")
                                {
                                    Double newtime = Double.Parse(checkforerror.Substring(index, 1).Replace("7", "1"));
                                    if (newtime > optimaltime - 0.5) return newtime;
                                }
                                return maptimereached;
                            }
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

        string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
        System.Timers.Timer timer = new System.Timers.Timer();
        private bool IsEnabled = false;
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
                SetTimerInterval(3);
            }
            Starttimer();
        }
        public void SetTimerInterval(double timetick)
        {
            timer.Interval = timetick * 1000;
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
                    using (var upscaledScreenshot = UpscaleAndGrayscaleImage(screenshot, 2.0f))
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
                graphics.DrawImage(original, new Rectangle(0, 0, upscaledAndGrayscale.Width, upscaledAndGrayscale.Height));
            }

            Rectangle rect = new Rectangle(0, 0, upscaledAndGrayscale.Width, upscaledAndGrayscale.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                upscaledAndGrayscale.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                upscaledAndGrayscale.PixelFormat);
            IntPtr ptr = bmpData.Scan0;

            int bytes = Math.Abs(bmpData.Stride) * upscaledAndGrayscale.Height;
            byte[] rgbValues = new byte[bytes];

            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);
            for (int i = 0; i < rgbValues.Length; i += 4)
            {
                byte gray = (byte)(rgbValues[i] * 0.11 + rgbValues[i + 1] * 0.59 + rgbValues[i + 2] * 0.3);
                rgbValues[i] = rgbValues[i + 1] = rgbValues[i + 2] = gray;
            }

            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);

            upscaledAndGrayscale.UnlockBits(bmpData);

            original.Dispose();

            return upscaledAndGrayscale;
        }
        public void Write(string message, System.Windows.Controls.TextBox textbox, double writespeed, bool txtbx2)
        {
            ControlWriter cw = new ControlWriter(textbox, writespeed);
            if (!txtbx2)
            {
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
                engine.SetVariable("user_words_suffix", "user-words");
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
        string timepattern = @"\d{2}[.]\d{3}";
        char[] delimiters = new char[] { ' ', '\n', ',', '!', ';', ':', '[', ']', '(', ')' };
        public Dictionary<string, double> Data(string screentext, Dictionary<string, double> maptimes, Dictionary<string, double> gravmapstimes)
        {
            string previousstring = "";
            bool containsyou = false;
            maptimes.Clear();
            List<string> mapkeys = new List<string>();
            StringBuilder sb = new StringBuilder();
            string[] words = screentext.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            foreach (string word in words)
            {
                    sb.Append(word);
                    if (sb.ToString().Contains("youfinished") && !containsyou)
                    {
                        containsyou = true;
                    }
                    if (Regex.IsMatch(word, timepattern) && maptimes.Count == 5 && maptimes.ContainsValue(0) && containsyou)
                    {
                            double minuteover = double.Parse(previousstring.Substring(1, 1));
                            double reachedseconds = Double.Parse(word);
                            if (minuteover > 0) reachedseconds += 60;
                            foreach (var key in maptimes.Keys.ToList())
                            {
                                maptimes[key] = reachedseconds;
                            }
                            return maptimes;
                    }
                    if (gravmapstimes.ContainsKey(word) && !maptimes.ContainsKey(word))
                    {
                        if (maptimes.Count == 5)
                        {
                        string firstkey = mapkeys[0];
                        maptimes.Remove(firstkey);
                        mapkeys.Remove(firstkey);
                        }
                        mapkeys.Add(word);
                        maptimes.Add(word, 0);
                    }
                    previousstring = word;
                
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

