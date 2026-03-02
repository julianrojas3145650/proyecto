using Proyecto_senavicola.view;
using Proyecto_senavicola.services;
using System.Windows;
using System.Windows.Input;

namespace Proyecto_senavicola.view.window
{
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();
        }

        private void BtnSignIn_Click(object sender, RoutedEventArgs e)
        {
            SignInWindow loginWindow = new SignInWindow();
            loginWindow.Show();
            this.Close();
        }

        private void BtnGuest_Click(object sender, RoutedEventArgs e)
        {
            // Iniciar sesión como invitado
            AuthenticationService.IniciarSesionComoInvitado();

            // Abrir dashboard en modo solo lectura
            SeleccionCamaraDialog dashboard = new SeleccionCamaraDialog();
            dashboard.Show();
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnMinimizar_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}