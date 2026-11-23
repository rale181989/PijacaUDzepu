using System.ComponentModel.DataAnnotations;

namespace PijacaUDzepu.API.Models.DTO.Input;

// Tačka tezge koju prodavac postavi kada je na tezgi ("Ovde sam") ili pomeri na mapi
public class StallLocationDto
{
    [Range(-90, 90, ErrorMessage = "Lokacija nije ispravna.")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Lokacija nije ispravna.")]
    public double Longitude { get; set; }
}
