using System;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using ClosedXML.Excel;
using Microsoft.Win32;
using Proyecto_senavicola.data;
using Proyecto_senavicola.services;
using Proyecto_senavicola.view.dialogs;

namespace Proyecto_senavicola.view.pages
{
    public partial class ReportesPage : Page
    {
        public ReportesPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            navBar.SetModulo("📊 Reportes", "Genera y exporta reportes del sistema en PDF o Excel");
        }

        #region Reporte de Gallinas

        private void BtnReporteGallinas_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SeleccionFormatoReporteDialog("Reporte de Gallinas");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                if (dialog.FormatoSeleccionado == "Excel")
                {
                    GenerarReporteGallinasExcel();
                }
                else
                {
                    MessageBox.Show("La exportación a PDF estará disponible próximamente.",
                        "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void GenerarReporteGallinasExcel()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    FileName = $"Reporte_Gallinas_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        // Hoja 1: Galpones
                        var wsGalpones = workbook.Worksheets.Add("Galpones");
                        wsGalpones.Cell(1, 1).Value = "Código";
                        wsGalpones.Cell(1, 2).Value = "Nombre";
                        wsGalpones.Cell(1, 3).Value = "Raza";
                        wsGalpones.Cell(1, 4).Value = "Cantidad Aves";
                        wsGalpones.Cell(1, 5).Value = "Longitud (m)";
                        wsGalpones.Cell(1, 6).Value = "Ración/Ave (g)";
                        wsGalpones.Cell(1, 7).Value = "Fecha Creación";

                        int row = 2;
                        using (var conn = DatabaseHelper.GetConnection())
                        {
                            conn.Open();
                            string sql = @"SELECT Codigo, Nombre, Raza, CantidadAves, Longitud, 
                                          RacionPorAve, FechaCreacion FROM Galpones WHERE Activo = 1";
                            using (var cmd = new SQLiteCommand(sql, conn))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    wsGalpones.Cell(row, 1).Value = reader.GetString(0);
                                    wsGalpones.Cell(row, 2).Value = reader.GetString(1);
                                    wsGalpones.Cell(row, 3).Value = reader.GetString(2);
                                    wsGalpones.Cell(row, 4).Value = reader.GetInt32(3);
                                    wsGalpones.Cell(row, 5).Value = reader.GetDouble(4);
                                    wsGalpones.Cell(row, 6).Value = reader.GetDouble(5);
                                    wsGalpones.Cell(row, 7).Value = reader.GetDateTime(6).ToString("dd/MM/yyyy");
                                    row++;
                                }
                            }
                        }

                        // Hoja 2: Lotes de Gallinas
                        var wsLotes = workbook.Worksheets.Add("Lotes");
                        wsLotes.Cell(1, 1).Value = "Código Lote";
                        wsLotes.Cell(1, 2).Value = "Cantidad Total";
                        wsLotes.Cell(1, 3).Value = "Asignadas";
                        wsLotes.Cell(1, 4).Value = "Pendientes";
                        wsLotes.Cell(1, 5).Value = "Raza";
                        wsLotes.Cell(1, 6).Value = "Edad (sem)";
                        wsLotes.Cell(1, 7).Value = "Fecha Llegada";
                        wsLotes.Cell(1, 8).Value = "Estado";

