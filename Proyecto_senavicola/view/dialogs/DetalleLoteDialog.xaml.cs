using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Windows;
using Proyecto_senavicola.data;
using Proyecto_senavicola.view.pages;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class DetalleLoteDialog : Window
    {
        private LoteGallinasModel lote;
        private ObservableCollection<AsignacionDetalle> asignaciones;

        public DetalleLoteDialog(LoteGallinasModel lote)
        {
            InitializeComponent();
            this.lote = lote;
            this.asignaciones = new ObservableCollection<AsignacionDetalle>();
            dgAsignaciones.ItemsSource = asignaciones;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CargarInformacion();
        }

        private void CargarInformacion()
        {
            // Información general
            txtTitulo.Text = $"📦 Detalle del Lote - {lote.CodigoLote}";
            txtCodigoLote.Text = lote.CodigoLote;
            txtRaza.Text = lote.Raza;
            txtEdad.Text = lote.EdadSemanas > 0 ? $"{lote.EdadSemanas} semanas" : "No especificada";
            txtFechaLlegada.Text = lote.FechaLlegada.ToString("dd/MM/yyyy");
            txtProveedor.Text = string.IsNullOrWhiteSpace(lote.Proveedor) ? "No especificado" : lote.Proveedor;
            txtObservaciones.Text = string.IsNullOrWhiteSpace(lote.Observaciones) ? "Sin observaciones" : lote.Observaciones;

            // Estadísticas
            txtTotal.Text = lote.CantidadTotal.ToString();
            txtAsignadas.Text = lote.CantidadAsignada.ToString();
            txtPendientes.Text = lote.CantidadPendiente.ToString();

            // Cargar historial de asignaciones
            CargarAsignaciones();
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
                            g.Codigo as CodigoGalpon,
                            a.Cantidad,
                            COALESCE(u.Nombre || ' ' || u.Apellido, 'Sistema') as Usuario
                        FROM AsignacionGallinas a
                        INNER JOIN Galpones g ON a.GalponId = g.Id
                        LEFT JOIN Usuarios u ON a.UsuarioId = u.Id
                        WHERE a.LoteId = @loteId
                        ORDER BY a.FechaAsignacion DESC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@loteId", lote.Id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                asignaciones.Add(new AsignacionDetalle
                                {
                                    FechaAsignacion = reader.GetDateTime(0),
                                    CodigoGalpon = reader.GetString(1),
                                    Cantidad = reader.GetInt32(2),
                                    UsuarioNombre = reader.GetString(3)
                                });
                            }
                        }
                    }
                }

                if (asignaciones.Count == 0)
                {
                    // Mensaje si no hay asignaciones
                    dgAsignaciones.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar asignaciones: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class AsignacionDetalle
    {
        public DateTime FechaAsignacion { get; set; }
        public string CodigoGalpon { get; set; }
        public int Cantidad { get; set; }
        public string UsuarioNombre { get; set; }
    }
}