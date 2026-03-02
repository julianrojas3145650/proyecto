using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class EditarHuevoDialog : Window
    {
        private view.pages.HuevoInventario huevo;
        private string tipoOriginal;

        public int Cantidad { get; private set; }
        public double Peso { get; private set; }

        public EditarHuevoDialog(view.pages.HuevoInventario huevo)
        {
            InitializeComponent();
            this.huevo = huevo;
            this.tipoOriginal = huevo.Tipo;

            txtInfoHuevo.Text = $"Editando huevo del lote {huevo.Lote}";
            txtTipo.Text = huevo.Tipo;
            txtCantidad.Text = huevo.Cantidad.ToString();
            txtPeso.Text = huevo.PesoPromedio.ToString("F2");
        }

        private void TxtPeso_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(txtPeso.Text, out double peso) && peso > 0)
            {
                string nuevoTipo = ClasificarPorPeso(peso);

                if (nuevoTipo != tipoOriginal)
                {
                    borderNuevoTipo.Visibility = Visibility.Visible;
                    txtNuevoTipo.Text = $"El nuevo peso ({peso:F2}g) corresponde a la categoría '{nuevoTipo}'. " +
                                       $"El huevo se reclasificará automáticamente.";
                }
                else
                {
                    borderNuevoTipo.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                borderNuevoTipo.Visibility = Visibility.Collapsed;
            }
        }

        private string ClasificarPorPeso(double peso)
        {
            if (peso > 78) return "Jumbo";
            if (peso >= 67) return "AAA";
            if (peso >= 60) return "AA";
            if (peso >= 53) return "A";
            if (peso >= 46) return "B";
            return "C";
        }

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            // Validar cantidad
            if (!int.TryParse(txtCantidad.Text, out int cantidad) || cantidad <= 0)
            {
                MessageBox.Show("Por favor, ingresa una cantidad válida (mayor a 0).",
                    "Campo Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCantidad.Focus();
                return;
            }

            // Validar peso
            if (!double.TryParse(txtPeso.Text, out double peso) || peso <= 0)
            {
                MessageBox.Show("Por favor, ingresa un peso válido (mayor a 0).",
                    "Campo Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPeso.Focus();
                return;
            }

            // Verificar si hay cambio de clasificación
            string nuevoTipo = ClasificarPorPeso(peso);
            if (nuevoTipo != tipoOriginal)
            {
                var result = MessageBox.Show(
                    $"El nuevo peso ({peso:F2}g) cambiará la clasificación de '{tipoOriginal}' a '{nuevoTipo}'.\n\n" +
                    "¿Deseas continuar con esta reclasificación?",
                    "Cambio de Clasificación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            Cantidad = cantidad;
            Peso = peso;

            DialogResult = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
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