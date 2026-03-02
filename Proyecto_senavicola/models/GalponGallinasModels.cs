using System;
using System.Collections.Generic;
using System.Linq;

namespace Proyecto_senavicola.models
{
    /// <summary>
    /// Modelo que representa un Galpón en la granja avícola
    /// </summary>
    public class Galpon
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Raza { get; set; }
        public double Longitud { get; set; }
        public double RacionPorAve { get; set; }
        public DateTime FechaCreacion { get; set; }
        public List<Gallina> Gallinas { get; set; }

        public Galpon()
        {
            Gallinas = new List<Gallina>();
            FechaCreacion = DateTime.Now;
            RacionPorAve = 120; // Valor por defecto en gramos
            Nombre = string.Empty;
            Raza = string.Empty;
        }

        // Propiedades calculadas
        public int TotalGallinas => Gallinas?.Count ?? 0;

        public int GallinasActivas => Gallinas?.Count(g => g.Estado == "Activa") ?? 0;

        public double PesoPromedio
        {
            get
            {
                if (Gallinas == null || Gallinas.Count == 0) return 0;
                var gallinasConPeso = Gallinas.Where(g => g.Peso.HasValue).ToList();
                if (gallinasConPeso.Count == 0) return 0;

                return gallinasConPeso.Average(g => g.Peso.Value);
            }
        }

        public double ConsumoTotalDiario => TotalGallinas * RacionPorAve;

        public string InfoGalpon => $"{Nombre} - {Raza} ({TotalGallinas} gallinas)";
    }

    /// <summary>
    /// Modelo que representa una Gallina individual
    /// </summary>
    public class Gallina
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Estado { get; set; }
        public DateTime FechaIngreso { get; set; }
        public double? Peso { get; set; }
        public int GalponId { get; set; }
        public DateTime? FechaBaja { get; set; }
        public string MotivoEgreso { get; set; }

        public Gallina()
        {
            FechaIngreso = DateTime.Now;
            Estado = "Activa";
            Nombre = string.Empty;
        }

        // Propiedad calculada: días en la granja
        public int DiasEnGranja
        {
            get
            {
                DateTime fechaFin = FechaBaja ?? DateTime.Now;
                return (fechaFin - FechaIngreso).Days;
            }
        }

        // Propiedad para mostrar el peso formateado
        public string PesoDisplay => Peso.HasValue ? $"{Peso.Value:F1} g" : "Sin registrar";

        // Propiedad para mostrar el estado con un icono
        public string EstadoDisplay
        {
            get
            {
                return Estado switch
                {
                    "Activa" => "✓ Activa",
                    "En observación" => "⚠ En observación",
                    "Enferma" => "🏥 Enferma",
                    "Vendida" => "💰 Vendida",
                    "Fallecida" => "✝ Fallecida",
                    _ => Estado
                };
            }
        }
    }
}