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


public enum Mode
{
    Idle,
    Activity,
    Interrupting,
    Break,
}

public class ActivityState {
    public DateTime ActivityStart { get; set; }
    public TimeSpan ActivityTime { get; set; }
    public TimeSpan BreakTime { get; set; }
    public Mode Mode { get; set; }
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
        // private KeyBeep keyBeep = new KeyBeep();
        private ActivityMonitor activityMonitor = new ActivityMonitor();
        private DateTime prevActivityDateThrottled = DateTime.Now;
        private DateTime lastActivityDateThrottled = DateTime.Now;
        private Mode _mode = Mode.Idle;
        private Mode mode
        {
            get
            {
                return _mode;
            }
            set
            {
                if (_mode == value)
                {
                    return;
                }

                switch (value)
                {
                    case Mode.Idle:
                        toast.Hide();
                        if (_mode == Mode.Interrupting || _mode == Mode.Break)
                        {
                            Console.Beep(1000, 400);
                        }
                        break;
                    case Mode.Activity:
                        toast.Hide();
                        Console.Beep(2000, 100);
                        break;
                    case Mode.Interrupting:
                        toast.Scale = 3;
                        toast.Animation = false;
                        toast.LocationCenter = false;
                        toast.Show();
                        Console.Beep(800, 200);
                        break;
                    case Mode.Break:
                        lastActivityDateThrottled = DateTime.Now;
                        toast.Scale = 4;
                        toast.Animation = true;
                        toast.LocationCenter = true;
                        toast.Show();
                        Console.Beep(800, 150);
                        Thread.Sleep(100);
                        Console.Beep(800, 150);
                        Thread.Sleep(100);
                        Console.Beep(800, 150);
                        break;
                    default:
                        throw new Exception("Unknown Mode: " + mode);
                }

                _mode = value;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            this.Closing += (s, e) =>
            {
                Application.Current.Shutdown();
            };

            ActivityTime = TimeSpan.FromMinutes(30);
            InterruptingTime = TimeSpan.FromMinutes(3);
            BreakTime = TimeSpan.FromMinutes(3);
            MinBreakTime = TimeSpan.FromMinutes(2);

            load();

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

            var activityStart = DateTime.Now;

            var MIN_ACTIVITY_TIME = TimeSpan.FromSeconds(1);
            var MIN_BREAK_TIME = TimeSpan.FromSeconds(2);
            var prevActivityDate = DateTime.Now;

            toast.TransparentForMouse = true;

            timer.Tick += (s, args) =>
            {
                var now = DateTime.Now;
                DateTime lastActivityDate = activityMonitor.LastActivityDate;

                if (now - lastActivityDate > MIN_BREAK_TIME)
                {
                    prevActivityDate = now;
                }
                if (lastActivityDate - prevActivityDate > MIN_ACTIVITY_TIME)
                {
                    lastActivityDateThrottled = lastActivityDate;
                }

                switch (mode)
                {
                    case Mode.Idle:
                        if (lastActivityDateThrottled != prevActivityDateThrottled)
                        {
                            activityStart = lastActivityDateThrottled;
                            mode = Mode.Activity;
                        }
                        break;
                    case Mode.Activity:
                        if (now - lastActivityDateThrottled > (MinBreakTime < BreakTime ? MinBreakTime : BreakTime))
                        {
                            mode = Mode.Idle;
                        }
                        if (lastActivityDateThrottled - activityStart > ActivityTime)
                        {
                            mode = Mode.Interrupting;
                        }
                        break;
                    case Mode.Interrupting:
                        if (now - lastActivityDateThrottled > BreakTime)
                        {
                            mode = Mode.Idle;
                        }
                        if (lastActivityDateThrottled - activityStart > ActivityTime + InterruptingTime)
                        {
                            mode = Mode.Break;
                        }
                        break;
                    case Mode.Break:
                        if (now - lastActivityDateThrottled > BreakTime)
                        {
                            mode = Mode.Idle;
                        }
                        break;
                    default:
                        throw new Exception("Unknown Mode: " + mode);
                }
                prevActivityDateThrottled = lastActivityDateThrottled;

                toast.Message = "You should take a break in "
                    + (BreakTime - (now - lastActivityDateThrottled)).ToString(@"mm\:ss");

                ActivityState = new ActivityState
                {
                    ActivityStart = activityStart,
                    ActivityTime = lastActivityDateThrottled - activityStart,
                    BreakTime = now - lastActivityDateThrottled,
                    Mode = mode,
                };
            };
        }

        ~MainWindow()
        {
            // this.keyBeep.Dispose();
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
            = DependencyProperty.Register(
                "ActivityTime",
                typeof(TimeSpan),
                typeof(MainWindow),
                new PropertyMetadata(TimeSpan.FromMinutes(30), OnActivityTimeChanged)
            );

        private static void OnActivityTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MainWindow mainWindow = d as MainWindow;
            mainWindow.save();
        }

        public TimeSpan ActivityTime
        {
            get { return (TimeSpan)GetValue(ActivityTimeProperty); }
            set { SetValue(ActivityTimeProperty, value); }
        }

        #endregion

        #region InterruptingTime

        public static readonly DependencyProperty InterruptingTimeProperty
            = DependencyProperty.Register(
                "InterruptingTime",
                typeof(TimeSpan),
                typeof(MainWindow),
                new PropertyMetadata(TimeSpan.FromMinutes(5), OnInterruptingTimeChanged)
            );

        private static void OnInterruptingTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MainWindow mainWindow = d as MainWindow;
            mainWindow.save();
        }

        public TimeSpan InterruptingTime
        {
            get { return (TimeSpan)GetValue(InterruptingTimeProperty); }
            set { SetValue(InterruptingTimeProperty, value); }
        }

        #endregion

        #region BreakTime

        public static readonly DependencyProperty BreakTimeProperty
            = DependencyProperty.Register(
                "BreakTime",
                typeof(TimeSpan),
                typeof(MainWindow),
                new PropertyMetadata(TimeSpan.FromMinutes(5), OnBreakTimeChanged)
            );

        private static void OnBreakTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MainWindow mainWindow = d as MainWindow;
            mainWindow.save();
        }

        public TimeSpan BreakTime
        {
            get { return (TimeSpan)GetValue(BreakTimeProperty); }
            set { SetValue(BreakTimeProperty, value); }
        }

        #endregion

        #region MinBreakTime

        public static readonly DependencyProperty MinBreakTimeProperty
            = DependencyProperty.Register(
                "MinBreakTime",
                typeof(TimeSpan),
                typeof(MainWindow),
                new PropertyMetadata(TimeSpan.FromMinutes(1), OnMinBreakTimeChanged)
            );

        private static void OnMinBreakTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MainWindow mainWindow = d as MainWindow;
            mainWindow.save();
        }

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
            mode = Mode.Break;
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            mode = Mode.Idle;
        }

        private Properties.Settings settings = Properties.Settings.Default;


        private bool isLoading = true;
        private void load()
        {
            ActivityTime = settings.ActivityTime;
            InterruptingTime = settings.InterruptingTime;
            BreakTime = settings.BreakTime;
            MinBreakTime = settings.MinBreakTime;
            isLoading = false;
        }

        private void save()
        {
            if (isLoading)
            {
                return;
            }
            settings.ActivityTime = ActivityTime;
            settings.InterruptingTime = InterruptingTime;
            settings.BreakTime = BreakTime;
            settings.MinBreakTime = MinBreakTime;
            settings.Save();
        }
    }
}
