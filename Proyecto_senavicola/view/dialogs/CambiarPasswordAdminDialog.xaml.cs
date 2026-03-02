using System;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Proyecto_senavicola.data;
using Proyecto_senavicola.services;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class CambiarPasswordAdminDialog : Window
    {
        public string NuevaPassword { get; private set; }

        private string documentoUsuario;
        private string nombreUsuario;

        public CambiarPasswordAdminDialog(string documento, string nombre)
        {
            InitializeComponent();
            documentoUsuario = documento;
            nombreUsuario = nombre;
            txtUsuarioInfo.Text = $"{nombre} (Doc: {documento})";
        }

        private void Password_Changed(object sender, RoutedEventArgs e)
        {
            ValidarPasswords();
        }

        private void ValidarPasswords()
        {
            string nueva = txtNuevaPassword.Password;
            string confirmar = txtConfirmarPassword.Password;

            // Resetear mensaje
            txtMensajeValidacion.Visibility = Visibility.Collapsed;
            btnGuardar.IsEnabled = false;

            // Validar que no estén vacías
            if (string.IsNullOrWhiteSpace(nueva))
            {
                return;
            }

            // Validar longitud mínima
            if (nueva.Length < 6)
            {
                MostrarMensajeError("❌ La contraseña debe tener al menos 6 caracteres");
                return;
            }

            // Validar que coincidan
            if (nueva != confirmar)
            {
                if (!string.IsNullOrWhiteSpace(confirmar))
                {
                    MostrarMensajeError("❌ Las contraseñas no coinciden");
                }
                return;
            }

            // Todo OK
            MostrarMensajeExito("✅ Las contraseñas coinciden y son válidas");
            btnGuardar.IsEnabled = true;
        }

        private void MostrarMensajeError(string mensaje)
        {
            txtMensajeValidacion.Text = mensaje;
            txtMensajeValidacion.Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69));
            txtMensajeValidacion.Visibility = Visibility.Visible;
        }

        private void MostrarMensajeExito(string mensaje)
        {
            txtMensajeValidacion.Text = mensaje;
            txtMensajeValidacion.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            txtMensajeValidacion.Visibility = Visibility.Visible;
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string nueva = txtNuevaPassword.Password;
            string confirmar = txtConfirmarPassword.Password;

            // Validación final
            if (string.IsNullOrWhiteSpace(nueva) || nueva.Length < 6)
            {
                MessageBox.Show("La contraseña debe tener al menos 6 caracteres.",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (nueva != confirmar)
            {
                MessageBox.Show("Las contraseñas no coinciden.",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirmación
            var result = MessageBox.Show(
                $"¿Estás seguro de cambiar la contraseña de:\n\n{nombreUsuario} (Doc: {documentoUsuario})?",
                "Confirmar Cambio",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // ✅ GUARDAR EN LA BASE DE DATOS
                    bool exito = AuthenticationService.CambiarPasswordAdmin(documentoUsuario, nueva);

                    if (exito)
                    {
                        NuevaPassword = nueva;
                        DialogResult = true;

                        MessageBox.Show("✅ Contraseña actualizada exitosamente en la base de datos.",
                            "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("❌ Error: No se pudo actualizar la contraseña. Verifica que el usuario exista.",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Error al guardar la contraseña:\n\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }
    }
}
