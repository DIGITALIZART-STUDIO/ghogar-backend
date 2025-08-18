using System;
using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class SendOtpRequestDto
{
    [Required]
    public Guid UserId { get; set; }
}

public class SendOtpResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
}

public class VerifyOtpRequestDto
{
    [Required]
    [StringLength(6, MinimumLength = 6)]
    public required string OtpCode { get; set; }
}

public class VerifyOtpResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
