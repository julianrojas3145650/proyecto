using System;
using System.Windows;
using System.Windows.Input;
using Proyecto_senavicola.view.pages;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class RegistrarInsumoDialog : Window
    {
        private InsumoModel insumoOriginal;
        private bool esEdicion;

        // Propiedades públicas para acceder desde InsumosPage
        public string Nombre { get; private set; }
        public string Tipo { get; private set; }
        public double Cantidad { get; private set; }
        public string Unidad { get; private set; }
        public double StockMinimo { get; private set; }
        public DateTime? FechaVencimiento { get; private set; }
        public string Proveedor { get; private set; }
        public double PrecioUnitario { get; private set; }
        public string Observaciones { get; private set; }

        public RegistrarInsumoDialog()
        {
            InitializeComponent();
            esEdicion = false;
            txtTitulo.Text = "📦 Registrar Insumo";
            btnRegistrar.Content = "Registrar";
        }

        public RegistrarInsumoDialog(InsumoModel insumo)
        {
            InitializeComponent();
            esEdicion = true;
            insumoOriginal = insumo;
            txtTitulo.Text = "✏️ Editar Insumo";
            btnRegistrar.Content = "Actualizar";
            CargarDatos();
        }

        private void CargarDatos()
        {
            TxtNombre.Text = insumoOriginal.Nombre;

            // Seleccionar el tipo en el ComboBox
            switch (insumoOriginal.Tipo)
            {
                case "Alimento": CmbTipo.SelectedIndex = 0; break;
                case "Herramienta": CmbTipo.SelectedIndex = 1; break;
                case "Medicamento": CmbTipo.SelectedIndex = 2; break;
                case "Utensilio": CmbTipo.SelectedIndex = 3; break;
                default: CmbTipo.SelectedIndex = 4; break;
            }

            TxtCantidad.Text = insumoOriginal.Cantidad.ToString();
            CmbUnidad.Text = insumoOriginal.Unidad;
            TxtStockMinimo.Text = insumoOriginal.StockMinimo.ToString();
            DpVencimiento.SelectedDate = insumoOriginal.FechaVencimiento;
            TxtProveedor.Text = insumoOriginal.Proveedor;
            TxtPrecio.Text = insumoOriginal.PrecioUnitario.ToString();
            TxtObservaciones.Text = insumoOriginal.Observaciones;
        }

        private void BtnRegistrar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarCampos())
                return;

            Nombre = TxtNombre.Text.Trim();
            Tipo = (CmbTipo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString();
            Cantidad = double.Parse(TxtCantidad.Text);
            Unidad = (CmbUnidad.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString();
            StockMinimo = double.Parse(TxtStockMinimo.Text);
            FechaVencimiento = DpVencimiento.SelectedDate;
            Proveedor = TxtProveedor.Text.Trim();
            PrecioUnitario = double.Parse(TxtPrecio.Text);
            Observaciones = TxtObservaciones.Text.Trim();

            DialogResult = true;
            Close();
        }

        private bool ValidarCampos()
        {
            if (string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                MessageBox.Show("El nombre del insumo es obligatorio.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNombre.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(TxtCantidad.Text) ||
                !double.TryParse(TxtCantidad.Text, out double cantidad) ||
                cantidad < 0)
            {
                MessageBox.Show("La cantidad debe ser un número válido mayor o igual a 0.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCantidad.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(TxtStockMinimo.Text) ||
                !double.TryParse(TxtStockMinimo.Text, out double stockMin) ||
                stockMin < 0)
            {
                MessageBox.Show("El stock mínimo debe ser un número válido mayor o igual a 0.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtStockMinimo.Focus();
                return false;
            }

            if (!double.TryParse(TxtPrecio.Text, out double precio) || precio < 0)
            {
                MessageBox.Show("El precio debe ser un número válido mayor o igual a 0.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPrecio.Focus();
                return false;
            }

            return true;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, @"^[0-9.]+$");
        }
    }
}