                        row = 2;
                        using (var conn = DatabaseHelper.GetConnection())
                        {
                            conn.Open();
                            string sql = "SELECT * FROM LotesGallinas ORDER BY FechaLlegada DESC";
                            using (var cmd = new SQLiteCommand(sql, conn))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    wsLotes.Cell(row, 1).Value = reader.GetString(1);
                                    wsLotes.Cell(row, 2).Value = reader.GetInt32(2);
                                    wsLotes.Cell(row, 3).Value = reader.GetInt32(3);
                                    wsLotes.Cell(row, 4).Value = reader.GetInt32(4);
                                    wsLotes.Cell(row, 5).Value = reader.GetString(5);
                                    wsLotes.Cell(row, 6).Value = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
                                    wsLotes.Cell(row, 7).Value = reader.GetDateTime(7).ToString("dd/MM/yyyy");
                                    wsLotes.Cell(row, 8).Value = reader.GetString(10);
                                    row++;
                                }
                            }
                        }

                        // Estilo
                        wsGalpones.Columns().AdjustToContents();
                        wsLotes.Columns().AdjustToContents();

                        workbook.SaveAs(saveDialog.FileName);
                    }

                    DatabaseHelper.RegistrarHistorial("Reportes", "Exportar Excel",
                        "Reporte de Gallinas", AuthenticationService.UsuarioActual?.Id);

                    MessageBox.Show($"✅ Reporte generado:\n{saveDialog.FileName}",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                    System.Diagnostics.Process.Start(saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Reporte de Huevos

        private void BtnReporteHuevos_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SeleccionFormatoReporteDialog("Reporte de Huevos");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.FormatoSeleccionado == "Excel")
            {
                GenerarReporteHuevosExcel();
            }
        }

        private void GenerarReporteHuevosExcel()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    FileName = $"Reporte_Huevos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        // Inventario
                        var wsInv = workbook.Worksheets.Add("Inventario");
                        wsInv.Cell(1, 1).Value = "Lote";
                        wsInv.Cell(1, 2).Value = "Galpón";
                        wsInv.Cell(1, 3).Value = "Tipo";
                        wsInv.Cell(1, 4).Value = "Cantidad";
                        wsInv.Cell(1, 5).Value = "Peso Promedio";
                        wsInv.Cell(1, 6).Value = "Fecha";
                        wsInv.Cell(1, 7).Value = "Estado";

                        int row = 2;
                        using (var conn = DatabaseHelper.GetConnection())
                        {
                            conn.Open();
                            string sql = @"
                                SELECT LoteHuevos, GalponId, Tipo, Cantidad, PesoPromedio, 
                                       FechaRecoleccion, Estado 
                                FROM Huevos 
                                ORDER BY FechaRecoleccion DESC";

                            using (var cmd = new SQLiteCommand(sql, conn))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    wsInv.Cell(row, 1).Value = reader.GetString(0);
                                    wsInv.Cell(row, 2).Value = reader.GetInt32(1);
                                    wsInv.Cell(row, 3).Value = reader.GetString(2);
                                    wsInv.Cell(row, 4).Value = reader.GetInt32(3);
                                    wsInv.Cell(row, 5).Value = reader.GetDouble(4);
                                    wsInv.Cell(row, 6).Value = reader.GetDateTime(5).ToString("dd/MM/yyyy HH:mm");
                                    wsInv.Cell(row, 7).Value = reader.GetString(6);
                                    row++;
                                }
                            }
                        }

                        // Ventas
                        var wsVentas = workbook.Worksheets.Add("Ventas");
                        wsVentas.Cell(1, 1).Value = "Fecha Venta";
                        wsVentas.Cell(1, 2).Value = "Cliente";
                        wsVentas.Cell(1, 3).Value = "Cantidad";
                        wsVentas.Cell(1, 4).Value = "Precio Unit.";
                        wsVentas.Cell(1, 5).Value = "Total";

                        row = 2;
                        using (var conn = DatabaseHelper.GetConnection())
                        {
                            conn.Open();
                            string sql = "SELECT FechaVenta, Cliente, Cantidad, PrecioUnitario, Total FROM VentasHuevos ORDER BY FechaVenta DESC";
                            using (var cmd = new SQLiteCommand(sql, conn))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    wsVentas.Cell(row, 1).Value = reader.GetDateTime(0).ToString("dd/MM/yyyy");
                                    wsVentas.Cell(row, 2).Value = reader.GetString(1);
                                    wsVentas.Cell(row, 3).Value = reader.GetInt32(2);
                                    wsVentas.Cell(row, 4).Value = reader.GetDouble(3);
                                    wsVentas.Cell(row, 5).Value = reader.GetDouble(4);
                                    row++;
                                }
                            }
                        }

                        wsInv.Columns().AdjustToContents();
                        wsVentas.Columns().AdjustToContents();

                        workbook.SaveAs(saveDialog.FileName);
                    }

                    DatabaseHelper.RegistrarHistorial("Reportes", "Exportar Excel",
                        "Reporte de Huevos", AuthenticationService.UsuarioActual?.Id);

                    MessageBox.Show("✅ Reporte generado exitosamente", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    System.Diagnostics.Process.Start(saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Reporte de Insumos

        private void BtnReporteInsumos_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SeleccionFormatoReporteDialog("Reporte de Insumos");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.FormatoSeleccionado == "Excel")
            {
                GenerarReporteInsumosExcel();
            }
        }

        private void GenerarReporteInsumosExcel()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    FileName = $"Reporte_Insumos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var ws = workbook.Worksheets.Add("Insumos");
                        ws.Cell(1, 1).Value = "Nombre";
                        ws.Cell(1, 2).Value = "Tipo";
                        ws.Cell(1, 3).Value = "Cantidad";
                        ws.Cell(1, 4).Value = "Unidad";
                        ws.Cell(1, 5).Value = "Stock Mínimo";
                        ws.Cell(1, 6).Value = "Proveedor";
                        ws.Cell(1, 7).Value = "Precio Unit.";
                        ws.Cell(1, 8).Value = "Estado";

                        int row = 2;
                        using (var conn = DatabaseHelper.GetConnection())
                        {
                            conn.Open();
                            string sql = "SELECT * FROM Insumos ORDER BY Nombre";
                            using (var cmd = new SQLiteCommand(sql, conn))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    ws.Cell(row, 1).Value = reader.GetString(1);
                                    ws.Cell(row, 2).Value = reader.GetString(2);
                                    ws.Cell(row, 3).Value = reader.GetDouble(3);
                                    ws.Cell(row, 4).Value = reader.GetString(4);
                                    ws.Cell(row, 5).Value = reader.GetDouble(5);
                                    ws.Cell(row, 6).Value = reader.IsDBNull(8) ? "" : reader.GetString(8);
                                    ws.Cell(row, 7).Value = reader.IsDBNull(9) ? 0 : reader.GetDouble(9);

                                    double cantidad = reader.GetDouble(3);
                                    double stockMin = reader.GetDouble(5);
                                    ws.Cell(row, 8).Value = cantidad <= stockMin ? "BAJO STOCK" : "Normal";

                                    row++;
                                }
                            }
                        }

                        ws.Columns().AdjustToContents();
                        workbook.SaveAs(saveDialog.FileName);
                    }

                    DatabaseHelper.RegistrarHistorial("Reportes", "Exportar Excel",
                        "Reporte de Insumos", AuthenticationService.UsuarioActual?.Id);

                    MessageBox.Show("✅ Reporte generado", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    System.Diagnostics.Process.Start(saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Reporte de Producción

        private void BtnReporteProduccion_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SeleccionFormatoReporteDialog("Reporte de Producción");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.FormatoSeleccionado == "Excel")
            {
                GenerarReporteProduccionExcel();
            }
        }

        private void GenerarReporteProduccionExcel()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    FileName = $"Reporte_Produccion_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var ws = workbook.Worksheets.Add("Producción por Galpón");
                        ws.Cell(1, 1).Value = "Galpón";
                        ws.Cell(1, 2).Value = "Raza";
                        ws.Cell(1, 3).Value = "Total Huevos";
                        ws.Cell(1, 4).Value = "Jumbo";
                        ws.Cell(1, 5).Value = "AAA";
                        ws.Cell(1, 6).Value = "AA";
                        ws.Cell(1, 7).Value = "A";
                        ws.Cell(1, 8).Value = "B";
                        ws.Cell(1, 9).Value = "C";

                        int row = 2;
                        using (var conn = DatabaseHelper.GetConnection())
                        {
                            conn.Open();
                            string sql = @"
                                SELECT 
                                    g.Codigo,
                                    g.Raza,
                                    COALESCE(SUM(h.Cantidad), 0) as Total,
                                    COALESCE(SUM(CASE WHEN h.Tipo = 'Jumbo' THEN h.Cantidad ELSE 0 END), 0) as Jumbo,
                                    COALESCE(SUM(CASE WHEN h.Tipo = 'AAA' THEN h.Cantidad ELSE 0 END), 0) as AAA,
                                    COALESCE(SUM(CASE WHEN h.Tipo = 'AA' THEN h.Cantidad ELSE 0 END), 0) as AA,
                                    COALESCE(SUM(CASE WHEN h.Tipo = 'A' THEN h.Cantidad ELSE 0 END), 0) as A,
                                    COALESCE(SUM(CASE WHEN h.Tipo = 'B' THEN h.Cantidad ELSE 0 END), 0) as B,
                                    COALESCE(SUM(CASE WHEN h.Tipo = 'C' THEN h.Cantidad ELSE 0 END), 0) as C
                                FROM Galpones g
                                LEFT JOIN Huevos h ON g.Id = h.GalponId
                                WHERE g.Activo = 1
                                GROUP BY g.Id, g.Codigo, g.Raza
                                ORDER BY Total DESC";

                            using (var cmd = new SQLiteCommand(sql, conn))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    ws.Cell(row, 1).Value = reader.GetString(0);
                                    ws.Cell(row, 2).Value = reader.GetString(1);
                                    ws.Cell(row, 3).Value = reader.GetInt32(2);
                                    ws.Cell(row, 4).Value = reader.GetInt32(3);
                                    ws.Cell(row, 5).Value = reader.GetInt32(4);
                                    ws.Cell(row, 6).Value = reader.GetInt32(5);
                                    ws.Cell(row, 7).Value = reader.GetInt32(6);
                                    ws.Cell(row, 8).Value = reader.GetInt32(7);
                                    ws.Cell(row, 9).Value = reader.GetInt32(8);
                                    row++;
                                }
                            }
                        }

                        ws.Columns().AdjustToContents();
                        workbook.SaveAs(saveDialog.FileName);
                    }

                    DatabaseHelper.RegistrarHistorial("Reportes", "Exportar Excel",
                        "Reporte de Producción", AuthenticationService.UsuarioActual?.Id);

                    MessageBox.Show("✅ Reporte generado", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    System.Diagnostics.Process.Start(saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Reporte Financiero

        private void BtnReporteFinanciero_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SeleccionFormatoReporteDialog("Reporte Financiero");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.FormatoSeleccionado == "Excel")
            {
                GenerarReporteFinancieroExcel();
            }
        }

        private void GenerarReporteFinancieroExcel()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    FileName = $"Reporte_Financiero_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var ws = workbook.Worksheets.Add("Resumen Financiero");
                        ws.Cell(1, 1).Value = "REPORTE FINANCIERO";
                        ws.Cell(1, 1).Style.Font.Bold = true;
                        ws.Cell(1, 1).Style.Font.FontSize = 16;

                        ws.Cell(3, 1).Value = "Mes";
                        ws.Cell(3, 2).Value = "Total Ventas";
                        ws.Cell(3, 3).Value = "Cantidad Vendida";
                        ws.Cell(3, 4).Value = "Precio Promedio";

                        int row = 4;
                        using (var conn = DatabaseHelper.GetConnection())
                        {
                            conn.Open();
                            string sql = @"
                                SELECT 
                                    strftime('%Y-%m', FechaVenta) as Mes,
                                    SUM(Total) as TotalVentas,
                                    SUM(Cantidad) as CantidadVendida,
                                    AVG(PrecioUnitario) as PrecioPromedio
                                FROM VentasHuevos
                                GROUP BY strftime('%Y-%m', FechaVenta)
                                ORDER BY Mes DESC";

                            using (var cmd = new SQLiteCommand(sql, conn))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    ws.Cell(row, 1).Value = reader.GetString(0);
                                    ws.Cell(row, 2).Value = reader.GetDouble(1);
                                    ws.Cell(row, 3).Value = reader.GetInt32(2);
                                    ws.Cell(row, 4).Value = reader.GetDouble(3);
                                    row++;
                                }
                            }
                        }

                        ws.Columns().AdjustToContents();
                        workbook.SaveAs(saveDialog.FileName);
                    }

                    DatabaseHelper.RegistrarHistorial("Reportes", "Exportar Excel",
                        "Reporte Financiero", AuthenticationService.UsuarioActual?.Id);

                    MessageBox.Show("✅ Reporte generado", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    System.Diagnostics.Process.Start(saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Historial General

        private void BtnHistorialGeneral_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SeleccionFormatoReporteDialog("Historial General");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.FormatoSeleccionado == "Excel")
            {
                GenerarHistorialGeneralExcel();
            }
        }

        private void GenerarHistorialGeneralExcel()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    FileName = $"Historial_General_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var ws = workbook.Worksheets.Add("Historial");
                        ws.Cell(1, 1).Value = "Fecha/Hora";
                        ws.Cell(1, 2).Value = "Módulo";
                        ws.Cell(1, 3).Value = "Acción";
                        ws.Cell(1, 4).Value = "Detalle";
                        ws.Cell(1, 5).Value = "Usuario";

                        int row = 2;
                        using (var conn = DatabaseHelper.GetConnection())
                        {
                            conn.Open();
                            string sql = @"
                                SELECT 
                                    h.FechaHora,
                                    h.Modulo,
                                    h.Accion,
                                    h.Detalle,
                                    COALESCE(u.Nombre || ' ' || u.Apellido, 'Sistema') as Usuario
                                FROM Historial h
                                LEFT JOIN Usuarios u ON h.UsuarioId = u.Id
                                ORDER BY h.FechaHora DESC
                                LIMIT 1000";

                            using (var cmd = new SQLiteCommand(sql, conn))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    ws.Cell(row, 1).Value = reader.GetDateTime(0).ToString("dd/MM/yyyy HH:mm:ss");
                                    ws.Cell(row, 2).Value = reader.GetString(1);
                                    ws.Cell(row, 3).Value = reader.GetString(2);
                                    ws.Cell(row, 4).Value = reader.IsDBNull(3) ? "" : reader.GetString(3);
                                    ws.Cell(row, 5).Value = reader.GetString(4);
                                    row++;
                                }
                            }
                        }

                        ws.Columns().AdjustToContents();
                        workbook.SaveAs(saveDialog.FileName);
                    }

                    DatabaseHelper.RegistrarHistorial("Reportes", "Exportar Excel",
                        "Historial General", AuthenticationService.UsuarioActual?.Id);

                    MessageBox.Show("✅ Reporte generado", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    System.Diagnostics.Process.Start(saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}