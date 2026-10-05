namespace PijacaUDzepu.API.Models.DTO.Output;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
}
