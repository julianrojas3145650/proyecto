using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;
using Proyecto_senavicola.data;
using Proyecto_senavicola.services;
using Proyecto_senavicola.view.dialogs;
using Tesseract;
using OCV = OpenCvSharp;

namespace Proyecto_senavicola.view.pages
{
    public partial class HuevosPage : System.Windows.Controls.Page
    {
        private ObservableCollection<HuevoInventario> inventario;
        private ObservableCollection<HuevoVendido> vendidos;
        private ObservableCollection<HuevoDanado> danados;
        private ObservableCollection<HistorialMovimiento> historial;
        private ObservableCollection<LoteAves> lotesAves;
        private ObservableCollection<ProduccionGalpon> produccionPorGalpon;

        private bool camaraActiva = false;
        private bool grameraConectada = false;
        private double pesoActualGramera = 0.0;
        private System.Windows.Threading.DispatcherTimer timerGramera;

        private Queue<double> bufferOCR = new Queue<double>();
        private Queue<double> bufferPesosValidos = new Queue<double>();

        private double ultimoPesoConfirmado = 0;
        private const int BUFFER_OCR_SIZE = 5;
        private const int REPETICIONES_MINIMAS = 3;

        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private bool isDisposing = false;
        private object cameraLock = new object();
        private System.Windows.Controls.Image imagenCamara;

        private LoteAves loteSeleccionado;

        private EggVisionProcessor _eggProcessor;

