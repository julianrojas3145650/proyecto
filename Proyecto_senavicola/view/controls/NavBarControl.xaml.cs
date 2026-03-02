using System;
using System.Windows;
using System.Windows.Controls;
using Proyecto_senavicola.services;

namespace Proyecto_senavicola.view.controls
{
    public partial class NavBarControl : UserControl
    {
        public NavBarControl()
        {
            InitializeComponent();
            CargarInformacionUsuario();
        }

        public void SetModulo(string modulo)
        {
            txtModulo.Text = modulo;
        }

        public void SetModulo(string modulo, string descripcion)
        {
            txtModulo.Text = modulo;
            txtDescripcion.Text = descripcion;
        }

        private void CargarInformacionUsuario()
        {
            if (AuthenticationService.UsuarioActual != null)
            {
                txtUsuarioNombre.Text = AuthenticationService.UsuarioActual.NombreCompleto;
                txtUsuarioRol.Text = AuthenticationService.UsuarioActual.Rol;
            }
            else
            {
                txtUsuarioNombre.Text = "Invitado";
                txtUsuarioRol.Text = "Sin sesión";
            }
        }
        private void BtnPerfilUsuario_Click(object sender, RoutedEventArgs e)
        {
            // Abrir diálogo de perfil de usuario
            var perfilDialog = new view.dialogs.PerfilUsuarioDialog();

            // Obtener la ventana contenedora de este UserControl
            var ownerWindow = Window.GetWindow(this);
            if (ownerWindow != null)
                perfilDialog.Owner = ownerWindow;

            perfilDialog.ShowDialog();
        }

        public void SetNotificaciones(int count)
        {
            if (count > 0)
            {
                badgeNotificaciones.Visibility = Visibility.Visible;
                txtCountNotificaciones.Text = count > 99 ? "99+" : count.ToString();
            }
            else
            {
                badgeNotificaciones.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnNotificaciones_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funcionalidad de notificaciones en desarrollo",
                "Notificaciones",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnAyuda_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Ayuda del sistema\n\nEsta es una aplicación de gestión avícola.\n\n" +
                "Para obtener ayuda específica sobre cada módulo, consulta la documentación.",
                "Ayuda",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public void RefrescarUsuario()
        {
            CargarInformacionUsuario();
        }
    }
}
