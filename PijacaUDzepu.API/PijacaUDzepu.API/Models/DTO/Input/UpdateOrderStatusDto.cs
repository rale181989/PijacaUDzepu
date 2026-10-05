using System.ComponentModel.DataAnnotations;
using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class UpdateOrderStatusDto
{
    [Required]
    public OrderStatus Status { get; set; }
}
