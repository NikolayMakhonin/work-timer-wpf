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

enum ActivityType
{
    Active,
    Break
}

namespace WorkTimer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ActivityTime = TimeSpan.FromMinutes(1);
            InterruptingTime = TimeSpan.FromSeconds(20);
            BreakTime = TimeSpan.FromSeconds(20);

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000);
            timer.Start();

            var lastActivityTimeDescriptor = DependencyPropertyDescriptor.FromProperty(MainWindow.LastActivityTimeProperty, typeof(ToastNotification));
            var activityTime = DateTime.Now;
            var activityType = ActivityType.Active;
            var activeTime = TimeSpan.Zero;
            var breakTime = TimeSpan.Zero;
            var toast = new ToastNotification();

            Func<ActivityType, TimeSpan, TimeSpan> getNextBreakTime = (
                ActivityType type,
                TimeSpan time
            ) =>
            {
                return type == ActivityType.Active
                    ? TimeSpan.FromSeconds(breakTime.TotalSeconds + time.TotalSeconds * BreakTime.TotalSeconds / ActivityTime.TotalSeconds)
                    : TimeSpan.FromSeconds(Math.Max(
                        0,
                        Math.Min(BreakTime.TotalSeconds, breakTime.TotalSeconds) - time.TotalSeconds
                    ));
            };

            timer.Tick += (s, args) =>
            {
                TimeSpan lastActivityTimeSpan = GetLastInputTime();
                var now = DateTime.Now;
                DateTime lastActivityTime = new DateTime(now.Ticks - lastActivityTimeSpan.Ticks);
                
                if (
                    activityType == ActivityType.Active &&
                    now - lastActivityTime > TimeSpan.FromSeconds(10)
                )
                {
                    Debug.WriteLine("Break");
                    breakTime = getNextBreakTime(ActivityType.Active, now - activityTime);
                    activityTime = lastActivityTime;
                    activityType = ActivityType.Break;
                }
                else if (
                    activityType == ActivityType.Break
                    && lastActivityTime > activityTime
                    && lastActivityTime - activityTime > TimeSpan.FromSeconds(1)
                )
                {
                    Debug.WriteLine("Active");
                    if (now - activityTime > TimeSpan.FromSeconds(60))
                    {
                        breakTime = getNextBreakTime(ActivityType.Break, now - activityTime);
                    }
                    else
                    {
                        breakTime = getNextBreakTime(ActivityType.Active, now - activityTime);
                    }
                    activityTime = lastActivityTime;
                    activityType = ActivityType.Active;
                }

                var nextBreakTime = getNextBreakTime(activityType, now - activityTime);

                Debug.WriteLine($"breakTime: {breakTime}, nextBreakTime: {nextBreakTime}, activityType: {activityType}");

                // if nextBreakTime >= BreakTime then show toast
                // if nextBreakTime > 0 then update toast message with Math.min(BreakTime, nextBreakTime)
                // if activityType == ActivityType.Break && nextBreakTime == 0 then hide toast
                // if activityType == ActivityType.Active && nextBreakTime >= BreakTime + InterruptingTime then animate toast
                if (nextBreakTime >= BreakTime && toast.IsVisible == false)
                {
                    toast.Show();
                }
                if (nextBreakTime.TotalSeconds > 0)
                {
                    toast.Message = $"You should take a break in {TimeSpan.FromSeconds(Math.Min(BreakTime.TotalSeconds, nextBreakTime.TotalSeconds)).ToString(@"mm\:ss")}";
                }
                if (activityType == ActivityType.Break && nextBreakTime.TotalSeconds == 0 && toast.IsVisible == true)
                {
                    toast.Hide();
                }
                toast.Animation = activityType == ActivityType.Active
                    && nextBreakTime.TotalSeconds >= BreakTime.TotalSeconds + InterruptingTime.TotalSeconds;
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

        #region Max activity time

        public static readonly DependencyProperty ActivityTimeProperty
            = DependencyProperty.Register("ActivityTime", typeof(TimeSpan), typeof(ToastNotification), new PropertyMetadata(TimeSpan.FromMinutes(30)));

        public TimeSpan ActivityTime
        {
            get { return (TimeSpan)GetValue(ActivityTimeProperty); }
            set { SetValue(ActivityTimeProperty, value); }
        }

        #endregion

        #region Max activity interrupting time

        public static readonly DependencyProperty InterruptingTimeProperty
            = DependencyProperty.Register("InterruptingTime", typeof(TimeSpan), typeof(ToastNotification), new PropertyMetadata(TimeSpan.FromMinutes(5)));

        public TimeSpan InterruptingTime
        {
            get { return (TimeSpan)GetValue(InterruptingTimeProperty); }
            set { SetValue(InterruptingTimeProperty, value); }
        }

        #endregion

        #region Min activity brake time
        
        public static readonly DependencyProperty BreakTimeProperty
            = DependencyProperty.Register("BreakTime", typeof(TimeSpan), typeof(ToastNotification), new PropertyMetadata(TimeSpan.FromMinutes(5)));

        public TimeSpan BreakTime
        {
            get { return (TimeSpan)GetValue(BreakTimeProperty); }
            set { SetValue(BreakTimeProperty, value); }
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
