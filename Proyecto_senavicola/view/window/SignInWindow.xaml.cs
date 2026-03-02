using System.Windows;
using System.Windows.Input;
using Proyecto_senavicola.services;

namespace Proyecto_senavicola.view.window
{
    public partial class SignInWindow : Window
    {
        public SignInWindow()
        {
            InitializeComponent();
        }

        private void BtnSignIn_Click(object sender, RoutedEventArgs e)
        {
            IniciarSesion();
        }

        private void TxtDocumento_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                txtPassword.Focus();
            }
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                IniciarSesion();
            }
        }

        private void IniciarSesion()
        {
            // Validar campos
            if (string.IsNullOrWhiteSpace(txtDocumento.Text))
            {
                MessageBox.Show("Por favor, ingresa tu documento.",
                    "Campos Requeridos", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtDocumento.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                MessageBox.Show("Por favor, ingresa la contraseña.",
                    "Campos Requeridos", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPassword.Focus();
                return;
            }

            // ✅ VALIDACIÓN NUMÉRICA DEL DOCUMENTO (AGREGADA AQUÍ)
            if (!AuthenticationService.ValidarDocumentoNumerico(txtDocumento.Text.Trim()))
            {
                MessageBox.Show(
                    "❌ El documento debe contener solo números.\n\n" +
                    "No se permiten letras ni caracteres especiales.",
                    "Error de Validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                txtDocumento.Focus();
                txtDocumento.SelectAll();
                return;
            }

            try
            {
                // Intentar iniciar sesión
                bool loginExitoso = AuthenticationService.IniciarSesionPorDocumento(
                    txtDocumento.Text.Trim(),
                    txtPassword.Password
                );

                if (loginExitoso)
                {
                    // Abrir dashboard
                    SeleccionCamaraDialog dashboard = new SeleccionCamaraDialog();
                    dashboard.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show(
                        "Documento o contraseña incorrectos.\n\n" +
                        "Verifica e intenta nuevamente.",
                        "Error de Autenticación",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    // Limpiar contraseña
                    txtPassword.Clear();
                    txtDocumento.Focus();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Error al intentar iniciar sesión:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            WelcomeWindow welcomeWindow = new WelcomeWindow();
            welcomeWindow.Show();
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