        public HuevosPage()
        {
            InitializeComponent();
            InicializarColecciones();
            ConfigurarTimers();
            InicializarCamara();

            _eggProcessor = new EggVisionProcessor();

            this.Loaded += (s, e) =>
            {
                var window = System.Windows.Window.GetWindow(this);
                if (window != null)
                    window.Closing += (ws, we) => { isDisposing = true; DetenerCamaraSafe(); };
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

        // ────────────────────────────────────────────────────────────
        //  INICIALIZACIÓN
        // ────────────────────────────────────────────────────────────

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

        private void ConfigurarTimers()
        {
            timerGramera = new System.Windows.Threading.DispatcherTimer();
            timerGramera.Interval = TimeSpan.FromMilliseconds(500);
            timerGramera.Tick += TimerGramera_Tick;
        }

        // ────────────────────────────────────────────────────────────
        //  CARGA DESDE BASE DE DATOS
        // ────────────────────────────────────────────────────────────

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
                MessageBox.Show("No hay galpones registrados.\n\nVe al módulo de Gestión de Gallinas para crear galpones primero.",
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

        // ────────────────────────────────────────────────────────────
        //  PERSISTENCIA EN DB
        // ────────────────────────────────────────────────────────────


        // ── Helper: abre conexión con WAL mode y busy_timeout para evitar "database is locked" ──
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

        // ────────────────────────────────────────────────────────────
        //  SELECCIÓN DE GALPÓN
        // ────────────────────────────────────────────────────────────

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

        // ────────────────────────────────────────────────────────────
        //  CLASIFICACIÓN MANUAL
        // ────────────────────────────────────────────────────────────

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

        // ────────────────────────────────────────────────────────────
        //  CLASIFICACIÓN AUTOMÁTICA
        // ────────────────────────────────────────────────────────────

        private System.Windows.Threading.DispatcherTimer _timerDeteccion;

        private void IniciarDeteccion_Click(object sender, RoutedEventArgs e)
        {
            if (!camaraActiva)
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

            overlayDeteccion.Visibility = Visibility.Visible;

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

            var r = _eggProcessor?.UltimoResultado;

            if (r != null &&
                r.HuevoDetectado &&
                r.PesoConfirmado)
            {
                overlayDeteccion.Visibility = Visibility.Collapsed;
                AgregarHuevoAutomatico(r.PesoEstable);
            }
        }

        private double IntentarLeerPesoOCR()
        {
            string textoOcr = _eggProcessor?.UltimoResultado?.TextoOCR ?? "";
            string limpio = textoOcr.Trim().TrimStart('0');

            if (!double.TryParse(limpio,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double peso))
                return 0;

            if (peso > 120 && peso < 1200)
                peso /= 10.0;

            if (peso < 30 || peso > 120)
                return 0;

            // ── Buffer OCR ──
            bufferOCR.Enqueue(peso);
            if (bufferOCR.Count > BUFFER_OCR_SIZE)
                bufferOCR.Dequeue();

            if (bufferOCR.Count < REPETICIONES_MINIMAS)
                return 0;

            var ultimos = bufferOCR.Reverse().Take(REPETICIONES_MINIMAS).ToList();

            if (!ultimos.All(p => Math.Abs(p - ultimos[0]) < 0.5))
                return 0;

            // ── Filtro salto brusco ──
            if (ultimoPesoConfirmado > 0 &&
                Math.Abs(peso - ultimoPesoConfirmado) > 5)
                return 0;

            // ── Promedio móvil ──
            bufferPesosValidos.Enqueue(peso);
            if (bufferPesosValidos.Count > BUFFER_OCR_SIZE)
                bufferPesosValidos.Dequeue();

            double promedio = bufferPesosValidos.Average();

            if (bufferPesosValidos.All(p => Math.Abs(p - promedio) < 1))
            {
                ultimoPesoConfirmado = promedio;
                _eggProcessor.UltimoResultado.PesoEstable = promedio;
                _eggProcessor.UltimoResultado.PesoConfirmado = true;
                return promedio;
            }

            return 0;
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

            var rv = _eggProcessor?.UltimoResultado;
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

        // ────────────────────────────────────────────────────────────
        //  CRUD INVENTARIO
        // ────────────────────────────────────────────────────────────

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

        // ────────────────────────────────────────────────────────────
        //  ESTADÍSTICAS
        // ────────────────────────────────────────────────────────────

        private void ActualizarEstadisticas()
        {
            txtTotalInventario.Text = inventario.Sum(h => h.Cantidad).ToString();
            txtVendidosHoy.Text = vendidos.Where(v => v.FechaVenta.Date == DateTime.Today).Sum(v => v.Cantidad).ToString();
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
            var todos = inventario.Select(h => new { h.LoteAvesId, h.LoteAvesCodigo, h.Tipo, h.Cantidad, Peso = h.PesoPromedio })
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

        // ────────────────────────────────────────────────────────────
        //  HISTORIAL EN MEMORIA
        // ────────────────────────────────────────────────────────────

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

        // ────────────────────────────────────────────────────────────
        //  FILTROS
        // ────────────────────────────────────────────────────────────

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

        // ────────────────────────────────────────────────────────────
        //  GRAMERA
        // ────────────────────────────────────────────────────────────

        private void TimerGramera_Tick(object sender, EventArgs e)
        {
            if (!grameraConectada) return;
            pesoActualGramera = 45 + (new Random().NextDouble() * 35);
            txtLecturaGramera.Text = $"{pesoActualGramera:F1} g";
            string tipo = ClasificarPorPeso(pesoActualGramera);
            txtTipoAutomatico.Text = tipo;
            switch (tipo)
            {
                case "Jumbo":
                    txtTipoAutomatico.Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                    break;
                case "AAA":
                case "AA":
                    txtTipoAutomatico.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    break;
                case "A":
                    txtTipoAutomatico.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    break;
                default:
                    txtTipoAutomatico.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                    break;
            }

            ActualizarPanelVision();
        }

        // ────────────────────────────────────────────────────────────
        //  CÁMARA + VISIÓN POR COMPUTADOR
        // ────────────────────────────────────────────────────────────

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            isDisposing = true;
            try
            {
                DetenerCamaraSafe();
                _eggProcessor?.Dispose();
                _eggProcessor = null;

                if (timerGramera != null)
                {
                    timerGramera.Stop();
                    timerGramera.Tick -= TimerGramera_Tick;
                    timerGramera = null;
                }
            }
            catch { }
        }

        private void InicializarCamara()
        {
            try { videoDevices = null; }
            catch { }
        }

        private void ActivarCamara_Click(object sender, RoutedEventArgs e)
        {
            if (!camaraActiva)
            {
                try
                {
                    videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                    if (videoDevices.Count == 0)
                    {
                        MessageBox.Show("No se detectaron cámaras.", "Sin Cámara",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var dlg = new view.dialogs.SeleccionCamaraDialog(videoDevices);
                    dlg.Owner = System.Windows.Window.GetWindow(this);
                    if (dlg.ShowDialog() == true && dlg.CamaraSeleccionada != null)
                    {
                        videoSource = new VideoCaptureDevice(dlg.CamaraSeleccionada.MonikerString);

                        if (videoSource.VideoCapabilities != null && videoSource.VideoCapabilities.Length > 0)
                        {
                            var mejorRes = videoSource.VideoCapabilities
                                .OrderByDescending(c => c.FrameSize.Width * c.FrameSize.Height)
                                .First();
                            videoSource.VideoResolution = mejorRes;
                        }

                        videoSource.NewFrame += VideoSource_NewFrame;
                        imagenCamara = new System.Windows.Controls.Image
                        {
                            Stretch = Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                            RenderTransform = new ScaleTransform(1, 1)
                        };
                        borderCamera.Child = imagenCamara;
                        videoSource.Start();
                        camaraActiva = true;
                        statusCamera.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                        (sender as Button).Content = "📷 Desactivar Cámara";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al activar la cámara:\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    DetenerCamaraSafe();
                    camaraActiva = false;
                }
            }
            else
            {
                lock (cameraLock)
                {
                    DetenerCamaraSafe();
                    camaraActiva = false;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        statusCamera.Fill = new SolidColorBrush(Color.FromRgb(220, 53, 69));
                        (sender as Button).Content = "📷 Activar Cámara";
                        var sp = new StackPanel
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                        sp.Children.Add(new TextBlock
                        {
                            Text = "📷",
                            FontSize = 80,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Foreground = Brushes.White,
                            Margin = new Thickness(0, 0, 0, 20)
                        });
                        sp.Children.Add(new TextBlock
                        {
                            Text = "Cámara Desactivada",
                            FontSize = 18,
                            Foreground = Brushes.White,
                            HorizontalAlignment = HorizontalAlignment.Center
                        });
                        borderCamera.Child = sp;
                    }));
                }
            }
        }

        private void DetenerCamaraSafe()
        {
            lock (cameraLock)
            {
                try
                {
                    if (videoSource != null && videoSource.IsRunning)
                    {
                        videoSource.NewFrame -= VideoSource_NewFrame;
                        videoSource.SignalToStop();
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            try { videoSource?.WaitForStop(); } catch { }
                        }).Wait(2000);
                    }
                    if (videoSource != null)
                    {
                        try { videoSource.NewFrame -= VideoSource_NewFrame; } catch { }
                        videoSource = null;
                    }
                    if (!isDisposing)
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try { if (imagenCamara != null) imagenCamara.Source = null; }
                            catch { }
                        }));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al detener cámara: {ex.Message}");
                }
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (isDisposing || !camaraActiva) return;
            try
            {
                using (var bitmapOriginal = (System.Drawing.Bitmap)eventArgs.Frame.Clone())
                {
                    System.Drawing.Bitmap bitmapFinal = _eggProcessor != null
                        ? _eggProcessor.ProcesarFrame(bitmapOriginal)
                        : (System.Drawing.Bitmap)bitmapOriginal.Clone();

                    var bi = ConvertirBitmapABitmapImage(bitmapFinal);
                    bitmapFinal.Dispose();

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (imagenCamara != null && camaraActiva && !isDisposing)
                            {
                                imagenCamara.Source = bi;
                                ActualizarPanelVision();
                            }
                        }
                        catch { }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch { }
        }

        private void ActualizarPanelVision()
        {
            var r = _eggProcessor?.UltimoResultado;
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
                                    {
                                        if (tipo == "Jumbo")
                                        txtTipoAutomatico.Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                                        else if (tipo == "AAA" || tipo == "AA")
                                        txtTipoAutomatico.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                                        else if (tipo == "A")
                                        txtTipoAutomatico.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                                        else
                                        txtTipoAutomatico.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                                }
                                ;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        private BitmapImage ConvertirBitmapABitmapImage(System.Drawing.Bitmap bitmap)
        {
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
        }

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

    // ================================================================
    //  RESULTADO DEL ANÁLISIS DE VISIÓN
    // ================================================================

    public class ResultadoAnalisis
    {
        public double DiametroCm { get; set; }
        public double LargoCm { get; set; }
        public double VolumenCm3 { get; set; }
        public double PxPorCm { get; set; }
        public int CuadrosDetect { get; set; }
        public string TextoOCR { get; set; }
        public bool HuevoDetectado { get; set; }
        /// <summary>Indica cómo se calculó la escala: "Cuadrícula 1mm", "Cuadrícula 5mm", "Hoja circular" o "Default".</summary>
        public string ModoEscala { get; set; }

        public double PesoEstable { get; set; }
        public bool PesoConfirmado { get; set; }

    }

    // ================================================================
    //  PROCESADOR DE VISIÓN POR COMPUTADOR — VERSIÓN MEJORADA
    //  Estrategia de escala (3 métodos en cascada):
    //    1. Cuadrícula milimetrada fina  (1 mm por cuadro)
    //    2. Cuadrícula milimetrada gruesa (5 mm por cuadro)
    //    3. Círculo físico de la hoja (DIAMETRO_HOJA_CM)
    //  Detección del huevo:
    //    · Segmentación HSV (marrón/naranja) → más robusta ante iluminación
    //    · Fallback a thresholding adaptativo para huevos blancos/claros
    // ================================================================

    public class EggVisionProcessor : IDisposable
    {
        // ── Calibración ──────────────────────────────────────────────
        // Cuadros milimetrados
        private const double MM_POR_CUADRO_PEQUENO = 1.0;   // líneas finas = 1 mm
        private const double MM_POR_CUADRO_GRANDE = 5.0;   // líneas maestras = 5 mm
        private const double MM_POR_CM = 10.0;

        // ⚠️ Mide físicamente el diámetro de tu hoja circular y ajusta este valor:
        private const double DIAMETRO_HOJA_CM = 20.0;

        // OCR
        private const int OCR_INTERVAL_MS = 600;

        // ── Estado ───────────────────────────────────────────────────
        private TesseractEngine _tesseract;
        private readonly object _tesseractLock = new object();
        private DateTime _ultimoOcr = DateTime.MinValue;
        private string _textoOcrCache = "";
        private bool _disposed;

        public ResultadoAnalisis UltimoResultado { get; private set; } = new ResultadoAnalisis();

        public EggVisionProcessor() => InicializarTesseract();

        // ════════════════════════════════════════════════════════════
        //  PUNTO DE ENTRADA PRINCIPAL
        // ════════════════════════════════════════════════════════════
        public System.Drawing.Bitmap ProcesarFrame(System.Drawing.Bitmap frameSrc)
        {
            var frame = (System.Drawing.Bitmap)frameSrc.Clone();
            using var mat = BitmapToMat(frame);

            // 1. Escala (cuadrícula → hoja → default)
            var (pxPorCm, modoEscala) = DetectarEscala(mat);

            // 2. Detección del huevo
            var (contorno, rectHuevo, huevoOk) = DetectarHuevo(mat);

            // 3. OCR (báscula LCD)
            if ((DateTime.Now - _ultimoOcr).TotalMilliseconds > OCR_INTERVAL_MS)
            {
                _textoOcrCache = EjecutarOCR(frame);
                _ultimoOcr = DateTime.Now;
            }

            // 4. Métricas
            double diametroCm = 0, largoCm = 0, volumen = 0;
            int cuadros = 0;
            if (huevoOk && pxPorCm > 0)
            {
                diametroCm = rectHuevo.Width / pxPorCm;
                largoCm = rectHuevo.Height / pxPorCm;
                // cuadros en milímetros (dimensión mayor)
                cuadros = (int)Math.Round(Math.Max(diametroCm, largoCm) * MM_POR_CM);
                double a = largoCm / 2.0;
                double b = diametroCm / 2.0;
                volumen = (4.0 / 3.0) * Math.PI * a * b * b;
            }

            // 5. Dibujar anotaciones sobre el frame
            DibujarAnotaciones(frame, contorno, rectHuevo, huevoOk,
                               pxPorCm, diametroCm, largoCm, volumen,
                               cuadros, _textoOcrCache, modoEscala);

            UltimoResultado = new ResultadoAnalisis
            {
                DiametroCm = Math.Round(diametroCm, 2),
                LargoCm = Math.Round(largoCm, 2),
                VolumenCm3 = Math.Round(volumen, 3),
                PxPorCm = Math.Round(pxPorCm, 2),
                CuadrosDetect = cuadros,
                TextoOCR = _textoOcrCache,
                HuevoDetectado = huevoOk,
                ModoEscala = modoEscala
            };

            return frame;
        }

        // ════════════════════════════════════════════════════════════
        //  DETECCIÓN DE ESCALA — 3 métodos en cascada
        // ════════════════════════════════════════════════════════════
        private (double pxPorCm, string modo) DetectarEscala(OCV.Mat matColor)
        {
            // Intento 1 — cuadrícula fina (1 mm)
            double px = DetectarEscalaCuadricula(matColor, MM_POR_CUADRO_PEQUENO);
            if (px > 5) return (px, "Cuadrícula 1mm");

            // Intento 2 — líneas maestras (5 mm)
            px = DetectarEscalaCuadricula(matColor, MM_POR_CUADRO_GRANDE);
            if (px > 5) return (px, "Cuadrícula 5mm");

            // Intento 3 — diámetro físico de la hoja circular
            px = DetectarEscalaPorHoja(matColor, DIAMETRO_HOJA_CM);
            if (px > 5) return (px, "Hoja circular");

            // Fallback — ~40 px/cm (cámara FHD a ~30 cm)
            return (40.0, "Default");
        }

        // ── Escala por cuadrícula Hough ──────────────────────────────
        private double DetectarEscalaCuadricula(OCV.Mat matColor, double mmPorCuadro)
        {
            try
            {
                using var gray = new OCV.Mat();
                using var blur = new OCV.Mat();
                using var edges = new OCV.Mat();

                OCV.Cv2.CvtColor(matColor, gray, OCV.ColorConversionCodes.BGR2GRAY);
                OCV.Cv2.GaussianBlur(gray, blur, new OCV.Size(3, 3), 0);
                OCV.Cv2.Canny(blur, edges, 20, 60);

                double minLen = mmPorCuadro < 2 ? 15 : 30;
                double maxGap = mmPorCuadro < 2 ? 3 : 8;
                int thr = mmPorCuadro < 2 ? 30 : 50;

                var lineas = OCV.Cv2.HoughLinesP(edges,
                    rho: 1, theta: Math.PI / 180,
                    threshold: thr,
                    minLineLength: minLen,
                    maxLineGap: maxGap);

                if (lineas == null || lineas.Length < 8) return 0;

                var posH = new System.Collections.Generic.List<double>();
                var posV = new System.Collections.Generic.List<double>();

                foreach (var l in lineas)
                {
                    double ang = Math.Abs(
                        Math.Atan2(l.P2.Y - l.P1.Y, l.P2.X - l.P1.X) * 180 / Math.PI);
                    if (ang < 8 || ang > 172)
                        posH.Add((l.P1.Y + l.P2.Y) / 2.0);
                    else if (ang > 82 && ang < 98)
                        posV.Add((l.P1.X + l.P2.X) / 2.0);
                }

                double espH = EspaciadoConsistente(posH);
                double espV = EspaciadoConsistente(posV);
                double esp = (espH > 2 && espV > 2) ? (espH + espV) / 2.0
                            : (espH > 2) ? espH
                            : (espV > 2) ? espV : 0;

                if (esp < 2) return 0;

                // ── Validación: escala resultante debe ser realista ──
                // Para una hoja milimetrada a 20-50 cm de la cámara:
                //   1mm → esperamos entre 2 y 15 px/mm → 20-150 px/cm
                //   5mm → esperamos entre 10 y 80 px por 5mm → 20-160 px/cm
                double pxPorCm = esp / (mmPorCuadro / MM_POR_CM);
                if (pxPorCm < 15 || pxPorCm > 300) return 0; // valor fuera de rango realista

                return pxPorCm;
            }
            catch { return 0; }
        }

        // ── Escala por círculo de la hoja ────────────────────────────
        private double DetectarEscalaPorHoja(OCV.Mat matColor, double diametroCm)
        {
            try
            {
                using var gray = new OCV.Mat();
                using var blur = new OCV.Mat();
                OCV.Cv2.CvtColor(matColor, gray, OCV.ColorConversionCodes.BGR2GRAY);
                OCV.Cv2.GaussianBlur(gray, blur, new OCV.Size(9, 9), 2);

                var circles = OCV.Cv2.HoughCircles(blur,
                    OCV.HoughModes.Gradient, dp: 1.2,
                    minDist: matColor.Width * 0.3,
                    param1: 80, param2: 40,
                    minRadius: matColor.Width / 6,
                    maxRadius: matColor.Width / 2);

                if (circles == null || circles.Length == 0) return 0;
                double maxR = circles.Max(c => c.Radius);
                return (maxR * 2) / diametroCm;
            }
            catch { return 0; }
        }

        // ── Espaciado mediano entre posiciones ───────────────────────
        private double EspaciadoConsistente(System.Collections.Generic.List<double> pos)
        {
            if (pos.Count < 4) return 0;
            pos.Sort();

            // Eliminar duplicados cercanos (< 2px)
            var unique = new System.Collections.Generic.List<double> { pos[0] };
            foreach (var p in pos)
                if (p - unique[unique.Count - 1] > 2) unique.Add(p);

            if (unique.Count < 4) return 0;

            var difs = new System.Collections.Generic.List<double>();
            for (int i = 1; i < unique.Count; i++)
            {
                double d = unique[i] - unique[i - 1];
                if (d > 2 && d < 250) difs.Add(d);
            }
            if (difs.Count < 3) return 0;

            difs.Sort();
            double mediana = difs[difs.Count / 2];

            // Verificar consistencia: al menos el 60% de los intervalos deben estar
            // dentro del ±30% de la mediana (patrón regular = cuadrícula real)
            int consistentes = difs.Count(d => Math.Abs(d - mediana) / mediana < 0.30);
            if ((double)consistentes / difs.Count < 0.60) return 0;

            return mediana;
        }

        // ════════════════════════════════════════════════════════════
        //  DETECCIÓN DEL HUEVO
        //  Primario: segmentación HSV (marrón/naranja)
        //  Fallback: thresholding adaptativo (huevos blancos/claros)
        // ════════════════════════════════════════════════════════════
        private (OCV.Point[] contorno, OCV.Rect rect, bool ok) DetectarHuevo(OCV.Mat matColor)
        {
            try
            {
                using var hsv = new OCV.Mat();
                using var mask1 = new OCV.Mat();
                using var mask2 = new OCV.Mat();
                using var mask = new OCV.Mat();
                using var morph = new OCV.Mat();

                OCV.Cv2.CvtColor(matColor, hsv, OCV.ColorConversionCodes.BGR2HSV);

                // Rango principal: marrón/naranja (huevos pigmentados)
                OCV.Cv2.InRange(hsv,
                    new OCV.Scalar(5, 60, 100),
                    new OCV.Scalar(30, 255, 255), mask1);

                // Rango secundario: beige/blanco cálido
                OCV.Cv2.InRange(hsv,
                    new OCV.Scalar(0, 20, 160),
                    new OCV.Scalar(20, 100, 255), mask2);

                OCV.Cv2.BitwiseOr(mask1, mask2, mask);

                var k = OCV.Cv2.GetStructuringElement(OCV.MorphShapes.Ellipse, new OCV.Size(9, 9));
                OCV.Cv2.MorphologyEx(mask, morph, OCV.MorphTypes.Close, k, iterations: 3);
                OCV.Cv2.MorphologyEx(morph, morph, OCV.MorphTypes.Open, k, iterations: 2);
                k.Dispose();

                OCV.Cv2.FindContours(morph, out var conts, out _,
                    OCV.RetrievalModes.External,
                    OCV.ContourApproximationModes.ApproxSimple);

                var resultado = MejorContorno(conts, matColor.Rows * matColor.Cols);
                if (resultado.ok) return resultado;

                // Fallback si HSV no detecta nada
                return FallbackDeteccion(matColor);
            }
            catch { return (null, default, false); }
        }

        private (OCV.Point[] c, OCV.Rect r, bool ok) FallbackDeteccion(OCV.Mat matColor)
        {
            try
            {
                using var gray = new OCV.Mat();
                using var blur = new OCV.Mat();
                using var thresh = new OCV.Mat();
                using var morph = new OCV.Mat();

                OCV.Cv2.CvtColor(matColor, gray, OCV.ColorConversionCodes.BGR2GRAY);
                OCV.Cv2.GaussianBlur(gray, blur, new OCV.Size(9, 9), 2);
                OCV.Cv2.AdaptiveThreshold(blur, thresh, 255,
                    OCV.AdaptiveThresholdTypes.GaussianC,
                    OCV.ThresholdTypes.BinaryInv, 21, 4);

                var k = OCV.Cv2.GetStructuringElement(OCV.MorphShapes.Ellipse, new OCV.Size(5, 5));
                OCV.Cv2.MorphologyEx(thresh, morph, OCV.MorphTypes.Close, k, iterations: 3);
                k.Dispose();

                OCV.Cv2.FindContours(morph, out var conts, out _,
                    OCV.RetrievalModes.External,
                    OCV.ContourApproximationModes.ApproxSimple);

                return MejorContorno(conts, matColor.Rows * matColor.Cols);
            }
            catch { return (null, default, false); }
        }

        // ── Selección del contorno más adecuado ──────────────────────
        private (OCV.Point[] c, OCV.Rect r, bool ok) MejorContorno(OCV.Point[][] conts, int imgArea)
        {
            if (conts == null || conts.Length == 0) return (null, default, false);

            OCV.Point[] mejor = null;
            double maxA = 0;
            OCV.Rect rect = default;

            foreach (var c in conts)
            {
                double area = OCV.Cv2.ContourArea(c);
                if (area < imgArea * 0.01 || area > imgArea * 0.55) continue;

                var br = OCV.Cv2.BoundingRect(c);
                double ar = (double)br.Width / br.Height;
                if (ar < 0.4 || ar > 2.5) continue;

                double per = OCV.Cv2.ArcLength(c, true);
                if (per < 1) continue;
                double circ = 4 * Math.PI * area / (per * per);
                if (circ < 0.35) continue;

                if (area > maxA) { maxA = area; mejor = c; rect = br; }
            }

            if (mejor == null) return (null, default, false);
            return (mejor, rect, true);
        }

        // ════════════════════════════════════════════════════════════
        //  OCR (báscula LCD azul)
        // ════════════════════════════════════════════════════════════
        private void InicializarTesseract()
        {
            // Tesseract es OPCIONAL — si no hay tessdata, el OCR simplemente no funciona
            // pero la detección visual del huevo sigue operando con normalidad.
            _tesseract = null;

            // Rutas candidatas donde puede estar la carpeta tessdata
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidatos = new[]
            {
                Path.Combine(baseDir, "tessdata"),                          // bin\Debug\tessdata
                Path.Combine(baseDir, "..", "..", "tessdata"),              // raíz del proyecto
                Path.Combine(baseDir, "..", "..", "..", "tessdata"),        // un nivel más arriba
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                    "Tesseract-OCR", "tessdata"),                           // instalación global
                @"C:\Program Files\Tesseract-OCR\tessdata",                // ruta típica en Windows
                @"C:\Tesseract-OCR\tessdata",
            };

            // Idiomas a intentar (de más completo a más simple)
            string[][] idiomasCandidatos = new[]
            {
                new[] { "spa", "eng" },
                new[] { "eng" },
                new[] { "spa" },
            };

            foreach (string ruta in candidatos)
            {
                string rutaAbs = Path.GetFullPath(ruta);
                if (!Directory.Exists(rutaAbs)) continue;

                foreach (string[] idiomas in idiomasCandidatos)
                {
                    // Verificar que existan los archivos .traineddata
                    bool todoPresente = idiomas.All(lang =>
                        File.Exists(Path.Combine(rutaAbs, lang + ".traineddata")));
                    if (!todoPresente) continue;

                    try
                    {
                        string langStr = string.Join("+", idiomas);
                        _tesseract = new TesseractEngine(rutaAbs, langStr, EngineMode.Default);
                        System.Diagnostics.Debug.WriteLine(
                            $"[Tesseract] OK — ruta: {rutaAbs} | idiomas: {langStr}");
                        return; // Éxito, salir
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[Tesseract] Fallo con ruta '{rutaAbs}': {ex.Message}");
                        _tesseract = null;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine(
                "[Tesseract] No se encontraron archivos traineddata. " +
                "OCR desactivado. Descarga spa.traineddata y/o eng.traineddata " +
                "desde https://github.com/tesseract-ocr/tessdata y colócalos en: " +
                Path.Combine(baseDir, "tessdata"));
        }

        private string EjecutarOCR(System.Drawing.Bitmap bitmap)
        {
            try
            {
                using var mat = BitmapToMat(bitmap);
                using var gray = new OCV.Mat();
                OCV.Cv2.CvtColor(mat, gray, OCV.ColorConversionCodes.BGR2GRAY);

                // La banda correcta es 55%-95% de altura donde está la bandeja
                int y1 = Math.Max(0, (int)(mat.Height * 0.55));
                int h1 = Math.Min(mat.Height - y1, (int)(mat.Height * 0.40));
                using var banda = gray[new OCV.Rect(0, y1, mat.Width, h1)].Clone();

                // Umbral 180 + INVERTIR: dígitos oscuros sobre fondo blanco → dígitos blancos
                using var th = new OCV.Mat();
                using var thInv = new OCV.Mat();
                OCV.Cv2.Threshold(banda, th, 180, 255, OCV.ThresholdTypes.Binary);
                OCV.Cv2.BitwiseNot(th, thInv); // Invertir: ahora dígitos son BLANCOS

                // Encontrar la región blanca grande (la bandeja/plato de la báscula)
                OCV.Cv2.FindContours(th, out var contsPlato, out _,
                    OCV.RetrievalModes.External,
                    OCV.ContourApproximationModes.ApproxSimple);

                // Buscar el contorno más grande = la bandeja blanca
                OCV.Rect platoRect = default;
                double maxArea = 0;
                foreach (var c in contsPlato ?? Array.Empty<OCV.Point[]>())
                {
                    var br = OCV.Cv2.BoundingRect(c);
                    double area = br.Width * (double)br.Height;
                    double fracTotal = area / (banda.Width * (double)banda.Height);
                    // La bandeja ocupa ~20%-60% del frame
                    if (fracTotal < 0.15 || fracTotal > 0.65) continue;
                    double ar = (double)br.Width / Math.Max(br.Height, 1);
                    if (ar < 0.8 || ar > 4.0) continue;
                    if (area > maxArea) { maxArea = area; platoRect = br; }
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[OCR] Plato detectado: {platoRect.Width}x{platoRect.Height} en Y={platoRect.Y}");

                // Recortar solo la zona del plato donde están los dígitos
                OCV.Mat recorteDigitos;
                if (platoRect.Width > 30 && platoRect.Height > 20)
                {
                    // Los dígitos están en la mitad superior del plato
                    int dh = (int)(platoRect.Height * 0.55);
                    var zonaDigitos = new OCV.Rect(
                        platoRect.X,
                        platoRect.Y,
                        platoRect.Width,
                        Math.Min(dh, banda.Rows - platoRect.Y));
                    recorteDigitos = thInv[zonaDigitos].Clone();
                }
                else
                {
                    // Fallback: banda completa invertida
                    recorteDigitos = thInv.Clone();
                }

                // Limpiar ruido con morfología
                using var kernel = OCV.Cv2.GetStructuringElement(
                    OCV.MorphShapes.Rect, new OCV.Size(3, 3));
                using var clean = new OCV.Mat();
                OCV.Cv2.MorphologyEx(recorteDigitos, clean, OCV.MorphTypes.Open, kernel);
                recorteDigitos.Dispose();

                // Buscar contornos de dígitos en la zona del plato
                OCV.Cv2.FindContours(clean, out var contsDigitos, out _,
                    OCV.RetrievalModes.External,
                    OCV.ContourApproximationModes.ApproxSimple);

                var digitos = new System.Collections.Generic.List<OCV.Rect>();
                foreach (var c in contsDigitos ?? Array.Empty<OCV.Point[]>())
                {
                    var br = OCV.Cv2.BoundingRect(c);
                    double ratio = (double)br.Height / Math.Max(br.Width, 1);
                    double area = br.Width * (double)br.Height;

                    if (ratio < 0.8 || ratio > 4.0) continue;
                    if (br.Height < clean.Rows * 0.20) continue;
                    if (br.Height > clean.Rows * 0.98) continue;
                    if (area < 80) continue;

                    digitos.Add(br);
                }

                System.Diagnostics.Debug.WriteLine($"[OCR] Dígitos encontrados: {digitos.Count}");

                if (digitos.Count < 1 || digitos.Count > 5)
                {
                    clean.Dispose();
                    return "";
                }

                digitos.Sort((a, b) => a.X.CompareTo(b.X));

                // Si hay Tesseract, usarlo sobre el recorte limpio
                if (_tesseract != null)
                {
                    using var scaled = new OCV.Mat();
                    OCV.Cv2.Resize(clean, scaled,
                        new OCV.Size(clean.Width * 4, clean.Height * 4),
                        interpolation: OCV.InterpolationFlags.Cubic);
                    using var bmpOcr = MatToBitmap(scaled);
                    clean.Dispose();

                    lock (_tesseractLock)
                    {
                        _tesseract.SetVariable("tessedit_char_whitelist", "0123456789.");
                        using var pix = PixConverter.ToPix(bmpOcr);
                        using var page = _tesseract.Process(pix, PageSegMode.SingleLine);
                        string txt = page.GetText().Trim();
                        txt = System.Text.RegularExpressions.Regex.Replace(txt, @"[^0-9.]", "");
                        System.Diagnostics.Debug.WriteLine($"[OCR Tesseract] '{txt}'");
                        return txt;
                    }
                }

                clean.Dispose();

                // Sin Tesseract: estimar peso por ancho de dígitos
                // Sabemos que son 2 dígitos ("58") → peso directo en gramos
                if (digitos.Count == 2)
                {
                    // Calcular posición relativa en el frame para estimar escala
                    // Con 2 dígitos el valor es XX (sin decimal) → peso directo
                    return $"DIGITOS:2";
                }
                if (digitos.Count == 3)
                {
                    // Podría ser XX.X
                    return $"DIGITOS:3";
                }

                return "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OCR Error] {ex.Message}");
                return "";
            }
        }

        // ════════════════════════════════════════════════════════════
        //  DIBUJAR ANOTACIONES (GDI+)
        // ════════════════════════════════════════════════════════════
        private void DibujarAnotaciones(
            System.Drawing.Bitmap frame,
            OCV.Point[] contorno, OCV.Rect rectHuevo, bool huevoOk,
            double pxPorCm, double diametroCm, double largoCm,
            double volumen, int cuadros, string textoOcr, string modoEscala)
        {
            using var g = System.Drawing.Graphics.FromImage(frame);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Contorno + bounding box + flechas
            if (huevoOk && contorno != null)
            {
                var pts = Array.ConvertAll(contorno, p => new System.Drawing.PointF(p.X, p.Y));
                if (pts.Length > 2)
                {
                    using var pC = new System.Drawing.Pen(System.Drawing.Color.LimeGreen, 2.5f);
                    g.DrawPolygon(pC, pts);
                }

                using var pB = new System.Drawing.Pen(System.Drawing.Color.Cyan, 1.5f)
                { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawRectangle(pB, rectHuevo.X, rectHuevo.Y, rectHuevo.Width, rectHuevo.Height);

                using var pM = new System.Drawing.Pen(System.Drawing.Color.Yellow, 1.8f);
                pM.CustomStartCap = new System.Drawing.Drawing2D.AdjustableArrowCap(4, 4);
                pM.CustomEndCap = new System.Drawing.Drawing2D.AdjustableArrowCap(4, 4);

                int cy = rectHuevo.Y + rectHuevo.Height / 2;
                g.DrawLine(pM, rectHuevo.Left, cy, rectHuevo.Right, cy);
                int cx = rectHuevo.X + rectHuevo.Width / 2;
                g.DrawLine(pM, cx, rectHuevo.Top, cx, rectHuevo.Bottom);

                using var fM = new System.Drawing.Font("Segoe UI", 8f, System.Drawing.FontStyle.Bold);
                g.DrawString($"{diametroCm:F2} cm", fM, System.Drawing.Brushes.Yellow,
                             rectHuevo.Right + 5, cy - 9);
                g.DrawString($"{largoCm:F2} cm", fM, System.Drawing.Brushes.Yellow,
                             cx + 5, rectHuevo.Bottom + 2);
            }

            DibujarPanel(g, diametroCm, largoCm, volumen, cuadros, pxPorCm, modoEscala, huevoOk);

            // OCR overlay
            if (!string.IsNullOrWhiteSpace(textoOcr))
            {
                using var fO = new System.Drawing.Font("Consolas", 9f);
                string lin = textoOcr.Replace("\n", " | ");
                if (lin.Length > 60) lin = lin.Substring(0, 60) + "…";
                var sz = g.MeasureString(lin, fO);
                int py = frame.Height - 30;
                g.FillRectangle(
                    new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(160, 0, 0, 0)),
                    5, py - 4, sz.Width + 8, sz.Height + 4);
                g.DrawString(lin, fO, System.Drawing.Brushes.White, 9, py);
            }
        }

        private void DibujarPanel(
            System.Drawing.Graphics g,
            double diametroCm, double largoCm, double volumen,
            int cuadros, double pxPorCm, string modoEscala, bool huevoOk)
        {
            int w = 240, h = huevoOk ? 155 : 58;
            g.FillRectangle(
                new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(170, 0, 0, 0)),
                8, 8, w, h);
            g.DrawRectangle(
                new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, System.Drawing.Color.LimeGreen), 1f),
                8, 8, w, h);

            using var fT = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold);
            using var fV = new System.Drawing.Font("Segoe UI", 8.5f);
            using var fI = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Italic);

            int y = 14;
            g.DrawString("🥚 Medición de Huevo", fT, System.Drawing.Brushes.LimeGreen, 14, y);
            y += 20;

            if (!huevoOk)
            {
                g.DrawString("Sin huevo detectado", fI, System.Drawing.Brushes.Orange, 14, y);
                return;
            }

            Fila(g, fV, ref y, "Diámetro:", $"{diametroCm:F2} cm", System.Drawing.Color.Cyan);
            Fila(g, fV, ref y, "Largo:", $"{largoCm:F2} cm", System.Drawing.Color.Cyan);
            Fila(g, fV, ref y, "Volumen:", $"{volumen:F3} cm³", System.Drawing.Color.Yellow);
            Fila(g, fV, ref y, "Longitud:", $"{cuadros} mm", System.Drawing.Color.LightGray);
            Fila(g, fV, ref y, "Escala:", $"{pxPorCm:F1} px/cm", System.Drawing.Color.LightGray);
            Fila(g, fV, ref y, "Método:", modoEscala, System.Drawing.Color.LightGreen);
        }

