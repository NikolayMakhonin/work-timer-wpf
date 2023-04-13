using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

public enum ActivityType
{
    Active,
    Break
}

public class ActivityState {
    public DateTime TimeStart { get; set; }
    public ActivityType Type { get; set; }
    public TimeSpan BreakTime { get; set; }
    public TimeSpan NextBreakTime { get; set; }
}

public interface IMainWindow
{
    TimeSpan ActivityTime { get; set; }
    TimeSpan InterruptingTime { get; set; }
    TimeSpan BreakTime { get; set; }
    TimeSpan MinBreakTime { get; set; }
    TimeSpan LastActivityDate { get; set; }
    ActivityState ActivityState { get; set; }
}

namespace WorkTimer
{
    public class ActivityMonitor {
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public static readonly int SizeOf = Marshal.SizeOf(typeof(LASTINPUTINFO));

            [MarshalAs(UnmanagedType.U4)]
            public int cbSize;
            [MarshalAs(UnmanagedType.U4)]
            public int dwTime;
        }

        public static TimeSpan GetLastInputTime()
        {
            var lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = LASTINPUTINFO.SizeOf;
            if (!GetLastInputInfo(ref lastInputInfo))
            {
                return TimeSpan.Zero;
            }

            return TimeSpan.FromMilliseconds(Environment.TickCount - lastInputInfo.dwTime);
        }

        private DispatcherTimer timer = new DispatcherTimer();

        public ActivityMonitor() {
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += _timer_Tick;
        }

        public double MinRate { get; set; } = 0.15;
        public TimeSpan WindowTime { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan CheckInterval {
            get { return timer.Interval; }
            set { timer.Interval = value; }
        }
        public double Rate {
            get {
                var now = DateTime.Now;
                while (_inputDateQueue.Count > 0 && now - _inputDateQueue.Peek() > WindowTime)
                {
                    _inputDateQueue.Dequeue();
                }
                return (double)_inputDateQueue.Count * CheckInterval.TotalMilliseconds / WindowTime.TotalMilliseconds;
            }
        }

        public DateTime LastActivityDate {
          get;
          private set;
        } = DateTime.MinValue;

        private DateTime _prevInputDate = DateTime.MinValue;
        private Queue<DateTime> _inputDateQueue = new Queue<DateTime>();
        private void _timer_Tick(object sender, EventArgs e)
        {
            var now = DateTime.Now;

            var inputDate = now - GetLastInputTime();

            if (inputDate - _prevInputDate < CheckInterval)
            {
                return;
            }
            _prevInputDate = inputDate;

            var rate = Rate;

            _inputDateQueue.Enqueue(inputDate);

            if (rate > MinRate)
            {
                LastActivityDate = inputDate;
            }
        }

        public void Start() {
            timer.Start();
        }

        public void Stop() {
            timer.Stop();
        }
    }

    public class TimeSpanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var timeSpan = (TimeSpan)value;
            return timeSpan.ToString(@"hh\:mm\:ss");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string timeString = (string)value;
            if (TimeSpan.TryParse(timeString, out TimeSpan timeSpan))
            {
                return timeSpan;
            }
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ToastNotification toast = new ToastNotification();
        private TimeSpan prevBreakTime = TimeSpan.Zero;
        private KeyBeep keyBeep = new KeyBeep();
        private ActivityMonitor activityMonitor = new ActivityMonitor();

