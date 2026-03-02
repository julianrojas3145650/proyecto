using System;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Proyecto_senavicola.data
{
    public class DatabaseHelper
    {
        // Ruta de la base de datos en la carpeta de la aplicación
        private static string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SenavicoDB.db");
        private static string connectionString = $"Data Source={dbPath};Version=3;Foreign Keys=True;";

        public static void InicializarBaseDatos()
        {
            try
            {
                if (!File.Exists(dbPath))
                {
                    SQLiteConnection.CreateFile(dbPath);
                    System.Diagnostics.Debug.WriteLine($"✅ Base de datos creada en: {dbPath}");
                }

                CrearTablas();

                // Solo insertar datos iniciales si no existen usuarios
                if (!ExistenUsuarios())
                {
                    InsertarDatosIniciales();
                    System.Diagnostics.Debug.WriteLine("✅ Datos iniciales insertados");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al inicializar BD: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private static bool ExistenUsuarios()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM Usuarios";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando usuarios: {ex.Message}");
                return false;
            }
        }

        private static void CrearTablas()
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                // Habilitar foreign keys
                using (var cmd = new SQLiteCommand("PRAGMA foreign_keys = ON", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // Tabla de Usuarios
                string createUsuarios = @"
                    CREATE TABLE IF NOT EXISTS Usuarios (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Documento TEXT UNIQUE NOT NULL,
                        Nombre TEXT NOT NULL,
                        Apellido TEXT NOT NULL,
                        Email TEXT,
                        Rol TEXT NOT NULL CHECK(Rol IN ('Administrador', 'Aprendiz', 'Visitante')),
                        Password TEXT NOT NULL,
                        FechaCreacion DATETIME DEFAULT CURRENT_TIMESTAMP,
                        UltimoAcceso DATETIME,
                        Activo INTEGER DEFAULT 1
                    )";

                // Tabla de Galpones
                string createGalpones = @"
                    CREATE TABLE IF NOT EXISTS Galpones (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Codigo TEXT UNIQUE NOT NULL,
                        Nombre TEXT NOT NULL,
                        Longitud REAL NOT NULL,
                        Raza TEXT NOT NULL,
                        RacionPorAve REAL NOT NULL,
                        CantidadAves INTEGER DEFAULT 0,
                        FechaCreacion DATETIME DEFAULT CURRENT_TIMESTAMP,
                        Activo INTEGER DEFAULT 1
                    )";

                // Tabla de Lotes de Gallinas
                string createLotesGallinas = @"
                    CREATE TABLE IF NOT EXISTS LotesGallinas (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        CodigoLote TEXT UNIQUE NOT NULL,
                        CantidadTotal INTEGER NOT NULL,
                        CantidadAsignada INTEGER DEFAULT 0,
                        CantidadPendiente INTEGER NOT NULL,
                        Raza TEXT NOT NULL,
                        EdadSemanas INTEGER,
                        FechaLlegada DATETIME NOT NULL,
                        Proveedor TEXT,
                        Observaciones TEXT,
                        Estado TEXT DEFAULT 'Pendiente' CHECK(Estado IN ('Pendiente', 'Parcial', 'Completo'))
                    )";

                // Tabla de Asignación de Gallinas a Galpones
                string createAsignacionGallinas = @"
                    CREATE TABLE IF NOT EXISTS AsignacionGallinas (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        LoteId INTEGER NOT NULL,
                        GalponId INTEGER NOT NULL,
                        Cantidad INTEGER NOT NULL,
                        FechaAsignacion DATETIME DEFAULT CURRENT_TIMESTAMP,
                        UsuarioId INTEGER,
                        FOREIGN KEY(LoteId) REFERENCES LotesGallinas(Id) ON DELETE CASCADE,
                        FOREIGN KEY(GalponId) REFERENCES Galpones(Id) ON DELETE CASCADE,
                        FOREIGN KEY(UsuarioId) REFERENCES Usuarios(Id) ON DELETE SET NULL
                    )";

                // Tabla de Alimentación
                string createAlimentacion = @"
                    CREATE TABLE IF NOT EXISTS Alimentacion (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        GalponId INTEGER NOT NULL,
                        Fecha DATE NOT NULL,
                        Turno TEXT NOT NULL CHECK(Turno IN ('Mañana', 'Tarde')),
                        CantidadKg REAL NOT NULL,
                        Realizado INTEGER DEFAULT 0,
                        HoraRealizado DATETIME,
                        UsuarioId INTEGER,
                        Observaciones TEXT,
                        FOREIGN KEY(GalponId) REFERENCES Galpones(Id) ON DELETE CASCADE,
                        FOREIGN KEY(UsuarioId) REFERENCES Usuarios(Id) ON DELETE SET NULL
                    )";

                // ⭐ TABLA DE HUEVOS - CORREGIDA Y MEJORADA ⭐
                string createHuevos = @"
                    CREATE TABLE IF NOT EXISTS Huevos (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        LoteHuevos TEXT NOT NULL,
                        GalponId INTEGER,
                        Tipo TEXT NOT NULL CHECK(Tipo IN ('Jumbo', 'AAA', 'AA', 'A', 'B', 'C')),
                        Cantidad INTEGER NOT NULL CHECK(Cantidad >= 0),
                        PesoPromedio REAL NOT NULL CHECK(PesoPromedio > 0),
                        FechaRecoleccion DATETIME DEFAULT CURRENT_TIMESTAMP,
                        MetodoClasificacion TEXT DEFAULT 'Manual' CHECK(MetodoClasificacion IN ('Manual', 'Automatico')),
                        Estado TEXT DEFAULT 'Disponible' CHECK(Estado IN ('Disponible', 'Vendido', 'Dañado')),
                        UsuarioId INTEGER,
                        FOREIGN KEY(GalponId) REFERENCES Galpones(Id) ON DELETE SET NULL,
                        FOREIGN KEY(UsuarioId) REFERENCES Usuarios(Id) ON DELETE SET NULL
                    )";

                // Tabla de Ventas de Huevos
                string createVentasHuevos = @"
                    CREATE TABLE IF NOT EXISTS VentasHuevos (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        HuevoId INTEGER NOT NULL,
                        Cliente TEXT NOT NULL,
                        Cantidad INTEGER NOT NULL CHECK(Cantidad > 0),
                        PrecioUnitario REAL NOT NULL CHECK(PrecioUnitario >= 0),
                        Total REAL NOT NULL CHECK(Total >= 0),
                        FechaVenta DATETIME DEFAULT CURRENT_TIMESTAMP,
                        UsuarioId INTEGER,
                        Observaciones TEXT,
                        FOREIGN KEY(HuevoId) REFERENCES Huevos(Id) ON DELETE CASCADE,
                        FOREIGN KEY(UsuarioId) REFERENCES Usuarios(Id) ON DELETE SET NULL
                    )";

                // Tabla de Huevos Dañados
                string createHuevosDanados = @"
                    CREATE TABLE IF NOT EXISTS HuevosDanados (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        HuevoId INTEGER NOT NULL,
                        Cantidad INTEGER NOT NULL CHECK(Cantidad > 0),
                        Motivo TEXT NOT NULL,
                        FechaRegistro DATETIME DEFAULT CURRENT_TIMESTAMP,
                        Observaciones TEXT,
                        UsuarioId INTEGER,
                        FOREIGN KEY(HuevoId) REFERENCES Huevos(Id) ON DELETE CASCADE,
                        FOREIGN KEY(UsuarioId) REFERENCES Usuarios(Id) ON DELETE SET NULL
                    )";

                // Tabla de Insumos
                string createInsumos = @"
                    CREATE TABLE IF NOT EXISTS Insumos (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Nombre TEXT NOT NULL,
                        Tipo TEXT NOT NULL CHECK(Tipo IN ('Alimento', 'Herramienta', 'Medicamento', 'Utensilio', 'Otro')),
                        Cantidad REAL NOT NULL CHECK(Cantidad >= 0),
                        Unidad TEXT NOT NULL,
                        StockMinimo REAL DEFAULT 0,
                        FechaIngreso DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FechaVencimiento DATETIME,
                        Proveedor TEXT,
                        PrecioUnitario REAL,
                        Responsable TEXT,
                        Observaciones TEXT
                    )";

                // Tabla de Movimientos de Insumos
                string createMovimientosInsumos = @"
                    CREATE TABLE IF NOT EXISTS MovimientosInsumos (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        InsumoId INTEGER NOT NULL,
                        TipoMovimiento TEXT NOT NULL CHECK(TipoMovimiento IN ('Entrada', 'Salida', 'Ajuste')),
                        Cantidad REAL NOT NULL,
                        FechaMovimiento DATETIME DEFAULT CURRENT_TIMESTAMP,
                        Motivo TEXT,
                        UsuarioId INTEGER,
                        FOREIGN KEY(InsumoId) REFERENCES Insumos(Id) ON DELETE CASCADE,
                        FOREIGN KEY(UsuarioId) REFERENCES Usuarios(Id) ON DELETE SET NULL
                    )";

                // Tabla de Historial General
                string createHistorial = @"
                    CREATE TABLE IF NOT EXISTS Historial (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Modulo TEXT NOT NULL,
                        Accion TEXT NOT NULL,
                        Detalle TEXT,
                        UsuarioId INTEGER,
                        FechaHora DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY(UsuarioId) REFERENCES Usuarios(Id) ON DELETE SET NULL
                    )";

                using (var cmd = new SQLiteCommand(conn))
                {
                    try
                    {
                        cmd.CommandText = createUsuarios; cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("✅ Tabla Usuarios creada");

                        cmd.CommandText = createGalpones; cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("✅ Tabla Galpones creada");

                        cmd.CommandText = createLotesGallinas; cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("✅ Tabla LotesGallinas creada");

                        cmd.CommandText = createAsignacionGallinas; cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("✅ Tabla AsignacionGallinas creada");

                        cmd.CommandText = createAlimentacion; cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("✅ Tabla Alimentacion creada");

                        cmd.CommandText = createHuevos; cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("✅ Tabla Huevos creada");

                        cmd.CommandText = createVentasHuevos; cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("✅ Tabla VentasHuevos creada");

                        cmd.CommandText = createHuevosDanados; cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("✅ Tabla HuevosDanados creada");

                        cmd.CommandText = createInsumos; cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("✅ Tabla Insumos creada");

                        cmd.CommandText = createMovimientosInsumos; cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("✅ Tabla MovimientosInsumos creada");

                        cmd.CommandText = createHistorial; cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("✅ Tabla Historial creada");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Error creando tablas: {ex.Message}");
                        throw;
                    }
                }
            }
        }

        private static void InsertarDatosIniciales()
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                // Insertar 3 usuarios predefinidos
                string insertUsuarios = @"
                    INSERT INTO Usuarios (Documento, Nombre, Apellido, Email, Rol, Password) VALUES
                    ('1014202985', 'Administrador', 'Sistema', 'admin@senavicola.com', 'Administrador', @pass1),
                    ('aprendiz', 'Aprendiz', 'SENA', 'aprendiz@senavicola.com', 'Aprendiz', @pass2),
                    ('visitante', 'Visitante', 'General', 'visitante@senavicola.com', 'Visitante', @pass3)";

                using (var cmd = new SQLiteCommand(insertUsuarios, conn))
                {
                    cmd.Parameters.AddWithValue("@pass1", HashPassword("administrador123"));
                    cmd.Parameters.AddWithValue("@pass2", HashPassword("aprendiz123"));
                    cmd.Parameters.AddWithValue("@pass3", HashPassword("visitante123"));
                    cmd.ExecuteNonQuery();
                }

                System.Diagnostics.Debug.WriteLine("✅ Usuarios iniciales creados:");
                System.Diagnostics.Debug.WriteLine("   • 1014202985 / administrador123 (Administrador)");
                System.Diagnostics.Debug.WriteLine("   • visitante / visitante123 (Visitante)");
            }
        }

        public static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static SQLiteConnection GetConnection()
        {
            var connection = new SQLiteConnection(connectionString);
            connection.Open();
            // Habilitar foreign keys para esta conexión
            using (var cmd = new SQLiteCommand("PRAGMA foreign_keys = ON", connection))
            {
                cmd.ExecuteNonQuery();
            }
            connection.Close();
            return connection;
        }

        public static void RegistrarHistorial(string modulo, string accion, string detalle, int? usuarioId = null)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = "INSERT INTO Historial (Modulo, Accion, Detalle, UsuarioId) VALUES (@modulo, @accion, @detalle, @usuario)";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@modulo", modulo);
                        cmd.Parameters.AddWithValue("@accion", accion);
                        cmd.Parameters.AddWithValue("@detalle", detalle ?? "");
                        cmd.Parameters.AddWithValue("@usuario", usuarioId.HasValue ? (object)usuarioId.Value : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al registrar historial: {ex.Message}");
            }
        }

        // Método para obtener la ruta de la base de datos (útil para backups)
        public static string ObtenerRutaBaseDatos()
        {
            return dbPath;
        }
    }
}