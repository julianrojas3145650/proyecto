using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Proyecto_senavicola.services;
using Proyecto_senavicola.view.dialogs;
using Proyecto_senavicola.view.pages;

namespace Proyecto_senavicola.view.window
{
    public partial class SeleccionCamaraDialog : Window
    {
        public SeleccionCamaraDialog()
        {
            InitializeComponent();
            MostrarPaginaInicio();
            ConfigurarPermisos();
            ActualizarInformacionUsuario();
        }

        private void MostrarPaginaInicio()
        {
            MainFrame.Navigate(new InicioPage());
        }

        private void ConfigurarPermisos()
        {
            if (AuthenticationService.UsuarioActual == null)
                return;

            string rol = AuthenticationService.UsuarioActual.Rol;

            // INVITADO: Solo puede ver reportes y datos (sin modificar nada)
            if (AuthenticationService.EsModoInvitado)
            {

            }

            // VISITANTE: Solo puede ver, no puede editar ni crear
            else if (rol == "Visitante")
            {
                // Los visitantes tienen acceso a todas las páginas pero solo lectura
                // La restricción de acciones se maneja en cada página
            }

            // APRENDIZ: Puede gestionar pero no administrar usuarios ni cambiar contraseñas
            else if (rol == "Aprendiz")
            {
                // Puede acceder a todas las funcionalidades excepto gestión de usuarios
            }

            // ADMINISTRADOR: Acceso completo
            else if (rol == "Administrador")
            {
                // Acceso completo a todas las funciones
            }
        }

        private void ActualizarInformacionUsuario()
        {
            if (AuthenticationService.UsuarioActual != null)
            {
                if (AuthenticationService.EsModoInvitado)
                {
                    this.Title = "Senavícola - Modo Invitado (Solo Lectura)";
                }
                else
                {
                    this.Title = $"Senavícola - {AuthenticationService.UsuarioActual.NombreCompleto} ({AuthenticationService.UsuarioActual.Rol})";
                }
            }
        }

        #region Eventos de Navegación

        private void BtnInicio_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new InicioPage());
        }

        private void BtnGallinas_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new GallinasPage());
        }


        private void BtnHuevos_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new HuevosPage());
        }

        private void BtnInsumos_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new InsumosPage());
        }

        private void BtnReportes_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ReportesPage());
        }

        private void BtnConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ConfiguracionPage());
        }

        #endregion

        #region Controles de Ventana

        private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void BtnMinimizar_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximizar_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            var resultado = MessageBox.Show(
                "¿Estás seguro de que deseas salir de la aplicación?",
                "Confirmar Salida",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resultado == MessageBoxResult.Yes)
            {
                AuthenticationService.CerrarSesion();
                Application.Current.Shutdown();
            }
        }

        #endregion

        #region Sesión

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            var cerrar_Sesion = new Cerrar_sesion();
            cerrar_Sesion.Show();
        }

        #endregion

        #region Frame Events

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // Actualizar UI cuando cambie la página
        }

        #endregion
    }
}