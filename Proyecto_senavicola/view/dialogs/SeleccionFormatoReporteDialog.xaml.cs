using System.Windows;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class SeleccionFormatoReporteDialog : Window
    {
        public string FormatoSeleccionado { get; private set; }

        public SeleccionFormatoReporteDialog(string nombreReporte)
        {
            InitializeComponent();
            txtNombreReporte.Text = nombreReporte;
        }

        private void BtnPDF_Click(object sender, RoutedEventArgs e)
        {
            FormatoSeleccionado = "PDF";
            DialogResult = true;
            Close();
        }

        private void BtnExcel_Click(object sender, RoutedEventArgs e)
        {
            FormatoSeleccionado = "Excel";
            DialogResult = true;
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}