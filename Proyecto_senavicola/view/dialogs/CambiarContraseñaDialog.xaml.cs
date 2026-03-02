using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Proyecto_senavicola.services;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class CambiarContraseñaDialog : Window
    {
        public string RolSeleccionado { get; private set; }
        public string NuevaContraseña { get; private set; }

        public CambiarContraseñaDialog()
        {
            InitializeComponent();
            cmbRol.SelectedIndex = 0;
        }

        private void BtnCambiar_Click(object sender, RoutedEventArgs e)
        {
            // Validaciones
            if (cmbRol.SelectedIndex == -1)
            {
                MessageBox.Show("Por favor, selecciona un rol.",
                    "Campo Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNuevaPassword.Password))
            {
                MessageBox.Show("Por favor, ingresa una nueva contraseña.",
                    "Campo Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (txtNuevaPassword.Password.Length < 6)
            {
                MessageBox.Show("La contraseña debe tener al menos 6 caracteres.",
                    "Contraseña Débil", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (txtNuevaPassword.Password != txtConfirmarPassword.Password)
            {
                MessageBox.Show("Las contraseñas no coinciden.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Obtener rol seleccionado
            var item = cmbRol.SelectedItem as ComboBoxItem;
            RolSeleccionado = item.Tag.ToString();
            NuevaContraseña = txtNuevaPassword.Password;

            // Confirmar cambio
            var resultado = MessageBox.Show(
                $"¿Estás seguro de cambiar la contraseña del rol '{RolSeleccionado}'?\n\n" +
                "Esta acción es permanente y afectará al acceso de este rol.",
                "Confirmar Cambio",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resultado == MessageBoxResult.Yes)
            {
                // Cambiar contraseña
                bool exito = AuthenticationService.CambiarContraseña(RolSeleccionado, NuevaContraseña);

                if (exito)
                {
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Error al cambiar la contraseña. Intenta nuevamente.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void TxtNuevaPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ActualizarIndicadorFortaleza();
        }

        private void TxtConfirmarPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ActualizarIndicadorFortaleza();
        }

        private void ActualizarIndicadorFortaleza()
        {
            string password = txtNuevaPassword.Password;

            if (string.IsNullOrEmpty(password))
            {
                borderIndicador.Visibility = Visibility.Collapsed;
                txtFortaleza.Visibility = Visibility.Collapsed;
                return;
            }

            borderIndicador.Visibility = Visibility.Visible;
            txtFortaleza.Visibility = Visibility.Visible;

            // Calcular fortaleza
            int fortaleza = 0;

            if (password.Length >= 6) fortaleza += 25;
            if (password.Length >= 8) fortaleza += 25;
            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]")) fortaleza += 15;
            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-z]")) fortaleza += 15;
            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[0-9]")) fortaleza += 10;
            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[^a-zA-Z0-9]")) fortaleza += 10;

            // Actualizar barra
            barraFortaleza.Width = borderIndicador.ActualWidth * fortaleza / 100;

            // Actualizar color y texto
            if (fortaleza < 40)
            {
                barraFortaleza.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                txtFortaleza.Text = "⚠️ Contraseña débil";
                txtFortaleza.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }
            else if (fortaleza < 70)
            {
                barraFortaleza.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                txtFortaleza.Text = "⚡ Contraseña media";
                txtFortaleza.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
            }
            else
            {
                barraFortaleza.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                txtFortaleza.Text = "✅ Contraseña fuerte";
                txtFortaleza.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }
        }
    }
}