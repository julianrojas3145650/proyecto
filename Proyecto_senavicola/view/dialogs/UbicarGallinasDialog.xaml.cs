using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Proyecto_senavicola.view.pages;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class UbicarGallinasDialog : Window
    {
        private LoteGallinasModel lote;
        private List<GalponModel> galpones;

        public GalponModel GalponSeleccionado { get; private set; }
        public int CantidadAsignar { get; private set; }

        public UbicarGallinasDialog(LoteGallinasModel lote, List<GalponModel> galpones)
        {
            InitializeComponent();
            this.lote = lote;
            this.galpones = galpones;

            CargarInformacion();
        }

        private void CargarInformacion()
        {
            txtInfoLote.Text = $"Lote: {lote.CodigoLote} - Raza: {lote.Raza}";
            txtCantidadPendiente.Text = $"Gallinas pendientes: {lote.CantidadPendiente}";

            cmbGalpon.ItemsSource = galpones;
        }

        private void CmbGalpon_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ValidarSeleccion();
        }

        private void TxtCantidad_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidarSeleccion();
        }

        private void ValidarSeleccion()
        {
            if (string.IsNullOrWhiteSpace(txtCantidad.Text) || cmbGalpon.SelectedItem == null)
            {
                borderAdvertencia.Visibility = Visibility.Collapsed;
                return;
            }

            if (int.TryParse(txtCantidad.Text, out int cantidad))
            {
                if (cantidad > lote.CantidadPendiente)
                {
                    borderAdvertencia.Visibility = Visibility.Visible;
                    txtAdvertencia.Text = $"La cantidad no puede ser mayor a las gallinas pendientes ({lote.CantidadPendiente}).";
                }
                else if (cantidad <= 0)
                {
                    borderAdvertencia.Visibility = Visibility.Visible;
                    txtAdvertencia.Text = "La cantidad debe ser mayor a cero.";
                }
                else
                {
                    borderAdvertencia.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void BtnTodasLasGallinas_Click(object sender, RoutedEventArgs e)
        {
            txtCantidad.Text = lote.CantidadPendiente.ToString();
        }

        private void BtnUbicar_Click(object sender, RoutedEventArgs e)
        {
            if (cmbGalpon.SelectedItem == null)
            {
                MessageBox.Show("Selecciona un galpón.", "Campo Requerido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtCantidad.Text) ||
                !int.TryParse(txtCantidad.Text, out int cantidad) ||
                cantidad <= 0)
            {
                MessageBox.Show("Ingresa una cantidad válida.", "Campo Requerido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCantidad.Focus();
                return;
            }

            if (cantidad > lote.CantidadPendiente)
            {
                MessageBox.Show($"La cantidad no puede ser mayor a {lote.CantidadPendiente}.",
                    "Cantidad Inválida",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GalponSeleccionado = cmbGalpon.SelectedItem as GalponModel;
            CantidadAsignar = cantidad;

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
            e.Handled = !IsTextNumeric(e.Text);
        }

        private bool IsTextNumeric(string text)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"^[0-9]+$");
        }
    }
}