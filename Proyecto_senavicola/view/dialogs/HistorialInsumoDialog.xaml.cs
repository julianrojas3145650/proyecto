using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Proyecto_senavicola.data;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class HistorialInsumosDialog : Window
    {
        private ObservableCollection<MovimientoInsumo> movimientos;
        private ObservableCollection<MovimientoInsumo> movimientosFiltrados;

        public HistorialInsumosDialog()
        {
            movimientos = new ObservableCollection<MovimientoInsumo>();
            movimientosFiltrados = new ObservableCollection<MovimientoInsumo>();

            InitializeComponent();

            dgMovimientos.ItemsSource = movimientosFiltrados;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CargarHistorial();
        }

        private void CargarHistorial()
        {
            movimientos.Clear();

            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    // ✅ Columna correcta: FechaMovimiento (no Fecha)
                    string sql = @"
                        SELECT 
                            m.FechaMovimiento,
                            i.Nombre as NombreInsumo,
                            m.TipoMovimiento,
                            m.Cantidad,
                            m.Motivo,
                            COALESCE(u.Nombre || ' ' || u.Apellido, 'Sistema') as Usuario
                        FROM MovimientosInsumos m
                        INNER JOIN Insumos i ON m.InsumoId = i.Id
                        LEFT JOIN Usuarios u ON m.UsuarioId = u.Id
                        ORDER BY m.FechaMovimiento DESC
                        LIMIT 500";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            movimientos.Add(new MovimientoInsumo
                            {
                                FechaMovimiento = reader.GetDateTime(0),
                                NombreInsumo = reader.GetString(1),
                                TipoMovimiento = reader.GetString(2),
                                Cantidad = reader.GetDouble(3),
                                Motivo = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                UsuarioNombre = reader.GetString(5)
                            });
                        }
                    }
                }

                AplicarFiltro();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar historial: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbFiltro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AplicarFiltro();
        }

        private void AplicarFiltro()
        {
            movimientosFiltrados.Clear();

            var filtrados = movimientos.AsEnumerable();

            if (cmbFiltroTipo?.SelectedIndex > 0)
            {
                string filtro = (cmbFiltroTipo.SelectedItem as ComboBoxItem)?.Content.ToString();
                // Quitar la "s" final si viene como "Entradas", "Salidas", "Ajustes"
                if (!string.IsNullOrEmpty(filtro) && filtro.EndsWith("s"))
                    filtro = filtro.Substring(0, filtro.Length - 1);

                filtrados = filtrados.Where(m => m.TipoMovimiento == filtro);
            }

            foreach (var movimiento in filtrados)
                movimientosFiltrados.Add(movimiento);
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class MovimientoInsumo
    {
        public DateTime FechaMovimiento { get; set; }
        public string NombreInsumo { get; set; }
        public string TipoMovimiento { get; set; }
        public double Cantidad { get; set; }
        public string Motivo { get; set; }
        public string UsuarioNombre { get; set; }

        // ✅ switch expression reemplazado por if-else (compatible con C# 7/8)
        public Brush TipoColor
        {
            get
            {
                if (TipoMovimiento == "Entrada") return new SolidColorBrush(Color.FromRgb(76, 175, 80));
                if (TipoMovimiento == "Salida") return new SolidColorBrush(Color.FromRgb(244, 67, 54));
                if (TipoMovimiento == "Ajuste") return new SolidColorBrush(Color.FromRgb(255, 152, 0));
                return new SolidColorBrush(Color.FromRgb(128, 128, 128));
            }
        }
    }
}