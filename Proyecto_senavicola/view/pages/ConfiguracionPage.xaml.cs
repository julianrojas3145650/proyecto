using System;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Proyecto_senavicola.data;
using Proyecto_senavicola.services;
using Proyecto_senavicola.view.dialogs;

namespace Proyecto_senavicola.view.pages
{
    public partial class ConfiguracionPage : Page
    {
        public ConfiguracionPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            navBar.SetModulo("⚙️ Configuración", "Gestión y parámetros del sistema");
            ConfigurarPermisos();
        }


        private void ConfigurarPermisos()
        {
            // Solo administradores pueden cambiar contraseñas
            btnCambiarContraseñas.IsEnabled = AuthenticationService.TienePermiso("CambiarContraseña");

            // Solo administradores pueden restaurar BD
            btnRestaurarDB.IsEnabled = AuthenticationService.TienePermiso("Administrar");

            // Solo administradores pueden limpiar historial
            btnLimpiarHistorial.IsEnabled = AuthenticationService.TienePermiso("Administrar");
        }

        #region Cambiar Contraseñas

        private void BtnGestionUsuarios_Click(object sender, RoutedEventArgs e)
        {
            // Verificar permisos (opcional pero recomendado)
            if (!AuthenticationService.TienePermiso("Administrar"))
            {
                MessageBox.Show("Solo los administradores pueden gestionar usuarios.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new GestionUsuariosDialog();
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
        }

        #endregion

        #region Backup y Restauración

        private void BtnBackupDB_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "SQLite Database|*.db",
                    FileName = $"Backup_Senavicola_{DateTime.Now:yyyyMMdd_HHmmss}.db",
                    Title = "Guardar Respaldo de Base de Datos"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    string rutaOrigen = DatabaseHelper.ObtenerRutaBaseDatos();
                    File.Copy(rutaOrigen, saveDialog.FileName, true);

                    DatabaseHelper.RegistrarHistorial("Configuración", "Backup BD",
                        $"Respaldo creado en: {saveDialog.FileName}",
                        AuthenticationService.UsuarioActual?.Id);

                    MessageBox.Show(
                        $"✅ Respaldo creado exitosamente:\n\n{saveDialog.FileName}",
                        "Backup Exitoso",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear respaldo:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRestaurarDB_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthenticationService.TienePermiso("Administrar"))
            {
                MessageBox.Show("Solo los administradores pueden restaurar la base de datos.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var resultado = MessageBox.Show(
                "⚠️ ADVERTENCIA: Esta acción reemplazará toda la base de datos actual.\n\n" +
                "Se perderán todos los datos que no estén en el respaldo.\n\n" +
                "¿Estás seguro de continuar?",
                "Confirmar Restauración",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (resultado == MessageBoxResult.Yes)
            {
                try
                {
                    var openDialog = new OpenFileDialog
                    {
                        Filter = "SQLite Database|*.db",
                        Title = "Seleccionar Archivo de Respaldo"
                    };

                    if (openDialog.ShowDialog() == true)
                    {
                        string rutaDestino = DatabaseHelper.ObtenerRutaBaseDatos();

                        // Cerrar todas las conexiones
                        GC.Collect();
                        GC.WaitForPendingFinalizers();

                        File.Copy(openDialog.FileName, rutaDestino, true);

                        DatabaseHelper.RegistrarHistorial("Configuración", "Restaurar BD",
                            $"BD restaurada desde: {openDialog.FileName}",
                            AuthenticationService.UsuarioActual?.Id);

                        MessageBox.Show(
                            "✅ Base de datos restaurada exitosamente.\n\n" +
                            "Se recomienda reiniciar la aplicación.",
                            "Restauración Exitosa",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al restaurar base de datos:\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Limpiar Historial

        private void BtnLimpiarHistorial_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthenticationService.TienePermiso("Administrar"))
            {
                MessageBox.Show("Solo los administradores pueden limpiar el historial.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var resultado = MessageBox.Show(
                "¿Cuánto historial deseas conservar?\n\n" +
                "Sí = Últimos 30 días\n" +
                "No = Últimos 7 días\n" +
                "Cancelar = Cancelar operación",
                "Limpiar Historial",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (resultado == MessageBoxResult.Cancel)
                return;

            int diasConservar = resultado == MessageBoxResult.Yes ? 30 : 7;

            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();

                    string sql = $"DELETE FROM Historial WHERE FechaHora < datetime('now', '-{diasConservar} days')";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        int registrosEliminados = cmd.ExecuteNonQuery();

                        DatabaseHelper.RegistrarHistorial("Configuración", "Limpiar Historial",
                            $"{registrosEliminados} registros eliminados (conservados últimos {diasConservar} días)",
                            AuthenticationService.UsuarioActual?.Id);

                        MessageBox.Show(
                            $"✅ {registrosEliminados} registros de historial eliminados.\n\n" +
                            $"Se conservaron los últimos {diasConservar} días.",
                            "Historial Limpiado",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al limpiar historial:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Información y Acerca de

        private void BtnInfoSistema_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string info = "";

                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();

                    // Contar registros
                    info += "📊 Estadísticas de la Base de Datos:\n\n";

                    string[] tablas = { "Galpones", "LotesGallinas", "Huevos", "Insumos", "Usuarios", "Historial" };

                    foreach (var tabla in tablas)
                    {
                        string sql = $"SELECT COUNT(*) FROM {tabla}";
                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            int count = Convert.ToInt32(cmd.ExecuteScalar());
                            info += $"• {tabla}: {count} registros\n";
                        }
                    }

                    // Tamaño de la BD
                    FileInfo dbFile = new FileInfo(DatabaseHelper.ObtenerRutaBaseDatos());
                    double sizeMB = dbFile.Length / (1024.0 * 1024.0);
                    info += $"\n📁 Tamaño de BD: {sizeMB:F2} MB\n";

                    // Último acceso
                    info += $"\n🕒 Último Acceso: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n";
                    info += $"👤 Usuario: {AuthenticationService.UsuarioActual?.NombreCompleto ?? "N/A"}";
                }

                MessageBox.Show(info, "Información del Sistema",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al obtener información:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAcercaDe_Click(object sender, RoutedEventArgs e)
        {
            string mensaje = "Tecnologías:\n" +
                           "• WPF .NET Framework 4.7.2\n" +
                           "• SQLite Database\n" +
                           "• LiveCharts para gráficos\n\n" +
                           "SENA - 2025";

            MessageBox.Show(mensaje, "Acerca de Senavícola",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion
    }
}