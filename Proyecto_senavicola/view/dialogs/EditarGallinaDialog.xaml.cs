using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Proyecto_senavicola.models;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class EditarGallinaDialog : Window
    {
        private Gallina gallina;

        public EditarGallinaDialog(Gallina gallinaExistente)
        {
            InitializeComponent();
            gallina = gallinaExistente;
            CargarDatos();
        }

        private void CargarDatos()
        {
            TxtId.Text = gallina.Id.ToString();
            TxtNombre.Text = gallina.Nombre;
            CmbEstado.Text = gallina.Estado;
            TxtPeso.Text = gallina.Peso?.ToString() ?? "";
            TxtFechaIngreso.Text = gallina.FechaIngreso.ToString("dd/MM/yyyy HH:mm");
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarCampos())
                return;

            try
            {
                gallina.Nombre = TxtNombre.Text.Trim();
                gallina.Estado = CmbEstado.Text;

                if (!string.IsNullOrWhiteSpace(TxtPeso.Text))
                {
                    gallina.Peso = double.Parse(TxtPeso.Text);
                }
                else
                {
                    gallina.Peso = null;
                }

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
                MessageBox.Show("El nombre de la gallina es obligatorio.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNombre.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(CmbEstado.Text))
            {
                MessageBox.Show("El estado es obligatorio.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbEstado.Focus();
                return false;
            }

            if (!string.IsNullOrWhiteSpace(TxtPeso.Text))
            {
                if (!double.TryParse(TxtPeso.Text, out double peso) || peso <= 0)
                {
                    MessageBox.Show("El peso debe ser un número mayor a 0.", "Validación",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtPeso.Focus();
                    return false;
                }
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
            // Permitir solo números y punto decimal
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}