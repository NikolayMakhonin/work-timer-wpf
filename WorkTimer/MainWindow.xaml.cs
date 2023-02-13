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
        }


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
