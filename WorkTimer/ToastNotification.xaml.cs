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
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WorkTimer
{
    /// <summary>
    /// Interaction logic for ToastNotification.xaml
    /// </summary>
    public partial class ToastNotification : Window
    {
        public ToastNotification()
        {
            InitializeComponent();

            Action updateDisplayMessage = () => {
                DisplayMessage = Message + (CloseTimeSpan.HasValue ? " (" + CloseTimeSpan.Value.ToString(@"mm\:ss") + ")" : "");
            };

            Action updateCloseTimeSpan = () => {
                var value = CloseDateTime.HasValue
                  ? CloseDateTime.Value - DateTime.Now
                  : (TimeSpan?)null;
                SetValue(CloseTimeSpanProperty, value);
            };

            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Start();
            this.Closed += (s, args) => timer.Stop();

            var messageDescriptor = DependencyPropertyDescriptor.FromProperty(ToastNotification.MessageProperty, typeof(ToastNotification));
            var closeDateTimeDescriptor = DependencyPropertyDescriptor.FromProperty(ToastNotification.CloseDateTimeProperty, typeof(ToastNotification));
            var closeTimeSpanDescriptor = DependencyPropertyDescriptor.FromProperty(ToastNotification.CloseTimeSpanProperty, typeof(ToastNotification));

            closeDateTimeDescriptor.AddValueChanged(this, (s, args) => updateCloseTimeSpan());
            timer.Tick += (s, args) => updateCloseTimeSpan();
            messageDescriptor.AddValueChanged(this, (s, args) => updateDisplayMessage());
            closeTimeSpanDescriptor.AddValueChanged(this, (s, args) => updateDisplayMessage());
            closeTimeSpanDescriptor.AddValueChanged(this, (s, args) =>
            {
                if (CloseTimeSpan.HasValue && CloseTimeSpan.Value <= TimeSpan.Zero)
                {
                    this.Close();
                }
            });
        }

        #region Message

        public static readonly DependencyProperty MessageProperty
            = DependencyProperty.Register("Message", typeof(string), typeof(ToastNotification), new PropertyMetadata(""));

        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        #endregion

        #region DisplayMessage

        public static readonly DependencyProperty DisplayMessageProperty
            = DependencyProperty.Register("DisplayMessage", typeof(string), typeof(ToastNotification), new PropertyMetadata(""));

        public string DisplayMessage
        {
            get { return (string)GetValue(DisplayMessageProperty); }
            private set { SetValue(DisplayMessageProperty, value); }
        }

        #endregion

        #region Transparent for mouse

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

        // Transparent for mouse property
        public static readonly DependencyProperty TransparentForMouseProperty
            = DependencyProperty.Register("TransparentForMouse", typeof(bool), typeof(ToastNotification), new PropertyMetadata(false, new PropertyChangedCallback(_TransparentForMousePropertyChanged)));

        public bool TransparentForMouse
        {
            get { return (bool)GetValue(TransparentForMouseProperty); }
            set { SetValue(TransparentForMouseProperty, value); }
        }

        private static void _TransparentForMousePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var toast = d as ToastNotification;
            if (toast != null)
            {
                toast.TransparentForMousePropertyChanged(e);
            }
        }

        private void TransparentForMousePropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (TransparentForMouse)
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            }
            else
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
            }
        }

        #endregion

        #region Animation boolean property

        public static readonly DependencyProperty AnimationProperty
            = DependencyProperty.Register("Animation", typeof(bool), typeof(ToastNotification), new PropertyMetadata(false));

        public bool Animation
        {
            get { return (bool)GetValue(AnimationProperty); }
            set { SetValue(AnimationProperty, value); }
        }

        #endregion

        #region Close DateTime nullable property

        public static readonly DependencyProperty CloseDateTimeProperty
            = DependencyProperty.Register("CloseDateTime", typeof(DateTime?), typeof(ToastNotification), new PropertyMetadata(null));

        public DateTime? CloseDateTime
        {
            get { return (DateTime?)GetValue(CloseDateTimeProperty); }
            set { SetValue(CloseDateTimeProperty, value); }
        }

        #endregion

        #region Close TimeSpan nullable property with auto update every second

        public static readonly DependencyProperty CloseTimeSpanProperty
            = DependencyProperty.Register("CloseTimeSpan", typeof(TimeSpan?), typeof(ToastNotification), new PropertyMetadata(null));

        public TimeSpan? CloseTimeSpan
        {
            get { return (TimeSpan?)GetValue(CloseTimeSpanProperty); }
            set {
                CloseDateTime = value.HasValue ? DateTime.Now + value.Value : (DateTime?)null;
            }
        }

        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // set the window to be bottom center of the client area of the screen
            var workingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = workingArea.Left + (workingArea.Width - this.Width) / 2;
            this.Top = workingArea.Bottom - this.Height;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Animation = !Animation;
        }
    }
}
