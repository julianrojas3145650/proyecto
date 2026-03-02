using System;
using System.Windows;
using System.Windows.Input;
using Proyecto_senavicola.view.pages;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class RegistrarGalponDialog : Window
    {
        private GalponModel galponEditar;
        private bool esEdicion = false;

        public string Codigo { get; private set; }
        public string Nombre { get; private set; }
        public double Longitud { get; private set; }
        public string Raza { get; private set; }
        public double RacionPorAve { get; private set; }

        // Constructor para crear nuevo galpón
        public RegistrarGalponDialog()
        {
            InitializeComponent();
            txtCodigo.Text = $"GALPON-{DateTime.Now:yyyyMMddHHmmss}";
        }

        // Constructor para editar galpón existente
        public RegistrarGalponDialog(GalponModel galpon)
        {
            InitializeComponent();
            this.galponEditar = galpon;
            this.esEdicion = true;

            txtTitulo.Text = "✏️ Editar Galpón";
            btnRegistrar.Content = "Actualizar";

            // Cargar datos
            txtCodigo.Text = galpon.Codigo;
            txtCodigo.IsEnabled = false; // No permitir cambiar el código
            txtNombre.Text = galpon.Nombre;
            txtLongitud.Text = galpon.Longitud.ToString();
            txtRacion.Text = galpon.RacionPorAve.ToString();

            // Seleccionar raza
            foreach (System.Windows.Controls.ComboBoxItem item in cmbRaza.Items)
            {
                if (item.Content.ToString() == galpon.Raza)
                {
                    cmbRaza.SelectedItem = item;
                    break;
                }
            }
        }

        private void BtnRegistrar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarCampos())
                return;

            Codigo = txtCodigo.Text.Trim();
            Nombre = txtNombre.Text.Trim();
            Longitud = double.Parse(txtLongitud.Text);
            Raza = (cmbRaza.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString() ?? "Mixta";
            RacionPorAve = double.Parse(txtRacion.Text);

            DialogResult = true;
            Close();
        }

        private bool ValidarCampos()
        {
            if (string.IsNullOrWhiteSpace(txtCodigo.Text))
            {
                MessageBox.Show("Ingresa el código del galpón.", "Campo Requerido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCodigo.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtNombre.Text))
            {
                MessageBox.Show("Ingresa el nombre del galpón.", "Campo Requerido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNombre.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtLongitud.Text) ||
                !double.TryParse(txtLongitud.Text, out double longitud) ||
                longitud <= 0)
            {
                MessageBox.Show("Ingresa una longitud válida.", "Campo Requerido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtLongitud.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtRacion.Text) ||
                !double.TryParse(txtRacion.Text, out double racion) ||
                racion <= 0)
            {
                MessageBox.Show("Ingresa una ración válida.", "Campo Requerido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtRacion.Focus();
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
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"^[0-9.]+$");
        }
    }
}