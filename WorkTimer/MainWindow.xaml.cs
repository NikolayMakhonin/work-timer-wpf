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
    TimeSpan LastActivityTime { get; set; }
    ActivityState ActivityState { get; set; }
}

namespace WorkTimer
{
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

        public MainWindow()
        {
            InitializeComponent();

            ActivityTime = TimeSpan.FromMinutes(20);
            InterruptingTime = TimeSpan.FromMinutes(2);
            BreakTime = TimeSpan.FromMinutes(5);
            MinBreakTime = TimeSpan.FromMinutes(2);

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000);
            timer.Tick += (s, args) =>
            {
                LastActivityTime = DateTime.Now - GetLastInputTime();
            };
            timer.Start();

            var prevActivityTime = DateTime.Now;
            toast.TransparentForMouse = true;

            Func<bool, DateTime, DateTime, TimeSpan, TimeSpan> getNextBreakTime = (
                bool increment,
                DateTime _prevActivityTime,
                DateTime _newActivityTime,
                TimeSpan _prevBreakTime
            ) =>
            {
                if (increment)
                {
                    if (_prevBreakTime > BreakTime)
                    {
                        return _prevBreakTime + (_newActivityTime - _prevActivityTime);
                    }
                    var result = _prevBreakTime.TotalSeconds + (_newActivityTime - _prevActivityTime).TotalSeconds * BreakTime.TotalSeconds / ActivityTime.TotalSeconds;
                    if (result > BreakTime.TotalSeconds)
                    {
                        result = BreakTime.TotalSeconds + (result - BreakTime.TotalSeconds) * ActivityTime.TotalSeconds / BreakTime.TotalSeconds;
                    }
                    return TimeSpan.FromSeconds(result);
                }

                return TimeSpan.FromSeconds(Math.Max(
                    0,
                    Math.Min(BreakTime.TotalSeconds, _prevBreakTime.TotalSeconds)
                    - (_newActivityTime - _prevActivityTime).TotalSeconds
                ));
            };

            timer.Tick += (s, args) =>
            {
                var now = DateTime.Now;
                TimeSpan lastActivityTimeSpan = GetLastInputTime();
                DateTime newActivityTime = new DateTime(now.Ticks - lastActivityTimeSpan.Ticks);
                
                if (newActivityTime - prevActivityTime > TimeSpan.FromSeconds(1))
                {
                    if (newActivityTime - prevActivityTime > TimeSpan.FromSeconds(60)) {
                        Console.Beep(1000, 100);
                    }
                    prevBreakTime = getNextBreakTime(
                        newActivityTime - prevActivityTime < MinBreakTime,
                        prevActivityTime, newActivityTime, prevBreakTime
                    );
                    prevActivityTime = newActivityTime;
                }

                var nextBreakTime = TimeSpan.FromSeconds(Math.Max(
                    (MinBreakTime - (now - prevActivityTime)).TotalSeconds,
                    getNextBreakTime(
                        false,
                        prevActivityTime, now, prevBreakTime
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
                }
                else if (toast.Animation != true)
                {
                    toast.Animation = true;
                    Console.Beep(800, 150);
                    Thread.Sleep(100);
                    Console.Beep(800, 150);
                    Thread.Sleep(100);
                    Console.Beep(800, 150);
                }
                if (now - prevActivityTime >= MinBreakTime)
                {
                    toast.Scale = 3;
                }
                else if (interruptingTimeExpired)
                {
                    toast.Scale = 2;
                }
                else
                {
                    toast.Scale = 1;
                }

                ActivityState = new ActivityState
                {
                    TimeStart = prevActivityTime,
                    BreakTime = prevBreakTime,
                    NextBreakTime = nextBreakTime
                };
            };
        }

        #region Last activity time

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

        public static readonly DependencyProperty LastActivityTimeProperty
            = DependencyProperty.Register("LastActivityTime", typeof(DateTime), typeof(MainWindow), new PropertyMetadata(DateTime.MinValue));

        public DateTime LastActivityTime
        {
            get { return (DateTime)GetValue(LastActivityTimeProperty); }
            set { SetValue(LastActivityTimeProperty, value); }
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
            toast.Animation = true;
            toast.Scale = 2;
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
