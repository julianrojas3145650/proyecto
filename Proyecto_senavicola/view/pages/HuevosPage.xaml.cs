using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Proyecto_senavicola.data;
using Proyecto_senavicola.services;
using Proyecto_senavicola.view.dialogs;

namespace Proyecto_senavicola.view.pages
{
    public partial class HuevosPage : System.Windows.Controls.Page
    {
        // ── Colecciones de datos ─────────────────────────────────────
        private ObservableCollection<HuevoInventario> inventario;
        private ObservableCollection<HuevoVendido> vendidos;
        private ObservableCollection<HuevoDanado> danados;
        private ObservableCollection<HistorialMovimiento> historial;
        private ObservableCollection<LoteAves> lotesAves;
        private ObservableCollection<ProduccionGalpon> produccionPorGalpon;

        // ── Estado de gramera ────────────────────────────────────────
        private bool grameraConectada = false;
        private double pesoActualGramera = 0.0;

        // ── Selección de galpón ──────────────────────────────────────
        private LoteAves loteSeleccionado;

        // ── Servicio de cámara y visión ──────────────────────────────
        private CameraVisionService _cameraService;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════

        public HuevosPage()
        {
            InitializeComponent();
            InicializarColecciones();

            // Crear el servicio inyectando los controles WPF que necesita
            _cameraService = new CameraVisionService(
                dispatcher: Dispatcher,
                borderCamera: borderCamera,
                statusCamera: statusCamera,
                btnActivarCamara: btnActivarCamara
                );

            _cameraService.InicializarCamara();

            // Suscribirse al evento de nuevo frame para actualizar el panel de visión
            _cameraService.NuevoFrame += (bi, resultado) => ActualizarPanelVision(resultado);

            this.Loaded += Page_Loaded;

            this.Loaded += (s, e) =>
            {
                var window = System.Windows.Window.GetWindow(this);
                if (window != null)
                    window.Closing += (ws, we) => _cameraService?.MarcarDisposing();
            };
            this.Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            navBar.SetModulo("🥚 Gestión de Huevos", "Control y gestión de la producción de huevos");

            CargarGalponesDesdeDB();
            CargarInventarioDesdeDB();
            CargarVendidosDesdeDB();
            CargarDanadosDesdeDB();
            CargarHistorialDesdeDB();

            ActualizarEstadisticas();
            ActualizarTotales();
            ActualizarProduccionPorGalpon();
        }

        // ════════════════════════════════════════════════════════════
        //  INICIALIZACIÓN
        // ════════════════════════════════════════════════════════════

        private void InicializarColecciones()
        {
            inventario = new ObservableCollection<HuevoInventario>();
            vendidos = new ObservableCollection<HuevoVendido>();
            danados = new ObservableCollection<HuevoDanado>();
            historial = new ObservableCollection<HistorialMovimiento>();
            lotesAves = new ObservableCollection<LoteAves>();
            produccionPorGalpon = new ObservableCollection<ProduccionGalpon>();

            dgInventario.ItemsSource = inventario;
            dgVendidos.ItemsSource = vendidos;
            dgDanados.ItemsSource = danados;
            dgHistorial.ItemsSource = historial;
            dgProduccionGalpon.ItemsSource = produccionPorGalpon;
        }

        // ════════════════════════════════════════════════════════════
        //  CARGA DESDE BASE DE DATOS
        // ════════════════════════════════════════════════════════════

