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
        public MainWindow()
        {
            InitializeComponent();

            ActivityTime = TimeSpan.FromMinutes(5);
            InterruptingTime = TimeSpan.FromMinutes(1);
            BreakTime = TimeSpan.FromMinutes(1);
            MinBreakTime = TimeSpan.FromSeconds(20);

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000);
            timer.Tick += (s, args) =>
            {
                LastActivityTime = DateTime.Now - GetLastInputTime();
            };
            timer.Start();

            var activityTime = DateTime.Now;
            var activityType = ActivityType.Active;
            var activeTime = TimeSpan.Zero;
            var breakTime = TimeSpan.Zero;
            var toast = new ToastNotification();

            Func<ActivityType, TimeSpan, TimeSpan, TimeSpan> getNextBreakTime = (
                ActivityType type,
                TimeSpan prevBreakTime,
                TimeSpan time
            ) =>
            {
                return type == ActivityType.Active
                    ? TimeSpan.FromSeconds(prevBreakTime.TotalSeconds + time.TotalSeconds * BreakTime.TotalSeconds / ActivityTime.TotalSeconds)
                    : TimeSpan.FromSeconds(Math.Max(
                        0,
                        Math.Min(BreakTime.TotalSeconds, prevBreakTime.TotalSeconds) - time.TotalSeconds
                    ));
            };

            timer.Tick += (s, args) =>
            {
                var now = DateTime.Now;
                TimeSpan lastActivityTimeSpan = GetLastInputTime();
                DateTime lastActivityTime = new DateTime(now.Ticks - lastActivityTimeSpan.Ticks);
                
                if (
                    activityType == ActivityType.Active &&
                    now - lastActivityTime > TimeSpan.FromSeconds(5)
                )
                {
                    Debug.WriteLine("Break");
                    breakTime = getNextBreakTime(ActivityType.Active, breakTime, lastActivityTime - activityTime);
                    activityTime = lastActivityTime;
                    activityType = ActivityType.Break;
                }
                else if (
                    activityType == ActivityType.Break
                    && lastActivityTime - activityTime > TimeSpan.FromSeconds(1)
                )
                {
                    Debug.WriteLine("Active");
                    if (now - activityTime > MinBreakTime)
                    {
                        breakTime = getNextBreakTime(ActivityType.Break, breakTime, now - activityTime);
                    }
                    else
                    {
                        breakTime = getNextBreakTime(ActivityType.Active, breakTime, now - activityTime);
                    }
                    activityTime = lastActivityTime;
                    activityType = ActivityType.Active;
                }

                var nextBreakTime = getNextBreakTime(activityType, breakTime, now - activityTime);

                Debug.WriteLine($"breakTime: {breakTime}, nextBreakTime: {nextBreakTime}, activityType: {activityType}");

                // if nextBreakTime >= BreakTime then show toast
                // if nextBreakTime > 0 then update toast message with Math.min(BreakTime, nextBreakTime)
                // if activityType == ActivityType.Break && nextBreakTime == 0 then hide toast
                // if activityType == ActivityType.Active && nextBreakTime >= BreakTime + InterruptingTime then animate toast
                if (nextBreakTime >= BreakTime && toast.IsVisible == false)
                {
                    toast.Show();
                }
                if (nextBreakTime > TimeSpan.Zero)
                {
                    toast.Message = "You should take a break in " + TimeSpan.FromSeconds(Math.Min(
                        BreakTime.TotalSeconds,
                        activityType == ActivityType.Break
                            ? nextBreakTime.TotalSeconds
                            : (getNextBreakTime(
                                ActivityType.Break,
                                getNextBreakTime(ActivityType.Active, breakTime, lastActivityTime - activityTime),
                                now - lastActivityTime
                            )).TotalSeconds
                    )).ToString(@"mm\:ss");
                }
                if (activityType == ActivityType.Break && nextBreakTime <= TimeSpan.Zero && toast.IsVisible == true)
                {
                    toast.Hide();
                }
                toast.Animation = activityType == ActivityType.Active && nextBreakTime >= BreakTime + InterruptingTime;

                ActivityState = new ActivityState
                {
                    TimeStart = activityTime,
                    Type = activityType,
                    BreakTime = breakTime,
                    NextBreakTime = nextBreakTime
                };
                // Convert ActivityState to string and log it
                Debug.WriteLine(ActivityState.ToString());
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

        // Button_Click that open ToastNotification window with simple message
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            showMessage("Hello World!");
        }

        private void showMessage(string message)
        {
            ToastNotification toast = new ToastNotification();
            // set the Message property of the toast
            toast.Message = message;
            toast.CloseTimeSpan = TimeSpan.FromSeconds(60);
            toast.Show();

            // blink the taskbar icon
            // this.FlashTaskbarIcon();
        }
    }
}
