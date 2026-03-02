using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Proyecto_senavicola.models;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class GalponDialog : Window
    {
        public Galpon Galpon { get; private set; }
        private bool modoEdicion;

        public GalponDialog()
        {
            InitializeComponent();
            Galpon = new Galpon();
            modoEdicion = false;
            TxtTitulo.Text = "Nuevo Galpón";
        }

        public GalponDialog(Galpon galponExistente)
        {
            InitializeComponent();
            Galpon = galponExistente;
            modoEdicion = true;
            TxtTitulo.Text = "Editar Galpón";
            CargarDatos();
        }

        private void CargarDatos()
        {
            TxtNombre.Text = Galpon.Nombre;
            CmbRaza.Text = Galpon.Raza;
            TxtLongitud.Text = Galpon.Longitud.ToString();
            TxtRacion.Text = Galpon.RacionPorAve.ToString();
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarCampos())
                return;

            try
            {
                Galpon.Nombre = TxtNombre.Text.Trim();
                Galpon.Raza = CmbRaza.Text.Trim();
                Galpon.Longitud = double.Parse(TxtLongitud.Text);
                Galpon.RacionPorAve = double.Parse(TxtRacion.Text);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidarCampos()
        {
            if (string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                MessageBox.Show("El nombre del galpón es obligatorio.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNombre.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(CmbRaza.Text))
            {
                MessageBox.Show("La raza es obligatoria.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbRaza.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(TxtLongitud.Text) ||
                !double.TryParse(TxtLongitud.Text, out double longitud) || longitud <= 0)
            {
                MessageBox.Show("La longitud debe ser un número mayor a 0.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtLongitud.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(TxtRacion.Text) ||
                !double.TryParse(TxtRacion.Text, out double racion) || racion <= 0)
            {
                MessageBox.Show("La ración debe ser un número mayor a 0.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtRacion.Focus();
                return false;
            }

            return true;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NumeroDecimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}