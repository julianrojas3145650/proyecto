using Proyecto_senavicola.view.window;
using System.Windows;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class Cerrar_sesion : Window
    {
        public bool IsConfirmed { get; private set; } = false;

        public Cerrar_sesion()
        {
            InitializeComponent();
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            var welcomeWindow = new WelcomeWindow();
            welcomeWindow.Show();

            foreach (Window w in Application.Current.Windows)
            {
                if (w != welcomeWindow)
                    w.Close();
            }
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
