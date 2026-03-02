using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Proyecto_senavicola.models;

namespace Proyecto_senavicola.view.dialogs
{
    public partial class AgregarGallinasDialog : Window
    {
        public List<Gallina> Gallinas { get; private set; }
        private Galpon galpon;
        private ObservableCollection<GallinaTemp> gallinasTemp;
        private int siguienteId;

        public AgregarGallinasDialog(Galpon galponSeleccionado)
        {
            InitializeComponent();
            galpon = galponSeleccionado;
            Gallinas = new List<Gallina>();
            gallinasTemp = new ObservableCollection<GallinaTemp>();

            TxtGalponInfo.Text = $"Galpón: {galpon.Nombre} - {galpon.Raza}";

            // Calcular el siguiente ID para las nuevas gallinas
            siguienteId = galpon.Gallinas.Count > 0 ? galpon.Gallinas.Max(g => g.Id) + 1 : 1;

            ListaGallinas.ItemsSource = gallinasTemp;
        }

        private void BtnGenerarNombres_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtCantidad.Text, out int cantidad) || cantidad <= 0)
            {
                MessageBox.Show("Por favor, ingresa una cantidad válida mayor a 0.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cantidad > 1000)
            {
                MessageBox.Show("La cantidad máxima permitida es 1000 gallinas por vez.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var resultado = MessageBox.Show(
                $"Se generarán {cantidad} campos para registrar gallinas.\n¿Deseas continuar?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resultado == MessageBoxResult.Yes)
            {
                GenerarCamposGallinas(cantidad);
            }
        }

        private void GenerarCamposGallinas(int cantidad)
        {
            gallinasTemp.Clear();

            // Obtener el número de la última gallina del galpón para continuar la secuencia
            int numeroInicial = 1;
            if (galpon.Gallinas.Count > 0)
            {
                // Buscar el número más alto en los nombres existentes
                foreach (var g in galpon.Gallinas)
                {
                    var match = Regex.Match(g.Nombre, @"Gallina (\d+)");
                    if (match.Success)
                    {
                        int num = int.Parse(match.Groups[1].Value);
                        if (num >= numeroInicial)
                            numeroInicial = num + 1;
                    }
                }
            }

            // Generar los campos con nombres secuenciales
            for (int i = 0; i < cantidad; i++)
            {
                gallinasTemp.Add(new GallinaTemp
                {
                    Numero = i + 1,
                    Nombre = $"Gallina {numeroInicial + i}",
                    Estado = "Activa",
                    Peso = null
                });
            }

            TxtTotalGallinas.Text = cantidad.ToString();
        }

        private void TxtCantidad_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Opcional: Auto-generar cuando cambie la cantidad
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (gallinasTemp.Count == 0)
            {
                MessageBox.Show("Debes generar al menos una gallina para guardar.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validar que todos los nombres estén completos
            var nombreVacio = gallinasTemp.FirstOrDefault(g => string.IsNullOrWhiteSpace(g.Nombre));
            if (nombreVacio != null)
            {
                MessageBox.Show($"La gallina #{nombreVacio.Numero} no tiene nombre. Por favor, completa todos los nombres.",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validar nombres duplicados
            var nombresDuplicados = gallinasTemp.GroupBy(g => g.Nombre.Trim())
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (nombresDuplicados.Any())
            {
                MessageBox.Show($"Hay nombres duplicados: {string.Join(", ", nombresDuplicados)}\n\nPor favor, asegúrate de que todos los nombres sean únicos.",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Convertir gallinas temporales a gallinas reales
                // IMPORTANTE: Crea un registro individual por cada gallina
                foreach (var gallinaTemp in gallinasTemp)
                {
                    var gallina = new Gallina
                    {
                        Id = siguienteId++,
                        Nombre = gallinaTemp.Nombre.Trim(),
                        Estado = gallinaTemp.Estado,
                        FechaIngreso = DateTime.Now,
                        Peso = gallinaTemp.Peso,
                        GalponId = galpon.Id
                    };

                    Gallinas.Add(gallina);
                }

                MessageBox.Show($"Se agregarán {Gallinas.Count} gallinas al {galpon.Nombre}.",
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar las gallinas: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NumeroEntero_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Permitir solo números enteros
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }

    // Clase temporal para el binding en el formulario de agregar gallinas
    public class GallinaTemp : INotifyPropertyChanged
    {
        private int numero;
        private string nombre;
        private string estado;
        private double? peso;

        public int Numero
        {
            get => numero;
            set
            {
                numero = value;
                OnPropertyChanged(nameof(Numero));
            }
        }

        public string Nombre
        {
            get => nombre;
            set
            {
                nombre = value;
                OnPropertyChanged(nameof(Nombre));
            }
        }

        public string Estado
        {
            get => estado;
            set
            {
                estado = value;
                OnPropertyChanged(nameof(Estado));
            }
        }

        public double? Peso
        {
            get => peso;
            set
            {
                peso = value;
                OnPropertyChanged(nameof(Peso));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
