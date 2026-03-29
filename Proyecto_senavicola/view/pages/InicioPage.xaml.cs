using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Proyecto_senavicola.data;

namespace Proyecto_senavicola.view.pages
{
    public partial class InicioPage : Page
    {
        public InicioPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            navBar.SetModulo("🐔 Inicio", "Gestiona tus lotes y galpones de gallinas");
            CargarDashboard();
        }

        private void CargarDashboard()
        {
            try
            {
                CargarMetricasPrincipales();
                CargarGraficoProduccion();
                CargarEstadoGalpones();
                CargarActividadReciente();
                CargarEventosProximos();
                CargarResumenSistema();

                txtFechaActualizacion.Text = $"Última actualización: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el dashboard: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Métricas Principales

        private void CargarMetricasPrincipales()
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                // Total de huevos disponibles
                string sqlHuevos = @"SELECT COALESCE(SUM(Cantidad), 0) FROM Huevos 
                                    WHERE Estado = 'Disponible'";
                using (var cmd = new SQLiteCommand(sqlHuevos, conn))
                {
                    int totalHuevos = Convert.ToInt32(cmd.ExecuteScalar());
                    txtTotalHuevos.Text = totalHuevos.ToString("N0");
                    txtHuevosInfo.Text = $"{totalHuevos} en inventario";
                }

                // Total de gallinas activas
                string sqlGallinas = @"SELECT COALESCE(SUM(CantidadAves), 0) FROM Galpones 
                                      WHERE Activo = 1";
                using (var cmd = new SQLiteCommand(sqlGallinas, conn))
                {
                    int totalGallinas = Convert.ToInt32(cmd.ExecuteScalar());
                    txtTotalGallinas.Text = totalGallinas.ToString("N0");
                    txtGallinasInfo.Text = $"{totalGallinas} en producción";
                }

                // Ventas del mes
                string sqlVentas = @"SELECT 
                                    COALESCE(SUM(Total), 0) as TotalVentas,
                                    COALESCE(SUM(Cantidad), 0) as TotalCantidad
                                    FROM VentasHuevos 
                                    WHERE strftime('%Y-%m', FechaVenta) = strftime('%Y-%m', 'now')";
                using (var cmd = new SQLiteCommand(sqlVentas, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        decimal totalVentas = Convert.ToDecimal(reader["TotalVentas"]);
                        int cantidadVendida = Convert.ToInt32(reader["TotalCantidad"]);
                        txtVentasMes.Text = totalVentas.ToString("C0");
                        txtVentasInfo.Text = $"{cantidadVendida} huevos vendidos";
                    }
                }

                // Alertas (tareas de alimentación pendientes + stock bajo de insumos)
                string sqlAlertas = @"
                    SELECT 
                        (SELECT COUNT(*) FROM Alimentacion WHERE Realizado = 0 AND DATE(Fecha) <= DATE('now')) +
                        (SELECT COUNT(*) FROM Insumos WHERE Cantidad <= StockMinimo) as TotalAlertas";
                using (var cmd = new SQLiteCommand(sqlAlertas, conn))
                {
                    int alertas = Convert.ToInt32(cmd.ExecuteScalar());
                    txtAlertas.Text = alertas.ToString();
                    txtAlertasInfo.Text = alertas == 0 ? "Sin alertas" :
                                         alertas == 1 ? "1 tarea pendiente" :
                                         $"{alertas} tareas pendientes";
                }
            }
        }

        #endregion

        #region Gráfico de Producción

        private void CargarGraficoProduccion()
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                // Obtener cantidades por tipo
                string sql = @"SELECT 
                                Tipo,
                                COALESCE(SUM(Cantidad), 0) as Total
                               FROM Huevos 
                               WHERE Estado = 'Disponible'
                               GROUP BY Tipo";

