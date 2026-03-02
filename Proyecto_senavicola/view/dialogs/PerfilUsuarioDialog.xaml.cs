using System.Windows;
using Proyecto_senavicola.services;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class PerfilUsuarioDialog : Window
    {
        public PerfilUsuarioDialog()
        {
            InitializeComponent();
            CargarDatosUsuario();
        }

        private void CargarDatosUsuario()
        {
            if (AuthenticationService.UsuarioActual != null)
            {
                var usuario = AuthenticationService.UsuarioActual;
                txtDocumento.Text = usuario.Documento;
                txtNombre.Text = usuario.Nombre;
                txtApellido.Text = usuario.Apellido;
                txtEmail.Text = usuario.Email;
                txtRol.Text = usuario.Rol;
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}