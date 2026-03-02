using Proyecto_senavicola.view.pages;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class MotivoEliminacionDialog : Window
    {
        public string Motivo { get; private set; }
        public int Cantidad { get; private set; }
        public decimal PrecioUnitario { get; private set; }
        public string Cliente { get; private set; }
        public string MotivoDano { get; private set; }
        public string Observaciones { get; private set; }
        public bool Confirmado { get; private set; }
        public bool EsVendido => Motivo == "Vendido";

        private int _cantidadMaxima;
        private string _tipoHuevo;
        private string _lote;

        // Constructor actualizado para recibir el objeto HuevoInventario completo
        public MotivoEliminacionDialog(HuevoInventario huevo)
        {
            InitializeComponent();
            _cantidadMaxima = huevo.Cantidad;
            _tipoHuevo = huevo.Tipo;
            _lote = huevo.Lote;

            txtInfoHuevo.Text = $"Registro: {_tipoHuevo} - Lote: {_lote} - Galpón: {huevo.LoteAvesCodigo} | Cantidad disponible: {_cantidadMaxima} huevos";

            txtCantidad.TextChanged += TxtCantidad_TextChanged;
            txtPrecioUnitario.TextChanged += TxtPrecioUnitario_TextChanged;

            Confirmado = false;
            Motivo = "Vendido"; // Por defecto vendido
        }

        private void RbVendido_Checked(object sender, RoutedEventArgs e)
        {
            if (panelVendido != null && panelDanado != null)
            {
                panelVendido.Visibility = Visibility.Visible;
                panelDanado.Visibility = Visibility.Collapsed;
                Motivo = "Vendido";
            }
        }

        private void RbDanado_Checked(object sender, RoutedEventArgs e)
        {
            if (panelVendido != null && panelDanado != null)
            {
                panelVendido.Visibility = Visibility.Collapsed;
                panelDanado.Visibility = Visibility.Visible;
                Motivo = "Dañado";
            }
        }

        private void TxtCantidad_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            CalcularTotal();
        }

        private void TxtPrecioUnitario_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            CalcularTotal();
        }

        private void CalcularTotal()
        {
            if (txtTotal == null) return;

            if (int.TryParse(txtCantidad.Text, out int cantidad) &&
                decimal.TryParse(txtPrecioUnitario.Text, out decimal precio))
            {
                decimal total = cantidad * precio;
                txtTotal.Text = $"${total:N2}";
            }
            else
            {
                txtTotal.Text = "$0.00";
            }
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);

            // Evitar múltiples puntos decimales
            if (e.Text == "." && ((System.Windows.Controls.TextBox)sender).Text.Contains("."))
            {
                e.Handled = true;
            }
        }

        private void Confirmar_Click(object sender, RoutedEventArgs e)
        {
            // Validar cantidad
            if (string.IsNullOrWhiteSpace(txtCantidad.Text))
            {
                MessageBox.Show("Por favor ingresa la cantidad de huevos a eliminar.",
                    "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCantidad.Focus();
                return;
            }

            if (!int.TryParse(txtCantidad.Text, out int cantidad) || cantidad <= 0)
            {
                MessageBox.Show("La cantidad debe ser un número válido mayor a 0.",
                    "Cantidad inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCantidad.Focus();
                return;
            }

            // ⚠️ VALIDACIÓN CRÍTICA: No permitir cantidad mayor a la disponible
            if (cantidad > _cantidadMaxima)
            {
                MessageBox.Show($"❌ ERROR: La cantidad ingresada ({cantidad}) es MAYOR a la cantidad disponible en el registro ({_cantidadMaxima}).\n\n" +
                    $"Por favor ingresa una cantidad igual o menor a {_cantidadMaxima} huevos.",
                    "Cantidad excedida", MessageBoxButton.OK, MessageBoxImage.Error);
                txtCantidad.Focus();
                txtCantidad.SelectAll();
                return;
            }

            Cantidad = cantidad;

            // Validaciones específicas según el motivo
            if (rbVendido.IsChecked == true)
            {
                Motivo = "Vendido";

                if (string.IsNullOrWhiteSpace(txtPrecioUnitario.Text))
                {
                    MessageBox.Show("Por favor ingresa el precio unitario.",
                        "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtPrecioUnitario.Focus();
                    return;
                }

                if (!decimal.TryParse(txtPrecioUnitario.Text, out decimal precio) || precio <= 0)
                {
                    MessageBox.Show("El precio unitario debe ser un número válido mayor a 0.",
                        "Precio inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtPrecioUnitario.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtCliente.Text))
                {
                    MessageBox.Show("Por favor ingresa el nombre del cliente.",
                        "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtCliente.Focus();
                    return;
                }

                PrecioUnitario = precio;
                Cliente = txtCliente.Text.Trim();
            }
            else if (rbDanado.IsChecked == true)
            {
                Motivo = "Dañado";

                if (cmbMotivoDano.SelectedItem == null)
                {
                    MessageBox.Show("Por favor selecciona el motivo del daño.",
                        "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    cmbMotivoDano.Focus();
                    return;
                }

                MotivoDano = ((System.Windows.Controls.ComboBoxItem)cmbMotivoDano.SelectedItem).Content.ToString();
                Observaciones = txtObservaciones.Text.Trim();
            }

            // Confirmar la operación
            string accion = Motivo == "Vendido" ? "venta" : "registro de daño";
            string mensaje = Motivo == "Vendido"
                ? $"¿Confirmas la venta de {cantidad} huevos {_tipoHuevo}?\n\n" +
                  $"Cliente: {Cliente}\n" +
                  $"Precio unitario: ${PrecioUnitario:N2}\n" +
                  $"Total: ${PrecioUnitario * cantidad:N2}"
                : $"¿Confirmas el registro de {cantidad} huevos {_tipoHuevo} como dañados?\n\n" +
                  $"Motivo: {MotivoDano}";

            if (cantidad == _cantidadMaxima)
            {
                mensaje += "\n\n⚠️ ATENCIÓN: Esto eliminará COMPLETAMENTE el registro del inventario.";
            }
            else
            {
                mensaje += $"\n\n✅ Quedarán {_cantidadMaxima - cantidad} huevos en el inventario.";
            }

            var result = MessageBox.Show(mensaje, $"Confirmar {accion}",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Confirmado = true;
                DialogResult = true;
                Close();
            }
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            Confirmado = false;
            DialogResult = false;
            Close();
        }
    }

}