                var datos = new Dictionary<string, int>
                {
                    {"Jumbo", 0}, {"AAA", 0}, {"AA", 0}, {"A", 0}, {"B", 0}, {"C", 0}
                };

                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string tipo = reader["Tipo"].ToString();
                        int total = Convert.ToInt32(reader["Total"]);
                        if (datos.ContainsKey(tipo))
                            datos[tipo] = total;
                    }
                }

                // Encontrar el valor máximo para escalar las barras
                int maxValor = datos.Values.Max();
                if (maxValor == 0) maxValor = 1; // Evitar división por cero

                double alturaMaxima = 180; // Altura máxima de las barras en píxeles

                // Actualizar barras
                ActualizarBarra(barJumbo, txtBarJumbo, datos["Jumbo"], maxValor, alturaMaxima);
                ActualizarBarra(barAAA, txtBarAAA, datos["AAA"], maxValor, alturaMaxima);
                ActualizarBarra(barAA, txtBarAA, datos["AA"], maxValor, alturaMaxima);
                ActualizarBarra(barA, txtBarA, datos["A"], maxValor, alturaMaxima);
                ActualizarBarra(barB, txtBarB, datos["B"], maxValor, alturaMaxima);
                ActualizarBarra(barC, txtBarC, datos["C"], maxValor, alturaMaxima);
            }
        }

        private void ActualizarBarra(Border barra, TextBlock texto, int valor, int maxValor, double alturaMaxima)
        {
            double altura = maxValor > 0 ? (valor * alturaMaxima / maxValor) : 0;
            altura = Math.Max(altura, 30); // Altura mínima para que se vea el texto

            barra.Height = altura;
            texto.Text = valor.ToString("N0");
        }

        #endregion

        #region Estado de Galpones

        private void CargarEstadoGalpones()
        {
            stackGalpones.Children.Clear();

            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                string sql = @"SELECT 
                                Codigo,
                                Nombre,
                                CantidadAves,
                                Raza
                               FROM Galpones 
                               WHERE Activo = 1
                               ORDER BY Codigo
                               LIMIT 5";

                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    int count = 0;
                    while (reader.Read())
                    {
                        count++;
                        string codigo = reader["Codigo"].ToString();
                        string nombre = reader["Nombre"].ToString();
                        int cantidad = Convert.ToInt32(reader["CantidadAves"]);
                        string raza = reader["Raza"].ToString();

                        var galponItem = CrearItemGalpon(codigo, nombre, cantidad, raza);
                        stackGalpones.Children.Add(galponItem);
                    }

                    if (count == 0)
                    {
                        var sinDatos = new TextBlock
                        {
                            Text = "No hay galpones registrados",
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999")),
                            FontStyle = FontStyles.Italic,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 20, 0, 0)
                        };
                        stackGalpones.Children.Add(sinDatos);
                    }
                }
            }
        }

        private Border CrearItemGalpon(string codigo, string nombre, int cantidad, string raza)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9F9F9")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stackInfo = new StackPanel();

            var txtCodigo = new TextBlock
            {
                Text = $"🏠 {nombre}",
                FontWeight = FontWeights.Bold,
                FontSize = 13
            };

            var txtRaza = new TextBlock
            {
                Text = raza,
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                Margin = new Thickness(0, 2, 0, 0)
            };

            stackInfo.Children.Add(txtCodigo);
            stackInfo.Children.Add(txtRaza);

            var txtCantidad = new TextBlock
            {
                Text = $"{cantidad} 🐔",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#179002")),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(stackInfo, 0);
            Grid.SetColumn(txtCantidad, 1);

            grid.Children.Add(stackInfo);
            grid.Children.Add(txtCantidad);
            border.Child = grid;

            return border;
        }

        #endregion

        #region Actividad Reciente

        private void CargarActividadReciente()
        {
            stackActividad.Children.Clear();

            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                string sql = @"SELECT 
                                Modulo,
                                Accion,
                                Detalle,
                                FechaHora
                               FROM Historial 
                               ORDER BY FechaHora DESC
                               LIMIT 10";

                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    int count = 0;
                    while (reader.Read())
                    {
                        count++;
                        string modulo = reader["Modulo"].ToString();
                        string accion = reader["Accion"].ToString();
                        string detalle = reader["Detalle"]?.ToString() ?? "";
                        DateTime fecha = Convert.ToDateTime(reader["FechaHora"]);

                        var actividadItem = CrearItemActividad(modulo, accion, detalle, fecha);
                        stackActividad.Children.Add(actividadItem);
                    }

                    if (count == 0)
                    {
                        var sinDatos = new TextBlock
                        {
                            Text = "No hay actividad reciente",
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999")),
                            FontStyle = FontStyles.Italic,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 20, 0, 0)
                        };
                        stackActividad.Children.Add(sinDatos);
                    }
                }
            }
        }

        private Border CrearItemActividad(string modulo, string accion, string detalle, DateTime fecha)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9F9F9")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var stack = new StackPanel();

            var txtAccion = new TextBlock
            {
                Text = $"{ObtenerIconoModulo(modulo)} {accion}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            };

            var txtFecha = new TextBlock
            {
                Text = ObtenerTiempoTranscurrido(fecha),
                FontSize = 9,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999")),
                Margin = new Thickness(0, 2, 0, 0)
            };

            stack.Children.Add(txtAccion);
            stack.Children.Add(txtFecha);

            if (!string.IsNullOrEmpty(detalle))
            {
                var txtDetalle = new TextBlock
                {
                    Text = detalle,
                    FontSize = 9,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0)
                };
                stack.Children.Add(txtDetalle);
            }

            border.Child = stack;
            return border;
        }

        private string ObtenerIconoModulo(string modulo)
        {
            return modulo switch
            {
                "Huevos" => "🥚",
                "Gallinas" => "🐔",
                "Insumos" => "📦",
                "Ventas" => "💰",
                "Galpones" => "🏠",
                "Alimentación" => "🍽️",
                _ => "📋"
            };
        }

        private string ObtenerTiempoTranscurrido(DateTime fecha)
        {
            var diferencia = DateTime.Now - fecha;

            if (diferencia.TotalMinutes < 1)
                return "Hace unos segundos";
            if (diferencia.TotalMinutes < 60)
                return $"Hace {(int)diferencia.TotalMinutes} minutos";
            if (diferencia.TotalHours < 24)
                return $"Hace {(int)diferencia.TotalHours} horas";
            if (diferencia.TotalDays < 7)
                return $"Hace {(int)diferencia.TotalDays} días";

            return fecha.ToString("dd/MM/yyyy");
        }

        #endregion

        #region Eventos Próximos

        private void CargarEventosProximos()
        {
            stackEventos.Children.Clear();

            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                // Tareas de alimentación pendientes de hoy
                string sqlAlimentacion = @"SELECT 
                                            G.Nombre as GalponNombre,
                                            A.Turno,
                                            A.Fecha
                                           FROM Alimentacion A
                                           INNER JOIN Galpones G ON A.GalponId = G.Id
                                           WHERE A.Realizado = 0 
                                           AND DATE(A.Fecha) = DATE('now')
                                           ORDER BY A.Turno
                                           LIMIT 5";

                using (var cmd = new SQLiteCommand(sqlAlimentacion, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    bool hayEventos = false;
                    while (reader.Read())
                    {
                        if (!hayEventos)
                        {
                            stackEventos.Children.Clear();
                            hayEventos = true;
                        }

                        string galpon = reader["GalponNombre"].ToString();
                        string turno = reader["Turno"].ToString();

                        var eventoItem = CrearItemEvento(
                            "🍽️ Alimentación pendiente",
                            $"{galpon} - Turno {turno}",
                            "#FF9800"
                        );
                        stackEventos.Children.Add(eventoItem);
                    }

                    if (!hayEventos)
                    {
                        var sinEventos = new TextBlock
                        {
                            Text = "✅ No hay tareas pendientes para hoy",
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#179002")),
                            FontStyle = FontStyles.Italic,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 20, 0, 0)
                        };
                        stackEventos.Children.Add(sinEventos);
                    }
                }
            }
        }

        private Border CrearItemEvento(string titulo, string descripcion, string color)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9F9F9")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                BorderThickness = new Thickness(3, 0, 0, 0),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();

            var txtTitulo = new TextBlock
            {
                Text = titulo,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            };

            var txtDescripcion = new TextBlock
            {
                Text = descripcion,
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                Margin = new Thickness(0, 3, 0, 0)
            };

            stack.Children.Add(txtTitulo);
            stack.Children.Add(txtDescripcion);
            border.Child = stack;

            return border;
        }

        #endregion

        #region Resumen del Sistema

        private void CargarResumenSistema()
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                // Total galpones activos
                string sqlGalpones = "SELECT COUNT(*) FROM Galpones WHERE Activo = 1";
                using (var cmd = new SQLiteCommand(sqlGalpones, conn))
                {
                    txtResumenGalpones.Text = cmd.ExecuteScalar().ToString();
                }

                // Lotes activos
                string sqlLotes = "SELECT COUNT(*) FROM LotesGallinas WHERE Estado != 'Completo'";
                using (var cmd = new SQLiteCommand(sqlLotes, conn))
                {
                    txtResumenLotes.Text = cmd.ExecuteScalar().ToString();
                }

                // Inventario total
                string sqlInventario = "SELECT COALESCE(SUM(Cantidad), 0) FROM Huevos WHERE Estado = 'Disponible'";
                using (var cmd = new SQLiteCommand(sqlInventario, conn))
                {
                    int total = Convert.ToInt32(cmd.ExecuteScalar());
                    txtResumenInventario.Text = $"{total:N0} huevos";
                }

                // Ventas de hoy
                string sqlVentasHoy = @"SELECT COALESCE(SUM(Total), 0) FROM VentasHuevos 
                                       WHERE DATE(FechaVenta) = DATE('now')";
                using (var cmd = new SQLiteCommand(sqlVentasHoy, conn))
                {
                    decimal total = Convert.ToDecimal(cmd.ExecuteScalar());
                    txtResumenVentasHoy.Text = total.ToString("C0");
                }

                // Huevos dañados (total histórico)
                string sqlDanados = "SELECT COALESCE(SUM(Cantidad), 0) FROM HuevosDanados";
                using (var cmd = new SQLiteCommand(sqlDanados, conn))
                {
                    txtResumenDanados.Text = cmd.ExecuteScalar().ToString();
                }
            }
        }

        #endregion

        #region Eventos de Botones de Acciones Rápidas

        private void BtnRegistrarHuevos_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new HuevosPage());
        }

        private void BtnGestionGallinas_Click(object sender, RoutedEventArgs e)
        {
            // Navegar a la página de gallinas
            MessageBox.Show("Navegando a Gestión de Gallinas...", "Información",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnReportes_Click(object sender, RoutedEventArgs e)
        {
            // Navegar a la página de reportes
            MessageBox.Show("Navegando a Reportes...", "Información",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnInsumos_Click(object sender, RoutedEventArgs e)
        {
            // Navegar a la página de insumos
            MessageBox.Show("Navegando a Gestión de Insumos...", "Información",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAlimentacion_Click(object sender, RoutedEventArgs e)
        {
            // Navegar a la página de alimentación
            MessageBox.Show("Navegando a Control de Alimentación...", "Información",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnVentas_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new HuevosPage());
        }

        private void BtnConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            // Navegar a configuración
            MessageBox.Show("Navegando a Configuración...", "Información",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            CargarDashboard();
            MessageBox.Show("Dashboard actualizado correctamente", "Éxito",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion
    }
}