        private void Fila(System.Drawing.Graphics g, System.Drawing.Font f,
                          ref int y, string label, string valor, System.Drawing.Color cValor)
        {
            g.DrawString(label, f, System.Drawing.Brushes.White, 14, y);
            g.DrawString(valor, f, new System.Drawing.SolidBrush(cValor), 98, y);
            y += 18;
        }

        // ════════════════════════════════════════════════════════════
        //  CONVERSIONES Bitmap ↔ Mat
        // ════════════════════════════════════════════════════════════
        private static OCV.Mat BitmapToMat(System.Drawing.Bitmap bmp)
        {
            var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
            var bmpData = bmp.LockBits(rect,
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            try
            {
                int stride = Math.Abs(bmpData.Stride);
                int total = stride * bmp.Height;
                byte[] px = new byte[total];
                System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, px, 0, total);
                var mat = new OCV.Mat(bmp.Height, bmp.Width, OCV.MatType.CV_8UC3);
                System.Runtime.InteropServices.Marshal.Copy(px, 0, mat.Data, total);
                return mat;
            }
            finally { bmp.UnlockBits(bmpData); }
        }

        private static System.Drawing.Bitmap MatToBitmap(OCV.Mat mat)
        {
            OCV.Mat src; bool disp = false;
            if (mat.Channels() == 1)
            {
                src = new OCV.Mat();
                OCV.Cv2.CvtColor(mat, src, OCV.ColorConversionCodes.GRAY2BGR);
                disp = true;
            }
            else src = mat;

            var bmp = new System.Drawing.Bitmap(src.Width, src.Height,
                              System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var bmpData = bmp.LockBits(
                new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            try
            {
                int total = Math.Abs(bmpData.Stride) * src.Height;
                byte[] px = new byte[total];
                System.Runtime.InteropServices.Marshal.Copy(src.Data, px, 0, total);
                System.Runtime.InteropServices.Marshal.Copy(px, 0, bmpData.Scan0, total);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
                if (disp) src.Dispose();
            }
            return bmp;
        }

        // ════════════════════════════════════════════════════════════
        //  DISPOSE
        // ════════════════════════════════════════════════════════════
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_tesseractLock) { _tesseract?.Dispose(); _tesseract = null; }
        }
    }
}