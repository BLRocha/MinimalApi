namespace MinimalApi.Models
{
    public class Fornecedor
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Documento { get; set; }
        public bool Ativo { get; set; }

        public bool Validate() {
            return !String.IsNullOrWhiteSpace(Name) &&
                !String.IsNullOrWhiteSpace(Documento);
        }
    }
}