        private void CargarGalponesDesdeDB()
        {
            lotesAves.Clear();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    using (var _pragma = new SQLiteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;", conn)) _pragma.ExecuteNonQuery();
                    string sql = "SELECT Id, Codigo, Nombre, Raza, CantidadAves FROM Galpones WHERE Activo = 1 ORDER BY Codigo";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lotesAves.Add(new LoteAves
                            {
                                Id = reader.GetInt32(0),
                                Codigo = reader.GetString(1),
                                Raza = reader.GetString(3),
                                CantidadAves = reader.GetInt32(4),
                                EdadSemanas = 0,
                                FechaIngreso = DateTime.Now,
                                Estado = "Activo"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar galpones: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            cmbLoteAves.Items.Clear();
            cmbLoteAves.Items.Add(new ComboBoxItem
            {
                Content = "Seleccionar galpón...",
                IsSelected = true,
                IsEnabled = false
            });
            foreach (var g in lotesAves)
            {
                cmbLoteAves.Items.Add(new ComboBoxItem
                {
                    Content = $"{g.Codigo} - {g.Raza} ({g.CantidadAves} aves)",
                    Tag = g
                });
            }

            if (lotesAves.Count == 0)
                MessageBox.Show(
                    "No hay galpones registrados.\n\nVe al módulo de Gestión de Gallinas para crear galpones primero.",
                    "Sin Galpones", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CargarInventarioDesdeDB()
        {
            inventario.Clear();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    using (var _pragma = new SQLiteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;", conn)) _pragma.ExecuteNonQuery();
                    string sql = @"
                        SELECT h.Id, h.LoteHuevos, h.GalponId, h.Tipo, h.Cantidad,
                               h.PesoPromedio, h.FechaRecoleccion, g.Codigo
                        FROM Huevos h
                        LEFT JOIN Galpones g ON h.GalponId = g.Id
                        WHERE h.Estado = 'Disponible'
                        ORDER BY h.FechaRecoleccion DESC";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            inventario.Add(new HuevoInventario
                            {
                                Id = reader.GetInt32(0),
                                Lote = reader.GetString(1),
                                LoteAvesId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                Tipo = reader.GetString(3),
                                Cantidad = reader.GetInt32(4),
                                PesoPromedio = reader.GetDouble(5),
                                FechaIngreso = reader.GetDateTime(6),
                                LoteAvesCodigo = reader.IsDBNull(7) ? "" : reader.GetString(7)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar inventario: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CargarVendidosDesdeDB()
        {
            vendidos.Clear();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    using (var _pragma = new SQLiteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;", conn)) _pragma.ExecuteNonQuery();
                    string sql = @"
                        SELECT v.Id, h.Tipo, v.Cantidad, v.PrecioUnitario, v.Total,
                               v.FechaVenta, v.Cliente, g.Codigo
                        FROM VentasHuevos v
                        INNER JOIN Huevos h ON v.HuevoId = h.Id
                        LEFT JOIN Galpones g ON h.GalponId = g.Id
                        ORDER BY v.FechaVenta DESC";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            vendidos.Add(new HuevoVendido
                            {
                                Id = reader.GetInt32(0),
                                Tipo = reader.GetString(1),
                                Cantidad = reader.GetInt32(2),
                                PrecioUnitario = reader.GetDouble(3),
                                Total = reader.GetDouble(4),
                                FechaVenta = reader.GetDateTime(5),
                                Cliente = reader.GetString(6),
                                LoteAvesCodigo = reader.IsDBNull(7) ? "" : reader.GetString(7)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar ventas: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CargarDanadosDesdeDB()
        {
            danados.Clear();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    using (var _pragma = new SQLiteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;", conn)) _pragma.ExecuteNonQuery();
                    string sql = @"
                        SELECT d.Id, h.Tipo, d.Cantidad, d.Motivo,
                               d.FechaRegistro, d.Observaciones, g.Codigo
                        FROM HuevosDanados d
                        INNER JOIN Huevos h ON d.HuevoId = h.Id
                        LEFT JOIN Galpones g ON h.GalponId = g.Id
                        ORDER BY d.FechaRegistro DESC";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            danados.Add(new HuevoDanado
                            {
                                Id = reader.GetInt32(0),
                                Tipo = reader.GetString(1),
                                Cantidad = reader.GetInt32(2),
                                Motivo = reader.GetString(3),
                                FechaRegistro = reader.GetDateTime(4),
                                Observaciones = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                LoteAvesCodigo = reader.IsDBNull(6) ? "" : reader.GetString(6)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar dañados: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CargarHistorialDesdeDB()
        {
            historial.Clear();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    using (var _pragma = new SQLiteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;", conn)) _pragma.ExecuteNonQuery();
                    string sql = @"
                        SELECT h.Id, h.FechaHora, h.Accion, h.Detalle,
                               u.Nombre || ' ' || u.Apellido
                        FROM Historial h
                        LEFT JOIN Usuarios u ON h.UsuarioId = u.Id
                        WHERE h.Modulo = 'Huevos'
                        ORDER BY h.FechaHora DESC LIMIT 500";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string detalle = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            historial.Add(new HistorialMovimiento
                            {
                                Id = reader.GetInt32(0),
                                Fecha = reader.GetDateTime(1),
                                Accion = reader.GetString(2),
                                Tipo = ExtraerDelDetalle(detalle, "Tipo"),
                                Cantidad = int.TryParse(ExtraerDelDetalle(detalle, "Cantidad"), out int q) ? q : 0,
                                Usuario = reader.IsDBNull(4) ? "Sistema" : reader.GetString(4),
                                Detalles = detalle
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando historial: {ex.Message}");
            }
        }

        private string ExtraerDelDetalle(string detalle, string clave)
        {
            if (string.IsNullOrEmpty(detalle)) return "";
            foreach (var parte in detalle.Split('|'))
            {
                var kv = parte.Split(':');
                if (kv.Length >= 2 && kv[0].Trim() == clave)
                    return kv[1].Trim();
            }
            return "";
        }

        // ════════════════════════════════════════════════════════════
        //  PERSISTENCIA EN DB
        // ════════════════════════════════════════════════════════════

        private static SQLiteConnection AbrirConexionSegura()
        {
            var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using (var p = new SQLiteCommand(
                "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;", conn))
                p.ExecuteNonQuery();
            return conn;
        }

        private int InsertarHuevoDB(string loteHuevos, int galponId, string tipo,
                                    int cantidad, double pesoPromedio, string metodo = "Manual")
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using (var _pragma = new SQLiteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;", conn)) _pragma.ExecuteNonQuery();
                string sql = @"
                    INSERT INTO Huevos
                        (LoteHuevos, GalponId, Tipo, Cantidad, PesoPromedio,
                         FechaRecoleccion, MetodoClasificacion, Estado, UsuarioId)
                    VALUES
                        (@lote, @galpon, @tipo, @cantidad, @peso,
                         @fecha, @metodo, 'Disponible', @usuario);
                    SELECT last_insert_rowid();";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@lote", loteHuevos);
                    cmd.Parameters.AddWithValue("@galpon", galponId > 0 ? (object)galponId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipo", tipo);
                    cmd.Parameters.AddWithValue("@cantidad", cantidad);
                    cmd.Parameters.AddWithValue("@peso", pesoPromedio);
                    cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                    cmd.Parameters.AddWithValue("@metodo", metodo);
                    cmd.Parameters.AddWithValue("@usuario",
                        AuthenticationService.UsuarioActual?.Id > 0
                            ? (object)AuthenticationService.UsuarioActual.Id : DBNull.Value);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        private void ActualizarHuevoDB(int id, int cantidad, double pesoPromedio)
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using (var _pragma = new SQLiteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;", conn)) _pragma.ExecuteNonQuery();
                string sql = "UPDATE Huevos SET Cantidad = @cantidad, PesoPromedio = @peso WHERE Id = @id";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cantidad", cantidad);
                    cmd.Parameters.AddWithValue("@peso", pesoPromedio);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void ReducirOEliminarHuevoDB(int id, int nuevaCantidad, string nuevoEstado)
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using (var _pragma = new SQLiteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;", conn)) _pragma.ExecuteNonQuery();
                string sql = "UPDATE Huevos SET Cantidad = @cantidad, Estado = @estado WHERE Id = @id";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cantidad", nuevaCantidad);
                    cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void RegistrarVentaDB(int huevoId, string cliente, int cantidad,
                                      double precioUnitario, double total)
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using (var _pragma = new SQLiteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;", conn)) _pragma.ExecuteNonQuery();
                string sql = @"
                    INSERT INTO VentasHuevos
                        (HuevoId, Cliente, Cantidad, PrecioUnitario, Total, FechaVenta, UsuarioId)
                    VALUES
                        (@huevo, @cliente, @cantidad, @precio, @total, @fecha, @usuario)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@huevo", huevoId);
                    cmd.Parameters.AddWithValue("@cliente", cliente);
                    cmd.Parameters.AddWithValue("@cantidad", cantidad);
                    cmd.Parameters.AddWithValue("@precio", precioUnitario);
                    cmd.Parameters.AddWithValue("@total", total);
                    cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                    cmd.Parameters.AddWithValue("@usuario",
                        AuthenticationService.UsuarioActual?.Id > 0
                            ? (object)AuthenticationService.UsuarioActual.Id : DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void RegistrarDanadoDB(int huevoId, int cantidad, string motivo, string observaciones)
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using (var _pragma = new SQLiteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;", conn)) _pragma.ExecuteNonQuery();
                string sql = @"
                    INSERT INTO HuevosDanados
                        (HuevoId, Cantidad, Motivo, FechaRegistro, Observaciones, UsuarioId)
                    VALUES
                        (@huevo, @cantidad, @motivo, @fecha, @obs, @usuario)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@huevo", huevoId);
                    cmd.Parameters.AddWithValue("@cantidad", cantidad);
                    cmd.Parameters.AddWithValue("@motivo", motivo);
                    cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                    cmd.Parameters.AddWithValue("@obs", observaciones ?? "");
                    cmd.Parameters.AddWithValue("@usuario",
                        AuthenticationService.UsuarioActual?.Id > 0
                            ? (object)AuthenticationService.UsuarioActual.Id : DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ════════════════════════════════════════════════════════════
        //  SELECCIÓN DE GALPÓN
        // ════════════════════════════════════════════════════════════

        private void CmbLoteAves_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = cmbLoteAves.SelectedItem as ComboBoxItem;
            if (item?.Tag is LoteAves galpon)
            {
                loteSeleccionado = galpon;
                if (borderInfoLote != null && txtInfoLote != null)
                {
                    borderInfoLote.Visibility = Visibility.Visible;
                    txtInfoLote.Text = $"Galpón: {galpon.Codigo} | Raza: {galpon.Raza} | Aves: {galpon.CantidadAves}";
                }
            }
            else
            {
                loteSeleccionado = null;
                if (borderInfoLote != null)
                    borderInfoLote.Visibility = Visibility.Collapsed;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  CLASIFICACIÓN MANUAL
        // ════════════════════════════════════════════════════════════

        private void AgregarHuevo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (loteSeleccionado == null)
                {
                    MessageBox.Show("Por favor, selecciona un galpón antes de agregar huevos.",
                        "Galpón Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    cmbLoteAves.Focus();
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtCantidad.Text) || string.IsNullOrWhiteSpace(txtPeso.Text))
                {
                    MessageBox.Show("Por favor, completa todos los campos.", "Campos Requeridos",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!int.TryParse(txtCantidad.Text, out int cantidad) || cantidad <= 0)
                {
                    MessageBox.Show("La cantidad debe ser un entero mayor a cero.", "Valor Inválido",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!double.TryParse(txtPeso.Text, out double peso) || peso <= 0)
                {
                    MessageBox.Show("El peso debe ser un número mayor a cero.", "Valor Inválido",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string tipo = ClasificarPorPeso(peso);
                string loteHuevos = $"H{DateTime.Now:yyyyMMddHHmmss}";

                var existente = inventario.FirstOrDefault(h =>
                    h.Tipo == tipo &&
                    h.LoteAvesId == loteSeleccionado.Id &&
                    h.FechaIngreso.Date == DateTime.Today);

                if (existente != null)
                {
                    double pesoTotal = existente.PesoPromedio * existente.Cantidad;
                    int nuevaCant = existente.Cantidad + cantidad;
                    double nuevoPeso = (pesoTotal + (peso * cantidad)) / nuevaCant;
                    ActualizarHuevoDB(existente.Id, nuevaCant, nuevoPeso);
                    existente.Cantidad = nuevaCant;
                    existente.PesoPromedio = nuevoPeso;
                }
                else
                {
                    int nuevoId = InsertarHuevoDB(loteHuevos, loteSeleccionado.Id, tipo, cantidad, peso, "Manual");
                    inventario.Add(new HuevoInventario
                    {
                        Id = nuevoId,
                        Tipo = tipo,
                        Cantidad = cantidad,
                        PesoPromedio = peso,
                        FechaIngreso = DateTime.Now,
                        Lote = loteHuevos,
                        LoteAvesId = loteSeleccionado.Id,
                        LoteAvesCodigo = loteSeleccionado.Codigo
                    });
                }

                string det = $"Tipo:{tipo}|Cantidad:{cantidad}|Peso:{peso:F1}g|Galpón:{loteSeleccionado.Codigo}|Lote:{loteHuevos}";
                DatabaseHelper.RegistrarHistorial("Huevos", "Ingreso Manual", det,
                    AuthenticationService.UsuarioActual?.Id);
                RegistrarHistorialEnMemoria("Ingreso Manual", tipo, cantidad, det);

                txtCantidad.Text = "1";
                txtPeso.Text = "";
                txtTipoDetectado.Text = "";

                ActualizarEstadisticas();
                ActualizarTotales();
                ActualizarProduccionPorGalpon();

                MessageBox.Show(
                    $"✅ {cantidad} huevo(s) tipo {tipo} registrado(s).\n\nGalpón: {loteSeleccionado.Codigo}\nLote: {loteHuevos}",
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al agregar huevos: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtPeso_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(txtPeso.Text, out double peso) && peso > 0)
                txtTipoDetectado.Text = ClasificarPorPeso(peso);
            else
                txtTipoDetectado.Text = "";
        }

        private string ClasificarPorPeso(double peso)
        {
            if (peso > 78) return "Jumbo";
            if (peso >= 67) return "AAA";
            if (peso >= 60) return "AA";
            if (peso >= 53) return "A";
            if (peso >= 46) return "B";
            return "C";
        }

        // ════════════════════════════════════════════════════════════
        //  CLASIFICACIÓN AUTOMÁTICA (usa el servicio de visión)
        // ════════════════════════════════════════════════════════════

        private System.Windows.Threading.DispatcherTimer _timerDeteccion;

        private void IniciarDeteccion_Click(object sender, RoutedEventArgs e)
        {
            if (!_cameraService.CamaraActiva)
            {
                MessageBox.Show("Debe activar la cámara primero.",
                    "Cámara Requerida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (loteSeleccionado == null)
            {
                MessageBox.Show("Por favor, selecciona un galpón primero.",
                    "Galpón Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_timerDeteccion == null)
            {
                _timerDeteccion = new System.Windows.Threading.DispatcherTimer();
                _timerDeteccion.Interval = TimeSpan.FromMilliseconds(1000);
                _timerDeteccion.Tick += TimerDeteccion_Tick;
            }
            _timerDeteccion.Start();
        }

        private void TimerDeteccion_Tick(object sender, EventArgs e)
        {
            _timerDeteccion.Stop();

            var r = _cameraService.UltimoResultado;
            if (r != null && r.HuevoDetectado && r.PesoConfirmado)
                AgregarHuevoAutomatico(r.PesoEstable);
        }

        private void AgregarHuevoAutomatico(double peso)
        {
            string tipo = ClasificarPorPeso(peso);
            string loteHuevos = $"H{DateTime.Now:yyyyMMddHHmmss}";

            var existente = inventario.FirstOrDefault(h =>
                h.Tipo == tipo &&
                h.LoteAvesId == loteSeleccionado.Id &&
                h.FechaIngreso.Date == DateTime.Today);

            if (existente != null)
            {
                double pesoTotal = existente.PesoPromedio * existente.Cantidad;
                int nuevaCant = existente.Cantidad + 1;
                double nuevoPeso = (pesoTotal + peso) / nuevaCant;
                ActualizarHuevoDB(existente.Id, nuevaCant, nuevoPeso);
                existente.Cantidad = nuevaCant;
                existente.PesoPromedio = nuevoPeso;
            }
            else
            {
                int nuevoId = InsertarHuevoDB(loteHuevos, loteSeleccionado.Id, tipo, 1, peso, "Automatico");
                inventario.Add(new HuevoInventario
                {
                    Id = nuevoId,
                    Tipo = tipo,
                    Cantidad = 1,
                    PesoPromedio = peso,
                    FechaIngreso = DateTime.Now,
                    Lote = loteHuevos,
                    LoteAvesId = loteSeleccionado.Id,
                    LoteAvesCodigo = loteSeleccionado.Codigo
                });
            }

            var rv = _cameraService.UltimoResultado;
            string volInfo = (rv != null && rv.HuevoDetectado)
                ? $"|Diámetro:{rv.DiametroCm:F2}cm|Largo:{rv.LargoCm:F2}cm|Volumen:{rv.VolumenCm3:F3}cm³|Escala:{rv.ModoEscala}"
                : "";
            string det = $"Tipo:{tipo}|Cantidad:1|Peso:{peso:F2}g|Galpón:{loteSeleccionado.Codigo}|Detección:IA{volInfo}";
            DatabaseHelper.RegistrarHistorial("Huevos", "Ingreso Automático", det,
                AuthenticationService.UsuarioActual?.Id);
            RegistrarHistorialEnMemoria("Ingreso Automático", tipo, 1, det);

            ActualizarEstadisticas();
            ActualizarTotales();
            ActualizarProduccionPorGalpon();

            string msgVol = (rv != null && rv.HuevoDetectado)
                ? $"\nVolumen: {rv.VolumenCm3:F3} cm³\nDiámetro: {rv.DiametroCm:F2} cm | Largo: {rv.LargoCm:F2} cm\nEscala: {rv.ModoEscala}"
                : "";
            MessageBox.Show(
                $"✅ Huevo tipo {tipo} detectado y registrado.\nPeso: {peso:F1}g\nGalpón: {loteSeleccionado.Codigo}{msgVol}",
                "Detección Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ════════════════════════════════════════════════════════════
        //  CRUD INVENTARIO
        // ════════════════════════════════════════════════════════════

        private void EditarHuevo_Click(object sender, RoutedEventArgs e)
        {
            var huevo = (sender as Button)?.Tag as HuevoInventario;
            if (huevo == null) return;

            var dialog = new view.dialogs.EditarHuevoDialog(huevo);
            if (dialog.ShowDialog() == true)
            {
                var item = inventario.FirstOrDefault(h => h.Id == huevo.Id);
                if (item != null)
                {
                    ActualizarHuevoDB(item.Id, dialog.Cantidad, dialog.Peso);
                    item.Cantidad = dialog.Cantidad;
                    item.PesoPromedio = dialog.Peso;
                    dgInventario.Items.Refresh();

                    string det = $"Tipo:{item.Tipo}|Cantidad:{item.Cantidad}|Peso:{item.PesoPromedio:F2}g|Galpón:{item.LoteAvesCodigo}";
                    DatabaseHelper.RegistrarHistorial("Huevos", "Edición", det, AuthenticationService.UsuarioActual?.Id);
                    RegistrarHistorialEnMemoria("Edición", item.Tipo, item.Cantidad, det);

                    ActualizarEstadisticas();
                    ActualizarTotales();
                    ActualizarProduccionPorGalpon();
                    MessageBox.Show("✅ Huevo actualizado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void EliminarHuevo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var huevo = (sender as Button)?.Tag as HuevoInventario;
                if (huevo == null || !inventario.Contains(huevo))
                {
                    MessageBox.Show("No se pudo obtener la información del huevo.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dialog = new view.dialogs.MotivoEliminacionDialog(huevo);
                dialog.Owner = System.Windows.Window.GetWindow(this);

                if (dialog.ShowDialog() == true && dialog.Confirmado)
                {
                    int cantidadElim = dialog.Cantidad;
                    int nuevaCant = huevo.Cantidad - cantidadElim;

                    if (dialog.EsVendido)
                    {
                        double total = (double)(cantidadElim * dialog.PrecioUnitario);
                        RegistrarVentaDB(huevo.Id, dialog.Cliente, cantidadElim, (double)dialog.PrecioUnitario, total);
                        vendidos.Add(new HuevoVendido
                        {
                            Id = vendidos.Count > 0 ? vendidos.Max(v => v.Id) + 1 : 1,
                            Tipo = huevo.Tipo,
                            Cantidad = cantidadElim,
                            PrecioUnitario = (double)dialog.PrecioUnitario,
                            Total = total,
                            FechaVenta = DateTime.Now,
                            Cliente = dialog.Cliente,
                            LoteAvesCodigo = huevo.LoteAvesCodigo
                        });
                        string det = $"Tipo:{huevo.Tipo}|Cantidad:{cantidadElim}|Cliente:{dialog.Cliente}|Precio:{dialog.PrecioUnitario:F2}|Galpón:{huevo.LoteAvesCodigo}";
                        DatabaseHelper.RegistrarHistorial("Huevos", "Venta", det, AuthenticationService.UsuarioActual?.Id);
                        RegistrarHistorialEnMemoria("Venta", huevo.Tipo, cantidadElim, det);
                    }
                    else
                    {
                        RegistrarDanadoDB(huevo.Id, cantidadElim, dialog.MotivoDano, dialog.Observaciones);
                        danados.Add(new HuevoDanado
                        {
                            Id = danados.Count > 0 ? danados.Max(d => d.Id) + 1 : 1,
                            Tipo = huevo.Tipo,
                            Cantidad = cantidadElim,
                            Motivo = dialog.MotivoDano,
                            FechaRegistro = DateTime.Now,
                            Observaciones = dialog.Observaciones ?? "",
                            LoteAvesCodigo = huevo.LoteAvesCodigo
                        });
                        string det = $"Tipo:{huevo.Tipo}|Cantidad:{cantidadElim}|Motivo:{dialog.MotivoDano}|Galpón:{huevo.LoteAvesCodigo}";
                        DatabaseHelper.RegistrarHistorial("Huevos", "Daño", det, AuthenticationService.UsuarioActual?.Id);
                        RegistrarHistorialEnMemoria("Daño", huevo.Tipo, cantidadElim, det);
                    }

                    if (nuevaCant <= 0)
                    {
                        string estado = dialog.EsVendido ? "Vendido" : "Dañado";
                        ReducirOEliminarHuevoDB(huevo.Id, 0, estado);
                        inventario.Remove(huevo);
                        string accion = dialog.EsVendido ? "vendidos" : "marcados como dañados";
                        MessageBox.Show(
                            $"✅ {cantidadElim} huevos {huevo.Tipo} {accion}.\nGalpón: {huevo.LoteAvesCodigo}",
                            "Registro Eliminado", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ReducirOEliminarHuevoDB(huevo.Id, nuevaCant, "Disponible");
                        huevo.Cantidad = nuevaCant;
                        dgInventario.Items.Refresh();
                        string accion = dialog.EsVendido ? "vendidos" : "marcados como dañados";
                        MessageBox.Show(
                            $"✅ {cantidadElim} huevos {accion}. Quedan {nuevaCant} en inventario.\nGalpón: {huevo.LoteAvesCodigo}",
                            "Cantidad Actualizada", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    ActualizarEstadisticas();
                    ActualizarTotales();
                    ActualizarProduccionPorGalpon();
                    dgInventario.Items.Refresh();
                    dgVendidos.Items.Refresh();
                    dgDanados.Items.Refresh();
                    dgHistorial.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error:\n{ex.Message}\n{ex.StackTrace}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  ESTADÍSTICAS
        // ════════════════════════════════════════════════════════════

        private void ActualizarEstadisticas()
        {
            txtTotalInventario.Text = inventario.Sum(h => h.Cantidad).ToString();
            txtVendidosHoy.Text = vendidos.Where(v => v.FechaVenta.Date == DateTime.Today)
                                              .Sum(v => v.Cantidad).ToString();
            txtDanados.Text = danados.Sum(d => d.Cantidad).ToString();

            if (inventario.Any())
            {
                txtTipoComun.Text = inventario.GroupBy(h => h.Tipo)
                    .OrderByDescending(g => g.Sum(h => h.Cantidad)).First().Key;
                txtPesoPromedio.Text = $"{inventario.Average(h => h.PesoPromedio):F1}g";
            }
        }

        private void ActualizarTotales()
        {
            txtTotalJumbo.Text = $"Jumbo: {inventario.Where(h => h.Tipo == "Jumbo").Sum(h => h.Cantidad)}";
            txtTotalAAA.Text = $"AAA: {inventario.Where(h => h.Tipo == "AAA").Sum(h => h.Cantidad)}";
            txtTotalAA.Text = $"AA: {inventario.Where(h => h.Tipo == "AA").Sum(h => h.Cantidad)}";
            txtTotalA.Text = $"A: {inventario.Where(h => h.Tipo == "A").Sum(h => h.Cantidad)}";
            txtTotalB.Text = $"B: {inventario.Where(h => h.Tipo == "B").Sum(h => h.Cantidad)}";
            txtTotalC.Text = $"C: {inventario.Where(h => h.Tipo == "C").Sum(h => h.Cantidad)}";

            int tGeneral = inventario.Sum(h => h.Cantidad);
            txtTotalGeneral.Text = $"Total General: {tGeneral} huevos";

            int tVCant = vendidos.Sum(v => v.Cantidad);
            double tVValor = vendidos.Sum(v => v.Total);
            txtTotalVendidosCantidad.Text = $"{tVCant} huevos";
            txtTotalVendidosValor.Text = $"Total: ${tVValor:F2}";

            int tDCant = danados.Sum(d => d.Cantidad);
            txtTotalDanados.Text = $"{tDCant} huevos";

            int tTotal = tGeneral + tVCant + tDCant;
            double pct = tTotal > 0 ? tDCant * 100.0 / tTotal : 0;
            txtPorcentajeDanados.Text = $"({pct:F1}% del total)";
        }

        private void ActualizarProduccionPorGalpon()
        {
            produccionPorGalpon.Clear();
            var todos = inventario.Select(h => new
            {
                h.LoteAvesId,
                h.LoteAvesCodigo,
                h.Tipo,
                h.Cantidad,
                Peso = h.PesoPromedio
            })
                .Concat(vendidos.Select(v => new
                {
                    LoteAvesId = lotesAves.FirstOrDefault(l => l.Codigo == v.LoteAvesCodigo)?.Id ?? 0,
                    LoteAvesCodigo = v.LoteAvesCodigo,
                    v.Tipo,
                    v.Cantidad,
                    Peso = 0.0
                }))
                .Concat(danados.Select(d => new
                {
                    LoteAvesId = lotesAves.FirstOrDefault(l => l.Codigo == d.LoteAvesCodigo)?.Id ?? 0,
                    LoteAvesCodigo = d.LoteAvesCodigo,
                    d.Tipo,
                    d.Cantidad,
                    Peso = 0.0
                }));

            foreach (var g in todos.GroupBy(h => h.LoteAvesCodigo)
                                    .OrderByDescending(g => g.Sum(h => h.Cantidad)))
            {
                var lote = lotesAves.FirstOrDefault(l => l.Codigo == g.Key);
                produccionPorGalpon.Add(new ProduccionGalpon
                {
                    CodigoLote = g.Key,
                    Raza = lote?.Raza ?? "Desconocido",
                    TotalHuevos = g.Sum(h => h.Cantidad),
                    CantidadJumbo = g.Where(h => h.Tipo == "Jumbo").Sum(h => h.Cantidad),
                    CantidadAAA = g.Where(h => h.Tipo == "AAA").Sum(h => h.Cantidad),
                    CantidadAA = g.Where(h => h.Tipo == "AA").Sum(h => h.Cantidad),
                    CantidadA = g.Where(h => h.Tipo == "A").Sum(h => h.Cantidad),
                    CantidadB = g.Where(h => h.Tipo == "B").Sum(h => h.Cantidad),
                    CantidadC = g.Where(h => h.Tipo == "C").Sum(h => h.Cantidad),
                    PesoPromedio = g.Where(h => h.Peso > 0).Any()
                                    ? g.Where(h => h.Peso > 0).Average(h => h.Peso) : 0
                });
            }
        }

        // ════════════════════════════════════════════════════════════
        //  HISTORIAL EN MEMORIA
        // ════════════════════════════════════════════════════════════

        private void RegistrarHistorialEnMemoria(string accion, string tipo, int cantidad, string detalles)
        {
            historial.Insert(0, new HistorialMovimiento
            {
                Id = historial.Count > 0 ? historial.Max(h => h.Id) + 1 : 1,
                Fecha = DateTime.Now,
                Accion = accion,
                Tipo = tipo,
                Cantidad = cantidad,
                Usuario = AuthenticationService.UsuarioActual?.Nombre ?? "Sistema",
                Detalles = detalles
            });
            while (historial.Count > 500)
                historial.RemoveAt(historial.Count - 1);
        }

        // ════════════════════════════════════════════════════════════
        //  FILTROS
        // ════════════════════════════════════════════════════════════

        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltrosInventario();
        private void CmbFiltro_SelectionChanged(object sender, SelectionChangedEventArgs e) => AplicarFiltrosInventario();
        private void DpFiltroFecha_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => AplicarFiltrosInventario();

        private void AplicarFiltrosInventario()
        {
            if (dgInventario == null) return;
            var vista = inventario.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(txtBuscarInventario?.Text))
            {
                string b = txtBuscarInventario.Text.ToLower();
                vista = vista.Where(h =>
                    h.Tipo.ToLower().Contains(b) ||
                    h.Lote.ToLower().Contains(b) ||
                    h.LoteAvesCodigo.ToLower().Contains(b));
            }
            if (cmbFiltroTipo?.SelectedIndex > 0)
            {
                string tipo = (cmbFiltroTipo.SelectedItem as ComboBoxItem)?.Content.ToString();
                vista = vista.Where(h => h.Tipo == tipo);
            }
            if (dpFiltroFecha?.SelectedDate.HasValue == true)
            {
                var fecha = dpFiltroFecha.SelectedDate.Value;
                vista = vista.Where(h => h.FechaIngreso.Date == fecha.Date);
            }
            dgInventario.ItemsSource = vista.ToList();
        }

        private void TxtBuscarVendidos_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (dgVendidos == null) return;
            var vista = vendidos.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(txtBuscarVendidos?.Text))
            {
                string b = txtBuscarVendidos.Text.ToLower();
                vista = vista.Where(v =>
                    v.Tipo.ToLower().Contains(b) ||
                    v.Cliente.ToLower().Contains(b) ||
                    v.LoteAvesCodigo.ToLower().Contains(b));
            }
            dgVendidos.ItemsSource = vista.ToList();
        }

        private void TxtBuscarDanados_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (dgDanados == null) return;
            var vista = danados.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(txtBuscarDanados?.Text))
            {
                string b = txtBuscarDanados.Text.ToLower();
                vista = vista.Where(d =>
                    d.Tipo.ToLower().Contains(b) ||
                    d.Motivo.ToLower().Contains(b) ||
                    d.LoteAvesCodigo.ToLower().Contains(b));
            }
            dgDanados.ItemsSource = vista.ToList();
        }

        // ════════════════════════════════════════════════════════════
        //  EVENTOS DE CÁMARA — delegan al CameraVisionService
        // ════════════════════════════════════════════════════════════

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _cameraService?.Dispose();
                _cameraService = null;
            }
            catch { }
        }

        private void ActivarCamara_Click(object sender, RoutedEventArgs e)
        {
            if (!_cameraService.CamaraActiva)
                _cameraService.IniciarCamara(System.Windows.Window.GetWindow(this));
            else
                _cameraService.DetenerCamara();
        }

        // ════════════════════════════════════════════════════════════
        //  ACTUALIZAR PANEL VISIÓN (llamado por el evento NuevoFrame)
        // ════════════════════════════════════════════════════════════

        private void ActualizarPanelVision(ResultadoAnalisis r)
        {
            if (r == null) return;

            try { if (statusCV != null) statusCV.Fill = new SolidColorBrush(r.HuevoDetectado ? Color.FromRgb(76, 175, 80) : Color.FromRgb(220, 53, 69)); } catch { }
            try
            {
                if (txtEstadoCV != null)
                {
                    txtEstadoCV.Text = r.HuevoDetectado ? "⬤  Detectado" : "⬤  Sin huevo";
                    txtEstadoCV.Foreground = new SolidColorBrush(r.HuevoDetectado ? Color.FromRgb(76, 175, 80) : Color.FromRgb(255, 107, 107));
                }
            }
            catch { }

            try { if (txtVisionDiametro != null) txtVisionDiametro.Text = r.HuevoDetectado ? $"{r.DiametroCm:F2} cm" : "-- cm"; } catch { }
            try { if (txtVisionLargo != null) txtVisionLargo.Text = r.HuevoDetectado ? $"{r.LargoCm:F2} cm" : "-- cm"; } catch { }
            try { if (txtVisionVolumen != null) txtVisionVolumen.Text = r.HuevoDetectado ? $"{r.VolumenCm3:F3} cm³" : "-- cm³"; } catch { }
            try { if (txtVisionEscala != null) txtVisionEscala.Text = $"{r.PxPorCm:F1} px/cm — {r.ModoEscala}"; } catch { }
            try { if (txtVisionCuadros != null) txtVisionCuadros.Text = r.HuevoDetectado ? $"{r.CuadrosDetect} mm" : "-- mm"; } catch { }
            try
            {
                if (txtVisionOCR != null)
                    txtVisionOCR.Text = string.IsNullOrWhiteSpace(r.TextoOCR)
                        ? "Sin texto detectado"
                        : r.TextoOCR.Replace("\n", " ").Trim();
            }
            catch { }

            try { if (txtResumenVolumen != null) txtResumenVolumen.Text = r.HuevoDetectado ? $"{r.VolumenCm3:F3} cm³" : "--"; } catch { }
            try { if (txtResumenDiametro != null) txtResumenDiametro.Text = r.HuevoDetectado ? $"{r.DiametroCm:F2} cm" : "--"; } catch { }

            // Si la gramera no está conectada, intentar leer el peso por OCR
            if (!grameraConectada && r != null)
            {
                string textoOcr = r.TextoOCR ?? "";
                if (!string.IsNullOrWhiteSpace(textoOcr))
                {
                    string limpio = textoOcr.Trim();
                    if (double.TryParse(limpio,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double pesoLeido))
                    {
                        if (pesoLeido > 120 && pesoLeido < 1200) pesoLeido /= 10.0;

                        if (pesoLeido >= 30 && pesoLeido <= 120)
                        {
                            pesoActualGramera = pesoLeido;
                            try
                            {
                                if (txtLecturaGramera != null)
                                    txtLecturaGramera.Text = $"{pesoLeido:F1} g  📷";

                                string tipo = ClasificarPorPeso(pesoLeido);
                                if (txtTipoAutomatico != null)
                                {
                                    txtTipoAutomatico.Text = tipo;
                                    if (tipo == "Jumbo")
                                        txtTipoAutomatico.Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                                    else if (tipo == "AAA" || tipo == "AA")
                                        txtTipoAutomatico.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                                    else if (tipo == "A")
                                        txtTipoAutomatico.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                                    else
                                        txtTipoAutomatico.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        // ════════════════════════════════════════════════════════════
        //  UTILIDADES UI
        // ════════════════════════════════════════════════════════════

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, @"^[0-9.]+$");
        }
    }

    // ================================================================
    //  MODELOS
    // ================================================================

    public class HuevoInventario : INotifyPropertyChanged
    {
        private int id; private string tipo; private int cantidad;
        private double pesoPromedio; private DateTime fechaIngreso;
        private string lote; private int loteAvesId; private string loteAvesCodigo;

        public int Id { get => id; set { id = value; OnPropertyChanged(nameof(Id)); } }
        public string Tipo { get => tipo; set { tipo = value; OnPropertyChanged(nameof(Tipo)); } }
        public int Cantidad { get => cantidad; set { cantidad = value; OnPropertyChanged(nameof(Cantidad)); } }
        public double PesoPromedio { get => pesoPromedio; set { pesoPromedio = value; OnPropertyChanged(nameof(PesoPromedio)); } }
        public DateTime FechaIngreso { get => fechaIngreso; set { fechaIngreso = value; OnPropertyChanged(nameof(FechaIngreso)); } }
        public string Lote { get => lote; set { lote = value; OnPropertyChanged(nameof(Lote)); } }
        public int LoteAvesId { get => loteAvesId; set { loteAvesId = value; OnPropertyChanged(nameof(LoteAvesId)); } }
        public string LoteAvesCodigo { get => loteAvesCodigo; set { loteAvesCodigo = value; OnPropertyChanged(nameof(LoteAvesCodigo)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public class HuevoVendido
    {
        public int Id { get; set; }
        public string Tipo { get; set; }
        public int Cantidad { get; set; }
        public double PrecioUnitario { get; set; }
        public double Total { get; set; }
        public DateTime FechaVenta { get; set; }
        public string Cliente { get; set; }
        public string LoteAvesCodigo { get; set; }
    }

    public class HuevoDanado
    {
        public int Id { get; set; }
        public string Tipo { get; set; }
        public int Cantidad { get; set; }
        public string Motivo { get; set; }
        public DateTime FechaRegistro { get; set; }
        public string Observaciones { get; set; }
        public string LoteAvesCodigo { get; set; }
    }

    public class HistorialMovimiento
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string Accion { get; set; }
        public string Tipo { get; set; }
        public int Cantidad { get; set; }
        public string Usuario { get; set; }
        public string Detalles { get; set; }
    }

    public class LoteAves
    {
        public int Id { get; set; }
        public string Codigo { get; set; }
        public string Raza { get; set; }
        public int CantidadAves { get; set; }
        public int EdadSemanas { get; set; }
        public DateTime FechaIngreso { get; set; }
        public string Estado { get; set; }
    }

    public class ProduccionGalpon
    {
        public string CodigoLote { get; set; }
        public string Raza { get; set; }
        public int TotalHuevos { get; set; }
        public int CantidadJumbo { get; set; }
        public int CantidadAAA { get; set; }
        public int CantidadAA { get; set; }
        public int CantidadA { get; set; }
        public int CantidadB { get; set; }
        public int CantidadC { get; set; }
        public double PesoPromedio { get; set; }
    }
}