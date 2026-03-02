using System;

namespace Proyecto_senavicola.view.pages
{
    public enum TipoInsumo
    {
        Alimento = 0,
        Herramienta = 1,
        Medicamento = 2,
        Utensilio = 3,
        Otro = 4
    }

    public class Insumo
    {
        public int Id { get; set; }
        public TipoInsumo Tipo { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public double Cantidad { get; set; }
        public string Unidad { get; set; }
        public double CantidadMinima { get; set; }
        public DateTime FechaIngreso { get; set; }
        public string Responsable { get; set; }
    }
}