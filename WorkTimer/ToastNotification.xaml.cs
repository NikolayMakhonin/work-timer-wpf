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
using System.Windows.Interop;

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

            Action updateTransparentForMouse = () => {
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
            };

            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Start();
            this.Closed += (s, args) => timer.Stop();

            var messageDescriptor = DependencyPropertyDescriptor.FromProperty(ToastNotification.MessageProperty, typeof(ToastNotification));
            var closeDateTimeDescriptor = DependencyPropertyDescriptor.FromProperty(ToastNotification.CloseDateTimeProperty, typeof(ToastNotification));
            var closeTimeSpanDescriptor = DependencyPropertyDescriptor.FromProperty(ToastNotification.CloseTimeSpanProperty, typeof(ToastNotification));
            var transparentForMouseDescriptor = DependencyPropertyDescriptor.FromProperty(ToastNotification.TransparentForMouseProperty, typeof(ToastNotification));

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
            transparentForMouseDescriptor.AddValueChanged(this, (s, args) => updateTransparentForMouse());
            this.Loaded += (s, args) => updateTransparentForMouse();

            // detect global mouse and keyboard events, and log last activity time
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

        public static readonly DependencyProperty TransparentForMouseProperty
            = DependencyProperty.Register("TransparentForMouse", typeof(bool), typeof(ToastNotification), new PropertyMetadata(false));

        public bool TransparentForMouse
        {
            get { return (bool)GetValue(TransparentForMouseProperty); }
            set { SetValue(TransparentForMouseProperty, value); }
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
            // set the window to be bottom center of the screen
            this.Left = System.Windows.SystemParameters.PrimaryScreenWidth / 2 - this.Width / 2;
            this.Top = System.Windows.SystemParameters.PrimaryScreenHeight - this.Height - 10;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Animation = !Animation;
        }
    }
}
