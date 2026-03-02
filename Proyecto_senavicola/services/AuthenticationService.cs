using System;
using System.Data.SQLite;
using System.Text.RegularExpressions;
using Proyecto_senavicola.data;

namespace Proyecto_senavicola.services
{
    public class AuthenticationService
    {
        public static Usuario UsuarioActual { get; private set; }
        public static bool EsModoInvitado { get; private set; }

        /// <summary>
        /// ✅ VALIDA QUE EL DOCUMENTO SOLO CONTENGA NÚMEROS
        /// </summary>
        public static bool ValidarDocumentoNumerico(string documento)
        {
            if (string.IsNullOrWhiteSpace(documento))
                return false;

            // Solo números permitidos (sin espacios, letras, ni caracteres especiales)
            return Regex.IsMatch(documento, @"^\d+$");
        }

        /// <summary>
        /// Inicia sesión con documento y contraseña (NUEVO)
        /// </summary>
        public static bool IniciarSesionPorDocumento(string documento, string password)
        {
            try
            {
                // ✅ VALIDAR QUE EL DOCUMENTO SEA NUMÉRICO
                if (!ValidarDocumentoNumerico(documento))
                {
                    return false; // El documento debe ser solo números
                }

                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT * FROM Usuarios WHERE Documento = @documento AND Activo = 1";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@documento", documento);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string passwordHash = reader.GetString(reader.GetOrdinal("Password"));
                                string inputHash = DatabaseHelper.HashPassword(password);

                                // Verificar contraseña
                                if (passwordHash == inputHash)
                                {
                                    UsuarioActual = new Usuario
                                    {
                                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                        Documento = reader.GetString(reader.GetOrdinal("Documento")),
                                        Nombre = reader.GetString(reader.GetOrdinal("Nombre")),
                                        Apellido = reader.GetString(reader.GetOrdinal("Apellido")),
                                        Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? "" : reader.GetString(reader.GetOrdinal("Email")),
                                        Rol = reader.GetString(reader.GetOrdinal("Rol")),
                                        FechaCreacion = reader.GetDateTime(reader.GetOrdinal("FechaCreacion")),
                                        Activo = reader.GetInt32(reader.GetOrdinal("Activo")) == 1
                                    };

                                    EsModoInvitado = false;

                                    // Actualizar último acceso
                                    ActualizarUltimoAcceso(UsuarioActual.Id);

                                    // Registrar en historial
                                    DatabaseHelper.RegistrarHistorial(
                                        "Sistema",
                                        "Inicio de Sesión",
                                        $"{UsuarioActual.NombreCompleto} - Rol: {UsuarioActual.Rol}",
                                        UsuarioActual.Id
                                    );

                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al iniciar sesión: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Inicia sesión como invitado (modo solo lectura) - NUEVO
        /// </summary>
        public static void IniciarSesionComoInvitado()
        {
            UsuarioActual = new Usuario
            {
                Id = 0,
                Documento = "invitado",
                Nombre = "Invitado",
                Apellido = "Sistema",
                Email = "invitado@senavicola.com",
                Rol = "Invitado",
                FechaCreacion = DateTime.Now,
                Activo = true
            };

            EsModoInvitado = true;

            // Registrar en historial
            DatabaseHelper.RegistrarHistorial(
                "Sistema",
                "Acceso como invitado",
                "Usuario ingresó en modo solo lectura",
                null
            );
        }

        /// <summary>
        /// Inicia sesión por rol (MANTENER PARA COMPATIBILIDAD)
        /// </summary>
        public static bool IniciarSesionPorRol(string rol, string password)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();

                    string sql = "SELECT * FROM Usuarios WHERE Rol = @rol AND Activo = 1";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@rol", rol);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string passwordHash = reader.GetString(reader.GetOrdinal("Password"));

                                // Verificar contraseña
                                if (DatabaseHelper.HashPassword(password) == passwordHash)
                                {
                                    UsuarioActual = new Usuario
                                    {
                                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                        Documento = reader.GetString(reader.GetOrdinal("Documento")),
                                        Nombre = reader.GetString(reader.GetOrdinal("Nombre")),
                                        Apellido = reader.GetString(reader.GetOrdinal("Apellido")),
                                        Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? "" : reader.GetString(reader.GetOrdinal("Email")),
                                        Rol = reader.GetString(reader.GetOrdinal("Rol")),
                                        FechaCreacion = reader.GetDateTime(reader.GetOrdinal("FechaCreacion")),
                                        Activo = reader.GetInt32(reader.GetOrdinal("Activo")) == 1
                                    };

                                    EsModoInvitado = false;

                                    // Actualizar último acceso
                                    ActualizarUltimoAcceso(UsuarioActual.Id);

                                    // Registrar en historial
                                    DatabaseHelper.RegistrarHistorial("Sistema", "Inicio de Sesión",
                                        $"{UsuarioActual.NombreCompleto} - Rol: {UsuarioActual.Rol}",
                                        UsuarioActual.Id);

                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al iniciar sesión: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ✅ CAMBIA CONTRASEÑA DE CUALQUIER USUARIO (SOLO ADMIN)
        /// </summary>
        public static bool CambiarPasswordAdmin(string documento, string nuevaPassword)
        {
            try
            {
                // ✅ VALIDAR QUE EL DOCUMENTO SEA NUMÉRICO
                if (!ValidarDocumentoNumerico(documento))
                {
                    return false;
                }

                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();

                    string sql = "UPDATE Usuarios SET Password = @password WHERE Documento = @documento";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@password", DatabaseHelper.HashPassword(nuevaPassword));
                        cmd.Parameters.AddWithValue("@documento", documento);

                        int filasAfectadas = cmd.ExecuteNonQuery();

                        if (filasAfectadas > 0)
                        {
                            DatabaseHelper.RegistrarHistorial(
                                "Usuarios",
                                "Cambiar Password (Admin)",
                                $"Contraseña actualizada para usuario {documento}",
                                UsuarioActual?.Id
                            );

                            return true;
                        }

                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cambiar contraseña (admin): {ex.Message}");
                return false;
            }
        }

        public static bool CambiarPassword(string passwordActual, string nuevaPassword)
        {
            if (UsuarioActual == null || EsModoInvitado)
                return false;

            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();

                    // Verificar que la contraseña actual sea correcta
                    string sqlVerificar = "SELECT Password FROM Usuarios WHERE Id = @id";
                    using (var cmdVerificar = new SQLiteCommand(sqlVerificar, conn))
                    {
                        cmdVerificar.Parameters.AddWithValue("@id", UsuarioActual.Id);
                        string passwordHashActual = cmdVerificar.ExecuteScalar()?.ToString();

                        if (passwordHashActual != DatabaseHelper.HashPassword(passwordActual))
                        {
                            return false; // Contraseña actual incorrecta
                        }
                    }

                    // Actualizar con la nueva contraseña
                    string sql = "UPDATE Usuarios SET Password = @password WHERE Id = @id";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@password", DatabaseHelper.HashPassword(nuevaPassword));
                        cmd.Parameters.AddWithValue("@id", UsuarioActual.Id);

                        int filasAfectadas = cmd.ExecuteNonQuery();

                        if (filasAfectadas > 0)
                        {
                            DatabaseHelper.RegistrarHistorial(
                                "Perfil",
                                "Cambiar Contraseña",
                                $"Usuario {UsuarioActual.Documento} cambió su contraseña",
                                UsuarioActual.Id
                            );

                            return true;
                        }

                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cambiar contraseña: {ex.Message}");
                return false;
            }
        }

        private static void ActualizarUltimoAcceso(int usuarioId)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "UPDATE Usuarios SET UltimoAcceso = @fecha WHERE Id = @id";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                        cmd.Parameters.AddWithValue("@id", usuarioId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al actualizar último acceso: {ex.Message}");
            }
        }

        public static void CerrarSesion()
        {
            if (UsuarioActual != null && !EsModoInvitado)
            {
                DatabaseHelper.RegistrarHistorial("Sistema", "Cierre de Sesión",
                    $"{UsuarioActual.NombreCompleto} cerró sesión",
                    UsuarioActual.Id);
            }

            UsuarioActual = null;
            EsModoInvitado = false;
        }

        // MÉTODOS DE PERMISOS ACTUALIZADOS

        /// <summary>
        /// Verifica si puede modificar (crear/editar) registros
        /// </summary>
        public static bool PuedeModificar()
        {
            if (UsuarioActual == null || EsModoInvitado) return false;
            return UsuarioActual.Rol == "Administrador" || UsuarioActual.Rol == "Aprendiz";
        }

        /// <summary>
        /// Verifica si puede eliminar registros
        /// </summary>
        public static bool PuedeEliminar()
        {
            if (UsuarioActual == null || EsModoInvitado) return false;
            return UsuarioActual.Rol == "Administrador";
        }

        /// <summary>
        /// Verifica si puede acceder a reportes
        /// </summary>
        public static bool PuedeVerReportes()
        {
            return UsuarioActual != null; // Todos incluido invitado pueden ver reportes
        }

        /// <summary>
        /// Verifica si puede gestionar usuarios
        /// </summary>
        public static bool PuedeGestionarUsuarios()
        {
            if (UsuarioActual == null || EsModoInvitado) return false;
            return UsuarioActual.Rol == "Administrador";
        }

        /// <summary>
        /// Verifica si puede cambiar contraseñas
        /// </summary>
        public static bool PuedeCambiarContraseñas()
        {
            if (UsuarioActual == null || EsModoInvitado) return false;
            return UsuarioActual.Rol == "Administrador";
        }

        // Verificar permisos según rol (MÉTODO ORIGINAL MANTENIDO)
        public static bool TienePermiso(string accion)
        {
            if (UsuarioActual == null) return false;
            if (EsModoInvitado) return accion == "Visualizar";

            return accion switch
            {
                "Visualizar" => true, // Todos pueden visualizar
                "Gestionar" => UsuarioActual.Rol == "Administrador" || UsuarioActual.Rol == "Aprendiz",
                "Eliminar" => UsuarioActual.Rol == "Administrador",
                "Administrar" => UsuarioActual.Rol == "Administrador",
                "CambiarContraseña" => UsuarioActual.Rol == "Administrador",
                _ => false
            };
        }

        public static string ObtenerDescripcionPermiso(string accion)
        {
            return accion switch
            {
                "Visualizar" => "Ver información del sistema",
                "Gestionar" => "Crear y editar registros",
                "Eliminar" => "Eliminar registros permanentemente",
                "Administrar" => "Gestionar usuarios y configuraciones críticas",
                "CambiarContraseña" => "Cambiar contraseñas de cualquier usuario",
                _ => "Acción no definida"
            };
        }

        public static bool CambiarContraseña(string rolDestino, string nuevaContraseña)
        {
            if (!TienePermiso("CambiarContraseña"))
            {
                return false;
            }

            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();

                    string sql = "UPDATE Usuarios SET Password = @password WHERE Rol = @rol";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@password", DatabaseHelper.HashPassword(nuevaContraseña));
                        cmd.Parameters.AddWithValue("@rol", rolDestino);

                        int filasAfectadas = cmd.ExecuteNonQuery();

                        if (filasAfectadas > 0)
                        {
                            DatabaseHelper.RegistrarHistorial("Configuración", "Cambio de Contraseña",
                                $"Contraseña del rol {rolDestino} actualizada",
                                UsuarioActual?.Id);

                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cambiar contraseña: {ex.Message}");
                return false;
            }
        }
    }

    public class Usuario
    {
        public int Id { get; set; }
        public string Documento { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Email { get; set; }
        public string Rol { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? UltimoAcceso { get; set; }
        public bool Activo { get; set; }

        public string NombreCompleto => $"{Nombre} {Apellido}";
    }
}