        public MainWindow()
        {
            InitializeComponent();

            this.Closing += (s, e) =>
            {
                Application.Current.Shutdown();
            };

            ActivityTime = TimeSpan.FromMinutes(30);
            InterruptingTime = TimeSpan.FromMinutes(0.1);
            BreakTime = TimeSpan.FromMinutes(7.5);
            MinBreakTime = TimeSpan.FromMinutes(3);

            activityMonitor.MinRate = 0.1;
            activityMonitor.CheckInterval = TimeSpan.FromMilliseconds(100);
            activityMonitor.WindowTime = TimeSpan.FromSeconds(5);
            activityMonitor.Start();

            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += (s, args) =>
            {
                LastActivityDate = activityMonitor.LastActivityDate;
                CurrentActivityRate = activityMonitor.Rate;
            };
            timer.Start();

            var prevActivityDate = DateTime.Now;
            toast.TransparentForMouse = true;

            Func<bool, DateTime, DateTime, TimeSpan, TimeSpan> getNextBreakTime = (
                bool increment,
                DateTime _prevActivityDate,
                DateTime _newActivityDate,
                TimeSpan _prevBreakTime
            ) =>
            {
                if (increment)
                {
                    if (_prevBreakTime > BreakTime)
                    {
                        return _prevBreakTime + (_newActivityDate - _prevActivityDate);
                    }
                    var result = _prevBreakTime.TotalSeconds + (_newActivityDate - _prevActivityDate).TotalSeconds * BreakTime.TotalSeconds / ActivityTime.TotalSeconds;
                    if (result > BreakTime.TotalSeconds)
                    {
                        result = BreakTime.TotalSeconds + (result - BreakTime.TotalSeconds) * ActivityTime.TotalSeconds / BreakTime.TotalSeconds;
                    }
                    return TimeSpan.FromSeconds(result);
                }

                return TimeSpan.FromSeconds(Math.Max(
                    0,
                    Math.Min(BreakTime.TotalSeconds, _prevBreakTime.TotalSeconds)
                    - (_newActivityDate - _prevActivityDate).TotalSeconds
                ));
            };

            timer.Tick += (s, args) =>
            {
                var now = DateTime.Now;
                DateTime newActivityDate = activityMonitor.LastActivityDate;

                if (newActivityDate - prevActivityDate > TimeSpan.FromSeconds(1))
                {
                    if (newActivityDate - prevActivityDate > TimeSpan.FromSeconds(60)) {
                        Console.Beep(1000, 100);
                    }
                    prevBreakTime = getNextBreakTime(
                        newActivityDate - prevActivityDate < MinBreakTime,
                        prevActivityDate, newActivityDate, prevBreakTime
                    );
                    prevActivityDate = newActivityDate;
                }

                var nextBreakTime = TimeSpan.FromSeconds(Math.Max(
                    (MinBreakTime - (now - prevActivityDate)).TotalSeconds,
                    getNextBreakTime(
                        false,
                        prevActivityDate, now, prevBreakTime
                    ).TotalSeconds
                ));

                if ((nextBreakTime - BreakTime) > -TimeSpan.FromSeconds(1) && toast.IsVisible == false)
                {
                    toast.Show();
                    Console.Beep(800, 200);
                }
                toast.Message = "You should take a break in " + TimeSpan.FromSeconds(Math.Min(
                    BreakTime.TotalSeconds,
                    nextBreakTime.TotalSeconds
                )).ToString(@"mm\:ss");
                if (nextBreakTime <= TimeSpan.Zero && toast.IsVisible == true)
                {
                    toast.Hide();
                    Console.Beep(1000, 400);
                }
                var interruptingTimeExpired = prevBreakTime >= BreakTime + InterruptingTime;
                if (!interruptingTimeExpired)
                {
                    toast.Animation = false;
                    toast.LocationCenter = false;
                }
                else if (toast.Animation != true)
                {
                    toast.Animation = true;
                    toast.LocationCenter = true;
                    Console.Beep(800, 150);
                    Thread.Sleep(100);
                    Console.Beep(800, 150);
                    Thread.Sleep(100);
                    Console.Beep(800, 150);
                }
                if (now - prevActivityDate >= MinBreakTime)
                {
                    toast.Scale = 4;
                }
                else if (interruptingTimeExpired)
                {
                    toast.Scale = 4;
                }
                else
                {
                    toast.Scale = 1;
                }

                ActivityState = new ActivityState
                {
                    TimeStart = prevActivityDate,
                    BreakTime = prevBreakTime,
                    NextBreakTime = nextBreakTime
                };
            };
        }

