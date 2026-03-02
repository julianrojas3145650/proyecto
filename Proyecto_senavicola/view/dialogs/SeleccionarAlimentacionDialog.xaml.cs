using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class SeleccionarAlimentoDialog : Window
    {
        public string AlimentoSeleccionado { get; private set; }

        public SeleccionarAlimentoDialog(List<string> alimentos, double cantidadNecesaria)
        {
            InitializeComponent();

            // Cargar lista de alimentos
            foreach (var alimento in alimentos)
            {
                CmbAlimentos.Items.Add(alimento);
            }

            if (alimentos.Count > 0)
                CmbAlimentos.SelectedIndex = 0;

            TxtCantidadNecesaria.Text = $"Cantidad necesaria: {cantidadNecesaria:F2} kg";
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            if (CmbAlimentos.SelectedItem == null)
            {
                MessageBox.Show("Por favor, selecciona un alimento.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AlimentoSeleccionado = CmbAlimentos.SelectedItem.ToString();
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