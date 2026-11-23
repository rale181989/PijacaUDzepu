using System.ComponentModel.DataAnnotations;

namespace PijacaUDzepu.API.Models.DTO.Input;

// Tačka tezge koju prodavac postavi kada je na tezgi ("Ovde sam") ili pomeri na mapi.
// Obe koordinate su obavezne: prazan zahtev bi se inače sačuvao kao 0,0 ("tezga usred mora").
public class StallLocationDto
{
    [Required(ErrorMessage = "Lokacija nije ispravna.")]
    [Range(-90, 90, ErrorMessage = "Lokacija nije ispravna.")]
    public double? Latitude { get; set; }

    [Required(ErrorMessage = "Lokacija nije ispravna.")]
    [Range(-180, 180, ErrorMessage = "Lokacija nije ispravna.")]
    public double? Longitude { get; set; }
}
