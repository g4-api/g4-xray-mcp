namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Represents a custom field with a name and associated value.
    /// </summary>
    public class CustomFieldModel
    {
        /// <summary>
        /// Gets or sets the name of the custom field.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value assigned to the custom field.
        /// </summary>
        public object Value { get; set; }
    }
}
