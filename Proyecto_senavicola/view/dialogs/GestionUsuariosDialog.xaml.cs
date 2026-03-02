using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Media;
using Proyecto_senavicola.data;
using Proyecto_senavicola.services;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class GestionUsuariosDialog : Window
    {
        private ObservableCollection<UsuarioModel> usuarios;

        public GestionUsuariosDialog()
        {
            InitializeComponent();
            usuarios = new ObservableCollection<UsuarioModel>();
            dgUsuarios.ItemsSource = usuarios;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CargarUsuarios();
        }

        private void CargarUsuarios()
        {
            usuarios.Clear();

            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT Id, Documento, Nombre, Apellido, Email, Rol, Activo FROM Usuarios ORDER BY Documento";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            usuarios.Add(new UsuarioModel
                            {
                                Id = reader.GetInt32(0),
                                Documento = reader.GetString(1),
                                Nombre = reader.GetString(2),
                                Apellido = reader.GetString(3),
                                Email = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Rol = reader.GetString(5),
                                Activo = reader.GetInt32(6) == 1
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar usuarios: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAgregarUsuario_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RegistrarUsuarioDialog();
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string sql = @"
                            INSERT INTO Usuarios (Documento, Nombre, Apellido, Email, Rol, Password)
                            VALUES (@doc, @nombre, @apellido, @email, @rol, @pass)";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@doc", dialog.Documento);
                            cmd.Parameters.AddWithValue("@nombre", dialog.Nombre);
                            cmd.Parameters.AddWithValue("@apellido", dialog.Apellido);
                            cmd.Parameters.AddWithValue("@email", dialog.Email);
                            cmd.Parameters.AddWithValue("@rol", dialog.Rol);
                            cmd.Parameters.AddWithValue("@pass", DatabaseHelper.HashPassword(dialog.Password));

                            cmd.ExecuteNonQuery();
                        }
                    }

                    DatabaseHelper.RegistrarHistorial("Usuarios", "Crear Usuario",
                        $"Usuario {dialog.Documento} creado con rol {dialog.Rol}",
                        AuthenticationService.UsuarioActual?.Id);

                    CargarUsuarios();

                    MessageBox.Show("✅ Usuario registrado exitosamente.", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al registrar usuario: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnCambiarPassword_Click(object sender, RoutedEventArgs e)
        {
            var usuario = (sender as System.Windows.Controls.Button)?.Tag as UsuarioModel;
            if (usuario == null) return;

            var dialog = new CambiarPasswordAdminDialog(usuario.Documento, usuario.NombreCompleto);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                if (AuthenticationService.CambiarPasswordAdmin(usuario.Documento, dialog.NuevaPassword))
                {
                    MessageBox.Show($"✅ Contraseña de {usuario.NombreCompleto} actualizada correctamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Error al cambiar la contraseña.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEditarUsuario_Click(object sender, RoutedEventArgs e)
        {
            var usuario = (sender as System.Windows.Controls.Button)?.Tag as UsuarioModel;
            if (usuario == null) return;

            var dialog = new RegistrarUsuarioDialog(usuario);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string sql = @"
                            UPDATE Usuarios 
                            SET Nombre = @nombre, Apellido = @apellido, Email = @email, Rol = @rol
                            WHERE Id = @id";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@nombre", dialog.Nombre);
                            cmd.Parameters.AddWithValue("@apellido", dialog.Apellido);
                            cmd.Parameters.AddWithValue("@email", dialog.Email);
                            cmd.Parameters.AddWithValue("@rol", dialog.Rol);
                            cmd.Parameters.AddWithValue("@id", usuario.Id);

                            cmd.ExecuteNonQuery();
                        }
                    }

                    DatabaseHelper.RegistrarHistorial("Usuarios", "Editar Usuario",
                        $"Usuario {usuario.Documento} actualizado",
                        AuthenticationService.UsuarioActual?.Id);

                    CargarUsuarios();

                    MessageBox.Show("✅ Usuario actualizado exitosamente.", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al actualizar usuario: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDesactivarUsuario_Click(object sender, RoutedEventArgs e)
        {
            var usuario = (sender as System.Windows.Controls.Button)?.Tag as UsuarioModel;
            if (usuario == null) return;

            if (usuario.Documento == AuthenticationService.UsuarioActual?.Documento)
            {
                MessageBox.Show("No puedes desactivar tu propio usuario.", "Operación No Permitida",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var accion = usuario.Activo ? "desactivar" : "activar";
            var resultado = MessageBox.Show(
                $"¿Estás seguro de {accion} al usuario {usuario.NombreCompleto}?",
                "Confirmar Acción",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resultado == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string sql = "UPDATE Usuarios SET Activo = @activo WHERE Id = @id";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@activo", usuario.Activo ? 0 : 1);
                            cmd.Parameters.AddWithValue("@id", usuario.Id);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    DatabaseHelper.RegistrarHistorial("Usuarios", $"{(usuario.Activo ? "Desactivar" : "Activar")} Usuario",
                        $"Usuario {usuario.Documento}",
                        AuthenticationService.UsuarioActual?.Id);

                    CargarUsuarios();

                    MessageBox.Show($"✅ Usuario {(usuario.Activo ? "desactivado" : "activado")} exitosamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al cambiar estado del usuario: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class UsuarioModel
    {
        public int Id { get; set; }
        public string Documento { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Email { get; set; }
        public string Rol { get; set; }
        public bool Activo { get; set; }

        public string NombreCompleto => $"{Nombre} {Apellido}";
        public string EstadoTexto => Activo ? "Activo" : "Inactivo";

        public Brush RolColor
        {
            get
            {
                return Rol switch
                {
                    "Administrador" => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                    "Aprendiz" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    "Visitante" => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                    _ => new SolidColorBrush(Color.FromRgb(128, 128, 128))
                };
            }
        }

        public Brush EstadoColor
        {
            get
            {
                return Activo
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                    : new SolidColorBrush(Color.FromRgb(158, 158, 158));
            }
        }
    }
}