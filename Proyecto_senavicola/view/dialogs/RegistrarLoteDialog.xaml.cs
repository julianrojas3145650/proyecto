using System;
using System.Windows;
using System.Windows.Input;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class RegistrarLoteDialog : Window
    {
        public string CodigoLote { get; private set; }
        public int Cantidad { get; private set; }
        public string Raza { get; private set; }
        public int EdadSemanas { get; private set; }
        public DateTime FechaLlegada { get; private set; }
        public string Proveedor { get; private set; }
        public string Observaciones { get; private set; }

        public RegistrarLoteDialog()
        {
            InitializeComponent();
            dpFecha.SelectedDate = DateTime.Today;

            // Generar código automático
            txtCodigo.Text = $"LOTE-{DateTime.Now:yyyyMMddHHmmss}";
        }

        private void BtnRegistrar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarCampos())
                return;

            CodigoLote = txtCodigo.Text.Trim();
            Cantidad = int.Parse(txtCantidad.Text);
            Raza = (cmbRaza.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString() ?? "Otra";
            EdadSemanas = string.IsNullOrWhiteSpace(txtEdad.Text) ? 0 : int.Parse(txtEdad.Text);
            FechaLlegada = dpFecha.SelectedDate ?? DateTime.Today;
            Proveedor = txtProveedor.Text.Trim();
            Observaciones = txtObservaciones.Text.Trim();

            DialogResult = true;
            Close();
        }

        private bool ValidarCampos()
        {
            if (string.IsNullOrWhiteSpace(txtCodigo.Text))
            {
                MessageBox.Show("Ingresa el código del lote.", "Campo Requerido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCodigo.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtCantidad.Text) || !int.TryParse(txtCantidad.Text, out int cantidad) || cantidad <= 0)
            {
                MessageBox.Show("Ingresa una cantidad válida.", "Campo Requerido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCantidad.Focus();
                return false;
            }

            if (!dpFecha.SelectedDate.HasValue)
            {
                MessageBox.Show("Selecciona la fecha de llegada.", "Campo Requerido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
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
            e.Handled = !IsTextNumeric(e.Text);
        }

        private bool IsTextNumeric(string text)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"^[0-9]+$");
        }
    }
}