using System;
using System.Windows;
using AForge.Video.DirectShow;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class SeleccionCamaraDialog : Window
    {
        // ✅ CAMBIO: Devolver FilterInfo en lugar de int
        public FilterInfo CamaraSeleccionada { get; private set; }

        public SeleccionCamaraDialog(FilterInfoCollection camaras)
        {
            InitializeComponent();

            // Cargar las cámaras disponibles
            foreach (FilterInfo camara in camaras)
            {
                lstCamaras.Items.Add(camara);
            }

            // Seleccionar la primera por defecto
            if (lstCamaras.Items.Count > 0)
            {
                lstCamaras.SelectedIndex = 0;
            }
        }

        private void LstCamaras_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            btnSeleccionar.IsEnabled = lstCamaras.SelectedIndex >= 0;
        }

        private void Seleccionar_Click(object sender, RoutedEventArgs e)
        {
            if (lstCamaras.SelectedIndex >= 0)
            {
                // ✅ CAMBIO: Asignar el objeto FilterInfo completo
                CamaraSeleccionada = lstCamaras.SelectedItem as FilterInfo;
                DialogResult = true;
                Close();
            }
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}