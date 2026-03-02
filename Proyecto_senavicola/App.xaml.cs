using System.Windows;
using Proyecto_senavicola.data;

namespace Proyecto_senavicola
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Inicializar base de datos al iniciar la aplicación
            try
            {
                DatabaseHelper.InicializarBaseDatos();
                System.Diagnostics.Debug.WriteLine("✅ Base de datos inicializada correctamente");
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error al inicializar la base de datos:\n{ex.Message}",
                    "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}