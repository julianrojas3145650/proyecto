using System.Windows;
using System.Windows.Input;
using Proyecto_senavicola.view.pages;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class ReabastecerInsumoDialog : Window
    {
        public double Cantidad { get; private set; }
        public string Observaciones { get; private set; }

        public ReabastecerInsumoDialog(InsumoModel insumo)
        {
            InitializeComponent();
            txtNombreInsumo.Text = insumo.Nombre;
            txtCantidadActual.Text = $"Cantidad actual: {insumo.Cantidad} {insumo.Unidad}";
        }

        private void BtnReabastecer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCantidad.Text) ||
                !double.TryParse(txtCantidad.Text, out double cantidad) ||
                cantidad <= 0)
            {
                MessageBox.Show("Ingresa una cantidad válida mayor a 0.", "Campo Requerido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCantidad.Focus();
                return;
            }

            Cantidad = cantidad;
            Observaciones = txtObservaciones.Text.Trim();
            DialogResult = true;
            Close();
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