using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Proyecto_senavicola.data;
using Proyecto_senavicola.services;
using Proyecto_senavicola.view.dialogs;

namespace Proyecto_senavicola.view.pages
{
    public partial class GallinasPage : Page
    {
        private ObservableCollection<LoteGallinasModel> lotesGallinas;
        private ObservableCollection<GalponModel> galpones;
        private ObservableCollection<AsignacionModel> asignaciones;

        public GallinasPage()
        {
            InitializeComponent();
            InicializarColecciones();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            navBar.SetModulo("🐔 Gestión de Gallinas", "Gestiona tus lotes y galpones de gallinas");
            CargarDatos();
        }

        private void InicializarColecciones()
        {
            lotesGallinas = new ObservableCollection<LoteGallinasModel>();
            galpones = new ObservableCollection<GalponModel>();
            asignaciones = new ObservableCollection<AsignacionModel>();

            dgLotesGallinas.ItemsSource = lotesGallinas;
            dgGalpones.ItemsSource = galpones;
            dgAsignaciones.ItemsSource = asignaciones;
        }

        private void CargarDatos()
        {
            CargarLotesGallinas();
            CargarGalpones();
            CargarAsignaciones();
        }

        #region Carga de Datos

        private void CargarLotesGallinas()
        {
            lotesGallinas.Clear();

            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT * FROM LotesGallinas ORDER BY FechaLlegada DESC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var lote = new LoteGallinasModel
                            {
                                Id = reader.GetInt32(0),
                                CodigoLote = reader.GetString(1),
                                CantidadTotal = reader.GetInt32(2),
                                CantidadAsignada = reader.GetInt32(3),
                                CantidadPendiente = reader.GetInt32(4),
                                Raza = reader.GetString(5),
                                EdadSemanas = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                                FechaLlegada = reader.GetDateTime(7),
                                Proveedor = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                Observaciones = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                Estado = reader.GetString(10)
                            };

                            lotesGallinas.Add(lote);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar lotes: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CargarGalpones()
        {
            galpones.Clear();

            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT * FROM Galpones WHERE Activo = 1 ORDER BY Codigo";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var galpon = new GalponModel
                            {
                                Id = reader.GetInt32(0),
                                Codigo = reader.GetString(1),
                                Nombre = reader.GetString(2),
                                Longitud = reader.GetDouble(3),
                                Raza = reader.GetString(4),
                                RacionPorAve = reader.GetDouble(5),
                                CantidadAves = reader.GetInt32(6)
                            };

                            galpones.Add(galpon);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar galpones: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CargarAsignaciones()
        {
            asignaciones.Clear();

            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT 
                            a.FechaAsignacion,
                            l.CodigoLote,
                            g.Codigo as CodigoGalpon,
                            a.Cantidad,
                            l.Raza,
                            u.Nombre || ' ' || u.Apellido as Usuario
                        FROM AsignacionGallinas a
                        INNER JOIN LotesGallinas l ON a.LoteId = l.Id
                        INNER JOIN Galpones g ON a.GalponId = g.Id
                        LEFT JOIN Usuarios u ON a.UsuarioId = u.Id
                        ORDER BY a.FechaAsignacion DESC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var asignacion = new AsignacionModel
                            {
                                FechaAsignacion = reader.GetDateTime(0),
                                CodigoLote = reader.GetString(1),
                                CodigoGalpon = reader.GetString(2),
                                Cantidad = reader.GetInt32(3),
                                Raza = reader.GetString(4),
                                UsuarioNombre = reader.IsDBNull(5) ? "N/A" : reader.GetString(5)
                            };

                            asignaciones.Add(asignacion);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar asignaciones: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Eventos de Botones - Lotes

        private void BtnRegistrarLote_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthenticationService.TienePermiso("Gestionar"))
            {
                MessageBox.Show("No tienes permisos para registrar lotes.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new RegistrarLoteDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string sql = @"
                            INSERT INTO LotesGallinas 
                            (CodigoLote, CantidadTotal, CantidadPendiente, Raza, EdadSemanas, 
                             FechaLlegada, Proveedor, Observaciones)
                            VALUES (@codigo, @total, @total, @raza, @edad, @fecha, @proveedor, @obs)";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@codigo", dialog.CodigoLote);
                            cmd.Parameters.AddWithValue("@total", dialog.Cantidad);
                            cmd.Parameters.AddWithValue("@raza", dialog.Raza);
                            cmd.Parameters.AddWithValue("@edad", dialog.EdadSemanas);
                            cmd.Parameters.AddWithValue("@fecha", dialog.FechaLlegada);
                            cmd.Parameters.AddWithValue("@proveedor", dialog.Proveedor);
                            cmd.Parameters.AddWithValue("@obs", dialog.Observaciones);

                            cmd.ExecuteNonQuery();
                        }
                    }

                    DatabaseHelper.RegistrarHistorial("Gallinas", "Registrar Lote",
                        $"Lote {dialog.CodigoLote} - {dialog.Cantidad} gallinas {dialog.Raza}",
                        AuthenticationService.UsuarioActual?.Id);

                    CargarLotesGallinas();

                    MessageBox.Show($"✅ Lote {dialog.CodigoLote} registrado exitosamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al registrar lote: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnUbicarGallinas_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthenticationService.TienePermiso("Gestionar"))
            {
                MessageBox.Show("No tienes permisos para ubicar gallinas.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var lote = (sender as Button)?.Tag as LoteGallinasModel;
            if (lote == null) return;

            if (lote.CantidadPendiente == 0)
            {
                MessageBox.Show("Este lote ya tiene todas las gallinas asignadas.",
                    "Lote Completo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new UbicarGallinasDialog(lote, galpones.ToList());
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                // Declarar variables FUERA del try para usarlas después
                int nuevaCantidadAsignada = 0;
                int nuevaCantidadPendiente = 0;

                try
                {
                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        using (var transaction = conn.BeginTransaction())
                        {
                            // Registrar asignación
                            string sqlAsignacion = @"
                        INSERT INTO AsignacionGallinas (LoteId, GalponId, Cantidad, UsuarioId)
                        VALUES (@lote, @galpon, @cantidad, @usuario)";

                            using (var cmd = new SQLiteCommand(sqlAsignacion, conn))
                            {
                                cmd.Parameters.AddWithValue("@lote", lote.Id);
                                cmd.Parameters.AddWithValue("@galpon", dialog.GalponSeleccionado.Id);
                                cmd.Parameters.AddWithValue("@cantidad", dialog.CantidadAsignar);
                                cmd.Parameters.AddWithValue("@usuario",
                                    AuthenticationService.UsuarioActual?.Id ?? (object)DBNull.Value);
                                cmd.ExecuteNonQuery();
                            }

                            // Calcular nuevas cantidades
                            nuevaCantidadAsignada = lote.CantidadAsignada + dialog.CantidadAsignar;
                            nuevaCantidadPendiente = lote.CantidadPendiente - dialog.CantidadAsignar;
                            string nuevoEstado = nuevaCantidadPendiente == 0 ? "Completo" :
                                               (nuevaCantidadAsignada > 0 ? "Parcial" : "Pendiente");

                            // Actualizar lote
                            string sqlLote = @"
                        UPDATE LotesGallinas 
                        SET CantidadAsignada = @asignada, 
                            CantidadPendiente = @pendiente,
                            Estado = @estado
                        WHERE Id = @id";

                            using (var cmd = new SQLiteCommand(sqlLote, conn))
                            {
                                cmd.Parameters.AddWithValue("@asignada", nuevaCantidadAsignada);
                                cmd.Parameters.AddWithValue("@pendiente", nuevaCantidadPendiente);
                                cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                                cmd.Parameters.AddWithValue("@id", lote.Id);
                                cmd.ExecuteNonQuery();
                            }

                            // Actualizar cantidad en galpón
                            string sqlGalpon = @"
                        UPDATE Galpones 
                        SET CantidadAves = CantidadAves + @cantidad
                        WHERE Id = @id";

                            using (var cmd = new SQLiteCommand(sqlGalpon, conn))
                            {
                                cmd.Parameters.AddWithValue("@cantidad", dialog.CantidadAsignar);
                                cmd.Parameters.AddWithValue("@id", dialog.GalponSeleccionado.Id);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                    }

                    DatabaseHelper.RegistrarHistorial("Gallinas", "Ubicar Gallinas",
                        $"{dialog.CantidadAsignar} gallinas del lote {lote.CodigoLote} al galpón {dialog.GalponSeleccionado.Codigo}",
                        AuthenticationService.UsuarioActual?.Id);

                    CargarDatos();

                    MessageBox.Show(
                        $"✅ {dialog.CantidadAsignar} gallinas ubicadas en {dialog.GalponSeleccionado.Codigo}\n\n" +
                        $"Pendientes del lote: {nuevaCantidadPendiente}",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al ubicar gallinas: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDetalleLote_Click(object sender, RoutedEventArgs e)
        {
            var lote = (sender as Button)?.Tag as LoteGallinasModel;
            if (lote == null) return;

            var dialog = new DetalleLoteDialog(lote);
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
        }

        #endregion

        #region Eventos de Botones - Galpones

        private void BtnRegistrarGalpon_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthenticationService.TienePermiso("Gestionar"))
            {
                MessageBox.Show("No tienes permisos para registrar galpones.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new RegistrarGalponDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string sql = @"
                            INSERT INTO Galpones (Codigo, Nombre, Longitud, Raza, RacionPorAve)
                            VALUES (@codigo, @nombre, @longitud, @raza, @racion)";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@codigo", dialog.Codigo);
                            cmd.Parameters.AddWithValue("@nombre", dialog.Nombre);
                            cmd.Parameters.AddWithValue("@longitud", dialog.Longitud);
                            cmd.Parameters.AddWithValue("@raza", dialog.Raza);
                            cmd.Parameters.AddWithValue("@racion", dialog.RacionPorAve);

                            cmd.ExecuteNonQuery();
                        }
                    }

                    DatabaseHelper.RegistrarHistorial("Gallinas", "Registrar Galpón",
                        $"Galpón {dialog.Codigo} - {dialog.Nombre}",
                        AuthenticationService.UsuarioActual?.Id);

                    CargarGalpones();

                    MessageBox.Show($"✅ Galpón {dialog.Codigo} registrado exitosamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al registrar galpón: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEditarGalpon_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthenticationService.TienePermiso("Gestionar"))
            {
                MessageBox.Show("No tienes permisos para editar galpones.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var galpon = (sender as Button)?.Tag as GalponModel;
            if (galpon == null) return;

            var dialog = new RegistrarGalponDialog(galpon);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string sql = @"
                            UPDATE Galpones 
                            SET Nombre = @nombre, Longitud = @longitud, 
                                Raza = @raza, RacionPorAve = @racion
                            WHERE Id = @id";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@nombre", dialog.Nombre);
                            cmd.Parameters.AddWithValue("@longitud", dialog.Longitud);
                            cmd.Parameters.AddWithValue("@raza", dialog.Raza);
                            cmd.Parameters.AddWithValue("@racion", dialog.RacionPorAve);
                            cmd.Parameters.AddWithValue("@id", galpon.Id);

                            cmd.ExecuteNonQuery();
                        }
                    }

                    DatabaseHelper.RegistrarHistorial("Gallinas", "Editar Galpón",
                        $"Galpón {dialog.Codigo} actualizado",
                        AuthenticationService.UsuarioActual?.Id);

                    CargarGalpones();

                    MessageBox.Show("✅ Galpón actualizado exitosamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al actualizar galpón: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEliminarGalpon_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthenticationService.TienePermiso("Administrar"))
            {
                MessageBox.Show("Solo los administradores pueden eliminar galpones.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var galpon = (sender as Button)?.Tag as GalponModel;
            if (galpon == null) return;

            if (galpon.CantidadAves > 0)
            {
                MessageBox.Show("No se puede eliminar un galpón con gallinas asignadas.",
                    "Operación No Permitida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var resultado = MessageBox.Show(
                $"¿Estás seguro de eliminar el galpón {galpon.Codigo}?",
                "Confirmar Eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resultado == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string sql = "UPDATE Galpones SET Activo = 0 WHERE Id = @id";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", galpon.Id);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    DatabaseHelper.RegistrarHistorial("Gallinas", "Eliminar Galpón",
                        $"Galpón {galpon.Codigo} eliminado",
                        AuthenticationService.UsuarioActual?.Id);

                    CargarGalpones();

                    MessageBox.Show("✅ Galpón eliminado exitosamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar galpón: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Filtros

        private void CmbFiltroEstadoLote_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgLotesGallinas == null) return;

            var combo = sender as ComboBox;
            var selectedItem = combo?.SelectedItem as ComboBoxItem;
            string filtro = selectedItem?.Content.ToString();

            if (filtro == "Todos" || string.IsNullOrEmpty(filtro))
            {
                dgLotesGallinas.ItemsSource = lotesGallinas;
            }
            else
            {
                var filtrados = lotesGallinas.Where(l => l.Estado == filtro).ToList();
                dgLotesGallinas.ItemsSource = filtrados;
            }
        }

        #endregion
    }

    #region Modelos

    public class LoteGallinasModel
    {
        public int Id { get; set; }
        public string CodigoLote { get; set; }
        public int CantidadTotal { get; set; }
        public int CantidadAsignada { get; set; }
        public int CantidadPendiente { get; set; }
        public string Raza { get; set; }
        public int EdadSemanas { get; set; }
        public DateTime FechaLlegada { get; set; }
        public string Proveedor { get; set; }
        public string Observaciones { get; set; }
        public string Estado { get; set; }

        public Brush EstadoColor
        {
            get
            {
                return Estado switch
                {
                    "Completo" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    "Parcial" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    _ => new SolidColorBrush(Color.FromRgb(244, 67, 54))
                };
            }
        }
    }

    public class GalponModel
    {
        public int Id { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public double Longitud { get; set; }
        public string Raza { get; set; }
        public double RacionPorAve { get; set; }
        public int CantidadAves { get; set; }
    }

    public class AsignacionModel
    {
        public DateTime FechaAsignacion { get; set; }
        public string CodigoLote { get; set; }
        public string CodigoGalpon { get; set; }
        public int Cantidad { get; set; }
        public string Raza { get; set; }
        public string UsuarioNombre { get; set; }
    }

    #endregion
}