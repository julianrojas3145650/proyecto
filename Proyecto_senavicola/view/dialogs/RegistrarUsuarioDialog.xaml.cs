using System.Windows;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class RegistrarUsuarioDialog : Window
    {
        public string Documento { get; private set; }
        public string Nombre { get; private set; }
        public string Apellido { get; private set; }
        public string Email { get; private set; }
        public string Rol { get; private set; }
        public string Password { get; private set; }

        private bool esEdicion = false;

        // Constructor para crear nuevo usuario
        public RegistrarUsuarioDialog()
        {
            InitializeComponent();
            cmbRol.SelectedIndex = 0;
            esEdicion = false;
        }

        // Constructor para editar usuario existente
        public RegistrarUsuarioDialog(object usuario)
        {
            InitializeComponent();
            esEdicion = true;

            txtTitulo.Text = "✏️ Editar Usuario";
            btnGuardar.Content = "Actualizar";

            // Ocultar campos de contraseña en edición
            pnlPassword.Visibility = Visibility.Collapsed;

            // Cargar datos del usuario (asumiendo que es un dynamic o UsuarioModel)
            dynamic user = usuario;
            txtDocumento.Text = user.Documento;
            txtDocumento.IsEnabled = false; // No permitir cambiar documento
            txtNombre.Text = user.Nombre;
            txtApellido.Text = user.Apellido;
            txtEmail.Text = user.Email;

            switch (user.Rol)
            {
                case "Administrador":
                    cmbRol.SelectedIndex = 0;
                    break;
                case "Aprendiz":
                    cmbRol.SelectedIndex = 1;
                    break;
                case "Visitante":
                    cmbRol.SelectedIndex = 2;
                    break;
            }
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(txtDocumento.Text))
            {
                MessageBox.Show("El documento es obligatorio.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtDocumento.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNombre.Text))
            {
                MessageBox.Show("El nombre es obligatorio.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNombre.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtApellido.Text))
            {
                MessageBox.Show("El apellido es obligatorio.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtApellido.Focus();
                return;
            }

            if (cmbRol.SelectedIndex == -1)
            {
                MessageBox.Show("Debes seleccionar un rol.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                cmbRol.Focus();
                return;
            }

            // Validar contraseñas solo si es nuevo usuario
            if (!esEdicion)
            {
                if (string.IsNullOrWhiteSpace(txtPassword.Password))
                {
                    MessageBox.Show("La contraseña es obligatoria.", "Validación",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtPassword.Focus();
                    return;
                }

                if (txtPassword.Password.Length < 6)
                {
                    MessageBox.Show("La contraseña debe tener al menos 6 caracteres.", "Validación",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtPassword.Focus();
                    return;
                }

                if (txtPassword.Password != txtConfirmarPassword.Password)
                {
                    MessageBox.Show("Las contraseñas no coinciden.", "Validación",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtConfirmarPassword.Focus();
                    return;
                }

                Password = txtPassword.Password;
            }

            // Asignar valores
            Documento = txtDocumento.Text.Trim();
            Nombre = txtNombre.Text.Trim();
            Apellido = txtApellido.Text.Trim();
            Email = txtEmail.Text.Trim();
            Rol = (cmbRol.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString();

            DialogResult = true;
            this.Close();
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