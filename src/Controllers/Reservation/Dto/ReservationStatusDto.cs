using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class ReservationStatusDto
{
    public string Status { get; set; } = string.Empty;
    public bool? IsFullPayment { get; set; } // Opcional: true si es pago completo, false si es parcial
    public decimal? PaymentAmount { get; set; } // Opcional: monto del pago (solo si IsFullPayment = false)

    // Campos para agregar al PaymentHistory cuando se confirma un pago
    public DateTime? PaymentDate { get; set; } // Fecha del pago
    public PaymentMethod? PaymentMethod { get; set; } // Método de pago
    public string? BankName { get; set; } // Nombre del banco (si aplica)
    public string? PaymentReference { get; set; } // Referencia del pago
    public string? PaymentNotes { get; set; } // Notas del pago
}
