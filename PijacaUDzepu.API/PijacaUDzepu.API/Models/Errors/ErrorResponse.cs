namespace PijacaUDzepu.API.Models.Errors;

public class ErrorResponse
{
    public int Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}
