using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClosedXML.Excel;
using Microsoft.Win32;
using Proyecto_senavicola.data;
using Proyecto_senavicola.services;
using Proyecto_senavicola.view.dialogs;

namespace Proyecto_senavicola.view.pages
{
    public partial class InsumosPage : Page
    {
        private ObservableCollection<InsumoModel> insumos;

        public InsumosPage()
        {
            InitializeComponent();
            insumos = new ObservableCollection<InsumoModel>();
            DgInsumos.ItemsSource = insumos;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            navBar.SetModulo("📦 Gestión de Insumos", "Registra los alimentos y materiales que usa la granja");
            CargarInsumos();
            ActualizarEstadisticas();
        }

        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            PlaceholderBuscar.Visibility =
                string.IsNullOrWhiteSpace(TxtBuscar.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void CargarInsumos()
        {
            insumos.Clear();

            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT * FROM Insumos ORDER BY FechaIngreso DESC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var insumo = new InsumoModel
                            {
                                Id = reader.GetInt32(0),
                                Nombre = reader.GetString(1),
                                Tipo = reader.GetString(2),
                                Cantidad = reader.GetDouble(3),
                                Unidad = reader.GetString(4),
                                StockMinimo = reader.GetDouble(5),
                                FechaIngreso = reader.GetDateTime(6),
                                FechaVencimiento = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                                Proveedor = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                PrecioUnitario = reader.IsDBNull(9) ? 0 : reader.GetDouble(9),
                                Responsable = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                Observaciones = reader.IsDBNull(11) ? "" : reader.GetString(11)
                            };

                            insumos.Add(insumo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar insumos: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActualizarEstadisticas()
        {
            double totalAlimento = insumos.Where(i => i.Tipo == "Alimento").Sum(i => i.Cantidad);
            TxtTotalAlimento.Text = $"{totalAlimento:F2} kg";

            int totalHerramientas = insumos.Count(i => i.Tipo == "Herramienta");
            TxtTotalHerramientas.Text = totalHerramientas.ToString();

            int totalMedicamentos = insumos.Count(i => i.Tipo == "Medicamento");
            TxtTotalMedicamentos.Text = totalMedicamentos.ToString();

            int alertasBajoStock = insumos.Count(i => i.Cantidad <= i.StockMinimo);
            TxtAlertasBajoStock.Text = alertasBajoStock.ToString();
        }

        private void BtnRegistrarInsumo_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthenticationService.TienePermiso("Gestionar"))
            {
                MessageBox.Show("No tienes permisos para registrar insumos.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new RegistrarInsumoDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    int nuevoId;

                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string sql = @"
                            INSERT INTO Insumos 
                            (Nombre, Tipo, Cantidad, Unidad, StockMinimo, FechaIngreso, FechaVencimiento, 
                             Proveedor, PrecioUnitario, Responsable, Observaciones)
                            VALUES (@nombre, @tipo, @cantidad, @unidad, @stockMin, @fechaIngreso, @vencimiento,
                                    @proveedor, @precio, @responsable, @obs);
                            SELECT last_insert_rowid();";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@nombre", dialog.Nombre);
                            cmd.Parameters.AddWithValue("@tipo", dialog.Tipo);
                            cmd.Parameters.AddWithValue("@cantidad", dialog.Cantidad);
                            cmd.Parameters.AddWithValue("@unidad", dialog.Unidad);
                            cmd.Parameters.AddWithValue("@stockMin", dialog.StockMinimo);
                            cmd.Parameters.AddWithValue("@fechaIngreso", DateTime.Now);
                            cmd.Parameters.AddWithValue("@vencimiento",
                                dialog.FechaVencimiento.HasValue ? (object)dialog.FechaVencimiento.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@proveedor", dialog.Proveedor ?? "");
                            cmd.Parameters.AddWithValue("@precio", dialog.PrecioUnitario);
                            cmd.Parameters.AddWithValue("@responsable", AuthenticationService.UsuarioActual?.NombreCompleto ?? "");
                            cmd.Parameters.AddWithValue("@obs", dialog.Observaciones ?? "");

                            nuevoId = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                    }

                    // ✅ Registrar movimiento de entrada inicial con el Id real del insumo recién creado
                    RegistrarMovimiento(nuevoId, "Entrada", dialog.Cantidad, "Registro inicial de insumo");

                    DatabaseHelper.RegistrarHistorial("Insumos", "Registrar Insumo",
                        $"{dialog.Nombre} - {dialog.Cantidad} {dialog.Unidad}",
                        AuthenticationService.UsuarioActual?.Id);

                    CargarInsumos();
                    ActualizarEstadisticas();

                    MessageBox.Show("✅ Insumo registrado exitosamente.", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al registrar insumo: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnReabastecer_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthenticationService.TienePermiso("Gestionar"))
            {
                MessageBox.Show("No tienes permisos para reabastecer.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var insumo = (sender as Button)?.Tag as InsumoModel;
            if (insumo == null) return;

            var dialog = new ReabastecerInsumoDialog(insumo);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string sql = "UPDATE Insumos SET Cantidad = Cantidad + @cantidad WHERE Id = @id";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@cantidad", dialog.Cantidad);
                            cmd.Parameters.AddWithValue("@id", insumo.Id);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // ✅ Movimiento de entrada por reabastecimiento
                    RegistrarMovimiento(insumo.Id, "Entrada", dialog.Cantidad,
                        dialog.Observaciones ?? "Reabastecimiento");

                    DatabaseHelper.RegistrarHistorial("Insumos", "Reabastecer",
                        $"{insumo.Nombre} - +{dialog.Cantidad} {insumo.Unidad}",
                        AuthenticationService.UsuarioActual?.Id);

                    CargarInsumos();
                    ActualizarEstadisticas();

                    MessageBox.Show($"✅ Se agregaron {dialog.Cantidad} {insumo.Unidad} de {insumo.Nombre}",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al reabastecer: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEditarInsumo_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthenticationService.TienePermiso("Gestionar"))
            {
                MessageBox.Show("No tienes permisos para editar insumos.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var insumo = (sender as Button)?.Tag as InsumoModel;
            if (insumo == null) return;

            var dialog = new RegistrarInsumoDialog(insumo);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    double cantidadAnterior = insumo.Cantidad;

                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string sql = @"
                            UPDATE Insumos 
                            SET Nombre = @nombre, Tipo = @tipo, Unidad = @unidad, 
                                StockMinimo = @stockMin, FechaVencimiento = @vencimiento,
                                Proveedor = @proveedor, PrecioUnitario = @precio, Observaciones = @obs
                            WHERE Id = @id";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@nombre", dialog.Nombre);
                            cmd.Parameters.AddWithValue("@tipo", dialog.Tipo);
                            cmd.Parameters.AddWithValue("@unidad", dialog.Unidad);
                            cmd.Parameters.AddWithValue("@stockMin", dialog.StockMinimo);
                            cmd.Parameters.AddWithValue("@vencimiento",
                                dialog.FechaVencimiento.HasValue ? (object)dialog.FechaVencimiento.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@proveedor", dialog.Proveedor ?? "");
                            cmd.Parameters.AddWithValue("@precio", dialog.PrecioUnitario);
                            cmd.Parameters.AddWithValue("@obs", dialog.Observaciones ?? "");
                            cmd.Parameters.AddWithValue("@id", insumo.Id);

                            cmd.ExecuteNonQuery();
                        }
                    }

                    // ✅ Si la cantidad cambió, registrar un ajuste en el historial
                    if (dialog.Cantidad != cantidadAnterior)
                    {
                        double diferencia = dialog.Cantidad - cantidadAnterior;

                        // Actualizar también la cantidad si el diálogo la expone
                        using (var conn = DatabaseHelper.GetConnection())
                        {
                            conn.Open();
                            string sqlCant = "UPDATE Insumos SET Cantidad = @cantidad WHERE Id = @id";
                            using (var cmd = new SQLiteCommand(sqlCant, conn))
                            {
                                cmd.Parameters.AddWithValue("@cantidad", dialog.Cantidad);
                                cmd.Parameters.AddWithValue("@id", insumo.Id);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        RegistrarMovimiento(insumo.Id, "Ajuste", Math.Abs(diferencia),
                            $"Ajuste por edición. Anterior: {cantidadAnterior}, Nuevo: {dialog.Cantidad}");
                    }

                    DatabaseHelper.RegistrarHistorial("Insumos", "Editar Insumo",
                        $"{dialog.Nombre} actualizado", AuthenticationService.UsuarioActual?.Id);

                    CargarInsumos();
                    ActualizarEstadisticas();

                    MessageBox.Show("✅ Insumo actualizado exitosamente.", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al actualizar insumo: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEliminarInsumo_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthenticationService.TienePermiso("Administrar"))
            {
                MessageBox.Show("Solo los administradores pueden eliminar insumos.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var insumo = (sender as Button)?.Tag as InsumoModel;
            if (insumo == null) return;

            var resultado = MessageBox.Show(
                $"¿Estás seguro de eliminar el insumo '{insumo.Nombre}'?",
                "Confirmar Eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resultado == MessageBoxResult.Yes)
            {
                try
                {
                    // ✅ Registrar movimiento de salida antes de eliminar (mientras aún existe la FK)
                    RegistrarMovimiento(insumo.Id, "Salida", insumo.Cantidad, "Eliminación del insumo");

                    using (var conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string sql = "DELETE FROM Insumos WHERE Id = @id";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", insumo.Id);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    DatabaseHelper.RegistrarHistorial("Insumos", "Eliminar Insumo",
                        $"{insumo.Nombre} eliminado", AuthenticationService.UsuarioActual?.Id);

                    CargarInsumos();
                    ActualizarEstadisticas();

                    MessageBox.Show("✅ Insumo eliminado exitosamente.", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar insumo: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// ✅ CORRECCIÓN PRINCIPAL: columna correcta es FechaMovimiento (no Fecha).
        /// La columna tiene DEFAULT CURRENT_TIMESTAMP así que no se envía en el INSERT.
        /// Ahora muestra el error al usuario en lugar de ocultarlo en Debug.
        /// </summary>
        private void RegistrarMovimiento(int insumoId, string tipo, double cantidad, string motivo)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    // ✅ Sin columna Fecha — FechaMovimiento se llena con DEFAULT CURRENT_TIMESTAMP
                    string sql = @"
                        INSERT INTO MovimientosInsumos (InsumoId, TipoMovimiento, Cantidad, Motivo, UsuarioId)
                        VALUES (@insumo, @tipo, @cantidad, @motivo, @usuario)";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@insumo", insumoId);
                        cmd.Parameters.AddWithValue("@tipo", tipo);
                        cmd.Parameters.AddWithValue("@cantidad", cantidad);
                        cmd.Parameters.AddWithValue("@motivo", motivo ?? "");
                        cmd.Parameters.AddWithValue("@usuario",
                            AuthenticationService.UsuarioActual?.Id > 0
                                ? (object)AuthenticationService.UsuarioActual.Id
                                : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // ✅ Ahora el error es visible para el usuario, no solo en Debug
                MessageBox.Show($"Error al registrar movimiento en historial: {ex.Message}",
                    "Error de Historial", MessageBoxButton.OK, MessageBoxImage.Warning);
                System.Diagnostics.Debug.WriteLine($"Error al registrar movimiento: {ex.Message}");
            }
        }

        private void BtnVerHistorial_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new HistorialInsumosDialog
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
        }

        private void BtnExportar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    FileName = $"Inventario_Insumos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Insumos");

                        worksheet.Cell(1, 1).Value = "ID";
                        worksheet.Cell(1, 2).Value = "Nombre";
                        worksheet.Cell(1, 3).Value = "Tipo";
                        worksheet.Cell(1, 4).Value = "Cantidad";
                        worksheet.Cell(1, 5).Value = "Unidad";
                        worksheet.Cell(1, 6).Value = "Stock Mínimo";
                        worksheet.Cell(1, 7).Value = "Fecha Ingreso";
                        worksheet.Cell(1, 8).Value = "Proveedor";
                        worksheet.Cell(1, 9).Value = "Responsable";

                        var headerRange = worksheet.Range(1, 1, 1, 9);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#179002");
                        headerRange.Style.Font.FontColor = XLColor.White;

                        int row = 2;
                        foreach (var insumo in insumos)
                        {
                            worksheet.Cell(row, 1).Value = insumo.Id;
                            worksheet.Cell(row, 2).Value = insumo.Nombre;
                            worksheet.Cell(row, 3).Value = insumo.TipoDisplay;
                            worksheet.Cell(row, 4).Value = insumo.Cantidad;
                            worksheet.Cell(row, 5).Value = insumo.Unidad;
                            worksheet.Cell(row, 6).Value = insumo.StockMinimo;
                            worksheet.Cell(row, 7).Value = insumo.FechaIngreso.ToString("dd/MM/yyyy");
                            worksheet.Cell(row, 8).Value = insumo.Proveedor;
                            worksheet.Cell(row, 9).Value = insumo.Responsable;
                            row++;
                        }

                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(saveDialog.FileName);
                    }

                    DatabaseHelper.RegistrarHistorial("Insumos", "Exportar Excel",
                        "Inventario de insumos exportado", AuthenticationService.UsuarioActual?.Id);

                    MessageBox.Show($"✅ Archivo exportado exitosamente:\n{saveDialog.FileName}",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                    System.Diagnostics.Process.Start(saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbFiltroTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AplicarFiltros();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            AplicarFiltros();
        }

        private void AplicarFiltros()
        {
            if (DgInsumos == null) return;

            var vista = insumos.AsEnumerable();

            if (CmbFiltroTipo?.SelectedIndex > 0)
            {
                string tipoFiltro = (CmbFiltroTipo.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (tipoFiltro != null)
                {
                    tipoFiltro = tipoFiltro.Replace("🥣 ", "").Replace("🧰 ", "")
                        .Replace("💊 ", "").Replace("🛠️ ", "").Replace("📦 ", "").Trim();
                    vista = vista.Where(i => i.Tipo == tipoFiltro);
                }
            }

            if (!string.IsNullOrWhiteSpace(TxtBuscar?.Text) && TxtBuscar.Text != "🔍 Buscar insumo...")
            {
                string busqueda = TxtBuscar.Text.ToLower();
                vista = vista.Where(i =>
                    i.Nombre.ToLower().Contains(busqueda) ||
                    (i.Proveedor ?? "").ToLower().Contains(busqueda));
            }

            DgInsumos.ItemsSource = vista.ToList();
        }
    }

    // ================================================================
    //  MODELO
    // ================================================================

    public class InsumoModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Tipo { get; set; }
        public double Cantidad { get; set; }
        public string Unidad { get; set; }
        public double StockMinimo { get; set; }
        public DateTime FechaIngreso { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public string Proveedor { get; set; }
        public double PrecioUnitario { get; set; }
        public string Responsable { get; set; }
        public string Observaciones { get; set; }

        // ✅ switch expression reemplazado por if-else (compatible con C# 7/8)
        public string TipoDisplay
        {
            get
            {
                if (Tipo == "Alimento") return "🥣 Alimento";
                if (Tipo == "Herramienta") return "🧰 Herramienta";
                if (Tipo == "Medicamento") return "💊 Medicamento";
                if (Tipo == "Utensilio") return "🛠️ Utensilio";
                return "📦 Otro";
            }
        }

        public string EstadoTexto => Cantidad <= StockMinimo ? "Bajo" : "Normal";

        public Brush EstadoColor
        {
            get
            {
                return Cantidad <= StockMinimo
                    ? new SolidColorBrush(Color.FromRgb(244, 67, 54))
                    : new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }
        }
    }
}