        ~MainWindow()
        {
            this.keyBeep.Dispose();
        }

        #region Last activity date

        public static readonly DependencyProperty LastActivityDateProperty
            = DependencyProperty.Register("LastActivityDate", typeof(DateTime), typeof(MainWindow), new PropertyMetadata(DateTime.MinValue));

        public DateTime LastActivityDate
        {
            get { return (DateTime)GetValue(LastActivityDateProperty); }
            set { SetValue(LastActivityDateProperty, value); }
        }

        #endregion

        #region CurrentActivityRate

        public static readonly DependencyProperty CurrentActivityRateProperty
            = DependencyProperty.Register("CurrentActivityRate", typeof(double), typeof(MainWindow), new PropertyMetadata(0.0));

        public double CurrentActivityRate
        {
            get { return (double)GetValue(CurrentActivityRateProperty); }
            set { SetValue(CurrentActivityRateProperty, value); }
        }

        #endregion

        #region ActivityTime

        public static readonly DependencyProperty ActivityTimeProperty
            = DependencyProperty.Register("ActivityTime", typeof(TimeSpan), typeof(MainWindow), new PropertyMetadata(TimeSpan.FromMinutes(30)));

        public TimeSpan ActivityTime
        {
            get { return (TimeSpan)GetValue(ActivityTimeProperty); }
            set { SetValue(ActivityTimeProperty, value); }
        }

        #endregion

        #region InterruptingTime

        public static readonly DependencyProperty InterruptingTimeProperty
            = DependencyProperty.Register("InterruptingTime", typeof(TimeSpan), typeof(MainWindow), new PropertyMetadata(TimeSpan.FromMinutes(5)));

        public TimeSpan InterruptingTime
        {
            get { return (TimeSpan)GetValue(InterruptingTimeProperty); }
            set { SetValue(InterruptingTimeProperty, value); }
        }

        #endregion

        #region BreakTime

        public static readonly DependencyProperty BreakTimeProperty
            = DependencyProperty.Register("BreakTime", typeof(TimeSpan), typeof(MainWindow), new PropertyMetadata(TimeSpan.FromMinutes(5)));

        public TimeSpan BreakTime
        {
            get { return (TimeSpan)GetValue(BreakTimeProperty); }
            set { SetValue(BreakTimeProperty, value); }
        }

        #endregion

        #region MinBreakTime

        public static readonly DependencyProperty MinBreakTimeProperty
            = DependencyProperty.Register("MinBreakTime", typeof(TimeSpan), typeof(MainWindow), new PropertyMetadata(TimeSpan.FromMinutes(1)));

        /// <summary>
        /// Should be less than BreakTime
        /// </summary>
        public TimeSpan MinBreakTime
        {
            get { return (TimeSpan)GetValue(MinBreakTimeProperty); }
            set { SetValue(MinBreakTimeProperty, value); }
        }

        #endregion

        #region ActivityState

        public static readonly DependencyProperty ActivityStateProperty
            = DependencyProperty.Register("ActivityState", typeof(ActivityState), typeof(MainWindow), new PropertyMetadata(default(ActivityState)));

        public ActivityState ActivityState
        {
            get { return (ActivityState)GetValue(ActivityStateProperty); }
            set { SetValue(ActivityStateProperty, value); }
        }

        #endregion

        private void Show_Click(object sender, RoutedEventArgs e)
        {
            //toast.Animation = true;
            //toast.LocationCenter = true;
            //toast.Scale = 2;
            toast.Show();
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            toast.Hide();
        }

        private void Break_Click(object sender, RoutedEventArgs e)
        {
            prevBreakTime = BreakTime;
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            prevBreakTime = TimeSpan.Zero;
            toast.Hide();
        }
    }
}
